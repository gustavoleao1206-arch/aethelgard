using UnityEngine;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Inventory;
using JogoDaTrupe.Core.Timing;

namespace JogoDaTrupe.Gameplay.Automation
{
    /// <summary>
    /// Coletor Automático: a máquina que "trabalha por você". Posicionada perto de um
    /// nó de recurso (<see cref="ResourceNode"/>), colhe sozinha em ciclos e deposita
    /// o que colheu no inventário do dono — sem o jogador precisar interagir.
    ///
    /// Princípios respeitados:
    /// • <b>Simulação ≠ Apresentação:</b> aqui só há lógica de jogo; o ritmo dos ciclos
    ///   é calculado por <see cref="AutomationMath"/> (puro e testado).
    /// • <b>DRY:</b> a colheita reusa a API do nó (<c>Available</c>/<c>Take</c>) — a
    ///   mesma usada pela coleta manual do jogador.
    /// • <b>Multiplayer-ready (M-NET-7 P6):</b> só a AUTORIDADE simula (gate
    ///   <see cref="SimulationAuthority"/> — a réplica do cliente fica inerte; o estoque
    ///   do nó chega por NodeStockChanged e os itens pelo NetworkInventory). O depósito
    ///   vai para o inventário do DONO (<see cref="Ownable.OwnerId"/>): se <c>_output</c>
    ///   não veio serializado (cena de co-op não tem player authored), é resolvido em
    ///   runtime — o coletor authored tem dono 0, então deposita no host (interino
    ///   honesto até coletores construíveis terem dono natural).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Ownable))]
    public sealed class AutoCollector : MonoBehaviour
    {
        [SerializeField] private AutomationDefinition _definition;

        [Tooltip("Inventário que recebe o que for coletado. Se vazio (cena de co-op), " +
                 "é resolvido em runtime pelo dono (Ownable.OwnerId).")]
        [SerializeField] private PlayerInventory _output;

        private Ownable _ownable;
        private ResourceNode _target;
        private float _accumulated;
        private float _nextOutputSearch; // re-busca com intervalo (padrão FindPlayer da IA)

        // Trava de segurança: nunca processa um número absurdo de ciclos num único
        // frame (ex.: após uma pausa longa do Editor). O estoque do nó também limita.
        private const int MaxCyclesPerFrame = 8;

        /// <summary>Verdadeiro quando tem um nó válido para colher.</summary>
        public bool IsWorking => _target != null && !_target.IsDepleted;

        private void Awake() => _ownable = GetComponent<Ownable>();

        private void Update()
        {
            // Só a autoridade simula a automação (M-NET-7 P6 — o gate que ficou de fora
            // no M-NET-6 P0). Sem isto, o coletor do CLIENTE colheria a cópia local do nó
            // em paralelo (dessinc). A réplica fica parada: estoque e itens chegam
            // replicados do servidor.
            if (!SimulationAuthority.IsAuthority) return;

            if (_definition == null) return;
            if (_output == null && !TryResolveOutput()) return;

            // Garante um alvo válido (reaquire se o atual esgotou ou sumiu).
            if (_target == null || _target.IsDepleted) AcquireTarget();
            if (_target == null) { _accumulated = 0f; return; } // ocioso: nada por perto

            _accumulated += GameTime.DeltaTime;
            int cycles = AutomationMath.AdvanceCycles(_accumulated, _definition.cycleSeconds, out _accumulated);
            if (cycles <= 0) return;
            if (cycles > MaxCyclesPerFrame) cycles = MaxCyclesPerFrame;

            for (int i = 0; i < cycles; i++)
            {
                if (_target == null || _target.IsDepleted)
                {
                    AcquireTarget();
                    if (_target == null) break; // não há mais o que colher
                }
                HarvestOnce();
            }
        }

        // Colhe uma porção do alvo e deposita no inventário, respeitando o que o nó
        // ainda tem e o espaço livre do inventário.
        private void HarvestOnce()
        {
            int want = Mathf.Max(1, _definition.amountPerCycle);
            int give = _target.Available(want);
            if (give <= 0) { _target = null; return; }

            int leftover = _output.Add(_target.Item, give); // publica os eventos do inventário
            int accepted = give - leftover;                 // inventário pode estar cheio
            _target.Take(accepted);                         // só remove do nó o que entrou
        }

        // Resolve o inventário do DONO em runtime (M-NET-7 P6): a cena de co-op não tem
        // player authored para o gerador ligar (_output fica vazio) — e o player de rede
        // nasce DEPOIS do Awake (lição do Bug #9), então a busca re-tenta com intervalo
        // até o dono existir. Casa pelo Ownable.OwnerId (o coletor authored tem dono 0 →
        // deposita no host). Camada Gameplay pura: nada de NGO aqui.
        private bool TryResolveOutput()
        {
            if (GameTime.Time < _nextOutputSearch) return false;
            _nextOutputSearch = GameTime.Time + 0.5f;

            int owner = _ownable != null ? _ownable.OwnerId : Ownable.LocalPlayerId;
            PlayerInventory[] inventories = Object.FindObjectsByType<PlayerInventory>();
            for (int i = 0; i < inventories.Length; i++)
            {
                PlayerInventory inventory = inventories[i];
                if (inventory != null && inventory.TryGetComponent(out Ownable o) && o.OwnerId == owner)
                {
                    _output = inventory;
                    return true;
                }
            }
            return false;
        }

        // Acha o nó de recurso NÃO esgotado mais próximo dentro do raio de busca.
        // Chamado só ao adquirir/reataquirir alvo (não todo frame), então o custo
        // do FindObjects é aceitável no slice.
        private void AcquireTarget()
        {
            _target = null;
            float radius = Mathf.Max(0f, _definition.searchRadius);
            float best = float.MaxValue;

            // Overload sem parâmetro: a versão com FindObjectsSortMode foi deprecada na Unity 6.5.
            ResourceNode[] nodes = Object.FindObjectsByType<ResourceNode>();
            for (int i = 0; i < nodes.Length; i++)
            {
                ResourceNode node = nodes[i];
                if (node == null || node.IsDepleted || node.Item == null) continue;

                float dist = Vector3.Distance(transform.position, node.transform.position);
                if (dist <= radius && dist < best)
                {
                    best = dist;
                    _target = node;
                }
            }
        }
    }
}

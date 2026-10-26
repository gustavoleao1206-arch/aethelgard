using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Identity;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Combat;
using JogoDaTrupe.Gameplay.Enemies;

namespace JogoDaTrupe.Gameplay.Capture
{
    /// <summary>
    /// Criatura que pode ser CAPTURADA em vez de morta. O jogador a enfraquece em
    /// combate; ao chegar no limiar de vida ela "desmaia" enfraquecida (para de
    /// lutar) e fica sincronizável. Segurar E (via <see cref="Synchronizer"/>)
    /// completa a captura.
    ///
    /// Capturar publica um <see cref="EcoCollected"/> — a MESMA porta de entrada das
    /// criaturas derrotadas — então a habilidade (Cipó) entra na Memória e pode ser
    /// incorporada/equipada no Altar, reusando todo o sistema de Incorporação.
    ///
    /// A vida nunca chega a zero: o <see cref="Health"/> recebe um piso igual ao
    /// limiar de captura (a criatura não morre, só fica capturável).
    ///
    /// MUNDO COMPARTILHADO (M-NET-7 P7): a captura é ARBITRADA PELO SERVIDOR —
    /// 🎨 decisão do Diretor (02/07/2026): "quem completa a sincronização primeiro
    /// leva" (a criatura é de UM jogador só).
    /// • Sem autoridade (cliente), <see cref="Synchronize"/> não captura local: publica
    ///   a intenção <see cref="SyncRequested"/> (o forwarder a leva ao servidor).
    /// • Na autoridade, o 1º pedido válido captura (<c>_captured</c> bloqueia os
    ///   seguintes) e o corpo some das duas telas (despawn do prefab de rede).
    /// • A réplica fica "pronta" pelo espelho (<see cref="SyncSynchronizable"/>, via
    ///   NetworkCapture) — o limiar de vida só é avaliado na autoridade.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class CapturableCreature : MonoBehaviour, ISynchronizable
    {
        [SerializeField] private EnemyDefinition _definition;

        [Tooltip("Fração da vida máxima abaixo da qual a criatura pode ser sincronizada (0.3 = 30%).")]
        [Range(0.05f, 0.9f)]
        [SerializeField] private float _captureFraction = 0.3f;

        private Health _health;
        private EnemyController _combat; // opcional: se a criatura luta, acalma ao ficar sincronizável
        private EntityGuid _guid;        // id compartilhado (baked no prefab — igual nas 2 telas)
        private bool _synchronizable;
        private bool _captured;

        public bool CanSynchronize => _synchronizable && !_captured;
        public string Label => _definition != null ? _definition.displayName : "Criatura";

        /// <summary>Id estável da criatura (casa o pedido de captura pela rede).</summary>
        public string Guid => _guid != null ? _guid.Value : string.Empty;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _combat = GetComponent<EnemyController>();
            _guid = GetComponent<EntityGuid>();
        }

        private void Start()
        {
            // Piso de vida = limiar de captura: a criatura nunca morre; ao ser
            // enfraquecida até o limiar, fica disponível para sincronizar.
            _health.SetMinimum(CaptureMath.ThresholdHp(_health.Max, _captureFraction));
        }

        private void Update()
        {
            // Só a AUTORIDADE avalia o limiar (fonte única): a réplica fica pronta pelo
            // espelho (SyncSynchronizable), nunca pela própria conta da vida espelhada.
            if (!SimulationAuthority.IsAuthority) return;

            if (_captured || _synchronizable) return; // já resolvido / já pronta
            if (CaptureMath.IsSynchronizable(_health.Current, _health.Max, _captureFraction))
            {
                BecomeSynchronizable();
            }
        }

        // Enfraquecida: para de lutar (se for o caso) e fica pronta para sincronizar.
        private void BecomeSynchronizable()
        {
            _synchronizable = true;
            if (_combat != null) _combat.enabled = false; // acalma (não persegue/ataca)
            Tintar(new Color(0.5f, 0.9f, 0.45f)); // verde claro: "pronta para sincronizar"
        }

        /// <summary>
        /// ESPELHO da réplica (M-NET-7 P7): o NetworkCapture chama quando o servidor
        /// anuncia que a criatura ficou sincronizável — a réplica tinge de verde e vira
        /// alvo válido do canal local (CanSynchronize), sem avaliar vida nenhuma.
        /// </summary>
        public void SyncSynchronizable()
        {
            if (_synchronizable) return;
            BecomeSynchronizable();
        }

        // Chamado pelo Synchronizer quando o canal de sincronização completa.
        public void Synchronize(int ownerId)
        {
            if (!CanSynchronize) return;

            // SEM autoridade (cliente): não captura local — publica a INTENÇÃO. O
            // servidor arbitra (WorldRequestExecutor): o 1º pedido válido leva (🎨
            // decisão do Diretor); o corpo some pelas 2 telas via despawn.
            if (!SimulationAuthority.IsAuthority)
            {
                EventBus<SyncRequested>.Publish(new SyncRequested { CreatureGuid = Guid, OwnerId = ownerId });
                return;
            }

            _captured = true;

            // Concede o Eco (mesma porta das criaturas derrotadas) → entra na Memória.
            if (_definition != null && _definition.grantedAbility != null)
            {
                EventBus<EcoCollected>.Publish(new EcoCollected { OwnerId = ownerId, Source = _definition });
            }

            // Feedback de captura (a UI escuta para avisar).
            EventBus<CreatureCaptured>.Publish(new CreatureCaptured { OwnerId = ownerId, Source = _definition });

            // Sincronizada: some do mundo (sem arte ainda — só sai de cena).
            Destroy(gameObject, 0.15f);
        }

        private void Tintar(Color cor)
        {
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null) r.material.color = cor;
        }
    }
}

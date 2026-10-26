using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Identity;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.Saving;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Interaction;

namespace JogoDaTrupe.Gameplay.Inventory
{
    /// <summary>
    /// Nó de recurso no mundo (árvore, pedra...). Interagir (E) coleta uma porção
    /// para o inventário de QUEM interagiu, até esgotar.
    ///
    /// MUNDO COMPARTILHADO (M-NET-7 P3): o estoque é AUTORITATIVO no servidor.
    /// • Numa máquina SEM autoridade (cliente), <see cref="Interact"/> não colhe local:
    ///   publica a intenção <see cref="HarvestRequested"/> (o nó é identificado pelo
    ///   <see cref="EntityGuid"/>, idêntico nas 2 telas porque a cena é o mesmo asset).
    /// • Na autoridade (solo/host), colhe de verdade e publica <see cref="NodeStockChanged"/>,
    ///   que o adaptador relaya; a réplica do cliente espelha o estoque (<see cref="SyncStock"/>).
    /// A "reserva de nó" sai de graça: o servidor processa um pedido por vez.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(EntityGuid))]
    public sealed class ResourceNode : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField] private int _amountPerHarvest = 3;
        [SerializeField] private int _totalAmount = 9;

        private ResourceStock _stock;
        private Renderer _renderer;
        private EntityGuid _guid;

        private void Awake()
        {
            _stock = new ResourceStock(_totalAmount);
            _renderer = GetComponentInChildren<Renderer>();
            _guid = GetComponent<EntityGuid>();
            _guid.EnsureId();
            WorldNodeIndex.Register(_guid.Value, this);
        }

        private void OnEnable() => EventBus<NodeStockChanged>.Subscribe(OnNodeStockChanged);
        private void OnDisable() => EventBus<NodeStockChanged>.Unsubscribe(OnNodeStockChanged);
        private void OnDestroy() => WorldNodeIndex.Unregister(_guid != null ? _guid.Value : null);

        public string Prompt =>
            IsDepleted ? "Esgotado" : (_item != null ? $"Coletar {_item.displayName}" : "Coletar");

        /// <summary>O recurso que este nó entrega (Definição).</summary>
        public ItemDefinition Item => _item;

        /// <summary>Id estável do nó (igual nas 2 máquinas — casa o pedido de coleta pela rede).</summary>
        public string Guid => _guid != null ? _guid.Value : string.Empty;

        /// <summary>Verdadeiro quando o nó já foi todo colhido.</summary>
        public bool IsDepleted => _stock == null || _stock.IsDepleted;

        /// <summary>Quanto ainda resta para colher (autoritativo no servidor; espelhado no cliente).</summary>
        public int Remaining => _stock != null ? _stock.Remaining : 0;

        /// <summary>
        /// Quanto desta colheita pode sair AGORA, limitado pelo que ainda resta.
        /// Não remove nada — serve para "ver" antes de depositar (a coleta manual e o
        /// Coletor Automático usam isto para só remover o que couber no inventário).
        /// </summary>
        public int Available(int want) => _stock != null ? _stock.Available(want) : 0;

        /// <summary>
        /// Remove de fato <paramref name="amount"/> do nó (use depois de depositar o
        /// que foi aceito no inventário). Esgota o nó se chegar a zero.
        ///
        /// PONTO ÚNICO do <see cref="NodeStockChanged"/> (M-NET-7 P6): toda mutação
        /// autoritativa do estoque passa por aqui — coleta manual (via Interact) E o
        /// Coletor Automático (que usa Available/Take direto). Publicar só no Interact
        /// deixaria a colheita do coletor invisível para as réplicas.
        /// </summary>
        public void Take(int amount)
        {
            if (_stock == null || amount <= 0) return; // nada mudou → nada a anunciar
            bool tinhaEstoque = !_stock.IsDepleted;
            _stock.Take(amount);
            if (tinhaEstoque && _stock.IsDepleted) Deplete(); // só na transição → escurece uma vez

            // Anuncia o novo estoque às réplicas (relayado pelo EventScopeAdapter).
            // Em solo é publicado e ninguém relaya — inofensivo (o próprio nó ignora,
            // pois com autoridade o espelho não roda).
            if (SimulationAuthority.IsAuthority)
                EventBus<NodeStockChanged>.Publish(new NodeStockChanged { NodeGuid = Guid, Remaining = Remaining });
        }

        public void Interact(GameObject interactor)
        {
            if (IsDepleted || _item == null || interactor == null) return;

            // SEM autoridade (cliente): não colhe local — publica a INTENÇÃO. O servidor
            // colhe de verdade (WorldRequestExecutor) e o estoque volta por NodeStockChanged.
            if (!SimulationAuthority.IsAuthority)
            {
                int ownerId = interactor.TryGetComponent(out Ownable ownable) ? ownable.OwnerId : Ownable.LocalPlayerId;
                EventBus<HarvestRequested>.Publish(new HarvestRequested { NodeGuid = Guid, OwnerId = ownerId });
                return;
            }

            // Autoridade (solo/host, ou executor server-side chamando com o player do dono):
            // colhe de verdade — o Take anuncia o novo estoque às réplicas (ponto único).
            if (!interactor.TryGetComponent(out PlayerInventory inventory)) return;

            int give = Available(_amountPerHarvest);
            int leftover = inventory.Add(_item, give);
            Take(give - leftover); // só remove o que realmente entrou no inventário
        }

        // Réplica do cliente: aplica o estoque autoritativo recebido do servidor.
        private void OnNodeStockChanged(NodeStockChanged e)
        {
            if (SimulationAuthority.IsAuthority) return; // no host o nó É a fonte
            if (e.NodeGuid != Guid) return;              // só o MEU nó
            SyncStock(e.Remaining);
        }

        // Espelha o estoque e escurece se esgotou (sem tocar o inventário — o item já
        // entrou no servidor e chega pela replicação do NetworkInventory).
        private void SyncStock(int remaining)
        {
            if (_stock == null) return;
            bool tinhaEstoque = !_stock.IsDepleted;
            _stock.SyncTo(remaining);
            if (tinhaEstoque && _stock.IsDepleted) Deplete();
            // Diagnóstico do co-op (raro: só quando o estoque replicado muda no cliente).
            Debug.Log($"[Rede] Estoque replicado de {name}: {remaining}.");
        }

        // Esgotado: escurece para sinalizar (o estoque já está zerado no ResourceStock).
        private void Deplete()
        {
            if (_renderer != null) _renderer.material.color = new Color(0.3f, 0.3f, 0.3f);
        }

        // ---- Save (ISaveable) — estado de MUNDO (M-NET-9 P3) ----
        // O estoque do nó entra no save (fecha metade da dívida de "estado de mundo"). A
        // chave é por GUID (não por jogador) — o mundo pertence ao host. Vale no SOLO
        // também (o Save é gateado por autoridade, true no solo).

        public string SaveKey => "node:" + Guid;

        [System.Serializable]
        private struct NodeSave { public int remaining; }

        public object CaptureState() => new NodeSave { remaining = Remaining };

        public void RestoreState(object state)
        {
            if (_stock == null) return;
            var d = JsonUtility.FromJson<NodeSave>((string)state);

            bool tinhaEstoque = !_stock.IsDepleted;
            _stock.SyncTo(d.remaining);
            if (tinhaEstoque && _stock.IsDepleted) Deplete();

            // Re-anuncia às réplicas (relayado): o Load é host-only (autoridade), então
            // os clientes espelham o estoque restaurado pelo NodeStockChanged.
            if (SimulationAuthority.IsAuthority)
                EventBus<NodeStockChanged>.Publish(new NodeStockChanged { NodeGuid = Guid, Remaining = Remaining });
        }
    }
}

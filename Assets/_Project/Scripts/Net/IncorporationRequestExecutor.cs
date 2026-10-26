using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Incorporation;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// EXECUTOR server-side dos pedidos de INCORPORAÇÃO (M-NET-8 P3) — irmão do
    /// <see cref="WorldRequestExecutor"/> no prefab <c>P_NetSystems</c> (arquivo
    /// separado: responsabilidade única). SÓ o servidor age: assina as 5 intenções e
    /// executa a operação REAL nos componentes do player do dono — reusando os mesmos
    /// métodos do solo (nenhuma lógica duplicada).
    ///
    /// A VALIDAÇÃO DE ALTAR é por PROXIMIDADE REAL no servidor (<see cref="AltarMath"/>),
    /// nunca pelo bool do cliente (plano-mãe M-NET-8). O bool <c>AtAltar</c> do loadout
    /// da réplica é UX local do dono — o executor o iça temporariamente só para passar
    /// pelas regras puras (LoadoutMath) DEPOIS de validar a posição de verdade.
    ///
    /// O custo do respec (🎨 2 Pedra) é debitado pelo próprio <c>TryRespec</c> no
    /// inventário AUTORITATIVO (replicado pelo NetworkInventory). Invest/Respec não
    /// publicam evento — o executor chama <see cref="NetworkLoadout.Rewrite"/> para
    /// replicar o resultado.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class IncorporationRequestExecutor : NetworkBehaviour
    {
        private Altar _altar; // há UM Altar, authored no Hub (cena é o mesmo asset)

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            _altar = FindAnyObjectByType<Altar>();
            if (_altar == null)
                Debug.LogWarning("[Rede] IncorporationRequestExecutor sem Altar na cena — pedidos de loadout serão negados.");

            EventBus<EquipRequested>.Subscribe(OnEquipRequested);
            EventBus<UnequipRequested>.Subscribe(OnUnequipRequested);
            EventBus<InvestNodeRequested>.Subscribe(OnInvestNodeRequested);
            EventBus<RespecRequested>.Subscribe(OnRespecRequested);
            EventBus<InvestAttributeRequested>.Subscribe(OnInvestAttributeRequested);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            EventBus<EquipRequested>.Unsubscribe(OnEquipRequested);
            EventBus<UnequipRequested>.Unsubscribe(OnUnequipRequested);
            EventBus<InvestNodeRequested>.Unsubscribe(OnInvestNodeRequested);
            EventBus<RespecRequested>.Unsubscribe(OnRespecRequested);
            EventBus<InvestAttributeRequested>.Unsubscribe(OnInvestAttributeRequested);
        }

        // ── Handlers ────────────────────────────────────────────────────────────────────

        private void OnEquipRequested(EquipRequested e)
        {
            GameObject player = ResolvePlayer(e.OwnerId);
            if (player == null || !ValidateAltar(player, e.OwnerId, "equipar")) return;

            var loadout = player.GetComponent<AbilityLoadout>();
            var memory = player.GetComponent<MemoryBank>();
            if (loadout == null || memory == null) return;

            // O servidor é o juiz: o Eco tem que estar INCORPORÁVEL na memória
            // AUTORITATIVA (não confiamos na visão do cliente, que é espelho).
            MemoryBank.Eco eco = FindIncorporableEco(memory, e.AbilityId);
            if (eco == null)
            {
                Debug.Log($"[Rede] Equip de '{e.AbilityId}' negado: Eco não incorporável na memória do dono {e.OwnerId}.");
                return;
            }

            bool ok = WithAltarLifted(loadout, () =>
                loadout.TryEquip(new AbilityInstance(eco.Ability, eco.HighestLayer), out string reason)
                    ? (true, (string)null) : (false, reason));
            Debug.Log($"[Rede] Equip de '{e.AbilityId}' pelo dono {e.OwnerId}: {(ok ? "OK" : "negado")}.");
            // AbilityEquipped já dispara no TryEquip → NetworkLoadout reescreve sozinho.
        }

        private void OnUnequipRequested(UnequipRequested e)
        {
            GameObject player = ResolvePlayer(e.OwnerId);
            if (player == null || !ValidateAltar(player, e.OwnerId, "desequipar")) return;

            var loadout = player.GetComponent<AbilityLoadout>();
            if (loadout == null) return;

            bool ok = WithAltarLifted(loadout, () => (loadout.Unequip(e.Slot), (string)null));
            Debug.Log($"[Rede] Unequip do slot {e.Slot} pelo dono {e.OwnerId}: {(ok ? "OK" : "negado")}.");
        }

        private void OnInvestNodeRequested(InvestNodeRequested e)
        {
            GameObject player = ResolvePlayer(e.OwnerId);
            if (player == null || !ValidateAltar(player, e.OwnerId, "investir nó")) return;

            var loadout = player.GetComponent<AbilityLoadout>();
            if (loadout == null) return;

            bool ok = loadout.TryInvestNode(e.Slot, e.NodeIndex, out string reason);
            Debug.Log($"[Rede] InvestNode (slot {e.Slot}, nó {e.NodeIndex}) pelo dono {e.OwnerId}: {(ok ? "OK" : reason)}.");
            if (ok) player.GetComponent<NetworkLoadout>()?.Rewrite(); // invest não publica evento
        }

        private void OnRespecRequested(RespecRequested e)
        {
            GameObject player = ResolvePlayer(e.OwnerId);
            if (player == null || !ValidateAltar(player, e.OwnerId, "respecar")) return;

            var loadout = player.GetComponent<AbilityLoadout>();
            if (loadout == null) return;

            // TryRespec valida e DEBITA o custo (🎨 2 Pedra) no inventário autoritativo.
            (bool ok, string reason) = WithAltarLiftedR(loadout, () =>
                loadout.TryRespec(e.Slot, out string r) ? (true, (string)null) : (false, r));
            Debug.Log($"[Rede] Respec do slot {e.Slot} pelo dono {e.OwnerId}: {(ok ? "OK (custo debitado)" : reason)}.");
            if (ok) player.GetComponent<NetworkLoadout>()?.Rewrite(); // respec não publica evento
        }

        private void OnInvestAttributeRequested(InvestAttributeRequested e)
        {
            // SEM exigência de Altar (paridade com o solo — a ficha abre em qualquer lugar).
            GameObject player = ResolvePlayer(e.OwnerId);
            if (player == null) return;

            var attributes = player.GetComponent<PlayerAttributes>();
            if (attributes == null) return;

            bool ok = attributes.TryInvest((AttributeType)e.Attribute);
            Debug.Log($"[Rede] InvestAttribute ({e.Attribute}) pelo dono {e.OwnerId}: {(ok ? "OK" : "sem pontos")}.");
            // AttributesChanged dispara no TryInvest → NetworkAttributes reescreve sozinho.
        }

        // ── Validações / helpers ────────────────────────────────────────────────────────

        // Proximidade REAL do Altar, medida no servidor (a segurança de verdade).
        private bool ValidateAltar(GameObject player, int ownerId, string operation)
        {
            if (_altar == null) return false;
            bool near = AltarMath.IsNear(player.transform.position, _altar.transform.position, AltarMath.DefaultRange);
            if (!near)
                Debug.Log($"[Rede] Pedido de {operation} NEGADO: dono {ownerId} longe do Altar (validação do servidor).");
            return near;
        }

        // O bool AtAltar é UX local do dono (a réplica do servidor não o tem ligado).
        // Depois de validar a POSIÇÃO de verdade, içamos o bool só para as regras puras
        // (LoadoutMath.CanEquip / Unequip / TryRespec) não recusarem por ele.
        private static bool WithAltarLifted(AbilityLoadout loadout, System.Func<(bool ok, string reason)> action)
            => WithAltarLiftedR(loadout, action).ok;

        private static (bool ok, string reason) WithAltarLiftedR(AbilityLoadout loadout, System.Func<(bool ok, string reason)> action)
        {
            bool previous = loadout.AtAltar;
            loadout.AtAltar = true;
            try { return action(); }
            finally { loadout.AtAltar = previous; }
        }

        // O pedido chega por abilityId; a memória é indexada por família — varre os
        // incorporáveis (poucos) procurando a habilidade pedida.
        private static MemoryBank.Eco FindIncorporableEco(MemoryBank memory, string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return null;
            foreach (MemoryBank.Eco eco in memory.IncorporableEcos)
            {
                if (eco.Ability != null && eco.Ability.abilityId == abilityId) return eco;
            }
            return null;
        }

        // Resolve o GameObject do player a partir do ASSENTO do dono (M-NET-9 P2): assento
        // → clientId (SeatRegistry) → PlayerObject (molde do P3 do M-NET-7).
        private static GameObject ResolvePlayer(int ownerId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return null;
            if (!SeatRegistry.TryClientIdOf(ownerId, out ulong clientId)) return null;
            if (!nm.ConnectedClients.TryGetValue(clientId, out NetworkClient client)) return null;
            return client.PlayerObject != null ? client.PlayerObject.gameObject : null;
        }
    }
}

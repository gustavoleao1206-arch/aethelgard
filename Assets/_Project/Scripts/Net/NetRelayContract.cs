using System;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// O CONTRATO do correio de eventos (M-NET-7 P2): quais eventos o
    /// <see cref="EventScopeAdapter"/> relaya por RPC servidor→clientes. Extraído do
    /// adaptador (NetworkBehaviour, não-testável em EditMode) para DADO PURO — os
    /// testes garantem as regras sem precisar de NetworkManager.
    ///
    /// Regras que os testes protegem:
    /// • Tudo aqui é <see cref="NetEventScope.Simulation"/> (intenção nunca é relayada).
    /// • <c>ItemCollected</c>/<c>InventoryChanged</c> estão FORA desde o M-NET-7 P2:
    ///   o inventário viaja por <c>NetworkList</c> (<see cref="NetworkInventory"/>) —
    ///   relayar também entregaria a mudança 2× no cliente (mesmo raciocínio do
    ///   <c>HealthChanged</c> de inimigo no M-NET-6 P3, ver <see cref="NetRelayRules"/>).
    /// • <c>AbilityEquipped</c>/<c>AbilityUnequipped</c>/<c>AbilityLeveledUp</c> saíram
    ///   no M-NET-8 P3 pelo MESMO motivo: o loadout viaja por <c>NetworkList</c>
    ///   (<see cref="NetworkLoadout"/>) e o espelho republica os eventos locais —
    ///   relay + espelho = 2×.
    /// • <c>EcoCollected</c>/<c>CreatureCaptured</c> FICAM: no cliente são APRESENTAÇÃO
    ///   (toast/feedback) — a mutação de estado é gateada por autoridade (M-NET-8 P0).
    /// • <c>HealthChanged</c> segue no relay SÓ para vida de player
    ///   (<see cref="NetRelayRules.ShouldRelayHealthChanged"/> filtra em runtime).
    /// </summary>
    public static class NetRelayContract
    {
        /// <summary>Eventos que o adaptador captura no servidor e relaya por RPC.</summary>
        public static readonly Type[] RelayedByRpc =
        {
            typeof(HealthChanged), typeof(EntityDied),
            typeof(PlayerRespawned), // M-NET-9 P0: o dono se teleporta ao spawn e re-liga o controle
            typeof(EcoCollected),
            typeof(CreatureCaptured),
            typeof(NodeStockChanged), // M-NET-7 P3: estoque do nó (réplica espelha)
            typeof(ItemCrafted)       // M-NET-7 P4: gancho de feedback de craft (SO viaja por recipeId)
        };
    }
}

using System;
using NUnit.Framework;
using JogoDaTrupe.Net;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Testes do CONTRATO do correio de eventos (M-NET-7 P2) — a lista do que o
    /// EventScopeAdapter relaya por RPC, extraída para dado puro justamente para
    /// estas garantias valerem sem NetworkManager.
    /// </summary>
    public class NetRelayContractTests
    {
        // A regra anti-2× do inventário: com o NetworkInventory (NetworkList) como
        // transporte, relayar ItemCollected/InventoryChanged entregaria a mesma
        // mudança DUAS vezes no cliente. Fora do contrato — para sempre.
        [Test]
        public void InventoryEvents_AreNotRelayedByRpc()
        {
            Assert.IsFalse(Array.IndexOf(NetRelayContract.RelayedByRpc, typeof(ItemCollected)) >= 0,
                "ItemCollected viaja por NetworkList (NetworkInventory) — relayar duplicaria no cliente");
            Assert.IsFalse(Array.IndexOf(NetRelayContract.RelayedByRpc, typeof(InventoryChanged)) >= 0,
                "InventoryChanged viaja por NetworkList (NetworkInventory) — relayar duplicaria no cliente");
        }

        // Tudo que o adaptador relaya TEM que ser Simulação (a trava que o adaptador
        // aplica em runtime, garantida aqui em EditMode).
        [Test]
        public void EverythingRelayed_IsSimulation()
        {
            foreach (Type t in NetRelayContract.RelayedByRpc)
            {
                Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf(t),
                    $"{t.Name} está no contrato de relay mas não é Simulation");
            }
        }

        // Nenhuma INTENÇÃO pode entrar no contrato (pedidos fluem cliente→servidor
        // pelos forwarders; relayar de volta seria eco de pedido).
        [Test]
        public void NoIntentEvent_IsEverRelayed()
        {
            foreach (Type t in NetRelayContract.RelayedByRpc)
            {
                Assert.AreNotEqual(NetEventScope.Intent, EventScopeRegistry.ScopeOf(t),
                    $"{t.Name} é uma intenção — não pode ser relayada");
            }
        }

        // M-NET-8 P3: com o NetworkLoadout (NetworkList) como transporte, relayar os
        // eventos de loadout entregaria a mudança DUAS vezes (o espelho já republica).
        [Test]
        public void LoadoutEvents_AreNotRelayedByRpc()
        {
            Assert.IsFalse(Array.IndexOf(NetRelayContract.RelayedByRpc, typeof(AbilityEquipped)) >= 0,
                "AbilityEquipped viaja por NetworkList (NetworkLoadout) — relayar duplicaria no cliente");
            Assert.IsFalse(Array.IndexOf(NetRelayContract.RelayedByRpc, typeof(AbilityUnequipped)) >= 0,
                "AbilityUnequipped viaja por NetworkList (NetworkLoadout) — relayar duplicaria no cliente");
            Assert.IsFalse(Array.IndexOf(NetRelayContract.RelayedByRpc, typeof(AbilityLeveledUp)) >= 0,
                "AbilityLeveledUp viaja por NetworkList (NetworkLoadout) — relayar duplicaria no cliente");
        }

        // Os eventos-fonte de APRESENTAÇÃO continuam relayados (toast/feedback no
        // cliente) — a mutação de estado é gateada por autoridade (M-NET-8 P0).
        [Test]
        public void PresentationSourceEvents_StayRelayed()
        {
            Assert.IsTrue(Array.IndexOf(NetRelayContract.RelayedByRpc, typeof(EcoCollected)) >= 0);
            Assert.IsTrue(Array.IndexOf(NetRelayContract.RelayedByRpc, typeof(CreatureCaptured)) >= 0);
        }

        // M-NET-7: o estoque do nó (P3) e o feedback de craft (P4) entram no contrato.
        [Test]
        public void WorldSimulationEvents_AreRelayed()
        {
            Assert.IsTrue(Array.IndexOf(NetRelayContract.RelayedByRpc, typeof(NodeStockChanged)) >= 0,
                "NodeStockChanged é relayado para as réplicas espelharem o estoque (P3)");
            Assert.IsTrue(Array.IndexOf(NetRelayContract.RelayedByRpc, typeof(ItemCrafted)) >= 0,
                "ItemCrafted é relayado como gancho de feedback (P4)");
        }
    }
}

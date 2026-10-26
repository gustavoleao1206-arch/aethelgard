using NUnit.Framework;
using JogoDaTrupe.Net;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Saving;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Testes do contrato de escopo de eventos (M-NET-4 P1). Provam que a fronteira
    /// simulação × apresentação está classificada certa e que a FALHA SEGURA funciona
    /// (evento não listado = Presentation = não trafega pela rede).
    /// </summary>
    public class EventScopeRegistryTests
    {
        // Os 12 eventos de SIMULAÇÃO devem ser classificados como Simulation (serão
        // replicados pelo adaptador quando a fonte autoritativa existir — M-NET-5/6).
        [Test]
        public void SimulationEvents_AreMappedToSimulation()
        {
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<HealthChanged>());
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<EntityDied>());
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<PlayerAttacked>());
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<EcoCollected>());
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<AbilityEquipped>());
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<AbilityUnequipped>());
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<AbilityLeveledUp>());
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<AbilityUsed>());
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<InventoryChanged>());
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<ItemCollected>());
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<ItemCrafted>());
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<CreatureCaptured>());
        }

        // Eventos de APRESENTAÇÃO (só-tela) NUNCA trafegam: caem no default por não
        // estarem no mapa. Abrir a minha UI não pode abrir a do amigo.
        [Test]
        public void PresentationEvents_DefaultToPresentation()
        {
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<AltarOpened>());
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<AltarClosed>());
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<InventoryToggled>());
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<CraftingToggled>());
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<CharacterSheetToggled>());
        }

        // Eventos DERIVADOS não são relayados (o cliente os recria pelos componentes
        // por-jogador). Ficam fora do mapa de propósito → default Presentation.
        [Test]
        public void DerivedEvents_AreNotRelayed()
        {
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<AffinityChanged>());   // deriva de AbilityUsed
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<MasteryChanged>());    // deriva de EcoCollected
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<AttributesChanged>()); // deriva de EcoCollected
        }

        // Sessão (save/load) fica de fora até a decisão de experiência (§6 do plano):
        // por enquanto default Presentation = "não trafega".
        [Test]
        public void SessionEvents_NotMappedYet()
        {
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<GameSaved>());
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<GameLoaded>());
        }

        // A "regra dos derivados": relaya só a FONTE; o derivado não trafega (senão
        // o cliente contaria em dobro).
        [Test]
        public void SourceIsRelayed_ButItsDerivedIsNot()
        {
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<AbilityUsed>(),
                "AbilityUsed é a fonte");
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<AffinityChanged>(),
                "AffinityChanged deriva de AbilityUsed — recriado no cliente, não relayado");

            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<EcoCollected>(),
                "EcoCollected é a fonte");
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf<MasteryChanged>(),
                "MasteryChanged deriva de EcoCollected — recriado no cliente, não relayado");
        }

        // A versão por Type tem o mesmo default seguro (inclui o caso null).
        [Test]
        public void ScopeOfType_HasSameSafeDefault()
        {
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf(typeof(HealthChanged)));
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf(typeof(InventoryToggled)));
            Assert.AreEqual(NetEventScope.Presentation, EventScopeRegistry.ScopeOf(null));
        }

        // ---- M-NET-7 P1: escopo Intent (pedidos cliente→servidor) ----

        // As 5 intenções são Intent: fluem SÓ cliente→servidor pelos forwarders.
        [Test]
        public void IntentEvents_AreMappedToIntent()
        {
            Assert.AreEqual(NetEventScope.Intent, EventScopeRegistry.ScopeOf<DamageRequested>()); // M-NET-6, formalizado
            Assert.AreEqual(NetEventScope.Intent, EventScopeRegistry.ScopeOf<HarvestRequested>());
            Assert.AreEqual(NetEventScope.Intent, EventScopeRegistry.ScopeOf<CraftRequested>());
            Assert.AreEqual(NetEventScope.Intent, EventScopeRegistry.ScopeOf<BuildRequested>());
            Assert.AreEqual(NetEventScope.Intent, EventScopeRegistry.ScopeOf<SyncRequested>());
        }

        // A garantia central: NENHUMA intenção é classificada como Simulation — a trava
        // do adaptador (que só relaya Simulation) as rejeita se alguém tentar.
        [Test]
        public void IntentEvents_AreNeverSimulation()
        {
            Assert.AreNotEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<DamageRequested>());
            Assert.AreNotEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<HarvestRequested>());
            Assert.AreNotEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<CraftRequested>());
            Assert.AreNotEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<BuildRequested>());
            Assert.AreNotEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<SyncRequested>());
        }

        // O estoque do nó É simulação (relayado 1× servidor→clientes no P3).
        [Test]
        public void NodeStockChanged_IsSimulation()
        {
            Assert.AreEqual(NetEventScope.Simulation, EventScopeRegistry.ScopeOf<NodeStockChanged>());
        }
    }
}

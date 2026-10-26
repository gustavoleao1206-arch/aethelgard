using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Incorporation;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;
using JogoDaTrupe.Gameplay.Inventory;
using JogoDaTrupe.Net;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa os CONTRATOS de rede da Incorporação (M-NET-8 P0/P1): sem autoridade as
    /// mutações viram INTENÇÕES (nada muta local); os espelhos (SyncFrom) aplicam o
    /// estado replicado e republicam os eventos locais; o XP por uso ingere gateado no
    /// loadout (ponto único); o respec cobra o custo (🎨 2 Pedra) atômico.
    /// </summary>
    public class IncorporationNetTests
    {
        private GameObject _go;
        private AbilityDefinition _ability;
        private EnemyDefinition _creature;
        private ItemDefinition _pedra;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("PlayerDeTeste_M8");
            _go.AddComponent<Ownable>(); // dono 0 (default)

            _ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            _ability.abilityId = "hab_teste";
            _ability.familyId = "FamiliaTeste";
            _ability.baseCooldown = 100f; // alto de propósito (teste de preservação de recarga)

            _creature = ScriptableObject.CreateInstance<EnemyDefinition>();
            _creature.grantedAbility = _ability;
            _creature.masteryThresholds = new[] { 1, 3, 6 };

            _pedra = ScriptableObject.CreateInstance<ItemDefinition>();
            _pedra.displayName = "Pedra";
        }

        [TearDown]
        public void Cleanup()
        {
            SimulationAuthority.IsAuthority = true; // seam global — restaurar o default
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_ability);
            Object.DestroyImmediate(_creature);
            Object.DestroyImmediate(_pedra);
        }

        // Em EditMode o lifecycle não roda no AddComponent — invocamos por reflexão.
        private static void Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(target, args);

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        // ---- Escopos (P1) ----

        [Test]
        public void IntencoesDeIncorporacao_SaoIntent_NuncaSimulation()
        {
            Assert.AreEqual(NetEventScope.Intent, EventScopeRegistry.ScopeOf<EquipRequested>());
            Assert.AreEqual(NetEventScope.Intent, EventScopeRegistry.ScopeOf<UnequipRequested>());
            Assert.AreEqual(NetEventScope.Intent, EventScopeRegistry.ScopeOf<InvestNodeRequested>());
            Assert.AreEqual(NetEventScope.Intent, EventScopeRegistry.ScopeOf<RespecRequested>());
            Assert.AreEqual(NetEventScope.Intent, EventScopeRegistry.ScopeOf<InvestAttributeRequested>());
        }

        // ---- MemoryBank ----

        [Test]
        public void MemoryBank_SemAutoridade_NaoIngereEco()
        {
            var bank = _go.AddComponent<MemoryBank>();
            Invoke(bank, "Awake");
            SimulationAuthority.IsAuthority = false;

            Invoke(bank, "OnEcoCollected", new EcoCollected { OwnerId = 0, Source = _creature });

            Assert.IsFalse(bank.TryGetEco("FamiliaTeste", out _),
                "sem autoridade o espelho não se auto-alimenta — o estado chega replicado (senão seria 2×)");
        }

        [Test]
        public void MemoryBank_SyncFrom_ReconstroiEPublicaMastery()
        {
            var bank = _go.AddComponent<MemoryBank>();
            Invoke(bank, "Awake");
            SimulationAuthority.IsAuthority = false; // espelho no cliente

            MasteryChanged? received = null;
            void OnMastery(MasteryChanged e) => received = e;
            EventBus<MasteryChanged>.Subscribe(OnMastery);
            try
            {
                bank.SyncFrom(new List<(string, string, int, int, int, int)>
                {
                    ("FamiliaTeste", "hab_teste", 3, 1, 3, 6)
                });
            }
            finally { EventBus<MasteryChanged>.Unsubscribe(OnMastery); }

            Assert.IsTrue(bank.TryGetEco("FamiliaTeste", out MemoryBank.Eco eco));
            Assert.AreEqual(3, eco.Defeats);
            Assert.IsTrue(eco.AnyLayerUnlocked, "3 derrotas com limiares 1/3/6 liberam camada");
            Assert.IsTrue(received.HasValue, "o espelho republica MasteryChanged para as HUDs");
        }

        // ---- PlayerAttributes ----

        [Test]
        public void Atributos_TryInvest_SemAutoridade_PublicaIntencao_ENaoMuta()
        {
            var attrs = _go.AddComponent<PlayerAttributes>();
            Invoke(attrs, "Awake");
            SimulationAuthority.IsAuthority = false;
            int before = attrs.GetValue((AttributeType)0);

            InvestAttributeRequested? intent = null;
            void OnIntent(InvestAttributeRequested e) => intent = e;
            EventBus<InvestAttributeRequested>.Subscribe(OnIntent);
            try
            {
                bool invested = attrs.TryInvest((AttributeType)0);
                Assert.IsFalse(invested);
            }
            finally { EventBus<InvestAttributeRequested>.Unsubscribe(OnIntent); }

            Assert.IsTrue(intent.HasValue, "sem autoridade o clique vira INTENÇÃO ao servidor");
            Assert.AreEqual(0, intent.Value.Attribute);
            Assert.AreEqual(before, attrs.GetValue((AttributeType)0), "nada muta local");
        }

        [Test]
        public void Atributos_SyncFrom_AplicaEPublica()
        {
            var attrs = _go.AddComponent<PlayerAttributes>();
            Invoke(attrs, "Awake");

            AttributesChanged? received = null;
            void OnChanged(AttributesChanged e) => received = e;
            EventBus<AttributesChanged>.Subscribe(OnChanged);
            try
            {
                attrs.SyncFrom(level: 3, xp: 10, points: 2, values: new[] { 9, 9, 9, 9, 9, 9 });
            }
            finally { EventBus<AttributesChanged>.Unsubscribe(OnChanged); }

            Assert.AreEqual(3, attrs.Level);
            Assert.AreEqual(2, attrs.PointsAvailable);
            Assert.AreEqual(9, attrs.GetValue((AttributeType)0));
            Assert.IsTrue(received.HasValue);
        }

        // ---- AffinityState ----

        [Test]
        public void Afinidade_SemAutoridade_NaoSobeNoUso()
        {
            var affinity = _go.AddComponent<AffinityState>();
            Invoke(affinity, "Awake");
            SimulationAuthority.IsAuthority = false;
            float before = affinity.GetAffinity(_ability.element);

            Invoke(affinity, "OnAbilityUsed", new AbilityUsed { OwnerId = 0, Slot = 0, Ability = _ability });

            Assert.AreEqual(before, affinity.GetAffinity(_ability.element),
                "sem autoridade a afinidade não sobe local — o servidor ingere (via style forwarder) e replica");
        }

        [Test]
        public void Afinidade_SyncFrom_AplicaEPublica()
        {
            var affinity = _go.AddComponent<AffinityState>();
            Invoke(affinity, "Awake");

            int received = 0;
            void OnChanged(AffinityChanged e) => received++;
            EventBus<AffinityChanged>.Subscribe(OnChanged);
            try
            {
                affinity.SyncFrom(new[] { 55f, 30f, 30f, 30f, 30f, 30f });
            }
            finally { EventBus<AffinityChanged>.Unsubscribe(OnChanged); }

            Assert.AreEqual(55f, affinity.GetAffinity((ElementType)0));
            Assert.AreEqual(6, received, "o espelho republica AffinityChanged por elemento (a roda atualiza)");
        }

        // ---- AbilityLoadout ----

        [Test]
        public void Loadout_TryEquip_SemAutoridade_PublicaEquipRequested()
        {
            var loadout = _go.AddComponent<AbilityLoadout>();
            Invoke(loadout, "Awake");
            SimulationAuthority.IsAuthority = false;

            EquipRequested? intent = null;
            void OnIntent(EquipRequested e) => intent = e;
            EventBus<EquipRequested>.Subscribe(OnIntent);
            try
            {
                bool equipped = loadout.TryEquip(new AbilityInstance(_ability, default), out string reason);
                Assert.IsFalse(equipped);
                StringAssert.Contains("servidor", reason);
            }
            finally { EventBus<EquipRequested>.Unsubscribe(OnIntent); }

            Assert.IsTrue(intent.HasValue);
            Assert.AreEqual("hab_teste", intent.Value.AbilityId);
            Assert.IsNull(loadout.GetSlot(0), "nada equipa local — o slot volta replicado");
        }

        [Test]
        public void Loadout_XpPorUso_IngereSoComAutoridade()
        {
            var loadout = _go.AddComponent<AbilityLoadout>();
            Invoke(loadout, "Awake");
            loadout.AtAltar = true;
            Assert.IsTrue(loadout.TryEquip(new AbilityInstance(_ability, default), out _));
            AbilityInstance inst = loadout.GetSlot(0);

            // Com autoridade: o uso dá XP (ponto único movido do AbilityRunner — M-NET-8).
            Invoke(loadout, "OnAbilityUsed", new AbilityUsed { OwnerId = 0, Slot = 0, Ability = _ability });
            Assert.AreEqual(AbilityMath.XpPerUse, inst.Xp);

            // Sem autoridade: o espelho não se auto-alimenta.
            SimulationAuthority.IsAuthority = false;
            Invoke(loadout, "OnAbilityUsed", new AbilityUsed { OwnerId = 0, Slot = 0, Ability = _ability });
            Assert.AreEqual(AbilityMath.XpPerUse, inst.Xp, "sem autoridade o XP não sobe local (viria 2×)");
        }

        [Test]
        public void Loadout_TryRespec_CobraOCusto_Atomico()
        {
            var inventory = _go.AddComponent<PlayerInventory>();
            Invoke(inventory, "Awake");
            var loadout = _go.AddComponent<AbilityLoadout>();
            Invoke(loadout, "Awake");
            loadout.AtAltar = true;
            SetField(loadout, "_respecCost", new[] { new RecipeIngredient { item = _pedra, amount = 2 } });
            Assert.IsTrue(loadout.TryEquip(new AbilityInstance(_ability, default), out _));

            // Sem material: recusa e nada é debitado.
            Assert.IsFalse(loadout.TryRespec(0, out string reason));
            StringAssert.Contains("Falta material", reason);

            // Com material: paga (2 Pedra) e reverte.
            inventory.Add(_pedra, 2);
            Assert.IsTrue(loadout.TryRespec(0, out _));
            Assert.AreEqual(0, inventory.CountOf(_pedra), "🎨 o respec debitou as 2 Pedra");
        }

        [Test]
        public void Loadout_TryRespec_SemAutoridade_PublicaRespecRequested()
        {
            var loadout = _go.AddComponent<AbilityLoadout>();
            Invoke(loadout, "Awake");
            SimulationAuthority.IsAuthority = false;

            RespecRequested? intent = null;
            void OnIntent(RespecRequested e) => intent = e;
            EventBus<RespecRequested>.Subscribe(OnIntent);
            try
            {
                Assert.IsFalse(loadout.TryRespec(2, out _));
            }
            finally { EventBus<RespecRequested>.Unsubscribe(OnIntent); }

            Assert.IsTrue(intent.HasValue);
            Assert.AreEqual(2, intent.Value.Slot);
        }

        // ---- Structs de replicação (M-NET-8 P2 — equality evita re-send/loops do NGO) ----

        [Test]
        public void SnapshotsDeReplicacao_EqualityFunciona()
        {
            var eco1 = new NetworkMemoryBank.EcoEntry { FamilyId = "f", AbilityId = "a", Defeats = 2, T0 = 1, T1 = 3, T2 = 6 };
            var eco2 = new NetworkMemoryBank.EcoEntry { FamilyId = "f", AbilityId = "a", Defeats = 2, T0 = 1, T1 = 3, T2 = 6 };
            Assert.IsTrue(eco1.Equals(eco2));
            eco2.Defeats = 3;
            Assert.IsFalse(eco1.Equals(eco2));

            var at1 = new NetworkAttributes.AttributesSnapshot { Level = 2, Points = 1, V0 = 5 };
            var at2 = new NetworkAttributes.AttributesSnapshot { Level = 2, Points = 1, V0 = 5 };
            Assert.IsTrue(at1.Equals(at2));
            at2.V5 = 9;
            Assert.IsFalse(at1.Equals(at2));
            Assert.AreEqual(new[] { 5, 0, 0, 0, 0, 0 }, at1.Values());

            var af1 = new NetworkAffinity.AffinitySnapshot { A0 = 30f, A3 = 55f };
            var af2 = new NetworkAffinity.AffinitySnapshot { A0 = 30f, A3 = 55f };
            Assert.IsTrue(af1.Equals(af2));
            af2.A3 = 56f;
            Assert.IsFalse(af1.Equals(af2));

            var sl1 = new NetworkLoadout.SlotEntry { Filled = true, AbilityId = "a", Level = 2, Points = 1, N1 = 3 };
            var sl2 = new NetworkLoadout.SlotEntry { Filled = true, AbilityId = "a", Level = 2, Points = 1, N1 = 3 };
            Assert.IsTrue(sl1.Equals(sl2));
            sl2.N1 = 4;
            Assert.IsFalse(sl1.Equals(sl2));
            Assert.AreEqual(new[] { 0, 3, 0, 0 }, sl1.Nodes());
        }

        [Test]
        public void Loadout_SyncFrom_EspelhaProgressao_EPreservaRecargaLocal()
        {
            var loadout = _go.AddComponent<AbilityLoadout>();
            Invoke(loadout, "Awake");
            loadout.AtAltar = true;
            Assert.IsTrue(loadout.TryEquip(new AbilityInstance(_ability, default), out _));
            AbilityInstance before = loadout.GetSlot(0);
            before.MarkUsed(); // recarga de 100s em andamento — LOCAL, não replica
            Assert.IsFalse(before.IsReady);

            SimulationAuthority.IsAuthority = false; // espelho no cliente
            loadout.SyncFrom(new List<(bool, string, int, int, int, int, int[])>
            {
                (true, "hab_teste", 0, 5, 12, 3, new int[0])
            });

            AbilityInstance after = loadout.GetSlot(0);
            Assert.AreSame(before, after, "mesma habilidade → atualiza IN-PLACE (preserva o timestamp de recarga)");
            Assert.AreEqual(5, after.Level, "a progressão veio do servidor");
            Assert.AreEqual(3, after.PointsAvailable);
            Assert.IsFalse(after.IsReady, "a recarga local sobreviveu ao espelho");
        }
    }
}

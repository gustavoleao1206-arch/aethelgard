using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.Timing;
using JogoDaTrupe.Gameplay.Characters;
using JogoDaTrupe.Gameplay.Combat;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa o RESPAWN + fim do zumbi (M-NET-9 P0): Health.Revive gateado por autoridade
    /// e o timing do PlayerRespawner (só a autoridade agenda; revive e anuncia após o
    /// tempo). O tempo é o seam GameTime (falso no teste — princípio multiplayer #8).
    /// </summary>
    public class RespawnTests
    {
        private sealed class FakeTime : ITimeProvider
        {
            public float T;
            public float Time => T;
            public float DeltaTime => 0.016f;
        }

        private ITimeProvider _realProvider;

        [SetUp]
        public void Setup() => _realProvider = GameTime.Provider;

        [TearDown]
        public void Cleanup()
        {
            GameTime.Provider = _realProvider;
            SimulationAuthority.IsAuthority = true;
        }

        private static void Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(target, args);

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        // ---- Health.Revive ----

        [Test]
        public void Revive_ComAutoridade_RestauraVidaCheia()
        {
            var go = new GameObject("Vida");
            go.AddComponent<Ownable>();
            var health = go.AddComponent<Health>();
            Invoke(health, "Awake");
            SimulationAuthority.IsAuthority = true;

            health.TakeDamage(60f, Ownable.NoOwner); // 100 → 40
            Assert.AreEqual(40f, health.Current, 0.01f);

            health.Revive();
            Assert.AreEqual(health.Max, health.Current, 0.01f);
            Assert.IsFalse(health.IsDead);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Revive_SemAutoridade_NaoMuta()
        {
            var go = new GameObject("Vida2");
            go.AddComponent<Ownable>();
            var health = go.AddComponent<Health>();
            Invoke(health, "Awake");

            SimulationAuthority.IsAuthority = true;
            health.TakeDamage(60f, Ownable.NoOwner); // 100 → 40 (com autoridade)

            SimulationAuthority.IsAuthority = false;
            health.Revive(); // sem autoridade: no-op (a vida volta replicada no cliente)
            Assert.AreEqual(40f, health.Current, 0.01f, "sem autoridade o Revive não muta — o cliente é espelho");

            Object.DestroyImmediate(go);
        }

        // ---- PlayerRespawner (timing) ----

        [Test]
        public void Respawner_ComAutoridade_ReviveEAnunciaAposODelay()
        {
            var fake = new FakeTime { T = 0f };
            GameTime.Provider = fake;
            SimulationAuthority.IsAuthority = true;

            var go = new GameObject("Player");
            go.AddComponent<Ownable>(); // dono 0
            var health = go.AddComponent<Health>();
            var respawner = go.AddComponent<PlayerRespawner>();
            Invoke(health, "Awake");
            Invoke(respawner, "Awake");

            health.TakeDamage(999f, Ownable.NoOwner); // morre
            Assert.IsTrue(health.IsDead);

            int respawnedCount = 0;
            void OnResp(PlayerRespawned e) { if (e.OwnerId == 0) respawnedCount++; }
            EventBus<PlayerRespawned>.Subscribe(OnResp);
            try
            {
                Invoke(respawner, "OnEntityDied", new EntityDied { OwnerId = 0 });
                Assert.IsTrue(Field<bool>(respawner, "_pending"), "a autoridade agenda o renascer");

                fake.T = 3f; // antes do delay (5s)
                Invoke(respawner, "Update");
                Assert.AreEqual(0, respawnedCount, "não renasce antes do tempo");
                Assert.IsTrue(health.IsDead);

                fake.T = 6f; // depois do delay
                Invoke(respawner, "Update");
                Assert.AreEqual(1, respawnedCount, "renasce e anuncia PlayerRespawned uma vez");
                Assert.IsFalse(health.IsDead, "revive restaurou a vida");
            }
            finally { EventBus<PlayerRespawned>.Unsubscribe(OnResp); }

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Respawner_SemAutoridade_NaoAgenda()
        {
            var fake = new FakeTime { T = 0f };
            GameTime.Provider = fake;

            var go = new GameObject("PlayerCliente");
            go.AddComponent<Ownable>();
            var health = go.AddComponent<Health>();
            var respawner = go.AddComponent<PlayerRespawner>();
            Invoke(health, "Awake");
            Invoke(respawner, "Awake");

            SimulationAuthority.IsAuthority = false; // cliente
            Invoke(respawner, "OnEntityDied", new EntityDied { OwnerId = 0 });
            Assert.IsFalse(Field<bool>(respawner, "_pending"),
                "o cliente NÃO agenda — o renascer chega pelo PlayerRespawned relayado do servidor");

            Object.DestroyImmediate(go);
        }
    }
}

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Identity;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Capture;
using JogoDaTrupe.Gameplay.Combat;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa o CONTRATO de captura em rede (M-NET-7 P7, 🎨 decisão do Diretor "um leva"):
    /// sem autoridade a criatura publica a INTENÇÃO (SyncRequested) e não captura local;
    /// já capturada, não concede nada (o perdedor da corrida não ganha Eco); o espelho
    /// (SyncSynchronizable) torna a réplica alvo válido do canal local.
    /// </summary>
    public class CapturableCreatureTests
    {
        private GameObject _go;
        private CapturableCreature _creature;
        private EntityGuid _guid;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("CriaturaDeTeste_P7");
            _guid = _go.AddComponent<EntityGuid>();
            _guid.EnsureId();
            _go.AddComponent<Health>();
            _creature = _go.AddComponent<CapturableCreature>();

            // Em EditMode o Awake não roda no AddComponent — invocamos por reflexão
            // (cacheia _health/_guid; _combat fica null, tolerado).
            typeof(CapturableCreature).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_creature, null);
        }

        [TearDown]
        public void Cleanup()
        {
            SimulationAuthority.IsAuthority = true; // seam global — restaurar o default
            Object.DestroyImmediate(_go);
        }

        private void SetFlag(string field, bool value) =>
            typeof(CapturableCreature).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_creature, value);

        [Test]
        public void Synchronize_SemAutoridade_PublicaIntencao_ENaoCapturaLocal()
        {
            SetFlag("_synchronizable", true);       // enfraquecida (espelhada do servidor)
            SimulationAuthority.IsAuthority = false; // cliente

            SyncRequested? intent = null;
            void OnIntent(SyncRequested e) => intent = e;
            EcoCollected? eco = null;
            void OnEco(EcoCollected e) => eco = e;
            EventBus<SyncRequested>.Subscribe(OnIntent);
            EventBus<EcoCollected>.Subscribe(OnEco);
            try
            {
                _creature.Synchronize(ownerId: 1);
            }
            finally
            {
                EventBus<SyncRequested>.Unsubscribe(OnIntent);
                EventBus<EcoCollected>.Unsubscribe(OnEco);
            }

            Assert.IsTrue(intent.HasValue, "sem autoridade, o canal completo deve virar INTENÇÃO ao servidor");
            Assert.AreEqual(_guid.Value, intent.Value.CreatureGuid, "a intenção identifica a criatura pelo guid compartilhado");
            Assert.AreEqual(1, intent.Value.OwnerId);
            Assert.IsFalse(eco.HasValue, "o cliente NÃO concede Eco local — quem concede é o servidor (senão duplicaria)");
            Assert.IsTrue(_creature.CanSynchronize, "a réplica não se marca capturada — o servidor arbitra");
        }

        [Test]
        public void Synchronize_JaCapturada_NaoConcedeNada()
        {
            // A corrida do "um leva": o 1º pedido marcou _captured; o 2º não ganha nada.
            SetFlag("_synchronizable", true);
            SetFlag("_captured", true);

            EcoCollected? eco = null;
            void OnEco(EcoCollected e) => eco = e;
            CreatureCaptured? captured = null;
            void OnCap(CreatureCaptured e) => captured = e;
            EventBus<EcoCollected>.Subscribe(OnEco);
            EventBus<CreatureCaptured>.Subscribe(OnCap);
            try
            {
                _creature.Synchronize(ownerId: 1); // o "perdedor" tenta
            }
            finally
            {
                EventBus<EcoCollected>.Unsubscribe(OnEco);
                EventBus<CreatureCaptured>.Unsubscribe(OnCap);
            }

            Assert.IsFalse(_creature.CanSynchronize);
            Assert.IsFalse(eco.HasValue, "criatura já capturada não concede Eco de novo (um leva)");
            Assert.IsFalse(captured.HasValue);
        }

        [Test]
        public void SyncSynchronizable_ReplicaViraAlvoValido()
        {
            SimulationAuthority.IsAuthority = false; // réplica no cliente
            Assert.IsFalse(_creature.CanSynchronize, "antes do espelho, a réplica não é alvo");

            _creature.SyncSynchronizable(); // NetworkCapture espelhou o anúncio do servidor

            Assert.IsTrue(_creature.CanSynchronize, "após o espelho, o canal local pode mirar a réplica");
        }
    }
}

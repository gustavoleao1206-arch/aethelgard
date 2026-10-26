using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa o Altar MULTIUSUÁRIO (M-NET-8 P4): sessões por dono — o Interact do 2º
    /// jogador NÃO expulsa o 1º (o bug do campo único `_currentUser`), e cada um
    /// fecha só a própria sessão.
    /// </summary>
    public class AltarMultiuserTests
    {
        private GameObject _altarGo;
        private Altar _altar;
        private GameObject _playerA; // dono 0
        private GameObject _playerB; // dono 1

        [SetUp]
        public void Setup()
        {
            _altarGo = new GameObject("AltarDeTeste");
            _altarGo.AddComponent<BoxCollider>();
            _altar = _altarGo.AddComponent<Altar>();

            _playerA = CreatePlayer("A", ownerId: 0);
            _playerB = CreatePlayer("B", ownerId: 1);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_altarGo);
            Object.DestroyImmediate(_playerA);
            Object.DestroyImmediate(_playerB);
        }

        private static GameObject CreatePlayer(string name, int ownerId)
        {
            var go = new GameObject("Player" + name);
            go.AddComponent<Ownable>().SetOwner(ownerId);
            var loadout = go.AddComponent<AbilityLoadout>();
            typeof(AbilityLoadout).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(loadout, null);
            return go;
        }

        [Test]
        public void DoisJogadores_UsamOAltarAoMesmoTempo_SemExpulsao()
        {
            _altar.Interact(_playerA);
            Assert.IsTrue(_altar.HasSession(0), "A abriu a sessão dele");

            _altar.Interact(_playerB); // ANTES: isto fechava a sessão de A (expulsão)
            Assert.IsTrue(_altar.HasSession(0), "a sessão de A SOBREVIVE ao Interact de B");
            Assert.IsTrue(_altar.HasSession(1), "B abriu a própria sessão");
            Assert.IsTrue(_playerA.GetComponent<AbilityLoadout>().AtAltar);
            Assert.IsTrue(_playerB.GetComponent<AbilityLoadout>().AtAltar);
        }

        [Test]
        public void FecharASessao_SoFechaADoInterator()
        {
            _altar.Interact(_playerA);
            _altar.Interact(_playerB);

            _altar.Interact(_playerA); // toggle: fecha SÓ a de A
            Assert.IsFalse(_altar.HasSession(0));
            Assert.IsFalse(_playerA.GetComponent<AbilityLoadout>().AtAltar);
            Assert.IsTrue(_altar.HasSession(1), "a sessão de B continua aberta");
            Assert.IsTrue(_playerB.GetComponent<AbilityLoadout>().AtAltar);
        }

        [Test]
        public void EventosCarregamODonoCerto()
        {
            int openedOwner = -99, closedOwner = -99;
            void OnOpened(AltarOpened e) => openedOwner = e.OwnerId;
            void OnClosed(AltarClosed e) => closedOwner = e.OwnerId;
            EventBus<AltarOpened>.Subscribe(OnOpened);
            EventBus<AltarClosed>.Subscribe(OnClosed);
            try
            {
                _altar.Interact(_playerB);
                Assert.AreEqual(1, openedOwner, "AltarOpened com o dono do interator");

                _altar.Interact(_playerB);
                Assert.AreEqual(1, closedOwner, "AltarClosed com o dono do interator");
            }
            finally
            {
                EventBus<AltarOpened>.Unsubscribe(OnOpened);
                EventBus<AltarClosed>.Unsubscribe(OnClosed);
            }
        }
    }
}

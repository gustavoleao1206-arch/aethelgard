using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.Identity;

namespace JogoDaTrupe.Tests.Core
{
    /// <summary>
    /// Testa a atribuição de id do EntityGuid (M-NET-2): geração sob demanda e a API
    /// pública usada pela rede (AssignIfEmpty não sobrescreve).
    ///
    /// Obs.: no Editor, o componente já recebe um id ao ser adicionado (Reset/OnValidate).
    /// Para testar o caminho "estava vazio" de forma determinística, zeramos o campo
    /// privado <c>_id</c> por reflexão — simulando uma entidade ainda sem id (o estado em
    /// que a rede/host chamaria AssignIfEmpty antes de a entidade ativar).
    /// </summary>
    public class EntityGuidTests
    {
        private GameObject _go;
        private EntityGuid _guid;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("test-entity");
            _guid = _go.AddComponent<EntityGuid>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // Zera o id (campo privado) para simular uma entidade ainda sem id.
        private void ClearId()
        {
            typeof(EntityGuid)
                .GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_guid, null);
        }

        [Test]
        public void Componente_RecebeIdAutomaticamente_NoEditor()
        {
            // Reset/OnValidate geram o id ao adicionar o componente no Editor.
            Assert.IsTrue(_guid.HasId);
        }

        [Test]
        public void EnsureId_GeraQuandoVazio_EReusaDepois()
        {
            ClearId();
            Assert.IsFalse(_guid.HasId);

            string a = _guid.EnsureId();
            Assert.IsFalse(string.IsNullOrEmpty(a));
            Assert.IsTrue(_guid.HasId);

            string b = _guid.EnsureId(); // idempotente: não troca o id já gerado
            Assert.AreEqual(a, b);
        }

        [Test]
        public void AssignIfEmpty_AtribuiQuandoVazio()
        {
            ClearId();
            bool ok = _guid.AssignIfEmpty("abc-123");
            Assert.IsTrue(ok);
            Assert.AreEqual("abc-123", _guid.Value);
        }

        [Test]
        public void AssignIfEmpty_NaoSobrescreveIdExistente()
        {
            ClearId();
            _guid.AssignIfEmpty("primeiro");
            bool ok = _guid.AssignIfEmpty("segundo");
            Assert.IsFalse(ok);
            Assert.AreEqual("primeiro", _guid.Value);
        }

        [Test]
        public void AssignIfEmpty_IgnoraIdVazioOuNulo()
        {
            ClearId();
            Assert.IsFalse(_guid.AssignIfEmpty(""));
            Assert.IsFalse(_guid.AssignIfEmpty(null));
            Assert.IsFalse(_guid.HasId);
        }
    }
}

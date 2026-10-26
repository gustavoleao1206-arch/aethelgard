using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa o CONTRATO de replicação do estoque (M-NET-7 P6): <see cref="ResourceNode.Take"/>
    /// é o PONTO ÚNICO do <see cref="NodeStockChanged"/> — toda mutação autoritativa anuncia
    /// (coleta manual E Coletor Automático). Se esse contrato quebrar, a colheita do coletor
    /// fica invisível para as réplicas do cliente.
    /// </summary>
    public class ResourceNodeTests
    {
        private GameObject _go;
        private ResourceNode _node;
        private NodeStockChanged? _received;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("NóDeTeste_P6");
            _go.AddComponent<BoxCollider>();
            _go.AddComponent<JogoDaTrupe.Core.Identity.EntityGuid>();
            _node = _go.AddComponent<ResourceNode>();

            // Em EditMode o Awake não roda no AddComponent — invocamos por reflexão
            // (cria o ResourceStock com o _totalAmount default = 9 e registra o guid).
            typeof(ResourceNode).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_node, null);

            _received = null;
            EventBus<NodeStockChanged>.Subscribe(OnStock);
        }

        [TearDown]
        public void Cleanup()
        {
            EventBus<NodeStockChanged>.Unsubscribe(OnStock);
            SimulationAuthority.IsAuthority = true; // seam global — restaurar o default
            Object.DestroyImmediate(_go);           // OnDestroy desregistra do WorldNodeIndex
        }

        private void OnStock(NodeStockChanged e) => _received = e;

        // ---- Save do MUNDO: estoque do nó (M-NET-9 P3) ----

        [Test]
        public void Save_RoundTrip_RestauraORestante()
        {
            _node.Take(6); // 9 → 3
            Assert.AreEqual(3, _node.Remaining);

            // O SaveSystem serializa o CaptureState em JSON e devolve no RestoreState.
            string json = UnityEngine.JsonUtility.ToJson(_node.CaptureState());

            // Um nó NOVO (cheio, 9) restaura o json → deve virar 3 (o estoque persistiu).
            var go2 = new GameObject("NóRestaurado");
            go2.AddComponent<BoxCollider>();
            go2.AddComponent<JogoDaTrupe.Core.Identity.EntityGuid>();
            var node2 = go2.AddComponent<ResourceNode>();
            typeof(ResourceNode).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(node2, null);

            node2.RestoreState(json);
            Assert.AreEqual(3, node2.Remaining, "o estoque restaurado do save bate");

            Object.DestroyImmediate(go2);
        }

        [Test]
        public void Save_RestoreState_ComAutoridade_AnunciaAsReplicas()
        {
            string json = UnityEngine.JsonUtility.ToJson(_node.CaptureState()); // 9
            _node.Take(9); // esgota localmente (só p/ ter um valor diferente)
            _received = null;

            SimulationAuthority.IsAuthority = true;
            _node.RestoreState(json); // volta a 9 e anuncia

            Assert.AreEqual(9, _node.Remaining);
            Assert.IsTrue(_received.HasValue, "o host re-anuncia o estoque restaurado às réplicas");
            Assert.AreEqual(9, _received.Value.Remaining);
        }

        [Test]
        public void Take_ComAutoridade_PublicaNodeStockChanged_ComRestante()
        {
            _node.Take(3); // estoque default 9 → resta 6

            Assert.IsTrue(_received.HasValue, "Take na autoridade deve anunciar o novo estoque às réplicas");
            Assert.AreEqual(6, _received.Value.Remaining);
            Assert.AreEqual(_node.Guid, _received.Value.NodeGuid, "o anúncio identifica o nó pelo guid compartilhado");
        }

        [Test]
        public void Take_Zero_NaoPublica()
        {
            _node.Take(0); // nada mudou → nada a anunciar (evita tráfego inútil)
            Assert.IsFalse(_received.HasValue);
            Assert.AreEqual(9, _node.Remaining);
        }

        [Test]
        public void Take_SemAutoridade_NaoPublica()
        {
            // Defensivo: sem autoridade ninguém deveria chamar Take (o espelho usa SyncStock),
            // mas se chamar, NÃO pode anunciar às réplicas como se fosse a fonte.
            SimulationAuthority.IsAuthority = false;
            _node.Take(3);
            Assert.IsFalse(_received.HasValue);
        }
    }
}

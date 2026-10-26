using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa o índice de nós por guid (M-NET-7 P3): é por ele que o executor server-side
    /// acha o nó que um HarvestRequested pediu (o guid é idêntico nas 2 máquinas porque a
    /// cena é o mesmo asset). Ver WorldNodeIndex.
    /// </summary>
    public class WorldNodeIndexTests
    {
        [Test]
        public void Find_GuidNuloOuVazio_RetornaNull()
        {
            Assert.IsNull(WorldNodeIndex.Find(null));
            Assert.IsNull(WorldNodeIndex.Find(""));
        }

        [Test]
        public void Find_GuidNaoRegistrado_RetornaNull()
        {
            Assert.IsNull(WorldNodeIndex.Find("guid-que-nunca-foi-registrado"));
        }

        [Test]
        public void Register_DepoisFind_DevolveONo_EUnregisterORemove()
        {
            var go = new GameObject("NóDeTeste");
            go.AddComponent<BoxCollider>();
            go.AddComponent<JogoDaTrupe.Core.Identity.EntityGuid>();
            var node = go.AddComponent<ResourceNode>();
            const string guid = "guid-de-teste-p3";

            WorldNodeIndex.Register(guid, node);
            Assert.AreSame(node, WorldNodeIndex.Find(guid));

            WorldNodeIndex.Unregister(guid);
            Assert.IsNull(WorldNodeIndex.Find(guid));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Register_GuidVazioOuNoNulo_NaoRegistra()
        {
            var go = new GameObject("NóDeTeste2");
            go.AddComponent<BoxCollider>();
            go.AddComponent<JogoDaTrupe.Core.Identity.EntityGuid>();
            var node = go.AddComponent<ResourceNode>();

            WorldNodeIndex.Register("", node);        // guid vazio → ignorado
            WorldNodeIndex.Register("guid-x", null);  // nó nulo → ignorado
            Assert.IsNull(WorldNodeIndex.Find("guid-x"));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Unregister_GuidInexistente_NaoLanca()
        {
            Assert.DoesNotThrow(() => WorldNodeIndex.Unregister("nada"));
            Assert.DoesNotThrow(() => WorldNodeIndex.Unregister(null));
        }
    }
}

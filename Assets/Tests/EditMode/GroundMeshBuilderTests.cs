using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Gameplay.World;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// O chão esculpido: a malha nasce do WorldHeight, sem asset, e chega ao filter e ao collider.
    /// Prova a fiação ExecuteAlways → OnEnable → Rebuild que o bake da NavMesh depende.
    /// </summary>
    public class GroundMeshBuilderTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // AddComponent num objeto ativo dispara OnEnable na hora (ExecuteAlways) — igual ao gerador.
        private GroundMeshBuilder Make()
        {
            _go = new GameObject("Chão (teste)");
            return _go.AddComponent<GroundMeshBuilder>();
        }

        [Test]
        public void Rebuild_ProducesFullGridAndFeedsTheCollider()
        {
            GroundMeshBuilder builder = Make();
            Mesh mesh = _go.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh, "OnEnable deve construir a malha.");
            int n = builder.VerticesPerSide;
            Assert.AreEqual(n * n, mesh.vertexCount);
            Assert.AreEqual((n - 1) * (n - 1) * 6, mesh.triangles.Length);
            Assert.AreSame(mesh, _go.GetComponent<MeshCollider>().sharedMesh, "O collider precisa da MESMA malha.");
        }

        [Test]
        public void Vertices_FollowWorldHeightExactly()
        {
            Make();
            Vector3[] vertices = _go.GetComponent<MeshFilter>().sharedMesh.vertices;
            foreach (Vector3 v in vertices)
                Assert.AreEqual(WorldHeight.SampleHeight(v.x, v.z), v.y, "Vértice em (" + v.x + ", " + v.z + ").");
        }

        [Test]
        public void Mesh_IsNotSavedWithTheScene()
        {
            Make();
            Mesh mesh = _go.GetComponent<MeshFilter>().sharedMesh;
            // Sem DontSave a cena guardaria uma referência quebrada (o Mesh não é asset).
            Assert.AreEqual(HideFlags.DontSave, mesh.hideFlags & HideFlags.DontSave);
        }

        [Test]
        public void Normals_PointUpAtTheHubCenter()
        {
            Make();
            Mesh mesh = _go.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            // O vértice mais perto do centro do hub está num platô plano: se a ordem dos
            // triângulos estivesse invertida, a normal apontaria para BAIXO e o chão sumiria.
            int nearest = 0;
            float nearestSq = float.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                float sq = vertices[i].x * vertices[i].x + vertices[i].z * vertices[i].z;
                if (sq < nearestSq) { nearestSq = sq; nearest = i; }
            }
            Assert.Greater(normals[nearest].y, 0.99f);
        }
    }
}

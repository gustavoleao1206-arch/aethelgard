using System;
using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Tests
{
    /// <summary>Contrato da fita: geometria pura, sem criar malhas nativas, objetos ou cenas.</summary>
    public class PathRibbonTests
    {
        [Test]
        public void Straight_TenMeters_HasElevenRingsAndExactWidth()
        {
            var mesh = PathRibbon.Build(new[] { Vector2.zero, new Vector2(10f, 0f) }, 4f, Flat);
            Assert.IsFalse(mesh.IsEmpty);
            Assert.AreEqual(22, mesh.Vertices.Length);
            Assert.AreEqual(22, mesh.Uvs.Length);
            Assert.AreEqual(60, mesh.Triangles.Length);
            for (int ring = 0; ring < 11; ring++)
            {
                Assert.That(Vector3.Distance(mesh.Vertices[ring * 2], mesh.Vertices[ring * 2 + 1]),
                    Is.EqualTo(4f).Within(0.0001f));
                AssertCenter(mesh, ring, new Vector2(ring, 0f));
            }
            // Um comprimento não divisível por step exige repartição uniforme, incluindo o fim.
            var uneven = PathRibbon.Build(new[] { Vector2.zero, new Vector2(2.5f, 0f) }, 4f, Flat);
            Assert.AreEqual(8, uneven.Vertices.Length);
            for (int ring = 0; ring < 4; ring++)
                AssertCenter(uneven, ring, new Vector2(2.5f * ring / 3f, 0f));
            AssertMaxStep(uneven, 1f);
        }

        [Test]
        public void Drape_EveryVertex_UsesItsOwnHeightAndLift()
        {
            var points = new[] { Vector2.zero, new Vector2(-2.5f, 7.5f), new Vector2(3f, 9f) };
            Func<float, float, float> ramp = (x, z) => 0.1f * x;
            AssertDraped(PathRibbon.Build(points, 4f, ramp, lift: 0.03f), ramp, 0.03f);
            AssertDraped(PathRibbon.Build(points, 4f, WorldHeight.SampleHeight, lift: 0.03f),
                WorldHeight.SampleHeight, 0.03f);
            // Fora dos corredores, o teste real também cobre alturas variáveis e o fundo do lago.
            var lake = new[] { new Vector2(-6f, -25f), new Vector2(0f, -25f), new Vector2(6f, -23f) };
            var mesh = PathRibbon.Build(lake, 2f, WorldHeight.SampleHeight, lift: 0.07f, step: 0.6f);
            AssertDraped(mesh, WorldHeight.SampleHeight, 0.07f);
            bool hasNegativeHeight = false;
            foreach (Vector3 vertex in mesh.Vertices)
                hasNegativeHeight |= vertex.y < -1f;
            Assert.IsTrue(hasNegativeHeight);
        }

        [Test]
        public void Winding_FlatGround_AllTrianglesFaceUp()
        {
            Vector2[][] paths =
            {
                new[] { Vector2.zero, new Vector2(10f, 0f) },
                new[] { Vector2.zero, new Vector2(0f, -10f) },
                new[] { Vector2.zero, new Vector2(10f, 0f), new Vector2(10f, 10f) },
                new[] { new Vector2(10f, 10f), new Vector2(10f, 0f), Vector2.zero },
                new[] { Vector2.zero, new Vector2(-2.5f, 7.5f), new Vector2(3f, 9f) }
            };
            foreach (Vector2[] path in paths)
            {
                var mesh = PathRibbon.Build(path, 4f, Flat);
                for (int i = 0; i < mesh.Triangles.Length; i += 3)
                {
                    Vector3 a = mesh.Vertices[mesh.Triangles[i]];
                    Vector3 b = mesh.Vertices[mesh.Triangles[i + 1]];
                    Vector3 c = mesh.Vertices[mesh.Triangles[i + 2]];
                    Assert.Greater(Vector3.Cross(b - a, c - a).y, 0f, "Triângulo " + i / 3);
                }
            }
        }

        [Test]
        public void RightAngle_SharedJoint_HasFiniteBoundedVertices()
        {
            var mesh = PathRibbon.Build(
                new[] { Vector2.zero, new Vector2(10f, 0f), new Vector2(10f, 10f) }, 4f, Flat);
            Assert.AreEqual(42, mesh.Vertices.Length);
            Assert.AreEqual(120, mesh.Triangles.Length);
            for (int ring = 0; ring < 21; ring++)
            {
                Vector2 center = ring <= 10 ? new Vector2(ring, 0f) : new Vector2(10f, ring - 10);
                AssertCenter(mesh, ring, center);
                for (int side = 0; side < 2; side++)
                {
                    Vector3 vertex = mesh.Vertices[ring * 2 + side];
                    AssertFinite(vertex.x);
                    AssertFinite(vertex.y);
                    AssertFinite(vertex.z);
                    Assert.LessOrEqual(Vector2.Distance(new Vector2(vertex.x, vertex.z), center), 4f);
                }
            }
            AssertMaxStep(mesh, 1f);
            // A seção da junta deve seguir a bissetriz, não a direção isolada de um segmento.
            float diagonal = Mathf.Sqrt(2f);
            Assert.That(mesh.Vertices[20].x, Is.EqualTo(10f - diagonal).Within(0.0001f));
            Assert.That(mesh.Vertices[20].z, Is.EqualTo(diagonal).Within(0.0001f));

            var reversal = PathRibbon.Build(
                new[] { Vector2.zero, new Vector2(10f, 0f), Vector2.zero }, 4f, Flat);
            foreach (Vector3 vertex in reversal.Vertices)
            {
                AssertFinite(vertex.x);
                AssertFinite(vertex.y);
                AssertFinite(vertex.z);
            }
        }

        [Test]
        public void Uvs_SidesAndAccumulatedDistance_AreCorrect()
        {
            var straight = PathRibbon.Build(new[] { Vector2.zero, new Vector2(10f, 0f) },
                4f, Flat, uvRepeat: 4f);
            AssertUvs(straight);
            Assert.That(straight.Uvs[20].y, Is.EqualTo(2.5f).Within(0.0001f));
            var bent = PathRibbon.Build(
                new[] { Vector2.zero, new Vector2(2.5f, 0f), new Vector2(2.5f, 3.5f) },
                4f, Flat, uvRepeat: 4f);
            AssertUvs(bent);
            Assert.That(bent.Uvs[6].y, Is.EqualTo(2.5f / 4f).Within(0.0001f));
            Assert.That(bent.Uvs[bent.Uvs.Length - 1].y, Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void Combine_OffsetsSecondRibbonAndSkipsEmptyEntries()
        {
            var first = PathRibbon.Build(new[] { Vector2.zero, new Vector2(10f, 0f) }, 4f, Flat);
            var second = PathRibbon.Build(new[] { new Vector2(20f, 0f), new Vector2(30f, 0f) }, 4f, Flat);
            var combined = PathRibbon.Combine(new[] { first, second });
            Assert.AreEqual(44, combined.Vertices.Length);
            Assert.AreEqual(44, combined.Uvs.Length);
            Assert.AreEqual(120, combined.Triangles.Length);
            foreach (int index in combined.Triangles)
                Assert.That(index, Is.InRange(0, 43));
            Assert.AreEqual(22, combined.Triangles[60]);
            for (int i = 0; i < 22; i++)
            {
                AssertVertex(first.Vertices[i], combined.Vertices[i]);
                AssertVertex(second.Vertices[i], combined.Vertices[i + 22]);
                AssertUv(first.Uvs[i], combined.Uvs[i]);
                AssertUv(second.Uvs[i], combined.Uvs[i + 22]);
            }
            for (int i = 0; i < 60; i++)
            {
                Assert.AreEqual(first.Triangles[i], combined.Triangles[i]);
                Assert.AreEqual(second.Triangles[i] + 22, combined.Triangles[i + 60]);
            }
            var empty = default(PathRibbon.RibbonMesh);
            AssertMeshEqual(combined, PathRibbon.Combine(new[] { empty, first, empty, second, empty }));
            Assert.IsTrue(PathRibbon.Combine(new PathRibbon.RibbonMesh[0]).IsEmpty);
            Assert.IsTrue(PathRibbon.Combine(new[] { empty }).IsEmpty);
            Assert.IsTrue(PathRibbon.Combine(null).IsEmpty);
            Assert.AreNotSame(first.Vertices, combined.Vertices);
            Assert.AreNotSame(first.Uvs, combined.Uvs);
            Assert.AreNotSame(first.Triangles, combined.Triangles);
        }

        [Test]
        public void Degenerates_ReturnEmptyAndConsecutiveDuplicatesAreIgnored()
        {
            Vector2[][] invalid =
            {
                new Vector2[0], new[] { Vector2.zero },
                new[] { Vector2.zero, Vector2.zero },
                new[] { Vector2.zero, new Vector2(0.00005f, 0f) }
            };
            foreach (Vector2[] points in invalid)
                Assert.IsTrue(PathRibbon.Build(points, 4f, Flat).IsEmpty);
            Assert.IsTrue(PathRibbon.Build(null, 4f, Flat).IsEmpty);
            var valid = new[] { Vector2.zero, new Vector2(10f, 0f), new Vector2(10f, 10f) };
            Assert.IsTrue(PathRibbon.Build(valid, 0f, Flat).IsEmpty);
            Assert.IsTrue(PathRibbon.Build(valid, -1f, Flat).IsEmpty);
            Assert.IsTrue(PathRibbon.Build(valid, 4f, Flat, step: 0f).IsEmpty);
            Assert.IsTrue(PathRibbon.Build(valid, 4f, Flat, step: -1f).IsEmpty);
            Assert.IsTrue(PathRibbon.Build(valid, 4f, Flat, uvRepeat: 0f).IsEmpty);
            Assert.IsTrue(PathRibbon.Build(valid, float.NaN, Flat).IsEmpty);
            Assert.IsTrue(PathRibbon.Build(valid, 4f, Flat, step: float.PositiveInfinity).IsEmpty);
            Assert.IsTrue(PathRibbon.Build(valid, 4f, null).IsEmpty);
            var repeated = new[]
            {
                Vector2.zero, Vector2.zero, new Vector2(0.00005f, 0f),
                new Vector2(10f, 0f), new Vector2(10f, 0f), new Vector2(10f, 0.00005f),
                new Vector2(10f, 10f), new Vector2(10f, 10f)
            };
            AssertMeshEqual(PathRibbon.Build(valid, 4f, Flat), PathRibbon.Build(repeated, 4f, Flat));
        }

        [Test]
        public void Determinism_FiftyBuilds_HaveIdenticalBitsAndIndices()
        {
            var points = new[]
            {
                new Vector2(-6f, -25f), new Vector2(0f, -25f), new Vector2(6f, -23f)
            };
            var original = (Vector2[])points.Clone();
            var expected = PathRibbon.Build(points, 2f, WorldHeight.SampleHeight, step: 0.7f);
            for (int repeat = 0; repeat < 50; repeat++)
                AssertMeshEqual(expected, PathRibbon.Build(points, 2f, WorldHeight.SampleHeight, step: 0.7f));
            for (int i = 0; i < points.Length; i++)
                AssertUv(original[i], points[i]);
        }

        private static float Flat(float x, float z) { return 0f; }

        private static Vector2 Center(PathRibbon.RibbonMesh mesh, int ring)
        {
            Vector3 center = (mesh.Vertices[ring * 2] + mesh.Vertices[ring * 2 + 1]) * 0.5f;
            return new Vector2(center.x, center.z);
        }

        private static void AssertCenter(PathRibbon.RibbonMesh mesh, int ring, Vector2 expected)
        {
            Vector2 center = Center(mesh, ring);
            Assert.That(center.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(center.y, Is.EqualTo(expected.y).Within(0.0001f));
        }

        private static void AssertMaxStep(PathRibbon.RibbonMesh mesh, float step)
        {
            for (int ring = 1; ring < mesh.Vertices.Length / 2; ring++)
                Assert.LessOrEqual(Vector2.Distance(Center(mesh, ring - 1), Center(mesh, ring)), step + 0.0001f);
        }

        private static void AssertDraped(PathRibbon.RibbonMesh mesh, Func<float, float, float> heightAt, float lift)
        {
            Assert.IsFalse(mesh.IsEmpty);
            foreach (Vector3 vertex in mesh.Vertices)
                Assert.That(vertex.y, Is.EqualTo(heightAt(vertex.x, vertex.z) + lift).Within(0.00001f));
        }

        private static void AssertUvs(PathRibbon.RibbonMesh mesh)
        {
            float previous = 0f;
            for (int i = 0; i < mesh.Uvs.Length; i += 2)
            {
                Assert.AreEqual(0f, mesh.Uvs[i].x);
                Assert.AreEqual(1f, mesh.Uvs[i + 1].x);
                Assert.AreEqual(mesh.Uvs[i].y, mesh.Uvs[i + 1].y);
                Assert.GreaterOrEqual(mesh.Uvs[i].y, previous);
                previous = mesh.Uvs[i].y;
            }
        }

        private static void AssertFinite(float value)
        {
            Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value));
        }

        private static void AssertBits(float expected, float actual)
        {
            Assert.AreEqual(BitConverter.ToInt32(BitConverter.GetBytes(expected), 0),
                BitConverter.ToInt32(BitConverter.GetBytes(actual), 0));
        }

        private static void AssertVertex(Vector3 expected, Vector3 actual)
        {
            AssertBits(expected.x, actual.x);
            AssertBits(expected.y, actual.y);
            AssertBits(expected.z, actual.z);
        }

        private static void AssertUv(Vector2 expected, Vector2 actual)
        {
            AssertBits(expected.x, actual.x);
            AssertBits(expected.y, actual.y);
        }

        private static void AssertMeshEqual(PathRibbon.RibbonMesh expected, PathRibbon.RibbonMesh actual)
        {
            Assert.AreEqual(expected.Vertices.Length, actual.Vertices.Length);
            Assert.AreEqual(expected.Uvs.Length, actual.Uvs.Length);
            CollectionAssert.AreEqual(expected.Triangles, actual.Triangles);
            for (int i = 0; i < expected.Vertices.Length; i++)
            {
                AssertVertex(expected.Vertices[i], actual.Vertices[i]);
                AssertUv(expected.Uvs[i], actual.Uvs[i]);
            }
        }
    }
}
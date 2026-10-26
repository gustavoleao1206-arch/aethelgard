using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Tests
{
    /// <summary>Contrato geométrico do terreno, sem cenas, física ou estado da Unity.</summary>
    public class WorldHeightTests
    {
        [Test]
        public void Plateaus_CentersFixedSamplesAndEdges_AreFlat()
        {
            // Valores do contrato independentes das constantes de afinação da implementação.
            AssertPlateau(0f, 0f, 6f);
            AssertPlateau(-11.3f, 11.3f, 5f);
            AssertPlateau(11.3f, 11.3f, 5f);
            AssertPlateau(-11.3f, -11.3f, 5f);
            AssertPlateau(11.3f, -11.3f, 5f);
            AssertPlateau(0f, 26f, 8f);
            AssertPlateau(-23f, 0f, 12f); // vila oeste (M-TERR-5)
            AssertPlateau(23f, 0f, 12f);  // vila leste
        }

        [Test]
        public void SampleHeight_RepeatedOneHundredTimes_IsExactlyEqual()
        {
            // Inclui ruído, lago e transições; só testar platôs mascararia ruído instável.
            for (int x = -44; x <= 44; x += 11)
            {
                for (int z = -44; z <= 44; z += 11)
                {
                    float expected = WorldHeight.SampleHeight(x + 0.37f, z - 0.19f);
                    for (int repeat = 0; repeat < 100; repeat++)
                        Assert.IsTrue(expected == WorldHeight.SampleHeight(x + 0.37f, z - 0.19f),
                            "A mesma entrada deve produzir igualdade exata, sem tolerância.");
                }
            }
        }

        [Test]
        public void Lake_HasNegativeBottomAndDeepWater()
        {
            Assert.Less(WorldHeight.SampleHeight(0f, -25f), 0f);
            float minimum = float.PositiveInfinity;
            for (int x = -9; x <= 9; x++)
            {
                for (int dz = -9; dz <= 9; dz++)
                {
                    if (x * x + dz * dz <= 81)
                        minimum = Mathf.Min(minimum, WorldHeight.SampleHeight(x, -25f + dz));
                }
            }
            Assert.Less(minimum, -3f);
        }

        [Test]
        public void Lake_DoesNotInvadeSouthernZones()
        {
            AssertPlateau(-11.3f, -11.3f, 5f);
            AssertPlateau(11.3f, -11.3f, 5f);
            float centerDistance = Mathf.Sqrt(11.3f * 11.3f + 13.7f * 13.7f);
            Assert.Greater(centerDistance, 9f + 5f,
                "Os discos do lago e das zonas ao sul precisam ter folga geométrica.");
        }

        [Test]
        public void Terrain_OutsidePlayableAreas_HasPeaksAboveFiveMeters()
        {
            // O setor nordeste — entre a zona de Combate, a arena e a vila leste — está fora de todos os
            // platôs e corredores. (Antes era o setor leste em z≈0, que virou o pad da vila leste, M-TERR-5.)
            float maximum = float.NegativeInfinity;
            for (int x = 22; x <= 30; x++)
            {
                for (int z = 16; z <= 24; z++)
                    maximum = Mathf.Max(maximum, WorldHeight.SampleHeight(x, z));
            }
            Assert.Greater(maximum, 5f);
        }

        [Test]
        public void Corridors_OneMeterSteps_StayBelowThirtyDegrees()
        {
            Vector3 hub = new Vector3(0f, 0f, 0f);
            AssertWalkableSegment(hub, new Vector3(-11.3f, 0f, 11.3f));
            AssertWalkableSegment(hub, new Vector3(11.3f, 0f, 11.3f));
            AssertWalkableSegment(hub, new Vector3(-11.3f, 0f, -11.3f));
            AssertWalkableSegment(hub, new Vector3(11.3f, 0f, -11.3f));
            AssertWalkableSegment(hub, new Vector3(0f, 0f, 26f));

            // Verifica também cada trecho real da trilha, incluindo suas conexões aos pisos.
            Vector3[] ruins =
            {
                hub, new Vector3(-2.5f, 0f, 7.5f), new Vector3(3f, 0f, 9f),
                new Vector3(-1.5f, 0f, 15f), new Vector3(2.2f, 0f, 19f),
                new Vector3(-2f, 0f, 22f), new Vector3(0f, 0f, 26f)
            };
            for (int i = 1; i < ruins.Length; i++)
                AssertWalkableSegment(ruins[i - 1], ruins[i]);
        }

        [Test]
        public void Map_TwoMeterGrid_IsFiniteAndWithinBounds()
        {
            for (int x = -44; x <= 44; x += 2)
            {
                for (int z = -44; z <= 44; z += 2)
                {
                    float height = WorldHeight.SampleHeight(x, z);
                    Assert.IsFalse(float.IsNaN(height) || float.IsInfinity(height));
                    Assert.That(height, Is.InRange(-10f, 20f), "Ponto: (" + x + ", " + z + ")");
                }
            }
        }

        [Test]
        public void OnGround_PreservesCoordinatesAndAddsOptionalOffset()
        {
            Vector3[] points =
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -25f),
                new Vector3(35.25f, 0f, -2.75f), new Vector3(-23.5f, 0f, 17.25f)
            };
            foreach (Vector3 point in points)
            {
                Vector3 ground = WorldHeight.OnGround(point.x, point.z);
                Assert.AreEqual(point.x, ground.x);
                Assert.AreEqual(WorldHeight.SampleHeight(point.x, point.z), ground.y);
                Assert.AreEqual(point.z, ground.z);
                foreach (float offset in new[] { -2f, 0f, 1.75f })
                {
                    Vector3 shifted = WorldHeight.OnGround(point.x, point.z, offset);
                    Assert.AreEqual(point.x, shifted.x);
                    Assert.AreEqual(ground.y + offset, shifted.y);
                    Assert.AreEqual(point.z, shifted.z);
                }
            }
        }

        private static void AssertPlateau(float centerX, float centerZ, float radius)
        {
            Assert.That(WorldHeight.SampleHeight(centerX, centerZ), Is.EqualTo(0f).Within(0.05f));
            uint state = 0x12345678u;
            // Sequência local fixa: amostra o disco sem depender de Random ou de estado compartilhado.
            for (int i = 0; i < 128; i++)
            {
                state = unchecked(state * 1664525u + 1013904223u);
                float angle = (state & 0xffffu) / 65536f * 2f * Mathf.PI;
                state = unchecked(state * 1664525u + 1013904223u);
                float distance = radius * Mathf.Sqrt((state & 0xffffu) / 65536f);
                AssertFlatPoint(centerX + Mathf.Cos(angle) * distance, centerZ + Mathf.Sin(angle) * distance);
            }
            for (int i = 0; i < 32; i++)
            {
                float angle = i * 2f * Mathf.PI / 32f;
                AssertFlatPoint(centerX + Mathf.Cos(angle) * radius, centerZ + Mathf.Sin(angle) * radius);
            }
        }

        private static void AssertFlatPoint(float x, float z)
        {
            Assert.That(WorldHeight.SampleHeight(x, z), Is.EqualTo(0f).Within(0.05f),
                "Platô deve permanecer plano em (" + x + ", " + z + ").");
        }

        private static void AssertWalkableSegment(Vector3 start, Vector3 end)
        {
            float length = Vector3.Distance(start, end);
            Vector3 direction = (end - start) / length;
            // Três faixas cobrem também a largura útil de cinco metros do corredor.
            Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x);
            for (int lane = -1; lane <= 1; lane++)
            {
                Vector3 laneStart = start + perpendicular * (lane * 2.5f);
                float previousHeight = WorldHeight.SampleHeight(laneStart.x, laneStart.z);
                float previousDistance = 0f;
                for (int step = 1; step <= Mathf.CeilToInt(length); step++)
                {
                    float distance = Mathf.Min(step, length);
                    Vector3 point = laneStart + direction * distance;
                    float height = WorldHeight.SampleHeight(point.x, point.z);
                    float slope = Mathf.Atan(Mathf.Abs(height - previousHeight)
                        / (distance - previousDistance)) * Mathf.Rad2Deg;
                    Assert.Less(slope, 30f, "Inclinação em (" + point.x + ", " + point.z + ").");
                    previousHeight = height;
                    previousDistance = distance;
                }
            }
        }
    }
}
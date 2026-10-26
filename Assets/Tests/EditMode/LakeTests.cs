using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// O lago como CONTRATO entre o relevo e o espelho d'água: a cota da água fica entre o fundo
    /// e o chão, o centro está submerso, e a BORDA da bacia está sempre acima da água — ou seja,
    /// a água só aparece dentro da bacia (nunca em vales soltos do mapa), e existe margem.
    /// </summary>
    public class LakeTests
    {
        [Test]
        public void WaterLevel_IsBetweenTheBottomAndTheGround()
        {
            Assert.Greater(WorldHeight.LakeWaterLevel, -WorldHeight.LakeDepth);
            Assert.Less(WorldHeight.LakeWaterLevel, 0f);
        }

        [Test]
        public void LakeCenter_IsUnderWater()
        {
            Assert.Less(WorldHeight.SampleHeight(WorldHeight.LakeCenterX, WorldHeight.LakeCenterZ), WorldHeight.LakeWaterLevel);
        }

        [Test]
        public void LakeRim_IsAboveWater_AllAround()
        {
            // Na borda da bacia (d = raio) a máscara do lago é 0: sobra o relevo cru, cujo piso é
            // -0.25 * amplitude. A cota da água precisa ficar ABAIXO disso — senão a água "vaza"
            // pra fora da bacia. Este teste é o que acusa se alguém subir a amplitude sem
            // rebaixar a cota (ou vice-versa).
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI * 2f / 32f;
                float x = WorldHeight.LakeCenterX + Mathf.Cos(a) * WorldHeight.LakeRadius;
                float z = WorldHeight.LakeCenterZ + Mathf.Sin(a) * WorldHeight.LakeRadius;
                Assert.GreaterOrEqual(WorldHeight.SampleHeight(x, z), WorldHeight.LakeWaterLevel,
                    "Borda do lago submersa no ângulo " + i + " — a água vazaria da bacia.");
            }
        }

        [Test]
        public void Shore_ExistsBetweenCenterAndRim()
        {
            // Andando do centro para a borda, em algum ponto o relevo cruza a cota da água.
            bool crossed = false;
            float previous = WorldHeight.SampleHeight(WorldHeight.LakeCenterX, WorldHeight.LakeCenterZ);
            for (float d = 0.5f; d <= WorldHeight.LakeRadius; d += 0.5f)
            {
                float h = WorldHeight.SampleHeight(WorldHeight.LakeCenterX + d, WorldHeight.LakeCenterZ);
                if (previous < WorldHeight.LakeWaterLevel && h >= WorldHeight.LakeWaterLevel) crossed = true;
                previous = h;
            }
            Assert.IsTrue(crossed, "Sem linha d'água entre o centro e a borda — o lago não teria margem.");
        }
    }
}

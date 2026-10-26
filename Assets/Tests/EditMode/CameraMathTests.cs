using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Presentation.Camera;

namespace JogoDaTrupe.Tests.Presentation
{
    /// <summary>
    /// Testa a lógica pura da câmera (alternância de modo, limite de inclinação e
    /// posicionamento em 3ª pessoa). Roda em EditMode, sem abrir o jogo.
    /// </summary>
    public class CameraMathTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Toggle_DeTerceira_VaiParaPrimeira()
        {
            Assert.AreEqual(CameraMode.FirstPerson, CameraMath.Toggle(CameraMode.ThirdPerson));
        }

        [Test]
        public void Toggle_DePrimeira_VoltaParaTerceira()
        {
            Assert.AreEqual(CameraMode.ThirdPerson, CameraMath.Toggle(CameraMode.FirstPerson));
        }

        [Test]
        public void ClampPitch_LimitaAoMaximoEAoMinimo()
        {
            Assert.AreEqual(75f, CameraMath.ClampPitch(120f, -60f, 75f), Tolerance);
            Assert.AreEqual(-60f, CameraMath.ClampPitch(-90f, -60f, 75f), Tolerance);
            Assert.AreEqual(10f, CameraMath.ClampPitch(10f, -60f, 75f), Tolerance);
        }

        [Test]
        public void ThirdPersonPosition_RecuaNaDistanciaCerta()
        {
            // Câmera olhando para frente (sem rotação), foco na origem, distância 5.
            // Deve ficar 5 unidades atrás (−Z).
            Vector3 pos = CameraMath.ThirdPersonPosition(Vector3.zero, Quaternion.identity, 5f);

            Assert.AreEqual(0f, pos.x, Tolerance);
            Assert.AreEqual(0f, pos.y, Tolerance);
            Assert.AreEqual(-5f, pos.z, Tolerance);
        }
    }
}

using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Gameplay.Characters;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa o cálculo de direção de movimento relativo à câmera. Como é lógica
    /// pura, roda em EditMode (rápido, sem abrir o jogo).
    /// </summary>
    public class MovementMathTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void CameraOlhandoFrente_PressionarW_MoveParaFrente()
        {
            // Câmera com yaw 0 olha para +Z. Apertar "frente" (0,1) deve ir para +Z.
            Vector3 dir = MovementMath.CameraRelativeDirection(new Vector2(0f, 1f), 0f);

            Assert.AreEqual(0f, dir.x, Tolerance);
            Assert.AreEqual(0f, dir.y, Tolerance);
            Assert.AreEqual(1f, dir.z, Tolerance);
        }

        [Test]
        public void CameraOlhandoFrente_PressionarD_MoveParaDireita()
        {
            Vector3 dir = MovementMath.CameraRelativeDirection(new Vector2(1f, 0f), 0f);

            Assert.AreEqual(1f, dir.x, Tolerance);
            Assert.AreEqual(0f, dir.z, Tolerance);
        }

        [Test]
        public void CameraGirada90Graus_PressionarW_SegueAOrientacaoDaCamera()
        {
            // Com a câmera virada 90°, "frente" passa a apontar para +X.
            Vector3 dir = MovementMath.CameraRelativeDirection(new Vector2(0f, 1f), 90f);

            Assert.AreEqual(1f, dir.x, Tolerance);
            Assert.AreEqual(0f, dir.z, Tolerance);
        }

        [Test]
        public void MovimentoDiagonal_NaoFicaMaisRapidoQueOReto()
        {
            // Apertar frente + direita não pode resultar em magnitude maior que 1.
            Vector3 dir = MovementMath.CameraRelativeDirection(new Vector2(1f, 1f), 0f);

            Assert.LessOrEqual(dir.magnitude, 1f + Tolerance);
        }
    }
}

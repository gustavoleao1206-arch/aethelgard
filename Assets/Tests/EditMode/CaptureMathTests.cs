using NUnit.Framework;
using JogoDaTrupe.Gameplay.Capture;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a lógica pura da captura (EditMode): limiar de vida, se a criatura está
    /// sincronizável e o progresso do canal de sincronização. Ver CaptureMath.
    /// </summary>
    public class CaptureMathTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void ThresholdHp_EhFracaoDaVidaMaxima()
        {
            Assert.AreEqual(30f, CaptureMath.ThresholdHp(100f, 0.3f), Tolerance);
        }

        [Test]
        public void ThresholdHp_MaxInvalido_RetornaZero()
        {
            Assert.AreEqual(0f, CaptureMath.ThresholdHp(0f, 0.3f), Tolerance);
        }

        [Test]
        public void ThresholdHp_FracaoForaDoIntervalo_EhLimitada()
        {
            Assert.AreEqual(100f, CaptureMath.ThresholdHp(100f, 2f), Tolerance);   // clamp em 1
            Assert.AreEqual(0f, CaptureMath.ThresholdHp(100f, -1f), Tolerance);    // clamp em 0
        }

        [Test]
        public void IsSynchronizable_NoLimiar_OuAbaixo_EhVerdadeiro()
        {
            Assert.IsTrue(CaptureMath.IsSynchronizable(30f, 100f, 0.3f)); // exatamente no limiar
            Assert.IsTrue(CaptureMath.IsSynchronizable(10f, 100f, 0.3f)); // abaixo
        }

        [Test]
        public void IsSynchronizable_AcimaDoLimiar_EhFalso()
        {
            Assert.IsFalse(CaptureMath.IsSynchronizable(31f, 100f, 0.3f));
            Assert.IsFalse(CaptureMath.IsSynchronizable(100f, 100f, 0.3f));
        }

        [Test]
        public void IsSynchronizable_MaxInvalido_EhFalso()
        {
            Assert.IsFalse(CaptureMath.IsSynchronizable(0f, 0f, 0.3f));
        }

        [Test]
        public void ChannelProgress_VaiDeZeroAUm_EhLimitado()
        {
            Assert.AreEqual(0f, CaptureMath.ChannelProgress(0f, 1.5f), Tolerance);
            Assert.AreEqual(0.5f, CaptureMath.ChannelProgress(0.75f, 1.5f), Tolerance);
            Assert.AreEqual(1f, CaptureMath.ChannelProgress(1.5f, 1.5f), Tolerance);
            Assert.AreEqual(1f, CaptureMath.ChannelProgress(3f, 1.5f), Tolerance); // não passa de 1
        }

        [Test]
        public void ChannelProgress_DuracaoZero_EhInstantaneo()
        {
            Assert.AreEqual(1f, CaptureMath.ChannelProgress(0f, 0f), Tolerance);
        }
    }
}

using NUnit.Framework;
using JogoDaTrupe.Gameplay.Automation;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a lógica pura da automação (EditMode): a contagem de ciclos a partir do
    /// tempo acumulado e do intervalo, com o tempo que sobra. Ver AutomationMath.
    /// </summary>
    public class AutomationMathTests
    {
        [Test]
        public void AdvanceCycles_TempoInsuficiente_ZeroCiclos_TempoIntacto()
        {
            int cycles = AutomationMath.AdvanceCycles(0.4f, 1f, out float remainder);

            Assert.AreEqual(0, cycles);
            Assert.AreEqual(0.4f, remainder, 1e-4f);
        }

        [Test]
        public void AdvanceCycles_ExatamenteUmIntervalo_UmCiclo_SemSobra()
        {
            int cycles = AutomationMath.AdvanceCycles(1f, 1f, out float remainder);

            Assert.AreEqual(1, cycles);
            Assert.AreEqual(0f, remainder, 1e-4f);
        }

        [Test]
        public void AdvanceCycles_DoisCiclosEMeio_DoisCiclos_SobraMeio()
        {
            int cycles = AutomationMath.AdvanceCycles(2.5f, 1f, out float remainder);

            Assert.AreEqual(2, cycles);
            Assert.AreEqual(0.5f, remainder, 1e-4f);
        }

        [Test]
        public void AdvanceCycles_VariosCiclos_ContaCertoESemSobra()
        {
            int cycles = AutomationMath.AdvanceCycles(4.5f, 1.5f, out float remainder);

            Assert.AreEqual(3, cycles);
            Assert.AreEqual(0f, remainder, 1e-4f);
        }

        [Test]
        public void AdvanceCycles_IntervaloInvalido_ZeroCiclos_TempoIntacto()
        {
            int cycles = AutomationMath.AdvanceCycles(5f, 0f, out float remainder);

            Assert.AreEqual(0, cycles);
            Assert.AreEqual(5f, remainder, 1e-4f);
        }
    }
}

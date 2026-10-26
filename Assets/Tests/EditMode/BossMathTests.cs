using NUnit.Framework;
using JogoDaTrupe.Gameplay.Boss;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a lógica pura do boss (EditMode): fase por vida, multiplicador de stats
    /// por fase e qual elemento o jogador mais repetiu. Ver BossMath.
    /// </summary>
    public class BossMathTests
    {
        private const float Tolerance = 0.0001f;
        private static readonly float[] Thr = { 0.66f, 0.33f };

        [Test]
        public void PhaseForHealth_AcimaDoPrimeiroLimiar_Fase0()
        {
            Assert.AreEqual(0, BossMath.PhaseForHealth(0.8f, Thr));
        }

        [Test]
        public void PhaseForHealth_EntreLimiares_Fase1()
        {
            Assert.AreEqual(1, BossMath.PhaseForHealth(0.5f, Thr));
        }

        [Test]
        public void PhaseForHealth_AbaixoDoUltimoLimiar_Fase2()
        {
            Assert.AreEqual(2, BossMath.PhaseForHealth(0.2f, Thr));
        }

        [Test]
        public void PhaseForHealth_NoLimiar_ContaComoCruzado()
        {
            Assert.AreEqual(1, BossMath.PhaseForHealth(0.66f, Thr)); // fração <= limiar
        }

        [Test]
        public void PhaseForHealth_SemLimiares_Fase0()
        {
            Assert.AreEqual(0, BossMath.PhaseForHealth(0.5f, null));
        }

        [Test]
        public void StatMultiplier_CresceComAFase()
        {
            Assert.AreEqual(1f, BossMath.StatMultiplier(0, 0.25f), Tolerance);
            Assert.AreEqual(1.5f, BossMath.StatMultiplier(2, 0.25f), Tolerance);
        }

        [Test]
        public void StatMultiplier_FaseNegativa_TratadaComoZero()
        {
            Assert.AreEqual(1f, BossMath.StatMultiplier(-3, 0.25f), Tolerance);
        }

        [Test]
        public void StatMultiplier_BonusNegativo_NaoReduzAbaixoDeUm()
        {
            // O bônus é clampado em 0: o boss nunca fica MAIS fraco a cada fase.
            Assert.AreEqual(1f, BossMath.StatMultiplier(3, -0.5f), Tolerance);
        }

        [Test]
        public void MostUsedElement_RetornaOMaisUsado_SeAtingeMinimo()
        {
            // Índices: Metal0 Eter1 Fogo2 Agua3 Terra4 Planta5
            int[] counts = { 0, 0, 4, 0, 1, 0 };
            Assert.AreEqual(2, BossMath.MostUsedElement(counts, 3)); // Fogo, 4 >= 3
        }

        [Test]
        public void MostUsedElement_AbaixoDoMinimo_RetornaMenosUm()
        {
            int[] counts = { 0, 0, 2, 0, 1, 0 };
            Assert.AreEqual(-1, BossMath.MostUsedElement(counts, 3));
        }

        [Test]
        public void MostUsedElement_Empate_RetornaOPrimeiro()
        {
            int[] counts = { 3, 3, 0, 0, 0, 0 };
            Assert.AreEqual(0, BossMath.MostUsedElement(counts, 3));
        }

        [Test]
        public void MostUsedElement_Nulo_RetornaMenosUm()
        {
            Assert.AreEqual(-1, BossMath.MostUsedElement(null, 1));
        }
    }
}

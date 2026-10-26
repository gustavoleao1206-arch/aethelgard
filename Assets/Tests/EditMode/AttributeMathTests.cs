using NUnit.Framework;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a lógica pura dos atributos (EditMode): curva de XP do personagem e a
    /// penalidade suave quando falta atributo. Ver AttributeMath.
    /// </summary>
    public class AttributeMathTests
    {
        [Test]
        public void CurvaDeXp_CresceComONivel()
        {
            Assert.AreEqual(50, AttributeMath.XpForPlayerLevel(1));
            Assert.Less(AttributeMath.XpForPlayerLevel(1), AttributeMath.XpForPlayerLevel(2));
        }

        [Test]
        public void SemRequisito_SemPenalidade()
        {
            Assert.AreEqual(1f, AttributeMath.PenaltyMultiplier(0, 0), 0.0001f);
        }

        [Test]
        public void AtributoSuficiente_SemPenalidade()
        {
            Assert.AreEqual(1f, AttributeMath.PenaltyMultiplier(8, 8), 0.0001f);
            Assert.AreEqual(1f, AttributeMath.PenaltyMultiplier(8, 12), 0.0001f);
        }

        [Test]
        public void AtributoZero_CaiAtePiso()
        {
            Assert.AreEqual(AttributeMath.MinPenaltyMultiplier, AttributeMath.PenaltyMultiplier(8, 0), 0.0001f);
        }

        [Test]
        public void AtributoMetade_PenalidadeIntermediaria()
        {
            // current/required = 4/8 = 0.5 → lerp(0.4, 1, 0.5) = 0.7
            float m = AttributeMath.PenaltyMultiplier(8, 4);
            Assert.AreEqual(0.7f, m, 0.0001f);
        }

        [Test]
        public void NuncaBloqueiaTotalmente()
        {
            // Mesmo sem nada do atributo, o multiplicador é maior que zero (penalidade suave).
            Assert.Greater(AttributeMath.PenaltyMultiplier(100, 0), 0f);
        }
    }
}

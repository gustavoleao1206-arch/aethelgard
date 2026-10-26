using NUnit.Framework;
using JogoDaTrupe.Gameplay.Combat;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a lógica pura de combate (aplicar dano e curar), sem abrir o jogo.
    /// </summary>
    public class CombatMathTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void ApplyDamage_ReduzAVida()
        {
            Assert.AreEqual(70f, CombatMath.ApplyDamage(100f, 30f), Tolerance);
        }

        [Test]
        public void ApplyDamage_NaoFicaNegativa()
        {
            Assert.AreEqual(0f, CombatMath.ApplyDamage(20f, 50f), Tolerance);
        }

        [Test]
        public void ApplyDamage_IgnoraDanoNegativo()
        {
            Assert.AreEqual(100f, CombatMath.ApplyDamage(100f, -30f), Tolerance);
        }

        [Test]
        public void Heal_NaoPassaDoMaximo()
        {
            Assert.AreEqual(100f, CombatMath.Heal(90f, 50f, 100f), Tolerance);
        }

        [Test]
        public void Heal_SomaNormalmenteDentroDoLimite()
        {
            Assert.AreEqual(75f, CombatMath.Heal(50f, 25f, 100f), Tolerance);
        }

        [Test]
        public void Heal_IgnoraCuraNegativa()
        {
            // Paridade com ApplyDamage_IgnoraDanoNegativo: cura negativa não tira vida.
            Assert.AreEqual(50f, CombatMath.Heal(50f, -25f, 100f), Tolerance);
        }

        [Test]
        public void ApplyDamage_ComPiso_NaoCaiAbaixoDoMinimo()
        {
            // Dano grande, mas a vida para no piso (criatura capturável: nunca morre).
            Assert.AreEqual(20f, CombatMath.ApplyDamage(40f, 50f, 20f), Tolerance);
        }

        [Test]
        public void ApplyDamage_ComPiso_DanoNormalAcimaDoMinimo()
        {
            Assert.AreEqual(70f, CombatMath.ApplyDamage(100f, 30f, 20f), Tolerance);
        }

        [Test]
        public void ApplyDamage_PisoZero_IgualAoSemPiso()
        {
            Assert.AreEqual(0f, CombatMath.ApplyDamage(20f, 50f, 0f), Tolerance);
        }
    }
}

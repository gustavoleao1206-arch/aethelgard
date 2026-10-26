using NUnit.Framework;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Enemies;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a decisão de postura da IA adaptativa (lógica pura, EditMode).
    /// </summary>
    public class AdaptiveBrainTests
    {
        [Test]
        public void Berserker_SempreAgressivo_MesmoComJogadorMartelando()
        {
            EnemyStance s = AdaptiveBrain.DecideStance(1f, 2f, 10, EnemyTrick.Berserker);
            Assert.AreEqual(EnemyStance.Aggressive, s);
        }

        [Test]
        public void JogadorMartelandoEPerto_FicaEsquivo()
        {
            // 3+ ataques recentes e dentro de 1.5x o alcance → esquiva.
            EnemyStance s = AdaptiveBrain.DecideStance(2f, 2f, 3, EnemyTrick.HitAndRun);
            Assert.AreEqual(EnemyStance.Evasive, s);
        }

        [Test]
        public void HitAndRun_NoAlcance_FicaDefensivo()
        {
            // Sem martelar (0 ataques), bate-e-corre no alcance → defensivo (recua).
            EnemyStance s = AdaptiveBrain.DecideStance(2f, 2f, 0, EnemyTrick.HitAndRun);
            Assert.AreEqual(EnemyStance.Defensive, s);
        }

        [Test]
        public void Longe_FicaAgressivo_ParaSeAproximar()
        {
            EnemyStance s = AdaptiveBrain.DecideStance(10f, 2f, 0, EnemyTrick.Zigzag);
            Assert.AreEqual(EnemyStance.Aggressive, s);
        }
    }
}

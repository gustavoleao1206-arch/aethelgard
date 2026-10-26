using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Enemies
{
    /// <summary>
    /// Cérebro adaptativo do inimigo: decide a POSTURA com base na distância, no
    /// quanto o jogador tem atacado (ritmo) e no "truque" do inimigo.
    ///
    /// É lógica PURA (sem MonoBehaviour) para poder ser testada sem abrir o jogo.
    /// A ideia: punir repetição (se o jogador martela ataque, o inimigo esquiva) e
    /// variar a postura para a luta não ficar previsível — sempre batível.
    /// </summary>
    public static class AdaptiveBrain
    {
        /// <summary>Quantos ataques recentes do jogador já contam como "martelando".</summary>
        public const int SpamThreshold = 2;

        public static EnemyStance DecideStance(float distance, float attackRange, int recentPlayerAttacks, EnemyTrick trick)
        {
            // Berserker: quase sempre agressivo, ignora recuo.
            if (trick == EnemyTrick.Berserker)
            {
                return EnemyStance.Aggressive;
            }

            // Jogador martelando ataque e o inimigo está por perto → esquiva (pune a repetição).
            if (recentPlayerAttacks >= SpamThreshold && distance <= attackRange * 1.5f)
            {
                return EnemyStance.Evasive;
            }

            // Bate-e-corre: quando chega ao alcance, prefere recuar/manter distância.
            if (trick == EnemyTrick.HitAndRun && distance <= attackRange)
            {
                return EnemyStance.Defensive;
            }

            // Caso geral: avança para atacar.
            return EnemyStance.Aggressive;
        }
    }
}

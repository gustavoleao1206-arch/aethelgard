using UnityEngine;

namespace JogoDaTrupe.Gameplay.Boss
{
    /// <summary>
    /// Lógica PURA do boss (testável sem abrir o jogo): a fase a partir da vida, o
    /// multiplicador de stats por fase e qual elemento o jogador mais repetiu (para o
    /// boss resistir). Mantém as regras adaptativas separadas do componente.
    /// </summary>
    public static class BossMath
    {
        /// <summary>
        /// Fase a partir da fração de vida (0..1) e dos limiares em ordem decrescente
        /// (ex.: [0.66, 0.33]). Acima do 1º limiar → fase 0; ao cruzar cada limiar,
        /// avança uma fase (fração ≤ limiar conta como cruzado).
        /// </summary>
        public static int PhaseForHealth(float fraction, float[] thresholds)
        {
            if (thresholds == null) return 0;
            int phase = 0;
            for (int i = 0; i < thresholds.Length; i++)
                if (fraction <= thresholds[i]) phase = i + 1;
            return phase;
        }

        /// <summary>Multiplicador de stat (velocidade/dano) na fase: 1 + fase × bônus.</summary>
        public static float StatMultiplier(int phase, float bonusPerPhase)
        {
            if (phase < 0) phase = 0;
            return 1f + phase * Mathf.Max(0f, bonusPerPhase);
        }

        /// <summary>
        /// Índice do elemento mais usado (maior contagem), desde que atinja
        /// <paramref name="minUses"/>; senão -1 (sem resistência). Empate → o primeiro.
        /// </summary>
        public static int MostUsedElement(int[] counts, int minUses)
        {
            if (counts == null) return -1;

            int best = -1, bestCount = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > bestCount)
                {
                    bestCount = counts[i];
                    best = i;
                }
            }
            return bestCount >= minUses ? best : -1;
        }
    }
}

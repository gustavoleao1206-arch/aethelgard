using UnityEngine;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Lógica PURA dos atributos do personagem (testável sem abrir o jogo): curva
    /// de XP do personagem e a PENALIDADE SUAVE quando falta atributo para uma
    /// habilidade.
    ///
    /// Penalidade suave (decisão de design): faltando o atributo, a habilidade
    /// funciona PIOR — não é bloqueada (Incorporacao.md seção 8). Nunca zera: há um
    /// piso, para o jogador ainda poder usar a habilidade enquanto evolui.
    /// </summary>
    public static class AttributeMath
    {
        /// <summary>Pontos de atributo concedidos a cada nível do personagem.</summary>
        public const int PointsPerLevel = 3;

        /// <summary>Piso da penalidade: mesmo sem nada do atributo, a habilidade rende isto.</summary>
        public const float MinPenaltyMultiplier = 0.4f;

        /// <summary>XP necessário para o personagem avançar DO nível <paramref name="level"/> para o seguinte.</summary>
        public static int XpForPlayerLevel(int level)
        {
            if (level < 1) level = 1;
            return 50 * level; // 1→2: 50, 2→3: 100, 3→4: 150...
        }

        /// <summary>
        /// Multiplicador (0..1] aplicado à habilidade conforme o atributo atual atende
        /// (ou não) o requisito. >= requisito → 1 (sem penalidade). Abaixo, cai
        /// linearmente até o piso. requisito &lt;= 0 → sem requisito (1).
        /// </summary>
        public static float PenaltyMultiplier(int requiredValue, int currentValue)
        {
            if (requiredValue <= 0) return 1f;
            if (currentValue >= requiredValue) return 1f;

            float ratio = Mathf.Clamp01((float)currentValue / requiredValue);
            return Mathf.Lerp(MinPenaltyMultiplier, 1f, ratio);
        }
    }
}

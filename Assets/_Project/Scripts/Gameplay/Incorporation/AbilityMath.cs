using UnityEngine;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Lógica PURA da evolução de habilidades (testável sem abrir o jogo): curva
    /// de XP e cálculo dos stats finais a partir das especializações escolhidas.
    ///
    /// Os multiplicadores são CAPADOS de propósito — é o principal botão de
    /// balanceamento do sistema (evita power creep combinatório). Ver
    /// 01_GameDesign/Incorporacao.md (seção 7 e "Dificuldade de balanceamento").
    /// </summary>
    public static class AbilityMath
    {
        /// <summary>XP ganho a cada uso da habilidade.</summary>
        public const int XpPerUse = 25;

        /// <summary>Teto de aumento por especialização (ex.: dano no máx. +150%).</summary>
        public const float MaxIncreaseFraction = 1.5f;

        /// <summary>Teto de redução por especialização (ex.: recarga no máx. −80%).</summary>
        public const float MaxReductionFraction = 0.8f;

        /// <summary>
        /// XP necessário para avançar DO nível <paramref name="level"/> para o seguinte.
        /// Curva enxuta de propósito no slice (1º ponto em ~2 usos) para o sistema
        /// ser fácil de sentir/testar; afinável depois em dados/tuning.
        /// </summary>
        public static int XpForLevel(int level)
        {
            if (level < 1) level = 1;
            return 30 + 20 * level; // 1→2: 50, 2→3: 70, 3→4: 90...
        }

        /// <summary>
        /// Soma a fração de bônus dos nós de especialização que afetam um eixo
        /// genérico (ignora nós "assinatura", tratados à parte no futuro).
        /// </summary>
        public static float AxisBonusFraction(AbilityDefinition def, int[] nodeLevels, SpecAxis axis)
        {
            if (def == null || def.specializations == null || nodeLevels == null) return 0f;

            float sum = 0f;
            int count = Mathf.Min(def.specializations.Length, nodeLevels.Length);
            for (int i = 0; i < count; i++)
            {
                SpecializationNode node = def.specializations[i];
                if (!node.isSignature && node.axis == axis)
                {
                    sum += node.valuePerLevel * nodeLevels[i];
                }
            }
            return sum;
        }

        /// <summary>Aplica um bônus de AUMENTO (dano, alcance, área...), capado.</summary>
        public static float FinalIncrease(float baseValue, float bonusFraction)
        {
            return baseValue * (1f + Mathf.Clamp(bonusFraction, 0f, MaxIncreaseFraction));
        }

        /// <summary>Aplica um bônus de REDUÇÃO (recarga, custo...), capado.</summary>
        public static float FinalReduction(float baseValue, float reductionFraction)
        {
            return baseValue * (1f - Mathf.Clamp(reductionFraction, 0f, MaxReductionFraction));
        }
    }
}

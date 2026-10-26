using UnityEngine;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Lógica PURA da afinidade elemental (testável sem abrir o jogo). Implementa
    /// as regras de design (Incorporacao.md seção 9):
    /// - 6 elementos, 3 pares opostos: Fogo↔Água, Terra↔Éter, Metal↔Planta.
    /// - Usar uma habilidade SOBE a afinidade do elemento e DERRUBA a do oposto.
    /// - Até 60%: só o oposto cai. Acima de 60%: os DEMAIS também caem devagar
    ///   (você está especializando).
    /// - Bônus ao especialista: acima de 60%, habilidades daquele elemento ganham
    ///   um bônus de dano crescente (até um teto em 100%).
    /// - Sem reset: mudar de especialização é usar outro elemento aos poucos.
    ///
    /// Números são de tuning (constantes aqui); afináveis depois.
    /// </summary>
    public static class AffinityMath
    {
        public const float Max = 100f;
        public const float SpecialistThreshold = 60f;

        /// <summary>Bônus de dano máximo do especialista (em 100% de afinidade).</summary>
        public const float MaxSpecialistBonus = 0.5f; // +50%

        // Taxas por uso (tuning enxuto para o slice ser sentido rápido).
        public const float RiseRate = 10f;          // sobe o elemento usado
        public const float OppositeFallRate = 8f;   // derruba o oposto
        public const float SpreadFallRate = 2f;     // derruba os demais (só quando especializado)

        /// <summary>Elemento oposto (3 pares). Sem oposto reconhecido, retorna ele mesmo.</summary>
        public static ElementType OppositeOf(ElementType e)
        {
            switch (e)
            {
                case ElementType.Fogo: return ElementType.Agua;
                case ElementType.Agua: return ElementType.Fogo;
                case ElementType.Terra: return ElementType.Eter;
                case ElementType.Eter: return ElementType.Terra;
                case ElementType.Metal: return ElementType.Planta;
                case ElementType.Planta: return ElementType.Metal;
                default: return e;
            }
        }

        /// <summary>
        /// Aplica o uso de uma habilidade do elemento <paramref name="used"/> ao vetor
        /// de afinidades (indexado por (int)ElementType). Altera o vetor no lugar.
        /// </summary>
        public static void ApplyUse(float[] affinities, ElementType used)
        {
            if (affinities == null) return;

            int u = (int)used;
            int opp = (int)OppositeOf(used);

            affinities[u] = Mathf.Min(Max, affinities[u] + RiseRate);
            if (opp != u) affinities[opp] = Mathf.Max(0f, affinities[opp] - OppositeFallRate);

            // Especializando (acima do limiar): os demais elementos caem devagar.
            if (affinities[u] > SpecialistThreshold)
            {
                for (int i = 0; i < affinities.Length; i++)
                {
                    if (i == u || i == opp) continue;
                    affinities[i] = Mathf.Max(0f, affinities[i] - SpreadFallRate);
                }
            }
        }

        /// <summary>
        /// Bônus de dano por especialização: 0 até o limiar; cresce linearmente até
        /// <see cref="MaxSpecialistBonus"/> em 100%.
        /// </summary>
        public static float SpecialistBonus(float affinity)
        {
            if (affinity <= SpecialistThreshold) return 0f;
            float t = Mathf.Clamp01((affinity - SpecialistThreshold) / (Max - SpecialistThreshold));
            return Mathf.Lerp(0f, MaxSpecialistBonus, t);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using JogoDaTrupe.Core.Utils;

namespace JogoDaTrupe.Gameplay.Combat
{
    /// <summary>
    /// Matemática PURA de seleção de alvo da IA (M-NET-6 P2). Distância PLANAR
    /// (XZ, via <see cref="Geometry2D"/> — DRY), consistente com o movimento da IA,
    /// que ignora a altura. Testada em <c>TargetMathTests</c>.
    /// </summary>
    public static class TargetMath
    {
        /// <summary>
        /// Índice do candidato mais PERTO de <paramref name="origin"/> (planar);
        /// -1 se a lista for nula/vazia. Empate → o primeiro da lista.
        /// </summary>
        public static int NearestIndex(Vector3 origin, IReadOnlyList<Vector3> candidates)
        {
            if (candidates == null || candidates.Count == 0) return -1;

            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                float d = Geometry2D.PlanarDistanceSqr(origin, candidates[i]);
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = i;
                }
            }
            return best;
        }
    }
}

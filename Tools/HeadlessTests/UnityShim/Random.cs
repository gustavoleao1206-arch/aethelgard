namespace UnityEngine
{
    /// <summary>
    /// Imitação mínima do <c>UnityEngine.Random</c> sobre um <c>System.Random</c> estático.
    /// Só serve para o Core compilar e o <c>SystemRandomProvider</c> funcionar fora da Unity;
    /// a SEQUÊNCIA não é a da Unity (outro gerador). Teste que precise de sequência fixa usa
    /// <c>SeededRandom</c>/<c>GameRandom.Provider</c>, nunca este.
    /// </summary>
    public static class Random
    {
        // 2^24 passos cabem exatos num float: dá um [0, 1] com as duas pontas possíveis.
        private const int Steps = 1 << 24;
        private static readonly System.Random Rng = new System.Random();

        /// <summary>Valor em [0, 1], com as DUAS pontas inclusivas, como na Unity.</summary>
        public static float value => Rng.Next(0, Steps + 1) / (float)Steps;

        /// <summary>
        /// Real em [min, max], com as DUAS pontas inclusivas, como na Unity. Nomes dos parâmetros
        /// iguais aos da Unity (chamada com argumento nomeado tem que compilar igual lá e aqui).
        /// O resultado é limitado ao intervalo: com value == 1 a conta pode passar do máximo por 1 ULP.
        /// </summary>
        public static float Range(float minInclusive, float maxInclusive)
        {
            float result = minInclusive + (maxInclusive - minInclusive) * value;
            float lo = minInclusive < maxInclusive ? minInclusive : maxInclusive;
            float hi = minInclusive < maxInclusive ? maxInclusive : minInclusive;
            return result < lo ? lo : (result > hi ? hi : result);
        }

        /// <summary>
        /// Inteiro em [min, max) — max EXCLUSIVO, como na Unity. Também como na Unity:
        /// min == max devolve min, e min &gt; max devolve um valor em (max, min] em vez de lançar
        /// exceção (o System.Random lançaria).
        /// </summary>
        public static int Range(int minInclusive, int maxExclusive)
        {
            if (minInclusive < maxExclusive)
                return Rng.Next(minInclusive, maxExclusive);
            // [max, min) + 1 = (max, min]; o Next(int, int) já trata a faixa inteira sem estourar.
            if (minInclusive > maxExclusive)
                return Rng.Next(maxExclusive, minInclusive) + 1;
            return minInclusive;
        }
    }
}

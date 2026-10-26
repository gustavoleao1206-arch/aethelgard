namespace JogoDaTrupe.Core.Rng
{
    /// <summary>
    /// Provedor de ALEATORIEDADE DE SIMULAÇÃO. Hoje (solo) usa o RNG da Unity; na rede
    /// será semeado pelo servidor, para que sorteios de gameplay sejam determinísticos e
    /// iguais em todas as máquinas. (Princípio multiplayer #8.)
    /// </summary>
    public interface IDeterministicRandom
    {
        /// <summary>Valor em [0, 1).</summary>
        float Value { get; }

        /// <summary>Valor real em [min, max).</summary>
        float Range(float min, float max);

        /// <summary>Inteiro em [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);
    }

    /// <summary>Implementação solo: encapsula <c>UnityEngine.Random</c>.</summary>
    public sealed class SystemRandomProvider : IDeterministicRandom
    {
        public float Value => UnityEngine.Random.value;
        public float Range(float min, float max) => UnityEngine.Random.Range(min, max);
        public int Range(int minInclusive, int maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    /// <summary>
    /// Aleatoriedade DETERMINÍSTICA por semente (<c>System.Random</c>). Mesma semente →
    /// mesma sequência. Útil para testes e como base do RNG semeado pelo servidor na rede.
    /// </summary>
    public sealed class SeededRandom : IDeterministicRandom
    {
        private readonly System.Random _rng;
        public SeededRandom(int seed) => _rng = new System.Random(seed);

        public float Value => (float)_rng.NextDouble();
        public float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        public int Range(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);
    }

    /// <summary>
    /// Ponto ÚNICO de acesso à aleatoriedade de simulação. A simulação lê
    /// <c>GameRandom.Value</c>/<c>Range</c> em vez de <c>UnityEngine.Random</c>, para que a
    /// rede troque UMA referência (o <see cref="Provider"/>) por um RNG semeado pelo
    /// servidor — sem mexer em quem consome. Estado global de simulação, não por-jogador.
    /// </summary>
    public static class GameRandom
    {
        /// <summary>Provedor ativo. Solo = <see cref="SystemRandomProvider"/>; rede troca por um semeado.</summary>
        public static IDeterministicRandom Provider { get; set; } = new SystemRandomProvider();

        public static float Value => Provider.Value;
        public static float Range(float min, float max) => Provider.Range(min, max);
        public static int Range(int minInclusive, int maxExclusive) => Provider.Range(minInclusive, maxExclusive);
    }
}

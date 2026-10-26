namespace UnityEngine
{
    /// <summary>
    /// Imitação mínima do <c>UnityEngine.Time</c>: fora da Unity não há relógio de frames,
    /// então os dois valores ficam em 0. Só existe para o <c>SystemTimeProvider</c> compilar;
    /// teste que precise de tempo injeta um provider em <c>GameTime.Provider</c>.
    /// Somente leitura, como na Unity.
    /// </summary>
    public static class Time
    {
        public static float time { get; }
        public static float deltaTime { get; }
    }
}

using UnityEngine;

namespace JogoDaTrupe.Gameplay.Capture
{
    /// <summary>
    /// Lógica PURA da captura (testável sem abrir o jogo): o limiar de vida abaixo do
    /// qual a criatura fica "sincronizável", se ela já está sincronizável, e o
    /// progresso do canal de sincronização (segurar E). Mantém as regras separadas
    /// do componente, como os outros sistemas (*Math) do projeto.
    /// </summary>
    public static class CaptureMath
    {
        /// <summary>Vida-limiar de captura: <paramref name="fraction"/> (0..1) da vida máxima.</summary>
        public static float ThresholdHp(float max, float fraction)
        {
            if (max <= 0f) return 0f;
            return max * Mathf.Clamp01(fraction);
        }

        /// <summary>
        /// A criatura pode ser sincronizada? Verdadeiro quando a vida atual está
        /// no/abaixo do limiar (e há vida máxima válida).
        /// </summary>
        public static bool IsSynchronizable(float current, float max, float fraction)
        {
            if (max <= 0f) return false;
            return current <= ThresholdHp(max, fraction);
        }

        /// <summary>
        /// Progresso do canal de sincronização (0..1) a partir do tempo já segurado e
        /// da duração total. Duração ≤ 0 = instantâneo (1).
        /// </summary>
        public static float ChannelProgress(float elapsed, float holdSeconds)
        {
            if (holdSeconds <= 0f) return 1f;
            return Mathf.Clamp01(elapsed / holdSeconds);
        }
    }
}

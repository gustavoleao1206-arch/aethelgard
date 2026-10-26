namespace JogoDaTrupe.Gameplay.Automation
{
    /// <summary>
    /// Lógica PURA da automação (testável sem abrir o jogo): a partir de um tempo
    /// acumulado e do intervalo de um ciclo, dizer <b>quantos ciclos</b> dispararam
    /// e <b>quanto tempo sobra</b> para o próximo. Mantém o ritmo do Coletor
    /// independente da taxa de frames (não perde nem duplica ciclos).
    /// </summary>
    public static class AutomationMath
    {
        /// <summary>
        /// Quantos ciclos completos cabem em <paramref name="accumulated"/> segundos,
        /// dado um <paramref name="intervalSeconds"/> por ciclo. Devolve em
        /// <paramref name="remainder"/> o tempo que sobra (a "fração" do próximo ciclo).
        ///
        /// Intervalo inválido (≤ 0) ou tempo insuficiente → 0 ciclos e o tempo intacto.
        /// </summary>
        public static int AdvanceCycles(float accumulated, float intervalSeconds, out float remainder)
        {
            if (intervalSeconds <= 0f || accumulated < intervalSeconds)
            {
                remainder = accumulated;
                return 0;
            }

            int cycles = (int)(accumulated / intervalSeconds);
            remainder = accumulated - cycles * intervalSeconds;
            return cycles;
        }
    }
}

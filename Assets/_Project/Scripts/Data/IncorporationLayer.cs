namespace JogoDaTrupe.Data
{
    /// <summary>
    /// Camadas de incorporação de uma habilidade, da mais comum à raríssima.
    /// A camada liberada depende da Maestria (nº de derrotas) da criatura
    /// (ver MasteryMath). Os valores inteiros (0,1,2) são usados como índice nos
    /// limiares de maestria — NÃO reordenar.
    /// </summary>
    public enum IncorporationLayer
    {
        Vestigio = 0,     // versão simples (comum)
        Ressonancia = 1,  // versão intermediária (raro)
        Nucleo = 2        // versão máxima (raríssimo)
    }
}

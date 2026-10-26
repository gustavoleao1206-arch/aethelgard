namespace JogoDaTrupe.Data
{
    /// <summary>
    /// Truque marcante de cada inimigo (comportamento que o distingue).
    /// - Berserker: quase sempre agressivo, avança sem recuar.
    /// - HitAndRun: chega perto, bate e recua (bate-e-corre).
    /// - Zigzag: se aproxima em ziguezague, difícil de acertar.
    /// </summary>
    public enum EnemyTrick
    {
        Berserker,
        HitAndRun,
        Zigzag
    }
}

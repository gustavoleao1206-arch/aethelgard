namespace JogoDaTrupe.Gameplay.Enemies
{
    /// <summary>
    /// Postura de combate que o inimigo assume e troca durante a luta.
    /// - Aggressive: avança e ataca.
    /// - Defensive: mantém distância média, recua se chegar perto demais.
    /// - Evasive: esquiva/circula, foge do jogador.
    /// </summary>
    public enum EnemyStance
    {
        Aggressive,
        Defensive,
        Evasive
    }
}

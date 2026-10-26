namespace JogoDaTrupe.Data
{
    /// <summary>
    /// Tier de uma habilidade dentro da sua FAMÍLIA (ex.: Mordida I / II / III).
    /// Variantes mais fortes de uma mesma família entram em tiers mais altos —
    /// a família compartilha a árvore da habilidade e escala por tier, em vez de
    /// balancear cada criatura à mão. Ver Incorporacao.md (seção 4).
    /// </summary>
    public enum AbilityTier
    {
        TierI = 1,
        TierII = 2,
        TierIII = 3
    }
}

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// Eixos GENÉRICOS de especialização compartilhados por todas as habilidades
    /// (framework de upgrades reutilizável — evita criar uma árvore do zero por
    /// habilidade). Cada habilidade pode ainda ter 1–2 nós "assinatura" únicos.
    /// Usado na evolução por XP (M3). Ver Incorporacao.md (seção 7).
    /// </summary>
    public enum SpecAxis
    {
        Damage,
        Range,
        Cost,
        Cooldown,
        Duration,
        Area
    }
}

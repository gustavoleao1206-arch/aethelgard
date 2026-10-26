namespace JogoDaTrupe.Core.Combat
{
    /// <summary>
    /// REDUZ o dano antes de ele chegar à vida (ex.: bloquear com o escudo). O
    /// <c>Health</c> procura um mitigador no PRÓPRIO GameObject e, se houver, passa o dano
    /// por ele antes de aplicar — só na máquina com AUTORIDADE (o gate de rede vem antes).
    ///
    /// Fica no Core (junto de <c>IDamageable</c>) para que o Health não precise conhecer o
    /// componente concreto que bloqueia (hoje o <c>Blocker</c> do jogador; amanhã armadura,
    /// escudo mágico, etc.).
    /// </summary>
    public interface IDamageMitigator
    {
        /// <summary>Devolve o dano JÁ reduzido (nunca negativo).</summary>
        float Mitigate(float amount, int attackerOwnerId);
    }
}

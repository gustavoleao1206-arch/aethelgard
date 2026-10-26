namespace JogoDaTrupe.Core.Combat
{
    /// <summary>
    /// Implementado por qualquer coisa que possa receber dano (player, inimigos,
    /// boss, objetos destrutíveis). Mantém o combate desacoplado: quem ataca não
    /// precisa conhecer o tipo concreto do alvo, só este contrato.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Aplica dano ao alvo. <paramref name="attackerOwnerId"/> = dono do ATACANTE
        /// (base do "Eco a todos os contribuintes"): o OwnerId do jogador que atacou, ou
        /// <see cref="JogoDaTrupe.Core.Ownership.Ownable.NoOwner"/> (-1) para dano de
        /// IA/ambiente (não conta como contribuinte). SEM valor default de propósito — a
        /// mudança de assinatura obriga cada call site a declarar o atacante (M-NET-5).
        /// </summary>
        void TakeDamage(float amount, int attackerOwnerId);

        /// <summary>Verdadeiro se o alvo já está morto (vida zero).</summary>
        bool IsDead { get; }
    }
}

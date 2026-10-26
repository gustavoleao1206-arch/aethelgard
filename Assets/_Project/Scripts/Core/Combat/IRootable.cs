namespace JogoDaTrupe.Core.Combat
{
    /// <summary>
    /// Implementado por qualquer coisa que possa ser "presa" (controle de movimento)
    /// por um tempo — ex.: um inimigo preso pelo Cipó. Mantém o efeito de controle
    /// desacoplado: quem aplica o controle (AbilityRunner) não precisa conhecer o
    /// tipo concreto do alvo, só este contrato (irmão de IDamageable).
    /// </summary>
    public interface IRootable
    {
        /// <summary>Prende o alvo por <paramref name="seconds"/> segundos (estende se já preso).</summary>
        void ApplyRoot(float seconds);
    }
}

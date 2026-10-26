using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Combat
{
    /// <summary>
    /// Implementado por algo que RESISTE (ou é fraco) a um elemento — ex.: o boss
    /// "Eco Corrompido", que passa a resistir ao elemento que o jogador mais repete.
    /// Quem aplica dano elemental (AbilityRunner) consulta este contrato e multiplica
    /// o dano, sem conhecer o tipo concreto do alvo (irmão de IDamageable/IRootable).
    ///
    /// Fica na camada Gameplay (não no Core) porque referencia <see cref="ElementType"/>
    /// (Data), e o Core não pode depender de Data.
    /// </summary>
    public interface IElementResistant
    {
        /// <summary>Multiplicador de dano para o elemento dado (1 = normal, &lt; 1 = resiste).</summary>
        float DamageMultiplierFor(ElementType element);
    }
}

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// Tipo de efeito de uma habilidade. A ficha (AbilityDefinition) escolhe qual;
    /// o efeito concreto é executado pelo AbilityRunner (Gameplay), mantendo
    /// Definição ≠ Estado e a lógica fora dos dados.
    /// </summary>
    public enum AbilityEffect
    {
        /// <summary>Golpe à frente: rajada de dano numa esfera (ex.: Mordida).</summary>
        DamageBurst = 0,

        /// <summary>Controle: dano + prende o alvo por alguns segundos (ex.: Cipó).</summary>
        Snare = 1
    }
}

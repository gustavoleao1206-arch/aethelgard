namespace JogoDaTrupe.Gameplay.Capture
{
    /// <summary>
    /// Implementado por algo que o jogador pode CAPTURAR por sincronização (segurar
    /// E). Mantém o <see cref="Synchronizer"/> desacoplado do tipo concreto da
    /// criatura — ele só precisa deste contrato.
    /// </summary>
    public interface ISynchronizable
    {
        /// <summary>Está pronta para ser sincronizada agora (enfraquecida e ainda não capturada)?</summary>
        bool CanSynchronize { get; }

        /// <summary>Nome curto para o aviso na tela (ex.: "Criatura de Planta").</summary>
        string Label { get; }

        /// <summary>Conclui a captura para o jogador <paramref name="ownerId"/> (concede o Eco).</summary>
        void Synchronize(int ownerId);
    }
}

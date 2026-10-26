namespace JogoDaTrupe.Core.Events
{
    /// <summary>
    /// Marca um tipo como "evento de jogo" que pode trafegar pelo EventBus.
    ///
    /// Convenção do projeto: todo evento deve carregar QUEM o originou
    /// (por exemplo, um campo OwnerId). Assim, no multiplayer, sabemos a qual
    /// jogador o acontecimento pertence sem mudar quem escuta o evento.
    /// </summary>
    public interface IGameEvent { }
}

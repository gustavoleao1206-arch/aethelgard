using JogoDaTrupe.Core.Events;

namespace JogoDaTrupe.Data
{
    // Eventos do inventário. Como podem carregar referências de Data (ItemDefinition),
    // vivem aqui (mesma razão dos eventos de Incorporação): o Core não pode depender
    // de Data. Todos carregam o OwnerId (multiplayer-ready).

    /// <summary>
    /// Disparado quando o conteúdo do inventário de um jogador muda. A UI escuta e
    /// relê o inventário do jogador para se atualizar.
    /// </summary>
    public struct InventoryChanged : IGameEvent
    {
        public int OwnerId;
    }

    /// <summary>
    /// Disparado quando um item é coletado (para feedback: som, "+3 Madeira", etc.).
    /// </summary>
    public struct ItemCollected : IGameEvent
    {
        public int OwnerId;
        public ItemDefinition Item;
        public int Amount;
    }

    /// <summary>
    /// Disparado quando o jogador pede abrir/fechar o inventário (tecla I).
    /// Só carrega o OwnerId; a UI encontra o inventário do jogador.
    /// </summary>
    public struct InventoryToggled : IGameEvent
    {
        public int OwnerId;
    }
}

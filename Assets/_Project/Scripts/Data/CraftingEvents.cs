using JogoDaTrupe.Core.Events;

namespace JogoDaTrupe.Data
{
    // Eventos de crafting. Como podem carregar referências de Data (RecipeDefinition/
    // ItemDefinition), vivem aqui (o Core não pode depender de Data). Carregam OwnerId.

    /// <summary>
    /// Disparado quando o jogador abre/fecha a bancada (interagindo com ela).
    /// Só carrega o OwnerId; a UI encontra o inventário do jogador.
    /// </summary>
    public struct CraftingToggled : IGameEvent
    {
        public int OwnerId;
    }

    /// <summary>Disparado quando um item é craftado (para feedback futuro).</summary>
    public struct ItemCrafted : IGameEvent
    {
        public int OwnerId;
        public RecipeDefinition Recipe;
    }
}

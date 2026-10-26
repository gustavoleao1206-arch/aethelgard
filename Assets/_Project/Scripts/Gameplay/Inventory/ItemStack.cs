using System;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Inventory
{
    /// <summary>
    /// Uma pilha de itens num slot do inventário: aponta para a Definição (ficha) e
    /// guarda a quantidade (Estado). Classe simples e serializável para viver no
    /// Inventory e, no futuro, ir para o save (regra Definição ≠ Estado).
    /// </summary>
    [Serializable]
    public sealed class ItemStack
    {
        public ItemDefinition Definition;
        public int Count;

        public ItemStack(ItemDefinition definition, int count)
        {
            Definition = definition;
            Count = count;
        }
    }
}

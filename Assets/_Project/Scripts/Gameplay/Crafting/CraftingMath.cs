using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.Gameplay.Crafting
{
    /// <summary>
    /// Lógica PURA do crafting (testável sem abrir o jogo): saber se dá para craftar
    /// uma receita com o que há no inventário e executar o craft (consome
    /// ingredientes, produz o resultado). Opera sobre o <see cref="InventoryModel"/>.
    /// Os eventos/feedback ficam no <see cref="PlayerInventory"/> (que envolve o modelo).
    /// </summary>
    public static class CraftingMath
    {
        /// <summary>Verdadeiro se o inventário tem todos os ingredientes e a receita é válida.</summary>
        public static bool CanCraft(InventoryModel inventory, RecipeDefinition recipe)
        {
            if (inventory == null || recipe == null || recipe.output == null) return false;
            if (recipe.ingredients == null || recipe.outputAmount <= 0) return false;

            // HasAll também recusa ingrediente com item nulo (receita malformada).
            return inventory.HasAll(recipe.ingredients);
        }

        /// <summary>
        /// Crafta se possível: consome os ingredientes e adiciona o resultado.
        /// Retorna true se craftou. Não dispara eventos (quem faz isso é o PlayerInventory).
        ///
        /// É ATÔMICO: se o resultado não couber inteiro no inventário (cheio), desfaz
        /// tudo (rollback) e retorna false — nunca consome ingredientes "no vácuo".
        /// </summary>
        public static bool Craft(InventoryModel inventory, RecipeDefinition recipe)
        {
            if (!CanCraft(inventory, recipe)) return false;

            // Consome os ingredientes.
            for (int i = 0; i < recipe.ingredients.Length; i++)
            {
                RecipeIngredient ing = recipe.ingredients[i];
                inventory.Remove(ing.item, ing.amount);
            }

            // Adiciona o resultado; se não couber tudo, desfaz e falha.
            int leftover = inventory.Add(recipe.output, recipe.outputAmount);
            if (leftover > 0)
            {
                inventory.Remove(recipe.output, recipe.outputAmount - leftover); // tira o que entrou
                for (int i = 0; i < recipe.ingredients.Length; i++)
                {
                    RecipeIngredient ing = recipe.ingredients[i];
                    inventory.Add(ing.item, ing.amount); // devolve os ingredientes
                }
                return false;
            }

            return true;
        }
    }
}

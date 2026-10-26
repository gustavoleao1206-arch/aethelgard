using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Inventory;
using JogoDaTrupe.Gameplay.Crafting;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a lógica pura do crafting (EditMode): saber se dá para craftar e o
    /// craft consumir os ingredientes e produzir o resultado. Ver CraftingMath.
    /// </summary>
    public class CraftingMathTests
    {
        private static ItemDefinition Item(string name)
        {
            var d = ScriptableObject.CreateInstance<ItemDefinition>();
            d.displayName = name;
            d.maxStack = 99;
            return d;
        }

        private static RecipeIngredient Ing(ItemDefinition item, int amount) =>
            new RecipeIngredient { item = item, amount = amount };

        private static RecipeDefinition Recipe(ItemDefinition output, params RecipeIngredient[] ings)
        {
            var r = ScriptableObject.CreateInstance<RecipeDefinition>();
            r.output = output;
            r.outputAmount = 1;
            r.ingredients = ings;
            return r;
        }

        [Test]
        public void CanCraft_True_QuandoTemTudo()
        {
            var madeira = Item("Madeira");
            var tabua = Item("Tábua");
            var inv = new InventoryModel(10);
            inv.Add(madeira, 2);

            var recipe = Recipe(tabua, Ing(madeira, 2));
            Assert.IsTrue(CraftingMath.CanCraft(inv, recipe));

            Object.DestroyImmediate(madeira); Object.DestroyImmediate(tabua); Object.DestroyImmediate(recipe);
        }

        [Test]
        public void CanCraft_False_QuandoFaltaIngrediente()
        {
            var madeira = Item("Madeira");
            var pedra = Item("Pedra");
            var machado = Item("Machado");
            var inv = new InventoryModel(10);
            inv.Add(madeira, 3); // tem madeira, mas nenhuma pedra

            var recipe = Recipe(machado, Ing(madeira, 3), Ing(pedra, 2));
            Assert.IsFalse(CraftingMath.CanCraft(inv, recipe));

            Object.DestroyImmediate(madeira); Object.DestroyImmediate(pedra);
            Object.DestroyImmediate(machado); Object.DestroyImmediate(recipe);
        }

        [Test]
        public void Craft_ConsomeIngredientes_EProduzResultado()
        {
            var madeira = Item("Madeira");
            var pedra = Item("Pedra");
            var machado = Item("Machado");
            var inv = new InventoryModel(10);
            inv.Add(madeira, 5);
            inv.Add(pedra, 3);

            var recipe = Recipe(machado, Ing(madeira, 3), Ing(pedra, 2));
            bool ok = CraftingMath.Craft(inv, recipe);

            Assert.IsTrue(ok);
            Assert.AreEqual(2, inv.CountOf(madeira)); // 5 - 3
            Assert.AreEqual(1, inv.CountOf(pedra));   // 3 - 2
            Assert.AreEqual(1, inv.CountOf(machado)); // produzido

            Object.DestroyImmediate(madeira); Object.DestroyImmediate(pedra);
            Object.DestroyImmediate(machado); Object.DestroyImmediate(recipe);
        }

        [Test]
        public void Craft_Falha_NaoAlteraInventario()
        {
            var madeira = Item("Madeira");
            var tabua = Item("Tábua");
            var inv = new InventoryModel(10);
            inv.Add(madeira, 1); // precisa de 2

            var recipe = Recipe(tabua, Ing(madeira, 2));
            bool ok = CraftingMath.Craft(inv, recipe);

            Assert.IsFalse(ok);
            Assert.AreEqual(1, inv.CountOf(madeira)); // intacto
            Assert.AreEqual(0, inv.CountOf(tabua));

            Object.DestroyImmediate(madeira); Object.DestroyImmediate(tabua); Object.DestroyImmediate(recipe);
        }

        [Test]
        public void Craft_SemEspacoParaResultado_FazRollback_RetornaFalse()
        {
            // Inventário de 1 slot, ocupado pela própria Madeira: há ingredientes,
            // mas o resultado (Tábua, item diferente) não tem onde entrar.
            var madeira = Item("Madeira");
            var tabua = Item("Tábua");
            var inv = new InventoryModel(1);
            inv.Add(madeira, 4); // único slot vira "Madeira x4"

            var recipe = Recipe(tabua, Ing(madeira, 2));
            bool ok = CraftingMath.Craft(inv, recipe);

            Assert.IsFalse(ok);                      // não craftou (sem espaço para a Tábua)
            Assert.AreEqual(4, inv.CountOf(madeira)); // ingredientes intactos (rollback)
            Assert.AreEqual(0, inv.CountOf(tabua));   // nada produzido

            Object.DestroyImmediate(madeira); Object.DestroyImmediate(tabua); Object.DestroyImmediate(recipe);
        }

        [Test]
        public void CanCraft_False_ReceitaMalformada()
        {
            var tabua = Item("Tábua");
            var inv = new InventoryModel(10);

            // Ingrediente com item nulo → receita inválida.
            var recipe = Recipe(tabua, Ing(null, 2));
            Assert.IsFalse(CraftingMath.CanCraft(inv, recipe));

            // Saída nula → inválida.
            var semSaida = Recipe(null, Ing(tabua, 1));
            Assert.IsFalse(CraftingMath.CanCraft(inv, semSaida));

            Object.DestroyImmediate(tabua); Object.DestroyImmediate(recipe); Object.DestroyImmediate(semSaida);
        }

        [Test]
        public void CanCraft_False_OutputAmountInvalido()
        {
            var madeira = Item("Madeira");
            var tabua = Item("Tábua");
            var inv = new InventoryModel(10);
            inv.Add(madeira, 5);

            var zero = Recipe(tabua, Ing(madeira, 2));
            zero.outputAmount = 0;
            Assert.IsFalse(CraftingMath.CanCraft(inv, zero));

            var negativo = Recipe(tabua, Ing(madeira, 2));
            negativo.outputAmount = -3;
            Assert.IsFalse(CraftingMath.CanCraft(inv, negativo));

            Object.DestroyImmediate(madeira); Object.DestroyImmediate(tabua);
            Object.DestroyImmediate(zero); Object.DestroyImmediate(negativo);
        }
    }
}

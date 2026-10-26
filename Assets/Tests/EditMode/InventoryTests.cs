using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa o modelo de inventário (lógica pura, EditMode): empilhar respeitando
    /// maxStack, ocupar vários slots, capacidade cheia (sobra), remover e contar.
    /// </summary>
    public class InventoryTests
    {
        private static ItemDefinition MakeItem(int maxStack)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.displayName = "Teste";
            def.maxStack = maxStack;
            return def;
        }

        [Test]
        public void Add_EmInventarioVazio_CriaUmaPilha()
        {
            var item = MakeItem(99);
            var inv = new InventoryModel(10);

            int leftover = inv.Add(item, 5);

            Assert.AreEqual(0, leftover);
            Assert.AreEqual(1, inv.Slots.Count);
            Assert.AreEqual(5, inv.CountOf(item));
            Object.DestroyImmediate(item);
        }

        [Test]
        public void Add_RespeitaMaxStack_OcupandoVariosSlots()
        {
            var item = MakeItem(10);
            var inv = new InventoryModel(10);

            int leftover = inv.Add(item, 25); // 10 + 10 + 5 → 3 pilhas

            Assert.AreEqual(0, leftover);
            Assert.AreEqual(3, inv.Slots.Count);
            Assert.AreEqual(25, inv.CountOf(item));
            Object.DestroyImmediate(item);
        }

        [Test]
        public void Add_CompletaPilhaExistenteAntesDeCriarNova()
        {
            var item = MakeItem(10);
            var inv = new InventoryModel(10);

            inv.Add(item, 7);        // 1 pilha com 7
            int leftover = inv.Add(item, 5); // completa para 10 e cria 1 com 2

            Assert.AreEqual(0, leftover);
            Assert.AreEqual(2, inv.Slots.Count);
            Assert.AreEqual(12, inv.CountOf(item));
            Object.DestroyImmediate(item);
        }

        [Test]
        public void Add_AlemDaCapacidade_RetornaSobra()
        {
            var item = MakeItem(10);
            var inv = new InventoryModel(2); // só 2 slots → cabem 20

            int leftover = inv.Add(item, 25);

            Assert.AreEqual(5, leftover);
            Assert.AreEqual(2, inv.Slots.Count);
            Assert.AreEqual(20, inv.CountOf(item));
            Object.DestroyImmediate(item);
        }

        [Test]
        public void Remove_RetiraEContaCorreto_ELimpaPilhasVazias()
        {
            var item = MakeItem(10);
            var inv = new InventoryModel(10);
            inv.Add(item, 15); // 2 pilhas (10 + 5)

            int removed = inv.Remove(item, 12);

            Assert.AreEqual(12, removed);
            Assert.AreEqual(3, inv.CountOf(item));
            Assert.AreEqual(1, inv.Slots.Count); // a pilha esvaziada foi removida
            Object.DestroyImmediate(item);
        }

        [Test]
        public void Remove_MaisDoQueTem_RemoveSoODisponivel()
        {
            var item = MakeItem(10);
            var inv = new InventoryModel(10);
            inv.Add(item, 4);

            int removed = inv.Remove(item, 10);

            Assert.AreEqual(4, removed);
            Assert.AreEqual(0, inv.CountOf(item));
            Assert.AreEqual(0, inv.Slots.Count);
            Object.DestroyImmediate(item);
        }

        [Test]
        public void Has_RefleteAQuantidade()
        {
            var item = MakeItem(99);
            var inv = new InventoryModel(10);
            inv.Add(item, 3);

            Assert.IsTrue(inv.Has(item, 3));
            Assert.IsFalse(inv.Has(item, 4));
            Object.DestroyImmediate(item);
        }

        [Test]
        public void HasAll_VerificaTodosOsRequisitos()
        {
            var madeira = MakeItem(99);
            var pedra = MakeItem(99);
            var inv = new InventoryModel(10);
            inv.Add(madeira, 3);
            inv.Add(pedra, 1);

            var custoOk = new[]
            {
                new RecipeIngredient { item = madeira, amount = 3 },
                new RecipeIngredient { item = pedra, amount = 1 }
            };
            var custoFalta = new[]
            {
                new RecipeIngredient { item = madeira, amount = 3 },
                new RecipeIngredient { item = pedra, amount = 2 } // só tem 1
            };

            Assert.IsTrue(inv.HasAll(custoOk));
            Assert.IsFalse(inv.HasAll(custoFalta));
            Assert.IsTrue(inv.HasAll(null)); // sem requisito

            Object.DestroyImmediate(madeira); Object.DestroyImmediate(pedra);
        }
    }
}

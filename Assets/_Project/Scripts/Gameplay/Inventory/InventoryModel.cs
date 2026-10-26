using System.Collections.Generic;
using UnityEngine;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Inventory
{
    /// <summary>
    /// Modelo do inventário: ESTADO de runtime (pilhas de itens em N slots). É uma
    /// classe PURA (sem MonoBehaviour), com toda a lógica de empilhar/remover
    /// testável em EditMode. O componente <see cref="PlayerInventory"/> a envolve,
    /// dispara eventos e cuida do dono — mantendo regras e apresentação separadas.
    ///
    /// (Nome "InventoryModel" e não "Inventory" para não colidir com o namespace
    /// JogoDaTrupe.Gameplay.Inventory — evita ambiguidade de nome em C#.)
    ///
    /// Empilhar respeita o maxStack de cada item; um item pode ocupar vários slots.
    /// </summary>
    public sealed class InventoryModel
    {
        private readonly List<ItemStack> _slots = new List<ItemStack>();

        public InventoryModel(int capacity)
        {
            Capacity = Mathf.Max(1, capacity);
        }

        /// <summary>Número máximo de slots (pilhas) que o inventário comporta.</summary>
        public int Capacity { get; }

        /// <summary>Slots ocupados (somente leitura).</summary>
        public IReadOnlyList<ItemStack> Slots => _slots;

        /// <summary>Esvazia o inventário (usado ao restaurar um save).</summary>
        public void Clear() => _slots.Clear();

        /// <summary>Quantidade total de um item (somando todas as pilhas).</summary>
        public int CountOf(ItemDefinition def)
        {
            if (def == null) return 0;
            int total = 0;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Definition == def) total += _slots[i].Count;
            return total;
        }

        public bool Has(ItemDefinition def, int amount) => CountOf(def) >= amount;

        /// <summary>
        /// Tem TODOS os requisitos (pares item+quantidade)? Usado pelo crafting e pela
        /// construção (custo), para não duplicar a checagem. Lista nula = sem requisito.
        /// </summary>
        public bool HasAll(RecipeIngredient[] requirements)
        {
            if (requirements == null) return true;
            for (int i = 0; i < requirements.Length; i++)
            {
                RecipeIngredient req = requirements[i];
                if (req.item == null) return false;
                if (CountOf(req.item) < req.amount) return false;
            }
            return true;
        }

        /// <summary>
        /// Adiciona <paramref name="amount"/> do item: completa pilhas existentes e cria
        /// novas enquanto houver slot. Retorna o que NÃO coube (0 = coube tudo).
        /// </summary>
        public int Add(ItemDefinition def, int amount)
        {
            if (def == null || amount <= 0) return 0;

            int max = Mathf.Max(1, def.maxStack);
            int remaining = amount;

            // 1) Completa pilhas já existentes do mesmo item.
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                ItemStack stack = _slots[i];
                if (stack.Definition != def || stack.Count >= max) continue;

                int space = max - stack.Count;
                int put = Mathf.Min(space, remaining);
                stack.Count += put;
                remaining -= put;
            }

            // 2) Cria novas pilhas enquanto houver slot livre.
            while (remaining > 0 && _slots.Count < Capacity)
            {
                int put = Mathf.Min(max, remaining);
                _slots.Add(new ItemStack(def, put));
                remaining -= put;
            }

            return remaining; // sobra que não coube
        }

        /// <summary>
        /// Remove até <paramref name="amount"/> do item. Retorna quanto foi removido
        /// de fato (pode ser menor se não havia o suficiente). Limpa pilhas vazias.
        /// </summary>
        public int Remove(ItemDefinition def, int amount)
        {
            if (def == null || amount <= 0) return 0;

            int toRemove = amount;
            int removed = 0;

            for (int i = _slots.Count - 1; i >= 0 && toRemove > 0; i--)
            {
                ItemStack stack = _slots[i];
                if (stack.Definition != def) continue;

                int take = Mathf.Min(stack.Count, toRemove);
                stack.Count -= take;
                toRemove -= take;
                removed += take;

                if (stack.Count <= 0) _slots.RemoveAt(i);
            }

            return removed;
        }
    }
}

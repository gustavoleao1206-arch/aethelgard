using System.Collections.Generic;
using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.Saving;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Crafting;

namespace JogoDaTrupe.Gameplay.Inventory
{
    /// <summary>
    /// Inventário do jogador (estado por-jogador, vai para o save). Envolve um
    /// <see cref="InventoryModel"/> (a lógica pura) e avisa o resto do jogo por
    /// eventos com OwnerId (multiplayer-ready), sem conhecer a UI.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Ownable))]
    public sealed class PlayerInventory : MonoBehaviour, ISaveable
    {
        [Tooltip("Número de slots (pilhas) do inventário.")]
        [SerializeField] private int _capacity = 20;

        private InventoryModel _model;
        private Ownable _ownable;

        private void Awake()
        {
            _ownable = GetComponent<Ownable>();
            _model = new InventoryModel(_capacity);
        }

        public int Capacity => _model.Capacity;
        public IReadOnlyList<ItemStack> Slots => _model.Slots;
        public int CountOf(ItemDefinition def) => _model.CountOf(def);

        /// <summary>
        /// Adiciona itens. Retorna o que NÃO coube (0 = coube tudo). Dispara
        /// ItemCollected (pelo que entrou) e InventoryChanged.
        ///
        /// SEM AUTORIDADE (cliente no co-op, M-NET-7 P2) não muta: o inventário
        /// autoritativo vive no servidor; este é um espelho (só o SyncFrom o escreve).
        /// Retorna o amount inteiro ("nada coube") — quem deposita (ex.: ResourceNode)
        /// não desconta nada da fonte local.
        /// </summary>
        public int Add(ItemDefinition def, int amount)
        {
            if (def == null || amount <= 0) return 0;
            if (!SimulationAuthority.IsAuthority) return amount;

            int leftover = _model.Add(def, amount);
            int accepted = amount - leftover;
            if (accepted <= 0) return leftover; // nada coube (inventário cheio)

            EventBus<ItemCollected>.Publish(new ItemCollected
            {
                OwnerId = _ownable.OwnerId,
                Item = def,
                Amount = accepted
            });
            PublishChanged();
            return leftover;
        }

        /// <summary>Remove itens. Retorna quanto saiu de fato. Dispara InventoryChanged se removeu algo. Sem autoridade não muta (retorna 0).</summary>
        public int Remove(ItemDefinition def, int amount)
        {
            if (!SimulationAuthority.IsAuthority) return 0;

            int removed = _model.Remove(def, amount);
            if (removed > 0) PublishChanged();
            return removed;
        }

        /// <summary>O inventário tem os ingredientes para esta receita?</summary>
        public bool CanCraft(RecipeDefinition recipe) => CraftingMath.CanCraft(_model, recipe);

        /// <summary>Tem os itens para pagar um custo (ex.: construir uma estrutura)?</summary>
        public bool CanAfford(RecipeIngredient[] cost) => _model.HasAll(cost);

        /// <summary>
        /// Paga um custo (remove os itens) se puder. Dispara InventoryChanged uma vez.
        /// Retorna true se pagou. Sem autoridade não muta (o servidor valida e paga).
        /// </summary>
        public bool SpendCost(RecipeIngredient[] cost)
        {
            if (!SimulationAuthority.IsAuthority) return false;
            if (!_model.HasAll(cost)) return false;
            if (cost != null)
            {
                for (int i = 0; i < cost.Length; i++)
                    _model.Remove(cost[i].item, cost[i].amount);
            }
            PublishChanged();
            return true;
        }

        /// <summary>
        /// Crafta a receita (consome ingredientes, adiciona o resultado). Dispara
        /// InventoryChanged + ItemCrafted uma única vez. Retorna true se craftou.
        ///
        /// SEM autoridade (cliente no co-op, M-NET-7 P4): não crafta local — publica a
        /// intenção <see cref="CraftRequested"/> e retorna false. O servidor valida no
        /// inventário autoritativo e o resultado volta pela replicação do
        /// <c>NetworkInventory</c>. Mesmo padrão do <c>ResourceNode.Interact</c> (P3).
        /// </summary>
        public bool TryCraft(RecipeDefinition recipe)
        {
            if (!SimulationAuthority.IsAuthority)
            {
                if (recipe != null)
                    EventBus<CraftRequested>.Publish(new CraftRequested { RecipeId = recipe.recipeId, OwnerId = _ownable.OwnerId });
                return false;
            }
            if (!CraftingMath.Craft(_model, recipe)) return false;

            PublishChanged();
            EventBus<ItemCrafted>.Publish(new ItemCrafted { OwnerId = _ownable.OwnerId, Recipe = recipe });
            return true;
        }

        /// <summary>
        /// ESPELHA o inventário vindo da REDE (M-NET-7 P2). Só a réplica usa isto (via
        /// <c>NetworkInventory</c>): o conteúdo autoritativo é decidido no servidor e
        /// chega por <c>NetworkList</c>; aqui apenas se recompõe o modelo local e se
        /// republica o <c>InventoryChanged</c> (HUD/Crafting releem os slots). NÃO passa
        /// pelos gates de autoridade — este é exatamente o caminho de escrita do espelho.
        /// Ids desconhecidos são pulados (aviso), não derrubam o resto.
        /// </summary>
        public void SyncFrom(IReadOnlyList<(string itemId, int count)> entries)
        {
            _model.Clear();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    (string itemId, int count) = entries[i];
                    if (count <= 0) continue;

                    ItemDefinition def = DefinitionRegistry.Active != null
                        ? DefinitionRegistry.Active.ItemById(itemId) : null;
                    if (def == null)
                    {
                        Debug.LogWarning($"[Rede] Inventário espelhado pulou o item '{itemId}': não encontrado no DefinitionRegistry.Active.");
                        continue;
                    }
                    _model.Add(def, count);
                }
            }
            PublishChanged();
        }

        private void PublishChanged()
        {
            EventBus<InventoryChanged>.Publish(new InventoryChanged { OwnerId = _ownable.OwnerId });
        }

        // ---- Save (ISaveable) ----

        public string SaveKey => "inventory:" + _ownable.OwnerId;

        [System.Serializable]
        private struct InventorySave
        {
            public List<string> itemIds;
            public List<int> counts;
        }

        public object CaptureState()
        {
            var data = new InventorySave { itemIds = new List<string>(), counts = new List<int>() };
            foreach (ItemStack stack in _model.Slots)
            {
                if (stack.Definition == null) continue;
                data.itemIds.Add(stack.Definition.itemId);
                data.counts.Add(stack.Count);
            }
            return data;
        }

        public void RestoreState(object state)
        {
            var data = JsonUtility.FromJson<InventorySave>((string)state);
            _model.Clear();
            // Guarda de comprimento: o JSON vem do disco (pode estar editado/corrompido),
            // então só itera até onde as duas listas paralelas casam.
            int count = (data.itemIds != null && data.counts != null)
                ? Mathf.Min(data.itemIds.Count, data.counts.Count) : 0;
            for (int i = 0; i < count; i++)
            {
                ItemDefinition def = DefinitionRegistry.Active != null
                    ? DefinitionRegistry.Active.ItemById(data.itemIds[i]) : null;
                if (def != null) _model.Add(def, data.counts[i]);
            }
            PublishChanged();
        }
    }
}

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// Tela de crafting da bancada: abre/fecha quando o jogador usa a bancada (via
    /// evento) e lista as receitas. Cada receita só fica clicável se o jogador tem
    /// os ingredientes. Reage aos eventos (desacoplada) e lê o inventário do jogador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CraftingUI : MonoBehaviour
    {
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private RecipeDefinition[] _recipes;

        /// <summary>Liga esta tela ao inventário do jogador local (rede, no spawn do dono). As receitas são assets.</summary>
        public void Bind(PlayerInventory inventory) => _inventory = inventory;

        private GameObject _panel;
        private Transform _list;
        private Text _status;
        private readonly StringBuilder _sb = new StringBuilder(64);

        private void Awake()
        {
            BuildPanel();
            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus<CraftingToggled>.Subscribe(OnToggle);
            EventBus<InventoryChanged>.Subscribe(OnInventoryChanged);
        }

        private void OnDisable()
        {
            EventBus<CraftingToggled>.Unsubscribe(OnToggle);
            EventBus<InventoryChanged>.Unsubscribe(OnInventoryChanged);
        }

        private void OnToggle(CraftingToggled e)
        {
            if (e.OwnerId != LocalPlayer.Id) return;
            bool show = !_panel.activeSelf;
            _panel.SetActive(show);
            if (show)
            {
                _status.text = "Escolha uma receita.";
                Rebuild();
            }
        }

        private void OnInventoryChanged(InventoryChanged e)
        {
            if (e.OwnerId != LocalPlayer.Id) return;
            if (_panel.activeSelf) Rebuild();
        }

        private void Rebuild()
        {
            for (int i = _list.childCount - 1; i >= 0; i--)
                Destroy(_list.GetChild(i).gameObject);

            if (_inventory == null || _recipes == null) return;

            foreach (RecipeDefinition recipe in _recipes)
            {
                if (recipe == null) continue;

                bool canCraft = _inventory.CanCraft(recipe);
                RecipeDefinition captured = recipe;

                Button b = UiFactory.CreateButton(_list, RecipeLabel(recipe), new Vector2(460f, 36f));
                b.interactable = canCraft;
                b.onClick.AddListener(() => Craft(captured));
            }
        }

        // "Tábua x1  ←  2 Madeira(5) , ..." — mostra o resultado e os ingredientes
        // com a quantidade que o jogador tem entre parênteses.
        private string RecipeLabel(RecipeDefinition recipe)
        {
            _sb.Clear();
            string outName = recipe.output != null ? recipe.output.displayName : "?";
            _sb.Append(outName).Append(" x").Append(recipe.outputAmount).Append("   <=   ");

            if (recipe.ingredients != null)
            {
                for (int i = 0; i < recipe.ingredients.Length; i++)
                {
                    RecipeIngredient ing = recipe.ingredients[i];
                    if (ing.item == null) continue;
                    if (i > 0) _sb.Append(" , ");
                    int owned = _inventory != null ? _inventory.CountOf(ing.item) : 0;
                    _sb.Append(ing.amount).Append(' ').Append(ing.item.displayName)
                       .Append('(').Append(owned).Append(')');
                }
            }
            return _sb.ToString();
        }

        private void Craft(RecipeDefinition recipe)
        {
            if (_inventory.TryCraft(recipe))
            {
                string outName = recipe.output != null ? recipe.output.displayName : "item";
                _status.text = $"Craftado: {outName}!";
            }
            else if (!SimulationAuthority.IsAuthority)
            {
                // Cliente no co-op: o TryCraft publicou o pedido; o servidor crafta e o
                // resultado chega pela replicação do inventário (a lista se atualiza sozinha).
                _status.text = "Pedido enviado ao servidor…";
            }
            else
            {
                _status.text = "Não deu para craftar (faltam ingredientes ou espaço).";
            }
            Rebuild();
        }

        private void BuildPanel()
        {
            Image bg = UiFactory.CreatePanel(transform, UiTheme.Card, rounded: true);
            _panel = bg.gameObject;
            _panel.name = "CraftingPainel";

            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = new Vector2(0.36f, 0.18f);
            rt.anchorMax = new Vector2(0.72f, 0.92f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text title = UiFactory.CreateText(_panel.transform, "BANCADA — Crafting (E fecha)", 18, TextAnchor.UpperCenter);
            title.color = UiTheme.Text;
            UiFactory.AddTextShadow(title);
            AnchorTop(title.rectTransform, 16f);

            _status = UiFactory.CreateText(_panel.transform, "", 14, TextAnchor.UpperCenter);
            _status.color = UiTheme.TextDim;
            AnchorTop(_status.rectTransform, 48f);

            var listGo = new GameObject("Lista", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(_panel.transform, false);
            var lrt = (RectTransform)listGo.transform;
            lrt.anchorMin = new Vector2(0.05f, 0.05f);
            lrt.anchorMax = new Vector2(0.95f, 0.82f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var layout = listGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            _list = listGo.transform;
        }

        private static void AnchorTop(RectTransform rt, float fromTop)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -fromTop);
            rt.sizeDelta = new Vector2(-16f, 28f);
        }
    }
}

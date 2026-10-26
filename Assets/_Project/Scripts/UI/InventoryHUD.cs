using UnityEngine;
using UnityEngine.UI;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// Tela do inventário: abre/fecha com a tecla I (via evento) e lista as pilhas
    /// de itens. Reage aos eventos (desacoplada da Simulação) e lê o
    /// <see cref="PlayerInventory"/> do jogador (referência ligada na cena).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryHUD : MonoBehaviour
    {
        [SerializeField] private PlayerInventory _inventory;

        /// <summary>Liga esta tela ao inventário do jogador local (rede, no spawn do dono).</summary>
        public void Bind(PlayerInventory inventory) => _inventory = inventory;

        private GameObject _panel;
        private Transform _list;
        private Text _header;

        private void Awake()
        {
            BuildPanel();
            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus<InventoryToggled>.Subscribe(OnToggle);
            EventBus<InventoryChanged>.Subscribe(OnChanged);
        }

        private void OnDisable()
        {
            EventBus<InventoryToggled>.Unsubscribe(OnToggle);
            EventBus<InventoryChanged>.Unsubscribe(OnChanged);
        }

        private void OnToggle(InventoryToggled e)
        {
            if (e.OwnerId != LocalPlayer.Id) return;
            bool show = !_panel.activeSelf;
            _panel.SetActive(show);
            if (show) Rebuild();
        }

        private void OnChanged(InventoryChanged e)
        {
            if (e.OwnerId != LocalPlayer.Id) return;
            if (_panel.activeSelf) Rebuild();
        }

        private void Rebuild()
        {
            for (int i = _list.childCount - 1; i >= 0; i--)
                Destroy(_list.GetChild(i).gameObject);

            if (_inventory == null) return;

            var slots = _inventory.Slots;
            _header.text = $"INVENTÁRIO ({slots.Count}/{_inventory.Capacity})";

            if (slots.Count == 0)
            {
                Text empty = UiFactory.CreateText(_list, "(vazio — colete recursos com E)", 15, TextAnchor.MiddleLeft);
                empty.color = UiTheme.TextDim;
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                ItemStack stack = slots[i];
                string name = stack.Definition != null ? stack.Definition.displayName : "?";
                UiFactory.CreateText(_list, $"{name}  x{stack.Count}", 16, TextAnchor.MiddleLeft);
            }
        }

        private void BuildPanel()
        {
            Image bg = UiFactory.CreatePanel(transform, UiTheme.Card, rounded: true);
            _panel = bg.gameObject;
            _panel.name = "InventarioPainel";

            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = new Vector2(0.02f, 0.18f);
            rt.anchorMax = new Vector2(0.34f, 0.92f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _header = UiFactory.CreateText(_panel.transform, "INVENTÁRIO", 18, TextAnchor.UpperLeft);
            _header.color = UiTheme.Text;
            UiFactory.AddTextShadow(_header);
            RectTransform hrt = _header.rectTransform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -16f);
            hrt.sizeDelta = new Vector2(-24f, 28f);

            var listGo = new GameObject("Lista", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(_panel.transform, false);
            var lrt = (RectTransform)listGo.transform;
            lrt.anchorMin = new Vector2(0.06f, 0.05f);
            lrt.anchorMax = new Vector2(0.96f, 0.86f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var layout = listGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            _list = listGo.transform;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Building;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// Dica do modo construção (topo da tela): mostra a estrutura atual, se dá para
    /// pagar e os controles. Só aparece no modo construção. Lê o BuildController do
    /// jogador (referência ligada na cena) — só apresentação.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildHUD : MonoBehaviour
    {
        [SerializeField] private BuildController _build;

        /// <summary>Liga esta HUD ao BuildController do jogador local (rede, no spawn do dono).</summary>
        public void Bind(BuildController build) => _build = build;

        private Text _text;

        private void Awake()
        {
            _text = UiFactory.CreateText(transform, "", 18, TextAnchor.UpperCenter);
            RectTransform rt = _text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -56f);
            rt.sizeDelta = new Vector2(0f, 28f);
        }

        private void Update()
        {
            if (_build == null || _text == null) return;

            if (!_build.IsBuilding)
            {
                if (_text.text.Length != 0) _text.text = string.Empty;
                return;
            }

            StructureDefinition def = _build.Current;
            string name = def != null ? def.displayName : "?";
            string afford = _build.CanAffordCurrent ? string.Empty : "  (sem material)";
            _text.text = $"CONSTRUÇÃO: {name}{afford}    [Q troca | F coloca | B sai]";
        }
    }
}

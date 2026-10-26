using System;
using UnityEngine;
using UnityEngine.UI;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// Ficha de atributos: abre/fecha com a tecla C (via evento) e deixa o jogador
    /// distribuir os pontos ganhos por nível entre os 6 atributos. Reage aos
    /// eventos (desacoplada da Simulação) e lê o `PlayerAttributes` do jogador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttributesUI : MonoBehaviour
    {
        [SerializeField] private PlayerAttributes _attributes;
        [SerializeField] private AffinityState _affinity;

        /// <summary>Liga esta ficha aos atributos/afinidade do jogador local (rede, no spawn do dono).</summary>
        public void Bind(PlayerAttributes attributes, AffinityState affinity)
        {
            _attributes = attributes;
            _affinity = affinity;
        }

        // Nomes amigáveis dos 6 atributos (na ordem do enum AttributeType).
        private static readonly string[] Labels =
        {
            "Vida (HP)", "Força", "Inteligência", "Resistência", "Destreza", "Vontade"
        };

        // Nomes dos 6 elementos (na ordem do enum ElementType).
        private static readonly string[] ElementLabels =
        {
            "Metal", "Éter", "Fogo", "Água", "Terra", "Planta"
        };

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
            EventBus<CharacterSheetToggled>.Subscribe(OnToggle);
            EventBus<AttributesChanged>.Subscribe(OnChanged);
            EventBus<AffinityChanged>.Subscribe(OnAffinityChanged);
        }

        private void OnDisable()
        {
            EventBus<CharacterSheetToggled>.Unsubscribe(OnToggle);
            EventBus<AttributesChanged>.Unsubscribe(OnChanged);
            EventBus<AffinityChanged>.Unsubscribe(OnAffinityChanged);
        }

        private void OnToggle(CharacterSheetToggled e)
        {
            if (e.OwnerId != LocalPlayer.Id) return;
            bool show = !_panel.activeSelf;
            _panel.SetActive(show);
            if (show) Rebuild();
        }

        private void OnChanged(AttributesChanged e)
        {
            if (e.OwnerId != LocalPlayer.Id) return;
            if (_panel.activeSelf) Rebuild();
        }

        private void OnAffinityChanged(AffinityChanged e)
        {
            if (e.OwnerId != LocalPlayer.Id) return;
            if (_panel.activeSelf) Rebuild();
        }

        private void Rebuild()
        {
            for (int i = _list.childCount - 1; i >= 0; i--)
                Destroy(_list.GetChild(i).gameObject);

            // Defensivo: sem os atributos ligados não há o que mostrar.
            if (_attributes == null) return;

            _header.text = $"FICHA — nível {_attributes.Level}   •   pontos a distribuir: {_attributes.PointsAvailable}";

            foreach (AttributeType type in (AttributeType[])Enum.GetValues(typeof(AttributeType)))
            {
                AttributeType captured = type;
                int value = _attributes.GetValue(type);
                Button b = UiFactory.CreateButton(_list, $"{Labels[(int)type]}: {value}    [ + ]", new Vector2(360f, 32f));
                b.onClick.AddListener(() => { _attributes.TryInvest(captured); });
            }

            Text hint = UiFactory.CreateText(_list, "Dica: a Mordida rende mais com Força alta.", 14, TextAnchor.MiddleLeft);
            hint.color = UiTheme.TextDim;

            BuildAffinitySection();
        }

        // Roda de afinidade (versão lista): mostra os 6 elementos e seu %. Acima de
        // 60% o elemento vira "especialista" (bônus de dano) — marcado com ★.
        private void BuildAffinitySection()
        {
            if (_affinity == null) return;

            Text affHeader = UiFactory.CreateText(_list, "AFINIDADE", 18, TextAnchor.MiddleLeft);
            affHeader.color = UiTheme.Text;
            UiFactory.AddTextShadow(affHeader);
            foreach (ElementType el in (ElementType[])Enum.GetValues(typeof(ElementType)))
            {
                float value = _affinity.GetAffinity(el);
                bool specialist = value > AffinityMath.SpecialistThreshold;
                string star = specialist ? "  *especialista*" : "";
                Text elLine = UiFactory.CreateText(_list,
                    $"{ElementLabels[(int)el]}: {Mathf.RoundToInt(value)}%{star}", 15, TextAnchor.MiddleLeft);
                elLine.color = specialist ? UiTheme.Resonance : UiTheme.TextDim;
            }
        }

        private void BuildPanel()
        {
            Image bg = UiFactory.CreatePanel(transform, UiTheme.Card, rounded: true);
            _panel = bg.gameObject;
            _panel.name = "FichaPainel";

            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = new Vector2(0.62f, 0.18f);
            rt.anchorMax = new Vector2(0.98f, 0.92f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _header = UiFactory.CreateText(_panel.transform, "FICHA", 18, TextAnchor.UpperLeft);
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
            lrt.anchorMin = new Vector2(0.06f, 0.06f);
            lrt.anchorMax = new Vector2(0.94f, 0.86f);
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
    }
}

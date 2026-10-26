using System.Text;
using UnityEngine;
using UnityEngine.UI;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// Barra das habilidades equipadas (rodapé): mostra os 5 slots, o nível e o
    /// **dano efetivo** de cada habilidade (já com a penalidade suave por atributo),
    /// marcando "..." na recarga. Só apresentação — lê o loadout e os atributos do
    /// jogador (referências ligadas na cena), sem aplicar regras.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityBarHUD : MonoBehaviour
    {
        [SerializeField] private AbilityLoadout _loadout;
        [SerializeField] private PlayerAttributes _attributes;
        [SerializeField] private AffinityState _affinity;

        /// <summary>Liga esta HUD ao jogador local (chamado pela rede no spawn do dono).</summary>
        public void Bind(AbilityLoadout loadout, PlayerAttributes attributes, AffinityState affinity)
        {
            _loadout = loadout;
            _attributes = attributes;
            _affinity = affinity;
        }

        private Text _text;
        private readonly StringBuilder _sb = new StringBuilder(128);

        private void Awake()
        {
            // Cartão (com sombra atrás) no rodapé, ATRÁS dos 5 slots de texto — mesmo tema
            // dos vitais. Criados ANTES do texto para ficarem no fundo (ordem de irmãos).
            Image shadow = UiFactory.CreatePanel(transform, UiTheme.Shadow, rounded: true);
            RectTransform srt = shadow.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0f, 10f);
            srt.sizeDelta = new Vector2(-32f, 44f);

            Image card = UiFactory.CreatePanel(transform, UiTheme.Card, rounded: true);
            RectTransform crt = card.rectTransform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 12f);
            crt.sizeDelta = new Vector2(-40f, 40f);

            _text = UiFactory.CreateText(transform, "", 18, TextAnchor.LowerCenter);
            _text.color = UiTheme.Text;
            UiFactory.AddTextShadow(_text);
            RectTransform rt = _text.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 16f);
            rt.sizeDelta = new Vector2(0f, 28f);
        }

        private void Update()
        {
            if (_loadout == null || _text == null) return;

            _sb.Clear();
            for (int i = 0; i < _loadout.Capacity; i++)
            {
                AbilityInstance inst = _loadout.GetSlot(i);
                _sb.Append('[').Append(i + 1).Append(']');

                if (inst == null)
                {
                    _sb.Append("-  ");
                    continue;
                }

                _sb.Append(inst.Definition.displayName);
                _sb.Append(" Nv").Append(inst.Level);
                if (inst.PointsAvailable > 0) _sb.Append('*'); // tem ponto para gastar no Altar

                _sb.Append(" dano").Append(Mathf.RoundToInt(EffectiveDamage(inst)));
                _sb.Append(inst.IsReady ? "   " : " (...) ");
            }

            _text.text = _sb.ToString();
        }

        // Dano da habilidade já temperado pela penalidade de atributo (M4) e pelo
        // bônus de afinidade do especialista (M5) — espelha o cálculo do AbilityRunner.
        private float EffectiveDamage(AbilityInstance inst)
        {
            AbilityDefinition def = inst.Definition;
            float damage = inst.FinalDamage;

            if (_attributes != null)
            {
                int current = _attributes.GetValue(def.requiredAttribute);
                damage *= AttributeMath.PenaltyMultiplier(def.requiredAttributeValue, current);
            }

            if (_affinity != null)
            {
                damage *= 1f + _affinity.SpecialistBonus(def.element);
            }

            return damage;
        }
    }
}

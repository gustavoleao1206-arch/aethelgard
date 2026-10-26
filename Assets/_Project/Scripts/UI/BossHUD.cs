using UnityEngine;
using UnityEngine.UI;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Boss;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// HUD do boss (Eco Corrompido): barra de vida grande no topo, nome, fase e o elemento a
    /// que ele RESISTE. No tema (UiTheme/UiFactory: cartão + trilho + carmesim + "chip" dourado
    /// + gloss, como o HealthBarHUD). Só APARECE quando o jogador chega perto do boss
    /// (<see cref="_showRange"/>) — some ao afastar ou quando o boss morre. Lê o
    /// <see cref="BossController"/> por referência (desacoplada da simulação); re-busca sozinha
    /// na cena de rede (o boss nasce por spawn do servidor).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossHUD : MonoBehaviour
    {
        [SerializeField] private BossController _boss;
        [Tooltip("A barra do boss só aparece quando o jogador chega a esta distância dele (m).")]
        [SerializeField] private float _showRange = 16f;

        private GameObject _panel;
        private Text _label;
        private RectTransform _fill;
        private RectTransform _trail;
        private Camera _cam;
        private float _nextSearch;
        private float _shown, _trailF;

        private void Awake()
        {
            BuildUI();
            _panel.SetActive(false);
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas.transform.localScale = Vector3.one;
        }

        private void Update()
        {
            // Cena de REDE: a ref nasce vazia (boss = prefab spawnado) → religa sozinha.
            if (_boss == null && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 0.5f;
                _boss = FindAnyObjectByType<BossController>();
            }

            bool show = _boss != null && WithinRange();
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;

            float frac = Mathf.Clamp01(_boss.HealthFraction);
            _shown = Mathf.MoveTowards(_shown, frac, 1.8f * Time.unscaledDeltaTime);
            // chip dourado: escoa devagar ao levar dano; acompanha na hora ao curar (fases)
            _trailF = frac < _trailF ? Mathf.MoveTowards(_trailF, frac, 0.6f * Time.unscaledDeltaTime) : frac;
            SetFrac(_fill, _shown);
            SetFrac(_trail, _trailF);

            string resist = _boss.HasResistedElement
                ? $"   ·   Resiste a {ElementName(_boss.ResistedElement)}"
                : "";
            _label.text = $"{_boss.DisplayName}   ·   Fase {_boss.Phase + 1}{resist}";
        }

        private bool WithinRange()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return true; // sem câmera → não esconde
            Vector3 a = _cam.transform.position; a.y = 0f;
            Vector3 b = _boss.transform.position; b.y = 0f;
            return (a - b).sqrMagnitude <= _showRange * _showRange;
        }

        private static string ElementName(ElementType e)
        {
            switch (e)
            {
                case ElementType.Eter: return "Éter";
                case ElementType.Agua: return "Água";
                default: return e.ToString();
            }
        }

        private void BuildUI()
        {
            _panel = new GameObject("BossPainel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -22f);
            prt.sizeDelta = new Vector2(700f, 68f);

            Image shadow = UiFactory.CreatePanel(prt, UiTheme.Shadow, true); Stretch(shadow.rectTransform, -5f);
            Image card = UiFactory.CreatePanel(prt, UiTheme.Card, true); Stretch(card.rectTransform, 0f);

            // Nome · fase · resistência (metade de cima do cartão).
            _label = UiFactory.CreateText(prt, "Eco Corrompido", 18, TextAnchor.MiddleCenter);
            UiFactory.AddTextShadow(_label);
            _label.color = UiTheme.Text;
            Band(_label.rectTransform, 0.52f, 1f, 0.05f);

            // Barra (metade de baixo): trilho + trail dourado + carmesim + gloss.
            Image track = UiFactory.CreatePanel(prt, UiTheme.Track, true);
            Band(track.rectTransform, 0.10f, 0.48f, 0.03f);
            _trail = UiFactory.CreatePanel(track.rectTransform, UiTheme.HpTrail, true).rectTransform; Fillable(_trail);
            _fill = UiFactory.CreatePanel(track.rectTransform, UiTheme.Hp, true).rectTransform; Fillable(_fill);
            Image gloss = UiFactory.CreatePanel(track.rectTransform, new Color(1f, 1f, 1f, 0.14f), true);
            var g = gloss.rectTransform; g.anchorMin = new Vector2(0f, 0.55f); g.anchorMax = new Vector2(1f, 1f);
            g.offsetMin = Vector2.zero; g.offsetMax = Vector2.zero;
        }

        // trilho/trail/fill preenchem o pai; a largura vem da âncora (SetFrac).
        private static void Fillable(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0.5f);
        }

        private static void SetFrac(RectTransform rt, float f)
        {
            Vector2 a = rt.anchorMax; a.x = Mathf.Clamp01(f); rt.anchorMax = a; rt.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rt, float pad)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
        }

        // Faixa horizontal (0..1 vertical) com margem lateral, dentro do painel.
        private static void Band(RectTransform rt, float yMin, float yMax, float xPad)
        {
            rt.anchorMin = new Vector2(xPad, yMin); rt.anchorMax = new Vector2(1f - xPad, yMax);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}

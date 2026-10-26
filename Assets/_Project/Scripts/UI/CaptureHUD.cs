using UnityEngine;
using UnityEngine.UI;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Capture;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// HUD da captura. Quando o jogador mira numa criatura sincronizável, mostra
    /// "Segurar E: Sincronizar ..." e uma barra que enche enquanto ele segura. Ao
    /// capturar, mostra um aviso rápido. Lê o <see cref="Synchronizer"/> (estado em
    /// tempo real) e escuta <see cref="CreatureCaptured"/> (evento) — desacoplada da
    /// simulação. A barra usa a ÂNCORA do RectTransform (mesmo truque do
    /// HealthBarHUD: robusto sem depender de sprite/Filled).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaptureHUD : MonoBehaviour
    {
        [SerializeField] private Synchronizer _synchronizer;

        /// <summary>Liga esta HUD ao Synchronizer do jogador local (rede, no spawn do dono).</summary>
        public void Bind(Synchronizer synchronizer) => _synchronizer = synchronizer;

        private GameObject _panel;
        private Text _label;
        private RectTransform _barFill;
        private Text _toast;
        private float _toastUntil = -1f;

        private void Awake()
        {
            BuildUI();
            _panel.SetActive(false);
            _toast.gameObject.SetActive(false);

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas.transform.localScale = Vector3.one;
        }

        private void OnEnable() => EventBus<CreatureCaptured>.Subscribe(OnCaptured);
        private void OnDisable() => EventBus<CreatureCaptured>.Unsubscribe(OnCaptured);

        private void OnCaptured(CreatureCaptured e)
        {
            if (e.OwnerId != LocalPlayer.Id) return;
            string nome = e.Source != null ? e.Source.displayName : "Criatura";
            _toast.text = $"Sincronizado: {nome}!  Incorpore no Altar.";
            _toastUntil = Time.time + 3f;
        }

        private void Update()
        {
            bool show = _synchronizer != null && _synchronizer.HasTarget;
            _panel.SetActive(show);
            if (show)
            {
                _label.text = $"Segurar E: Sincronizar {_synchronizer.TargetLabel}";
                Vector2 max = _barFill.anchorMax;
                max.x = _synchronizer.Progress; // largura = progresso do canal
                _barFill.anchorMax = max;
            }

            bool toastOn = Time.time < _toastUntil;
            if (_toast.gameObject.activeSelf != toastOn) _toast.gameObject.SetActive(toastOn);
        }

        // Monta: painel central-inferior com o texto e a barra de canal + um aviso (toast) em cima.
        private void BuildUI()
        {
            // Painel do prompt (centro-inferior).
            Image bg = UiFactory.CreatePanel(transform, UiTheme.Card, rounded: true);
            _panel = bg.gameObject;
            _panel.name = "CapturaPainel";

            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 110f);
            prt.sizeDelta = new Vector2(420f, 64f);

            _label = UiFactory.CreateText(_panel.transform, "Segurar E: Sincronizar", 17, TextAnchor.MiddleCenter);
            _label.color = UiTheme.Text;
            UiFactory.AddTextShadow(_label);
            RectTransform lrt = _label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0.45f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(8f, 0f);
            lrt.offsetMax = new Vector2(-8f, -4f);

            // Fundo da barra (trilho).
            Image barBg = UiFactory.CreatePanel(_panel.transform, UiTheme.Track, rounded: true);
            barBg.gameObject.name = "BarraFundo";
            RectTransform brt = barBg.rectTransform;
            brt.anchorMin = new Vector2(0.05f, 0.12f);
            brt.anchorMax = new Vector2(0.95f, 0.4f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            // Preenchimento (largura pela âncora, como o HealthBarHUD).
            Image fill = UiFactory.CreatePanel(barBg.transform, UiTheme.Resonance, rounded: true);
            fill.gameObject.name = "BarraPreenchimento";
            _barFill = fill.rectTransform;
            _barFill.anchorMin = new Vector2(0f, 0f);
            _barFill.anchorMax = new Vector2(0f, 1f); // x = 0 no início; cresce com o progresso
            _barFill.pivot = new Vector2(0f, 0.5f);
            _barFill.offsetMin = Vector2.zero;
            _barFill.offsetMax = Vector2.zero;

            // Aviso (toast) de captura concluída, mais acima.
            _toast = UiFactory.CreateText(transform, "", 20, TextAnchor.MiddleCenter);
            RectTransform trt = _toast.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 0f);
            trt.anchorMax = new Vector2(0.5f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.anchoredPosition = new Vector2(0f, 190f);
            trt.sizeDelta = new Vector2(620f, 34f);
            _toast.color = UiTheme.Resonance;
            UiFactory.AddTextShadow(_toast);
        }
    }
}

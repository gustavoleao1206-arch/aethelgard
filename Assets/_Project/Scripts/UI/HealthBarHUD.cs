using UnityEngine;
using UnityEngine.UI;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Gameplay.Characters;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// HUD de VITAIS do jogador (VIDA + ESTAMINA), desenhada em código no tema
    /// dark-fantasy + ciano Ressonância (ver <see cref="UiTheme"/>): barras arredondadas,
    /// brilho no topo e um "rastro" dourado de dano atrasado (juice). Não importa arte —
    /// usa o sprite arredondado gerado em <see cref="UiFactory.RoundedSprite"/>.
    ///
    /// Reconstrói os PRÓPRIOS visuais em <see cref="Awake"/> (desligando a barra verde
    /// placeholder que a cena monta), então mexer no estilo é só editar este arquivo —
    /// NÃO precisa regerar a cena, é só dar Play.
    ///
    /// • Vida: reage ao <see cref="HealthChanged"/> filtrando pelo dono LOCAL (co-op OK).
    /// • Estamina: acha a <see cref="Stamina"/> do jogador local (via <see cref="Ownable"/>)
    ///   e reflete <see cref="Stamina.Normalized"/> (ciano; laranja quando zera).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealthBarHUD : MonoBehaviour
    {
        // Campos da cena ANTIGA (barra verde placeholder). Mantidos só para desligá-los
        // em Awake sem quebrar as referências serializadas.
        [SerializeField] private Image _fill;
        [SerializeField] private Image _background;

        private RectTransform _hpFillRt, _hpTrailRt, _stFillRt;
        private Image _stFillImg;
        private Text _hpLabel;
        private Stamina _stamina;
        private float _nextStaminaSearch;

        private float _hpTarget = 1f;    // fração-alvo (vinda do evento)
        private float _hpShown = 1f;     // fração exibida (segue o alvo rápido)
        private float _hpTrailFrac = 1f; // rastro dourado (segue o exibido devagar)
        private float _hpCurrent = 100f, _hpMax = 100f;

        private void Awake()
        {
            // Esconde a barra placeholder antiga (retângulo verde) montada pela cena.
            if (_background != null) _background.gameObject.SetActive(false);
            if (_fill != null) _fill.gameObject.SetActive(false);

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas.transform.localScale = Vector3.one;

            BuildVitals(canvas != null ? canvas.transform : transform);
        }

        private void OnEnable() => EventBus<HealthChanged>.Subscribe(OnHealthChanged);
        private void OnDisable() => EventBus<HealthChanged>.Unsubscribe(OnHealthChanged);

        private void OnHealthChanged(HealthChanged e)
        {
            if (e.OwnerId != LocalPlayer.Id) return; // só a vida do jogador local
            _hpCurrent = e.Current;
            _hpMax = e.Max;
            _hpTarget = e.Max > 0f ? Mathf.Clamp01(e.Current / e.Max) : 0f;
        }

        private void Update()
        {
            // VIDA: o preenchimento segue o alvo rápido; o rastro dourado desce devagar,
            // deixando ver "quanto de vida acabou de sumir" (feedback de dano).
            _hpShown = Mathf.MoveTowards(_hpShown, _hpTarget, 2.2f * Time.deltaTime);
            if (_hpTrailFrac < _hpShown) _hpTrailFrac = _hpShown;                                   // curou: rastro acompanha na hora
            else _hpTrailFrac = Mathf.MoveTowards(_hpTrailFrac, _hpShown, 0.9f * Time.deltaTime);   // levou dano: desce devagar
            SetFrac(_hpFillRt, _hpShown);
            SetFrac(_hpTrailRt, _hpTrailFrac);
            if (_hpLabel != null) _hpLabel.text = Mathf.CeilToInt(_hpCurrent) + " / " + Mathf.CeilToInt(_hpMax);

            // ESTAMINA: acha a do jogador local e reflete Normalized.
            if (_stamina == null && Time.time >= _nextStaminaSearch) FindLocalStamina();
            if (_stamina != null && _stFillRt != null)
            {
                SetFrac(_stFillRt, _stamina.Normalized);
                if (_stFillImg != null)
                    _stFillImg.color = _stamina.IsEmpty ? UiTheme.StaminaEmpty : UiTheme.Stamina;
            }
        }

        private void FindLocalStamina()
        {
            _nextStaminaSearch = Time.time + 0.5f; // tenta de novo em breve se o player ainda não nasceu
            Stamina fallback = null;
            foreach (var s in Object.FindObjectsByType<Stamina>(FindObjectsSortMode.None))
            {
                var own = s.GetComponent<Ownable>();
                if (own != null && own.OwnerId == LocalPlayer.Id) { _stamina = s; return; }
                if (fallback == null) fallback = s;
            }
            _stamina = fallback; // solo / sem Ownable: a única que existe
        }

        // Largura proporcional via âncora (não depende de sprite "Filled").
        private static void SetFrac(RectTransform rt, float frac)
        {
            if (rt == null) return;
            Vector2 max = rt.anchorMax;
            max.x = Mathf.Clamp01(frac);
            rt.anchorMax = max;
        }

        // ---------------- Construção dos visuais ----------------

        private void BuildVitals(Transform root)
        {
            // Sombra + cartão dos vitais (canto superior esquerdo).
            Image shadow = UiFactory.CreatePanel(root, UiTheme.Shadow, rounded: true);
            AnchorTopLeft(shadow.rectTransform, new Vector2(16f, -14f), new Vector2(312f, 82f));

            Image card = UiFactory.CreatePanel(root, UiTheme.Card, rounded: true);
            AnchorTopLeft(card.rectTransform, new Vector2(18f, -16f), new Vector2(306f, 78f));

            // Acento ciano fininho na base do cartão.
            Image accent = UiFactory.CreatePanel(card.transform, UiTheme.Resonance, rounded: true);
            RectTransform ar = accent.rectTransform;
            ar.anchorMin = new Vector2(0f, 0f); ar.anchorMax = new Vector2(1f, 0f);
            ar.pivot = new Vector2(0.5f, 0f);
            ar.offsetMin = new Vector2(12f, 7f); ar.offsetMax = new Vector2(-12f, 10f);

            // Ícone de coração da vida.
            Text heart = UiFactory.CreateText(card.transform, "♥", 24, TextAnchor.MiddleCenter);
            heart.color = UiTheme.Hp; UiFactory.AddTextShadow(heart);
            AnchorTopLeft(heart.rectTransform, new Vector2(10f, -8f), new Vector2(30f, 28f));

            // Barra de VIDA (com rastro dourado) + rótulo numérico.
            RectTransform hpTrack = MakeBar(card.transform, new Vector2(46f, -10f), new Vector2(248f, 22f),
                                            UiTheme.Hp, UiTheme.HpTrail, out _hpTrailRt, out _hpFillRt);
            _hpLabel = UiFactory.CreateText(hpTrack, "100 / 100", 13, TextAnchor.MiddleCenter);
            _hpLabel.color = UiTheme.Text; UiFactory.AddTextShadow(_hpLabel);
            StretchFull(_hpLabel.rectTransform);

            // Barra de ESTAMINA (sem rastro).
            MakeBar(card.transform, new Vector2(46f, -40f), new Vector2(248f, 13f),
                    UiTheme.Stamina, UiTheme.Stamina, out RectTransform stTrail, out _stFillRt);
            if (stTrail != null) stTrail.gameObject.SetActive(false);
            _stFillImg = _stFillRt != null ? _stFillRt.GetComponent<Image>() : null;
        }

        // Trilho arredondado + rastro + preenchimento + brilho. Devolve o RT do trilho.
        private static RectTransform MakeBar(Transform parent, Vector2 pos, Vector2 size,
                                             Color fillColor, Color trailColor,
                                             out RectTransform trailRt, out RectTransform fillRt)
        {
            Image track = UiFactory.CreatePanel(parent, UiTheme.Track, rounded: true);
            RectTransform trackRt = track.rectTransform;
            AnchorTopLeft(trackRt, pos, size);

            trailRt = MakeFill(trackRt, trailColor); // atrás
            fillRt = MakeFill(trackRt, fillColor);   // na frente

            // Brilho (gloss) na metade de cima do preenchimento — escala junto com a barra.
            Image gloss = UiFactory.CreatePanel(fillRt, new Color(1f, 1f, 1f, 0.16f), rounded: true);
            RectTransform gr = gloss.rectTransform;
            gr.anchorMin = new Vector2(0f, 0.52f); gr.anchorMax = new Vector2(1f, 1f);
            gr.offsetMin = new Vector2(3f, 0f); gr.offsetMax = new Vector2(-3f, -2f);

            return trackRt;
        }

        private static RectTransform MakeFill(RectTransform track, Color color)
        {
            Image img = UiFactory.CreatePanel(track, color, rounded: true);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void AnchorTopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}

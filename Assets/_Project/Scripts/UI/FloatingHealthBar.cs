using UnityEngine;
using UnityEngine.UI;
using JogoDaTrupe.Gameplay.Combat;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// Barra de vida FLUTUANTE (world-space) sobre um inimigo — estilo Palworld: acompanha o
    /// mob ao se mover e faz BILLBOARD pra câmera. No tema (UiTheme/UiFactory): trilho escuro +
    /// preenchimento carmesim + "chip" dourado que escoa ao levar dano.
    ///
    /// Lê a vida por POLLING do <see cref="Health"/> do próprio GameObject (o evento global
    /// HealthChanged não identifica a instância — inimigos são NoOwner). Auto-constrói o Canvas
    /// world-space no Awake. Adicionada aos mobs pelo EnemySetup (boss usa a barra do topo).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingHealthBar : MonoBehaviour
    {
        [SerializeField] private float _heightAboveTop = 0.5f; // acima do topo do collider (m)

        private const float BarWorldWidth = 1.3f; // largura em metros
        private const float PxWidth = 130f;
        private const float PxHeight = 18f;

        private Health _health;
        private Camera _cam;
        private Transform _bar;
        private RectTransform _fill;
        private RectTransform _trail;
        private float _topOffset;          // topo do collider relativo ao root (m)
        private float _shown = 1f, _trailF = 1f;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _topOffset = MeasureTop();
            Build();
        }

        // Topo do MODELO visível (renderers) — NÃO do collider (que tem só 2m). Sem isto, num
        // mob grande (o Grunt/"ogro" tem ~4.4m) a barra ficava no torso e sumia atrás do corpo.
        private float MeasureTop()
        {
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                return b.max.y - transform.position.y;
            }
            var col = GetComponent<Collider>();
            return col != null ? col.bounds.max.y - transform.position.y : 2f;
        }

        private void Build()
        {
            var go = new GameObject("FloatingHealthBar", typeof(Canvas));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            _bar = go.transform;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(PxWidth, PxHeight);
            rt.localScale = Vector3.one * (BarWorldWidth / PxWidth); // px → metros

            Image shadow = UiFactory.CreatePanel(rt, UiTheme.Shadow, true); Stretch(shadow.rectTransform, -3f);
            Image track = UiFactory.CreatePanel(rt, UiTheme.Track, true); Stretch(track.rectTransform, 0f);
            _trail = UiFactory.CreatePanel(track.rectTransform, UiTheme.HpTrail, true).rectTransform; Fillable(_trail);
            _fill = UiFactory.CreatePanel(track.rectTransform, UiTheme.Hp, true).rectTransform; Fillable(_fill);
        }

        private void LateUpdate()
        {
            if (_health == null || _bar == null) return;
            if (_cam == null) _cam = Camera.main;

            _bar.position = transform.position + Vector3.up * (_topOffset + _heightAboveTop);
            if (_cam != null)
                _bar.rotation = Quaternion.LookRotation(_bar.position - _cam.transform.position, Vector3.up);

            float target = _health.Max > 0f ? Mathf.Clamp01(_health.Current / _health.Max) : 0f;
            _shown = Mathf.MoveTowards(_shown, target, 2.5f * Time.deltaTime);
            // trail dourado: cai devagar ao levar dano, acompanha na hora ao curar
            _trailF = target < _trailF ? Mathf.MoveTowards(_trailF, target, 0.7f * Time.deltaTime) : target;

            SetFrac(_fill, _shown);
            SetFrac(_trail, _trailF);
        }

        // trilho/trail/fill ocupam todo o track; a largura vem da âncora (SetFrac) — não Image.Filled.
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
    }
}

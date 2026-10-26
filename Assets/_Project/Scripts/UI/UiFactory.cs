using UnityEngine;
using UnityEngine.UI;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// Pequenos auxiliares para montar UI por código (botões e textos), evitando
    /// repetir o mesmo boilerplate em cada tela. A UI do slice é montada em código
    /// (mesmo padrão do HealthBarHUD) para ser repetível e versionável.
    /// </summary>
    public static class UiFactory
    {
        // Fonte embutida da Unity (sem depender de importar um .ttf no projeto).
        public static Font DefaultFont =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Cria um Text simples como filho de <paramref name="parent"/>.</summary>
        public static Text CreateText(Transform parent, string content, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject("Texto", typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>Cria um botão com rótulo. Devolve o Button (use onClick).</summary>
        public static Button CreateButton(Transform parent, string label, Vector2 size)
        {
            var go = new GameObject("Botao", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.18f, 0.2f, 0.28f, 0.95f);
            EnsureSprite(bg);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;

            Text label2 = CreateText(go.transform, label, 16, TextAnchor.MiddleCenter);
            RectTransform lrt = label2.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8f, 4f);
            lrt.offsetMax = new Vector2(-8f, -4f);

            return go.GetComponent<Button>();
        }

        // Uma Image sem sprite não renderiza no Unity 6; garante um sprite branco.
        public static void EnsureSprite(Image image)
        {
            if (image == null || image.sprite != null) return;
            Texture2D tex = Texture2D.whiteTexture;
            image.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        // ---- Tema/HUD bonita (adições) ----

        private static Sprite _rounded;

        /// <summary>
        /// Sprite de canto ARREDONDADO gerado em código (9-slice) — dá cantos suaves nas
        /// barras/painéis sem precisar importar arte, e sem depender de builtin resource
        /// (que às vezes vem nulo em runtime). É gerado uma vez e cacheado.
        /// </summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded == null) _rounded = BuildRoundedSprite();
                return _rounded;
            }
        }

        private static Sprite BuildRoundedSprite()
        {
            const int s = 32, r = 10; // tamanho da textura e raio do canto
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int dx = Mathf.Min(x, s - 1 - x);
                int dy = Mathf.Min(y, s - 1 - y);
                float a = 1f;
                if (dx < r && dy < r) // só os quatro cantos afinam o alpha (antialias na borda)
                {
                    float dist = Mathf.Sqrt((r - dx) * (r - dx) + (r - dy) * (r - dy));
                    a = Mathf.Clamp01(r - dist + 0.5f);
                }
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            // Borda de 9-slice = raio, para os cantos ficarem crocantes em qualquer tamanho.
            return Sprite.Create(tex, new Rect(0f, 0f, s, s), new Vector2(0.5f, 0.5f),
                                 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        /// <summary>Cria um painel (Image) — arredondado (9-slice) ou reto — já colorido e não-clicável.</summary>
        public static Image CreatePanel(Transform parent, Color color, bool rounded)
        {
            var go = new GameObject("Painel", typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            if (rounded)
            {
                img.sprite = RoundedSprite;
                img.type = Image.Type.Sliced;
            }
            else EnsureSprite(img);
            img.color = color;
            img.raycastTarget = false; // HUD passiva não deve bloquear cliques
            return img;
        }

        /// <summary>Sombrinha preta no texto para legibilidade sobre a cena 3D.</summary>
        public static void AddTextShadow(Text text)
        {
            if (text == null) return;
            var sh = text.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.7f);
            sh.effectDistance = new Vector2(1f, -1f);
        }
    }
}

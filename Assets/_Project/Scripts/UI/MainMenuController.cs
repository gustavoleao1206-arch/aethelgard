using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using JogoDaTrupe.Core.Session;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// Menu principal (M-NET-9 P4) — o ponto de entrada do jogo num BUILD. Três opções:
    /// Jogar Solo (carrega a cena singleplayer), Hospedar Co-op e Entrar no Co-op (com um
    /// IP). Grava a intenção no <see cref="SessionRequest"/> e carrega a cena; lá o
    /// <c>SessionAutoStart</c> sobe Host/Client. UI pura (não conhece rede — só grava o
    /// pedido no Core e troca de cena). Graybox, no espírito do slice.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string _soloScene = "VerticalSlice";
        [SerializeField] private string _coopScene = "VerticalSlice_Net";

        private InputField _ipField;

        private void Start() => BuildMenu();

        private void BuildMenu()
        {
            Text title = UiFactory.CreateText(transform, "JOGO DA TRUPE", 40, TextAnchor.MiddleCenter);
            Center(title.rectTransform, 0f, 180f, 600f, 60f);

            Text subtitle = UiFactory.CreateText(transform, "RPG cooperativo — camada de rede (M-NET)", 18, TextAnchor.MiddleCenter);
            Center(subtitle.rectTransform, 0f, 130f, 600f, 30f);

            Button solo = UiFactory.CreateButton(transform, "Jogar Solo", new Vector2(320f, 48f));
            Center(solo.GetComponent<RectTransform>(), 0f, 50f, 320f, 48f);
            solo.onClick.AddListener(PlaySolo);

            Button host = UiFactory.CreateButton(transform, "Hospedar Co-op", new Vector2(320f, 48f));
            Center(host.GetComponent<RectTransform>(), 0f, -10f, 320f, 48f);
            host.onClick.AddListener(HostCoop);

            Button join = UiFactory.CreateButton(transform, "Entrar no Co-op", new Vector2(320f, 48f));
            Center(join.GetComponent<RectTransform>(), 0f, -70f, 320f, 48f);
            join.onClick.AddListener(JoinCoop);

            _ipField = BuildIpField();
            Center(_ipField.GetComponent<RectTransform>(), 0f, -130f, 320f, 40f);
        }

        private void PlaySolo()
        {
            SessionRequest.Mode = SessionMode.None; // solo não tem rede
            SceneManager.LoadScene(_soloScene);
        }

        private void HostCoop()
        {
            SessionRequest.Mode = SessionMode.Host;
            SceneManager.LoadScene(_coopScene);
        }

        private void JoinCoop()
        {
            SessionRequest.Mode = SessionMode.Client;
            SessionRequest.Ip = _ipField != null && !string.IsNullOrEmpty(_ipField.text) ? _ipField.text : "127.0.0.1";
            SceneManager.LoadScene(_coopScene);
        }

        // Campo de IP mínimo (legacy InputField) com placeholder e o localhost como padrão.
        private InputField BuildIpField()
        {
            var go = new GameObject("CampoIP", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(transform, false);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            UiFactory.EnsureSprite(bg);

            Text placeholder = UiFactory.CreateText(go.transform, "IP do anfitrião (padrão 127.0.0.1)", 15, TextAnchor.MiddleCenter);
            Stretch(placeholder.rectTransform);
            placeholder.color = new Color(0.6f, 0.6f, 0.65f);

            Text text = UiFactory.CreateText(go.transform, "", 15, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            text.color = Color.white;

            var field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.text = "127.0.0.1";
            return field;
        }

        private static void Center(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 4f);
            rt.offsetMax = new Vector2(-8f, -4f);
        }
    }
}

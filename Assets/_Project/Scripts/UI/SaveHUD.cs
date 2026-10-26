using UnityEngine;
using UnityEngine.UI;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Saving;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// Aviso rápido de save/load. Escuta GameSaved/GameLoaded e mostra um texto por
    /// alguns segundos. Desacoplada da simulação (só reage a eventos).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveHUD : MonoBehaviour
    {
        private Text _toast;
        private float _until = -1f;

        private void Awake()
        {
            BuildUI();
            _toast.gameObject.SetActive(false);

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas.transform.localScale = Vector3.one;
        }

        private void OnEnable()
        {
            EventBus<GameSaved>.Subscribe(OnSaved);
            EventBus<GameLoaded>.Subscribe(OnLoaded);
        }

        private void OnDisable()
        {
            EventBus<GameSaved>.Unsubscribe(OnSaved);
            EventBus<GameLoaded>.Unsubscribe(OnLoaded);
        }

        private void OnSaved(GameSaved e) =>
            Show(!string.IsNullOrEmpty(e.Message) ? e.Message : "Jogo salvo (F5)");

        private void OnLoaded(GameLoaded e) =>
            Show(!string.IsNullOrEmpty(e.Message) ? e.Message : (e.Ok ? "Jogo carregado (F9)" : "Nenhum save encontrado"));

        private void Show(string msg)
        {
            _toast.text = msg;
            _until = Time.time + 2.5f;
            _toast.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_toast.gameObject.activeSelf && Time.time >= _until)
                _toast.gameObject.SetActive(false);
        }

        private void BuildUI()
        {
            _toast = UiFactory.CreateText(transform, "", 18, TextAnchor.MiddleCenter);
            RectTransform rt = _toast.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 60f);
            rt.sizeDelta = new Vector2(420f, 30f);
            _toast.color = new Color(0.8f, 0.95f, 1f);
        }
    }
}

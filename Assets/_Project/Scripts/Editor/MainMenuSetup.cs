using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using JogoDaTrupe.UI;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Monta a cena "MainMenu" (M-NET-9 P4) — o ponto de entrada do jogo num build:
    /// Canvas + <see cref="MainMenuController"/> (Solo/Hospedar/Entrar) + EventSystem.
    /// Entra como PRIMEIRA cena das Build Settings; as cenas de jogo (VerticalSlice e
    /// VerticalSlice_Net) precisam estar nas Build Settings para o menu carregá-las
    /// (o gerador de cada uma já as adiciona).
    /// </summary>
    public static class MainMenuSetup
    {
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string ScenePath = SceneFolder + "/MainMenu.unity";

        [MenuItem("Jogo da Trupe/Setup/Criar Menu Principal")]
        public static void CreateMainMenu()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Fundo escuro (uma câmera 2D basta; a DefaultGameObjects já traz uma câmera).
            var canvasGo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.transform.localScale = Vector3.one;

            var bgGo = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bg = bgGo.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.10f, 1f);
            UiFactory.EnsureSprite(bg);

            // O controlador do menu (constrói os botões no Start).
            var menuGo = new GameObject("MainMenu", typeof(RectTransform));
            menuGo.transform.SetParent(canvasGo.transform, false);
            var menuRt = (RectTransform)menuGo.transform;
            menuRt.anchorMin = Vector2.zero; menuRt.anchorMax = Vector2.one;
            menuRt.offsetMin = Vector2.zero; menuRt.offsetMax = Vector2.zero;
            menuGo.AddComponent<MainMenuController>();

            EnsureEventSystem();

            SaveAndRegisterScene(scene);
            Debug.Log("[Setup] Menu Principal criado em " + ScenePath + " (Solo/Hospedar/Entrar). É a 1ª cena do build.");
        }

        // EventSystem com o módulo do Input System (o projeto usa o novo Input System).
        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            try { es.AddComponent<InputSystemUIInputModule>(); }
            catch (System.InvalidOperationException)
            {
                // Bug #6 do pacote Input System após o Test Runner: o módulo religa as
                // ações padrão sozinho no Play. Numa 1ª execução pós-compilação não ocorre.
                Debug.LogWarning("[Setup] InputSystemUIInputModule adiou as ações padrão — normal após o Test Runner; religa no Play.");
            }
        }

        private static void SaveAndRegisterScene(Scene scene)
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);

            // MainMenu = PRIMEIRA cena das Build Settings (ponto de entrada do jogo).
            var entries = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            entries.RemoveAll(s => s.path == ScenePath);
            entries.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = entries.ToArray();

            AssetDatabase.SaveAssets();
        }
    }
}

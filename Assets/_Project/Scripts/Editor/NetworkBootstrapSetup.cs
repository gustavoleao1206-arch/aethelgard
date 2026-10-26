using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Monta a cena "Bootstrap" da rede (M-NET-0): só os sistemas globais — um
    /// <c>NetworkManager</c> com <c>UnityTransport</c> —, sem nada visual. É o ponto de
    /// entrada do co-op (a Arquitetura, seção 9, prevê Bootstrap → VerticalSlice + UI).
    ///
    /// O NetworkManager fica IDLE: não inicia sozinho. Em Play, dá para iniciar
    /// Host/Cliente pelos botões que o NetworkManager mostra no Inspector — o smoke test
    /// do M-NET-0 é "compila e inicia Host sem erro". O fluxo de cenas e o spawn de
    /// jogadores entram nos milestones seguintes (M-NET-3 / M-NET-9).
    /// </summary>
    public static class NetworkBootstrapSetup
    {
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string ScenePath = SceneFolder + "/Bootstrap.unity";

        [MenuItem("Jogo da Trupe/Setup/Criar Cena Bootstrap (Rede)")]
        public static void CreateBootstrapScene()
        {
            // Cena vazia: a Bootstrap não tem visual, só sistemas.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject nmGo = new GameObject("NetworkManager");
            NetworkManager nm = nmGo.AddComponent<NetworkManager>();
            UnityTransport transport = nmGo.AddComponent<UnityTransport>();

            // Liga o transporte ao NetworkManager (UnityTransport = UDP via UTP).
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;

            SaveAndRegisterScene(scene);

            Selection.activeGameObject = nmGo;
            Debug.Log("[Setup] Cena Bootstrap (rede) criada em " + ScenePath +
                      ". NetworkManager + UnityTransport prontos (idle).");
        }

        private static void SaveAndRegisterScene(Scene scene)
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);

            // Bootstrap entra como PRIMEIRA cena das Build Settings (ponto de entrada do jogo).
            var entries = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            entries.RemoveAll(s => s.path == ScenePath);
            entries.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = entries.ToArray();

            AssetDatabase.SaveAssets();
        }
    }
}

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using JogoDaTrupe.Core.Identity;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Net;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Monta um PROOF isolado de rede (M-NET-3a): cena de teste com chão, câmera fixa, um
    /// NetworkManager e um prefab de jogador em rede. Em Play, "Start Host" faz um boneco
    /// nascer e andar com WASD (cada cliente controla só o seu). Valida todo o encanamento
    /// de rede (spawn + movimento + sincronização) SEM mexer no jogo real (VerticalSlice).
    /// </summary>
    public static class NetworkTestSetup
    {
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string ScenePath = SceneFolder + "/NetworkTest.unity";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string PrefabPath = PrefabFolder + "/P_NetPlayer.prefab";
        private const string MaterialPath = PrefabFolder + "/M_NetPlayer.mat";

        [MenuItem("Jogo da Trupe/Setup/Criar Cena de Teste de Rede")]
        public static void CreateNetworkTestScene()
        {
            GameObject prefab = CreateNetworkPlayerPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Chão (material de runtime serve em cena; só o prefab precisa de material-asset).
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Chão";
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            EditorMaterials.Paint(ground, new Color(0.3f, 0.35f, 0.3f));

            // Câmera fixa olhando o centro (vê o boneco andar).
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 9f, -9f);
                cam.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            }

            // NetworkManager + transporte + prefab do jogador (auto-spawn por cliente).
            GameObject nmGo = new GameObject("NetworkManager");
            NetworkManager nm = nmGo.AddComponent<NetworkManager>();
            UnityTransport transport = nmGo.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = prefab;

            SaveAndRegisterScene(scene);
            Debug.Log("[Setup] Cena de Teste de Rede criada em " + ScenePath +
                      ". Dê Play e clique 'Start Host' no NetworkManager (Inspector): " +
                      "um boneco nasce e anda com WASD.");
        }

        // Cria (ou recria) o prefab do jogador em rede, com material-asset (persiste no prefab).
        private static GameObject CreateNetworkPlayerPrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            // Material como ASSET — material de runtime (new Material) não serializa dentro
            // de um prefab e sairia magenta. Por isso salvamos um .mat de verdade.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader) { color = new Color(0.2f, 0.45f, 0.85f) };
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
                AssetDatabase.DeleteAsset(MaterialPath);
            AssetDatabase.CreateAsset(mat, MaterialPath);

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temp.name = "P_NetPlayer";
            temp.transform.position = new Vector3(0f, 1.1f, 0f); // nasce com os pés no chão
            Object.DestroyImmediate(temp.GetComponent<CapsuleCollider>());
            temp.GetComponent<Renderer>().sharedMaterial = mat;

            CharacterController cc = temp.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f; cc.center = Vector3.zero;

            // Ordem importa: o NetworkObject primeiro; os NetworkBehaviour exigem-no.
            temp.AddComponent<EntityGuid>();
            temp.AddComponent<Ownable>();
            temp.AddComponent<NetworkObject>();
            temp.AddComponent<NetworkOwnable>();
            temp.AddComponent<NetworkPlayerMover>();
            temp.AddComponent<ClientNetworkTransform>();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                AssetDatabase.DeleteAsset(PrefabPath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static void SaveAndRegisterScene(Scene scene)
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);

            var entries = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!entries.Exists(s => s.path == ScenePath))
                entries.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = entries.ToArray();

            AssetDatabase.SaveAssets();
        }
    }
}

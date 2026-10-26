using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using JogoDaTrupe.Net;
using JogoDaTrupe.Presentation.Camera;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Cria o prefab do PLAYER REAL em rede (<c>P_Player</c>) — o personagem completo
    /// (18 componentes via <see cref="PlayerSceneSetup.AddPlayerComponents"/>) + as 4 peças
    /// de rede (NetworkObject, NetworkOwnable, ClientNetworkTransform, NetworkPlayerSetup).
    ///
    /// E monta uma cena de teste (<c>RealPlayerNetTest</c>): chão + câmera (PlayerCamera) +
    /// NetworkManager apontando para esse prefab. Em Play → Start Host → nasce o personagem
    /// real, jogável (anda relativo à câmera, pula, V alterna 1ª/3ª pessoa). Sem HUD/mundo
    /// ainda — isso vem na integração com a VerticalSlice (P6). É o teste single-host do P2/P3.
    /// </summary>
    public static class PlayerPrefabSetup
    {
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string ScenePath = SceneFolder + "/RealPlayerNetTest.unity";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string PrefabPath = PrefabFolder + "/P_Player.prefab";
        private const string MaterialPath = PrefabFolder + "/M_Player.mat";

        [MenuItem("Jogo da Trupe/Setup/Criar Cena de Teste do Player Real (Rede)")]
        public static void CreateRealPlayerTestScene()
        {
            GameObject prefab = CreatePlayerPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Chão";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            EditorMaterials.Paint(ground, new Color(0.3f, 0.35f, 0.3f));

            // Câmera do jogador na Main Camera (será apontada ao player no spawn do dono).
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam != null && cam.GetComponent<PlayerCamera>() == null)
                cam.gameObject.AddComponent<PlayerCamera>();

            GameObject nmGo = new GameObject("NetworkManager");
            NetworkManager nm = nmGo.AddComponent<NetworkManager>();
            UnityTransport transport = nmGo.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = prefab;

            SaveAndRegisterScene(scene);
            Debug.Log("[Setup] Cena de Teste do Player Real criada em " + ScenePath +
                      ". Play → NetworkManager > Start Host: nasce o personagem real (WASD, pulo, V).");
        }

        // Cria (ou recria) o prefab P_Player. Internal para o gerador da cena de co-op
        // (PlayerSceneSetup) reusar o MESMO prefab de rede — sem duplicar a montagem (DRY).
        internal static GameObject CreatePlayerPrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            // Material-asset CEL-SHADED (JogoDaTrupe/Toon) — direção de arte (05/07/2026).
            Material mat = EditorMaterials.MakeToon(new Color(0.2f, 0.45f, 0.85f));
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
                AssetDatabase.DeleteAsset(MaterialPath);
            AssetDatabase.CreateAsset(mat, MaterialPath);

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temp.name = "P_Player";
            temp.tag = "Player";
            temp.transform.position = new Vector3(0f, 1.1f, 0f); // nasce com os pés no chão
            temp.GetComponent<Renderer>().sharedMaterial = mat;

            // 18 componentes do jogador (CharacterController substitui o CapsuleCollider).
            PlayerSceneSetup.AddPlayerComponents(temp);

            // Peças de rede: NetworkObject primeiro; depois os NetworkBehaviour.
            // NetworkStyleForwarder leva o ritmo/estilo do CLIENTE ao servidor (M-NET-6 P4);
            // NetworkWorldRequestForwarder leva os pedidos de mundo (M-NET-7 P1).
            temp.AddComponent<NetworkObject>();
            temp.AddComponent<NetworkOwnable>();
            temp.AddComponent<ClientNetworkTransform>();
            temp.AddComponent<NetworkPlayerSetup>();
            temp.AddComponent<NetworkStyleForwarder>();
            temp.AddComponent<NetworkWorldRequestForwarder>();
            temp.AddComponent<NetworkInventory>(); // inventário replicado (M-NET-7 P2)
            temp.AddComponent<NetworkBlocker>();    // guarda do escudo replicada (dono escreve, servidor mitiga)

            // Progressão replicada (M-NET-8 P2/P3): memória/atributos/afinidade/loadout
            // autoritativos no servidor, espelhados no dono.
            temp.AddComponent<NetworkMemoryBank>();
            temp.AddComponent<NetworkAttributes>();
            temp.AddComponent<NetworkAffinity>();
            temp.AddComponent<NetworkLoadout>();

            // Liga a lista de estruturas ao BuildController do PREFAB (M-NET-7 P5): no co-op
            // o player nasce daqui, então precisa saber o que construir (a cena de co-op roda
            // CreateBuilding com player=null — só cria os assets). Requer que Structure_Parede/
            // Piso já existam (a cena de co-op os cria antes deste ponto).
            PlayerSceneSetup.WireBuildStructures(temp);

            // Custo do respec no loadout do PREFAB (M-NET-8; 🎨 2 Pedra) — mesmo motivo.
            PlayerSceneSetup.WireRespecCost(temp);

            // Mesh do herói (cápsula → boneco Dungeon Mason + Animator), IGUAL ao caminho solo
            // (CreatePlayer). Sem isto, regerar a cena de co-op ressuscitava a CÁPSULA no P_Player
            // (o "Aplicar herói ao prefab de rede" era um passo manual fácil de esquecer). Agora a
            // geração é auto-contida: o co-op nasce com o herói. O `temp` é cápsula nova a cada
            // geração — o AttachHeroVisual troca o renderer da cápsula pelo mesh, sem sobra.
            HeroSetup.AttachHeroVisual(temp);

            // GUID ESTÁVEL (sem delete): o hash de rede do NGO deriva do GUID do asset, então
            // reusar o P_Player (em vez de deletar+recriar) mantém o hash idêntico entre
            // regerações e entre as 2 instâncias do MPPM. Correção durável — ver NetPrefabTooling
            // e 06_Testes/Melhorias.md. Bônus: acaba com a ref "missing" ao P_Player na cena de
            // rede gerada ANTES (a dívida "Regenerar uma cena invalida o P_Player da outra").
            return NetPrefabTooling.SavePrefabStable(temp, PrefabPath);
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

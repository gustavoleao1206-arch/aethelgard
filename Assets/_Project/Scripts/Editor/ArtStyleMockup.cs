using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>Gatilho de teste dos VFX: dispara um "chuveiro" de ~3s no jogador local
    /// (só em Play), para dar tempo de ver/capturar os efeitos transientes.</summary>
    internal static class VfxTestMenu
    {
        private static double _end;
        private static double _nextAt;

        [MenuItem("Jogo da Trupe/Arte/Testar VFX no jogador (em Play)")]
        public static void TestVfx()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Arte] Entre em Play primeiro — os VFX nascem no jogador ao vivo.");
                return;
            }
            _end = EditorApplication.timeSinceStartup + 15.0; // chuveiro longo p/ dar tempo de ver
            _nextAt = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[Arte] Chuveiro de VFX de teste (~3s) no jogador " + LocalPlayer.Id + ".");
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now >= _end || !Application.isPlaying) { EditorApplication.update -= Tick; return; }
            if (now < _nextAt) return;
            _nextAt = now + 0.28;

            int owner = LocalPlayer.Id;
            AbilityDefinition abil = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(
                "Assets/_Project/Data/Abilities/Ability_PulsoCorrompido.asset");
            if (abil != null)
                EventBus<AbilityUsed>.Publish(new AbilityUsed { OwnerId = owner, Slot = 0, Ability = abil });
            EventBus<EcoCollected>.Publish(new EcoCollected { OwnerId = owner, Source = null });
        }
    }

    /// <summary>
    /// Ferramenta de MOCKUP da fase de arte: renderiza a MESMA cena do jogo (a solo
    /// VerticalSlice, com player/inimigos/boss/criatura autorados) em DOIS estilos —
    /// Toon cel-shaded e Stylized low-poly — e salva um PNG limpo de cada, para o
    /// Diretor escolher a direção visual. É tudo por código (fiel ao jogo, repetível),
    /// no espírito do resto da montagem (ForestWorld/EditorMaterials).
    ///
    /// O estilo é carregado pelos SHADERS (JogoDaTrupe/Toon e JogoDaTrupe/FlatLowPoly)
    /// + luz/névoa/fundo — sem pós-processamento por ora (bloom/color-grading entram
    /// quando o estilo for escolhido, como Volume na cena real). Os materiais de mockup
    /// NÃO são salvos: a cena é reaberta limpa ao fim.
    /// </summary>
    public static class ArtStyleMockup
    {
        private const string SoloScene = "Assets/_Project/Scenes/VerticalSlice.unity";

        [MenuItem("Jogo da Trupe/Arte/Capturar Mockups de Estilo (Toon + Low-Poly)")]
        public static void CaptureBoth()
        {
            if (Shader.Find("JogoDaTrupe/Toon") == null || Shader.Find("JogoDaTrupe/FlatLowPoly") == null)
            {
                Debug.LogError("[Arte] Shaders de estilo não encontrados (JogoDaTrupe/Toon e /FlatLowPoly). " +
                    "Confira Assets/_Project/Art/Shaders e recompile.");
                return;
            }

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "04_Assets", "Mockups"));
            Directory.CreateDirectory(outDir);

            // Força compilação SÍNCRONA dos shaders: sem isso, a Unity 6 pode renderizar a
            // 1ª vez com o placeholder de "compilando em segundo plano" (geometria some no
            // capture offscreen). Restaura no fim.
            bool prevAsync = EditorSettings.asyncShaderCompilation;
            EditorSettings.asyncShaderCompilation = false;
            try
            {
                OpenSolo();
                ApplyToon();
                Capture(Path.Combine(outDir, "estilo_toon.png"), new Color(0.05f, 0.08f, 0.10f));

                OpenSolo(); // reabre limpo (descarta os swaps do toon)
                ApplyLowPoly();
                Capture(Path.Combine(outDir, "estilo_lowpoly.png"), new Color(0.60f, 0.75f, 0.88f));

                OpenSolo(); // deixa a cena solo limpa (mockup não é salvo)
            }
            finally { EditorSettings.asyncShaderCompilation = prevAsync; }
            Debug.Log("[Arte] Mockups salvos em 04_Assets/Mockups (estilo_toon.png, estilo_lowpoly.png). " +
                      "A cena solo foi reaberta limpa — os materiais de mockup não foram gravados.");
        }

        // Previews que aplicam o estilo na cena AO VIVO e enquadram a câmera principal,
        // para inspecionar direto na Game view (caminho de render normal do URP) — útil
        // quando o capture offscreen falha por algum motivo específico do pipeline.
        [MenuItem("Jogo da Trupe/Arte/Preview Toon (Game view)")]
        public static void PreviewToon()
        {
            OpenSolo(); ApplyToon(); FrameMainCamera(new Color(0.05f, 0.08f, 0.10f));
            Debug.Log("[Arte] Preview TOON aplicado na cena (Game view). Não salve a cena.");
        }

        [MenuItem("Jogo da Trupe/Arte/Preview Low-Poly (Game view)")]
        public static void PreviewLowPoly()
        {
            OpenSolo(); ApplyLowPoly(); FrameMainCamera(new Color(0.60f, 0.75f, 0.88f));
            Debug.Log("[Arte] Preview LOW-POLY aplicado na cena (Game view). Não salve a cena.");
        }

        private static void FrameMainCamera(Color bg)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            var pc = cam.GetComponent("PlayerCamera") as MonoBehaviour;
            if (pc != null) pc.enabled = false; // não deixa o follow mexer no enquadramento
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.fieldOfView = 46f;
            cam.farClipPlane = 600f;
            cam.transform.position = new Vector3(4.5f, 5.0f, -10.8f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.5f, 4.0f) - cam.transform.position);
        }

        private static void OpenSolo() => EditorSceneManager.OpenScene(SoloScene, OpenSceneMode.Single);

        // ---- estilo TOON ---------------------------------------------------------
        private static void ApplyToon()
        {
            Light dir = FindDirectional();
            if (dir != null)
            {
                dir.color = new Color(1f, 0.95f, 0.85f);
                dir.intensity = 1.15f;
                dir.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.29f, 0.33f); // frio, mais contraste
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.09f, 0.11f);
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 84f;

            Shader toon = Shader.Find("JogoDaTrupe/Toon");
            var cache = new Dictionary<Color, Material>();
            foreach (Renderer r in AllRenderers())
                r.sharedMaterials = MapMaterials(r.sharedMaterials, cache, c =>
                {
                    Material m = new Material(toon);
                    m.SetColor("_BaseColor", c);
                    if (IsResonance(c)) m.SetColor("_Emission", c * 1.5f); // brilho ciano dos acentos
                    return m;
                });
        }

        // ---- estilo LOW-POLY -----------------------------------------------------
        private static void ApplyLowPoly()
        {
            Light dir = FindDirectional();
            if (dir != null)
            {
                dir.color = new Color(1f, 0.97f, 0.90f);
                dir.intensity = 1.05f;
                dir.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.50f, 0.52f); // claro e suave
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.74f, 0.85f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 120f;

            Shader flat = Shader.Find("JogoDaTrupe/FlatLowPoly");
            var cache = new Dictionary<Color, Material>();
            foreach (Renderer r in AllRenderers())
                r.sharedMaterials = MapMaterials(r.sharedMaterials, cache, c =>
                {
                    Material m = new Material(flat);
                    m.SetColor("_BaseColor", c);
                    return m;
                });
        }

        // ---- helpers -------------------------------------------------------------

        private static Material[] MapMaterials(Material[] mats, Dictionary<Color, Material> cache, System.Func<Color, Material> make)
        {
            var outMats = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                Color c = BaseColorOf(mats[i]);
                if (!cache.TryGetValue(c, out Material m)) { m = make(c); cache[c] = m; }
                outMats[i] = m;
            }
            return outMats;
        }

        // Ciano da Ressonância (colunas do hub, Altar) → ganha emissão no toon.
        private static bool IsResonance(Color c) => c.b > 0.5f && c.g > 0.45f && c.r < 0.42f;

        private static Color BaseColorOf(Material m)
        {
            if (m == null) return Color.gray;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.gray;
        }

        private static IEnumerable<Renderer> AllRenderers()
        {
            var list = new List<Renderer>();
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<Renderer>(true));
            return list;
        }

        private static Light FindDirectional()
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Light l = root.GetComponentInChildren<Light>(true);
                if (l != null && l.type == LightType.Directional) return l;
            }
            return null;
        }

        // Renderiza uma câmera "herói" (3/4, sobre o hub olhando o Altar e a arena na
        // névoa) para uma RenderTexture e grava PNG. Fundo sólido do estilo.
        private static void Capture(string path, Color background)
        {
            var camGO = new GameObject("~MockupCam");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.fieldOfView = 46f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;
            cam.transform.position = new Vector3(4.5f, 5.0f, -10.8f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.5f, 4.0f) - cam.transform.position);

            const int W = 1664, H = 936;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
        }
    }
}

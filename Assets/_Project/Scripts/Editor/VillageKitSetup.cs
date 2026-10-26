using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// SETUP do kit de vilas (Quaternius Medieval Village MegaKit [Standard], CC0 — M-TERR-5).
    /// O importador de FBX cria os materiais URP/Lit certos (texturas achadas por nome em
    /// <c>Textures/</c>), mas EMBUTIDOS em cada FBX: 176 modelos × ~2 = 299 cópias de 11 materiais,
    /// impossíveis de afinar. Este utilitário faz o mesmo que o botão "Extract Materials" do
    /// Inspector, para o kit inteiro: extrai cada material embutido para <c>FBX/Materials/M_*.mat</c>
    /// UMA vez por nome e REMAPEIA todos os FBX para o mesmo asset (11 materiais compartilhados).
    /// Depois conserta os dois que o kit deixa sem textura: <b>MI_Vine</b> (folhas de trepadeira →
    /// T_VineLeaf com recorte alpha) e <b>MI_WindowGlass</b> (vidro → translúcido azulado).
    /// Idempotente; reimporta os FBX remapeados (~1 min). Unity 6: "External Material Location"
    /// do importador está obsoleto — por isso extração + remap explícitos.
    /// </summary>
    public static class VillageKitSetup
    {
        private const string KitRoot = "Assets/Quaternius_MedievalVillage/FBX";
        private const string MaterialsFolder = KitRoot + "/Materials";
        private const string TexturesFolder = "Assets/Quaternius_MedievalVillage/Textures";

        [MenuItem("Jogo da Trupe/Arte/Kit de vilas: extrair materiais compartilhados (URP)")]
        public static void Run()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { KitRoot });
            if (guids.Length == 0)
            {
                Debug.LogWarning("[Arte] Kit de vilas não encontrado em " + KitRoot);
                return;
            }
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder(KitRoot, "Materials");

            var shared = new Dictionary<string, Material>();
            foreach (string g in AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null) shared[m.name] = m;
            }

            int extracted = 0, remapped = 0, reimported = 0;
            var toReimport = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                bool dirty = false;
                foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    // Só materiais realmente embutidos no FBX (os já remapeados não aparecem como sub-assets).
                    if (AssetDatabase.GetAssetPath(embedded) != path) continue;
                    var id = new AssetImporter.SourceAssetIdentifier(embedded);
                    if (shared.TryGetValue(embedded.name, out Material target))
                    {
                        importer.AddRemap(id, target);
                        remapped++;
                    }
                    else
                    {
                        string newPath = MaterialsFolder + "/" + embedded.name + ".mat";
                        string err = AssetDatabase.ExtractAsset(embedded, newPath); // extrai E remapeia este FBX
                        if (!string.IsNullOrEmpty(err)) { Debug.LogWarning("[Arte] " + path + ": " + err); continue; }
                        var created = AssetDatabase.LoadAssetAtPath<Material>(newPath);
                        if (created != null) shared[embedded.name] = created;
                        extracted++;
                    }
                    dirty = true;
                }
                if (dirty) toReimport.Add(path);
            }

            if (toReimport.Count > 0)
            {
                try
                {
                    AssetDatabase.StartAssetEditing();
                    foreach (string p in toReimport) { AssetDatabase.WriteImportSettingsIfDirty(p); AssetDatabase.ImportAsset(p); reimported++; }
                }
                finally { AssetDatabase.StopAssetEditing(); }
            }

            int fixedCount = FixSpecialMaterials(shared);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Arte] Kit de vilas: " + extracted + " materiais extraídos, " + remapped + " remaps, " +
                      reimported + " FBX reimportados; " + shared.Count + " materiais compartilhados em " +
                      MaterialsFolder + "; " + fixedCount + " consertados (vinha/vidro).");
        }

        /// <summary>Trepadeira = folha com recorte alpha; vidro = translúcido. Retorna quantos foram ajustados.</summary>
        private static int FixSpecialMaterials(Dictionary<string, Material> shared)
        {
            int n = 0;
            var leaf = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesFolder + "/T_VineLeaf.png");
            if (shared.TryGetValue("MI_Vine", out Material vine) && leaf != null)
            {
                vine.SetTexture("_BaseMap", leaf);
                vine.SetFloat("_AlphaClip", 1f);
                vine.SetFloat("_Cutoff", 0.5f);
                vine.EnableKeyword("_ALPHATEST_ON");
                vine.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off); // folha vista dos dois lados
                vine.SetFloat("_Smoothness", 0.1f);
                vine.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                EditorUtility.SetDirty(vine);
                n++;
            }

            if (shared.TryGetValue("MI_WindowGlass", out Material glass))
            {
                glass.SetFloat("_Surface", 1f); // Transparent
                glass.SetFloat("_Blend", 0f);   // Alpha
                glass.SetOverrideTag("RenderType", "Transparent");
                glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glass.SetFloat("_ZWrite", 0f);
                glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                glass.SetColor("_BaseColor", new Color(0.62f, 0.80f, 0.92f, 0.45f));
                glass.SetFloat("_Smoothness", 0.9f);
                glass.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                EditorUtility.SetDirty(glass);
                n++;
            }
            return n;
        }
    }
}

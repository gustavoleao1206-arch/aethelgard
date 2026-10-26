using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// AUDITORIA do kit de vilas (Quaternius Medieval Village MegaKit [Standard], CC0 — M-TERR-5).
    /// Para cada FBX do kit escreve tamanho (bounds no espaço do prefab), vértices e materiais
    /// (shader + texturas) num relatório TSV em <c>Temp/</c> (fora do git). Serve para:
    /// (1) montar o CATÁLOGO de módulos do gerador de casas com medidas reais (parede/piso/telhado);
    /// (2) conferir que o importador criou materiais URP (nada rosa) e que toda textura foi achada.
    /// Só lê assets — não altera nada.
    /// </summary>
    public static class VillageKitAudit
    {
        private const string KitRoot = "Assets/Quaternius_MedievalVillage/FBX";
        private const string ReportPath = "Temp/VillageKit_Audit.tsv";

        [MenuItem("Jogo da Trupe/Arte/Auditar kit de vilas (relatorio em Temp)")]
        public static void Run()
        {
            var rows = new StringBuilder();
            var matLines = new SortedDictionary<string, string>();
            var shaderCount = new SortedDictionary<string, int>();
            int models = 0, matsWithoutBase = 0;

            rows.AppendLine("name\tsize_x\tsize_y\tsize_z\tmin_x\tmin_y\tmin_z\tmax_x\tmax_y\tmax_z\tverts\tmaterials");
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { KitRoot });
            foreach (string path in guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                models++;

                bool has = false;
                var b = new Bounds();
                int verts = 0;
                foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    verts += mf.sharedMesh.vertexCount;
                    Bounds mb = mf.sharedMesh.bounds;
                    Matrix4x4 m = mf.transform.localToWorldMatrix; // raiz do asset = identidade
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = new Vector3(
                            (i & 1) == 0 ? mb.min.x : mb.max.x,
                            (i & 2) == 0 ? mb.min.y : mb.max.y,
                            (i & 4) == 0 ? mb.min.z : mb.max.z);
                        Vector3 p = m.MultiplyPoint3x4(corner);
                        if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                        else b.Encapsulate(p);
                    }
                }

                var matNames = new List<string>();
                foreach (Material mat in AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Material>())
                {
                    matNames.Add(mat.name);
                    string shader = mat.shader != null ? mat.shader.name : "(null)";
                    shaderCount[shader] = shaderCount.TryGetValue(shader, out int c) ? c + 1 : 1;
                    Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                    Texture bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                    if (baseMap == null) matsWithoutBase++;
                    string line = shader + "\t" + (baseMap != null ? baseMap.name : "(SEM base)") + "\t" + (bump != null ? bump.name : "-");
                    if (!matLines.ContainsKey(mat.name)) matLines[mat.name] = line;
                }

                string name = Path.GetFileNameWithoutExtension(path);
                rows.AppendLine(string.Join("\t", name,
                    F(b.size.x), F(b.size.y), F(b.size.z),
                    F(b.min.x), F(b.min.y), F(b.min.z),
                    F(b.max.x), F(b.max.y), F(b.max.z),
                    verts.ToString(), string.Join("|", matNames)));
            }

            var sb = new StringBuilder();
            sb.AppendLine("# VillageKit audit — modelos: " + models + ", materiais distintos: " + matLines.Count +
                          ", materiais sem _BaseMap: " + matsWithoutBase);
            sb.AppendLine("# shaders: " + string.Join(", ", shaderCount.Select(kv => kv.Key + "=" + kv.Value)));
            sb.AppendLine("# material\tshader\tbase\tnormal");
            foreach (var kv in matLines) sb.AppendLine("# " + kv.Key + "\t" + kv.Value);
            sb.Append(rows);

            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("[Arte] Auditoria do kit de vilas: " + models + " modelos, " + matLines.Count +
                      " materiais (" + matsWithoutBase + " sem base) -> " + ReportPath);
        }

        private static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}

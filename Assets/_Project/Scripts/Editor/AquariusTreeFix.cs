using UnityEngine;
using UnityEditor;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Conserto pontual do pacote de árvores <b>Aquarius Stylized Trees</b> (arte comprada,
    /// 06/07/2026): as folhas vêm num Shader Graph URP (renderizam certo), mas os materiais
    /// de CASCA/TRONCO ("Bark") vêm no shader <b>Standard do Built-in</b> — que aparece ROSA
    /// no nosso URP. Este utilitário reaponta todo material Standard/Legacy do pacote para
    /// <c>Universal Render Pipeline/Lit</c>, PRESERVANDO a textura e a cor base (só troca o
    /// shader; mantém o visual pintado). Rodar uma vez pelo menu; materiais são assets
    /// compartilhados, então todas as árvores da cena corrigem na hora (sem regerar).
    /// </summary>
    public static class AquariusTreeFix
    {
        // Pacotes de arte comprados/importados que vêm com materiais Built-in (rosa no URP).
        private static readonly string[] PackRoots =
        {
            "Assets/Aquarius_Stylized_Trees",               // árvores/grama
            "Assets/Altar_Ruins_FREE",                      // altar + colunas + ruínas
            "Assets/RPGHero",                               // herói jogável (Dungeon Mason)
            "Assets/Mini Legion Footman PBR HP Polyart",    // mob
            "Assets/Mini Legion Lich PBR HP Polyart",       // mob
            "Assets/Mini Legion Grunt PBR HP Polyart",      // mob
            "Assets/FourEvilDragonsHP",                     // 4 dragões (boss)
        };

        [MenuItem("Jogo da Trupe/Arte/Consertar materiais dos pacotes (URP)")]
        public static void FixTreeMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[Arte] Shader 'Universal Render Pipeline/Lit' não encontrado (URP instalado?).");
                return;
            }
            string[] roots = System.Array.FindAll(PackRoots, AssetDatabase.IsValidFolder);
            if (roots.Length == 0)
            {
                Debug.LogWarning("[Arte] Nenhum pacote de arte encontrado para converter.");
                return;
            }

            int converted = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", roots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null) continue;

                string sn = m.shader.name;
                // Só toca no que está no Built-in (Standard/Legacy) — deixa o Shader Graph das
                // folhas (que já funciona) intacto.
                bool isBuiltIn = sn == "Standard" || sn == "Standard (Specular setup)"
                                 || sn.StartsWith("Legacy Shaders/");
                if (!isBuiltIn) continue;

                Texture mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                Color baseCol = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;

                m.shader = urpLit; // troca o shader preservando o que der
                if (mainTex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", mainTex);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);

                EditorUtility.SetDirty(m);
                converted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Arte] Materiais de árvore Standard→URP/Lit convertidos: {converted}. " +
                      "O rosa deve sumir sem regerar a cena.");
        }
    }
}

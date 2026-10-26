using System.Collections.Generic;
using UnityEngine;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Fábrica ÚNICA de materiais coloridos para a montagem de cena por código (Editor).
    ///
    /// Existe para não duplicar a lógica "achar o shader URP/Lit (com fallback Standard),
    /// criar um Material com a cor e atribuir ao Renderer" — que antes vivia tanto no
    /// <see cref="ForestWorld"/> quanto no <see cref="PlayerSceneSetup"/> (violação de DRY,
    /// ENGINEERING_RULES #1). Agora a política e o cache por cor existem num só lugar.
    ///
    /// O cache evita criar centenas de materiais iguais ao montar o cenário. Chame
    /// <see cref="ResetCache"/> no início de cada geração de cena.
    /// </summary>
    internal static class EditorMaterials
    {
        private static readonly Dictionary<Color, Material> _cache = new Dictionary<Color, Material>();

        /// <summary>Esvazia o cache — chamar no começo de cada montagem de cena.</summary>
        public static void ResetCache() => _cache.Clear();

        /// <summary>
        /// Material CEL-SHADED (shader JogoDaTrupe/Toon — direção de arte escolhida pelo
        /// Diretor em 05/07/2026), da cor pedida, reaproveitado por cor. Os acentos de
        /// Ressonância (ciano do hub/Altar) ganham EMISSÃO para brilhar. Fallback URP/Lit
        /// se o shader Toon não for encontrado (ex.: antes de compilar).
        /// </summary>
        public static Material Get(Color color)
        {
            if (_cache.TryGetValue(color, out Material existing)) return existing;

            Shader shader = Shader.Find("JogoDaTrupe/Toon");
            Material mat;
            if (shader != null)
            {
                mat = new Material(shader);
                mat.SetColor("_BaseColor", color);
                if (IsResonance(color)) mat.SetColor("_Emission", color * 0.5f); // brilho ciano
            }
            else
            {
                Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
                if (fallback == null) fallback = Shader.Find("Standard");
                mat = new Material(fallback) { color = color };
            }
            _cache[color] = mat;
            return mat;
        }

        /// <summary>Cor de Ressonância (ciano do hub/Altar) — recebe emissão no toon.</summary>
        internal static bool IsResonance(Color c) => c.b > 0.5f && c.g > 0.45f && c.r < 0.42f;

        /// <summary>
        /// Cria um material toon NOVO (não cacheado) da cor pedida — para os geradores de
        /// PREFAB que salvam o material como asset (M_*.mat). Emissão nos acentos de
        /// Ressonância. Fallback URP/Lit se o shader Toon não existir ainda.
        /// </summary>
        public static Material MakeToon(Color color)
        {
            Shader shader = Shader.Find("JogoDaTrupe/Toon");
            if (shader == null)
            {
                Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
                if (fallback == null) fallback = Shader.Find("Standard");
                return new Material(fallback) { color = color };
            }
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            if (IsResonance(color)) mat.SetColor("_Emission", color * 0.5f);
            return mat;
        }

        /// <summary>Pinta o Renderer do objeto com a cor pedida (no-op se não houver Renderer).</summary>
        public static void Paint(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Get(color);
        }
    }
}

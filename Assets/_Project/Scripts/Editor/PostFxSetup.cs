using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Pós-processamento da fase de arte (P2, cel-shaded): monta um perfil de Volume com
    /// BLOOM (o brilho da Ressonância — os acentos ciano emissivos passam do threshold e
    /// "glowam"), COLOR GRADING (mais saturação/contraste/exposição), TONEMAPPING neutro e
    /// VIGNETTE (foco + clima noturno). Adiciona um Volume GLOBAL na cena e liga o
    /// pós-processamento na câmera principal. Chamado pela geração de cena (PlayerSceneSetup).
    ///
    /// O perfil é um ASSET reusado (GUID estável) e RECONFIGURADO a cada geração — assim
    /// mudar os valores no código vale na próxima geração, sem quebrar a referência das cenas.
    /// </summary>
    internal static class PostFxSetup
    {
        private const string ProfilePath = "Assets/_Project/Art/JogoDaTrupe_PostFx.asset";

        internal static void Apply(Camera mainCamera)
        {
            VolumeProfile profile = BuildProfile();

            var volGo = new GameObject("PostFX Volume");
            Volume vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
            vol.sharedProfile = profile;

            if (mainCamera != null)
            {
                UniversalAdditionalCameraData data = mainCamera.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }
        }

        // Cria (ou reusa) o perfil e reconfigura os overrides de forma determinística.
        private static VolumeProfile BuildProfile()
        {
            EnsureArtFolder();
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            else
            {
                // Remove os componentes antigos (sub-assets) para reconfigurar do zero.
                var old = new List<VolumeComponent>(profile.components);
                profile.components.Clear();
                foreach (VolumeComponent c in old)
                    if (c != null) { AssetDatabase.RemoveObjectFromAsset(c); UnityEngine.Object.DestroyImmediate(c, true); }
            }

            // Bloom — brilho da Ressonância (acentos ciano emissivos ultrapassam o threshold).
            // SUAVE (ajuste 05/07/2026): threshold mais alto + intensidade menor para o ciano
            // GLOWAR sem estourar (mantém a COR em vez de virar branco no núcleo).
            AddComp<Bloom>(profile, b =>
            {
                b.threshold.Override(0.92f);
                b.intensity.Override(0.5f);
                b.scatter.Override(0.6f);
                b.tint.Override(new Color(0.75f, 0.95f, 1f)); // leve viés ciano
            });

            // Color grading — punch estilizado: mais saturação/contraste + leve exposição.
            AddComp<ColorAdjustments>(profile, c =>
            {
                c.postExposure.Override(0.06f);
                c.contrast.Override(12f);
                c.saturation.Override(18f);
            });

            // Tonemapping neutro (mantém as cores estilizadas sem estourar o realce).
            AddComp<Tonemapping>(profile, t => t.mode.Override(TonemappingMode.Neutral));

            // Vignette — foco e clima noturno de Aethelgard.
            AddComp<Vignette>(profile, v =>
            {
                v.intensity.Override(0.3f);
                v.smoothness.Override(0.4f);
            });

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void AddComp<T>(VolumeProfile profile, Action<T> configure) where T : VolumeComponent
        {
            T comp = profile.Add<T>();
            comp.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(comp, profile);
            configure(comp);
        }

        private static void EnsureArtFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                AssetDatabase.CreateFolder("Assets/_Project", "Art");
        }
    }
}

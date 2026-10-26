using UnityEditor;
using UnityEngine;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// SONDA do kit de vilas (M-TERR-5, ferramenta de Debug): monta UMA casa 4×4 m com módulos crus
    /// do Quaternius MegaKit num canto livre do mapa, só para CONFERIR no Editor as convenções de
    /// pivô/orientação de cada módulo (para que lado a parede é "fora", onde o telhado assenta,
    /// escala do FBX, materiais URP). O que se aprende aqui vira a spec do gerador de casas
    /// (<c>HouseBlueprint</c>). Não faz parte da geração de cena; apagar a "Sonda_Casa" depois.
    /// </summary>
    public static class VillageKitProbe
    {
        private const string RootName = "Sonda_Casa (kit de vilas)";
        // No platô da zona de Coleta (plano, cota 0): fora dos platôs o relevo tem até 8 m e a casa
        // fica enterrada (1ª tentativa em (-22, 0, 0) sumiu dentro de um morro).
        private static readonly Vector3 Origin = new Vector3(-11.3f, 0f, 11.3f);

        [MenuItem("Jogo da Trupe/Debug/Kit de vilas: montar casa-sonda 4x4")]
        public static void Build()
        {
            Remove();
            var root = new GameObject(RootName);
            root.transform.position = Origin;

            // Piso 2×2 células (cada Floor = 2×2 m, pivô no centro)
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -1; iz <= 1; iz += 2)
                    Place(root, "Floor_WoodDark", new Vector3(ix, 0f, iz), 0f);

            // Paredes: pivô no centro da base do segmento de 2 m; hipótese = lado -Z do módulo é o EXTERIOR
            Place(root, "Wall_Plaster_Door_Round",        new Vector3(-1f, 0f, -2f), 0f);   // sul (frente), com porta
            Place(root, "Wall_Plaster_Straight",          new Vector3( 1f, 0f, -2f), 0f);
            Place(root, "Wall_Plaster_Window_Wide_Round", new Vector3(-1f, 0f,  2f), 180f); // norte
            Place(root, "Wall_Plaster_Window_Wide_Round", new Vector3( 1f, 0f,  2f), 180f);
            Place(root, "Wall_UnevenBrick_Straight",      new Vector3(-2f, 0f, -1f), 90f);  // oeste (tijolo p/ comparar)
            Place(root, "Wall_UnevenBrick_Straight",      new Vector3(-2f, 0f,  1f), 90f);
            Place(root, "Wall_Plaster_Window_Thin_Round", new Vector3( 2f, 0f, -1f), -90f); // leste
            Place(root, "Wall_Plaster_Window_Thin_Round", new Vector3( 2f, 0f,  1f), -90f);

            // Cantos (postes)
            Place(root, "Corner_Exterior_Wood", new Vector3(-2f, 0f, -2f), 0f);
            Place(root, "Corner_Exterior_Wood", new Vector3( 2f, 0f, -2f), 0f);
            Place(root, "Corner_Exterior_Wood", new Vector3(-2f, 0f,  2f), 0f);
            Place(root, "Corner_Exterior_Wood", new Vector3( 2f, 0f,  2f), 0f);

            // Porta (folha + batente) e janelas (vidro/esquadria) e venezianas no MESMO pivô da parede
            Place(root, "DoorFrame_Round_WoodDark", new Vector3(-1f, 0f, -2f), 0f);
            Place(root, "Door_1_Round",             new Vector3(-1.55f, 0f, -2f), 0f); // folha: pivô na dobradiça (x 0..1.1)
            Place(root, "Window_Wide_Round1",       new Vector3(-1f, 0f,  2f), 180f);
            Place(root, "Window_Wide_Round1",       new Vector3( 1f, 0f,  2f), 180f);
            Place(root, "WindowShutters_Wide_Round_Open", new Vector3(1f, 0f, 2f), 180f);
            Place(root, "Window_Thin_Round1",       new Vector3( 2f, 0f, -1f), -90f);
            Place(root, "Window_Thin_Round1",       new Vector3( 2f, 0f,  1f), -90f);

            // Telhado inteiro 4×4 + empenas (frente/fundo) + chaminé; pé-direito = 3 m
            Place(root, "Roof_RoundTiles_4x4", new Vector3(0f, 3f, 0f), 0f);
            Place(root, "Roof_Front_Brick4",   new Vector3(0f, 3f, -2f), 0f);
            Place(root, "Roof_Front_Brick4",   new Vector3(0f, 3f,  2f), 180f);
            Place(root, "Prop_Chimney",        new Vector3(1.2f, 3f, 0.4f), 0f);

            // Adereços de quintal
            Place(root, "Prop_Crate", new Vector3(3.2f, 0f, -1f), 20f);
            Place(root, "Prop_WoodenFence_Single", new Vector3(0f, 0f, -4f), 0f);

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("[Sonda] Casa 4x4 do kit montada em " + Origin + " (" + root.transform.childCount + " módulos).");
        }

        [MenuItem("Jogo da Trupe/Debug/Kit de vilas: remover casa-sonda")]
        public static void Remove()
        {
            GameObject old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
        }

        /// <summary>
        /// Fotografa a casa-sonda de 3 ângulos (isométrica frente-esquerda, fundo-direita e topo) para
        /// <c>Temp/Sonda_*.png</c> — conferência sem depender de navegar a Scene view na mão.
        /// </summary>
        [MenuItem("Jogo da Trupe/Debug/Kit de vilas: fotografar casa-sonda (Temp)")]
        public static void Snapshot()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null) { Debug.LogWarning("[Sonda] Monte a casa-sonda primeiro."); return; }
            Vector3 c = root.transform.position + new Vector3(0f, 2f, 0f);
            Shoot("Sonda_FrenteEsq", c + new Vector3(-7f, 5f, -9f), c);
            Shoot("Sonda_FundoDir",  c + new Vector3( 8f, 5f,  8f), c);
            Shoot("Sonda_Topo",      c + new Vector3(0.01f, 14f, 0f), c);
            Shoot("Sonda_Porta",     c + new Vector3(-1f, -0.5f, -6f), c + new Vector3(-1f, -1f, 0f));
            Debug.Log("[Sonda] Fotos em Temp/Sonda_*.png");
        }

        /// <summary>
        /// Fotografa as duas VILAS geradas na cena (com o cenário em volta) para <c>Temp/Vila_*.png</c>:
        /// conferência rápida do M-TERR-5 sem navegar a Scene view (e prévia para o Diretor).
        /// </summary>
        [MenuItem("Jogo da Trupe/Debug/Kit de vilas: fotografar vilas da cena (Temp)")]
        public static void SnapshotVillages()
        {
            int shots = 0;
            foreach (string vila in new[] { "Vila Oeste", "Vila Leste" })
            {
                GameObject go = GameObject.Find(vila);
                if (go == null) { Debug.LogWarning("[Sonda] Não achei '" + vila + "' na cena (regenerou?)."); continue; }
                Vector3 c = go.transform.position + new Vector3(0f, 1.5f, 0f);
                string tag = vila.Replace(" ", "");
                Shoot(tag + "_Alta",   c + new Vector3(-14f, 16f, -14f), c, isolate: false);
                Shoot(tag + "_Praca",  c + new Vector3(0.5f, 1.6f, -7f), c + new Vector3(0f, 0.5f, 0f), isolate: false);
                Shoot(tag + "_Estrada", c + new Vector3(go.transform.position.x < 0f ? 16f : -16f, 3f, 0.5f), c, isolate: false);
                shots += 3;
            }
            Debug.Log("[Sonda] " + shots + " fotos das vilas em Temp/Vila*_*.png");
        }

        private static void Shoot(string name, Vector3 from, Vector3 lookAt, bool isolate = true)
        {
            var go = new GameObject("SondaCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.transform.position = from;
            cam.transform.LookAt(lookAt);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            if (isolate)
            {
                cam.cullingMask = 1 << ProbeLayer;            // só a casa-sonda
                cam.clearFlags = CameraClearFlags.SolidColor;  // fundo neutro em vez do skybox
                cam.backgroundColor = new Color(0.55f, 0.65f, 0.80f);
            }
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes("Temp/" + name + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null; // solta a RT antes de destruí-la (senão a Unity reclama)
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }

        private static void Place(GameObject root, string module, Vector3 localPos, float yaw)
        {
            // Mesma âncora do VillageBuilder (a raiz do FBX do Blender carrega a rotação de eixos;
            // sobrescrevê-la deita o módulo — 1ª foto da sonda saiu com o telhado em pé).
            GameObject anchor = VillageBuilder.PlaceModule(root.transform, module, localPos, yaw);
            if (anchor == null) return;
            foreach (Transform t in anchor.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = ProbeLayer; // isola a foto (a câmera da sonda só enxerga esta layer)
        }

        // Layer built-in "TransparentFX" (1): só para a câmera da sonda fotografar a casa SEM as
        // árvores/cenário em volta (a zona de Coleta é um bosque fechado). Nada mais usa essa layer.
        private const int ProbeLayer = 1;
    }
}

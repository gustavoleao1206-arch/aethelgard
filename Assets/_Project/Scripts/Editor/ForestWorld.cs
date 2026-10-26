using UnityEngine;
using UnityEditor;
using JogoDaTrupe.Core.Utils;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Gameplay.World;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Constrói o MUNDO do Vertical Slice — a "Floresta da Ressonância" (decisão criativa
    /// do Diretor, 30/06/2026): ruínas antigas tomadas pela natureza, à meia-luz, com
    /// névoa (o tom de mistério de Aethelgard após o Grande Silêncio).
    ///
    /// Faz só o CENÁRIO (chão, limites, plataforma do hub, arena do boss, árvores, rochas,
    /// ruínas, luz e névoa). Os SISTEMAS (player, inimigos, itens, boss…) continuam sendo
    /// montados pelo <see cref="PlayerSceneSetup"/>, que posiciona tudo lendo de
    /// <see cref="MapLayout"/>. Esta separação inicia a quebra documentada do antigo
    /// "PlayerSceneSetup gigante" (ver 06_Testes/Melhorias.md).
    ///
    /// As ÁRVORES usam modelos reais do pacote Aquarius Stylized Trees (arte comprada,
    /// 06/07/2026); rochas/ruínas/colunas seguem "graybox temático" com primitivos (URP).
    /// O layout é DETERMINÍSTICO (semente fixa) — repetível e versionável, como o resto
    /// da montagem por código.
    ///
    /// Política de colliders (para a IA por transform não travar): só o ANEL-LIMITE e as
    /// colunas da arena bloqueiam; vegetação, rochas, ruínas e os pisos decorativos são
    /// puramente visuais (sem collider).
    /// </summary>
    internal static class ForestWorld
    {
        // Paleta da Floresta da Ressonância.
        private static readonly Color ForestFloor = new Color(0.18f, 0.24f, 0.15f); // fallback (sem o pacote)
        private static readonly Color TrunkColor  = new Color(0.32f, 0.22f, 0.13f);
        private static readonly Color RockColor   = new Color(0.40f, 0.42f, 0.40f);
        private static readonly Color RuinColor   = new Color(0.55f, 0.55f, 0.48f);
        private static readonly Color HubStone    = new Color(0.50f, 0.50f, 0.46f);
        private static readonly Color HubAccent   = new Color(0.30f, 0.62f, 0.64f); // ressonância (ciano)
        private static readonly Color ArenaFloor  = new Color(0.22f, 0.12f, 0.22f); // corrompido
        private static readonly Color ArenaPillar = new Color(0.30f, 0.16f, 0.32f);
        private static readonly Color CliffColor  = new Color(0.22f, 0.22f, 0.24f);

        private static Transform _decorRoot;

        public static void Build()
        {
            EditorMaterials.ResetCache();
            WarnIfArenaTooCloseToBorder();

            GameObject world = new GameObject("World (Floresta da Ressonância)");
            _decorRoot = new GameObject("Decor").transform;
            _decorRoot.SetParent(world.transform, false);

            CreateGround(world.transform);
            ConfigureLighting();
            CreateHubPlatform(world.transform);
            CreateBossArena(world.transform);
            CreateBorderRing(world.transform);
            CreateLake(world.transform); // espelho d'água na bacia do sul (Milestone do Terreno)
            CreatePaths(world.transform); // fita de trilha sobre os corredores (M-TERR-4)
            CreateVillages(world.transform); // vilas a oeste e a leste nos pads planos (M-TERR-5)
            ScatterScenery();
        }

        // Guarda geométrica: garante que a arena (com suas colunas) cabe dentro do
        // anel-limite com folga. Falha ALTO (LogError) na geração da cena se alguém
        // reposicionar a arena/limite no MapLayout de forma a (re)criar interpenetração
        // de colliders (achados SCENE-01/02 da revisão de 30/06/2026).
        private static void WarnIfArenaTooCloseToBorder()
        {
            float arenaCenterDist = new Vector2(MapLayout.BossArena.x, MapLayout.BossArena.z).magnitude;
            float arenaOuter = arenaCenterDist + MapLayout.ArenaRadius
                             + MapLayout.ArenaPillarOffset + MapLayout.ArenaPillarHalfWidth;
            float borderInner = MapLayout.BorderRadius - MapLayout.CliffDepth * 0.5f;
            const float folga = 2f;
            if (arenaOuter + folga > borderInner)
                Debug.LogError($"[Mapa] A arena do boss está perto demais do anel-limite " +
                    $"(folga < {folga} m): borda externa da arena = {arenaOuter:0.0}, " +
                    $"face interna do limite = {borderInner:0.0}. Ajuste o MapLayout " +
                    $"(BorderRadius/GroundHalfExtent maiores, BossArena ao sul, ou ArenaRadius menor).");
        }

        // Chão grande de "chão de floresta" (cobre todo o anel-limite).
        private static void CreateGround(Transform parent)
        {
            // CHÃO ESCULPIDO (Milestone do Terreno): no lugar do Plane liso, uma malha gerada a
            // partir de WorldHeight.SampleHeight — platôs planos nas zonas, relevo dramático entre
            // elas, bacia do lago ao sul. O GroundMeshBuilder (ExecuteAlways) constrói a malha
            // AGORA, então o MeshCollider já existe para o bake da NavMesh logo adiante. Nada de
            // asset de Mesh: seria um binário de MB reescrito a cada regeneração.
            var ground = new GameObject("Chão da Floresta");
            ground.transform.SetParent(parent, false);
            ground.AddComponent<GroundMeshBuilder>();

            // Chão TEXTURIZADO (grama estilizada tile-able do pacote Aquarius) no lugar da cor
            // chapada — mata o "chão de limbo" (pedido do Diretor). Fallback = cor chapada.
            // O UV da malha vai 0..1 no chão inteiro (igual ao Plane), então a repetição a cada
            // ~4 m do EnsureGroundMaterial continua valendo sem mudança.
            Material groundMat = EnsureGroundMaterial();
            if (groundMat != null) ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
            else Paint(ground, ForestFloor);
        }

        // Cria (ou reusa, GUID estável) o material de chão de grama estilizada TILE-ABLE do
        // pacote Aquarius, com a grama repetindo ao longo do plano grande. Null sem o pacote
        // (aí o chão cai na cor chapada de fallback).
        private static Material EnsureGroundMaterial()
        {
            const string albedoPath = "Assets/Aquarius_Stylized_Trees/Textures/Tile-able Plane A_Stylized_Grass_1A1_AlbedoTransparency.png";
            Texture albedo = AssetDatabase.LoadAssetAtPath<Texture>(albedoPath);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (albedo == null || urpLit == null) return null;

            const string matPath = "Assets/_Project/Art/M_GroundGrass.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Art");
                m = new Material(urpLit);
                AssetDatabase.CreateAsset(m, matPath);
            }
            else if (m.shader != urpLit) m.shader = urpLit;

            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", new Color(0.42f, 0.58f, 0.38f)); // verde de grama (não lavar a textura)
            m.SetFloat("_Smoothness", 0.0f); // grama FOSCA (sem brilho especular que lava pra branco)
            // Repete a grama a cada ~4 m ao longo do plano (o UV do Plane vai 0-1 no plano todo).
            float tiles = Mathf.Max(4f, Mathf.Round(MapLayout.GroundHalfExtent * 2f / 4f));
            m.SetTextureScale("_BaseMap", new Vector2(tiles, tiles));
            EditorUtility.SetDirty(m);
            return m;
        }

        // Luz baixa e quente + névoa esverdeada = "meia-luz" e mistério.
        private static void ConfigureLighting()
        {
            // A cena acabou de ser criada por código (DefaultGameObjects): a luz padrão é
            // um objeto-raiz. Varredura de raízes evita a API FindObjectsByType(SortMode),
            // deprecada na Unity 6.5 (warning CS0618 no Console).
            Light dir = null;
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.TryGetComponent(out Light l) && l.type == LightType.Directional) { dir = l; break; }

            if (dir == null)
            {
                GameObject go = new GameObject("Directional Light", typeof(Light));
                dir = go.GetComponent<Light>();
                dir.type = LightType.Directional;
            }

            // Atmosfera de DIA (pedido do Diretor 06/07/2026 — melhor visibilidade na criação
            // do mapa; o jogo terá ciclo dia/noite depois): sol forte e quente, ambiente claro,
            // névoa leve só ao longe. Céu procedural de dia (ver EnsureSkyboxMaterial).
            dir.color = new Color(1.0f, 0.97f, 0.90f);
            dir.intensity = 1.4f;
            dir.transform.rotation = Quaternion.Euler(55f, -35f, 0f); // sol alto (dia)

            // Ambiente de dia (claro; a luz vem do céu procedural + este flat de base).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.60f);

            // Névoa: dá profundidade, esconde o limite e reforça o mistério (teal escuro).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.80f, 0.88f); // haze de dia (clara)
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 170f; // só ao longe (mundo bem visível)

            // Céu de gradiente (noite teal de Aethelgard) no lugar do fundo chapado — dá
            // profundidade e combina com a névoa. Ver JogoDaTrupe/SkyboxGradient.
            Material sky = EnsureSkyboxMaterial();
            if (sky != null) RenderSettings.skybox = sky;
        }

        // Céu de DIA. Preferência: um céu HAND-PAINTED do pacote Fantasy Skybox FREE (escolha
        // do Diretor, 06/07/2026). Fallback: céu procedural (Skybox/Procedural — azul + sol).
        // O jogo terá ciclo dia/noite depois (o pacote tem Dia/Pôr-do-sol/Noite p/ trocar).
        private static Material EnsureSkyboxMaterial()
        {
            Material fantasy = LoadFantasyDaySky();
            if (fantasy != null) return fantasy;

            const string path = "Assets/_Project/Art/M_SkyDay.mat";
            Shader sky = Shader.Find("Skybox/Procedural");
            if (sky == null) return null; // sem shader: mantém o skybox atual

            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Art");
                m = new Material(sky);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != sky) m.shader = sky;

            // Céu de dia agradável (reconfigura sempre, determinístico).
            if (m.HasProperty("_SunSize"))             m.SetFloat("_SunSize", 0.045f);
            if (m.HasProperty("_AtmosphereThickness")) m.SetFloat("_AtmosphereThickness", 1.1f);
            if (m.HasProperty("_SkyTint"))             m.SetColor("_SkyTint", new Color(0.55f, 0.68f, 0.92f));
            if (m.HasProperty("_GroundColor"))         m.SetColor("_GroundColor", new Color(0.46f, 0.47f, 0.45f));
            if (m.HasProperty("_Exposure"))            m.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Céu de DIA do pacote Fantasy Skybox FREE (material de cubemap pronto). Null se o
        // pacote não está presente (aí cai no céu procedural). O pacote tem 10+ céus de dia
        // (FS000_Day_01..06, FS002/003/013/017_Day) — trocar o nome aqui muda o céu.
        private const string DaySkyName = "FS000_Day_03";
        private static Material LoadFantasyDaySky()
        {
            const string folder = "Assets/Fantasy Skybox FREE";
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (string guid in AssetDatabase.FindAssets(DaySkyName + " t:Material", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path).Equals(DaySkyName, System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            return null;
        }

        // Centro do jogo = o "ALTAR CIRCLE" do pacote Altar Ruins (referência do Diretor,
        // 06/07/2026): disco rúnico (Platform_circle) + anel de ARCOS de pedra SÓLIDOS + entulho.
        // O Altar (com glow) vem à parte no CreateAltar. Fallback graybox sem o pacote.
        private static void CreateHubPlatform(Transform parent)
        {
            GameObject hub = new GameObject("Hub (Marco da Ressonância)");
            hub.transform.SetParent(parent, false);
            var rng = new System.Random(101);
            Vector3 hubGround = MapLayout.At(MapLayout.Hub, 0f, 0f, 0f);

            // Disco central: modelo Platform_circle (o disco rúnico do altar-circle), escalado
            // pra cobrir o hub. Sem collider (o chão é a superfície andável). Fallback: cilindro.
            GameObject[] platMod = LoadAltarModels("Platform_circle");
            if (platMod.Length > 0)
            {
                var plat = (GameObject)PrefabUtility.InstantiatePrefab(platMod[0]);
                plat.name = "Plataforma (Altar Circle)";
                plat.transform.SetParent(hub.transform, false);
                plat.transform.position = hubGround;
                float diam = Mathf.Max(0.01f, Mathf.Max(ModelBounds(plat).size.x, ModelBounds(plat).size.z));
                plat.transform.localScale = Vector3.one * (MapLayout.HubRadius * 2.1f / diam);
                plat.transform.position += new Vector3(0f, hubGround.y - ModelBounds(plat).min.y, 0f);
                AddMeshColliders(plat); // SÓLIDO: o player fica EM CIMA do disco (não afunda)
            }
            else
            {
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Plataforma";
                disc.transform.SetParent(hub.transform, false);
                disc.transform.position = MapLayout.At(MapLayout.Hub, 0f, 0.06f, 0f);
                disc.transform.localScale = new Vector3(MapLayout.HubRadius * 2f, 0.06f, MapLayout.HubRadius * 2f);
                Paint(disc, HubStone);
                NoCollider(disc);
            }

            // Anel de 8 ARCOS de pedra (o anel do altar-circle) — SÓLIDOS: o player NÃO atravessa
            // (mesh colliders). Fallback: colunas de pedra, senão cubos.
            int count = 8;
            GameObject[] arches = RuinArchModels();
            GameObject[] columns = HubColumnModels();
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f;
                float r = MapLayout.HubRadius + 0.4f;
                float yRot = ang * Mathf.Rad2Deg + 90f; // arco de frente pro centro
                Vector3 g = Ground(MapLayout.At(MapLayout.Hub, Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
                if (PlaceModelFitHeight(arches, hub.transform, g, yRot, 3.6f, true, rng) != null) continue;
                if (PlaceModelFitHeight(columns, hub.transform, g, yRot, 2.6f, true, rng) != null) continue;

                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube); // fallback graybox (sólido)
                p.name = "Coluna";
                p.transform.SetParent(hub.transform, false);
                p.transform.position = Ground(MapLayout.At(MapLayout.Hub, Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r), 1.3f);
                p.transform.localScale = new Vector3(0.6f, 2.6f, 0.6f);
                p.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
                Paint(p, RuinColor);
            }

            // Entulho espalhado no disco (Rubble/Brick) — ambientação baixa, sem collider.
            GameObject[] rubble = LoadAltarModels("Rubble_1", "Rubble_2", "Rubble_3", "Rubble_4", "Brick");
            for (int i = 0; i < 10; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float rr = (float)rng.NextDouble() * (MapLayout.HubRadius - 1f);
                Vector3 g = Ground(MapLayout.At(MapLayout.Hub, Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr));
                PlaceModelFitHeight(rubble, hub.transform, g, (float)rng.NextDouble() * 360f,
                                    0.4f + (float)rng.NextDouble() * 0.4f, false, rng);
            }
        }

        // Arena do boss: piso corrompido + anel de colunas escuras com uma entrada ao sul.
        private static void CreateBossArena(Transform parent)
        {
            GameObject arena = new GameObject("Arena do Boss");
            arena.transform.SetParent(parent, false);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Piso Corrompido";
            floor.transform.SetParent(arena.transform, false);
            floor.transform.position = MapLayout.At(MapLayout.BossArena, 0f, 0.07f, 0f);
            floor.transform.localScale = new Vector3(MapLayout.ArenaRadius * 2f, 0.07f, MapLayout.ArenaRadius * 2f);
            Paint(floor, ArenaFloor);
            NoCollider(floor);

            // Colunas no perímetro (com collider = "parede" da arena). Pula as do sul
            // (viradas para o hub) para deixar a entrada por onde o jogador chega.
            int count = 12;
            float r = MapLayout.ArenaRadius + MapLayout.ArenaPillarOffset;
            float w = MapLayout.ArenaPillarHalfWidth * 2f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                // Sul = ângulo ~270° (-Z, lado do hub). Deixa um vão de ~3 colunas.
                float ang = t * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                if (dir.z < -0.55f) continue; // vão de entrada ao sul

                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = "Coluna Corrompida";
                p.transform.SetParent(arena.transform, false);
                p.transform.position = Ground(MapLayout.At(MapLayout.BossArena, dir.x * r, 0f, dir.z * r), 3f);
                p.transform.localScale = new Vector3(w, 6f, w);
                Paint(p, ArenaPillar); // mantém collider (parede)
            }
        }

        // Anel de penhascos altos que fecha o mapa (com collider — prende o jogador dentro).
        private static void CreateBorderRing(Transform parent)
        {
            GameObject ring = new GameObject("Limite do Mapa");
            ring.transform.SetParent(parent, false);

            int count = 44; // sobrepostos → sem brechas para o jogador escapar
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                GameObject cliff = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cliff.name = "Penhasco";
                cliff.transform.SetParent(ring.transform, false);
                cliff.transform.position = Ground(dir * MapLayout.BorderRadius, 4.5f); // muro assentado na borda elevada
                cliff.transform.rotation = Quaternion.LookRotation(-dir); // face larga tangente
                cliff.transform.localScale = new Vector3(6.5f, 9f, MapLayout.CliffDepth);
                Paint(cliff, CliffColor); // mantém collider (muro)
            }
        }

        // VILAS (Milestone do Terreno, M-TERR-5): duas vilas do kit Quaternius nos pads planos a oeste e
        // a leste (MapLayout.VillageWest/East, cota 0). A estrada de cada uma chega do hub: a oeste ela
        // entra pelo lado +X (0°), a leste pelo lado −X (180°) — nenhuma casa nasce nesse setor.
        private static void CreateVillages(Transform parent)
        {
            VillageBuilder.Build(parent, MapLayout.VillageWest, "Vila Oeste", 5101, roadAngleDeg: 0f);
            VillageBuilder.Build(parent, MapLayout.VillageEast, "Vila Leste", 5102, roadAngleDeg: 180f);
        }

        // CAMINHOS (Milestone do Terreno, M-TERR-4): a fita de trilha (PathMeshBuilder) drapeia a
        // malha do PathRibbon sobre as polilinhas do WorldPaths — as MESMAS que achatam o relevo.
        // Sem collider (o chão por baixo é quem colide); não entra no bake da NavMesh.
        private static void CreatePaths(Transform parent)
        {
            var paths = new GameObject("Caminhos");
            paths.transform.SetParent(parent, false);
            paths.AddComponent<PathMeshBuilder>();
            Material pathMat = EnsurePathMaterial();
            if (pathMat != null) paths.GetComponent<MeshRenderer>().sharedMaterial = pathMat;
            else Paint(paths, TrunkColor);
        }

        // Material de TERRA da trilha (URP/Lit, GUID estável) com a textura "Forest Dirt Ground A" do
        // MESMO pacote da grama (Aquarius) — estilo consistente. O UV da fita já repete a cada ~4 m
        // ao longo do comprimento (uvRepeat), então a escala da textura fica em (1, 1). Fosca, como a
        // grama (brilho especular lava pra branco). Null sem a textura/URP (fallback = cor chapada).
        private static Material EnsurePathMaterial()
        {
            const string albedoPath = "Assets/Aquarius_Stylized_Trees/Textures/Forest Dirt Ground A.png";
            Texture albedo = AssetDatabase.LoadAssetAtPath<Texture>(albedoPath);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (albedo == null || urpLit == null) return null;

            const string matPath = "Assets/_Project/Art/M_Path.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Art");
                m = new Material(urpLit);
                AssetDatabase.CreateAsset(m, matPath);
            }
            else if (m.shader != urpLit) m.shader = urpLit;

            m.SetTexture("_BaseMap", albedo);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", new Color(0.78f, 0.66f, 0.50f)); // terra batida, um tom mais quente que a grama
            m.SetFloat("_Smoothness", 0.0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // LAGO (Milestone do Terreno, M-TERR-3): a BACIA já está esculpida no relevo pelo
        // WorldHeight (centro/raio/fundo/cota vivem lá); aqui entra só o ESPELHO D'ÁGUA — um
        // plano translúcido na cota LakeWaterLevel. O plano é MAIOR que a bacia de propósito:
        // onde o terreno está acima da água, o chão (opaco) o esconde, e a margem nasce sozinha
        // onde o relevo cruza a cota. Sem collider: 1ª passada = o jogador vadeia (bloqueio/
        // natação em água funda é polish futuro). Rochas na linha d'água e arbustos na margem.
        private static void CreateLake(Transform parent)
        {
            var lake = new GameObject("Lago");
            lake.transform.SetParent(parent, false);

            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Espelho d'água";
            water.transform.SetParent(lake.transform, false);
            water.transform.position = new Vector3(WorldHeight.LakeCenterX, WorldHeight.LakeWaterLevel, WorldHeight.LakeCenterZ);
            float size = (WorldHeight.LakeRadius * 2f + 4f) / 10f; // Plane = 10 m a escala 1; folga p/ a margem
            water.transform.localScale = new Vector3(size, 1f, size);
            Material waterMat = EnsureWaterMaterial();
            if (waterMat != null) water.GetComponent<MeshRenderer>().sharedMaterial = waterMat;
            else Paint(water, HubAccent);
            NoCollider(water); // vadeável

            // Margem: rochas na linha d'água (parcialmente submersas) e arbustos na parte seca.
            var rng = new System.Random(3030);
            for (int i = 0; i < 14; i++)
            {
                bool rock = i % 2 == 0;
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float f = rock ? 0.45f + (float)rng.NextDouble() * 0.15f : 0.62f + (float)rng.NextDouble() * 0.23f;
                float r = WorldHeight.LakeRadius * f;
                Vector3 p = new Vector3(WorldHeight.LakeCenterX + Mathf.Cos(a) * r, 0f, WorldHeight.LakeCenterZ + Mathf.Sin(a) * r);
                if (rock) Rock(p, 0.5f + (float)rng.NextDouble() * 0.9f, rng); else Bush(p, rng);
            }
        }

        // Material de ÁGUA (URP/Lit translúcido, GUID estável). O Water_Plane_A.mat do pacote
        // Aquarius usa o shader Standard (built-in) → ROSA no URP (mesma pegadinha das árvores);
        // fazemos o nosso com a cor teal dele. Flat e translúcido casa com o cel-shaded (sem
        // normal map/ondas — polish futuro). Null sem URP/Lit (fallback = cor chapada).
        private static Material EnsureWaterMaterial()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return null;

            const string matPath = "Assets/_Project/Art/M_Water.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Art");
                m = new Material(urpLit);
                AssetDatabase.CreateAsset(m, matPath);
            }
            else if (m.shader != urpLit) m.shader = urpLit;

            // Superfície TRANSPARENTE no URP/Lit: _Surface=1 + blend alpha + sem ZWrite + fila Transparent.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(0.60f, 0.91f, 1.0f, 0.55f)); // teal do Water_Plane_A, um pouco mais opaco
            m.SetFloat("_Smoothness", 0.85f); // brilho de espelho d'água
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Espalha árvores, rochas e ruínas pelo mundo (clusters por zona + borda da mata).
        private static void ScatterScenery()
        {
            var rng = new System.Random(2026);

            // Cluster de mata DENSA em cada zona (pedido do Diretor; centro de gameplay livre).
            TreeCluster(MapLayout.ZoneColeta, 13, 5.5f, rng);
            TreeCluster(MapLayout.ZoneCombate, 8, 6f, rng);
            TreeCluster(MapLayout.ZoneAutomacao, 10, 5.5f, rng);
            TreeCluster(MapLayout.ZoneCriatura, 17, 5.5f, rng); // bosque fechado (Criatura de Planta)

            // Borda da floresta: anel espesso de árvores dentro do limite = parede de mata escura.
            int edge = 46;
            for (int i = 0; i < edge; i++)
            {
                float ang = (i / (float)edge) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.1f;
                float r = MapLayout.BorderRadius - 3f - (float)rng.NextDouble() * 3f;
                Vector3 pos = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (InsideArena(pos) || InsideLake(pos) || InsideVillage(pos)) continue; // nem a arena (norte) nem o lago (sul)
                Tree(pos, 1.1f + (float)rng.NextDouble() * 0.8f, rng);
            }

            // Rochas espalhadas (musgo da floresta).
            for (int i = 0; i < 30; i++)
            {
                Vector3 pos = RandomGroundPoint(rng, 8f, MapLayout.BorderRadius - 4f);
                if (InsideArena(pos) || InsideLake(pos) || InsideVillage(pos)) continue;
                Rock(pos, 0.6f + (float)rng.NextDouble() * 1.2f, rng);
            }

            // Vegetação rasteira (arbustos do pacote) espalhada pelo chão — quebra o "chão liso
            // de limbo" e adensa a mata. No-op sem o pacote (o graybox não tinha rasteira).
            for (int i = 0; i < 70; i++)
            {
                Vector3 pos = RandomGroundPoint(rng, 5f, MapLayout.BorderRadius - 3f);
                if (InsideArena(pos) || InsideLake(pos) || InsideVillage(pos)) continue;
                if (Geometry2D.InsideCircleXZ(pos, MapLayout.Hub, MapLayout.HubRadius + 1.5f)) continue; // hub livre
                Bush(pos, rng);
            }

            // Ruínas: colunas quebradas ao longo da trilha do hub até a arena (coordenadas
            // na fonte única MapLayout). A "Era da Ressonância" engolida pela mata.
            foreach (Vector3 p in MapLayout.RuinTrail)
                RuinPillar(p, rng);
        }

        // ---- helpers de cenário -------------------------------------------------

        private static void TreeCluster(Vector3 center, int count, float spread, System.Random rng)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 2.5f + (float)rng.NextDouble() * spread; // deixa o centro da zona livre
                Vector3 pos = center + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                Tree(pos, 0.9f + (float)rng.NextDouble() * 0.9f, rng);
            }
        }

        // Árvore da Floresta: um prefab REAL do pacote Aquarius Stylized Trees (arte comprada
        // pelo Diretor, 06/07/2026), com rotação e escala variadas. O clima sombrio/teal vem
        // da NOSSA atmosfera (luz + névoa + skybox), então as árvores entram como estão. Sem o
        // pacote, cai no fallback de árvore-primitivo (mantém a cena gerável em testes/sem asset).
        // Decorativa: remove colliders (a IA anda por transform e não pode travar na mata).
        private static void Tree(Vector3 basePos, float scale, System.Random rng)
        {
            GameObject[] prefabs = TreePrefabs();
            if (prefabs.Length == 0) { PrimitiveTree(basePos, scale, rng); return; }

            GameObject prefab = prefabs[rng.Next(prefabs.Length)];
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tree.transform.SetParent(_decorRoot, false);
            tree.transform.position = Ground(basePos); // pé da árvore no relevo
            tree.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            float s = TreeBaseScale * (0.85f + (float)rng.NextDouble() * 0.45f); // variação natural
            tree.transform.localScale = new Vector3(s, s, s);
            AddTrunkCollider(tree); // o player esbarra no tronco (sólido)
        }

        // Collider FINO de tronco (cápsula vertical) numa árvore — o player esbarra, sem mesh
        // collider pesado (perf) nem travar tanto a IA. Dimensionado pelos bounds do modelo.
        private static void AddTrunkCollider(GameObject tree)
        {
            foreach (Collider c in tree.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            Bounds b = ModelBounds(tree);
            float scale = Mathf.Max(0.0001f, tree.transform.lossyScale.x);
            var cap = tree.AddComponent<CapsuleCollider>();
            cap.direction = 1; // eixo Y (tronco vertical)
            cap.center = new Vector3(0f, tree.transform.InverseTransformPoint(b.center).y, 0f);
            cap.height = b.size.y / scale;
            cap.radius = 0.35f / scale; // ~0.35 m de raio no mundo
        }

        // Multiplicador base da escala das árvores do pacote (ajuste fino do tamanho no mundo).
        private const float TreeBaseScale = 1.0f;

        // Carrega (uma vez) os prefabs de árvore CHEIA do pacote (Birch, Fir, Acacia — sem as
        // "Fir Snowy", cuja neve branca destoaria da noite teal). Vazio se o pacote não existe.
        private static GameObject[] _treePrefabsCache;
        private static bool _treePrefabsLoaded;
        private static GameObject[] TreePrefabs()
        {
            if (_treePrefabsLoaded) return _treePrefabsCache;
            _treePrefabsLoaded = true;

            string[] folders =
            {
                "Assets/Aquarius_Stylized_Trees/Prefabs/Trees/Set 1/Birch",
                "Assets/Aquarius_Stylized_Trees/Prefabs/Trees/Set 1/Fir",
                "Assets/Aquarius_Stylized_Trees/Prefabs/Trees/Set 1/Acacia",
            };
            var valid = new System.Collections.Generic.List<string>();
            foreach (string f in folders)
                if (AssetDatabase.IsValidFolder(f)) valid.Add(f);

            var list = new System.Collections.Generic.List<GameObject>();
            if (valid.Count > 0)
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", valid.ToArray()))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (go != null) list.Add(go);
                }
            _treePrefabsCache = list.ToArray();
            if (_treePrefabsCache.Length == 0)
                Debug.LogWarning("[Mapa] Pacote de árvores (Aquarius) não encontrado — usando árvores-primitivo (fallback).");
            return _treePrefabsCache;
        }

        // Remove todos os colliders da instância (árvore = cenário puramente visual).
        private static void StripColliders(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
        }

        // Arbustos/vegetação rasteira do pacote (undergrowth), carregados uma vez. Vazio sem o pacote.
        private static GameObject[] _bushPrefabsCache;
        private static bool _bushPrefabsLoaded;
        private static GameObject[] BushPrefabs()
        {
            if (_bushPrefabsLoaded) return _bushPrefabsCache;
            _bushPrefabsLoaded = true;
            const string folder = "Assets/Aquarius_Stylized_Trees/Prefabs/Trees/Set 1/Foliage, Bushes";
            var list = new System.Collections.Generic.List<GameObject>();
            if (AssetDatabase.IsValidFolder(folder))
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (go != null) list.Add(go);
                }
            _bushPrefabsCache = list.ToArray();
            return _bushPrefabsCache;
        }

        // Arbusto = prefab do pacote, escala baixa e rotação aleatória, sem collider. No-op sem o pacote.
        private static void Bush(Vector3 basePos, System.Random rng)
        {
            GameObject[] prefabs = BushPrefabs();
            if (prefabs.Length == 0) return;
            var bush = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rng.Next(prefabs.Length)]);
            bush.transform.SetParent(_decorRoot, false);
            bush.transform.position = Ground(basePos);
            bush.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            float s = 0.7f + (float)rng.NextDouble() * 0.6f;
            bush.transform.localScale = new Vector3(s, s, s);
            StripColliders(bush);
        }

        // Fallback SEM o pacote de árvores: tronco (cilindro) + 1-2 copas (esferas). Decorativa.
        private static void PrimitiveTree(Vector3 basePos, float scale, System.Random rng)
        {
            float trunkH = 2.2f * scale;
            float groundY = Ground(basePos).y; // pé da árvore no relevo
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Árvore";
            trunk.transform.SetParent(_decorRoot, false);
            trunk.transform.position = new Vector3(basePos.x, groundY + trunkH * 0.5f, basePos.z);
            trunk.transform.localScale = new Vector3(0.35f * scale, trunkH * 0.5f, 0.35f * scale);
            Paint(trunk, TrunkColor);
            NoCollider(trunk);

            int blobs = 1 + rng.Next(0, 2);
            Color foliage = new Color(0.14f + (float)rng.NextDouble() * 0.08f,
                                      0.36f + (float)rng.NextDouble() * 0.12f,
                                      0.18f + (float)rng.NextDouble() * 0.07f);
            for (int b = 0; b < blobs; b++)
            {
                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaf.name = "Copa";
                // Filha do Decor (escala 1), NÃO do tronco — senão herdaria a escala
                // não-uniforme do tronco (alto e fino) e a copa ficaria distorcida.
                leaf.transform.SetParent(_decorRoot, false);
                float crown = groundY + trunkH + (b * 0.7f * scale);
                leaf.transform.position = new Vector3(
                    basePos.x + ((float)rng.NextDouble() - 0.5f) * 0.6f,
                    crown,
                    basePos.z + ((float)rng.NextDouble() - 0.5f) * 0.6f);
                float s = (1.8f - b * 0.4f) * scale;
                leaf.transform.localScale = new Vector3(s, s, s);
                Paint(leaf, foliage);
                NoCollider(leaf);
            }
        }

        private static void Rock(Vector3 pos, float scale, System.Random rng)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rocha";
            rock.transform.SetParent(_decorRoot, false);
            rock.transform.position = Ground(pos, scale * 0.3f); // semi-enterrada no relevo
            rock.transform.localScale = new Vector3(scale, scale * 0.6f, scale * 0.85f);
            rock.transform.rotation = Quaternion.Euler(
                (float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f);
            Paint(rock, RockColor); // mantém o SphereCollider do primitivo → o player esbarra na rocha
        }

        // Coluna de ruína: quebrada e levemente tombada (sem collider — só ambientação).
        private static void RuinPillar(Vector3 basePos, System.Random rng)
        {
            // Ruína = modelo do pacote (arco/coluna quebrada/tijolo), variado. Fallback: cubo.
            if (PlaceRuinModel(RuinTrailModels(), _decorRoot, Ground(basePos),
                    (float)rng.NextDouble() * 360f, 0.28f + (float)rng.NextDouble() * 0.16f, rng))
                return;

            float h = 2.2f + (float)rng.NextDouble() * 1.8f;
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Ruína";
            pillar.transform.SetParent(_decorRoot, false);
            pillar.transform.position = Ground(basePos, h * 0.5f);
            pillar.transform.localScale = new Vector3(0.8f, h, 0.8f);
            pillar.transform.rotation = Quaternion.Euler(
                (float)rng.NextDouble() * 8f - 4f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 8f - 4f);
            Paint(pillar, RuinColor);
            NoCollider(pillar);
        }

        // Verdadeiro dentro de um dos pads das VILAS (M-TERR-5, +1 m de folga): as casas nascem lá;
        // árvore/rocha/arbusto aleatório não.
        private static bool InsideVillage(Vector3 pos)
            => Geometry2D.InsideCircleXZ(pos, MapLayout.VillageWest, MapLayout.VillageRadius + 1f)
            || Geometry2D.InsideCircleXZ(pos, MapLayout.VillageEast, MapLayout.VillageRadius + 1f);

        // Verdadeiro dentro da bacia do lago (raio do WorldHeight): cenário não nasce debaixo d'água.
        private static bool InsideLake(Vector3 pos)
            => Geometry2D.InsideCircleXZ(pos, new Vector3(WorldHeight.LakeCenterX, 0f, WorldHeight.LakeCenterZ), WorldHeight.LakeRadius);

        // Verdadeiro se a posição cai dentro (ou na margem) da arena do boss — usado
        // para não espalhar cenário em cima da luta de chefe. Geometria pura testável.
        private static bool InsideArena(Vector3 pos)
            => Geometry2D.InsideCircleXZ(pos, MapLayout.BossArena, MapLayout.ArenaRadius + 2.5f);

        // Assenta um ponto (x, z) no RELEVO (Milestone do Terreno). Antes o chão era um plano em
        // Y=0 e todo objeto era posto num Y literal; agora a altura vem da MESMA função que gera a
        // malha do chão (WorldHeight) — malha e cenário nunca divergem. O Y de entrada é ignorado.
        private static Vector3 Ground(Vector3 xz, float yOffset = 0f)
            => WorldHeight.OnGround(xz.x, xz.z, yOffset);

        // Ponto aleatório no chão dentro de um anel [min,max] do centro, fora do hub.
        private static Vector3 RandomGroundPoint(System.Random rng, float min, float max)
        {
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = min + (float)rng.NextDouble() * (max - min);
            return new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
        }

        // ---- modelos de coluna/ruína do pacote Altar Ruins (arte, 06/07/2026) -----------

        private static GameObject[] _hubColumnCache; private static bool _hubColumnLoaded;
        private static GameObject[] HubColumnModels()
        {
            if (_hubColumnLoaded) return _hubColumnCache;
            _hubColumnLoaded = true;
            _hubColumnCache = LoadAltarModels("Broken_column", "Column", "Column_broken_small");
            return _hubColumnCache;
        }

        private static GameObject[] _ruinTrailCache; private static bool _ruinTrailLoaded;
        private static GameObject[] RuinTrailModels()
        {
            if (_ruinTrailLoaded) return _ruinTrailCache;
            _ruinTrailLoaded = true;
            _ruinTrailCache = LoadAltarModels("Arch_1", "Arch_2", "Arch_3", "Broken_column", "Column_broken_small", "Brick");
            return _ruinTrailCache;
        }

        // Carrega modelos por NOME EXATO do pacote Altar Ruins. Vazio sem o pacote (fallback graybox).
        private static GameObject[] LoadAltarModels(params string[] names)
        {
            const string folder = "Assets/Altar_Ruins_FREE";
            var list = new System.Collections.Generic.List<GameObject>();
            if (AssetDatabase.IsValidFolder(folder))
                foreach (string n in names)
                    foreach (string guid in AssetDatabase.FindAssets(n + " t:GameObject", new[] { folder }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (System.IO.Path.GetFileNameWithoutExtension(path).Equals(n, System.StringComparison.OrdinalIgnoreCase))
                        { list.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path)); break; }
                    }
            return list.ToArray();
        }

        // Instancia um modelo de ruína (aleatório do conjunto), assentado no chão. false se vazio.
        private static bool PlaceRuinModel(GameObject[] models, Transform parent, Vector3 pos, float yRotDeg, float scale, System.Random rng)
        {
            if (models.Length == 0) return false;
            var m = (GameObject)PrefabUtility.InstantiatePrefab(models[rng.Next(models.Length)]);
            m.transform.SetParent(parent, false);
            m.transform.position = pos;
            m.transform.rotation = Quaternion.Euler(0f, yRotDeg, 0f);
            m.transform.localScale = Vector3.one * scale;
            StripColliders(m);
            return true;
        }

        private static GameObject[] _archCache; private static bool _archLoaded;
        private static GameObject[] RuinArchModels()
        {
            if (_archLoaded) return _archCache;
            _archLoaded = true;
            _archCache = LoadAltarModels("Arch_1", "Arch_2", "Arch_3", "Arch_4", "Arch_5");
            return _archCache;
        }

        // Adiciona MeshCollider SÓLIDO a cada mesh do modelo — estrutura que o player NÃO
        // atravessa (arcos/colunas do centro). Limpa colliders herdados antes. Oposto de StripColliders.
        private static void AddMeshColliders(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null)
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        // Bounds (mundo) combinados dos renderers do modelo — pra medir tamanho e reassentar a base.
        private static Bounds ModelBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        // Instancia um modelo (aleatório do conjunto), ESCALA pra bater uma ALTURA-alvo (m) —
        // mede os bounds, então nunca fica gigante/minúsculo — e assenta a base no chão.
        // solid=true → mesh colliders (player não atravessa). Null se o conjunto está vazio.
        private static GameObject PlaceModelFitHeight(GameObject[] models, Transform parent, Vector3 groundPos, float yRotDeg, float targetH, bool solid, System.Random rng)
        {
            if (models.Length == 0) return null;
            var m = (GameObject)PrefabUtility.InstantiatePrefab(models[rng.Next(models.Length)]);
            m.transform.SetParent(parent, false);
            m.transform.rotation = Quaternion.Euler(0f, yRotDeg, 0f);
            m.transform.position = groundPos;
            float h = Mathf.Max(0.01f, ModelBounds(m).size.y);
            m.transform.localScale = Vector3.one * (targetH / h);
            m.transform.position += new Vector3(0f, groundPos.y - ModelBounds(m).min.y, 0f); // base no chão
            if (solid) AddMeshColliders(m); else StripColliders(m);
            return m;
        }

        // ---- helpers de material ------------------------------------------------

        // Delega à fábrica única de materiais de Editor (evita duplicar a lógica de
        // shader/material — ENGINEERING_RULES #1).
        private static void Paint(GameObject go, Color color) => EditorMaterials.Paint(go, color);

        private static void NoCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }
    }
}

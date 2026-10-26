using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using JogoDaTrupe.Core.Identity;
using JogoDaTrupe.Core.Ownership;
using UnityEngine.UI;
using JogoDaTrupe.Input;
using JogoDaTrupe.Gameplay.Characters;
using JogoDaTrupe.Gameplay.Interaction;
using JogoDaTrupe.Gameplay.Combat;
using JogoDaTrupe.Gameplay.Enemies;
using JogoDaTrupe.Gameplay.Incorporation;
using JogoDaTrupe.Gameplay.Inventory;
using JogoDaTrupe.Gameplay.Crafting;
using JogoDaTrupe.Gameplay.Building;
using JogoDaTrupe.Gameplay.Automation;
using JogoDaTrupe.Gameplay.Capture;
using JogoDaTrupe.Gameplay.Boss;
using JogoDaTrupe.Gameplay.Saving;
using JogoDaTrupe.Presentation.Camera;
using JogoDaTrupe.Presentation.Vfx;
using JogoDaTrupe.UI;
using JogoDaTrupe.Data;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using JogoDaTrupe.Core.World;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using JogoDaTrupe.Net;

namespace JogoDaTrupe.EditorTools
{
    // JUSTIFICATIVA DE TAMANHO: este é um utilitário de Editor (não roda no jogo) que
    // monta a cena inteira do Vertical Slice por código — um bloco "CreateX" por sistema
    // (Player, câmera, combate, incorporação, inventário, crafting, construção, automação,
    // captura, boss, save, UIs). O tamanho reflete o nº de sistemas montados, não acúmulo
    // de responsabilidades. Será quebrado por sistema (SceneSetup.Player/.Combat/...) no
    // item 22 (mapa real), quando a montagem procedural der lugar a uma cena deliberada.
    // Dívida registrada em 06_Testes/Melhorias.md. (ENGINEERING_RULES #2)
    /// <summary>
    /// Monta automaticamente uma cena jogável do Player (chão + personagem +
    /// câmera + um objeto interativo) e a adiciona às Build Settings.
    ///
    /// Tudo é feito por código (em vez de montar na mão) para ser repetível,
    /// versionável e revisável. Use pelo menu: "Jogo da Trupe > Setup > Criar Cena Jogável (Player)".
    /// </summary>
    public static class PlayerSceneSetup
    {
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string ScenePath = SceneFolder + "/VerticalSlice.unity";
        // Cena de co-op (M-NET-3c P6): o MESMO mundo/UI, mas SEM player authored — o
        // personagem nasce em rede (P_Player) pelo NetworkManager da cena.
        private const string NetScenePath = SceneFolder + "/VerticalSlice_Net.unity";

        [MenuItem("Jogo da Trupe/Setup/Criar Cena Jogável (Player)")]
        public static void CreatePlayableScene()
        {
            // Cena SOLO: mundo + player authored, sem rede. É o baseline singleplayer.
            BuildScene(ScenePath, includePlayer: true, includeNetwork: false);
        }

        [MenuItem("Jogo da Trupe/Setup/Criar Cena Co-op (Rede)")]
        public static void CreateCoopScene()
        {
            // Cena CO-OP: o mesmo mundo/UI, SEM player authored (vem do spawn de rede) +
            // um NetworkManager com o P_Player e o aprovador de conexão. Play → Start Host
            // (ou Multiplayer Play Mode p/ 2 players) → o(s) personagem(ns) nasce(m) no mundo.
            BuildScene(NetScenePath, includePlayer: false, includeNetwork: true);
        }

        /// <summary>
        /// Monta a cena do Vertical Slice. <paramref name="includePlayer"/> = player authored
        /// (solo); quando falso, as HUDs nascem com refs vazias e são religadas em runtime pelo
        /// <see cref="Net.NetworkPlayerSetup"/> no spawn do dono (co-op). <paramref name="includeNetwork"/>
        /// adiciona o NetworkManager + P_Player + aprovador de conexão.
        /// </summary>
        private static void BuildScene(string scenePath, bool includePlayer, bool includeNetwork)
        {
            // Cena nova já com câmera principal e luz direcional.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Mundo deliberado: a "Floresta da Ressonância" (chão, limites, hub, arena,
            // vegetação, ruínas, luz e névoa). Os sistemas abaixo se posicionam por zona
            // lendo de MapLayout. Ver ForestWorld + MapLayout.
            ForestWorld.Build();

            // Malha de navegação: assada AQUI, com o mundo estático pronto e ANTES de player,
            // inimigos e props — o bake recolhe colliders, e os corpos deles virariam buracos
            // permanentes na malha. Ver NavMeshSetup.
            NavMeshSetup.Bake(System.IO.Path.GetFileNameWithoutExtension(scenePath));

            GameObject player = includePlayer ? CreatePlayer() : null;
            SetupCamera();
            CreateSampleInteractable();
            CreateVfxDirector(); // VFX de habilidades/Ecos (fase de arte)

            // Sistema de Incorporação (M1+M2): habilidade "Mordida" concedida pelas
            // criaturas, Altar para incorporar/equipar e UIs. Com player == null, as HUDs
            // ainda são criadas (refs de player ficam vazias até o Bind de rede).
            AbilityDefinition mordida = CreateMordidaAbility();
            // Na cena de rede os inimigos/boss NÃO são autorados (nascem por spawn do
            // servidor — M-NET-6 P1); as fichas continuam sendo criadas nas duas cenas.
            CreateEnemies(mordida, authorEnemies: !includeNetwork);
            CreateHUD();
            CreateAltar();
            CreateIncorporationUI(player);
            CreateInventory(player);
            CreateCrafting(player);
            CreateBuilding(player);
            CreateAutomation(player);
            CreateCapture(player, authorCreature: !includeNetwork);
            CreateBoss(player, authorBoss: !includeNetwork);
            WireRespecCost(player); // M-NET-8: 🎨 respec custa 2 Pedra (itens já existem aqui)
            CreateSave(player);

            if (includeNetwork) CreateNetworkManager();

            // Pós-processamento cel-shaded (P2): Volume global (bloom da Ressonância +
            // color grading + vignette) + liga o post na câmera. Ver PostFxSetup.
            PostFxSetup.Apply(UnityEngine.Camera.main);

            SaveAndRegisterScene(scene, scenePath);

            Selection.activeGameObject = player; // null no co-op (sem player authored) — ok.
            Debug.Log("[Setup] Cena criada em " + scenePath +
                      (includeNetwork
                          ? ". Play → NetworkManager > Start Host (ou Multiplayer Play Mode p/ 2 players)."
                          : ". Pressione Play para testar."));
        }

        // Adiciona o NetworkManager da cena de co-op: transporte, o prefab do player REAL
        // (P_Player, reusado do PlayerPrefabSetup) e o aprovador que dá spawns distintos.
        private static void CreateNetworkManager()
        {
            GameObject prefab = PlayerPrefabSetup.CreatePlayerPrefab();

            GameObject nmGo = new GameObject("NetworkManager");
            NetworkManager nm = nmGo.AddComponent<NetworkManager>();
            UnityTransport transport = nmGo.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = prefab;

            // Liga a aprovação de conexão + spawns por índice (lê MapLayout.PlayerSpawns).
            nmGo.AddComponent<CoopConnectionApprover>();

            // Correio de eventos (EventScopeAdapter, M-NET-5 P4) hospedado num PREFAB
            // spawnado quando o servidor sobe (NetSystemsSpawner). NÃO pode ser objeto
            // in-scene: NetworkObject criado por gerador de cena fica com
            // GlobalObjectIdHash=0 e o cliente falha a sincronização (Bugs.md #7).
            // Estruturas construíveis como PREFABS de rede (M-NET-7 P5): o servidor os spawna
            // ao atender um BuildRequested — aparecem nas 2 telas. Fichas baked (parede/piso
            // já criados por CreateBuilding). Casados por structureId no WorldRequestExecutor.
            GameObject[] structurePrefabs = StructurePrefabSetup.CreateStructurePrefabs(
                LoadStructure("Structure_Parede"), LoadStructure("Structure_Piso"));

            GameObject systemsPrefab = CreateNetSystemsPrefab(structurePrefabs);
            NetSystemsSpawner spawner = nmGo.AddComponent<NetSystemsSpawner>();
            SetObjectField(spawner, "_systemsPrefab", systemsPrefab);

            // Autoridade da simulação (M-NET-6 P1): host liga, cliente desliga, shutdown
            // restaura o solo. É quem escreve o seam SimulationAuthority (Core) que a IA
            // e o EcoDropper consultam (gates do P0).
            nmGo.AddComponent<NetAuthorityBinder>();

            // Atalho de teclado dev-only (H=Host, J=Client) para dirigir o MPPM sem o
            // Inspector da janela virtual (M-NET-7). Inerte em build.
            nmGo.AddComponent<NetHotkeyStarter>();

            // Sobe a sessão pedida pelo Menu Principal (M-NET-9 P4): no Start lê o
            // SessionRequest e inicia Host/Client. Convive com o H/J (só age se o modo
            // veio do menu; MPPM abre a cena direto → modo None → atalhos seguem valendo).
            nmGo.AddComponent<SessionAutoStart>();

            // Inimigos e boss como PREFABS spawnados pelo servidor (M-NET-6 P1): um corpo
            // por inimigo, igual nas duas telas. Fichas baked (a réplica nasce certa).
            GameObject[] enemyPrefabs = EnemyPrefabSetup.CreateEnemyPrefabs(
                LoadEnemy("Enemy_Berserker"), LoadEnemy("Enemy_HitAndRun"), LoadEnemy("Enemy_Zigzag"));
            GameObject bossPrefab = EnemyPrefabSetup.CreateBossPrefab(LoadEnemy("Boss_EcoCorrompido"));
            // Criatura capturável como prefab de rede (M-NET-7 P7): guid baked igual nas
            // 2 telas; captura arbitrada no servidor ("um leva"). Ficha já criada pelo
            // CreateCapture (roda antes deste ponto no BuildScene).
            GameObject creaturePrefab = CreaturePrefabSetup.CreateCreaturePrefab(LoadEnemy("Creature_Planta"));
            NetEnemySpawner enemySpawner = nmGo.AddComponent<NetEnemySpawner>();
            SetObjectArrayField(enemySpawner, "_enemyPrefabs", enemyPrefabs);
            SetObjectField(enemySpawner, "_bossPrefab", bossPrefab);
            SetObjectField(enemySpawner, "_creaturePrefab", creaturePrefab);

            // Registro DETERMINÍSTICO dos prefabs de rede (correção durável — Melhorias.md).
            // O NGO mantém a auto-lista DefaultNetworkPrefabs (postprocessor) e o Reset do
            // NetworkManager a liga ao manager recém-criado. Antes só VALIDÁVAMOS isso e
            // falhávamos alto; o problema é que a auto-lista DEFASAVA após muitas regerações
            // (o delete+recria trocava o GUID → o hash mudava e a entrada sumia). Agora, além
            // de o GUID ser estável (NetPrefabTooling.SavePrefabStable), o gerador GARANTE o
            // registro: EnsureRegistered é no-op se já está na lista, senão ADICIONA (idempotente
            // via Contains — sem 2ª fonte, sem Bug #8). O SaveAssets no fim da geração persiste.
            NetPrefabTooling.EnsureRegistered(nm, systemsPrefab);
            foreach (GameObject p in structurePrefabs) NetPrefabTooling.EnsureRegistered(nm, p);
            foreach (GameObject p in enemyPrefabs) NetPrefabTooling.EnsureRegistered(nm, p);
            NetPrefabTooling.EnsureRegistered(nm, bossPrefab);
            NetPrefabTooling.EnsureRegistered(nm, creaturePrefab);

            // Auto-cura: se a auto-lista do NGO acumulou uma entrada duplicada (P_NetSystems foi
            // registrado 2× num histórico antigo → erro "duplicate GlobalObjectIdHash" no Start
            // Host), a geração remove as sobras. Idempotente. Ver NetPrefabTooling.DedupeRegistry.
            NetPrefabTooling.DedupeRegistry(nm);
        }

        private static EnemyDefinition LoadEnemy(string assetName) =>
            AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/_Project/Data/Creatures/" + assetName + ".asset");

        // Cria (ou recria) o prefab P_NetSystems: NetworkObject + EventScopeAdapter +
        // WorldRequestExecutor (com os prefabs de estrutura casados por structureId — P5).
        // Mesmo pipeline de prefab do P_Player (hash de rede calculado no import do asset).
        private static GameObject CreateNetSystemsPrefab(GameObject[] structurePrefabs)
        {
            const string prefabFolder = "Assets/_Project/Prefabs";
            const string prefabPath = prefabFolder + "/P_NetSystems.prefab";

            if (!AssetDatabase.IsValidFolder(prefabFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            GameObject temp = new GameObject("P_NetSystems");
            temp.AddComponent<NetworkObject>();
            temp.AddComponent<EventScopeAdapter>();
            WorldRequestExecutor executor = temp.AddComponent<WorldRequestExecutor>(); // pedidos de mundo (M-NET-7 P3+)
            SetObjectArrayField(executor, "_structurePrefabs", structurePrefabs);      // estruturas p/ spawnar (P5)
            temp.AddComponent<IncorporationRequestExecutor>(); // pedidos de incorporação (M-NET-8 P3)
            temp.AddComponent<LateJoinWorldSync>();            // re-anuncia estoque dos nós ao late-joiner (M-NET-9 P3)

            // GUID ESTÁVEL (sem delete): o hash de rede deriva do GUID, então reusar o
            // asset mantém o hash idêntico entre regerações e telas. Ver NetPrefabTooling.
            return NetPrefabTooling.SavePrefabStable(temp, prefabPath);
        }

        // GetComponent tolerante a player nulo (cena de co-op não tem player authored;
        // a ref é religada em runtime pelo NetworkPlayerSetup do dono).
        private static T Comp<T>(GameObject go) where T : Component
            => go != null ? go.GetComponent<T>() : null;

        private static GameObject CreatePlayer()
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            // Nasce no 1º ponto de spawn (co-op: jogadores são uma coleção, tamanho 1 hoje).
            player.transform.position = MapLayout.SpawnFor(0);
            ApplyColor(player, new Color(0.2f, 0.45f, 0.85f));

            AddPlayerComponents(player);
            player.AddComponent<StaminaHUD>(); // aposentada (vitais ficam na HUD do HealthBarHUD)
            HeroSetup.AttachHeroVisual(player); // cápsula → mesh do herói Dungeon Mason + Animator
            return player;
        }

        /// <summary>
        /// Adiciona o CharacterController + os 18 componentes do jogador (fundação +
        /// combate + incorporação + inventário + captura). COMPARTILHADO entre a montagem
        /// da cena solo (CreatePlayer) e o prefab de rede P_Player (PlayerPrefabSetup) — DRY.
        /// Não adiciona os componentes de REDE (NetworkObject etc.); esses só vão no prefab.
        /// </summary>
        internal static void AddPlayerComponents(GameObject player)
        {
            // O CharacterController substitui o collider de cápsula padrão (se houver).
            CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
            if (capsule != null) Object.DestroyImmediate(capsule);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;

            // Fundação multiplayer-ready + sistemas do jogador.
            player.AddComponent<EntityGuid>();
            player.AddComponent<Ownable>();
            player.AddComponent<PlayerInputReader>();
            player.AddComponent<GroundCheck>();
            player.AddComponent<Stamina>();          // estamina (correr/deslizar)
            player.AddComponent<PlayerController>();
            player.AddComponent<Interactor>();
            player.AddComponent<Health>();
            player.AddComponent<MeleeAttack>();
            player.AddComponent<Blocker>();          // guarda com escudo (botão direito) — mitiga o dano no Health

            // Sistema de Incorporação (estado por-jogador, multiplayer-ready).
            player.AddComponent<MemoryBank>();
            player.AddComponent<AbilityLoadout>();
            player.AddComponent<PlayerAttributes>();
            player.AddComponent<AffinityState>();
            player.AddComponent<AbilityRunner>();
            player.AddComponent<CharacterSheetToggle>();

            // Inventário (Fase 3).
            player.AddComponent<PlayerInventory>();
            player.AddComponent<InventoryToggle>();
            player.AddComponent<BuildController>();

            // Captura (Fase 4): sincronizar criaturas enfraquecidas (segurar E).
            player.AddComponent<Synchronizer>();

            // Respawn + fim do "zumbi jogável" (M-NET-9 P0): morte desliga o controle e a
            // autoridade revive no spawn após 5s. Vale no solo (autoridade local).
            player.AddComponent<PlayerRespawner>();
        }

        private static void CreateEnemies(AbilityDefinition grantedAbility, bool authorEnemies)
        {
            EnsureCreaturesFolder();

            // 3 arquétipos, cada um com atributos e um "truque" próprios. Todos
            // concedem o Eco da mesma família ("Fera") no slice — uma habilidade só.
            EnemyDefinition berserker = CreateEnemyDef("Enemy_Berserker", new Color(0.85f, 0.2f, 0.15f), 120f, 4.8f, 18f, EnemyTrick.Berserker, grantedAbility);
            EnemyDefinition hitAndRun = CreateEnemyDef("Enemy_HitAndRun", new Color(0.95f, 0.55f, 0.1f), 70f, 5.5f, 12f, EnemyTrick.HitAndRun, grantedAbility);
            EnemyDefinition zigzag = CreateEnemyDef("Enemy_Zigzag", new Color(0.6f, 0.2f, 0.7f), 90f, 4.5f, 14f, EnemyTrick.Zigzag, grantedAbility);

            // Cena de REDE: inimigos NÃO são autorados — nascem como prefabs spawnados
            // pelo servidor (NetEnemySpawner), para existir UM corpo por inimigo nas duas
            // telas. (NetworkObject in-scene gerado por código também quebraria o cliente
            // com hash 0 — Bugs.md #7.) As fichas acima continuam sendo criadas: os
            // prefabs e o DefinitionRegistry dependem delas.
            if (!authorEnemies) return;

            // Zona de Combate (NE): ficam ociosos até o jogador entrar na clareira.
            SpawnEnemy(berserker, MapLayout.EnemySpawns[0]);
            SpawnEnemy(hitAndRun, MapLayout.EnemySpawns[1]);
            SpawnEnemy(zigzag, MapLayout.EnemySpawns[2]);
        }

        // Cria o asset da habilidade "Mordida" (família "Fera"), o Eco do slice.
        private static AbilityDefinition CreateMordidaAbility()
        {
            EnsureCreaturesFolder();
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Abilities"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Abilities");

            AbilityDefinition def = ScriptableObject.CreateInstance<AbilityDefinition>();
            def.abilityId = "mordida";
            def.displayName = "Mordida";
            def.familyId = "Fera";
            def.element = ElementType.Terra;
            def.tier = AbilityTier.TierI;
            def.baseDamage = 45f;
            def.baseRange = 2.6f;
            def.baseRadius = 1.4f;
            def.baseCooldown = 0.9f;

            // Requisito de atributo (M4): exige Força 8. Começando em 5, a Mordida
            // rende menos até o jogador investir pontos em Força (penalidade suave).
            def.requiredAttribute = AttributeType.Forca;
            def.requiredAttributeValue = 8;

            // Nós de especialização (M3): o jogador molda a habilidade com o uso.
            def.specializations = new[]
            {
                new SpecializationNode
                {
                    label = "Presas afiadas (+dano)",
                    isSignature = false, axis = SpecAxis.Damage,
                    valuePerLevel = 0.15f, pointCost = 1, maxLevel = 3
                },
                new SpecializationNode
                {
                    label = "Reflexos (-recarga)",
                    isSignature = false, axis = SpecAxis.Cooldown,
                    valuePerLevel = 0.10f, pointCost = 1, maxLevel = 2
                }
            };

            string path = "Assets/_Project/Data/Abilities/Ability_Mordida.asset";
            if (AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(def, path);
            return def;
        }

        private static void EnsureCreaturesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Creatures"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Creatures");
        }

        // Cria (ou recria) a definição do inimigo como ASSET. ScriptableObject via
        // CreateAsset é confiável (sem o problema de timing dos sprites/texturas).
        private static EnemyDefinition CreateEnemyDef(string assetName, Color color, float hp, float speed, float dmg, EnemyTrick trick, AbilityDefinition grantedAbility, string displayName = null)
        {
            // REUSA o asset existente (GUID estável) em vez de deletar+recriar: os prefabs
            // de rede dos inimigos (P_Enemy_*/P_Boss, M-NET-6 P1) referenciam a ficha, e um
            // GUID novo deixaria essas referências "missing" na próxima regeneração
            // (mesmo padrão de risco já registrado em Melhorias.md para o P_Player).
            string path = "Assets/_Project/Data/Creatures/" + assetName + ".asset";
            EnemyDefinition def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            bool isNew = def == null;
            if (isNew) def = ScriptableObject.CreateInstance<EnemyDefinition>();

            def.enemyId = assetName.ToLowerInvariant(); // id estável (espelha itemId; usado na rede/save)
            def.displayName = string.IsNullOrEmpty(displayName) ? assetName : displayName;
            def.color = color;
            def.maxHealth = hp;
            def.moveSpeed = speed;
            def.damage = dmg;
            def.trick = trick;
            def.grantedAbility = grantedAbility;
            def.masteryThresholds = new[] { 1, 3, 6 }; // 1 derrota libera o Vestígio

            if (isNew) AssetDatabase.CreateAsset(def, path);
            else EditorUtility.SetDirty(def); // persistido pelo SaveAssets no fim
            return def;
        }

        private static void SpawnEnemy(EnemyDefinition def, Vector3 position)
        {
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = def.displayName;
            enemy.transform.position = position;
            ApplyColor(enemy, def.color);

            // Mantém a CapsuleCollider (para ser atingido). Sem Ownable de propósito,
            // para a vida do inimigo contar como "sem dono" e não mexer no HUD do jogador.
            enemy.AddComponent<EntityGuid>();
            Health health = enemy.AddComponent<Health>();
            EnemyController controller = enemy.AddComponent<EnemyController>();
            EcoDropper dropper = enemy.AddComponent<EcoDropper>();

            SetFloatField(health, "_max", def.maxHealth);
            SetObjectField(controller, "_definition", def);
            SetObjectField(dropper, "_definition", def); // deixa o Eco ao morrer

            // Modelo 3D no lugar da cápsula (Grunt/Lich/Footman conforme o enemyId).
            EnemySetup.AttachCreatureVisual(enemy, def.enemyId, false);
            EnemySetup.AddNavigation(enemy); // contorna colunas/arcos/árvores
        }

        // Altar: onde o jogador incorpora/equipa/troca habilidades (perto do spawn). O host
        // (GameObject vazio) carrega o Altar + um BoxCollider de interação; o VISUAL é o MODELO
        // do pacote Altar Ruins (arte, 06/07/2026) + um GLOW ciano de Ressonância (luz pontual —
        // nosso jeito de deixar o altar "mágico"). Fallback: cubo teal (graybox) sem o pacote.
        private static void CreateAltar()
        {
            var altar = new GameObject("Altar");
            // No centro do hub, à frente de onde o jogador nasce.
            altar.transform.position = MapLayout.At(MapLayout.Hub, 0f, 0.75f, 2.5f);
            BoxCollider box = altar.AddComponent<BoxCollider>();
            box.size = new Vector3(2.0f, 2.4f, 2.0f); // SÓLIDO (player não atravessa) + volume de interação
            box.center = new Vector3(0f, 0.4f, 0f);   // cobre a base do altar
            altar.AddComponent<Altar>();

            GameObject baseModel = LoadModel("Assets/Altar_Ruins_FREE", "Altar_2");
            if (baseModel != null)
            {
                // MONUMENTO do centro (referência do Diretor): base + mastro/cruz (Flag_beam) +
                // bandeira (Flag), medidos por bounds pra assentar certo. O host está +0.75 acima
                // do chão do hub, então a base do altar vai em localY -0.75 (assenta no chão).
                var b = FitPlace(baseModel, altar.transform, -0.75f, 1.8f);
                if (b != null) b.name = "AltarBase";
                // NOTA: o Flag_beam do pacote é uma viga HORIZONTAL — vira "mastro atravessado"
                // (feio) ao ajustar por altura; ficamos só com a base + o glow. Detalhe/design do
                // altar (e melhoria de gráficos/textura no estilo stylized) fica p/ fase futura.

                // Glow ciano da Ressonância — o toque mágico do santuário (o VFX já reage aos eventos).
                var glow = new GameObject("Ressonancia Glow");
                glow.transform.SetParent(altar.transform, false);
                glow.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                Light pl = glow.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = new Color(0.3f, 0.85f, 1f);
                pl.intensity = 4.5f;
                pl.range = 10f;
            }
            else
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); // fallback graybox
                cube.transform.SetParent(altar.transform, false);
                cube.transform.localScale = new Vector3(1f, 1.5f, 1f);
                ApplyColor(cube, new Color(0.2f, 0.7f, 0.75f));
                Object.DestroyImmediate(cube.GetComponent<Collider>());
            }
        }

        // Carrega um MODELO (FBX) por nome EXATO dentro de uma pasta de pacote. Null se não achar
        // (mantém o gerador robusto: fallback graybox quando o asset comprado não está presente).
        private static GameObject LoadModel(string folder, string modelName)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (string guid in AssetDatabase.FindAssets(modelName + " t:GameObject", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path).Equals(modelName, System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        // Remove todos os colliders de uma instância (modelo = só visual; a interação usa o
        // BoxCollider do host — e a IA anda por transform, não pode travar em collider de decor).
        private static void StripAllColliders(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
        }

        // Instancia um modelo, ESCALA pra bater targetHeight (m, via bounds dos renderers) e
        // assenta a BASE em baseLocalY (local ao pai, sem rotação). Sem collider. Null se model
        // == null. Evita o problema de escala de FBX (modelos gigantes/minúsculos).
        private static GameObject FitPlace(GameObject model, Transform parent, float baseLocalY, float targetHeight)
        {
            if (model == null) return null;
            var m = (GameObject)PrefabUtility.InstantiatePrefab(model);
            m.transform.SetParent(parent, false);
            m.transform.localPosition = Vector3.zero;
            var rends = m.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds bb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) bb.Encapsulate(rends[i].bounds);
                m.transform.localScale = Vector3.one * (targetHeight / Mathf.Max(0.01f, bb.size.y));
                rends = m.GetComponentsInChildren<Renderer>();
                bb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) bb.Encapsulate(rends[i].bounds);
                m.transform.position += new Vector3(0f, (parent.position.y + baseLocalY) - bb.min.y, 0f);
            }
            StripAllColliders(m);
            return m;
        }

        // Inventário (Fase 3): cria itens, nós de recurso no mundo e a HUD (tecla I).
        private static void CreateInventory(GameObject player)
        {
            EnsureItemsFolder();

            ItemDefinition madeira = CreateItemDef("Item_Madeira", "Madeira", new Color(0.55f, 0.35f, 0.18f));
            ItemDefinition pedra = CreateItemDef("Item_Pedra", "Pedra", new Color(0.55f, 0.55f, 0.58f));

            // Zona de Coleta (NO): bosque de recursos.
            SpawnResourceNode(madeira, MapLayout.At(MapLayout.ZoneColeta, -1.5f, 0.6f, 0.8f));
            SpawnResourceNode(madeira, MapLayout.At(MapLayout.ZoneColeta, 1.3f, 0.6f, 2.0f));
            SpawnResourceNode(pedra, MapLayout.At(MapLayout.ZoneColeta, -2.2f, 0.6f, -1.8f));

            PlayerInventory inventory = Comp<PlayerInventory>(player);
            GameObject invCanvasGo = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas invCanvas = invCanvasGo.GetComponent<Canvas>();
            invCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            invCanvas.sortingOrder = 9;
            invCanvasGo.transform.localScale = Vector3.one;

            InventoryHUD hud = invCanvasGo.AddComponent<InventoryHUD>();
            SetObjectField(hud, "_inventory", inventory);
        }

        private static void EnsureItemsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Items"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Items");
        }

        private static ItemDefinition CreateItemDef(string assetName, string displayName, Color color, ItemCategory category = ItemCategory.Recurso)
        {
            ItemDefinition def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.itemId = assetName.ToLowerInvariant();
            def.displayName = displayName;
            def.color = color;
            def.category = category;
            def.maxStack = 99;

            string path = "Assets/_Project/Data/Items/" + assetName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(def, path);
            return def;
        }

        private static void SpawnResourceNode(ItemDefinition item, Vector3 position)
        {
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            node.name = "Recurso_" + item.displayName;
            node.transform.position = position;
            node.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
            ApplyColor(node, item.color);

            // EntityGuid com id CONCRETO serializado na cena (M-NET-7 P3): a cena de co-op
            // é o mesmo asset nas 2 máquinas, então host e cliente veem o MESMO guid neste
            // nó — é assim que o pedido de coleta (HarvestRequested) casa o nó certo pela rede.
            node.AddComponent<EntityGuid>().EnsureId();

            ResourceNode rn = node.AddComponent<ResourceNode>();
            SetObjectField(rn, "_item", item);
            SetIntField(rn, "_amountPerHarvest", 3);
            SetIntField(rn, "_totalAmount", 9);
        }

        // Crafting (Fase 3): itens resultantes, receitas, bancada e a UI (E na bancada).
        private static void CreateCrafting(GameObject player)
        {
            EnsureItemsFolder();
            EnsureRecipesFolder();

            // Itens resultantes do craft.
            ItemDefinition tabua = CreateItemDef("Item_Tabua", "Tábua", new Color(0.7f, 0.5f, 0.25f), ItemCategory.Material);
            ItemDefinition machado = CreateItemDef("Item_Machado", "Machado", new Color(0.6f, 0.6f, 0.65f), ItemCategory.Equipamento);

            // Recursos já criados em CreateInventory.
            ItemDefinition madeira = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Item_Madeira.asset");
            ItemDefinition pedra = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Item_Pedra.asset");

            // Receitas aprovadas: Tábua (2 Madeira) e Machado (3 Madeira + 2 Pedra).
            RecipeDefinition rTabua = CreateRecipe("Recipe_Tabua", "Tábua",
                new[] { Ingredient(madeira, 2) }, tabua, 1);
            RecipeDefinition rMachado = CreateRecipe("Recipe_Machado", "Machado",
                new[] { Ingredient(madeira, 3), Ingredient(pedra, 2) }, machado, 1);

            // Bancada no mundo (interagir com E abre o crafting).
            GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "Bancada";
            // Base do jogador: ao lado do hub.
            bench.transform.position = MapLayout.At(MapLayout.Hub, 3.8f, 0.6f, -1.5f);
            bench.transform.localScale = new Vector3(1.4f, 1.2f, 0.9f);
            ApplyColor(bench, new Color(0.5f, 0.35f, 0.2f));
            bench.AddComponent<Workbench>();

            // UI do crafting.
            PlayerInventory inventory = Comp<PlayerInventory>(player);
            GameObject craftCanvasGo = new GameObject("CraftingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas craftCanvas = craftCanvasGo.GetComponent<Canvas>();
            craftCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            craftCanvas.sortingOrder = 10;
            craftCanvasGo.transform.localScale = Vector3.one;

            CraftingUI ui = craftCanvasGo.AddComponent<CraftingUI>();
            SetObjectField(ui, "_inventory", inventory);
            SetObjectArrayField(ui, "_recipes", new Object[] { rTabua, rMachado });
        }

        private static void EnsureRecipesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Recipes"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Recipes");
        }

        private static RecipeIngredient Ingredient(ItemDefinition item, int amount)
        {
            return new RecipeIngredient { item = item, amount = amount };
        }

        private static RecipeDefinition CreateRecipe(string assetName, string displayName, RecipeIngredient[] ingredients, ItemDefinition output, int outputAmount)
        {
            // REUSA o asset existente (GUID estável) em vez de deletar+recriar — mesmo
            // padrão do CreateEnemyDef (M-NET-6 P1): a UI/prefabs referenciam a ficha e
            // um GUID novo deixaria as referências "missing" na próxima regeneração.
            string path = "Assets/_Project/Data/Recipes/" + assetName + ".asset";
            RecipeDefinition recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
            bool isNew = recipe == null;
            if (isNew) recipe = ScriptableObject.CreateInstance<RecipeDefinition>();

            recipe.recipeId = assetName.ToLowerInvariant(); // id estável (espelha enemyId; usado na rede/save — M-NET-7 P0)
            recipe.displayName = displayName;
            recipe.ingredients = ingredients;
            recipe.output = output;
            recipe.outputAmount = outputAmount;

            if (isNew) AssetDatabase.CreateAsset(recipe, path);
            else EditorUtility.SetDirty(recipe); // persistido pelo SaveAssets no fim
            return recipe;
        }

        // Construção (Fase 3): estruturas (Parede, Piso), liga a lista ao BuildController e cria a HUD.
        private static void CreateBuilding(GameObject player)
        {
            EnsureStructuresFolder();
            ItemDefinition tabua = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Item_Tabua.asset");

            StructureDefinition parede = CreateStructureDef("Structure_Parede", "Parede",
                new[] { Ingredient(tabua, 2) }, new Vector3(1f, 1.2f, 0.2f), 0.6f, new Color(0.7f, 0.55f, 0.35f));
            StructureDefinition piso = CreateStructureDef("Structure_Piso", "Piso",
                new[] { Ingredient(tabua, 2) }, new Vector3(1f, 0.15f, 1f), 0.075f, new Color(0.6f, 0.5f, 0.3f));

            BuildController build = Comp<BuildController>(player);
            // Co-op (build == null aqui): a lista de estruturas é dado do BuildController do
            // player, que no co-op vem do prefab/M-NET-7 (construção authoritative). A BuildHUD
            // é religada em runtime pelo NetworkPlayerSetup. Ver 06_Testes/Melhorias.md.
            if (build != null) SetObjectArrayField(build, "_structures", new Object[] { parede, piso });

            GameObject buildCanvasGo = new GameObject("BuildCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas buildCanvas = buildCanvasGo.GetComponent<Canvas>();
            buildCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            buildCanvas.sortingOrder = 8;
            buildCanvasGo.transform.localScale = Vector3.one;

            BuildHUD hud = buildCanvasGo.AddComponent<BuildHUD>();
            SetObjectField(hud, "_build", build);
        }

        /// <summary>Carrega uma StructureDefinition pelo nome do asset (null se não existe).</summary>
        internal static StructureDefinition LoadStructure(string assetName) =>
            AssetDatabase.LoadAssetAtPath<StructureDefinition>("Assets/_Project/Data/Structures/" + assetName + ".asset");

        /// <summary>
        /// Escreve o custo do respec no <see cref="AbilityLoadout"/> de um player/prefab
        /// (M-NET-8; 🎨 2 Pedra — decisão de 03/07/2026). RecipeIngredient é STRUCT
        /// serializado — os campos vão por FindPropertyRelative (SetObjectArrayField não
        /// serve: ele escreve objectReferenceValue). No-op se o Item_Pedra ainda não
        /// existe (ex.: cena de teste standalone gerada antes dos itens).
        /// </summary>
        internal static void WireRespecCost(GameObject player)
        {
            AbilityLoadout loadout = Comp<AbilityLoadout>(player);
            if (loadout == null) return;
            ItemDefinition pedra = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Item_Pedra.asset");
            if (pedra == null) return;

            var so = new SerializedObject(loadout);
            SerializedProperty prop = so.FindProperty("_respecCost");
            if (prop == null) return;
            prop.arraySize = 1;
            SerializedProperty el = prop.GetArrayElementAtIndex(0);
            el.FindPropertyRelative("item").objectReferenceValue = pedra;
            el.FindPropertyRelative("amount").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Liga a lista de estruturas ao <see cref="BuildController"/> de um player/prefab
        /// (M-NET-7 P5). No co-op o player nasce do prefab <c>P_Player</c> — <c>CreateBuilding</c>
        /// roda com player=null (só cria os assets), então o PREFAB precisa ser ligado aqui
        /// (senão o cliente não teria o que construir). No-op se os assets ainda não existem
        /// (ex.: cena de teste standalone do player).
        /// </summary>
        internal static void WireBuildStructures(GameObject player)
        {
            BuildController build = Comp<BuildController>(player);
            if (build == null) return;
            StructureDefinition parede = LoadStructure("Structure_Parede");
            StructureDefinition piso = LoadStructure("Structure_Piso");
            if (parede == null || piso == null) return;
            SetObjectArrayField(build, "_structures", new Object[] { parede, piso });
        }

        private static void EnsureStructuresFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Structures"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Structures");
        }

        private static StructureDefinition CreateStructureDef(string assetName, string displayName, RecipeIngredient[] cost, Vector3 size, float baseY, Color color)
        {
            // REUSA o asset existente (GUID estável) — mesmo padrão do CreateEnemyDef:
            // os prefabs de rede das estruturas (P_Structure_*, M-NET-7 P5) vão
            // referenciar a ficha; deletar+recriar quebraria essas referências.
            string path = "Assets/_Project/Data/Structures/" + assetName + ".asset";
            StructureDefinition def = AssetDatabase.LoadAssetAtPath<StructureDefinition>(path);
            bool isNew = def == null;
            if (isNew) def = ScriptableObject.CreateInstance<StructureDefinition>();

            def.structureId = assetName.ToLowerInvariant(); // id estável (espelha enemyId; usado na rede — M-NET-7 P0)
            def.displayName = displayName;
            def.cost = cost;
            def.size = size;
            def.baseY = baseY;
            def.color = color;

            if (isNew) AssetDatabase.CreateAsset(def, path);
            else EditorUtility.SetDirty(def); // persistido pelo SaveAssets no fim
            return def;
        }

        // Automação (Fase 3 — último item): um Coletor Automático que colhe um nó de
        // Madeira sozinho, em ciclos, e enche o inventário do jogador SEM ele interagir.
        // É a "prova" de automação simples do slice.
        private static void CreateAutomation(GameObject player)
        {
            EnsureItemsFolder();
            EnsureAutomationFolder();

            ItemDefinition madeira = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Item_Madeira.asset");
            AutomationDefinition coletorDef = CreateAutomationDef("Automation_Coletor", "Coletor Automático");

            // Nó de Madeira dedicado à demonstração (estoque maior, para dar tempo de
            // ver o inventário enchendo). Fica num canto livre da cena.
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            node.name = "Recurso_Madeira (Automação)";
            // Zona de Automação (SO): clareira da máquina.
            node.transform.position = MapLayout.At(MapLayout.ZoneAutomacao, 0.8f, 0.6f, 0f);
            node.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
            if (madeira != null) ApplyColor(node, madeira.color);

            node.AddComponent<EntityGuid>().EnsureId(); // guid estável, igual nas 2 telas (M-NET-7 P3)

            ResourceNode rn = node.AddComponent<ResourceNode>();
            SetObjectField(rn, "_item", madeira);
            SetIntField(rn, "_amountPerHarvest", 3);
            SetIntField(rn, "_totalAmount", 30);

            // O Coletor, encostado no nó (dentro do raio de busca).
            GameObject collector = GameObject.CreatePrimitive(PrimitiveType.Cube);
            collector.name = "Coletor Automático";
            // Encostado no nó (dentro do raio de busca de 4 m).
            collector.transform.position = MapLayout.At(MapLayout.ZoneAutomacao, -1.4f, coletorDef.baseY, 0f);
            collector.transform.localScale = coletorDef.size;
            ApplyColor(collector, coletorDef.color);

            collector.AddComponent<EntityGuid>(); // id estável (pronto para o save)
            collector.AddComponent<Ownable>();     // dono = jogador local (multiplayer-ready)
            AutoCollector auto = collector.AddComponent<AutoCollector>();
            SetObjectField(auto, "_definition", coletorDef);
            SetObjectField(auto, "_output", Comp<PlayerInventory>(player));
        }

        private static void EnsureAutomationFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Automation"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Automation");
        }

        private static AutomationDefinition CreateAutomationDef(string assetName, string displayName)
        {
            AutomationDefinition def = ScriptableObject.CreateInstance<AutomationDefinition>();
            def.displayName = displayName;
            def.cycleSeconds = 1.5f;
            def.amountPerCycle = 1;
            def.searchRadius = 4f;

            string path = "Assets/_Project/Data/Automation/" + assetName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<AutomationDefinition>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(def, path);
            return def;
        }

        // Captura (Fase 4): habilidade Cipó, criatura de Planta capturável e a HUD de captura.
        // No co-op (authorCreature=false) a criatura NÃO é authored: vira prefab de rede
        // spawnado pelo NetEnemySpawner (M-NET-7 P7) — um corpo, igual nas 2 telas.
        private static void CreateCapture(GameObject player, bool authorCreature)
        {
            AbilityDefinition cipo = CreateCipoAbility();

            // Criatura de Planta: luta fraco e, ao ser enfraquecida, fica sincronizável;
            // capturá-la concede o Cipó (mesma porta do Eco das criaturas derrotadas).
            EnemyDefinition plantaDef = CreateEnemyDef(
                "Creature_Planta", new Color(0.3f, 0.7f, 0.35f),
                hp: 90f, speed: 3.2f, dmg: 8f, EnemyTrick.HitAndRun, cipo,
                displayName: "Criatura de Planta");

            // Zona da Criatura (SE): bosque denso onde vive a Criatura de Planta.
            if (authorCreature)
                SpawnCapturable(plantaDef, MapLayout.At(MapLayout.ZoneCriatura, 0f, 1f, 0f));

            // HUD de captura (prompt + barra do canal ao segurar E).
            GameObject capCanvasGo = new GameObject("CaptureCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas capCanvas = capCanvasGo.GetComponent<Canvas>();
            capCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            capCanvas.sortingOrder = 9;
            capCanvasGo.transform.localScale = Vector3.one;

            CaptureHUD hud = capCanvasGo.AddComponent<CaptureHUD>();
            SetObjectField(hud, "_synchronizer", Comp<Synchronizer>(player));
        }

        // Cria o asset da habilidade "Cipó" (família "Planta"): controle (Snare) + dano.
        private static AbilityDefinition CreateCipoAbility()
        {
            EnsureCreaturesFolder();
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Abilities"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Abilities");

            AbilityDefinition def = ScriptableObject.CreateInstance<AbilityDefinition>();
            def.abilityId = "cipo";
            def.displayName = "Cipó";
            def.familyId = "Planta";
            def.element = ElementType.Planta;
            def.tier = AbilityTier.TierI;
            def.effect = AbilityEffect.Snare;
            def.snareSeconds = 2f;
            def.baseDamage = 28f;
            def.baseRange = 2.8f;
            def.baseRadius = 1.4f;
            def.baseCooldown = 1.6f;

            // Sem requisito de atributo no slice (a penalidade já é demonstrada pela Mordida).
            def.requiredAttribute = AttributeType.Forca;
            def.requiredAttributeValue = 0;

            // Nós de especialização (M3): o jogador molda o Cipó com o uso.
            def.specializations = new[]
            {
                new SpecializationNode
                {
                    label = "Espinhos (+dano)",
                    isSignature = false, axis = SpecAxis.Damage,
                    valuePerLevel = 0.15f, pointCost = 1, maxLevel = 3
                },
                new SpecializationNode
                {
                    label = "Raízes firmes (-recarga)",
                    isSignature = false, axis = SpecAxis.Cooldown,
                    valuePerLevel = 0.10f, pointCost = 1, maxLevel = 2
                }
            };

            string path = "Assets/_Project/Data/Abilities/Ability_Cipo.asset";
            if (AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(def, path);
            return def;
        }

        // Spawna a criatura capturável: ela luta (EnemyController) mas não morre — ao
        // enfraquecer, vira sincronizável (CapturableCreature). Sem Ownable de propósito
        // (a vida dela não aparece no HUD do jogador), sem EcoDropper (o Eco vem da captura).
        private static void SpawnCapturable(EnemyDefinition def, Vector3 position)
        {
            GameObject creature = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            creature.name = def.displayName;
            creature.transform.position = position;
            ApplyColor(creature, def.color);

            creature.AddComponent<EntityGuid>();
            Health health = creature.AddComponent<Health>();
            EnemyController controller = creature.AddComponent<EnemyController>();
            CapturableCreature capturable = creature.AddComponent<CapturableCreature>();

            SetFloatField(health, "_max", def.maxHealth);
            SetObjectField(controller, "_definition", def);
            SetObjectField(capturable, "_definition", def);
        }

        // Boss (Fase 5): o "Eco Corrompido". Cria a habilidade-recompensa (o Eco do
        // boss), a definição do boss e a HUD. O boss adapta: fases por vida + resiste
        // ao elemento que o jogador mais repete (BossController).
        private static void CreateBoss(GameObject player, bool authorBoss)
        {
            AbilityDefinition pulso = CreatePulsoAbility();

            EnemyDefinition bossDef = CreateEnemyDef(
                "Boss_EcoCorrompido", new Color(0.5f, 0.15f, 0.45f),
                hp: 400f, speed: 3.6f, dmg: 16f, EnemyTrick.Berserker, pulso,
                displayName: "Eco Corrompido");
            // Ajustes próprios do boss (CreateEnemyDef usa os defaults para o resto).
            bossDef.attackRange = 3f;
            bossDef.attackCooldown = 1.3f;
            bossDef.detectRange = 14f; // fica ocioso até o jogador se aproximar (arena)
            EditorUtility.SetDirty(bossDef); // persistido pelo SaveAssets no fim

            // Arena ao norte: o boss fica ocioso (detectRange 14) até o jogador entrar.
            // Cena de REDE: o boss não é autorado (nasce como prefab spawnado pelo
            // servidor — NetEnemySpawner); a HUD nasce com a ref vazia e religa sozinha
            // em runtime (re-busca do BossHUD) quando o boss spawnar.
            BossController boss = authorBoss ? SpawnBoss(bossDef, MapLayout.BossSpawn) : null;

            GameObject bossCanvasGo = new GameObject("BossCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas bossCanvas = bossCanvasGo.GetComponent<Canvas>();
            bossCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            bossCanvas.sortingOrder = 9;
            bossCanvasGo.transform.localScale = Vector3.one;

            BossHUD hud = bossCanvasGo.AddComponent<BossHUD>();
            if (boss != null) SetObjectField(hud, "_boss", boss);
        }

        // Habilidade-recompensa do boss: "Pulso Corrompido" (Éter, golpe forte).
        private static AbilityDefinition CreatePulsoAbility()
        {
            EnsureCreaturesFolder();
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Abilities"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Abilities");

            AbilityDefinition def = ScriptableObject.CreateInstance<AbilityDefinition>();
            def.abilityId = "pulso_corrompido";
            def.displayName = "Pulso Corrompido";
            def.familyId = "Corrompido";
            def.element = ElementType.Eter;
            def.tier = AbilityTier.TierI;
            def.effect = AbilityEffect.DamageBurst;
            def.baseDamage = 60f;
            def.baseRange = 3f;
            def.baseRadius = 1.7f;
            def.baseCooldown = 1.3f;
            def.requiredAttribute = AttributeType.Forca;
            def.requiredAttributeValue = 0;
            def.specializations = new[]
            {
                new SpecializationNode { label = "Eco instável (+dano)", isSignature = false, axis = SpecAxis.Damage, valuePerLevel = 0.15f, pointCost = 1, maxLevel = 3 },
                new SpecializationNode { label = "Ressonância (+área)", isSignature = false, axis = SpecAxis.Area, valuePerLevel = 0.12f, pointCost = 1, maxLevel = 2 }
            };

            string path = "Assets/_Project/Data/Abilities/Ability_PulsoCorrompido.asset";
            if (AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(def, path);
            return def;
        }

        // Spawna o boss: cápsula grande e escura, com Health (não morre por piso — é
        // morto normalmente), BossController (fases + resistência) e EcoDropper (Eco ao morrer).
        private static BossController SpawnBoss(EnemyDefinition def, Vector3 position)
        {
            GameObject boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            boss.name = def.displayName;
            boss.transform.position = position;
            boss.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);
            ApplyColor(boss, def.color);

            boss.AddComponent<EntityGuid>();
            Health health = boss.AddComponent<Health>();
            BossController controller = boss.AddComponent<BossController>();
            EcoDropper dropper = boss.AddComponent<EcoDropper>();

            SetFloatField(health, "_max", def.maxHealth);
            SetObjectField(controller, "_definition", def);
            SetObjectField(dropper, "_definition", def);

            // Modelo 3D no lugar da cápsula: o dragão TerrorBringer Roxo (isBoss=true).
            EnemySetup.AttachCreatureVisual(boss, def.enemyId, true);
            return controller;
        }

        // Save/load (Fase 5): registro de definições (resolve ids → fichas), o
        // SaveSystem (F5 salva, F9 carrega) e a HUD de aviso.
        private static void CreateSave(GameObject player)
        {
            DefinitionRegistry registry = ScriptableObject.CreateInstance<DefinitionRegistry>();
            registry.items = new[]
            {
                LoadItem("Item_Madeira"), LoadItem("Item_Pedra"),
                LoadItem("Item_Tabua"), LoadItem("Item_Machado")
            };
            registry.abilities = new[]
            {
                LoadAbility("Ability_Mordida"), LoadAbility("Ability_Cipo"),
                LoadAbility("Ability_PulsoCorrompido")
            };
            // Todas as fichas de inimigo/criatura/boss da cena (id → ficha), para a rede
            // reconstruir EcoCollected/CreatureCaptured no cliente a partir do enemyId (M-NET-4 P4).
            registry.enemies = LoadAllEnemies();
            // Receitas e estruturas (id → ficha) — M-NET-7 P0: destravam o relay de
            // ItemCrafted e os pedidos CraftRequested/BuildRequested (mundo compartilhado).
            registry.recipes = LoadAll<RecipeDefinition>("Assets/_Project/Data/Recipes");
            registry.structures = LoadAll<StructureDefinition>("Assets/_Project/Data/Structures");

            string regPath = "Assets/_Project/Data/DefinitionRegistry.asset";
            if (AssetDatabase.LoadAssetAtPath<DefinitionRegistry>(regPath) != null)
                AssetDatabase.DeleteAsset(regPath);
            AssetDatabase.CreateAsset(registry, regPath);

            GameObject saveGo = new GameObject("SaveSystem");
            SaveSystem save = saveGo.AddComponent<SaveSystem>();
            SetObjectField(save, "_registry", registry);
            // Co-op: _input fica null aqui; o save (F5/F9) é fase M-NET-8/9. Ver Melhorias.md.
            SetObjectField(save, "_input", Comp<PlayerInputReader>(player));

            GameObject saveCanvasGo = new GameObject("SaveCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas saveCanvas = saveCanvasGo.GetComponent<Canvas>();
            saveCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            saveCanvas.sortingOrder = 12;
            saveCanvasGo.transform.localScale = Vector3.one;
            saveCanvasGo.AddComponent<SaveHUD>();
        }

        private static ItemDefinition LoadItem(string assetName) =>
            AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/" + assetName + ".asset");

        private static AbilityDefinition LoadAbility(string assetName) =>
            AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/_Project/Data/Abilities/" + assetName + ".asset");

        // Carrega todas as EnemyDefinition da pasta de criaturas (inimigos + criatura + boss).
        private static EnemyDefinition[] LoadAllEnemies() => LoadAll<EnemyDefinition>("Assets/_Project/Data/Creatures");

        // Carrega todas as fichas de um tipo numa pasta (genérico — DRY entre
        // enemies/recipes/structures do DefinitionRegistry).
        private static T[] LoadAll<T>(string folder) where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
            var list = new System.Collections.Generic.List<T>(guids.Length);
            foreach (string guid in guids)
            {
                T def = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            return list.ToArray();
        }

        // Cria as telas do sistema de Incorporação e liga às peças do jogador.
        private static void CreateIncorporationUI(GameObject player)
        {
            EnsureEventSystem();

            MemoryBank memory = Comp<MemoryBank>(player);
            AbilityLoadout loadout = Comp<AbilityLoadout>(player);
            PlayerAttributes attributes = Comp<PlayerAttributes>(player);
            AffinityState affinity = Comp<AffinityState>(player);

            // Tela do Altar (cobre a tela; oculta até abrir um Altar).
            GameObject altarCanvasGo = new GameObject("AltarCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas altarCanvas = altarCanvasGo.GetComponent<Canvas>();
            altarCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            altarCanvas.sortingOrder = 10; // por cima do HUD
            altarCanvasGo.transform.localScale = Vector3.one;

            AltarUI altarUI = altarCanvasGo.AddComponent<AltarUI>();
            SetObjectField(altarUI, "_memory", memory);
            SetObjectField(altarUI, "_loadout", loadout);

            // Barra das habilidades equipadas (rodapé).
            GameObject barCanvasGo = new GameObject("AbilityBarCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas barCanvas = barCanvasGo.GetComponent<Canvas>();
            barCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            barCanvasGo.transform.localScale = Vector3.one;

            AbilityBarHUD bar = barCanvasGo.AddComponent<AbilityBarHUD>();
            SetObjectField(bar, "_loadout", loadout);
            SetObjectField(bar, "_attributes", attributes);
            SetObjectField(bar, "_affinity", affinity);

            // Ficha de atributos + afinidade (tecla C).
            GameObject attrCanvasGo = new GameObject("AttributesCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas attrCanvas = attrCanvasGo.GetComponent<Canvas>();
            attrCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            attrCanvas.sortingOrder = 11; // por cima do HUD e do Altar
            attrCanvasGo.transform.localScale = Vector3.one;

            AttributesUI attrUI = attrCanvasGo.AddComponent<AttributesUI>();
            SetObjectField(attrUI, "_attributes", attributes);
            SetObjectField(attrUI, "_affinity", affinity);
        }

        // Garante um EventSystem com o módulo do NOVO Input System (o projeto usa
        // só o Input System novo — activeInputHandler = 1). Sem isso, os cliques nos
        // botões do Altar não funcionariam.
        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();

            // Bug conhecido do pacote Input System: depois de o Test Runner rodar, o estado
            // interno do pacote fica com as ações padrão "órfãs" (fora de um InputActionAsset)
            // e o AssignDefaultActions estoura InvalidOperationException — só até o próximo
            // domain reload (recompilar/reabrir o Editor). Não pode derrubar a geração da
            // cena: com o campo vazio, o próprio módulo atribui as ações padrão sozinho no
            // Play (OnEnable). Então tenta já no Editor; se a Unity estiver nesse estado,
            // avisa e segue — a UI continua clicável em Play Mode.
            try
            {
                module.AssignDefaultActions(); // ações de UI padrão (apontar/clicar) prontas
            }
            catch (System.InvalidOperationException e)
            {
                Debug.LogWarning("[Setup] Ações de UI não atribuídas no Editor (estado interno " +
                    "do Input System após o Test Runner — inofensivo: o módulo atribui as padrão " +
                    "sozinho no Play). Se algum botão não clicar, gere a cena de novo após " +
                    "recompilar/reabrir o Editor. Detalhe: " + e.Message);
            }
        }

        private static void CreateHUD()
        {
            GameObject canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.transform.localScale = Vector3.one;

            // Obs.: o sprite branco (necessário para a Image renderizar) é atribuído
            // em runtime pelo HealthBarHUD — evita problemas de import de asset no Editor.

            // Fundo da barra, ancorado no canto superior esquerdo.
            GameObject bg = new GameObject("BarraDeVida_Fundo", typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            Image bgImg = bg.GetComponent<Image>();
            bgImg.type = Image.Type.Simple;
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);
            RectTransform bgRt = bgImg.rectTransform;
            bgRt.anchorMin = new Vector2(0f, 1f);
            bgRt.anchorMax = new Vector2(0f, 1f);
            bgRt.pivot = new Vector2(0f, 1f);
            bgRt.anchoredPosition = new Vector2(20f, -20f);
            bgRt.sizeDelta = new Vector2(260f, 28f);

            // Preenchimento verde, filho do fundo. A largura é controlada pela
            // âncora (anchorMax.x) no HealthBarHUD — não depende de sprite/Filled.
            GameObject fill = new GameObject("Preenchimento", typeof(Image));
            fill.transform.SetParent(bg.transform, false);
            Image fillImg = fill.GetComponent<Image>();
            fillImg.type = Image.Type.Simple;
            fillImg.color = new Color(0.2f, 0.8f, 0.25f);
            RectTransform fillRt = fillImg.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            HealthBarHUD hud = canvasGo.AddComponent<HealthBarHUD>();
            SetObjectField(hud, "_fill", fillImg);
            SetObjectField(hud, "_background", bgImg);
        }

        private static void SetupCamera()
        {
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[Setup] Câmera principal não encontrada; criando uma.");
                GameObject camGo = new GameObject("Main Camera", typeof(UnityEngine.Camera));
                camGo.tag = "MainCamera";
                mainCamera = camGo.GetComponent<UnityEngine.Camera>();
            }

            if (mainCamera.GetComponent<PlayerCamera>() == null)
            {
                mainCamera.gameObject.AddComponent<PlayerCamera>();
            }
        }

        private static void CreateSampleInteractable()
        {
            // Marco de Ressonância no hub: cristal interativo (demonstra a interação por E).
            GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.name = "Marco de Ressonância";
            crystal.transform.position = MapLayout.At(MapLayout.Hub, -3.5f, 0.7f, -1.5f);
            crystal.transform.localScale = new Vector3(0.5f, 1.4f, 0.5f);
            crystal.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            ApplyColor(crystal, new Color(0.45f, 0.85f, 0.85f));
            crystal.AddComponent<SampleInteractable>();
        }

        // Diretor de VFX (fase de arte): objeto de cena que escuta EcoCollected/AbilityUsed
        // e dispara as partículas (absorção de Eco + burst de habilidade). Recebe o material
        // aditivo do glow como ASSET (shader entra no build; ver ParticleFactory/VfxDirector).
        private static void CreateVfxDirector()
        {
            var go = new GameObject("VfxDirector");
            VfxDirector director = go.AddComponent<VfxDirector>();
            Material glow = EnsureParticleGlowMaterial();
            if (glow != null) SetObjectField(director, "_glowMaterial", glow);
        }

        // Cria (ou reusa, GUID estável) o material aditivo das partículas de glow.
        private static Material EnsureParticleGlowMaterial()
        {
            const string path = "Assets/_Project/Art/M_ParticleGlow.mat";
            Shader s = Shader.Find("JogoDaTrupe/ParticleGlow");
            if (s == null) return null;

            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Art");
                m = new Material(s);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != s)
            {
                m.shader = s;
            }
            return m;
        }

        // Pinta o objeto com a cor pedida via a fábrica única de materiais de Editor
        // (mesma política URP do ForestWorld — sem duplicar a lógica, ENGINEERING_RULES #1).
        private static void ApplyColor(GameObject go, Color color) => EditorMaterials.Paint(go, color);

        // Liga um campo privado [SerializeField] que referencia um objeto (ex.: o
        // Image do HUD), via SerializedObject, já que não dá para atribuir por código direto.
        // Internal: reusado pelo EnemyPrefabSetup (montagem dos prefabs de rede) — DRY.
        internal static void SetObjectField(Object target, string fieldName, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Igual ao SetObjectField, mas para um campo numérico (float).
        // Internal: reusado pelo EnemyPrefabSetup (montagem dos prefabs de rede) — DRY.
        internal static void SetFloatField(Object target, string fieldName, float value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Igual ao SetObjectField, mas para um campo inteiro (int).
        private static void SetIntField(Object target, string fieldName, int value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.intValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Liga um campo que é um ARRAY de referências de objeto (ex.: as receitas da CraftingUI).
        private static void SetObjectArrayField(Object target, string fieldName, Object[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SaveAndRegisterScene(Scene scene, string scenePath)
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            }

            EditorSceneManager.SaveScene(scene, scenePath);

            // Garante que a cena esteja nas Build Settings, na primeira posição.
            var entries = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool alreadyListed = entries.Exists(s => s.path == scenePath);
            if (!alreadyListed)
            {
                entries.Insert(0, new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = entries.ToArray();
            }

            AssetDatabase.SaveAssets();
        }
    }
}

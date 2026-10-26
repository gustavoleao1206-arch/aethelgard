using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using JogoDaTrupe.Presentation;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Monta o HERÓI ANIMADO no padrão code-gen do projeto. Duas responsabilidades:
    ///
    /// 1) <see cref="PrepareHero"/> (menu, rodar UMA vez): conserta materiais dos packs pra
    ///    URP (tira o rosa), põe os clipes do Mixamo (crouch/slide) em rig Humanoid pra
    ///    retargetar no herói, garante loop nos clipes cíclicos e GERA o <c>Hero.controller</c>
    ///    (estados Idle/Walk/Run/Jump/Fall/Crouch/Slide).
    ///
    /// 2) <see cref="AttachHeroVisual"/> (chamado pelo PlayerSceneSetup ao gerar a cena):
    ///    instancia o mesh do herói como filho do Player, escala/assenta pela CharacterController,
    ///    liga o Animator no Hero.controller e adiciona o <see cref="PlayerAnimator"/> — a
    ///    cápsula some (renderer desligado) mas a colisão continua.
    ///
    /// Locomoção vem do Kevin Iglesias "Human Basic Motions" (Humanoid); agachar/deslizar do
    /// Mixamo. Sem root motion: o CharacterController move o corpo, a animação só dá vida.
    /// </summary>
    internal static class HeroSetup
    {
        private const string HeroPrefab = "Assets/RPGHero/Prefabs/RPGHeroPolyart.prefab";
        // Pacote do herói: traz clipes .anim de espada+escudo (locomoção, 2 golpes, flinch,
        // morte) feitos PARA ESTE rig — sem retarget, sem risco de facing torto.
        private const string HeroPackRoot = "Assets/RPGHero";
        private const string KevinRoot = "Assets/Kevin Iglesias";
        private const string MixamoRoot = "Assets/_Project/Animations/Mixamo";
        private const string ControllerPath = "Assets/_Project/Animation/Hero.controller";
        private const string NetworkPlayerPrefab = "Assets/_Project/Prefabs/P_Player.prefab";
        private const float HeroHeight = 1.85f;  // altura visual alvo (a CharacterController tem 2m)
        private const float HeroYawOffset = 0f;  // clipes do Kevin (idle/walk/run) já apontam +Z; sem offset
        // Facing dos clipes do Mixamo (ver FixMixamoFacing). CONFIRMADO em Play pelo Diretor:
        // • deslize/idle nascem ~+90° virados → base "Original" + giro -90° casa com o Kevin;
        // • "Crouched Walking" tem orientação atravessada que NÃO casa por offset fixo (deu
        //   direita a -90° e esquerda a -180°) → usa "Body Orientation": o Unity calcula o
        //   forward do próprio corpo e alinha sozinho, sem eu chutar ângulo. Tratamento POR clipe.
        // • "Sword And Shield Block Idle" já nasce alinhado: a -90° a guarda ia pra DIREITA e a
        //   -180° pra TRÁS (medido em Play) — ou seja, offset negativo gira pra direita. 90° à
        //   esquerda de -90° é 0°: o clipe não precisa de giro nenhum, só da base "Original".
        private const float MixamoYawFix = -90f;   // deslize + idle (base Original)
        private const float MixamoBlockYawFix = 0f; // guarda com escudo (base Original, sem giro)

        [MenuItem("Jogo da Trupe/Personagens/Preparar herói (materiais + rig + controller)")]
        public static void PrepareHero()
        {
            AquariusTreeFix.FixTreeMaterials(); // materiais dos packs (incl. herói) -> URP
            MakeMixamoHumanoid();               // crouch/slide viram Humanoid (retarget no herói)
            EnsureLoops();                      // idle/andar/correr/agachar em loop
            FixMixamoFacing();                  // agachar/deslizar apontam +Z como o Kevin (tira o "de lado")
            EnsureController();                 // gera o Hero.controller
            AssetDatabase.SaveAssets();
            Debug.Log("[Personagens] ✅ Herói preparado (materiais URP + Mixamo Humanoid + facing + Hero.controller). " +
                      "Agora regere a cena solo pra ver o boneco no lugar da cápsula.");
        }

        // ---- Preparo dos assets ----

        private static void MakeMixamoHumanoid()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { MixamoRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                if (imp.animationType != ModelImporterAnimationType.Human)
                {
                    imp.animationType = ModelImporterAnimationType.Human;
                    imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    imp.SaveAndReimport();
                }
            }
        }

        private static void EnsureLoops()
        {
            SetLoop(FindClip(MixamoRoot, "Idle"), true); // X Bot@Idle (Mixamo) adicionado pelo Diretor
            SetLoop(FindClip(KevinRoot, "Idle01") ?? FindClip(KevinRoot, "Idle"), true);
            SetLoop(FindClip(KevinRoot, "Walk01_Forward") ?? FindClip(KevinRoot, "Walk"), true);
            SetLoop(FindClip(KevinRoot, "Run01_Forward") ?? FindClip(KevinRoot, "Run"), true);
            SetLoop(FindClip(MixamoRoot, "Crouched Walking") ?? FindClip(MixamoRoot, "Crouch"), true);
            SetLoop(FindClip(MixamoRoot, "Block Idle") ?? FindClip(MixamoRoot, "Block"), true); // guarda sustentada

            // Locomoção do pacote do herói: são .anim STANDALONE (sem ModelImporter) → o loop
            // mora nas settings do próprio clipe. Golpe/flinch/morte ficam SEM loop (one-shot).
            SetLoopAnim(FindClip(HeroPackRoot, "Idle_SwordShield"), true);
            SetLoopAnim(FindClip(HeroPackRoot, "Walk_SwordShield"), true);
            SetLoopAnim(FindClip(HeroPackRoot, "Run_SwordShield"), true);
        }

        // Loop de um AnimationClip .anim avulso (não vem de FBX, logo não tem ModelImporter).
        private static void SetLoopAnim(AnimationClip clip, bool loop)
        {
            if (clip == null) return;
            AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
            if (s.loopTime == loop) return;
            s.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            EditorUtility.SetDirty(clip);
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            if (clip == null) return;
            string path = AssetDatabase.GetAssetPath(clip);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return;
            ModelImporterClipAnimation[] clips = imp.clipAnimations;
            if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            bool changed = false;
            foreach (var c in clips)
            {
                if (c.loopTime != loop) { c.loopTime = loop; changed = true; }
                // Loop Pose: casa a pose de início/fim p/ o loop ficar CONTÍNUO (mata o "reset"
                // do idle). Só faz sentido em clipe que loopa.
                if (loop && !c.loopPose) { c.loopPose = true; changed = true; }
            }
            if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }
        }

        // Os clipes do Mixamo (agachar/deslizar) foram authorados com o CORPO ~90° virado em
        // relação ao Kevin (idle/andar/correr). Como o mesh do herói tem yaw 0 (alinhado ao
        // Kevin), esses 2 apareceriam "de lado". Aqui ligamos "Bake Into Pose" na ROTAÇÃO
        // (a orientação vira parte da pose, não vira root motion — que seria descartado sem
        // root motion) e aplicamos um offset p/ o corpo apontar +Z como os demais. Some o
        // desalinhamento SÓ nessas 2 ações, sem tocar no transform (que quebraria idle/andar).
        private static void FixMixamoFacing()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { MixamoRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                ModelImporterClipAnimation[] clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
                if (clips == null || clips.Length == 0) continue;
                // Agachar: Body Orientation (Unity acha o forward do corpo, offset 0).
                // Guarda: Original + -180° (nasce 90° a mais virado que os outros).
                // Deslize/idle: Original + -90° (confirmado bom no deslize).
                var cmp = System.StringComparison.OrdinalIgnoreCase;
                bool isCrouch = path.IndexOf("Crouch", cmp) >= 0;
                bool isBlock = path.IndexOf("Block", cmp) >= 0;
                bool keepOriginal = !isCrouch;
                float target = isCrouch ? 0f : (isBlock ? MixamoBlockYawFix : MixamoYawFix);
                bool changed = false;
                foreach (var c in clips)
                {
                    if (!c.lockRootRotation) { c.lockRootRotation = true; changed = true; }              // Bake Into Pose (rotação)
                    if (c.keepOriginalOrientation != keepOriginal) { c.keepOriginalOrientation = keepOriginal; changed = true; } // Original (offset puro) vs Body Orientation
                    if (Mathf.Abs(c.rotationOffset - target) > 0.01f) { c.rotationOffset = target; changed = true; }
                }
                if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }
            }
        }

        // ---- Prefab de rede (co-op) ----

        // Aplica o mesh do herói ao P_Player (prefab que a rede spawna) — mesma técnica da cena
        // solo, mas editando o ASSET do prefab. Sem isto, no co-op os jogadores nascem como
        // cápsula. Idempotente: remove um HeroVisual anterior antes de reanexar.
        [MenuItem("Jogo da Trupe/Personagens/Aplicar herói ao prefab de rede (P_Player)")]
        public static void ApplyHeroToNetworkPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPrefab) == null)
            {
                Debug.LogError("[Personagens] Prefab de rede não encontrado: " + NetworkPlayerPrefab);
                return;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(NetworkPlayerPrefab);
            try
            {
                Transform old = root.transform.Find("HeroVisual");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                AttachHeroVisual(root);
                PrefabUtility.SaveAsPrefabAsset(root, NetworkPlayerPrefab);
                Debug.Log("[Personagens] ✅ Herói aplicado ao prefab de rede P_Player — co-op deixa de nascer como cápsula.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---- Controller ----

        private static void EnsureController()
        {
            EnsureFolder("Assets/_Project/Animation");

            // LOCOMOÇÃO: Kevin Iglesias (idle/andar/correr). Decisão do Diretor — a locomoção do
            // pacote do herói (_SwordShield) fica em pose de combate e anda com as pernas
            // arrastadas; a do Kevin tem o passo certo. Idle vem da MESMA fonte do walk/run
            // (mesma orientação); o idle do Mixamo aponta 90° diferente e desalinha o rosto.
            // Loop Pose é ligado no EnsureLoops. Fallback no pacote do herói se o Kevin sumir.
            AnimationClip idle = FindClip(KevinRoot, "Idle01") ?? FindClip(HeroPackRoot, "Idle_SwordShield");
            AnimationClip walk = FindClip(KevinRoot, "Walk01_Forward") ?? FindClip(HeroPackRoot, "Walk_SwordShield");
            AnimationClip run = FindClip(KevinRoot, "Run01_Forward") ?? FindClip(HeroPackRoot, "Run_SwordShield");
            // COMBATE (pacote do herói, com espada e escudo): 2 golpes alternados, dano e morte.
            AnimationClip atk1 = FindClip(HeroPackRoot, "NormalAttack01");
            AnimationClip atk2 = FindClip(HeroPackRoot, "NormalAttack02") ?? atk1;
            AnimationClip hit = FindClip(HeroPackRoot, "GetHit_SwordShield");
            AnimationClip die = FindClip(HeroPackRoot, "Die_SwordShield");
            AnimationClip jump = FindClip(KevinRoot, "Jump01") ?? FindClip(KevinRoot, "Jump");
            AnimationClip fall = FindClip(KevinRoot, "Fall01") ?? FindClip(KevinRoot, "Fall") ?? jump;
            AnimationClip crouch = FindClip(MixamoRoot, "Crouched Walking") ?? FindClip(MixamoRoot, "Crouch");
            AnimationClip slide = FindClip(MixamoRoot, "Running Slide") ?? FindClip(MixamoRoot, "Slide");
            // Guarda com o escudo (Mixamo "Sword And Shield Block Idle", baixado pelo Diretor).
            AnimationClip block = FindClip(MixamoRoot, "Block Idle") ?? FindClip(MixamoRoot, "Block");

            if (idle == null)
            {
                Debug.LogError("[Personagens] Não achei nenhum clipe de Idle no Kevin Iglesias — controller não gerado.");
                return;
            }

            // REUSA o asset se já existe (GUID ESTÁVEL). Deletar+recriar trocaria o GUID e o
            // Animator já salvo na cena viraria "None" → herói em T-pose. Aqui só limpamos e
            // reconstruímos os estados IN-PLACE, preservando a identidade do asset.
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                     ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine sm = ac.layers[0].stateMachine;
            foreach (var existing in sm.states) sm.RemoveState(existing.state); // limpa estados antigos

            // Estados nomeados (o PlayerAnimator dá CrossFade por nome — sem transições).
            // Fallbacks pra idle garantem que TODO estado exista (CrossFade nunca falha).
            AnimatorState sIdle = AddState(sm, "Idle", idle);
            AddState(sm, "Walk", walk ?? idle);
            AddState(sm, "Run", run ?? walk ?? idle);
            AddState(sm, "Jump", jump ?? idle);
            AddState(sm, "Fall", fall ?? idle);
            AddState(sm, "Crouch", crouch ?? idle);
            AddState(sm, "Slide", slide ?? run ?? idle);
            // Combate: 2 golpes (alternados pelo ComboIndex), guarda, flinch e morte.
            AddState(sm, "Attack1", atk1 ?? idle);
            AddState(sm, "Attack2", atk2 ?? atk1 ?? idle);
            AddState(sm, "Block", block ?? idle);
            AddState(sm, "Hit", hit ?? idle);
            AddState(sm, "Death", die ?? idle);
            sm.defaultState = sIdle;

            EditorUtility.SetDirty(ac);
            Debug.Log("[Personagens] Hero.controller gerado (Idle/Walk/Run/Jump/Fall/Crouch/Slide/Attack1/Attack2/Block/Hit/Death).");
        }

        // internal: reaproveitado pelo EnemySetup (mesmo padrão code-gen p/ mobs/boss).
        internal static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion)
        {
            AnimatorState st = sm.AddState(name);
            st.motion = motion;
            st.writeDefaultValues = true;
            return st;
        }

        // ---- Anexar o mesh do herói ao Player (chamado ao gerar a cena) ----

        internal static void AttachHeroVisual(GameObject player)
        {
            var heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefab);
            if (heroPrefab == null)
            {
                Debug.LogWarning("[Personagens] Prefab do herói não encontrado (" + HeroPrefab +
                                 ") — cápsula mantida. Importou o RPG Hero?");
                return;
            }

            // REMOVE o visual da cápsula (destrói, não só desliga): a PlayerCamera religa
            // TODOS os renderers do corpo em 3ª pessoa (ApplyBodyVisibility), então apenas
            // desligar não bastaria — a cápsula opaca voltaria e taparia o herói. A colisão
            // continua com o CharacterController; a câmera passa a achar só o mesh do herói.
            var capsuleRenderer = player.GetComponent<MeshRenderer>();
            if (capsuleRenderer != null) Object.DestroyImmediate(capsuleRenderer);
            var capsuleFilter = player.GetComponent<MeshFilter>();
            if (capsuleFilter != null) Object.DestroyImmediate(capsuleFilter);

            // Instancia o mesh do herói como filho.
            var hero = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab, player.transform);
            hero.name = "HeroVisual";
            hero.transform.localPosition = Vector3.zero;
            hero.transform.localRotation = Quaternion.Euler(0f, HeroYawOffset, 0f); // corrige o "rosto" pro forward

            FitToController(player, hero);

            // Animator no Hero.controller (o avatar Humanoid do herói vem do próprio prefab).
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { EnsureController(); controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath); }
            Animator animator = hero.GetComponentInChildren<Animator>();
            if (animator == null) animator = hero.AddComponent<Animator>();
            if (controller != null) animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // Driver de animação no root do player.
            if (player.GetComponent<PlayerAnimator>() == null) player.AddComponent<PlayerAnimator>();
        }

        // Mede os bounds do mesh, escala pra HeroHeight e assenta os PÉS na base do CharacterController.
        private static void FitToController(GameObject player, GameObject hero)
        {
            var cc = player.GetComponent<CharacterController>();
            float bottomLocalY = cc != null ? cc.center.y - cc.height * 0.5f : -1f;

            if (!TryMeasure(hero, out Bounds b)) return;
            if (b.size.y > 0.001f)
            {
                float scale = HeroHeight / b.size.y;
                hero.transform.localScale *= scale;
            }
            if (!TryMeasure(hero, out b)) return;

            // Player nasce sem rotação/escala custom -> delta em mundo == delta local em Y.
            float desiredFeetWorldY = player.transform.position.y + bottomLocalY;
            hero.transform.localPosition += new Vector3(0f, desiredFeetWorldY - b.min.y, 0f);
        }

        internal static bool TryMeasure(GameObject go, out Bounds bounds)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            bounds = default;
            if (rends.Length == 0) return false;
            bounds = rends[0].bounds;
            foreach (var r in rends) bounds.Encapsulate(r.bounds);
            return true;
        }

        // ---- Utilitários ----

        // Acha o MELHOR AnimationClip cujo nome/arquivo casa com o trecho. Pontua p/ escolher
        // o clipe certo quando há vários parecidos (ex.: prefere HumanM@Idle01, evita a
        // transição HumanM@Idle01-Idle02 e a versão feminina HumanF@Idle01).
        internal static AnimationClip FindClip(string root, string nameContains)
        {
            if (!AssetDatabase.IsValidFolder(root)) return null;

            AnimationClip best = null;
            int bestScore = int.MinValue;
            var cmp = System.StringComparison.OrdinalIgnoreCase;

            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool fileMatches = Path.GetFileNameWithoutExtension(path).IndexOf(nameContains, cmp) >= 0;
                // LoadAllAssets (não só Representations): clipes embutidos em FBX vêm como
                // sub-asset (representation), mas .anim STANDALONE (ex.: Grunt) é o asset
                // PRINCIPAL — Representations o ignoraria e o clip não seria achado.
                foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var clip = obj as AnimationClip;
                    if (clip == null || clip.name.StartsWith("__preview__")) continue;
                    string n = clip.name;
                    bool nameMatches = n.IndexOf(nameContains, cmp) >= 0;
                    if (!nameMatches && !fileMatches) continue;

                    int score = 0;
                    if (n.IndexOf("HumanM", cmp) >= 0) score += 4;   // herói é masculino
                    if (n.IndexOf("HumanF", cmp) >= 0) score -= 4;   // evita o clipe feminino
                    if (n.Contains("-")) score -= 6;                 // evita transições (Idle01-Idle02) e "- Begin/Land"
                    if (n.Contains("[")) score -= 5;                 // evita variantes RootMotion "[RM]"
                    if (nameMatches) score += 2;                     // casar pelo nome > pelo arquivo
                    score -= Mathf.Abs(n.Length - nameContains.Length); // prefere o nome mais "justo"

                    if (score > bestScore) { bestScore = score; best = clip; }
                }
            }
            return best;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}

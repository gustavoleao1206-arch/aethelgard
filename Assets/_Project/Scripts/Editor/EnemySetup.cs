using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using JogoDaTrupe.Gameplay.Enemies;
using JogoDaTrupe.Presentation;
using JogoDaTrupe.Presentation.Vfx;
using JogoDaTrupe.UI;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Troca a CÁPSULA dos inimigos/boss pelo MODELO 3D real, no mesmo padrão code-gen do
    /// herói (<see cref="HeroSetup"/>): destrói o visual da cápsula, instancia o mesh como
    /// filho "CreatureVisual", assenta pela CapsuleCollider, gera um AnimatorController
    /// GUID-estável e liga o Animator + o driver <see cref="EnemyAnimator"/> (mobs).
    ///
    /// Enganchado nos DOIS geradores (igual o herói precisou): cena solo em
    /// <c>PlayerSceneSetup.SpawnEnemy/SpawnBoss</c> e prefab de rede em
    /// <c>EnemyPrefabSetup.CreatePrefab</c>. Também há um menu p/ aplicar aos prefabs de rede
    /// já existentes sem regerar a cena.
    ///
    /// MAPA CRIATIVO (decisão do Diretor 07/07/2026): Berserker=Grunt, HitAndRun=Lich,
    /// Zigzag=Footman; boss "Eco Corrompido" = dragão TerrorBringer Roxo. Trocar aqui é 1 linha.
    /// </summary>
    internal static class EnemySetup
    {
        // Modelo + clipes de um mob. Nomes de clipe são case-insensitive (o FindClip do herói
        // pontua o melhor match); o casing/nome de "morte" varia por pacote (Death/Die/die).
        private sealed class CreatureModel
        {
            public string prefab;
            public string clipRoot;
            public string idle, walk, run, attack, death;
            public float height;
        }

        private static readonly Dictionary<string, CreatureModel> Mobs = new Dictionary<string, CreatureModel>
        {
            ["enemy_berserker"] = new CreatureModel   // BRUTO
            {
                prefab = "Assets/Mini Legion Grunt PBR HP Polyart/Prefab/GruntPolyart.prefab",
                clipRoot = "Assets/Mini Legion Grunt PBR HP Polyart/Animations",
                idle = "Idle", walk = "Walk", run = "Run", attack = "Attack01", death = "Die", height = 4.4f,
            },
            ["enemy_hitandrun"] = new CreatureModel    // MAGO (ataca de longe)
            {
                prefab = "Assets/Mini Legion Lich PBR HP Polyart/Prefabs/FreeLichPolyart.prefab",
                clipRoot = "Assets/Mini Legion Lich PBR HP Polyart/Animations",
                idle = "idle", walk = "walk", run = "run", attack = "attack01", death = "die", height = 4.2f,
            },
            ["enemy_zigzag"] = new CreatureModel       // SOLDADO BLINDADO
            {
                prefab = "Assets/Mini Legion Footman PBR HP Polyart/Prefabs/FootmanPolyart.prefab",
                clipRoot = "Assets/Mini Legion Footman PBR HP Polyart/Animations",
                idle = "Idle", walk = "Walk", run = "Run", attack = "Attack01", death = "Death", height = 4.2f,
            },
        };

        // Boss = dragão TerrorBringer Roxo (rig GENERIC → usa clipe do próprio dragão). Por ora
        // só idle no chão (imponente no altar); ataque/fases animados ficam p/ depois.
        private const string BossEnemyId = "boss_ecocorrompido";
        private const string BossPrefab = "Assets/FourEvilDragonsHP/Prefab/DragonTerrorBringer/Purple.prefab";
        private const string BossClipRoot = "Assets/FourEvilDragonsHP/Animations/DragonTerrorBringer";
        // Dragão VOADOR imponente (escolha do Diretor 07/07/2026): usa as animações de VOO do
        // próprio dragão — paira parado, voa pra frente ao perseguir. Fica intencional (não o
        // bug de "animação de chão no ar" que parecia travado).
        private const string BossIdleClip = "FlyIdle";
        private const string BossWalkClip = "FlyForward";
        private const string BossAttackClip = "FlyAttack"; // mergulho de ataque (BossController.IsAttacking)
        private const float BossHeight = 10.0f; // dragão GRANDE e imponente (dobrado a pedido do Diretor)
        // A animação de VOO levanta o corpo do dragão ~esta altura acima do root. Baixamos o
        // VISUAL nisto pra ele alinhar com a hitbox/root — assim o mergulho traz o dragão
        // VISÍVEL até o chão (senão só a hitbox desce e não dá pra acertar). Tunável.
        private const float BossVisualDrop = 7.5f;

        private const string ControllerFolder = "Assets/_Project/Animation";

        // enemyId → prefab de rede (co-op).
        private static readonly Dictionary<string, string> NetPrefabs = new Dictionary<string, string>
        {
            ["enemy_berserker"] = "Assets/_Project/Prefabs/P_Enemy_Berserker.prefab",
            ["enemy_hitandrun"] = "Assets/_Project/Prefabs/P_Enemy_HitAndRun.prefab",
            ["enemy_zigzag"] = "Assets/_Project/Prefabs/P_Enemy_Zigzag.prefab",
            [BossEnemyId] = "Assets/_Project/Prefabs/P_Boss_EcoCorrompido.prefab",
        };

        // ---- Ponto de entrada (chamado pelos geradores: cena solo + prefab de rede) ----

        internal static void AttachCreatureVisual(GameObject root, string enemyId, bool isBoss)
        {
            if (root == null) return;
            if (isBoss) AttachBoss(root);
            else AttachMob(root, enemyId);
        }

        /// <summary>
        /// Dá NAVEGAÇÃO ao mob (<see cref="EnemyNavigator"/>): ele contorna colunas, arcos e
        /// árvores em vez de atravessá-las. **Só mobs** — o boss é um dragão VOADOR e não anda
        /// na malha. Enganchado nos dois geradores (cena solo + prefab de rede).
        ///
        /// Sem <c>NavMeshAgent</c> de propósito: o agente só devolve direção confiável quando ele
        /// mesmo dirige o transform, e ele não pode — quem move o corpo é o `EnemyController`
        /// (e, no cliente do co-op, o `NetworkTransform`). O navegador calcula a rota e segue os
        /// cantos dela, sem tocar no transform.
        /// </summary>
        internal static void AddNavigation(GameObject root)
        {
            if (root == null || root.GetComponent<EnemyNavigator>() != null) return;
            root.AddComponent<EnemyNavigator>();
        }

        // ---- Menu: aplica os modelos aos prefabs de rede já existentes (co-op) ----

        [MenuItem("Jogo da Trupe/Personagens/Aplicar modelos aos inimigos (prefabs de rede)")]
        public static void ApplyModelsToNetworkPrefabs()
        {
            int done = 0;
            foreach (KeyValuePair<string, string> kv in NetPrefabs)
            {
                string id = kv.Key, path = kv.Value;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning("[Personagens] Prefab de rede não encontrado: " + path);
                    continue;
                }
                GameObject r = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform old = r.transform.Find("CreatureVisual");
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                    AttachCreatureVisual(r, id, id == BossEnemyId);
                    PrefabUtility.SaveAsPrefabAsset(r, path);
                    done++;
                }
                finally { PrefabUtility.UnloadPrefabContents(r); }
            }
            Debug.Log("[Personagens] ✅ Modelos aplicados a " + done + " prefab(s) de rede (mobs + boss) — co-op deixa de nascer como cápsula.");
        }

        // ---- Mob ----

        private static void AttachMob(GameObject root, string enemyId)
        {
            if (!Mobs.TryGetValue(enemyId, out CreatureModel m))
            {
                Debug.LogWarning("[Personagens] Sem modelo mapeado p/ enemyId '" + enemyId + "' — cápsula mantida.");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(m.prefab);
            if (prefab == null) { Debug.LogWarning("[Personagens] Modelo não encontrado: " + m.prefab + " — cápsula mantida."); return; }

            StripCapsuleVisual(root);
            GameObject vis = InstantiateVisual(prefab, root);
            FitVisual(root, vis, m.height);

            AnimatorController ctrl = EnsureMobController(enemyId, m);
            BindAnimator(vis, ctrl);

            if (root.GetComponent<EnemyAnimator>() == null) root.AddComponent<EnemyAnimator>();
            // Barra de vida flutuante estilo Palworld (só mobs; o boss usa a barra do topo).
            if (root.GetComponent<FloatingHealthBar>() == null) root.AddComponent<FloatingHealthBar>();
        }

        // Gera Enemy_<id>.controller (GUID-estável, reusa in-place) com Idle/Walk/Run/Attack/Death.
        private static AnimatorController EnsureMobController(string enemyId, CreatureModel m)
        {
            HeroSetup.EnsureFolder(ControllerFolder);
            string path = ControllerFolder + "/Enemy_" + enemyId + ".controller";

            AnimationClip idle = HeroSetup.FindClip(m.clipRoot, m.idle);
            if (idle == null)
            {
                Debug.LogError("[Personagens] Idle não achado p/ " + enemyId + " em " + m.clipRoot + " — controller não gerado.");
                return null;
            }
            AnimationClip walk = HeroSetup.FindClip(m.clipRoot, m.walk) ?? idle;
            AnimationClip run = HeroSetup.FindClip(m.clipRoot, m.run) ?? walk;
            AnimationClip atk = HeroSetup.FindClip(m.clipRoot, m.attack) ?? idle;
            AnimationClip die = HeroSetup.FindClip(m.clipRoot, m.death) ?? idle;

            return BuildController(path, new (string, AnimationClip)[]
            {
                ("Idle", idle), ("Walk", walk), ("Run", run), ("Attack", atk), ("Death", die),
            });
        }

        // ---- Boss (dragão Generic: só Idle no chão por ora) ----

        private static void AttachBoss(GameObject root)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefab);
            if (prefab == null) { Debug.LogWarning("[Personagens] Dragão do boss não encontrado: " + BossPrefab + " — cápsula mantida."); return; }

            StripCapsuleVisual(root);
            GameObject vis = InstantiateVisual(prefab, root);
            FitVisual(root, vis, BossHeight);
            // Compensa o "levantar" da animação de voo: baixa o visual pra alinhar com o root/
            // hitbox, pra o mergulho descer o dragão VISÍVEL até o jogador (não só a hitbox).
            float sy = Mathf.Max(1e-4f, root.transform.lossyScale.y);
            vis.transform.localPosition += Vector3.down * (BossVisualDrop / sy);
            BindAnimator(vis, EnsureBossController());

            // Driver de voo (Idle/Walk/Attack por velocidade + IsAttacking). Sem isso o dragão
            // DESLIZARIA travado quando o BossController o move.
            if (root.GetComponent<BossAnimator>() == null) root.AddComponent<BossAnimator>();
            // Cospe FOGO pela boca durante o mergulho de ataque.
            if (root.GetComponent<BossFireVfx>() == null) root.AddComponent<BossFireVfx>();
        }

        private static AnimatorController EnsureBossController()
        {
            HeroSetup.EnsureFolder(ControllerFolder);
            NormalizeBossClips(); // desfaz bake de aterrar (o dragão é VOADOR: mantém a altura do voo)
            string path = ControllerFolder + "/Boss_EcoCorrompido.controller";
            AnimationClip idle = HeroSetup.FindClip(BossClipRoot, BossIdleClip) ?? HeroSetup.FindClip(BossClipRoot, "idle");
            if (idle == null)
            {
                Debug.LogError("[Personagens] Idle do dragão não achado em " + BossClipRoot + " — controller não gerado.");
                return null;
            }
            AnimationClip walk = HeroSetup.FindClip(BossClipRoot, BossWalkClip) ?? idle;
            AnimationClip attack = HeroSetup.FindClip(BossClipRoot, BossAttackClip) ?? walk;
            // Voo: Idle=pairar, Walk=voar, Attack=mergulho (BossController.IsAttacking).
            return BuildController(path, new (string, AnimationClip)[] { ("Idle", idle), ("Walk", walk), ("Attack", attack) });
        }

        // ---- Helpers ----

        // Restaura o tratamento PADRÃO do eixo Y nos clipes do dragão (desfaz um bake de aterrar
        // que tentei antes). Ele é VOADOR: FlyIdle/FlyForward devem manter a altura NATURAL do
        // voo — nada de forçar os pés no chão. Idempotente.
        private static void NormalizeBossClips()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { BossClipRoot }))
            {
                string clipPath = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(clipPath) as ModelImporter;
                if (imp == null) continue;
                ModelImporterClipAnimation[] clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
                if (clips == null || clips.Length == 0) continue;
                bool changed = false;
                foreach (var c in clips)
                {
                    if (c.lockRootHeightY) { c.lockRootHeightY = false; changed = true; }        // Bake Into Pose (Y) OFF (padrão)
                    if (!c.keepOriginalPositionY) { c.keepOriginalPositionY = true; changed = true; } // Based Upon: Original
                    if (c.heightFromFeet) { c.heightFromFeet = false; changed = true; }
                }
                if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }
            }
        }

        // Reusa o asset (GUID ESTÁVEL — não deletar/recriar) e reconstrói os estados in-place.
        private static AnimatorController BuildController(string path, (string name, AnimationClip clip)[] states)
        {
            AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                                    ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine sm = ac.layers[0].stateMachine;
            foreach (var existing in sm.states) sm.RemoveState(existing.state);

            AnimatorState first = null;
            foreach ((string name, AnimationClip clip) in states)
            {
                AnimatorState st = HeroSetup.AddState(sm, name, clip);
                if (first == null) first = st;
            }
            if (first != null) sm.defaultState = first;
            EditorUtility.SetDirty(ac);
            return ac;
        }

        private static void StripCapsuleVisual(GameObject root)
        {
            var mr = root.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);
            var mf = root.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf);
        }

        private static GameObject InstantiateVisual(GameObject prefab, GameObject root)
        {
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            vis.name = "CreatureVisual";
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;
            return vis;
        }

        private static void BindAnimator(GameObject vis, AnimatorController ctrl)
        {
            Animator anim = vis.GetComponentInChildren<Animator>();
            if (anim == null) anim = vis.AddComponent<Animator>();
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false; // o controller/rede move; a anim só toca no lugar
        }

        // Mede bounds, escala p/ targetHeight e assenta os PÉS na base da CapsuleCollider.
        // Robusto a pai ESCALADO (o boss tem escala 1.8): trabalha em mundo e converte o delta
        // de volta p/ o espaço local do pai.
        private static void FitVisual(GameObject root, GameObject vis, float targetHeight)
        {
            float parentScaleY = root.transform.lossyScale.y;
            if (Mathf.Abs(parentScaleY) < 1e-4f) parentScaleY = 1f;

            float bottomLocalY = -1f;
            var cap = root.GetComponent<CapsuleCollider>();
            if (cap != null) bottomLocalY = cap.center.y - cap.height * 0.5f;

            if (!HeroSetup.TryMeasure(vis, out Bounds b)) return;
            if (b.size.y > 0.001f) vis.transform.localScale *= (targetHeight / b.size.y);
            if (!HeroSetup.TryMeasure(vis, out b)) return;

            float feetWorldY = root.transform.position.y + bottomLocalY * parentScaleY;
            vis.transform.localPosition += new Vector3(0f, (feetWorldY - b.min.y) / parentScaleY, 0f);
        }
    }
}

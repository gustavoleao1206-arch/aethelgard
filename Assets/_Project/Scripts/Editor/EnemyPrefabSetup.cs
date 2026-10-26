using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using JogoDaTrupe.Core.Identity;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Combat;
using JogoDaTrupe.Gameplay.Enemies;
using JogoDaTrupe.Gameplay.Boss;
using JogoDaTrupe.Gameplay.Incorporation;
using JogoDaTrupe.Net;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Cria os PREFABS DE REDE dos inimigos e do boss (M-NET-6 P1) — o mesmo pipeline
    /// comprovado do <c>P_Player</c>/<c>P_NetSystems</c> (o hash de rede nasce certo no
    /// import do asset; objeto in-scene gerado por código fica com hash 0 — Bugs.md #7).
    ///
    /// Cada arquétipo tem o SEU prefab com a <see cref="EnemyDefinition"/> BAKED: a
    /// réplica do cliente nasce com a ficha certa sem replicar id nem fazer lookup.
    /// O asset é REUSADO entre regerações (GUID estável ⇒ hash de rede estável —
    /// <see cref="NetPrefabTooling"/>) e o registro é GARANTIDO no gerador (EnsureRegistered).
    ///
    /// Peças de rede: <c>NetworkObject</c> + <c>NetworkTransform</c> PADRÃO
    /// (server-authoritative — o corpo do inimigo é comandado só pelo servidor;
    /// diferente do player, que usa o <c>ClientNetworkTransform</c> owner-auth).
    /// A IA/EcoDropper das réplicas fica inerte pelo gate <c>SimulationAuthority</c> (P0).
    /// </summary>
    internal static class EnemyPrefabSetup
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs";

        /// <summary>Prefabs dos 3 arquétipos, PAREADOS POR ÍNDICE com MapLayout.EnemySpawns.</summary>
        internal static GameObject[] CreateEnemyPrefabs(EnemyDefinition berserker, EnemyDefinition hitAndRun, EnemyDefinition zigzag)
            => new[]
            {
                CreatePrefab(berserker, "P_Enemy_Berserker", scale: 1f, isBoss: false),
                CreatePrefab(hitAndRun, "P_Enemy_HitAndRun", scale: 1f, isBoss: false),
                CreatePrefab(zigzag, "P_Enemy_Zigzag", scale: 1f, isBoss: false),
            };

        internal static GameObject CreateBossPrefab(EnemyDefinition bossDef)
            => CreatePrefab(bossDef, "P_Boss_EcoCorrompido", scale: 1.8f, isBoss: true);

        // Espelha o SpawnEnemy/SpawnBoss da cena solo (cápsula + EntityGuid + Health +
        // controller + EcoDropper; sem Ownable de propósito — vida "sem dono" não mexe
        // no HUD do jogador) + as 2 peças de rede.
        private static GameObject CreatePrefab(EnemyDefinition def, string name, float scale, bool isBoss)
        {
            if (def == null)
            {
                Debug.LogError("[Setup] EnemyPrefabSetup: EnemyDefinition nula para " + name + " — prefab não criado.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            // Material-ASSET CEL-SHADED (JogoDaTrupe/Toon). A cor de POSTURA continua sendo
            // aplicada em runtime pelo controller via Material.color — o _BaseColor do toon
            // é [MainColor], então Material.color continua funcionando (só na autoridade).
            Material mat = EditorMaterials.MakeToon(def.color);
            string matPath = PrefabFolder + "/M_" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temp.name = name;
            temp.transform.localScale = Vector3.one * scale;
            temp.GetComponent<Renderer>().sharedMaterial = mat;

            temp.AddComponent<EntityGuid>();
            Health health = temp.AddComponent<Health>();
            Behaviour controller = isBoss
                ? (Behaviour)temp.AddComponent<BossController>()
                : temp.AddComponent<EnemyController>();
            EcoDropper dropper = temp.AddComponent<EcoDropper>();

            // Peças de rede: NetworkObject primeiro; NetworkTransform padrão (server-auth);
            // NetworkHealth replica a vida (servidor semeia; réplica espelha — M-NET-6 P3);
            // NetworkDamageForwarder leva o dano do CLIENTE ao servidor (M-NET-6 P4).
            temp.AddComponent<NetworkObject>();
            temp.AddComponent<NetworkTransform>();
            temp.AddComponent<NetworkHealth>();
            temp.AddComponent<NetworkDamageForwarder>();

            PlayerSceneSetup.SetFloatField(health, "_max", def.maxHealth);
            PlayerSceneSetup.SetObjectField(controller, "_definition", def);
            PlayerSceneSetup.SetObjectField(dropper, "_definition", def);

            // MODELO 3D no lugar da cápsula (mesma técnica do herói). A cápsula-visual é
            // destruída; a CapsuleCollider (hitbox/alvo) e as peças de rede continuam.
            EnemySetup.AttachCreatureVisual(temp, def.enemyId, isBoss);
            if (!isBoss) EnemySetup.AddNavigation(temp); // boss é voador: não usa NavMesh

            // GUID ESTÁVEL (sem delete): o hash de rede deriva do GUID, então reusar o
            // asset mantém o hash idêntico entre regerações e telas. Ver NetPrefabTooling.
            string path = PrefabFolder + "/" + name + ".prefab";
            return NetPrefabTooling.SavePrefabStable(temp, path);
        }
    }
}

using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using JogoDaTrupe.Core.Identity;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Capture;
using JogoDaTrupe.Gameplay.Combat;
using JogoDaTrupe.Gameplay.Enemies;
using JogoDaTrupe.Net;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Cria o PREFAB DE REDE da criatura capturável (M-NET-7 P7) — o pipeline dos
    /// inimigos (<see cref="EnemyPrefabSetup"/>), com duas diferenças de propósito:
    ///
    /// • <b>Guid CONCRETO baked</b> (<c>EnsureId</c> na criação do asset): o pedido de
    ///   captura viaja por <c>SyncRequested{CreatureGuid}</c> e o guid precisa ser IGUAL
    ///   nas 2 máquinas — instância do servidor e réplica nascem do MESMO asset. (Vale
    ///   enquanto há UMA criatura; várias instâncias do mesmo prefab compartilhariam o
    ///   guid — registrado em Melhorias.)
    /// • <b><see cref="NetworkCapture"/></b> no lugar do EcoDropper: a criatura não
    ///   morre (piso de vida) — ela fica SINCRONIZÁVEL, e esse estado replica por
    ///   NetworkVariable. Sem Ownable de propósito (vida "sem dono", como os inimigos).
    ///
    /// Peças de rede: NetworkObject + NetworkTransform (server-auth: a criatura luta
    /// fraco e se move) + NetworkHealth (vida replicada) + NetworkDamageForwarder
    /// (dano do CLIENTE enfraquece — infra do M-NET-6 de graça) + NetworkCapture.
    /// </summary>
    internal static class CreaturePrefabSetup
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs";

        internal static GameObject CreateCreaturePrefab(EnemyDefinition def)
        {
            const string name = "P_Creature_Planta";
            if (def == null)
            {
                Debug.LogError("[Setup] CreaturePrefabSetup: EnemyDefinition nula — prefab não criado.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            // Material-ASSET (material de runtime sairia magenta dentro do prefab).
            Material mat = EditorMaterials.MakeToon(def.color); // cel-shaded (JogoDaTrupe/Toon)
            string matPath = PrefabFolder + "/M_" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);

            // Espelha o SpawnCapturable da cena solo (cápsula + EntityGuid + Health +
            // EnemyController + CapturableCreature) + as peças de rede.
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temp.name = name;
            temp.GetComponent<Renderer>().sharedMaterial = mat;

            temp.AddComponent<EntityGuid>().EnsureId(); // guid CONCRETO, igual nas 2 telas
            Health health = temp.AddComponent<Health>();
            EnemyController controller = temp.AddComponent<EnemyController>();
            CapturableCreature capturable = temp.AddComponent<CapturableCreature>();

            temp.AddComponent<NetworkObject>();
            temp.AddComponent<NetworkTransform>();
            temp.AddComponent<NetworkHealth>();
            temp.AddComponent<NetworkDamageForwarder>();
            temp.AddComponent<NetworkCapture>();

            PlayerSceneSetup.SetFloatField(health, "_max", def.maxHealth);
            PlayerSceneSetup.SetObjectField(controller, "_definition", def);
            PlayerSceneSetup.SetObjectField(capturable, "_definition", def);

            // GUID ESTÁVEL (sem delete): o hash de rede deriva do GUID, então reusar o
            // asset mantém o hash idêntico entre regerações e telas. Ver NetPrefabTooling.
            // (O guid CONCRETO baked no EntityGuid — acima — continua igual nas 2 telas.)
            string path = PrefabFolder + "/" + name + ".prefab";
            return NetPrefabTooling.SavePrefabStable(temp, path);
        }
    }
}

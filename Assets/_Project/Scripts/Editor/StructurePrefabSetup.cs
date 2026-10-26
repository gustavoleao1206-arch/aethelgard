using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using JogoDaTrupe.Core.Identity;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Building;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Cria os PREFABS DE REDE das estruturas construíveis (M-NET-7 P5) — o mesmo
    /// pipeline comprovado do <see cref="EnemyPrefabSetup"/> (o hash de rede nasce certo
    /// no import do asset; objeto in-scene gerado por código fica com hash 0 — Bugs.md #7).
    ///
    /// Cada estrutura tem o SEU prefab com a <see cref="StructureDefinition"/> BAKED (no
    /// componente <see cref="Structure"/>): o servidor spawna o prefab ao atender um
    /// <c>BuildRequested</c> e a réplica nasce com tamanho/cor/id certos sem replicar nada.
    ///
    /// Peças de rede: só <c>NetworkObject</c> — SEM NetworkTransform. Estrutura NÃO se move;
    /// a posição do spawn (vinda do pedido, já encaixada na grade) basta, e o NGO a envia
    /// no spawn inicial. Mantém o Collider (parede/piso são sólidos).
    /// O asset é REUSADO entre regerações (GUID estável ⇒ hash de rede estável —
    /// <see cref="NetPrefabTooling"/>) e o registro é GARANTIDO no gerador (EnsureRegistered).
    /// </summary>
    internal static class StructurePrefabSetup
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs";

        /// <summary>Prefabs de rede das estruturas, um por <see cref="StructureDefinition"/>.</summary>
        internal static GameObject[] CreateStructurePrefabs(StructureDefinition parede, StructureDefinition piso)
            => new[]
            {
                CreatePrefab(parede, "P_Structure_Parede"),
                CreatePrefab(piso, "P_Structure_Piso"),
            };

        // Espelha o TryPlace do solo (cubo com tamanho/cor da ficha) + EntityGuid (id
        // estável p/ o save) + a ficha baked no componente Structure + NetworkObject.
        private static GameObject CreatePrefab(StructureDefinition def, string name)
        {
            if (def == null)
            {
                Debug.LogError("[Setup] StructurePrefabSetup: StructureDefinition nula para " + name + " — prefab não criado.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            // Material-ASSET (material de runtime sairia magenta dentro do prefab — mesmo
            // motivo do M_Player/M_Enemy).
            Material mat = EditorMaterials.MakeToon(def.color); // cel-shaded (JogoDaTrupe/Toon)
            string matPath = PrefabFolder + "/M_" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temp.name = name;
            temp.transform.localScale = def.size;
            temp.GetComponent<Renderer>().sharedMaterial = mat;

            temp.AddComponent<EntityGuid>();
            Structure structure = temp.AddComponent<Structure>();
            PlayerSceneSetup.SetObjectField(structure, "_definition", def);

            // Peça de rede: só NetworkObject (estrutura estática — sem NetworkTransform).
            temp.AddComponent<NetworkObject>();

            // GUID ESTÁVEL (sem delete): o hash de rede deriva do GUID, então reusar o
            // asset mantém o hash idêntico entre regerações e telas. Ver NetPrefabTooling.
            string path = PrefabFolder + "/" + name + ".prefab";
            return NetPrefabTooling.SavePrefabStable(temp, path);
        }
    }
}

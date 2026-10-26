using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Assa a MALHA DE NAVEGAÇÃO (NavMesh) da cena e a salva como asset. É o que permite aos
    /// mobs contornar colunas, arcos e árvores em vez de andar por dentro deles
    /// (<see cref="JogoDaTrupe.Gameplay.Enemies.EnemyNavigator"/>).
    ///
    /// ⚠️ QUANDO chamar: logo depois do mundo ESTÁTICO (<c>ForestWorld.Build</c>) e ANTES de
    /// criar player, inimigos, altar, nós e estruturas. O bake recolhe COLLIDERS — se os
    /// personagens já existirem, os corpos deles viram buracos permanentes na malha e o mob
    /// nasce em cima do próprio buraco.
    ///
    /// O boss não usa nada disto (dragão voador).
    /// </summary>
    internal static class NavMeshSetup
    {
        private const string Folder = "Assets/_Project/Navigation";

        internal static void Bake(string sceneName)
        {
            var host = new GameObject("NavMesh");
            NavMeshSurface surface = host.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; // árvores/colunas são colliders
            surface.BuildNavMesh();

            NavMeshData data = surface.navMeshData;
            if (data == null)
            {
                Debug.LogWarning("[NavMesh] Bake não produziu malha — os inimigos vão andar em linha reta.");
                return;
            }

            // A malha precisa virar ASSET, senão se perde ao salvar a cena (a surface só guarda
            // uma referência). GUID novo a cada regeneração é inofensivo aqui: quem aponta pra
            // ela é a própria cena, que está sendo regerada no mesmo instante.
            HeroSetup.EnsureFolder(Folder);
            string path = Folder + "/" + sceneName + "_NavMesh.asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);

            Debug.Log("[NavMesh] Malha assada e salva em " + path + " — os mobs contornam os obstáculos.");
        }
    }
}

using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Spawna os INIMIGOS e o BOSS como objetos de rede quando o servidor sobe
    /// (M-NET-6 P1) — um corpo por inimigo, igual nas duas telas: o servidor simula
    /// (IA gateada por SimulationAuthority) e o NetworkTransform replica a posição.
    ///
    /// As posições vêm de <see cref="MapLayout.EnemySpawns"/>/<see cref="MapLayout.BossSpawn"/>
    /// (fonte única — a cena solo authored lê das MESMAS constantes), e os prefabs são
    /// PAREADOS POR ÍNDICE com EnemySpawns (0=Berserker, 1=HitAndRun, 2=Zigzag).
    ///
    /// MonoBehaviour puro no GameObject do NetworkManager (par do NetSystemsSpawner).
    /// Registro dos prefabs é automático (DefaultNetworkPrefabs — Bugs.md #8). Objetos
    /// spawnados são server-owned e o NGO os destrói no shutdown.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetEnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject[] _enemyPrefabs; // pareado por índice com MapLayout.EnemySpawns
        [SerializeField] private GameObject _bossPrefab;     // nasce em MapLayout.BossSpawn
        [SerializeField] private GameObject _creaturePrefab; // criatura capturável, nasce em MapLayout.ZoneCriatura (M-NET-7 P7)

        private NetworkManager _manager;

        private void Awake() => _manager = GetComponent<NetworkManager>();

        private void OnEnable() => _manager.OnServerStarted += SpawnWorld;

        private void OnDisable()
        {
            if (_manager != null) _manager.OnServerStarted -= SpawnWorld;
        }

        // Só o servidor recebe OnServerStarted; os clientes recebem as réplicas.
        private void SpawnWorld()
        {
            int count = _enemyPrefabs != null
                ? Mathf.Min(_enemyPrefabs.Length, MapLayout.EnemySpawns.Length)
                : 0;
            for (int i = 0; i < count; i++)
                Spawn(_enemyPrefabs[i], MapLayout.EnemySpawns[i]);

            Spawn(_bossPrefab, MapLayout.BossSpawn);

            // Criatura capturável (M-NET-7 P7): mesma posição do authored da cena solo.
            Spawn(_creaturePrefab, MapLayout.At(MapLayout.ZoneCriatura, 0f, 1f, 0f));
        }

        private static void Spawn(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                Debug.LogError("[Rede] NetEnemySpawner sem prefab de inimigo — um inimigo não vai nascer.");
                return;
            }

            GameObject go = Instantiate(prefab, position, Quaternion.identity);
            go.GetComponent<NetworkObject>().Spawn();
        }
    }
}

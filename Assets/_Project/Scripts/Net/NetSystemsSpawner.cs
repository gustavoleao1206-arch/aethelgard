using UnityEngine;
using Unity.Netcode;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Spawna o objeto de SISTEMAS de rede (prefab <c>P_NetSystems</c>, que carrega o
    /// <see cref="EventScopeAdapter"/>) quando o servidor sobe (M-NET-5 P4, Opção B).
    ///
    /// É MonoBehaviour puro no GameObject do NetworkManager — o par natural do
    /// <see cref="CoopConnectionApprover"/> (um NetworkBehaviour não pode morar lá).
    ///
    /// A hospedagem por PREFAB spawnado (e não por objeto colocado na cena) é deliberada:
    /// um NetworkObject in-scene criado por gerador de cena fica com GlobalObjectIdHash=0
    /// (a Unity só calcula esse hash no fluxo de salvamento manual do Editor) e o CLIENTE
    /// falha a sincronização — "NetworkPrefab hash was not found... Hash: 0!" (bug visto
    /// na verificação de 01/07/2026, ver 06_Testes/Bugs.md #7). O caminho de prefab é o
    /// mesmo do P_Player — comprovado no projeto desde o M-NET-3.
    ///
    /// O REGISTRO do prefab (nos dois lados) NÃO é feito aqui: o PRÓPRIO NGO já o faz —
    /// o postprocessor do pacote mantém todo prefab-com-NetworkObject no asset
    /// <c>DefaultNetworkPrefabs</c>, que o Reset do NetworkManager liga ao manager.
    /// Registrar de novo (em runtime ou numa segunda lista) cria uma SEGUNDA fonte e
    /// duplica o registro (Bugs.md #8). O gerador da cena só valida que o automático
    /// existe.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetSystemsSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _systemsPrefab;

        private NetworkManager _manager;

        private void Awake() => _manager = GetComponent<NetworkManager>();

        private void OnEnable()
        {
            _manager.OnServerStarted += SpawnSystems;
        }

        private void OnDisable()
        {
            if (_manager != null) _manager.OnServerStarted -= SpawnSystems;
        }

        // Só o servidor recebe OnServerStarted. O objeto spawnado é server-owned e o NGO
        // o destrói sozinho no shutdown — um novo Start Host spawna um novo, sem sobra.
        private void SpawnSystems()
        {
            if (_systemsPrefab == null)
            {
                Debug.LogError("[Rede] NetSystemsSpawner sem prefab de sistemas — o correio de eventos (EventScopeAdapter) não vai subir.");
                return;
            }

            GameObject go = Instantiate(_systemsPrefab);
            go.GetComponent<NetworkObject>().Spawn();
        }
    }
}

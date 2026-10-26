using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Ownership;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Escreve o seam <see cref="SimulationAuthority"/> conforme a sessão de rede
    /// (M-NET-6 P1): servidor/host → autoridade; cliente → não simula o mundo
    /// (IA/EcoDropper ficam inertes pelos gates do P0; as réplicas dos inimigos são
    /// movidas pelo NetworkTransform). No shutdown restaura o default solo (true).
    ///
    /// MonoBehaviour puro no GameObject do NetworkManager, par do
    /// <see cref="CoopConnectionApprover"/>/<see cref="NetSystemsSpawner"/>.
    /// No host os DOIS eventos disparam (OnServerStarted e OnClientStarted) — ambos
    /// aplicam <c>IsServer</c>, então o host fica corretamente com autoridade.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetAuthorityBinder : MonoBehaviour
    {
        private NetworkManager _manager;

        private void Awake() => _manager = GetComponent<NetworkManager>();

        private void OnEnable()
        {
            _manager.OnServerStarted += Apply;
            _manager.OnClientStarted += Apply;
            _manager.OnServerStopped += RestoreSolo;
            _manager.OnClientStopped += RestoreSolo;
        }

        private void OnDisable()
        {
            if (_manager == null) return;
            _manager.OnServerStarted -= Apply;
            _manager.OnClientStarted -= Apply;
            _manager.OnServerStopped -= RestoreSolo;
            _manager.OnClientStopped -= RestoreSolo;
        }

        private void Apply()
        {
            SimulationAuthority.IsAuthority = _manager.IsServer;
            SimulationAuthority.IsNetworked = true; // há sessão de rede (host ou cliente)
        }

        // Sessão encerrada: voltar ao solo é seguro (esta máquina volta a ser o mundo).
        private void RestoreSolo(bool wasHost)
        {
            SimulationAuthority.IsAuthority = true;
            SimulationAuthority.IsNetworked = false;
        }
    }
}

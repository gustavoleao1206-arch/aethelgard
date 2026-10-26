using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Aprovador de conexão do co-op (M-NET-3c P6). Fica no MESMO GameObject do
    /// <see cref="NetworkManager"/>. Quando um jogador conecta (inclusive o próprio host):
    /// • aceita até <see cref="MapLayout.PlayerSpawns"/> jogadores (4 hoje);
    /// • manda o Netcode criar o PlayerObject (o prefab <c>P_Player</c>) já no ponto de
    ///   spawn daquele índice — cada jogador nasce num lugar distinto do anel ao redor do
    ///   Altar, em vez de todos empilhados na origem.
    ///
    /// É o gancho que a Arquitetura previu para o spawn distinto por jogador
    /// (ENGINEERING_RULES #13, "jogadores são uma coleção, tamanho 1 hoje"). Lê as posições
    /// de <see cref="MapLayout"/> (fonte única, já em runtime desde o M-NET-3c P0).
    ///
    /// Em solo nada disto roda: a cena solo (VerticalSlice) não tem NetworkManager, então
    /// não há aprovação de conexão nem spawn de rede — o player é authored como sempre.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class CoopConnectionApprover : MonoBehaviour
    {
        private NetworkManager _networkManager;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();

            // A aprovação de conexão precisa estar ligada para escolhermos posição e limite.
            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.ConnectionApprovalCallback += Approve;

            // Drop-in/drop-out (M-NET-9 P2): ao desconectar, LIBERA o assento; ao encerrar,
            // zera o registro (o próximo Start Host começa limpo).
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnect;
            _networkManager.OnServerStopped += HandleServerStopped;
        }

        private void OnDestroy()
        {
            if (_networkManager == null) return;
            _networkManager.ConnectionApprovalCallback -= Approve;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
            _networkManager.OnServerStopped -= HandleServerStopped;
        }

        private void HandleClientDisconnect(ulong clientId) => SeatRegistry.Release(clientId);
        private void HandleServerStopped(bool _) => SeatRegistry.Clear();

        // Chamado pelo Netcode a cada tentativa de conexão (host e clientes).
        private void Approve(NetworkManager.ConnectionApprovalRequest request,
                             NetworkManager.ConnectionApprovalResponse response)
        {
            // Reserva o MENOR assento livre (0..3). Quem reconecta ocupa um assento que
            // foi liberado — a base do drop-in/drop-out. -1 = lotado.
            int seat = SeatRegistry.Claim(request.ClientNetworkId, MapLayout.PlayerSpawns.Length);
            bool hasRoom = seat >= 0;

            response.Approved = hasRoom;
            response.CreatePlayerObject = hasRoom;

            if (hasRoom)
            {
                response.Position = MapLayout.SpawnFor(seat); // nasce no ponto do assento
                response.Rotation = Quaternion.identity;
            }

            // Decisão imediata (não assíncrona): não deixamos a conexão "pendente".
            response.Pending = false;
        }
    }
}

using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Ownership;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Helper dos filtros por ASSENTO (M-NET-9 P2). Os forwarders/companions filtravam
    /// "é do MEU jogador?" comparando o <c>OwnerId</c> do evento com <c>OwnerClientId</c>
    /// (o clientId do NGO). Com o dono virando o ASSENTO (o <see cref="NetworkOwnable"/>
    /// grava o assento no <c>Ownable.OwnerId</c>), a comparação passa a ser contra o
    /// assento deste jogador — lido do <see cref="Ownable"/> irmão. Um só ponto de troca:
    /// <c>(int)OwnerClientId</c> → <c>NetSeat.Of(this)</c>.
    /// </summary>
    public static class NetSeat
    {
        /// <summary>Assento do jogador ao qual este componente pertence (-1 sem Ownable).</summary>
        public static int Of(Component c) => c.TryGetComponent(out Ownable o) ? o.OwnerId : -1;

        /// <summary>
        /// Assento de quem ENVIOU um RPC <c>SendTo.Server</c>, lido da CONEXÃO
        /// (<c>RpcParams.Receive.SenderClientId</c>, preenchido pelo NGO no servidor) — o
        /// único dono confiável de um pedido vindo da rede. Nunca use um <c>ownerId</c> do
        /// payload: o cliente escreve o que quiser ali. Falso (com aviso no Console) =
        /// remetente sem assento → o chamador descarta o pedido.
        /// </summary>
        public static bool TryOfSender(RpcParams rpcParams, out int seat)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (SeatRegistry.TryResolveSender(sender, out seat)) return true;
            Debug.LogWarning($"[Rede] Pedido descartado: o cliente {sender} não tem assento no servidor.");
            return false;
        }
    }
}

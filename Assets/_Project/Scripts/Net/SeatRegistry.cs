using System.Collections.Generic;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Lógica PURA dos assentos (M-NET-9 P2, testável sem rede).
    /// </summary>
    public static class SeatMath
    {
        /// <summary>Menor assento (0..capacity-1) não ocupado, ou -1 se lotado.</summary>
        public static int LowestFree(ICollection<int> occupied, int capacity)
        {
            for (int seat = 0; seat < capacity; seat++)
                if (occupied == null || !occupied.Contains(seat)) return seat;
            return -1;
        }
    }

    /// <summary>
    /// ASSENTOS LÓGICOS do co-op (M-NET-9 P2) — a chave do drop-in/drop-out. O clientId
    /// do NGO NUNCA se repete (reconectar = id novo), então usá-lo como identidade
    /// perderia a progressão de quem volta. Aqui mapeamos clientId → ASSENTO (0..3, o
    /// menor livre): quem sai LIBERA o assento; quem entra pega o menor livre. Com o
    /// <see cref="NetworkOwnable"/> gravando o assento no <c>Ownable.OwnerId</c>, as
    /// SaveKeys ficam por assento — reconectar no mesmo assento retoma a progressão
    /// (host dá F9). Estado de PROCESSO no servidor (o servidor decide os assentos);
    /// não fere o anti-singleton (é infra de sessão, não estado de jogador).
    /// </summary>
    public static class SeatRegistry
    {
        private static readonly Dictionary<ulong, int> _seatByClient = new Dictionary<ulong, int>();
        private static readonly Dictionary<int, ulong> _clientBySeat = new Dictionary<int, ulong>();

        /// <summary>Reserva (ou devolve, se já tem) o menor assento livre para o cliente. -1 se lotado.</summary>
        public static int Claim(ulong clientId, int capacity)
        {
            if (_seatByClient.TryGetValue(clientId, out int existing)) return existing;

            int seat = SeatMath.LowestFree(_clientBySeat.Keys, capacity);
            if (seat < 0) return -1;

            _seatByClient[clientId] = seat;
            _clientBySeat[seat] = clientId;
            return seat;
        }

        /// <summary>Libera o assento do cliente (na desconexão) — o próximo a entrar o reusa.</summary>
        public static void Release(ulong clientId)
        {
            if (!_seatByClient.TryGetValue(clientId, out int seat)) return;
            _seatByClient.Remove(clientId);
            _clientBySeat.Remove(seat);
        }

        /// <summary>Assento do cliente, ou -1 se não tem.</summary>
        public static int SeatOf(ulong clientId) => _seatByClient.TryGetValue(clientId, out int s) ? s : -1;

        /// <summary>
        /// Assento de quem ENVIOU um pedido pela rede. A identidade vem da CONEXÃO (o
        /// <c>SenderClientId</c> que o NGO preenche no servidor), nunca de um id escrito no
        /// payload: um cliente alterado poderia escrever ali o assento de outro jogador e
        /// mexer no inventário/progressão alheios. Falso = remetente sem assento (não
        /// aprovado ou já desconectado) → o pedido deve ser descartado.
        /// </summary>
        public static bool TryResolveSender(ulong senderClientId, out int seat)
        {
            seat = SeatOf(senderClientId);
            return seat >= 0;
        }

        /// <summary>Cliente atualmente sentado no assento (para o executor resolver seat → player).</summary>
        public static bool TryClientIdOf(int seat, out ulong clientId) => _clientBySeat.TryGetValue(seat, out clientId);

        /// <summary>Zera tudo (no shutdown do servidor).</summary>
        public static void Clear()
        {
            _seatByClient.Clear();
            _clientBySeat.Clear();
        }
    }
}

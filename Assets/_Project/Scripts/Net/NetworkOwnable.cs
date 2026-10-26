using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Ownership;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Ponte entre o dono de rede do Netcode e o nosso "dono lógico" (<see cref="Ownable"/>).
    /// Grava o dono no <c>Ownable.OwnerId</c> — assim TODOS os sistemas que já leem
    /// <c>Ownable.OwnerId</c> (Health, AbilityLoadout, AutoCollector, Synchronizer…) e as
    /// SaveKeys refletem o dono <b>sem nenhum deles mudar</b>.
    ///
    /// M-NET-9 P2 — o dono passa a ser o ASSENTO (0..3), não o <c>OwnerClientId</c>: o
    /// clientId do NGO nunca se repete, então reconectar perderia a progressão. O assento
    /// (menor livre, do <see cref="SeatRegistry"/>) é replicado num <c>NetworkVariable</c>
    /// write-server. Também grava <c>LocalPlayer.Id</c> = assento no dono (centralizado
    /// aqui). O duplo-gancho (OnNetworkSpawn + OnValueChanged) cobre a ordem de sync.
    ///
    /// Vive na camada <c>JogoDaTrupe.Net</c>. Vai num prefab com <c>NetworkObject</c> +
    /// <see cref="Ownable"/> (o <c>P_Player</c>).
    /// </summary>
    [RequireComponent(typeof(Ownable))]
    public sealed class NetworkOwnable : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _seat = new NetworkVariable<int>(-1); // escrita: servidor
        private Ownable _ownable;

        /// <summary>Assento lógico deste jogador (0..3). -1 antes do sync.</summary>
        public int Seat => _seat.Value;

        private void Awake() => _ownable = GetComponent<Ownable>();

        public override void OnNetworkSpawn()
        {
            if (IsServer) _seat.Value = SeatRegistry.SeatOf(OwnerClientId); // assento do dono (o servidor decidiu)
            _seat.OnValueChanged += OnSeatChanged;
            Apply(_seat.Value);
        }

        public override void OnNetworkDespawn() => _seat.OnValueChanged -= OnSeatChanged;

        public override void OnGainedOwnership() => Apply(_seat.Value);
        public override void OnLostOwnership() => Apply(_seat.Value);

        private void OnSeatChanged(int _, int seat) => Apply(seat);

        private void Apply(int seat)
        {
            // Fallback defensivo enquanto o NetworkVariable não sincronizou: no caso comum
            // (sessão sem reconexão) assento == clientId, então isto nem se nota.
            if (seat < 0) seat = (int)OwnerClientId;

            if (_ownable != null) _ownable.SetOwner(seat);
            if (IsOwner) LocalPlayer.Id = seat; // as HUDs locais filtram por este id
        }
    }
}

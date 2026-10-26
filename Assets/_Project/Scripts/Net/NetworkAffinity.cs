using System;
using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// AFINIDADE REPLICADA (M-NET-8 P2) — companion do <see cref="AffinityState"/> no
    /// <c>P_Player</c>. 6 floats fixos → SNAPSHOT ATÔMICO (mesmo molde do
    /// <see cref="NetworkAttributes"/>).
    ///
    /// A ingestão server-side já existe de graça: o <c>AbilityUsed</c> do dono-cliente
    /// chega ao servidor pelo <see cref="NetworkStyleForwarder"/> (M-NET-6) e o
    /// <c>AffinityState</c> da cópia do servidor o processa (gate P0) publicando
    /// <c>AffinityChanged</c> — que este companion escuta para reescrever o snapshot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AffinityState))]
    public sealed class NetworkAffinity : NetworkBehaviour
    {
        /// <summary>Snapshot atômico das afinidades (6 = nº de ElementType).</summary>
        public struct AffinitySnapshot : INetworkSerializable, IEquatable<AffinitySnapshot>
        {
            public float A0, A1, A2, A3, A4, A5;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref A0);
                serializer.SerializeValue(ref A1);
                serializer.SerializeValue(ref A2);
                serializer.SerializeValue(ref A3);
                serializer.SerializeValue(ref A4);
                serializer.SerializeValue(ref A5);
            }

            public bool Equals(AffinitySnapshot other) =>
                A0 == other.A0 && A1 == other.A1 && A2 == other.A2 &&
                A3 == other.A3 && A4 == other.A4 && A5 == other.A5;

            public float[] Values() => new[] { A0, A1, A2, A3, A4, A5 };
        }

        private readonly NetworkVariable<AffinitySnapshot> _snapshot =
            new NetworkVariable<AffinitySnapshot>(); // defaults: leitura Everyone, escrita Server

        private AffinityState _affinity;
        private bool _mirrorDirty;

        public override void OnNetworkSpawn()
        {
            _affinity = GetComponent<AffinityState>();

            if (IsServer)
            {
                EventBus<AffinityChanged>.Subscribe(OnAffinityChanged);
                WriteSnapshot();
            }
            else
            {
                _snapshot.OnValueChanged += OnSnapshotChanged;
                _mirrorDirty = true; // late-join
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) EventBus<AffinityChanged>.Unsubscribe(OnAffinityChanged);
            else _snapshot.OnValueChanged -= OnSnapshotChanged;
        }

        // ── Servidor ────────────────────────────────────────────────────────────────────

        private void OnAffinityChanged(AffinityChanged e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            WriteSnapshot();
        }

        private void WriteSnapshot()
        {
            _snapshot.Value = new AffinitySnapshot
            {
                A0 = _affinity.GetAffinity((ElementType)0),
                A1 = _affinity.GetAffinity((ElementType)1),
                A2 = _affinity.GetAffinity((ElementType)2),
                A3 = _affinity.GetAffinity((ElementType)3),
                A4 = _affinity.GetAffinity((ElementType)4),
                A5 = _affinity.GetAffinity((ElementType)5)
            };
        }

        // ── Cliente (espelho) ───────────────────────────────────────────────────────────

        private void OnSnapshotChanged(AffinitySnapshot _, AffinitySnapshot __) => _mirrorDirty = true;

        private void Update()
        {
            if (!_mirrorDirty || IsServer) return;
            _mirrorDirty = false;

            _affinity.SyncFrom(_snapshot.Value.Values());
            // Diagnóstico do co-op (dispara a cada uso de habilidade do dono — raro).
            Debug.Log($"[Rede] Afinidade replicada (dono {OwnerClientId}).");
        }
    }
}

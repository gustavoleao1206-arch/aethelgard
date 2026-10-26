using System;
using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// ATRIBUTOS REPLICADOS (M-NET-8 P2) — companion do <see cref="PlayerAttributes"/>
    /// no <c>P_Player</c>. Estado pequeno e fixo (nível/XP/pontos + 6 valores) →
    /// viaja num SNAPSHOT ATÔMICO (<see cref="NetworkVariable{T}"/> de struct), em vez
    /// de NetworkList: uma mudança = um pacote consistente (nunca "nível novo com
    /// pontos velhos").
    ///
    /// Servidor assina <c>AttributesChanged</c> deste dono e reescreve o snapshot;
    /// cliente espelha via <c>PlayerAttributes.SyncFrom</c> (republica o evento —
    /// ficha/HUD atualizam). Molde do NetworkInventory/NetworkHealth.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttributes))]
    public sealed class NetworkAttributes : NetworkBehaviour
    {
        /// <summary>Snapshot atômico dos atributos. Só primitivos (6 = nº de AttributeType).</summary>
        public struct AttributesSnapshot : INetworkSerializable, IEquatable<AttributesSnapshot>
        {
            public int Level, Xp, Points;
            public int V0, V1, V2, V3, V4, V5;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Level);
                serializer.SerializeValue(ref Xp);
                serializer.SerializeValue(ref Points);
                serializer.SerializeValue(ref V0);
                serializer.SerializeValue(ref V1);
                serializer.SerializeValue(ref V2);
                serializer.SerializeValue(ref V3);
                serializer.SerializeValue(ref V4);
                serializer.SerializeValue(ref V5);
            }

            public bool Equals(AttributesSnapshot other) =>
                Level == other.Level && Xp == other.Xp && Points == other.Points &&
                V0 == other.V0 && V1 == other.V1 && V2 == other.V2 &&
                V3 == other.V3 && V4 == other.V4 && V5 == other.V5;

            public int[] Values() => new[] { V0, V1, V2, V3, V4, V5 };
        }

        private readonly NetworkVariable<AttributesSnapshot> _snapshot =
            new NetworkVariable<AttributesSnapshot>(); // defaults: leitura Everyone, escrita Server

        private PlayerAttributes _attributes;
        private bool _mirrorDirty;

        public override void OnNetworkSpawn()
        {
            _attributes = GetComponent<PlayerAttributes>();

            if (IsServer)
            {
                EventBus<AttributesChanged>.Subscribe(OnAttributesChanged);
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
            if (IsServer) EventBus<AttributesChanged>.Unsubscribe(OnAttributesChanged);
            else _snapshot.OnValueChanged -= OnSnapshotChanged;
        }

        // ── Servidor ────────────────────────────────────────────────────────────────────

        private void OnAttributesChanged(AttributesChanged e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            WriteSnapshot();
        }

        private void WriteSnapshot()
        {
            _snapshot.Value = new AttributesSnapshot
            {
                Level = _attributes.Level,
                Xp = _attributes.Xp,
                Points = _attributes.PointsAvailable,
                V0 = _attributes.GetValue((AttributeType)0),
                V1 = _attributes.GetValue((AttributeType)1),
                V2 = _attributes.GetValue((AttributeType)2),
                V3 = _attributes.GetValue((AttributeType)3),
                V4 = _attributes.GetValue((AttributeType)4),
                V5 = _attributes.GetValue((AttributeType)5)
            };
        }

        // ── Cliente (espelho) ───────────────────────────────────────────────────────────

        private void OnSnapshotChanged(AttributesSnapshot _, AttributesSnapshot __) => _mirrorDirty = true;

        private void Update()
        {
            if (!_mirrorDirty || IsServer) return;
            _mirrorDirty = false;

            AttributesSnapshot s = _snapshot.Value;
            _attributes.SyncFrom(s.Level, s.Xp, s.Points, s.Values());
            // Diagnóstico do co-op (raro: só quando o snapshot replicado muda no cliente).
            Debug.Log($"[Rede] Atributos replicados (dono {OwnerClientId}): nível {s.Level}, {s.Points} ponto(s).");
        }
    }
}

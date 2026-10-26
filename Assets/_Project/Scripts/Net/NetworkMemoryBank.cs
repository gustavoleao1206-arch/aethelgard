using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// MEMÓRIA (Ecos/maestria) REPLICADA (M-NET-8 P2) — companion do
    /// <see cref="MemoryBank"/> no <c>P_Player</c>, molde exato do
    /// <see cref="NetworkInventory"/>: o estado autoritativo vive no servidor e viaja
    /// por replicação de ESTADO (robusto a perda e a late-join — alinhado ao
    /// drop-in/drop-out SIM do Diretor).
    ///
    /// • <b>Servidor:</b> assina <c>MasteryChanged</c> DESTE dono (o MemoryBank do
    ///   servidor publica ao ingerir cada Eco) e reescreve a lista completa (≤ punhado
    ///   de famílias; simplicidade primeiro).
    /// • <b>Cliente:</b> espelha via <c>MemoryBank.SyncFrom</c> (coalescido no Update),
    ///   que republica <c>MasteryChanged</c> — AltarUI/HUDs releem.
    ///
    /// Os 3 LIMIARES de maestria viajam por entrada: vivem na ficha da CRIATURA
    /// (EnemyDefinition), não na habilidade — sem eles o espelho não recomputa camada.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MemoryBank))]
    public sealed class NetworkMemoryBank : NetworkBehaviour
    {
        /// <summary>Um Eco na rede: ids + derrotas + os 3 limiares. Só primitivos.</summary>
        public struct EcoEntry : INetworkSerializable, IEquatable<EcoEntry>
        {
            public FixedString64Bytes FamilyId;
            public FixedString64Bytes AbilityId;
            public int Defeats;
            public int T0, T1, T2;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref FamilyId);
                serializer.SerializeValue(ref AbilityId);
                serializer.SerializeValue(ref Defeats);
                serializer.SerializeValue(ref T0);
                serializer.SerializeValue(ref T1);
                serializer.SerializeValue(ref T2);
            }

            public bool Equals(EcoEntry other) =>
                FamilyId.Equals(other.FamilyId) && AbilityId.Equals(other.AbilityId) &&
                Defeats == other.Defeats && T0 == other.T0 && T1 == other.T1 && T2 == other.T2;
        }

        private readonly NetworkList<EcoEntry> _entries =
            new NetworkList<EcoEntry>(); // defaults: leitura Everyone, escrita Server

        private MemoryBank _memory;
        private bool _mirrorDirty;

        public override void OnNetworkSpawn()
        {
            _memory = GetComponent<MemoryBank>();

            if (IsServer)
            {
                EventBus<MasteryChanged>.Subscribe(OnMasteryChanged);
                RewriteList();
            }
            else
            {
                _entries.OnListChanged += OnEntriesChanged;
                _mirrorDirty = true; // late-join: espelha o que já chegou
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) EventBus<MasteryChanged>.Unsubscribe(OnMasteryChanged);
            else _entries.OnListChanged -= OnEntriesChanged;
        }

        // ── Servidor: MemoryBank → NetworkList ──────────────────────────────────────────

        private void OnMasteryChanged(MasteryChanged e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            RewriteList();
        }

        private void RewriteList()
        {
            _entries.Clear();
            foreach (MemoryBank.Eco eco in _memory.AllEcos)
            {
                _entries.Add(new EcoEntry
                {
                    FamilyId = eco.FamilyId ?? string.Empty,
                    AbilityId = eco.Ability != null ? eco.Ability.abilityId : string.Empty,
                    Defeats = eco.Defeats,
                    T0 = ThresholdAt(eco.Thresholds, 0),
                    T1 = ThresholdAt(eco.Thresholds, 1),
                    T2 = ThresholdAt(eco.Thresholds, 2)
                });
            }
        }

        // Limiar ausente = "nunca desbloqueia" (int.MaxValue) — preserva a semântica do
        // MasteryMath com ficha incompleta em vez de desbloquear com 0 derrotas.
        private static int ThresholdAt(int[] thresholds, int i) =>
            thresholds != null && i < thresholds.Length ? thresholds[i] : int.MaxValue;

        // ── Cliente: NetworkList → MemoryBank (espelho) ─────────────────────────────────

        private void OnEntriesChanged(NetworkListEvent<EcoEntry> _) => _mirrorDirty = true;

        private void Update()
        {
            if (!_mirrorDirty || IsServer) return;
            _mirrorDirty = false;

            var entries = new List<(string, string, int, int, int, int)>(_entries.Count);
            foreach (EcoEntry e in _entries)
                entries.Add((e.FamilyId.ToString(), e.AbilityId.ToString(), e.Defeats, e.T0, e.T1, e.T2));

            _memory.SyncFrom(entries);
            // Diagnóstico do co-op (raro: só quando a memória replicada muda no cliente).
            Debug.Log($"[Rede] Memória replicada (dono {OwnerClientId}): {entries.Count} eco(s).");
        }
    }
}

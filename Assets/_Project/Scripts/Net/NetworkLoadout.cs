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
    /// LOADOUT REPLICADO (M-NET-8 P3) — companion do <see cref="AbilityLoadout"/> no
    /// <c>P_Player</c>, molde do <see cref="NetworkInventory"/>: os 5 slots
    /// autoritativos vivem no servidor e viajam por ESTADO.
    ///
    /// • <b>Servidor:</b> assina <c>AbilityEquipped</c>/<c>AbilityUnequipped</c>/
    ///   <c>AbilityLeveledUp</c> DESTE dono e reescreve a lista. Invest/Respec NÃO
    ///   publicam evento — o <see cref="IncorporationRequestExecutor"/> chama
    ///   <see cref="Rewrite"/> após executá-los.
    /// • <b>Cliente:</b> espelha via <c>AbilityLoadout.SyncFrom</c> (coalescido no
    ///   Update), que atualiza IN-PLACE quando a habilidade não mudou — preservando o
    ///   timestamp de RECARGA local (cooldown é do dono, não replica).
    ///
    /// Com o loadout viajando por NetworkList, o <c>EventScopeAdapter</c> NÃO relaya
    /// mais os 3 eventos de loadout (contrato em <see cref="NetRelayContract"/>).
    /// Os níveis de nó viajam em 4 campos FIXOS (N0..N3) — habilidades têm ≤4 nós
    /// hoje; o RewriteList avisa alto se uma ficha exceder (risco #1 do plano).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AbilityLoadout))]
    public sealed class NetworkLoadout : NetworkBehaviour
    {
        private const int MaxNodes = 4;

        /// <summary>Um slot na rede: ficha por id + progressão + nós fixos. Só primitivos.</summary>
        public struct SlotEntry : INetworkSerializable, IEquatable<SlotEntry>
        {
            public bool Filled;
            public FixedString64Bytes AbilityId;
            public int Layer, Level, Xp, Points;
            public int N0, N1, N2, N3;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Filled);
                serializer.SerializeValue(ref AbilityId);
                serializer.SerializeValue(ref Layer);
                serializer.SerializeValue(ref Level);
                serializer.SerializeValue(ref Xp);
                serializer.SerializeValue(ref Points);
                serializer.SerializeValue(ref N0);
                serializer.SerializeValue(ref N1);
                serializer.SerializeValue(ref N2);
                serializer.SerializeValue(ref N3);
            }

            public bool Equals(SlotEntry other) =>
                Filled == other.Filled && AbilityId.Equals(other.AbilityId) &&
                Layer == other.Layer && Level == other.Level && Xp == other.Xp &&
                Points == other.Points &&
                N0 == other.N0 && N1 == other.N1 && N2 == other.N2 && N3 == other.N3;

            public int[] Nodes() => new[] { N0, N1, N2, N3 };
        }

        private readonly NetworkList<SlotEntry> _slots =
            new NetworkList<SlotEntry>(); // defaults: leitura Everyone, escrita Server

        private AbilityLoadout _loadout;
        private bool _mirrorDirty;

        public override void OnNetworkSpawn()
        {
            _loadout = GetComponent<AbilityLoadout>();

            if (IsServer)
            {
                EventBus<AbilityEquipped>.Subscribe(OnLoadoutEvent);
                EventBus<AbilityUnequipped>.Subscribe(OnUnequipEvent);
                EventBus<AbilityLeveledUp>.Subscribe(OnLevelEvent);
                RewriteList();
            }
            else
            {
                _slots.OnListChanged += OnSlotsChanged;
                _mirrorDirty = true; // late-join
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                EventBus<AbilityEquipped>.Unsubscribe(OnLoadoutEvent);
                EventBus<AbilityUnequipped>.Unsubscribe(OnUnequipEvent);
                EventBus<AbilityLeveledUp>.Unsubscribe(OnLevelEvent);
            }
            else
            {
                _slots.OnListChanged -= OnSlotsChanged;
            }
        }

        // ── Servidor: AbilityLoadout → NetworkList ──────────────────────────────────────

        private void OnLoadoutEvent(AbilityEquipped e) { if (e.OwnerId == NetSeat.Of(this)) RewriteList(); }
        private void OnUnequipEvent(AbilityUnequipped e) { if (e.OwnerId == NetSeat.Of(this)) RewriteList(); }
        private void OnLevelEvent(AbilityLeveledUp e) { if (e.OwnerId == NetSeat.Of(this)) RewriteList(); }

        /// <summary>
        /// Reescreve a lista replicada a partir do loadout local. O executor chama após
        /// Invest/Respec (mutações que não publicam evento). Server-only (no-op fora).
        /// </summary>
        public void Rewrite()
        {
            if (!IsServer) return;
            RewriteList();
        }

        private void RewriteList()
        {
            _slots.Clear();
            for (int i = 0; i < _loadout.Capacity; i++)
            {
                AbilityInstance inst = _loadout.GetSlot(i);
                if (inst == null || inst.Definition == null)
                {
                    _slots.Add(new SlotEntry { Filled = false });
                    continue;
                }

                int[] nodes = inst.NodeLevels;
                if (nodes != null && nodes.Length > MaxNodes)
                    Debug.LogError($"[Rede] Habilidade '{inst.Definition.abilityId}' tem {nodes.Length} nós — o NetworkLoadout replica só {MaxNodes} (N0..N3). Ampliar o SlotEntry.");

                _slots.Add(new SlotEntry
                {
                    Filled = true,
                    AbilityId = inst.Definition.abilityId ?? string.Empty,
                    Layer = (int)inst.Layer,
                    Level = inst.Level,
                    Xp = inst.Xp,
                    Points = inst.PointsAvailable,
                    N0 = NodeAt(nodes, 0),
                    N1 = NodeAt(nodes, 1),
                    N2 = NodeAt(nodes, 2),
                    N3 = NodeAt(nodes, 3)
                });
            }
        }

        private static int NodeAt(int[] nodes, int i) =>
            nodes != null && i < nodes.Length ? nodes[i] : 0;

        // ── Cliente: NetworkList → AbilityLoadout (espelho) ─────────────────────────────

        private void OnSlotsChanged(NetworkListEvent<SlotEntry> _) => _mirrorDirty = true;

        private void Update()
        {
            if (!_mirrorDirty || IsServer) return;
            _mirrorDirty = false;

            var slots = new List<(bool, string, int, int, int, int, int[])>(_slots.Count);
            foreach (SlotEntry s in _slots)
                slots.Add((s.Filled, s.AbilityId.ToString(), s.Layer, s.Level, s.Xp, s.Points, s.Nodes()));

            _loadout.SyncFrom(slots);
            // Diagnóstico do co-op (raro: só quando o loadout replicado muda no cliente).
            Debug.Log($"[Rede] Loadout replicado (dono {OwnerClientId}).");
        }
    }
}

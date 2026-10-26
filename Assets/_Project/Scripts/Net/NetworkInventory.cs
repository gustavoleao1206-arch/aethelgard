using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// INVENTÁRIO REPLICADO (M-NET-7 P2) — companion do <see cref="PlayerInventory"/>
    /// no prefab <c>P_Player</c>, a peça central do mundo compartilhado. Mesmo padrão
    /// do <see cref="NetworkHealth"/>: o estado autoritativo vive no servidor e viaja
    /// por replicação de ESTADO (não por eventos — robusto a perda e a late-join).
    ///
    /// • <b>Servidor:</b> é a fonte — semeia a <see cref="NetworkList{T}"/> no spawn e a
    ///   reescreve quando o <c>InventoryChanged</c> DESTE dono dispara (assinatura
    ///   filtrada, não poll). Reescrita completa: são ≤20 slots e mudanças são raras
    ///   (coleta/craft) — simplicidade primeiro; otimizar só com Profiler acusando.
    /// • <b>Cliente:</b> espelha no <see cref="PlayerInventory"/> local via
    ///   <c>SyncFrom</c> (que republica o <c>InventoryChanged</c> — HUD/Crafting
    ///   releem os slots). Mudanças da lista são coalescidas num flag e aplicadas 1×
    ///   no Update (a reescrita completa dispara N callbacks; espelhar N vezes por
    ///   frame seria desperdício).
    ///
    /// Com o inventário viajando por NetworkList, o <c>EventScopeAdapter</c> NÃO
    /// relaya mais <c>ItemCollected</c>/<c>InventoryChanged</c> (chegariam 2× no
    /// cliente) — contrato em <see cref="NetRelayContract"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class NetworkInventory : NetworkBehaviour
    {
        /// <summary>Um slot na rede: id do item (string fixa) + quantidade. Só primitivos.</summary>
        public struct InventoryEntry : INetworkSerializable, IEquatable<InventoryEntry>
        {
            public FixedString64Bytes ItemId;
            public int Count;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref ItemId);
                serializer.SerializeValue(ref Count);
            }

            public bool Equals(InventoryEntry other) => ItemId.Equals(other.ItemId) && Count == other.Count;
        }

        private readonly NetworkList<InventoryEntry> _entries =
            new NetworkList<InventoryEntry>(); // defaults: leitura Everyone, escrita Server

        private PlayerInventory _inventory;
        private bool _mirrorDirty; // cliente: aplica o espelho 1× no Update (coalescência)

        public override void OnNetworkSpawn()
        {
            _inventory = GetComponent<PlayerInventory>();

            if (IsServer)
            {
                EventBus<InventoryChanged>.Subscribe(OnInventoryChanged);
                RewriteList(); // semeia o estado atual (em geral vazio no spawn)
            }
            else
            {
                _entries.OnListChanged += OnEntriesChanged;
                _mirrorDirty = true; // late-join: espelha o que já chegou
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) EventBus<InventoryChanged>.Unsubscribe(OnInventoryChanged);
            else _entries.OnListChanged -= OnEntriesChanged;
        }

        // ── Servidor: PlayerInventory → NetworkList ─────────────────────────────────────

        // O InventoryChanged é global e identifica o dono — só reescreve se for o MEU.
        private void OnInventoryChanged(InventoryChanged e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            RewriteList();
        }

        private void RewriteList()
        {
            _entries.Clear();
            IReadOnlyList<ItemStack> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                ItemStack stack = slots[i];
                if (stack.Definition == null || stack.Count <= 0) continue;
                _entries.Add(new InventoryEntry
                {
                    ItemId = stack.Definition.itemId ?? string.Empty,
                    Count = stack.Count
                });
            }
        }

        // ── Cliente: NetworkList → PlayerInventory (espelho) ────────────────────────────

        private void OnEntriesChanged(NetworkListEvent<InventoryEntry> _) => _mirrorDirty = true;

        private void Update()
        {
            if (!_mirrorDirty || IsServer) return;
            _mirrorDirty = false;

            var entries = new List<(string itemId, int count)>(_entries.Count);
            foreach (InventoryEntry e in _entries)
                entries.Add((e.ItemId.ToString(), e.Count));

            _inventory.SyncFrom(entries);
        }
    }
}

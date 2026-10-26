using System.Collections.Generic;
using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Incorporation;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.Saving;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// As habilidades EQUIPADAS do jogador: até 5 slots, uma por criatura/família,
    /// e troca permitida só quando o jogador está num Altar. Fica no objeto do
    /// jogador (por-jogador, com Ownable). As regras puras estão em LoadoutMath.
    ///
    /// Avisa por eventos (AbilityEquipped/Unequipped) carregando o OwnerId — a UI
    /// reage sem acoplar à Simulação (multiplayer-ready).
    ///
    /// MUNDO COMPARTILHADO (M-NET-8): o loadout é AUTORITATIVO no servidor.
    /// • Sem autoridade (cliente), as mutações publicam INTENÇÕES (Equip/Unequip/
    ///   InvestNode/Respec Requested) — o servidor valida (Altar por PROXIMIDADE
    ///   real, custo do respec no inventário autoritativo) e o resultado volta
    ///   replicado (NetworkLoadout → SyncFrom).
    /// • O XP por uso ingere AQUI (assinatura de AbilityUsed, gateada) e não mais no
    ///   AbilityRunner — ponto único: o AbilityUsed do dono-cliente chega ao servidor
    ///   pelo NetworkStyleForwarder e a cópia de lá o ingere.
    /// • 🎨 Respec custa 2 Pedra (decisão de 03/07/2026; configurável no Inspector).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Ownable))]
    public sealed class AbilityLoadout : MonoBehaviour, ISaveable
    {
        public const int MaxAbilities = LoadoutMath.MaxAbilities;

        [Tooltip("Custo do respec (🎨 2 Pedra). Debitado do inventário do dono, no servidor.")]
        [SerializeField] private RecipeIngredient[] _respecCost;

        private readonly AbilityInstance[] _slots = new AbilityInstance[MaxAbilities];
        private Ownable _ownable;
        private PlayerInventory _inventory; // paga o respec (irmão no player)

        /// <summary>Verdadeiro enquanto o jogador está com um Altar aberto (libera trocas).</summary>
        public bool AtAltar { get; set; }

        private void Awake()
        {
            _ownable = GetComponent<Ownable>();
            _inventory = GetComponent<PlayerInventory>();
        }

        private void OnEnable() => EventBus<AbilityUsed>.Subscribe(OnAbilityUsed);
        private void OnDisable() => EventBus<AbilityUsed>.Unsubscribe(OnAbilityUsed);

        // XP por uso (M-NET-8: movido do AbilityRunner para cá — ponto único, gateado).
        // No servidor, o AbilityUsed do cliente chega pelo NetworkStyleForwarder; o do
        // host nasce local. No cliente o gate bloqueia: Level/Xp voltam replicados.
        private void OnAbilityUsed(AbilityUsed e)
        {
            if (!SimulationAuthority.IsAuthority) return;
            if (e.OwnerId != _ownable.OwnerId) return;

            AbilityInstance inst = GetSlot(e.Slot);
            if (inst == null) return;

            if (inst.AddXp(AbilityMath.XpPerUse))
            {
                EventBus<AbilityLeveledUp>.Publish(new AbilityLeveledUp
                {
                    OwnerId = _ownable.OwnerId,
                    Slot = e.Slot,
                    Ability = inst.Definition,
                    Level = inst.Level
                });
            }
        }

        public int Capacity => MaxAbilities;
        public AbilityInstance GetSlot(int index) =>
            (index >= 0 && index < _slots.Length) ? _slots[index] : null;

        private IReadOnlyList<string> EquippedFamilyIds()
        {
            var ids = new List<string>(MaxAbilities);
            foreach (AbilityInstance inst in _slots)
            {
                if (inst != null) ids.Add(inst.FamilyId);
            }
            return ids;
        }

        /// <summary>
        /// Tenta equipar a habilidade (no primeiro slot livre). Respeita o limite,
        /// "uma por criatura" e "só no Altar" (LoadoutMath). Devolve o motivo em caso de recusa.
        /// </summary>
        public bool TryEquip(AbilityInstance instance, out string reason)
        {
            if (instance == null || instance.Definition == null)
            {
                reason = "Habilidade inválida.";
                return false;
            }

            // SEM autoridade (cliente): publica a INTENÇÃO — o servidor valida no
            // MemoryBank autoritativo + proximidade real do Altar e o slot volta
            // replicado (padrão do TryCraft, M-NET-7 P4).
            if (!SimulationAuthority.IsAuthority)
            {
                EventBus<EquipRequested>.Publish(new EquipRequested
                {
                    AbilityId = instance.Definition.abilityId,
                    OwnerId = _ownable.OwnerId
                });
                reason = "Pedido enviado ao servidor…";
                return false;
            }

            if (!LoadoutMath.CanEquip(EquippedFamilyIds(), instance.FamilyId, AtAltar, out reason))
            {
                return false;
            }

            int slot = FirstFreeSlot();
            if (slot < 0)
            {
                reason = $"Limite de {MaxAbilities} habilidades equipadas atingido.";
                return false;
            }

            _slots[slot] = instance;
            EventBus<AbilityEquipped>.Publish(new AbilityEquipped
            {
                OwnerId = _ownable.OwnerId,
                Slot = slot,
                Ability = instance.Definition
            });
            return true;
        }

        /// <summary>Esvazia um slot (só permitido num Altar).</summary>
        public bool Unequip(int slot)
        {
            if (!SimulationAuthority.IsAuthority)
            {
                EventBus<UnequipRequested>.Publish(new UnequipRequested
                {
                    Slot = slot,
                    OwnerId = _ownable.OwnerId
                });
                return false;
            }

            if (!AtAltar) return false;
            if (slot < 0 || slot >= _slots.Length || _slots[slot] == null) return false;

            _slots[slot] = null;
            EventBus<AbilityUnequipped>.Publish(new AbilityUnequipped
            {
                OwnerId = _ownable.OwnerId,
                Slot = slot
            });
            return true;
        }

        /// <summary>
        /// Investe um ponto num nó de especialização do slot (operação de Altar).
        /// Sem autoridade publica <see cref="InvestNodeRequested"/> e retorna false.
        /// </summary>
        public bool TryInvestNode(int slot, int nodeIndex, out string reason)
        {
            if (!SimulationAuthority.IsAuthority)
            {
                EventBus<InvestNodeRequested>.Publish(new InvestNodeRequested
                {
                    Slot = slot,
                    NodeIndex = nodeIndex,
                    OwnerId = _ownable.OwnerId
                });
                reason = "Pedido enviado ao servidor…";
                return false;
            }

            AbilityInstance inst = GetSlot(slot);
            if (inst == null)
            {
                reason = "Slot vazio.";
                return false;
            }
            return inst.TryInvest(nodeIndex, out reason);
        }

        /// <summary>
        /// RESPEC do slot: reverte os nós e devolve os pontos, cobrando o custo
        /// (🎨 2 Pedra) do inventário do dono — atômico: sem material, nada reverte.
        /// Operação de Altar. Sem autoridade publica <see cref="RespecRequested"/>.
        /// </summary>
        public bool TryRespec(int slot, out string reason)
        {
            if (!SimulationAuthority.IsAuthority)
            {
                EventBus<RespecRequested>.Publish(new RespecRequested
                {
                    Slot = slot,
                    OwnerId = _ownable.OwnerId
                });
                reason = "Pedido enviado ao servidor…";
                return false;
            }

            if (!AtAltar)
            {
                reason = "Só é possível respecar num Altar.";
                return false;
            }

            AbilityInstance inst = GetSlot(slot);
            if (inst == null)
            {
                reason = "Slot vazio.";
                return false;
            }

            // Cobra o custo ANTES de reverter (SpendCost é atômico: só paga se puder).
            if (_inventory != null && !_inventory.SpendCost(_respecCost))
            {
                reason = "Falta material para o respec (" + CostLabel() + ").";
                return false;
            }

            inst.Respec();
            reason = null;
            return true;
        }

        // Descreve o custo do respec para a UI ("2× Pedra").
        private string CostLabel()
        {
            if (_respecCost == null || _respecCost.Length == 0) return "grátis";
            var parts = new List<string>(_respecCost.Length);
            foreach (RecipeIngredient ing in _respecCost)
            {
                if (ing.item != null) parts.Add($"{ing.amount}× {ing.item.displayName}");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "grátis";
        }

        /// <summary>
        /// Pede para ativar o slot. Devolve a instância pronta para uso (recarga OK)
        /// ou null. Quem executa o efeito é o AbilityRunner — aqui só validamos.
        /// </summary>
        public AbilityInstance RequestActivate(int slot)
        {
            AbilityInstance inst = GetSlot(slot);
            if (inst == null || !inst.IsReady) return null;
            return inst;
        }

        private int FirstFreeSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) return i;
            }
            return -1;
        }

        /// <summary>
        /// ESPELHO da réplica (M-NET-8): aplica o loadout AUTORITATIVO recebido do
        /// servidor (via NetworkLoadout). Reconstrói por abilityId (desenho do
        /// RestoreState) — mas quando o slot JÁ tem a mesma habilidade, atualiza a
        /// instância IN-PLACE para preservar o timestamp de recarga LOCAL (o cooldown
        /// é do dono, não replica — decisão do plano §3.1). Republica
        /// AbilityEquipped/Unequipped só nos slots que mudaram de habilidade.
        /// Tupla: (filled, abilityId, layer, level, xp, points, nodeLevels).
        /// </summary>
        public void SyncFrom(IReadOnlyList<(bool filled, string abilityId, int layer, int level, int xp, int points, int[] nodeLevels)> slots)
        {
            if (slots == null) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                var data = i < slots.Count ? slots[i] : default;

                if (!data.filled)
                {
                    if (_slots[i] != null)
                    {
                        _slots[i] = null;
                        EventBus<AbilityUnequipped>.Publish(new AbilityUnequipped
                        {
                            OwnerId = _ownable.OwnerId,
                            Slot = i
                        });
                    }
                    continue;
                }

                AbilityInstance current = _slots[i];
                bool sameAbility = current != null && current.Definition != null &&
                                   current.Definition.abilityId == data.abilityId;

                if (!sameAbility)
                {
                    AbilityDefinition def = DefinitionRegistry.Active != null
                        ? DefinitionRegistry.Active.AbilityById(data.abilityId) : null;
                    if (def == null) continue; // ficha desconhecida — melhor slot antigo que NRE

                    current = new AbilityInstance(def, (IncorporationLayer)data.layer);
                    _slots[i] = current;

                    EventBus<AbilityEquipped>.Publish(new AbilityEquipped
                    {
                        OwnerId = _ownable.OwnerId,
                        Slot = i,
                        Ability = def
                    });
                }

                // Progressão sempre espelhada (in-place preserva _lastUsedTime local).
                current.Layer = (IncorporationLayer)data.layer;
                current.Level = data.level;
                current.Xp = data.xp;
                current.PointsAvailable = data.points;
                if (data.nodeLevels != null && current.NodeLevels != null)
                {
                    int n = Mathf.Min(current.NodeLevels.Length, data.nodeLevels.Length);
                    for (int k = 0; k < n; k++) current.NodeLevels[k] = data.nodeLevels[k];
                }
            }
        }

        // ---- Save (ISaveable) ----

        public string SaveKey => "loadout:" + _ownable.OwnerId;

        [System.Serializable]
        private struct SlotSave
        {
            public bool filled;
            public string abilityId;
            public int layer;
            public int level;
            public int xp;
            public int points;
            public int[] nodeLevels;
        }

        [System.Serializable]
        private struct LoadoutSave { public List<SlotSave> slots; }

        public object CaptureState()
        {
            var data = new LoadoutSave { slots = new List<SlotSave>() };
            for (int i = 0; i < _slots.Length; i++)
            {
                AbilityInstance inst = _slots[i];
                if (inst == null || inst.Definition == null)
                {
                    data.slots.Add(new SlotSave { filled = false });
                    continue;
                }
                data.slots.Add(new SlotSave
                {
                    filled = true,
                    abilityId = inst.Definition.abilityId,
                    layer = (int)inst.Layer,
                    level = inst.Level,
                    xp = inst.Xp,
                    points = inst.PointsAvailable,
                    nodeLevels = inst.NodeLevels
                });
            }
            return data;
        }

        public void RestoreState(object state)
        {
            var data = JsonUtility.FromJson<LoadoutSave>((string)state);
            for (int i = 0; i < _slots.Length; i++) _slots[i] = null;
            if (data.slots == null) return;

            for (int i = 0; i < data.slots.Count && i < _slots.Length; i++)
            {
                SlotSave ss = data.slots[i];
                if (!ss.filled) continue;

                AbilityDefinition def = DefinitionRegistry.Active != null
                    ? DefinitionRegistry.Active.AbilityById(ss.abilityId) : null;
                if (def == null) continue;

                var inst = new AbilityInstance(def, (IncorporationLayer)ss.layer)
                {
                    Level = ss.level,
                    Xp = ss.xp,
                    PointsAvailable = ss.points
                };
                // Copia os níveis para o array JÁ dimensionado pela Definition atual
                // (o construtor o criou com o tamanho de specializations). Se a ficha
                // mudou de nós entre o save e o load, isto evita estourar índice na
                // AltarUI/TryInvest — nós novos ficam zerados.
                if (ss.nodeLevels != null && inst.NodeLevels != null)
                {
                    int n = Mathf.Min(inst.NodeLevels.Length, ss.nodeLevels.Length);
                    for (int k = 0; k < n; k++) inst.NodeLevels[k] = ss.nodeLevels[k];
                }
                _slots[i] = inst;

                EventBus<AbilityEquipped>.Publish(new AbilityEquipped
                {
                    OwnerId = _ownable.OwnerId,
                    Slot = i,
                    Ability = def
                });
            }
        }
    }
}

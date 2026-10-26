using UnityEngine;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Combat;
using JogoDaTrupe.Input;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Usa as habilidades equipadas. Lê a INTENÇÃO do jogador (teclas 1–5, via
    /// PlayerInputReader — mesmo padrão do ataque) e, se o slot tiver uma habilidade
    /// pronta, executa o efeito.
    ///
    /// No slice o único efeito é um "golpe à frente" (rajada de dano numa esfera),
    /// reaproveitando o padrão do MeleeAttack. Outros tipos de efeito entram depois;
    /// por isso o efeito concreto fica aqui (Gameplay) e não na ficha (Data).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AbilityLoadout))]
    public sealed class AbilityRunner : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _origin;
        [SerializeField] private LayerMask _hittableLayers = ~0;

        private AbilityLoadout _loadout;
        private Ownable _ownable;
        private PlayerAttributes _attributes;
        private AffinityState _affinity;
        private readonly Collider[] _buffer = new Collider[16];

        private void Awake()
        {
            _loadout = GetComponent<AbilityLoadout>();
            _ownable = GetComponent<Ownable>();
            _attributes = GetComponent<PlayerAttributes>();
            _affinity = GetComponent<AffinityState>();
            if (_input == null) _input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            if (_input == null) return;

            int slot = _input.AbilitySlotPressedThisFrame; // 0..4, ou -1
            if (slot < 0) return;

            AbilityInstance inst = _loadout.RequestActivate(slot);
            if (inst == null) return; // slot vazio ou em recarga

            int ownerId = _ownable != null ? _ownable.OwnerId : Ownable.LocalPlayerId;

            ExecuteEffect(inst, ownerId);
            inst.MarkUsed();

            // O XP por uso ingere no AbilityLoadout (assinatura de AbilityUsed,
            // gateada por autoridade — M-NET-8): ponto único que funciona no solo,
            // no host e no servidor recebendo o AbilityUsed do cliente pelo
            // NetworkStyleForwarder. Aqui só anunciamos o uso.
            EventBus<AbilityUsed>.Publish(new AbilityUsed
            {
                OwnerId = ownerId,
                Slot = slot,
                Ability = inst.Definition
            });
        }

        // Efeito do slice: atinge tudo que for IDamageable numa esfera à frente.
        // Dano = especialização (FinalDamage) × penalidade de atributo × bônus de afinidade.
        // A afinidade usada aqui é a ATUAL; o evento AbilityUsed (publicado depois)
        // é que sobe a afinidade — então este uso se beneficia do que já foi acumulado.
        // Se a habilidade for de controle (Snare, ex.: Cipó), também PRENDE o alvo.
        private void ExecuteEffect(AbilityInstance inst, int attackerOwnerId)
        {
            AbilityDefinition def = inst.Definition;
            float damage = inst.FinalDamage
                * AttributePenalty(def)
                * (1f + AffinityBonus(def));
            bool snare = def.effect == AbilityEffect.Snare;

            Transform o = _origin != null ? _origin : transform;
            Vector3 center = o.position + o.forward * (inst.FinalRange * 0.5f) + Vector3.up * 0.8f;

            int count = Physics.OverlapSphereNonAlloc(
                center, inst.FinalRadius, _buffer, _hittableLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider c = _buffer[i];
                if (c == null) continue;
                if (c.transform.root == transform.root) continue; // não acerta a si mesmo

                if (c.TryGetComponent(out IDamageable target) && !target.IsDead)
                {
                    float dealt = damage;

                    // Resistência elemental (ex.: boss que resiste ao elemento repetido).
                    if (c.TryGetComponent(out IElementResistant resist))
                        dealt *= resist.DamageMultiplierFor(def.element);

                    target.TakeDamage(dealt, attackerOwnerId);

                    // Controle: prende o alvo (se ele puder ser preso).
                    if (snare && c.TryGetComponent(out IRootable rootable))
                    {
                        rootable.ApplyRoot(def.snareSeconds);
                    }
                }
            }
        }

        // Penalidade suave: se faltar o atributo exigido pela habilidade, ela rende
        // menos (não bloqueia). Sem PlayerAttributes, não há penalidade.
        private float AttributePenalty(AbilityDefinition def)
        {
            if (_attributes == null) return 1f;
            int current = _attributes.GetValue(def.requiredAttribute);
            return AttributeMath.PenaltyMultiplier(def.requiredAttributeValue, current);
        }

        // Bônus do especialista no elemento da habilidade (0 se não houver afinidade alta).
        private float AffinityBonus(AbilityDefinition def)
        {
            return _affinity != null ? _affinity.SpecialistBonus(def.element) : 0f;
        }
    }
}

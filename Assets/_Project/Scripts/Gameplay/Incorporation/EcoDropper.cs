using UnityEngine;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Combat;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Faz a criatura deixar um Eco ao morrer. Fica na criatura, conhece sua
    /// <see cref="EnemyDefinition"/> (que carrega a habilidade concedida) e publica
    /// <c>EcoCollected</c> <b>uma vez por jogador que contribuiu na derrota</b> — a
    /// decisão de design "todos os participantes recebem o Eco".
    ///
    /// Reage ao evento <c>EntityDied</c> (não faz mais poll no Update). Na rede, a morte é
    /// decidida no host (modelo "host decide", M-NET-5) e o <c>EventScopeAdapter</c> (P4)
    /// replica cada <c>EcoCollected</c> aos clientes. Em solo/host roda idêntico: o
    /// <c>MemoryBank</c> de cada jogador filtra por dono e cada contribuinte recebe o seu.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EcoDropper : MonoBehaviour
    {
        [SerializeField] private EnemyDefinition _definition;

        private Health _health;
        private bool _dropped;

        private void Awake() => _health = GetComponent<Health>();

        private void OnEnable() => EventBus<EntityDied>.Subscribe(OnEntityDied);
        private void OnDisable() => EventBus<EntityDied>.Unsubscribe(OnEntityDied);

        // EntityDied é global e não identifica a instância morta (só carrega OwnerId, que
        // para inimigo é sempre NoOwner). Por isso o gate: só age se for ESTA criatura que
        // morreu (o próprio _health, mesmo GameObject via RequireComponent) + idempotência.
        private void OnEntityDied(EntityDied e)
        {
            // Só a autoridade dropa (M-NET-6 P0): no cliente, quando a vida replicada
            // zerar, o dropper local NÃO publica — o EcoCollected chega pelo relay do
            // servidor (senão o cliente ganharia o Eco em dobro).
            if (!SimulationAuthority.IsAuthority) return;

            if (_dropped || !_health.IsDead) return;
            _dropped = true;

            // Um Eco para CADA contribuinte (a lista já vem deduplicada e sem IA do Health).
            foreach (int ownerId in EcoDrop.Recipients(_definition, _health.Contributors))
            {
                EventBus<EcoCollected>.Publish(new EcoCollected
                {
                    OwnerId = ownerId,
                    Source = _definition
                });
            }
        }
    }
}

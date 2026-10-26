using System.Collections.Generic;
using UnityEngine;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Identity;
using JogoDaTrupe.Core.Ownership;

namespace JogoDaTrupe.Gameplay.Combat
{
    /// <summary>
    /// Vida de uma entidade. Componente REUTILIZÁVEL: o mesmo serve para o player,
    /// inimigos e o boss — basta adicionar e ajustar o máximo no Inspector.
    ///
    /// Avisa o resto do jogo por eventos (HealthChanged, EntityDied) carregando o
    /// dono (Ownable), sem conhecer quem escuta (HUD, áudio, etc.). Implementa
    /// IDamageable para que qualquer ataque possa causar dano sem conhecer o tipo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _max = 100f;

        private float _current;
        private float _minimum; // piso de vida (0 = pode morrer; > 0 = não morre, ex.: criatura capturável)
        private Ownable _ownable;
        private EntityGuid _guid; // identidade estável (usada para encaminhar o dano do cliente — M-NET-6 P4)
        private IDamageMitigator _mitigator; // opcional: reduz o dano antes de aplicar (ex.: Blocker/escudo)
        private readonly DamageContributors _contributors = new DamageContributors();

        public float Current => _current;
        public float Max => _max;
        public bool IsDead => _current <= 0f;

        /// <summary>Donos (OwnerId) dos jogadores que causaram dano — os "contribuintes" do Eco (M-NET-5).</summary>
        public IReadOnlyCollection<int> Contributors => _contributors.Ids;

        // Sem Ownable, a entidade é tratada como "sem dono" (ex.: inimigos), para
        // não se confundir com o jogador local (id 0) — importante para o HUD.
        private int OwnerId => _ownable != null ? _ownable.OwnerId : Ownable.NoOwner;

        private void Awake()
        {
            _ownable = GetComponent<Ownable>();
            _guid = GetComponent<EntityGuid>();
            _mitigator = GetComponent<IDamageMitigator>();
            _current = _max;
        }

        private void Start()
        {
            // Publica o valor inicial para o HUD começar correto.
            PublishChanged();
        }

        public void TakeDamage(float amount, int attackerOwnerId)
        {
            if (IsDead) return;

            // Rede (M-NET-6 P4): numa máquina SEM autoridade (cliente no co-op) o dano NÃO
            // muta aqui — publica a INTENÇÃO e o NetworkDamageForwarder do alvo a leva ao
            // servidor, que aplica de verdade (a vida volta replicada pelo NetworkHealth).
            // Padrão Input → Intenção → Ação. Em solo/host esta máquina é sempre a
            // autoridade, então segue direto pelo caminho normal abaixo.
            if (!SimulationAuthority.IsAuthority)
            {
                EventBus<DamageRequested>.Publish(new DamageRequested
                {
                    TargetGuid = _guid != null ? _guid.Value : string.Empty,
                    Amount = amount,
                    AttackerOwnerId = attackerOwnerId
                });
                return;
            }

            // Guarda levantada? O escudo absorve parte do golpe ANTES de a vida ser tocada.
            // Só a autoridade passa por aqui (o gate acima já devolveu no cliente), então a
            // mitigação acontece uma única vez, no mesmo lugar em que o dano é aplicado.
            if (_mitigator != null) amount = Mathf.Max(0f, _mitigator.Mitigate(amount, attackerOwnerId));

            // Anota quem bateu (para o "Eco a todos os contribuintes"). Dano de IA/ambiente
            // (NoOwner) é ignorado pelo próprio DamageContributors. CombatMath fica intocado.
            _contributors.Register(attackerOwnerId);

            _current = CombatMath.ApplyDamage(_current, amount, _minimum);
            PublishChanged();

            if (IsDead)
            {
                EventBus<EntityDied>.Publish(new EntityDied { OwnerId = OwnerId });
            }
        }

        /// <summary>
        /// Define um PISO de vida: a entidade não cai abaixo dele por dano (e, se o
        /// piso for &gt; 0, não morre). Usado pela criatura capturável, que enfraquece
        /// até o limiar mas não morre. O piso é limitado a [0, Max].
        /// </summary>
        public void SetMinimum(float minimum)
        {
            _minimum = Mathf.Clamp(minimum, 0f, _max);
            if (_current < _minimum)
            {
                _current = _minimum;
                PublishChanged();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;

            _current = CombatMath.Heal(_current, amount, _max);
            PublishChanged();
        }

        /// <summary>
        /// REVIVE a entidade (M-NET-9 P0): restaura a vida cheia e zera os contribuintes
        /// (o próximo Eco começa limpo). Só a AUTORIDADE muta — no cliente a vida volta
        /// replicada pelo <c>NetworkHealth</c>; sem o gate, o cliente reviveria em
        /// paralelo (divergência). Chamado pelo <c>PlayerRespawner</c> após o tempo de
        /// respawn. Vale no solo (autoridade local).
        /// </summary>
        public void Revive()
        {
            if (!SimulationAuthority.IsAuthority) return;

            _contributors.Clear();
            _current = _max;
            PublishChanged();
        }

        /// <summary>
        /// ESPELHA um valor de vida vindo da REDE (M-NET-6 P3). Só a RÉPLICA do cliente
        /// usa isto (via <c>NetworkHealth</c>): o valor autoritativo é decidido no servidor
        /// e chega por <c>NetworkVariable</c>; aqui apenas se copia e republica o
        /// <c>HealthChanged</c> local (HUD/feedback). NÃO publica <c>EntityDied</c> —
        /// morte é decisão do servidor (chega pelo relay) e o corpo some pelo despawn.
        /// </summary>
        public void SyncFrom(float current)
        {
            float clamped = Mathf.Clamp(current, 0f, _max);
            if (Mathf.Approximately(clamped, _current)) return;

            _current = clamped;
            PublishChanged();
        }

        private void PublishChanged()
        {
            EventBus<HealthChanged>.Publish(new HealthChanged
            {
                OwnerId = OwnerId,
                Current = _current,
                Max = _max
            });
        }
    }
}

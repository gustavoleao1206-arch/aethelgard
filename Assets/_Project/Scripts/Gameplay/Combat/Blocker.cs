using UnityEngine;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Input;

namespace JogoDaTrupe.Gameplay.Combat
{
    /// <summary>
    /// BLOQUEIO com o escudo: enquanto o jogador segura o botão direito, o herói levanta a
    /// guarda e absorve a maior parte do dano. O <see cref="Health"/> passa o dano por aqui
    /// (via <see cref="IDamageMitigator"/>) ANTES de descontar da vida.
    ///
    /// Regras:
    ///  • Não dá pra atacar e defender ao mesmo tempo — golpe em andamento cancela a guarda.
    ///  • Também não se bloqueia deslizando (o corpo está no chão) nem morto.
    ///
    /// Lê a INTENÇÃO (PlayerInputReader), não a tecla — mesmo padrão do MeleeAttack.
    ///
    /// Co-op: a mitigação roda na máquina com AUTORIDADE (o host), mas o botão é apertado no
    /// CLIENTE, onde só o input daquele boneco está vivo. Por isso a guarda tem DOIS lados:
    ///  • <see cref="WantsToBlock"/> — a intenção CRUA, computada no DONO (input + não morto +
    ///    não golpeando), onde esses componentes estão ligados;
    ///  • <see cref="IsBlocking"/> — o estado EFETIVO que anima e mitiga. Em rede vem replicado
    ///    (o companion <c>NetworkBlocker</c> escreve via <see cref="SyncBlocking"/>, mesmo padrão
    ///    do <c>Health.SyncFrom</c>); em solo, é a própria intenção local.
    ///
    /// Assim o host mitiga o dano do cliente pela guarda DELE, e os outros players veem o escudo
    /// levantado. A parede de assembly fica de pé: quem conhece a rede é o Net; aqui só se recebe
    /// um bool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Blocker : MonoBehaviour, IDamageMitigator
    {
        [Header("Referências (auto se vazias)")]
        [SerializeField] private PlayerInputReader _input;

        [Header("Guarda")]
        [Tooltip("Fração do dano ABSORVIDA pelo escudo (0.7 = o herói toma só 30%).")]
        [Range(0f, 1f)]
        [SerializeField] private float _damageReduction = 0.7f;

        private MeleeAttack _melee;
        private Health _health;

        private bool _networkControlled; // um NetworkBlocker está alimentando o estado?
        private bool _networkBlocking;   // último valor replicado

        /// <summary>
        /// Intenção CRUA de bloquear, lida do input local. Vale onde o input/melee/health estão
        /// vivos — ou seja, no DONO. É o que o companion de rede replica.
        /// </summary>
        public bool WantsToBlock =>
            _input != null && _input.isActiveAndEnabled && _input.BlockHeld &&
            (_health == null || !_health.IsDead) &&
            (_melee == null || !_melee.IsAttacking);

        /// <summary>
        /// O herói está de guarda levantada? Estado EFETIVO que a animação e a mitigação leem.
        /// Em rede, é o valor replicado; em solo, a intenção local.
        /// </summary>
        public bool IsBlocking => _networkControlled ? _networkBlocking : WantsToBlock;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<PlayerInputReader>();
            _melee = GetComponent<MeleeAttack>();
            _health = GetComponent<Health>();
        }

        /// <summary>
        /// ESPELHA o estado de guarda vindo da rede (chamado todo frame pelo <c>NetworkBlocker</c>).
        /// A partir daqui a guarda deixa de ler o input local e passa a seguir a réplica — mesmo
        /// desenho do <c>Health.SyncFrom</c>. Nunca chamado em solo (sem companion de rede).
        /// </summary>
        public void SyncBlocking(bool blocking)
        {
            _networkControlled = true;
            _networkBlocking = blocking;
        }

        /// <summary>Chamado pelo Health antes de aplicar o dano.</summary>
        public float Mitigate(float amount, int attackerOwnerId)
        {
            if (!IsBlocking) return amount;
            return Mathf.Max(0f, amount * (1f - _damageReduction));
        }
    }
}

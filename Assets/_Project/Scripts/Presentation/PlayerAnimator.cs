using UnityEngine;
using JogoDaTrupe.Gameplay.Characters;
using JogoDaTrupe.Gameplay.Combat;

namespace JogoDaTrupe.Presentation
{
    /// <summary>
    /// Dá VIDA ao mesh do herói: lê o estado do <see cref="PlayerController"/> e toca o
    /// estado de animação certo no <see cref="Animator"/> (via CrossFade), sem root motion —
    /// quem move o corpo é o CharacterController; as animações tocam "no lugar".
    ///
    /// Fica no ROOT do player; o Animator está no mesh-filho (HeroVisual). Os estados
    /// esperados no controller: Idle/Walk/Run/Jump/Fall/Crouch/Slide (ver HeroSetup).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerController _controller;
        [SerializeField] private MeleeAttack _melee;
        [SerializeField] private Blocker _blocker;
        [SerializeField] private Health _health;
        [SerializeField] private float _crossFade = 0.12f;

        [Tooltip("Quanto tempo o herói 'sente' o golpe recebido (flinch).")]
        [SerializeField] private float _hitFlinch = 0.35f;

        // Limiares de locomoção (sobre LocomotionSpeed01: 0 parado .. 1 correndo).
        private const float WalkAt = 0.10f;
        private const float RunAt = 0.62f;

        // Fallback p/ bonecos REMOTOS (co-op): o dono DESLIGA o PlayerController dos players
        // dos outros (NetworkPlayerSetup), então lá não há LocomotionSpeed01. Medimos a
        // velocidade real do transform (que a rede move via ClientNetworkTransform) e
        // derivamos andar/correr daí. ~ _moveSpeed(5) * _runMultiplier(1.7).
        private const float MaxPlanarSpeed = 8.5f;

        private int _current;
        private Vector3 _lastPlanarPos;
        private float _measuredSpeed01;
        private bool _hasLastPos;
        private float _lastHp = -1f;   // detecta dano recebido comparando a vida frame a frame
        private float _hitUntil = -1f; // janela do flinch

        private static readonly int Idle = Animator.StringToHash("Idle");
        private static readonly int Walk = Animator.StringToHash("Walk");
        private static readonly int Run = Animator.StringToHash("Run");
        private static readonly int Jump = Animator.StringToHash("Jump");
        private static readonly int Fall = Animator.StringToHash("Fall");
        private static readonly int Crouch = Animator.StringToHash("Crouch");
        private static readonly int Slide = Animator.StringToHash("Slide");
        private static readonly int Attack1 = Animator.StringToHash("Attack1");
        private static readonly int Attack2 = Animator.StringToHash("Attack2");
        private static readonly int Block = Animator.StringToHash("Block");
        private static readonly int Hit = Animator.StringToHash("Hit");
        private static readonly int Death = Animator.StringToHash("Death");

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_controller == null) _controller = GetComponent<PlayerController>();
            if (_melee == null) _melee = GetComponent<MeleeAttack>();
            if (_blocker == null) _blocker = GetComponent<Blocker>();
            if (_health == null) _health = GetComponent<Health>();
            if (_animator != null) _animator.applyRootMotion = false; // código move; anim só toca no lugar
            _current = Idle;
        }

        private void Update()
        {
            if (_animator == null) return;

            MeasurePlanarSpeed();
            TrackHit();

            int target = PickState();
            if (target != _current)
            {
                _current = target;
                _animator.CrossFadeInFixedTime(target, _crossFade);
            }
        }

        // Velocidade horizontal REAL do boneco (usada no fallback remoto). Suavizada p/ não
        // "piscar" com o jitter da interpolação de rede.
        private void MeasurePlanarSpeed()
        {
            Vector3 p = transform.position; p.y = 0f;
            float dt = Time.deltaTime;
            if (_hasLastPos && dt > 0f)
            {
                float v = (p - _lastPlanarPos).magnitude / dt;
                float t = Mathf.Clamp01(v / MaxPlanarSpeed);
                _measuredSpeed01 = Mathf.Lerp(_measuredSpeed01, t, 0.35f);
            }
            _lastPlanarPos = p;
            _hasLastPos = true;
        }

        // Levou dano? (a vida caiu desde o último frame) → flinch por um instante. Vale no dono
        // E na réplica do cliente (a vida chega replicada pelo NetworkHealth).
        private void TrackHit()
        {
            if (_health == null) return;
            float hp = _health.Current;
            if (_lastHp >= 0f && hp < _lastHp - 0.01f && !_health.IsDead) _hitUntil = Time.time + _hitFlinch;
            _lastHp = hp;
        }

        // Prioridade: morrer > golpear > bloquear > levar dano > deslizar > no ar > agachar > locomoção.
        private int PickState()
        {
            // COMBATE primeiro. Morte/flinch valem também na réplica (vida replicada); o golpe
            // só no dono (o MeleeAttack do player alheio fica desligado — igual ao telegraph).
            if (_health != null && _health.IsDead) return Death;
            if (_melee != null && _melee.IsAttacking) return _melee.ComboIndex == 0 ? Attack1 : Attack2;
            // Guarda ACIMA do flinch: bloqueando, o herói aguenta o golpe atrás do escudo em vez
            // de cambalear (o dano que vaza é chip damage, não deve quebrar a pose).
            if (_blocker != null && _blocker.IsBlocking) return Block;
            if (Time.time < _hitUntil) return Hit;

            // Boneco LOCAL (dono): estado RICO do controller (agachar/deslizar/pular/cair).
            if (_controller != null && _controller.isActiveAndEnabled)
            {
                if (_controller.IsSliding) return Slide;
                if (!_controller.IsGrounded) return _controller.VerticalVelocity > 0.5f ? Jump : Fall;
                if (_controller.IsCrouching) return Crouch;

                float s = _controller.LocomotionSpeed01;
                if (s >= RunAt) return Run;
                if (s >= WalkAt) return Walk;
                return Idle;
            }

            // Boneco REMOTO (co-op): sem controller ativo → anda/corre pela velocidade medida.
            float m = _measuredSpeed01;
            if (m >= RunAt) return Run;
            if (m >= WalkAt) return Walk;
            return Idle;
        }
    }
}

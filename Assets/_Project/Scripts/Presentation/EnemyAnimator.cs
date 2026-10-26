using UnityEngine;
using JogoDaTrupe.Gameplay.Enemies;

namespace JogoDaTrupe.Presentation
{
    /// <summary>
    /// Dá VIDA ao mesh de um MOB (espelha o <see cref="PlayerAnimator"/>): MEDE a velocidade
    /// real do transform — funciona no host que simula a IA E na réplica do cliente movida
    /// pelo NetworkTransform — e lê ataque/morte do <see cref="EnemyController"/>. Toca por
    /// CrossFade, sem root motion (quem move é o controller/rede; a anim só dá vida no lugar).
    ///
    /// Fica no ROOT do inimigo; o Animator está no mesh-filho (CreatureVisual). Estados
    /// esperados no controller: Idle/Walk/Run/Attack/Death (ver EnemySetup).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private EnemyController _controller;
        [SerializeField] private float _crossFade = 0.12f;

        // Limiares sobre a velocidade medida (0..1). Mobs perseguem em velocidade cheia →
        // "correr" cobre a maior parte do movimento; "andar" pega as aproximações lentas.
        private const float WalkAt = 0.10f;
        private const float RunAt = 0.55f;
        private const float MaxPlanarSpeed = 5f; // ~ maior moveSpeed dos mobs (evasivo ~4.6)

        private int _current;
        private Vector3 _lastPlanarPos;
        private float _measuredSpeed01;
        private bool _hasLastPos;

        private static readonly int Idle = Animator.StringToHash("Idle");
        private static readonly int Walk = Animator.StringToHash("Walk");
        private static readonly int Run = Animator.StringToHash("Run");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Death = Animator.StringToHash("Death");

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_controller == null) _controller = GetComponent<EnemyController>();
            if (_animator != null) _animator.applyRootMotion = false;
            _current = Idle;
        }

        private void Update()
        {
            if (_animator == null) return;

            MeasurePlanarSpeed();

            int target = PickState();
            if (target != _current)
            {
                _current = target;
                _animator.CrossFadeInFixedTime(target, _crossFade);
            }
        }

        // Velocidade horizontal REAL (host e cliente). Suavizada p/ não "piscar" com o
        // jitter da interpolação de rede.
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

        // Prioridade: morte > ataque (telegraph/windup) > locomoção medida.
        private int PickState()
        {
            if (_controller != null)
            {
                if (_controller.IsDead) return Death;
                if (_controller.IsAttacking) return Attack;
            }

            float m = _measuredSpeed01;
            if (m >= RunAt) return Run;
            if (m >= WalkAt) return Walk;
            return Idle;
        }
    }
}

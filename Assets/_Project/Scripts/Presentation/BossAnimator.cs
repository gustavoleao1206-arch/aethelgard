using UnityEngine;
using JogoDaTrupe.Gameplay.Boss;

namespace JogoDaTrupe.Presentation
{
    /// <summary>
    /// Dá VIDA ao mesh do BOSS (dragão VOADOR, rig Generic): MEDE a velocidade real do transform
    /// (host que simula E réplica do cliente) e alterna Idle/Walk por CrossFade, sem root motion.
    /// O dragão é voador (escolha do Diretor) → "Idle"=FlyIdle (pairar), "Walk"=FlyForward (voar),
    /// "Attack"=FlyAttack no MERGULHO (lê BossController.IsAttacking) — ver EnemySetup.
    ///
    /// Falta ainda o VFX de fogo no ataque (abrir a boca cuspindo fogo) — Backlog "combate do
    /// boss". Fica no ROOT do boss; o Animator está no mesh-filho (CreatureVisual).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private BossController _controller;
        [SerializeField] private float _crossFade = 0.15f;

        private const float WalkAt = 0.08f;
        private const float MaxPlanarSpeed = 4f; // ~ moveSpeed do boss

        private int _current;
        private Vector3 _lastPlanarPos;
        private float _measuredSpeed01;
        private bool _hasLastPos;

        private static readonly int Idle = Animator.StringToHash("Idle");
        private static readonly int Walk = Animator.StringToHash("Walk");
        private static readonly int Attack = Animator.StringToHash("Attack");

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_controller == null) _controller = GetComponent<BossController>();
            if (_animator != null) _animator.applyRootMotion = false;
            _current = Idle;
        }

        private void Update()
        {
            if (_animator == null) return;

            Vector3 p = transform.position; p.y = 0f;
            float dt = Time.deltaTime;
            if (_hasLastPos && dt > 0f)
            {
                float v = (p - _lastPlanarPos).magnitude / dt;
                _measuredSpeed01 = Mathf.Lerp(_measuredSpeed01, Mathf.Clamp01(v / MaxPlanarSpeed), 0.3f);
            }
            _lastPlanarPos = p;
            _hasLastPos = true;

            // Ataque (mergulho) tem prioridade; senão paira/voa pela velocidade medida.
            int target = (_controller != null && _controller.IsAttacking) ? Attack
                       : (_measuredSpeed01 >= WalkAt ? Walk : Idle);
            if (target != _current)
            {
                _current = target;
                _animator.CrossFadeInFixedTime(target, _crossFade);
            }
        }
    }
}

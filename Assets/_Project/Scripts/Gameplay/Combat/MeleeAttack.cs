using UnityEngine;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Input;
using JogoDaTrupe.Core.Timing;

namespace JogoDaTrupe.Gameplay.Combat
{
    /// <summary>
    /// Ataque corpo-a-corpo do jogador. Ao apertar atacar, atinge tudo que for
    /// IDamageable numa esfera logo à frente do personagem (perdoa mira imperfeita),
    /// respeitando um tempo de recarga.
    ///
    /// Lê a INTENÇÃO de atacar (não a tecla direto) via PlayerInputReader — mesmo
    /// padrão multiplayer-ready do resto do Player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeAttack : MonoBehaviour
    {
        [Header("Referências (auto se vazias)")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _origin;

        [Header("Ataque")]
        [SerializeField] private float _range = 2.2f;
        [SerializeField] private float _radius = 1f;
        [SerializeField] private float _damage = 34f;
        [SerializeField] private float _cooldown = 0.4f;
        [SerializeField] private LayerMask _hittableLayers = ~0;

        [Tooltip("Por quanto tempo o golpe conta como 'em andamento' — é a janela em que o PlayerAnimator toca a animação de ataque.")]
        [SerializeField] private float _swingTime = 0.45f;

        private float _lastAttackTime = -999f;
        private int _combo = 1; // alterna 0↔1 no golpe; começa em 1 pra o PRIMEIRO golpe ser o 01
        private readonly Collider[] _buffer = new Collider[16];
        private Ownable _ownable;

        // Estado exposto para a camada de ANIMAÇÃO (PlayerAnimator).
        /// <summary>Golpe em andamento → dispara a animação de ataque.</summary>
        public bool IsAttacking => GameTime.Time - _lastAttackTime < _swingTime;
        /// <summary>Alterna a cada golpe: 0 = NormalAttack01, 1 = NormalAttack02.</summary>
        public int ComboIndex => _combo;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<PlayerInputReader>();
            _ownable = GetComponent<Ownable>();
        }

        private void Update()
        {
            if (_input != null && _input.AttackPressedThisFrame)
            {
                TryAttack();
            }
        }

        private void TryAttack()
        {
            if (GameTime.Time - _lastAttackTime < _cooldown) return;
            _lastAttackTime = GameTime.Time;
            _combo ^= 1; // alterna o golpe (01 ↔ 02)

            // Avisa o mundo que o jogador atacou (a IA dos inimigos usa isso).
            int ownerId = _ownable != null ? _ownable.OwnerId : Ownable.LocalPlayerId;
            EventBus<PlayerAttacked>.Publish(new PlayerAttacked { OwnerId = ownerId });

            Transform o = _origin != null ? _origin : transform;
            Vector3 center = o.position + o.forward * (_range * 0.5f) + Vector3.up * 0.8f;

            int count = Physics.OverlapSphereNonAlloc(center, _radius, _buffer, _hittableLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = _buffer[i];
                if (c == null) continue;
                if (c.transform.root == transform.root) continue; // não bate em si mesmo

                if (c.TryGetComponent(out IDamageable target) && !target.IsDead)
                {
                    target.TakeDamage(_damage, ownerId); // ownerId já computado acima (linha do PlayerAttacked)
                }
            }
        }
    }
}

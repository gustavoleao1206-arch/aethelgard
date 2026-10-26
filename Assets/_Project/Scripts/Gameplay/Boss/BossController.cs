using UnityEngine;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Combat;
using JogoDaTrupe.Core.Timing;

namespace JogoDaTrupe.Gameplay.Boss
{
    /// <summary>
    /// Boss do Vertical Slice: o "Eco Corrompido". Persegue e ataca o jogador, mas
    /// ADAPTA de duas formas:
    /// 1) <b>Fases por vida</b> — ao cruzar limiares de vida, fica mais rápido e forte
    ///    (BossMath.StatMultiplier) e mais corrompido (visual);
    /// 2) <b>Resiste ao seu estilo</b> — conta qual elemento o jogador mais repete
    ///    (eventos AbilityUsed) e passa a tomar menos dano daquele elemento
    ///    (IElementResistant), forçando o jogador a variar a build.
    ///
    /// Reusa Health/CombatMath; a lógica adaptativa pura vive em BossMath (testada).
    /// NÃO é IRootable de propósito: não pode ser preso pelo Cipó (é um boss).
    /// É um controlador próprio (não o EnemyController) porque precisa de stats que
    /// mudam em runtime (fases) e da resistência elemental.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Collider))]
    public sealed class BossController : MonoBehaviour, IElementResistant
    {
        [SerializeField] private EnemyDefinition _definition;
        [SerializeField] private float _despawnDelay = 4f;
        [SerializeField] private float _turnSpeed = 360f;

        [Header("Fases (frações de vida, ordem decrescente)")]
        [SerializeField] private float[] _phaseThresholds = { 0.66f, 0.33f };
        [SerializeField] private float _statBonusPerPhase = 0.25f; // +25% vel./dano por fase

        [Header("Adaptação de elemento")]
        [SerializeField] private int _minUsesToResist = 3;
        [SerializeField] private float _resistedMultiplier = 0.4f; // toma 40% do dano do elemento repetido
        [Tooltip("A cada quantos segundos o uso de elementos 'esfria', para a resistência seguir o estilo recente.")]
        [SerializeField] private float _decaySeconds = 2.5f;

        private Health _health;
        private readonly PlayerTargeting _targeting = new PlayerTargeting(); // alvo = jogador vivo mais perto (M-NET-6 P2)
        private Renderer _renderer;
        private Color _baseColor = new Color(0.5f, 0.15f, 0.45f);

        [Tooltip("A que distância da arena (spawn) o boss desengata e volta. Guardião de arena: não persegue o jogador para fora nem invade o hub.")]
        [SerializeField] private float _leashRadius = 12f;
        private Vector3 _home; // centro da arena que o boss guarda

        [Header("Voo (dragão): PAIRA fora do alcance e MERGULHA pra atacar")]
        [Tooltip("Altura de voo acima do chão da arena. Pairando aqui, o melee NÃO alcança — o counter é ataque à distância (Eco/arco); pra dar dano no melee, espere o mergulho.")]
        [SerializeField] private float _hoverHeight = 3f;
        [Tooltip("Velocidade de subir/descer (o mergulho e a subida).")]
        [SerializeField] private float _verticalSpeed = 12f;
        [Tooltip("Quanto tempo o dragão SEGURA no chão após golpear (a janela de melee do jogador antes de ele subir).")]
        [SerializeField] private float _attackDwell = 0.9f;
        private float _groundY;         // Y do chão da arena (destino do mergulho)
        private bool _attacking;        // true durante o MERGULHO de ataque (desce → golpeia → segura → sobe)
        private float _attackHoldUntil = -1f; // enquanto >0 e Time<isto: segura no chão (janela de melee)

        private int[] _elementUses;
        private int _resistedElement = -1;
        private int _phase;
        private float _lastAttackTime = -999f;
        private float _nextDecay;
        private bool _deathHandled;

        public float HealthFraction => (_health != null && _health.Max > 0f) ? _health.Current / _health.Max : 0f;
        public int Phase => _phase;
        public string DisplayName => _definition != null ? _definition.displayName : "Eco Corrompido";
        public bool HasResistedElement => _resistedElement >= 0;
        public ElementType ResistedElement => (ElementType)Mathf.Max(0, _resistedElement);
        /// <summary>Em MERGULHO de ataque (desce voando pra golpear) → dispara a anim de ataque.</summary>
        public bool IsAttacking => _attacking;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _renderer = GetComponentInChildren<Renderer>();
            _elementUses = new int[System.Enum.GetValues(typeof(ElementType)).Length];
            if (_definition != null) _baseColor = _definition.color;
            _home = transform.position; // centro da arena (Instantiate/cena posicionou antes do Awake)
            _groundY = transform.position.y; // nível do chão da arena (o mergulho desce até aqui)
        }

        private void OnEnable() => EventBus<AbilityUsed>.Subscribe(OnAbilityUsed);
        private void OnDisable() => EventBus<AbilityUsed>.Unsubscribe(OnAbilityUsed);

        // Conta o elemento usado e recalcula a que o boss resiste.
        private void OnAbilityUsed(AbilityUsed e)
        {
            if (e.Ability == null) return;
            int idx = (int)e.Ability.element;
            if (idx >= 0 && idx < _elementUses.Length) _elementUses[idx]++;
            _resistedElement = BossMath.MostUsedElement(_elementUses, _minUsesToResist);
        }

        // Consultado pelo AbilityRunner ao aplicar dano elemental.
        public float DamageMultiplierFor(ElementType element)
        {
            return (_resistedElement >= 0 && (int)element == _resistedElement) ? _resistedMultiplier : 1f;
        }

        private void Update()
        {
            // Só a máquina-autoridade simula a IA (solo: sempre; rede: só o host —
            // no cliente a réplica é movida pelo NetworkTransform). M-NET-6 P0.
            if (!SimulationAuthority.IsAuthority) return;

            if (_health.IsDead) { HandleDeath(); return; }

            // Reavalia o alvo (jogador vivo MAIS PERTO, com intervalo — M-NET-6 P2).
            // A RESISTÊNCIA elemental segue global (estilo do GRUPO — design aprovado).
            _targeting.Refresh(transform.position);

            // GUARDIÃO DE ARENA: só engaja quem ENTRA na arena (detect a partir da CASA)
            // e não estica além do leash — não persegue o jogador para fora nem invade o
            // hub. Sem ninguém na arena, volta calmamente ao centro.
            float detect = _definition != null ? _definition.detectRange : 22f;
            bool engage = _targeting.Target != null
                && ZoneGuard.ShouldEngage(_home, transform.position, _targeting.Target.position, detect, _leashRadius);
            if (!engage)
            {
                ReturnHome();
                return;
            }

            float distance = PlanarDistanceToPlayer();

            UpdatePhase();
            DecayElementUses();

            float speed = _definition != null ? _definition.moveSpeed : 3.5f;
            float mult = BossMath.StatMultiplier(_phase, _statBonusPerPhase);
            float attackRange = _definition != null ? _definition.attackRange : 2.6f;
            float cooldown = _definition != null ? _definition.attackCooldown : 1.3f;

            Vector3 dir = PlayerDirection();
            FaceDirection(dir);

            // VOO: paira ALTO (fora do melee) enquanto se aproxima; MERGULHA até o chão pra
            // golpear e volta a SUBIR. Dá janela de melee só no mergulho; pairando, o counter
            // é ataque à distância (design do Diretor).
            MoveVertical(_attacking ? _groundY : _groundY + _hoverHeight);

            if (distance > attackRange)
                transform.position += dir * (speed * mult * GameTime.DeltaTime); // fecha no jogador em XZ

            if (_attacking)
            {
                // Chegou ao chão: golpeia 1× e SEGURA no chão (_attackDwell) — a janela de melee
                // do jogador —, depois SOBE de volta pro voo.
                if (transform.position.y <= _groundY + 0.3f)
                {
                    if (_attackHoldUntil < 0f)
                    {
                        if (distance <= attackRange * 1.5f) Strike(mult);
                        _attackHoldUntil = GameTime.Time + _attackDwell;
                    }
                    else if (GameTime.Time >= _attackHoldUntil)
                    {
                        _attacking = false;
                        _attackHoldUntil = -1f;
                        _lastAttackTime = GameTime.Time; // recarga conta do FIM do mergulho
                    }
                }
            }
            else if (distance <= attackRange && GameTime.Time - _lastAttackTime >= cooldown)
            {
                _attacking = true; // pairando no alcance e recarregado → inicia o mergulho
            }
        }

        // Sobe/desce o corpo (voo) suavemente até o Y alvo.
        private void MoveVertical(float targetY)
        {
            Vector3 p = transform.position;
            p.y = Mathf.MoveTowards(p.y, targetY, _verticalSpeed * GameTime.DeltaTime);
            transform.position = p;
        }

        private void UpdatePhase()
        {
            int p = BossMath.PhaseForHealth(HealthFraction, _phaseThresholds);
            if (p == _phase) return;

            _phase = p;
            // Feedback: mais corrompido (mais vermelho) a cada fase.
            if (_renderer != null)
                _renderer.material.color = Color.Lerp(_baseColor, new Color(0.9f, 0.1f, 0.1f), Mathf.Clamp01(_phase * 0.4f));
        }

        // Esfria as contagens de elemento periodicamente, para a resistência seguir o
        // estilo RECENTE (parar de usar um elemento faz a resistência migrar).
        private void DecayElementUses()
        {
            if (_decaySeconds <= 0f || GameTime.Time < _nextDecay) return;
            _nextDecay = GameTime.Time + _decaySeconds;

            for (int i = 0; i < _elementUses.Length; i++)
                if (_elementUses[i] > 0) _elementUses[i]--;

            _resistedElement = BossMath.MostUsedElement(_elementUses, _minUsesToResist);
        }

        // Desfere o golpe do mergulho (a recarga e o timing vivem no Update / máquina do voo).
        private void Strike(float mult)
        {
            IDamageable target = _targeting.TargetDamageable;
            if (target != null && !target.IsDead)
            {
                float dmg = (_definition != null ? _definition.damage : 16f) * mult;
                // O boss não é jogador → NoOwner (não vira contribuinte de Eco).
                target.TakeDamage(dmg, Ownable.NoOwner);
            }
        }

        private void FaceDirection(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * GameTime.DeltaTime);
        }

        private Vector3 PlayerDirection()
        {
            Vector3 dir = _targeting.Target.position - transform.position;
            dir.y = 0f;
            return dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
        }

        private float PlanarDistanceToPlayer() => PlanarDistance(transform.position, _targeting.Target.position);

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // Guardião de arena: sem jogador na arena, volta calmamente ao centro.
        private void ReturnHome()
        {
            _attacking = false;
            _attackHoldUntil = -1f;
            MoveVertical(_groundY + _hoverHeight); // desengajou: volta a PAIRAR na arena
            Vector3 toHome = _home - transform.position;
            toHome.y = 0f;
            if (toHome.sqrMagnitude < 0.16f) return; // já no centro
            float speed = _definition != null ? _definition.moveSpeed : 3.5f;
            Vector3 dir = toHome.normalized;
            transform.position += dir * (speed * 0.8f * GameTime.DeltaTime);
            FaceDirection(dir);
        }

        private void HandleDeath()
        {
            if (_deathHandled) return;
            _deathHandled = true;
            if (_renderer != null) _renderer.material.color = new Color(0.2f, 0.2f, 0.2f);
            Destroy(gameObject, _despawnDelay); // o EcoDropper publica o Eco ao morrer
        }
    }
}

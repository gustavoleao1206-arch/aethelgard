using System.Collections.Generic;
using UnityEngine;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Combat;
using JogoDaTrupe.Core.Timing;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Gameplay.Enemies
{
    /// <summary>
    /// Inimigo com IA adaptativa simples. Lê o jogador (distância e ritmo de
    /// ataque) e troca de POSTURA (agressivo / defensivo / esquivo) durante a luta,
    /// usando o <see cref="AdaptiveBrain"/>. Cada inimigo tem um "truque" próprio
    /// (ver <see cref="EnemyDefinition"/>) que tempera o movimento.
    ///
    /// Os atributos vêm de uma EnemyDefinition (ScriptableObject) — Definição
    /// separada do Estado. Movimento no plano; a DIREÇÃO de perseguição/volta pra casa vem do
    /// <see cref="EnemyNavigator"/> (NavMesh), então o inimigo contorna colunas, arcos e
    /// árvores em vez de esbarrar neles. Sem NavMesh, tudo cai na linha reta de antes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Collider))]
    public sealed class EnemyController : MonoBehaviour, IRootable
    {
        [SerializeField] private EnemyDefinition _definition;
        [SerializeField] private float _despawnDelay = 3f;
        [SerializeField] private float _turnSpeed = 540f;

        [Tooltip("Altura do pivô acima dos pés (meia-altura da cápsula de 2 m) — cola o inimigo no relevo.")]
        [SerializeField] private float _groundOffset = 1f;

        [Tooltip("Por quanto tempo o inimigo mantém uma postura antes de reavaliar (evita oscilar e deixa a mudança visível).")]
        [SerializeField] private float _stanceHold = 0.7f;

        private Health _health;
        private EnemyNavigator _navigator; // opcional: dá a direção que contorna obstáculos (NavMesh)
        private readonly PlayerTargeting _targeting = new PlayerTargeting(); // alvo = jogador vivo mais perto (M-NET-6 P2)
        private Renderer _renderer;
        private Color _baseColor = Color.white;

        private readonly Queue<float> _recentAttacks = new Queue<float>();
        private const float AttackWindow = 3f; // segundos que um ataque "conta" para o ritmo

        private EnemyStance _stance = EnemyStance.Aggressive;
        private float _stanceUntil;
        private float _lastAttackTime = -999f;
        private float _windupEndsAt = -1f; // >= 0 enquanto prepara o golpe (telegraph)
        private float _zigzagPhase;
        private bool _mimicQueued;
        private bool _deathHandled;
        private float _rootedUntil = -999f; // preso pelo Cipó até este instante

        [Tooltip("A que distância da CASA (spawn) o inimigo desengata e volta. Guardião de zona: não persegue o jogador para fora da clareira nem invade o hub.")]
        [SerializeField] private float _leashRadius = 10f;
        private Vector3 _home; // posição de spawn = centro da zona que o inimigo guarda

        private void Awake()
        {
            _health = GetComponent<Health>();
            _navigator = GetComponent<EnemyNavigator>();
            _renderer = GetComponentInChildren<Renderer>();
            if (_definition != null) _baseColor = _definition.color;
            _home = transform.position; // Instantiate/cena já posicionou antes do Awake
        }

        private void OnEnable()
        {
            EventBus<PlayerAttacked>.Subscribe(OnPlayerAttacked);
        }

        private void OnDisable()
        {
            EventBus<PlayerAttacked>.Unsubscribe(OnPlayerAttacked);
        }

        private void OnPlayerAttacked(PlayerAttacked e)
        {
            // Ritmo/imitação contam SÓ o ALVO atual (M-NET-6 P2): com 2 jogadores, o
            // inimigo não pode "sentir" o ritmo somado dos dois (puniria quem não atacou).
            if (e.OwnerId != _targeting.TargetOwnerId) return;

            // Registra o ataque (para medir o ritmo) e arma uma possível imitação/contra-ataque.
            _recentAttacks.Enqueue(GameTime.Time);
            _mimicQueued = true;
        }

        // Preso pelo Cipó (IRootable): fica imóvel por alguns segundos (estende se já preso).
        public void ApplyRoot(float seconds)
        {
            if (seconds <= 0f) return;
            float until = GameTime.Time + seconds;
            if (until > _rootedUntil) _rootedUntil = until;
        }

        // Estado exposto p/ a camada de ANIMAÇÃO (EnemyAnimator). A LOCOMOÇÃO o EnemyAnimator
        // MEDE pelo transform (funciona no host que simula E na réplica do cliente movida pelo
        // NetworkTransform); aqui só expomos o que ele não consegue medir. Como o windup só
        // roda na autoridade (host), o ataque anima no host — igual ao clarão do telegraph.
        /// <summary>Preparando/desferindo o golpe (telegraph ativo) → dispara a anim de ataque.</summary>
        public bool IsAttacking => _windupEndsAt >= 0f;
        /// <summary>Morto → dispara a anim de morte.</summary>
        public bool IsDead => _health != null && _health.IsDead;

        private void Update()
        {
            // Só a máquina-autoridade simula a IA (solo: sempre; rede: só o host —
            // no cliente a réplica é movida pelo NetworkTransform). M-NET-6 P0.
            if (!SimulationAuthority.IsAuthority) return;

            if (_definition == null) return;

            if (_health.IsDead)
            {
                HandleDeath();
                return;
            }

            // Reavalia o alvo (jogador vivo MAIS PERTO, com intervalo — M-NET-6 P2).
            // Cobre também o spawn tardio da cena de rede (Bug #9) e soltar alvo morto.
            _targeting.Refresh(transform.position);

            // GUARDIÃO DE ZONA (fix da "morte instantânea" no co-op): o inimigo só engaja
            // quem entra na sua CLAREIRA (detectRange medido a partir da CASA, não da
            // posição atual) e não estica além do leash. Assim ele não persegue o jogador
            // até o hub nem mata quem acabou de nascer lá — igual ao boss, ocioso até
            // alguém entrar na arena. Sem ninguém na zona, volta calmamente para a casa.
            bool engage = _targeting.Target != null
                && ZoneGuard.ShouldEngage(_home, transform.position, _targeting.Target.position, _definition.detectRange, _leashRadius);
            if (!engage)
            {
                if (_windupEndsAt >= 0f) { ShowTelegraph(false); _windupEndsAt = -1f; } // cancela golpe pendente
                ReturnHome();
                return;
            }

            // Preso pelo Cipó: não se move nem ataca enquanto durar a raiz.
            if (GameTime.Time < _rootedUntil) return;

            TrimOldAttacks();

            float distance = PlanarDistanceToPlayer();

            // TELEGRAPH (peso/reação): enquanto prepara o golpe, fica PARADO encarando; ao fim
            // resolve o dano SÓ se o alvo ainda estiver no alcance (recuar no windup = errar).
            if (_windupEndsAt >= 0f)
            {
                FaceDirection(PlayerDirection());
                if (GameTime.Time >= _windupEndsAt) { ResolveStrike(distance); _windupEndsAt = -1f; }
                return;
            }

            // Reavalia a postura periodicamente (compromisso), para não oscilar a cada
            // frame e para a mudança ser visível.
            if (GameTime.Time >= _stanceUntil)
            {
                _stance = AdaptiveBrain.DecideStance(
                    distance, _definition.attackRange, _recentAttacks.Count, _definition.trick);
                _stanceUntil = GameTime.Time + _stanceHold;
                ApplyStanceColor();
            }

            switch (_stance)
            {
                case EnemyStance.Aggressive: ActAggressive(distance); break;
                case EnemyStance.Defensive: ActDefensive(distance); break;
                case EnemyStance.Evasive: ActEvasive(); break;
            }

            // Imitação: se o jogador acabou de atacar e estamos no alcance, contra-ataca uma vez.
            if (_mimicQueued && distance <= _definition.attackRange)
            {
                TryStartWindup();
                _mimicQueued = false;
            }
        }

        // ---- Posturas ----

        private void ActAggressive(float distance)
        {
            if (CanReach(distance))
            {
                FaceDirection(PlayerDirection());
                TryStartWindup();
            }
            else
            {
                Move(ChaseDirection(), _definition.moveSpeed);
            }
        }

        private void ActDefensive(float distance)
        {
            float desired = _definition.attackRange * 0.9f;

            if (distance < desired)
            {
                Move(-PlayerDirection(), _definition.moveSpeed * 0.8f); // recua
            }
            else if (CanReach(distance))
            {
                FaceDirection(PlayerDirection());
                TryStartWindup();
            }
            else
            {
                Move(ChaseDirection(), _definition.moveSpeed * 0.6f);
            }
        }

        private void ActEvasive()
        {
            // Recua decididamente (mais "para trás" do que de lado) e um pouco mais rápido.
            Vector3 away = -PlayerDirection();
            Vector3 strafe = Vector3.Cross(Vector3.up, PlayerDirection());
            Vector3 dir = (away * 1.6f + strafe).normalized;
            Move(dir, _definition.moveSpeed * 1.15f);
        }

        // Indicador visual da postura (ajuda a "ver" a IA pensando):
        // ciano = esquivando, amarelo = defensivo, cor base = agressivo.
        private void ApplyStanceColor()
        {
            if (_renderer == null) return;

            Color c = _baseColor;
            if (_stance == EnemyStance.Evasive) c = Color.Lerp(_baseColor, Color.cyan, 0.6f);
            else if (_stance == EnemyStance.Defensive) c = Color.Lerp(_baseColor, Color.yellow, 0.5f);

            _renderer.material.color = c;
        }

        // ---- Movimento / ataque ----

        private void Move(Vector3 dir, float speed)
        {
            // Truque Zigzag: oscila lateralmente ao se mover.
            if (_definition.trick == EnemyTrick.Zigzag)
            {
                _zigzagPhase += GameTime.DeltaTime * 6f;
                Vector3 side = Vector3.Cross(Vector3.up, dir) * (Mathf.Sin(_zigzagPhase) * 0.6f);
                dir = (dir + side).normalized;
            }

            Vector3 desired = transform.position + dir * (speed * GameTime.DeltaTime);

            // O inimigo não tem física: sem prender o passo à malha, ele atravessaria troncos,
            // colunas e arcos. O navegador para o passo na borda da malha.
            Vector3 next = _navigator != null ? _navigator.ClampStep(desired) : desired;
            // Relevo (Milestone do Terreno): os pés seguem o terreno pela MESMA função que gera a
            // malha do chão — antes o chão era plano e o Y nunca mudava; num morro o mob flutuaria.
            next.y = WorldHeight.SampleHeight(next.x, next.z) + _groundOffset;
            transform.position = next;
            FaceDirection(dir);
        }

        // TELEGRAPH em 2 tempos: começa o "preparar golpe" (sem dano ainda). O dano só sai no
        // fim do windup (ResolveStrike) e SÓ se o alvo ainda estiver no alcance — dá pra reagir/
        // recuar. É a base p/ futuros tempos de conjuração das habilidades das criaturas.
        private void TryStartWindup()
        {
            if (GameTime.Time - _lastAttackTime < _definition.attackCooldown) return;
            if (_windupEndsAt >= 0f) return;
            _lastAttackTime = GameTime.Time; // a recarga conta desde o INÍCIO do golpe
            _windupEndsAt = GameTime.Time + Mathf.Max(0f, _definition.attackWindup);
            ShowTelegraph(true);
        }

        private void ResolveStrike(float distance)
        {
            ShowTelegraph(false);
            // Saiu do alcance OU se escondeu atrás de uma coluna durante o windup → errou o golpe.
            if (!CanReach(distance)) return;

            IDamageable target = _targeting.TargetDamageable;
            if (target != null && !target.IsDead)
            {
                // O inimigo não é jogador → NoOwner (não vira contribuinte de Eco).
                target.TakeDamage(_definition.damage, Ownable.NoOwner);
            }
        }

        // Clarão de "vou bater" durante o telegraph. Sem mesh/anim ainda, a COR é a leitura;
        // quando os mobs tiverem modelo, o windup passa a disparar a animação de ataque.
        private void ShowTelegraph(bool on)
        {
            if (_renderer == null) return;
            if (on) _renderer.material.color = Color.Lerp(_baseColor, Color.white, 0.75f);
            else ApplyStanceColor();
        }

        private void FaceDirection(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * GameTime.DeltaTime);
        }

        // Guardião de zona: sem jogador na clareira (ou esticado além do leash), volta
        // calmamente para a casa e "esfria" a postura ao chegar. Não invade o hub.
        private void ReturnHome()
        {
            Vector3 toHome = _home - transform.position;
            toHome.y = 0f;
            if (toHome.magnitude > 0.4f)
            {
                Move(NavigateTo(_home, toHome.normalized), _definition.moveSpeed * 0.8f);
            }
            else if (_stance != EnemyStance.Aggressive)
            {
                _stance = EnemyStance.Aggressive; // em casa: reseta a cor base (fim do combate)
                ApplyStanceColor();
            }
        }

        // ---- Auxiliares ----

        // Direção CRUA até o jogador (linha reta). Serve pra encarar, mirar o golpe e recuar —
        // gestos curtos, em que rotear não faz sentido.
        private Vector3 PlayerDirection()
        {
            Vector3 dir = _targeting.Target.position - transform.position;
            dir.y = 0f;
            return dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
        }

        // Direção de PERSEGUIÇÃO: contorna colunas, arcos e árvores (NavMesh). Sem malha, é a
        // linha reta de antes.
        private Vector3 ChaseDirection() => NavigateTo(_targeting.Target.position, PlayerDirection());

        private Vector3 NavigateTo(Vector3 destination, Vector3 straightLine)
            => _navigator != null ? _navigator.DirectionTo(destination, straightLine) : straightLine;

        // Dá pra golpear o jogador daqui? Perto NÃO basta: a distância é medida em linha reta, e
        // com uma coluna no meio o inimigo ficaria batendo NA COLUNA em vez de dar a volta.
        private bool CanReach(float distance) => distance <= _definition.attackRange && !BlockedByWall();

        private bool BlockedByWall()
            => _navigator != null && _targeting.Target != null && _navigator.BlockedTo(_targeting.Target.position);

        private float PlanarDistanceToPlayer() => PlanarDistance(transform.position, _targeting.Target.position);

        // Distância no plano (ignora altura) entre dois pontos.
        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void TrimOldAttacks()
        {
            while (_recentAttacks.Count > 0 && GameTime.Time - _recentAttacks.Peek() > AttackWindow)
            {
                _recentAttacks.Dequeue();
            }
        }

        private void HandleDeath()
        {
            if (_deathHandled) return;
            _deathHandled = true;

            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r != null) r.material.color = new Color(0.25f, 0.25f, 0.25f);
            }

            Destroy(gameObject, _despawnDelay);
        }
    }
}

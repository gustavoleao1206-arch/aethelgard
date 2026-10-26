using UnityEngine;
using JogoDaTrupe.Input;
using JogoDaTrupe.Core.View;

namespace JogoDaTrupe.Gameplay.Characters
{
    /// <summary>
    /// Controla a movimentação do personagem: anda na direção da câmera, gira
    /// para onde anda, aplica gravidade e pula.
    ///
    /// Lê a VONTADE do jogador via PlayerInputReader (não toca em teclas direto)
    /// e aplica o movimento com um CharacterController. Mantém o estado vertical
    /// (velocidade de queda/pulo) internamente.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movimento")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 720f;

        [Header("Pulo / Gravidade")]
        [SerializeField] private float _jumpHeight = 1.2f;
        [SerializeField] private float _gravity = -20f;

        [Header("Correr / Agachar / Deslizar (estilo Palworld)")]
        [SerializeField] private float _runMultiplier = 1.7f;      // sprint = anda × isto
        [SerializeField] private float _crouchMultiplier = 0.5f;   // agachado = anda × isto
        [SerializeField] private float _runStaminaPerSecond = 18f; // dreno de estamina correndo
        [SerializeField] private float _slideSpeed = 14f;          // pico do deslize (impulso claro > sprint)
        [SerializeField] private float _slideDuration = 0.7f;
        [SerializeField] private float _slideStaminaCost = 22f;
        [SerializeField] private float _crouchHeight = 1.1f;       // altura do controller agachado/deslizando

        [Header("Referências (preenchidas automaticamente se vazias)")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private GroundCheck _groundCheck;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private Stamina _stamina;

        private CharacterController _controller;
        private float _verticalVelocity;
        private float _standHeight; // altura normal do controller (lida no Awake)
        private float _slideTimer;  // > 0 enquanto desliza
        private Vector3 _slideDir;  // direção travada do deslize

        // Estado exposto para a camada de ANIMAÇÃO (lido pelo PlayerAnimator; escrito no fim do Update).
        private float _animSpeed01;
        private bool _animGrounded;
        private bool _animCrouching;
        private bool _animSliding;

        /// <summary>Locomoção normalizada p/ animação: 0 parado, ~0.5 andando, 1 correndo.</summary>
        public float LocomotionSpeed01 => _animSpeed01;
        /// <summary>No chão? (alterna idle/andar/correr vs pular/cair.)</summary>
        public bool IsGrounded => _animGrounded;
        /// <summary>Agachado (segurando o crouch, fora do deslize).</summary>
        public bool IsCrouching => _animCrouching;
        /// <summary>Deslizando agora.</summary>
        public bool IsSliding => _animSliding;
        /// <summary>Velocidade vertical atual (sinal indica subindo/caindo).</summary>
        public float VerticalVelocity => _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            // Auto-conexão das referências, para a cena "simplesmente funcionar".
            if (_input == null) _input = GetComponent<PlayerInputReader>();
            if (_groundCheck == null) _groundCheck = GetComponent<GroundCheck>();
            if (_cameraTransform == null && Camera.main != null) _cameraTransform = Camera.main.transform;
            if (_stamina == null) _stamina = GetComponent<Stamina>();
            _standHeight = _controller.height; // altura normal (para restaurar após agachar/deslizar)
        }

        private void Update()
        {
            // 1) Direção desejada, relativa à câmera.
            Vector2 moveInput = _input != null ? _input.MoveAxis : Vector2.zero;
            float cameraYaw = _cameraTransform != null ? _cameraTransform.eulerAngles.y : 0f;
            Vector3 direction = MovementMath.CameraRelativeDirection(moveInput, cameraYaw);
            bool moving = direction.sqrMagnitude > 0.001f;
            bool grounded = _groundCheck != null ? _groundCheck.IsGrounded : _controller.isGrounded;

            // 2) Correr / agachar / deslizar (estilo Palworld).
            bool wantRun = _input != null && _input.RunHeld;
            bool wantCrouch = _input != null && _input.CrouchHeld;
            bool crouchPressed = _input != null && _input.CrouchPressedThisFrame;
            bool sprinting = wantRun && moving && !wantCrouch && (_stamina == null || !_stamina.IsEmpty);
            // "Correndo rápido" p/ disparar o deslize: NÃO exclui o crouch — senão, no frame em
            // que aperta Ctrl, wantCrouch já é true e sprinting vira false → o deslize nunca dispara.
            bool runningFast = wantRun && moving && grounded && (_stamina == null || !_stamina.IsEmpty);

            // Deslize: começa ao AGACHAR enquanto corre no chão (custa estamina, direção travada).
            if (_slideTimer <= 0f && crouchPressed && runningFast &&
                (_stamina == null || _stamina.TrySpend(_slideStaminaCost)))
            {
                _slideTimer = _slideDuration;
                _slideDir = transform.forward;
            }

            float speed = _moveSpeed;
            Vector3 moveDir = direction;
            bool crouchingNow = wantCrouch;

            if (_slideTimer > 0f)
            {
                _slideTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_slideTimer / _slideDuration);
                speed = Mathf.Lerp(_moveSpeed * _crouchMultiplier, _slideSpeed, t); // desacelera até o fim
                moveDir = _slideDir;   // direção travada (não vira no meio do deslize)
                crouchingNow = true;
            }
            else if (sprinting)
            {
                speed = _moveSpeed * _runMultiplier;
                _stamina?.Drain(_runStaminaPerSecond * Time.deltaTime);
                if (_stamina != null && _stamina.IsEmpty) speed = _moveSpeed; // acabou a estamina
            }
            else if (wantCrouch)
            {
                speed = _moveSpeed * _crouchMultiplier;
            }

            // 3) Gira o personagem (nunca durante o deslize — a direção fica travada).
            if (_slideTimer <= 0f)
            {
                if (CameraView.IsFirstPerson && _cameraTransform != null)
                {
                    // 1ª pessoa (Skyrim): o CORPO acompanha a câmera na hora, mesmo parado —
                    // senão a câmera giraria dentro de um corpo estático e você veria o próprio
                    // ombro passando. Só o yaw (olhar pra cima/baixo não deita o personagem).
                    transform.rotation = Quaternion.Euler(0f, cameraYaw, 0f);
                }
                else if (moving)
                {
                    // 3ª pessoa: gira suavemente para a direção do movimento.
                    Quaternion target = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, target, _rotationSpeed * Time.deltaTime);
                }
            }

            // 4) Chão, gravidade e pulo.
            if (grounded && _verticalVelocity < 0f) _verticalVelocity = -2f; // "colado" no chão
            if (grounded && _input != null && _input.JumpPressedThisFrame)
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            _verticalVelocity += _gravity * Time.deltaTime;

            // 5) Altura do CharacterController (agachar/deslizar abaixa; mantém os PÉS no lugar).
            float targetH = crouchingNow ? _crouchHeight : _standHeight;
            if (Mathf.Abs(_controller.height - targetH) > 0.01f)
            {
                _controller.height = Mathf.MoveTowards(_controller.height, targetH, 6f * Time.deltaTime);
                _controller.center = new Vector3(0f, (_controller.height - _standHeight) * 0.5f, 0f);
            }

            // 6) Estado para a ANIMAÇÃO (lido pelo PlayerAnimator, que toca o clipe certo).
            _animGrounded = grounded;
            _animSliding = _slideTimer > 0f;
            _animCrouching = crouchingNow && !_animSliding;
            float planar = moveDir.sqrMagnitude > 0.0001f ? speed : 0f; // velocidade horizontal aplicada
            _animSpeed01 = (_moveSpeed * _runMultiplier) > 0.01f
                ? Mathf.Clamp01(planar / (_moveSpeed * _runMultiplier)) : 0f;

            // 7) Move tudo de uma vez (horizontal + vertical).
            Vector3 velocity = moveDir * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}

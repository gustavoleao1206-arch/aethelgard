using UnityEngine;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Input;
using JogoDaTrupe.Core.Timing;

namespace JogoDaTrupe.Gameplay.Capture
{
    /// <summary>
    /// Captura por SINCRONIZAÇÃO. Quando o jogador mira numa criatura enfraquecida
    /// (<see cref="ISynchronizable"/> pronta) e SEGURA E, um canal enche ao longo de
    /// alguns segundos; ao completar, sincroniza (captura). Soltar antes cancela.
    ///
    /// Lê só a INTENÇÃO do input (segurar E) — multiplayer-ready; quem aplica o
    /// efeito é a criatura (<c>Synchronize</c>), com o dono (Ownable) do jogador.
    /// Faz a própria varredura à frente (como o Interactor), mas para um alvo de
    /// tipo diferente (captura é uma interação contínua, não instantânea).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Ownable))]
    public sealed class Synchronizer : MonoBehaviour
    {
        [Header("Referências (preenchidas automaticamente se vazias)")]
        [SerializeField] private PlayerInputReader _input;
        [Tooltip("De onde a varredura parte. Se vazio, usa este objeto.")]
        [SerializeField] private Transform _rayOrigin;

        [Header("Alcance / canal")]
        [SerializeField] private float _range = 3.5f;
        [SerializeField] private float _thickness = 0.5f;
        [SerializeField] private LayerMask _mask = ~0;
        [Tooltip("Segundos segurando E para completar a sincronização.")]
        [SerializeField] private float _holdSeconds = 1.5f;

        private Ownable _ownable;
        private ISynchronizable _target;
        private float _channel;

        /// <summary>Há uma criatura sincronizável na mira agora?</summary>
        public bool HasTarget => _target != null && _target.CanSynchronize;

        /// <summary>Progresso do canal (0..1) — para a HUD.</summary>
        public float Progress => CaptureMath.ChannelProgress(_channel, _holdSeconds);

        /// <summary>Nome da criatura mirada (para o aviso na tela).</summary>
        public string TargetLabel => _target != null ? _target.Label : "";

        private void Awake()
        {
            _ownable = GetComponent<Ownable>();
            if (_input == null) _input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            _target = FindTarget();

            // Sem alvo válido: zera o canal.
            if (_target == null || !_target.CanSynchronize)
            {
                _channel = 0f;
                return;
            }

            // Segurando E: enche o canal; soltou: cancela.
            if (_input != null && _input.InteractHeld)
            {
                _channel += GameTime.DeltaTime;
                if (_channel >= _holdSeconds)
                {
                    int ownerId = _ownable != null ? _ownable.OwnerId : Ownable.LocalPlayerId;
                    _target.Synchronize(ownerId);
                    _channel = 0f;
                    _target = null;
                }
            }
            else
            {
                _channel = 0f;
            }
        }

        // Varredura à frente procurando uma criatura sincronizável (mesma ideia do
        // Interactor: esfera grossa para perdoar pequenas diferenças de mira).
        private ISynchronizable FindTarget()
        {
            Transform origin = _rayOrigin != null ? _rayOrigin : transform;
            if (Physics.SphereCast(origin.position, _thickness, origin.forward,
                out RaycastHit hit, _range, _mask, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.TryGetComponent(out ISynchronizable s) && s.CanSynchronize) return s;
            }
            return null;
        }
    }
}

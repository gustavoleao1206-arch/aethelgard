using UnityEngine;

namespace JogoDaTrupe.Gameplay.Characters
{
    /// <summary>
    /// Detecta se o personagem está tocando o chão.
    ///
    /// É um componente separado (em vez de embutir no PlayerController) porque
    /// a checagem de chão é útil para vários sistemas (pulo, animação, queda) e
    /// pode ter regras próprias. Mantê-la isolada segue o princípio de
    /// responsabilidade única.
    ///
    /// A checagem parte dos PÉS do personagem (calculados a partir do
    /// CharacterController quando existe), e não do centro — isso garante que
    /// funcione independentemente de onde está o pivô do objeto.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundCheck : MonoBehaviour
    {
        [Tooltip("De onde a checagem parte. Se vazio, os pés são calculados automaticamente.")]
        [SerializeField] private Transform _origin;

        [SerializeField] private float _radius = 0.25f;

        [Tooltip("Folga abaixo dos pés que ainda conta como chão.")]
        [SerializeField] private float _skin = 0.2f;

        [Tooltip("Quais camadas contam como chão. Por padrão, todas.")]
        [SerializeField] private LayerMask _groundMask = ~0;

        /// <summary>Verdadeiro enquanto o personagem está apoiado no chão.</summary>
        public bool IsGrounded { get; private set; }

        private CharacterController _controller;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            // Começa a esfera logo acima dos pés e varre um pouco para baixo.
            // O próprio collider do personagem é ignorado pela SphereCast porque
            // já está em sobreposição no início da varredura.
            Vector3 feet = FeetPosition();
            Vector3 start = feet + Vector3.up * (_radius + 0.05f);
            float distance = _radius + 0.05f + _skin;

            IsGrounded = Physics.SphereCast(
                start,
                _radius,
                Vector3.down,
                out _,
                distance,
                _groundMask,
                QueryTriggerInteraction.Ignore);
        }

        // Posição dos pés: usa o ponto definido, senão calcula a partir do
        // CharacterController, senão usa o próprio transform.
        private Vector3 FeetPosition()
        {
            if (_origin != null) return _origin.position;
            if (_controller != null)
                return transform.position + _controller.center - Vector3.up * (_controller.height * 0.5f);
            return transform.position;
        }
    }
}

using UnityEngine;
using JogoDaTrupe.Input;

namespace JogoDaTrupe.Gameplay.Interaction
{
    /// <summary>
    /// Permite ao jogador interagir com o que estiver à sua frente.
    /// Quando o jogador aperta interagir, faz uma varredura (esfera) para a frente
    /// e, se acertar algo que seja IInteractable, dispara a interação.
    ///
    /// Usa SphereCast (e não um raio fino) para perdoar pequenas diferenças de
    /// altura entre o personagem e o objeto — interação não deve exigir mira exata.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Interactor : MonoBehaviour
    {
        [Header("Referências (preenchidas automaticamente se vazias)")]
        [SerializeField] private PlayerInputReader _input;

        [Tooltip("De onde a varredura parte. Se vazio, usa este objeto.")]
        [SerializeField] private Transform _rayOrigin;

        [Header("Alcance")]
        [SerializeField] private float _range = 3f;
        [SerializeField] private float _thickness = 0.4f;
        [SerializeField] private LayerMask _mask = ~0;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            if (_input != null && _input.InteractPressedThisFrame)
            {
                TryInteract();
            }
        }

        private void TryInteract()
        {
            Transform origin = _rayOrigin != null ? _rayOrigin : transform;
            // Parte da altura do tronco (perto do centro do personagem), para a
            // esfera cobrir objetos baixos e altos à frente.
            Vector3 start = origin.position;

            if (Physics.SphereCast(start, _thickness, origin.forward, out RaycastHit hit, _range, _mask, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.TryGetComponent(out IInteractable interactable))
                {
                    interactable.Interact(gameObject);
                }
            }
        }
    }
}

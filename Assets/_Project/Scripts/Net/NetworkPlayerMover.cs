using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Movimento MÍNIMO de rede (M-NET-3a — proof isolado): só o DONO lê o teclado (WASD)
    /// e move o próprio personagem; o <see cref="ClientNetworkTransform"/> replica a
    /// posição aos demais clientes. Cada cliente controla apenas o SEU boneco (gate IsOwner).
    ///
    /// É um harness de teste para provar spawn + movimento + sincronização de uma vez. O
    /// player REAL (PlayerController + câmera + PlayerInputReader, com gate de dono) é
    /// integrado no M-NET-3b — este mover é descartável.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPlayerMover : NetworkBehaviour
    {
        [SerializeField] private float _speed = 6f;

        private CharacterController _cc;

        private void Awake() => _cc = GetComponent<CharacterController>();

        private void Update()
        {
            if (!IsOwner) return; // cada cliente move só o SEU boneco

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

            Vector3 dir = new Vector3(h, 0f, v);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            // SimpleMove aplica gravidade e já usa o deltaTime internamente.
            _cc.SimpleMove(dir * _speed);
        }
    }
}

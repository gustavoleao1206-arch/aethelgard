using UnityEngine;
using JogoDaTrupe.Gameplay.Boss;

namespace JogoDaTrupe.Presentation.Vfx
{
    /// <summary>
    /// Cospe FOGO pela boca do dragão durante o MERGULHO de ataque (lê
    /// <see cref="BossController.IsAttacking"/>). A origem é ANCORADA no bone de cabeça/mandíbula
    /// do rig (achado por nome no Awake) — assim o fogo sai da BOCA de verdade, seguindo a pose
    /// e a escala, sem depender de offset chutado a partir do root. Fallback por offset do root
    /// se o rig não tiver um bone reconhecível.
    ///
    /// Presentation/local: IsAttacking só é verdadeiro na AUTORIDADE (host/solo), então o fogo
    /// aparece no host — igual à animação de ataque (o combate do boss é server-only).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossFireVfx : MonoBehaviour
    {
        [SerializeField] private BossController _controller;
        [Tooltip("Empurra a origem à FRENTE do bone da cabeça (m) — do pivô do crânio até o focinho.")]
        [SerializeField] private float _snoutForward = 1.5f;
        [Tooltip("Cadência dos jatos enquanto ataca (s).")]
        [SerializeField] private float _interval = 0.12f;

        [Header("Fallback (rig sem bone de cabeça reconhecível): offset do root")]
        [SerializeField] private float _fallbackForward = 4f;
        [SerializeField] private float _fallbackUp = 0.5f;

        private static readonly Color Fire = new Color(1f, 0.4f, 0.08f);
        private Transform _mouth;
        private float _nextFire;

        private void Awake()
        {
            if (_controller == null) _controller = GetComponent<BossController>();
            _mouth = FindMouth();
        }

        // Procura um bone de mandíbula/boca (melhor) ou cabeça no rig do dragão (CreatureVisual).
        private Transform FindMouth()
        {
            Transform head = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("jaw") || n.Contains("mouth")) return t; // mais preciso
                if (head == null && n.Contains("head")) head = t;
            }
            return head;
        }

        private void Update()
        {
            if (_controller == null || !_controller.IsAttacking) return;
            if (Time.time < _nextFire) return;
            _nextFire = Time.time + Mathf.Max(0.03f, _interval);

            Vector3 dir = transform.forward; // o dragão encara o jogador
            Vector3 origin = _mouth != null
                ? _mouth.position + dir * _snoutForward
                : transform.position + dir * _fallbackForward + Vector3.up * _fallbackUp;
            ParticleFactory.FireBreath(origin, dir, Fire);
        }
    }
}

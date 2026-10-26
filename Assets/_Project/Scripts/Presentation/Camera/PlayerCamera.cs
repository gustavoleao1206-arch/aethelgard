using UnityEngine;
using JogoDaTrupe.Input;
using JogoDaTrupe.Core.View;

namespace JogoDaTrupe.Presentation.Camera
{
    /// <summary>
    /// Câmera do jogador com dois modos alternáveis: 3ª pessoa (orbita atrás do
    /// personagem, estilo Palworld) e 1ª pessoa (nos OLHOS, estilo Skyrim).
    ///
    /// O jogador alterna com a tecla dedicada (ver controles); a escolha é salva
    /// nas configurações (PlayerPrefs) e restaurada na próxima vez.
    ///
    /// 1ª PESSOA (estilo Skyrim, decisão do Diretor): o CORPO continua VISÍVEL — dá para
    /// olhar para baixo e ver o próprio tronco e a arma. Só a CABEÇA é escondida (o bone é
    /// encolhido), senão a câmera, que fica dentro do crânio, veria por dentro dele. Se o rig
    /// não tiver bone de cabeça (ex.: cápsula de teste), cai no comportamento antigo de
    /// esconder os renderers do corpo.
    ///
    /// Fica na camada de Apresentação: só posiciona a câmera a partir do alvo e do
    /// olhar; não decide regras de jogo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCamera : MonoBehaviour
    {
        [Header("Alvo (auto: procura a tag 'Player' se vazio)")]
        [SerializeField] private Transform _target;
        [SerializeField] private PlayerInputReader _input;

        [Header("3ª pessoa")]
        [SerializeField] private float _distance = 5f;
        [SerializeField] private float _thirdPersonHeight = 1.6f;

        [Header("1ª pessoa")]
        [Tooltip("Altura dos OLHOS medida do transform do player — que é o CENTRO da cápsula " +
                 "(CharacterController altura 2, centro 0 → pés em root-1). O herói tem ~1,85 m, " +
                 "logo a cabeça fica em root+0,85 e os olhos ≈ root+0,72.")]
        [SerializeField] private float _eyeHeight = 0.72f;
        [Tooltip("Near clip em 1ª pessoa: pequeno pra o próprio corpo/arma não serem cortados de perto.")]
        [SerializeField] private float _firstPersonNearClip = 0.05f;

        [Header("Olhar")]
        [SerializeField] private float _sensitivity = 0.15f;
        [SerializeField] private float _minPitch = -60f;
        [SerializeField] private float _maxPitch = 75f;

        private const string PrefKey = "camera_mode";
        private const float HiddenHeadScale = 0.01f; // cabeça "some" sem desligar o mesh do corpo

        private CameraMode _mode = CameraMode.ThirdPerson;
        private float _yaw;
        private float _pitch = 15f;
        private Renderer[] _bodyRenderers;

        private Transform _head;                    // bone da cabeça (Humanoid) — null = fallback
        private Vector3 _headScale = Vector3.one;   // escala original, restaurada em 3ª pessoa
        private UnityEngine.Camera _cam;
        private float _defaultNearClip = 0.3f;

        /// <summary>Modo de câmera atual (1ª/3ª pessoa).</summary>
        public CameraMode Mode => _mode;

        /// <summary>
        /// Aponta a câmera para um jogador específico. Usado pela rede no spawn do DONO
        /// (em vez de procurar pela tag "Player", que pegaria um jogador arbitrário com 2+).
        /// Recaptura corpo e bone da cabeça para o modo de 1ª pessoa valer para ESTE alvo.
        /// </summary>
        public void SetTarget(Transform target, PlayerInputReader input)
        {
            _target = target;
            _input = input;
            _bodyRenderers = target != null ? target.GetComponentsInChildren<Renderer>() : null;
            CacheHead(target);
            ApplyMode();
        }

        private void Start()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            if (_cam != null) _defaultNearClip = _cam.nearClipPlane;

            AutoWire();
            // Restaura a preferência salva (padrão: 3ª pessoa).
            _mode = (CameraMode)PlayerPrefs.GetInt(PrefKey, (int)CameraMode.ThirdPerson);
            ApplyMode();
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                AutoWire();
                if (_target == null) return;
            }

            if (_input != null && _input.ToggleViewPressedThisFrame)
            {
                SwitchMode();
            }

            // Acumula o olhar do jogador.
            Vector2 look = _input != null ? _input.LookDelta : Vector2.zero;
            _yaw += look.x * _sensitivity;
            _pitch = CameraMath.ClampPitch(_pitch - look.y * _sensitivity, _minPitch, _maxPitch);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            if (_mode == CameraMode.FirstPerson)
            {
                Vector3 eye = _target.position + Vector3.up * _eyeHeight;
                transform.SetPositionAndRotation(eye, rotation);
                // Re-aplica a cabeça encolhida DEPOIS do Animator ter escrito a pose deste frame.
                if (_head != null) _head.localScale = _headScale * HiddenHeadScale;
            }
            else
            {
                Vector3 focus = _target.position + Vector3.up * _thirdPersonHeight;
                transform.SetPositionAndRotation(CameraMath.ThirdPersonPosition(focus, rotation, _distance), rotation);
            }
        }

        private void SwitchMode()
        {
            _mode = CameraMath.Toggle(_mode);
            PlayerPrefs.SetInt(PrefKey, (int)_mode);
            ApplyMode();
        }

        // Guarda o bone da cabeça do rig Humanoid (o herói é Humanoid). Sem ele, o modo de
        // 1ª pessoa cai no fallback de esconder os renderers do corpo.
        private void CacheHead(Transform target)
        {
            _head = null;
            if (target == null) return;
            Animator anim = target.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman) _head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (_head != null) _headScale = _head.localScale;
        }

        // 1ª pessoa (Skyrim): corpo VISÍVEL, só a cabeça some + near clip curto pra ver a arma.
        // Sem bone de cabeça (cápsula): esconde os renderers, como antes.
        private void ApplyMode()
        {
            bool first = _mode == CameraMode.FirstPerson;
            // Publica no seam: o PlayerController gira o CORPO junto com a câmera em 1ª pessoa.
            CameraView.IsFirstPerson = first;

            if (_head != null)
            {
                _head.localScale = first ? _headScale * HiddenHeadScale : _headScale;
                if (_bodyRenderers != null)
                    foreach (Renderer r in _bodyRenderers) if (r != null) r.enabled = true;
            }
            else if (_bodyRenderers != null)
            {
                foreach (Renderer r in _bodyRenderers) if (r != null) r.enabled = !first;
            }

            if (_cam != null) _cam.nearClipPlane = first ? _firstPersonNearClip : _defaultNearClip;
        }

        private void AutoWire()
        {
            if (_target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _target = player.transform;
            }

            if (_target != null)
            {
                if (_input == null) _input = _target.GetComponent<PlayerInputReader>();
                if (_bodyRenderers == null) _bodyRenderers = _target.GetComponentsInChildren<Renderer>();
                if (_head == null) CacheHead(_target);
            }
        }
    }
}

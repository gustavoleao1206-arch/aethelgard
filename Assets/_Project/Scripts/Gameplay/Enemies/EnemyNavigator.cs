using UnityEngine;
using UnityEngine.AI;
using JogoDaTrupe.Core.Timing;

namespace JogoDaTrupe.Gameplay.Enemies
{
    /// <summary>
    /// NAVEGAÇÃO do inimigo sobre a malha (NavMesh). Não usa <c>NavMeshAgent</c>: calcula a rota
    /// com <see cref="NavMesh.CalculatePath"/> e devolve a direção do PRÓXIMO CANTO dela. Quem
    /// move o corpo continua sendo o <see cref="EnemyController"/>.
    ///
    /// Por que sem agente: o agente só entrega direção confiável quando ele mesmo dirige o
    /// transform — e ele NÃO pode dirigir, porque (a) o feel do movimento (velocidade por
    /// postura, zigzag, giro) está afinado no controller e (b) no co-op a réplica do cliente é
    /// movida pelo <c>NetworkTransform</c>, com quem o agente brigaria. Seguir os cantos da rota
    /// dá o mesmo contorno, é determinístico e não escreve no transform em máquina nenhuma.
    ///
    /// Oferece três serviços ao controller:
    ///  • <see cref="DirectionTo"/> — para onde andar pra contornar o obstáculo;
    ///  • <see cref="ClampStep"/>   — impede sair da malha (atravessar árvore/coluna);
    ///  • <see cref="BlockedTo"/>   — há parede entre nós? (senão o mob bate na coluna achando
    ///    que está batendo no jogador, que está do outro lado dela).
    ///
    /// Sem malha sob os pés, tudo cai no comportamento antigo de linha reta — e avisa no Console.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyNavigator : MonoBehaviour
    {
        [Tooltip("Raio de busca da malha. Generoso de propósito: o pivô do mob/jogador fica ~1m ACIMA do chão (centro da cápsula), não nos pés.")]
        [SerializeField] private float _sampleRadius = 3f;

        [Tooltip("De quanto em quanto tempo recalcular a rota (o alvo se mexe).")]
        [SerializeField] private float _repathInterval = 0.25f;

        [Tooltip("A que distância um canto da rota conta como alcançado.")]
        [SerializeField] private float _cornerReached = 0.8f;

        private const int MaxCorners = 64;
        private const float EdgeInset = 0.05f; // recuo para o corpo nunca repousar EM CIMA da borda

        // ⚠️ NavMeshPath NÃO pode nascer num inicializador de campo (o Unity constrói o componente
        // antes de a engine estar pronta e lança InitializeNavMeshPath). Criar no Awake.
        private NavMeshPath _path;
        private readonly Vector3[] _corners = new Vector3[MaxCorners];
        private int _cornerCount;
        private int _corner;
        private Vector3 _lastDestination;
        private float _nextRepath = -999f;

        /// <summary>Existe malha de navegação onde este inimigo vive?</summary>
        public bool HasMesh { get; private set; }

        private void Awake() => _path = new NavMeshPath();

        private void Start()
        {
            HasMesh = NavMesh.SamplePosition(transform.position, out _, _sampleRadius, NavMesh.AllAreas);
            if (!HasMesh)
            {
                // Fallback SILENCIOSO é armadilha: sem este aviso, o mob andaria em linha reta e
                // pareceria só "meio burro" — ninguém desconfiaria que faltou assar a malha.
                Debug.LogWarning($"[NavMesh] '{name}' nasceu a mais de {_sampleRadius}m da malha — " +
                                 "vai andar em linha reta (sem contornar obstáculos).", this);
            }
        }

        /// <summary>
        /// Direção para andar até <paramref name="destination"/> CONTORNANDO os obstáculos: aponta
        /// para o próximo canto da rota. Cai em <paramref name="fallback"/> (linha reta) sem malha,
        /// sem rota, ou já no último canto (aí o alvo está à vista).
        /// </summary>
        public Vector3 DirectionTo(Vector3 destination, Vector3 fallback)
        {
            if (!HasMesh) return fallback;
            if (!TryMap(transform.position, out Vector3 from) || !TryMap(destination, out Vector3 to)) return fallback;

            Repath(from, to);

            // Consome os cantos já alcançados (o corpo anda; a rota fica).
            while (_corner < _cornerCount && Planar(_corners[_corner] - from).sqrMagnitude < _cornerReached * _cornerReached)
                _corner++;

            if (_corner >= _cornerCount) return fallback; // fim da rota: segue reto no alvo

            Vector3 dir = Planar(_corners[_corner] - from);
            return dir.sqrMagnitude < 0.0001f ? fallback : dir.normalized;
        }

        /// <summary>
        /// Corrige um passo para que o corpo NÃO saia da malha: se o trecho atravessa a borda
        /// (tronco de árvore, coluna, precipício), DESLIZA ao longo dela. É a "colisão" do
        /// inimigo — ele não tem física, então sem isto atravessaria os objetos.
        ///
        /// ⚠️ Deslizar, não parar. Parar na borda prende o mob: no frame seguinte o teste nasce
        /// EM CIMA da borda, acusa bloqueio a distância zero, e ele fica colado na coluna para
        /// sempre — sem nunca dobrar a esquina que a rota mandou dobrar.
        /// </summary>
        public Vector3 ClampStep(Vector3 desired)
        {
            if (!HasMesh) return desired;
            if (!TryMap(transform.position, out Vector3 from)) return desired;

            Vector3 to = new Vector3(desired.x, from.y, desired.z);
            if (!NavMesh.Raycast(from, to, out NavMeshHit hit, NavMesh.AllAreas)) return desired; // caminho livre

            // Bateu na borda: aproveita só a parte do passo que corre PARALELA a ela.
            Vector3 slide = Vector3.ProjectOnPlane(to - from, hit.normal);
            if (slide.sqrMagnitude > 1e-6f)
            {
                Vector3 slideTo = from + slide;
                if (!NavMesh.Raycast(from, slideTo, out _, NavMesh.AllAreas))
                    return new Vector3(slideTo.x, desired.y, slideTo.z);
            }

            // Encurralado (canto): encosta, mas RECUADO um tico para dentro da malha — senão o
            // próximo passo nasce sobre a borda e o mob trava (ver aviso acima).
            Vector3 inward = from - hit.position;
            Vector3 rest = inward.sqrMagnitude > 1e-6f
                ? hit.position + inward.normalized * EdgeInset
                : from;
            return new Vector3(rest.x, desired.y, rest.z);
        }

        /// <summary>
        /// Há obstáculo (buraco na malha) entre este inimigo e <paramref name="target"/>? Serve
        /// para não atacar o jogador através de uma coluna: bloqueado, o mob prefere contornar.
        /// </summary>
        public bool BlockedTo(Vector3 target)
        {
            if (!HasMesh) return false;
            if (!TryMap(transform.position, out Vector3 from) || !TryMap(target, out Vector3 to)) return false;
            return NavMesh.Raycast(from, to, out _, NavMesh.AllAreas);
        }

        // Recalcula a rota de tempos em tempos, ou assim que o destino se move de verdade
        // (o jogador anda). Entre recálculos, o mob segue os cantos já conhecidos.
        private void Repath(Vector3 from, Vector3 to)
        {
            bool destinationMoved = (to - _lastDestination).sqrMagnitude > 1f;
            if (GameTime.Time < _nextRepath && !destinationMoved) return;

            _nextRepath = GameTime.Time + _repathInterval;
            _lastDestination = to;
            _corner = 1; // corners[0] é onde já estamos
            _cornerCount = 0;

            // PathPartial serve: o mob anda o quanto der. Só PathInvalid não leva a lugar nenhum.
            if (NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _path) && _path.status != NavMeshPathStatus.PathInvalid)
                _cornerCount = _path.GetCornersNonAlloc(_corners); // sem alocar (roda todo frame)
        }

        private bool TryMap(Vector3 point, out Vector3 onMesh)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, _sampleRadius, NavMesh.AllAreas))
            {
                onMesh = hit.position;
                return true;
            }
            onMesh = point;
            return false;
        }

        private static Vector3 Planar(Vector3 v) { v.y = 0f; return v; }
    }
}

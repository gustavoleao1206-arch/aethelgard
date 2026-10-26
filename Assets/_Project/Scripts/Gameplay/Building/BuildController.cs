using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Identity;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Inventory;
using JogoDaTrupe.Input;

namespace JogoDaTrupe.Gameplay.Building
{
    /// <summary>
    /// Construção em GRADE. Tecla B entra/sai do modo construção; Q alterna a
    /// estrutura; F posiciona. Uma prévia ("fantasma") segue a mira encaixada na
    /// grade (verde = dá para pagar, vermelho = falta material). Posicionar consome
    /// o custo do inventário e cria a estrutura (com EntityGuid, pronta para o save).
    ///
    /// Lê INTENÇÕES (PlayerInputReader) e paga via PlayerInventory (multiplayer-ready).
    /// A criação do bloco em runtime é um atalho do slice (sem prefab/arte ainda).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class BuildController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private StructureDefinition[] _structures;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private float _aimRange = 25f;
        [SerializeField] private LayerMask _aimMask = ~0;

        private PlayerInventory _inventory;
        private Ownable _ownable;
        private Camera _camera;
        private int _index;
        private bool _building;
        private bool _validTarget;
        private Vector3 _targetPos;

        private GameObject _ghost;
        private Renderer _ghostRenderer;

        private static readonly Color GhostOk = new Color(0.3f, 0.9f, 0.4f);
        private static readonly Color GhostNo = new Color(0.9f, 0.3f, 0.3f);

        public bool IsBuilding => _building;
        public StructureDefinition Current =>
            (_structures != null && _structures.Length > 0) ? _structures[_index] : null;
        public bool CanAffordCurrent => Current != null && _inventory.CanAfford(Current.cost);

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _ownable = GetComponent<Ownable>();
            if (_input == null) _input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            if (_input == null) return;

            if (_input.ToggleBuildPressedThisFrame) ToggleBuild();
            if (!_building) return;

            if (_input.CycleBuildPressedThisFrame) Cycle();

            UpdateGhost();

            if (_input.PlaceBuildPressedThisFrame) TryPlace();
        }

        private void ToggleBuild()
        {
            _building = !_building;
            if (_building) RebuildGhost();
            else DestroyGhost();
        }

        private void Cycle()
        {
            if (_structures == null || _structures.Length == 0) return;
            _index = (_index + 1) % _structures.Length;
            RebuildGhost();
        }

        // Aponta da mira (centro da tela) para o chão e encaixa na grade.
        private void UpdateGhost()
        {
            _validTarget = false;
            if (_ghost == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) { _ghost.SetActive(false); return; }

            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, _aimRange, _aimMask, QueryTriggerInteraction.Ignore)
                && hit.transform.root != transform.root) // não mira em si mesmo
            {
                StructureDefinition def = Current;
                if (def == null) { _ghost.SetActive(false); return; } // defensivo (sem estrutura selecionada)

                Vector3 snapped = BuildMath.Snap(hit.point, _cellSize);
                // Relevo (Milestone do Terreno): a altura vem da MESMA função que gera a malha do
                // chão, no ponto JÁ encaixado na grade. Antes o Y era forçado a baseY (chão plano
                // em 0) — numa encosta a peça flutuaria/afundaria. baseY vira deslocamento sobre o chão.
                snapped.y = WorldHeight.SampleHeight(snapped.x, snapped.z) + def.baseY;

                _targetPos = snapped;
                _ghost.transform.position = snapped;
                _ghost.SetActive(true);
                _validTarget = true;

                if (_ghostRenderer != null)
                    _ghostRenderer.material.color = CanAffordCurrent ? GhostOk : GhostNo;
            }
            else
            {
                _ghost.SetActive(false);
            }
        }

        private void TryPlace()
        {
            if (!_validTarget || Current == null) return;

            // EM REDE (M-NET-7 P5): quem constrói de verdade é o SERVIDOR — ele valida o
            // custo no inventário autoritativo, debita e spawna o prefab de rede (que
            // aparece nas duas telas). Aqui só publicamos a INTENÇÃO: no host o executor a
            // atende direto pelo bus; no cliente o NetworkWorldRequestForwarder a leva por
            // RPC. NÃO debitamos aqui (o servidor debita); a prévia já checou se dá pra
            // pagar (fantasma verde). Vale para host E cliente — um cubo local só o host
            // veria. Padrão Intenção→Executor→Estado replicado (o mesmo do dano/coleta).
            if (SimulationAuthority.IsNetworked)
            {
                if (!CanAffordCurrent) return; // não envia pedido que o servidor vai negar
                EventBus<BuildRequested>.Publish(new BuildRequested
                {
                    StructureId = Current.structureId,
                    X = _targetPos.x,
                    Y = _targetPos.y,
                    Z = _targetPos.z,
                    OwnerId = _ownable != null ? _ownable.OwnerId : 0
                });
                return;
            }

            // SOLO: cria o cubo local como sempre (autoridade verdadeira; regressão-zero).
            if (!_inventory.SpendCost(Current.cost)) return; // sem material

            StructureDefinition def = Current;
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = "Estrutura_" + def.displayName;
            s.transform.position = _targetPos;
            s.transform.localScale = def.size;
            Paint(s.GetComponent<Renderer>(), def.color);
            s.AddComponent<EntityGuid>(); // id estável para o save
        }

        // ---- Prévia ("fantasma") ----

        private void RebuildGhost()
        {
            DestroyGhost();
            StructureDefinition def = Current;
            if (def == null) return;

            _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghost.name = "BuildGhost";
            Destroy(_ghost.GetComponent<Collider>()); // não colide
            _ghost.transform.localScale = def.size;
            _ghostRenderer = _ghost.GetComponent<Renderer>();
            Paint(_ghostRenderer, GhostOk);
            _ghost.SetActive(false);
        }

        private void DestroyGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostRenderer = null;
        }

        private void OnDisable() => DestroyGhost();

        // Material CEL-SHADED (JogoDaTrupe/Toon — direção de arte 05/07/2026) com a cor.
        // Fallback URP/Lit → Standard para não sair "magenta" se o shader não existir.
        private static void Paint(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            Shader shader = Shader.Find("JogoDaTrupe/Toon");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            renderer.material = new Material(shader) { color = color };
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Gameplay.World
{
    /// <summary>
    /// FITA DE TRILHA (Milestone do Terreno, M-TERR-4): desenha as trilhas de <see cref="WorldPaths"/>
    /// como uma malha de fita drapeada no relevo (<see cref="PathRibbon"/> + <see cref="WorldHeight"/>),
    /// um pouco mais ESTREITA que o corredor plano (sobra grama nas beiradas) e levemente ACIMA do
    /// chão (sem z-fighting). As polilinhas são as mesmas que achatam o relevo, então trilha visível
    /// e corredor caminhável nunca divergem.
    ///
    /// Mesmo desenho do <see cref="GroundMeshBuilder"/>: ExecuteAlways, reconstrói no OnEnable, malha
    /// DontSave (nada de asset binário no git), afinar constantes vale no próximo load. SEM collider:
    /// quem colide é o chão por baixo — a fita é só visual.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PathMeshBuilder : MonoBehaviour
    {
        [Tooltip("Largura da fita (m). Menor que o corredor plano (6 m) para a trilha ter uma faixa de grama nas beiradas.")]
        [SerializeField] private float _width = 4f;

        [Tooltip("Elevação acima do chão (m) — evita z-fighting com a malha do chão.")]
        [SerializeField] private float _lift = 0.03f;

        [Tooltip("Espaçamento máximo entre anéis da fita (m) — menor = segue melhor o relevo.")]
        [SerializeField] private float _step = 1f;

        [Tooltip("Metros por repetição da textura ao longo da trilha.")]
        [SerializeField] private float _uvRepeat = 4f;

        private Mesh _mesh;

        private void OnEnable() => Rebuild();

        private void OnDisable()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
            _mesh = null;
        }

        /// <summary>(Re)constrói a fita de todas as trilhas numa malha só.</summary>
        public void Rebuild()
        {
            var ribbons = new List<PathRibbon.RibbonMesh>(WorldPaths.Polylines.Length);
            foreach (Vector2[] path in WorldPaths.Polylines)
                ribbons.Add(PathRibbon.Build(path, _width, WorldHeight.SampleHeight, _lift, _step, _uvRepeat));
            PathRibbon.RibbonMesh all = PathRibbon.Combine(ribbons);

            if (_mesh == null) _mesh = new Mesh { name = "PathMesh (gerada)", hideFlags = HideFlags.DontSave };
            else _mesh.Clear();

            if (!all.IsEmpty)
            {
                _mesh.indexFormat = all.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                _mesh.vertices = all.Vertices;
                _mesh.uv = all.Uvs;
                _mesh.triangles = all.Triangles;
                _mesh.RecalculateNormals();
                _mesh.RecalculateBounds();
            }

            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Gameplay.World
{
    /// <summary>
    /// CHÃO ESCULPIDO (Milestone do Terreno): gera a malha do terreno a partir de
    /// <see cref="WorldHeight.SampleHeight"/> e a entrega ao MeshFilter (visual) e ao
    /// MeshCollider (o jogador anda nele, a NavMesh assa em cima, o BuildController mira).
    /// Substitui o Plane liso.
    ///
    /// A malha NÃO é salva como asset (<c>HideFlags.DontSave</c>): é reconstruída em OnEnable —
    /// no Editor (ExecuteAlways) e no jogo. Motivo: um Mesh de ~31k vértices como asset seria
    /// um binário de ~2 MB reescrito a cada regeneração, inchando o git sem merge possível
    /// (lição registrada no milestone). Como a função de altura é determinística, reconstruir
    /// dá SEMPRE a mesma malha — e afinar as constantes do WorldHeight passa a valer no próximo
    /// load, sem regerar a cena (só a NavMesh precisa de re-bake).
    ///
    /// O gerador de cena adiciona este componente ANTES de assar a NavMesh: com ExecuteAlways,
    /// OnEnable roda na hora e o MeshCollider já existe para o bake.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class GroundMeshBuilder : MonoBehaviour
    {
        [Tooltip("Meia-largura do chão em metros (cobre o anel de penhascos).")]
        [SerializeField] private float _halfExtent = 44f;

        [Tooltip("Tamanho da célula da grade em metros. 0.5 m dá ~31k vértices num chão de 88 m — suave o bastante para os falloffs de 5 m e o ruído de 9–18 m do WorldHeight.")]
        [SerializeField] private float _cellSize = 0.5f;

        private Mesh _mesh;

        /// <summary>Vértices por lado da grade (exposto para os testes).</summary>
        public int VerticesPerSide => Mathf.Max(2, Mathf.RoundToInt(_halfExtent * 2f / _cellSize) + 1);

        private void OnEnable() => Rebuild();

        private void OnDisable()
        {
            if (_mesh == null) return;
            // Um Mesh DontSave não é limpo pela cena: destruímos nós mesmos para não vazar entre reloads.
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
            _mesh = null;
        }

        /// <summary>(Re)constrói a malha a partir da função de altura e a entrega ao filter e ao collider.</summary>
        public void Rebuild()
        {
            int n = VerticesPerSide;
            float step = _halfExtent * 2f / (n - 1);

            var vertices = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            var colors = new Color[n * n]; // branco por ora; o blend de caminho (M-TERR-4) pinta aqui
            for (int j = 0; j < n; j++)
            {
                float z = -_halfExtent + j * step;
                for (int i = 0; i < n; i++)
                {
                    float x = -_halfExtent + i * step;
                    int k = j * n + i;
                    vertices[k] = new Vector3(x, WorldHeight.SampleHeight(x, z), z);
                    // UV 0..1 no chão inteiro, igual ao Plane: o material continua repetindo a
                    // grama a cada ~4 m (EnsureGroundMaterial) sem nenhuma mudança.
                    uvs[k] = new Vector2((float)i / (n - 1), (float)j / (n - 1));
                    colors[k] = Color.white;
                }
            }

            var triangles = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int j = 0; j < n - 1; j++)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    // Ordem (a, c, b) / (b, c, d): produto vetorial aponta para +Y — o chão é
                    // visível por cima e as normais recalculadas saem para cima.
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            }

            if (_mesh == null) _mesh = new Mesh { name = "GroundMesh (gerada)", hideFlags = HideFlags.DontSave };
            else _mesh.Clear();

            // 16 bits só cabem 65k vértices; célula menor que ~0.35 m estoura — sobe para 32 bits.
            _mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.vertices = vertices;
            _mesh.uv = uvs;
            _mesh.colors = colors;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var collider = GetComponent<MeshCollider>();
            collider.sharedMesh = null; // força re-cozinhar a colisão com a malha nova
            collider.sharedMesh = _mesh;
        }
    }
}

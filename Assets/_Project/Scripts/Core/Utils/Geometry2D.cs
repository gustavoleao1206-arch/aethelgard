using UnityEngine;

namespace JogoDaTrupe.Core.Utils
{
    /// <summary>
    /// Funções de geometria PURAS no plano horizontal (XZ) — sem estado, sem dependência
    /// de cena. Vivem no Core (camada de fundação) para serem reutilizáveis por qualquer
    /// sistema e, principalmente, **testáveis** em EditMode (a assembly de testes enxerga o Core).
    ///
    /// Ignoram o eixo Y de propósito: a maior parte da lógica espacial do jogo (zonas,
    /// raios de busca, contenção em área) acontece no plano do chão.
    /// </summary>
    public static class Geometry2D
    {
        /// <summary>Distância ao quadrado entre dois pontos no plano XZ (ignora Y). Sem raiz — barato.</summary>
        public static float PlanarDistanceSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        /// <summary>
        /// Verdadeiro se <paramref name="point"/> está ESTRITAMENTE dentro de um círculo de
        /// raio <paramref name="radius"/> centrado em <paramref name="center"/> (plano XZ).
        /// Em cima da borda → falso. Raio &lt;= 0 → sempre falso.
        /// </summary>
        public static bool InsideCircleXZ(Vector3 point, Vector3 center, float radius)
        {
            if (radius <= 0f) return false;
            return PlanarDistanceSqr(point, center) < radius * radius;
        }
    }
}

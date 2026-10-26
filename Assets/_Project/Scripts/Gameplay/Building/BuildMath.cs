using UnityEngine;

namespace JogoDaTrupe.Gameplay.Building
{
    /// <summary>
    /// Lógica PURA da construção (testável sem abrir o jogo): encaixar uma posição
    /// do mundo numa grade (snapping). Mantém o X/Z na grade e preserva o Y.
    /// </summary>
    public static class BuildMath
    {
        /// <summary>
        /// Encaixa <paramref name="position"/> numa grade de célula <paramref name="cellSize"/>
        /// (arredonda X e Z para o múltiplo mais próximo). Y é preservado.
        /// </summary>
        public static Vector3 Snap(Vector3 position, float cellSize)
        {
            if (cellSize <= 0f) return position;

            float x = Mathf.Round(position.x / cellSize) * cellSize;
            float z = Mathf.Round(position.z / cellSize) * cellSize;
            return new Vector3(x, position.y, z);
        }
    }
}

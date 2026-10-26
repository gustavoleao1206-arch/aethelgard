using UnityEngine;

namespace JogoDaTrupe.Gameplay.Characters
{
    /// <summary>
    /// Cálculos puros de movimento, separados do MonoBehaviour para poderem ser
    /// testados sem abrir o jogo (testes EditMode rápidos).
    /// </summary>
    public static class MovementMath
    {
        /// <summary>
        /// Converte a intenção de movimento do jogador (x = direita, y = frente)
        /// numa direção no mundo, relativa à orientação da câmera no plano (yaw).
        /// Assim, "frente" é sempre para onde a câmera aponta — não um eixo fixo.
        /// </summary>
        /// <param name="input">Intenção de movimento (já normalizada, -1..1).</param>
        /// <param name="cameraYawDegrees">Rotação horizontal da câmera, em graus.</param>
        /// <returns>Direção no plano XZ (y = 0), com magnitude no máximo 1.</returns>
        public static Vector3 CameraRelativeDirection(Vector2 input, float cameraYawDegrees)
        {
            float yawRad = cameraYawDegrees * Mathf.Deg2Rad;

            // Vetores "frente" e "direita" da câmera projetados no chão.
            Vector3 forward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            Vector3 right = new Vector3(Mathf.Cos(yawRad), 0f, -Mathf.Sin(yawRad));

            Vector3 direction = right * input.x + forward * input.y;

            // Não deixa o movimento diagonal ficar mais rápido que o reto.
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }
    }
}

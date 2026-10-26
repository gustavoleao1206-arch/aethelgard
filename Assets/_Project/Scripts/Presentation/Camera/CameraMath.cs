using UnityEngine;

namespace JogoDaTrupe.Presentation.Camera
{
    /// <summary>
    /// Cálculos puros da câmera, separados do MonoBehaviour para poderem ser
    /// testados sem abrir o jogo (testes EditMode rápidos).
    /// </summary>
    public static class CameraMath
    {
        /// <summary>Alterna entre 1ª e 3ª pessoa.</summary>
        public static CameraMode Toggle(CameraMode mode)
        {
            return mode == CameraMode.ThirdPerson ? CameraMode.FirstPerson : CameraMode.ThirdPerson;
        }

        /// <summary>Limita a inclinação vertical (pitch) entre um mínimo e um máximo.</summary>
        public static float ClampPitch(float pitch, float min, float max)
        {
            return Mathf.Clamp(pitch, min, max);
        }

        /// <summary>
        /// Posição da câmera em 3ª pessoa: parte do ponto focal e recua na direção
        /// oposta à que a câmera olha, pela distância desejada.
        /// </summary>
        public static Vector3 ThirdPersonPosition(Vector3 focus, Quaternion rotation, float distance)
        {
            return focus - rotation * Vector3.forward * distance;
        }
    }
}

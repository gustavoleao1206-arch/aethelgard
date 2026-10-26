using UnityEngine;

namespace JogoDaTrupe.Gameplay.Combat
{
    /// <summary>
    /// Cálculos puros de combate, separados do MonoBehaviour para serem testados
    /// sem abrir o jogo (testes EditMode rápidos).
    /// </summary>
    public static class CombatMath
    {
        /// <summary>Aplica dano à vida atual, sem deixar passar de 0. Dano negativo é ignorado.</summary>
        public static float ApplyDamage(float current, float amount)
        {
            if (amount < 0f) amount = 0f;
            return Mathf.Max(0f, current - amount);
        }

        /// <summary>
        /// Aplica dano respeitando um PISO (mínimo) de vida — para entidades que não
        /// devem morrer (ex.: a criatura capturável fica "enfraquecida" no limiar,
        /// nunca em 0). Piso ≤ 0 equivale ao <see cref="ApplyDamage(float,float)"/>.
        /// </summary>
        public static float ApplyDamage(float current, float amount, float minimum)
        {
            float result = ApplyDamage(current, amount);
            return Mathf.Max(minimum, result);
        }

        /// <summary>Cura a vida atual, sem passar do máximo. Cura negativa é ignorada.</summary>
        public static float Heal(float current, float amount, float max)
        {
            if (amount < 0f) amount = 0f;
            return Mathf.Min(max, current + amount);
        }
    }
}

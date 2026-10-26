using UnityEngine;
using JogoDaTrupe.Core.Utils;

namespace JogoDaTrupe.Gameplay.Combat
{
    /// <summary>
    /// Lógica PURA de "guardião de zona" (fix da morte instantânea no co-op, 02/07/2026).
    /// Um inimigo/boss só ENGAJA um alvo que entrou na sua zona — a detecção é medida a
    /// partir da CASA (spawn), não da posição atual — e só enquanto ele próprio não
    /// esticou além do <c>leashRadius</c> da casa. Assim ele não persegue o jogador para
    /// fora da clareira nem invade o hub (onde jogadores nascem), igual ao boss que fica
    /// ocioso até alguém entrar na arena.
    ///
    /// No plano XZ (ignora altura). Sem estado/cena → testável em EditMode.
    /// </summary>
    public static class ZoneGuard
    {
        /// <summary>
        /// Deve engajar? Verdadeiro se o alvo está dentro de <paramref name="detectRange"/>
        /// da <paramref name="home"/> E o próprio guardião está dentro de
        /// <paramref name="leashRadius"/> da casa (na borda conta — usa &lt;=).
        /// </summary>
        public static bool ShouldEngage(Vector3 home, Vector3 self, Vector3 target, float detectRange, float leashRadius)
        {
            return Geometry2D.PlanarDistanceSqr(home, target) <= detectRange * detectRange
                && Geometry2D.PlanarDistanceSqr(home, self) <= leashRadius * leashRadius;
        }
    }
}

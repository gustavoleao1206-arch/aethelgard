using UnityEngine;
using JogoDaTrupe.Core.Utils;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Lógica PURA da validação de Altar (M-NET-8 P3, testável sem abrir o jogo):
    /// o jogador está perto o bastante do Altar para operações de loadout?
    ///
    /// É o que o SERVIDOR usa para validar Equip/Unequip/InvestNode/Respec — a
    /// "proximidade REAL, nunca o bool do cliente" (plano-mãe M-NET-8). Distância
    /// PLANAR (XZ), como o ZoneGuard: a altura do pivot do Altar não deve importar.
    /// </summary>
    public static class AltarMath
    {
        /// <summary>
        /// Alcance da validação server-side: alcance de interação (~2,5 m) + folga
        /// generosa. É anti-trapaça de co-op entre amigos, não PvP — o caso a barrar
        /// é "equipar do outro lado do mapa", não "estava a 10 cm da borda".
        /// </summary>
        public const float DefaultRange = 6f;

        /// <summary>Verdadeiro se <paramref name="player"/> está a até <paramref name="range"/> do Altar (XZ).</summary>
        public static bool IsNear(Vector3 player, Vector3 altar, float range)
        {
            if (range <= 0f) return false;
            return Geometry2D.PlanarDistanceSqr(player, altar) <= range * range;
        }
    }
}

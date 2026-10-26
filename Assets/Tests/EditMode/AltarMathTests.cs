using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a validação PURA de proximidade do Altar (M-NET-8 P3) — é o que o
    /// servidor usa para aceitar/negar operações de loadout ("proximidade real,
    /// nunca o bool do cliente"). Distância PLANAR (XZ), como o ZoneGuard.
    /// </summary>
    public class AltarMathTests
    {
        private static readonly Vector3 Altar = new Vector3(0f, 0.75f, 2.5f);

        [Test]
        public void Perto_DentroDoAlcance_Valida()
        {
            Vector3 player = Altar + new Vector3(2f, 0.35f, 1f); // ~2,2m no plano
            Assert.IsTrue(AltarMath.IsNear(player, Altar, AltarMath.DefaultRange));
        }

        [Test]
        public void Longe_ForaDoAlcance_Nega()
        {
            Vector3 player = Altar + new Vector3(10f, 0f, 10f); // outra zona
            Assert.IsFalse(AltarMath.IsNear(player, Altar, AltarMath.DefaultRange),
                "equipar do outro lado do mapa é exatamente o que a validação barra");
        }

        [Test]
        public void Borda_ExatamenteNoAlcance_Valida_EAlturaNaoImporta()
        {
            Vector3 player = Altar + new Vector3(AltarMath.DefaultRange, 5f, 0f); // borda, 5m acima
            Assert.IsTrue(AltarMath.IsNear(player, Altar, AltarMath.DefaultRange),
                "<= na borda e distância PLANAR (a altura do pivot não conta)");
        }

        [Test]
        public void AlcanceInvalido_Nega()
        {
            Assert.IsFalse(AltarMath.IsNear(Altar, Altar, 0f));
            Assert.IsFalse(AltarMath.IsNear(Altar, Altar, -1f));
        }
    }
}

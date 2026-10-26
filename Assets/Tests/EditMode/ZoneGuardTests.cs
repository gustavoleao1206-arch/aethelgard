using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Gameplay.Combat;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a lógica pura do guardião de zona (fix da morte instantânea no co-op):
    /// o inimigo só engaja quem entrou na sua zona (detecção a partir da CASA) e
    /// enquanto ele mesmo não esticou além do leash. Ver ZoneGuard.
    /// </summary>
    public class ZoneGuardTests
    {
        private static readonly Vector3 Home = new Vector3(11.3f, 0f, 11.3f); // zona de combate

        [Test]
        public void JogadorNaZona_ComGuardiaEmCasa_Engaja()
        {
            Vector3 target = Home + new Vector3(3f, 0f, 2f); // dentro dos 16
            Assert.IsTrue(ZoneGuard.ShouldEngage(Home, Home, target, detectRange: 16f, leashRadius: 10f));
        }

        [Test]
        public void JogadorNoHub_ForaDaZona_NaoEngaja()
        {
            Vector3 hubPlayer = new Vector3(0f, 1.1f, -2f); // ~18 m da casa → fora dos 16
            Assert.IsFalse(ZoneGuard.ShouldEngage(Home, Home, hubPlayer, detectRange: 16f, leashRadius: 10f),
                "jogador no hub não pode acordar o guardião da zona de combate");
        }

        [Test]
        public void GuardiaEsticadoAlemDoLeash_NaoEngaja_MesmoComAlvoNaZona()
        {
            Vector3 target = Home + new Vector3(2f, 0f, 0f);            // alvo pertinho da casa
            Vector3 selfLonge = Home + new Vector3(-12f, 0f, 0f);       // guardião a 12 m da casa (> leash 10)
            Assert.IsFalse(ZoneGuard.ShouldEngage(Home, selfLonge, target, detectRange: 16f, leashRadius: 10f),
                "esticado além do leash → desengata e volta pra casa");
        }

        [Test]
        public void NaBordaDoDetect_Engaja_UsaMenorOuIgual()
        {
            Vector3 target = Home + new Vector3(16f, 0f, 0f); // exatamente na borda dos 16
            Assert.IsTrue(ZoneGuard.ShouldEngage(Home, Home, target, detectRange: 16f, leashRadius: 20f));
        }

        [Test]
        public void IgnoraAltura_SoContaOPlano_XZ()
        {
            // Alvo dentro no plano, mas muito acima: a altura não deve tirá-lo da zona.
            Vector3 target = Home + new Vector3(3f, 50f, 2f);
            Assert.IsTrue(ZoneGuard.ShouldEngage(Home, Home, target, detectRange: 16f, leashRadius: 10f));
        }
    }
}

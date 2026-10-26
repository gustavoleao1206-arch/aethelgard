using System.Collections.Generic;
using NUnit.Framework;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa as regras puras do loadout (EditMode): limite de 5, uma por criatura
    /// e troca só no Altar. Ver LoadoutMath.
    /// </summary>
    public class LoadoutMathTests
    {
        [Test]
        public void ForaDoAltar_NaoPodeEquipar()
        {
            bool ok = LoadoutMath.CanEquip(new List<string>(), "Fera", atAltar: false, out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void NoAltar_ListaVazia_PodeEquipar()
        {
            bool ok = LoadoutMath.CanEquip(new List<string>(), "Fera", atAltar: true, out _);
            Assert.IsTrue(ok);
        }

        [Test]
        public void FamiliaJaEquipada_NaoPodeRepetir()
        {
            var equipped = new List<string> { "Fera" };
            bool ok = LoadoutMath.CanEquip(equipped, "Fera", atAltar: true, out string reason);
            Assert.IsFalse(ok);
            Assert.IsNotNull(reason);
        }

        [Test]
        public void OutraFamilia_PodeEquipar()
        {
            var equipped = new List<string> { "Fera" };
            bool ok = LoadoutMath.CanEquip(equipped, "Ave", atAltar: true, out _);
            Assert.IsTrue(ok);
        }

        [Test]
        public void Cheio_NaoPodeEquipar()
        {
            var equipped = new List<string> { "A", "B", "C", "D", "E" }; // 5 = MaxAbilities
            Assert.AreEqual(LoadoutMath.MaxAbilities, equipped.Count);
            bool ok = LoadoutMath.CanEquip(equipped, "F", atAltar: true, out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void FamiliaInvalida_NaoPodeEquipar()
        {
            bool ok = LoadoutMath.CanEquip(new List<string>(), "", atAltar: true, out _);
            Assert.IsFalse(ok);
        }
    }
}

using NUnit.Framework;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a Maestria determinística (lógica pura, EditMode): qual camada uma
    /// criatura libera para um dado nº de derrotas. Ver MasteryMath.
    /// </summary>
    public class MasteryMathTests
    {
        private static readonly int[] Thresholds = { 1, 3, 6 }; // Vestígio, Ressonância, Núcleo

        [Test]
        public void SemDerrotas_NaoLiberaNada()
        {
            bool ok = MasteryMath.TryHighestLayer(0, Thresholds, out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void UmaDerrota_LiberaVestigio()
        {
            bool ok = MasteryMath.TryHighestLayer(1, Thresholds, out IncorporationLayer layer);
            Assert.IsTrue(ok);
            Assert.AreEqual(IncorporationLayer.Vestigio, layer);
        }

        [Test]
        public void TresDerrotas_LiberaRessonancia()
        {
            MasteryMath.TryHighestLayer(3, Thresholds, out IncorporationLayer layer);
            Assert.AreEqual(IncorporationLayer.Ressonancia, layer);
        }

        [Test]
        public void SeisOuMais_LiberaNucleo()
        {
            MasteryMath.TryHighestLayer(7, Thresholds, out IncorporationLayer layer);
            Assert.AreEqual(IncorporationLayer.Nucleo, layer);
        }

        [Test]
        public void EntreLimiares_MantemCamadaAnterior()
        {
            // 2 derrotas: passou de Vestígio (1) mas não chegou em Ressonância (3).
            MasteryMath.TryHighestLayer(2, Thresholds, out IncorporationLayer layer);
            Assert.AreEqual(IncorporationLayer.Vestigio, layer);
        }

        [Test]
        public void LimiaresVazios_NaoLiberaNada()
        {
            Assert.IsFalse(MasteryMath.TryHighestLayer(99, new int[0], out _));
            Assert.IsFalse(MasteryMath.TryHighestLayer(99, null, out _));
        }
    }
}

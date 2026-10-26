using NUnit.Framework;
using JogoDaTrupe.Core.Timing;
using JogoDaTrupe.Core.Rng;

namespace JogoDaTrupe.Tests.Core
{
    /// <summary>
    /// Testa a centralização de tempo/RNG (M-NET-1): trocar o Provider muda o que a
    /// simulação enxerga (o seam que a rede usará), e o RNG semeado é reprodutível.
    /// </summary>
    public class TimeAndRandomTests
    {
        // Provedor de tempo falso, para provar que GameTime usa o Provider injetado.
        private sealed class FakeTime : ITimeProvider
        {
            public float Time { get; set; }
            public float DeltaTime { get; set; }
        }

        // Restaura os provedores padrão após cada teste (são estáticos — não vazar estado).
        [TearDown]
        public void Reset()
        {
            GameTime.Provider = new SystemTimeProvider();
            GameRandom.Provider = new SystemRandomProvider();
        }

        [Test]
        public void GameTime_UsaOProviderInjetado()
        {
            GameTime.Provider = new FakeTime { Time = 42f, DeltaTime = 0.5f };
            Assert.AreEqual(42f, GameTime.Time, 0.0001f);
            Assert.AreEqual(0.5f, GameTime.DeltaTime, 0.0001f);
        }

        [Test]
        public void GameTime_ProviderPadrao_NaoENulo()
        {
            // Após o TearDown, o padrão solo está ativo (encapsula UnityEngine.Time).
            Assert.IsInstanceOf<SystemTimeProvider>(GameTime.Provider);
        }

        [Test]
        public void SeededRandom_MesmaSemente_MesmaSequencia()
        {
            var a = new SeededRandom(123);
            var b = new SeededRandom(123);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(a.Value, b.Value, 0.0000001f);
        }

        [Test]
        public void SeededRandom_SementesDiferentes_SequenciasDiferentes()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);
            bool algumaDiferenca = false;
            for (int i = 0; i < 8; i++)
                if (a.Value != b.Value) { algumaDiferenca = true; break; }
            Assert.IsTrue(algumaDiferenca);
        }

        [Test]
        public void SeededRandom_RangeInt_DentroDoIntervalo()
        {
            var r = new SeededRandom(7);
            for (int i = 0; i < 100; i++)
            {
                int v = r.Range(2, 5); // [2,5)
                Assert.GreaterOrEqual(v, 2);
                Assert.Less(v, 5);
            }
        }

        [Test]
        public void SeededRandom_RangeFloat_DentroDoIntervalo()
        {
            var r = new SeededRandom(9);
            for (int i = 0; i < 100; i++)
            {
                float v = r.Range(-1f, 3f); // [-1,3)
                Assert.GreaterOrEqual(v, -1f);
                Assert.Less(v, 3f);
            }
        }

        [Test]
        public void GameRandom_UsaOProviderInjetado()
        {
            GameRandom.Provider = new SeededRandom(99);
            float v = GameRandom.Value;
            Assert.GreaterOrEqual(v, 0f);
            Assert.Less(v, 1f);
        }
    }
}

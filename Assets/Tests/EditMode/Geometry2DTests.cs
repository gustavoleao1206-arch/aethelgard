using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.Utils;

namespace JogoDaTrupe.Tests.Core
{
    /// <summary>
    /// Testa a geometria pura no plano XZ (EditMode). Ver Geometry2D.
    /// Usada, entre outros, pela guarda que impede espalhar cenário em cima da
    /// arena do boss (ForestWorld.InsideArena).
    /// </summary>
    public class Geometry2DTests
    {
        private static readonly Vector3 Centro = new Vector3(0f, 0f, 26f); // arena do boss
        private const float Raio = 9f; // ArenaRadius (6.5) + margem (2.5)

        [Test]
        public void InsideCircle_Centro_EstaDentro()
        {
            Assert.IsTrue(Geometry2D.InsideCircleXZ(Centro, Centro, Raio));
        }

        [Test]
        public void InsideCircle_IgnoraEixoY()
        {
            // Mesma posição no plano, Y bem diferente → ainda dentro (Y é ignorado).
            Vector3 alto = new Vector3(0f, 50f, 26f);
            Assert.IsTrue(Geometry2D.InsideCircleXZ(alto, Centro, Raio));
        }

        [Test]
        public void InsideCircle_LogoAlemDoRaio_EstaFora()
        {
            Vector3 fora = new Vector3(0f, 0f, 26f + Raio + 0.1f);
            Assert.IsFalse(Geometry2D.InsideCircleXZ(fora, Centro, Raio));
        }

        [Test]
        public void InsideCircle_ExatamenteNaBorda_EstaFora()
        {
            // Borda estrita: distância == raio → fora (comportamento fixado).
            Vector3 borda = new Vector3(0f, 0f, 26f + Raio);
            Assert.IsFalse(Geometry2D.InsideCircleXZ(borda, Centro, Raio));
        }

        [Test]
        public void InsideCircle_Origem_EstaForaDaArena()
        {
            // O hub (origem) fica a 26 m da arena → bem fora do raio 9.
            Assert.IsFalse(Geometry2D.InsideCircleXZ(Vector3.zero, Centro, Raio));
        }

        [Test]
        public void InsideCircle_RaioNaoPositivo_SempreFalso()
        {
            Assert.IsFalse(Geometry2D.InsideCircleXZ(Centro, Centro, 0f));
            Assert.IsFalse(Geometry2D.InsideCircleXZ(Centro, Centro, -3f));
        }

        [Test]
        public void PlanarDistanceSqr_IgnoraY()
        {
            float d = Geometry2D.PlanarDistanceSqr(new Vector3(3f, 99f, 0f), new Vector3(0f, -10f, 4f));
            Assert.AreEqual(25f, d, 0.0001f); // 3-4-5 → 5² = 25, Y ignorado
        }
    }
}

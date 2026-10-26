using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Gameplay.Combat;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Testes da seleção pura de alvo (M-NET-6 P2): a IA mira o jogador MAIS PERTO,
    /// em distância PLANAR (altura não conta — consistente com o movimento da IA).
    /// </summary>
    public class TargetMathTests
    {
        [Test]
        public void ListaVaziaOuNula_RetornaMenosUm()
        {
            Assert.AreEqual(-1, TargetMath.NearestIndex(Vector3.zero, null));
            Assert.AreEqual(-1, TargetMath.NearestIndex(Vector3.zero, new Vector3[0]));
        }

        [Test]
        public void UnicoCandidato_RetornaZero()
        {
            var candidates = new[] { new Vector3(50f, 0f, 50f) };
            Assert.AreEqual(0, TargetMath.NearestIndex(Vector3.zero, candidates));
        }

        [Test]
        public void EscolheOMaisPerto()
        {
            var candidates = new[]
            {
                new Vector3(10f, 0f, 0f),  // longe
                new Vector3(2f, 0f, 1f),   // perto ← este
                new Vector3(0f, 0f, 8f),   // médio
            };
            Assert.AreEqual(1, TargetMath.NearestIndex(Vector3.zero, candidates));
        }

        [Test]
        public void AlturaNaoConta_DistanciaEhPlanar()
        {
            var candidates = new[]
            {
                new Vector3(3f, 0f, 0f),    // 3 m no plano, no chão
                new Vector3(1f, 100f, 0f),  // 1 m no plano, mas lá no alto ← ainda é o mais perto
            };
            Assert.AreEqual(1, TargetMath.NearestIndex(Vector3.zero, candidates));
        }

        [Test]
        public void Empate_FicaComOPrimeiro()
        {
            var candidates = new[]
            {
                new Vector3(5f, 0f, 0f),   // mesma distância ← o primeiro vence
                new Vector3(-5f, 0f, 0f),  // mesma distância
            };
            Assert.AreEqual(0, TargetMath.NearestIndex(Vector3.zero, candidates));
        }
    }
}

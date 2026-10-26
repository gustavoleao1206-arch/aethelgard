using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Gameplay.Building;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa o encaixe na grade (lógica pura, EditMode). Ver BuildMath.Snap.
    /// </summary>
    public class BuildMathTests
    {
        [Test]
        public void Snap_ArredondaXZ_PreservaY()
        {
            Vector3 r = BuildMath.Snap(new Vector3(0.4f, 5f, 0.6f), 1f);
            Assert.AreEqual(0f, r.x, 0.0001f);
            Assert.AreEqual(5f, r.y, 0.0001f); // Y preservado
            Assert.AreEqual(1f, r.z, 0.0001f);
        }

        [Test]
        public void Snap_ParaCelulaMaisProxima()
        {
            Vector3 r = BuildMath.Snap(new Vector3(2.7f, 0f, -1.2f), 1f);
            Assert.AreEqual(3f, r.x, 0.0001f);
            Assert.AreEqual(-1f, r.z, 0.0001f);
        }

        [Test]
        public void Snap_RespeitaTamanhoDaCelula()
        {
            Vector3 r = BuildMath.Snap(new Vector3(1.1f, 0f, 0.2f), 0.5f);
            Assert.AreEqual(1.0f, r.x, 0.0001f); // 1.1/0.5=2.2 → 2 → 1.0
            Assert.AreEqual(0.0f, r.z, 0.0001f); // 0.2/0.5=0.4 → 0 → 0.0
        }

        [Test]
        public void Snap_CelulaInvalida_RetornaPosicaoOriginal()
        {
            Vector3 original = new Vector3(1.37f, 2f, 9.81f);
            Vector3 r = BuildMath.Snap(original, 0f);
            Assert.AreEqual(original, r);
        }

        // Documenta o comportamento do Mathf.Round no limite .5 (arredondamento
        // "banker's": vai para o PAR mais próximo). Para grade, o lado do empate é
        // irrelevante ao gameplay, mas fixamos para não haver surpresa.
        [Test]
        public void Snap_NoLimite_Meio_UsaArredondamentoParaPar()
        {
            Assert.AreEqual(0f, BuildMath.Snap(new Vector3(0.5f, 0f, 0f), 1f).x, 0.0001f); // 0.5 → 0 (par)
            Assert.AreEqual(2f, BuildMath.Snap(new Vector3(1.5f, 0f, 0f), 1f).x, 0.0001f); // 1.5 → 2 (par)
            Assert.AreEqual(2f, BuildMath.Snap(new Vector3(2.5f, 0f, 0f), 1f).x, 0.0001f); // 2.5 → 2 (par)
        }
    }
}

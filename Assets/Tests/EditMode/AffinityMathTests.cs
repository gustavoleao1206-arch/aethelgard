using NUnit.Framework;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a lógica pura da afinidade (EditMode): opostos, subir/descer ao usar,
    /// a regra dos 60% (só especialista derruba os demais) e o bônus do especialista.
    /// </summary>
    public class AffinityMathTests
    {
        // Cria um vetor de afinidades já com valores por elemento.
        private static float[] Affinities(float metal = 0, float eter = 0, float fogo = 0,
            float agua = 0, float terra = 0, float planta = 0)
        {
            return new[] { metal, eter, fogo, agua, terra, planta };
        }

        [Test]
        public void Opostos_SaoSimetricos()
        {
            Assert.AreEqual(ElementType.Agua, AffinityMath.OppositeOf(ElementType.Fogo));
            Assert.AreEqual(ElementType.Fogo, AffinityMath.OppositeOf(ElementType.Agua));
            Assert.AreEqual(ElementType.Eter, AffinityMath.OppositeOf(ElementType.Terra));
            Assert.AreEqual(ElementType.Terra, AffinityMath.OppositeOf(ElementType.Eter));
            Assert.AreEqual(ElementType.Planta, AffinityMath.OppositeOf(ElementType.Metal));
            Assert.AreEqual(ElementType.Metal, AffinityMath.OppositeOf(ElementType.Planta));
        }

        [Test]
        public void Usar_SobeOElemento_EDerrubaOOposto()
        {
            float[] aff = Affinities(fogo: 20f, agua: 20f);
            AffinityMath.ApplyUse(aff, ElementType.Fogo);

            Assert.AreEqual(20f + AffinityMath.RiseRate, aff[(int)ElementType.Fogo], 0.001f);
            Assert.AreEqual(20f - AffinityMath.OppositeFallRate, aff[(int)ElementType.Agua], 0.001f);
        }

        [Test]
        public void Afinidade_NaoPassaDe100_NemAbaixoDeZero()
        {
            float[] aff = Affinities(fogo: 98f, agua: 3f);
            AffinityMath.ApplyUse(aff, ElementType.Fogo);
            Assert.AreEqual(100f, aff[(int)ElementType.Fogo], 0.001f);  // capado no topo
            Assert.AreEqual(0f, aff[(int)ElementType.Agua], 0.001f);    // capado no piso
        }

        [Test]
        public void AbaixoDe60_NaoDerrubaOsDemais()
        {
            // Fogo em 30 → após uso fica 40 (< 60): só o oposto (Água) cai; Terra fica igual.
            float[] aff = Affinities(fogo: 30f, agua: 50f, terra: 50f);
            AffinityMath.ApplyUse(aff, ElementType.Fogo);
            Assert.AreEqual(50f, aff[(int)ElementType.Terra], 0.001f); // inalterado
        }

        [Test]
        public void AcimaDe60_DerrubaOsDemais_MasNaoOUsadoNemOOposto()
        {
            // Fogo em 70 → após uso fica 80 (> 60): os demais (Terra) caem; Água (oposto) cai pela regra do oposto.
            float[] aff = Affinities(fogo: 70f, agua: 50f, terra: 50f, metal: 50f);
            AffinityMath.ApplyUse(aff, ElementType.Fogo);

            Assert.AreEqual(80f, aff[(int)ElementType.Fogo], 0.001f);
            Assert.AreEqual(50f - AffinityMath.SpreadFallRate, aff[(int)ElementType.Terra], 0.001f);
            Assert.AreEqual(50f - AffinityMath.SpreadFallRate, aff[(int)ElementType.Metal], 0.001f);
            Assert.AreEqual(50f - AffinityMath.OppositeFallRate, aff[(int)ElementType.Agua], 0.001f);
        }

        [Test]
        public void BonusEspecialista_ZeroAbaixoDoLimiar_CrescenteAcima()
        {
            Assert.AreEqual(0f, AffinityMath.SpecialistBonus(60f), 0.001f);
            Assert.AreEqual(0f, AffinityMath.SpecialistBonus(40f), 0.001f);
            Assert.AreEqual(AffinityMath.MaxSpecialistBonus, AffinityMath.SpecialistBonus(100f), 0.001f);
            // Em 80% (meio do caminho 60→100) → metade do bônus máximo.
            Assert.AreEqual(AffinityMath.MaxSpecialistBonus * 0.5f, AffinityMath.SpecialistBonus(80f), 0.001f);
        }
    }
}

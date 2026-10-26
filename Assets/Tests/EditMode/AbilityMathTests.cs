using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a evolução de habilidades (lógica pura, EditMode): curva de XP, soma
    /// de bônus de especialização por eixo e os tetos (caps) de aumento/redução.
    /// </summary>
    public class AbilityMathTests
    {
        [Test]
        public void CurvaDeXp_CresceComONivel()
        {
            // 1→2 custa menos que 2→3 (curva crescente).
            Assert.Less(AbilityMath.XpForLevel(1), AbilityMath.XpForLevel(2));
            Assert.AreEqual(50, AbilityMath.XpForLevel(1));
        }

        [Test]
        public void AxisBonus_SomaApenasOEixoCorreto()
        {
            var def = ScriptableObject.CreateInstance<AbilityDefinition>();
            def.specializations = new[]
            {
                new SpecializationNode { axis = SpecAxis.Damage, valuePerLevel = 0.15f, maxLevel = 3 },
                new SpecializationNode { axis = SpecAxis.Cooldown, valuePerLevel = 0.10f, maxLevel = 2 }
            };
            int[] levels = { 2, 1 }; // 2 níveis de dano, 1 de recarga

            float dmg = AbilityMath.AxisBonusFraction(def, levels, SpecAxis.Damage);
            float cd = AbilityMath.AxisBonusFraction(def, levels, SpecAxis.Cooldown);

            Assert.AreEqual(0.30f, dmg, 0.0001f); // 2 × 0.15
            Assert.AreEqual(0.10f, cd, 0.0001f);  // 1 × 0.10
            Object.DestroyImmediate(def);
        }

        [Test]
        public void NoAssinatura_NaoEntraNoEixoGenerico()
        {
            var def = ScriptableObject.CreateInstance<AbilityDefinition>();
            def.specializations = new[]
            {
                new SpecializationNode { isSignature = true, axis = SpecAxis.Damage, valuePerLevel = 0.5f, maxLevel = 1 }
            };
            float dmg = AbilityMath.AxisBonusFraction(def, new[] { 1 }, SpecAxis.Damage);
            Assert.AreEqual(0f, dmg, 0.0001f);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void Aumento_RespeitaOTeto()
        {
            // Bônus enorme (10x) é capado em MaxIncreaseFraction.
            float result = AbilityMath.FinalIncrease(100f, 10f);
            float expected = 100f * (1f + AbilityMath.MaxIncreaseFraction);
            Assert.AreEqual(expected, result, 0.0001f);
        }

        [Test]
        public void Reducao_RespeitaOTeto()
        {
            // Redução enorme não zera o valor: capada em MaxReductionFraction.
            float result = AbilityMath.FinalReduction(1f, 10f);
            float expected = 1f * (1f - AbilityMath.MaxReductionFraction);
            Assert.AreEqual(expected, result, 0.0001f);
        }

        [Test]
        public void Investir_SobeNivelDoNo_EConsomePontos()
        {
            var def = ScriptableObject.CreateInstance<AbilityDefinition>();
            def.baseDamage = 100f;
            def.specializations = new[]
            {
                new SpecializationNode { axis = SpecAxis.Damage, valuePerLevel = 0.2f, pointCost = 1, maxLevel = 2 }
            };
            var inst = new AbilityInstance(def, IncorporationLayer.Vestigio);

            // Sem pontos, não investe.
            Assert.IsFalse(inst.TryInvest(0, out _));

            // Ganha XP suficiente para ao menos 1 nível (100 XP = nível 2).
            inst.AddXp(100);
            Assert.AreEqual(2, inst.Level);
            Assert.AreEqual(1, inst.PointsAvailable);

            Assert.IsTrue(inst.TryInvest(0, out _));
            Assert.AreEqual(0, inst.PointsAvailable);
            Assert.AreEqual(120f, inst.FinalDamage, 0.001f); // 100 × (1 + 0.2)

            // Respec devolve o ponto e zera o nó.
            inst.Respec();
            Assert.AreEqual(1, inst.PointsAvailable);
            Assert.AreEqual(100f, inst.FinalDamage, 0.001f);

            Object.DestroyImmediate(def);
        }
    }
}

using System;
using UnityEngine;
using JogoDaTrupe.Data;
using JogoDaTrupe.Core.Timing;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// ESTADO de runtime de uma habilidade equipada: aponta para a Definição
    /// (ficha técnica) e guarda o que muda em jogo — camada incorporada, XP/nível,
    /// especializações escolhidas e o controle de recarga. Vai para o save no
    /// futuro (regra Definição ≠ Estado).
    ///
    /// Os stats FINAIS (dano, recarga...) saem da Definição temperada pelas
    /// especializações, calculados em AbilityMath (puro e testado).
    /// </summary>
    [Serializable]
    public sealed class AbilityInstance
    {
        public AbilityDefinition Definition;
        public IncorporationLayer Layer;

        // Progressão por uso (M3).
        public int Level = 1;
        public int Xp = 0;
        public int PointsAvailable = 0;

        /// <summary>Níveis investidos em cada nó de especialização (paralelo a Definition.specializations).</summary>
        public int[] NodeLevels;

        // Recarga: instante do último uso (runtime; não serializar a recarga em si).
        [NonSerialized] private float _lastUsedTime = -999f;

        public AbilityInstance(AbilityDefinition definition, IncorporationLayer layer)
        {
            Definition = definition;
            Layer = layer;
            int nodeCount = definition != null && definition.specializations != null
                ? definition.specializations.Length : 0;
            NodeLevels = new int[nodeCount];
        }

        public string FamilyId => Definition != null ? Definition.familyId : null;

        // ---- Stats finais (Definição × especialização, capados) ----

        public float FinalDamage =>
            AbilityMath.FinalIncrease(Definition.baseDamage,
                AbilityMath.AxisBonusFraction(Definition, NodeLevels, SpecAxis.Damage));

        public float FinalCooldown =>
            AbilityMath.FinalReduction(Definition.baseCooldown,
                AbilityMath.AxisBonusFraction(Definition, NodeLevels, SpecAxis.Cooldown));

        public float FinalRange =>
            AbilityMath.FinalIncrease(Definition.baseRange,
                AbilityMath.AxisBonusFraction(Definition, NodeLevels, SpecAxis.Range));

        public float FinalRadius =>
            AbilityMath.FinalIncrease(Definition.baseRadius,
                AbilityMath.AxisBonusFraction(Definition, NodeLevels, SpecAxis.Area));

        // ---- Recarga ----

        public bool IsReady => Definition != null && GameTime.Time - _lastUsedTime >= FinalCooldown;

        public float CooldownFraction
        {
            get
            {
                float cd = FinalCooldown;
                if (cd <= 0f) return 1f;
                return Mathf.Clamp01((GameTime.Time - _lastUsedTime) / cd);
            }
        }

        public void MarkUsed() => _lastUsedTime = GameTime.Time;

        // ---- Progressão ----

        /// <summary>Ganha XP (ao usar). Sobe de nível e concede pontos. Retorna true se subiu de nível.</summary>
        public bool AddXp(int amount)
        {
            if (amount <= 0) return false;
            Xp += amount;

            bool leveled = false;
            while (Xp >= AbilityMath.XpForLevel(Level))
            {
                Xp -= AbilityMath.XpForLevel(Level);
                Level++;
                PointsAvailable++;
                leveled = true;
            }
            return leveled;
        }

        /// <summary>Investe um ponto num nó de especialização, se houver ponto e o nó não estiver no teto.</summary>
        public bool TryInvest(int nodeIndex, out string reason)
        {
            if (Definition == null || Definition.specializations == null ||
                nodeIndex < 0 || nodeIndex >= Definition.specializations.Length)
            {
                reason = "Nó inválido.";
                return false;
            }

            SpecializationNode node = Definition.specializations[nodeIndex];
            if (NodeLevels[nodeIndex] >= node.maxLevel)
            {
                reason = "Nó já no máximo.";
                return false;
            }
            if (PointsAvailable < node.pointCost)
            {
                reason = "Pontos insuficientes.";
                return false;
            }

            PointsAvailable -= node.pointCost;
            NodeLevels[nodeIndex]++;
            reason = null;
            return true;
        }

        /// <summary>
        /// Reverte todas as especializações, devolvendo os pontos (respec no Altar).
        /// O CUSTO (🎨 2 Pedra, M-NET-8) é cobrado por quem chama —
        /// <c>AbilityLoadout.TryRespec</c> debita o inventário ANTES de reverter.
        /// </summary>
        public void Respec()
        {
            if (Definition == null || Definition.specializations == null) return;

            int count = Mathf.Min(Definition.specializations.Length, NodeLevels.Length);
            for (int i = 0; i < count; i++)
            {
                PointsAvailable += NodeLevels[i] * Definition.specializations[i].pointCost;
                NodeLevels[i] = 0;
            }
        }
    }
}

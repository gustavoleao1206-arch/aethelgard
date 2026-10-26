using System;

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// Um nó de especialização de uma habilidade (Definição, não Estado). Ao subir
    /// de nível, o jogador gasta pontos para aplicar nós — moldando a habilidade
    /// (+ dano, + alcance, − recarga...). Usado na evolução por XP (M3).
    ///
    /// É um struct serializável para aparecer como lista no Inspector da
    /// AbilityDefinition, sem virar um asset separado.
    /// </summary>
    [Serializable]
    public struct SpecializationNode
    {
        public string label;          // nome legível ("Presas afiadas")

        public bool isSignature;      // nó "assinatura" único da habilidade?
        public SpecAxis axis;         // eixo genérico afetado (ignorado se assinatura)

        public float valuePerLevel;   // quanto soma por nível investido (ex.: +0.1 = +10%)
        public int pointCost;         // custo em pontos de especialização
        public int maxLevel;          // teto de níveis deste nó (0 = sem investimento)
    }
}

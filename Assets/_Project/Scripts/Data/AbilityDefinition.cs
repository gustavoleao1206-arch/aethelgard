using UnityEngine;

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// "Ficha técnica" de uma habilidade incorporável (Definição, não Estado).
    /// O Diretor cria/ajusta estas no Editor sem programar. O ESTADO de uma
    /// habilidade equipada (XP, nível, especializações escolhidas, recarga) vive
    /// num AbilityInstance de runtime — nunca aqui (regra Definição ≠ Estado).
    ///
    /// No Vertical Slice o único efeito implementado é um "golpe à frente" (rajada
    /// de dano), descrito pelos campos de Efeito. Outros tipos de efeito entram
    /// depois — por isso os campos são genéricos e o efeito concreto fica no
    /// AbilityRunner (Gameplay), não na ficha.
    /// </summary>
    [CreateAssetMenu(fileName = "Ability", menuName = "Jogo da Trupe/Habilidade")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        [Header("Identidade")]
        public string abilityId = "ability";
        public string displayName = "Habilidade";

        [Tooltip("Família compartilhada (ex.: 'Fera'). A regra 'uma habilidade por criatura' do loadout usa este id; variantes da mesma família escalam por tier.")]
        public string familyId = "Fera";

        public ElementType element = ElementType.Terra;
        public AbilityTier tier = AbilityTier.TierI;

        [Header("Efeito (golpe à frente — slice)")]
        [Tooltip("Tipo de efeito: golpe de dano (Mordida) ou controle que prende o alvo (Cipó).")]
        public AbilityEffect effect = AbilityEffect.DamageBurst;
        public float baseDamage = 40f;
        public float baseRange = 2.5f;   // até onde alcança à frente
        public float baseRadius = 1.2f;  // raio da esfera de acerto
        public float baseCooldown = 1f;  // recarga em segundos

        [Tooltip("Só para o efeito Snare (Cipó): por quantos segundos o alvo fica preso.")]
        public float snareSeconds = 2f;

        [Header("Requisitos de atributo (M4)")]
        public AttributeType requiredAttribute = AttributeType.Forca;
        [Tooltip("Valor mínimo do atributo para usar a habilidade sem penalidade. 0 = sem requisito (ainda não exercitado no slice).")]
        public int requiredAttributeValue = 0;

        [Header("Especialização (M3)")]
        public SpecializationNode[] specializations;
    }
}

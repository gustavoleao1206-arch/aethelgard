using UnityEngine;

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// "Ficha técnica" de um inimigo (Definição, não Estado). Permite ao Diretor
    /// ajustar atributos no Editor sem programar. Cada inimigo do mundo aponta
    /// para uma destas definições.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Jogo da Trupe/Inimigo")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Tooltip("Id estável do inimigo (usado no save e na rede para reencontrar a definição). Não mudar depois de criado.")]
        public string enemyId = "enemy";

        public string displayName = "Inimigo";
        public Color color = new Color(0.85f, 0.25f, 0.2f);

        [Header("Atributos")]
        public float maxHealth = 100f;
        public float moveSpeed = 4f;
        public float damage = 15f;

        [Header("Alcances")]
        public float attackRange = 2.2f;
        public float detectRange = 16f;
        public float attackCooldown = 1f;

        [Tooltip("Telegraph: tempo de 'preparar o golpe' antes do dano sair. Dá janela de reação — sair do alcance durante o windup ANULA o hit. É o peso/ritmo do combate (não a dificuldade).")]
        public float attackWindup = 0.45f;

        [Header("Comportamento")]
        public EnemyTrick trick = EnemyTrick.Berserker;

        [Header("Incorporação (Eco)")]
        [Tooltip("Habilidade que esta criatura concede ao ser derrotada (o Eco). Se vazio, a criatura não deixa Eco.")]
        public AbilityDefinition grantedAbility;

        [Tooltip("Nº de derrotas necessárias para liberar cada camada: [0]=Vestígio, [1]=Ressonância, [2]=Núcleo. A Maestria é determinística (sem sorte) — ver MasteryMath.")]
        public int[] masteryThresholds = { 1, 3, 6 };
    }
}

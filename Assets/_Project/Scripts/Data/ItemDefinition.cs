using UnityEngine;

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// "Ficha técnica" de um item (Definição, não Estado). O Diretor cria/ajusta no
    /// Editor sem programar. Quanto o jogador TEM de cada item é Estado de runtime
    /// (no Inventory), nunca aqui (regra Definição ≠ Estado).
    /// </summary>
    [CreateAssetMenu(fileName = "Item", menuName = "Jogo da Trupe/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Tooltip("Id estável do item (usado no save para reencontrar a definição). Não mudar depois de criado.")]
        public string itemId = "item";

        public string displayName = "Item";
        public ItemCategory category = ItemCategory.Recurso;

        [Tooltip("Quantos cabem numa pilha (slot). 1 = não empilha.")]
        [Min(1)] public int maxStack = 99;

        [Tooltip("Cor de exibição no slice (no lugar de um ícone).")]
        public Color color = new Color(0.8f, 0.7f, 0.4f);
    }
}

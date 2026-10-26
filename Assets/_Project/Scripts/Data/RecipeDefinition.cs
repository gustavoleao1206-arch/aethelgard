using UnityEngine;

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// "Ficha técnica" de uma receita de crafting (Definição, não Estado). O Diretor
    /// cria/ajusta no Editor sem programar: ingredientes (itens + quantidades) e o
    /// resultado. A lógica de craftar fica fora daqui (CraftingMath, na Simulação).
    /// </summary>
    [CreateAssetMenu(fileName = "Recipe", menuName = "Jogo da Trupe/Receita")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        [Tooltip("Identificador ESTÁVEL da receita (usado pelo save e pela rede — o ScriptableObject não viaja, o id sim). Não mudar depois de publicado.")]
        public string recipeId;

        public string displayName = "Receita";

        [Tooltip("Itens consumidos para craftar.")]
        public RecipeIngredient[] ingredients;

        [Header("Resultado")]
        public ItemDefinition output;
        [Min(1)] public int outputAmount = 1;
    }
}

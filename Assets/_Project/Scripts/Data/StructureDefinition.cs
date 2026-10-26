using UnityEngine;

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// "Ficha técnica" de uma estrutura construível (Definição, não Estado). O
    /// Diretor cria/ajusta no Editor: custo (itens), tamanho e cor. O custo reusa
    /// RecipeIngredient (mesmo par item+quantidade do crafting).
    ///
    /// No slice a estrutura é um bloco simples (primitivo) com este tamanho/cor;
    /// arte de verdade entra depois.
    /// </summary>
    [CreateAssetMenu(fileName = "Structure", menuName = "Jogo da Trupe/Estrutura")]
    public sealed class StructureDefinition : ScriptableObject
    {
        [Tooltip("Identificador ESTÁVEL da estrutura (usado pelo save e pela rede — o ScriptableObject não viaja, o id sim). Não mudar depois de publicado.")]
        public string structureId;

        public string displayName = "Estrutura";

        [Tooltip("Itens consumidos para construir.")]
        public RecipeIngredient[] cost;

        [Header("Visual / encaixe")]
        public Vector3 size = Vector3.one;
        public Color color = new Color(0.7f, 0.6f, 0.45f);

        [Tooltip("Altura do CENTRO quando posicionada (a base encosta no chão). Ex.: parede alta tem baseY ~ altura/2.")]
        public float baseY = 0.5f;
    }
}

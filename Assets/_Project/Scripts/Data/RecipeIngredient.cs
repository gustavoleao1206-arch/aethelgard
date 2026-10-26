using System;

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// Um ingrediente de uma receita: qual item e quantos. Struct serializável para
    /// aparecer como lista no Inspector da RecipeDefinition (sem virar asset).
    /// </summary>
    [Serializable]
    public struct RecipeIngredient
    {
        public ItemDefinition item;
        public int amount;
    }
}

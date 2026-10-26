namespace JogoDaTrupe.Data
{
    /// <summary>
    /// Categoria de um item, para organização/filtros futuros (UI, crafting).
    /// Mantida simples no slice; novas categorias entram conforme a necessidade.
    /// </summary>
    public enum ItemCategory
    {
        Recurso,      // matéria-prima coletada no mundo (madeira, pedra...)
        Material,     // processado/intermediário
        Consumivel,   // poções, comida...
        Equipamento   // ferramentas, peças
    }
}

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// Os 6 atributos do personagem. O jogador sobe de nível e distribui pontos
    /// entre eles. Habilidades poderosas exigem um mínimo de algum atributo;
    /// faltando, há penalidade suave (M4). Ver 01_GameDesign/Incorporacao.md (seção 8).
    /// </summary>
    public enum AttributeType
    {
        HP,
        Forca,
        Inteligencia,
        Resistencia,
        Destreza,
        Vontade
    }
}

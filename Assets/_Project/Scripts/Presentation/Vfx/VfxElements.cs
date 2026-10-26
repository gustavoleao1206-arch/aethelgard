using UnityEngine;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Presentation.Vfx
{
    /// <summary>
    /// Paleta dos VFX por ELEMENTO (fase de arte). Fonte única da cor de cada elemento
    /// para as partículas de habilidade, e a cor da Ressonância (absorção de Eco).
    /// </summary>
    public static class VfxElements
    {
        /// <summary>Ciano da Ressonância — cor da absorção de Eco (casa com os acentos do hub).</summary>
        public static readonly Color Resonance = new Color(0.45f, 0.9f, 1.0f);

        /// <summary>Cor viva de cada elemento, para o burst da habilidade.</summary>
        public static Color ForElement(ElementType element)
        {
            switch (element)
            {
                case ElementType.Metal:  return new Color(0.78f, 0.80f, 0.88f); // prata
                case ElementType.Eter:   return new Color(0.62f, 0.35f, 0.95f); // roxo etéreo
                case ElementType.Fogo:   return new Color(1.00f, 0.45f, 0.12f); // laranja-fogo
                case ElementType.Agua:   return new Color(0.25f, 0.62f, 1.00f); // azul
                case ElementType.Terra:  return new Color(0.70f, 0.52f, 0.28f); // terroso
                case ElementType.Planta: return new Color(0.35f, 0.85f, 0.40f); // verde
                default:                 return Color.white;
            }
        }
    }
}

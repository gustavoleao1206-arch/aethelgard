using UnityEngine;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// Paleta ÚNICA da HUD (tema dark-fantasy + ciano "Ressonância"). Centralizar as
    /// cores aqui deixa todas as telas coerentes e fáceis de reajustar num lugar só —
    /// quando o Diretor pedir "mais escuro" ou "ciano mais forte", muda aqui e vale pra tudo.
    /// </summary>
    public static class UiTheme
    {
        /// <summary>Fundo dos cartões/painéis (translúcido, quase preto azulado).</summary>
        public static readonly Color Card = new Color(0.055f, 0.067f, 0.086f, 0.82f);
        /// <summary>Trilho vazio das barras (o "canal" atrás do preenchimento).</summary>
        public static readonly Color Track = new Color(0.10f, 0.12f, 0.16f, 0.95f);
        /// <summary>Sombra projetada atrás dos cartões (profundidade).</summary>
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.45f);

        /// <summary>Ciano da Ressonância — o acento da identidade visual do jogo.</summary>
        public static readonly Color Resonance = new Color(0.25f, 0.78f, 1f, 1f);

        /// <summary>Vida (carmesim vivo).</summary>
        public static readonly Color Hp = new Color(0.886f, 0.231f, 0.306f, 1f);
        /// <summary>Rastro de dano da vida (dourado — o "chip" que desce atrasado).</summary>
        public static readonly Color HpTrail = new Color(0.98f, 0.82f, 0.36f, 1f);

        /// <summary>Estamina cheia (ciano Ressonância).</summary>
        public static readonly Color Stamina = new Color(0.25f, 0.78f, 1f, 1f);
        /// <summary>Estamina zerada (laranja de alerta).</summary>
        public static readonly Color StaminaEmpty = new Color(0.95f, 0.5f, 0.22f, 1f);

        /// <summary>Texto principal.</summary>
        public static readonly Color Text = new Color(0.96f, 0.97f, 1f, 1f);
        /// <summary>Texto secundário/rótulos.</summary>
        public static readonly Color TextDim = new Color(0.72f, 0.79f, 0.88f, 1f);
    }
}

using UnityEngine;

namespace JogoDaTrupe.Core.World
{
    /// <summary>
    /// As TRILHAS do mundo como polilinhas no plano (Vector2 = (x, z)). Fonte ÚNICA para duas
    /// coisas que precisam concordar: o CORREDOR PLANO no relevo (<see cref="WorldHeight"/>,
    /// máscara de corredor) e a FITA DE TRILHA visível (PathMeshBuilder, M-TERR-4).
    ///
    /// Coordenadas duplicadas de propósito em relação ao <see cref="MapLayout"/>: o MapLayout lê o
    /// WorldHeight (spawns assentados) e o WorldHeight lê daqui — se isto lesse o MapLayout, a
    /// inicialização estática viraria um CICLO (RuinTrail ainda nulo ao ser lido). O
    /// WorldPathsTests garante que os números continuam iguais aos do MapLayout.
    /// </summary>
    public static class WorldPaths
    {
        /// <summary>Largura TOTAL do corredor plano (m). A fita visível usa um pouco menos.</summary>
        public const float Width = WorldHeight.CorridorWidth;

        public static readonly Vector2[] HubToColeta    = { new Vector2(0f, 0f), new Vector2(-11.3f,  11.3f) };
        public static readonly Vector2[] HubToCombate   = { new Vector2(0f, 0f), new Vector2( 11.3f,  11.3f) };
        public static readonly Vector2[] HubToAutomacao = { new Vector2(0f, 0f), new Vector2(-11.3f, -11.3f) };
        public static readonly Vector2[] HubToCriatura  = { new Vector2(0f, 0f), new Vector2( 11.3f, -11.3f) };

        // Trilha de ruínas: hub → os 5 pontos do MapLayout.RuinTrail → centro da arena do boss.
        public static readonly Vector2[] RuinTrail =
        {
            new Vector2(0f, 0f), new Vector2(-2.5f, 7.5f), new Vector2(3f, 9f), new Vector2(-1.5f, 15f),
            new Vector2(2.2f, 19f), new Vector2(-2f, 22f), new Vector2(0f, 26f),
        };

        // Estradas das VILAS (M-TERR-5): retas do hub aos pads a oeste e a leste (±VillageCenterX, 0).
        // Passam entre os pares de zonas (z = ±11.3, raio 5): folga de ~5 m de floresta de cada lado.
        public static readonly Vector2[] HubToVilaOeste = { new Vector2(0f, 0f), new Vector2(-23f, 0f) };
        public static readonly Vector2[] HubToVilaLeste = { new Vector2(0f, 0f), new Vector2( 23f, 0f) };

        /// <summary>Todas as trilhas (ordem: 4 raios do hub + a trilha de ruínas + 2 estradas das vilas).</summary>
        public static readonly Vector2[][] Polylines =
        {
            HubToColeta, HubToCombate, HubToAutomacao, HubToCriatura, RuinTrail, HubToVilaOeste, HubToVilaLeste,
        };
    }
}

using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// WorldPaths duplica coordenadas do MapLayout de propósito (evita um ciclo de inicialização
    /// estática). Estes testes são o que impede as duas fontes de se desencontrarem — e provam
    /// que toda trilha corre sobre corredor plano no relevo.
    /// </summary>
    public class WorldPathsTests
    {
        private static void AssertXZ(Vector2 p, Vector3 expected)
        {
            Assert.AreEqual(expected.x, p.x, 1e-4f, "x");
            Assert.AreEqual(expected.z, p.y, 1e-4f, "z (Vector2.y)");
        }

        [Test]
        public void RuinTrail_MatchesMapLayout_WithHubAndArenaAsEndpoints()
        {
            Vector2[] trail = WorldPaths.RuinTrail;
            Assert.AreEqual(MapLayout.RuinTrail.Length + 2, trail.Length);
            AssertXZ(trail[0], MapLayout.Hub);
            for (int i = 0; i < MapLayout.RuinTrail.Length; i++) AssertXZ(trail[i + 1], MapLayout.RuinTrail[i]);
            AssertXZ(trail[trail.Length - 1], MapLayout.BossArena);
        }

        [Test]
        public void Spokes_StartAtHubAndEndAtZoneCenters()
        {
            AssertXZ(WorldPaths.HubToColeta[0], MapLayout.Hub);
            AssertXZ(WorldPaths.HubToColeta[1], MapLayout.ZoneColeta);
            AssertXZ(WorldPaths.HubToCombate[1], MapLayout.ZoneCombate);
            AssertXZ(WorldPaths.HubToAutomacao[1], MapLayout.ZoneAutomacao);
            AssertXZ(WorldPaths.HubToCriatura[1], MapLayout.ZoneCriatura);
        }

        [Test]
        public void VillageRoads_GoStraightFromHubToEachVillageCenter()
        {
            // M-TERR-5: as estradas das vilas saem do hub e terminam no centro de cada pad (MapLayout).
            Assert.AreEqual(2, WorldPaths.HubToVilaOeste.Length);
            Assert.AreEqual(2, WorldPaths.HubToVilaLeste.Length);
            AssertXZ(WorldPaths.HubToVilaOeste[0], MapLayout.Hub);
            AssertXZ(WorldPaths.HubToVilaOeste[1], MapLayout.VillageWest);
            AssertXZ(WorldPaths.HubToVilaLeste[0], MapLayout.Hub);
            AssertXZ(WorldPaths.HubToVilaLeste[1], MapLayout.VillageEast);
            Assert.AreEqual(7, WorldPaths.Polylines.Length, "4 raios + ruínas + 2 estradas das vilas");
        }

        [Test]
        public void Width_IsTheCorridorWidth()
        {
            Assert.AreEqual(WorldHeight.CorridorWidth, WorldPaths.Width);
        }

        [Test]
        public void EveryPolylinePoint_LiesOnFlatGround()
        {
            // Os corredores achatam o relevo ao longo das polilinhas: em cada vértice a altura é 0.
            foreach (Vector2[] path in WorldPaths.Polylines)
                foreach (Vector2 p in path)
                    Assert.That(WorldHeight.SampleHeight(p.x, p.y), Is.EqualTo(0f).Within(0.05f),
                        "Ponto (" + p.x + ", " + p.y + ") deveria estar num corredor plano.");
        }
    }
}

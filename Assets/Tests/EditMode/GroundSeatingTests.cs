using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Os pontos da fonte única (MapLayout) nascem ASSENTADOS no relevo: a altura vem do mesmo
    /// WorldHeight que gera a malha do chão. Vale nos platôs (altura 0) e continuaria valendo
    /// se um platô mudasse de lugar — é o contrato que impede spawn flutuando ou enterrado.
    /// </summary>
    public class GroundSeatingTests
    {
        private static void AssertSeated(Vector3 p, float yOffset)
        {
            Assert.AreEqual(WorldHeight.SampleHeight(p.x, p.z) + yOffset, p.y, 1e-4f,
                "Ponto (" + p.x + ", " + p.z + ") deveria estar a " + yOffset + " m do chão.");
        }

        [Test]
        public void PlayerSpawns_AreSeatedOnTheGround()
        {
            foreach (Vector3 s in MapLayout.PlayerSpawns) AssertSeated(s, 1.1f);
        }

        [Test]
        public void EnemySpawns_AreSeatedOnTheGround()
        {
            foreach (Vector3 s in MapLayout.EnemySpawns) AssertSeated(s, 1f);
        }

        [Test]
        public void BossSpawn_IsSeatedOnTheArena()
        {
            AssertSeated(MapLayout.BossSpawn, 1.8f);
        }

        [Test]
        public void OnGround_OffsetsFromTheZoneCenterAndSeats()
        {
            Vector3 p = MapLayout.OnGround(MapLayout.ZoneCombate, 2f, -3f, 0.5f);
            Assert.AreEqual(MapLayout.ZoneCombate.x + 2f, p.x, 1e-5f);
            Assert.AreEqual(MapLayout.ZoneCombate.z - 3f, p.z, 1e-5f);
            AssertSeated(p, 0.5f);
        }

        [Test]
        public void OnGround_OutsidePlateaus_FollowsTheRelief()
        {
            // Setor nordeste (entre a zona de Combate, a arena e a vila leste): fora de todo
            // platô/corredor, a altura NÃO é 0 — o ponto tem que acompanhar o relevo (é exatamente
            // onde o decor flutuava/afundava). O setor leste em z≈0 virou o pad da vila (M-TERR-5).
            Vector3 p = MapLayout.OnGround(MapLayout.Hub, 26f, 20f, 0f);
            Assert.AreNotEqual(0f, p.y);
            AssertSeated(p, 0f);
        }
    }
}

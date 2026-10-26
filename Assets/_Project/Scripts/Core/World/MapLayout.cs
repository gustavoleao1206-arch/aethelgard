using UnityEngine;

namespace JogoDaTrupe.Core.World
{
    /// <summary>
    /// Fonte ÚNICA de coordenadas do mapa do Vertical Slice — a "Floresta da Ressonância".
    ///
    /// O mapa é um <b>hub central com zonas radiais</b> (decisão criativa do Diretor,
    /// 30/06/2026): o Altar fica no centro (hub) e cada sistema do jogo mora numa zona
    /// ao redor, com a arena do boss ao norte, depois de uma trilha de ruínas.
    ///
    /// Vive no Core (runtime, público) — e não na camada de Editor — porque os pontos de
    /// spawn (<see cref="PlayerSpawns"/>/<see cref="SpawnFor"/>) são lidos em RUNTIME pela
    /// aprovação de conexão do co-op (host decide onde cada jogador nasce). O construtor do
    /// mundo (ForestWorld) e o montador da cena (PlayerSceneSetup), ambos de Editor, leem
    /// daqui também — assim posições nunca ficam duplicadas/dessincronizadas (DRY).
    /// </summary>
    public static class MapLayout
    {
        // Centro do mundo: onde o jogador nasce e onde fica o Altar (hub).
        public static readonly Vector3 Hub = new Vector3(0f, 0f, 0f);

        // 4 zonas nos cantos (anel ao redor do hub), ~16 m do centro.
        public static readonly Vector3 ZoneColeta    = new Vector3(-11.3f, 0f,  11.3f); // NO — bosque de recursos
        public static readonly Vector3 ZoneCombate   = new Vector3( 11.3f, 0f,  11.3f); // NE — clareira dos inimigos
        public static readonly Vector3 ZoneAutomacao = new Vector3(-11.3f, 0f, -11.3f); // SO — clareira da máquina
        public static readonly Vector3 ZoneCriatura  = new Vector3( 11.3f, 0f, -11.3f); // SE — bosque da criatura

        // Arena do boss, ao norte, isolada por uma trilha de ruínas.
        public static readonly Vector3 BossArena = new Vector3(0f, 0f, 26f);

        // VILAS (M-TERR-5, decisão do Diretor 11/09/2026): dois pads planos a oeste e a leste, no anel
        // entre as zonas e a borda, ligados ao hub por estradas retas (WorldPaths). Raio = pad plano.
        public static readonly Vector3 VillageWest = new Vector3(-WorldHeight.VillageCenterX, 0f, 0f);
        public static readonly Vector3 VillageEast = new Vector3( WorldHeight.VillageCenterX, 0f, 0f);
        public const float VillageRadius = WorldHeight.VillageRadius;

        public const float HubRadius = 6f;        // raio da plataforma de pedra do hub
        public const float ArenaRadius = 6.5f;    // raio do piso corrompido da arena
        public const float ArenaPillarOffset = 0.8f;   // raio extra das colunas além do piso da arena
        public const float ArenaPillarHalfWidth = 0.6f; // metade da largura de uma coluna da arena
        public const float CliffDepth = 2.5f;     // profundidade radial dos penhascos do limite
        public const float BorderRadius = 38f;    // anel de penhascos que fecha o mapa (folga p/ a arena)
        public const float GroundHalfExtent = 44f; // meia-largura do chão (cobre o anel)

        /// <summary>Posição deslocada de um centro de zona (dx/dz no plano, y explícito). Use só onde o Y
        /// é irrelevante (dado de XZ) ou dentro de um PLATÔ (altura 0); fora disso, <see cref="OnGround"/>.</summary>
        public static Vector3 At(Vector3 center, float dx, float y, float dz)
            => new Vector3(center.x + dx, y, center.z + dz);

        /// <summary>
        /// Posição deslocada de um centro de zona e ASSENTADA no relevo (Milestone do Terreno): a
        /// altura vem de <see cref="WorldHeight"/> — a mesma função que gera a malha do chão — mais
        /// um deslocamento vertical (ex.: meia-altura da cápsula). Nos platôs equivale a <c>At</c> com
        /// y = yOffset; fora deles é o que impede spawn flutuando ou enterrado.
        /// </summary>
        public static Vector3 OnGround(Vector3 center, float dx, float dz, float yOffset)
            => WorldHeight.OnGround(center.x + dx, center.z + dz, yOffset);

        // Pontos de nascimento dos jogadores (co-op 1–4). TAMANHO 1 importa no slice solo,
        // mas o conceito é plural desde já: a aprovação de conexão da rede instancia N
        // players lendo deste anel ao sul do Altar — princípio multiplayer-ready "jogadores
        // são uma coleção, tamanho 1 hoje" (ENGINEERING_RULES #13, Multiplayer.md #2).
        public static readonly Vector3[] PlayerSpawns =
        {
            OnGround(Hub,  0f,   -2f,   1.1f),
            OnGround(Hub, -1.8f, -2.6f, 1.1f),
            OnGround(Hub,  1.8f, -2.6f, 1.1f),
            OnGround(Hub,  0f,   -3.8f, 1.1f),
        };

        /// <summary>Ponto de spawn do jogador de índice <paramref name="index"/> (limitado à lista).</summary>
        public static Vector3 SpawnFor(int index)
            => PlayerSpawns[Mathf.Clamp(index, 0, PlayerSpawns.Length - 1)];

        // Trilha de ruínas que liga o hub (z=0) à arena do boss (z=26) — a "Era da
        // Ressonância" engolida pela mata. Coordenadas vivem AQUI (fonte única).
        public static readonly Vector3[] RuinTrail =
        {
            At(Hub, -2.5f, 0f, 7.5f),
            At(Hub,  3.0f, 0f, 9.0f),
            At(Hub, -1.5f, 0f, 15f),
            At(Hub,  2.2f, 0f, 19f),
            At(Hub, -2.0f, 0f, 22f),
        };

        // Spawns dos inimigos da Zona de Combate, PAREADOS POR ÍNDICE com os arquétipos
        // (0=Berserker, 1=HitAndRun, 2=Zigzag). Fonte única (M-NET-6 P1): a cena solo
        // (authored pelo PlayerSceneSetup) e a rede (NetEnemySpawner, no servidor) leem
        // daqui — mesma manobra do PlayerSpawns, para posições nunca divergirem.
        public static readonly Vector3[] EnemySpawns =
        {
            OnGround(ZoneCombate,  0.5f,  0.8f, 1f), // Berserker
            OnGround(ZoneCombate, -2.2f,  1.6f, 1f), // HitAndRun
            OnGround(ZoneCombate,  2.4f, -1.2f, 1f), // Zigzag
        };

        // Spawn do boss no centro da arena (y = meia-altura da cápsula grande).
        public static readonly Vector3 BossSpawn = OnGround(BossArena, 0f, 0f, 1.8f);
    }
}

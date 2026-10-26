using UnityEngine;

namespace JogoDaTrupe.Core.World
{
    /// <summary>Fonte pura de altura para a malha e o assentamento dos objetos do mundo.</summary>
    public static class WorldHeight
    {
        // Geometria duplicada de propósito: Core não depende dos geradores nem de outros assemblies.
        public const float HubRadius = 6f;
        public const float ZoneCenterOffset = 11.3f;
        public const float ZoneRadius = 5f;
        public const float BossCenterZ = 26f;
        public const float BossRadius = 8f;
        // VILAS (M-TERR-5): dois pads planos a leste e oeste, no anel entre as zonas (raio ~16) e a
        // subida da borda (28). Raio 12 → borda do pad em 35: as casas ocupam até ~11 m do centro e
        // sobra um ANEL PLANO atrás delas para a NavMesh (com 10,5 o morro começava colado no fundo das
        // casas e os mobs não seguiam o jogador por trás da vila; com 9 as quinas afundavam).
        public const float VillageCenterX = 23f;
        public const float VillageRadius = 12f;

        // Cinco metros de transição preservam a área útil inteira dos platôs.
        public const float PlateauFalloff = 5f;
        public const float CorridorWidth = 6f;
        public const float CorridorFalloff = 5f;

        // Comprimentos em metros; amplitude escala cristas e vales, sem afetar os pisos planos.
        public const float TerrainAmplitude = 8f; // 12 → 8: Diretor achou "alto demais alguns" (11/09/2026)
        public const float NoiseWavelength = 18f;
        public const float DetailWavelength = 9f;
        public const float BorderRise = 4f;
        public const float BorderStartRadius = 28f;
        public const float BorderRadius = 38f;

        // O raio inclui a margem suave; o lago não tem influência fora desse disco.
        public const float LakeCenterX = 0f;
        public const float LakeCenterZ = -25f;
        public const float LakeRadius = 9f;
        public const float LakeDepth = 6f;
        // Cota do ESPELHO D'ÁGUA. Fica abaixo do piso do ruído (-0.25 * amplitude = -2 m) de
        // propósito: assim a água só aparece dentro da bacia, nunca em vales soltos do mapa.
        public const float LakeWaterLevel = -2.5f;

        /// <summary>Altura do terreno no ponto (x, z) do plano. Determinística e pura.</summary>
        public static float SampleHeight(float x, float z)
        {
            float mask = PlateauMask(x, z, 0f, 0f, HubRadius);
            mask *= PlateauMask(x, z, -ZoneCenterOffset, ZoneCenterOffset, ZoneRadius);
            mask *= PlateauMask(x, z, ZoneCenterOffset, ZoneCenterOffset, ZoneRadius);
            mask *= PlateauMask(x, z, -ZoneCenterOffset, -ZoneCenterOffset, ZoneRadius);
            mask *= PlateauMask(x, z, ZoneCenterOffset, -ZoneCenterOffset, ZoneRadius);
            mask *= PlateauMask(x, z, 0f, BossCenterZ, BossRadius);
            mask *= PlateauMask(x, z, -VillageCenterX, 0f, VillageRadius); // vila oeste
            mask *= PlateauMask(x, z,  VillageCenterX, 0f, VillageRadius); // vila leste
            if (mask == 0f)
                return 0f;

            // O produto mantém transições suaves também onde corredores e platôs se encontram.
            // As polilinhas vivem em WorldPaths — a MESMA fonte que desenha a fita de trilha
            // (M-TERR-4): corredor plano e trilha visível nunca divergem. São arrays estáticos
            // somente-leitura de literais: sem estado mutável e sem ler o MapLayout (que lê a
            // gente — ler de volta criaria um ciclo de inicialização estática).
            foreach (Vector2[] path in WorldPaths.Polylines)
                for (int i = 1; i < path.Length; i++)
                    mask *= CorridorMask(x, z, path[i - 1].x, path[i - 1].y, path[i].x, path[i].y);
            if (mask == 0f)
                return 0f;

            float noise = 0.7f * ValueNoise(x / NoiseWavelength, z / NoiseWavelength)
                + 0.3f * ValueNoise(x / DetailWavelength, z / DetailWavelength);
            float radius = Mathf.Sqrt(x * x + z * z);
            float height = TerrainAmplitude * (1.1f * noise - 0.25f)
                + BorderRise * Smooth01((radius - BorderStartRadius) / (BorderRadius - BorderStartRadius));

            float lakeX = x - LakeCenterX;
            float lakeZ = z - LakeCenterZ;
            float lakeDistance = Mathf.Sqrt(lakeX * lakeX + lakeZ * lakeZ);
            float lakeMask = 1f - Smooth01(lakeDistance / LakeRadius);
            // Interpolar para o fundo fixa sua profundidade, independentemente do ruído local.
            height += (-LakeDepth - height) * lakeMask;

            // A proteção final das áreas jogáveis prevalece até sobre o recorte do lago.
            return height * mask;
        }

        /// <summary>Ponto (x, z) assentado no terreno, com um deslocamento vertical opcional.</summary>
        public static Vector3 OnGround(float x, float z, float yOffset = 0f)
        {
            return new Vector3(x, SampleHeight(x, z) + yOffset, z);
        }

        private static float PlateauMask(float x, float z, float centerX, float centerZ, float radius)
        {
            float dx = x - centerX;
            float dz = z - centerZ;
            return DistanceMask(dx * dx + dz * dz, radius, PlateauFalloff);
        }

        private static float CorridorMask(float x, float z, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax;
            float dz = bz - az;
            float lengthSquared = dx * dx + dz * dz;
            float t = lengthSquared > 0f
                ? Mathf.Clamp01(((x - ax) * dx + (z - az) * dz) / lengthSquared)
                : 0f;
            float offsetX = x - (ax + t * dx);
            float offsetZ = z - (az + t * dz);
            return DistanceMask(offsetX * offsetX + offsetZ * offsetZ, CorridorWidth * 0.5f, CorridorFalloff);
        }

        private static float DistanceMask(float distanceSquared, float radius, float falloff)
        {
            // A maioria das consultas dispensa raiz quadrada: já está no piso ou fora da transição.
            if (distanceSquared <= radius * radius)
                return 0f;
            float outerRadius = radius + falloff;
            if (distanceSquared >= outerRadius * outerRadius)
                return 1f;
            return Smooth01((Mathf.Sqrt(distanceSquared) - radius) / falloff);
        }

        private static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private static float ValueNoise(float x, float z)
        {
            // Floor, em vez de truncamento, mantém a continuidade nas coordenadas negativas.
            int ix = Mathf.FloorToInt(x);
            int iz = Mathf.FloorToInt(z);
            float tx = Smooth01(x - ix);
            float tz = Smooth01(z - iz);
            float a = Hash01(ix, iz);
            float b = Hash01(unchecked(ix + 1), iz);
            float c = Hash01(ix, unchecked(iz + 1));
            float d = Hash01(unchecked(ix + 1), unchecked(iz + 1));
            float lower = a + (b - a) * tx;
            float upper = c + (d - c) * tx;
            return lower + (upper - lower) * tz;
        }

        private static float Hash01(int x, int z)
        {
            // Overflow uint explícito e 24 bits exatos em float tornam o hash portátil,
            // sem sementes externas, relógio ou dependência da implementação de Perlin da Unity.
            unchecked
            {
                uint hash = (uint)x * 0x8da6b343u ^ (uint)z * 0xd8163841u ^ 0xcb1ab31fu;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                hash *= 0x846ca68bu;
                hash ^= hash >> 16;
                return (hash & 0x00ffffffu) * (1f / 16777216f);
            }
        }
    }
}
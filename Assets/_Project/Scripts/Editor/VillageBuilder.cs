using UnityEditor;
using UnityEngine;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// VILAS (Milestone do Terreno, M-TERR-5): monta uma vila num PAD plano do relevo
    /// (<see cref="WorldHeight"/>: cota 0 dentro do raio) com módulos do kit Quaternius.
    /// Lotes = casas num anel ao redor de uma pracinha, cada uma de FRENTE para o centro, deixando
    /// livre o lado por onde chega a estrada (<see cref="WorldPaths"/>). Cada casa é uma planta
    /// pura do <see cref="HouseBlueprint"/> (spec seeded) instanciada módulo a módulo.
    ///
    /// Convenções do kit (conferidas com a casa-sonda, VillageKitProbe): a raiz de cada FBX do
    /// Blender carrega a rotação de conversão de eixos, por isso TODO módulo nasce sob uma ÂNCORA
    /// vazia que recebe posição/yaw — a raiz do modelo nunca é tocada.
    ///
    /// Colisão v1 = uma caixa por casa (pegada × pé-direito): bloqueia jogador e mobs e entra no bake
    /// da NavMesh como obstáculo. Interior não é visitável nesta passada (porta decorativa).
    /// </summary>
    internal static class VillageBuilder
    {
        private const string KitRoot = "Assets/Quaternius_MedievalVillage/FBX/";
        // Raio do anel onde começa a FRENTE das casas: 4,5 m + até 6 m de profundidade = fundo da casa
        // em ≤ 10,5 m (raio do pad plano). Anel maior + setor da estrada menor = casas vizinhas com
        // ≥ 2,5 m de vão entre si: os mobs (raio 0,5) passam ENTRE as casas em vez de travar numa rota
        // parcial deslizando na borda da NavMesh (Play do Diretor com anel de 3,5 m e 4 casas).
        private const float HouseRing = 4.5f;
        private const float RoadGapDegrees = 50f; // meia-abertura livre em volta da direção da estrada

        /// <summary>
        /// Monta a vila em <paramref name="center"/>. <paramref name="roadAngleDeg"/> = direção (graus,
        /// 0 = +X, 90 = +Z) por onde a estrada entra no pad; nenhuma casa nasce nesse setor.
        /// </summary>
        public static GameObject Build(Transform parent, Vector3 center, string name, int seed, float roadAngleDeg, int houses = 4)
        {
            var village = new GameObject(name);
            village.transform.SetParent(parent, false);
            village.transform.position = center;
            var rng = new System.Random(seed);

            // Ângulos das casas: distribuídos fora do setor da estrada, com um leve ruído.
            float usable = 360f - 2f * RoadGapDegrees;
            for (int i = 0; i < houses; i++)
            {
                float t = (i + 0.5f) / houses;
                float angle = roadAngleDeg + RoadGapDegrees + usable * t + Lerp(rng, -8f, 8f);
                var spec = new HouseSpec
                {
                    WidthCells = i == 0 && rng.NextDouble() < 0.6 ? 3 : 2, // no máximo UMA casa larga por vila (vãos)
                    DepthCells = 2 + rng.Next(0, 2),          // 2 ou 3 (4 ou 6 m)
                    Storeys = rng.NextDouble() < 0.3 ? 2 : 1,
                    Style = rng.NextDouble() < 0.5 ? WallStyle.Plaster : WallStyle.UnevenBrick,
                    Seed = rng.Next(),
                };
                BuildHouse(village.transform, angle, spec, "Casa " + (i + 1));
            }

            // Pracinha: uma carroça e caixotes perto do centro, fora da boca da estrada.
            float backAngle = roadAngleDeg + 180f;
            PlaceModule(village.transform, "Prop_Wagon", Polar(backAngle + Lerp(rng, -20f, 20f), 1.6f), backAngle + 90f + Lerp(rng, -25f, 25f));
            PlaceModule(village.transform, "Prop_Crate", Polar(backAngle + 60f, 2.2f), Lerp(rng, 0f, 90f));
            PlaceModule(village.transform, "Prop_Crate", Polar(backAngle - 60f, 2.4f), Lerp(rng, 0f, 90f));
            return village;
        }

        // Uma casa no anel: a FRENTE (−Z local) fica a HouseRing do centro e olha para ele.
        private static void BuildHouse(Transform village, float angleDeg, HouseSpec spec, string name)
        {
            Vector2 fp = HouseBlueprint.Footprint(spec);
            float dist = HouseRing + fp.y * 0.5f; // centro da casa = anel + meia profundidade
            Vector3 pos = Polar(angleDeg, dist);
            // −Z local deve apontar para o centro (−pos): forward (+Z) aponta para fora, na direção de pos.
            float yaw = Mathf.Atan2(pos.x, pos.z) * Mathf.Rad2Deg;

            var house = new GameObject(name);
            house.transform.SetParent(village, false);
            house.transform.localPosition = pos;
            house.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            foreach (ModulePlacement m in HouseBlueprint.Generate(spec))
                PlaceModule(house.transform, m.Module, m.LocalPosition, m.YawDegrees);

            float height = 3f * spec.Storeys;
            var box = house.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(fp.x, height, fp.y);
        }

        /// <summary>Instancia um módulo do kit sob uma âncora com a pose pedida. Null se o FBX não existir.</summary>
        internal static GameObject PlaceModule(Transform parent, string module, Vector3 localPos, float yawDeg)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KitRoot + module + ".fbx");
            if (model == null)
            {
                Debug.LogWarning("[Vila] Módulo do kit não encontrado: " + module);
                return null;
            }
            var anchor = new GameObject(module);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPos;
            anchor.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            PrefabUtility.InstantiatePrefab(model, anchor.transform);
            return anchor;
        }

        private static Vector3 Polar(float angleDeg, float radius)
            => new Vector3(Mathf.Cos(angleDeg * Mathf.Deg2Rad) * radius, 0f, Mathf.Sin(angleDeg * Mathf.Deg2Rad) * radius);

        private static float Lerp(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);
    }
}

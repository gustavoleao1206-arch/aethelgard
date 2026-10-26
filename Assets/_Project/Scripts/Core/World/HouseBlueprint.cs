using System;
using System.Collections.Generic;
using UnityEngine;

namespace JogoDaTrupe.Core.World
{
    public enum WallStyle { Plaster, UnevenBrick }

    public struct HouseSpec
    {
        public int WidthCells;
        public int DepthCells;
        public int Storeys;
        public WallStyle Style;
        public int Seed;
    }

    public readonly struct ModulePlacement
    {
        public readonly string Module;
        public readonly Vector3 LocalPosition;
        public readonly float YawDegrees;

        public ModulePlacement(string module, Vector3 localPosition, float yawDegrees)
        {
            Module = module;
            LocalPosition = localPosition;
            YawDegrees = yawDegrees;
        }
    }

    /// <summary>Planta local do kit modular: casa centrada na origem, frente para -Z.</summary>
    public static class HouseBlueprint
    {
        private const float CellSize = 2f;
        private const float StoreyHeight = 3f;

        /// <summary>Gera módulos sem alterar a spec ou compartilhar o estado do sorteio.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Algum campo limitado da spec é inválido.</exception>
        public static IReadOnlyList<ModulePlacement> Generate(HouseSpec spec)
        {
            Validate(spec);
            var random = new System.Random(spec.Seed);
            float halfWidth = spec.WidthCells;
            float halfDepth = spec.DepthCells;
            string wallPrefix = spec.Style == WallStyle.Plaster ? "Wall_Plaster" : "Wall_UnevenBrick";
            int perimeterCount = 2 * (spec.WidthCells + spec.DepthCells) * spec.Storeys;
            var modules = new List<ModulePlacement>(
                spec.WidthCells * spec.DepthCells * spec.Storeys + perimeterCount * 3 + spec.Storeys * 4 + 8);

            // A indicação explícita de escolher a esquerda prevalece sobre WidthCells / 2
            // quando a quantidade de células é par; com três células, escolhe a central.
            int doorIndex = (spec.WidthCells - 1) / 2;
            for (int storey = 0; storey < spec.Storeys; storey++)
            {
                float y = StoreyHeight * storey;
                for (int j = 0; j < spec.DepthCells; j++)
                {
                    for (int i = 0; i < spec.WidthCells; i++)
                        Add(modules, "Floor_WoodDark", -halfWidth + 1f + CellSize * i,
                            y, -halfDepth + 1f + CellSize * j, 0f);
                }

                for (int i = 0; i < spec.WidthCells; i++)
                {
                    float x = -halfWidth + 1f + CellSize * i;
                    AddWall(modules, random, wallPrefix, new Vector3(x, y, -halfDepth),
                        0f, storey, storey == 0 && i == doorIndex);
                    AddWall(modules, random, wallPrefix, new Vector3(x, y, halfDepth),
                        180f, storey, false);
                }
                for (int j = 0; j < spec.DepthCells; j++)
                {
                    float z = -halfDepth + 1f + CellSize * j;
                    // -Z local é o exterior: oeste precisa de +90, leste de -90.
                    AddWall(modules, random, wallPrefix, new Vector3(-halfWidth, y, z),
                        90f, storey, false);
                    AddWall(modules, random, wallPrefix, new Vector3(halfWidth, y, z),
                        -90f, storey, false);
                }
                for (int xSign = -1; xSign <= 1; xSign += 2)
                {
                    for (int zSign = -1; zSign <= 1; zSign += 2)
                        Add(modules, "Corner_Exterior_Wood", xSign * halfWidth, y, zSign * halfDepth, 0f);
                }
            }

            // Seleção fechada evita nomes de telhados inexistentes ou formatação dependente da cultura.
            string widthName = spec.WidthCells == 2 ? "4" : "6";
            string depthName = spec.DepthCells == 2 ? "4" : spec.DepthCells == 3 ? "6" : "8";
            float roofY = StoreyHeight * spec.Storeys;
            Add(modules, "Roof_RoundTiles_" + widthName + "x" + depthName, 0f, roofY, 0f, 0f);
            Add(modules, "Roof_Front_Brick" + widthName, 0f, roofY, -halfDepth, 0f);
            Add(modules, "Roof_Front_Brick" + widthName, 0f, roofY, halfDepth, 180f);
            float chimneySign = random.Next(2) == 0 ? -1f : 1f;
            Add(modules, "Prop_Chimney", chimneySign * (halfWidth - 0.8f), roofY, 0.4f, 0f);

            int crateCount = random.Next(3);
            for (int i = 0; i < crateCount; i++)
            {
                float side = random.Next(2) == 0 ? -1f : 1f;
                // Pivôs meio metro para fora; células diferentes evitam caixas coincidentes.
                Add(modules, "Prop_Crate", side * (halfWidth + 0.5f), 0f,
                    -halfDepth + 1f + CellSize * i, 0f);
            }

            // Zero cercas está dentro de 0-2. A distância prescrita de 2 m da frente
            // contradiz o limite global de 1 m; não inventa outra posição para a cerca.
            return modules.AsReadOnly();
        }

        /// <summary>Tamanho da pegada em metros (sem beiral).</summary>
        public static Vector2 Footprint(HouseSpec spec)
        {
            Validate(spec);
            return new Vector2(spec.WidthCells * CellSize, spec.DepthCells * CellSize);
        }

        private static void AddWall(List<ModulePlacement> modules, System.Random random, string prefix,
            Vector3 position, float yaw, int storey, bool isDoor)
        {
            if (isDoor)
            {
                modules.Add(new ModulePlacement(prefix + "_Door_Round", position, yaw));
                modules.Add(new ModulePlacement("DoorFrame_Round_WoodDark", position, yaw));
                // A única porta fica ao sul (yaw 0), portanto X local coincide com X da casa.
                Vector3 hinge = new Vector3(position.x + 0.55f, position.y, position.z);
                modules.Add(new ModulePlacement("Door_1_Round", hinge, yaw));
                return;
            }

            int roll = random.Next(100);
            int wideThreshold = storey == 0 ? 50 : 60;
            // A spec só fixa os 60% de largas no andar superior; divide o restante igualmente.
            int thinThreshold = storey == 0 ? 75 : 80;
            if (roll < wideThreshold)
            {
                modules.Add(new ModulePlacement(prefix + "_Window_Wide_Round", position, yaw));
                modules.Add(new ModulePlacement("Window_Wide_Round1", position, yaw));
                if (storey == 0 && random.Next(100) < 30)
                    modules.Add(new ModulePlacement("WindowShutters_Wide_Round_Open", position, yaw));
            }
            else if (roll < thinThreshold)
            {
                modules.Add(new ModulePlacement(prefix + "_Window_Thin_Round", position, yaw));
                modules.Add(new ModulePlacement("Window_Thin_Round1", position, yaw));
            }
            else
                modules.Add(new ModulePlacement(prefix + "_Straight", position, yaw));
        }

        private static void Add(List<ModulePlacement> modules, string module,
            float x, float y, float z, float yaw)
        {
            modules.Add(new ModulePlacement(module, new Vector3(x, y, z), yaw));
        }

        private static void Validate(HouseSpec spec)
        {
            if (spec.WidthCells < 2 || spec.WidthCells > 3)
                throw new ArgumentOutOfRangeException(nameof(spec.WidthCells), spec.WidthCells, "Use 2 ou 3 células.");
            if (spec.DepthCells < 2 || spec.DepthCells > 4)
                throw new ArgumentOutOfRangeException(nameof(spec.DepthCells), spec.DepthCells, "Use 2, 3 ou 4 células.");
            if (spec.Storeys < 1 || spec.Storeys > 2)
                throw new ArgumentOutOfRangeException(nameof(spec.Storeys), spec.Storeys, "Use 1 ou 2 andares.");
            if (spec.Style != WallStyle.Plaster && spec.Style != WallStyle.UnevenBrick)
                throw new ArgumentOutOfRangeException(nameof(spec.Style), spec.Style, "Estilo fora do catálogo.");
        }
    }
}
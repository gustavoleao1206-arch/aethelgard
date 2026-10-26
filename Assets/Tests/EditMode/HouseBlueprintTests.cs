using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Tests.EditMode
{
    /// <summary>Valida nomes e pivôs locais do contrato, sem instanciar os FBX ou acessar cenas.</summary>
    public class HouseBlueprintTests
    {
        [Test]
        public void Floors_EveryCellAndStorey_HasOneCenteredModule()
        {
            foreach (HouseSpec spec in ValidSpecs())
            {
                var modules = HouseBlueprint.Generate(spec);
                Assert.AreEqual(spec.WidthCells * spec.DepthCells * spec.Storeys, Count(modules, "Floor_WoodDark"));
                for (int k = 0; k < spec.Storeys; k++)
                    for (int j = 0; j < spec.DepthCells; j++)
                        for (int i = 0; i < spec.WidthCells; i++)
                            Assert.AreEqual(1, CountAt(modules, "Floor_WoodDark",
                                new Vector3(-spec.WidthCells + 1 + 2 * i, 3 * k, -spec.DepthCells + 1 + 2 * j), 0f));
            }
        }

        [Test]
        public void Walls_PerimeterCountPivotsAndOutwardYaws_MatchEveryStorey()
        {
            foreach (HouseSpec spec in ValidSpecs())
            {
                var modules = HouseBlueprint.Generate(spec);
                int walls = 0;
                string style = spec.Style == WallStyle.Plaster ? "Wall_Plaster_" : "Wall_UnevenBrick_";
                foreach (ModulePlacement module in modules)
                {
                    if (!module.Module.StartsWith("Wall_", StringComparison.Ordinal)) continue;
                    walls++;
                    Assert.IsTrue(module.Module.StartsWith(style, StringComparison.Ordinal));
                }
                Assert.AreEqual(2 * (spec.WidthCells + spec.DepthCells) * spec.Storeys, walls);
                for (int k = 0; k < spec.Storeys; k++)
                {
                    for (int i = 0; i < spec.WidthCells; i++)
                    {
                        float x = -spec.WidthCells + 1 + 2 * i;
                        AssertWallAt(modules, new Vector3(x, 3 * k, -spec.DepthCells), 0f);
                        AssertWallAt(modules, new Vector3(x, 3 * k, spec.DepthCells), 180f);
                    }
                    for (int j = 0; j < spec.DepthCells; j++)
                    {
                        float z = -spec.DepthCells + 1 + 2 * j;
                        AssertWallAt(modules, new Vector3(-spec.WidthCells, 3 * k, z), 90f);
                        AssertWallAt(modules, new Vector3(spec.WidthCells, 3 * k, z), -90f);
                    }
                }
            }
        }

        [Test]
        public void Door_OnlyOneOnSouthGroundFloor_FrameAndHingeMatch()
        {
            foreach (HouseSpec spec in ValidSpecs())
            {
                var modules = HouseBlueprint.Generate(spec);
                // A regra textual pede a esquerda no caso par e o centro no caso ímpar.
                float doorX = spec.WidthCells == 2 ? -1f : 0f;
                var pivot = new Vector3(doorX, 0f, -spec.DepthCells);
                int doors = 0;
                foreach (ModulePlacement module in modules)
                {
                    if (!module.Module.StartsWith("Wall_", StringComparison.Ordinal)
                        || module.Module.IndexOf("_Door_", StringComparison.Ordinal) < 0) continue;
                    doors++;
                    AssertPosition(pivot, module.LocalPosition);
                    Assert.AreEqual(0f, module.YawDegrees);
                }
                Assert.AreEqual(1, doors);
                Assert.AreEqual(1, Count(modules, "DoorFrame_Round_WoodDark"));
                Assert.AreEqual(1, Count(modules, "Door_1_Round"));
                Assert.AreEqual(1, CountAt(modules, "DoorFrame_Round_WoodDark", pivot, 0f));
                Assert.AreEqual(1, CountAt(modules, "Door_1_Round", new Vector3(doorX + 0.55f, 0f, -spec.DepthCells), 0f));
            }
        }

        [Test]
        public void Corners_FourSignCombinations_PerStorey()
        {
            foreach (HouseSpec spec in ValidSpecs())
            {
                var modules = HouseBlueprint.Generate(spec);
                Assert.AreEqual(4 * spec.Storeys, Count(modules, "Corner_Exterior_Wood"));
                for (int k = 0; k < spec.Storeys; k++)
                    foreach (int sx in new[] { -1, 1 })
                        foreach (int sz in new[] { -1, 1 })
                            Assert.AreEqual(1, CountAt(modules, "Corner_Exterior_Wood",
                                new Vector3(sx * spec.WidthCells, 3 * k, sz * spec.DepthCells), 0f));
            }
        }

        [Test]
        public void Roof_OneMatchingSizeAndTwoGables_HaveCorrectTransforms()
        {
            foreach (HouseSpec spec in ValidSpecs())
            {
                var modules = HouseBlueprint.Generate(spec);
                string roof = "Roof_RoundTiles_" + (spec.WidthCells * 2) + "x" + (spec.DepthCells * 2);
                string gable = "Roof_Front_Brick" + (spec.WidthCells * 2);
                Assert.AreEqual(1, CountPrefix(modules, "Roof_RoundTiles_"));
                Assert.AreEqual(2, CountPrefix(modules, "Roof_Front_"));
                Assert.AreEqual(1, CountAt(modules, roof, new Vector3(0f, 3 * spec.Storeys, 0f), 0f));
                Assert.AreEqual(1, CountAt(modules, gable, new Vector3(0f, 3 * spec.Storeys, -spec.DepthCells), 0f));
                Assert.AreEqual(1, CountAt(modules, gable, new Vector3(0f, 3 * spec.Storeys, spec.DepthCells), 180f));
            }
        }

        [Test]
        public void Windows_ExactlyOneMatchingInsert_PerWindowAndNoneOnSolidWalls()
        {
            foreach (HouseSpec spec in ValidSpecs())
            {
                var modules = HouseBlueprint.Generate(spec);
                int expectedInserts = 0;
                foreach (ModulePlacement wall in modules)
                {
                    if (!wall.Module.StartsWith("Wall_", StringComparison.Ordinal)) continue;
                    bool wide = wall.Module.EndsWith("_Window_Wide_Round", StringComparison.Ordinal);
                    bool thin = wall.Module.EndsWith("_Window_Thin_Round", StringComparison.Ordinal);
                    Assert.AreEqual(wide ? 1 : 0, CountAt(modules, "Window_Wide_Round1", wall.LocalPosition, wall.YawDegrees));
                    Assert.AreEqual(thin ? 1 : 0, CountAt(modules, "Window_Thin_Round1", wall.LocalPosition, wall.YawDegrees));
                    if (wide || thin) expectedInserts++;
                }
                Assert.AreEqual(expectedInserts, Count(modules, "Window_Wide_Round1") + Count(modules, "Window_Thin_Round1"));
            }
        }

        [Test]
        public void Determinism_SameSpecMatches_DifferentSeedsChangeWindows()
        {
            foreach (HouseSpec spec in ValidSpecs())
                AssertSame(HouseBlueprint.Generate(spec), HouseBlueprint.Generate(spec));
            foreach (WallStyle style in new[] { WallStyle.Plaster, WallStyle.UnevenBrick })
            {
                var first = new HouseSpec { WidthCells = 3, DepthCells = 4, Storeys = 2, Style = style, Seed = 1 };
                HouseSpec second = first;
                second.Seed = 2;
                List<string> firstWalls = WallNames(HouseBlueprint.Generate(first));
                List<string> secondWalls = WallNames(HouseBlueprint.Generate(second));
                Assert.AreEqual(firstWalls.Count, secondWalls.Count);
                bool windowDifference = false;
                for (int i = 0; i < firstWalls.Count; i++)
                    windowDifference |= firstWalls[i] != secondWalls[i];
                Assert.IsTrue(windowDifference, "A variação deve aparecer nas paredes, não apenas nos adereços.");
            }
        }

        [Test]
        public void InvalidSpecs_GenerateAndFootprint_ThrowArgumentOutOfRange()
        {
            var valid = new HouseSpec { WidthCells = 2, DepthCells = 2, Storeys = 1, Style = WallStyle.Plaster };
            foreach (int width in new[] { int.MinValue, -1, 0, 1, 4, int.MaxValue })
            {
                HouseSpec invalid = valid;
                invalid.WidthCells = width;
                AssertInvalid(invalid);
            }
            foreach (int depth in new[] { -1, 0, 1, 5, int.MaxValue })
            {
                HouseSpec invalid = valid;
                invalid.DepthCells = depth;
                AssertInvalid(invalid);
            }
            foreach (int storeys in new[] { -1, 0, 3, int.MaxValue })
            {
                HouseSpec invalid = valid;
                invalid.Storeys = storeys;
                AssertInvalid(invalid);
            }
            foreach (int style in new[] { -1, 2, int.MaxValue })
            {
                HouseSpec invalid = valid;
                invalid.Style = (WallStyle)style;
                AssertInvalid(invalid);
            }
        }

        [Test]
        public void CatalogAndBounds_EveryPlacement_IsKnownFiniteAndWithinOneMeter()
        {
            var catalog = new HashSet<string>(StringComparer.Ordinal)
            {
                "Floor_WoodDark",
                "Wall_Plaster_Straight", "Wall_Plaster_Door_Round",
                "Wall_Plaster_Window_Wide_Round", "Wall_Plaster_Window_Thin_Round",
                "Wall_UnevenBrick_Straight", "Wall_UnevenBrick_Door_Round",
                "Wall_UnevenBrick_Window_Wide_Round", "Wall_UnevenBrick_Window_Thin_Round",
                "DoorFrame_Round_WoodDark", "Door_1_Round",
                "Window_Wide_Round1", "Window_Thin_Round1", "WindowShutters_Wide_Round_Open",
                "Corner_Exterior_Wood", "Roof_RoundTiles_4x4", "Roof_RoundTiles_4x6", "Roof_RoundTiles_4x8",
                "Roof_RoundTiles_6x4", "Roof_RoundTiles_6x6", "Roof_RoundTiles_6x8",
                "Roof_Front_Brick4", "Roof_Front_Brick6", "Prop_Chimney", "Prop_Crate", "Prop_WoodenFence_Single"
            };
            foreach (HouseSpec spec in ValidSpecs())
            {
                foreach (ModulePlacement module in HouseBlueprint.Generate(spec))
                {
                    Assert.IsTrue(catalog.Contains(module.Module), module.Module);
                    Vector3 p = module.LocalPosition;
                    AssertFinite(p.x);
                    AssertFinite(p.y);
                    AssertFinite(p.z);
                    AssertFinite(module.YawDegrees);
                    float allowance = module.Module.StartsWith("Roof_", StringComparison.Ordinal) ? 0.8f : 1f;
                    Assert.LessOrEqual(Math.Abs(p.x), spec.WidthCells + allowance);
                    Assert.LessOrEqual(Math.Abs(p.z), spec.DepthCells + allowance);
                }
            }
        }

        [Test]
        public void Footprint_ReportsWidthAndDepthInMeters_RegardlessOfSeed()
        {
            foreach (HouseSpec spec in ValidSpecs())
            {
                Vector2 size = HouseBlueprint.Footprint(spec);
                Assert.AreEqual(spec.WidthCells * 2f, size.x);
                Assert.AreEqual(spec.DepthCells * 2f, size.y);
            }
        }

        [Test]
        public void Details_ChimneyCratesAndShutters_RespectPlacementRules()
        {
            bool sawShutters = false;
            bool sawCrates = false;
            foreach (HouseSpec spec in ValidSpecs())
            {
                var modules = HouseBlueprint.Generate(spec);
                Assert.AreEqual(1, Count(modules, "Prop_Chimney"));
                int crates = Count(modules, "Prop_Crate");
                int fences = Count(modules, "Prop_WoodenFence_Single");
                Assert.That(crates, Is.InRange(0, 2));
                Assert.That(fences, Is.InRange(0, 2));
                sawCrates |= crates > 0;
                foreach (ModulePlacement module in modules)
                {
                    Vector3 p = module.LocalPosition;
                    if (module.Module == "Prop_Chimney")
                    {
                        Assert.AreEqual(spec.WidthCells - 0.8f, Math.Abs(p.x), 0.00001f);
                        Assert.AreEqual(3 * spec.Storeys, p.y);
                        Assert.AreEqual(0.4f, p.z);
                        Assert.AreEqual(0f, module.YawDegrees);
                    }
                    else if (module.Module == "Prop_Crate")
                    {
                        Assert.AreEqual(0f, p.y);
                        Assert.Greater(Math.Abs(p.x), spec.WidthCells);
                        Assert.LessOrEqual(Math.Abs(p.x), spec.WidthCells + 1f);
                        Assert.LessOrEqual(Math.Abs(p.z), spec.DepthCells);
                        Assert.AreEqual(1, CountAt(modules, "Prop_Crate", p, module.YawDegrees));
                    }
                    else if (module.Module == "WindowShutters_Wide_Round_Open")
                    {
                        sawShutters = true;
                        Assert.AreEqual(0f, p.y);
                        Assert.AreEqual(1, CountAt(modules, "Window_Wide_Round1", p, module.YawDegrees));
                    }
                    else if (module.Module == "Prop_WoodenFence_Single")
                    {
                        Assert.AreEqual(0f, p.y);
                        Assert.AreEqual(-spec.DepthCells - 2f, p.z);
                        Assert.AreEqual(0f, module.YawDegrees);
                    }
                }
            }
            Assert.IsTrue(sawShutters);
            Assert.IsTrue(sawCrates);
        }

        private static IEnumerable<HouseSpec> ValidSpecs()
        {
            // Todas as dimensões/estilos, incluindo seeds negativos e os extremos de int.
            for (int width = 2; width <= 3; width++)
                for (int depth = 2; depth <= 4; depth++)
                    for (int storeys = 1; storeys <= 2; storeys++)
                        foreach (WallStyle style in new[] { WallStyle.Plaster, WallStyle.UnevenBrick })
                            foreach (int seed in new[] { -17, 0, 42, int.MinValue, int.MaxValue })
                                yield return new HouseSpec
                                {
                                    WidthCells = width, DepthCells = depth, Storeys = storeys, Style = style, Seed = seed
                                };
        }

        private static int Count(IReadOnlyList<ModulePlacement> modules, string name)
        {
            int count = 0;
            foreach (ModulePlacement module in modules)
                if (module.Module == name) count++;
            return count;
        }

        private static int CountPrefix(IReadOnlyList<ModulePlacement> modules, string prefix)
        {
            int count = 0;
            foreach (ModulePlacement module in modules)
                if (module.Module.StartsWith(prefix, StringComparison.Ordinal)) count++;
            return count;
        }

        private static int CountAt(IReadOnlyList<ModulePlacement> modules, string name, Vector3 position, float yaw)
        {
            int count = 0;
            foreach (ModulePlacement module in modules)
                if (module.Module == name && module.LocalPosition.Equals(position) && module.YawDegrees.Equals(yaw))
                    count++;
            return count;
        }

        private static void AssertWallAt(IReadOnlyList<ModulePlacement> modules, Vector3 position, float yaw)
        {
            int count = 0;
            foreach (ModulePlacement module in modules)
                if (module.Module.StartsWith("Wall_", StringComparison.Ordinal)
                    && module.LocalPosition.Equals(position) && module.YawDegrees.Equals(yaw)) count++;
            Assert.AreEqual(1, count);
        }

        private static List<string> WallNames(IReadOnlyList<ModulePlacement> modules)
        {
            var result = new List<string>();
            foreach (ModulePlacement module in modules)
                if (module.Module.StartsWith("Wall_", StringComparison.Ordinal)) result.Add(module.Module);
            return result;
        }

        private static void AssertInvalid(HouseSpec spec)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => { HouseBlueprint.Generate(spec); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { HouseBlueprint.Footprint(spec); });
        }

        private static void AssertPosition(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.00001f);
            Assert.AreEqual(expected.y, actual.y, 0.00001f);
            Assert.AreEqual(expected.z, actual.z, 0.00001f);
        }

        private static void AssertFinite(float value)
        {
            Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value));
        }

        private static void AssertSame(IReadOnlyList<ModulePlacement> expected, IReadOnlyList<ModulePlacement> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Module, actual[i].Module);
                AssertBits(expected[i].LocalPosition.x, actual[i].LocalPosition.x);
                AssertBits(expected[i].LocalPosition.y, actual[i].LocalPosition.y);
                AssertBits(expected[i].LocalPosition.z, actual[i].LocalPosition.z);
                AssertBits(expected[i].YawDegrees, actual[i].YawDegrees);
            }
        }

        private static void AssertBits(float expected, float actual)
        {
            Assert.AreEqual(BitConverter.ToInt32(BitConverter.GetBytes(expected), 0),
                BitConverter.ToInt32(BitConverter.GetBytes(actual), 0));
        }
    }
}
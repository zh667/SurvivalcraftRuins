using SurvivalcraftRuins.Builders;
using SurvivalcraftRuins.Core;
using Xunit;

namespace SurvivalcraftRuins.Tests;

/// <summary>Flat dry world at a fixed altitude.</summary>
internal sealed class FlatSampler : IWorldSampler
{
    public int OceanLevel => 64;
    private readonly float m_height;

    public FlatSampler(float height)
    {
        m_height = height;
    }

    public float Height(float x, float z)
    {
        return m_height;
    }
}

/// <summary>Open ocean with a uniform seabed depth.</summary>
internal sealed class OceanSampler : IWorldSampler
{
    public int OceanLevel => 64;

    public float Height(float x, float z)
    {
        return 56f;
    }
}

public class DeterministicHashTests
{
    [Fact]
    public void MixIsStable()
    {
        Assert.Equal(DeterministicHash.Mix(123, 7, -4, 9), DeterministicHash.Mix(123, 7, -4, 9));
        Assert.NotEqual(DeterministicHash.Mix(123, 7, -4, 9), DeterministicHash.Mix(124, 7, -4, 9));
    }

    [Fact]
    public void UnitFloatIsInRange()
    {
        for (uint i = 0; i < 1000; i++)
        {
            float f = DeterministicHash.ToUnitFloat(DeterministicHash.Mix(i, 42u));
            Assert.InRange(f, 0f, 0.9999999f);
        }
    }

    [Fact]
    public void StringSaltIsStable()
    {
        Assert.Equal(DeterministicHash.StringSalt("werewolf_den"), DeterministicHash.StringSalt("werewolf_den"));
        Assert.NotEqual(DeterministicHash.StringSalt("a"), DeterministicHash.StringSalt("b"));
    }
}

public class PlacementEngineTests
{
    private static StructureDef SimpleLandDef(string name, int gridChunks = 8, float chance = 0.5f)
    {
        return new StructureDef
        {
            Name = name,
            GridChunks = gridChunks,
            Chance = chance,
            Anchor = AnchorKind.Land,
            Variants = new[]
            {
                new StructureVariant
                {
                    Name = name,
                    Provider = _ =>
                    {
                        var b = new StructureBuilder(name);
                        b.Fill(0, 0, 0, 4, 3, 4, "PlanksBlock");
                        b.Chest(2, 1, 2);
                        return b.Build();
                    },
                    ChestPools = new[] { "baseline" },
                },
            },
        };
    }

    [Fact]
    public void SameSeedSameLayout()
    {
        var defs = new[] { SimpleLandDef("hut") };
        var a = new PlacementEngine(1234, new FlatSampler(80f), defs);
        var b = new PlacementEngine(1234, new FlatSampler(80f), defs);
        for (int cx = -6; cx <= 6; cx++)
        {
            for (int cz = -6; cz <= 6; cz++)
            {
                var ia = a.GetInstance(0, cx, cz);
                var ib = b.GetInstance(0, cx, cz);
                Assert.Equal(ia == null, ib == null);
                if (ia != null && ib != null)
                {
                    Assert.Equal((ia.OriginX, ia.OriginY, ia.OriginZ), (ib.OriginX, ib.OriginY, ib.OriginZ));
                }
            }
        }
    }

    [Fact]
    public void DifferentSeedsDiffer()
    {
        var defs = new[] { SimpleLandDef("hut", chance: 0.9f) };
        var a = new PlacementEngine(1, new FlatSampler(80f), defs);
        var b = new PlacementEngine(2, new FlatSampler(80f), defs);
        int differences = 0;
        for (int cx = -8; cx <= 8; cx++)
        {
            for (int cz = -8; cz <= 8; cz++)
            {
                var ia = a.GetInstance(0, cx, cz);
                var ib = b.GetInstance(0, cx, cz);
                if ((ia == null) != (ib == null))
                {
                    differences++;
                }
                else if (ia != null && ib != null && (ia.OriginX != ib.OriginX || ia.OriginZ != ib.OriginZ))
                {
                    differences++;
                }
            }
        }
        Assert.True(differences > 10, $"expected layouts to diverge, only {differences} differences");
    }

    [Fact]
    public void InstanceStaysInsideItsCell()
    {
        var defs = new[] { SimpleLandDef("hut", chance: 1f) };
        var engine = new PlacementEngine(99, new FlatSampler(90f), defs);
        for (int cx = -5; cx <= 5; cx++)
        {
            for (int cz = -5; cz <= 5; cz++)
            {
                var instance = engine.GetInstance(0, cx, cz);
                Assert.NotNull(instance);
                int span = defs[0].GridChunks * 16;
                Assert.InRange(instance!.WorldMinX, cx * span, (cx + 1) * span - 1);
                Assert.InRange(instance.WorldMaxX, cx * span, (cx + 1) * span - 1);
                Assert.InRange(instance.WorldMinZ, cz * span, (cz + 1) * span - 1);
                Assert.InRange(instance.WorldMaxZ, cz * span, (cz + 1) * span - 1);
            }
        }
    }

    [Fact]
    public void LandStructuresRejectOcean()
    {
        var defs = new[] { SimpleLandDef("hut", chance: 1f) };
        var engine = new PlacementEngine(7, new OceanSampler(), defs);
        for (int cx = -4; cx <= 4; cx++)
        {
            for (int cz = -4; cz <= 4; cz++)
            {
                Assert.Null(engine.GetInstance(0, cx, cz));
            }
        }
    }

    [Fact]
    public void OffshoreStructuresRequireWater()
    {
        var wreck = new StructureDef
        {
            Name = "wreck",
            GridChunks = 8,
            Chance = 1f,
            Anchor = AnchorKind.Offshore,
            Variants = new[]
            {
                new StructureVariant
                {
                    Name = "wreck",
                    Provider = ShipwreckBuilder.Build,
                    ChestPools = new[] { "ship_supply" },
                },
            },
        };
        var ocean = new PlacementEngine(5, new OceanSampler(), new[] { wreck });
        Assert.NotNull(ocean.GetInstance(0, 0, 0));
        var land = new PlacementEngine(5, new FlatSampler(90f), new[] { wreck });
        Assert.Null(land.GetInstance(0, 0, 0));
    }

    [Fact]
    public void HigherPriorityDefSuppressesOverlap()
    {
        // Two defs with identical grids and 100% chance will always collide in
        // the same cell; the second must yield.
        var defs = new[] { SimpleLandDef("first", chance: 1f), SimpleLandDef("second", chance: 1f) };
        var engine = new PlacementEngine(42, new FlatSampler(80f), defs);
        for (int cx = -3; cx <= 3; cx++)
        {
            for (int cz = -3; cz <= 3; cz++)
            {
                var first = engine.GetInstance(0, cx, cz);
                var second = engine.GetInstance(1, cx, cz);
                Assert.NotNull(first);
                if (second != null)
                {
                    Assert.False(second.IntersectsColumnRange(
                        first!.WorldMinX, first.WorldMinZ, first.WorldMaxX, first.WorldMaxZ));
                }
            }
        }
    }

    [Fact]
    public void ChunkQueryMatchesDirectEnumeration()
    {
        var defs = new[] { SimpleLandDef("hut", chance: 0.8f) };
        var engine = new PlacementEngine(2024, new FlatSampler(75f), defs);
        var fromChunks = new HashSet<(int, int, int)>();
        for (int chunkX = -20; chunkX <= 20; chunkX++)
        {
            for (int chunkZ = -20; chunkZ <= 20; chunkZ++)
            {
                foreach (var instance in engine.InstancesIntersecting(
                             chunkX * 16, chunkZ * 16, chunkX * 16 + 15, chunkZ * 16 + 15))
                {
                    fromChunks.Add((instance.OriginX, instance.OriginY, instance.OriginZ));
                }
            }
        }
        Assert.True(fromChunks.Count > 3, $"expected several instances, got {fromChunks.Count}");
    }
}

public class BuilderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    public void BuildersAreDeterministic(int seed)
    {
        foreach (var build in new Func<int, StructureData>[]
                 { ShipwreckBuilder.Build, MineBuilder.Build, LighthouseBuilder.Build, CampBuilder.Build,
                   MarketStallBuilder.Build, WatchtowerBuilder.Build, ShrineBuilder.Build })
        {
            var a = build(seed);
            var b = build(seed);
            Assert.Equal(a.Cells.Count, b.Cells.Count);
            for (int i = 0; i < a.Cells.Count; i++)
            {
                Assert.Equal(a.Cells[i], b.Cells[i]);
            }
        }
    }

    [Fact]
    public void ShipwreckHasCargoAndTreasure()
    {
        var ship = ShipwreckBuilder.Build(7);
        Assert.Equal(3, ship.Chests.Count);
        Assert.Contains(ship.Palette, p => p.BlockName == "TreasureGeneratorBlock");
        Assert.Contains(ship.Palette, p => p.BlockName == "SmallGunpowderKegBlock");
    }

    [Fact]
    public void MineHasChestsOreAndAmbush()
    {
        var mine = MineBuilder.Build(3);
        Assert.Equal(2, mine.Chests.Count);
        Assert.Single(mine.Ambushes);
        Assert.Contains(mine.Palette, p => p.BlockName.EndsWith("OreBlock", StringComparison.Ordinal));
        Assert.True(mine.MinY < -10, "mine should dig well below the surface");
    }

    [Fact]
    public void LighthouseIsTallWithLamps()
    {
        var lighthouse = LighthouseBuilder.Build(11);
        Assert.Equal(2, lighthouse.Chests.Count);
        Assert.True(lighthouse.MaxY >= 20, "tower should be tall");
        Assert.Contains(lighthouse.Palette, p => p.BlockName == "WickerLampBlock");
    }

    [Fact]
    public void ColorfulBuildersHaveExpectedFixtures()
    {
        var stall = MarketStallBuilder.Build(9);
        Assert.Equal(2, stall.Chests.Count);
        Assert.Contains(stall.Palette, p => p.BlockName == "CarpetBlock");
        var tower = WatchtowerBuilder.Build(4);
        Assert.Equal(2, tower.Chests.Count);
        Assert.True(tower.MaxY >= 11);
        var shrine = ShrineBuilder.Build(2);
        Assert.Single(shrine.Chests);
        Assert.Contains(shrine.Palette, p => p.BlockName == "TreasureGeneratorBlock");
    }

    [Fact]
    public void CampHasTwoChests()
    {
        var camp = CampBuilder.Build(5);
        Assert.Equal(2, camp.Chests.Count);
        Assert.Contains(camp.Palette, p => p.BlockName == "CampfireBlock");
    }
}

public class LootAndTemplateTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "SurvivalcraftRuins")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string AssetsDir => Path.Combine(RepoRoot(), "src", "SurvivalcraftRuins", "Assets");

    [Fact]
    public void LootPoolsParseAndRollWithinBounds()
    {
        var pools = LootConfig.Parse(File.ReadAllText(Path.Combine(AssetsDir, "Loot", "pools.json")));
        Assert.True(pools.Count >= 8);
        foreach (var pool in pools.Values)
        {
            var random = new Random(42);
            for (int i = 0; i < 200; i++)
            {
                var items = pool.Roll(random);
                Assert.True(items.Count <= pool.MaxRolls);
                foreach (var item in items)
                {
                    Assert.False(string.IsNullOrEmpty(item.BlockName));
                    Assert.InRange(item.Count, 1, 64);
                }
            }
        }
    }

    [Fact]
    public void LootRollsAreDeterministicPerSeed()
    {
        var pools = LootConfig.Parse(File.ReadAllText(Path.Combine(AssetsDir, "Loot", "pools.json")));
        var pool = pools["ship_treasure"];
        var a = pool.Roll(new Random(777));
        var b = pool.Roll(new Random(777));
        Assert.Equal(a, b);
    }

    [Fact]
    public void AllTemplatesParseWithConsistentMarkers()
    {
        var files = Directory.GetFiles(Path.Combine(AssetsDir, "Structures"), "*.json");
        Assert.Equal(13, files.Length);
        foreach (var file in files)
        {
            var data = StructureTemplate.Parse(File.ReadAllText(file));
            Assert.NotEmpty(data.Cells);
            var chestCells = new HashSet<(short, short, short)>();
            foreach (var cell in data.Cells)
            {
                if (data.Palette[cell.PaletteIndex].BlockName == "ChestBlock")
                {
                    chestCells.Add((cell.X, cell.Y, cell.Z));
                }
            }
            foreach (var chest in data.Chests)
            {
                Assert.Contains((chest.X, chest.Y, chest.Z), chestCells);
            }
            Assert.True(data.SizeX <= PlacementEngine.MaxFootprint, $"{data.Name} too wide");
            Assert.True(data.SizeZ <= PlacementEngine.MaxFootprint, $"{data.Name} too deep");
        }
    }

    [Fact]
    public void RegistryPoolsAllExist()
    {
        var pools = LootConfig.Parse(File.ReadAllText(Path.Combine(AssetsDir, "Loot", "pools.json")));
        var templates = new Dictionary<string, StructureData>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(Path.Combine(AssetsDir, "Structures"), "*.json"))
        {
            var data = StructureTemplate.Parse(File.ReadAllText(file));
            templates[data.Name] = data;
        }
        var defs = StructureRegistry.CreateDefs(templates);
        Assert.Equal(11, defs.Count);
        int variantCount = 0;
        foreach (var def in defs)
        {
            foreach (var variant in def.Variants)
            {
                variantCount++;
                var data = variant.Provider(123);
                foreach (var pool in variant.ChestPools)
                {
                    Assert.True(pools.ContainsKey(pool), $"{variant.Name}: missing pool '{pool}'");
                }
                if (variant.ChestPools.Count > 0)
                {
                    Assert.True(data.Chests.Count > 0, $"{variant.Name}: pools assigned but no chest markers");
                }
                if (variant.Ambush != null)
                {
                    Assert.True(data.Ambushes.Count > 0, $"{variant.Name}: ambush spec but no markers");
                }
                Assert.True(data.SizeX <= PlacementEngine.MaxFootprint, $"{variant.Name} too wide: {data.SizeX}");
                Assert.True(data.SizeZ <= PlacementEngine.MaxFootprint, $"{variant.Name} too deep: {data.SizeZ}");
            }
        }
        Assert.Equal(20, variantCount);
    }
}

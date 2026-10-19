using System.IO.Compression;
using Engine;
using Game;
using SurvivalcraftRuins.Core;
using SurvivalcraftRuins.Core.Village;
using Xunit;

namespace SurvivalcraftRuins.Tests;

public class VillageAssetTests
{
    [Fact]
    public void EveryMinecraftStyleShips()
    {
        var villages = TestRegistry.Villages();
        foreach (string style in VillageClimate.Styles)
        {
            Assert.True(villages.ContainsKey(style), $"missing village style '{style}'");
            Assert.True(villages[style].Layouts.Length >= 6, $"{style}: only {villages[style].Layouts.Length} layouts");
        }
    }

    [Fact]
    public void EveryClimateGetsAStyleThatShips()
    {
        var villages = TestRegistry.Villages();
        for (int t = 0; t <= 15; t++)
        {
            for (int h = 0; h <= 15; h++)
            {
                Assert.True(villages.ContainsKey(VillageClimate.StyleFor(t, h)), $"t={t} h={h}");
            }
        }
        Assert.Equal("snowy", VillageClimate.StyleFor(0, 10));
        Assert.Equal("desert", VillageClimate.StyleFor(12, 3));
        Assert.Equal("savanna", VillageClimate.StyleFor(6, 4));
        Assert.Equal("taiga", VillageClimate.StyleFor(4, 11));
        Assert.Equal("plains", VillageClimate.StyleFor(10, 12));
    }

    /// <summary>
    /// Every block the converter wrote must be a real Survivalcraft block:
    /// an unknown name is written as air in game, which would silently punch
    /// holes in every house.
    /// </summary>
    [Fact]
    public void EveryPaletteEntryIsARealBlock()
    {
        foreach (var style in TestRegistry.Villages().Values)
        {
            foreach (var entry in style.Palette)
            {
                Assert.True(entry.BlockName == "AirBlock" || BlockResolver.TryGetIndex(entry.BlockName, out _),
                    $"{style.Name}: '{entry.BlockName}' is not a Survivalcraft block");
            }
        }
    }

    [Fact]
    public void EveryLayoutBuildsWithLootedChests()
    {
        var pools = LootConfig.Parse(File.ReadAllText(Path.Combine(TestRegistry.AssetsDir(), "Loot", "pools.json")));
        foreach (var style in TestRegistry.Villages().Values)
        {
            int chests = 0;
            for (int i = 0; i < style.Layouts.Length; i++)
            {
                var data = VillageBuilder.BuildFlat(style, i);
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
                    Assert.NotNull(chest.Pool);
                    Assert.True(pools.ContainsKey(chest.Pool!), $"{style.Name}: chest pool '{chest.Pool}' missing");
                }
                chests += data.Chests.Count;
            }
            Assert.True(chests >= style.Layouts.Length, $"{style.Name}: only {chests} chests in all layouts");
        }
    }

    [Fact]
    public void VillageSlotHasRoomInItsCell()
    {
        var def = TestRegistry.FullDefs().Single(d => d.Name == "village");
        foreach (var style in TestRegistry.Villages().Values)
        {
            foreach (var layout in style.Layouts)
            {
                int reach = Math.Max(Math.Max(-layout.MinX, layout.MaxX), Math.Max(-layout.MinZ, layout.MaxZ));
                // Centres are jittered within the middle half of the cell.
                Assert.True(reach < def.GridChunks * 16 / 4, $"{style.Name}: reaches {reach}");
                Assert.True(layout.MaxX - layout.MinX + 1 <= def.Footprint && layout.MaxZ - layout.MinZ + 1 <= def.Footprint,
                    $"{style.Name}: layout outgrows the slot footprint");
            }
        }
    }
}

/// <summary>Synthetic terrain the builder has to cope with.</summary>
public class VillageTerrainTests
{
    private sealed class Sampler : IWorldSampler
    {
        public required Func<int, int, int> Top { get; init; }
        public int OceanLevel { get; init; } = 64;
        public float Height(float x, float z) => Top((int)x, (int)z);
        public int GroundTop(int x, int z) => Top(x, z);
    }

    private static VillageStyle Plains => TestRegistry.Villages()["plains"];

    [Fact]
    public void CentreInTheSeaRefusesTheSite()
    {
        var sea = new Sampler { Top = (_, _) => 50 };
        for (int i = 0; i < Plains.Layouts.Length; i++)
        {
            Assert.Null(VillageBuilder.Build(Plains, i, 0, 0, sea));
        }
    }

    [Fact]
    public void StreetsCrossWaterOnPlanksAtSeaLevel()
    {
        // Dry land in a 60-block disc, sea outside it.
        var island = new Sampler { Top = (x, z) => x * x + z * z < 60 * 60 ? 70 : 55 };
        int bridges = 0;
        for (int i = 0; i < Plains.Layouts.Length; i++)
        {
            var sited = VillageBuilder.Build(Plains, i, 0, 0, island);
            if (sited == null)
            {
                continue;
            }
            foreach (var cell in sited.Data.Cells)
            {
                if (cell.PaletteIndex == Plains.BridgePalette && cell.X * cell.X + cell.Z * cell.Z >= 60 * 60)
                {
                    Assert.Equal(island.OceanLevel, sited.OriginY + cell.Y);
                    bridges++;
                }
            }
            var plan = VillageBuilder.Plan(Plains, i, 0, 0, island)!;
            var layout = Plains.Layouts[i];
            for (int p = 0; p < layout.Pieces.Length; p++)
            {
                var piece = Plains.Pieces[layout.Pieces[p].Ref];
                if (piece.Kind == VillagePieceKind.House && plan.Keep[p])
                {
                    // Kept houses stand on land at every corner.
                    var lp = layout.Pieces[p];
                    foreach (var (x, z) in new[] { (lp.X, lp.Z), (lp.X + piece.SizeX - 1, lp.Z + piece.SizeZ - 1) })
                    {
                        Assert.True(x * x + z * z < 60 * 60, $"house at {x},{z} kept in the sea");
                    }
                }
            }
        }
        Assert.True(bridges > 0, "no layout reached the water");
    }

    [Fact]
    public void HousesHungOffAStreetMeetItAtTheGround()
    {
        // Gentle hills: every house is anchored where its door meets the street.
        var hills = new Sampler { Top = (x, z) => 70 + (int)(4f * MathF.Sin(x * 0.05f) + 3f * MathF.Cos(z * 0.07f)) };
        int checkedHouses = 0;
        for (int i = 0; i < Plains.Layouts.Length; i++)
        {
            var plan = VillageBuilder.Plan(Plains, i, 0, 0, hills);
            if (plan == null)
            {
                continue;
            }
            var layout = Plains.Layouts[i];
            for (int p = 0; p < layout.Pieces.Length; p++)
            {
                var lp = layout.Pieces[p];
                var piece = Plains.Pieces[lp.Ref];
                if (piece.Kind != VillagePieceKind.House || !plan.Keep[p] || !lp.Anchor.OnGround)
                {
                    continue;
                }
                int street = hills.Top(lp.Anchor.X, lp.Anchor.Z);
                // The door connector sits one above the street block it opens on.
                Assert.Equal(street + 1, plan.Floors[p] - lp.Anchor.Dy);
                checkedHouses++;
            }
        }
        Assert.True(checkedHouses > 20, $"only {checkedHouses} houses checked");
    }

    [Fact]
    public void BuildingsAcrossACliffAreLeftOut()
    {
        // A 12-block step along x = 9.
        var cliff = new Sampler { Top = (x, _) => x < 9 ? 70 : 82 };
        int dropped = 0;
        for (int i = 0; i < Plains.Layouts.Length; i++)
        {
            var plan = VillageBuilder.Plan(Plains, i, 0, 0, cliff);
            if (plan == null)
            {
                continue;
            }
            var layout = Plains.Layouts[i];
            for (int p = 0; p < layout.Pieces.Length; p++)
            {
                var lp = layout.Pieces[p];
                var piece = Plains.Pieces[lp.Ref];
                if (piece.Kind != VillagePieceKind.House)
                {
                    continue;
                }
                bool straddles = lp.X < 9 && lp.X + piece.SizeX - 1 >= 9;
                if (straddles)
                {
                    Assert.False(plan.Keep[p], "a house was built across the cliff");
                    dropped++;
                }
            }
        }
        Assert.True(dropped > 0, "no house crossed the cliff; the test proves nothing");
    }
}

/// <summary>
/// Villages generated into chunks of the game's own terrain: the generator
/// runs passes 1-2 for real, then the mod's chunk writer puts the village in.
/// </summary>
public class VillageWorldTests
{
    internal sealed class RealWorld
    {
        public required TerrainContentsGenerator24 Generator { get; init; }
        public required GeneratorSampler Sampler { get; init; }
        public required IReadOnlyList<StructureDef> Defs { get; init; }
        public required int Seed { get; init; }
        public Dictionary<Point2, TerrainChunk> Chunks { get; } = new();

        public static RealWorld Create(int seed)
        {
            var generator = GameGenerator.Create(seed);
            return new RealWorld
            {
                Generator = generator,
                Sampler = new GeneratorSampler(generator),
                Defs = TestRegistry.FullDefs(),
                Seed = seed,
            };
        }

        public WorldState NewState(StructureLedger? ledger = null)
        {
            return new WorldState
            {
                Project = null!,
                Terrain = null!,
                GameInfo = null!,
                BlockEntities = null!,
                Players = null!,
                CreatureSpawn = null!,
                Generator = Generator,
                Placement = new PlacementEngine(Seed, Sampler, Defs),
                LootPools = new Dictionary<string, LootPool>(),
                WorldSeed = Seed,
                Sampler = Sampler,
                Ledger = ledger ?? new StructureLedger(),
            };
        }

        public int VillageIndex => Defs.ToList().FindIndex(d => d.Name == "village");

        /// <summary>The village nearest the origin within a few cells, if any.</summary>
        public StructureInstance? FindVillage(PlacementEngine engine, int radius = 6)
        {
            StructureInstance? best = null;
            for (int cx = -radius; cx <= radius; cx++)
            {
                for (int cz = -radius; cz <= radius; cz++)
                {
                    var instance = engine.GetInstance(VillageIndex, cx, cz);
                    if (instance != null && (best == null
                        || (long)instance.CenterX * instance.CenterX + (long)instance.CenterZ * instance.CenterZ
                           < (long)best.CenterX * best.CenterX + (long)best.CenterZ * best.CenterZ))
                    {
                        best = instance;
                    }
                }
            }
            return best;
        }

        public void Generate(WorldState state, StructureInstance instance)
        {
            for (int cx = PlacementEngine.FloorDiv(instance.WorldMinX, 16); cx <= PlacementEngine.FloorDiv(instance.WorldMaxX, 16); cx++)
            {
                for (int cz = PlacementEngine.FloorDiv(instance.WorldMinZ, 16); cz <= PlacementEngine.FloorDiv(instance.WorldMaxZ, 16); cz++)
                {
                    var chunk = GameGenerator.GenerateTerrain(Generator, cx, cz);
                    state.WriteChunk(chunk);
                    Chunks[new Point2(cx, cz)] = chunk;
                }
            }
        }

        public int Contents(int x, int y, int z)
        {
            var chunk = Chunks[new Point2(PlacementEngine.FloorDiv(x, 16), PlacementEngine.FloorDiv(z, 16))];
            return Terrain.ExtractContents(chunk.GetCellValueFast(x - chunk.Origin.X, y, z - chunk.Origin.Y));
        }
    }

    [Theory]
    [InlineData(12345)]
    [InlineData(-987654321)]
    [InlineData(20260820)]
    public void VillageStandsOnTheRealGround(int seed)
    {
        var world = RealWorld.Create(seed);
        var state = world.NewState();
        var village = world.FindVillage(state.Placement);
        Assert.NotNull(village);
        world.Generate(state, village!);

        string styleName = village!.Variant.Name.Split('_')[1];
        var style = TestRegistry.Villages()[styleName];
        int layoutIndex = int.Parse(village.Variant.Name.Split('_')[2]);
        var layout = style.Layouts[layoutIndex];
        var plan = VillageBuilder.Plan(style, layoutIndex, village.OriginX, village.OriginZ, world.Sampler)!;
        BlockResolver.TryGetIndex("WaterBlock", out int water);

        // Every block the village itself writes: pieces hang smaller pieces
        // (lamps, decorations) inside their own boxes.
        var villageCells = new HashSet<(int, int, int)>();
        foreach (var c in village.Data.Cells)
        {
            villageCells.Add((village.OriginX + c.X, village.OriginY + c.Y, village.OriginZ + c.Z));
        }
        int floored = 0, buried = 0, floating = 0, houses = 0;
        for (int p = 0; p < layout.Pieces.Length; p++)
        {
            var lp = layout.Pieces[p];
            var piece = style.Pieces[lp.Ref];
            if (!plan.Keep[p] || !piece.Rigid || piece.Kind is VillagePieceKind.Well)
            {
                continue;
            }
            if (piece.Kind == VillagePieceKind.House)
            {
                houses++;
            }
            int floor = plan.Floors[p];
            for (int x = 0; x < piece.SizeX; x++)
            {
                for (int z = 0; z < piece.SizeZ; z++)
                {
                    int wx = village.OriginX + lp.X + x;
                    int wz = village.OriginZ + lp.Z + z;
                    if (piece.FloorColumns[x + z * piece.SizeX])
                    {
                        floored++;
                        int below = world.Contents(wx, floor - 1, wz);
                        if (below == 0 || below == water)
                        {
                            floating++;
                        }
                    }
                    if (piece.Kind is VillagePieceKind.House or VillagePieceKind.Center)
                    {
                        // Inside the box, above the ground it hangs from, only the
                        // building's own blocks may remain: no leftover hillside.
                        for (int y = plan.ClearFrom[p]; y < floor + piece.SizeY; y++)
                        {
                            if (!villageCells.Contains((wx, y, wz)) && world.Contents(wx, y, wz) != 0)
                            {
                                buried++;
                            }
                        }
                    }
                }
            }
        }
        Assert.True(houses >= 5, $"only {houses} houses");
        Assert.True(floored > 100, $"only {floored} floor columns");
        Assert.Equal(0, floating);
        Assert.Equal(0, buried);

        // Streets lie on the ground: under every street block on land there is
        // something solid, and over water the street is a bridge at sea level.
        int streetCells = 0, hanging = 0, bridges = 0;
        for (int p = 0; p < layout.Pieces.Length; p++)
        {
            var lp = layout.Pieces[p];
            var piece = style.Pieces[lp.Ref];
            if (piece.Rigid)
            {
                continue;
            }
            foreach (var c in piece.Cells)
            {
                if (c.Y != 0)
                {
                    continue;
                }
                int wx = village.OriginX + lp.X + c.X, wz = village.OriginZ + lp.Z + c.Z;
                int top = world.Sampler.GroundTop(wx, wz);
                if (top < world.Sampler.OceanLevel)
                {
                    bridges++;
                    continue;
                }
                streetCells++;
                if (world.Contents(wx, top, wz) == 0 || world.Contents(wx, top - 1, wz) == 0)
                {
                    hanging++;
                }
            }
        }
        Assert.True(streetCells > 200, $"only {streetCells} street cells");
        Assert.Equal(0, hanging);

        Preview.Write(world, village, $"village_{seed}_{village.Variant.Name}");
    }

    /// <summary>
    /// The world rebuilds a village from its ledger record, chunk by chunk, so
    /// the rebuilt one must be block for block the one first written.
    /// </summary>
    [Fact]
    public void LedgerRebuildsTheSameVillage()
    {
        var world = RealWorld.Create(12345);
        var first = world.NewState();
        var village = world.FindVillage(first.Placement)!;
        int cx = PlacementEngine.FloorDiv(village.CenterX, 16);
        int cz = PlacementEngine.FloorDiv(village.CenterZ, 16);
        var a = GameGenerator.GenerateTerrain(world.Generator, cx, cz);
        first.WriteChunk(a);

        var ledger = StructureLedger.Decode(first.Ledger.Encode());
        var second = world.NewState(ledger);
        // Same ledger, a different table: the village must still come back.
        Assert.False(second.MixedLand);
        second.ApplyDensity(0f);
        // Land written under the old table now exists, so new candidates must
        // prove their ground is untouched from here on.
        Assert.True(second.MixedLand);
        var b = GameGenerator.GenerateTerrain(world.Generator, cx, cz);
        second.WriteChunk(b);
        Assert.Equal(a.Cells, b.Cells);
        Assert.NotEqual(GameGenerator.GenerateTerrain(world.Generator, cx, cz).Cells, b.Cells);
    }

    /// <summary>
    /// How often villages turn up on real worlds. The plan was a village every
    /// 6-8 km² of land: rarer than temples, because a village is a landmark.
    /// Measured over land only: the area around the origin is two thirds sea.
    /// </summary>
    [Fact]
    public void VillagesAreLandmarkRare()
    {
        int villages = 0;
        double landKm2 = 0;
        var styles = new Dictionary<string, int>();
        foreach (int seed in new[] { 12345, -987654321, 20260820, 777, 31337 })
        {
            var world = RealWorld.Create(seed);
            var engine = new PlacementEngine(seed, world.Sampler, world.Defs);
            int span = world.Defs[world.VillageIndex].GridChunks * 16;
            int land = 0, samples = 4000;
            var rng = new System.Random(seed);
            for (int i = 0; i < samples; i++)
            {
                if (world.Sampler.GroundTop(rng.Next(-4 * span, 4 * span), rng.Next(-4 * span, 4 * span)) > world.Sampler.OceanLevel)
                {
                    land++;
                }
            }
            landKm2 += 64 * (span / 1000.0) * (span / 1000.0) * land / samples;
            for (int cx = -4; cx < 4; cx++)
            {
                for (int cz = -4; cz < 4; cz++)
                {
                    var v = engine.GetInstance(world.VillageIndex, cx, cz);
                    if (v != null)
                    {
                        villages++;
                        string style = v.Variant.Name.Split('_')[1];
                        styles[style] = styles.GetValueOrDefault(style) + 1;
                    }
                }
            }
        }
        double perVillage = landKm2 / Math.Max(1, villages);
        string detail = $"{villages} villages on {landKm2:0} km² of land, one per {perVillage:0.0} km² ("
            + string.Join(", ", styles.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}")) + ")";
        TestContext.Current.TestOutputHelper?.WriteLine(detail);
        Assert.True(perVillage >= 5 && perVillage <= 9, detail);
        Assert.True(styles.Count >= 3, detail);
    }
}

/// <summary>
/// Top-down pictures of generated villages, written only when
/// RUINS_PREVIEW_DIR is set, for a human to look at.
/// </summary>
internal static class Preview
{
    public static void Write(VillageWorldTests.RealWorld world, StructureInstance village, string name)
    {
        string? dir = Environment.GetEnvironmentVariable("RUINS_PREVIEW_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }
        Directory.CreateDirectory(dir);
        int x0 = village.WorldMinX - 8, z0 = village.WorldMinZ - 8;
        int w = village.WorldMaxX - village.WorldMinX + 17, h = village.WorldMaxZ - village.WorldMinZ + 17;
        int scale = 3;
        var pixels = new byte[w * scale * h * scale * 3];
        var names = new Dictionary<int, string>();
        foreach (var block in new[] { "GravelBlock", "PlanksBlock", "CobblestoneBlock", "WaterBlock", "GlassBlock", "SandstoneBlock",
                     "SandBlock", "DirtBlock", "GrassBlock", "SoilBlock", "RyeBlock", "BricksBlock", "SnowBlock", "WoodenStairsBlock" })
        {
            if (BlockResolver.TryGetIndex(block, out int index))
            {
                names[index] = block;
            }
        }
        for (int dz = 0; dz < h; dz++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                int wx = x0 + dx, wz = z0 + dz;
                var key = new Point2(PlacementEngine.FloorDiv(wx, 16), PlacementEngine.FloorDiv(wz, 16));
                (byte r, byte g, byte b) colour = (20, 20, 20);
                if (world.Chunks.TryGetValue(key, out var chunk))
                {
                    int lx = wx - chunk.Origin.X, lz = wz - chunk.Origin.Y;
                    for (int y = 254; y > 0; y--)
                    {
                        int contents = Terrain.ExtractContents(chunk.GetCellValueFast(lx, y, lz));
                        if (contents == 0)
                        {
                            continue;
                        }
                        colour = Colour(names.GetValueOrDefault(contents, contents == 3 || contents == 67 ? "Stone" : "Other"), y);
                        break;
                    }
                }
                for (int sy = 0; sy < scale; sy++)
                {
                    for (int sx = 0; sx < scale; sx++)
                    {
                        int i = ((dz * scale + sy) * w * scale + dx * scale + sx) * 3;
                        pixels[i] = colour.r;
                        pixels[i + 1] = colour.g;
                        pixels[i + 2] = colour.b;
                    }
                }
            }
        }
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), Png(w * scale, h * scale, pixels));
    }

    private static (byte, byte, byte) Colour(string block, int y)
    {
        (int r, int g, int b) c = block switch
        {
            "GravelBlock" => (160, 150, 130),
            "PlanksBlock" => (185, 145, 90),
            "CobblestoneBlock" => (125, 125, 125),
            "WaterBlock" => (50, 90, 210),
            "GlassBlock" => (190, 225, 235),
            "SandstoneBlock" or "SandBlock" => (225, 205, 145),
            "DirtBlock" => (120, 85, 55),
            "GrassBlock" => (90, 160, 70),
            "SoilBlock" => (90, 60, 40),
            "RyeBlock" => (200, 190, 80),
            "BricksBlock" => (170, 90, 70),
            "SnowBlock" => (240, 240, 250),
            "WoodenStairsBlock" => (150, 110, 70),
            "Stone" => (105, 110, 105),
            _ => (200, 80, 200),
        };
        float shade = Math.Clamp(0.55f + (y - 60) * 0.015f, 0.4f, 1.3f);
        return ((byte)Math.Clamp(c.r * shade, 0, 255), (byte)Math.Clamp(c.g * shade, 0, 255), (byte)Math.Clamp(c.b * shade, 0, 255));
    }

    private static byte[] Png(int width, int height, byte[] rgb)
    {
        using var raw = new MemoryStream();
        for (int y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgb, y * width * 3, width * 3);
        }
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(z);
        }
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        WriteBig(header, 0, width);
        WriteBig(header, 4, height);
        header[8] = 8;
        header[9] = 2;
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBig(len, 0, data.Length);
        s.Write(len);
        var typed = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type, 0, 4, typed, 0);
        Buffer.BlockCopy(data, 0, typed, 4, data.Length);
        s.Write(typed);
        var crc = new byte[4];
        WriteBig(crc, 0, (int)Crc32(typed));
        s.Write(crc);
    }

    private static void WriteBig(byte[] b, int o, int v)
    {
        b[o] = (byte)(v >> 24);
        b[o + 1] = (byte)(v >> 16);
        b[o + 2] = (byte)(v >> 8);
        b[o + 3] = (byte)v;
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte d in data)
        {
            crc ^= d;
            for (int k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return ~crc;
    }
}

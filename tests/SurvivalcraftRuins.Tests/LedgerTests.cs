using SurvivalcraftRuins.Core;
using Xunit;

namespace SurvivalcraftRuins.Tests;

/// <summary>A chunk file holding exactly the chunks it was handed.</summary>
internal sealed class FakeChunkProbe : IChunkProbe
{
    private readonly HashSet<(int, int)> m_existing;

    public int Queries { get; private set; }

    public FakeChunkProbe(params (int X, int Z)[] existing)
    {
        m_existing = new HashSet<(int, int)>(existing);
    }

    public bool Exists(int chunkX, int chunkZ)
    {
        Queries++;
        return m_existing.Contains((chunkX, chunkZ));
    }
}

public class LedgerRecordTests
{
    private static LedgerRecord Sample()
    {
        return new LedgerRecord
        {
            Key = StructureLedger.MakeKey("sky", -3, 12),
            Approved = true,
            Def = "sky",
            Variant = "sky_fortress",
            Seed = -1234567,
            OriginX = -1904,
            OriginY = 190,
            OriginZ = 7712,
            MinX = -1904,
            MinZ = 7712,
            MaxX = -1841,
            MaxZ = 7775,
        };
    }

    [Fact]
    public void ApprovedRecordSurvivesEncoding()
    {
        var original = Sample();
        var decoded = LedgerRecord.TryDecode(original.Encode());

        Assert.NotNull(decoded);
        Assert.Equal(original.Key, decoded!.Key);
        Assert.True(decoded.Approved);
        Assert.Equal(original.Def, decoded.Def);
        Assert.Equal(original.Variant, decoded.Variant);
        Assert.Equal(original.Seed, decoded.Seed);
        Assert.Equal(original.OriginX, decoded.OriginX);
        Assert.Equal(original.OriginY, decoded.OriginY);
        Assert.Equal(original.OriginZ, decoded.OriginZ);
        Assert.Equal(original.MinX, decoded.MinX);
        Assert.Equal(original.MinZ, decoded.MinZ);
        Assert.Equal(original.MaxX, decoded.MaxX);
        Assert.Equal(original.MaxZ, decoded.MaxZ);
    }

    [Fact]
    public void RejectedRecordEncodesToKeyAndFlagOnly()
    {
        var record = new LedgerRecord { Key = "small:1:2", Approved = false };
        Assert.Equal("small:1:2|0", record.Encode());

        var decoded = LedgerRecord.TryDecode(record.Encode());
        Assert.NotNull(decoded);
        Assert.False(decoded!.Approved);
        Assert.Equal("small:1:2", decoded.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("|1")]
    [InlineData("only_a_key")]
    [InlineData("truncated|1|sky|sky_fortress|1|2|3")]
    [InlineData("badnumber|1|sky|sky_fortress|x|2|3|4|5|6|7|8")]
    public void MalformedLinesAreDroppedRatherThanThrowing(string line)
    {
        Assert.Null(LedgerRecord.TryDecode(line));
    }
}

public class StructureLedgerTests
{
    private static LedgerRecord Approved(string key, int minX, int minZ, int maxX, int maxZ)
    {
        return new LedgerRecord
        {
            Key = key,
            Approved = true,
            Def = "small",
            Variant = "hunter_camp",
            Seed = 7,
            OriginX = minX,
            OriginY = 70,
            OriginZ = minZ,
            MinX = minX,
            MinZ = minZ,
            MaxX = maxX,
            MaxZ = maxZ,
        };
    }

    [Fact]
    public void ApprovedRecordIsFoundFromEveryChunkItCovers()
    {
        // Straddles chunks (0,0) and (1,1): x 8..20, z 8..20.
        var ledger = StructureLedger.Decode(new[] { Approved("small:0:0", 8, 8, 20, 20).Encode() });

        foreach (var (cx, cz) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
        {
            Assert.Single(ledger.RecordsForChunk(cx, cz));
        }
        Assert.Empty(ledger.RecordsForChunk(2, 0));
        Assert.Empty(ledger.RecordsForChunk(-1, 0));
    }

    [Fact]
    public void NegativeCoordinatesIndexOntoTheRightChunks()
    {
        var ledger = StructureLedger.Decode(new[] { Approved("small:-1:-1", -20, -20, -9, -9).Encode() });

        Assert.Single(ledger.RecordsForChunk(-2, -2));
        Assert.Single(ledger.RecordsForChunk(-1, -1));
        Assert.Empty(ledger.RecordsForChunk(0, 0));
    }

    [Fact]
    public void AVerdictIsFrozenOnFirstSight()
    {
        var ledger = new StructureLedger();
        ledger.Reject("small:0:0");

        Assert.True(ledger.Contains("small:0:0"));
        Assert.Empty(ledger.RecordsForChunk(0, 0));
        Assert.True(ledger.TryGet("small:0:0", out var record));
        Assert.False(record.Approved);
    }

    [Fact]
    public void LedgerSurvivesASaveLoadCycle()
    {
        var ledger = StructureLedger.Decode(new[]
        {
            Approved("small:0:0", 8, 8, 20, 20).Encode(),
            "grand:2:2|0",
        });

        var reloaded = StructureLedger.Decode(ledger.Encode());

        Assert.Equal(2, reloaded.Count);
        Assert.Single(reloaded.RecordsForChunk(1, 1));
        Assert.True(reloaded.TryGet("grand:2:2", out var rejected));
        Assert.False(rejected.Approved);
    }

    [Fact]
    public void EncodingIsStableSoSavesDoNotChurn()
    {
        var lines = new[] { "small:0:0|0", "grand:2:2|0", "sky:-1:4|0" };
        Assert.Equal(
            StructureLedger.Decode(lines).Encode(),
            StructureLedger.Decode(new[] { lines[2], lines[0], lines[1] }).Encode());
    }

    [Fact]
    public void KeysSeparateSlotsAndCells()
    {
        Assert.Equal("sky:-3:12", StructureLedger.MakeKey("sky", -3, 12));
        Assert.NotEqual(StructureLedger.MakeKey("sky", 1, 2), StructureLedger.MakeKey("sky", 2, 1));
    }
}

public class FrontierTests
{
    [Fact]
    public void VirginGroundAdmitsTheStructure()
    {
        var probe = new FakeChunkProbe();
        Assert.True(StructureLedger.FootprintUntouched(8, 8, 40, 40, 0, 0, probe));
    }

    [Fact]
    public void OneAlreadyGeneratedChunkUnderTheFootprintRejectsIt()
    {
        // The far corner of the building sits in land generated last session.
        var probe = new FakeChunkProbe((2, 2));
        Assert.False(StructureLedger.FootprintUntouched(8, 8, 40, 40, 0, 0, probe));
    }

    [Fact]
    public void TheChunkBeingGeneratedIsNotCountedAgainstIt()
    {
        // A probe that claimed the current chunk exists would reject everything.
        var probe = new FakeChunkProbe((0, 0), (1, 1));
        Assert.False(StructureLedger.FootprintUntouched(8, 8, 20, 20, 0, 0, probe));
        Assert.True(StructureLedger.FootprintUntouched(8, 8, 15, 15, 0, 0, probe));
    }

    [Fact]
    public void SingleChunkStructuresNeverProbeAnything()
    {
        var probe = new FakeChunkProbe((0, 0));
        Assert.True(StructureLedger.FootprintUntouched(2, 2, 13, 13, 0, 0, probe));
        Assert.Equal(0, probe.Queries);
    }
}

public class DensityKnobTests
{
    private static IReadOnlyList<StructureDef> Defs()
    {
        return StructureRegistry.CreateDefs(TestRegistry.Templates());
    }

    private static int CountLand(float density, string defName)
    {
        var defs = Defs();
        int index = -1;
        for (int i = 0; i < defs.Count; i++)
        {
            if (defs[i].Name == defName)
            {
                index = i;
            }
        }
        Assert.True(index >= 0);
        var engine = new PlacementEngine(4242, new FlatSampler(80f), defs, density);
        int found = 0;
        for (int cx = -12; cx <= 12; cx++)
        {
            for (int cz = -12; cz <= 12; cz++)
            {
                if (engine.GetInstance(index, cx, cz) != null)
                {
                    found++;
                }
            }
        }
        return found;
    }

    [Fact]
    public void ZeroDensityGeneratesNothingAtAll()
    {
        var defs = Defs();
        var engine = new PlacementEngine(1, new FlatSampler(80f), defs, 0f);
        foreach (var def in defs)
        {
            Assert.Equal(0f, engine.DensityFactor(def));
        }
        Assert.Equal(0, CountLand(0f, "small"));
    }

    [Fact]
    public void TheSliderMovesSmallFindsAtFaceValue()
    {
        var engine = new PlacementEngine(1, new FlatSampler(80f), Defs(), 2f);
        var small = Defs().Single(d => d.Name == "small");
        Assert.Equal(2f, engine.DensityFactor(small), 3);
        Assert.True(CountLand(2f, "small") > CountLand(1f, "small"));
    }

    [Fact]
    public void LandmarksFeelTheSliderAtHalfStrength()
    {
        var engine = new PlacementEngine(1, new FlatSampler(80f), Defs(), 4f);
        var sky = Defs().Single(d => d.Name == "sky");
        Assert.Equal(0.5f, sky.DensityExponent);
        Assert.Equal(2f, engine.DensityFactor(sky), 3);
    }

    /// <summary>
    /// The roll itself is monotone: the per cell hash does not depend on the
    /// setting, so turning density up can only turn empty cells into occupied
    /// ones. This is what makes the slider safe to touch mid-world.
    /// </summary>
    [Fact]
    public void RaisingDensityOnlyEverAddsCandidates()
    {
        var defs = Defs();
        var sparse = new PlacementEngine(99, new FlatSampler(80f), defs, 1f);
        var dense = new PlacementEngine(99, new FlatSampler(80f), defs, 2f);
        foreach (var def in defs)
        {
            for (int cx = -20; cx <= 20; cx++)
            {
                for (int cz = -20; cz <= 20; cz++)
                {
                    float roll = DeterministicHash.ToUnitFloat(
                        DeterministicHash.Mix(99, def.Salt, cx, cz));
                    if (roll < def.Chance * sparse.DensityFactor(def))
                    {
                        Assert.True(roll < def.Chance * dense.DensityFactor(def),
                            $"{def.Name} cell {cx},{cz} lost its candidate when density went up");
                    }
                }
            }
        }
    }

    /// <summary>
    /// And a candidate that survives the change is the same building in the
    /// same place: same variant, same seed, same origin. The one thing density
    /// can still take away is a small structure that a newly added landmark
    /// now stands too close to — which is exactly why the world ledger, not the
    /// table, is what decides whether a structure gets built.
    /// </summary>
    [Fact]
    public void AStructureThatSurvivesTheChangeIsUnmoved()
    {
        var defs = Defs();
        var sparse = new PlacementEngine(99, new FlatSampler(80f), defs, 1f);
        var dense = new PlacementEngine(99, new FlatSampler(80f), defs, 2f);
        int same = 0, crowdedOut = 0;
        for (int i = 0; i < defs.Count; i++)
        {
            for (int cx = -6; cx <= 6; cx++)
            {
                for (int cz = -6; cz <= 6; cz++)
                {
                    var before = sparse.GetInstance(i, cx, cz);
                    if (before == null)
                    {
                        continue;
                    }
                    var after = dense.GetInstance(i, cx, cz);
                    if (after == null)
                    {
                        crowdedOut++;
                        continue;
                    }
                    Assert.Equal(before.Variant.Name, after.Variant.Name);
                    Assert.Equal(before.InstanceSeed, after.InstanceSeed);
                    Assert.Equal((before.OriginX, before.OriginY, before.OriginZ),
                        (after.OriginX, after.OriginY, after.OriginZ));
                    same++;
                }
            }
        }
        Assert.True(same > 20, $"only {same} instances compared");
        Assert.True(crowdedOut * 10 < same, $"{crowdedOut} of {same + crowdedOut} were crowded out");
    }

    /// <summary>
    /// Every slot's cell must stay comfortably wider than the biggest thing it
    /// can hold, or the jitter that keeps neighbours apart has nowhere to go.
    /// </summary>
    [Fact]
    public void EverySlotStillFitsInsideItsGridCell()
    {
        foreach (var def in TestRegistry.FullDefs())
        {
            int span = def.GridChunks * 16;
            foreach (var variant in def.Variants)
            {
                var data = variant.Bounds is { } declared
                    ? declared
                    : StructureBounds.From(variant.Provider(1));
                Assert.True(data.SizeX * 2 < span && data.SizeZ * 2 < span,
                    $"{def.Name}/{variant.Name} is {data.SizeX}x{data.SizeZ} in a {span} cell");
                Assert.True(data.SizeX <= def.Footprint
                    && data.SizeZ <= def.Footprint,
                    $"{def.Name}/{variant.Name} outgrows the neighbourhood search");
            }
        }
    }

    [Fact]
    public void SettingsRangeCoversTheAdvertisedSlider()
    {
        Assert.Equal(0f, RuinsSettings.MinScale);
        Assert.Equal(4f, RuinsSettings.MaxScale);
    }
}

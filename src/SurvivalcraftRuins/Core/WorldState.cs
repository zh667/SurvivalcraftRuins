using System.Collections.Concurrent;
using Engine;
using Game;
using GameEntitySystem;
using System.Text.Json;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// A container written by worldgen that still needs its contents. Older saves
/// predate <see cref="Container"/>, whose empty value means "Chest".
/// </summary>
public sealed class PendingChest
{
    public int X, Y, Z;
    public string Pool = "";
    public int Seed;
    /// <summary>Entity template of the block entity: "Chest" or "Dispenser".</summary>
    public string Container = "";

    public string ContainerTemplate => string.IsNullOrEmpty(Container) ? "Chest" : Container;
}

public sealed class AmbushState
{
    public string Id = "";
    public int X, Y, Z;
    public string Creature = "";
    public int Min, Max;
    public float Radius;
    public bool Triggered;
    public int Seed;
    /// <summary>Guards spawn in water rather than in an air pocket.</summary>
    public bool Aquatic;
    /// <summary>Stand-in used when supernatural creatures are disabled; empty means skip.</summary>
    public string Substitute = "";
}

public sealed class RuinsSaveData
{
    // System.Text.Json needs IncludeFields for these plain field DTOs.

    public List<AmbushState> Ambushes = new();
    public List<PendingChest> Chests = new();

    /// <summary>
    /// Density in force when this save was last written. Recorded for support
    /// and for the settings screen; it no longer constrains anything, because
    /// the ledger — not the density setting — decides what gets built.
    /// </summary>
    public float DensityScale;

    /// <summary>
    /// Placement table generation that last wrote chunks here. Kept for
    /// diagnostics and to notice the first time a world meets a new table.
    /// </summary>
    public int LayoutVersion;

    /// <summary>
    /// True once this world is known to hold land generated under a different
    /// placement table than the current one — including land generated before
    /// the mod was installed at all. Sticky: a mixed world stays mixed, because
    /// its old frontier is still out there waiting to be walked into.
    /// </summary>
    public bool MixedLand;

    /// <summary>
    /// Encoded <see cref="LedgerRecord"/> lines: every candidate this world has
    /// ever ruled on. See <see cref="StructureLedger"/>.
    /// </summary>
    public List<string> Ledger = new();
}

/// <summary>Adapts the game's terrain generator to the testable sampler interface.</summary>
public sealed class GeneratorSampler : IWorldSampler
{
    private readonly ITerrainContentsGenerator m_generator;

    public GeneratorSampler(ITerrainContentsGenerator generator)
    {
        m_generator = generator;
    }

    public int OceanLevel => m_generator.OceanLevel;

    public float Height(float x, float z)
    {
        return m_generator.CalculateHeight(x, z);
    }

    public int Temperature(float x, float z)
    {
        return m_generator.CalculateTemperature(x, z);
    }

    public int Humidity(float x, float z)
    {
        return m_generator.CalculateHumidity(x, z);
    }
}

/// <summary>
/// Per-world runtime state. Chunk writing happens on the terrain thread; chest
/// entity creation and ambush spawning happen on the main thread via Update.
/// </summary>
public sealed class WorldState
{
    public required Project Project { get; init; }
    public required SubsystemTerrain Terrain { get; init; }
    public required SubsystemGameInfo GameInfo { get; init; }
    public required SubsystemBlockEntities BlockEntities { get; init; }
    public required SubsystemPlayers Players { get; init; }
    public required SubsystemCreatureSpawn CreatureSpawn { get; init; }
    public required ITerrainContentsGenerator Generator { get; init; }

    /// <summary>
    /// The placement table in force. Replaced wholesale when the player moves
    /// the density slider; the terrain thread only ever reads the reference, so
    /// a chunk already being generated finishes with the engine it started on.
    /// </summary>
    public required PlacementEngine Placement { get; set; }

    public required IReadOnlyDictionary<string, LootPool> LootPools { get; init; }

    /// <summary>World seed and terrain sampler, kept so the engine can be rebuilt.</summary>
    public required int WorldSeed { get; init; }
    public required IWorldSampler Sampler { get; init; }

    /// <summary>Everything this world has already committed to; the authority over the table.</summary>
    public StructureLedger Ledger { get; init; } = new();

    /// <summary>Tells whether a chunk is already on disk; null when the runtime cannot say.</summary>
    public IChunkProbe? ChunkProbe { get; init; }

    /// <summary>
    /// Set when this world holds land from an older placement table. Only then
    /// does a fresh candidate have to prove its whole footprint is untouched
    /// ground before it may be built.
    /// </summary>
    public bool MixedLand { get; set; }

    // Markers discovered on the terrain thread, drained on the main thread.
    private static readonly JsonSerializerOptions s_jsonOptions = new() { IncludeFields = true };

    public readonly ConcurrentQueue<PendingChest> IncomingChests = new();
    public readonly ConcurrentQueue<AmbushState> IncomingAmbushes = new();

    // Main-thread state, persisted to the world directory.
    public readonly List<PendingChest> PendingChests = new();
    public readonly Dictionary<string, AmbushState> Ambushes = new();
    public bool Dirty;
    public double LastSaveTime;

    private static readonly ConcurrentDictionary<StructureData, (short X, short Z)[]> s_baseColumns = new();
    private static readonly ConcurrentDictionary<StructureData, ChestMarker[]> s_sortedChests = new();

    /// <summary>Guard post budget per variant; per-world so it dies with the world.</summary>
    private readonly ConcurrentDictionary<StructureVariant, GuardPost[]> m_guardPosts = new();

    /// <summary>Structures rebuilt from ledger records, keyed by ledger key.</summary>
    private readonly ConcurrentDictionary<string, StructureInstance?> m_rebuilt = new(StringComparer.Ordinal);

    public string SavePath => Storage.CombinePaths(GameInfo.DirectoryName, "SurvivalcraftRuins.json");

    /// <summary>Density the current placement engine was built with.</summary>
    public float DensityScale { get; set; } = 1f;

    /// <summary>
    /// Rebuilds the placement table after the density slider moves. The engine
    /// takes its density at construction, so without this the setting appeared
    /// to do nothing until the world was next loaded — the worst possible
    /// behaviour for a knob whose whole point is that players can feel it. What
    /// is already in the ledger is untouched, so nothing standing moves.
    /// </summary>
    public void ApplyDensity(float density)
    {
        if (density == DensityScale)
        {
            return;
        }
        Placement = new PlacementEngine(WorldSeed, Sampler, Placement.Defs, density);
        DensityScale = density;
        // Land generated before the change was ruled on by the old table, so
        // from now on a new candidate must prove its ground is untouched.
        MixedLand = true;
        Dirty = true;
    }

    /// <summary>Placement table generation this world was created with.</summary>
    public int LayoutVersion { get; init; } = StructureRegistry.CurrentLayoutVersion;

    /// <summary>Live multiplier on guard counts; unlike density this may change any time.</summary>
    public float GuardScale => RuinsSettings.Guards;

    /// <summary>Reads a world's save file, or null when it has none yet.</summary>
    public static RuinsSaveData? ReadSaveData(string directoryName)
    {
        string path = Storage.CombinePaths(directoryName, "SurvivalcraftRuins.json");
        try
        {
            if (!Storage.FileExists(path))
            {
                return null;
            }
            using var stream = Storage.OpenFile(path, OpenFileMode.Read);
            using var reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<RuinsSaveData>(reader.ReadToEnd(), s_jsonOptions);
        }
        catch (Exception exception)
        {
            Log.Warning($"[Ruins] Failed to load persistence: {exception.Message}");
            return null;
        }
    }

    public void LoadPersistence(RuinsSaveData? data)
    {
        if (data == null)
        {
            return;
        }
        foreach (var ambush in data.Ambushes)
        {
            Ambushes[ambush.Id] = ambush;
        }
        PendingChests.AddRange(data.Chests);
    }

    public void SavePersistence()
    {
        try
        {
            var data = new RuinsSaveData
            {
                Ambushes = new List<AmbushState>(Ambushes.Values),
                Chests = new List<PendingChest>(PendingChests),
                DensityScale = DensityScale,
                LayoutVersion = LayoutVersion,
                MixedLand = MixedLand,
                Ledger = Ledger.Encode(),
            };
            string json = JsonSerializer.Serialize(data, s_jsonOptions);
            using var stream = Storage.OpenFile(SavePath, OpenFileMode.Create);
            using var writer = new StreamWriter(stream);
            writer.Write(json);
            Dirty = false;
        }
        catch (Exception exception)
        {
            Log.Warning($"[Ruins] Failed to save persistence: {exception.Message}");
        }
    }

    /// <summary>
    /// Writes every structure part intersecting the chunk. Runs on the terrain
    /// thread right after content pass 3, so vanilla pass 4 decoration
    /// (plants, trees) happens around the finished structure.
    ///
    /// Two sources feed it, in this order: structures this world has already
    /// committed to (the ledger), then candidates the current table proposes
    /// that the world has never ruled on. A new candidate is only admitted if
    /// the whole footprint is still unwritten ground, so a table change can add
    /// buildings to virgin land without ever slicing one along the frontier.
    /// </summary>
    public void WriteChunk(TerrainChunk chunk)
    {
        int baseX = chunk.Coords.X * 16;
        int baseZ = chunk.Coords.Y * 16;

        foreach (var record in Ledger.RecordsForChunk(chunk.Coords.X, chunk.Coords.Y))
        {
            var committed = Rebuild(record);
            if (committed != null)
            {
                WriteInstancePart(chunk, committed, baseX, baseZ);
            }
        }

        foreach (var instance in Placement.InstancesIntersecting(baseX, baseZ, baseX + 15, baseZ + 15))
        {
            string key = instance.LedgerKey;
            if (Ledger.Contains(key))
            {
                continue;
            }
            if (!IsGroundUntouched(instance, chunk.Coords))
            {
                Ledger.Reject(key);
                Dirty = true;
                continue;
            }
            // Written whoever wins the record: chunk generation is single
            // threaded on every path this hook runs on, and if that ever
            // changes, both threads computed the same instance from the same
            // table, so writing it twice puts down the same blocks. Skipping
            // instead would leave this chunk's slice missing for good.
            Ledger.Approve(key, instance);
            Dirty = true;
            WriteInstancePart(chunk, instance, baseX, baseZ);
        }
    }

    /// <summary>
    /// True when no chunk under the instance has been generated yet, so the
    /// whole building can still be written as its chunks come up. Worlds that
    /// have only ever seen the current table skip the check: every chunk of
    /// theirs already ruled on this candidate when it was generated.
    /// </summary>
    private bool IsGroundUntouched(StructureInstance instance, Point2 generating)
    {
        if (!MixedLand || ChunkProbe == null)
        {
            return true;
        }
        return StructureLedger.FootprintUntouched(
            instance.WorldMinX, instance.WorldMinZ, instance.WorldMaxX, instance.WorldMaxZ,
            generating.X, generating.Y, ChunkProbe);
    }

    /// <summary>
    /// Rebuilds a committed structure from its own record. The shape comes back
    /// from the variant's provider and the recorded seed, so it survives any
    /// change to the placement table; only editing a builder or a template can
    /// still change what an existing structure looks like.
    /// </summary>
    private StructureInstance? Rebuild(LedgerRecord record)
    {
        if (m_rebuilt.TryGetValue(record.Key, out var cached))
        {
            return cached;
        }
        StructureInstance? instance = null;
        try
        {
            foreach (var def in Placement.Defs)
            {
                if (!string.Equals(def.Name, record.Def, StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (var variant in def.Variants)
                {
                    if (!string.Equals(variant.Name, record.Variant, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    instance = new StructureInstance
                    {
                        Def = def,
                        Variant = variant,
                        Data = variant.Provider(record.Seed),
                        OriginX = record.OriginX,
                        OriginY = record.OriginY,
                        OriginZ = record.OriginZ,
                        InstanceSeed = record.Seed,
                    };
                    break;
                }
                break;
            }
            if (instance == null)
            {
                Log.Warning($"[Ruins] Ledger entry '{record.Key}' names a structure this build does not have.");
            }
        }
        catch (Exception exception)
        {
            Log.Warning($"[Ruins] Rebuilding ledger entry '{record.Key}' failed: {exception.Message}");
        }
        m_rebuilt[record.Key] = instance;
        return instance;
    }

    private void WriteInstancePart(TerrainChunk chunk, StructureInstance instance, int baseX, int baseZ)
    {
        var data = instance.Data;
        var def = instance.Def;
        var variant = instance.Variant;
        var weather = variant.Weather;
        int ox = instance.OriginX, oy = instance.OriginY, oz = instance.OriginZ;

        if (def.Anchor is AnchorKind.Land or AnchorKind.Coast)
        {
            WriteFoundation(chunk, instance, baseX, baseZ);
        }

        var sortedChests = s_sortedChests.GetOrAdd(data, static d =>
        {
            var array = d.Chests.ToArray();
            Array.Sort(array, (a, b) =>
            {
                int c = a.Y.CompareTo(b.Y);
                if (c != 0) return c;
                c = a.Z.CompareTo(b.Z);
                if (c != 0) return c;
                return a.X.CompareTo(b.X);
            });
            return array;
        });

        foreach (var cell in data.Cells)
        {
            int wx = ox + cell.X;
            int wz = oz + cell.Z;
            if (wx < baseX || wx > baseX + 15 || wz < baseZ || wz > baseZ + 15)
            {
                continue;
            }
            int wy = oy + cell.Y;
            if (wy < 1 || wy > 254)
            {
                continue;
            }
            var entry = data.Palette[cell.PaletteIndex];
            string blockName = entry.BlockName;

            bool weatherImmune = blockName is "AirBlock" or "ChestBlock" or "TreasureGeneratorBlock" or "WickerLampBlock";
            if (!weatherImmune && weather.DecayChance > 0f)
            {
                uint cellHash = DeterministicHash.Mix(
                    (uint)instance.InstanceSeed,
                    DeterministicHash.Mix((uint)cell.X, DeterministicHash.Mix((uint)cell.Y, (uint)cell.Z)));
                if (DeterministicHash.ToUnitFloat(cellHash) < weather.DecayChance)
                {
                    continue;
                }
                if (weather.SwapChance > 0f
                    && weather.Swaps.TryGetValue(blockName, out string? swapped)
                    && DeterministicHash.ToUnitFloat(DeterministicHash.Mix(cellHash, 7u)) < weather.SwapChance)
                {
                    blockName = swapped;
                }
            }

            chunk.SetCellValueFast(wx - baseX, wy, wz - baseZ, BlockResolver.MakeValue(blockName, entry.Data));

            if (blockName == "ChestBlock")
            {
                int chestIndex = Array.FindIndex(sortedChests, m => m.X == cell.X && m.Y == cell.Y && m.Z == cell.Z);
                EnqueueContainer(instance, wx, wy, wz, ResolveChestPool(variant, chestIndex), "Chest");
            }
            else if (blockName == "DispenserBlock" && variant.DispenserPool != null)
            {
                EnqueueContainer(instance, wx, wy, wz, variant.DispenserPool, "Dispenser");
            }
        }

        if (variant.Guards != null)
        {
            foreach (var post in m_guardPosts.GetOrAdd(variant, static (v, d) => GuardPlanner.Plan(v, d), data))
            {
                int wx = ox + post.X;
                int wz = oz + post.Z;
                if (wx < baseX || wx > baseX + 15 || wz < baseZ || wz > baseZ + 15)
                {
                    continue;
                }
                int wy = Math.Clamp(oy + post.Y, 1, 254);
                uint postHash = DeterministicHash.Mix((uint)instance.InstanceSeed, (uint)(wx * 31 ^ wz));
                var creatures = variant.Guards.Creatures;
                string creature = creatures[(int)(postHash % (uint)creatures.Count)];
                IncomingAmbushes.Enqueue(new AmbushState
                {
                    Id = $"{variant.Name}:{wx},{wy},{wz}",
                    X = wx,
                    Y = wy,
                    Z = wz,
                    Creature = creature,
                    Min = post.Count,
                    Max = post.Count,
                    Radius = variant.Guards.TriggerRadius,
                    Aquatic = variant.Guards.Aquatic,
                    Substitute = variant.Guards.MundaneSubstitute ?? "",
                    Seed = (int)DeterministicHash.Mix(postHash, 0x60ADu),
                });
            }
        }
    }

    private void EnqueueContainer(StructureInstance instance, int wx, int wy, int wz, string pool, string container)
    {
        IncomingChests.Enqueue(new PendingChest
        {
            X = wx,
            Y = wy,
            Z = wz,
            Pool = pool,
            Container = container,
            Seed = (int)DeterministicHash.Mix((uint)instance.InstanceSeed,
                DeterministicHash.Mix((uint)wx, DeterministicHash.Mix((uint)wy, (uint)wz))),
        });
    }

    private static string ResolveChestPool(StructureVariant variant, int chestIndex)
    {
        if (variant.ChestPools.Count == 0)
        {
            return "baseline";
        }
        if (chestIndex < 0 || chestIndex >= variant.ChestPools.Count)
        {
            return variant.ChestPools[variant.ChestPools.Count - 1];
        }
        return variant.ChestPools[chestIndex];
    }

    private void WriteFoundation(TerrainChunk chunk, StructureInstance instance, int baseX, int baseZ)
    {
        var data = instance.Data;
        var columns = s_baseColumns.GetOrAdd(data, static d =>
        {
            var set = new HashSet<(short, short)>();
            foreach (var cell in d.Cells)
            {
                if (cell.Y == d.MinY && d.Palette[cell.PaletteIndex].BlockName != "AirBlock")
                {
                    set.Add((cell.X, cell.Z));
                }
            }
            var array = new (short, short)[set.Count];
            int i = 0;
            foreach (var c in set)
            {
                array[i++] = c;
            }
            return array;
        });

        int foundationValue = BlockResolver.MakeValue(instance.Variant.FoundationBlock, 0);
        if (foundationValue == 0)
        {
            return;
        }
        int foundationTop = instance.OriginY + data.MinY - 1;
        foreach (var (dx, dz) in columns)
        {
            int wx = instance.OriginX + dx;
            int wz = instance.OriginZ + dz;
            if (wx < baseX || wx > baseX + 15 || wz < baseZ || wz > baseZ + 15)
            {
                continue;
            }
            int lx = wx - baseX;
            int lz = wz - baseZ;
            int top = chunk.CalculateTopmostCellHeight(lx, lz);
            for (int y = top + 1; y <= foundationTop && y <= 254; y++)
            {
                if (y >= 1)
                {
                    chunk.SetCellValueFast(lx, y, lz, foundationValue);
                }
            }
        }
    }
}

using System.Collections.Concurrent;
using Engine;
using Game;
using GameEntitySystem;
using System.Text.Json;

namespace SurvivalcraftRuins.Core;

public sealed class PendingChest
{
    public int X, Y, Z;
    public string Pool = "";
    public int Seed;
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
}

public sealed class RuinsSaveData
{
    // System.Text.Json needs IncludeFields for these plain field DTOs.

    public List<AmbushState> Ambushes = new();
    public List<PendingChest> Chests = new();
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
    public required PlacementEngine Placement { get; init; }
    public required IReadOnlyDictionary<string, LootPool> LootPools { get; init; }

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

    public string SavePath => Storage.CombinePaths(GameInfo.DirectoryName, "SurvivalcraftRuins.json");

    public void LoadPersistence()
    {
        try
        {
            if (!Storage.FileExists(SavePath))
            {
                return;
            }
            using var stream = Storage.OpenFile(SavePath, OpenFileMode.Read);
            using var reader = new StreamReader(stream);
            var data = JsonSerializer.Deserialize<RuinsSaveData>(reader.ReadToEnd(), s_jsonOptions);
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
        catch (Exception exception)
        {
            Log.Warning($"[Ruins] Failed to load persistence: {exception.Message}");
        }
    }

    public void SavePersistence()
    {
        try
        {
            var data = new RuinsSaveData
            {
                Ambushes = new List<AmbushState>(Ambushes.Values),
                Chests = new List<PendingChest>(PendingChests),
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
    /// </summary>
    public void WriteChunk(TerrainChunk chunk)
    {
        int baseX = chunk.Coords.X * 16;
        int baseZ = chunk.Coords.Y * 16;
        var instances = Placement.InstancesIntersecting(baseX, baseZ, baseX + 15, baseZ + 15);
        foreach (var instance in instances)
        {
            WriteInstancePart(chunk, instance, baseX, baseZ);
        }
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
                string pool = ResolveChestPool(variant, chestIndex);
                IncomingChests.Enqueue(new PendingChest
                {
                    X = wx,
                    Y = wy,
                    Z = wz,
                    Pool = pool,
                    Seed = (int)DeterministicHash.Mix((uint)instance.InstanceSeed,
                        DeterministicHash.Mix((uint)wx, DeterministicHash.Mix((uint)wy, (uint)wz))),
                });
            }
        }

        if (variant.Ambush != null)
        {
            foreach (var marker in data.Ambushes)
            {
                int wx = ox + marker.X;
                int wz = oz + marker.Z;
                if (wx < baseX || wx > baseX + 15 || wz < baseZ || wz > baseZ + 15)
                {
                    continue;
                }
                int wy = Math.Clamp(oy + marker.Y, 1, 254);
                IncomingAmbushes.Enqueue(new AmbushState
                {
                    Id = $"{variant.Name}:{wx},{wy},{wz}",
                    X = wx,
                    Y = wy,
                    Z = wz,
                    Creature = variant.Ambush.CreatureTemplate,
                    Min = variant.Ambush.MinCount,
                    Max = variant.Ambush.MaxCount,
                    Radius = variant.Ambush.TriggerRadius,
                    Seed = (int)DeterministicHash.Mix((uint)instance.InstanceSeed, (uint)(wx ^ wz)),
                });
            }
        }
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

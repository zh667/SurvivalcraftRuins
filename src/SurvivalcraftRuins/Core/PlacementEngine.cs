using System.Collections.Concurrent;

namespace SurvivalcraftRuins.Core;

/// <summary>Abstraction over the terrain generator so placement logic is testable.</summary>
public interface IWorldSampler
{
    int OceanLevel { get; }
    float Height(float x, float z);
}

public sealed class StructureInstance
{
    public required StructureDef Def { get; init; }
    public required StructureVariant Variant { get; init; }
    public required StructureData Data { get; init; }
    public required int OriginX { get; init; }
    public required int OriginY { get; init; }
    public required int OriginZ { get; init; }
    public required int InstanceSeed { get; init; }

    public int WorldMinX => OriginX + Data.MinX;
    public int WorldMaxX => OriginX + Data.MaxX;
    public int WorldMinZ => OriginZ + Data.MinZ;
    public int WorldMaxZ => OriginZ + Data.MaxZ;

    public bool IntersectsColumnRange(int minX, int minZ, int maxX, int maxZ)
    {
        return WorldMinX <= maxX && WorldMaxX >= minX && WorldMinZ <= maxZ && WorldMaxZ >= minZ;
    }
}

/// <summary>
/// Deterministic structure placement. The world is divided per-def into square
/// grid cells (GridChunks chunks wide); each cell rolls one candidate instance
/// from a hash of (worldSeed, defSalt, cellX, cellZ), so any chunk can compute
/// every instance that touches it without neighbor chunks existing yet.
/// </summary>
public sealed class PlacementEngine
{
    /// <summary>Upper bound on horizontal structure extent, governs the neighborhood search.</summary>
    public const int MaxFootprint = 72;

    private readonly int m_seed;
    private readonly IWorldSampler m_sampler;
    private readonly IReadOnlyList<StructureDef> m_defs;
    private readonly ConcurrentDictionary<(int, int, int), StructureInstance?> m_cache = new();

    public PlacementEngine(int seed, IWorldSampler sampler, IReadOnlyList<StructureDef> defs)
    {
        m_seed = seed;
        m_sampler = sampler;
        m_defs = defs;
    }

    public List<StructureInstance> InstancesIntersecting(int minX, int minZ, int maxX, int maxZ)
    {
        var result = new List<StructureInstance>();
        for (int defIndex = 0; defIndex < m_defs.Count; defIndex++)
        {
            int cellSpan = m_defs[defIndex].GridChunks * 16;
            int cellMinX = FloorDiv(minX - MaxFootprint, cellSpan);
            int cellMaxX = FloorDiv(maxX + MaxFootprint, cellSpan);
            int cellMinZ = FloorDiv(minZ - MaxFootprint, cellSpan);
            int cellMaxZ = FloorDiv(maxZ + MaxFootprint, cellSpan);
            for (int cx = cellMinX; cx <= cellMaxX; cx++)
            {
                for (int cz = cellMinZ; cz <= cellMaxZ; cz++)
                {
                    var instance = GetInstance(defIndex, cx, cz);
                    if (instance != null && instance.IntersectsColumnRange(minX, minZ, maxX, maxZ))
                    {
                        result.Add(instance);
                    }
                }
            }
        }
        return result;
    }

    public StructureInstance? GetInstance(int defIndex, int cellX, int cellZ)
    {
        return m_cache.GetOrAdd((defIndex, cellX, cellZ), key => ComputeInstance(key.Item1, key.Item2, key.Item3));
    }

    private StructureInstance? ComputeInstance(int defIndex, int cellX, int cellZ)
    {
        var def = m_defs[defIndex];
        uint hash = DeterministicHash.Mix(m_seed, def.Salt, cellX, cellZ);
        if (DeterministicHash.ToUnitFloat(hash) >= def.Chance)
        {
            return null;
        }
        int instanceSeed = (int)DeterministicHash.Mix(hash, 0x5EEDu);
        var variant = def.Variants[(int)(DeterministicHash.Mix(hash, 0x7A11u) % (uint)def.Variants.Count)];
        StructureData data;
        try
        {
            data = variant.Provider(instanceSeed);
        }
        catch (Exception)
        {
            return null;
        }

        int cellSpan = def.GridChunks * 16;
        int rangeX = Math.Max(1, cellSpan - data.SizeX - 2);
        int rangeZ = Math.Max(1, cellSpan - data.SizeZ - 2);
        int ox = cellX * cellSpan + 1 - data.MinX + (int)(DeterministicHash.Mix(hash, 11u) % (uint)rangeX);
        int oz = cellZ * cellSpan + 1 - data.MinZ + (int)(DeterministicHash.Mix(hash, 13u) % (uint)rangeZ);

        int? oy = ComputeAnchorY(def, variant, data, ox, oz);
        if (oy == null)
        {
            return null;
        }

        var instance = new StructureInstance
        {
            Def = def,
            Variant = variant,
            Data = data,
            OriginX = ox,
            OriginY = oy.Value,
            OriginZ = oz,
            InstanceSeed = instanceSeed,
        };

        // Higher-priority defs (lower index) win on overlap; suppression is
        // deterministic because it only ever consults strictly lower indices.
        for (int higher = 0; higher < defIndex; higher++)
        {
            int span = m_defs[higher].GridChunks * 16;
            int hMinX = FloorDiv(instance.WorldMinX - MaxFootprint, span);
            int hMaxX = FloorDiv(instance.WorldMaxX + MaxFootprint, span);
            int hMinZ = FloorDiv(instance.WorldMinZ - MaxFootprint, span);
            int hMaxZ = FloorDiv(instance.WorldMaxZ + MaxFootprint, span);
            for (int cx = hMinX; cx <= hMaxX; cx++)
            {
                for (int cz = hMinZ; cz <= hMaxZ; cz++)
                {
                    var other = GetInstance(higher, cx, cz);
                    if (other != null && other.IntersectsColumnRange(
                            instance.WorldMinX, instance.WorldMinZ, instance.WorldMaxX, instance.WorldMaxZ))
                    {
                        return null;
                    }
                }
            }
        }
        return instance;
    }

    private int? ComputeAnchorY(StructureDef def, StructureVariant variant, StructureData data, int ox, int oz)
    {
        float x0 = ox + data.MinX, x1 = ox + data.MaxX;
        float z0 = oz + data.MinZ, z1 = oz + data.MaxZ;
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        float hCenter = m_sampler.Height(cx, cz);
        float h00 = m_sampler.Height(x0, z0);
        float h01 = m_sampler.Height(x0, z1);
        float h10 = m_sampler.Height(x1, z0);
        float h11 = m_sampler.Height(x1, z1);
        float hMin = MathF.Min(MathF.Min(h00, h01), MathF.Min(h10, h11));
        float hMax = MathF.Max(MathF.Max(h00, h01), MathF.Max(h10, h11));
        int ocean = m_sampler.OceanLevel;

        int oy;
        switch (def.Anchor)
        {
            case AnchorKind.Land:
                if (hMin <= ocean + 1 || hMax - hMin > def.MaxSlope)
                {
                    return null;
                }
                oy = (int)MathF.Round(hCenter) + 1 - variant.SinkDepth;
                break;

            case AnchorKind.Coast:
            {
                if (hCenter <= ocean || hCenter > ocean + 4 || hMax - hMin > def.MaxSlope)
                {
                    return null;
                }
                // Require open water nearby so the tower actually faces the sea.
                bool nearWater = false;
                for (int i = 0; i < 8 && !nearWater; i++)
                {
                    float angle = i * MathF.PI / 4f;
                    float sx = cx + MathF.Cos(angle) * 20f;
                    float sz = cz + MathF.Sin(angle) * 20f;
                    if (m_sampler.Height(sx, sz) < ocean - 2)
                    {
                        nearWater = true;
                    }
                }
                if (!nearWater)
                {
                    return null;
                }
                oy = (int)MathF.Round(hCenter) + 1 - variant.SinkDepth;
                break;
            }

            case AnchorKind.Ocean:
            {
                // Floating hull: open water at least 4 deep across the footprint.
                if (ocean - hMax < 4f)
                {
                    return null;
                }
                oy = ocean + 1 - variant.SinkDepth;
                break;
            }

            case AnchorKind.Sky:
            {
                // Hover 16-30 blocks above the tallest terrain under the footprint.
                float ground = MathF.Max(hMax, ocean);
                uint variation = DeterministicHash.Mix((uint)ox, (uint)oz) % 15u;
                oy = (int)ground + 16 + (int)variation;
                int ceiling = 250 - data.MaxY;
                if (oy > ceiling)
                {
                    oy = ceiling;
                }
                if (oy < (int)ground + 8)
                {
                    return null;
                }
                break;
            }

            case AnchorKind.Offshore:
            {
                float depthCenter = ocean - hCenter;
                float depthShallowest = ocean - hMax;
                if (depthCenter < 4f || depthCenter > 14f || depthShallowest < 2f)
                {
                    return null;
                }
                oy = (int)MathF.Round(hCenter) + 1 - variant.SinkDepth;
                break;
            }

            default:
                return null;
        }

        if (oy + data.MinY < 2 || oy + data.MaxY > 250)
        {
            return null;
        }
        return oy;
    }

    public static int FloorDiv(int a, int b)
    {
        int q = a / b;
        if (a % b != 0 && (a < 0) != (b < 0))
        {
            q--;
        }
        return q;
    }
}

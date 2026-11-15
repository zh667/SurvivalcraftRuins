using System.Collections.Concurrent;
using System.Globalization;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// The frozen verdict for one placement-table candidate. Rejected records carry
/// only the key; approved ones carry everything needed to rebuild the structure
/// without consulting the table it came from, so a later table change can never
/// move, shrink or unbuild a structure that is already partly in the ground.
/// </summary>
public sealed class LedgerRecord
{
    public string Key = "";
    public bool Approved;
    public string Def = "";
    public string Variant = "";
    public int Seed;
    public int OriginX;
    public int OriginY;
    public int OriginZ;

    /// <summary>Inclusive world column bounds, cached so the shape need not be rebuilt to index it.</summary>
    public int MinX;
    public int MinZ;
    public int MaxX;
    public int MaxZ;

    public string Encode()
    {
        if (!Approved)
        {
            return Key + "|0";
        }
        var parts = new[]
        {
            Key, "1", Def, Variant,
            Seed.ToString(CultureInfo.InvariantCulture),
            OriginX.ToString(CultureInfo.InvariantCulture),
            OriginY.ToString(CultureInfo.InvariantCulture),
            OriginZ.ToString(CultureInfo.InvariantCulture),
            MinX.ToString(CultureInfo.InvariantCulture),
            MinZ.ToString(CultureInfo.InvariantCulture),
            MaxX.ToString(CultureInfo.InvariantCulture),
            MaxZ.ToString(CultureInfo.InvariantCulture),
        };
        return string.Join("|", parts);
    }

    public static LedgerRecord? TryDecode(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }
        string[] parts = line.Split('|');
        if (parts.Length < 2 || parts[0].Length == 0)
        {
            return null;
        }
        if (parts[1] == "0")
        {
            return new LedgerRecord { Key = parts[0], Approved = false };
        }
        if (parts.Length < 12)
        {
            return null;
        }
        var record = new LedgerRecord
        {
            Key = parts[0],
            Approved = true,
            Def = parts[2],
            Variant = parts[3],
        };
        if (!TryInt(parts[4], out record.Seed)
            || !TryInt(parts[5], out record.OriginX)
            || !TryInt(parts[6], out record.OriginY)
            || !TryInt(parts[7], out record.OriginZ)
            || !TryInt(parts[8], out record.MinX)
            || !TryInt(parts[9], out record.MinZ)
            || !TryInt(parts[10], out record.MaxX)
            || !TryInt(parts[11], out record.MaxZ))
        {
            return null;
        }
        return record;
    }

    private static bool TryInt(string text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}

/// <summary>
/// A world's structure ledger: every candidate the mod has ever looked at, and
/// what it decided. Structures are written into chunks at generation time and
/// never revisited, so the placement table alone cannot be the authority — a
/// table that changes under a half-explored world leaves buildings sliced along
/// the frontier. The ledger is the authority instead: once a structure is
/// approved it is rebuilt from its own record for every chunk it touches, for
/// as long as the world lives.
/// </summary>
public sealed class StructureLedger
{
    private static readonly LedgerRecord[] s_none = Array.Empty<LedgerRecord>();

    private readonly ConcurrentDictionary<string, LedgerRecord> m_records = new(StringComparer.Ordinal);

    // Copy-on-write buckets: chunk writes read this on the terrain thread every
    // chunk, approvals happen a few hundred times in a world's life.
    private readonly ConcurrentDictionary<long, LedgerRecord[]> m_byChunk = new();

    public int Count => m_records.Count;

    public static string MakeKey(string defName, int cellX, int cellZ)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{defName}:{cellX}:{cellZ}");
    }

    public bool Contains(string key)
    {
        return m_records.ContainsKey(key);
    }

    /// <summary>
    /// True when no chunk under the footprint has been generated yet, ignoring
    /// the chunk currently being generated (which by definition is not on disk).
    /// A candidate that fails this would only ever be written in slices.
    /// </summary>
    public static bool FootprintUntouched(
        int minX, int minZ, int maxX, int maxZ, int generatingChunkX, int generatingChunkZ, IChunkProbe probe)
    {
        int minChunkX = PlacementEngine.FloorDiv(minX, 16);
        int maxChunkX = PlacementEngine.FloorDiv(maxX, 16);
        int minChunkZ = PlacementEngine.FloorDiv(minZ, 16);
        int maxChunkZ = PlacementEngine.FloorDiv(maxZ, 16);
        for (int cx = minChunkX; cx <= maxChunkX; cx++)
        {
            for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
            {
                if ((cx != generatingChunkX || cz != generatingChunkZ) && probe.Exists(cx, cz))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public bool TryGet(string key, out LedgerRecord record)
    {
        return m_records.TryGetValue(key, out record!);
    }

    /// <summary>Records a candidate that must never be built in this world.</summary>
    public void Reject(string key)
    {
        m_records.TryAdd(key, new LedgerRecord { Key = key, Approved = false });
    }

    /// <summary>Commits an instance to the world. Returns false if it was already decided.</summary>
    public bool Approve(string key, StructureInstance instance)
    {
        var record = new LedgerRecord
        {
            Key = key,
            Approved = true,
            Def = instance.Def.Name,
            Variant = instance.Variant.Name,
            Seed = instance.InstanceSeed,
            OriginX = instance.OriginX,
            OriginY = instance.OriginY,
            OriginZ = instance.OriginZ,
            MinX = instance.WorldMinX,
            MinZ = instance.WorldMinZ,
            MaxX = instance.WorldMaxX,
            MaxZ = instance.WorldMaxZ,
        };
        if (!m_records.TryAdd(key, record))
        {
            return false;
        }
        Index(record);
        return true;
    }

    /// <summary>Approved records whose footprint covers the given chunk.</summary>
    public LedgerRecord[] RecordsForChunk(int chunkX, int chunkZ)
    {
        return m_byChunk.TryGetValue(ChunkKey(chunkX, chunkZ), out var bucket) ? bucket : s_none;
    }

    public List<string> Encode()
    {
        var lines = new List<string>(m_records.Count);
        foreach (var record in m_records.Values)
        {
            lines.Add(record.Encode());
        }
        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    public static StructureLedger Decode(IEnumerable<string>? lines)
    {
        var ledger = new StructureLedger();
        if (lines == null)
        {
            return ledger;
        }
        foreach (string line in lines)
        {
            var record = LedgerRecord.TryDecode(line);
            if (record == null || !ledger.m_records.TryAdd(record.Key, record))
            {
                continue;
            }
            if (record.Approved)
            {
                ledger.Index(record);
            }
        }
        return ledger;
    }

    private void Index(LedgerRecord record)
    {
        int minChunkX = PlacementEngine.FloorDiv(record.MinX, 16);
        int maxChunkX = PlacementEngine.FloorDiv(record.MaxX, 16);
        int minChunkZ = PlacementEngine.FloorDiv(record.MinZ, 16);
        int maxChunkZ = PlacementEngine.FloorDiv(record.MaxZ, 16);
        for (int cx = minChunkX; cx <= maxChunkX; cx++)
        {
            for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
            {
                m_byChunk.AddOrUpdate(
                    ChunkKey(cx, cz),
                    static (_, added) => new[] { added },
                    static (_, existing, added) =>
                    {
                        var grown = new LedgerRecord[existing.Length + 1];
                        Array.Copy(existing, grown, existing.Length);
                        grown[existing.Length] = added;
                        return grown;
                    },
                    record);
            }
        }
    }

    private static long ChunkKey(int chunkX, int chunkZ)
    {
        return ((long)chunkX << 32) | (uint)chunkZ;
    }
}

namespace SurvivalcraftRuins.Core;

/// <summary>A block cell in structure-relative coordinates. Y may be negative (below the anchor).</summary>
public readonly record struct Cell(short X, short Y, short Z, ushort PaletteIndex);

/// <summary>Palette entry: SC block class name plus data bits (Terrain data, not full value).</summary>
public readonly record struct PaletteEntry(string BlockName, int Data);

/// <summary>
/// A chest. <paramref name="Pool"/> names its loot pool when the structure
/// itself knows (village chests carry their Minecraft loot table); null leaves
/// the choice to the variant's ChestPools list.
/// </summary>
public readonly record struct ChestMarker(short X, short Y, short Z, string? Pool = null);

/// <summary>
/// Ground work for one column, done before any of the structure's cells are
/// written: terrain cleared out of a building's box and foundation filled up
/// to its floor. Settlements need it per building, because each house sits at
/// its own height on uneven land.
/// </summary>
public readonly struct SitePrep
{
    public const short None = short.MinValue;

    public short X { get; init; }
    public short Z { get; init; }

    /// <summary>Fill with <see cref="FoundationPalette"/> from the ground up to this height; <see cref="None"/> skips.</summary>
    public short FoundationTop { get; init; }

    /// <summary>Clear cells from here up to <see cref="ClearTop"/>; nothing when ClearTop is below it.</summary>
    public short ClearBottom { get; init; }
    public short ClearTop { get; init; }

    public ushort FoundationPalette { get; init; }

    /// <summary>When set, the topmost foundation cell uses <see cref="CapPalette"/> (grass on an embankment).</summary>
    public bool Capped { get; init; }
    public ushort CapPalette { get; init; }

    /// <summary>Clear only plants and snow cover, never terrain (used above streets).</summary>
    public bool CoverOnly { get; init; }
}

public readonly record struct AmbushMarker(short X, short Y, short Z);

/// <summary>
/// A realized structure: palette-compressed cells plus special markers.
/// Instances of this are immutable and shared; weathering is applied per world
/// position at write time so it never mutates the shared data.
/// </summary>
public sealed class StructureData
{
    public required string Name { get; init; }

    /// <summary>Inclusive bounds in relative coords. MinY may be negative (mines dig below the anchor).</summary>
    public required int MinX { get; init; }
    public required int MinY { get; init; }
    public required int MinZ { get; init; }
    public required int MaxX { get; init; }
    public required int MaxY { get; init; }
    public required int MaxZ { get; init; }

    public required IReadOnlyList<PaletteEntry> Palette { get; init; }
    public required IReadOnlyList<Cell> Cells { get; init; }
    public required IReadOnlyList<ChestMarker> Chests { get; init; }
    public required IReadOnlyList<AmbushMarker> Ambushes { get; init; }

    /// <summary>Per-column ground work, applied before the cells. Empty for most structures.</summary>
    public IReadOnlyList<SitePrep> SitePrep { get; init; } = Array.Empty<SitePrep>();

    public int SizeX => MaxX - MinX + 1;
    public int SizeY => MaxY - MinY + 1;
    public int SizeZ => MaxZ - MinZ + 1;

    public static StructureData FromCells(
        string name,
        IReadOnlyList<PaletteEntry> palette,
        IReadOnlyList<Cell> cells,
        IReadOnlyList<ChestMarker> chests,
        IReadOnlyList<AmbushMarker> ambushes,
        IReadOnlyList<SitePrep>? sitePrep = null)
    {
        if (cells.Count == 0)
        {
            throw new InvalidOperationException($"Structure '{name}' has no cells.");
        }
        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
        foreach (var c in cells)
        {
            if (c.X < minX) minX = c.X;
            if (c.Y < minY) minY = c.Y;
            if (c.Z < minZ) minZ = c.Z;
            if (c.X > maxX) maxX = c.X;
            if (c.Y > maxY) maxY = c.Y;
            if (c.Z > maxZ) maxZ = c.Z;
        }
        return new StructureData
        {
            Name = name,
            MinX = minX, MinY = minY, MinZ = minZ,
            MaxX = maxX, MaxY = maxY, MaxZ = maxZ,
            Palette = palette,
            Cells = cells,
            Chests = chests,
            Ambushes = ambushes,
            SitePrep = sitePrep ?? Array.Empty<SitePrep>(),
        };
    }
}

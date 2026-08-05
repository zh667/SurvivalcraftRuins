namespace SurvivalcraftRuins.Core;

public enum AnchorKind
{
    /// <summary>Flat-ish dry land; foundation columns are filled below the footprint.</summary>
    Land,
    /// <summary>On the beach/cliff line, close to open water (lighthouses).</summary>
    Coast,
    /// <summary>On the sea floor in shallow open water (shipwrecks).</summary>
    Offshore,
    /// <summary>Floating on the ocean surface (ships).</summary>
    Ocean,
    /// <summary>Floating high above the terrain (sky dungeons).</summary>
    Sky,
}

/// <summary>Per-block weathering applied deterministically at write time.</summary>
public sealed class WeatherProfile
{
    public static readonly WeatherProfile None = new() { DecayChance = 0f };

    /// <summary>Chance a non-air block is dropped entirely (collapse holes).</summary>
    public float DecayChance { get; init; }

    /// <summary>Chance a block listed in Swaps is replaced by its weathered version.</summary>
    public float SwapChance { get; init; }

    public IReadOnlyDictionary<string, string> Swaps { get; init; } = new Dictionary<string, string>();
}

public sealed class AmbushSpec
{
    /// <summary>Entity template name from Database.xml, e.g. "Wolf_Gray".</summary>
    public required string CreatureTemplate { get; init; }
    public required int MinCount { get; init; }
    public required int MaxCount { get; init; }
    /// <summary>Player distance that springs the ambush.</summary>
    public required float TriggerRadius { get; init; }
}

/// <summary>
/// One concrete structure that can appear in a slot. All variants of a slot
/// share placement rules (grid, chance, anchor); everything else is per-variant.
/// </summary>
public sealed class StructureVariant
{
    public required string Name { get; init; }

    /// <summary>Realizes the structure. Must be deterministic for a given instance seed.</summary>
    public required Func<int, StructureData> Provider { get; init; }

    /// <summary>
    /// Loot pool names assigned to chests in template order (sorted by Y, then Z,
    /// then X). When there are more chests than entries the last entry repeats.
    /// </summary>
    public required IReadOnlyList<string> ChestPools { get; init; }

    public WeatherProfile Weather { get; init; } = WeatherProfile.None;

    /// <summary>Ambush spawned at each ambush marker (null disables).</summary>
    public AmbushSpec? Ambush { get; init; }

    /// <summary>How many blocks the structure origin sits below the anchor surface.</summary>
    public int SinkDepth { get; init; }

    /// <summary>Block used to fill foundation columns under Land/Coast structures.</summary>
    public string FoundationBlock { get; init; } = "DirtBlock";
}

/// <summary>
/// A placement slot: one candidate per grid cell, realized as one of several
/// variants picked by hash — variety without extra density.
/// </summary>
public sealed class StructureDef
{
    public required string Name { get; init; }

    /// <summary>Placement grid cell size in chunks; one candidate instance per cell.</summary>
    public required int GridChunks { get; init; }

    /// <summary>Chance [0..1] that a grid cell actually contains an instance.</summary>
    public required float Chance { get; init; }

    public required AnchorKind Anchor { get; init; }

    /// <summary>Max height difference across footprint corners for Land/Coast placement.</summary>
    public float MaxSlope { get; init; } = 4f;

    public required IReadOnlyList<StructureVariant> Variants { get; init; }

    public int Salt => DeterministicHash.StringSalt(Name);
}

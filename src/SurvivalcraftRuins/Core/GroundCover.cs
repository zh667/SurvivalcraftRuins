namespace SurvivalcraftRuins.Core;

/// <summary>
/// Which blocks count as "not the ground" when a settlement prepares its site:
/// plants and snow cover are cleared off streets, and foundations fill down
/// through cover, water, leaves and tree trunks until they reach real ground.
/// </summary>
public static class GroundCover
{
    private static readonly string[] s_cover =
    {
        "TallGrassBlock", "RedFlowerBlock", "PurpleFlowerBlock", "WhiteFlowerBlock", "SaplingBlock",
        "DryBushBlock", "LargeDryBushBlock", "SnowBlock", "IvyBlock", "FallenLeavesBlock",
    };

    private static readonly string[] s_passThroughExtra =
    {
        "WaterBlock", "OakLeavesBlock", "BirchLeavesBlock", "SpruceLeavesBlock", "TallSpruceLeavesBlock",
        "MimosaLeavesBlock", "PoplarLeavesBlock", "OakWoodBlock", "BirchWoodBlock", "SpruceWoodBlock",
        "MimosaWoodBlock", "PoplarWoodBlock", "CactusBlock", "PumpkinBlock",
    };

    private static bool[]? s_isCover;
    private static bool[]? s_passThrough;

    /// <summary>Plants and snow lying on the ground.</summary>
    public static bool IsCover(int contents)
    {
        var table = s_isCover ??= Build(s_cover, includeAir: false);
        return contents >= 0 && contents < table.Length && table[contents];
    }

    /// <summary>Anything a foundation should replace on its way down to the ground.</summary>
    public static bool IsPassThrough(int contents)
    {
        var table = s_passThrough ??= Build(s_cover.Concat(s_passThroughExtra), includeAir: true);
        return contents >= 0 && contents < table.Length && table[contents];
    }

    private static bool[] Build(IEnumerable<string> names, bool includeAir)
    {
        var table = new bool[1024];
        table[0] = includeAir;
        foreach (string name in names)
        {
            if (BlockResolver.TryGetIndex(name, out int index) && index >= 0 && index < table.Length)
            {
                table[index] = true;
            }
        }
        return table;
    }
}

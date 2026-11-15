using Game;

namespace SurvivalcraftRuins.Mod;

/// <summary>
/// Screen strings. The mod ships no language files, so the handful of words the
/// settings screen needs are kept here and picked by the game's language code.
/// </summary>
public static class RuinsText
{
    private static bool Chinese
    {
        get
        {
            try
            {
                return ModsManager.Configs.TryGetValue("Language", out string? language)
                    && language != null
                    && language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    private static string Pick(string chinese, string english)
    {
        return Chinese ? chinese : english;
    }

    public static string Title => Pick("遗迹设置", "Ruins");

    public static string Button => Pick("遗迹", "Ruins");

    public static string Density => Pick("遗迹密度", "Structure density");

    public static string DensityHint => Pick(
        "只影响还没探索过的地方,已有建筑不会移动或消失",
        "Applies to land you have not explored yet; structures already in your world stay put");

    public static string Guards => Pick("守卫强度", "Guard strength");

    public static string GuardsHint => Pick(
        "守卫数量倍率,随时可改,对已有遗迹同样生效",
        "Multiplies guard counts; takes effect immediately, including at structures you already found");

    public static string GuardsOff => Pick("无守卫", "None");

    public static string Off => Pick("不生成", "Off");

    /// <summary>Names for <see cref="Core.RuinsSettings.DensityTiers"/>, low to high.</summary>
    public static string DensityTier(int tier)
    {
        string[] chinese = { "不生成", "极少", "很少", "默认", "较多", "很多", "极多", "最多" };
        string[] english = { "Off", "Very rare", "Rare", "Default", "Common", "Very common", "Abundant", "Maximum" };
        var names = Chinese ? chinese : english;
        return names[Math.Clamp(tier, 0, names.Length - 1)];
    }

    public static string Note => Pick(
        "想多搜刮就把密度调高。密度只决定新地区里遗迹出现的概率,\n"
        + "调高不会破坏任何已经生成的建筑。",
        "Turn density up for more to loot. It only changes how often structures\n"
        + "appear in new land, and never disturbs one that is already built.");
}

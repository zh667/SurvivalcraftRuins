#if RUINS_SCAPI
using Game;
using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Mod;

/// <summary>
/// The plugin runtime's own Mod Settings page (API 1.9.3+). The page and its
/// two items are declared in modinfo.json under "Settings"; the game builds
/// the screen, stores the values and tells us when one changes. Names and the
/// density step labels live in Assets/Lang.
/// </summary>
public static class RuinsModSettings
{
    public const string Package = "SurvivalcraftRuins";
    public const string Page = "Ruins";
    public const string DensityItem = "Density";
    public const string GuardsItem = "Guards";

    /// <summary>Copies the stored values into <see cref="RuinsSettings"/>.</summary>
    public static void Pull()
    {
        try
        {
            if (ModSettingsManager.TryGet<RuinsDensity>(out var density, Package, Page, DensityItem))
            {
                RuinsSettings.Density = RuinsSettings.DensityOf(density);
            }
            if (ModSettingsManager.TryGet<float>(out float guards, Package, Page, GuardsItem))
            {
                RuinsSettings.Guards = Math.Clamp(guards, RuinsSettings.MinScale, RuinsSettings.MaxScale);
            }
        }
        catch (Exception exception)
        {
            Engine.Log.Warning($"[Ruins] Reading mod settings failed: {exception.Message}");
        }
    }

    /// <summary>One value changed on the page; idPath is [page, item].</summary>
    public static void Apply(string[] idPath, object value)
    {
        if (idPath.Length != 2 || idPath[0] != Page)
        {
            return;
        }
        if (idPath[1] == DensityItem && value is RuinsDensity density)
        {
            RuinsSettings.Density = RuinsSettings.DensityOf(density);
        }
        else if (idPath[1] == GuardsItem && value is float guards)
        {
            RuinsSettings.Guards = Math.Clamp(guards, RuinsSettings.MinScale, RuinsSettings.MaxScale);
        }
    }
}
#endif

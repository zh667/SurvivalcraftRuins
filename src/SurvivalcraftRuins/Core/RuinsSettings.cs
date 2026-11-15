using System.Xml.Linq;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// The density steps as the plugin runtime's Mod Settings page shows them
/// (an enum slider; names come from Assets/Lang). Order matches
/// <see cref="RuinsSettings.DensityTiers"/>.
/// </summary>
public enum RuinsDensity
{
    Off,
    VeryRare,
    Rare,
    Default,
    Common,
    VeryCommon,
    Abundant,
    Maximum,
}

/// <summary>
/// Player-facing knobs, persisted by the game through ModLoader's settings
/// hooks and edited on <c>RuinsSettingsScreen</c>. Both may be changed at any
/// time, including halfway through a world: density decides only which
/// candidates a not-yet-generated chunk may still take up, and the world's
/// ledger keeps everything already built exactly where it is.
/// </summary>
public static class RuinsSettings
{
    public const float MinScale = 0f;
    public const float MaxScale = 4f;

    /// <summary>Multiplier on every slot's spawn chance. 1 = tuned default.</summary>
    public static float Density { get; set; } = 1f;

    /// <summary>
    /// The density slider's stops, low to high. Fixed stops rather than a free
    /// multiplier, so "low" and "high" mean the same thing to every player.
    /// Index <see cref="DefaultDensityTier"/> is the tuned default.
    /// </summary>
    public static readonly float[] DensityTiers = { 0f, 0.25f, 0.5f, 1f, 1.5f, 2f, 3f, 4f };

    public const int DefaultDensityTier = 3;

    /// <summary>
    /// The stop closest to <paramref name="density"/>; older versions saved
    /// any quarter step, and those must land on a sensible stop.
    /// </summary>
    public static int NearestDensityTier(float density)
    {
        int best = DefaultDensityTier;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < DensityTiers.Length; i++)
        {
            float distance = MathF.Abs(DensityTiers[i] - density);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }

    public static float DensityOf(RuinsDensity step)
    {
        return DensityTiers[Math.Clamp((int)step, 0, DensityTiers.Length - 1)];
    }

    /// <summary>Multiplier on guard counts. 0 disables guards entirely.</summary>
    public static float Guards { get; set; } = 1f;

    // The game hands every mod the same element to write attributes on, so the
    // names have to say whose they are. "Density" alone was one collision away
    // from a stranger's setting.
    private const string DensityKey = "RuinsDensity";
    private const string GuardsKey = "RuinsGuards";

    public static void Save(XElement element)
    {
        element.SetAttributeValue(DensityKey, Density.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        element.SetAttributeValue(GuardsKey, Guards.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

    public static void Load(XElement element)
    {
        Density = ReadScale(element, DensityKey, ReadScale(element, "Density", 1f));
        Guards = ReadScale(element, GuardsKey, ReadScale(element, "Guards", 1f));
    }

    private static float ReadScale(XElement element, string name, float fallback)
    {
        var attribute = element.Attribute(name);
        if (attribute != null
            && float.TryParse(attribute.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            return Math.Clamp(value, MinScale, MaxScale);
        }
        return fallback;
    }
}

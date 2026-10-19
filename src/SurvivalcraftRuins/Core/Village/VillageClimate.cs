namespace SurvivalcraftRuins.Core.Village;

/// <summary>
/// Which of Minecraft's five village styles suits a Survivalcraft climate.
/// Survivalcraft has no biome objects: temperature and humidity (0..15) are
/// the whole story, so the styles follow what the game grows there.
/// <list type="bullet">
/// <item>snowy — temperature 0..1: the ground freezes at sea level
///   (<c>SubsystemWeather.IsPlaceFrozen</c>: temperature + height adjustment &lt;= 0).</item>
/// <item>desert — hot (&gt;= 9) and dry (&lt;= 7): the sand surface of
///   <c>GenerateTerrain</c> (temperature &gt; 8 and humidity &lt; 8).</item>
/// <item>savanna — dry (&lt;= 6) but milder: mimosa country
///   (<c>PlantsManager</c> gives mimosas humidity 0..6).</item>
/// <item>taiga — cool (2..6) and not dry: spruce country (spruces fade out
///   from temperature 6 to 10).</item>
/// <item>plains — everything else, from temperate oak land to the jungle,
///   for which Minecraft has no village.</item>
/// </list>
/// </summary>
public static class VillageClimate
{
    public static readonly string[] Styles = { "plains", "desert", "savanna", "snowy", "taiga" };

    public static string StyleFor(int temperature, int humidity)
    {
        if (temperature <= 1)
        {
            return "snowy";
        }
        if (temperature >= 9 && humidity <= 7)
        {
            return "desert";
        }
        if (humidity <= 6)
        {
            return "savanna";
        }
        return temperature <= 6 ? "taiga" : "plains";
    }
}

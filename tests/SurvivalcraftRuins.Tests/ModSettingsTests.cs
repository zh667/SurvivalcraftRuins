using System.Text.Json;
using SurvivalcraftRuins.Core;
using Xunit;

namespace SurvivalcraftRuins.Tests;

/// <summary>
/// The plugin runtime builds the Mod Settings page from modinfo.json and
/// labels it from Assets/Lang; a typo in either shows up in game as a missing
/// item or a raw identifier, with no error anywhere.
/// </summary>
public class ModSettingsTests
{
    private static JsonElement Modinfo()
    {
        string path = Path.Combine(TestRegistry.AssetsDir(), "..", "modinfo.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    [Fact]
    public void DeclaredItemsMatchWhatTheModReads()
    {
        var root = Modinfo();
        Assert.Equal("SurvivalcraftRuins", root.GetProperty("PackageName").GetString());
        var page = root.GetProperty("Settings")[0];
        Assert.Equal("Ruins", page.GetProperty("Id").GetString());
        var items = page.GetProperty("Items").EnumerateArray().ToDictionary(i => i.GetProperty("Id").GetString()!);
        var density = items["Density"];
        Assert.Equal(typeof(RuinsDensity).FullName, density.GetProperty("Type").GetString());
        Assert.True(Enum.TryParse<RuinsDensity>(density.GetProperty("Default").GetString(), out var step));
        Assert.Equal(1f, RuinsSettings.DensityOf(step));
        Assert.Equal(Enum.GetValues<RuinsDensity>().Length, RuinsSettings.DensityTiers.Length);
        var guards = items["Guards"];
        Assert.Equal("System.Single", guards.GetProperty("Type").GetString());
        Assert.Equal(RuinsSettings.MaxScale, guards.GetProperty("WidgetProperties").GetProperty("MaxValue").GetSingle());
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    public void EveryLabelHasText(string language)
    {
        var lang = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestRegistry.AssetsDir(), "Lang", language + ".json"))).RootElement;
        var mine = lang.GetProperty("ModSettings").GetProperty("SurvivalcraftRuins");
        var page = mine.GetProperty("Ruins");
        foreach (string field in new[] { "Name", "Title", "Description" })
        {
            Assert.False(string.IsNullOrWhiteSpace(page.GetProperty(field).GetString()), field);
        }
        foreach (string item in new[] { "Density", "Guards" })
        {
            Assert.False(string.IsNullOrWhiteSpace(page.GetProperty(item).GetProperty("Name").GetString()), item);
        }
        foreach (string member in Enum.GetNames<RuinsDensity>())
        {
            Assert.False(string.IsNullOrWhiteSpace(mine.GetProperty(nameof(RuinsDensity)).GetProperty(member).GetString()), member);
        }
    }
}

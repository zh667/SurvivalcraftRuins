using Engine;
using Game;
using SurvivalcraftRuins.Core;
using Xunit;

namespace SurvivalcraftRuins.Tests;

/// <summary>
/// Dumps one real-terrain village per style for tools/village/render_preview.py.
/// Opt-in: runs only when RUINS_PREVIEW_DIR is set.
/// </summary>
public class VillagePreviewTests
{
    [Fact]
    public void DumpOneVillagePerStyle()
    {
        string? dir = Environment.GetEnvironmentVariable("RUINS_PREVIEW_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            Assert.Skip("set RUINS_PREVIEW_DIR to dump village previews");
        }
        Directory.CreateDirectory(dir!);
        var wanted = new HashSet<string>(SurvivalcraftRuins.Core.Village.VillageClimate.Styles);
        var names = new Dictionary<int, string>();
        foreach (var type in typeof(Block).Assembly.GetTypes())
        {
            var field = type.GetField("Index", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly);
            if (typeof(Block).IsAssignableFrom(type) && field?.GetValue(null) is int index)
            {
                names.TryAdd(index, type.Name);
            }
        }
        File.WriteAllText(Path.Combine(dir!, "blocks.json"), System.Text.Json.JsonSerializer.Serialize(names.ToDictionary(k => k.Key.ToString(), k => k.Value)));
        foreach (int seed in new[] { 12345, -987654321, 20260820, 777, 31337, 4242, 99, 2026, 555, 8080, 13, 271828 })
        {
            if (wanted.Count == 0)
            {
                break;
            }
            var world = VillageWorldTests.RealWorld.Create(seed);
            var state = world.NewState();
            for (int r = 0; r <= 5 && wanted.Count > 0; r++)
            {
                for (int cx = -r; cx <= r; cx++)
                {
                    for (int cz = -r; cz <= r; cz++)
                    {
                        if (Math.Max(Math.Abs(cx), Math.Abs(cz)) != r)
                        {
                            continue;
                        }
                        var v = state.Placement.GetInstance(world.VillageIndex, cx, cz);
                        if (v == null)
                        {
                            continue;
                        }
                        string style = v.Variant.Name.Split('_')[1];
                        if (!wanted.Remove(style))
                        {
                            continue;
                        }
                        world.Generate(state, v);
                        Dump(world, v, Path.Combine(dir!, $"{style}_seed{seed}_{v.CenterX}_{v.CenterZ}.bin"));
                    }
                }
            }
        }
        Assert.True(wanted.Count == 0, "no village found for: " + string.Join(", ", wanted));
    }

    private static void Dump(VillageWorldTests.RealWorld world, StructureInstance v, string path)
    {
        int pad = 10;
        int x0 = v.WorldMinX - pad, z0 = v.WorldMinZ - pad;
        int w = v.WorldMaxX - v.WorldMinX + 1 + 2 * pad, d = v.WorldMaxZ - v.WorldMinZ + 1 + 2 * pad;
        int y0 = Math.Max(1, v.OriginY + v.Data.MinY - 14), y1 = Math.Min(254, v.OriginY + v.Data.MaxY + 6);
        int h = y1 - y0 + 1;
        // Make sure the padding ring is generated too (terrain only, no village there).
        for (int cx = PlacementEngine.FloorDiv(x0, 16); cx <= PlacementEngine.FloorDiv(x0 + w - 1, 16); cx++)
        {
            for (int cz = PlacementEngine.FloorDiv(z0, 16); cz <= PlacementEngine.FloorDiv(z0 + d - 1, 16); cz++)
            {
                if (!world.Chunks.ContainsKey(new Point2(cx, cz)))
                {
                    world.Chunks[new Point2(cx, cz)] = GameGenerator.GenerateTerrain(world.Generator, cx, cz);
                }
            }
        }
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(w);
        writer.Write(h);
        writer.Write(d);
        writer.Write(x0);
        writer.Write(y0);
        writer.Write(z0);
        writer.Write(world.Sampler.OceanLevel);
        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < d; z++)
            {
                writer.Write((byte)world.Sampler.Temperature(x0 + x, z0 + z));
                writer.Write((byte)world.Sampler.Humidity(x0 + x, z0 + z));
                for (int y = 0; y < h; y++)
                {
                    int value = world.Chunks[new Point2(PlacementEngine.FloorDiv(x0 + x, 16), PlacementEngine.FloorDiv(z0 + z, 16))]
                        .GetCellValueFast(x0 + x - PlacementEngine.FloorDiv(x0 + x, 16) * 16, y0 + y,
                            z0 + z - PlacementEngine.FloorDiv(z0 + z, 16) * 16);
                    writer.Write((ushort)Terrain.ExtractContents(value));
                    writer.Write((byte)Terrain.ExtractData(value));
                }
            }
        }
    }
}

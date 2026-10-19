using Engine;
using Game;
using SurvivalcraftRuins.Core;
using Xunit;

namespace SurvivalcraftRuins.Tests;

/// <summary>
/// Orientation data checked against what the blocks mean in Survivalcraft,
/// across every shipped template, every builder and every village.
///
/// - Ladders and wall torches hang on the block at C - FaceToPoint3(data)
///   (SubsystemLadderBlockBehavior / SubsystemTorchBlockBehavior); that block
///   has to be part of the building, or the game drops them.
/// - A stair's tall half faces rotation 0 north, 1 west, 2 south, 3 east (the
///   collision boxes in StairsBlock). In a flight of steps, each one block up
///   and one along from the last, the tall half must face uphill or the flight
///   reads as a row of ramps pointing the wrong way.
/// </summary>
public class OrientationTests
{
    private static readonly Point3[] s_tallSide =
    {
        new(0, 0, -1), new(-1, 0, 0), new(0, 0, 1), new(1, 0, 0),
    };

    private static IEnumerable<StructureData> Everything()
    {
        foreach (var data in TestRegistry.Templates().Values)
        {
            yield return data;
        }
        foreach (var def in TestRegistry.FullDefs())
        {
            foreach (var variant in def.Variants)
            {
                if (variant.SitedProvider == null)
                {
                    for (int seed = 1; seed <= 3; seed++)
                    {
                        yield return variant.Provider(seed);
                    }
                }
                else
                {
                    yield return variant.Provider(1);
                }
            }
        }
    }

    private static Dictionary<(int, int, int), PaletteEntry> Solid(StructureData data)
    {
        var cells = new Dictionary<(int, int, int), PaletteEntry>();
        foreach (var c in data.Cells)
        {
            var entry = data.Palette[c.PaletteIndex];
            if (entry.BlockName != "AirBlock")
            {
                cells[(c.X, c.Y, c.Z)] = entry;
            }
        }
        return cells;
    }

    /// <summary>
    /// Counted as wrong: a ladder or torch hanging on nothing that would hang
    /// on the building if it faced another way. Ones with nothing around them
    /// at all lost their support in conversion (a modded block that became
    /// air) and say nothing about orientation.
    /// </summary>
    [Fact]
    public void LaddersAndWallTorchesHangOnTheBuilding()
    {
        int hung = 0;
        var turned = new List<string>();
        foreach (var data in Everything())
        {
            var cells = Solid(data);
            foreach (var ((x, y, z), entry) in cells)
            {
                bool ladder = entry.BlockName == "WoodenLadderBlock";
                bool wallTorch = entry.BlockName == "TorchBlock" && entry.Data < 4;
                if (!ladder && !wallTorch)
                {
                    continue;
                }
                bool HangsOn(int face)
                {
                    var p = CellFace.FaceToPoint3(face);
                    return cells.TryGetValue((x - p.X, y - p.Y, z - p.Z), out var support)
                        && support.BlockName is not ("WoodenLadderBlock" or "TorchBlock");
                }
                if (HangsOn(entry.Data & 3))
                {
                    hung++;
                }
                else if (Enumerable.Range(0, 4).Any(HangsOn))
                {
                    turned.Add($"{data.Name} {entry.BlockName}:{entry.Data} at {x},{y},{z}");
                }
            }
        }
        Assert.True(hung > 1000, $"only {hung} ladders/torches checked");
        Assert.True(turned.Count * 100 <= hung,
            $"{turned.Count} face away from the wall they could hang on ({hung} hang right):\n" + string.Join("\n", turned.Take(30)));
    }

    [Fact]
    public void FlightsOfStepsClimbTowardTheirTallSide()
    {
        int uphill = 0, downhill = 0;
        var wrong = new Dictionary<string, int>();
        foreach (var data in Everything())
        {
            var cells = Solid(data);
            foreach (var ((x, y, z), a) in cells)
            {
                if (!a.BlockName.EndsWith("StairsBlock") || (a.Data & 4) != 0)
                {
                    continue;
                }
                var t = s_tallSide[a.Data & 3];
                // The next step of a flight climbing toward t, or away from it.
                foreach (int sign in new[] { 1, -1 })
                {
                    if (cells.TryGetValue((x + sign * t.X, y + 1, z + sign * t.Z), out var b)
                        && b.BlockName.EndsWith("StairsBlock") && b.Data == a.Data)
                    {
                        if (sign == 1)
                        {
                            uphill++;
                        }
                        else
                        {
                            downhill++;
                            wrong[data.Name] = wrong.GetValueOrDefault(data.Name) + 1;
                        }
                    }
                }
            }
        }
        Assert.True(uphill + downhill > 200, $"only {uphill + downhill} flights found");
        Assert.True(downhill * 20 <= uphill, $"{downhill} steps climb away from their tall side, {uphill} toward it: "
            + string.Join(", ", wrong.OrderByDescending(kv => kv.Value).Take(15).Select(kv => $"{kv.Key} {kv.Value}")));
    }
}

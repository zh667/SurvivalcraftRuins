using System.Text.Json;

namespace SurvivalcraftRuins.Core;

public readonly record struct LootEntry(string BlockName, int Data, int Weight, int MinCount, int MaxCount);

public readonly record struct LootItem(string BlockName, int Data, int Count);

/// <summary>
/// Weighted loot pool. An entry with an empty block name is a deliberate miss,
/// used to make rare pools frequently come up empty.
/// </summary>
public sealed class LootPool
{
    public required string Name { get; init; }
    public required int MinRolls { get; init; }
    public required int MaxRolls { get; init; }
    public required IReadOnlyList<LootEntry> Entries { get; init; }

    public List<LootItem> Roll(Random random)
    {
        var result = new List<LootItem>();
        if (Entries.Count == 0)
        {
            return result;
        }
        int totalWeight = 0;
        foreach (var e in Entries)
        {
            totalWeight += e.Weight;
        }
        int rolls = random.Next(MinRolls, MaxRolls + 1);
        for (int i = 0; i < rolls; i++)
        {
            int pick = random.Next(totalWeight);
            foreach (var e in Entries)
            {
                pick -= e.Weight;
                if (pick < 0)
                {
                    if (!string.IsNullOrEmpty(e.BlockName))
                    {
                        int count = random.Next(e.MinCount, e.MaxCount + 1);
                        if (count > 0)
                        {
                            result.Add(new LootItem(e.BlockName, e.Data, count));
                        }
                    }
                    break;
                }
            }
        }
        return result;
    }
}

/// <summary>
/// Parses Assets/Loot/pools.json:
/// { "pools": [ { "name": "...", "rolls": [min,max],
///                "entries": [ { "block": "IronIngotBlock", "data": 0, "w": 6, "min": 1, "max": 3 },
///                             { "block": "", "w": 4 } ] } ] }
/// </summary>
public static class LootConfig
{
    public static Dictionary<string, LootPool> Parse(string json)
    {
        static int IntOr(JsonElement e, string property, int fallback)
        {
            return e.TryGetProperty(property, out var v) ? v.GetInt32() : fallback;
        }

        var pools = new Dictionary<string, LootPool>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        foreach (var p in document.RootElement.GetProperty("pools").EnumerateArray())
        {
            var rolls = p.GetProperty("rolls");
            var entries = new List<LootEntry>();
            foreach (var e in p.GetProperty("entries").EnumerateArray())
            {
                string block = e.TryGetProperty("block", out var b) ? b.GetString() ?? "" : "";
                entries.Add(new LootEntry(
                    block,
                    IntOr(e, "data", 0),
                    IntOr(e, "w", 1),
                    IntOr(e, "min", 1),
                    IntOr(e, "max", 1)));
            }
            string name = p.GetProperty("name").GetString()!;
            pools[name] = new LootPool
            {
                Name = name,
                MinRolls = rolls[0].GetInt32(),
                MaxRolls = rolls[1].GetInt32(),
                Entries = entries,
            };
        }
        return pools;
    }
}

using SurvivalcraftRuins.Builders;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// Placement slots. Each slot rolls one candidate per grid cell and realizes a
/// random variant, so the map stays varied without stacking density. Slot
/// order is priority: earlier slots win footprint overlaps.
/// </summary>
public static class StructureRegistry
{
    private static readonly WeatherProfile s_stoneRuin = new()
    {
        DecayChance = 0.04f,
        SwapChance = 0.2f,
        Swaps = new Dictionary<string, string> { ["StoneBrickBlock"] = "CobblestoneBlock" },
    };

    public static IReadOnlyList<StructureDef> CreateDefs(IReadOnlyDictionary<string, StructureData> templates)
    {
        Func<int, StructureData> Template(string name)
        {
            if (!templates.ContainsKey(name))
            {
                throw new InvalidOperationException($"Missing structure template '{name}'.");
            }
            return _ => templates[name];
        }

        return new[]
        {
            new StructureDef
            {
                Name = "sky",
                GridChunks = 24,
                Chance = 0.45f,
                Anchor = AnchorKind.Sky,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "sky_fortress",
                        Provider = Template("sky_fortress"),
                        ChestPools = Enumerable.Repeat("sky_cache", 12).Concat(Enumerable.Repeat("vault", 4)).ToArray(),
                    },
                    new StructureVariant
                    {
                        Name = "sky_blimp",
                        Provider = Template("sky_blimp"),
                        ChestPools = new[] { "sky_cache" },
                    },
                },
            },
            new StructureDef
            {
                Name = "grand",
                GridChunks = 24,
                Chance = 0.45f,
                Anchor = AnchorKind.Land,
                MaxSlope = 8f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "coliseum",
                        Provider = Template("coliseum"),
                        ChestPools = new[] { "arena" },
                        Weather = s_stoneRuin,
                        SinkDepth = 2,
                        FoundationBlock = "CobblestoneBlock",
                        Ambush = new AmbushSpec
                        {
                            CreatureTemplate = "Lion",
                            MinCount = 1,
                            MaxCount = 2,
                            TriggerRadius = 10f,
                        },
                    },
                    new StructureVariant
                    {
                        Name = "ruined_temple",
                        Provider = Template("ruined_temple"),
                        ChestPools = Enumerable.Repeat("offering", 6).Concat(Enumerable.Repeat("vault", 2)).ToArray(),
                        Weather = s_stoneRuin,
                        SinkDepth = 2,
                        FoundationBlock = "CobblestoneBlock",
                        Ambush = new AmbushSpec
                        {
                            CreatureTemplate = "Jaguar",
                            MinCount = 2,
                            MaxCount = 3,
                            TriggerRadius = 10f,
                        },
                    },
                    new StructureVariant
                    {
                        Name = "bathhouse",
                        Provider = Template("bathhouse"),
                        ChestPools = Enumerable.Repeat("trader_goods", 14).Concat(Enumerable.Repeat("offering", 4)).ToArray(),
                        Weather = s_stoneRuin,
                        SinkDepth = 2,
                        FoundationBlock = "CobblestoneBlock",
                    },
                },
            },
            new StructureDef
            {
                Name = "ocean_ship",
                GridChunks = 18,
                Chance = 0.45f,
                Anchor = AnchorKind.Ocean,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "galley",
                        Provider = Template("galley"),
                        ChestPools = Enumerable.Repeat("ship_supply", 8).Concat(Enumerable.Repeat("ship_treasure", 3)).ToArray(),
                        SinkDepth = 2,
                        Weather = new WeatherProfile { DecayChance = 0.03f },
                    },
                },
            },
            new StructureDef
            {
                Name = "lighthouse",
                GridChunks = 20,
                Chance = 0.5f,
                Anchor = AnchorKind.Coast,
                MaxSlope = 5f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "lighthouse",
                        Provider = LighthouseBuilder.Build,
                        ChestPools = new[] { "lighthouse_keeper", "baseline" },
                        Weather = new WeatherProfile
                        {
                            DecayChance = 0.03f,
                            SwapChance = 0.15f,
                            Swaps = new Dictionary<string, string> { ["StoneBrickBlock"] = "CobblestoneBlock" },
                        },
                        SinkDepth = 1,
                        FoundationBlock = "CobblestoneBlock",
                    },
                },
            },
            new StructureDef
            {
                Name = "shipwreck",
                GridChunks = 16,
                Chance = 0.45f,
                Anchor = AnchorKind.Offshore,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "shipwreck",
                        Provider = ShipwreckBuilder.Build,
                        ChestPools = new[] { "ship_supply", "ship_supply", "ship_treasure" },
                        Weather = new WeatherProfile { DecayChance = 0.05f },
                        SinkDepth = 1,
                    },
                },
            },
            new StructureDef
            {
                Name = "mine",
                GridChunks = 14,
                Chance = 0.5f,
                Anchor = AnchorKind.Land,
                MaxSlope = 4f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "abandoned_mine",
                        Provider = MineBuilder.Build,
                        ChestPools = new[] { "mine_supply", "mine_supply" },
                        Weather = new WeatherProfile { DecayChance = 0.03f },
                        SinkDepth = 1,
                        Ambush = new AmbushSpec
                        {
                            CreatureTemplate = "Hyena",
                            MinCount = 2,
                            MaxCount = 3,
                            TriggerRadius = 9f,
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "den",
                GridChunks = 13,
                Chance = 0.5f,
                Anchor = AnchorKind.Land,
                MaxSlope = 5f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "werewolf_den",
                        Provider = Template("werewolf_den"),
                        ChestPools = new[] { "den_cache", "den_cache", "baseline" },
                        Weather = new WeatherProfile
                        {
                            DecayChance = 0.10f,
                            SwapChance = 0.35f,
                            Swaps = new Dictionary<string, string> { ["StoneBrickBlock"] = "CobblestoneBlock" },
                        },
                        SinkDepth = 3,
                        FoundationBlock = "CobblestoneBlock",
                        Ambush = new AmbushSpec
                        {
                            CreatureTemplate = "Wolf_Gray",
                            MinCount = 4,
                            MaxCount = 6,
                            TriggerRadius = 12f,
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "tower",
                GridChunks = 14,
                Chance = 0.5f,
                Anchor = AnchorKind.Land,
                MaxSlope = 5f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "watchtower",
                        Provider = WatchtowerBuilder.Build,
                        ChestPools = new[] { "baseline", "watchman" },
                        Weather = new WeatherProfile { DecayChance = 0.03f },
                        SinkDepth = 1,
                        FoundationBlock = "CobblestoneBlock",
                    },
                    new StructureVariant
                    {
                        Name = "windmill",
                        Provider = Template("windmill"),
                        ChestPools = Enumerable.Repeat("homestead_cellar", 8).Concat(Enumerable.Repeat("homestead", 8)).ToArray(),
                        Weather = new WeatherProfile { DecayChance = 0.03f },
                        SinkDepth = 1,
                        FoundationBlock = "CobblestoneBlock",
                    },
                },
            },
            new StructureDef
            {
                Name = "homestead",
                GridChunks = 11,
                Chance = 0.5f,
                Anchor = AnchorKind.Land,
                MaxSlope = 3f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "prairie_ruin",
                        Provider = Template("prairie_ruin"),
                        ChestPools = new[] { "homestead_cellar", "homestead" },
                        Weather = new WeatherProfile
                        {
                            DecayChance = 0.08f,
                            SwapChance = 0.25f,
                            Swaps = new Dictionary<string, string> { ["StoneBrickBlock"] = "CobblestoneBlock" },
                        },
                        SinkDepth = 1,
                    },
                    new StructureVariant
                    {
                        Name = "mushroom_house",
                        Provider = Template("mushroom_house"),
                        ChestPools = new[] { "homestead_cellar", "homestead" },
                        SinkDepth = 2,
                    },
                    new StructureVariant
                    {
                        Name = "tree_house",
                        Provider = Template("tree_house"),
                        ChestPools = new[] { "camp_supply", "baseline" },
                        SinkDepth = 2,
                    },
                },
            },
            new StructureDef
            {
                Name = "coast_hut",
                GridChunks = 10,
                Chance = 0.5f,
                Anchor = AnchorKind.Coast,
                MaxSlope = 5f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "fishing_hut",
                        Provider = Template("fishing_hut"),
                        ChestPools = new[] { "fisher_supply", "fisher_supply", "baseline" },
                        Weather = new WeatherProfile { DecayChance = 0.04f },
                        SinkDepth = 1,
                        FoundationBlock = "GraniteBlock",
                    },
                },
            },
            new StructureDef
            {
                Name = "small",
                GridChunks = 7,
                Chance = 0.6f,
                Anchor = AnchorKind.Land,
                MaxSlope = 3f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "hunter_camp",
                        Provider = CampBuilder.Build,
                        ChestPools = new[] { "camp_supply", "baseline" },
                        SinkDepth = 1,
                    },
                    new StructureVariant
                    {
                        Name = "market_stall",
                        Provider = MarketStallBuilder.Build,
                        ChestPools = new[] { "trader_goods", "trader_goods" },
                        SinkDepth = 1,
                    },
                    new StructureVariant
                    {
                        Name = "shrine",
                        Provider = ShrineBuilder.Build,
                        ChestPools = new[] { "offering" },
                        SinkDepth = 1,
                        FoundationBlock = "CobblestoneBlock",
                    },
                    new StructureVariant
                    {
                        Name = "wishing_well",
                        Provider = Template("wishing_well"),
                        ChestPools = new[] { "well_treasure" },
                        Weather = new WeatherProfile { DecayChance = 0.03f },
                        SinkDepth = 9,
                    },
                },
            },
        };
    }
}

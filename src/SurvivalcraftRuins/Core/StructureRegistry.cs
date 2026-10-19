using SurvivalcraftRuins.Builders;
using SurvivalcraftRuins.Core.Village;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// Placement slots. Each slot rolls one candidate per grid cell and realizes a
/// random variant, so the map stays varied without stacking density. Slot
/// order is priority: earlier slots win footprint overlaps and keep-out radii.
/// </summary>
public static class StructureRegistry
{
    /// <summary>
    /// Which revision of the placement table generated a world's chunks. It is
    /// recorded in the world file for diagnostics only: chunks generated from
    /// now on always use the current table, so a world created before the mod
    /// existed, or with any older version of it, still gets today's structures
    /// in the land it has yet to explore.
    /// </summary>
    public const int CurrentLayoutVersion = 6;

    private static readonly WeatherProfile s_stoneRuin = new()
    {
        DecayChance = 0.04f,
        SwapChance = 0.2f,
        Swaps = new Dictionary<string, string> { ["StoneBrickBlock"] = "CobblestoneBlock" },
    };

    // Guard rosters. Every name is an EntityTemplate in the game's Database.xml.
    private static readonly string[] s_lightGuards = { "Wolf_Coyote", "Hyena" };
    private static readonly string[] s_mediumGuards = { "Jaguar", "Leopard", "Bear_Black" };
    private static readonly string[] s_heavyGuards = { "Lion", "Tiger", "Bear_Brown" };
    private static readonly string[] s_eliteGuards = { "Tiger_White", "Lion", "Rhino" };
    private static readonly string[] s_packGuards = { "Wolf_Gray" };
    private static readonly string[] s_reefGuards = { "Shark_Bull", "Barracuda", "Piranha" };
    private static readonly string[] s_deepGuards = { "Shark_Tiger", "Shark_GreatWhite", "Barracuda" };

    public static IReadOnlyList<StructureDef> CreateDefs(
        IReadOnlyDictionary<string, StructureData> templates,
        IReadOnlyDictionary<string, VillageStyle>? villages = null)
    {
        Func<int, StructureData> Template(string name)
        {
            if (!templates.ContainsKey(name))
            {
                throw new InvalidOperationException($"Missing structure template '{name}'.");
            }
            return _ => templates[name];
        }

        var defs = new[]
        {
            new StructureDef
            {
                Name = "sky",
                GridChunks = 40,
                Chance = 0.45f,
                SeparationRadius = 150,
                Anchor = AnchorKind.Sky,
                DensityExponent = 0.5f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "sky_fortress",
                        Provider = Template("sky_fortress"),
                        ChestPools = Enumerable.Repeat("sky_cache", 12).Concat(Enumerable.Repeat("vault", 4)).ToArray(),
                        // The richest loot in the mod; it now costs a fight.
                        Guards = new GuardSpec
                        {
                            Creatures = s_eliteGuards,
                            PerChest = 0.7f,
                            MinTotal = 8,
                            MaxTotal = 12,
                            TriggerRadius = 9f,
                        },
                    },
                    new StructureVariant
                    {
                        Name = "sky_blimp",
                        Provider = Template("sky_blimp"),
                        ChestPools = new[] { "sky_cache" },
                        Guards = new GuardSpec
                        {
                            Creatures = s_lightGuards,
                            PerChest = 1f,
                            MinTotal = 1,
                            MaxTotal = 2,
                            TriggerRadius = 8f,
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "grand",
                GridChunks = 40,
                Chance = 0.5f,
                SeparationRadius = 150,
                Anchor = AnchorKind.Land,
                MaxSlope = 8f,
                DensityExponent = 0.5f,
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
                        // Was up to 24 lions (1-2 at each of 12 markers).
                        Guards = new GuardSpec
                        {
                            Creatures = s_heavyGuards,
                            PerChest = 1f,
                            MinTotal = 6,
                            MaxTotal = 10,
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
                        Guards = new GuardSpec
                        {
                            Creatures = s_mediumGuards,
                            PerChest = 0.8f,
                            MinTotal = 5,
                            MaxTotal = 8,
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
                        Guards = new GuardSpec
                        {
                            Creatures = s_lightGuards,
                            PerChest = 0.25f,
                            MinTotal = 3,
                            MaxTotal = 5,
                            TriggerRadius = 9f,
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "monument",
                GridChunks = 34,
                Chance = 0.55f,
                SeparationRadius = 160,
                Anchor = AnchorKind.DeepOcean,
                DensityExponent = 0.5f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "ocean_monument",
                        Provider = OceanMonumentBuilder.Build,
                        Bounds = OceanMonumentBuilder.Bounds,
                        // Chests sort by height, so the two wing caches at y=2
                        // come before the pair in the vault at y=5.
                        ChestPools = new[] { "monument_wing", "monument_wing", "monument_vault", "monument_vault" },
                        Guards = new GuardSpec
                        {
                            Creatures = s_deepGuards,
                            PerChest = 1.5f,
                            BaseCount = 2,
                            MinTotal = 6,
                            MaxTotal = 10,
                            TriggerRadius = 12f,
                            Aquatic = true,
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "desert_temple",
                GridChunks = 11,
                Chance = 0.37f,
                SeparationRadius = 110,
                Anchor = AnchorKind.Land,
                MaxSlope = 8.0f,
                Biome = BiomeFilter.Desert,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "desert_temple",
                        Provider = DesertTempleBuilder.Build,
                        ChestPools = new[] { "desert_temple" },
                        Weather = new WeatherProfile { DecayChance = 0.02f },
                        SinkDepth = 1,
                        FoundationBlock = "SandstoneBlock",
                        Guards = new GuardSpec
                        {
                            Creatures = s_mediumGuards,
                            PerChest = 0.75f,
                            MinTotal = 2,
                            MaxTotal = 4,
                            TriggerRadius = 10f,
                            // Trap triggers sit next to the loot here.
                            PostsAtMarkers = true,
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "jungle_temple",
                GridChunks = 12,
                Chance = 0.28f,
                SeparationRadius = 100,
                Anchor = AnchorKind.Land,
                MaxSlope = 8.0f,
                Biome = BiomeFilter.Jungle,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "jungle_temple",
                        Provider = JungleTempleBuilder.Build,
                        ChestPools = new[] { "jungle_temple", "jungle_temple" },
                        DispenserPool = "trap_arrows",
                        Weather = new WeatherProfile { DecayChance = 0.03f },
                        SinkDepth = 1,
                        FoundationBlock = "CobblestoneBlock",
                        Guards = new GuardSpec
                        {
                            Creatures = s_mediumGuards,
                            PerChest = 1f,
                            MinTotal = 2,
                            MaxTotal = 4,
                            TriggerRadius = 10f,
                            // Trap triggers sit next to the loot here.
                            PostsAtMarkers = true,
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "ocean_ship",
                GridChunks = 32,
                Chance = 0.45f,
                SeparationRadius = 90,
                Anchor = AnchorKind.Ocean,
                DensityExponent = 0.5f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "galley",
                        Provider = Template("galley"),
                        ChestPools = Enumerable.Repeat("ship_supply", 8).Concat(Enumerable.Repeat("ship_treasure", 3)).ToArray(),
                        SinkDepth = 2,
                        Weather = new WeatherProfile { DecayChance = 0.03f },
                        Guards = new GuardSpec
                        {
                            Creatures = s_lightGuards,
                            PerChest = 0.3f,
                            MinTotal = 2,
                            MaxTotal = 4,
                            TriggerRadius = 9f,
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "lighthouse",
                GridChunks = 28,
                Chance = 0.5f,
                SeparationRadius = 80,
                Anchor = AnchorKind.Coast,
                MaxSlope = 5f,
                DensityExponent = 0.5f,
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
                GridChunks = 28,
                Chance = 0.5f,
                SeparationRadius = 80,
                Anchor = AnchorKind.Offshore,
                DensityExponent = 0.5f,
                Variants = new[]
                {
                    new StructureVariant
                    {
                        Name = "shipwreck",
                        Provider = ShipwreckBuilder.Build,
                        ChestPools = new[] { "ship_supply", "ship_supply", "ship_treasure" },
                        Weather = new WeatherProfile { DecayChance = 0.05f },
                        SinkDepth = 1,
                        Guards = new GuardSpec
                        {
                            Creatures = s_reefGuards,
                            PerChest = 1f,
                            MinTotal = 2,
                            MaxTotal = 4,
                            TriggerRadius = 10f,
                            Aquatic = true,
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "mine",
                GridChunks = 18,
                Chance = 0.28f,
                SeparationRadius = 70,
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
                        Guards = new GuardSpec
                        {
                            Creatures = s_lightGuards,
                            PerChest = 1.5f,
                            MinTotal = 3,
                            MaxTotal = 5,
                            TriggerRadius = 9f,
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "den",
                GridChunks = 24,
                Chance = 0.29f,
                SeparationRadius = 70,
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
                        // Wolves that turn at night, plus one that already has.
                        Guards = new GuardSpec
                        {
                            Creatures = new[] { "Wolf_Gray", "Wolf_Gray", "Werewolf" },
                            PerChest = 1.5f,
                            MinTotal = 4,
                            MaxTotal = 6,
                            TriggerRadius = 12f,
                            MundaneSubstitute = "Wolf_Gray",
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "tower",
                GridChunks = 15,
                Chance = 0.31f,
                SeparationRadius = 60,
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
                        Guards = new GuardSpec
                        {
                            Creatures = s_lightGuards,
                            PerChest = 0.5f,
                            MinTotal = 1,
                            MaxTotal = 2,
                            TriggerRadius = 8f,
                        },
                    },
                    new StructureVariant
                    {
                        Name = "windmill",
                        Provider = Template("windmill"),
                        ChestPools = Enumerable.Repeat("homestead_cellar", 8).Concat(Enumerable.Repeat("homestead", 8)).ToArray(),
                        Weather = new WeatherProfile { DecayChance = 0.03f },
                        SinkDepth = 1,
                        FoundationBlock = "CobblestoneBlock",
                        Guards = new GuardSpec
                        {
                            Creatures = s_lightGuards,
                            PerChest = 0.2f,
                            MinTotal = 2,
                            MaxTotal = 3,
                            TriggerRadius = 8f,
                        },
                    },
                },
            },
            new StructureDef
            {
                Name = "homestead",
                GridChunks = 11,
                Chance = 0.33f,
                SeparationRadius = 55,
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
                        Guards = new GuardSpec
                        {
                            Creatures = s_lightGuards,
                            PerChest = 0.5f,
                            MinTotal = 1,
                            MaxTotal = 2,
                            TriggerRadius = 8f,
                        },
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
                GridChunks = 9,
                Chance = 0.35f,
                SeparationRadius = 50,
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
                GridChunks = 10,
                Chance = 0.45f,
                SeparationRadius = 40,
                Anchor = AnchorKind.Land,
                MaxSlope = 3f,
                Variants = new[]
                {
                    // Deliberately unguarded: the small finds stay a free
                    // reward, which is what keeps exploring pleasant.
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

        var list = defs.ToList();
        var village = CreateVillageDef(villages);
        if (village != null)
        {
            // First: everything else makes room for a village. Behind the sky
            // fortresses a good share of them was refused for one hanging
            // over part of the site.
            list.Insert(0, village);
        }
        return list;
    }

    /// <summary>
    /// Minecraft villages: one slot whose variants are every layout of every
    /// style, the style picked by the climate at the site. Rarer than the
    /// grand landmarks per cell, but they are big: a village spans 70-150
    /// blocks, so small finds are kept a good walk away from its centre.
    /// </summary>
    public static StructureDef? CreateVillageDef(IReadOnlyDictionary<string, VillageStyle>? villages)
    {
        if (villages == null || villages.Count == 0)
        {
            return null;
        }
        var variants = new List<StructureVariant>();
        var byStyle = new Dictionary<string, List<StructureVariant>>(StringComparer.Ordinal);
        foreach (var style in villages.Values.OrderBy(v => v.Name, StringComparer.Ordinal))
        {
            var mine = new List<StructureVariant>();
            for (int i = 0; i < style.Layouts.Length; i++)
            {
                int layout = i;
                var variant = new StructureVariant
                {
                    Name = VillageBuilder.StructureName(style, layout),
                    Provider = _ => VillageBuilder.BuildFlat(style, layout),
                    SitedProvider = request => VillageBuilder.Build(
                        style, layout, request.CenterX, request.CenterZ, request.Sampler),
                    ChestPools = Array.Empty<string>(),
                };
                mine.Add(variant);
                variants.Add(variant);
            }
            byStyle[style.Name] = mine;
        }
        return new StructureDef
        {
            Name = "village",
            GridChunks = 40,
            Chance = 0.45f,
            Anchor = AnchorKind.Settlement,
            Footprint = 176,
            SeparationRadius = 160,
            Variants = variants,
            PickVariant = (temperature, humidity, hash) =>
                byStyle.TryGetValue(VillageClimate.StyleFor(temperature, humidity), out var options) && options.Count > 0
                    ? options[(int)(hash % (uint)options.Count)]
                    : null,
        };
    }
}

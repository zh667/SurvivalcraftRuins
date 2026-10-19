namespace SurvivalcraftRuins.Core.Village;

/// <summary>Per-piece floors and survivors of one village on one site.</summary>
public sealed record VillagePlan(int[] Floors, int[] ClearFrom, bool[] Keep, int Houses, int HousesKept);

/// <summary>
/// Lays a precomputed village layout onto real terrain.
///
/// Heights follow Minecraft's rules: the town centre's bottom layer sits on
/// the ground at its middle; a rigid piece hung off another rigid piece keeps
/// its offset from it; anything hung off a street takes the ground surface at
/// the street's connector; and streets themselves follow the ground column by
/// column, with planks where they cross water. Minecraft then reshapes the
/// terrain around each piece ("beardifier"); here each building clears its
/// box and gets foundation under its floor instead, so a house on a slope is
/// neither buried nor left floating.
///
/// Buildings that would stand in water or straddle a step of more than
/// <see cref="MaxBuildingRelief"/> blocks are left out; a site that loses its
/// town centre or too many houses is refused, and placement tries elsewhere.
/// </summary>
public static class VillageBuilder
{
    /// <summary>Largest ground height difference allowed under one building.</summary>
    public const int MaxBuildingRelief = 6;

    /// <summary>Share of a layout's houses that must survive the terrain.</summary>
    public const float MinHouseShare = 0.6f;

    /// <summary>Foundation never reaches deeper than this below a floor.</summary>
    public const int MaxFoundationDepth = 24;

    public static string StructureName(VillageStyle style, int layoutIndex) => $"village_{style.Name}_{layoutIndex}";

    /// <summary>
    /// Builds the layout centred on (centerX, centerZ), or returns null when
    /// the terrain there will not take it.
    /// </summary>
    public static SitedStructure? Build(VillageStyle style, int layoutIndex, int centerX, int centerZ, IWorldSampler sampler,
        bool checkClimate = true)
    {
        var plan = Plan(style, layoutIndex, centerX, centerZ, sampler, checkClimate);
        return plan == null ? null : Realize(style, layoutIndex, centerX, centerZ, sampler, plan);
    }

    /// <summary>
    /// Decides each piece's floor and which buildings the terrain accepts,
    /// without producing any cells; null when the site is refused.
    /// </summary>
    public static VillagePlan? Plan(VillageStyle style, int layoutIndex, int centerX, int centerZ, IWorldSampler sampler,
        bool checkClimate = true)
    {
        if (checkClimate && !ClimateHolds(style, centerX, centerZ, sampler))
        {
            return null;
        }
        var layout = style.Layouts[layoutIndex];
        var ground = new GroundCache(sampler);
        int ocean = sampler.OceanLevel;
        int n = layout.Pieces.Length;
        var floors = new int[n];
        var clearFrom = new int[n];
        var keep = new bool[n];
        int houses = 0, housesKept = 0;

        for (int i = 0; i < n; i++)
        {
            var lp = layout.Pieces[i];
            var piece = style.Pieces[lp.Ref];
            if (piece.Kind == VillagePieceKind.House)
            {
                houses++;
            }
            if (!piece.Rigid)
            {
                keep[i] = true;
                continue;
            }
            int floor;
            if (lp.Anchor.OnGround)
            {
                int surface = ground.Surface(centerX + lp.Anchor.X, centerZ + lp.Anchor.Z);
                floor = surface + 1 + lp.Anchor.Dy;
                // Never dig below the ground the piece is hung from: a square
                // whose bottom layer has gaps would otherwise get holes in it.
                clearFrom[i] = Math.Max(floor, surface + 1);
            }
            else
            {
                if (!keep[lp.Anchor.Parent])
                {
                    continue;
                }
                floor = floors[lp.Anchor.Parent] + lp.Anchor.Dy;
                clearFrom[i] = floor;
            }
            bool fits = floor >= 3 && floor + piece.SizeY <= 250;
            if (fits && piece.Kind != VillagePieceKind.Well)
            {
                fits = SiteFits(ground, ocean, centerX + lp.X, centerZ + lp.Z, piece,
                    checkRelief: piece.Kind != VillagePieceKind.Decor);
            }
            if (!fits)
            {
                if (piece.Kind == VillagePieceKind.Center)
                {
                    return null;
                }
                continue;
            }
            floors[i] = floor;
            keep[i] = true;
            if (piece.Kind == VillagePieceKind.House)
            {
                housesKept++;
            }
        }
        if (houses > 0 && housesKept < Math.Max(3, (int)MathF.Ceiling(houses * MinHouseShare)))
        {
            return null;
        }
        return new VillagePlan(floors, clearFrom, keep, houses, housesKept);
    }

    private static SitedStructure Realize(VillageStyle style, int layoutIndex, int centerX, int centerZ,
        IWorldSampler sampler, VillagePlan plan)
    {
        var layout = style.Layouts[layoutIndex];
        var ground = new GroundCache(sampler);
        int ocean = sampler.OceanLevel;
        int n = layout.Pieces.Length;
        var floors = plan.Floors;
        var clearFrom = plan.ClearFrom;
        var keep = plan.Keep;
        int originY = floors[0];
        var cells = new List<Cell>();
        var chests = new List<ChestMarker>();
        var prep = new List<SitePrep>();
        ushort foundation = (ushort)style.FoundationPalette;
        ushort bridge = (ushort)style.BridgePalette;

        for (int i = 0; i < n; i++)
        {
            if (!keep[i])
            {
                continue;
            }
            var lp = layout.Pieces[i];
            var piece = style.Pieces[lp.Ref];
            if (piece.Rigid)
            {
                int dy = floors[i] - originY;
                foreach (var c in piece.Cells)
                {
                    cells.Add(new Cell((short)(lp.X + c.X), (short)(dy + c.Y), (short)(lp.Z + c.Z), c.PaletteIndex));
                }
                foreach (var chest in piece.Chests)
                {
                    chests.Add(chest with { X = (short)(lp.X + chest.X), Y = (short)(dy + chest.Y), Z = (short)(lp.Z + chest.Z) });
                }
                bool clears = piece.Kind is VillagePieceKind.House or VillagePieceKind.Center;
                if (piece.Kind == VillagePieceKind.Well)
                {
                    continue;
                }
                for (int x = 0; x < piece.SizeX; x++)
                {
                    for (int z = 0; z < piece.SizeZ; z++)
                    {
                        bool floored = piece.FloorColumns[x + z * piece.SizeX];
                        if (!clears && !floored)
                        {
                            continue;
                        }
                        prep.Add(new SitePrep
                        {
                            X = (short)(lp.X + x),
                            Z = (short)(lp.Z + z),
                            FoundationTop = floored ? (short)(dy - 1) : SitePrep.None,
                            ClearBottom = clears ? (short)(clearFrom[i] - originY) : (short)1,
                            ClearTop = clears ? (short)(dy + piece.SizeY - 1) : (short)0,
                            FoundationPalette = foundation,
                        });
                    }
                }
            }
            else
            {
                foreach (var c in piece.Cells)
                {
                    int wx = centerX + lp.X + c.X;
                    int wz = centerZ + lp.Z + c.Z;
                    int top = ground.Top(wx, wz);
                    bool water = top < ocean;
                    int y = (water ? ocean : top) + c.Y;
                    ushort palette = water && c.Y == 0 ? bridge : c.PaletteIndex;
                    short x = (short)(lp.X + c.X), z = (short)(lp.Z + c.Z);
                    cells.Add(new Cell(x, (short)(y - originY), z, palette));
                    if (c.Y == 0 && !water)
                    {
                        prep.Add(new SitePrep
                        {
                            X = x,
                            Z = z,
                            FoundationTop = (short)(y - 1 - originY),
                            ClearBottom = (short)(y + 1 - originY),
                            ClearTop = (short)(y + 2 - originY),
                            FoundationPalette = foundation,
                            CoverOnly = true,
                        });
                    }
                }
            }
        }

        var data = Assemble(StructureName(style, layoutIndex), style.Palette, cells, chests, prep);
        return new SitedStructure(data, originY);
    }

    /// <summary>The same layout on endless flat grass at height 64: for tests and tools.</summary>
    public static StructureData BuildFlat(VillageStyle style, int layoutIndex)
    {
        return Build(style, layoutIndex, 0, 0, FlatGround.Instance, checkClimate: false)!.Data;
    }

    /// <summary>How far from the centre the climate is checked.</summary>
    public const int ClimateReach = 56;

    /// <summary>Of the centre and four probes at <see cref="ClimateReach"/>, how many must agree.</summary>
    public const int ClimateVotes = 4;

    /// <summary>
    /// The village stands in its own climate: a desert village half on grass,
    /// or a snowy one reaching into a jungle, reads as a mistake. Survivalcraft
    /// steps its climate in narrow bands (noise times three, cut into 16
    /// levels), so demanding all five probes would refuse a third of good
    /// sites; one edge may run over a border, the middle may not.
    /// </summary>
    public static bool ClimateHolds(VillageStyle style, int centerX, int centerZ, IWorldSampler sampler)
    {
        int r = ClimateReach;
        ReadOnlySpan<(int, int)> probes = stackalloc (int, int)[] { (0, 0), (r, 0), (-r, 0), (0, r), (0, -r) };
        int votes = 0;
        foreach (var (dx, dz) in probes)
        {
            int x = centerX + dx, z = centerZ + dz;
            if (VillageClimate.StyleFor(sampler.Temperature(x, z), sampler.Humidity(x, z)) == style.Name)
            {
                votes++;
            }
            else if (dx == 0 && dz == 0)
            {
                return false;
            }
        }
        return votes >= ClimateVotes;
    }

    private static bool SiteFits(GroundCache ground, int ocean, int minX, int minZ, VillagePiece piece, bool checkRelief)
    {
        int maxX = minX + piece.SizeX - 1;
        int maxZ = minZ + piece.SizeZ - 1;
        Span<int> tops = stackalloc int[]
        {
            ground.Top(minX, minZ), ground.Top(maxX, minZ), ground.Top(minX, maxZ), ground.Top(maxX, maxZ),
            ground.Top((minX + maxX) / 2, (minZ + maxZ) / 2),
        };
        int lo = int.MaxValue, hi = int.MinValue;
        foreach (int top in tops)
        {
            if (top < ocean)
            {
                return false;
            }
            lo = Math.Min(lo, top);
            hi = Math.Max(hi, top);
        }
        return !checkRelief || hi - lo <= MaxBuildingRelief;
    }

    /// <summary>
    /// StructureData whose bounds cover the ground work as well as the cells,
    /// so every chunk a building clears is visited even where it has no block.
    /// </summary>
    private static StructureData Assemble(string name, IReadOnlyList<PaletteEntry> palette, List<Cell> cells,
        List<ChestMarker> chests, List<SitePrep> prep)
    {
        var data = StructureData.FromCells(name, palette, cells, chests, Array.Empty<AmbushMarker>(), prep);
        int minX = data.MinX, minZ = data.MinZ, maxX = data.MaxX, maxZ = data.MaxZ;
        foreach (var p in prep)
        {
            minX = Math.Min(minX, p.X);
            minZ = Math.Min(minZ, p.Z);
            maxX = Math.Max(maxX, p.X);
            maxZ = Math.Max(maxZ, p.Z);
        }
        return new StructureData
        {
            Name = name,
            MinX = minX,
            MinY = data.MinY,
            MinZ = minZ,
            MaxX = maxX,
            MaxY = data.MaxY,
            MaxZ = maxZ,
            Palette = palette,
            Cells = data.Cells,
            Chests = data.Chests,
            Ambushes = data.Ambushes,
            SitePrep = data.SitePrep,
        };
    }

    /// <summary>Ground tops by column, asked once each: streets ask twice per column.</summary>
    private sealed class GroundCache
    {
        private readonly IWorldSampler m_sampler;
        private readonly Dictionary<long, int> m_tops = new();

        public GroundCache(IWorldSampler sampler)
        {
            m_sampler = sampler;
        }

        public int Top(int x, int z)
        {
            long key = ((long)x << 32) ^ (uint)z;
            if (!m_tops.TryGetValue(key, out int top))
            {
                top = m_sampler.GroundTop(x, z);
                m_tops[key] = top;
            }
            return top;
        }

        /// <summary>Topmost cell including water: Minecraft's WORLD_SURFACE_WG minus one.</summary>
        public int Surface(int x, int z)
        {
            return Math.Max(Top(x, z), m_sampler.OceanLevel);
        }
    }

    private sealed class FlatGround : IWorldSampler
    {
        public static readonly FlatGround Instance = new();

        public int OceanLevel => 0;

        public float Height(float x, float z) => 64f;
    }
}

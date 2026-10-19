using System.Text.Json;

namespace SurvivalcraftRuins.Core.Village;

public enum VillagePieceKind
{
    Center,
    Street,
    House,
    Decor,
    Well,
}

/// <summary>One converted Minecraft village piece at one rotation.</summary>
public sealed class VillagePiece
{
    public required string Name { get; init; }
    public required VillagePieceKind Kind { get; init; }

    /// <summary>Rigid pieces keep their shape; the others (streets) follow the ground per column.</summary>
    public required bool Rigid { get; init; }

    public required int SizeX { get; init; }
    public required int SizeY { get; init; }
    public required int SizeZ { get; init; }

    /// <summary>Cells relative to the piece's box minimum.</summary>
    public required Cell[] Cells { get; init; }

    /// <summary>Chests relative to the box minimum, each with its loot pool.</summary>
    public required ChestMarker[] Chests { get; init; }

    /// <summary>Footprint columns whose bottom layer is solid: they get foundation under them.</summary>
    public required bool[] FloorColumns { get; init; }
}

/// <summary>Where a piece's floor comes from (Minecraft's two rules).</summary>
public readonly record struct VillageAnchor(int Parent, int X, int Z, int Dy)
{
    /// <summary>Floor is the ground surface at (X, Z), plus one, plus Dy.</summary>
    public bool OnGround => Parent < 0;
}

public readonly record struct VillageLayoutPiece(int Ref, int X, int Z, int Floor, VillageAnchor Anchor);

public sealed class VillageLayout
{
    public required int Seed { get; init; }
    public required int Houses { get; init; }
    public required VillageLayoutPiece[] Pieces { get; init; }

    public int MinX { get; init; }
    public int MinZ { get; init; }
    public int MaxX { get; init; }
    public int MaxZ { get; init; }
}

/// <summary>
/// A village style (plains, desert, ...) as written by tools/village/build_villages.py:
/// shared palette, every piece the layouts use, and the layouts themselves.
/// </summary>
public sealed class VillageStyle
{
    public required string Name { get; init; }
    public required IReadOnlyList<PaletteEntry> Palette { get; init; }
    public required int BridgePalette { get; init; }
    public required int FoundationPalette { get; init; }
    public required VillagePiece[] Pieces { get; init; }
    public required VillageLayout[] Layouts { get; init; }

    public static VillageStyle Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        string name = root.GetProperty("style").GetString() ?? "plains";

        var palette = new List<PaletteEntry>();
        foreach (var pair in root.GetProperty("palette").EnumerateArray())
        {
            palette.Add(new PaletteEntry(pair[0].GetString() ?? "AirBlock", pair[1].GetInt32()));
        }
        int Add(string block, int data)
        {
            int index = palette.FindIndex(p => p.BlockName == block && p.Data == data);
            if (index < 0)
            {
                index = palette.Count;
                palette.Add(new PaletteEntry(block, data));
            }
            return index;
        }
        var bridge = root.GetProperty("bridge");
        int bridgePalette = Add(bridge[0].GetString() ?? "PlanksBlock", bridge[1].GetInt32());
        int foundationPalette = Add(name == "desert" ? "SandBlock" : "DirtBlock", 0);

        var pieces = new List<VillagePiece>();
        foreach (var p in root.GetProperty("pieces").EnumerateArray())
        {
            var size = p.GetProperty("size");
            int sx = size[0].GetInt32(), sy = size[1].GetInt32(), sz = size[2].GetInt32();
            var cells = new List<Cell>();
            var floor = new bool[sx * sz];
            foreach (var c in p.GetProperty("cells").EnumerateArray())
            {
                var cell = new Cell((short)c[0].GetInt32(), (short)c[1].GetInt32(), (short)c[2].GetInt32(),
                    (ushort)c[3].GetInt32());
                cells.Add(cell);
                if (cell.Y == 0 && cell.X >= 0 && cell.X < sx && cell.Z >= 0 && cell.Z < sz)
                {
                    floor[cell.X + cell.Z * sx] = true;
                }
            }
            var chests = new List<ChestMarker>();
            foreach (var c in p.GetProperty("chests").EnumerateArray())
            {
                chests.Add(new ChestMarker((short)c[0].GetInt32(), (short)c[1].GetInt32(), (short)c[2].GetInt32(),
                    c[3].GetString()));
            }
            pieces.Add(new VillagePiece
            {
                Name = p.GetProperty("name").GetString() ?? "",
                Kind = ParseKind(p.GetProperty("kind").GetString()),
                Rigid = p.GetProperty("rigid").GetBoolean(),
                SizeX = sx,
                SizeY = sy,
                SizeZ = sz,
                Cells = cells.ToArray(),
                Chests = chests.ToArray(),
                FloorColumns = floor,
            });
        }

        var layouts = new List<VillageLayout>();
        foreach (var l in root.GetProperty("layouts").EnumerateArray())
        {
            var list = new List<VillageLayoutPiece>();
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var lp in l.GetProperty("pieces").EnumerateArray())
            {
                var a = lp.GetProperty("anchor");
                var anchor = a.TryGetProperty("parent", out var parent)
                    ? new VillageAnchor(parent.GetInt32(), 0, 0, a.GetProperty("dy").GetInt32())
                    : new VillageAnchor(-1, a.GetProperty("x").GetInt32(), a.GetProperty("z").GetInt32(),
                        a.GetProperty("dy").GetInt32());
                var piece = new VillageLayoutPiece(lp.GetProperty("ref").GetInt32(), lp.GetProperty("x").GetInt32(),
                    lp.GetProperty("z").GetInt32(), lp.GetProperty("floor").GetInt32(), anchor);
                if (piece.Ref < 0 || piece.Ref >= pieces.Count)
                {
                    throw new InvalidDataException($"Village '{name}' layout names piece {piece.Ref} of {pieces.Count}.");
                }
                if (!anchor.OnGround && (anchor.Parent < 0 || anchor.Parent >= list.Count))
                {
                    throw new InvalidDataException($"Village '{name}' piece {list.Count} anchors to a later piece.");
                }
                var def = pieces[piece.Ref];
                minX = Math.Min(minX, piece.X);
                minZ = Math.Min(minZ, piece.Z);
                maxX = Math.Max(maxX, piece.X + def.SizeX - 1);
                maxZ = Math.Max(maxZ, piece.Z + def.SizeZ - 1);
                list.Add(piece);
            }
            layouts.Add(new VillageLayout
            {
                Seed = l.GetProperty("seed").GetInt32(),
                Houses = l.GetProperty("houses").GetInt32(),
                Pieces = list.ToArray(),
                MinX = minX,
                MinZ = minZ,
                MaxX = maxX,
                MaxZ = maxZ,
            });
        }

        return new VillageStyle
        {
            Name = name,
            Palette = palette,
            BridgePalette = bridgePalette,
            FoundationPalette = foundationPalette,
            Pieces = pieces.ToArray(),
            Layouts = layouts.ToArray(),
        };
    }

    private static VillagePieceKind ParseKind(string? kind)
    {
        return kind switch
        {
            "center" => VillagePieceKind.Center,
            "street" => VillagePieceKind.Street,
            "house" => VillagePieceKind.House,
            "well" => VillagePieceKind.Well,
            _ => VillagePieceKind.Decor,
        };
    }
}

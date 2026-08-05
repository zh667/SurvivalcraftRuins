using System.Text.Json;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// Loads converted structure templates from JSON:
/// { "name": "...", "palette": [["BlockName", data], ...],
///   "cells": [[x,y,z,paletteIndex], ...], "chests": [[x,y,z], ...], "spawners": [[x,y,z], ...] }
/// Cells listed with the palette name "AirBlock" carve terrain; cells absent
/// from the list leave terrain untouched.
/// </summary>
public static class StructureTemplate
{
    public static StructureData Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        string name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "unnamed" : "unnamed";

        var palette = new List<PaletteEntry>();
        foreach (var pair in root.GetProperty("palette").EnumerateArray())
        {
            palette.Add(new PaletteEntry(pair[0].GetString() ?? "AirBlock", pair[1].GetInt32()));
        }

        var cells = new List<Cell>();
        foreach (var q in root.GetProperty("cells").EnumerateArray())
        {
            cells.Add(new Cell(
                (short)q[0].GetInt32(), (short)q[1].GetInt32(), (short)q[2].GetInt32(),
                (ushort)q[3].GetInt32()));
        }

        var chests = new List<ChestMarker>();
        if (root.TryGetProperty("chests", out var chestArray))
        {
            foreach (var q in chestArray.EnumerateArray())
            {
                chests.Add(new ChestMarker((short)q[0].GetInt32(), (short)q[1].GetInt32(), (short)q[2].GetInt32()));
            }
        }

        var ambushes = new List<AmbushMarker>();
        if (root.TryGetProperty("spawners", out var spawnerArray))
        {
            foreach (var q in spawnerArray.EnumerateArray())
            {
                ambushes.Add(new AmbushMarker((short)q[0].GetInt32(), (short)q[1].GetInt32(), (short)q[2].GetInt32()));
            }
        }

        return StructureData.FromCells(name, palette, cells, chests, ambushes);
    }
}

#!/usr/bin/env python3
"""Build Survivalcraft village assets from the Minecraft 1.16.5 client jar.

Usage:
  build_villages.py <client.jar> <out-dir> [--layouts N] [--preview DIR]

For each of the five village styles this assembles candidate layouts with the
ported jigsaw algorithm (jigsaw.py, pools from pools.json), keeps N of them,
converts every (piece, rotation) the kept layouts use into Survivalcraft
blocks, and writes <out-dir>/village_<style>.json for the mod to load:

  { "style": ..., "palette": [[block, data], ...], "bridge": [block, data],
    "pieces": [ { "name", "kind", "rigid", "size": [x,y,z],
                  "cells": [[x,y,z,p], ...],           # relative to the box min
                  "chests": [[x,y,z,"pool"], ...] } ],
    "layouts": [ { "seed", "pieces": [ { "ref", "x", "z",   # box min, village-relative
                                          "anchor": {"x","z","dy"} | {"parent","dy"} } ] } ] }

Rigid pieces take their floor height from their anchor: the ground surface at
(x, z) plus dy, or their rigid parent's floor plus dy (Minecraft's two rules).
Terrain-matching pieces (streets) are laid per column on the ground surface.

Air is not exported. Minecraft places village pieces "legacy" style, which
ignores a template's air; the mod instead clears each rigid piece's box before
writing it, so a house set into a slope is not left full of hillside.
"""
import gzip
import json
import os
import random
import sys
import zipfile
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))

import convert_nbt as cn          # noqa: E402
import jigsaw as jg               # noqa: E402
import village_blocks as vb       # noqa: E402

STYLES = ["plains", "desert", "savanna", "snowy", "taiga"]

# Bridge block for streets that cross water, as Minecraft's STREET_* processors
# swap grass paths over water for planks.
BRIDGE = {"plains": "oak_planks", "desert": "smooth_sandstone", "savanna": "acacia_planks",
          "snowy": "spruce_planks", "taiga": "spruce_planks"}

# STREET_* processors: share of path blocks turned back into grass.
PATH_TO_GRASS = {"STREET_PLAINS": 0.1, "STREET_SAVANNA": 0.2, "STREET_SNOWY_OR_TAIGA": 0.2}

# A village is a landmark the player walks through, so prefer the compact
# end of what the generator produces: 9-20 buildings within 150 blocks.
MIN_HOUSES, MAX_HOUSES, MAX_EXTENT = 9, 20, 150


def piece_kind(location):
    parts = location.split("/")
    if len(parts) >= 3 and parts[1] == "common":
        return "well" if "well" in parts[-1] else "decor"
    group = parts[2] if len(parts) >= 4 else "decor"
    return {"town_centers": "center", "streets": "street", "houses": "house",
            "terminators": "street"}.get(group, "decor")


class TemplateSource:
    def __init__(self, jar_path):
        self.jar = zipfile.ZipFile(jar_path)
        self.cache = {}

    def __call__(self, location):
        location = location.removeprefix("minecraft:")
        if location not in self.cache:
            raw = self.jar.read(f"data/minecraft/structures/{location}.nbt")
            if raw[:2] == b"\x1f\x8b":
                raw = gzip.decompress(raw)
            r = cn.Reader(raw)
            r.u8()
            r.string()
            self.cache[location] = jg.Template(location, cn.parse_payload(r, 10))
        return self.cache[location]


def parse_state(text):
    """'minecraft:oak_stairs[facing=east,half=bottom]' -> (name, props)."""
    if "[" not in text:
        return text, {}
    name, rest = text.split("[", 1)
    props = {}
    for part in rest.rstrip("]").split(","):
        if "=" in part:
            k, v = part.split("=", 1)
            props[k] = v
    return name, props


class Palette:
    def __init__(self):
        self.entries = []
        self.index = {}

    def get(self, block, data):
        key = (block, data)
        if key not in self.index:
            self.index[key] = len(self.entries)
            self.entries.append([block, data])
        return self.index[key]


def convert_piece(element, rotation, palette, report):
    """Rotated, mapped cells of one piece, relative to its rotated box min."""
    template = element.template
    box = template.bbox(rotation, (0, 0, 0))
    rnd = random.Random(f"{element.location}@{rotation}")
    path_to_grass = PATH_TO_GRASS.get(element.processors, 0.0)
    cells = {}
    chests = []
    for pos, name, props, nbt in template.blocks:
        if name == "minecraft:jigsaw":
            name, props = parse_state(nbt.get("final_state", "minecraft:air"))
        if name in ("minecraft:air", "minecraft:cave_air", "minecraft:structure_void"):
            continue
        if name == "minecraft:grass_path" and path_to_grass and rnd.random() < path_to_grass:
            name, props = "minecraft:grass_block", {}
        props = jg.rotate_props(props, rotation)
        block, data = vb.map_block(name, props, report)
        if block in (cn.VOID, "AirBlock", "__spawner__"):
            continue
        x, y, z = jg.transform(*pos, rotation)
        key = (x - box.x0, y - box.y0, z - box.z0)
        cells[key] = palette.get(block, data)
        if block == "ChestBlock":
            table = (nbt.get("LootTable") or "").removeprefix("minecraft:chests/village/")
            chests.append([*key, vb.loot_pool(table)])
    sx, sy, sz = box.x1 - box.x0 + 1, box.y1 - box.y0 + 1, box.z1 - box.z0 + 1
    ordered = sorted(cells.items(), key=lambda kv: (kv[0][1], kv[0][2], kv[0][0]))
    return {
        "size": [sx, sy, sz],
        "cells": [[x, y, z, p] for (x, y, z), p in ordered],
        "chests": sorted(chests, key=lambda c: (c[1], c[2], c[0])),
    }


def layout_ok(pieces):
    houses = sum(1 for p in pieces if p.element.location and piece_kind(p.element.location) == "house")
    xs = [p.box.x0 for p in pieces] + [p.box.x1 for p in pieces]
    zs = [p.box.z0 for p in pieces] + [p.box.z1 for p in pieces]
    extent = max(max(xs) - min(xs), max(zs) - min(zs)) + 1
    return MIN_HOUSES <= houses <= MAX_HOUSES and extent <= MAX_EXTENT, houses, extent


def build_style(style, assembler, source, count, preview_dir):
    palette = Palette()
    pieces_out = []
    piece_refs = {}
    layouts = []
    report = {}
    seed = 0
    centres = Counter()
    while len(layouts) < count and seed < 4000:
        pieces, (cx, cz) = assembler.assemble(f"village/{style}/town_centers", seed)
        seed += 1
        ok, houses, extent = layout_ok(pieces)
        if not ok:
            continue
        centre = pieces[0].element.location
        # Spread the kept layouts over the town centres the style has.
        if centres[centre] >= max(2, count // 2):
            continue
        centres[centre] += 1
        index_map = {}
        out = []
        for i, piece in enumerate(pieces):
            element = piece.element
            if element.template is None:
                continue            # features (trees, flowers, hay piles) are left to the land
            key = f"{element.location}@{piece.rotation}"
            if key not in piece_refs:
                converted = convert_piece(element, piece.rotation, palette, report)
                converted.update({"name": key, "kind": piece_kind(element.location), "rigid": element.rigid})
                piece_refs[key] = len(pieces_out)
                pieces_out.append(converted)
            anchor = dict(piece.anchor)
            if "parent" in anchor:
                if anchor["parent"] not in index_map:
                    continue        # parent was a feature; cannot happen for template children
                anchor["parent"] = index_map[anchor["parent"]]
            else:
                anchor["x"] -= cx
                anchor["z"] -= cz
            index_map[i] = len(out)
            out.append({"ref": piece_refs[key], "x": piece.box.x0 - cx, "z": piece.box.z0 - cz,
                        "floor": piece.box.y0 - assembler.ground, "anchor": anchor})
        layouts.append({"seed": seed - 1, "houses": houses, "extent": extent, "pieces": out})
        if preview_dir:
            render_preview(os.path.join(preview_dir, f"{style}_{len(layouts):02d}.png"),
                           out, pieces_out, palette)
    data = {
        "style": style,
        "palette": palette.entries,
        "bridge": list(vb.map_block("minecraft:" + BRIDGE[style], {}, {})),
        "pieces": pieces_out,
        "layouts": layouts,
    }
    cells = sum(len(p["cells"]) for p in pieces_out)
    print(f"{style}: {len(layouts)} layouts (tried {seed}), {len(pieces_out)} pieces, {cells} cells, "
          f"houses {[l['houses'] for l in layouts]}, extents {[l['extent'] for l in layouts]}")
    if report:
        top = sorted(report.items(), key=lambda kv: -kv[1])[:12]
        print("  unmapped -> air: " + ", ".join(f"{k.removeprefix('minecraft:')}x{v}" for k, v in top))
    return data


# ------------------------------------------------------------------ preview

PREVIEW_COLORS = {
    "GravelBlock": (150, 140, 120), "GrassBlock": (95, 160, 70), "DirtBlock": (120, 85, 60),
    "PlanksBlock": (180, 140, 90), "CobblestoneBlock": (120, 120, 120), "SandstoneBlock": (220, 200, 140),
    "SandBlock": (230, 215, 160), "WaterBlock": (60, 100, 220), "SnowBlock": (240, 240, 250),
    "GlassBlock": (200, 230, 240), "WindowBlock": (200, 230, 240), "SoilBlock": (90, 60, 40),
    "BricksBlock": (170, 90, 70), "StoneBrickBlock": (140, 140, 140), "WoodenFenceBlock": (150, 110, 70),
}


def colour(block):
    if block in PREVIEW_COLORS:
        return PREVIEW_COLORS[block]
    if "Wood" in block or "Planks" in block or "Wooden" in block:
        return (160, 120, 75)
    if "Stairs" in block or "Slab" in block:
        return (150, 130, 110)
    if "Leaves" in block or "Grass" in block or "Rye" in block:
        return (80, 150, 60)
    return (200, 80, 200)


def render_preview(path, layout_pieces, pieces, palette):
    from PIL import Image, ImageDraw
    minx = min(p["x"] for p in layout_pieces)
    minz = min(p["z"] for p in layout_pieces)
    maxx = max(p["x"] + pieces[p["ref"]]["size"][0] for p in layout_pieces)
    maxz = max(p["z"] + pieces[p["ref"]]["size"][2] for p in layout_pieces)
    scale = 4
    img = Image.new("RGB", ((maxx - minx + 2) * scale, (maxz - minz + 2) * scale), (40, 60, 40))
    top = {}
    for lp in layout_pieces:
        piece = pieces[lp["ref"]]
        floor = lp["floor"] if piece["rigid"] else 0
        for x, y, z, p in piece["cells"]:
            key = (lp["x"] + x, lp["z"] + z)
            h = floor + y
            if key not in top or h >= top[key][0]:
                top[key] = (h, palette.entries[p][0])
    draw = ImageDraw.Draw(img)
    for (x, z), (_, block) in top.items():
        px, pz = (x - minx + 1) * scale, (z - minz + 1) * scale
        draw.rectangle([px, pz, px + scale - 1, pz + scale - 1], fill=colour(block))
    for lp in layout_pieces:
        piece = pieces[lp["ref"]]
        if piece["kind"] in ("house", "center"):
            px, pz = (lp["x"] - minx + 1) * scale, (lp["z"] - minz + 1) * scale
            draw.rectangle([px, pz, px + piece["size"][0] * scale - 1, pz + piece["size"][2] * scale - 1],
                           outline=(255, 255, 0) if piece["kind"] == "center" else (255, 255, 255))
    img.save(path)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    jar_path, out_dir = args[0], args[1]
    count = 8
    preview_dir = None
    for i, a in enumerate(sys.argv):
        if a == "--layouts":
            count = int(sys.argv[i + 1])
        if a == "--preview":
            preview_dir = sys.argv[i + 1]
    if preview_dir:
        os.makedirs(preview_dir, exist_ok=True)
        args = [a for a in args if a not in (str(count), preview_dir)]
    pools = json.load(open(os.path.join(HERE, "pools.json"), encoding="utf-8"))
    source = TemplateSource(jar_path)
    assembler = jg.Assembler(pools, source)
    os.makedirs(out_dir, exist_ok=True)
    for style in STYLES:
        data = build_style(style, assembler, source, count, preview_dir)
        path = os.path.join(out_dir, f"village_{style}.json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump(data, f, separators=(",", ":"))
        print(f"  -> {path} ({os.path.getsize(path) // 1024} KB)")


if __name__ == "__main__":
    main()

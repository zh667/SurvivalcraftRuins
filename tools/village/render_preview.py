#!/usr/bin/env python3
"""Render village dumps (from VillagePreviewTests) as top-down and isometric PNGs.

Usage: render_preview.py <dump-dir> <out-dir> [<Content.zip>]

With Survivalcraft's Content.zip, block colours come from its texture atlas
and palette (painted blocks included), as in compare_colors.py.

The dumps hold terrain from the game's first two generation passes plus the
mod's village, so the natural ground is bare rock there; it is tinted by the
column's climate (grass, sand, snow) to read like the finished world.
"""
import json
import os
import struct
import sys

from PIL import Image, ImageDraw

COLOURS = {
    "GravelBlock": (150, 142, 128), "PlanksBlock": (184, 146, 92), "CobblestoneBlock": (128, 128, 128),
    "WaterBlock": (55, 95, 205), "GlassBlock": (200, 228, 238), "WindowBlock": (200, 228, 238),
    "SandstoneBlock": (222, 204, 150), "SandBlock": (228, 214, 160), "DirtBlock": (122, 88, 58),
    "GrassBlock": (96, 158, 72), "SoilBlock": (92, 62, 42), "RyeBlock": (206, 190, 84),
    "BricksBlock": (168, 92, 72), "SnowBlock": (240, 242, 250), "MarbleBlock": (236, 236, 240),
    "WoodenStairsBlock": (160, 122, 78), "WoodenSlabBlock": (176, 140, 90), "OakWoodBlock": (110, 86, 54),
    "SpruceWoodBlock": (82, 62, 40), "BirchWoodBlock": (200, 196, 180), "MimosaWoodBlock": (150, 100, 70),
    "WoodenFenceBlock": (150, 112, 72), "WoodenDoorBlock": (140, 100, 60), "WoodenTrapdoorBlock": (140, 104, 64),
    "TorchBlock": (255, 210, 90), "WickerLampBlock": (255, 200, 110), "CarpetBlock": (200, 60, 60),
    "CraftingTableBlock": (150, 100, 60), "FurnaceBlock": (100, 100, 100), "ChestBlock": (170, 120, 60),
    "CobblestoneStairsBlock": (120, 120, 120), "StoneStairsBlock": (125, 125, 125), "StoneSlabBlock": (140, 140, 140),
    "SandstoneStairsBlock": (210, 192, 140), "SandstoneSlabBlock": (214, 196, 144), "StoneBrickBlock": (140, 140, 140),
    "OakLeavesBlock": (60, 120, 50), "PumpkinBlock": (220, 130, 40), "CactusBlock": (70, 130, 60),
    "TallGrassBlock": (90, 150, 70), "RedFlowerBlock": (200, 50, 50), "WhiteFlowerBlock": (230, 230, 230),
    "PurpleFlowerBlock": (150, 80, 180), "WoodenLadderBlock": (160, 120, 80), "IceBlock": (170, 200, 240),
    "IronFenceBlock": (90, 90, 90), "StoneFenceBlock": (120, 120, 120), "WoodenFenceGateBlock": (150, 112, 72),
    "ButtonBlock": (130, 130, 130), "PressurePlateBlock": (150, 130, 100), "WoodenAttachedSignBlock": (170, 130, 80),
    "WoodenPostedSignBlock": (170, 130, 80), "GraniteBlock": (115, 112, 108), "BasaltBlock": (70, 70, 74),
}
NATURAL = {3, 4, 67}          # granite, sandstone, basalt from the generator's first passes


def load(path):
    with open(path, "rb") as f:
        w, h, d, x0, y0, z0, ocean = struct.unpack("<7i", f.read(28))
        cols = {}
        climate = {}
        raw = f.read()
    stride = 2 + h * 6
    for x in range(w):
        for z in range(d):
            o = (x * d + z) * stride
            climate[(x, z)] = (raw[o], raw[o + 1])
            column = []
            for y in range(h):
                c, data = struct.unpack_from("<Hi", raw, o + 2 + y * 6)
                column.append((c, data))
            cols[(x, z)] = column
    return w, h, d, y0, ocean, cols, climate


def natural_colour(t, hum, depth_from_top):
    if depth_from_top > 0:
        return (120, 104, 84)
    if t <= 1:
        return (236, 238, 246)
    if t >= 9 and hum <= 7:
        return (226, 210, 156)
    if hum <= 6:
        return (150, 160, 80)
    return (92, 152, 70)


SC = None


def block_colour(names, cell, t, hum, depth):
    contents, data = cell
    if contents in NATURAL:
        return natural_colour(t, hum, depth)
    name = names.get(str(contents), "")
    if SC is not None:
        return tuple(int(v) for v in SC.colour(name, data))
    return COLOURS.get(name, (200, 90, 200))


def top_down(path, w, h, d, y0, cols, climate, names):
    scale = 4
    img = Image.new("RGB", (w * scale, d * scale))
    px = img.load()
    heights = {}
    for (x, z), column in cols.items():
        top = 0
        for y in range(h - 1, -1, -1):
            if column[y][0] != 0:
                top = y
                break
        heights[(x, z)] = top
    for (x, z), column in cols.items():
        top = heights[(x, z)]
        t, hum = climate[(x, z)]
        r, g, b = block_colour(names, column[top], t, hum, 0)
        east = heights.get((x + 1, z), top)
        north = heights.get((x, z - 1), top)
        shade = 1.0 + 0.06 * (top - east) + 0.04 * (top - north)
        shade *= 0.8 + 0.4 * top / max(1, h)
        colour = tuple(max(0, min(255, int(c * shade))) for c in (r, g, b))
        for sx in range(scale):
            for sz in range(scale):
                px[x * scale + sx, z * scale + sz] = colour
    img.save(path)


def isometric(path, w, h, d, y0, cols, climate, names):
    a, b, c = 4, 2, 5                 # half-width, half-depth, block height in pixels
    width = (w + d) * a + 4
    height = (w + d) * b + h * c + 4
    img = Image.new("RGB", (width, height), (24, 28, 34))
    draw = ImageDraw.Draw(img)

    def solid(x, y, z):
        if x < 0 or z < 0 or x >= w or z >= d or y < 0 or y >= h:
            return False
        return cols[(x, z)][y][0] != 0

    tops = {}
    for (x, z), column in cols.items():
        for y in range(h - 1, -1, -1):
            if column[y][0] != 0:
                tops[(x, z)] = y
                break
    # Painter's order: back to front (x + z), bottom to top.
    for s in range(w + d - 1):
        for x in range(max(0, s - d + 1), min(w, s + 1)):
            z = s - x
            column = cols[(x, z)]
            t, hum = climate[(x, z)]
            top = tops.get((x, z), 0)
            for y in range(h):
                contents = column[y][0]
                if contents == 0:
                    continue
                up = solid(x, y + 1, z)
                front_right = solid(x + 1, y, z)
                front_left = solid(x, y, z + 1)
                if up and front_left and front_right:
                    continue
                r, g, bl = block_colour(names, column[y], t, hum, top - y if contents in NATURAL else 0)
                sx = (x - z) * a + d * a
                sy = (x + z) * b + (h - y) * c
                topface = [(sx, sy), (sx + a, sy + b), (sx, sy + 2 * b), (sx - a, sy + b)]
                left = [(sx - a, sy + b), (sx, sy + 2 * b), (sx, sy + 2 * b + c), (sx - a, sy + b + c)]
                right = [(sx + a, sy + b), (sx, sy + 2 * b), (sx, sy + 2 * b + c), (sx + a, sy + b + c)]
                if not front_left:
                    draw.polygon(left, fill=(int(r * 0.72), int(g * 0.72), int(bl * 0.72)))
                if not front_right:
                    draw.polygon(right, fill=(int(r * 0.58), int(g * 0.58), int(bl * 0.58)))
                if not up:
                    draw.polygon(topface, fill=(r, g, bl))
    img = img.crop(img.getbbox())
    img.save(path)


def main():
    global SC
    dump_dir, out_dir = sys.argv[1], sys.argv[2]
    if len(sys.argv) > 3:
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        import compare_colors
        SC = compare_colors.Sc(sys.argv[3])
    os.makedirs(out_dir, exist_ok=True)
    names = json.load(open(os.path.join(dump_dir, "blocks.json")))
    for name in sorted(os.listdir(dump_dir)):
        if not name.endswith(".bin"):
            continue
        w, h, d, y0, ocean, cols, climate = load(os.path.join(dump_dir, name))
        base = name[:-4]
        top_down(os.path.join(out_dir, base + "_top.png"), w, h, d, y0, cols, climate, names)
        isometric(os.path.join(out_dir, base + "_iso.png"), w, h, d, y0, cols, climate, names)
        print("rendered", base)


if __name__ == "__main__":
    main()

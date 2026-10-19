#!/usr/bin/env python3
"""Compare village pieces block for block: Minecraft original vs Survivalcraft conversion.

Usage:
  compare_colors.py <client.jar> <Content.zip> --table
      Average texture colour of each Minecraft village block against every
      Survivalcraft option (paintable blocks with each default palette colour),
      nearest first. This is what the colour choices in village_blocks.py rest on.
  compare_colors.py <client.jar> <Content.zip> --sheets <out-dir> [--style plains]
      For every building of every style: the Minecraft piece drawn with
      Minecraft's own texture colours, next to the converted piece drawn with
      Survivalcraft's, isometric, so shape and colour can be checked by eye.

Colours are texture averages (alpha-weighted); grass and leaves, which
Minecraft tints by biome, get the plains tint. Survivalcraft painted blocks
use the block's paintable texture multiplied by the palette colour, which is
how the game shades them.
"""
import io
import json
import math
import os
import sys
import zipfile

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))

import build_villages as bv     # noqa: E402
import jigsaw as jg             # noqa: E402

PALETTE = [(255, 255, 255), (181, 255, 255), (255, 181, 255), (160, 181, 255), (255, 240, 160), (181, 255, 181),
           (255, 181, 160), (181, 181, 181), (112, 112, 112), (32, 112, 112), (112, 32, 112), (26, 52, 128),
           (87, 54, 31), (24, 116, 24), (136, 32, 32), (24, 24, 24)]

# Paintable texture slot per material (the constructor argument of each
# PaintedCubeBlock / stairs / slab / fence class).
PAINTED_SLOT = {"Planks": 23, "Bricks": 39, "Clay": 15, "Cobblestone": 69, "Granite": 24, "Marble": 51,
                "Sandstone": 64, "StoneBrick": 50, "Basalt": 40}
MATERIAL_OF = {
    "PlanksBlock": "Planks", "WoodenStairsBlock": "Planks", "WoodenSlabBlock": "Planks", "WoodenFenceBlock": "Planks",
    "WoodenFenceGateBlock": "Planks", "BricksBlock": "Bricks", "BrickStairsBlock": "Bricks", "BrickSlabBlock": "Bricks",
    "ClayBlock": "Clay", "CobblestoneBlock": "Cobblestone", "CobblestoneStairsBlock": "Cobblestone",
    "CobblestoneSlabBlock": "Cobblestone", "GraniteBlock": "Granite", "StoneStairsBlock": "Granite",
    "StoneSlabBlock": "Granite", "StoneFenceBlock": "Granite", "MarbleBlock": "Marble", "MarbleStairsBlock": "Marble",
    "MarbleSlabBlock": "Marble", "SandstoneBlock": "Sandstone", "SandstoneStairsBlock": "Sandstone",
    "SandstoneSlabBlock": "Sandstone", "StoneBrickBlock": "StoneBrick", "StoneBrickStairsBlock": "StoneBrick",
    "StoneBrickSlabBlock": "StoneBrick", "BasaltBlock": "Basalt",
}
MATERIAL_BLOCK = {"Planks": "PlanksBlock", "Bricks": "BricksBlock", "Clay": "ClayBlock", "Cobblestone": "CobblestoneBlock",
                  "Granite": "GraniteBlock", "Marble": "MarbleBlock", "Sandstone": "SandstoneBlock",
                  "StoneBrick": "StoneBrickBlock", "Basalt": "BasaltBlock"}
SLOT_OVERRIDE = {"OakWoodBlock": 20, "SpruceWoodBlock": 116, "BirchWoodBlock": 117, "MimosaWoodBlock": 116,
                 "PoplarWoodBlock": 109}
FIXED = {"GrassBlock": (96, 158, 72), "OakLeavesBlock": (70, 130, 45), "SpruceLeavesBlock": (55, 100, 50),
         "TallGrassBlock": (90, 150, 60), "WaterBlock": (55, 95, 205), "RedFlowerBlock": (200, 50, 50),
         "WhiteFlowerBlock": (235, 235, 235), "PurpleFlowerBlock": (150, 80, 180), "CampfireBlock": (210, 120, 40),
         "ChestBlock": (160, 112, 50)}
MC_TINT = {"grass_block_top": (145, 189, 89), "oak_leaves": (119, 171, 47), "spruce_leaves": (97, 153, 97),
           "grass": (145, 189, 89), "tall_grass_top": (145, 189, 89), "fern": (145, 189, 89),
           "large_fern_top": (145, 189, 89)}
WOODS = ("oak", "spruce", "birch", "jungle", "acacia", "dark_oak")


def avg(img):
    px = [p for p in (img.get_flattened_data() if hasattr(img, "get_flattened_data") else img.getdata()) if p[3] > 128]
    if not px:
        return None
    return tuple(sum(p[i] for p in px) / len(px) for i in range(3))


def dist(a, b):
    r = (a[0] + b[0]) / 2
    return math.sqrt((2 + r / 256) * (a[0] - b[0]) ** 2 + 4 * (a[1] - b[1]) ** 2 + (2 + (255 - r) / 256) * (a[2] - b[2]) ** 2)


class Sc:
    def __init__(self, content_zip):
        z = zipfile.ZipFile(content_zip)
        self.atlas = Image.open(io.BytesIO(z.read("Assets/Textures/Blocks.webp"))).convert("RGBA")
        self.px = self.atlas.size[0] // 16
        rows = z.read("Assets/BlocksData.txt").decode("utf-8-sig").splitlines()
        col = rows[0].split(";").index("DefaultTextureSlot")
        self.default_slot = {}
        for r in rows[2:]:
            c = r.split(";")
            if len(c) > col and c[col].strip().lstrip("-").isdigit() and int(c[col]) >= 0:
                self.default_slot[c[0]] = int(c[col])
        self.cache = {}

    def slot(self, i):
        if i not in self.cache:
            x, y = (i % 16) * self.px, (i // 16) * self.px
            self.cache[i] = avg(self.atlas.crop((x, y, x + self.px, y + self.px))) or (200, 0, 200)
        return self.cache[i]

    def painted(self, material, colour):
        base = self.slot(PAINTED_SLOT[material])
        return tuple(b * c / 255 for b, c in zip(base, PALETTE[colour]))

    def colour(self, block, data):
        if block in FIXED:
            return FIXED[block]
        if block == "CarpetBlock":
            return self.painted("Clay", data & 15)
        material = MATERIAL_OF.get(block)
        if material:
            paint = None
            if block.endswith("StairsBlock") and data & 0x20:
                paint = (data >> 6) & 15
            elif block.endswith("SlabBlock") and data & 2:
                paint = (data >> 2) & 15
            elif ("Fence" in block) and data & 0x10:
                paint = (data >> 5) & 15
            elif block == MATERIAL_BLOCK[material] and data & 1:
                paint = (data >> 1) & 15
            if paint is not None:
                return self.painted(material, paint)
            return self.slot(self.default_slot.get(MATERIAL_BLOCK[material], 4))
        if block in SLOT_OVERRIDE:
            return self.slot(SLOT_OVERRIDE[block])
        if block in ("WoodenDoorBlock", "WoodenTrapdoorBlock", "WoodenLadderBlock", "WoodenAttachedSignBlock",
                     "WoodenPostedSignBlock", "CraftingTableBlock"):
            return self.slot(self.default_slot.get(block, 4))
        if block in self.default_slot:
            return self.slot(self.default_slot[block])
        return (200, 80, 200)


class Mc:
    def __init__(self, jar):
        self.jar = zipfile.ZipFile(jar)
        self.names = set(self.jar.namelist())
        self.cache = {}

    def texture(self, tex):
        path = f"assets/minecraft/textures/block/{tex}.png"
        if path not in self.names:
            return None
        if tex not in self.cache:
            img = Image.open(io.BytesIO(self.jar.read(path))).convert("RGBA").crop((0, 0, 16, 16))
            c = avg(img)
            if c is not None and tex in MC_TINT:
                c = tuple(v * t / 255 for v, t in zip(c, MC_TINT[tex]))
            self.cache[tex] = c
        return self.cache[tex]

    def candidates(self, n):
        out = []
        for wood in WOODS:
            if n.startswith(wood + "_") and n.split(wood + "_", 1)[1] in (
                    "stairs", "slab", "fence", "fence_gate", "pressure_plate", "button", "sign", "wall_sign"):
                out.append(wood + "_planks")
            if n == f"{wood}_door":
                out.append(f"{wood}_door_top")
            if n in (f"{wood}_wood", f"stripped_{wood}_wood"):
                out.append(n.replace("_wood", "_log"))
        for suffix in ("_stairs", "_slab", "_wall"):
            if n.endswith(suffix):
                base = n.removesuffix(suffix)
                out += [base, base + "s", base + "_top", base.replace("smooth_", "") + "_top"]
        for colour_suffix in ("_bed", "_carpet", "_wall_banner", "_banner"):
            if n.endswith(colour_suffix):
                out.append(n.removesuffix(colour_suffix) + "_wool")
        if n.endswith("_stained_glass_pane"):
            out.append(n.removesuffix("_pane"))
        special = {"glass_pane": "glass", "wall_torch": "torch", "grass_path": "grass_path_top",
                   "grass_block": "grass_block_top", "wheat": "wheat_stage7", "snow": "snow",
                   "hay_block": "hay_block_side", "furnace": "furnace_front", "smoker": "smoker_front",
                   "blast_furnace": "blast_furnace_front", "crafting_table": "crafting_table_front",
                   "barrel": "barrel_side", "composter": "composter_side", "cauldron": "cauldron_side",
                   "smooth_sandstone": "sandstone_top", "melon_stem": "melon_stem", "pumpkin_stem": "pumpkin_stem",
                   "tall_grass": "tall_grass_top", "large_fern": "large_fern_top", "lectern": "lectern_sides",
                   "loom": "loom_front", "cartography_table": "cartography_table_side1",
                   "fletching_table": "fletching_table_front", "smithing_table": "smithing_table_front",
                   "grindstone": "grindstone_side", "stonecutter": "stonecutter_side", "bell": "bell_top",
                   "pumpkin": "pumpkin_side", "melon": "melon_side", "cactus": "cactus_side"}
        if n in special:
            out.insert(0, special[n])
        if n.startswith("potted_"):
            out.append("flower_pot")
        out += [n, n + "_side", n + "_top", n + "_front"]
        return out

    def colour(self, name):
        n = name.removeprefix("minecraft:")
        if n == "water":
            return (63, 118, 228)
        if n == "lava":
            return (207, 92, 15)
        if n in ("chest", "trapped_chest"):
            return (162, 115, 48)
        for tex in self.candidates(n):
            c = self.texture(tex)
            if c is not None:
                return c
        return (255, 0, 255)


def iso(cells, size, scale=(5, 2, 6)):
    """cells: {(x,y,z): rgb}. Draws an isometric picture, back to front."""
    a, b, c = scale
    sx, sy, sz = size
    w = (sx + sz) * a + 4
    h = (sx + sz) * b + sy * c + 4
    img = Image.new("RGB", (w, h), (34, 38, 46))
    draw = ImageDraw.Draw(img)
    for s in range(sx + sz - 1):
        for x in range(max(0, s - sz + 1), min(sx, s + 1)):
            z = s - x
            for y in range(sy):
                col = cells.get((x, y, z))
                if col is None:
                    continue
                if (x, y + 1, z) in cells and (x + 1, y, z) in cells and (x, y, z + 1) in cells:
                    continue
                r, g, bl = (int(v) for v in col)
                px = (x - z) * a + sz * a + 2
                py = (x + z) * b + (sy - y) * c
                top = [(px, py), (px + a, py + b), (px, py + 2 * b), (px - a, py + b)]
                left = [(px - a, py + b), (px, py + 2 * b), (px, py + 2 * b + c), (px - a, py + b + c)]
                right = [(px + a, py + b), (px, py + 2 * b), (px, py + 2 * b + c), (px + a, py + b + c)]
                if (x, y, z + 1) not in cells:
                    draw.polygon(left, fill=(int(r * 0.72), int(g * 0.72), int(bl * 0.72)))
                if (x + 1, y, z) not in cells:
                    draw.polygon(right, fill=(int(r * 0.56), int(g * 0.56), int(bl * 0.56)))
                if (x, y + 1, z) not in cells:
                    draw.polygon(top, fill=(r, g, bl))
    return img


def piece_pair(location, source, mc, sc, palette_cache):
    template = source(location)
    blocks = bv.final_blocks(template)
    mc_cells = {}
    for pos, (name, props, nbt) in blocks.items():
        if name in bv.EMPTY:
            continue
        mc_cells[pos] = mc.colour(name)
    element = jg.Element({"kind": "legacy", "location": location, "processors": "EMPTY"}, "rigid", source)
    palette = bv.Palette()
    converted = bv.convert_piece(element, "none", palette, {}, add_chest=bv.piece_kind(location) == "house")
    sc_cells = {}
    lost = 0
    for x, y, z, p in converted["cells"]:
        block, data = palette.entries[p]
        sc_cells[(x, y, z)] = sc.colour(block, data)
    for pos in mc_cells:
        if pos not in sc_cells:
            lost += 1
    size = template.size
    return iso(mc_cells, size), iso(sc_cells, size), lost, len(mc_cells), len(converted["chests"])


def sheets(jar, content, out_dir, only_style=None):
    os.makedirs(out_dir, exist_ok=True)
    source = bv.TemplateSource(jar)
    mc, sc = Mc(jar), Sc(content)
    pools = json.load(open(os.path.join(HERE, "pools.json"), encoding="utf-8"))
    for style in bv.STYLES:
        if only_style and style != only_style:
            continue
        locations = []
        for pool in (f"village/{style}/town_centers", f"village/{style}/houses"):
            for e in pools[pool]["elements"]:
                if e.get("location") and e["location"] not in locations:
                    locations.append(e["location"])
        tiles = []
        for loc in locations:
            a, b, lost, total, chests = piece_pair(loc, source, mc, sc, None)
            label = f"{loc.rsplit('/', 1)[-1]}: {total} blocks, {lost} lost, {chests} chest"
            tiles.append((label, a, b))
        cols = 2
        tile_w = max(max(a.size[0] + b.size[0] for _, a, b in tiles), 330) + 30
        tile_h = max(max(a.size[1], b.size[1]) for _, a, b in tiles) + 26
        rows = (len(tiles) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * tile_w, rows * tile_h + 30), (24, 26, 32))
        draw = ImageDraw.Draw(sheet)
        draw.text((8, 8), f"{style}: left = Minecraft 1.16.5 original, right = Survivalcraft conversion", fill=(230, 230, 230))
        for i, (label, a, b) in enumerate(tiles):
            ox, oy = (i % cols) * tile_w + 8, (i // cols) * tile_h + 30
            draw.text((ox, oy), label, fill=(210, 210, 210))
            sheet.paste(a, (ox, oy + 16))
            sheet.paste(b, (ox + a.size[0] + 14, oy + 16))
        path = os.path.join(out_dir, f"compare_{style}.png")
        sheet.save(path)
        print("wrote", path, f"{len(tiles)} pieces")


def table(jar, content):
    mc, sc = Mc(jar), Sc(content)
    options = []
    for material, slot in PAINTED_SLOT.items():
        block = MATERIAL_BLOCK[material]
        options.append((block, "unpainted", sc.slot(sc.default_slot.get(block, slot))))
        for i in range(16):
            options.append((block, f"paint {i}", sc.painted(material, i)))
    for name in ["oak_planks", "spruce_planks", "acacia_planks", "terracotta", "white_terracotta", "yellow_terracotta",
                 "orange_terracotta", "red_terracotta", "white_wool", "yellow_wool", "hay_block", "diorite",
                 "smooth_stone", "smooth_sandstone", "cut_sandstone", "snow_block", "packed_ice", "blue_ice"]:
        m = mc.colour("minecraft:" + name)
        best = sorted(options, key=lambda o: dist(m, o[2]))[:3]
        print(f"{name:20s} MC {tuple(int(v) for v in m)}: " + "; ".join(
            f"{b} {how} {tuple(int(v) for v in c)} d={dist(m, c):.0f}" for b, how, c in best))


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    jar, content = args[0], args[1]
    if "--table" in sys.argv:
        table(jar, content)
    if "--sheets" in sys.argv:
        out = sys.argv[sys.argv.index("--sheets") + 1]
        style = sys.argv[sys.argv.index("--style") + 1] if "--style" in sys.argv else None
        sheets(jar, content, out, style)


if __name__ == "__main__":
    main()

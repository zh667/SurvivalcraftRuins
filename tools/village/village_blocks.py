"""Minecraft -> Survivalcraft block mapping for village pieces.

Extends convert_nbt.map_block with the blocks villages use that the dungeon
templates never needed (paths, crops, doors, beds, work stations), and uses
facing tables checked against Survivalcraft's own block code. Anything not
handled here falls through to convert_nbt.

Facing (verified in StairsBlock collision boxes, SubsystemLadderBlockBehavior,
SubsystemTorchBlockBehavior, WoodBlock): Survivalcraft numbers faces
0 = +Z (south), 1 = +X (east), 2 = -Z (north), 3 = -X (west). Attached blocks
(ladders, wall torches, signs, buttons, trapdoors) store the face they look
out of: FACE. Stairs, gates and doors store the placing player's look
direction: LOOK. The tables are convert_nbt's (fixed in 1.1).
"""
import convert_nbt as cn

# Minecraft chest loot tables -> pools.json names.
LOOT = {
    "village_armorer": "village_armorer",
    "village_butcher": "village_butcher",
    "village_cartographer": "village_cartographer",
    "village_fisher": "village_fisher",
    "village_fletcher": "village_fletcher",
    "village_mason": "village_mason",
    "village_shepherd": "village_shepherd",
    "village_tannery": "village_tannery",
    "village_temple": "village_temple",
    "village_toolsmith": "village_toolsmith",
    "village_weaponsmith": "village_weaponsmith",
    "village_plains_house": "village_house",
    "village_desert_house": "village_house",
    "village_savanna_house": "village_house",
    "village_snowy_house": "village_house",
    "village_taiga_house": "village_house",
}


def loot_pool(table):
    return LOOT.get(table, "village_house")


# Pool for a chest the converter adds to a house that has none, by the
# profession its Minecraft piece is named after.
PROFESSIONS = [
    ("armorer", "village_armorer"), ("butcher", "village_butcher"), ("cartographer", "village_cartographer"),
    ("fisher", "village_fisher"), ("fletcher", "village_fletcher"), ("mason", "village_mason"),
    ("shepherd", "village_shepherd"), ("tannery", "village_tannery"), ("temple", "village_temple"),
    ("tool_smith", "village_toolsmith"), ("toolsmith", "village_toolsmith"),
    ("weaponsmith", "village_weaponsmith"), ("weapon_smith", "village_weaponsmith"),
]


def house_pool(location):
    name = location.rsplit("/", 1)[-1]
    for key, pool in PROFESSIONS:
        if key in name:
            return pool
    return "village_house"


# Facing tables live in convert_nbt (see the note there).
FACE = cn.FACE
LOOK = cn.LOOK

# Nearest SC fabric colour (CIELAB dE76 against SubsystemPalette fabric colours) per MC dye
SC_COLOR = {
    "white": 0, "orange": 6, "magenta": 10, "light_blue": 3, "yellow": 4, "lime": 13,
    "pink": 2, "gray": 8, "light_gray": 7, "cyan": 9, "purple": 10, "blue": 11,
    "brown": 12, "green": 13, "red": 14, "black": 15,
}

WOOD_AXIS = cn.WOOD_AXIS
SAPLING = {"oak": 0, "birch": 1, "spruce": 2, "acacia": 4, "jungle": 0, "dark_oak": 0}
STONE_PLATES = {"stone_pressure_plate", "polished_blackstone_pressure_plate",
                "light_weighted_pressure_plate", "heavy_weighted_pressure_plate"}


# Survivalcraft paint bits per block family (PaintedCubeBlock, StairsBlock,
# SlabBlock, FenceBlock/FenceGateBlock .SetColor).
def paint_cube(color):
    return 1 | (color << 1)


def paint_stairs(data, color):
    return data | 0x20 | (color << 6)


def paint_slab(data, color):
    return data | 2 | (color << 2)


def paint_fence(data, color):
    return data | 0x10 | (color << 5)


# Colour choices below were made by comparing average texture colours: each
# Minecraft block's own texture against Survivalcraft's atlas, painted with
# each of the 16 default palette colours where the block takes paint
# (tools/village/compare_colors.py prints the table; docs/village-blocks.md
# has the reasoning). Wood stays unpainted: Survivalcraft planks are already
# as dark as spruce, and the palette has no warm mid tones to do oak or acacia
# better. Where the palette has nothing close (saturated yellows), the hue is
# kept rather than the distance minimised.
TERRACOTTA = {
    "terracotta": ("BricksBlock", 0), "red_terracotta": ("BricksBlock", 0),
    "orange_terracotta": ("BricksBlock", 0), "brown_terracotta": ("BricksBlock", 0),
    "white_terracotta": ("MarbleBlock", paint_cube(6)),
    "yellow_terracotta": ("SandstoneBlock", paint_cube(4)),
    "lime_terracotta": ("ClayBlock", paint_cube(13)),
    "white_glazed_terracotta": ("BricksBlock", paint_cube(1)),
    "light_blue_glazed_terracotta": ("BricksBlock", paint_cube(3)),
    "yellow_glazed_terracotta": ("BricksBlock", paint_cube(4)),
    "lime_glazed_terracotta": ("ClayBlock", paint_cube(5)),
    "orange_glazed_terracotta": ("ClayBlock", 0),
}
PALE_SANDSTONE = 4      # smooth/cut sandstone is lighter than Survivalcraft's
SMOOTH_STONE = 7        # light grey granite


def _map(name, props, report):
    n = name.removeprefix("minecraft:")
    f = props.get("facing", "north")

    # --- colours ---
    if n in TERRACOTTA:
        return TERRACOTTA[n]
    if n.endswith("_wool"):
        colour = n.removesuffix("_wool")
        if colour == "white":
            return ("MarbleBlock", paint_cube(0))
        if colour == "light_gray":
            return ("ClayBlock", 0)
        if colour == "yellow":
            return ("ClayBlock", paint_cube(4))
        return ("ClayBlock", paint_cube(SC_COLOR.get(colour, 0)))
    if n == "hay_block":
        return ("PlanksBlock", paint_cube(4))
    if n in ("diorite", "polished_diorite"):
        return ("MarbleBlock", 0)
    if n in ("diorite_stairs", "polished_diorite_stairs"):
        return ("MarbleStairsBlock", LOOK[f] | (4 if props.get("half") == "top" else 0))
    if n in ("diorite_slab", "polished_diorite_slab"):
        return ("MarbleBlock", 0) if props.get("type") == "double" else ("MarbleSlabBlock", 1 if props.get("type") == "top" else 0)
    if n == "diorite_wall":
        return ("StoneFenceBlock", paint_fence(0, 0))
    if n == "smooth_stone":
        return ("GraniteBlock", paint_cube(SMOOTH_STONE))
    if n == "smooth_stone_slab":
        if props.get("type") == "double":
            return ("GraniteBlock", paint_cube(SMOOTH_STONE))
        return ("StoneSlabBlock", paint_slab(1 if props.get("type") == "top" else 0, SMOOTH_STONE))
    if n in ("smooth_sandstone", "cut_sandstone", "chiseled_sandstone"):
        return ("SandstoneBlock", paint_cube(PALE_SANDSTONE))
    if n in ("smooth_sandstone_stairs", "cut_sandstone_stairs"):
        return ("SandstoneStairsBlock", paint_stairs(LOOK[f] | (4 if props.get("half") == "top" else 0), PALE_SANDSTONE))
    if n in ("smooth_sandstone_slab", "cut_sandstone_slab"):
        if props.get("type") == "double":
            return ("SandstoneBlock", paint_cube(PALE_SANDSTONE))
        return ("SandstoneSlabBlock", paint_slab(1 if props.get("type") == "top" else 0, PALE_SANDSTONE))
    if n == "sandstone_wall":
        return ("StoneFenceBlock", paint_fence(0, PALE_SANDSTONE))
    if n == "packed_ice":
        return ("ClayBlock", paint_cube(3))
    if n == "blue_ice":
        return ("MarbleBlock", paint_cube(3))
    if n == "granite":
        return ("BricksBlock", 0)
    if n == "granite_stairs":
        return ("BrickStairsBlock", LOOK[f] | (4 if props.get("half") == "top" else 0))

    # --- fixed facing tables for blocks the converter already handles ---
    if n.endswith("_stairs"):
        block, _ = cn.map_block(name, props, {})
        return (block, LOOK[f] | (4 if props.get("half") == "top" else 0))
    if n.endswith("_fence_gate"):
        return ("WoodenFenceGateBlock", LOOK[f] | (4 if props.get("open") == "true" else 0))
    if n == "ladder":
        return ("WoodenLadderBlock", FACE[f])
    if n in ("wall_torch", "soul_wall_torch"):
        return ("TorchBlock", FACE[f])
    for kind, sc in cn.WOOD_KINDS.items():
        if n in (f"{kind}_log", f"{kind}_wood", f"stripped_{kind}_log", f"stripped_{kind}_wood"):
            return (sc, WOOD_AXIS.get(props.get("axis", "y"), 0))
    if n in ("furnace", "smoker", "blast_furnace"):
        return ("FurnaceBlock", FACE[f])
    if n in ("chest", "trapped_chest", "barrel"):
        return ("ChestBlock", FACE[f] if f in FACE else 0)

    # --- new mappings ---
    if n in ("grass_path", "dirt_path"):
        return ("GravelBlock", 0)
    if n == "snow":
        return ("ClayBlock", paint_cube(0)) if int(props.get("layers", "1")) >= 8 else ("SnowBlock", 0)
    if n == "snow_block":
        return ("ClayBlock", paint_cube(0))             # white clay: the closest white, and it never melts
    if n == "wheat":
        return ("RyeBlock", int(props.get("age", "0")) & 7)
    if n == "farmland":
        return ("SoilBlock", (1 if props.get("moisture") == "7" else 0) | (3 << 1))
    if n in ("pumpkin_stem", "melon_stem", "attached_pumpkin_stem", "attached_melon_stem"):
        age = int(props.get("age", "7"))
        size = 0 if age == 0 else min(age, 6)
        return ("PumpkinBlock", 7 - size)               # size stored inverted, alive
    if n == "melon":
        return ("PumpkinBlock", 0)                      # full-size pumpkin
    if n == "cactus":
        return ("CactusBlock", 0)
    if n.endswith("_sapling") and n.removesuffix("_sapling") in SAPLING:
        return ("SaplingBlock", SAPLING[n.removesuffix("_sapling")])
    if n.startswith("potted_") or n in ("flower_pot", "sea_pickle"):
        return ("AirBlock", 0)

    if n.endswith("_door"):
        d = LOOK[f] | (4 if props.get("open") == "true" else 0) | (8 if props.get("hinge", "left") == "left" else 0)
        return ("IronDoorBlock" if n == "iron_door" else "WoodenDoorBlock", d)
    if n.endswith("_trapdoor"):
        d = FACE[f] | (4 if props.get("open") == "true" else 0) | (8 if props.get("half") == "top" else 0)
        return ("CellTrapdoorBlock" if n == "iron_trapdoor" else "WoodenTrapdoorBlock", d)

    if n.endswith("_bed"):
        return ("CarpetBlock", SC_COLOR.get(n.removesuffix("_bed"), 0))
    if n.endswith("_carpet"):
        return ("CarpetBlock", SC_COLOR.get(n.removesuffix("_carpet"), 0))

    if n.endswith("_button"):
        face = props.get("face", "wall")
        return ("ButtonBlock", 4 if face == "floor" else 5 if face == "ceiling" else FACE[f])
    if n.endswith("_pressure_plate"):
        return ("PressurePlateBlock", (4 << 1) | (1 if n in STONE_PLATES else 0))

    if n in ("loom", "cartography_table", "fletching_table", "smithing_table"):
        return ("CraftingTableBlock", 0)
    if n == "composter":
        return ("PlanksBlock", 0)
    if n in ("grindstone", "stonecutter"):
        return ("StoneSlabBlock", 0)
    if n == "lectern":
        return ("WoodenPostedSignBlock", 2 * FACE[f])
    if n in ("brewing_stand", "cauldron", "bell"):
        return ("AirBlock", 0)
    if n.endswith("_wall_sign"):
        return ("WoodenAttachedSignBlock", FACE[f])
    if n.endswith("_wall_banner"):
        return ("WoodenAttachedSignBlock", FACE[f] | 4 | (SC_COLOR.get(n.removesuffix("_wall_banner"), 12) << 3))

    return cn.map_block(name, props, report)


def map_block(name, props, report):
    return _map(name, props, report)

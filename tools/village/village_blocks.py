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
direction: LOOK. convert_nbt.py's older single table gets east/west stairs
and north/south ladders and torches backwards, and swaps log axes x/z.
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


# SC CellFace numbering: FaceToPoint3(f) = 0:(0,0,+1) south, 1:(+1,0,0) east, 2:(0,0,-1) north, 3:(-1,0,0) west
FACE = {"south": 0, "east": 1, "north": 2, "west": 3}   # SC face index pointing in the MC direction
LOOK = {"north": 0, "west": 1, "south": 2, "east": 3}   # SC "rotation" = player's look direction at placement

# Nearest SC fabric colour (CIELAB dE76 against SubsystemPalette fabric colours) per MC dye
SC_COLOR = {
    "white": 0, "orange": 6, "magenta": 10, "light_blue": 3, "yellow": 4, "lime": 13,
    "pink": 2, "gray": 8, "light_gray": 7, "cyan": 9, "purple": 10, "blue": 11,
    "brown": 12, "green": 13, "red": 14, "black": 15,
}

WOOD_AXIS = {"y": 0, "z": 1, "x": 2}
SAPLING = {"oak": 0, "birch": 1, "spruce": 2, "acacia": 4, "jungle": 0, "dark_oak": 0}
STONE_PLATES = {"stone_pressure_plate", "polished_blackstone_pressure_plate",
                "light_weighted_pressure_plate", "heavy_weighted_pressure_plate"}


def _map(name, props, report):
    n = name.removeprefix("minecraft:")
    f = props.get("facing", "north")

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
        return ("MarbleBlock", 1) if int(props.get("layers", "1")) >= 8 else ("SnowBlock", 0)
    if n == "snow_block":
        return ("MarbleBlock", 1)                       # white-painted marble (1 | 0 << 1)
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
    if name.removeprefix("minecraft:") == "smooth_sandstone":
        return ("SandstoneBlock", 0)
    return _map(name, props, report)

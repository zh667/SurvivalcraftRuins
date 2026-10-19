"""Minecraft 1.16.5 jigsaw village assembly, ported for offline layout generation.

Follows JigsawPlacement.addPieces / Placer.tryPlacingChildren step by step:
breadth-first expansion from a random town centre, weighted shuffles of each
pool followed by its fallback, four shuffled rotations per candidate, the
"free space" collision volumes (an outer 160-block box for pieces attached
outward, the parent's own box for pieces attached inside it), the max depth of
6 after which only fallbacks (terminators) are tried, and the village
"expansion hack" that makes short pieces reserve the height their own inside
attachments may need.

Terrain is assumed flat while laying out, so collisions are tested in a flat
world exactly as Minecraft would on level ground. What the real terrain needs
is kept per piece as an *anchor*: either "surface at (x, z) plus dy" or "my
rigid parent's floor plus dy" -- the two rules Minecraft itself uses to pick a
piece's height. The mod evaluates those anchors against the actual
Survivalcraft terrain when it builds a village.

Only Python's own RNG is used, so layouts are reproducible here but are not
Minecraft's own layouts for any given seed.
"""
import random
from collections import deque

HORIZONTAL = ["north", "east", "south", "west"]   # clockwise order
STEP = {
    "north": (0, 0, -1), "south": (0, 0, 1), "east": (1, 0, 0),
    "west": (-1, 0, 0), "up": (0, 1, 0), "down": (0, -1, 0),
}
OPPOSITE = {"north": "south", "south": "north", "east": "west",
            "west": "east", "up": "down", "down": "up"}
ROTATIONS = ["none", "clockwise_90", "clockwise_180", "counterclockwise_90"]
QUARTERS = {"none": 0, "clockwise_90": 1, "clockwise_180": 2, "counterclockwise_90": 3}

OUTER_REACH = 80
MAX_DEPTH = 6


def jdiv2(a):
    """Java's (a) / 2 for ints: truncates toward zero."""
    return int(a / 2)


def rotate_dir(direction, rotation):
    if direction not in HORIZONTAL:
        return direction
    return HORIZONTAL[(HORIZONTAL.index(direction) + QUARTERS[rotation]) % 4]


def transform(x, y, z, rotation):
    """StructureTemplate.transform with the default pivot (0,0,0) and no mirror."""
    q = QUARTERS[rotation]
    if q == 0:
        return x, y, z
    if q == 1:
        return -z, y, x
    if q == 2:
        return -x, y, -z
    return z, y, -x


def rotate_props(props, rotation):
    """BlockState.rotate for the properties village pieces use."""
    q = QUARTERS[rotation]
    if q == 0:
        return dict(props)
    out = dict(props)
    if props.get("facing") in HORIZONTAL:
        out["facing"] = rotate_dir(props["facing"], rotation)
    if "axis" in props and q % 2 == 1:
        out["axis"] = {"x": "z", "z": "x"}.get(props["axis"], props["axis"])
    if "rotation" in props:
        out["rotation"] = str((int(props["rotation"]) + 4 * q) % 16)
    sides = [d for d in HORIZONTAL if d in props]
    if sides:
        for d in HORIZONTAL:
            out.pop(d, None)
        for d in sides:
            out[rotate_dir(d, rotation)] = props[d]
    if "orientation" in props:
        front, top = props["orientation"].split("_")
        out["orientation"] = f"{rotate_dir(front, rotation)}_{rotate_dir(top, rotation)}"
    return out


class Box:
    __slots__ = ("x0", "y0", "z0", "x1", "y1", "z1")

    def __init__(self, x0, y0, z0, x1, y1, z1):
        self.x0, self.y0, self.z0, self.x1, self.y1, self.z1 = x0, y0, z0, x1, y1, z1

    def moved(self, dx, dy, dz):
        return Box(self.x0 + dx, self.y0 + dy, self.z0 + dz, self.x1 + dx, self.y1 + dy, self.z1 + dz)

    def contains(self, p):
        return self.x0 <= p[0] <= self.x1 and self.y0 <= p[1] <= self.y1 and self.z0 <= p[2] <= self.z1

    def y_span(self):
        return self.y1 - self.y0 + 1

    def overlaps(self, o):
        return (self.x0 <= o.x1 and self.x1 >= o.x0 and self.y0 <= o.y1 and self.y1 >= o.y0
                and self.z0 <= o.z1 and self.z1 >= o.z0)

    def inside(self, o):
        return (self.x0 >= o.x0 and self.x1 <= o.x1 and self.y0 >= o.y0 and self.y1 <= o.y1
                and self.z0 >= o.z0 and self.z1 <= o.z1)

    def as_list(self):
        return [self.x0, self.y0, self.z0, self.x1, self.y1, self.z1]


class FreeSpace:
    """A container box minus every piece already placed in it.

    Minecraft keeps this as a VoxelShape and rejects a candidate whose box,
    shrunk by a quarter block, is not wholly inside the shape. With block
    aligned boxes that is: inside the container and touching no placed box.
    """

    def __init__(self, container):
        self.container = container
        self.taken = []

    def fits(self, box):
        if not box.inside(self.container):
            return False
        return not any(box.overlaps(t) for t in self.taken)

    def take(self, box):
        self.taken.append(box)


class Jigsaw:
    __slots__ = ("pos", "front", "top", "name", "pool", "target", "joint", "final_state")

    def __init__(self, pos, front, top, nbt):
        self.pos = pos
        self.front = front
        self.top = top
        self.name = nbt.get("name", "minecraft:empty")
        self.pool = nbt.get("pool", "minecraft:empty")
        self.target = nbt.get("target", "minecraft:empty")
        joint = nbt.get("joint")
        if not joint:
            joint = "aligned" if front in HORIZONTAL else "rollable"
        self.joint = joint
        self.final_state = nbt.get("final_state", "minecraft:air")


def can_attach(a, b):
    """JigsawBlock.canAttach."""
    return (a.front == OPPOSITE[b.front]
            and (a.joint == "rollable" or a.top == b.top)
            and a.target == b.name)


class Template:
    """A structure .nbt with what the assembler needs from it."""

    def __init__(self, location, data):
        self.location = location
        self.size = tuple(data["size"])
        palette = data.get("palette") or data["palettes"][0]
        self.blocks = []
        self.jigsaws = []
        for blk in data["blocks"]:
            entry = palette[blk["state"]]
            props = {k: str(v) for k, v in entry.get("Properties", {}).items()}
            pos = tuple(blk["pos"])
            nbt = blk.get("nbt") or {}
            self.blocks.append((pos, entry["Name"], props, nbt))
            if entry["Name"] == "minecraft:jigsaw":
                front, top = props["orientation"].split("_")
                self.jigsaws.append(Jigsaw(pos, front, top, nbt))

    def bbox(self, rotation, origin):
        sx, sy, sz = self.size
        a = transform(0, 0, 0, rotation)
        b = transform(sx - 1, sy - 1, sz - 1, rotation)
        return Box(min(a[0], b[0]) + origin[0], 0 + origin[1], min(a[2], b[2]) + origin[2],
                   max(a[0], b[0]) + origin[0], sy - 1 + origin[1], max(a[2], b[2]) + origin[2])

    def rotated_jigsaws(self, rotation, origin):
        out = []
        for j in self.jigsaws:
            x, y, z = transform(*j.pos, rotation)
            r = Jigsaw((x + origin[0], y + origin[1], z + origin[2]),
                       rotate_dir(j.front, rotation), rotate_dir(j.top, rotation), {})
            r.name, r.pool, r.target, r.joint, r.final_state = j.name, j.pool, j.target, j.joint, j.final_state
            out.append(r)
        return out


class Element:
    """One pool entry: a template piece, a feature placeholder, or 'empty'."""

    def __init__(self, spec, projection, templates):
        self.kind = spec["kind"]
        self.projection = projection
        self.location = spec.get("location")
        self.feature = spec.get("feature")
        self.processors = spec.get("processors", "EMPTY")
        self.template = templates(self.location) if self.location else None

    @property
    def rigid(self):
        return self.projection == "rigid"

    def bbox(self, rotation, origin):
        if self.template is None:
            # FeaturePoolElement: zero size, a single block.
            return Box(origin[0], origin[1], origin[2], origin[0], origin[1], origin[2])
        return self.template.bbox(rotation, origin)

    def jigsaws(self, rotation, origin, rnd):
        if self.template is None:
            # FeaturePoolElement's built-in jigsaw: facing down, named "bottom".
            j = Jigsaw(tuple(origin), "down", "south",
                       {"name": "minecraft:bottom", "pool": "minecraft:empty",
                        "target": "minecraft:empty", "joint": "rollable"})
            return [j]
        out = self.template.rotated_jigsaws(rotation, origin)
        rnd.shuffle(out)
        return out

    def __repr__(self):
        return self.location or f"feature:{self.feature}"


class Pool:
    def __init__(self, name, spec, templates):
        self.name = name
        self.fallback = spec["fallback"]
        self.entries = []          # one entry per unit of weight, as Minecraft does
        for e in spec["elements"]:
            element = Element(e, spec["projection"], templates)
            self.entries.extend([element] * e["weight"])
        self._max_size = None

    def size(self):
        return len(self.entries)

    def max_size(self):
        if self._max_size is None:
            spans = [e.bbox("none", (0, 0, 0)).y_span() for e in self.entries if e.kind != "empty"]
            self._max_size = max(spans) if spans else 0
        return self._max_size

    def shuffled(self, rnd):
        out = list(self.entries)
        rnd.shuffle(out)
        return out


class Piece:
    def __init__(self, element, rotation, pos, box, depth, anchor):
        self.element = element
        self.rotation = rotation
        self.pos = pos
        self.box = box
        self.depth = depth
        self.anchor = anchor


def normalize(name):
    return name.removeprefix("minecraft:")


class Assembler:
    def __init__(self, pool_specs, templates, ground=64, expansion_hack=True):
        self.pools = {}
        for name, spec in pool_specs.items():
            self.pools[name] = Pool(name, spec, templates)
        self.pools.setdefault("empty", Pool("empty", {"fallback": "empty", "projection": "rigid", "elements": []}, templates))
        self.ground = ground            # flat-world "first free height"
        self.expansion_hack = expansion_hack

    def pool(self, name):
        return self.pools.get(normalize(name))

    def assemble(self, start_pool, seed):
        rnd = random.Random(seed)
        rotation = rnd.choice(ROTATIONS)
        start = self.pool(start_pool)
        element = start.entries[rnd.randrange(start.size())]
        box = element.bbox(rotation, (0, 0, 0))
        cx = jdiv2(box.x1 + box.x0)
        cz = jdiv2(box.z1 + box.z0)
        y = self.ground
        dy = y - (box.y0 + 1)               # ground level delta of a template piece is 1
        root = Piece(element, rotation, (0, dy, 0), box.moved(0, dy, 0), 0,
                     {"x": cx, "z": cz, "dy": -1})
        pieces = [root]
        outer = Box(cx - OUTER_REACH, y - OUTER_REACH, cz - OUTER_REACH,
                    cx + OUTER_REACH, y + OUTER_REACH, cz + OUTER_REACH)
        free = FreeSpace(outer)
        free.take(root.box)
        queue = deque([(root, free)])
        while queue:
            piece, space = queue.popleft()
            self._children(piece, space, pieces, queue, rnd)
        return pieces, (cx, cz)

    def _children(self, parent, free, pieces, queue, rnd):
        element = parent.element
        parent_rigid = element.rigid
        box = parent.box
        y0 = box.y0
        inner = None
        for jig in element.jigsaws(parent.rotation, parent.pos, rnd):
            step = STEP[jig.front]
            target = (jig.pos[0] + step[0], jig.pos[1] + step[1], jig.pos[2] + step[2])
            local_y = jig.pos[1] - y0
            pool = self.pool(jig.pool)
            if pool is None or (pool.size() == 0 and normalize(jig.pool) != "empty"):
                continue
            fallback = self.pool(pool.fallback)
            if fallback is None or (fallback.size() == 0 and normalize(pool.fallback) != "empty"):
                continue
            if box.contains(target):
                if inner is None:
                    inner = FreeSpace(box)
                space = inner
            else:
                space = free
            candidates = []
            if parent.depth != MAX_DEPTH:
                candidates.extend(pool.shuffled(rnd))
            candidates.extend(fallback.shuffled(rnd))
            placed = False
            for candidate in candidates:
                if candidate.kind == "empty":
                    break
                rotations = list(ROTATIONS)
                rnd.shuffle(rotations)
                for rotation in rotations:
                    child_jigsaws = candidate.jigsaws(rotation, (0, 0, 0), rnd)
                    local_box = candidate.bbox(rotation, (0, 0, 0))
                    reserve = 0
                    if self.expansion_hack and local_box.y_span() <= 16:
                        for cj in child_jigsaws:
                            cs = STEP[cj.front]
                            if not local_box.contains((cj.pos[0] + cs[0], cj.pos[1] + cs[1], cj.pos[2] + cs[2])):
                                continue
                            p = self.pool(cj.pool)
                            f = self.pool(p.fallback) if p is not None else None
                            reserve = max(reserve, p.max_size() if p else 0, f.max_size() if f else 0)
                    for cj in child_jigsaws:
                        if not can_attach(jig, cj):
                            continue
                        origin = (target[0] - cj.pos[0], target[1] - cj.pos[1], target[2] - cj.pos[2])
                        child_box = candidate.bbox(rotation, origin)
                        delta = local_y - cj.pos[1] + STEP[jig.front][1]
                        if parent_rigid and candidate.rigid:
                            floor = y0 + delta
                            anchor = {"parent": pieces.index(parent), "dy": delta}
                        else:
                            floor = self.ground - cj.pos[1]
                            anchor = {"x": jig.pos[0], "z": jig.pos[2], "dy": -cj.pos[1]}
                        shift = floor - child_box.y0
                        moved = child_box.moved(0, shift, 0)
                        if reserve > 0:
                            moved.y1 = moved.y0 + max(reserve + 1, moved.y1 - moved.y0)
                        if not space.fits(moved):
                            continue
                        space.take(moved)
                        child = Piece(candidate, rotation, (origin[0], origin[1] + shift, origin[2]),
                                      moved, parent.depth + 1, anchor)
                        pieces.append(child)
                        if parent.depth + 1 <= MAX_DEPTH:
                            queue.append((child, space))
                        placed = True
                        break
                    if placed:
                        break
                if placed:
                    break

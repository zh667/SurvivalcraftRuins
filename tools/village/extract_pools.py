#!/usr/bin/env python3
"""Extract Minecraft 1.16.5 village jigsaw pools into tools/village/pools.json.

Usage: extract_pools.py <decompiled net/minecraft/data/worldgen dir> <out.json>

The pools (piece weights, fallbacks, projections) are not data files in the
1.16.5 jar: they are registered from code in PlainVillagePools, DesertVillagePools,
SavannaVillagePools, SnowyVillagePools and TaigaVillagePools. Decompile those
(CFR over the jar remapped with Mojang's official mappings; see the memory note
on the MC toolchain) and point this script at the output. Only the numbers and
names are kept; the decompiled code itself stays out of the repository.

Zombie villages are dropped, as are the pools that only spawn entities
(villagers, cats, golems, farm animals): Survivalcraft has no equivalents.
"""
import json
import os
import re
import sys

FILES = ["PlainVillagePools", "DesertVillagePools", "SavannaVillagePools",
         "SnowyVillagePools", "TaigaVillagePools"]

POOL_START = 'new StructureTemplatePool(new ResourceLocation("'
ELEMENT = re.compile(
    r'Pair\.of\(StructurePoolElement\.(\w+)\(([^()]*)\),\s*\(Object\)(\d+)\)')
PROJECTION = re.compile(r'Projection\.(RIGID|TERRAIN_MATCHING)\s*$')

# Pools whose pieces only carry entities. Survivalcraft has no villagers,
# cats or iron golems, so these are treated as Minecraft's own empty pool.
ENTITY_POOLS = re.compile(r'/(villagers|cats|iron_golem|animals|sheep|butcher_animals)$')


def balanced_call(text, start):
    """Returns the argument text of the call whose '(' is at text[start]."""
    depth = 0
    for i in range(start, len(text)):
        c = text[i]
        if c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
            if depth == 0:
                return text[start + 1:i]
    raise ValueError("unbalanced call")


def parse_element(kind, args):
    strings = re.findall(r'"([^"]*)"', args)
    processors = re.findall(r'ProcessorLists\.(\w+)', args)
    if kind in ("legacy", "single"):
        return {"kind": kind, "location": strings[0], "processors": processors[0] if processors else "EMPTY"}
    if kind == "feature":
        return {"kind": "feature", "feature": args.split(".")[-1].strip()}
    if kind == "empty":
        return {"kind": "empty"}
    raise ValueError(f"unknown element kind {kind}")


def extract(source_dir):
    pools = {}
    for name in FILES:
        text = open(os.path.join(source_dir, name + ".java"), encoding="utf-8").read()
        pos = 0
        while True:
            pos = text.find(POOL_START, pos)
            if pos < 0:
                break
            open_paren = text.index("(", pos)
            body = balanced_call(text, open_paren)
            pos = open_paren + len(body)
            names = re.findall(r'new ResourceLocation\("([^"]*)"\)', body)
            pool_name, fallback = names[0], names[1]
            if "/zombie/" in pool_name:
                continue
            projection = PROJECTION.search(body)
            if projection is None:
                raise ValueError(f"no projection for {pool_name}")
            elements = []
            for kind, args, weight in ELEMENT.findall(body):
                element = parse_element(kind, args)
                if "/zombie/" in element.get("location", ""):
                    continue
                element["weight"] = int(weight)
                elements.append(element)
            if ENTITY_POOLS.search(pool_name):
                elements = []
            pools[pool_name] = {
                "fallback": fallback,
                "projection": projection.group(1).lower(),
                "elements": elements,
            }
    return pools


def main():
    pools = extract(sys.argv[1])
    with open(sys.argv[2], "w", encoding="utf-8") as f:
        json.dump(pools, f, indent=1, sort_keys=True)
    total = sum(len(p["elements"]) for p in pools.values())
    print(f"{len(pools)} pools, {total} elements -> {sys.argv[2]}")


if __name__ == "__main__":
    main()

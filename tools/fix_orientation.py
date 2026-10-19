#!/usr/bin/env python3
"""One-off repair of templates converted before 1.1 (see convert_nbt.py).

Usage: fix_orientation.py <template.json>...

convert_nbt.py used north0/east1/south2/west3 for every oriented block. That
is right for north and south stairs and gates only. Templates made with it are
repaired in place by permuting palette data, which is exact because every
oriented entry came from that one table (no template was edited by hand):

  stairs, fence gates   rotation 1 <-> 3   (east/west; upside-down bit kept)
  ladders, wall torches 0 <-> 2            (north/south; floor torches = 4 kept)
  logs                  axis 1 <-> 2       (x/z)

Running it twice undoes it, so it refuses files already marked as fixed.
"""
import json
import sys

LOGS = {"OakWoodBlock", "SpruceWoodBlock", "BirchWoodBlock", "MimosaWoodBlock", "PoplarWoodBlock"}


def fix(block, data):
    if block.endswith("StairsBlock") or block == "WoodenFenceGateBlock":
        r = data & 3
        return (data & ~3) | {1: 3, 3: 1}.get(r, r)
    if block == "WoodenLadderBlock" or (block == "TorchBlock" and data < 4):
        return {0: 2, 2: 0}.get(data, data)
    if block in LOGS:
        return {1: 2, 2: 1}.get(data, data)
    return data


def main():
    for path in sys.argv[1:]:
        with open(path, encoding="utf-8") as f:
            template = json.load(f)
        if template.get("orientation") == "sc":
            print(f"{path}: already fixed, skipped")
            continue
        changed = 0
        for entry in template["palette"]:
            new = fix(entry[0], entry[1])
            if new != entry[1]:
                entry[1] = new
                changed += 1
        template["orientation"] = "sc"
        with open(path, "w", encoding="utf-8") as f:
            json.dump(template, f, separators=(",", ":"))
        print(f"{path}: {changed} palette entries re-oriented")


if __name__ == "__main__":
    main()

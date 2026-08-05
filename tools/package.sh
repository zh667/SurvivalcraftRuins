#!/usr/bin/env bash
# Builds and packages both flavors from the same source:
#   .scmod  — compiled against the SCAPI plugin runtime, for the Mods/ folder
#   .netmod — compiled against SurvivalcraftNet 2.4, for the NetMods/ folder
# Two separate compilations are REQUIRED: SCAPI's ModsManager lives in the
# global namespace while Net2.4's lives in Game, so a binary built against one
# runtime throws TypeLoadException on the other even though the source
# compiles against both.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/src/SurvivalcraftRuins"
VERSION=$(grep -oP '"Version": "\K[^"]+' "$SRC/modinfo.json" | head -1)
OUT="$ROOT/artifacts"
NET_DIR="${NET_DIR:-$HOME/sc-libs/}"
SCAPI_DIR="${SCAPI_DIR:-/home/dev/projects/[Windows]SurvivalcraftAPI_1.9.2.1/}"

mkdir -p "$OUT"

package_one() {
    local runtime_dir="$1" ext="$2"
    local pkg="$OUT/SurvivalcraftRuins-$VERSION.$ext"
    dotnet build "$SRC" -c Release "-p:SurvivalcraftDir=$runtime_dir" > /dev/null
    local stage
    stage=$(mktemp -d)
    cp "$SRC/bin/Release/net10.0/SurvivalcraftRuins.dll" "$stage/"
    if [ "$ext" = netmod ]; then
        # Net2.4's modinfo parser reflects JSON keys onto ModInfo fields with no
        # type coercion: a JSON object under "Dependencies" (SCAPI schema style)
        # gets assigned to List<string> and throws, silently dropping the whole
        # mod from the list. Emit only the fields Net2.4 declares, with
        # Dependencies as an array.
        python3 - "$SRC/modinfo.json" "$stage/modinfo.json" <<'PYEOF'
import json, sys
src = json.load(open(sys.argv[1], encoding="utf-8"))
deps = src.get("Dependencies") or []
if isinstance(deps, dict):
    deps = sorted(deps)
out = {k: src.get(k, "") for k in
       ("Name", "Version", "ApiVersion", "Description", "ScVersion", "Link", "Author", "PackageName")}
out["ApiVersion"] = "1.44"
out["ScVersion"] = "2.4.40.6"
out["Dependencies"] = deps
json.dump(out, open(sys.argv[2], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PYEOF
    else
        cp "$SRC/modinfo.json" "$stage/"
    fi
    cp -r "$SRC/Assets" "$stage/Assets"
    rm -f "$pkg"
    python3 - "$stage" "$pkg" <<'EOF'
import os, sys, zipfile
stage, pkg = sys.argv[1], sys.argv[2]
entries = []
for root, _, files in os.walk(stage):
    for f in files:
        full = os.path.join(root, f)
        entries.append((os.path.relpath(full, stage).replace(os.sep, "/"), full))
entries.sort()
with zipfile.ZipFile(pkg, "w", zipfile.ZIP_DEFLATED) as z:
    for rel, full in entries:
        info = zipfile.ZipInfo(rel, date_time=(2000, 1, 1, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        with open(full, "rb") as fh:
            z.writestr(info, fh.read())
print("packaged:", pkg)
EOF
    rm -rf "$stage"
    python3 -c "
import zipfile
z = zipfile.ZipFile('$pkg')
names = z.namelist()
assert 'modinfo.json' in names and 'SurvivalcraftRuins.dll' in names, names
print('$pkg'.split('/')[-1], f'{len(names)} entries OK')
"
}

if [ -f "${SCAPI_DIR}Survivalcraft.dll" ]; then
    package_one "$SCAPI_DIR" scmod
else
    echo "WARN: SCAPI runtime not found at $SCAPI_DIR — skipping .scmod" >&2
fi
package_one "$NET_DIR" netmod

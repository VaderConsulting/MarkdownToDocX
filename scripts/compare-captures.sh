#!/usr/bin/env bash
set -euo pipefail
# Usage: compare-captures.sh baseline_dir current_dir label
BASE="$1"
CUR="$2"
LABEL="${3:-compare}"
python3 - <<'PY' "$BASE" "$CUR" "$LABEL"
import sys, re
from pathlib import Path

base, cur, label = sys.argv[1:4]

def legality_map(path):
    m = {}
    p = Path(path) / "route-legality.tsv"
    if not p.exists():
        return m
    for line in p.read_text().splitlines():
        parts = line.split("|")
        if len(parts) < 3 or parts[0] != "ROUTE_LEGALITY":
            continue
        sid = parts[1]
        illegal = shared = offslot = 0
        for chunk in parts[2:]:
            if chunk.startswith("illegalPaths="):
                illegal = int(chunk.split("=", 1)[1])
            if chunk.startswith("sharedTracks="):
                shared = int(chunk.split("=", 1)[1])
        m[sid] = (illegal, shared)
    return m

def audit_map(path):
    m = {}
    p = Path(path) / "route-audit.tsv"
    if not p.exists():
        return m
    for line in p.read_text().splitlines():
        if not line.startswith("ROUTE_AUDIT|"):
            continue
        parts = line.split("|")
        if len(parts) < 4:
            continue
        sid = parts[2]
        fields = "|".join(parts[3:])
        def grab(key):
            m2 = re.search(rf"(?:^|\|){key}=(\d+)", fields)
            return int(m2.group(1)) if m2 else 0
        m[sid] = {
            "offSlot": grab("offSlotPorts"),
            "port": grab("port"),
            "shared": grab("sharedTracks") if "sharedTracks" in fields else 0,
        }
        # offSlotGroups from findings
        m2 = re.search(r"offSlotGroups=(\d+)", fields)
        if m2:
            m[sid]["offSlotGroups"] = int(m2.group(1))
    return m

def failed_tests(path):
    p = Path(path) / "layout-failed.txt"
    return set(p.read_text().splitlines()) if p.exists() else set()

b_leg = legality_map(base)
c_leg = legality_map(cur)
b_aud = audit_map(base)
c_aud = audit_map(cur)
b_fail = failed_tests(base)
c_fail = failed_tests(cur)

ids = sorted(set(b_leg) | set(c_leg) | set(b_aud) | set(c_aud))
worse = better = same = 0
print(f"=== Audit fault compare ({label}) ===")
for sid in ids:
    b = b_aud.get(sid, {})
    c = c_aud.get(sid, {})
    bl = b_leg.get(sid, (0, 0))
    cl = c_leg.get(sid, (0, 0))
    score_b = (bl[0], bl[1], b.get("offSlot", 0), b.get("port", 0))
    score_c = (cl[0], cl[1], c.get("offSlot", 0), c.get("port", 0))
    if score_c > score_b:
        worse += 1
        print(f"WORSE {sid} {score_b} -> {score_c}")
    elif score_c < score_b:
        better += 1
    else:
        same += 1
print(f"audit: worse={worse} better={better} unchanged={same}")

new_fail = sorted(c_fail - b_fail)
new_pass = sorted(b_fail - c_fail)
print(f"=== Layout tests ({label}) ===")
print(f"baseline_failed={len(b_fail)} current_failed={len(c_fail)}")
print(f"newly_failing={len(new_fail)} newly_passing={len(new_pass)} unchanged_fail={len(b_fail & c_fail)}")
if new_fail[:20]:
    print("sample newly failing:", ", ".join(new_fail[:20]))
PY

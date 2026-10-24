#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
import re
from pathlib import Path

def read_failed(dir):
    p = Path(dir) / "layout-failed.txt"
    return set(p.read_text().splitlines()) if p.exists() else set()

def read_layout_summary(dir):
    p = Path(dir) / "layout-summary.txt"
    return p.read_text().strip() if p.exists() else "pending"

def audit_failures(dir):
    p = Path(dir) / "audit-summary.txt"
    if not p.exists():
        return None
    m = re.search(r"Failed:\s+(\d+),\s+Passed:\s+(\d+)", p.read_text())
    return (int(m.group(1)), int(m.group(2))) if m else None

def scenario_metrics(dir):
    out = {}
    p = Path(dir) / "route-audit.tsv"
    if not p.exists():
        return out
    for line in p.read_text().splitlines():
        if not line.startswith("ROUTE_AUDIT|"):
            continue
        parts = line.split("|")
        if len(parts) < 4:
            continue
        sid = parts[2]
        blob = "|".join(parts[3:])
        def g(key):
            m = re.search(rf"(?:^|\|){key}=(\d+)", blob)
            return int(m.group(1)) if m else 0
        leg = Path(dir) / "route-legality.tsv"
        illegal = shared = 0
        if leg.exists():
            for lline in leg.read_text().splitlines():
                if f"|{sid}|" in lline:
                    for chunk in lline.split("|"):
                        if chunk.startswith("illegalPaths="):
                            illegal = int(chunk.split("=",1)[1])
                        if chunk.startswith("sharedTracks="):
                            shared = int(chunk.split("=",1)[1])
        out[sid] = (illegal, shared, g("offSlotPorts"), g("port"))
    return out

dirs = {
    "branch_start_18e5b09": "/tmp/capture/18e5b09",
    "master_eb09c58": "/tmp/capture/eb09c58",
    "current": "/tmp/capture/current",
}
failed = {k: read_failed(v) for k,v in dirs.items()}
metrics = {k: scenario_metrics(v) for k,v in dirs.items()}

print("=== Layout suite ===")
for k,v in dirs.items():
    print(f"{k}: {read_layout_summary(v)}")
    t = Path(v)/"layout-time.txt"
    if t.exists():
        print(f"  {t.read_text().strip()}")

print("\n=== Audit suite ===")
for k,v in dirs.items():
    af = audit_failures(v)
    print(f"{k}: {af if af else 'pending'}")
    t = Path(v)/"audit-time.txt"
    if t.exists():
        print(f"  {t.read_text().strip()}")

if metrics["branch_start_18e5b09"] and metrics["current"]:
    base = metrics["branch_start_18e5b09"]
    cur = metrics["current"]
    worse = better = same = 0
    for sid in sorted(set(base)|set(cur)):
        b = base.get(sid, (0,0,0,0))
        c = cur.get(sid, (0,0,0,0))
        if c > b:
            worse += 1
            if worse <= 15:
                print(f"WORSE vs 18e5b09 {sid} {b} -> {c}")
        elif c < b:
            better += 1
        else:
            same += 1
    print(f"\nAudit fault tuple (illegal, shared, offSlot, port): worse={worse} better={better} same={same}")

if failed["branch_start_18e5b09"] and failed["current"]:
    nf = sorted(failed["current"] - failed["branch_start_18e5b09"])
    np = sorted(failed["branch_start_18e5b09"] - failed["current"])
    print(f"\nLayout tests: newly_failing={len(nf)} newly_passing={len(np)} unchanged_fail={len(failed['current'] & failed['branch_start_18e5b09'])}")
PY

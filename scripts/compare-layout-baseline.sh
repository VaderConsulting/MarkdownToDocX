#!/usr/bin/env bash
set -euo pipefail
BASE_SHA="${1:-42dda5ee}"
ROOT="${2:-/workspace}"
export PATH="${HOME}/.dotnet:${PATH}"
WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

export_fingerprints() {
  local repo="$1"
  local out="$2"
  cd "$repo"
  dotnet build tests/MermaidRecognition.RuleValidation.Tests/MermaidRecognition.RuleValidation.Tests.csproj -c Release -v q
  dotnet test tests/MermaidRecognition.RuleValidation.Tests/MermaidRecognition.RuleValidation.Tests.csproj -c Release --no-build \
    --filter "FullyQualifiedName~CatalogueLayoutFingerprintTests.ExportAllLayoutFingerprints" \
    --logger "console;verbosity=detailed" 2>&1 \
    | rg $'^\S+\t(node|port|SKIP)\t' > "$out" || true
}

echo "Exporting layout fingerprints at baseline $BASE_SHA..."
git worktree add "$WORKDIR/baseline" "$BASE_SHA" --detach
mkdir -p "$WORKDIR/baseline/tests/MermaidRecognition.RuleValidation.Tests"
cp "$ROOT/tests/MermaidRecognition.RuleValidation.Tests/CatalogueLayoutFingerprintTests.cs" \
  "$WORKDIR/baseline/tests/MermaidRecognition.RuleValidation.Tests/CatalogueLayoutFingerprintTests.cs"
export_fingerprints "$WORKDIR/baseline" "$WORKDIR/baseline.tsv"

echo "Exporting layout fingerprints at HEAD..."
export_fingerprints "$ROOT" "$WORKDIR/head.tsv"

python3 <<'PY' "$WORKDIR/baseline.tsv" "$WORKDIR/head.tsv"
import sys
from collections import defaultdict

def load(path):
    by_case = defaultdict(list)
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("Fingerprint"):
                continue
            parts = line.split("\t")
            if len(parts) < 2:
                continue
            case = parts[0]
            by_case[case].append(line)
    return by_case

base = load(sys.argv[1])
head = load(sys.argv[2])
all_cases = sorted(set(base) | set(head))
changed = []
for case in all_cases:
    b = "\n".join(sorted(base.get(case, [])))
    h = "\n".join(sorted(head.get(case, [])))
    if b != h:
        if case not in base:
            changed.append((case, "new case or layout export missing at baseline"))
        elif case not in head:
            changed.append((case, "layout failed at HEAD"))
        else:
            bset = set(base.get(case, []))
            hset = set(head.get(case, []))
            node_moves = sum(1 for line in hset - bset if "\tnode\t" in line)
            port_moves = sum(1 for line in hset - bset if "\tport\t" in line)
            changed.append((case, f"node/port fingerprint differs (+{node_moves} node lines, +{port_moves} port lines vs baseline)"))

print(f"LAYOUT_BASELINE|baseline={sys.argv[1]}|changed={len(changed)}|total={len(all_cases)}")
for case, reason in changed:
    print(f"LAYOUT_CHANGED|{case}|{reason}")
PY

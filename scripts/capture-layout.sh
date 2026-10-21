#!/usr/bin/env bash
set -euo pipefail
ROOT="$1"
OUT="$2"
export PATH="${HOME}/.dotnet:${PATH}"
cd "$ROOT"
dotnet build tests/MermaidRecognition.Layout.Tests/MermaidRecognition.Layout.Tests.csproj -c Release -v q
START=$(date +%s)
dotnet test tests/MermaidRecognition.Layout.Tests/MermaidRecognition.Layout.Tests.csproj -c Release --no-build \
  --logger "console;verbosity=normal" 2>&1 | tee "$OUT/layout.log"
END=$(date +%s)
echo "LAYOUT_SECONDS=$((END-START))" | tee "$OUT/layout-time.txt"
grep -E '^  Failed |^  Passed |Failed!' "$OUT/layout.log" | tail -5 | tee "$OUT/layout-summary.txt"
grep '^  Failed ' "$OUT/layout.log" | sed 's/^  Failed //' | sort -u > "$OUT/layout-failed.txt" || true

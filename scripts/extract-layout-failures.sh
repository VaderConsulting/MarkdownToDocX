#!/usr/bin/env bash
OUT="$1"
grep '^  Failed ' "$OUT/layout.log" | sed 's/^  Failed //' | sort -u > "$OUT/layout-failed.txt" || true
grep -E '^Failed!|^Total tests' "$OUT/layout.log" | tail -2 > "$OUT/layout-summary.txt" || true

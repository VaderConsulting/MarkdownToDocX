#!/usr/bin/env bash
set -euo pipefail
ROOT="$1"
OUT="$2"
export PATH="${HOME}/.dotnet:${PATH}"
cd "$ROOT"
dotnet build tests/MermaidRecognition.RuleValidation.Tests/MermaidRecognition.RuleValidation.Tests.csproj -c Release -v q
START=$(date +%s)
set +e
timeout 10m env ROUTE_AUDIT_REQUIRE_ALL=1 dotnet test tests/MermaidRecognition.RuleValidation.Tests/MermaidRecognition.RuleValidation.Tests.csproj -c Release --no-build \
  --filter "FullyQualifiedName~RouteRuleCatalogueAuditTests.AuditModelRoutes" \
  --blame-hang --blame-hang-timeout 2m \
  --logger "console;verbosity=detailed" 2>&1 | tee "$OUT/audit.log"
set -e
END=$(date +%s)
echo "AUDIT_SECONDS=$((END-START))" | tee "$OUT/audit-time.txt"
grep 'ROUTE_LEGALITY|' "$OUT/audit.log" | sed 's/^ *//' > "$OUT/route-legality.tsv" || true
grep 'ROUTE_AUDIT|' "$OUT/audit.log" | sed 's/^ *//' > "$OUT/route-audit.tsv" || true
tail -3 "$OUT/audit.log" | tee "$OUT/audit-summary.txt"

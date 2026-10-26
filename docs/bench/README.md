# Layout catalogue benchmark

Records SVG render time and SHA-256 for every diagram in `enterprise.md`, `flow.md`, and `EIP.md`. This matches the harness described in `docs/ENGINE-REVIEW.md` Phase 0.

## Run

From the repository root, Release build recommended:

```powershell
dotnet run -c Release --project tools/LayoutCatalogueBench/LayoutCatalogueBench.csproj docs/bench/catalogue-baseline.jsonl
```

Optional arguments: output path, per-diagram cap in milliseconds (default 90000), optional diagram id filter (for example `A104`).

## Compare

Re-run after engine changes and diff `SvgSha256` per `Id`. Phases 1-3 require zero differences against the Phase 0 baseline file you capture after the port-rule work lands.

```powershell
dotnet run -c Release --project tools/LayoutCatalogueBench/LayoutCatalogueBench.csproj docs/bench/catalogue-verify.jsonl
powershell -File docs/bench/Compare-CatalogueHashes.ps1 -Baseline docs/bench/catalogue-baseline.jsonl -Candidate docs/bench/catalogue-verify.jsonl
```

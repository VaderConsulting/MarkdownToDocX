# Layout catalogue benchmark

Records SVG **render** wall time and SHA-256 for diagrams in `enterprise.md`, `flow.md`, and `EIP.md`. Use the harness to compare render time and output identity across a fixed catalogue.

This tool measures **render** end-to-end (layout, routing, and SVG emission in one number). To attribute **layout** separately, use `DiagramGridLayout.LayoutStageTimingObserver` and the enterprise stage observers while profiling a single id (third argument below).

Some catalogue scenarios are **skipped** (not rendered, not counted as failures). The rules live in `tools/LayoutCatalogueBench/CatalogueBenchExclusions.cs` and follow the gate text in the markdown catalogues:

- **enterprise.md** — expected-diagnostic scenarios (Level 15 and others listed under *Expected-diagnostic scenarios*); Level **17** layout stress (`A104`–`A112`); grid stress **`A142`**; hint stress **`A154`–`A163`** (rule 2.21 stress tests).
- **flow.md** — fences that are fully **`%%`** remarked (`F65`, `F70`, `F87`, `F88`); Level **17** wrap stress (`F151`–`F153`).

Pass an explicit diagram id as the third argument to render a skipped stress diagram anyway (for example profiling `A142` with a higher time cap).

### LiveEditor enterprise sample (layout tuning)

For day-to-day engine and preview work, time the **same** diagram as **Samples → Enterprise → Clauses and relationships (Complete)** in LiveEditor (`SampleCatalog`, title `All options` in source). This is not a row in `enterprise.md`.

```powershell
dotnet run -c Release --project tools/LayoutCatalogueBench/LayoutCatalogueBench.csproj -- @liveeditor-enterprise-complete
```

Optional: output jsonl path, cap milliseconds. Also writes `liveeditor-enterprise-complete-stages.json` (per-stage layout ms).

### Landgate Azure import (optional)

```powershell
dotnet run -c Release --project tools/LayoutCatalogueBench/LayoutCatalogueBench.csproj -- @landgate-enterprise-import
```

Requires `in/Landgate.csv`. Imports with enterprise category defaults, then renders preview SVG. Default cap 600000 ms. Outputs `landgate-enterprise-import.jsonl` and `landgate-enterprise-import-stages.json`. Not in CI.

Example (Release, 26 Oct 2026, enterprise defaults, ~137 resources after category filter): render ~**102** s, layout stage sum ~**199** s. See `landgate-enterprise-import-stages.json` for breakdown.

## Run

From the repository root, Release build recommended:

```powershell
dotnet run -c Release --project tools/LayoutCatalogueBench/LayoutCatalogueBench.csproj docs/bench/catalogue-baseline.jsonl
```

Optional arguments: output path, per-diagram cap in milliseconds (default 90000), optional diagram id filter (for example `A104`).

Each jsonl row includes `Milliseconds` (render wall time) and `LayoutMilliseconds` (sum of top-level flowchart layout stages plus enterprise layout, routing and analysis observers for that render).

## Compare

Re-run after engine changes and diff `SvgSha256` per `Id`. Engine changes that must leave every diagram unchanged require zero differences against the local baseline file you capture after the port-rule work lands.

```powershell
dotnet run -c Release --project tools/LayoutCatalogueBench/LayoutCatalogueBench.csproj docs/bench/catalogue-verify.jsonl
powershell -File docs/bench/Compare-CatalogueHashes.ps1 -Baseline docs/bench/catalogue-baseline.jsonl -Candidate docs/bench/catalogue-verify.jsonl
```

# Layout catalogue benchmark

Records SVG render time and SHA-256 for diagrams in `enterprise.md`, `flow.md`, and `EIP.md`. This matches the harness described in `docs/ENGINE-REVIEW.md` (baseline captured before engine speed work).

Some catalogue scenarios are **skipped** (not rendered, not counted as failures). The rules live in `tools/LayoutCatalogueBench/CatalogueBenchExclusions.cs` and follow the gate text in the markdown catalogues:

- **enterprise.md** — expected-diagnostic scenarios (Level 15 and others listed under *Expected-diagnostic scenarios*); Level **17** layout stress (`A104`–`A112`); grid stress **`A142`**; hint stress **`A154`–`A163`** (rule 2.21 stress tests).
- **flow.md** — fences that are fully **`%%`** remarked (`F65`, `F70`, `F87`, `F88`); Level **17** wrap stress (`F151`–`F153`).

Pass an explicit diagram id as the third argument to render a skipped stress diagram anyway (for example profiling `A142` with a higher time cap).

## Run

From the repository root, Release build recommended:

```powershell
dotnet run -c Release --project tools/LayoutCatalogueBench/LayoutCatalogueBench.csproj docs/bench/catalogue-baseline.jsonl
```

Optional arguments: output path, per-diagram cap in milliseconds (default 90000), optional diagram id filter (for example `A104`).

## Compare

Re-run after engine changes and diff `SvgSha256` per `Id`. Engine changes that must leave every diagram unchanged require zero differences against the local baseline file you capture after the port-rule work lands.

```powershell
dotnet run -c Release --project tools/LayoutCatalogueBench/LayoutCatalogueBench.csproj docs/bench/catalogue-verify.jsonl
powershell -File docs/bench/Compare-CatalogueHashes.ps1 -Baseline docs/bench/catalogue-baseline.jsonl -Candidate docs/bench/catalogue-verify.jsonl
```

# MermaidRecognition - Mermaid recognition and native render (Stage 11.A)

The Mermaid engine is the set of `MermaidRecognition*` projects under `src/`, with their tests under `tests/`. Related locations:

| Location | Role |
| --- | --- |
| `src/MDConverter/`, `src/md2docx/`, `src/Converter/` | DOCX conversion pipeline. `MDConverter` calls this engine for flowcharts. |
| `tests/MDConverter.Tests/` | DOCX pipeline tests |
| `extern/Naiad/` | Vendored renderer, **frozen**. Still renders every non-flowchart diagram; also used for output comparison against the native render (§12). |
| `mermaid/packages/mermaid/` | **Authoritative** vendored Mermaid 11.14.0: detectors, grammar, config (investigate here, not Naiad) |

The adopted MSAGL Sugiyama code lives only in `src/MermaidRecognition.Layout/Sugiyama/`. The verbatim `extern/Microsoft.AGL/` snapshot has been removed (see `SUGIYAMA_ADOPTION.md`).

## Stage 11.A scope (authoritative)

**Multi-kind recognition** with **per-diagram gates** — see `spec.md` §4.2, §7.4, §11.1.13 (Stage 11.A), §11.1.14 (line-type catalogues).

| Target | Meaning |
| --- | --- |
| **All kinds (L1)** | Every §3.18.2 diagram: detect, config, diagnostics, one smoke fixture |
| **Flowchart (L3)** | **Current phase target-state:** full line-type catalogue + axis gates (styles, metadata, subgraphs, …) |
| **Catalogue** | Every source line form documented (`flowchart.link.grouped`, `sequence.message`, …) — see §11.1.14.1 exemplar |

Flowchart-only code today is **slice 1** toward flowchart **L3**, not Stage 11.A complete.

## Layout (`MermaidRecognition`)

Recognition code is grouped by **aspect**, not only by diagram kind:

```text
src/MermaidRecognition/
  Core/           MermaidRecognizer, KindRecognizerRegistry, IKindRecognizer
  Contract/       DiagramContract, NodeContract, EdgeContract, ContractSnapshot, …
  Config/         DiagramConfigContract, MermaidPinnedDefaults (spec §3.17), MermaidConfigurationPipeline
  Detection/      DiagramKindDetector (vendored orchestration order)
  Diagnostics/    MermaidDiagnostic, source spans
  Render/         FlowchartRouter, NativeFlowchartSvgRenderer, NativeRenderCapabilities (Stage 11.B pilot)
  Kinds/
    Flowchart/    deepest implementation (L3 target)
    Sequence/     L2 catalogue + recogniser
    Class/, State/, Er/, Pie/
    LinePattern/   L1 stubs for gantt, mindmap, C4, …; MermaidOnlyKindRecognizer; YamlFrontMatterKindRecognizer

src/MermaidRecognition.Layout/
  FlowchartGeometryGraphBuilder, FlowchartSugiyamaLayoutEngine  →  adopted Sugiyama (`Sugiyama/`, namespace `MermaidRecognition.Layout.Sugiyama.*`)
  Sugiyama/                       # copied MSAGL sources (503 files); see SUGIYAMA_ADOPTION.md
```

**Tests** (under `tests/`): `MermaidRecognition.Tests/` (840+; includes `Gates/DiagramKindEdgeCaseTests`), `MermaidRecognition.Layout.Tests/`, `MermaidRecognition.NaiadAdapter.Tests/`, `MermaidRecognition.RuleValidation.Tests/`.

**Flowchart regression:** `tests/FlowchartRegression/approved/` holds the approved SVG for every `flow.md` fixture. Run `.\tools\FixtureProbe\Compare-Fixtures.ps1` to check that none has changed.

**Gate registry:** run `.\tools\Generate-DiagramGateRegistry.ps1` → `spec-gates/registry.generated.md`.

**Naiad adapter:** `src/MermaidRecognition.NaiadAdapter/` maps `DiagramContract` to the Naiad `FlowchartModel`. `MDConverter` uses its `FlowchartRecognitionRenderBridge` when rendering flowcharts, and it also supports side-by-side comparison of recognition against the Naiad parse and render.

**§12:** [`spec-gates/naiad-render-baseline.md`](../../spec-gates/naiad-render-baseline.md) - legacy comparison inventory (not parse authority).

Do **not** store `.mmd` / `.verified.json` fixtures inside the library project — only under `MermaidRecognition.Tests/`.

## Build and test

```powershell
dotnet build src/MermaidRecognition/MermaidRecognition.csproj
dotnet test tests/MermaidRecognition.Tests/MermaidRecognition.Tests.csproj
```

Fixture writer tests are explicit and are skipped during normal verification. Regenerate verified JSON after intentional parser changes by selecting the writers:

```powershell
dotnet test tests/MermaidRecognition.Tests/MermaidRecognition.Tests.csproj --filter "FullyQualifiedName~Write_"
```

## Dependency rules

- `MermaidRecognition` must **not** reference Naiad or `src/MDConverter`.
- Only `MermaidRecognition.NaiadAdapter` (and its tests) may reference Naiad from within the engine.
- `src/MDConverter` references `MermaidRecognition`, `MermaidRecognition.Layout` and `MermaidRecognition.NaiadAdapter` (see spec §11.1.8).

See `spec.md` §7.4 (gate dimensions), §11.1.14 (line-type catalogues), §11.1.14.1 (flowchart line types).

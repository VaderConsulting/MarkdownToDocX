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
| **Flowchart (L4)** | Minimum G12 slice complete; extended backlog may remain open |
| **Started kinds (9)** | All at **L4** minimum G12 slice (sequence, class, state, ER, pie, gantt, mindmap, gitGraph) |
| **Catalogue** | Every source line form documented (`flowchart.link.grouped`, `sequence.message`, …) — see §11.1.14.1 exemplar |

Stage 11.A recognition exit is met for all started kinds at L4 minimum. Native render is implemented for flowcharts (Sugiyama); other kinds still use Naiad.

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

**Tests** (under `tests/`): `MermaidRecognition.Tests/` (932 tests), `MermaidRecognition.Layout.Tests/` (268 tests), `MermaidRecognition.NaiadAdapter.Tests/` (47 tests), `MermaidRecognition.RuleValidation.Tests/` (57 tests). Some layout and rule-validation tests may fail on Linux CI; run on Windows for the full suite.

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

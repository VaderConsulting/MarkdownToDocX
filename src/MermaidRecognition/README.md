# MermaidRecognition - Mermaid recognition and native render

The Mermaid engine is the set of `MermaidRecognition*` projects under `src/`, with their tests under `tests/`.

| Location | Role |
| --- | --- |
| `src/MDConverter/`, `src/md2docx/`, `src/Converter/` | DOCX conversion pipeline. `MDConverter` calls this engine for flowcharts. |
| `tests/MDConverter.Tests/` | DOCX pipeline tests |
| `extern/Naiad/` | Vendored renderer, **frozen**. Still renders every non-flowchart diagram; also used for output comparison against the native render. |
| `mermaid/packages/mermaid/` | **Authoritative** vendored Mermaid 11.14.0: detectors, grammar, config |

The adopted MSAGL Sugiyama code lives in `src/MermaidRecognition.Layout/Sugiyama/`. Flowchart placement on `master` uses the dagre port in `src/MermaidRecognition.Layout/Dagre/` (see `FlowchartDagrePlacement.cs`).

## Recognition scope

**Multi-kind recognition** with **per-diagram gates** - see `spec.md` section 7.4 and section 11.1.14 (line-type catalogues).

| Target | Meaning |
| --- | --- |
| **All kinds (L1)** | Every section 3.18.2 diagram: detect, config, diagnostics, one smoke fixture |
| **Started kinds (L4)** | Flowchart, sequence, class, state, ER, pie, gantt, mindmap, git graph at minimum G12 slice |
| **Catalogue** | Every source line form documented - see section 11.1.14.1 exemplar |

## Project layout

```text
src/MermaidRecognition/
  Core/           MermaidRecognizer, KindRecognizerRegistry, IKindRecognizer
  Contract/       DiagramContract, NodeContract, EdgeContract, ContractSnapshot
  Config/         DiagramConfigContract, MermaidPinnedDefaults, MermaidConfigurationPipeline
  Detection/      DiagramKindDetector (vendored orchestration order)
  Diagnostics/    MermaidDiagnostic, source spans
  Render/         FlowchartRouter, NativeRenderCapabilities
  Kinds/
    Flowchart/    deepest implementation
    Sequence/     L4 recogniser
    Class/, State/, Er/, Pie/, Gantt/, Mindmap/, GitGraph/
    LinePattern/  L1 stubs for additional kinds

src/MermaidRecognition.Layout/
  Dagre/                          C# dagre port (flowchart placement on master)
  FlowchartDagrePlacement.cs      DiagramContract to dagre graph
  FlowchartSugiyamaSvgRenderer.cs Native SVG output
  FlowchartCurvePathBuilder.cs    Edge path data (curveBasis, rounded corners)
  Sugiyama/                       Adopted MSAGL sources (alternate layout path)
```

## Tests

| Project | Role |
| --- | --- |
| `tests/MermaidRecognition.Tests/` | Recognition gates, fixtures, truthfulness checklist |
| `tests/MermaidRecognition.Layout.Tests/` | Layout, routing, and render regression |
| `tests/MermaidRecognition.NaiadAdapter.Tests/` | Legacy comparison bridge |
| `tests/MermaidRecognition.RuleValidation.Tests/` | Structural rule-validation pipeline |

**Flowchart regression:** `tests/FlowchartRegression/approved/` holds approved SVG for `flow.md` fixtures. Run `tools/FixtureProbe/Compare-Fixtures.ps1` to check for drift.


## Build and test

```powershell
dotnet build src/MermaidRecognition/MermaidRecognition.csproj
dotnet test tests/MermaidRecognition.Tests/MermaidRecognition.Tests.csproj
```

Regenerate verified JSON after intentional parser changes:

```powershell
dotnet test tests/MermaidRecognition.Tests/MermaidRecognition.Tests.csproj --filter "FullyQualifiedName~Write_"
```

## Dependency rules

- `MermaidRecognition` must **not** reference Naiad or `src/MDConverter`.
- Only `MermaidRecognition.NaiadAdapter` (and its tests) may reference Naiad from within the engine.
- `src/MDConverter` references `MermaidRecognition`, `MermaidRecognition.Layout`, and `MermaidRecognition.NaiadAdapter`.

See `spec.md` section 7.4 (gate dimensions) and section 11.1.14 (line-type catalogues).

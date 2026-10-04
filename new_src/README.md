# new_src — greenfield Mermaid recognition (Stage 11.A)

All **new** Markdown→diagram recognition work lives here, separate from:

| Location | Role |
| --- | --- |
| `src/MDConverter/`, `src/md2docx/`, `Converter/` | **Legacy** product pipeline (DOCX conversion today) |
| `tests/MDConverter.Tests/` | **Legacy** tests |
| `extern/Naiad/` | **Legacy** renderer — **frozen**; use only for output comparison vs native render (§12) |
| `extern/Microsoft.AGL/` | **Transitional** MSAGL spike — copy required classes into `MermaidRecognition.Layout/Sugiyama/` then remove (see `SUGIYAMA_ADOPTION.md`) |
| `mermaid/packages/mermaid/` | **Authoritative** vendored Mermaid 11.14.0 — detectors, grammar, config (investigate here, not Naiad) |

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
new_src/MermaidRecognition/
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

new_src/MermaidRecognition.Layout/
  FlowchartGeometryGraphBuilder, FlowchartSugiyamaLayoutEngine  →  adopted Sugiyama (`Sugiyama/`, namespace `MermaidRecognition.Layout.Sugiyama.*`)
  Sugiyama/                       # copied MSAGL sources (503 files); see SUGIYAMA_ADOPTION.md
```

**Tests:** `MermaidRecognition.Tests/` (840+; includes `Gates/DiagramKindEdgeCaseTests`), `MermaidRecognition.Layout.Tests/` (Sugiyama spike), `MermaidRecognition.NaiadAdapter.Tests/`.

**Gate registry:** run `.\tools\Generate-DiagramGateRegistry.ps1` → `spec-gates/registry.generated.md`.

**Legacy comparison glue (not product path):** `new_src/MermaidRecognition.NaiadAdapter/` maps `DiagramContract` → legacy Naiad `FlowchartModel` for **side-by-side comparison** only (recognition vs legacy parse/render). Do not treat this as the DOCX product render target.

**§12:** [`spec-gates/naiad-render-baseline.md`](../spec-gates/naiad-render-baseline.md) — legacy comparison inventory (not parse authority).

Do **not** store `.mmd` / `.verified.json` fixtures inside the library project — only under `MermaidRecognition.Tests/`.

## Build and test

```powershell
dotnet build new_src/MermaidRecognition/MermaidRecognition.csproj
dotnet test new_src/MermaidRecognition.Tests/MermaidRecognition.Tests.csproj
```

Fixture writer tests are explicit and are skipped during normal verification. Regenerate verified JSON after intentional parser changes by selecting the writers:

```powershell
dotnet test new_src/MermaidRecognition.Tests/MermaidRecognition.Tests.csproj --filter "FullyQualifiedName~Write_"
```

## Dependency rules

- `MermaidRecognition` must **not** reference Naiad or legacy `src/MDConverter`.
- Legacy code must **not** reference `MermaidRecognition` until an explicit adapter is introduced (see spec §11.1.8).

See `spec.md` §7.4 (gate dimensions), §11.1.14 (line-type catalogues), §11.1.14.1 (flowchart line types).

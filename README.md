# MarkdownToDocX - Mermaid Diagram Recognition & DOCX Renderer

A research-grade .NET platform for **parsing, recognising, and rendering Mermaid diagram source text** into SVG and DOCX, built on a vendored Mermaid 11.14.0 parser and a Sugiyama graph-layout engine derived from Microsoft MSAGL.

**Initiated:** 2021-04-09 · **Framework:** .NET 9 / .NET 10 · **Solution:** `MDConverter.slnx`

---

## What Is This Project?

The repo has two layers:

| Layer | Location | Role |
|-------|----------|------|
| DOCX pipeline | `src/md2docx/`, `src/MDConverter/`, `src/Converter/` | Converts Markdown files to `.docx` using OpenXml |
| Mermaid recogniser and renderer | `src/MermaidRecognition*/` | Mermaid diagram parser → recognition model → native layout and SVG render |

Flowcharts render through the native Sugiyama layout in `src/MermaidRecognition.Layout`. Every other diagram type still renders through the vendored Naiad renderer in `extern/Naiad`, which will be removed once the native renderer covers each type.

## Repository Layout

| Folder | Contents |
|--------|----------|
| `src/` | Product code: the DOCX converter (`md2docx`, `MDConverter`, `Converter` GUI) and the Mermaid engine (`MermaidRecognition*`) |
| `tests/` | Test projects for every product project, plus `FlowchartRegression/approved` (the approved SVG for each `flow.md` fixture) |
| `tools/` | `FixtureProbe` (renders `flow.md` fixtures; `Compare-Fixtures.ps1` checks them against the approved SVGs), `NativeCompareDocx`, `RuleValidationHarness`, and maintenance scripts |
| `extern/Naiad/` | Vendored Naiad renderer, still used for non-flowchart diagrams |
| `mermaid/` | Vendored Mermaid 11.14.0 TypeScript source, used as the reference for tracing behaviour |

---

## Solution Structure

| Project | Purpose |
|---------|---------|
| `MermaidRecognition` | Core recogniser - parses Mermaid source into a typed diagram contract (vertices, edges, groups, styles, interactions) |
| `MermaidRecognition.Layout` | Graph layout engine; Sugiyama implementation derived from Microsoft MSAGL (MIT) - see [Third-party notices](src/MermaidRecognition.Layout/THIRD_PARTY_NOTICES.md) |
| `MermaidRecognition.NaiadAdapter` | Bridge between the new recognition model and the legacy Naiad SVG renderer |
| `MermaidRecognition.RuleValidation` | Validation pipeline generating SVG facts and checking orthogonal / smooth path rules |
| `MermaidRecognition.Tests` | Fixture-driven recognition tests; gate registry (`G12`) across 9 diagram kinds |
| `MermaidRecognition.Layout.Tests` | Layout-specific render tests (fan-out, fan-in, decision ports, edge labels, subgraph) |
| `md2docx` | Legacy Markdown to DOCX conversion CLI |
| `MDConverter` | Main DOCX output host; routes to native SVG render or legacy Naiad fallback |

---

## Supported Diagram Kinds

Flowchart, Sequence, Class, State, Entity-Relationship (ER), Gantt, Pie, Mindmap, GitGraph, and the native **enterprise** diagram (ArchiMate 3.2 notation; all layers and aspects).

The enterprise kind is not one of the 22 Mermaid L1 kinds. It is parsed and drawn in C# (`src/MermaidRecognition/Kinds/Enterprise` and `src/MermaidRecognition.Layout/Enterprise`). Fences ` ```mermaid ` (header `enterprise`) and ` ```enterprise ` are both accepted. See [docs/enterprise/DESIGN-STUDY.md](docs/enterprise/DESIGN-STUDY.md). The planned 4.0 version covers **all** ArchiMate 4 domains (not a subset): [docs/enterprise-archimate-4-plan.md](docs/enterprise-archimate-4-plan.md).

ArchiMate® is a registered trademark of The Open Group. This project is not affiliated with or endorsed by The Open Group.

---

## Recognition Architecture

```
Mermaid source text
        |
  MermaidRecognition  (kind detect, config, declarations, vertices, edges, groups)
        |
  MermaidRecognition.Layout  (Sugiyama layout; edge routing; label placement)
        |
  SVG renderer  (native path) --or-- Naiad adapter (legacy fallback)
        |
  MDConverter  ->  DOCX (via OpenXml)
```

Key contract: every diagram produces `NodeContract[]`, `EdgeContract[]`, `GroupContract[]` annotated with shape, style, accessibility metadata, and routing geometry.

---

## Gate System (G12)

Recognition quality is tracked through gates per diagram kind:

| Gate | Meaning |
|------|---------|
| L1 | Kind detected; diagnostics; one smoke fixture |
| L2 | Core line-type catalogue transcribed |
| L3 | Full line-type catalogue + axis gates (styles, metadata, subgraphs) |
| L4 | Minimum G12 slice per kind; `*L4AxisGateTests` passing |

**Current status:** All 9 kinds at L4. Flowchart at L4/G12 depth with Sugiyama layout adopted.

---

## Third-Party Components

| Component | Origin | Licence |
|-----------|--------|---------|
| Sugiyama layout engine (`MermaidRecognition.Layout/Sugiyama/`) | [Microsoft Automatic Graph Layout (MSAGL)](https://github.com/microsoft/automatic-graph-layout) | MIT - Copyright (c) Microsoft Corporation |
| Mermaid parser/grammar (`mermaid/`) | [Mermaid 11.14.0](https://github.com/mermaid-js/mermaid) | MIT |
| Naiad renderer (`extern/Naiad/`) | Naiad (vendored with local changes; see `extern/Naiad/license.txt` and `extern/Naiad/UpstreamContributions.md`) | See `extern/Naiad/license.txt` |
| Enterprise relationship-rule data (`docs/enterprise/enterprise-rules.json`, embedded in MermaidRecognition.dll) | Archi® `relationships.xml` (3.1 commit `9ba0559c`, release_4.6.0; 3.2 commit `0469def6`, release_5.0.0) | MIT - see [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) |

See [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) and [`src/MermaidRecognition.Layout/THIRD_PARTY_NOTICES.md`](src/MermaidRecognition.Layout/THIRD_PARTY_NOTICES.md) for full attribution.

---

## Documentation

| File | Description |
|------|-------------|
| `spec.md` | Normative recognition specification |
| `status.md` | Live implementation progress, gate registry, fixture counts |
| `flow.md` | Flowchart visual regression catalogue (F01-F130+) |
| `src/MermaidRecognition/README.md` | Mermaid recogniser scope and architecture |
| `Mermaid_Specification.md` | Flowchart geometry and routing rules, including Word-first parity notes |
| `docs/enterprise-archimate-4-plan.md` | Enterprise diagram 4.0 plan: full ArchiMate 4 domain coverage, public-summary deltas only |
# MarkdownToDocX — Mermaid Diagram Recognition & DOCX Renderer

A research-grade .NET platform for **parsing, recognising, and rendering Mermaid diagram source text** into SVG and DOCX, built on a vendored Mermaid 11.14.0 parser and a Sugiyama graph-layout engine derived from Microsoft MSAGL.

**Initiated:** 2021-04-09 · **Framework:** .NET 9 / .NET 10 · **Solution:** `MDConverter.slnx`

---

## What Is This Project?

The repo has two layers:

| Layer | Location | Role |
|-------|----------|------|
| **Legacy** DOCX pipeline | `src/md2docx/`, `src/MDConverter/`, `Converter/` | Converts Markdown files to `.docx` using OpenXml; stable, production path |
| **Greenfield** Mermaid recogniser | `new_src/MermaidRecognition*/` | Full Mermaid diagram parser → recognition model → SVG render; replaces the legacy Naiad renderer |

The greenfield work (Stage 11.A / Stage 12) is the primary active effort. The legacy `md2docx` pipeline is used for day-to-day DOCX output while the native renderer matures.

---

## Solution Structure

| Project | Purpose |
|---------|---------|
| `MermaidRecognition` | Core recogniser — parses Mermaid source into a typed diagram contract (vertices, edges, groups, styles, interactions) |
| `MermaidRecognition.Layout` | Graph layout engine; Sugiyama implementation derived from Microsoft MSAGL (MIT) — see [Third-party notices](new_src/MermaidRecognition.Layout/THIRD_PARTY_NOTICES.md) |
| `MermaidRecognition.NaiadAdapter` | Bridge between the new recognition model and the legacy Naiad SVG renderer |
| `MermaidRecognition.RuleValidation` | Validation pipeline generating SVG facts and checking orthogonal / smooth path rules |
| `MermaidRecognition.Tests` | Fixture-driven recognition tests; gate registry (`G12`) across 9 diagram kinds |
| `MermaidRecognition.Layout.Tests` | Layout-specific render tests (fan-out, fan-in, decision ports, edge labels, subgraph) |
| `md2docx` | Legacy Markdown to DOCX conversion CLI |
| `MDConverter` | Main DOCX output host; routes to native SVG render or legacy Naiad fallback |

---

## Supported Diagram Kinds

Flowchart, Sequence, Class, State, Entity-Relationship (ER), Gantt, Pie, Mindmap, GitGraph

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
| Sugiyama layout engine (`MermaidRecognition.Layout/Sugiyama/`) | [Microsoft Automatic Graph Layout (MSAGL)](https://github.com/microsoft/automatic-graph-layout) | MIT — Copyright (c) Microsoft Corporation |
| Mermaid parser/grammar (`mermaid/`) | [Mermaid 11.14.0](https://github.com/mermaid-js/mermaid) | MIT |

See [`new_src/MermaidRecognition.Layout/THIRD_PARTY_NOTICES.md`](new_src/MermaidRecognition.Layout/THIRD_PARTY_NOTICES.md) for full attribution.

---

## Documentation

| File | Description |
|------|-------------|
| `spec.md` | Normative recognition specification |
| `status.md` | Live implementation progress, gate registry, fixture counts |
| `flow.md` | Flowchart visual regression catalogue (F01-F130+) |
| `new_src/README.md` | Greenfield recogniser scope and architecture |
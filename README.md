# MarkdownToDocX

A .NET library and command-line tool that converts Markdown files to Word `.docx` documents. Mermaid diagram fences are parsed by a vendored Mermaid 11.14.0 grammar, recognised into a typed contract model, and rendered to SVG for embedding in the document.

**Solution:** `MDConverter.slnx`  
**Target framework:** .NET 10 (`net10.0`; Windows executables use `net10.0-windows`)

---

## Features

### Markdown to DOCX

- Converts headings, lists, tables, links, images, code blocks, and fenced Mermaid diagrams into Open XML (`.docx`) using [Markdig](https://github.com/xoofx/markdig) and [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK).
- Profiles control Markdown extension defaults: `default`, `strict-commonmark`, and `docsite`.

### Mermaid recognition

Nine Mermaid diagram kinds have recognition gates at level L4: flowchart, sequence, class, state, entity-relationship, gantt, pie, mindmap, and git graph. The recogniser parses source into a typed `DiagramContract` (nodes, edges, groups, styles, and configuration). Enterprise (ArchiMate 3.2) diagrams use a dedicated parser and model.

Many additional kinds are detected at level L1 smoke only. Recognition level and renderer support are separate; see [`currentstatus.md`](docs/currentstatus.md) for the full gate registry.

### Native layout and rendering

DOCX conversion uses native SVG rendering for flowcharts/graphs, Enterprise (ArchiMate 3.2) and EIP diagrams, and pie charts.

- **Flowcharts and graphs** use native Sugiyama placement and routing, with the vendored [Naiad](extern/Naiad/) renderer as a fallback if native rendering fails.
- **Enterprise and EIP** diagrams use the native shared-grid layout, native routing, and SVG renderer. Enterprise supports ArchiMate 3.2 by default and 3.1 when selected in the source.
- **Pie charts** use a native SVG renderer.

Other recognized diagram kinds continue to use Naiad until their native renderers are available.

### Diagram types rendered in DOCX

| Diagram type | Parser | Renderer |
| --- | --- | --- |
| Flowchart / graph | MermaidRecognition | Native layout and routing; Naiad fallback |
| Enterprise (ArchiMate 3.2 / 3.1), EIP | Enterprise parser | Native shared-grid layout, routing, and SVG |
| Pie | MermaidRecognition | Native SVG |
| Sequence, class, state, ER, gantt, mindmap, git graph | MermaidRecognition | Naiad |
| Timeline, user journey, quadrant, C4, kanban, requirement, Mermaid architecture, sankey, block, packet, radar, treemap, XY chart | Naiad detect + parse | Naiad |

Kinds detected but not rendered (for example `eventmodeling`, `treeView`, `ishikawa`, `venn`, `wardley`, `info`) appear as code blocks in the DOCX.

Enterprise uses a `mermaid` code fence. Its element and relationship grammar, placement and routing hints, styling, accessibility, and layout configuration are covered in the [Enterprise syntax guide](docs/enterprise-syntax.md); allowed relationships are listed in [`enterprise-rules.json`](docs/enterprise/enterprise-rules.json). Mermaid's `architecture-beta` diagram is distinct from an ArchiMate Enterprise diagram.

---

## Requirements

| Requirement | Version / notes |
| --- | --- |
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0** (projects target `net10.0`) |
| IDE (optional) | Visual Studio 2022 or Visual Studio 2026 with the .NET desktop workload for `md2docx` and `Converter` (`net10.0-windows`, Windows Forms) |
| SkiaSharp | 3.119.4 (via `MDConverter`; rasterises SVG for DOCX embedding) |
| Svg.Skia | 5.0.0 (via `MDConverter`) |
| DocumentFormat.OpenXml | 3.5.1 |
| Markdig | 1.2.0 |
| Pidgin | (via vendored `extern/Naiad`; parser combinator library) |

Linux and macOS can build and test the libraries and most tools. The `md2docx` CLI and `Converter` GUI require Windows because they target `net10.0-windows`.

---

## Build

```powershell
dotnet restore MDConverter.slnx
dotnet build MDConverter.slnx -c Release
```

Build the core conversion library on any platform:

```bash
dotnet build src/MDConverter/MDConverter.csproj -c Release
```

On Windows, build the CLI:

```powershell
dotnet build src/md2docx/md2docx.csproj -c Release
```

### Run tests

```powershell
dotnet test tests/MermaidRecognition.Tests/MermaidRecognition.Tests.csproj -c Release
dotnet test tests/MermaidRecognition.Layout.Tests/MermaidRecognition.Layout.Tests.csproj -c Release
dotnet test tests/MermaidRecognition.RuleValidation.Tests/MermaidRecognition.RuleValidation.Tests.csproj -c Release
dotnet test tests/MermaidRecognition.NaiadAdapter.Tests/MermaidRecognition.NaiadAdapter.Tests.csproj -c Release
dotnet test tests/MDConverter.Tests/MDConverter.Tests.csproj -c Release
```

---

## Command-line usage

### `md2docx` (Windows)

Converts a Markdown file to DOCX.

```text
md2docx <input.md> [output.docx] [options]
```

When `output.docx` is omitted, the tool writes alongside the input file with the same base name and a `.docx` extension.

| Option | Description |
| --- | --- |
| `--profile <name>` | Markdown profile: `default` (default), `strict-commonmark`, or `docsite`. |
| `--use-legacy-flowchart-parser` | Parse flowcharts with the legacy Naiad parser instead of MermaidRecognition. |
| `--use-legacy-flowchart-renderer` | Render flowcharts with Naiad only (skips the native layout path). |
| `--use-flowchart-recognition` | Forces MermaidRecognition for flowcharts (kept for backwards compatibility; this is already the default). |
| `--lock-timeout-seconds <n>` | When the output file is locked, auto-cancel the overwrite prompt after `n` seconds. When omitted, the prompt waits indefinitely. |

Default flowchart behaviour: MermaidRecognition parse, native layout SVG, legacy Naiad fallback on failure.

Example:

```powershell
dotnet run --project src/md2docx/md2docx.csproj -- document.md document.docx
dotnet run --project src/md2docx/md2docx.csproj -- document.md --use-legacy-flowchart-renderer
```

### `FixtureProbe`

Renders flowchart fixtures from `flow.md` or a Markdown file to SVG/PNG under `out/FixtureProbe/`.

```text
FixtureProbe [fixture-id ...] [--all] [--out <dir>] [--png] [--svgonly]
FixtureProbe --md <markdown-file> [fixture-id ...] [--out <dir>] [--svgonly]
```

### `RuleValidationHarness`

Runs the structural rule-validation pipeline on a Markdown catalogue.

```text
RuleValidationHarness <markdown-file> <output-directory> [spec-directory]
```

The default spec directory is `Rule Specification/` at the repository root.

### `NativeCompareDocx`

Writes a side-by-side DOCX comparing legacy Naiad and native flowchart renders.

```text
NativeCompareDocx [output.docx] [work-directory]
```

When no arguments are given, output defaults to `out/r1-native-compare.docx`.

### `FlowchartLayoutFixRender`

Maintains before/after comparison images under `docs/flowchart/layout-fixes/`.

```text
FlowchartLayoutFixRender rasterize <dir>
FlowchartLayoutFixRender render-flow <dir>
FlowchartLayoutFixRender render-mmd <input-dir> <output-dir>
FlowchartLayoutFixRender refresh-after [dir]
FlowchartLayoutFixRender dump-layout <file.mmd>
FlowchartLayoutFixRender dump-gate <file.mmd>
FlowchartLayoutFixRender dump-svg-gate <file.mmd> <file.svg>
```

---

## Repository layout

| Path | Contents |
| --- | --- |
| `src/md2docx/`, `src/MDConverter/`, `src/Converter/` | DOCX conversion pipeline (CLI, library, Windows Forms GUI) |
| `src/MermaidRecognition/` | Mermaid parser and recognition model |
| `src/MermaidRecognition.Layout/` | Native flowchart layout (dagre placement, edge routing, SVG render) and adopted MSAGL Sugiyama sources |
| `src/MermaidRecognition.NaiadAdapter/` | Bridge from the recognition model to the legacy Naiad renderer |
| `src/MermaidRecognition.RuleValidation/` | Structural rule-validation pipeline |
| `tests/` | Test projects and approved regression assets |
| `tools/` | `FixtureProbe`, `NativeCompareDocx`, `RuleValidationHarness`, `FlowchartLayoutFixRender` |
| `extern/Naiad/` | Vendored Naiad SVG renderer (non-flowchart kinds and legacy comparison) |
| `mermaid/` | Vendored Mermaid 11.14.0 TypeScript reference sources |

---

## Documentation

| File | Description |
| --- | --- |
| [`docs/spec.md`](docs/spec.md) | Normative recognition and rendering specification |
| [`docs/currentstatus.md`](docs/currentstatus.md) | What is true today, open work and test results |
| [`docs/Mermaid_Specification.md`](docs/Mermaid_Specification.md) | Flowchart geometry and routing rules |
| [`src/MermaidRecognition/README.md`](src/MermaidRecognition/README.md) | Recogniser scope and architecture |
| [`src/MermaidRecognition.Layout/SUGIYAMA_ADOPTION.md`](src/MermaidRecognition.Layout/SUGIYAMA_ADOPTION.md) | Adopted MSAGL Sugiyama slice policy |
| [`docs/flowchart/layout-fixes/README.md`](docs/flowchart/layout-fixes/README.md) | Before/after images for flowchart layout changes |
| [`docs/COMMIT-MESSAGE-STYLE.md`](docs/COMMIT-MESSAGE-STYLE.md) | Commit and pull request writing rules |

`flow.md` is the flowchart visual regression catalogue. It is maintained separately and is not described here.

---

## Third-party components

| Component | Origin | Licence |
| --- | --- | --- |
| Dagre layout port (`src/MermaidRecognition.Layout/Dagre/`) | [dagre](https://github.com/dagrejs/dagre) | MIT |
| MSAGL Sugiyama slice (`src/MermaidRecognition.Layout/Sugiyama/`) | [Microsoft Automatic Graph Layout](https://github.com/microsoft/automatic-graph-layout) | MIT - Copyright (c) Microsoft Corporation |
| Mermaid grammar (`mermaid/`) | [Mermaid 11.14.0](https://github.com/mermaid-js/mermaid) | MIT |
| Naiad renderer (`extern/Naiad/`) | Vendored with local changes; see `extern/Naiad/license.txt` | See `extern/Naiad/license.txt` |

Full MSAGL attribution: [`src/MermaidRecognition.Layout/THIRD_PARTY_NOTICES.md`](src/MermaidRecognition.Layout/THIRD_PARTY_NOTICES.md).

---

## Licence

MIT Licence. Copyright © Dave Robinson.

See [`LICENSE`](LICENSE) for the full licence text.

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

Nine diagram kinds have recognition gates at level L4 (flowchart, sequence, class, state, entity-relationship, gantt, pie, mindmap, and git graph). The recogniser detects diagram kind, parses source into `DiagramContract` (nodes, edges, groups, styles, and configuration), and feeds the render pipeline.

Many additional kinds are detected at level L1 smoke only. See [`status.md`](status.md) for the full gate registry.

### Flowchart layout and routing (native path)

Flowcharts are the default native render path in DOCX output:

1. **Placement** - a C# port of [dagre](https://github.com/dagrejs/dagre) in `src/MermaidRecognition.Layout/Dagre/` places nodes and subgraphs the same way Mermaid does (merged in PR #4).
2. **Routing** - edges follow layout waypoints as smooth `curveBasis` curves; aligned ports draw as a single straight segment. Decision diamonds use the diagram standard width at twice the height (PR #7). Retry loops and similar special cases use rounded orthogonal paths where the router requires them.
3. **Fallback** - when native render fails, the vendored [Naiad](extern/Naiad/) renderer draws the flowchart instead.

Non-flowchart kinds still render through Naiad until per-kind native adapters exist.

### Diagram types rendered in DOCX

| Kind | Parser | Renderer |
| --- | --- | --- |
| Flowchart / graph | MermaidRecognition (default) | Native layout (default) or Naiad |
| Sequence, class, state, ER | MermaidRecognition | Naiad |
| Gantt, pie, mindmap, git graph | MermaidRecognition | Naiad |
| Timeline, user journey, quadrant, C4, kanban, requirement, architecture, sankey, block, packet, radar, treemap, XY chart | Naiad detect + parse | Naiad |

Kinds detected but not rendered (for example `eventmodeling`, `treeView`, `ishikawa`, `venn`, `wardley`, `info`) appear as code blocks in the DOCX.

**In progress:** [PR #8](https://github.com/VaderConsulting/MarkdownToDocX/pull/8) adds a shared grid snap and title-band padding for subgraph frames. It is not merged on `master` yet.

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
| [`spec.md`](spec.md) | Normative recognition and rendering specification |
| [`status.md`](status.md) | Implementation progress, gate registry, and test summary |
| [`parity-roadmap.md`](parity-roadmap.md) | Recognition parity plan for started diagram kinds |
| [`Mermaid_Specification.md`](Mermaid_Specification.md) | Flowchart geometry and routing rules |
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

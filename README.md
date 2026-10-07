# MarkdownToDocX - Mermaid diagram recognition and DOCX renderer

A .NET platform for parsing, recognising, and rendering Mermaid diagram source text into SVG and DOCX. The engine uses a vendored Mermaid 11.14.0 parser and a native Sugiyama graph-layout implementation derived from Microsoft MSAGL.

**Initiated:** 2021-04-09  
**Framework:** .NET 10  
**Solution:** `MDConverter.slnx`

## Licence

MIT Licence. Copyright (c) Dave Robinson.

See [`LICENSE`](LICENSE) for the full text.

---

## What this project does

The repository has two layers:

| Layer | Location | Role |
|-------|----------|------|
| DOCX pipeline | `src/md2docx/`, `src/MDConverter/`, `src/Converter/` | Converts Markdown files to `.docx` using OpenXml |
| Mermaid recogniser and renderer | `src/MermaidRecognition*/` | Mermaid parser to recognition model to native layout and SVG render |

**Flowcharts** (`flowchart` / `graph`) use the native Sugiyama layout in `src/MermaidRecognition.Layout` with curved edge routing (`curveBasis` and corridor paths). **All other supported diagram kinds** still render through the vendored Naiad renderer in `extern/Naiad` until native adapters exist for each kind.

**In progress (not on `master`):** grid-based layout and rounded-corner routing for dense diagrams are under development on open branches (`cursor/grid-layout-pass-c72f`, `cursor/many-to-many-routing-3834`).

There is **no native AOT publish profile** in the solution today. Builds are standard .NET SDK builds.

---

## Supported diagram kinds

Recognition and DOCX rendering cover these nine kinds at gate **L4** (minimum parser parity slice):

| Kind | Recognition | Default render path |
|------|-------------|---------------------|
| Flowchart / graph | `MermaidRecognition` | Native Sugiyama SVG (Naiad fallback) |
| Sequence | `MermaidRecognition` | Naiad |
| Class | `MermaidRecognition` | Naiad |
| State | `MermaidRecognition` | Naiad |
| Entity-Relationship (ER) | `MermaidRecognition` | Naiad |
| Gantt | `MermaidRecognition` | Naiad |
| Pie | `MermaidRecognition` | Naiad |
| Mindmap | `MermaidRecognition` | Naiad |
| GitGraph | `MermaidRecognition` | Naiad |

Additional Mermaid kinds are detected at L1 smoke level but do not yet have full recognition or render paths. See [`status.md`](status.md) for the gate registry.

---

## Requirements

| Requirement | Version / notes |
|-------------|-----------------|
| .NET SDK | **10.0** (projects target `net10.0` or `net10.0-windows`) |
| Visual Studio | **2022** or **2026** on Windows for `md2docx` and `Converter` (WinForms hosts) |
| SkiaSharp | **3.119.4** (via `MDConverter` - rasterises SVG to PNG for DOCX embedding) |
| Svg.Skia | **5.0.0** (SVG parsing for SkiaSharp rasterisation) |
| DocumentFormat.OpenXml | **3.5.1** |
| Markdig | **1.2.0** |

Linux and macOS can build and test the cross-platform library projects (`MermaidRecognition`, `MermaidRecognition.Layout`, `MDConverter`). The WinForms executables (`md2docx`, `Converter`) require Windows.

---

## Build

```powershell
dotnet restore MDConverter.slnx
dotnet build MDConverter.slnx
```

Build individual projects:

```powershell
dotnet build src/MermaidRecognition/MermaidRecognition.csproj
dotnet build src/MermaidRecognition.Layout/MermaidRecognition.Layout.csproj
dotnet build src/MDConverter/MDConverter.csproj
```

Run tests:

```powershell
dotnet test tests/MermaidRecognition.Tests/MermaidRecognition.Tests.csproj
dotnet test tests/MermaidRecognition.Layout.Tests/MermaidRecognition.Layout.Tests.csproj
dotnet test tests/MermaidRecognition.RuleValidation.Tests/MermaidRecognition.RuleValidation.Tests.csproj
dotnet test tests/MermaidRecognition.NaiadAdapter.Tests/MermaidRecognition.NaiadAdapter.Tests.csproj
dotnet test tests/MDConverter.Tests/MDConverter.Tests.csproj
```

---

## Command-line usage

### md2docx

Simple Markdown-to-DOCX conversion (Windows GUI host with CLI entry point):

```text
md2docx <input.md> [output.docx] [options]
```

When `output.docx` is omitted, the output is written alongside the input with a `.docx` extension.

| Option | Purpose |
|--------|---------|
| `--profile <name>` | Options profile (`default`, `strict-commonmark`, `docsite`) |
| `--use-flowchart-recognition` | Forces `MermaidRecognition` for flowcharts (default behaviour; kept for compatibility) |
| `--use-legacy-flowchart-parser` | Forces the legacy Naiad flowchart parser (comparison only) |
| `--use-legacy-flowchart-renderer` | Skips native Sugiyama and uses legacy Naiad SVG render |
| `--lock-timeout-seconds <n>` | Auto-cancel file-lock prompt after *n* seconds |

Default flowchart path: `MermaidRecognition` parse, native Sugiyama SVG, legacy Naiad fallback on failure.

### Converter.exe

Headless and batch conversion (Windows). Run `Converter.exe -h` for full help.

**Batch convert** (writes `<name>.docx` beside each source):

```text
Converter.exe -in *.md
Converter.exe -in .\docs
Converter.exe -in chapter1.md -in chapter2.md
```

**Single file to DOCX:**

```text
Converter.exe -in <file.md> -out <file.docx>
Converter.exe -in <file.md> -docx <file.docx>
```

**Export Mermaid diagram to PNG and/or SVG** (one input file):

```text
Converter.exe -in <source> -png <file.png>
Converter.exe -in <source> -svg <file.svg>
Converter.exe -in <source> -png <file.png> -svg <file.svg>
```

Input may be a `.md` / `.markdown` file with a ` ```mermaid ` fence, or a raw `.mmd` / text file. For multiple fences in one Markdown file, add `-index N` (1 = first block).

**Flags:**

| Long | Short | Purpose |
|------|-------|---------|
| `-in` | `-i` | Input path (file, glob, or folder; repeatable) |
| `-out` | `-o` | Output DOCX for a single input |
| `-docx` | `-d` | Output DOCX for a single input |
| `-png` | `-p` | Output PNG (Mermaid export) |
| `-svg` | `-s` | Output SVG (Mermaid export) |
| `-index` | `-n` | Mermaid block index for export (default 1) |
| `-h`, `-help`, `--help`, `/?` | | Show help |

---

## Repository layout

| Folder | Contents |
|--------|----------|
| `src/` | Product code: DOCX converter (`md2docx`, `MDConverter`, `Converter` GUI) and Mermaid engine (`MermaidRecognition*`) |
| `tests/` | Test projects and `FlowchartRegression/approved` (approved SVG for `flow.md` fixtures) |
| `tools/` | `FixtureProbe`, `NativeCompareDocx`, `RuleValidationHarness`, and maintenance scripts |
| `extern/Naiad/` | Vendored Naiad renderer for non-flowchart diagrams |
| `mermaid/` | Vendored Mermaid 11.14.0 TypeScript source (reference for tracing behaviour) |

---

## Solution structure

| Project | Purpose |
|---------|---------|
| `MermaidRecognition` | Core recogniser - parses Mermaid source into a typed diagram contract |
| `MermaidRecognition.Layout` | Sugiyama layout engine (MSAGL-derived, MIT) and flowchart SVG render |
| `MermaidRecognition.NaiadAdapter` | Bridge between the recognition model and the legacy Naiad SVG renderer |
| `MermaidRecognition.RuleValidation` | Validation pipeline for flowchart routing rules |
| `MermaidRecognition.Tests` | Fixture-driven recognition tests; gate registry (G12) across nine diagram kinds |
| `MermaidRecognition.Layout.Tests` | Layout render tests (fan-out, fan-in, decision ports, edge labels, subgraph) |
| `md2docx` | Markdown to DOCX conversion CLI (Windows) |
| `MDConverter` | Main DOCX output host; routes to native SVG render or legacy Naiad fallback |
| `Converter` | Windows GUI and headless CLI for batch conversion and diagram export |

---

## Architecture

```text
Mermaid source text
        |
  MermaidRecognition  (kind detect, config, declarations, vertices, edges, groups)
        |
  MermaidRecognition.Layout  (Sugiyama layout; edge routing; label placement)  [flowcharts]
        |
  SVG renderer  (native path) --or-- Naiad adapter (legacy fallback / other kinds)
        |
  MDConverter  ->  DOCX (via OpenXml)
```

Every diagram produces `NodeContract[]`, `EdgeContract[]`, and `GroupContract[]` annotated with shape, style, accessibility metadata, and routing geometry.

---

## Third-party components

| Component | Origin | Licence |
|-----------|--------|---------|
| Sugiyama layout (`src/MermaidRecognition.Layout/Sugiyama/`) | [Microsoft Automatic Graph Layout (MSAGL)](https://github.com/microsoft/automatic-graph-layout) | MIT - Copyright (c) Microsoft Corporation |
| Mermaid parser (`mermaid/`) | [Mermaid 11.14.0](https://github.com/mermaid-js/mermaid) | MIT |
| Naiad renderer (`extern/Naiad/`) | Naiad (vendored; see `extern/Naiad/license.txt`) | See `extern/Naiad/license.txt` |

See [`src/MermaidRecognition.Layout/THIRD_PARTY_NOTICES.md`](src/MermaidRecognition.Layout/THIRD_PARTY_NOTICES.md) for full MSAGL attribution.

---

## Documentation

| File | Description |
|------|-------------|
| [`spec.md`](spec.md) | Normative recognition specification |
| [`status.md`](status.md) | Implementation progress, gate registry, fixture counts |
| [`flow.md`](flow.md) | Flowchart visual regression catalogue |
| [`src/MermaidRecognition/README.md`](src/MermaidRecognition/README.md) | Mermaid recogniser scope and architecture |
| [`Mermaid_Specification.md`](Mermaid_Specification.md) | Flowchart geometry and routing rules |
| [`src/MermaidRecognition.Layout/SUGIYAMA_ADOPTION.md`](src/MermaidRecognition.Layout/SUGIYAMA_ADOPTION.md) | MSAGL adoption notes |

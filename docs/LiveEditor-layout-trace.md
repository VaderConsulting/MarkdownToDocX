# LiveEditor layout trace

How diagram layout runs today and where work is duplicated.

## End-to-end preview (enterprise)

1. `MainForm.QueueRender` debounces via `RenderTimer`, then `StartRender`.
2. `RenderWorker` (background thread):
   - **One** `EnterpriseValidator.Analyze` (parse + validate) when the source is enterprise.
   - `DiagramMetadata.BuildFromAnalysis` (no second parse).
   - `PreviewCanvas.Load` with the same `EnterpriseAnalysisResult`.
3. `MermaidPreviewRender.TryRenderAnalyzed`:
   - `EnterpriseSvgRenderer.Render(document)` (layout + edge routing + SVG build).
   - `DocxWriter.TryRasterizeSvgToPreviewPng` (scaled raster for the picture).
4. UI thread: `ApplyPrepared` builds `PreviewHoverIndex` from SVG + **one** `EnterpriseParser.ParseDocument` for hit-test metadata (parse only, no layout).

Non-enterprise diagrams still use `DiagramMetadata.Build` plus `MermaidDiagramExport.TryRenderPreviewPng` (parse/layout inside the native or Naiad path).

## Inside `EnterpriseSvgRenderer.Render`

| Step | Stage name | Layout? |
|------|------------|---------|
| Geometry cache hit | (none) | No full layout; styles only |
| `EnterpriseLayoutEngine.Layout` | `enterprise-shared-flowchart-layout` | **Yes** (Sugiyama / grid via `FlowchartSugiyamaLayoutEngine`) |
| `EnterpriseFlowchartEdgeRouter.Route` | `enterprise-edge-routing` | Reuses routing context when present (no second Sugiyama) |
| SVG assembly | `enterprise-svg-build` | No |

`EnterpriseLayoutEngine` calls `TryBuildRoutingContext` **once** per render and keeps the context on `EnterpriseLayoutResult` so routing does not run Sugiyama again.

## Azure Resource Graph import

1. `AzureResourceGraphInterop.TryImportFile` maps CSV to Mermaid (no layout).
2. **One** `EnterpriseValidator.Analyze` on the generated source before returning.
3. `SetEditorContent` queues a single coalesced preview render (same path as above).

Import no longer calls `ParseDocument` separately from validation.

## Render scheduling

`MainForm` keeps **one** background render worker. New edits set `_PendingRenderSource`; when a run finishes, the worker drains the latest pending source **once** instead of stacking parallel `Task.Run` jobs that each pay for a full layout.

## Measuring

Use the **large** built-in enterprise sample (not `enterprise.md` catalogue ids):

- **LiveEditor UI:** **Samples → Enterprise → Clauses and relationships (Complete)** (source title `All options`; defined in `src/LiveEditor/Editor/SampleCatalog.cs`).
- **Bench (same source as the menu):**

```powershell
dotnet run -c Release --project tools/LayoutCatalogueBench/LayoutCatalogueBench.csproj -- @liveeditor-enterprise-complete
```

Writes `docs/bench/liveeditor-enterprise-complete.jsonl` with `Milliseconds` and `LayoutMilliseconds`. Optional args: output path, per-diagram cap ms (default 90000).

- **In-app:** status line and `DiagramMetadata` progress while that sample renders.
- **Stage breakdown:** `EnterpriseSvgRenderer.StageTimingObserver` and `EnterpriseFlowchartEdgeRouter.StageTimingObserver` (see `docs/LiveEditor-render-chokepoints.md`, measured on this sample).

For hash-gated catalogue regression, keep using `tools/LayoutCatalogueBench` on `enterprise.md` / `flow.md` / `EIP.md` separately.

### Landgate (Azure import path, optional)

Imports `in/Landgate.csv` with enterprise defaults, then times the same preview SVG pipeline:

```powershell
dotnet run -c Release --project tools/LayoutCatalogueBench/LayoutCatalogueBench.csproj -- @landgate-enterprise-import
```

Writes `docs/bench/landgate-enterprise-import.jsonl` and `landgate-enterprise-import-stages.json`. Default cap 600000 ms. Not in CI.

### Stage breakdown

Both `@liveeditor-enterprise-complete` and `@landgate-enterprise-import` write a `*-stages.json` file (per-stage milliseconds from layout observers).

## Preview raster (screen scale)

LiveEditor preview uses **`LiveEditorPreviewRasterScale` (1×)** in `DocxWriter.TryRasterizeSvgToPreviewPng`, still capped by `PreviewMaxRasterPixels`. Word/DOCX export keeps **4×** `MermaidRasterScale`. `PreviewCanvas.LastPreviewPngBytes` avoids re-encoding PNG on the UI thread for export.

## Further engine work

- **Phase 5:** reference completion cache in `SearchCellAssignmentsCore` (whole-route and reference-completion paths); final repair already caches via `BaseCompletions` / `CandidateCompletions`.
- **4.12 worklist:** deferred until parity-safe dirty-node pass (`FlowchartBendRemovingPortRepair.Apply`); see DR-06 in `docs/currentstatus.md`.
- **View layers:** `docs/view-layers-roadmap.md` (filter before layout, VL-5).

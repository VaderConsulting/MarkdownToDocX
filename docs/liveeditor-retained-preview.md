# LiveEditor retained Enterprise preview

AF (`IncrementalDiagramRouting`) now connects Enterprise grid source edits to `DiagramSourceUpdateService` after analysis in the single LiveEditor worker. AF, AG and AH remain independent and default off. Ordinary AF-off, legacy-renderer and explicit open-layout previews use the existing rendering pipeline. Open mode shows an advisory explaining the retained-mode limit.

## Requests and publication

Each queued edit immediately advances the observed source revision and cancels older work, before debounce. An immutable worker request captures source, generation, editor text version, layout options, renderer and the document-session owner. The worker coalesces pending requests without starting overlapping layout passes. UI publication checks all current ownership/revision facts; engine publication additionally checks the accepted-layout revision.

The worker analyses once, then prepares metadata-only, redraw or local-routing updates. A successful candidate supplies detached source/geometry for drawing before acceptance. SVG generation, preview rasterisation, bitmap decoding and matching source hit-test/error metadata all complete before the engine transaction is published on the UI thread. A discarded/failed drawing does not accept its source revision. PNG/SVG preview copy and export use the displayed accepted image; SVG export cannot run initial layout when AF has no accepted drawing.

The retained state contains model/cell regions, routes, hints, identities and accepted source metadata. Every accepted edit generates a fresh SVG/PNG. The last successful image remains available for display/export while new text is invalid; it is not the geometry authority for routing.

## Failures and rebuilds

Invalid text, ambiguous identity edits and rejected supported local operations preserve typed text and the last accepted picture. The banner identifies the preview as stale and gives the reason, even when the ordinary keep-last-good-preview switch is off. The next valid revision compares with the last accepted model. A failed local transaction cannot become initial placement or whole-diagram routing.

Unsupported structural changes, labels/fonts, Enterprise title/configuration and other geometry-affecting presentation changes request a separately identified rebuild. A successful rebuild reports its reason in the preview advisory. Failed rebuild candidates leave the prior accepted state available. Switching documents, options or renderer permanently retires the incompatible session. The AF-only **View > Auto layout** command also retires it and starts fresh; ordinary Refresh keeps the accepted session.

`EnterpriseLayoutEngine.FromGeometry`, the supplied-layout routing context and the Enterprise drawing builder consume accepted geometry without entering the initial-layout entry point. Retained drawing bypasses the ordinary geometry-SVG cache. A fixed layout-to-SVG frame prevents recentering unrelated components; negative drawing extents expand the viewBox instead of translating model geometry.

## View and editing

Hit testing accounts for viewBox origin. Supported redraws preserve the model-to-screen scale and camera position across changed extents and preview raster downscaling. A fitted view becomes a fixed camera on the first supported edit so it does not automatically refit on every keystroke. **Fit preview** explicitly recomputes the view. These view changes do not run layout.

Existing text-editor undo/redo re-enters the same source path. GUI node dragging and relationship/port command adapters remain pending. Source node insertion/removal, grouped moves/refitting, local label resizing, self-loop construction and exact port-slot ordering retain the shared service's documented limits. Unsupported source changes may rebuild; supported local failures stay rejected.

## Verification

Focused LiveEditor regressions:

```powershell
dotnet test tests/LiveEditor.Tests/LiveEditor.Tests.csproj --no-restore --filter 'FullyQualifiedName~RetainedEnterprisePreviewTests|FullyQualifiedName~LayoutFeatureGateTests|FullyQualifiedName~LayoutSettingsTests|FullyQualifiedName~PreviewCanvasModelTests|FullyQualifiedName~PreviewNodePlacementTests' --verbosity minimal -m:1 -nodeReuse:false
```

Shared-engine regressions use the filter in [diagram-source-updates.md](diagram-source-updates.md), plus `FullyQualifiedName~EnterpriseRetainedDrawingTests`.

Coverage includes zero initial-layout stages during candidate drawing, independent-component SVG geometry preservation, comment/colour route counts, typed hint dependency sets, rejected moves, invalid-source recovery, abandoned drawings, duplicate/competing/stale candidates, source undo/redo, rapid edits, document/flag switches, explicit Auto layout, AF-off output equivalence, AF/AG/AH combinations, open-mode fallback and camera preservation from both fitted and scrolled views. Camera tests also change viewBox origin and raster scale. Image-decoding failures are checked before publication.

Worker integration tests create a real window handle and pump WinForms events without calling `MainForm.Show()`: an attempted full-show test hung in the editor's show/focus path in this environment. Camera tests use a shown plain preview host. No manual gesture/full-window visual review or large-diagram latency/memory claim is made.

Final results and any baseline failures are recorded in [the implementation plan](incremental-diagram-layout-plan.md). Local logs are `out/retained-preview-tests.log`, `out/retained-worker-layout-tests.log` and `out/retained-preview-baseline.log`.

# Incremental diagram editing and readable layout

Status: implementation in progress. Updated 28 October 2026. AF supports grid-snapped GUI node drops through the shared source update worker, with one undoable placement-hint edit per release. NW/NE/SE/SW hints now allow independent row/column gaps; relationships retain orthogonal segments and bends. Supported source edits draw accepted geometry without initial layout; failed edits preserve typed text and show a stale-preview reason. Relationship endpoint gestures remain pending and must snap to actual connection points. All experiments remain default off.

Progress: the plan is committed in `9ab0801`; default-off AF/AG/AH switches, request options, scoped Enterprise propagation and geometry-cache isolation are implemented in `700dbcb`. The benchmark and initial baseline are committed in `ad3ac08`. The first AH experiment adds single-layer and vertical rank-row candidates and target-width selection. At a 700 px target the same-layer fan changes from 1006 x 1396 to 586 x 736, with bends 4 to 13, crossings 0 and hard findings 0. The Complete sample remains 2146 x 1666 because existing group/hint/rank constraints still limit its candidates. Width presets, final-label-aware selection, broader constrained packing and incremental editor integration remain unfinished. See `tools/DiagramLayoutBench/README.md` and the baseline/results JSON under `docs/bench`.

Current foundation status: empty-canvas construction, explicit node additions/moves, relationship additions/reconnects, port-side changes, dependency-scoped routing, candidate isolation and revision-checked publication are implemented in the shared engine. Stable parsed relationship identities now feed a shared source update service with metadata/paint/local/rebuild classification, typed node-hint replacement and atomic source/geometry publication. LiveEditor uses that service for AF-enabled Enterprise grid source edits and ungrouped node drops, redraws fresh SVG/PNG from accepted geometry, and exports the same accepted image. Grouped edits, node insertion through source, removal, local label resizing, self-loop construction, exact port-slot control and GUI relationship/port command adapters remain pending. Text and node-command undo/redo enter the shared source path. Large-diagram speed and memory have not yet been benchmarked. AF/AG/AH remain separate and default off.

Validation: the latest selection passes 355 cases: 74 parser, 126 shared-engine and 155 LiveEditor cases. This includes 36 new quadrant-placement and discovered-bug regressions. The five historical hover/selection failures recorded in earlier steps are now fixed and pass, along with the two help/IntelliSense failures found during this step. Details and current API limits are recorded in [retained-layout-session.md](retained-layout-session.md), [diagram-source-updates.md](diagram-source-updates.md) and [diagonal-placement-hints.md](diagonal-placement-hints.md). Phase checkboxes remain open until their full acceptance criteria are met.

Next work: connect GUI relationship/port editing through undoable source commands and the integrated source path. Endpoint feedback and release must snap to eligible actual connection points; exact slot retention requires more than the existing side-only hint API. Node dragging is enabled only for current AF Enterprise grid previews and ungrouped regular elements, using cardinal or quadrant relative hints with independent whole-cell gaps. Absolute cell pins, source node insertion/removal, grouped moves and local label resizing still need engine operations; large-diagram measurements and AH follow-ups remain pending.

## Working rule

After every step, commit the work and update this plan with the current status. Record what was completed, the validation results and what remains before proceeding to the next step. Include the status update in that step's commit.

Fix concrete bugs found while implementing and validating each step, including failures exposed by broader regression checks. Treat this as part of the step; record each fix and its validation instead of leaving a discovered bug merely labelled pre-existing.

## Intended outcome

Build and edit large diagrams while retaining a readable arrangement. Detaching and reconnecting a relationship should reroute its dependencies without unnecessarily moving nodes. Initial construction should account for routing quality and usable width. Problematic routes should be identifiable, with measured placement improvements available to the user.

Confirmed interaction constraint: a node drag must snap to the retained logical grid, update its declaration's placement hint and route only the dependency closure of that move. Relationship connection-point moves must snap to eligible actual connection points. Neither operation may enter initial placement or silently fall back to whole-diagram routing. Redrawing an image is separate from layout/routing work; the camera and unaffected geometry must remain stable. AF now enables a retained node gesture with snapped feedback, source-hint publication and cancellation/undo checks. The earlier full-refresh gesture prototype remains parked under `out/node-placement-prototype`.

Confirmed destination constraint: a dropped node may differ in both row and column from its previous position or its peer. NW/NE/SE/SW describe placement quadrants with independently snapped gaps, not 45-degree paths. Relationships continue to route with horizontal/vertical segments and bends.

Confirmed construction and editing constraint: a session must also start empty and accept a node or relationship at a time as items arrive from the script, toolbar drops or a future context menu. GUI edits and script edits must use the same operations for node movement, relationship reconnects and connection-point changes. Existing completed layouts are another session entry point, not a prerequisite. Fast local routing and exact preservation of unrelated geometry are the goal; a canvas redraw must not turn each item insertion into whole-diagram construction.

Foundation progress: the internal shared `DiagramLayoutSession` now supports an empty canvas or capture of authoritative cell geometry, isolated node additions/translations, relationship additions/reconnects, and endpoint-side hints. Publication checks document-session ownership, source revision and accepted-layout revision. Candidates retain independent routes, cell grids, guides and caches, expand routing dependencies for changed obstacles and redistributed terminals, and reject new legality findings or route-budget failures without a global fallback. The API and current limits are described in `retained-layout-session.md`. Source reconciliation, hint replacement, snapped node-drop commands and retained-geometry preview/export run through AF's source worker. Grouped edits and GUI relationship/port commands remain unfinished. The CORE checkboxes below remain open because this slice does not satisfy their complete acceptance criteria.

Start with native Enterprise diagrams in LiveEditor. Put reusable routing and placement services in the shared layout library. Extend to other supported native diagram paths after the Enterprise path is verified; existing fallback renderers retain their current behaviour.

## Feature guards

LiveEditor Settings exposes the three switches on a Diagram layout tab, alongside the target width. Choices are saved with the session and applied on OK before refreshing the preview; Cancel preserves the current session and running options. Existing sessions retain default-off experiments and a 1200 px target. AF enables retained Enterprise grid source updates; AG reserves its upcoming construction/diagnostic operations; AH enables the initial width experiment. The dialog keeps control construction and static layout in its Designer partial and is checked at normal, 150% and 200% font sizes.

All behaviour changes remain behind default-off A/B flags. The proposed names are currently available in `LiveEditorFeatureGate`.

| Flag | A: off | B: on |
| --- | --- | --- |
| `AF_IncrementalDiagramRouting` | Current editor render/layout behaviour | Retained editor layout state, incremental relationship operations and subsequent move support |
| `AG_RouteAwareDiagramConstruction` | Current initial construction and optimisation | Route diagnostics, suggested placement improvements and subsequently route-aware construction |
| `AH_WidthConstrainedLayout` | Current width/wrapping behaviour | Explicit target width and readability-aware candidate selection |

Expose these as explicit per-request/session layout options. LiveEditor gates select options; shared engine code must not depend on LiveEditor. Partition caches by options, document identity and relevant revisions. Shared services can be used by AG or AH without requiring AF to be enabled. Capture flag settings once per operation. Switching flags invalidates incompatible session state.

Existing behaviour, scoring and rules remain unchanged when flags are off. New controls and commands are visible only when their owning flag is enabled. Preview and export must carry the intended layout options and committed geometry consistently; batch conversion keeps its existing defaults.

## Phase 0 - Evidence and acceptance criteria

- [ ] **BASE-1:** Capture current output and timings for small fixtures, parallel relationships, self-loops, nested groups, constrained/hinted layouts, disconnected components and representative large diagrams. Include the Complete Enterprise sample and the existing large import fixture when available.
- [ ] **BASE-2:** Trace why the wide examples remain wide: declared direction, explicit row constraints, group sizes, band/chain/component wrapping candidates, routing corridors and final rendered extents. Record formed and rejected wrap candidates and rejection reasons.
- [ ] **BASE-3:** Record rendered width/height, label size at a declared fit-to-width viewport, bends, crossings, legality findings, layout time and route-rebuild count. For edits, also record affected-route count, moved nodes and fallback reason.
- [ ] **BASE-4:** Establish baseline geometry/findings hashes, known failures and practical latency/readability targets before changing algorithms. Preserve existing independent checkers.
- [x] **BASE-5:** Add the three default-off flags and explicit option plumbing, with a clear entry point for selecting each experimental mode.

Acceptance: reproducible baseline, explained width causes for selected examples, and flag-off output unchanged. Performance and quality claims use those same fixtures and settings.

## Phase 1 - Readable width experiment (AH)

This phase can proceed before incremental editing and addresses the current viewing problem directly.

- [ ] **WIDTH-1:** Define a layout target with screen, portrait page, landscape page and custom-width presets. Resolve the preset once for layout; resizing the window changes the view without automatically rearranging the graph.
- [ ] **WIDTH-2:** Generate bounded alternative row/band/component arrangements using existing wrapping machinery. Preserve explicit direction, grouping, semantic conventions, cell ownership and mandatory hints. Reserve route and label space when planning the footprint.
- [ ] **WIDTH-3:** Evaluate complete rendered extents, including external routes, labels, titles and markers. Distinguish target width from the minimum feasible width imposed by constraints.
- [ ] **WIDTH-4:** Define and document candidate ranking: legality and mandatory constraints first; width/readability and routing quality next with bounded, visible trade-offs; displacement and deterministic tie-breaking thereafter. Reuse existing bend/crossing rules. Keep any experimental width policy within AH.
- [ ] **WIDTH-5:** Report when the requested width cannot be achieved without breaking constraints. Prefer a readable fit-to-width view with vertical scrolling; retain fit-all as a view option.

Acceptance: selected wide examples show useful width/readability improvements with reported routing costs; all routes remain legal. A single oversized group or explicit row constraint produces an explained limitation. AH works with AF and AG off.

## Phase 2 - Retained layout and transactional routing foundation

- [ ] **CORE-1:** Introduce a per-document layout session owning authoritative cell regions/gaps, node and group bounds, terminals, routes, hints, dependency indexes and revision information. Keep model coordinates stable; viewport transforms remain separate.
- [ ] **CORE-2:** Add stable editor identities for relationships across reconnects, insertions, deletions and source reparsing, including parallel relationships. Current generated edge IDs contain endpoints and creation order and cannot serve as persistent edit identities. Define reconciliation for ambiguous text edits.
- [ ] **CORE-3:** Extract routing against supplied geometry from initial placement. Reuse existing route builders, hint handling, legality checks, scoped completion and scoring. Routing-only operations must not invoke repairs that move unrelated nodes.
- [ ] **CORE-4:** Compute affected-route closure from changed relationships, old/new endpoint inventories, old/new obstacle and title bounds, group boundaries, changed terminals and route/track dependencies. Re-evaluate interactions with frozen routes and expand the set when a dependency changes. Bound expansion and detect cycles.
- [ ] **CORE-5:** Implement isolated candidate state, validation, atomic publication and exact rollback. Existing repair snapshots contain mutable references; establish an ownership boundary suitable for editor transactions. Publish model, guides, labels and caches consistently.
- [ ] **CORE-6:** Integrate with the existing single background render worker using document revisions. Reject stale results. Define recovery from invalid source text and when a session must be rebuilt.
- [ ] **CORE-7:** Route the local dependency set and expand it only when a demonstrated dependency requires it. If that cannot produce a legal result, restore the committed edit and report the reason. Never fall back to routing unaffected relationships. Explicit Auto layout remains a separate operation.
- [ ] **CORE-8:** Render committed geometry without entering initial layout again. Full SVG/raster redraw is acceptable initially; partial painting is a separate optimisation.
- [ ] **CORE-9:** Use one update service for script edits and interactive commands. After the existing debounce and Enterprise analysis, compare the new model with the session's last committed model, reconcile identities, classify the change and submit a transaction. Dragging writes an undoable source hint and enters this same path; it must not trigger a second render or routing pass.
- [ ] **CORE-10:** Classify changes by their actual geometry effects. Whitespace/comments and source-line changes refresh metadata without placement or routing; colour-only changes redraw committed geometry. Placement hints, supported relationship additions/removals/reconnects, and geometry-affecting label changes use the corresponding local operation and dependency closure. Direction, grouping, diagram-kind or other unsupported structural changes require a separately identified session rebuild, with a recorded reason. A failed supported local operation must not silently become a full rebuild.
- [ ] **CORE-11:** Preserve typed text when validation or a local update fails. Retain the last valid committed geometry, identify the preview as stale and expose the reason; do not roll back the user's script. Diff the next valid revision against the last committed model. Treat undo/redo, paste and source changes made by editor commands through the same revision checks. Loading another document, changing layout options and explicit Auto layout are distinct session-invalidation operations.

Acceptance: a routing-only request performs no initial placement, preserves geometry outside its declared change set, and cannot publish partial or stale state. Shared services are usable without enabling an unrelated UI flag.

First proof case: retain a diagram containing two independent components, move one node through a placement hint, and require identical node/route geometry hashes for the other component. Instrument placement calls and route rebuild IDs; placement count must be zero and rebuilt routes must equal the reported dependency set. Add a moved-obstacle case, shared-terminal closure, rejected-move rollback and source-revision tests before enabling the gesture.

Script-edit integration point (implemented for AF-enabled Enterprise grid previews): `MainForm.Editor_TextChanged` calls `QueueRender`; the debounce timer calls `StartRender`, and `RenderWorkerPass` analyses Enterprise source before `Preview.Load` enters full rendering. Model comparison and update selection now run after analysis in that worker, before the ordinary full-render entry point. Immutable requests capture source revision, text version, options, renderer and document-session ownership. Supported updates draw a detached candidate and publish source metadata, retained geometry and the preview only for the current revision. AF-off previews retain the ordinary render path.

Additional acceptance: typing a placement hint and generating the identical hint by dragging produce the same committed geometry and affected-route set. Comment edits rebuild zero routes; colour edits perform no layout; label-size changes include newly obstructed routes. Temporary invalid text, undo/redo, rapid edits and document/flag switches cannot replace newer geometry with an older result. Verify the full-layout entry point is never called for supported local changes.

## Phase 3 - Relationship editing (AF)

- [ ] **EDIT-1:** Add reconnect/add/remove relationship commands using stable identities and transactional routing. Reconnect source and target independently; preserve unaffected node positions.
- [ ] **EDIT-2:** Implement endpoint detach as transient interaction state. Snap feedback and release to eligible actual connection points. Keep the committed relationship while dragging; reconnect commits once, cancel restores exactly. Do not write an incomplete relationship into Mermaid source. Extend side-only hints if necessary to retain the selected exact point.
- [ ] **EDIT-3:** Synchronise source, selection/hit-test metadata, route geometry and labels in one undoable command. Define no-op drops and invalid targets, including model rules for loops and parallel connections.
- [ ] **EDIT-4:** Avoid neighbour terminal redistribution where existing allocations remain legal. Include neighbours in the affected set whenever allocation must change.
- [ ] **EDIT-5:** Confirm copy/export/save use the committed arrangement. Define reload behaviour explicitly: persist required geometry through a documented hint/view-state representation if arrangement round-trip is promised.

Acceptance: detach/reconnect/cancel, add/delete, undo/redo and rapid consecutive edits behave consistently; unrelated nodes and routes retain exact geometry. Unsupported text edits retain a documented recovery path.

## Phase 4 - Route diagnostics and placement suggestions (AG)

- [ ] **QUALITY-1:** Attach route diagnostics to stable identities: bend count, crossing partners, shared tracks, detour and legality failures. Separate invalid routes, investigation candidates and verified improvements. Show measurements; a high count alone does not prove avoidable routing.
- [ ] **QUALITY-2:** Add a bounded node-move evaluation API using authoritative cell assignments. Trial source, target and relevant obstructions; try path/terminal changes before expanding the placement search. Respect locked nodes, hints and containing groups.
- [ ] **QUALITY-3:** Reroute affected dependencies for each trial and score interactions with the whole diagram. Reuse existing scoring and independent validation; avoid double-counting crossings. Apply AH's width policy when enabled.
- [ ] **QUALITY-4:** Offer Suggest placement improvement with a visual preview and before/after totals for bends, crossings, width and displacement. Commit a selected suggestion as one undoable command; cancel leaves the diagram unchanged.
- [ ] **QUALITY-5:** Prevent oscillation with strict improvement, visited-state tracking, deterministic ordering and a bounded search budget. Report when no improvement is found within that budget.

Acceptance: a local improvement cannot hide damage elsewhere. Verified suggestions match displayed measurements and preserve constraints. A valid but unavoidable detour remains explainable without being labelled invalid.

## Phase 5 - Route-aware initial construction (AG)

- [ ] **BUILD-1:** First experiment: existing initial placement, followed by incremental route evaluation and targeted placement improvement. Compare against the Phase 0 baseline before replacing the initial producer.
- [ ] **BUILD-2:** Build a complete topology/constraint plan before incremental construction. Define deterministic placement/routing order for groups, connected components, high-degree nodes and cycles, with routing capacity reserved for later relationships.
- [ ] **BUILD-3:** Introduce staged placement and routing using the same transaction and candidate-evaluation services as editing. When AH is enabled, generate candidates within the chosen width target.
- [ ] **BUILD-4:** Allow bounded backtracking or small neighbourhood moves when early choices block later routes. Keep the best complete legal result; provide a recorded fallback to existing construction if the budget is exhausted before a complete result exists.
- [ ] **BUILD-5:** Evaluate construction-order sensitivity and repeated-run determinism. Report overall quality, readable footprint, runtime and fallback frequency.

Acceptance: every declared drawable relationship is present and legal; existing semantic constraints are preserved. Demonstrate a useful improvement on the agreed large fixtures before considering default enablement.

## Phase 6 - Future direct node movement (AF)

- [ ] **MOVE-1:** Reuse the move evaluation API for drag/drop, mandatory retained-grid snapping and explicit locked positions. Define group moves, containment changes and resize rules separately.
- [ ] **MOVE-2:** Use lightweight drag feedback; perform authoritative routing on drop initially. Coalesce updates and reject stale results if continuous routing is added later.
- [ ] **MOVE-3:** Handle incident routes and unrelated routes obstructed by the move, including group/title changes. Commit source/view-state and geometry together with undo/redo and defined persistence.

Acceptance: dragging does not move unrelated nodes, lose relationships or leave stale guides. Invalid drops restore the original arrangement.

## Validation and rollout

- [ ] Verify flag-off equivalence against the captured baseline, including output, diagnostics and known failure identities.
- [ ] Test flag combinations: AF alone, AG alone, AH alone and combinations; include toggling flags and document switching to detect cache/state leakage.
- [ ] Check route inventory, legality, labels, hints, cell ownership and group constraints independently of the optimiser's own score.
- [ ] Assert exact geometry outside each affected set, exact cancellation/rollback, undo/redo consistency and rejection of stale results.
- [ ] Benchmark small edits separately from initial construction; report latency distributions, fallback reasons and route-rebuild counts. Do not claim incremental speed from a full-redraw timing alone.
- [ ] Review representative rendered diagrams visually at the declared viewing scale, alongside numerical width and routing measurements.
- [ ] Keep each flag off by default until its agreed checks pass. Enabling a default is a separate reviewed change.

## Recommended implementation order

1. Phase 0: baseline, width diagnosis and feature guards.
2. Phase 1: width/readability experiment addressing the present large-diagram problem.
3. Phase 2: shared retained-state and routing foundation.
4. Phase 3: relationship editing.
5. Phase 4: diagnostics and measured placement suggestions.
6. Phase 5: route-aware construction, beginning with current placement plus targeted improvement.
7. Phase 6: direct node movement when the interaction is required.

Phases 1 and 2 can be developed independently after the baseline. Construction and editor operations share engine services, while their A/B behaviour remains independently selectable.

## Starting points in the codebase

| Area | Existing code |
| --- | --- |
| LiveEditor flags | `src/LiveEditor/LiveEditorFeatureGate.cs` |
| Render scheduling and preview | `src/LiveEditor/MainForm.cs`, `src/LiveEditor/PreviewCanvas.cs`, `src/MDConverter/MermaidPreviewRender.cs` |
| Enterprise render/layout boundary | `src/MermaidRecognition.Layout/Enterprise/EnterpriseSvgRenderer.cs`, `EnterpriseLayoutEngine.cs`, `EnterpriseFlowchartEdgeRouter.cs` |
| Relationship ID mapping | `src/MermaidRecognition.Layout/Enterprise/EnterpriseFlowchartContractMapper.cs` |
| Scoped route completion | `src/MermaidRecognition.Layout/DiagramGridLayoutCellIncrementalRoutes.cs` |
| Candidate transactions and node movement | `src/MermaidRecognition.Layout/DiagramGridLayoutCellRepairs.cs`, `FlowchartCellRepair.cs` |
| Final validation and repair | `src/MermaidRecognition.Layout/DiagramGridLayoutFinalRepair.cs` |
| Width and wrapping | `src/MermaidRecognition.Layout/DiagramGridLayoutBandWraps.cs`, `Enterprise/EnterpriseBandPlan.cs`, chain/component wrapping helpers |
| Scoring and context | `src/MermaidRecognition.Layout/FlowchartLayoutScore.cs`, `FlowchartRenderContext.cs` |
| Existing measurements | `docs/LiveEditor-layout-trace.md`, `docs/LiveEditor-render-chokepoints.md`, `tools/LayoutCatalogueBench` |

## Completed step - source relationship identities (28 October 2026)

Implemented the engine-side CORE-2 reconciliation service, `DiagramRelationshipIdentityMap`. Immutable per-document maps preserve relationship identities across reparsing, insertion, deletion, reordering, unique reconnects and uniquely attributable label/port edits. Explicit identities take precedence; generated parser IDs and source positions are metadata. Distinct parallel relationships remain separate. Unchanged indistinguishable parallel declarations use documented occurrence order; ambiguous multiplicity/content/reconnect changes return a reason and no successor. Rejected or discarded candidates leave the accepted map and its identity allocation untouched. The API does not mutate parsed contracts or call placement/routing.

Validation: all 28 new identity cases and 39 selected retained-session, layout-option and width regression cases pass (67 total). See [diagram-source-identities.md](diagram-source-identities.md) for matching/publication rules, the test command and remaining integration limits; the local test log is `out/identity-map-regressions.log`. Feature defaults and existing rendering entry points are unchanged.

Remaining next step: apply reconciled bindings in the shared source update service, classify source changes, resolve typed placement hints to retained cell transactions, and publish identities, source metadata and accepted geometry together under document/source/layout revision checks. LiveEditor integration, GUI gestures, undo/redo and export are pending. CORE-2 and the other CORE checkboxes remain open until their complete editor-facing acceptance criteria pass.

## Completed step - shared source updates (28 October 2026)

Implemented `DiagramSourceUpdateService` over the retained routing engine. It applies reconciled stable IDs to detached routing/render contracts, classifies metadata-only, paint-only, supported local routing and rebuild-required changes, and prepares one isolated transaction for mixed hint/relationship edits. Typed relative node hints replace previous hint locks and move their existing hint dependents in whole cells. Hint removal retains geometry; occupied cells, missing peers, cycles, semantic constraints and routing-budget failures reject the update without a placement or whole-diagram routing fallback. Relationship additions, reconnects and routing-hint changes share this path. Unsupported structural/label edits report an explicit rebuild requirement; the service does not execute a rebuild.

Publication atomically accepts source text, complete detached source/presentation metadata, stable identity bindings, routes, guides and geometry under document/source/accepted-layout revision checks. Metadata/colour edits rebuild zero routes. Snapshots bind guides to the matching full source model. Invalid intermediate text only advances the observed revision; the next valid revision compares against the last accepted state. Explicit invalidation permanently retires an instance, so a document/options switch or Auto layout can create a fresh session while old workers remain unable to publish.

Validation: all 29 new source-update cases and 67 selected identity, retained-session, layout-option and width regression cases pass (96 total). Tests include exact typed-hint/direct-cell equivalence, zero initial placement, independent-component geometry preservation, moved obstacles, mixed-edit rollback, ambiguity, skipped invalid revisions, detached metadata, competing workers, replacement-session ownership and explicit rebuild classification for changed plateau timeline constraints. The test command and limits are documented in [diagram-source-updates.md](diagram-source-updates.md); output is in `out/source-update-regressions.log`. The build emits one pre-existing nullable warning in `CompactLayoutMetricTests.cs:119`.

Remaining next step: integrate the shared service into the LiveEditor Enterprise worker and draw/export its accepted snapshot, preserving source text, stale-preview reasons and viewport state. Node insertion through source, removal, grouped edits, local label resizing, self-loop construction and broader hint semantics remain explicit limits. GUI gestures, undo/redo, width follow-ups and large-diagram measurements remain pending. CORE checkboxes stay open until their full editor-facing acceptance criteria pass; all feature defaults remain unchanged.

## Completed step - LiveEditor retained preview worker (28 October 2026)

Integrated `DiagramSourceUpdateService` after Enterprise analysis in the single LiveEditor worker, behind AF. Immutable queued requests capture source/text revisions, options, renderer and document-session ownership. Every queued edit immediately supersedes older work, including edits still waiting for debounce. Supported comments, colours, typed placement hints and relationship/port edits prepare retained transactions; candidate SVG/raster/hit-test metadata are completed before revision-checked UI publication. Supported failures preserve the user's script and last accepted picture/export bytes and show a stale-preview reason. The next valid revision still compares with the accepted model. Structural changes use an explicitly identified rebuild; failed local operations never fall back to it.

Added a shared Enterprise supplied-geometry drawing boundary which never enters initial placement. Each accepted edit generates fresh SVG and PNG; the retained state is model/cell/route geometry and source identities, not an SVG scene. A fixed model-to-SVG frame and viewBox-aware hit testing keep unaffected components stable, including drawings extending left/up. The preview preserves model scale and camera position across changed extents and raster downscaling; a fitted view becomes a fixed camera after a supported edit, and Fit explicitly recomputes the view. PNG/SVG copy/export uses the displayed accepted image. Documents, options/renderer changes and the AF-only Auto layout command retire/recreate incompatible sessions. Explicit open-layout Enterprise diagrams keep their ordinary preview with an advisory.

Validation: all 53 selected LiveEditor cases and 97 shared-engine cases pass (150 total), including 21 new LiveEditor cases and the new supplied-geometry drawing case. Existing nullable warnings remain in the Azure mapper, syntax-highlighter call and `CompactLayoutMetricTests.cs`. Broader hover/selection checks found five failures; all five reproduce in a comparison build using unchanged HEAD source: `ANodeWinsWhenThePointerIsInsideIt`, `NodeHoverNamesTheElementType`, `RenderedPreviewNamesNodesAndRelationships`, `FlowchartEdgesAreSelectableAndNodesAreNot` and `OtherDiagramKindsDoNotThrow`. Current source was restored and the focused suites rebuilt after that comparison. Logs are `out/retained-preview-tests.log`, `out/retained-worker-layout-tests.log` and `out/retained-preview-baseline.log`. Worker tests use an actual window handle and the WinForms message pump; a full `MainForm.Show()` attempt hung in the editor's show/focus path in this environment, so this step makes no manual full-window interaction claim. See [liveeditor-retained-preview.md](liveeditor-retained-preview.md) for lifecycle, verification and remaining limits.

Remaining next step: GUI node movement and relationship/port commands must write one undoable source edit and enter the same worker path. Existing text-editor undo/redo is covered; gesture cancellation, GUI command undo/redo, source node insertion/removal, grouped edits, local label sizing, self-loop construction and exact port slots remain pending. Large-diagram latency/memory and AH width follow-ups remain unmeasured. Phase checkboxes remain open until their full acceptance criteria pass; AF/AG/AH defaults remain off.

## Completed step - snapped GUI node movement (28 October 2026)

Connected AF node dragging to the retained Enterprise source worker. A current accepted grid preview permits a single ungrouped regular element to move. Pointer feedback projects a detached authoritative grid, including expanded gaps, negative coordinates and multi-cell spans. The dashed outline snaps to the nearest whole-cell placement expressible by a relative hint against a same-layer peer, excluding hint cycles. Projection performs no placement, routing or redraw; zoom/raster/viewBox transforms preserve the grab offset. Offsets are logical cells, replacing the old prototype's pixel estimate on the shipped path.

A release writes one declaration replacement into the existing RichEdit undo history; `TextChanged` alone submits the worker request. Typed and GUI hints produce identical accepted SVG and affected/rebuilt route sets, with no initial layout or global fallback. Unrelated geometry and selected node identity survive publication. Escape, capture/focus loss, view/selection changes, source/document/options changes and release outside the viewport cancel transient feedback. Clicks and drops at the unchanged snapped position submit no edit. A failed local drop preserves its source hint and last accepted picture/export with the same stale-preview reason as a failed typed hint; one Undo restores the prior source through the shared path.

Validation: all 73 selected LiveEditor cases and 97 shared-engine cases pass (170 total). The 20 new cases cover whole-cell snapping in four directions with expanded gaps and multi-cell regions; Escape/capture/view/selection/host cancellation, changed eligibility, outside releases and one-shot drop publication; zoom/raster/viewBox/grab-offset projection; no-op and stale commands; AF-off/open-layout guards; rejected-drop recovery; and GUI/typed equivalence, selection, one render submission and undo/redo without initial layout. The shared-engine run reused its existing built regression assembly (`--no-build --no-restore`); this slice changes only its test friend-assembly declaration. The editor suite rebuilt current sources. Existing nullable warnings remain in the syntax highlighter and Azure mapper. Logs: `out/node-move-regressions.log` and `out/node-move-layout-tests.log`. Gesture adapter tests use a shown plain preview host with native capture and invoke its mouse handlers without moving the system pointer; worker tests use the existing hidden MainForm handle/message-pump harness. No manual full-editor interaction or large-diagram performance claim is made.

Remaining next step: implement GUI relationship/port commands with transient detach/cancel, one undoable source edit and snapping to eligible actual connection points. The existing endpoint-side API cannot yet promise exact slot persistence. Node movement currently requires a suitable peer and a placement representable by one cardinal relative hint; arbitrary two-axis locked positions, grouped moves, containment/resize, source node insertion/removal, local label sizing and self-loop construction remain pending. Large-diagram latency/memory and AH follow-ups remain unmeasured. CORE, EDIT and MOVE checkboxes stay open until their complete acceptance criteria pass; AF/AG/AH defaults remain off.

## Completed follow-up - reliable drag initiation and visible feedback (28 October 2026)

Reproduced a drag-start failure with native Windows mouse messages: selecting a node can change the floating details panel and refit/reposition the picture. The picture handler then hit-tested the old coordinate again and missed the node that was originally pressed. Both picture and viewport paths now retain the original hit and rebase the grab point through screen coordinates after selection. Native move/release messages preserve the intended model delta and publish exactly once.

Also reproduced invisible same-cell feedback with a raster comparison: the dashed outline overlapped the solid selected-node border while the pointer moved within the original snapped cell. Its four-screen-pixel visual margin now makes feedback distinct without changing snapped or committed geometry.

Validation: all 78 selected LiveEditor cases pass, including five new cases (picture/viewport native mouse messages with and without selection-driven refitting, plus same-cell raster visibility). Before the fix, the picture/refit reproduction failed to start a drag and the raster check found zero changed feedback pixels outside the selection border. Logs: `out/node-drag-feedback-repro.log`, `out/node-drag-outline-repro.log` and `out/node-drag-feedback-regressions.log`. Production sources were rebuilt for the first successful 76-case pass; the final 78-case pass rebuilt the test project with `-p:BuildProjectReferences=false` against those unchanged production binaries. Existing nullable warnings remain. The shared engine is unchanged, so its preceding 97-case run was not repeated. No manual full-editor review is claimed.

Remaining scope and default-off flags are unchanged: grouped nodes and nodes without an eligible peer cannot yet be dragged; GUI relationship/connection-point gestures remain the next planned slice.

## Completed follow-up - canvas-wide drag feedback (28 October 2026)

Reproduced the screenshot's clipped outline: capture and release already allowed movement outside the bitmap, but feedback was painted only inside the picture control. The viewport now paints the same transformed outline in the surrounding canvas while the picture paints its portion. Both surfaces invalidate on movement/cancellation, so outlines crossing the image boundary remain continuous and old feedback clears. Native capture remains on the picture; a release anywhere inside the viewport can commit, while releases outside the viewport still cancel. Accepted SVG/PNG bytes and the camera are unchanged during feedback; this does not enlarge or regenerate the image.

Validation: all 81 selected LiveEditor cases pass. Three new native mouse/raster cases verify all four outline sides fully outside a fixed/fitted image and straddling its edge, correct drop coordinates, one release publication inside empty canvas space, parent invalidation, clearing feedback and unchanged accepted SVG/PNG bytes. All three failed against the previous binaries. Logs: `out/node-drag-canvas-repro.log`, `out/node-drag-canvas-tests.log`, `out/node-drag-canvas-build.log` and `out/node-drag-canvas-regressions.log`. The production project and tests were rebuilt with `-p:BuildProjectReferences=false` against unchanged dependency binaries; existing nullable warnings remain. The engine was not changed or retested. No manual full-editor review is claimed.

Remaining scope, feature defaults and the next relationship/connection-point editing step are unchanged.

## Completed step - quadrant placement and discovered editor fixes (28 October 2026)

Added NW/NE/SE/SW relative placement hints throughout Enterprise parsing, the placement contract, retained source reconciliation and initial placement. Optional `+rows +columns` gaps are independent logical-cell counts; omission means zero, and one gap applies to both axes. For example, `SE peer +2 +5` places the owner below/right of the peer with two empty rows and five empty columns between their occupied regions. Drag candidates now evaluate four cardinal and four quadrant placements, snapping the two axes independently against authoritative regions/gaps instead of restricting destinations to cardinal lines or 45-degree rays. Relationship routing is unchanged and remains orthogonal, with bends.

The second gap survives detached model copies, source classification, validation and Archi property/source round-trips. Changing only that gap still routes locally. GUI and typed hints produce identical accepted geometry and affected-route sets; selection, negative viewBox extents, one render submission and one-step undo/redo are verified in all four quadrants. Completion/help expose the syntax. Initial layout honours the same hints on reparse/reload. See [diagonal-placement-hints.md](diagonal-placement-hints.md).

Fixed bugs exposed by broader checks instead of leaving them as baseline failures. Element help no longer includes unrelated relationship lists, hover help omits insertion-only examples/instructions, and label parameter help removes filler. Identifier help now finds earlier declarations even when the parser merges repeated same-type declarations and overwrites their line number. Node hover uses its complete relationship story. Non-Enterprise rendered hit indexes bind only existing drawn edges to source metadata: they no longer add unsupported/invisible node selections or duplicate a rendered edge that already has a line number. Source-only item construction still retains its full inventory. The five hover/selection failures recorded in the retained-worker step now pass. Bug fixing is explicitly part of the plan's working rule.

Validation: all 355 selected cases pass (74 parser, 126 shared-engine, 155 LiveEditor), including 36 new cases. Coverage includes four-quadrant parsing/defaults/independent gaps; initial placement and local updates with orthogonal-only route segments; hint dependents, a horizontal-gap-only edit, detached copies, independent-component preservation and collision rollback; expanded grid gaps, multi-cell spans, GUI/typed equivalence and undo/redo; completion and Archi round-trips; unique/repeated identifier help and no phantom rendered objects. A full production dependency build succeeded; subsequent editor/test rebuilds used `--no-restore -p:BuildProjectReferences=false` against those current dependency binaries. Existing nullable warnings remain in the Azure mapper, syntax highlighter and `CompactLayoutMetricTests.cs`. Logs: `out/diagonal-placement-build.log`, `out/diagonal-editor-build.log`, `out/diagonal-parser-tests.log`, `out/diagonal-layout-regressions.log`, `out/diagonal-editor-regressions.log` and `out/diagonal-discovered-bugs-tests.log`. The editor run includes real popup/preview host windows and the existing retained-worker handle/message-pump harness; no manual full-editor review or large-diagram performance claim is made.

Remaining next step: GUI relationship/connection-point commands with snapping to eligible actual points, transient detach/cancel and one undoable source edit. Exact endpoint slots, grouped gestures, absolute cell pins, lone-node placement without a peer, source node insertion/removal, local label sizing and self-loop construction remain pending. Large-diagram measurements and AH follow-ups remain open. AF/AG/AH defaults remain off, and phase checkboxes remain open until their full acceptance criteria pass.

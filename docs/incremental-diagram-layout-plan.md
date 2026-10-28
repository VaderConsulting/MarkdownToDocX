# Incremental diagram editing and readable layout

Status: proposed implementation plan. 28 October 2026.

Progress: the plan is committed in `9ab0801`; default-off AF/AG/AH switches, request options, scoped Enterprise propagation and geometry-cache isolation are implemented in `700dbcb`. The benchmark and initial baseline are committed in `ad3ac08`. The first AH experiment adds single-layer and vertical rank-row candidates and target-width selection. At a 700 px target the same-layer fan changes from 1006 x 1396 to 586 x 736, with bends 4 to 13, crossings 0 and hard findings 0. The Complete sample remains 2146 x 1666 because existing group/hint/rank constraints still limit its candidates. Width presets, final-label-aware selection, broader constrained packing and incremental edit operations remain unfinished. See `tools/DiagramLayoutBench/README.md` and the baseline/results JSON under `docs/bench`.

## Intended outcome

Build and edit large diagrams while retaining a readable arrangement. Detaching and reconnecting a relationship should reroute its dependencies without unnecessarily moving nodes. Initial construction should account for routing quality and usable width. Problematic routes should be identifiable, with measured placement improvements available to the user.

Start with native Enterprise diagrams in LiveEditor. Put reusable routing and placement services in the shared layout library. Extend to other supported native diagram paths after the Enterprise path is verified; existing fallback renderers retain their current behaviour.

## Feature guards

LiveEditor Settings exposes the three switches on a Diagram layout tab, alongside the target width. Choices are saved with the session and applied on OK before refreshing the preview; Cancel preserves the current session and running options. Existing sessions retain default-off experiments and a 1200 px target. AF and AG currently reserve modes for upcoming operations; AH enables the initial width experiment. The dialog keeps control construction and static layout in its Designer partial and is checked at normal, 150% and 200% font sizes.

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
- [ ] **CORE-7:** Define fallback: local route set, expanded dependency set, then all relationships at fixed node positions. If routing still fails, restore the committed edit and give a specific diagnostic. Explicit Auto layout remains available for placement changes.
- [ ] **CORE-8:** Render committed geometry without entering initial layout again. Full SVG/raster redraw is acceptable initially; partial painting is a separate optimisation.

Acceptance: a routing-only request performs no initial placement, preserves geometry outside its declared change set, and cannot publish partial or stale state. Shared services are usable without enabling an unrelated UI flag.

## Phase 3 - Relationship editing (AF)

- [ ] **EDIT-1:** Add reconnect/add/remove relationship commands using stable identities and transactional routing. Reconnect source and target independently; preserve unaffected node positions.
- [ ] **EDIT-2:** Implement endpoint detach as transient interaction state. Keep the committed relationship while dragging; reconnect commits once, cancel restores exactly. Do not write an incomplete relationship into Mermaid source.
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

- [ ] **MOVE-1:** Reuse the move evaluation API for drag/drop, grid snapping and explicit locked positions. Define group moves, containment changes and resize rules separately.
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

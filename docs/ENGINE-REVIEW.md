# MarkdownToDocX diagram layout and routing engine: performance review

- **Repo / commit reviewed:** VaderConsulting/MarkdownToDocX `master` at `2f5ff0159934aa8a1b0d135121ca444f2c56c8c5` ("Attach metadata to the diagram in the editor", 24 Oct 2026, 21:48 AWST). Fresh read-only clone on the box. Nothing was committed, pushed or posted.
- **Measurement box:** an 8-core Linux x64 machine running .NET SDK 10.0.401, a Release build, warm and in-process. Only the SVG step was timed (`MermaidDiagramExport.TryRenderPreviewSvg` in the committed harness), with a 90 s cap per diagram. Treat catalogue numbers as relative evidence, not as the ARM laptop timings used for day-to-day development.
- **Harness:** `tools/LayoutCatalogueBench` and `docs/bench` (see `docs/bench/README.md`). It records render time and an SVG SHA-256 for each in-gate catalogue diagram so before/after output identity can be checked.
- **Profiler:** `dotnet-trace` sampled thread-time profiles of A104, A115 and A142, analysed from the speedscope evented export (`bench/prof/*.speedscope.json`, using agg.py, self.py, callers.py, nest.py and path.py).

## Apply order (least disruption, least repetition)

**Starting point.** The centre-out 15 px port-rule work lands first. Every step below is measured against the catalogue output *after* that work. The item numbers are the ranked recommendations in §5.

**Code facts that set this order** (checked at `2f5ff015`):

- **The grid already has an immutable geometry version.**
  - `FlowchartLogicalGrid.Snapshot` is an immutable record of the cell regions and the column and row gaps. `Capture`, `TryCommit` and `Restore` swap whole snapshots in and out.
  - Item 2's grid cache can therefore live on the `Snapshot` itself, computed lazily on first use and never invalidated. It does not need a counter.
- **Bounds and routes have no version, so items 3 and 5 need one built once.**
  - Node and frame bounds and routes are mutable and live on `FlowchartLayoutResult`.
  - Items 3 and 5 both need a "geometry version". Build that counter once, bumped on every route or bounds write, and let both items use it.
  - The title-band cache (`FlowchartFrameTitleBandHelper.Get`) already keys on the bounds dictionary instance, so item 3 can reuse that pattern.
- **Some files are touched by several items, so their edits are grouped:**

  | File | Items | How they are grouped |
  |---|---|---|
  | `FlowchartFrameTitleBandHelper.cs` | 1 (top-port test order) and 3 (`FindEnteredBandIds` allocations) | Both done in Phase 1 |
  | `FlowchartPaddedVisibilityRouter.cs` | 4 (`PaddedBox` hashing) and 12 (index reuse, `AddVertex`, LINQ) | Both done in Phase 1 |
  | `DiagramGridLayoutModelChecks.cs` | 3 (10 `Build` calls), 5 (memoised `Check`/`Measure`), 12 (string and JSON keys), 10 | 3, 5 and 12 together in Phase 2; 10 is unavoidable later because it changes output |
  | `DiagramGridLayoutCellRepairs.cs` and `DiagramGridLayoutFinalRepair.cs` | 5, 12, 10 | Same split as above |

- **Items 8 and 9 depend on Phase 2.** Both call full `Check`/`Measure` and `FlowchartBranchRouteOrder.Intersections` on every candidate, so they should use Phase 2's memoised versions rather than build their own.
- **The GC and the hot getters distort every profile.** GC is 27–44 % of time and the computed getters are about 19 % of A104, so the per-call costs of 8, 9 and 10 are not known reliably until Phases 1 and 2 are done.

### Phase 0: freeze the baseline (no engine change)

- **What:** after the port rule lands, record:
  - the catalogue SVG hashes and times for enterprise.md, flow.md and EIP.md;
  - A142 with a long cap;
  - profiles of A104, A115 and A142.
- **Why first:** every later "identical" check compares against this single baseline.

### Phase 1: identical output; local hot-spot fixes with no shared infrastructure

- **Items:**
  - Item 1: a per-contract membership cache, and testing geometry before `IsInsideGroup` in `IsLegalTopPortCrossing`.
  - The `FindEnteredBandIds` part of item 3, because it is in the same file as item 1.
  - Item 2: lazy prefix sums stored on `FlowchartLogicalGrid.Snapshot`.
  - Item 4: `PaddedBox` equality and hashing, plus deterministic result order.
  - The router part of item 12, because it is in the same file as item 4.
- **Why here:** these are the hot spots and allocation sources that distort every later profile. None of them depends on another.
- **Check:** each item's commit gives byte-identical catalogue output.
- **Afterwards:** re-time the catalogue and **re-profile** A104, A115 and A142.

### Phase 2: identical output; build the geometry version once and use it everywhere

- **Items, in this order:**
  1. Add the version counter on `FlowchartLayoutResult`, bumped by every route or bounds write.
  2. Item 3: cached bands at the 10 `DiagramGridLayoutModelChecks.cs` sites.
  3. Item 5: `FindBackEdges` and `BuildGroupById` cached per contract; `Check` and `Measure` memoised per version.
  4. The cache-key part of item 12: hashes instead of JSON or string keys, in the same files.
  5. A spatial index in `TryTranslateInternalRun`.
- **Depends on:** Phase 1, which makes the remaining costs visible.
- **Check:** byte-identical output. A stale cache shows up as a hash difference, so do not relax the check.
- **Afterwards:** re-time the catalogue.

### Phase 3: identical output; make the two worst passes incremental

- **Items, in this order:**
  1. Item 8: 4.12 gets a dirty-node worklist, lazy scoring, obstacles and bands built once per pass, and cached reference intersections.
  2. Item 9: `FlowchartDistinctTrackRepair` builds its path cache once, uses a segment spatial index, and scores local differences.
- **Depends on:** Phase 2's memoised `Check`/`Measure` and the version counter.
- **Check:** byte-identical output, with the same candidate order and tie-breaks.
- **Afterwards:** re-time the catalogue and A142, and **re-profile**. Item 9 runs inside every nested completion, so it shrinks item 10's cost.

### Decision gate (no code)

Compare the Phase 3 timings with the 10 s target and the A142 watchdog. Then decide which of items 6, 10 and 11 are still needed, and how big they need to be.

### Phase 4: re-baseline 1; search limits (explicit approval, rules.yaml first)

- **Items:**
  - Item 7: a deadline that returns the best legal layout so far.
  - Item 6: budgets that scale with diagram size, plus a "patience" stop after k candidates in a row fail to improve.
- **Why together:** both are limits in the rule 5.10 section of rules.yaml, so they share one rule edit, one regeneration and one baseline capture.
- **Why after Phase 3:** measured on the faster engine, the budgets can probably stay higher.
- **Check:** this is a new baseline. Report the bend and crossing changes per diagram for approval.
- **Afterwards:** re-time on the ARM development laptop and record the worst case in rules.yaml, as the rules require.

### Phase 5: re-baseline 2; restructure candidate evaluation (explicit approval, rules.yaml first if 5.10 wording changes)

- **Items:**
  - Item 10: no full completion nested inside a candidate's completion.
  - Item 11: rank wrap widths using placement alone, fully route the top k, and adopt the winner without re-running it.
- **Why together:** both change how candidates are judged in `CompleteReferenceCellCandidate` and `ApplyWithRoutedBands`, so one re-baseline covers both.
- **Optional extra:** running wrap candidates in parallel with copied hint state can follow as an identical-output step against this new baseline.
- **Afterwards:** re-time on the laptop.

### Phase 6 (optional): one-pass segment ordering

- **What:** replace trial-and-error track separation with ELK/libavoid-style one-pass segment ordering and nudging.
- **When:** only if the shared-track faults or times still justify it. It is a separate re-baseline that needs approval.

### What can be dropped or shrunk

- **Item 6:** if Phases 1–3 bring A142 within the watchdog and the catalogue near the target, shrink it to the patience stop alone and keep budget 48.
  - Evidence: A104 and A105 give identical output at budget 16, so the extra candidates there are wasted.
- **Item 7:** keep it regardless; it is a cheap safety net.
- **Item 10:** may shrink to a simple depth limit if item 9 makes nested completions cheap. A104 has 43.5 % of its time at depth 2 today.
- **Item 11:** drop its parallel step if ranking alone is enough.
- **Item 12:** the router part may not matter after item 4, since the router is 3.5–12 % today. Re-profiling after Phase 1 decides.
- **Phase 6:** probably unnecessary.

### Paste-ready instruction (phased)

> Apply these engine speed fixes to MarkdownToDocX in this order, starting only after the port-rule work has landed. Fix the engine, never the checkers, and make one commit per numbered step.
>
> **Phase 0: baseline**
> 1. Record the catalogue SVG hashes and times for enterprise.md, flow.md and EIP.md, plus A142 with a long cap. Use this as the only baseline until Phase 4.
>
> **Phase 1: local hot spots**
>
> 2. Cache group membership per contract (a `ConditionalWeakTable<DiagramContract, …>`) inside `FlowchartSubgraphMembershipHelper.FindInnermostGroupId` and `BuildGroupById`. In `FlowchartFrameTitleBandHelper.IsLegalTopPortCrossing`, run the geometry tests before `IsInsideGroup`. Remove the `HashSet`/`OrderBy`/`ToArray` allocations from `FindEnteredBandIds`.
> 3. Store lazily computed gap prefix sums on the immutable `FlowchartLogicalGrid.Snapshot`, so `Extent`, `Position`, `Measure` and `Origin` (and therefore the `GridNode` and `FlowchartCellNode` coordinates) are fast and allocation-free.
> 4. In `FlowchartPaddedVisibilityRouter.cs`, give `PaddedBox` `IEquatable`/`GetHashCode` and keep query result order deterministic. Reuse `PaddedBoxSpatialIndex` per obstacle set, de-duplicate in `AddVertex` with a hash set, and remove LINQ from `TryGridPath`.
>
> Phase 1 must stay identical: every catalogue SVG matches the Phase 0 hashes after each step. Re-time and re-profile A104, A115 and A142.
>
> **Phase 2: one geometry version, shared caches**
>
> 5. Add one geometry version counter on `FlowchartLayoutResult`, bumped by every route or bounds write.
> 6. Replace the 10 uncached `FlowchartFrameTitleBandHelper.Build` calls in `DiagramGridLayoutModelChecks.cs` with a cache keyed on that version (or the existing `Get`).
> 7. Cache `FlowchartBackEdgeHelper.FindBackEdges` per contract, and memoise `FlowchartRouteRuleChecker.Check` and `FlowchartLayoutScore.Measure` per version.
> 8. Replace the JSON and string cache keys with 64-bit hashes: `SearchCellAssignmentsCore`, `RepairFinalFacingSides`, `FinalReferenceIdentity` and the frame-overlap loop key. Add a spatial index to `TryTranslateInternalRun`.
>
> Phase 2 must stay identical: all catalogue SVGs match Phase 0. A mismatch means a stale cache, so fix the invalidation and do not relax the check. Re-time.
>
> **Phase 3: incremental passes**
>
> 9. In `FlowchartBendRemovingPortRepair.Apply` (4.12), use a dirty-node worklist and lazy scoring, build obstacles and title bands once per pass, and cache the reference intersections in `InteractionsDoNotGrow`. Keep the candidate order, tie-breaks and rejection rules unchanged.
> 10. In `FlowchartDistinctTrackRepair.Apply`, build the path cache once per call, use a segment spatial index, and score candidates by local differences using the Phase 2 memoised `Check`/`Measure`.
>
> Phase 3 must stay identical: all catalogue SVGs match Phase 0. Re-time the catalogue and A142, re-profile, then stop and report before Phase 4.
>
> **Phase 4: only with explicit approval; rules.yaml first, then regenerate**
>
> 11. Add an overall layout deadline that returns the best legal layout found so far.
> 12. Make the search budgets scale with diagram size, with a stop after k non-improving candidates.
>
> Phase 4 is a new baseline. Report the per-diagram bend and crossing changes, then re-time on the ARM development laptop and record the worst case in rules.yaml.
>
> **Phase 5: only with explicit approval**
>
> 13. Stop running a full `ModelRouteCompletionStage.Apply` inside a candidate's completion (`ApplyFinalMoveBeforeDetour` → `SearchCellAssignmentsCore`).
> 14. In `ApplyWithRoutedBands`, rank wrap widths using placement alone, fully route only the top few, and adopt the winner without re-running it.
>
> Phase 5 is a new baseline, updated in rules.yaml first if the 5.10 wording changes.
>
> **Throughout:** F53 is protected. Never loosen, skip or rewrite a test or checker. Change no budget or limit outside Phase 4.
>
> Report back after each phase: the commits, the catalogue time before and after, the A142, A104, A115 and F153 times, the number of differing SVGs (zero for Phases 1–3), and the new top five profile entries.

**Steer or queue:** queue Phases 0–3 for straight after the port-rule run lands. Hold Phases 4–5 until they are approved after seeing the Phase 3 numbers.


## 1. Pipeline (call path)

`DocxWriter.TryRenderDiagramSvg` → `NativeDiagramRenderBridge.TryRenderSvg` → `EnterpriseRenderBridge.TryRenderEnterpriseSvg` → `EnterpriseSvgRenderer.Render/Measure` → `EnterpriseLayoutEngine.Layout/TryApplyFlowchartLayout` → `EnterpriseFlowchartEdgeRouter.TryBuildRoutingContext` → `FlowchartSugiyamaLayoutEngine.TryLayout` (dagre placement) → `DiagramGridLayout.Apply` → `ApplyWithRoutedBands` → `ApplyCore` (run once for the baseline, once per band-wrap candidate up to `repair_candidate_moves`, and once more to confirm the winner) → `CompletePublishedRoutes`.

`ApplyCore` runs these stages in order: grid and measured geometry, placement hints, automatic placement (dagre), group-frame fitting, route preparation, joint port allocation (4.7), routing hints, route construction, coupled route repair, `ModelRouteCompletionStage.Apply`, final legality, `ApplyFinalCompleteRepair`, and `FlowchartBendRemovingPortRepair.Apply` (4.12).

**The structural cost driver is nesting.** Almost every search, whether band wraps, cell moves, final repair or move-before-detour, judges a candidate by re-running the *whole* downstream pipeline on it.
- `RouteRepairStage.CompleteReferenceCellCandidate` runs the full `ModelRouteCompletionStage.Apply`, then a full `FlowchartRouteRuleChecker.Check`, then a full `FlowchartLayoutScore.Measure`.
- Inside `ModelRouteCompletionStage.Apply`, `ApplyFinalMoveBeforeDetour` → `TryMoveGroupForUnroutedLink` → `SearchCellAssignmentsCore` calls `CompleteReferenceCellCandidate` again. That runs a complete route-completion *inside* a complete route-completion.
- **Measured on A104:** 43.5 % of all samples sit at nesting depth 2 of `ModelRouteCompletionStage.Apply`, along exactly this path (`prof/nest.py`, `prof/path.py`). 87.7 % of A104 is inside `CompleteReferenceCellCandidate`.

Contemporary engines do not work this way. Graphviz dot, ELK Layered, dagre and MSAGL each run placement once and routing once, and then a single separate nudging/ordering pass. libavoid and yFiles re-route *incrementally*, touching only the affected edges ("sphere of action"), not the whole diagram per trial.

## 2. Measured timings (warm, in-process, SVG only, 8-core x64 box)

### Current master (budgets: moves 48, depth 5, lines 24)

| Source | Diagrams | Rendered | Total s | Median ms | p90 ms | Allocated GB |
|---|---:|---:|---:|---:|---:|---:|
| enterprise.md | 179 | 158 | 407.9 (A142 counted at its 90 s cap) | 104 | 2,200 | 486 |
| flow.md | 152 | 148 | 116.8 | 4.6 | 281 | 122 |
| EIP.md | 342 | 342 | 10.6 (max 0.37 s) | 4.1 | 133 | 15 |
| **All** | 673 | 648 | **535.4** (445.4 excluding A142) | | | 622 |

- Thirteen diagrams take more than 10 s, which breaks the rules.yaml 10 s target. Thirty take more than 1 s.
- The failures are expected:
  - enterprise.md A78–A94, A102, A127 and A130 are invalid-input tests.
  - flow.md F65, F70, F87 and F88 are commented-out (`%% flowchart`).
  - **A108 ("Many parallel relationships") returned "could not be rendered". I could not tell whether that is intended, so it is worth checking.**

**Slowest diagrams**

| Diagram | Time (s) | Allocated (GB) |
|---|---:|---:|
| A142 | > 90 (timeout) | — |
| F153 (160 nodes) | 60.5 | 65.2 |
| A167 | 39.9 | 69.6 |
| A115 | 38.4 | 25.6 |
| A165 | 35.0 | 31.2 |
| A104 | 32.1 | 100.9 |
| A114 | 27.5 | 44.1 |
| A163 | 24.5 | 23.8 |
| F151 | 23.1 | 18.5 |
| A113 | 23.1 | 40.2 |
| A116 | 17.3 | 35.7 |
| F152 | 14.3 | 11.0 |
| A105 | 11.5 | 25.9 |
| A110 | 9.2 | 16.5 |

Stage totals summed over the catalogue are inclusive and overlap:

| Stage | Total (s) |
|---|---:|
| enterprise-layout | 317 |
| final-complete-layout-repair | 174.5 |
| model-route-completion-and-rule-checks | 174.4 |
| bend-removing 4.12 | 55.7 |
| coupled-route-repair | 25.1 |
| grid-and-measured-geometry | 13.9 |
| group-frame-fitting | 12.9 |
| automatic-node-placement (dagre) | 2.1 |

**Dagre placement is cheap. Almost all the time goes on repair and search.**

**Noise:** the same diagram can vary by up to about ±40 % between catalogue runs. For example, A116 took 17.3 s in one run and 29.1 s in another, with identical output; run side by side, the two builds took 23.0 s and 22.3 s. Single-diagram differences below roughly 30 % are therefore not meaningful.

### A142 (the PR #33 hang)

- On this box A142 exceeds the 90 s cap.
- Under the profiler it ran for 800 s and **still had not finished the first (baseline) `ApplyCore`**. In that baseline pass alone:

| Stage | Time (s) |
|---|---:|
| grid geometry | 13.7 |
| frame fitting | 17.8 |
| coupled repair | 26.9 |
| model route completion | 124.4 |
| final complete repair | 76.7 |
| **4.12 bend-removing port reassignment** | **524.6** |

  The profiler adds overhead, so these numbers are inflated, but the proportions hold.
- The repo's own `docs/stress-watchdog-results.md` (20 Oct 2026, budget 16) measured the first 4.12 pass at 37.3 s and 11 full `ApplyCore` executions, giving 581.7 s in total.
- With the budget at 48 and up to 48 wrap candidates, each running a full `ApplyCore`, A142 cannot fit any watchdog. That is an estimate from the measured per-pass cost, not a measurement.

**Where A142's time goes (sampled, 800 s; self time is the share spent in the method itself, excluding its callees):**

| Method | Self time |
|---|---:|
| GC (`PollGC`) | 27.4 % |
| **`FlowchartSubgraphMembershipHelper.FindInnermostGroupId`** | **18.0 %**, plus string `SequenceEqual` 7.1 % (mostly called from it) |
| `DiagramGridRouteBuilder.PathIsValid` | 9.5 % |
| `FlowchartBranchRouteOrder.Intersections` | 7.7 % |
| `FlowchartFrameTitleBandHelper.FindEnteredBandIds` | 6.4 % |

| Method | Inclusive time |
|---|---:|
| `FlowchartBendRemovingPortRepair.Apply` (4.12) | 66.4 % |
| `DiagramGridRouteBuilder.BuildRoute` | 48 % |
| `TryBuildHintedOuterLaneRoute` | 30.1 % |
| `PathIsValid` | 29.3 % |
| `FindEnteredBandIds` | 28.4 % |
| `FindInnermostGroupId` | 23.1 % |
| `FlowchartFrameTitleBandHelper.Build` | 13.7 % (it rebuilds group bounds via `WithComputedGroupBounds` → `FlowchartSubgraphBoundsBuilder.BuildOrderedRects`) |

**The hang is 4.12 rebuilding routes whose validity test re-derives group membership and title bands by linear scans, inside a pass with no worklist, repeated for every wrap candidate.**

### Experiment 1: old search budgets (moves 16, depth 3, lines 12), changed only in a box copy of rules.yaml

| Source | Budget 48 | Budget 16 | SVGs identical |
|---|---:|---:|---:|
| enterprise.md | 407.9 s | 250.7 s | 119 of 158 |
| flow.md | 116.8 s | 37.4 s | 121 of 148 |
| EIP.md | 10.6 s | 4.9 s | 321 of 342 |
| **All** | **535.4 s** | **293.0 s** (−45 %) | |

Allocation fell from 622 GB to 217 GB, and diagrams over 10 s fell from 13 to 7.

**Some diagrams produced byte-identical output at both budgets, so the extra budget was pure waste for them:**

| Diagram | Budget 48 | Budget 16 |
|---|---:|---:|
| A104 | 32.1 s | 4.9 s |
| F151 | 23.1 s | 8.8 s |
| F152 | 14.3 s | 7.2 s |
| A105 | 11.5 s | 1.1 s |

The other 87 changed outputs were not quality-scored here, so this experiment does not show whether budget 48 improves them.

### Experiment 2: one line activating the existing membership cache

In `DiagramGridLayout.Apply` I added `using var FixedMembership = FlowchartSubgraphMembershipHelper.ReuseFixedMembership(Contract);`. `ReuseFixedMembership` exists but **has no callers anywhere in the code**: it is dead code.

- **All 673 SVGs were byte-identical** to master.
- Totals moved within noise: 535.4 s became 527.9 s. Per-diagram results:

| Diagram | Before | After |
|---|---:|---:|
| A115 | 38.4 s | 23.4 s (−39 %) |
| A165 | 35.0 s | 26.9 s (−23 %) |
| F153 | 60.5 s | 53.9 s (−11 %) |

- **A142, run side by side without the profiler and capped at 700 s:** both master and the experiment timed out. Their stage totals for the first (baseline) `ApplyCore` were about the same:

  | Stage | Master | Experiment |
  |---|---:|---:|
  | 4.12 | 444.3 s | 451.0 s |
  | Model completion | 111.9 s | 113.3 s |
  | Final repair | 68.8 s | 69.3 s |

  So the scoped cache does **not** reach A142's hot calls. Those calls almost certainly pass a different `DiagramContract` instance, because the cache only matches by `ReferenceEquals`. Examples include the local contracts built in `DiagramGridLayoutGroupFrames` and the enterprise-mapped contract. **The fix should be a per-contract `ConditionalWeakTable<DiagramContract, membership>` cache inside `FindInnermostGroupId` itself, not a scope.**
- **Unprofiled A142 on master:** more than 700 s, and its first core pass alone spends 444 s in 4.12.

### Experiment 3: GC settings on A104 (no code change)

| Setting | A104 time |
|---|---:|
| Default | 37.1 s |
| `DOTNET_GCgen0size=128MB` | 39.0 s |
| Server GC | 46.2 s (contaminated by a concurrent run) |

**GC tuning does not help.** The 30–44 % of time spent in GC has to be removed by allocating less.

## 3. Profiler evidence by diagram

**A104** (37 s, 103 GB allocated):

| Method | Share |
|---|---|
| GC | 31.6 % self |
| `DiagramGridLayout.TryTranslateInternalRun` | 24.7 % self |
| `FlowchartBranchRouteOrder.Intersections` | 6.4 % |
| `FlowchartLogicalGrid.Extent` / `FlowchartCellNode.get_Width` / `FlowchartLogicalGrid.Measure` / `FlowchartLogicalGrid.Position` | 5.8 / 5.7 / 3.8 / 3.7 % |
| `ValueType.GetHashCode` | 2.7 % |
| `Monitor.Enter` | 2.9 % (from `FlowchartRenderContext..ctor`) |
| `FlowchartDistinctTrackRepair.Apply` | 69 % inclusive |
| `SealDistinctTracks` | 64 % inclusive |
| `SearchCellAssignmentsCore` via `ApplyFinalMoveBeforeDetour` | 46.6 % inclusive |

**A115** (33 s, 26 GB allocated):

| Method | Share |
|---|---|
| GC | 43.8 % |
| `FindInnermostGroupId` | 7.9 % + 2 % string compare |
| `PathIsValid` | 7.0 % |
| `ValueType.GetHashCode` | 5.5 %, all from `HashSet<PaddedBox>` inside `PaddedBoxSpatialIndex.Query` |
| `FindEnteredBandIds` | 5.5 % |
| 4.12 | 47 % inclusive |
| `BuildRoute` | 42 % inclusive |

### Root causes in code

1. **Grid geometry is recomputed on every read.** `GridNode.X/Y/Width/Height` (in DiagramGridLayout.cs) delegate to `FlowchartCellNode`, which calls `FlowchartLogicalGrid.Measure/Origin`. Those use `Extent` and `Position`, and each runs a LINQ `Grown.Where(...).Sum(...)` over the sparse gap dictionary on *every* coordinate read, allocating each time. They are called inside the innermost loops (`TryTranslateInternalRun`, `Intersections`, scoring).
2. **`TryTranslateInternalRun`** (DiagramGridLayoutRouteRepair.cs) scans every placed node for each of three segments, with a new `FlowchartLayoutNodeBounds` per node and no spatial index.
3. **Group membership is found by linear scan.** `FlowchartSubgraphMembershipHelper.FindInnermostGroupId` scans `Contract.Groups` and then `Contract.Nodes` (string compares) unless `ActiveMembership` is set, and nothing ever sets it. `BuildGroupById` also allocates on each call (4.5 % of A142's GC).
4. **Title-band tests do the expensive check first.** `FlowchartFrameTitleBandHelper.IsLegalTopPortCrossing` calls `DiagramGridLayout.IsInsideGroup` twice per segment and band *before* the cheap geometry tests. `FindEnteredBandIds` then allocates a `HashSet`, `OrderBy` and `ToArray` per call.
   - `FlowchartFrameTitleBandHelper.Build` is uncached at ten call sites in DiagramGridLayoutModelChecks.cs, even though a cached `Get` exists, and each call recomputes group bounds.
5. **`PaddedBox` hashes slowly.** `PaddedBox` (FlowchartPaddedVisibilityRouter.cs, `private readonly struct PaddedBox`) has no `Equals` or `GetHashCode`, so it falls back to reflection-based `ValueType.GetHashCode`. Separately, `PaddedBoxSpatialIndex` is rebuilt on every `TryGridPath` call, `AddVertex` dedups in O(n²), and the search window doubles (`Margin *= 2`).
6. **`FlowchartDistinctTrackRepair.Apply` re-does work per route and per candidate.**
   - For each route it rebuilds the `Others` and `Paths` arrays, which is O(E²) allocation (E is the number of edges).
   - Each of up to 48 trial translations is scored with `Intersections` against every path.
   - Improving candidates get a new `FlowchartLayoutResult` and a full `Check`.
   - It opens with a full `Check` just to count rule 5.1.
   - It runs up to four times per `SealDistinctTracks`, inside every reference completion.
7. **4.12 has no worklist.** `FlowchartBendRemovingPortRepair.Apply` is a do/while loop over all nodes. For each node it eagerly runs a full `Measure`, a `CellMovementContext` and a full `Check`. It then tries up to 48 candidates, each with `BuildRoute`, `IsLegal`, `InteractionsDoNotGrow` (which recomputes the reference intersections each time) and a full `Check`. There is no dirty-node set, so one acceptance re-scans every node.
8. **Whole-layout checks repeat without memoisation.**

   | Method | Call sites | Notes |
   |---|---:|---|
   | `FlowchartRouteRuleChecker.Check` | 50 | O(E²) with a linear `FindRoute` |
   | `FlowchartLayoutScore.Measure` | 34 | |
   | `FlowchartBackEdgeHelper.FindBackEdges` | 70 | recomputed each time; 2.5–3.2 % of GC |

   Nothing is memoised per geometry version. `IncrementalContext` and `CellMovementContext` exist but are under-used.
9. **Signature and cache keys are built by JSON or string join.** Examples include `SearchCellAssignmentsCore` signatures, the `RepairFinalFacingSides` geometry key, `FinalReferenceIdentity` and the frame-overlap `while(true)` key.
10. **Band-wrap candidates run serially.** `ApplyWithRoutedBands` runs a full `ApplyCore` per wrap candidate and then once more to confirm. The candidates mutate shared hint state (`BandHintState` save and restore), which prevents running them in parallel.

## 4. Comparison with contemporary engines (checked on the web, Oct 2026)

| Engine | What is relevant here |
|---|---|
| **Graphviz dot** | Network-simplex ranking, then crossing minimisation and positioning. Every expensive loop has a named bound: `nslimit` (simplex iterations = nslimit × nodes), `searchsize` (30), `mclimit` (scales MinQuit 8 / MaxIter 24, so the search stops after 8 non-improving passes). `splines=ortho` is known to be slow and to ignore ports. |
| **ELK Layered** | Network-simplex layering and Brandes–Köpf placement. Its orthogonal routing assigns each layer gap's vertical segments to routing slots in **one pass**: it builds a conflict/crossing dependency graph (weights 1 and 16), breaks cycles and numbers the slots topologically. That is O(segments²) per gap, with no trial translations, and it respects port constraints (FIXED_SIDE/ORDER/POS), which suits a 15 px port grid. |
| **dagre** (used here for placement, and by Mermaid) | Network-simplex ranking, barycentre ordering, Brandes–Köpf; it is not an orthogonal router. **Measured here, placement is only 2.1 s of the whole catalogue.** |
| **Mermaid** | dagre by default, with ELK optional. It does no post-layout repair search at all. |
| **MSAGL RectilinearEdgeRouter** | A sparse or full orthogonal visibility graph with padding and a bend penalty, then a **separate Nudger** pass that spreads shared segments. |
| **libavoid / Adaptagrams** (Wybrow, Marriott and Stuckey, GD 2009) | An orthogonal visibility graph built by line sweep, A* on length plus bend cost (`TryGridPath` is the same idea), then `ImproveOrthogonalRoutes`: simplify → build nudging-order info → nudge shared segments apart. It is fast enough for interactive re-routing and re-routes only the connectors whose paths are invalidated. |
| **yFiles EdgeRouter** | Incremental scope ("sphere of action": only selected or affected edges), port candidates, and a **Maximal Duration / stopDuration** time limit that returns the best result found so far. |
| **Archi** | Manual bendpoints plus a simple Manhattan connection router. It does no global optimisation, so it is relevant only as the "manual hints win" model. |

**The pattern to borrow:** do one placement, one route, and one ordering/nudging pass. Any search should use incremental re-routing of affected edges, have named iteration bounds with a stop-after-k-non-improving-steps rule, and run under a global time budget that returns the best legal answer so far. This engine has the bounds (rules.yaml) but not the incrementality, the patience-based stopping or the deadline.

## 5. Ranked recommendations (impact ÷ effort; QW = quick win)

See the final message for the ranked list. The one-line forms:

1. **QW** Cache membership per contract with a `ConditionalWeakTable` inside `FindInnermostGroupId` and `BuildGroupById`. The scoped one-line version, `ReuseFixedMembership`, was output-identical on all 673 diagrams and made A115 39 % faster, but it misses A142's contract instances. Also reorder `IsLegalTopPortCrossing` to test geometry first.
2. **QW** Cache logical-grid geometry: prefix sums or position arrays per grid snapshot, invalidated when a gap changes.
3. **QW** Cache title bands and group bounds. Use `FlowchartFrameTitleBandHelper.Get`, or a per-bounds-version cache, instead of `Build` at the ten ModelChecks sites. Allocate nothing in `FindEnteredBandIds`.
4. **QW** Give `PaddedBox` `IEquatable` and `GetHashCode`, and make query result order deterministic.
5. **QW** Memoise per contract and per geometry version: `FindBackEdges`, `BuildGroupById`, `Check` and `Measure`.
6. Make the search budgets adaptive and stop on "patience" (Graphviz MinQuit style). This must change rules.yaml first. Measured: budget 16 instead of 48 saves 45 % of catalogue time, and A104 is identical at 4.9 s instead of 32 s.
7. Add a global deadline (yFiles stopDuration style) that returns the best legal layout so far. This prevents A142-type hangs, and it also needs a rules.yaml entry.
8. 4.12: add a dirty-node worklist, compute the baseline score lazily, reuse obstacles and bands for the whole pass, and cache the reference intersections in `InteractionsDoNotGrow`. These are the repo's own proposals 1–3, and they are exact.
9. `FlowchartDistinctTrackRepair`: build the path cache once, use a segment spatial index, and replace per-candidate `Check`/`Measure` with local deltas. In the long term, replace trial translation with ELK/libavoid one-pass slot ordering and nudging.
10. Stop nested completion. Inside a candidate completion, do not run `ApplyFinalMoveBeforeDetour`'s full `SearchCellAssignmentsCore` (43.5 % of A104 is at depth 2). Use affected-route completion with one exact confirmation, and require byte-identical output or explicit approval because of the earlier rejection in docs/cell-incremental-score-rejected.md.
11. Band wraps: rank candidates with a placement-only pre-pass, fully route only the top k, adopt the winner without re-running it, and then parallelise with cloned hint state.
12. Housekeeping: replace JSON and string keys with 64-bit hashes, cache `PaddedBoxSpatialIndex` per obstacle set, hash-dedup `AddVertex`, and remove LINQ from hot loops. Also give `TryTranslateInternalRun` a spatial index.

## 6. Interaction with the approved port rule (24 Oct 2026, being implemented now)

- The centre-out 15 px slot grid gives a small, fixed set of candidates per side. That favours **precomputed slot tables** and ELK-style one-pass slot assignment, and it shrinks 4.12's candidate set. Expect the port rule itself to change outputs, so take the byte-identity baseline **after** the port-rule work lands.
- Recommendations 1–5 are independent of port placement; they only cache pure functions. Recommendations 8–9 should be implemented on top of the new slot model, not before it.
- Recommendation 6 (budgets) interacts most: the port rule's "fallback to any free grid point" adds candidates, so re-time budgets after it lands.

## 7. Artefacts

- `bench/r1.jsonl`: master, all 673 diagrams.
- `exp16/bench/r16.jsonl`: old budgets.
- `expmem/bench/rmem.jsonl`: membership cache.
- `bench/prof/{a104,a115,a142}.*`: traces and speedscope files.
- Experiment copies from the original review box were not committed to this repository.

## 8. Not done

The Layout test suite (`tests/MermaidRecognition.Layout.Tests`) was **not timed**. Its timing was blocked by the length of the A142 runs; the catalogue numbers above stand in for it.

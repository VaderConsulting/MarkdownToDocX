# Corridor allocation follow-up

2026-10-20 WST. HEAD remains ce6f1ce. All engine and report work is uncommitted. No rules.yaml, checker, watchdog or limit changes; no DOCX regeneration or push.

## A110: every remaining pair classified

The usable-track census corrects the preliminary nominal 84/42 split to **83 sufficiently reserved internal pairs and 43 forecast/shortfall pairs**. Width divided by spacing is not the actual usable grid-track count: corner clearance and grid coordinates must also fit. The 43 are 42 pairs on exterior rails X=-24 or X=-210 that were not assigned finite forecast tracks, plus one internal pair in the column gap after column 1. That 90 px gap forecasts two lines but actually receives three; it has two usable tracks after 20 px corner clearance, not the three nominal width/spacing slots.

The short internal pair is `webShop->priceLookupSvc:41` × `objectStorage->paymentHub:59`. The larger group, 83 pairs, is already in gaps with enough usable tracks. The full pair inventory and every overlap location, forecast, actual peak and capacity are in:

- `out/markup-review/initial-corridor-capacity/pair-analysis/A110-shared-pairs.csv` (126 rows).
- `out/markup-review/initial-corridor-capacity/pair-analysis/A110-shared-segments.csv` (all overlapping runs; a pair can occur in more than one gap).

| Original gap | Overlap records | Forecast | Actual peak at overlaps | Usable tracks |
|---|---:|---:|---:|---:|
| Column gap after 1 | 1 | 2 | 3 | 2 |
| Exterior rails | 42 | No finite reservation | 9 | Unbounded space; no assigned tracks |
| Row gap after 0 | 6 | 6 | 5 | 7 |
| Row gap after 1 | 6 | 6 | 5 | 7 |
| Row gap after 2 | 6 | 6 | 5 | 7 |
| Row gap after 3 | 6 | 6 | 5 | 7 |
| Row gap after 4 | 6 | 6 | 5 | 7 |
| Row gap after 5 | 37 | 6 | 8 | 36 |
| Row gap after 6 | 27 | 32 | 15 | 39 |

The row-5 forecast is low, but its pre-existing wide gap has ample capacity. It belongs to the allocation problem rather than a shortage. Exterior space is not falsely reported as a narrow finite gap.

## Fix at construction

`DiagramGridRouteBuilder.RebuildAllRoutes` previously selected paths by bends and length without considering occupied rails. Several arms independently chose the same route despite reserved space. The builder now allocates against the other exact drawn paths **before publishing each path**, including when it retrieves a geometry-only cached proposal. Occupancy-dependent choices are never stored under that geometry-only cache key.

`FlowchartFallbackCorridorTracks` enumerates legal whole-grid tracks in the existing gap, retains exact allocated terminals and their normals, enforces corner clearance and configured track separation, and validates the entire proposed polyline. It adds no corners or crossings to the path it changes. A better already-legal route at identical ports is not discarded for a worse cached proposal. Search still uses the existing repair_candidate_moves and repair_chain_depth values; no larger limit was introduced. It only runs during the existing final model construction for layouts with initial corridor reservations. There is no added cleanup pass.

Each complete reserved-track assignment is also compared with ordinary routing of the **same cells and same gaps** in an isolated context. The normal score must improve; physical crossings, shared pairs, same-fan crossings and each rule's finding count must not rise. The pre-growth bend ceiling and honoured hints are retained. Rejected assignments are confirmed in the caller's context using ordinary routing, so trial grids, ports, guides and caches are not transplanted or leaked. This rejects the local A117 and A38 regressions found in intermediate trials. Eight complete assignments were kept and 21 rejected; every diagram retains its reserved gaps.

**A110: shared pairs 126 → 25, below the ceiling of 58; bends 184 → 184; physical crossings 791 → 489.** All 83 old sufficiently-reserved pair identities disappear. The remaining 25 comprise 24 exterior pairs and one different internal pair in row gap 5 (`orderManager->customerLookupSvc:37` × `webShop->priceLookupSvc:41`). The original short column-gap pair also disappears without widening that gap again. The remaining inventory is in `selected/A110-shared-pairs.csv` and `selected/A110-shared-segments.csv`.

## A115 and A46

A115's wider gaps changed relative pixel distances, frame-port positions and some facing-side choices. The existing independent shortest routes then crossed other paths. For example, `perthLan->perthEdge:1` changed from left/right sides to top/bottom; `internet->perthEdge:0` changed from a two-corner frame approach to a four-corner path. These are routing changes on unchanged node regions, not different crossing-checker semantics.

Construction now accounts for other routes and retains a legal less-interfering path when the geometry-only cache would discard it. The complete score falls from 3504.324238145128 for capacity-only routing to 3301.4341517947014. **A115 goes from 75 to 64 crossings, below its pre-sizing 67, and shared pairs from 1 to 0.** Bends are 84: one more than capacity-only 83, and one fewer than pre-sizing 85. This trade is selected by the unchanged normal score.

This removes the **net eight-crossing increase**, not every newly introduced pair identity. The old newly crossed pairs remain, while more crossings elsewhere are removed. The table below explicitly preserves that distinction. A measurement widening the existing side comparison to its directly affected lines and adding route dependencies to its cache key produced the same A115 84/64 result and took 26.31 s; it was reverted. All original search guards remain.

A46's `shop->api:0` and `shop->checkout:2` leaves share a rank. Their comparator returned equality, so a change in route enumeration inverted the far/near ports. During slot allocation their target sides are still provisionally left; a later side choice changes them to top. Waiting for perpendicular entry metadata therefore misses the fault. The fix identifies the off-axis flat-face fan from its endpoint geometry and actual allocated slot inversion, assigns the far leaf its outer port, and preserves settled order on later equal-rank allocations. Aligned straight neighbours, hints and curved/sloped connection inventories keep their existing policy. **A46 returns from one crossing to zero with 12 bends unchanged.** The curved-junction A47 control retains its exact old geometry and 5/0 counts.

| Case | Before sizing B/C/H | Capacity-only B/C/H | Retained B/C/H |
|---|---:|---:|---:|
| A110 | 200 / 854 / 215 | 184 / 791 / 126 | 184 / 489 / 25 |
| A115 | 85 / 67 / 2 | 83 / 75 / 1 | 84 / 64 / 0 |
| A46 | 12 / 0 / 0 | 12 / 1 / 0 | 12 / 0 / 0 |
| A107 | 10 / 0 / 0 | 10 / 0 / 0 | 10 / 0 / 0; port findings remain 2 |

## Changed cases in this routing follow-up

These are the eight geometry changes relative to capacity-only output. Every other measured case matches its geometry hash exactly. No rule's per-scenario finding count increases. A115's bend trade above is explicit.

| Case | Bends | Physical crossings | Shared pairs |
|---|---:|---:|---:|
| A104 | 20 → 20 | 0 → 0 | 0 → 0 |
| A110 | 184 → 184 | 791 → 489 | 126 → 25 |
| A115 | 83 → 84 | 75 → 64 | 1 → 0 |
| A116 | 98 → 97 | 73 → 50 | 12 → 5 |
| A167 | 50 → 50 | 67 → 65 | 2 → 2 |
| A46 | 12 → 12 | 1 → 0 | 0 → 0 |
| A68 | 5 → 5 | 2 → 1 | 0 → 0 |
| A69 | 13 → 13 | 1 → 0 | 0 → 0 |

## Original A115 additions, tracked by identity

This is the original increase table with current counts; it is not silently replaced by the improved net total. The full before/current pair comparison is `selected/A115-crossing-changes-from-before-sizing.csv`.

| Pair | Before sizing | Capacity-only | Current |
|---|---:|---:|---:|
| backupDue->backup:29 × perthLan->perthEdge:1 | 0 | 2 | 2 |
| drSvc->shop:23 × perthLan->perthApp:2 | 0 | 1 | 1 |
| drSvc->shop:23 × perthLan->perthEdge:1 | 0 | 2 | 2 |
| drSvc->shop:23 × perthRt1->containerSvc:13 | 0 | 1 | 1 |
| drSvc->shop:23 × perthRt2->containerSvc:14 | 0 | 1 | 1 |
| httpsIn->lbSvc:12 × perthLan->perthEdge:1 | 0 | 1 | 1 |
| internet->perthEdge:0 × perthDb->dbSvc:16 | 0 | 1 | 1 |
| internet->perthEdge:0 × perthLan->perthApp:2 | 0 | 1 | 1 |
| internet->perthEdge:0 × perthLan->perthData:3 | 0 | 1 | 1 |
| internet->perthEdge:0 × perthMq->mqSvc:17 | 0 | 1 | 1 |
| internet->perthEdge:0 × perthRt1->containerSvc:13 | 0 | 1 | 1 |
| internet->perthEdge:0 × perthRt2->containerSvc:14 | 0 | 1 | 1 |
| perthLan->perthEdge:1 × perthRt1->containerSvc:13 | 0 | 1 | 1 |

## Growth and protected behaviour

No node region, row, column, span, column gap or row gap changes relative to the capacity-only capture in any of the 67 cases. Rendered width/height increments remain the same as the previous growth report. The 29 gap-growth cases are repeated below for convenience; the other 38 original hashes remain unchanged. Full dimensions and percentages: `selected/growth/changed-cases.csv`. A38 becomes 24 px narrower in total route bounds despite gap growth; this is a route-extent change, not a resized node.

| Case | Wider px | Taller px |
|---|---:|---:|
| A100 | 90 | 60 |
| A104 | 0 | 90 |
| A110 | 1170 | 2220 |
| A111 | 60 | 150 |
| A113 | 660 | 1290 |
| A115 | 240 | 210 |
| A116 | 570 | 540 |
| A117 | 660 | 210 |
| A133 | 60 | 60 |
| A135 | 90 | 90 |
| A140 | 90 | 90 |
| A165 | 240 | 30 |
| A167 | 66 | 180 |
| A26 | 30 | 60 |
| A38 | -24 | 30 |
| A46 | 30 | 90 |
| A47 | 30 | 60 |
| A52 | 0 | 30 |
| A57 | 0 | 30 |
| A66 | 90 | 90 |
| A67 | 90 | 90 |
| A68 | 0 | 60 |
| A69 | 60 | 0 |
| A70 | 0 | 30 |
| A71 | 30 | 30 |
| A83 | 0 | 30 |
| A88 | 30 | 0 |
| A94 | 60 | 60 |
| A98 | 0 | 30 |

## Validation, timings and limitations

- 50 focused band/row/capacity/construction tests pass, including all four orientations, exact ports and normals, whole-grid/minimum-separated tracks, repeated allocation, and immutable cached seeds.
- Ten production regressions pass: the unchanged A107/A110 ceilings, A115's drift regression, A116/A117 singleton regressions, A115/A46/A47 crossing/bend controls, and A117/A38 rejected-complete-assignment controls.
- All 67 potentially affected multi-layer Enterprise captures except the already timed-out A142 complete. Strict slots, off-slot endpoints and final rules 10/3.1/4.1 are zero. Cell/gap parity is exact. This preserves band membership/order and the A117 timeline by exact regions.
- All 26 other stress captures complete under the unchanged 180 s policy, with those same strict checks zero. Against capacity-only output only A104 and A110 change stress geometry; 24 match exactly. Existing known port/shared faults in other stress cases remain, including A107's 2 port findings. A142 retains its recorded 182.1 s timeout and was not rerun.
- Two native PNG render tests pass, each proving the render's geometry hash matches production layout. Images use MermaidDiagramExport.TryRenderPng, the DOCX image path, without regenerating DOCX. A107 remains visually unchanged because its separate loop-back candidate lost the normal score comparison; see `docs/a107-loop-back-placement-trial.md`.
- Measurement process wall times: 67 captures 99.89 s; ten protected tests 63.48 s; PNG checks 23.37 s; 26 stress captures 48.89 s. These are separate test processes, **not a full 673-summary audit**. The full normal suite/failure identity set has not been recertified in this work.
- Current rules.yaml SHA256 remains A9EDCF038C2925C3CBE522F9BC43F82D080A790D044345B6BC35224E69129DC1. The buffer is still read through FlowchartLayoutRules, all limits/weights remain unchanged, and independent text spans stay off.

Fresh native renders: `out/markup-review/initial-corridor-capacity/selected/A107-after.png` and `A110-after.png`. The full A110 image is retained; `A110-preview.png` is only a reduced inspection copy.

Decisions made without waiting: correct the capacity census for usable rather than nominal tracks; fix the larger allocation group inside construction; isolate ordinary/reserved complete assignments and retain only lower-score admissible results; reject local regressions; leave curved junction geometry unchanged; respect the losing A107 score rather than force a picture or change a weight. Still open: A107's requested placement, A110's remaining 25 shared pairs, the original A115 added crossing identities, A142's known timeout, and the previously queued band work. No claim that the entire queue is complete.
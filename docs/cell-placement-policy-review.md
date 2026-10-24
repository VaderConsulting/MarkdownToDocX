# Cell placement policy review

The review applies only to experimental cell-bound nodes. Production cells and
independent text spans remain disabled. Existing cells-off branches, rules,
weights, hints and baselines are preserved. Superseded implementation details
and each separate census are recorded in cell-census-first-catch-review.md.

A source's rule 2.10 representative is (A+B)/2 over its inclusive occupied
interval. Representatives order candidate nominations; they never become node
positions. Complete integer region assignments compete through actual rebuilt
routes and the existing score. No unconditional centre alignment is used to
recover straightening removed by this review. Hints, legality and exact rollback
remain required. Candidate and chain budgets use the existing named limits.

## Every converted placement path

| Path | Decision | Reason |
| --- | --- | --- |
| Initial region producer | Keep; trim its centre-only metadata callers separately | Assigns exclusive rectangles from logical ranks/order and measured spans; no drawing-coordinate inverse. |
| Placement hints | Keep | Explicit whole-cell offsets and peer start alignment are user intent, not automatic centre positioning. |
| Spine and collectors | Trim | Keep graph selection and labelled side ordering; remove forced spine columns and collector rows/columns. |
| Empty-strip compaction | Keep | Removes whole empty strips, preserves ordering/minimum gaps and rejects hint conflicts atomically. |
| Centre snapping boundary | Already bypassed | Authoritative regions must not be resnapped from drawing centres. |
| Single terminal children | Already bypassed in b2a0e27 | Both phases existed only to line up centres. |
| Disconnected component packing | Keep | Packs complete components by footprint/aspect score, preserving their internal offsets. |
| Decision input chains | Bypass | Only forced an upstream chain onto the decision line; scored region moves replace it. |
| Root container reflow | Keep | Complete container packing, internal empty-strip compaction, frame conflicts, hints and footprint score. |
| Terminal fan centring | Bypass | Both median-child metadata and later chain movement only centred the fan. |
| Terminal side branches | Keep | Side ordering, occupied-rank eligibility and measured body clearance; no midpoint is assigned to occupancy. |
| Decision terminal outcomes | Bypass | Preset symmetric lanes in both phases, without independent clearance work. |
| Chain wrapping | Keep | Ordered grid wrapping, measured external label gaps and aspect improvement with exact rejection rollback. |
| Input fan compaction | Bypass | Only imposed target-centred symmetric lanes; general empty-strip compaction remains. |
| Circular junction chains | Bypass | Only imposed fan median/midpoint and input/output-chain alignment. |
| Measured port capacity | Keep | Fits actual connection-boundary slot demand by whole spans; no neighbour-count-only resizing. |
| Nested group centring | Bypass | Only translated the nested envelope to a parent-member midpoint. Frame/title containment remains elsewhere. |
| Parallel label clearance | Keep | Measured label/port extent shortfalls, not centre placement. |
| Junction branches and collectors | Trim | Preserve whole branch membership, rank order, hints and measured clearance as scored candidates; remove immediate assignment and collector centring. |
| Overlap boundary | Keep | Complete collision-free region/displacement assignment, with no pixel copy-back. |
| Partner ranks and extents | Trim | Retain measured capacity and growth-induced overlap clearance. Remove cross-rank alignment; test actual facing ports before claiming a straight route. |
| Partner channel gaps | Keep | Measure track corridor shortfall and grow an external shared gap atomically. |
| Sparse rank compaction | Keep | Reuses non-overlapping strips while preserving rank-skipping corridors and measured gaps. |
| Independent terminal inputs | Keep | Opposite-side ordering, a free slot in the collector rank and measured clearance; logical start positions, not centre equality. |
| Large fan grid packing | Keep | Scores complete column-grid/slot-capacity/clearance candidates; more than a centring pass. |
| Decision detours | Trim | Keep detour/collection side ordering; remove continuing-merge straight-line assignment. |
| Horizontal retries | Trim | Keep retry topology, rank and measured return corridor; remove success-node alignment. |
| Vertical loop branches | Trim | Keep opposing branch order; translate the tail uniformly instead of flattening its offsets. |
| Simple decision merges | Bypass placement | Remove preset symmetric lanes and right-child merge alignment. Keep topology detection and obstacle-checked route construction. |
| Shared fan roots and tails | Trim | Keep topology classification; remove midpoint root/tail writes. |
| Standalone group packing | Keep | Scores declared-order column-grid candidates and occupied footprint, not pairwise node alignment. |
| Grouped channels | Keep | Complete declared-order ranks, measured slot capacity and shared channel clearance compete as full placement candidates. |
| Grouped leaves | Keep | Whole-cell side candidates, measured label/body/title clearance and crossing/length cost. |
| External leaves | Keep | Same candidate scoring plus owner-frame clearance; no midpoint-to-cell conversion. |
| Same-rank label gaps | Keep | Measured label shortfalls grow shared external gaps without moving nodes against hints. |

Measured visual centres still appear in port and collision calculations: those
are forward projections of already proposed regions. They do not grant write
access to node centres. Midpoint origins inside complete block-packing candidates
serve footprint placement, not unconditional pairwise straightening. The routed
scorer remains responsible for straightening between connected nodes.

## Interpretation of cells-on metrics

There are 673 census cases: 662 grid cases (3,806 bound nodes), six open-mode
cases kept unbound and five known native skips/empty sources. Bends are measured
with the existing FlowchartLayoutScore.CountCorners on completed model routes.
They are not rule 4.2 findings. Failed frame cases have no completed route total
and are never counted as zero bends. Every comparison below uses the same 660
completed bound cases unless explicitly stated otherwise.

A164 and A165 remain experimental frame-fit failures. No cells-on legality
acceptance is claimed. Cells-off legality gates must remain zero and the entire
673-row audit must be byte-identical before this review is complete.

## Companion metadata and dormant paths

| Path | Decision | Reason |
| --- | --- | --- |
| Forward main-chain columns | Bypass for cells | Pure forced root-column straightening, hidden before the region producer. |
| Fan-in merge median column | Bypass for cells | Source median prescribed the target position without scoring. |
| Three-way decision centre-child columns | Bypass for cells | Retain original branch order; remove middle-child/parent-column alignment. |
| Detour rejoin column | Trim for cells | Keep branch side ordering, remove forced decision/rejoin equality. |
| Vertical chord blocker swap | Bypass for cells | Parent-column straightening was prescribed without testing full region/routing cost. |
| Two-way decision side order | Keep | Establishes left/right branch order; it does not make nodes share a centre line. |
| Column cascading | Keep | Resolves same-rank ordering collisions, not alignment across ranks. |
| Intergroup connector pixel placement | Bypass legacy writes for cells | Previously dormant A111 writer; common cell frame fitting retains clearance and whole-cell routed search compares endpoint/group moves. |
| Terminal-row hole filling | Trim into scored candidates | Previously dormant A173 writer; retain bounded-hole eligibility without publishing pixels or rank metadata. |

The two dormant writers were exposed by removing hidden centre policies. Both
now have original-source completion regressions, alongside A110/F136's missing
specialised track regressions. Initial native rank/order assignment is retained:
it supplies ordering at the native-to-logical boundary, not an automatic repair
that aligns already assigned cell regions.

## Incremental checkpoints

Every row reruns all 673 census cases. All rows have zero writer catches and the
same hard frame failures, A164/A165. Bends below cover the same 660 completed
bound cases. The six open-mode scenarios stay unbound; the five known skips are
unchanged. Timing is NUnit census time, not the production route-rule audit.

| Path | Commit | Before bends | After bends | Changed cases | Census seconds |
| --- | --- | ---: | ---: | ---: | ---: |
| Terminal fan centring | 645607b | 3270 | 3270 | 2 | 28.5041 |
| Nested group centring | 2a51ad4 | 3270 | 3272 | 1 | 29.3663 |
| Shared fan roots/tails and scored candidates | 4a74533 | 3272 | 3238 | 93 | 28.7116 |
| Simple decision merges | 71157d7 | 3238 | 3234 | 1 | 29.0639 |
| Decision terminal outcomes | 9838a76 | 3234 | 3232 | 2 | 29.5662 |
| Partner ranks/extents | 08c4ccd | 3232 | 3238 | 2 | 29.3435 |
| Junction branches/collectors | a730446 | 3238 | 3244 | 2 | 32.8158 |
| Circular fans/collectors | 760afdb | 3244 | 3233 | 3 | 33.7531 |
| Independent input fan | 91f1483 | 3233 | 3242 | 6 | 34.9403 |
| Decision input chain | 4d8668f | 3242 | 3240 | 2 | 33.4533 |
| Spine/collector alignment | 8c2e602 | 3240 | 3242 | 1 | 33.8506 |
| Vertical loop tail offsets | 24a3845 | 3242 | 3242 | 0 | 33.4211 |
| Horizontal retry success | 0d252bf | 3242 | 3236 | 2 | 33.8586 |
| Continuing decision merge | 42bf488 | 3236 | 3236 | 0 | 33.771 |
| Dormant intergroup writer | f02886f | 3236 | 3236 | 0 | 31.7906 |
| Hidden main-chain column | ba20dd8 | 3236 | 3158 | 86 | 35.6676 |
| Dormant terminal-row writer | 6d84918 | 3158 | 3158 | 0 | 34.2954 |
| Hidden fan-in median column | cc41f7b | 3158 | 3204 | 55 | 37.7723 |
| Hidden decision centre child | 92466d9 | 3204 | 3204 | 0 | 35.6358 |
| Hidden detour rejoin column | 7f0b29a | 3204 | 3201 | 1 | 33.7053 |
| Pre-placement chord swap | b2b85a9 | 3201 | 3211 | 6 | 34.4947 |

Detailed evidence:

- cell-placement-policy-bends.csv lists every changed scenario after each step,
  including increases. Each change is attributable to the path in its Step
  column and the policy removal/trim explained above.
- cell-placement-policy-final-bends.csv contains before/after bends for all 660
  completed bound cases, including unchanged rows.
- cell-placement-policy-increments.csv contains the full per-step census totals.
- cell-census-first-catch-review.md preserves each Superseded note and its checks.

Overall cells-on model bends: **3270 -> 3211**. Removing preset alignment can
increase bends in individual cases; the existing bounded routed search is not
claimed to find the global optimum. Those increases are exposed in the CSV,
not masked by restoring centre policies or changing a checker or weight.

## Cells-off acceptance

The final Release audit emits **all 673 summaries**, with ordered ordinal byte
comparison equal to the pre-review capture. Both SHA256 hashes are
387ECBD2DA61640A683969A580C4AE7B7054403B43EF952519B0232A2D10B939.
The audit reports **46.46 s**, A142 **15.8487 s** (profiling off).
This is slower than the earlier 35.05 s / 10.6321 s measurement; no speed gain
is claimed. The current changes take cell-only branches and leave production
summaries unchanged.

All prior zero gates remain zero: off-slot ports/groups, grid findings,
off-grid nodes/frame edges, title-band intersections, frame overlaps, missing
routes, no-legal-route failures, illegal paths, hint accounting failures and
cleanup corrections. Detailed unchanged counters are in
cell-placement-policy-off-audit-totals.csv.

| Quality count | Before | After |
| --- | ---: | ---: |
| 4.2 | 44 | 44 |
| 5.9 | 1694 | 1694 |
| 3.1 | 4 | 4 |
| 4.1 | 0 | 0 |
| 4.11 | 3 | 3 |
| 5.8 | 0 | 0 |
| Shared-track pairs | 2089 | 2089 |

The audit retains the same 25 known failing/skip cases and five unmeasured
native-skip/empty cases. Its exit code is therefore still 1; this is not a claim
that every quality rule passes. No baseline or checker was weakened.

Tests: **296 focused passes**, plus **12 real-source regression passes** (the
eight frame-fit cases and A110/A111/A173/F136). Superseded centre-policy test
expectations now check dispatcher bypass and exact ownership, rather than
requiring the removed alignment. F32/EIP261 remains parked for routing work.

The final cells-on census takes **34.4947 s**, with the same 660 completed bound
cases, zero catches, A164/A165 frame failures, six open-mode cases unbound and
five known skips. Across completed cases, 70 improve their bend total, 81 rise
and 509 are unchanged. Frame failures have no route total; they are not zeroes.

Exact comparison of page node/frame geometry, every declared route, painted
route guides and ordered diagnostic strings against diagnostics-before.jsonl:
**673 scenarios, changed=0, missing=0, 38.937 s**. Comparisons use full doubles
with no rounding or tolerance. Production cells, independent text spans,
rules.yaml, weights, hints and unrelated output remain unchanged.

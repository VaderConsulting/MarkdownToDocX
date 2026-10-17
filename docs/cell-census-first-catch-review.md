# Cell census first-catch review — 2026-10-18

The original 662 first catches are historical, not the current state. Their grouping comes from cell-guard-paths-on.csv: 648 PlaceOnGrid -> SetMain, 13 PlaceRelative -> Translate, and one PushToBoundary -> Translate. The largest group was converted first, followed by placement-hint writers before later repairs. Tests below are the passing focused selection at that checkpoint, not the entire repository suite.

Past old first catch means that the same scenario no longer stops at that writer. It can encounter another writer or a later hard failure; it does not imply completed layout. Counts use one primary record per scenario, excluding repeated NUnit failure text.

| Conversion | Commit | Old first catches | Past old first catch | New first catches | Hard failures | Focused tests | Census log |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| Initial placement | 9eddf0f | 662 | 648 | 662 | 0 | 144 | census-after-initial-placement.log |
| Placement hints | 0a65ada | 662 | 14 | 662 | 0 | 155 | census-after-cell-hints.log |
| Spine and collectors | 777df62 | 662 | 295 | 662 | 0 | 158 | census-after-cell-spine.log |
| Empty-strip compaction | 593fcb4 | 662 | 388 | 662 | 0 | 161 | census-after-cell-compaction.log |
| Centre snapping boundary | f046ae7 | 662 | 370 | 315 | 8 | 163 | census-after-cell-centres.log |
| Single terminal children | e3fee8f | 315 | 208 | 167 | 9 | 167 | census-after-cell-terminal-child.log |
| Disconnected component packing | 39508b6 | 167 | 43 | 127 | 9 | 169 | census-after-cell-disconnected-packing.log |
| Decision input chains | 7c0ab95 | 127 | 20 | 116 | 9 | 172 | census-after-cell-decision-inputs.log |
| Root container reflow | fb083a1 | 116 | 21 | 95 | 11 | 174 | census-after-cell-root-packing.log |
| Terminal fan centring | 690e995 | 95 | 12 | 86 | 11 | 176 | census-after-cell-fan-centres.log |
| Terminal side branches | d5f088e | 86 | 13 | 75 | 11 | 179 | census-after-cell-terminal-branches.log |
| Decision terminal outcomes | a870dbc | 75 | 12 | 65 | 11 | 182 | census-after-cell-decision-targets.log |
| Chain wrapping | fa3c00b | 65 | 10 | 55 | 11 | 186 | census-after-cell-chain-wrapping.log |
| Input fan compaction | d523a67 | 55 | 11 | 44 | 11 | 189 | census-after-cell-fan-in.log |
| Circular junction chains | 88562e5 | 44 | 6 | 43 | 11 | 192 | census-after-cell-circular-fans.log |
| Measured port capacity | 833827c | 43 | 6 | 42 | 11 | 195 | census-after-cell-port-growth.log |
| Nested group centring | 208010f | 42 | 5 | 37 | 15 | 198 | census-after-cell-nested-placement.log |
| Parallel label clearance | ed1f956 | 37 | 6 | 31 | 16 | 201 | census-after-cell-parallel-extents.log |
| Junction branches and collectors | 103d4f9 | 31 | 5 | 26 | 16 | 204 | census-after-cell-junction-branches.log |
| Overlap boundary | ef25642 | 26 | 3 | 24 | 16 | 206 | census-after-cell-overlap-boundary.log |
| Partner ranks and extents | a160586 | 24 | 4 | 24 | 16 | 209 | census-after-cell-partner-preparation.log |
| Partner channel gaps | 097fda5 | 24 | 3 | 21 | 16 | 212 | census-after-cell-partner-gaps.log |
| Sparse rank compaction | 8e4e156 | 21 | 4 | 17 | 16 | 215 | census-after-cell-sparse-compaction.log |
| Independent terminal inputs | d02db55 | 17 | 3 | 15 | 16 | 218 | census-after-cell-terminal-inputs.log |
| Large fan grid packing | 58dae30 | 15 | 3 | 12 | 16 | 233 | census-after-cell-fan-packing.log |
| Decision detours | d692607 | 12 | 2 | 10 | 16 | 237 | census-after-cell-decision-detours.log |
| Horizontal retries | 19686f6 | 10 | 2 | 8 | 16 | 240 | census-after-cell-horizontal-retries.log |
| Vertical loop branches | 0e1c61d | 8 | 2 | 7 | 16 | 243 | census-after-cell-vertical-loops.log |
| Simple decision merges | 1a1d957 | 7 | 1 | 6 | 16 | 246 | census-after-cell-simple-merges.log |
| Shared fan roots and tails | 4838a67 | 6 | 1 | 5 | 16 | 249 | census-after-cell-shared-fans.log |
| Standalone group packing | c4e5663 | 5 | 1 | 4 | 16 | 251 | census-after-cell-group-members.log |
| Grouped channels | 1d10b2b | 4 | 1 | 3 | 16 | 255 | census-after-cell-grouped-channels.log |
| Grouped leaves | c07cf2d | 3 | 2 | 4 | 17 | 264 | census-after-cell-grouped-leaves.log |
| External leaves | f848957 | 4 | 2 | 3 | 17 | 264 | census-after-cell-external-leaves.log |
| Same-rank label gaps | 810c2a9 | 3 | 3 | 0 | 17 | 268 | census-after-cell-label-gaps.log |

Current rerun: census-current-first-catch-review.log, 673 cases, 25.9800 s. No first writer catches; 662 grid scenarios / 3,806 primary nodes are bound. Of these, 645 complete and 17 fail frame fitting. Six open-mode scenarios (27 nodes) complete unbound: F145, F147, F149, A144, A146 and A148. Five pre-binding exits remain F65, F70, F87, F88 and A01. No new conversion is required to eliminate the original catches.

Native-skip instrumentation rerun: census-first-catch-native-reasons.log, 673 cases, 21.5003 s, 656 tests passing and the same 17 hard frame failures. F65, F70, F87 and F88 are CatalogueRemarked and rejected by NativeRenderCapabilities before Dagre runs. They are deliberately commented-out catalogue cases, not placement crashes. A01 is the documented header-only empty enterprise diagram and has zero nodes. All five existed in the pinned checkpoint audit and were not repaired. F151, F152 and F153 each bind all 160 nodes and complete the current probe.

Hard frame-fit scenarios: F42, F141, A114, A115, A142, A162, A164, A165, A166, A167, A168, A169, EIP42, EIP78, EIP266, EIP271 and EIP296. No cell-enabled legality acceptance is claimed while they remain.

Production cells and independent text spans remain disabled. Latest cells-off validation retains all 673 byte-identical summaries, every prior zero gate, exact diagnostic findings and zero geometry changes. The 268 focused tests and five routing-hint tests pass. F32/EIP261 remains parked. No DOCX files or unrelated work are included in this report checkpoint.

## Subsequent terminal-policy correction

The terminal-child row above records the historical conversion e3fee8f and its
then-current census. That conversion is now superseded: the cell helper was
removed because the legacy pass existed only to align centres. Both its
pre-placement ordering rewrite and post-placement adjustment are bypassed for
cell-bound nodes. Fresh before/after censuses retain zero writer catches and
the same 17 frame-fit failures (21.3973 s before, 20.6686 s after). The focused
selection now passes 270 tests. Cells-off geometry remains exact for all 673;
the full audit retains all 673 identical summaries and all prior zero gates.

## Superseded: terminal fan centring (690e995)

The cell-bound median-child centring conversion in 690e995 is superseded.
Both pre-placement Column rewriting and post-placement parent/input-chain
alignment are bypassed. The pass did no independent ordering or clearance
work. Cells-off keeps the legacy implementation. Whole-cell routed candidates,
not a median-child assignment, are responsible for any straightening.

Census placement-policy-00-before.log -> placement-policy-01-terminal-fan.log:
660 completed bound cases in both, hard failures A164/A165 unchanged, zero
writer catches. Model bends on those same cases are 3270 -> 3270. F142 changes
6 -> 3 and EIP178 changes 2 -> 5; all other case totals are unchanged. All 278
focused tests pass (the original 270 included, with the superseded centring
expectations replaced by both-phase bypass tests). Production cells and
independent spans remain off. Full cells-off verification follows the complete
placement-policy review; the unbound branch is unchanged in this checkpoint.

## Superseded: nested group centring (208010f)

The midpoint translation in 208010f is superseded for cell-bound placement.
CenterLoneNestedGroups now bypasses authoritative regions, and its cell-only
implementation is removed. The pass only aligned the nested member envelope
with the direct parent members' midpoint. Membership, title sizing, frame
containment and scored frame-clearance repair remain in their existing paths.
This follows the explicit placement-policy review instruction; rules.yaml is
unchanged and the production cells-off centring branch is preserved.

Census placement-policy-01-terminal-fan.log -> placement-policy-02-nested-group.log:
zero writer catches; A164/A165 remain the only hard failures; the same 660 bound
cases complete. Bends are 3270 -> 3272, entirely A114 (141 -> 143). All 279 focused
tests pass. Straightening must compete as complete whole-cell routed candidates,
not be recovered by reinstating an envelope-midpoint write.

## Superseded: shared fan midpoint and forced tail alignment (4838a67)

The midpoint/root and unconditional tail-position assignments in 4838a67 are
superseded for cells. Topological shared-source classification is retained;
no root, tail, ordering metadata or gap is written by that classification.
Straightening now competes in the existing complete routed-score search.

That search now offers peer-start/far-boundary translations, whole unbranched
tails and compass moves as atomic region candidates, interleaved across units.
Its candidate ordering uses rule 2.10: each source contributes (A+B)/2 over its
inclusive occupied cells; a dominant source takes priority and tied sources'
representatives are averaged. The possibly fractional result is ordering-only,
never rounded into occupancy. Normal hint locks, route legality, exact rollback
and the existing named candidate/chain/line limits remain. Straightening rejects
an increase in bends in the scored candidate; all original score weights stay
unchanged. Later routing passes can still change a scenario's final bend total.

Census placement-policy-02-nested-group.log -> placement-policy-03-shared-fans-final.log:
zero writer catches; A164/A165 unchanged; the same 660 bound cases complete.
Bends total 3272 -> 3238; 93 scenario totals change. The full before/after rows
are in the latter log's .bends.csv sidecar, including increases as well as gains.
All 285 focused tests pass. Cells-off verification is pending the final review
run; the new candidate enumerator is called only by the cell-bound repair path.

## Superseded: preset simple decision-merge positions (1a1d957)

The cell-only symmetric branch/merge placement in 1a1d957 is superseded.
The legacy dispatcher bypasses cell regions and the helper is removed. It
forced opposite lanes about the decision and put the merge on the right
branch's line without a routed score or a measured clearance shortfall.
The common topology detector, original branch order and obstacle-checked route
construction remain. Whole-cell scored candidates now supply straightening.

Census placement-policy-03-shared-fans-final.log -> placement-policy-04-simple-merges.log:
660 common completed bound cases; zero writer catches; hard failures remain
A164/A165. Bends 3238 -> 3234, entirely F53 (5 -> 1). All 286 focused tests pass.

## Superseded: symmetric decision terminal outcomes (a870dbc)

The forced outcome lanes and parent-column +/-1 metadata rewrites in a870dbc
are superseded for cell-bound nodes in both placement phases. Initial branch
order is retained, not replaced by a decision-midpoint arrangement. Topology,
joint symmetric port allocation and ordinary clearance checks remain. Whole-cell
positions compete through the routed score; no preset alignment is restored.

Census placement-policy-04-simple-merges.log -> placement-policy-05-decision-outcomes.log:
660 common completed bound cases; no writer catches; A164/A165 unchanged.
Bends 3234 -> 3232: F125 and F35 each improve 4 -> 3. All 286 focused tests pass.

## Superseded: forced partner rank alignment (a160586)

Partner preparation retains measured port capacity, whole-cell span growth and
atomic clearance packing. It no longer puts independently placed ranks on one
shared anchor or forces a child onto its partner's line. Straight-channel claims
are retained only when actual renderer-owned ports already align. Non-aligned
projected matching pairs receive a track instead of assuming forced alignment.

Census placement-policy-05-decision-outcomes.log -> placement-policy-06-partner-ranks-final.log:
660 common completed bound cases; zero writer catches; hard frame failures remain
A164/A165. Bends 3232 -> 3238: A110 166 -> 170, F136 12 -> 14, because the
previous forced alignment has been removed. All 287 focused tests pass.
The first census exposed missing specialised tracks for these two matching
pairs (not frame failures); the cell-only track enumeration now includes them.

## Superseded: unconditional junction branch and collector assignment (103d4f9)

The cell branch pass no longer applies the nearest lane or centres collectors.
It retains connected branch membership, occupied-rank order, hints and measured
clearance as unpublished whole-cell candidates. The complete routed scorer
chooses among those options; rejected options restore exact ownership and gaps.

Census placement-policy-06-partner-ranks-final.log -> placement-policy-07-junction-branches.log:
660 common completed bound cases; zero writer catches; A164/A165 unchanged.
Bends 3238 -> 3244: A112 27 -> 29, A47 4 -> 8 after removing preset collector
alignment. All 287 focused tests pass, including candidate atomicity and order.

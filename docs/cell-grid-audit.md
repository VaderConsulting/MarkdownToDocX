# Audit authoritative cell regions

## Title reservation

Already implemented in `f8596f3`: `DiagramGridLayoutGroupFrames.BuildFrames` and `BuildEmptyGroupFrame` call `FlowchartFrameTitleReservation.Measure` before frame fitting. Both measured title width and the multiline band height participate in the initial frame envelope. Routing's band helper uses the same measurement. Cell-bound frames bypass the old late enlargement, and the late writer itself refuses cell-bound state. Existing long-title, nested and empty-frame regressions remain enabled. No second title fix is needed.

The port-side retry remains a scored complete-route alternative. It restores each candidate, rebuilds allocated routes and compares `OptimizationScore`, while preserving hints and legality. It does not reserve title space by itself and cannot resize a cell frame.

## Independent cell audit

The audit now checks occupancy independently of the production `TryPrepare` validator. Regions are integer rectangles: fractional coordinates and disconnected ownership are excluded by their representation; positive spans and exclusive end boundaries are checked explicitly. Pairwise interval intersections flag both owners of shared cells. Base gaps must meet the rule defaults; every sparse grown gap must be finite and at least its configured base. Returned nodes and frames must match their forward projections, including ownership, missing/duplicate/extra output and stale geometry.

The old lattice checker remains unchanged for cells-off layouts. For cell-bound layouts it no longer attributes faults using unrelated historical pixel lattices; final faults are labelled `final-cell-regions`. Stage snapshots do not contain historical region snapshots, so the audit does not invent stage attribution. The explicit old/new comparison is diagnostic only and never decides the cell gate.

Coverage includes off-lattice changes on both node axes and all four frame edges. All are rejected by the authoritative projection check. Whole-step stale shifts and coincident shared cells, which the old check accepts, are also rejected. Valid spans can have half-cell centres and grown-gap offsets: these are legitimate cell geometry, not lost detections. Applying the centre lattice to them would reject legal layouts.

## Validation

Results are recorded after the complete cells-on and cells-off audits below. Production cells and independent spans remain off. No renderer, routing, rules, limits, baseline or weights are changed by this checkpoint. F32/EIP261's parked 4.2 refinement remains parked.

| Gate / counter | Cells off | Cells on |
| --- | ---: | ---: |
| Summaries | 673 | 673 |
| Illegal paths | 0 | 0 |
| Missing routes / no legal route | 0 / 0 | 0 / 0 |
| Title bands / unrelated frame overlaps | 0 / 0 | 0 / 0 |
| Grid / off-grid nodes / off-grid frame edges | 0 / 0 / 0 | 0 / 0 / 0 |
| Off-slot node / group ports | 0 / 0 | 0 / 0 |
| Hint accounting / cleanup | 0 / 0 | 0 / 0 |
| 4.1 / 3.2/4.7 | 0 / 0 | 0 / 0 |
| 4.2 | 44 | 52 |
| 5.9 | 1694 | 1234 |
| 3.1 | 4 | 2 |
| 3.3/5.2 | 5 | 5 |
| 4.9 | 6 | 4 |
| 4.11 | 3 | 5 |
| 5.8 | 0 | 3 |
| Shared-track pairs / 5.1 | 2089 | 346 |
| Port findings | 56 | 67 |
| Aligned / nonaligned port jogs | 15 / 1323 | 17 / 1254 |
| Unmeasured native skips/empty | 5 | 5 |
| Failed/skip audit cases | 25 | 29 |

309 focused tests and 57 cell regressions pass (13 new grid cases in addition to the original 44 selection). Full cells-on audit: 103.39 s, A142 38.4686 s. Full cells-off audit: 40.56 s, A142 11.7005 s. Both emit 673 summaries. Cells-off rows match the original saved baseline ordinally; cells-on rows match 42f7629 ordinally. Of 662 cell-bound scenarios, zero have a new-only grid fault; six open-mode scenarios remain unbound and five native skips/empty cases remain unmeasured. No scenario list is needed because it is empty. The tests demonstrate the stronger check on intentionally invalid fixtures rather than introducing artificial catalogue faults.

The audit exits nonzero for the unchanged quality/shared-track findings and unmeasured cases in the table; the legality, grid and slot gates are zero. Production geometry is untouched: this checkpoint changes only test/audit code and documentation. No DOCX files are committed and nothing is pushed.

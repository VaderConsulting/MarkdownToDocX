# Diagnosis of the three page-down migration attempts

This is a diagnosis of the previous attempts, not another migration attempt.
The coordinate prototype has not been repaired or rerun. The independent speed
fix is committed in `7b1eb66`; it preserves all 673 reference geometry snapshots.

## What the retained evidence establishes

Only five scenarios were selected for each failed attempt. The logs do not
establish how many of the full 673 would differ.

| Attempt | Recorded differences | Recorded interruption | Available coordinate measurements |
|---|---|---|---|
| 1 | F17, F30, F61, F153, A167 | None; all five completed | The log retains mismatch flags, not the coordinates. Magnitudes were not retained. |
| 2 | F17, F30, F61, F153 | A167 threw in `FlowchartPortSideRouteRepair.Length` because a route had zero points | Its coordinate snapshots were overwritten by attempt 3. Magnitudes cannot be recovered from this log. |
| 3 | F17, F30, F61, F153 | The process was stopped before A167 produced a snapshot | Four complete snapshots survive and are measured below. |

The first two attempts' missing magnitudes must not be inferred from the third.
Recovering them would require reproducing those implementations; that was not
done because the current instruction is diagnosis only. A167's third run was
interrupted, so neither its geometry nor a hang has been established.

Sources are `out/route-cost/page-down-attempt1.log`, `page-down-attempt2.log`,
`page-down-attempt3.log`, the 673-row `page-down-before-side-fix.jsonl` reference
and the surviving four-row `page-down-before-side-fix.actual.jsonl` snapshot.
The derived measurements are in
`out/route-cost/migration-diagnosis-saved-snapshots.json`.

## Measured differences in attempt 3

Coordinates are page pixels from the geometry harness's coordinate mapper.
Route length is the sum of Euclidean segment lengths. Bend counting uses the
same cross-product threshold as the route-cost measurements. The length values
below are displayed to three decimals; acceptance still compares exact doubles.

| Scenario | Changed routes | Node-centre displacement | Bends, reference to attempt | Total route length, reference to attempt |
|---|---:|---|---:|---:|
| F17 | 3/3 | RN moves +30 in X; other centres do not move | 0 to 0 | 195.000 to 165.000 |
| F30 | 5/5 | None | 6 to 5 | 899.600 to 1615.500 |
| F61 | 4/4 | Two centres move by 180 in Y, in opposite directions | 0 to 8 | 300.000 to 1060.000 |
| F153 | 163/163 | All 160 centres shift -14 in X | 146 to 107 | 51720.037 to 69966.039 |

### F17: a missed terminal conversion and a separate tiny difference

The box-to-rounded-box route moves from Y=128 to Y=248, despite those two node
centres remaining at Y=128. RN moves from X=293 to X=323, and its outgoing
connection shortens by 30 pixels. These are substantive geometry errors, not
rounding. They are consistent with remaining helpers interpreting a side or
box boundary in the old direction, followed by gap/placement repair reacting
to the wrong terminals. The first diverging stage was not captured, so that
causal sequence remains a hypothesis to verify.

The diamond-to-circle route also changes its final Y from
127.99999999999999 to 128: a difference of approximately
1.4210854715202004e-14 pixels. Its sides and point count remain unchanged.
Outline trigonometry and changed subtraction order are plausible causes of
this separate floating-point discrepancy. It is not grounds for accepting
the 120-pixel terminal error or weakening exact comparison.

### F30: unchanged placement, wrong terminal semantics

All node centres stay fixed. Several rectangular ports move by a full node
height: for example, A's departure changes from Y=128 to Y=8, and B's arrival
from Y=218 to Y=338. The three diamond departures keep their point locations
but change their declared exit side from bottom to top. All three targets'
top entries move to the opposite edge, 120 pixels lower. One route changes
point count, and total length grows by 715.900 pixels.

These are direct evidence of an incomplete side/outline conversion. Diamond
and initial port code were changed, while other terminal writers and decision
route builders still used upward-Y assumptions. Tie-breaking alone cannot
explain a port labelled top appearing on its target's bottom edge.

### F61: terminal errors feed a different placement

Two routes grow from two points to six points; one route changes from
top/bottom sides to bottom/top. The node movements are whole-grid 180-pixel
changes. The prototype still has an upward-Y `AsBox` in the joint placement
search and upward-Y conditions in final aligned-route comparisons. These are
specific missed conversions that can change both candidate costs and their
legality, causing a different winning placement.

Candidate order may also matter, because numerical Y sorting and nudge order
were not migrated consistently. The saved final snapshots cannot distinguish
a changed cost from an equal-cost tie. A candidate trace is needed before
attributing this to tie-breaking.

### F153: canvas translation plus real routing changes

The uniform -14-pixel X shift is consistent with changed canvas extents or
label clearance; it must be checked separately from raw model placement.
Several renderer extents and label-obstacle helpers still use the old axes.
However, a canvas translation cannot explain the whole failure: 36 routes
change point count, and total route length grows by 18246.002 pixels.
Unconverted route comparisons and candidate ordering are likely additional
causes. Their first diverging stage was not retained.

The renderer's private point mapping and enterprise-specific mapping were
also not fully migrated. These harness measurements therefore do not claim
to be a complete validation of the unfinished SVG output.

## Proposed plan before any further migration edits

1. Freeze `7b1eb66` as the accepted reference. Capture immutable snapshots for
   all 673 cases at every placement, frame, allocation, routing, repair,
   accepted-candidate and rollback boundary. Record the original coordinate
   origin/flip anchor, exact double bits, slot order, normals, title strips,
   active hints and final canvas extents. Serialize during the observation;
   retaining mutable layout references would lose the earlier states.
2. Establish the first divergence for the five focused cases before changing
   algorithms. Normalize the reference coordinates only inside the comparison
   harness, using the recorded stage convention. Compare raw model geometry
   separately from the SVG origin and guides. A global translation still
   fails final acceptance; it is only a diagnostic classification.
3. Inventory every geometry consumer and specify its page-down equations.
   Include `BoxRef`, joint-search `AsBox`, all terminal/fallback writers,
   diamond offsets and normals, title-strip crossing exemptions, compound
   bounds, collisions, frame reconstruction/restoration, route comparisons,
   hint checks and all renderer/enterprise mapping paths. North must mean
   decreasing Y in every shared-engine consumer. Keep Dagre's direction
   transforms; put optional Sugiyama output behind one conversion boundary.
4. Specify candidate order as well as coordinate equations. Preserve existing
   visual order, stable edge-id ties, permutation/nudge order and strict score
   comparisons. For example, upward-Y descending order becomes page-down
   ascending order. Keep the same named 16-move, 3-operation and 12-line limits;
   do not change weights or remove a previously offered candidate.
5. Preserve the existing outline calculation and rounding boundaries. Negate
   offsets/normals consistently rather than recomputing equivalent angles by
   another trigonometric branch. Verify exact circle/diamond endpoint bits
   and arithmetic evaluation order. Do not introduce epsilon acceptance,
   endpoint snapping or updated reference rows to hide a difference.
6. Only after that inventory and trace are reviewed, migrate the dependent
   consumers as one coherent change: remove the placement flip, repair mirror
   and renderer reflections together. The stage traces must match before the
   final output is considered. Retain positive finite frame dimensions, nested
   containment, exact rollback and north-above-south invariant checks.
7. Run F17, F30, F61, F153 and A167 first, then compare every route and guide for
   all 673 cases exactly. Run the complete audit and retained regressions only
   after zero geometry differences. Commit only with exact equality, identical
   hint outcomes and unchanged zero gates; otherwise retain the first-divergence
   evidence and leave the migration uncommitted.

No part of this migration plan has been applied.

# Review cell-induced straightness and side findings

This follows the finished-layout bend review. The comparison uses the retained
cells-off and cells-on catalogue captures after the original-hint correction
531ab82. It includes all 673 summaries, not only the original 81 bend cases.
Production cells and independent spans remain disabled.

## Every increase

Counts are raw checker findings on the final published model. An absent rule
field in a successful summary is zero; native layout-failed rows are not
invented measurements. Each row below is a scenario/rule pair, so the thirteen
scenarios occupy seventeen rows.

| Rule | Scenario | Cells off | Cells on | Cause group |
| --- | --- | ---: | ---: | --- |
| 4.2 | A115 | 0 | 3 | Direct node obstruction |
| 4.2 | A163 | 1 | 8 | Direct node obstruction |
| 4.2 | A164 | 1 | 2 | Direct node obstruction |
| 4.2 | A165 | 0 | 3 | Aligned coordinates, incompatible terminal directions |
| 4.2 | A167 | 0 | 1 | Direct node obstruction |
| 4.2 | A169 | 0 | 1 | Direct node obstruction |
| 4.2 | EIP137 | 0 | 1 | Aligned coordinates, incompatible terminal directions |
| 4.2 | EIP18 | 0 | 3 | One node obstruction; two incompatible terminal directions |
| 4.2 | EIP301 | 0 | 1 | Aligned coordinates, incompatible terminal directions |
| 4.2 | EIP332 | 0 | 1 | Aligned coordinates, incompatible terminal directions |
| 4.11 | A165 | 0 | 2 | Changed frame aspect ratio changes the facing side |
| 4.11 | A63 | 0 | 1 | Containing-frame side assignment; incomplete candidate family |
| 4.11 | EIP236 | 0 | 1 | Joint fan/convergence side assignment; incomplete candidate family |
| 4.11 | F136 | 0 | 1 | Forced partner alignment removed; remaining facing option adds a shared track |
| 5.8 | A63 | 0 | 1 | Containing-frame side assignment; incomplete candidate family |
| 5.8 | EIP236 | 0 | 1 | Joint fan/convergence side assignment; incomplete candidate family |
| 5.8 | F136 | 0 | 1 | Forced partner alignment removed; remaining facing option adds a shared track |

The positive 4.2 deltas sum to 22, offset by 14 fewer findings elsewhere:
A107 3->2, A117 1->0, A141 3->2, A142 1->0, A153 1->0, A70 1->0,
EIP266 3->2, EIP296 3->1, EIP42 2->1, EIP78 2->1 and F153 3->0.
The positive 4.11 deltas sum to five, offset by A142 3->0. There are no
5.8 decreases. Thus the complete totals remain 44->52, 3->5 and 0->3.

## Direct node obstruction: preserve legality

There are sixteen obstructed straight replacements among the 24 current 4.2
findings in the ten increased scenarios. The test temporarily substitutes the
exact two-point route at the already allocated ports, checks it, and restores
the original point objects in a finally block. Every rejection names its node.
`cell-quality-direct-rejections.csv` records all 24 substitutions and their
scores, crossings and overlap lengths.

Example: A169's analyst->collaborator:11 would cross visualiser. A163's eight
m*1->m*3 links each cross the corresponding m*6 node (m11->m13 crosses m16).
A115's adminLaptop->internet crosses replication, containerSvc, mqSvc, failover
and backupDue. A164's configure->bwmSvc crosses define, manageRes and onboard;
collabGrp->collabMgmtSvc crosses sendNotif. A167's secCustodian->admin crosses
owner. EIP18's Caller->Service crosses note-0.

The fix is already in the legality-preserving final straight-candidate pass:
keep the legal detour. Restoring a pixel nudge merely to stop aligned coordinates
from triggering 4.2 is not a cell-model fix. These are correct rejections for the
current regions and ports; this is not proof that no better whole-cell placement
exists. Scored placement remains bounded by the unchanged named limits.

## Aligned coordinates with incompatible terminal directions

Eight more replacements violate 3.1 terminal normals. Equality of one terminal
coordinate alone does not make a legal straight run. Example: EIP137 B->A has
right/right ports, X=150 at both ends and different Y values. A vertical segment
would be tangent to both right sides. Cells off also used right/right ports,
but X=165 and X=135, so the same raw 4.2 predicate did not flag its bent route.
EIP301 and EIP332 have the same mechanism. EIP18's two Service->Caller replies
also have right/right ports after joint allocation. A165's values->reqs has
left/right ports at equal X; reqs->prog and goals->prog have bottom/top ports
at equal Y. All eight must bend from those allocated terminals.

Keep the normal-preserving route; do not add a fractional nudge, remove a hint,
or weaken the checker. The parked F32/EIP261 4.2 refinement remains separate.
These additional raw findings are a consequence of valid whole-cell geometry,
not evidence of a lost legal straight candidate at these exact ports.

## Convergence and fan side assignment: still open

These increases are real final side differences or changed facing-side
expectations. They are not all established necessary consequences of the rules.
The existing side search constructs routes, reapportions displaced ports and
compares the complete assignment; it does not score only the requested side.
Observed rejected candidates are:

| Scenario | Observed candidate result | Retained limitation |
| --- | --- | --- |
| F136 | Four-line facing repair removes both remaining side findings but adds one 5.1 finding; six-line option also leaves a new shared track | Partner centring was intentionally removed. Need a better legal side/track or scored whole-cell option, not reinstated alignment. |
| A63 | Complete two/three-line options remove one/both side findings but introduce one/two 4.2 findings or 4.9 | shop is a containing frame. The bounded candidate family has not supplied an option preserving all current finding counts. |
| EIP236 | Complete three-line option removes both findings but introduces 4.2 | Need a joint side/route alternative that avoids the new aligned detour, not a relaxed acceptance filter. |
| A165 | Complete seven-line option removes both convergence findings but adds two 4.9 findings; other options fail route completion | Whole-cell frame fitting and hint closure widen prog. Its centre is (4050,1380), reqs (1245,2400), goals (5565,2400). The dominant-axis expected entry is left/right, while the retained legal routes enter top. |

A165 illustrates a changed facing expectation: both bottom/top assignments were
also bottom/top with cells off, but the formerly taller/narrower frame made top
the expected convergence side. The change in frame aspect ratio is valid; it
does not prove that the resulting side findings must remain. Hints and legality
remain authoritative. None of the open side cases is labelled an unavoidable
rule conflict.

## Check for unfinished scoring and leftover centring

`SearchCellAssignments` requires CellFinalRoutingReady, assigns ports, rebuilds
routes, refreshes hinted routes and invokes ModelRouteCompletionStage before
measuring the candidate. Rejected candidates restore the complete snapshot.
`RepairFinalFacingSides` builds every affected allocated route before comparing
findings and score, and rejects incomplete assignments.

As a diagnostic trial, the cell-only facing assignment also ran the final
aligned straightening pass before its acceptance comparison and rechecked the
unchanged affected-line limit. All thirteen scenarios retained exactly the same
4.2/4.11/5.8 counts. The trial was removed; no speculative extra pass is retained.
There is no demonstrated premature-scoring bug among these increases.

The converted placement review already bypasses terminal-fan, nested-group,
shared-fan, decision, partner and terminal-child centre assignments for bound
nodes. F136's changed partner alignment is explicitly documented in the
Superseded section of cell-census-first-catch-review.md. The active hard guard
finds zero illegal pixel writes in these completed cases and the full catalogue.
A zero guard count alone would not exclude a whole-cell centring assignment;
the source bypass review is the separate evidence for that. No remaining
unconditional centring pass was identified as a cause in these thirteen cases.

No functional engine change is retained in this review. Tests preserve the
published legality, cell projections and original hints, and require each of
the 24 direct replacements to have an explicit legality rejection. Side-search
traces and cells-off/on geometry are emitted for repeatable diagnosis. Remaining
side candidate-quality work is open, rather than hidden by checker changes.

## Validation

Final validation and timings are appended after the complete audit runs below.

The final unprofiled audit emits all 673 cells-on summaries byte-identical to
the retained pre-review capture: 120.35 s, A142 49.9605 s. Cells-off emits all
673 summaries byte-identical to the original baseline: 44.98 s, A142 13.5332 s.
The nonzero audit exit remains the documented quality/shared-track findings
and five unmeasured native cases, not a new hard-gate failure.

| Counter | Cells off | Cells on before review | Cells on after review |
| --- | ---: | ---: | ---: |
| 4.2 | 44 | 52 | 52 |
| 4.11 | 3 | 5 | 5 |
| 5.8 | 0 | 3 | 3 |
| 5.9 | 1694 | 1234 | 1234 |
| Shared-track pairs | 2089 | 346 | 346 |
| Illegal paths | 0 | 0 | 0 |
| Missing routes / no legal route | 0 / 0 | 0 / 0 | 0 / 0 |
| Title bands / frame overlaps | 0 / 0 | 0 / 0 | 0 / 0 |
| Grid / off-grid nodes / off-grid frame edges | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 |
| Off-slot ports / off-slot groups | 0 / 0 | 0 / 0 | 0 / 0 |
| Hint accounting / cleanup corrections | 0 / 0 | 0 / 0 | 0 / 0 |

No engine, rules, baseline, weights, hint behaviour, limits or activation flags
changed. DOCX files are excluded from this checkpoint; nothing is pushed.

311 existing focused tests and 73 cell regressions pass (the previous 60 plus
13 new source-based review cases).

# Recover complete candidates during incremental cell scoring

## Profile first

The retained profile before this change is 127.27 s / A142 49.6515 s.
Exclusive routing completion costs 94.6291 s, frame search 3.8677 s,
scoring outside routing 2.0412 s, hints 0.6980 s and final diagnostics 0.2574 s.
Routing is the cost to remove, not omitted diagnostics or hint checks.

## Incremental candidate completion

For a large diagram, dependencies include changed endpoints, old/new node
obstacles and title bands near individual route segments, and every neighbour
whose symmetric terminal allocation changes. The unchanged routes retain their
exact sides, normals and points. Candidate construction uses the established
terminal, facing-side, straightening and distinct-track families on that set.
Bends, disconnections, route costs and unchanged crossing pairs are reused;
only affected lines and their interactions are recomputed. Original active
hints are reapplied to affected lines. No hint-result cache is introduced.

The proposed winner runs complete reference completion before publication.
It must preserve all paths outside its dependency set and all the existing
hint, frame, length, bend, changed-line and diagnostic checks.

The earlier trials lost seven good moves because rejecting their provisional
winner did not recover a different complete option. Recovery now rechecks the
same already charged assignments, not additional candidates. A diagram whose
entire route inventory fits the existing twelve-line coupled-repair limit
retains its exact joint completion. For larger diagrams, an overflowed or
unroutable scope is rejected; completed scoped alternatives can invoke reference
recovery if none survives. Existing hard faults also retain full recovery so a
provisional slot fault cannot suppress the legal alternative.

This is conservative incremental scoring with an explicit full-completion
fallback, not a claim that every dependency can already be certified locally.
The fallback and small coupled diagrams are the remaining routing cost.
The sixteen distinct-assignment limit and all named limits stay unchanged.

## Slot acceptance and rollback

A candidate must have zero path-interior, diagonal and slot faults; merely
retaining the pre-search slot-fault count is insufficient. This addresses F58's
published-slot regression without changing its checker, projecting its cylinder
specially or adding an unconditional final allocation. The legacy centre-ray
projection issue remains an allocation limitation; it is not hidden by a
changed tolerance. Recovery can select the existing legal complete assignment.

Both guide-content and renderer-cache snapshots remain exact. New recovery
coverage forces a completed exception with the candidate budget already fully
charged, then checks exact restoration for A58, A138 and EIP84. No extra candidate
budget is granted. Incremental score/reference tests cover moves and span changes
with an independent frozen route. Eight source regressions pin all seven lost
moves and F58's exact slot gate.

## Measurements before adding new placement candidates

| Instrumented exclusive category | Before seconds | Incremental seconds |
| --- | ---: | ---: |
| Routing completion | 94.6291 | 45.5790 |
| Frame search | 3.8677 | 4.0497 |
| Scoring outside routing | 2.0412 | 2.0663 |
| Hint validation | 0.6980 | 0.8827 |

Profiled full audit: **90.35 s**, A142 **11.5310 s**. Unprofiled full audit:
**64.27 s**, A142 **9.50791 s**. Cells off: **39.74 s**, A142 **13.6054 s**.
The full-audit 45-second target is not met; A142 is under fifteen seconds.
The profiled inclusive routing total contains nested calls and must not be
added to the exclusive categories. Native placement, other pipeline work,
audit comparisons and host/output costs also remain outside the table.

All 673 cells-off summaries are byte-identical to the original baseline.
All 673 cells-on summaries preserve the retained catalogue totals: 4.2=52,
5.9=1234, 3.1=2, 4.1=0, 4.11=5, 5.8=3, shared pairs=346. Every legality,
grid, slot, frame, title-band and hint-accounting gate is zero. Five known native
skips/empty sources and existing quality/shared-track findings still make the
audit exit nonzero.

The original 81-case replay matches every retained bend count, total 1332.
The original worse-fifty therefore remain fifty before the new joint candidate
work: 24 shared-fan cases, 12 main-chain cases and fourteen other cases.
Production cells and independent spans remain off. Rules, weights, original
hints and the parked F32/EIP261 refinement are unchanged. No DOCX is staged and
nothing is pushed.

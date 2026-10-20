# Enterprise partner placement and v3 review — 20 October 2026

The uncommitted band, singleton and production-test-path changes survived the
usage-limit pause. HEAD is still `ce6f1ce`, **Define bend-removing port reassignment**.
The supplied `../Merge/20261020-0505-codex-limit` backup exists. No missing work
was found. No band engine was committed, no rules or limits were changed, no
existing test assertion was edited, and no DOCX was regenerated or pushed.

## Why the singleton test passed while production drifted

The old `SingleElementBandOverlapsItsPartnersColumns` test calls
`EnterpriseBandPlacement.Apply` directly and ends there. It never performs
production wrap selection, corridor reservation, port allocation or routing.

Two production failures were reproduced before the correction:

- A116: `payRun` is initially aligned at column 22 with `auditTrail`. In
  `route-coordinate-preparation`, skip-band corridor reservation displaces it
  to column 24 while its partner stays at 22.
- A117: the initial partner pass places `sponsor` at column 0 before timeline
  arrangement puts `programme` at column 8. Corridor reservation later moves
  `sponsor` to column 2, still far from that actual partner.

The before-stage evidence is
`out/markup-review/band-partners-v3/before-corrected-trace/` and
`singleton-stage-changes.tsv`. The first diagnostic trace serialized an internal
region as `{}`; that diagnostic was corrected to record explicit row and column
properties and rerun. It is not evidence of a missing production region.

Completion now revisits partner alignment after timeline arrangement and after
placement hints and measured span growth. Each complete assignment is applied
atomically through the cell grid. The attained unhinted singleton proximity is
part of complete-candidate validation, so a later corridor or repair must use
another legal assignment rather than silently undo it. Partner columns are
read from the candidate itself, not frozen to the first placement. Complete
hint-controlled units and their reference peers remain exempt from automatic
repositioning; the hint validators retain control of them.

## A115: required clearance versus avoidable spacing

Two faults were distinct from legitimate nested frame clearance:

1. `FreeFor` reserved a root frame's columns throughout every semantic band it
   spans, regardless of the candidate row. This treated space above or below
   the actual rectangle as occupied, forcing ungrouped elements to the edge.
   Completion now tests the actual projected frames and the existing frame
   conflict checks, with node bounds derived from the same cell candidate.
2. Initial frame-block packing reserved more empty columns than those measured
   frames required. Whole sibling blocks now compact together, preserving their
   order, all members and shared columns across bands. A proposed assignment
   cannot add a projected frame conflict, violate aspect order or share cells.

The measurement-only before-compaction run tried unpublished complete candidates
and verified the original snapshot remained exactly unchanged. With the other
members fixed, `shopImg2` could legally move 23→20 and `sydneyOrders` 56→53.
Their former four-column gaps were therefore not all required by nested frames.

The final plan compacts both sides of these blocks. Two-column members occupy:

| Pair | Before starting columns | Final starting columns | Empty columns between members |
| --- | --- | --- | --- |
| `shopImg1`, `shopImg2` | 17, 23 | 14, 17 | 4 → 1 |
| `shopImg3`, `sydneyOrders` | 50, 56 | 42, 45 | 4 → 1 |

The remaining column is retained by the projected nested-frame clearance checks;
it is not an arbitrary new spacing value. Frames still include their measured
titles and padding. No frame checker or required clearance was relaxed.
The final measurement confirms the lower bound: moving `shopImg2` 17→16 adds
overlaps between `perthApp1`/`perthApp2`, their runtime frames, and foreign-node
clearance faults. Moving `sydneyOrders` 45→44 similarly overlaps `sydneyApp` and
`sydneyData` and their runtime/database frames. Thus the retained column is
required by this final geometry, while the other three columns were avoidable.
These exact fault identities are in `final/A115-clearance.json`.

Mixed bands use **all actual partners**, including hosts in the same band;
singleton bands use their partners in other bands. An intermediate version used
cross-band pulls for mixed bands too. That left services pulled towards the
Application edge; the final version corrects this. Intermediate A115 captures
and the first v3 image are retained separately under
`out/markup-review/band-partners-v3/render-intermediate/`.

## Production tests and measured views

`EnterprisePartnerPlacementTests` tests the complete production pipeline:

- A116 and A117 singleton bands finish within one column of the actual partner
  median.
- In A115's mixed band, each member is within one column of its nearest band
  neighbour, unless its actual partner median justifies the separation.
- The same tests require complete routes, no node/title/terminal legality
  findings, legal authoritative cell projections and no unrelated frame overlap.

The valid three cases fail on the old engine and pass after the correction.
An initially proposed A113 mixed-band case had no mixed band, so it was a fixture
selection error and was removed; no assertion was weakened to make A113 pass.
The 28 existing focused band tests also pass. A117 retains its three level
Implementation rows and its band membership/order. This targeted result is not
a certification of every catalogue gate or the historical 205-failure suite.

Counts below use physical segment-pair crossings and completed routed geometry.
The before column is the retained row-fix engine, not the rejected shared-column
trial whose v2 images were previously shown.

| View | Bends before → v3 | Crossings before → v3 | v3 width × height | v3 aspect |
| --- | ---: | ---: | ---: | ---: |
| A113 | 164 → 164 | 142 → 142 | 1980 × 3210 | 0.617 |
| A115 | 79 → 82 | 122 → 66 | 8640 × 1530 | 5.647 |
| A116 | 102 → 101 | 104 → 104 | 5010 × 930 | 5.387 |
| A117 | 106 → 107 | 67 → 67 | 3930 × 1464 | 2.684 |

A114 is hash-identical at 136 bends, 352 crossings and aspect 0.296296.
A115 trades three extra bends for 56 fewer crossings; A117 gains one bend while
honouring singleton alignment. These are disclosed changes in uncommitted band
work, not a claim that every old no-bend-increase condition has passed.

## Native v3 renders

All images use `MermaidDiagramExport.TryRenderPng`, the DOCX image dispatcher,
and their published geometry hashes are checked against the production captures.

- `out/markup-review/band-views/A113-after-v3.png`
- `out/markup-review/band-views/A115-after-v3.png`
- `out/markup-review/band-views/A116-after-v3.png`
- `out/markup-review/band-views/A117-after-v3.png`

Evidence is under `out/markup-review/band-partners-v3/`: final captures, test
logs, clearance proposals and render logs. A113's narrow graph ranks, the dense
A116 local layout, diagonal drift and the shared-band shape work remain queued;
this correction does not claim to solve them. The approved test-only stress
watchdog is the next item. `docs/codex-status.md` records progress and all
subsequent measurements without being committed.

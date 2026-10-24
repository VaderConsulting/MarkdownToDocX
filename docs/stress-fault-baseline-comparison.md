# Stress faults compared with ce6f1ce

20 October 2026. This work is **partially complete**: the 23 introduced off-slot
endpoints are fixed, but A107's additional 4.2 finding and A110's 195 new
shared-track identities remain unresolved. They are not relabelled as known
pre-existing faults or accepted as zero gates. No engine or band commit was made.

## Isolated comparison

Detached worktree:
`C:/Users/windo/AppData/Local/Temp/md2docx-stress-ce6f1ce-20261020`.
Its HEAD is ce6f1ce. Only measurement fixtures and the canonical test-watchdog
metadata were added there; `git status --short -- src docs/Instructions` is empty.
No stash, reset, checkout or replacement of the dirty main worktree was used.

Both engines used the same catalogue sources, explicit `UseCellRegions=true`,
Release configuration and the same fault-capture fixture. The route checker
source hashes match exactly. The off-slot helper is unchanged. Every comparison
case completed under the unchanged 180-second stress supervisor. There are ten
unique cases: F59 was already included in the nine failures, so it is not counted
a second time. Raw baseline, original-current and repaired captures are under
`out/markup-review/stress-fault-comparison/{ce6f1ce,current,slot-fixed}/`.

Types below are deliberately distinguished:

- **S:** rule 3.2/4.7 slot findings. These are zero for all ten cases in both
  engines and after this fix.
- **O:** off-slot endpoints from the existing symmetric endpoint audit.
- **P:** the audit's printed `port` aggregate: rules 3.1, 3.2/4.7, 3.3/5.2, 4.2,
  4.11 and 5.8. For these ten cases the nonzero P entries are entirely rule 4.2;
  they are not three or eight perpendicular-terminal failures.
- **H:** rule 5.1 route pairs sharing positive-length track. Counts are pairs,
  not the number or length of individual overlapping segments.

| Case | ce6f1ce S/O/P/H | Before fix S/O/P/H | Introduced current identities | After fix S/O/P/H |
| --- | --- | --- | --- | --- |
| F59 | 0/0/0/0 | 0/1/0/0 | O 1 | 0/0/0/0 |
| F152 | 0/0/0/0 | 0/10/0/0 | O 10 | 0/0/0/0 |
| F153 | 0/0/0/7 | 0/1/0/6 | O 1; all six H are pre-existing | 0/0/0/6 |
| A104 | 0/0/0/0 | 0/2/0/0 | O 2 | 0/0/0/0 |
| A107 | 0/0/2/0 | 0/0/3/0 | P 1 | 0/0/3/0 |
| A110 | 0/0/0/58 | 0/0/0/215 | H 195; only 20 old pairs remain | 0/0/0/215 |
| A111 | 0/0/0/0 | 0/1/0/0 | O 1 | 0/0/0/0 |
| A159 | 0/0/0/0 | 0/1/0/0 | O 1 | 0/0/0/0 |
| A160 | 0/0/0/9 | 0/0/0/9 | None; identical nine H | 0/0/0/9 |
| A163 | 0/0/8/0 | 0/7/8/0 | O 7; identical eight P | 0/0/8/0 |

A110's net H rise is 157, but the introduced-identity count is **195**: 38 of the
old 58 pairs disappeared, leaving 20 old and 195 new pairs. Equal totals or a
smaller aggregate cannot establish identity parity. All current faults, with
origin and post-fix status, are listed individually in
`out/markup-review/stress-fault-comparison/fault-identities.csv`. All 84 baseline identities of these types (including the 39 no longer present) are recorded in `ce6f1ce-known-fault-identities.csv` in the same directory.

## Fixed source: 4.12 invented an unallocated slot

The alignment candidate in `FlowchartBendRemovingPortRepair.Candidates` could
project a lone endpoint onto its partner's approach coordinate. That could
straighten a line while moving its port away from its allocated symmetric slot.
The route checker did not flag these endpoint-audit mismatches, so its finding
identity guard was insufficient to reject the candidate.

The candidate now exchanges an existing, unhinted slot at the desired coordinate
with the line's current slot. It never invents a coordinate. The candidate stays
in the same budget position even when no matching slot exists; subsequent
candidates and the 16-candidate limit are unchanged. Existing complete-candidate,
port-order, separation, hint, crossing and rollback guards still apply.

All 23 introduced off-slot endpoint findings disappear. All node/frame bounds are
unchanged across the ten captures; A107, A110 and A160 have byte-identical
serialized geometry hashes before and after this fix. Supporting routes change
only in the seven slot-repaired cases. The per-route list is recorded with the
comparison artifacts. Honoured hinted routes are excluded by the existing pass.

Three focused 4.12 tests pass: lone allocated centres, accepted same-side swap,
and crossing-rejected swap with exact rollback. No assertion was weakened.

## A107: assertion omission and remaining placement fault

The former final audit assertions checked section 10/shared tracks, off-slot
endpoints, cleanup and frame/title faults, but did not assert the printed port
aggregate for every stress case. A107 therefore passed while reporting:

- `erp->shop:8`, rule 4.2 — already present at ce6f1ce.
- `retryQueue->retryQueue:9`, rule 4.2 — already present at ce6f1ce.
- `approve->draft:6`, rule 4.2 — introduced by the band placement.

A new assertion in the existing stress catalogue test enforces **the same printed
port aggregate equals zero** for every canonical stress identity, after emitting
the summary. A107 now correctly fails with all three identities in the message.
No checker threshold or normal-scenario expectation changed. Known faults stay
visible failures rather than becoming expected-zero exceptions.

The introduced feedback finding arises because the band placement puts approve,
submit and draft in one column. Approve and draft have equal right-edge X=150,
but submit lies between them, blocking a direct vertical route. The existing
same-axis straightening candidates collide with the partner. Private neighbour
candidates make the feedback straight, but the whole-candidate guard finds:

- moving approve: `submit->approve` 0 to 4 bends and `approve->fulfil` 0 to 2;
- moving draft: `draft->submit` 0 to 4 bends.

They were rejected, with exact snapshot equality after diagnosis. The next
candidate needed is a joint chain placement and port/track assignment, not a
single endpoint shift or a forced port nudge. That candidate is **not built or
activated** here; this introduced fault remains open. The failed diagnostic
source/test pair is restorable under
`docs/archive/stress-fault-neighbour-probe-2026-10-20/`; its optional engine API
was removed from the live tree after the measurement.

## A110: introduced band/track regression remains open

The dense multi-band mesh changes from 58 to 215 shared pairs. The compact band
wrap and changed leaf/port ordering feed independent routes into coincident
tracks; scoring the complete wrap does not make those shared tracks legal.
Every existing measured wrap already has shared tracks: widths unwrapped, 6, 3,
2 and 1 report 345, 345, 333, 286 and 215 pairs respectively. Selecting another
existing wrap therefore does not remove the introduced regression.

Three existing repair families were measured on private complete copies:

| Trial | Shared pairs | New identities versus current layout | Outcome |
| --- | ---: | --- | --- |
| Terminal first-bend repair | 215 | none | No change |
| Internal-run translations | 177 | H 11; rule 5.9 2 | Rejected |
| SeparateTracks | 109 | H 35; rule 5.9 94; section 10 2 | Rejected |

The internal trial reduces physical score crossings from 536 to 511, but it
still creates new fault identities. SeparateTracks reduces them to 461 but makes
two paths illegal. Neither is safe to wire into production under the unchanged
guards. The original model's exact snapshot is unchanged after all three trials.
A joint, band-aware allocation of ports and tracks for the mesh is still needed;
no safe completed candidate was found in these existing families. No new rule or
limit is proposed as a substitute for the missing engine work.

## Verification and remaining state

Final named audits: F59, F152, A104, A111 and A159 pass. F153, A107, A110, A160 and A163 remain red on the explicitly listed faults. A107 now fails at the new stress port assertion with all three identities. All ten summaries are emitted; every off-slot field is zero. The A115 regression passes under its unchanged 59-second watchdog. The three focused 4.12 tests also pass. Logs and TRX files are in `out/markup-review/stress-fault-comparison/final-gates/` and `slot-fix-focused.trx`. The final measurement-fixture build succeeds; existing nullable/XML documentation warnings remain. The canonical
59/180-second watchdog policy, stress quality exclusions, band membership/order,
A117 timeline, rule text and values are unchanged. The only retained engine
change in this follow-up is the allocated-slot exchange fix. No DOCX, push,
commit or band-engine acceptance is claimed.

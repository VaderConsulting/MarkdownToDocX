# Stress corridor and band-column results

HEAD remains `ce6f1ce`. The band engine and this work are uncommitted. No rules,
limits, checker thresholds, existing test expectations, DOCX files or watchdog
settings were changed in this item.

## Outcome

| Retained production measurement | Before | After | Ceiling test |
| --- | ---: | ---: | --- |
| A110 shared-track pairs | 215 | 215 | Fails the unchanged ceiling of 58 |
| A107 port findings | 3 | 2 | Passes the unchanged ceiling of 2 |
| A107 bends | 11 | 10 | One fewer bend |

A107 is fixed at its source in initial band column selection. A110 is **not
fixed**: no corridor-routing change was retained. Its chosen corridors have
measured capacity shortfalls. This is a partial result, not zero-gate or
whole-queue acceptance.

## A110: route construction and capacity blocker

`DiagramGridRouteBuilder.RebuildAllRoutes` invokes `BuildRoute` independently for
each allocated line. The route cache also stores independent paths. The corridor
families in `EnumerateCandidates` and `BuildForcedCorridorRoute` have no shared
track reservation. Several arms therefore choose the same corridor and the
same approach run. Merely assigning different central X coordinates would leave
the overlapping horizontal approach runs intact.

For example, the customer-lookup lines to Take Order, Amend Order and Cancel
Order all enter the left corridor through Y=1050 and share X=-54 over substantial
lengths. These coordinates are from the route-model capture, not page-row indices.

The measurement uses the existing capacity formula from
`EnterpriseBandRoutingSpace`: two 20 px corner allowances plus `(n-1)` times the
unchanged 30 px track spacing. It excludes terminal runs from the channel census;
those retain their exact allocated port coordinates. It samples every interval
between node boundaries and internal-run endpoints, groups simultaneously active
internal runs by their free channel, and records the worst section of each
finite channel. Open exterior channels have no finite-width shortfall; their
entry and exit gaps can still be short.

The following are requirements of the **currently selected corridor assignments**,
not proof that every possible side/track reassignment is impossible:

| Channel orientation | Free coordinate interval | Simultaneous lines | Available px | Required px | Shortfall px |
| --- | --- | ---: | ---: | ---: | ---: |
| Horizontal | Y 240–300 | 2 | 60 | 70 | 10 |
| Horizontal | Y 390–450 | 2 | 60 | 70 | 10 |
| Horizontal | Y 1020–1080 | 14 | 60 | 430 | 370 |
| Horizontal | Y 2370–2430 | 5 | 60 | 160 | 100 |
| Horizontal | Y 2520–2580 | 5 | 60 | 160 | 100 |
| Horizontal | Y 2670–2730 | 5 | 60 | 160 | 100 |
| Horizontal | Y 2820–2880 | 5 | 60 | 160 | 100 |
| Horizontal | Y 2970–3030 | 5 | 60 | 160 | 100 |
| Vertical | X 150–180 | 9 | 30 | 280 | 250 |
| Vertical | X 330–360 | 8 | 30 | 250 | 220 |
| Vertical | X 510–540 | 2 | 30 | 70 | 40 |
| Vertical | X 690–720 | 2 | 30 | 70 | 40 |
| Vertical | X 870–900 | 2 | 30 | 70 | 40 |

Even a single six-arm service fan needs 190 px in its shared approach under that
formula, versus the available 60 px. The combined channel is more crowded.
All 60 declared lines cross the Y=1050 cut. A gap-wide repair there is therefore
not an ordinary candidate involving at most 12 lines. The whole-fan-with-hub
exception does not authorise treating a gap-wide move as a fan-hub move.

I stopped the A110 item at the requested narrow-corridor branch. No allocator was
published that compresses tracks below the configured spacing, hides a missing
route, or silently stacks arms. The existing stacked routes remain a known
fault; this report does not claim to have removed them. A follow-up needs either
a legal joint reassignment within the existing affected-line limit or an
approved design for atomic gap-capacity growth and its affected routes. No rule
or limit exception was applied or assigned an arbitrary proposed value.

## A107: initial column selection

`EnterpriseBandPlan.Build` now checks blocked direct connections after its private
partner plan is complete and before `EnterpriseBandPlacement.Apply` atomically
publishes regions. It offers free whole columns in the same row for unhinted,
ungrouped blockers. Every proposal must:

- remove a blocker without introducing a new blocked-connection identity;
- preserve fixed bands, local order, occupancy and projected frame legality;
- keep already aligned cross-band partners in their columns;
- avoid increasing the connection-order crossing count, through a thin adapter
  to the existing Dagre adjacent-layer graph and crossing counter.

This is initial placement, not a route-cleanup pass. No partial region assignment
is published. When the enumerated alternatives fail, the original region stays
and a `BAND_CONNECTION_BLOCKED` diagnostic records the endpoints, blocker and
whether it is hint-controlled. The diagnostic deliberately does not claim an
exhaustive proof that no joint layout could work.

The introduced `approve → draft` finding disappears. Its final route is one
elbow: `(180,1095) → (100,1095) → (100,1350)`. The final two port-family findings
are the pre-existing `erp → shop` and `retryQueue → retryQueue` rule 4.2 findings.
Shared tracks remain zero; strict slot and off-slot counts remain zero.

The full production result moves Approve, Fulfil and Invoice from X=75 to X=255.
Submit ends at `(255,1395)` instead of `(75,1245)`, beside Draft. The new initial
planning operation changes columns only; the reported coordinates include the
existing subsequent routed-wrap selection and repairs.

## Rejected trials and independent decisions

1. The first clearance trial passed A107 but changed A113 from 187/147 to 185/249
   bends/crossings. It was not retained.
2. Adding the original Dagre crossing counter produced A113 177/148 and a lower
   score, but undid existing partner alignment and increased its 5.9 findings.
   That version was not retained either.
3. Requiring each individual move to clear an entire blocked relationship at
   once prevented the sequence needed for A107; its port count stayed 3. That
   restriction was removed, not a test assertion.
4. The retained choice preserves existing cross-band partner alignment as well
   as the Dagre crossing criterion. A107 reaches 2 and all four review views
   return to their exact pre-change geometry. This protects the already
   validated singleton/partner work rather than trading it for a local shortcut.
5. A110's capacity shortage was not hidden with fractional track packing, a band
   bypass, a later cleanup pass or an unauthorised larger repair budget.

## Verification and scope

- Both unchanged production ceiling tests were run on the retained state:
  A107 passes, A110 fails at 215. The ceiling fixture also checks zero strict
  slot and off-slot findings.
- All 32 focused initial-band/row tests pass: the existing 28 plus four new
  cases covering TB/BT clearance, a hinted blocker and a partner-aligned blocker.
- The A115 shared-pair regression and the A116/A117 production singleton tests
  pass, 3/3. Timeline and hint cases are included in the focused band tests.
- All 26 non-timeout stress cases were captured before and after with the
  unchanged watchdog policy. Only A107's complete geometry hash changes. The
  other 25 match exactly. Strict slot/off-slot counts are zero in every capture;
  final rule 10, 4.1 and 3.1 counts are also zero.
- Existing shared counts remain F153=6, A110=215 and A160=9. A163's eight existing
  port-family findings remain. These are measurements, not 26 passing gate
  tests. Accumulated stage diagnostics are not all zero and were not cleared.
  A107's historical/intermediate stage log changes from 19 to 20 combined
  `Rule4.2AlignedPorts` records across the set and from 315 to 317 `LineShape`
  records; its completed route checker nevertheless changes from 3 to 2 port
  findings, with no final hard-rule, slot or off-slot fault. No all-gates-zero
  or full normal-suite claim is made.
- A142 was not rerun. Its recorded 182.1 s timeout under the unchanged 180 s
  stress watchdog stands. No new A142 geometry or gate result is claimed.

The before/after stress **capture** invocations took 50.65/49.71 s wall time;
these are not full-catalogue audit timings. The final ceiling invocation took
15.59 s, and the protected production invocation took 31.67 s. No full catalogue
or normal-suite acceptance run was claimed for this partial item.

| Review view | Before/after bends | Before/after crossings | Before/after aspect | Exact geometry |
| --- | --- | --- | --- | --- |
| A113 | 187 / 187 | 147 / 147 | 0.616822 / 0.616822 | Identical |
| A115 | 85 / 85 | 67 / 67 | 5.647059 / 5.647059 | Identical |
| A116 | 103 / 103 | 104 / 104 | 5.387097 / 5.387097 | Identical |
| A117 | 129 / 129 | 67 / 67 | 2.684426 / 2.684426 | Identical |

No new renders were needed for this item: all four geometry hashes match. Older
v3 images are not relabelled as fresh renders of the retained slot-fixed engine.

## Evidence

Root: `C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX\out\markup-review\corridor-and-band-blockers\`

- `before/`, `final/`: exact node/frame/route geometry and every finding for 26 cases.
- `final-stress-comparison.csv`: every case, counts, timings and hash comparison.
- `before-views/`, `final-views/`, `four-view-comparison.csv`: complete review-view measurements.
- `A110-corridor-shortfalls.csv`: each finite deficient corridor's worst section.
- `A110-corridor-shortfall-sections.csv`: all measured sections and exact line IDs.
- `Measure-CorridorCapacity.ps1`: restorable measurement, not an engine switch.
- `unit/final-band-clearance.trx`, `final/ceilings.trx`, `final/protected.trx`:
  actual assertions and their results.

No commit, push or DOCX regeneration. The A110 ceiling is still red and blocks
claiming completion of both requested fixes.

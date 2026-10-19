# Exact allocated-port terminal runs: rejected trial

19 October 2026. Production cells remain on; independent text spans remain off.
No DOCX files were regenerated and nothing was pushed.

## Retained approvals

- `e52b049` adds the approved rule 3.2 text, references it from 4.2 and 4.7,
  and regenerates the rulebook and JSON twin. Validation passes. No conflicting
  rule was found; no parameter or candidate limit changes.
- `c65c9d4` applies the approved rounding and logical-cell test updates separately
  from engine work. A forced elbow exercises actual corner rounding; straight
  fan paths are allowed, and consecutive unrounded direction changes are rejected.
  The decision has one column and two rows; F90 has one 150 px column and measured
  text containment; F93 retains its text checks and checks whole-cell bounds.
  Long-chain size comparisons use logical cell configuration and keep no-overlap
  and near-4:3 wrap-plan requirements. LR remains a live failure at 3210 × 1890.

The approved subset has 18 passes and two existing cells-on wrap failures (LR
and TD). The complete normal layout suite has **1,120 passes and 211 failures**
out of 1,331 tests. Against the former 216 normal failures, six LR/RL rounding
expectations leave the set. The existing cells-on TD wrap failure is now exposed
in the normal run because this fixture explicitly builds cell-owned state.
This is a test-mode exposure, not a new engine regression. It must not be
misreported as “no new normal failing identity.” No wrap assertion was weakened.

## Rejected engine trial

The trial quantised interior track coordinates before candidate validation and
scoring, retained exact first and last port rays through their first bends, and
published those same points without another rounding pass. It did not restore
the archived whole-fractional-polyline implementation. All four cardinal terminal
tests, the three-ports-to-three-next-rank-nodes test and all eight crowded rounded
box variants passed: **13/13 with cells on**, also 13/13 with the normal defaults.

Nevertheless, the complete geometric comparison failed:

| Scenario | Before bends/crossings | Trial bends/crossings | Restored |
| --- | ---: | ---: | ---: |
| A110 | 180 / 463 | 180 / 464 | 180 / 463 |
| A117 | 138 / 86 | 138 / 88 | 138 / 86 |
| A104 | 38 / 0 | 38 / 0 | 38 / 0 |
| A105 | 28 / 0 | 28 / 0 | 28 / 0 |
| A114 | 143 / 353 | 142 / 352 | 143 / 353 |
| Whole catalogue | 2,999 / 2,095 | 2,998 / 2,097 | 2,999 / 2,095 |

All 673 geometric measurement rows were produced. A110 and A117 were the only
scenarios gaining bends or crossings. A104 and A105 have zero total crossings,
and therefore zero same-fan crossings.

### Which end runs caused the increases

The same unmodified intersection and shared-track checks were used throughout.
These are changes in drawn geometry, not a changed checker definition.

1. **A110:** the final runs of `containerHosting → orderManager` and
   `messageQueue → webShop` both become horizontal at Y = 1237.5.
   They overlap from X = 1590 to 1620, **30 px**. Previously their adjacent
   bend points were rounded to Y = 1238, tilting the runs into their exact ports.
   The pair's geometric crossing count rises from one to two, and its shared-track
   finding rises by one.
2. **A117:** the first run of `g3 → target` and the final run of
   `baseline → legacyPayroll` become horizontal at Y = 1147.5. They overlap from
   X = 3575 to 3595, **20 px**. Their former bend points were at Y = 1148.
   The pair's crossing count rises from one to two.
3. **A117:** the final run of `g2 → legacyPayroll` and the first run of
   `g3 → rosterApi` become horizontal at Y = 1102.5. They overlap from
   X = 3575 to 3595, **20 px**. Their former bend points were at Y = 1102.
   The pair's crossing count also rises from one to two.

These three terminal-track collisions require a complete legal track/port or
gap-capacity repair, not a port-coordinate nudge. The existing channel shortfall
search in `DiagramGridLayoutCellRepairs.CellGapShortfalls` explicitly excludes
non-centre ports through `AtConnectionCentre`, so it does not offer the required
off-centre capacity repair. The trial added **zero gap-widening operations**;
the new rule-1.7 capacity repair was not completed or retained. Existing baseline
gap growth was not separately counted. Continuing that work after the failed
acceptance run would contradict the instruction to archive, revert and stop.

### Both requested production audits

Both used `ROUTE_AUDIT_CELLS_ON=production` and `ROUTE_AUDIT_REQUIRE_ALL=1`.

| Run | Audit time | A142 | Summaries |
| --- | ---: | ---: | ---: |
| 1 | 52.20 s | 10.0363849 s | 673 |
| 2 | 51.62 s | 9.8607350 s | 673 |

Every hard gate was zero in both runs: slot ports/groups, grid findings,
off-grid nodes/frame edges, title bands, frame overlaps, missing routes,
no-legal-route failures, illegal paths, hint accounting and cleanup corrections.
Five native-ineligible cases have their explicit unmeasured summary rows.

Quality totals were identical between the two trial audits:
4.2 = 52; 5.9 = 1,190; 3.1 = 2; 4.1 = 0; 4.11 = 5; 5.8 = 3.
Shared-track pairs rose from **337 to 340**: A110 60 → 61 and A117 16 → 18.
These were the only per-scenario finding increases. Geometric catalogue crossings
above and the rule-5.9 audit total are different existing measurements.

## Disposition and evidence

The engine change and its new tests were archived and reverted. There is **no
engine commit**. The restored engine was rebuilt successfully, and named
production layout captures reconfirm A110 180/463 and A117 138/86. The two-case
diagnostic capture intentionally fails the fixture's all-673 teardown; it is not
being represented as a complete audit.

Archive: `docs/archive/exact-port-runs-rejected-2026-10-19/` contains the rejected
engine patch, new terminal tests, exact route-pair trace and both audit summaries.
Full local evidence remains in `out/markup-review/port-runs-*`.

The engine acceptance rules fail on new geometric crossings, new shared-track
findings, catalogue crossings above 2,095, and audit times above 51 s. The engine
failure-set comparison was not pursued after those conclusive failures. The 18
group-2 cases were **not rerun**, because step 6 applies only if the engine change
is kept. Groups 4 and 5 were not restarted.

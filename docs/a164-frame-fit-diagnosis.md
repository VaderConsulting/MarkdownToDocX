# A164 frame-fit candidate diagnosis

The original 97-candidate result was a historical finite-pool measurement of the earlier single-seed frame search, not a proof that the original hints were incompatible. Reproducing it from the earlier placement snapshot with the hint-dependent closure yields exactly 97 distinct complete options. No production limit was changed for this diagnosis.

## Every residual conflict

There is **no one conflict present in all 97 options**. All 97 retain at least one positive-area overlap between these three unrelated frames:

| Frame pair | Candidates retaining that overlap |
| --- | ---: |
| bwmGrp / dsmGrp | 87 |
| collabGrp / dsmGrp | 85 |
| bwmGrp / collabGrp | 77 |

These are Business Workflow Management, Data Source Management and Collaboration Management Service. Each option has between three and fifteen total frame/frame or frame/foreign-node clearance conflicts. `a164-single-set-candidates.csv` names every residual conflict and every region move for every one of the 97 candidates. The counts overlap; they are not separate candidate groups.

The two closest options (2 and 3) both retain:

* `frames|bwmGrp|collabGrp`: the two group envelopes overlap.
* `node|bwmGrp|createWs`: Create Collaboration Workspace violates the foreign-node clearance around Business Workflow Management.
* `node|collabGrp|configure`: Configure and Update Business Process violates the foreign-node clearance around Collaboration Management Service.

These are not evidence of a locked-node impossibility. The search was failing to combine independently nominated moving sets.

## The first missing combination

A165's hint-closure repair is isolated in `6d55987`, independently tested before the A164 search change. `210d917` adds the bounded complete-assignment chain for A164. This replaces the earlier combined local checkpoint; its old history remains available on backup branch `checkpoint/cell-frame-combined-a31638e`. The existing 16-candidate and three-level limits remain unchanged.

In the old placement, a complete assignment moves `define`, `configure` and `retire` up two rows, the seven `dsmGrp` members plus its `sendNotif` dependent down eight rows, and `onboard` right nine columns. Shared row-gap growth is part of that same atomic assignment. Combining the two group sets first leaves only `node|bwmGrp|onboard` (candidate 7); adding the external node clears it (candidate 12). Partial assignments remain private and are never published. `a164-joint-candidates.csv` records all sixteen bounded candidates for that historical placement.

## Correction to the earlier hint-preservation claim

The stronger comparison with a freshly parsed source found that the previous cell hint planner had already deactivated A164's `retire right-of define` before frame fitting. It also deactivated eight A165 member hints: `infoSec right-of dataDriven`, `ineffProc right-of ineffData`, `needData right-of ineffProc`, `planner right-of proponent`, `reduced right-of informedPlan`, `speed right-of reduced`, `landUse right-of speed`, and `engagement right-of landUse`.

The earlier “all hints honoured” statement was too broad: the census checked only hints still active in the mutated contract. It did not detect these pre-existing placement rejections. The 97-candidate experiment and candidate-12 fix therefore applied to a reduced hint contract, not every original declaration.

The original constraints are compatible. The planner was treating an earlier group's current envelope minimum as fixed when a later member hint changed which member defined that minimum. For A164, moving `retire` to the right of `define` changes the `bwmGrp` starting column and was rejected for breaking `bwmGrp below bwmSvc`. Translating the whole group back to its declared starting-column anchor preserves both constraints. No rule requires retaining that group's old member positions.

Checkpoint `531ab82` completes the A165 hint-preserving fix. Its general compensation prepares a complete group and its transitive hint dependents, translates it to retain the earlier group constraint, checks all earlier and current hints together, and commits through the existing whole-diagram hint transaction. A rejected compensation restores the private target plan; no live region or gap changes before final validation. This uses the existing chain-depth limit and leaves pixel/unbound hint handling untouched.

Fresh-source regression evidence includes every placement and routing hint's active state, directions, peer and offset. Neither A164 nor A165 may pass by deactivating or modifying a declaration. With the group-anchor correction A164 reaches its first legal frame option at candidate 2, so the old three-set combination is no longer needed for its corrected starting layout. This is not a request to drop a hint or change a limit.
## Catalogue-wide measurements with A164/A165 original hints retained

The new census passes all 673 cases: 662 completed cell-bound diagrams / 3,806 bound nodes, six unchanged open-mode cases kept unbound, and the five known native skips/empty cases. There are zero writer catches and every final active-cell constraint check is true. In addition, the A164/A165 regressions compare all original hint declarations with the final contracts, so an inactive hint cannot evade those tests.

| Scenario | Examined | First legal | Measured frame-search time | Whole scenario |
| --- | ---: | ---: | ---: | ---: |
| A164 | 6 | 2 | 288.361 ms | 663.050 ms |
| A165 | 7 | 3 | 1,032.288 ms | 1,346.391 ms |

Across 254 active frame-search invocations, 1,523 candidates were examined in 3.679 seconds total. Maximum per-diagram examination count is seven, below the unchanged sixteen-candidate limit; maximum search time is 1.561 seconds. The preceding reduced-hint run measured 1,533 candidates in 2.208 seconds. The new constraint-preserving work therefore costs about 1.47 seconds more in measured frame searching in these runs, while examining ten fewer candidates. Timings vary between runs and are not a guarantee of incremental runtime.

The complete census takes 102.714 seconds versus 98.622 seconds for the immediately preceding reduced-hint run. A142 takes 45.217 seconds versus 37.852 seconds; those whole-pipeline measurements include the pre-existing expensive final-route scoring, not just frame fitting. No speed improvement is claimed.

Only three completed scenarios change total bends relative to that reduced-hint census: A164 127 -> 125, A165 51 -> 56, A168 37 -> 38. These are cell-only effects of retaining/reconciling explicit group/member constraints rather than rejecting compatible member hints. The other 659 completed cases have identical bend totals. Production cells, independent spans, rules, weights and the parked F32/EIP261 work remain unchanged.
The final cells-on audit emits all 673 summaries in 114.61 seconds, with A142 at 48.9862 seconds. Every grid, exact-slot, hint-accounting, title-band, unrelated-frame, missing/no-legal/illegal-path and cleanup gate is zero. Quality totals are 4.2=52, 5.9=1234, 3.1=2, 4.1=0, 4.11=5, 5.8=3 and shared-track pairs=346. Quality failures remain and production readiness is not claimed. The hint-preserving change is not presented as a speed optimization.
Final verification: 306 focused tests and all 32 cell/source regressions pass. The two real-source hint regressions also require `HintsHonoured == HintsTotal` in the final model, in addition to unchanged declarations and actual logical placement constraints. Cells-off audit: all 673 summaries byte-identical in original order, 39.42 seconds, A142 12.2841 seconds, every previous zero gate still zero. The existing quality failures remain; no broad all-green claim is made. No DOCX files were committed and nothing was pushed.

Evidence files:

* [Residuals for every historical single-set candidate](a164-single-set-candidates.csv).
* [Historical reduced-hint joint candidates](a164-joint-candidates.csv).
* [Corrected original-hint candidates](a164-original-hint-candidates.csv).
* [Per-scenario frame-search costs and counts](a164-catalogue-frame-search-cost.csv).

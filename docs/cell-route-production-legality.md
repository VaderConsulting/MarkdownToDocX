# Produce legal cell routes before final recovery

The saved readiness audit contained two illegal paths in **one scenario, A112**, rather than two failing scenarios. Its summary was `illegalPaths=2`, with the A112 row alone reporting `10=2`.

| Scenario and path | Foreign node crossed | Writer that reproduced the fault |
| --- | --- | --- |
| A112: split1 -> checkPolicy | checkReceipts | DiagramGridRouteBuilder.RebuildAllRoutes, including the initial ModelRouteCompletionStage rebuild |
| A112: route -> managerOk | financeOk | The same automatic route rebuild |

These were routing/construction faults, not a frame resize after routing. The two links leave circle outlines on allocated sloping normals. A standard stub could already be outside the actual circle but still inside its wider enclosing visual rectangle. Visibility search could not use that start, and the ordinary corridor producer published a path through another node. The later `node-interior-legality` pass repaired it. A zero final gate therefore did not prove the producer was correct.

## Engine correction

The allocated-normal visibility candidate is now offered by automatic cell route reconstruction before publication and caching. Its outward stub extends to the enclosing box boundary only if actual obstacle geometry permits it. The complete candidate is checked with engine body/title geometry, normal direction and orthogonal interior runs, independently of the final audit checker.

Validated normal candidates retain their exact terminal and body coordinates both when first constructed and when retrieved from the geometry cache. Rounding just one normal stub could rotate its terminal or introduce a diagonal body segment, so this candidate is preserved as a whole polyline. Other routes keep their existing quantisation policy.

The same fallback implementation is shared with the existing final recovery path, which remains a backstop for other late writers. There is no link-name or circle-shape special case in the new producer dispatch: it uses allocated normals and actual endpoint/obstacle geometry. The previous endpoint obstacle policy is preserved. Unbound reconstruction and fallback semantics are unchanged.

Two new regressions directly rebuild both A112 routes with cold and warm caches, without invoking final model completion or recovery after the constructor. They require legal paths, perpendicular normals, unchanged allocated endpoints, exact cold/warm coordinates and unchanged cell ownership. The two old-producer tests fail with the original named node crossings; both pass with the new producer. Two additional cases within the four cover the current F32/EIP261 legality and slots.

The isolated before/after writer trace independently changes the first final-model automatic rebuild from both A112 crossings to no rule-10 findings, before `node-interior-legality` is reached.

## Parked pattern

F32/EIP261 are a separate historical experiment: the parked 4.2 direction-agreement refinement produced four illegal paths across those two scenarios. It exposed candidate/bounds/rollback coupling and remains parked. The current checker is not modified or re-enabled here. Both examples pass their current cell legality and exact-slot regressions. The A112 normal-stub/visibility fault is not that opposing-terminal 4.2 pattern.

## Late frame overlaps

Already fixed separately in `f8596f3` (Reserve frame titles before fitting and block late cell resizes). The initial fit includes the shared measured text-width and multiline band reservation. Both the cell bypass and the late writer's own guard prevent direct cell-frame resizing. The full title regression selection remains enabled, including long, nested, empty and historically affected frames. No catalogue case requires late physical growth; any future spatial enlargement must be an atomic scored region/gap assignment and frame rebuild.

## Validation

309 focused tests and 44 cell/source regressions pass. The new producer tests are included in the latter. Rules, weights, the sixteen-candidate limit, disabled production cells/independent spans and hints are unchanged. No checker or baseline was altered.

Both full audits emit all 673 summaries. Cells-on rows are byte-identical to the preceding title checkpoint; cells-off rows are byte-identical to the original ordered baseline. Exact cells-off geometry and ordered diagnostics match all 673 cases (changed=0, missing=0, 35.225 seconds).

| Gate/counter | Cells off | Cells on |
| --- | ---: | ---: |
| Illegal paths | 0 | 0 |
| Missing routes / no-legal-route failures | 0 / 0 | 0 / 0 |
| Title bands / unrelated frame overlaps | 0 / 0 | 0 / 0 |
| Grid / off-grid nodes / off-grid frame edges | 0 / 0 / 0 | 0 / 0 / 0 |
| Exact off-slot node / group ports | 0 / 0 | 0 / 0 |
| Hint accounting / cleanup corrections | 0 / 0 | 0 / 0 |
| 4.1 / 3.2/4.7 | 0 / 0 | 0 / 0 |
| 4.2 | 44 | 52 |
| 5.9 | 1,694 | 1,234 |
| 3.1 | 4 | 2 |
| 3.3/5.2 | 5 | 5 |
| 4.9 | 6 | 4 |
| 4.11 | 3 | 5 |
| 5.8 | 0 | 3 |
| Shared-track pairs | 2,089 | 346 |
| Port findings | 56 | 67 |
| Aligned / nonaligned port jogs | 15 / 1,323 | 17 / 1,254 |
| Unmeasured native skips/empty cases | 5 | 5 |
| Failed/skip audit cases | 25 | 29 |

Cells-on audit 109.11 seconds; A142 45.9994 seconds. Cells-off audit 38.44 seconds; A142 12.3808 seconds. Existing quality failures and the experimental audit slowdown remain open; nonzero audit exit codes reflect those known findings, not a nonzero legality/slot gate. Production activation is not approved or performed.

The two A112 paths share one engine fault and one implementation, so their correction is one checkpoint; the independent frame correction already has its own commit. No duplicate per-link fixes are introduced. No DOCX files are committed and nothing is pushed.

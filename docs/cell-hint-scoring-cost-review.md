# Separate hint validation from candidate routing costs

## Hint checkpoint and measurement

The original-hint correction is already isolated in `531ab82` (Preserve group anchors when applying hinted member moves). No duplicate commit or history rewrite was needed. A165 and A164's separate frame-search checkpoints remain `6d55987` and `210d917`.

Added optional nested cost scopes to the existing stage profiler. Each scope reports inclusive and exclusive time; child categories are subtracted exactly once from their parent. With profiling off, no scope or stopwatch is allocated. This replaces the misleading inference that the large `completion/final-diagnostics` marker is all diagnostic work: it includes the final candidate search.

The full before profile emits all 673 summaries in **127.27 s**, A142 **49.6515 s**. Every legality, grid, slot and hint-accounting gate is zero. The earlier unprofiled retained-code verification was 118.50 s / A142 50.1762 s. Profiled and unprofiled figures must not be treated as directly interchangeable.

## Exclusive cost comparison

The three largest measured categories are **routing completion 94.6291 s**, **frame search 3.8677 s**, and **final-layout scoring outside routing 2.0412 s**. The existing earlier profile identifies 3,921 private completed candidates costing 79.60 s. Scoring's inclusive 91.06 s includes that routing work; it must not be added to the exclusive routing column.

| Instrumented category | Before seconds | Rejected final trial seconds | A142 before | A142 trial |
| --- | ---: | ---: | ---: | ---: |
| Routing completion / allocated-port rebuilding | 94.6291 | 26.7401 | 43.0300 | 4.5760 |
| Frame search excluding hint checks | 3.8677 | 3.6147 | 2.1545 | 2.0391 |
| Final-layout scoring excluding routing/hints | 2.0412 | 1.7022 | 0.4289 | 0.1621 |
| Hint validation | 0.6980 | 0.0997 | 0.0017 | 0.0001 |
| Final diagnostics | 0.2574 | 0.0329 | 0.2029 | 0.0195 |

These categories cover the instrumented frame search, logical hint validation and final-model candidate path. Earlier route construction/repair, native placement, other packing, audit checks, output and host startup remain outside this table. Consequently their sum is not the full audit wall time. One-time hint-cache construction in the trial is also outside its validation-call timer. All raw timing and inclusive/exclusive CSV evidence is retained under ignored `out/logical-grid-prerequisite/hint-scoped-retry` and the preceding profile capture.

## Repeated hint checks

Yes: `FlowchartCellRepair.PlanAssignment` validates the proposed regions, and `Assignment.TryApply` validates them again. The full before run calls `CellHintsMatch` **49,170 times**, rebuilding node/group lookups and scanning active hints on each call. Nearly all of the measured hint cost occurs inside frame search. A142's total is only 1.7 ms; hint checking is not its speed fault.

The trial compiled membership once and cached each relation by its complete logical inputs: owner and peer regions, nested member envelopes, push siblings, kind, direction, peer id and offset. Active flags were still checked. A change to any dependency reran the original `CellHintMatches`; unchanged inputs reused its exact previous result. It did not assume a candidate was valid or erase a failed result on rollback. Reference tests covered owner/peer moves, unaffected nodes, pushes, changed offsets/peers, active flags, rejected states and restoration. Both tests passed. The validator was still invoked 49,170 times, but repeated internal work was avoided. Validation time fell from 0.6980 to 0.0997 s. This cannot by itself close a roughly eighty-second routing gap.

## Incremental scoring trial and rejection

The revised trial retained the engine's existing terminal candidate, facing-side, straightening and distinct-track families, scoped to affected routes. It cached unchanged bend, route-fault and crossing-pair costs and fully completed the proposed winner for confirmation. It also tested segment-level node/title-band dependencies and a final shared-slot seal. No score weights, checks or named limits were dropped or relaxed.

The first revised unprofiled audit took **43.08 s**, A142 **7.38143 s**, but five original-81 scenarios worsened and F58 had a slot failure. The subsequent correction pass took **54.08 s with profiling enabled**, A142 **10.6593 s**. It still had F58's off-slot port/group and seven worsened bend counts. The latter wall time is a profiled measurement, not an unprofiled claim against the 45 s target.

| Scenario | Retained bends | Final trial bends | Increase |
| --- | ---: | ---: | ---: |
| F59 | 6 | 10 | 4 |
| F61 | 4 | 6 | 2 |
| A18 | 20 | 21 | 1 |
| A107 | 14 | 15 | 1 |
| A141 | 12 | 13 | 1 |
| EIP215 | 12 | 13 | 1 |
| EIP246 | 12 | 13 | 1 |

For the fixed 81-case set: **74 unchanged, seven worse, none improved**; total bends **1332 -> 1343**. The retained result against actual cells-off output remains 31/81 no worse (13 fewer, 18 equal), with 50 still worse. The rejected trial does not replace it. Every case is recorded in `cell-hint-scoring-trial-bends.csv`.

The scoped dependency set and provisional ranking still failed to reproduce all successful full-completion choices. Whole-route bounding boxes include unrelated corridors; segment-only dependencies can miss changes in a neighbour's alternative routes and track costs. Reusing existing refinement families improved the earlier prototype but did not establish a correct minimal dependency closure. Full winner confirmation safely rejects unsuitable choices, but rejecting a provisional winner does not automatically recover the better option the old complete search found.

F58 is a concrete allocation bug, not permission to change its checker: `Persist` is a classic cylinder with no geometry provider. Legacy `ProjectToOutline` casts from the node centre through a point four pixels beyond the nominal slot. Requested Y **680** becomes **680.9467455621302**, then rounds to **681**, moving the along-side fraction from 1/3 to 0.338. Calling the same allocator at the end reproduces the drift. A pinned-candidate regression reproduces it without any accepted placement move. The slot assertion was not loosened and no shape-specific router exception was introduced. Correcting this legacy allocation path and proving a complete route-dependency closure remain open.

**Stopped and reverted the complete functional trial, including hint-result caching and its experimental tests.** Only the general cost instrumentation and this report are retained. No incremental scoring change is committed. The prototype sources, reference tests, failing pin fixture and ray-projection proof remain in ignored diagnostic output. Final verification of the restored correct engine follows below.

Rules.yaml, weights, sixteen-candidate limit, original hint behaviour, production cells OFF and independent spans OFF are unchanged. F32/EIP261 remains parked. No DOCX files included; nothing pushed.

## Restored verification and outcome

After removing all functional trial code, the restored unprofiled cells-on audit takes **117.80 s**, A142 **50.2319 s**, and emits 673 summaries ordinally identical to the correct retained checkpoint. Cells-off takes **40.43 s**, A142 **11.7259 s**, with all 673 summaries byte-identical to the original baseline. There are zero logical-hint mismatch reports. The 311 focused tests and 60 cell/source regressions pass after restoration.

| Gate / counter | Restored cells off | Restored cells on | Final rejected trial |
| --- | ---: | ---: | ---: |
| Summaries | 673 | 673 | 673 |
| Illegal paths | 0 | 0 | 0 |
| Grid / off-grid nodes / frame edges | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 |
| Off-slot ports / groups | 0 / 0 | 0 / 0 | 1 / 1 |
| Hint accounting / cleanup | 0 / 0 | 0 / 0 | 0 / 0 |
| Title bands / unrelated frame overlaps | 0 / 0 | 0 / 0 | 0 / 0 |
| Missing / no legal routes | 0 / 0 | 0 / 0 | 0 / 0 |
| 4.2 | 44 | 52 | 52 |
| 5.9 | 1694 | 1234 | 1240 |
| 3.1 / 4.1 | 4 / 0 | 2 / 0 | 2 / 0 |
| 3.2/4.7 | 0 | 0 | 1 |
| 3.3/5.2 | 5 | 5 | 5 |
| 4.9 / 4.11 / 5.8 | 6 / 3 / 0 | 4 / 5 / 3 | 4 / 5 / 3 |
| Shared-track pairs | 2089 | 346 | 347 |

Known quality/shared-track findings and five native skips/empty cases still make the audit exit nonzero; no restored legality, grid, slot or hint-accounting gate is nonzero. The speed target is not achieved by any accepted implementation. The hints checkpoint remains 531ab82; the new checkpoint retains instrumentation and evidence only, not an incremental scoring or hint-cache change.

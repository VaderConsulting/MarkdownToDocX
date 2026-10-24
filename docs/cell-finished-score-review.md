# Finished cell scoring and exact rejection rollback — 18 October 2026

## Identify the original failures

The saved first final-model catalogue logs name A58 and A138 in the two-case stack replay (`readiness-final-model-stack.log`). The full earlier census also names EIP84. All three had the same exception, not two distinct cell-assignment bugs: `FlowchartPortSideRouteRepair.Length` called `Enumerable.Range(1, Points.Count - 1)` for an empty unresolved trial route. Checkpoint `2db6901` changed the count to `Math.Max(0, Points.Count - 1)`, with zero-, one- and two-point tests and real-source completion regressions.

That checkpoint already moves cell quality evaluation after `AssignPortSides` and `ModelRouteCompletionStage.Apply`. The recursion guard prevents trial completion from recursively placing more nodes. The unchanged full score and repair penalties see final lines, not provisional corridors. No second implementation of finished-layout scoring was added here.

Completion tests alone were insufficient evidence of exact rejected-candidate rollback. `CellCompletedCandidateRollbackTests` now uses each original source (A58, A138 and EIP84), prepares a complete whole-cell translation, runs the actual bounded assignment search and full route completion, forces rejection at the candidate observer, and compares regions, both gap maps, allocated sides/normals/endpoints, every route point, node/frame bounds, mutable frame edges, painted guides, cached guides and diagnostics exactly. Search counters intentionally remain spent. The original hints are unchanged.

## Additional snapshot gaps found and fixed

* `12c33c2`: the saved painted guide list held mutable guide objects by reference. Saving the list alone could not restore in-place point or kind changes. Capture points/kinds by value and restore the original guide objects and family together with the rest of the option. The new forced-mutation/rejection test fails before this fix and passes after it. This is a snapshot-contract regression fixture; it is not evidence of a new catalogue geometry fault.
* `60b5fb1`: real trial completion overwrote `FlowchartRenderContext.CachedRouteGeometry` and `CachedRouteGuides`; rejection restored the public model but retained that trial cache. Save and restore the cache contract, exact key, original family and mutable guide values too. The new forced-rejection test observes the real completion overwrite, fails on the stale key/family before the fix and passes afterward.

Both retain authoritative regions/gaps first, then exact cached frames, ports and routes. Persistent rejected-write diagnostics and the named search budget are intentionally not erased by rollback. The existing sixteen-candidate limit, rules and weights are unchanged.

## Bend comparison methodology

The original 81-case set is frozen in the explicit `CellFinishedScoreBendReviewTests` selection. Every source is rerun in both modes with original hints; counts use `FlowchartLayoutScore.CountCorners` on the completed model routes. Per-link differences and side changes are logged. The older `Before` column was the placement-policy review's previous **cells-on** capture, not cells-off output. It must not be labelled a cells-off comparison. The new comparison measures actual cells-off output directly.

The historical first-change groups explain where extra cell bends entered during removal of unconditional centre positioning. They do not establish that remaining increases are necessary for legality. The bounded final-model search may exhaust its existing candidate budget or keep the original option because a complete legal candidate does not beat the unchanged combined score. No hint or rule is dropped to force a bend-only improvement.

## Current original-81 result

Against actual cells-off output: **31/81 are no worse (13 fewer, 18 equal); 50/81 still have more bends**. Aggregate bends for this fixed set are 1,143 off and 1,332 on. These are current measurements, not a claim that today's cache restoration alone improved 31 cases. The older original-81 report compared a previous cells-on capture and is not interchangeable with this baseline.

Every remaining case spent all sixteen candidate moves. Forty-five accepted one score-improving whole-cell move but still exceed their cells-off bend total; five accepted none (F136, A22, A110, A117, A132). The global budget was not replenished for another search entry point. Reaching the limit is evidence about the bounded search, not proof that a lower-bend legal arrangement cannot exist.

Historical attribution among the remaining cases: 24 shared-fan centring removals, 12 main-chain column removals, 10 fan-in median removals, and one each for chord swaps, junction branch nominations, nested groups and partner ranks. The measured per-link file records the actual final bend and side differences. These remain placement/candidate-quality work, not unavoidability claims. No rule relaxation, hint deactivation or new search limit is proposed in this checkpoint.

Every reviewed scenario is in `cell-finished-score-81-bends.csv`; every changed link is in `cell-finished-score-link-differences.csv`. The remaining cases are listed below. The cause column identifies the historical producer change; each has the same current limiting condition: a full sixteen-candidate search did not recover the cells-off bend count.

| Scenario | Cells off bends | Cells on bends | Historical producer change |
| --- | ---: | ---: | --- |
| F30 | 2 | 3 | Shared fan source/tail centring removed |
| F61 | 0 | 4 | Forced main-chain columns removed |
| F120 | 2 | 3 | Shared fan source/tail centring removed |
| F136 | 8 | 14 | Partner cross-rank alignment removed |
| A16 | 0 | 2 | Forced main-chain columns removed |
| A21 | 2 | 4 | Shared fan source/tail centring removed |
| A22 | 2 | 7 | Fan-in merge median column removed |
| A29 | 5 | 10 | Forced main-chain columns removed |
| A30 | 0 | 2 | Forced main-chain columns removed |
| A37 | 2 | 6 | Shared fan source/tail centring removed |
| A40 | 2 | 4 | Shared fan source/tail centring removed |
| A41 | 0 | 2 | Fan-in merge median column removed |
| A42 | 0 | 4 | Fan-in merge median column removed |
| A46 | 15 | 16 | Fan-in merge median column removed |
| A47 | 6 | 8 | Junction branch centring replaced by scored nominations |
| A72 | 8 | 9 | Fan-in merge median column removed |
| A82 | 0 | 2 | Fan-in merge median column removed |
| A98 | 2 | 4 | Forced main-chain columns removed |
| A103 | 0 | 8 | Shared fan source/tail centring removed |
| A110 | 150 | 180 | Forced main-chain columns removed |
| A112 | 31 | 37 | Forced main-chain columns removed |
| A114 | 108 | 143 | Nested group centring removed |
| A117 | 99 | 138 | Centre-driven chord swap removed |
| A132 | 2 | 7 | Fan-in merge median column removed |
| A134 | 2 | 4 | Fan-in merge median column removed |
| A136 | 4 | 5 | Forced main-chain columns removed |
| A141 | 11 | 12 | Forced main-chain columns removed |
| A161 | 24 | 26 | Shared fan source/tail centring removed |
| A172 | 0 | 2 | Shared fan source/tail centring removed |
| A173 | 0 | 6 | Fan-in merge median column removed |
| A174 | 0 | 2 | Forced main-chain columns removed |
| EIP36 | 4 | 5 | Shared fan source/tail centring removed |
| EIP46 | 4 | 8 | Shared fan source/tail centring removed |
| EIP63 | 4 | 8 | Shared fan source/tail centring removed |
| EIP83 | 1 | 2 | Shared fan source/tail centring removed |
| EIP99 | 0 | 2 | Shared fan source/tail centring removed |
| EIP131 | 0 | 2 | Shared fan source/tail centring removed |
| EIP162 | 4 | 8 | Shared fan source/tail centring removed |
| EIP211 | 0 | 2 | Forced main-chain columns removed |
| EIP215 | 7 | 12 | Shared fan source/tail centring removed |
| EIP225 | 0 | 8 | Shared fan source/tail centring removed |
| EIP231 | 0 | 2 | Shared fan source/tail centring removed |
| EIP240 | 0 | 8 | Shared fan source/tail centring removed |
| EIP241 | 0 | 2 | Forced main-chain columns removed |
| EIP245 | 0 | 8 | Shared fan source/tail centring removed |
| EIP246 | 7 | 12 | Shared fan source/tail centring removed |
| EIP261 | 2 | 4 | Fan-in merge median column removed |
| EIP280 | 4 | 8 | Shared fan source/tail centring removed |
| EIP315 | 0 | 8 | Shared fan source/tail centring removed |
| EIP342 | 0 | 2 | Shared fan source/tail centring removed |

The candidate audit adds useful distinctions to those causes: F136, A110 and A117 had no scored candidate passing the existing preservation filters; A22 and A132 each had five passing candidates but no total-score improvement. Among the other 45, an accepted legal improvement still left an excess relative to cells-off output. `CandidatesPassingPreservation` in the CSV reflects the complete compound guard (new findings, hints, overlap, length/bend budgets and changed-line count), not just path legality. It must not be interpreted as the count of geometrically illegal paths.

## Gates and timing

All three audits emit 673 summaries. The profiled and unprofiled cells-on rows match each other and the preceding checkpoint ordinally. Cells-off rows match the original saved baseline ordinally. The full gate/counter comparison is unchanged:

| Gate / counter | Cells off | Cells on |
| --- | ---: | ---: |
| Illegal paths | 0 | 0 |
| Missing routes / no legal route | 0 / 0 | 0 / 0 |
| Title bands / unrelated frame overlaps | 0 / 0 | 0 / 0 |
| Grid / off-grid nodes / frame edges | 0 / 0 / 0 | 0 / 0 / 0 |
| Off-slot node / group ports | 0 / 0 | 0 / 0 |
| Hint accounting / cleanup | 0 / 0 | 0 / 0 |
| 4.1 / 3.2/4.7 | 0 / 0 | 0 / 0 |
| 4.2 | 44 | 52 |
| 5.9 | 1694 | 1234 |
| 3.1 | 4 | 2 |
| 3.3/5.2 | 5 | 5 |
| 4.9 | 6 | 4 |
| 4.11 | 3 | 5 |
| 5.8 | 0 | 3 |
| Shared-track pairs / 5.1 | 2089 | 346 |
| Port findings | 56 | 67 |
| Aligned / nonaligned port jogs | 15 / 1323 | 17 / 1254 |
| Unmeasured native skips / empty | 5 | 5 |
| Failed / skip audit cases | 25 | 29 |

Unprofiled cells-on audit **115.54 s**, A142 **48.4578 s**. Unprofiled cells-off audit **37.75 s**, A142 **11.3095 s**. The profiling run takes **127.34 s**, A142 **52.9946 s**. Profiling changes timing/output overhead; the unprofiled run is the comparison figure. The previous checkpoint's cells-on audit was 103.39 s / A142 38.4686 s. These measurements do not establish that the difference is caused by the rollback changes: their measured capture/restore cost is negligible below. No speed improvement is claimed.

The profiled case layout clocks sum to 119.636 s; the wrapper also includes host startup, output and audit processing. Reconstructing completion nesting from its six ordered markers identifies 668 root completions and **3,921 private completed candidates**. Those candidate completions total **79.60 s**; A142 alone runs **nine** full candidates totalling **37.50 s**. Sixteen offers were spent there; earlier frame/eligibility rejections account for offers that do not reach full route completion.

Largest repeated completion-call timers, inclusive within those completions:

| Repeated stage | Catalogue seconds | A142 seconds |
| --- | ---: | ---: |
| Terminal-port candidates | 25.6043 | 8.5534 |
| Facing-side assignment | 21.8228 | 19.6819 |
| Route rebuild | 7.8954 | 1.3736 |
| Nonincreasing track search | 6.7046 | 2.0576 |
| Free-side comparison | 5.4764 | 2.6839 |
| Node-interior legality | 2.5020 | 1.9030 |
| Snapshot capture | 0.0181 | below 0.0001 |
| Snapshot restore | 0.0255 | 0.0003 |

These are nested timers and must not be added to parent completion totals. In particular, `completion/final-diagnostics` totals 95.7557 s, but its outer marker includes the entire final candidate search. The nested candidate completions already account for 79.60 s of that marker. The remainder includes candidate preparation/checking/scoring as well as genuine diagnostics; it is not all final diagnostic work. Leaf candidate diagnostic markers themselves total about 0.26 s. A142's final-diagnostics marker similarly includes its 37.50 s of nested completion.

The dominant cost is repeatedly running the complete route search for different placements, particularly side and terminal repair. The existing candidate/chain/line limits were not raised or replenished. A future optimization should reuse prepared obstacles and exact completed candidates where their full inputs match, retaining the full scorer as reference; it must not accept provisional bends or skip legality checks. **Stopped at profiling/reporting because the 45 s threshold is exceeded. No subsequent layout optimization or production activation is performed.**

Validation: 311 focused layout tests and 60 cell/source regressions pass. All 81 explicit two-mode measurement cases complete. The two extra snapshot fixes are separately committed; the original-source rejection tests are `48f6d6a`. Finished-layout scoring and the original empty-path fix remain `2db6901`. The sixteen-candidate limit, rules.yaml, weights, hints, production cells OFF and independent spans OFF are unchanged. Known quality findings and F32/EIP261's parked refinement remain open. No DOCX files are committed; nothing is pushed.

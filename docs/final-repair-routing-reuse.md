# Reuse final repair reference routing

19 October 2026. Reference: `1167023` (engine `01dfee1`, rule `942b4a4`).

## Cap verdict and watchdog

Neither the 65 px nor the 80 px measurement changed any layout or accepted another candidate. Keep `repair_extra_length_px` at 61 px. `rules.yaml` is unchanged and there is no cap commit. The engine reads the embedded rule value. The normal audit watchdog remains 59 seconds; long profiling runs use an explicit invocation-only watchdog override, not an edited default.

## Reference-routing diagnosis

The earlier profile attributed 304.763 seconds exclusively to final-pass routing/completion. The new instrumented reference run took 366.14 seconds overall (A142 layout: 224.528679 seconds), with identical output to `1167023`. Its direct inclusive call timers measured 288.785472 seconds: 31.431755 seconds for 237 track/port candidates on unchanged placement, and 257.353718 seconds for 2,374 region/gap candidates. These are measurements from a new run, not a retrospective exact split of the earlier 304.763-second sample. The inclusive direct-call timers and the exclusive nested cost category have different accounting boundaries.

There were 2,611 full reference calls across 165 scenarios: 160 scenarios made 16 calls, one made 12, three made 10, and one made 9. The other 508 scenarios made none. There were 260 repeated placement hashes, of which 200 were repeated base-placement calls (237 calls for 37 per-scenario base placements). Only 46 calls repeated the complete incoming route input. Track/port candidates deliberately have different incoming route assignments despite sharing placement. The complete per-scenario counts, including zeros, are in `final-repair-confirmation-counts.csv`.

| Scenario | Final pass seconds | Reference calls | Base calls | Repeated placement inputs | Repeated complete inputs |
|---|---:|---:|---:|---:|---:|
| A142 | 211.325856 | 16 | 6 | 5 | 0 |
| A114 | 15.337661 | 16 | 8 | 7 | 0 |
| F153 | 8.577138 | 16 | 5 | 4 | 0 |
| A163 | 7.765975 | 16 | 6 | 5 | 0 |
| F152 | 6.711940 | 16 | 0 | 6 | 6 |
| F151 | 6.637436 | 16 | 0 | 1 | 1 |
| A167 | 5.352947 | 16 | 7 | 6 | 0 |
| A115 | 4.451986 | 16 | 7 | 7 | 0 |
| A111 | 2.231632 | 16 | 8 | 7 | 0 |
| A164 | 2.134065 | 16 | 16 | 15 | 0 |

A142's `move|dmeDbms|0,132` consumed 167.958 seconds of reference routing before rejection for an unrouted line. Its other reference calls took about 1.9–3.7 seconds each. Its six base-placement calls share one placement hash but none share the full incoming route hash. Thus simple identical-input memoization cannot remove the dominant call.

## Incremental checks

### 3a — reuse base routing

Cache reference completion for track/port options by their exact placement, cell ownership, gaps, frame bounds and hint-status hash. A cache hit restores the complete route, port, frame, region, guide, hint and diagnostic snapshot before replaying the option's track/port assignment. Candidate enumeration, admission and tie-breaking are unchanged. The cache is local to one final pass.

The full profiled audit took **355.77 s**, A142 **237.904 s**. All 673 summary rows are byte-identical to the reference; all 668 measurable geometry/findings hashes match; all per-scenario metrics match; the same 77 accepted option keys occur in the same order in the same 58 scenarios. All hard gates remain zero. There were 200 cache hits, reducing actual full reference calls from 2,611 to 2,411. Base-call time including cache restoration fell from 31.431755 s to 5.402920 s. Distinct changed-geometry work varied from 257.353718 s to 276.588187 s, masking some of that saving in the wall time. No speed claim relies on the noisy A142 wall-time comparison.

Normal suite: 1,148 passes and exactly the same 205 failing test identities (no new or removed identities). All five focused final-repair tests pass, including exact cached-versus-reference model and acceptance-order comparisons for A115, F60 and A104, plus frame-overlap and rejected-candidate rollback regressions.

All 673 summaries are checked. The five native-ineligible sources have no route-geometry hash; their explicit statuses remain checked. There are 668 measurable geometry hashes, not 673 valid native geometries.
### 3b — reuse complete candidate confirmations

The candidate cache uses exact ordered regions/spans, gaps, node/frame projections, hint statuses and saved hinted paths, allocated sides/normals/points, guide contents, cached-guide geometry/contents and current diagnostics. It is scoped to the current scenario's final pass, like the base cache. A hit restores the complete reference snapshot; all guards, scoring and replay still execute in the original candidate order. Different guide or hint-restoration state cannot share a confirmation merely because the public route inputs match.

The first full check (before adding saved hinted paths to the signature) took 291.32 s, A142 182.134993 s, with 45 additional candidate-cache hits, 245 total hits and 2,366 actual full reference calls. All signatures, summaries, metrics and accepted sequences matched. The normal suite retained the exact same 205 failures; all six focused tests passed, including F152 exercising repeated full candidate inputs. This timing improvement is not all attributable to caching: A142 has no additional cache hits in 3b and its expensive distinct candidate has substantial run-to-run timing variation.

The retained, stricter-key full audit took **284.67 s**, A142 **178.919914 s**. All 673 summaries, all 668 available geometry/findings hashes, every scenario metric and the 77 accepted keys in the same order across 58 scenarios match `1167023`. All hard gates are zero. There are 199 base hits and 45 full candidate hits, leaving 2,367 real reference completions. One base hash that previously matched is deliberately separated by its different saved hint-restoration state. This is conservative reuse, with no prediction-based rejection.

The strict-signature normal run has 1,148 passes and exactly the same 205 failure identities (zero new or removed identities). All six focused final-repair tests pass. No audit checker, rule value, search limit, acceptance order or guard changed. Step 3a is checkpoint `073e408`.

### 3c — parallel confirmation eligibility

No parallel worker is retained. Each `FinalOption.Build` closes over the same live `GridLayoutState`, route objects, mutable cell owner, frames and incumbent snapshot. `CompleteReferenceCellCandidate` rebuilds every route and temporarily writes the static `RouteRepairCandidateObserver` and `RouteRepairOptionObserver`. Even candidates moving different nodes therefore do not have independent write sets under the current reference API. The lazy option factories also consume the current incumbent after each accepted result. A fixed-order merge alone would not prevent cross-worker mutation or stale proposals.

Safe parallel workers would require a separate deep-owned state/contract/hint/cache boundary and per-worker callback suppression, with invalidation of speculative results after an earlier acceptance. This increment does not introduce that architectural change or run shared mutable candidates concurrently. There is no separate 3c timing claim: its executable state is unchanged from 3b.
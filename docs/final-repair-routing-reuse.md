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
### 3d — reject fixed frame faults before confirmation

The preflight uses the existing unrelated-frame-overlap check only. It requires all placed nodes to be cell-owned, every declared group envelope to be present, all envelopes finite and positive, a unique complete frame inventory, and exact equality between public and internal frame boundaries. Under reference candidate mode all cell placement/frame-repair searches return without moving geometry; cell-bound title repair cannot enlarge an envelope; positive existing group bounds bypass fallback construction. Therefore routing cannot remove the pre-existing frame overlap. Missing, incomplete or stale envelopes still receive ordinary full confirmation. The original post-completion frame check remains in place. The candidate still consumes its original budget slot, and exact rollback still runs.

Seven focused tests pass. The F60 proof regression runs full uncached confirmation with the preflight disabled and checks unchanged node/frame bounds plus the surviving overlap for both reproducing moves. The enabled test checks those options never enter confirmation. Full cached-versus-reference models and acceptance sequences, plus rejected-candidate rollback, remain checked.

The full profiled audit took **135.93 s**, A142 **40.937247 s**. All 673 summaries are byte-identical, all 668 available geometry/findings hashes match, all per-scenario metrics match and the same 77 accepted keys occur in the same order across 58 scenarios. All hard gates are zero. The preflight skips 26 formed candidates across eight scenarios: F60 (3), F111 (6), F139 (1), A111 (5), A114 (5), A115 (2), A142 (1), EIP31 (3). The cache still reuses 244 completions; 2,341 actual reference completions remain. No candidate was skipped for an estimated score, length, bend or crossing count.

A142's dominant `move|dmeDbms|0,132` is now rejected for its fixed frame overlap before reference routing. A142 still has 10 actual confirmations and 5 cache restorations; its final pass takes 29.255423 s. Distinct reference calls remain necessary under the current guards. Catalogue reference-completion/restoration time is 67.851813 s (base 5.047893 s, changed geometry 62.803920 s), and the entire final pass is 71.480703 s. The remaining audit time is outside that pass.

The normal suite has 1,148 passes and exactly the same 205 failure identities, checked case-sensitively (zero new or removed identities). The final unprofiled audit completed under the unchanged **59-second default watchdog**, with no timeout override: **132.82 s**, A142 **43.827792 s**. It emitted all 673 summaries, with no timeout or incomplete batch. All untrimmed summary rows, all 668 available geometry/findings hashes, all metrics and all accepted sequences match the reference. The audit's same 23 pre-existing scenario assertions remain failing; no hard gate is nonzero.

Final measured totals: **2,875 bends, 2,039 crossings, 320 shared-track pairs, 1,191 rule 5.9 findings and 1,582 total findings**. A104 remains 38/0, A105 28/0 and A115 96/131 bends/crossings. Both large fans retain zero same-fan crossings. The 61 px embedded cap, all rules/weights/limits and every checker are unchanged. Steps 3a and 3b are checkpoints `073e408` and `a3a5d8c` respectively. The earlier speed targets remain unmet; the remaining final pass confirms distinct candidates serially.

## Reproduce and inspect

All measured catalogue runs use Release production-cell mode and the existing audit. For an unprofiled capture with the normal watchdog:

```powershell
$env:ROUTE_AUDIT_CELLS_ON = 'production'
$env:ROUTE_AUDIT_GEOMETRY = '1'
$env:FINAL_REPAIR_CAPTURE = Join-Path (Get-Location) 'out/final-repair-capture.jsonl'
[IO.File]::WriteAllText($env:FINAL_REPAIR_CAPTURE, '')
# Leave ROUTE_AUDIT_PROFILE, FINAL_REPAIR_PROFILE, FINAL_REPAIR_CAP
# and FINAL_REPAIR_CONFIRMATIONS unset.
pwsh -NoProfile -File tests/MermaidRecognition.RuleValidation.Tests/Run-RouteRuleCatalogueAudit.ps1
```

For the detailed diagnostic trace, set `FINAL_REPAIR_PROFILE=1` and `FINAL_REPAIR_CONFIRMATIONS=1`. The slow pre-optimisation profiles additionally used an invocation-only `-BatchTimeoutSeconds ([int]::MaxValue)`; the tracked default was never edited. Observer instrumentation is off in ordinary production calls.

Local evidence is retained under `out/markup-review/`: `final-reuse-profile-before`, `final-reuse-base`, `final-reuse-candidates-strict`, `final-reuse-preflight` and `final-reuse-default` each have JSONL, stdout, timing CSV and audit-log captures. The first four contain the detailed per-candidate completion trace. `final-reuse-{base,candidates-strict,preflight}-normal.trx` records each normal-suite comparison. `final-reuse-preflight-focused.trx` records the seven focused regression passes. No DOCX was regenerated and nothing was pushed.
# Keep nested fan tracks within authoritative cell layouts

19 October 2026. Cells-off compatibility remains available. No rule, parameter, score weight or limit changed from f207af1. Independent text spans remain off. No DOCX was regenerated.

## Cause and fix

The previous narrowed checker tested whether a whole distinct-leaf fan was nested, but applied that logical-grid overflow exception to unbound models as well. That changed the side findings used by legacy placement/repair before a finished cell-owned fan existed. The result was one painted A52 crossing and shifted three-child LR/RL terminal fans, even though the isolated-star nesting pass itself never moves nodes.

The exception now requires actual authoritative cell ownership, not a configuration flag. It still requires every declared fan branch, distinct leaves, beyond-edge top/bottom terminals and zero same-fan crossings/shared segments. Far-side terminals, corners, wrong normals, obstacle hits, port order and slot checks retain their existing enforcement. Unbound preparation, open mode and the retained cells-off path use their existing side policy. No arbitrary minimum size or new scenario identifier was added.

Restored nested header/exterior-track candidates and leaf-run label placement. Each complete option is checked and scored after all routes exist. It must lower the complete score, add no rule/subject finding, worsen no bends/crossings/shared tracks, and retain zero same-fan crossings. Rejected proposals restore terminal fields and independent point copies exactly. Painted guides are rebuilt after this stage. Nodes, regions, frames, gaps and hints never move in this increment. A* and targeted placement moves remain out.

## Geometry

| Scope | Before bends | After bends | Before geometric crossings | After geometric crossings |
| --- | ---: | ---: | ---: | ---: |
| A104 | 54 | 38 | 46 | 0 |
| A105 | 48 | 28 | 34 | 0 |
| A52 | 6 | 6 | 0 | 0 |
| Original 81 | 1118 | 1118 | 1100 | 1100 |
| Whole catalogue | 3043 | 2999 | 2175 | 2095 |

All 673 comparison cases passed against the checkpoint, including individual bend/crossing nonincrease. A104 and A105 have zero same-fan crossings; the actual enterprise painter test also checks that no label rectangle lies on another fan line. F143 improves 17 to 10 bends and F153 106 to 105; their crossing counts remain 0 and 87.

## Verification

The unprofiled Release audit emitted every one of the 673 scenario summaries in **43.86 s**. A142 **6.645654 s**, A104 **0.198020 s**, A105 **0.196342 s**. Five pre-existing native-placement cases remain explicitly unmeasured, not zero-route successes.

Hard gates are zero: illegal paths, missing routes, no-legal-route failures, title-band hits, frame overlaps, grid findings, off-grid nodes/frame edges, off-slot ports/groups, hint-accounting failures and cleanup corrections. Per-scenario rule-count comparison with the checkpoint has zero finding increases. Quality totals: 4.2 52; 5.9 1190; 3.1 2; 4.1 0; 4.11 5; 5.8 3; shared-track pairs 337. Existing quality/native failures still make the audit command return nonzero; its exit status is not labelled a full test-suite pass.

The complete layout suite: 1,112 passes and the **same 216 failing identities**, no added or removed failure identities, out of 1,328 tests. A52 painted crossing and LR/RL three-child placement pass unchanged; the two known TB/BT cases remain in the existing set. Eighteen focused fan/slot/label/authority/rollback tests pass, including the A58/A138/EIP84 rejected-candidate checks. Existing tests and expected values were not weakened.

Evidence: `out/markup-review/fan-authority-after.jsonl`, `fan-authority-comparison.log`, `fan-authority-audit.log`, `fan-authority-audit.stdout.log`, `fan-authority-audit.timings.csv`, `fan-authority-finding-increases.txt`, `fan-authority-layout-suite.log`, `fan-authority-layout-failures.txt`, `fan-authority-three-regressions.log` and `fan-authority-rollback-focused.log`.

## Cells-off report
All 673 scenario summary payloads match the final cells-off archive at f207af1: **zero changed scenarios**. The retained path was not retired. The relaxed acceptance permission was not needed for this increment. The cells-off audit also retains zero hard gates.


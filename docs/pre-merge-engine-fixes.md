# Pre-merge engine fixes

## A142 speed: retained change

Starting point: clean 6c07d39, with the original A115 work left in its original worktree. Rules, limits, checkers and existing test expectations are unchanged.

### Source and profile

The current >180-second regression enters at 4df45a1. ce6f1ce takes 36.3458 s; its src tree is identical to the immediate pre-4df45a1 checkpoint 85d8330. At 4df45a1 A142 times out at the unchanged 180-second stress watchdog (180.0832 s invocation). The earlier master production capture at eb09c58 took 4.5350 s. Earlier final-pass work had also caused a separate historical slowdown, subsequently reduced before ce6f1ce; this diagnoses the current band/port-pass regression rather than claiming no earlier slow commit existed.

One outside-gate 6c07d39 profile completes in 520.2803 s. Rule 4.12 accounts for 390.89 s across 13 calls; final repair takes 43.53 s and model completion 31.21 s. A 30-second sampled-thread trace attributes substantial work to repeated membership scans, candidate collision checks and allocations. Multiple widths produce identical starting geometry yet run all those stages again.

The retained engine change:

- Plans and scores every original wrap width in the original order, reusing a finished private trial only for an identical ordered input signature, including regions, gaps, frames, routes and hint state. Winning layouts still undergo the existing caller-context confirmation.
- Leaves non-finite/incomplete Dagre seeds on the original uncached path until later frame fitting resolves them. The first prototype exposed this requirement in A167; that prototype was not committed.
- Memoizes pure port-route construction within one immutable rule 4.12 pass. All candidate, legality, interaction, scoring and acceptance checks still execute.
- Reuses exact group membership only inside the port-only pass, with contract isolation and nested-scope restoration. No declaration or membership is changed.

Local route reuse alone takes 373.2171 s. Combined trial/route reuse takes 124.9964 s in the diagnostic run, with exactly the same final geometry hash as the 520.2803 s reference: ED71474A142ACDD7BCC4E1F8A9AC22614EFBE86D051C96FBDCA9757E9F69B088. The final membership optimization brings A142's production audit layout time to **91.6501 s**.

### Final audit and protected tests

The full production audit emits all **673 unique summaries** under the unchanged 59-second normal and 180-second stress watchdogs. Result: **649 passed, 24 failed, zero timeouts**, wall **181.6311 s** with capture/geometry evidence enabled. Against clean 6c07d39, all 667 comparable successful-layout geometry signatures match and all 672 previously completed case metrics match. The five unchanged layout failures remain. A142 now completes and reports its pre-existing shared-track failure instead of timing out.

A142: 180 routes, 314 bends, 176 physical crossings, 23 shared-track pairs, 120 rule 5.9 findings. The rule 2.13 scorer counts 125 chargeable crossings; this differs from the physical intersection inventory. Bounds 37530 x 5070, empty share 0.4688225751, optimization score 10197.0955538. Slot, off-slot, grid, node/title-band and frame-overlap gates are zero on every successfully measured case. The baseline diagnostic independently confirms the same A142 geometry and shared pairs before this fix.

The clean 6c07d39 audit excluding A142 has 649 passes and 23 failures across 672 cases, 133.9545 s. It already includes the A90 fallback-text failure introduced before this speed work: its assertion expects `basket->shop` in the rendered error text after the redundant diagnostic was removed. No checker or expectation was changed here. Master has no equivalent native audit fixture; the earlier common-checker replay and native-suite comparison remain in the pre-merge report. The master native suite was 2306 passed, 226 failed and 11 watchdog timeouts.

Validation: all 44 original spacing/assignment tests pass; the new fixed-membership regression passes; all three new routed-trial equivalence tests pass (including honoured hints and A167); all 11 protected production regressions pass. Total **59 passed, zero failures** in these sets. No existing expectation changed. A47 and every other comparable catalogue geometry signature remain exact.

Complete successfully measured catalogue totals, including stress cases: **2695 bends, 2054 physical crossings, 275 shared pairs, 1350 rule 5.9 findings, 1693 findings overall**. Five layout failures are not counted as zero findings.

### Shared-track starting counts

| Scenario | Shared pairs |
|---|---:|
| A110 | 30 |
| A113 | 7 |
| A114 | 108 |
| A116 | 1 |
| A117 | 8 |
| A153 | 50 |
| A164 | 13 |
| A167 | 2 |
| A169 | 5 |
| F111 | 2 |
| F113 | 9 |
| F61 | 1 |

A142 adds 23 previously unmeasured pairs. A160 has 9, A165 has 1 and F153 has 6. Shared-track work starts next with A114 and A110; none of these counts is waived.

Evidence is stored outside the repository in C:\Users\windo\AppData\Local\Temp\premerge-20261021-976961\premerge-fixes-6c07d39: before/local/trial JSONL profiles, A142-before.nettrace, baseline-audit, speed-audit-v2 and speed-results/speed-protected. The first speed-audit attempt is retained separately; it exposed A167's incomplete-seed key failure and is not the retained acceptance run. No DOCX or PNG regeneration.

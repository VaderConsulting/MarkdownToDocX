# Reject the incremental cell-scoring prototype

## Separate frame-fit checkpoints

The frame fitting work was already committed separately from final-layout scoring:

- `6d55987` moves A165's complete hint-dependent set.
- `210d917` adds the complete joint alternatives needed by A164.
- `531ab82` separately preserves original group hint anchors and their dependent member hints.

Their changes do not contain the final-layout scoring implementation. That implementation is in `2db6901`. No history was rewritten and no duplicate empty frame-fit commit was made. The restored current tree passes all 311 focused tests, including the earlier 305, and all 60 cell/source regressions.

## Profile used

The latest complete profile (recorded in `cell-finished-score-review.md`) identifies 3,921 private candidate completions costing 79.60 seconds, with nine A142 completions costing 37.50 seconds. Repeated terminal-port repair costs 25.60 seconds across the catalogue, facing-side assignment 21.82 seconds and route rebuilding 7.90 seconds. These are nested timers, not additive parent totals. Snapshot capture and restoration cost only 0.0436 seconds. The final-diagnostics marker includes nested completion and cannot be read as pure diagnostics time.

## Experiment and keep decision

The uncommitted prototype cached unchanged bend, crossing-pair, disconnection and route-fault costs. Each candidate identified moved node/frame bounds, nearby corridors and redistributed peer ports, rebuilt only affected routes, asserted exact preservation outside that set, and fully completed the selected winner for confirmation. Its two move/span tests matched the full scorer; all 313 prototype focused tests and the four selected real-source rollback/final-cost tests passed. The checker, weights, rule file and named limits were unchanged.

The full experimental cells-on audit emitted all 673 summaries in **43.55 seconds**. A142 took **15.2505 seconds**. Despite the speed gain, the prototype fails the user's keep conditions:

- Eight of the original 81 scenarios have more bends than the retained finished-scoring version. Seventy-two are unchanged and A171 improves by one bend. Aggregate bends rise from 1,332 to 1,346.
- F58 has one off-slot node port and one affected allocation group. `Persist`, cylinder, left source port on `L-Persist-Done-12`, was measured at fraction 0.338 instead of 1/3. Legality remains zero, but the exact-slot gates do not.
- A142 remains slightly above the requested under-15-second limit.

| Scenario | Retained bends | Trial bends | Increase |
| --- | ---: | ---: | ---: |
| F59 | 6 | 10 | 4 |
| A31 | 8 | 11 | 3 |
| A38 | 5 | 7 | 2 |
| A107 | 14 | 15 | 1 |
| A141 | 12 | 13 | 1 |
| A161 | 26 | 28 | 2 |
| EIP215 | 12 | 13 | 1 |
| EIP246 | 12 | 13 | 1 |

The scoped builder did not reproduce complete route refinement well enough to preserve the previous accepted choices. Each of the eight finishes with no accepted move in the shared budget, losing the prior layout's lower bend count. Full confirmation is a safety guard but cannot repair an incorrectly ranked candidate pool: rejecting its provisional winner can leave the original layout rather than the better option found by the old search. This is a limitation of the rejected prototype, not justification to weaken a gate. The exact mechanism of the F58 final slot drift remains unresolved; no fix is claimed after the stop condition.

The shared budget's `AcceptedMoves` includes earlier placement/frame work; it must not be interpreted as a count of accepted final-route moves without the actual `cell-move` winner trace. The earlier report's 45/5 accepted-versus-retained split was based on that shared counter and overstated its attribution. Per-case bend measurements themselves are unchanged.

**The entire prototype and its two tests were reverted. There is no scoring-change commit.** The attempted sources and logs remain only in ignored `out/logical-grid-prerequisite/incremental-experiment`. `cell-incremental-score-rejected-bends.csv` records all 81 trial measurements. No further optimization was attempted after the failure; only restoration verification was run.

Production cells and independent spans remain OFF. The sixteen-candidate limit, rules.yaml, weights, original hints and baseline rows remain unchanged. No DOCX files are included and nothing is pushed.

## Restoration verification

After reverting every prototype source and test file, the restored full cells-on audit emits 673 summaries in **118.50 seconds**, A142 **50.1762 seconds**. All 673 rows are ordinally identical to the retained checkpoint, including F58's zero slot findings. All 311 focused tests and 60 cell/source regressions pass again. This is the unchanged slow implementation, not a retained speed improvement.

| Gate / counter | Rejected trial | Restored cells on |
| --- | ---: | ---: |
| Summaries | 673 | 673 |
| Illegal paths | 0 | 0 |
| Missing / no legal routes | 0 / 0 | 0 / 0 |
| Title bands / frame overlaps | 0 / 0 | 0 / 0 |
| Grid / off-grid nodes / frame edges | 0 / 0 / 0 | 0 / 0 / 0 |
| Off-slot ports / allocation groups | 1 / 1 | 0 / 0 |
| Hint accounting / cleanup | 0 / 0 | 0 / 0 |
| 4.2 | 52 | 52 |
| 5.9 | 1244 | 1234 |
| 3.1 / 4.1 | 2 / 0 | 2 / 0 |
| 3.2/4.7 | 1 | 0 |
| 3.3/5.2 | 5 | 5 |
| 4.9 / 4.11 / 5.8 | 4 / 5 / 3 | 4 / 5 / 3 |
| Shared-track pairs | 348 | 346 |

The audit remains nonzero for unchanged quality/shared-track findings and five native skips/empty cases. Every restored legality and slot gate is zero. The rejected prototype provides evidence for the cost reduction, not an acceptable engine change. No incremental scoring commit was made.

Restored cells-off audit: **39.41 seconds**, A142 **12.0582 seconds**, all 673 summaries byte-identical to the original saved baseline. Every legality, grid and slot gate is zero. Quality totals remain 4.2=44, 5.9=1694, 3.1=4, 4.1=0, 4.11=3, 5.8=0 and shared tracks=2089. The restored cells-on totals above are unchanged from the preceding checkpoint.

# Hint-dependent frame fitting — 18 October 2026

Production cells and independent text spans remain disabled. No rule, limit, score weight or baseline was changed. The F32/EIP261 routing refinement remains parked.

## Frame-fit fix

Checkpoint `00528f7` builds a moving group or node's complete active placement-hint dependent closure, including group membership and transitive references. All target regions and gap changes are prepared together and checked before atomic application. Rejected assignments restore exact ownership and gaps.

Closure alone fixed A165 but not A164. A164 needed complete multi-step alternatives: the search now carries private partial assignments through the existing three-level chain depth and reserves the remaining named candidate budget across those levels. Partial assignments are never published. Complete legal options compete with the unchanged placement score.

| Scenario | Examined candidates | First legal candidate | Frame search | Whole census scenario |
| --- | ---: | ---: | ---: | ---: |
| A164 | 16 | 12 | 64.205 ms | 428.948 ms |
| A165 | 7 | 4 | 138.514 ms | 353.000 ms |

Final census: 673 cases pass, 662 completed cell-bound scenarios / 3,806 nodes, six open-mode scenarios remain unbound, five unchanged native skips/empty cases. Zero writer catches. Every one of the 662 final logical hint checks is true. Census elapsed time is 87.762 seconds; this includes the separate final-route scoring work described below.

Across the catalogue, 254 frame-search invocations examined 1,533 candidates in total. Maximum per-diagram count was 16. Measured search time totalled 1.896 seconds, with a 1.075-second maximum invocation. These timings include preparing proposals, not just counting accepted candidate offers.

A temporary closure-only measurement, removed from production code, enumerated its finite pool: A164 examined 97 candidates in 2.067 seconds without a legal result; A165 examined 409 in 6.976 seconds and first found legality at candidate 10. This demonstrates missing joint alternatives rather than evidence for a larger limit. **No separate frame-fit limit or increase is proposed now.** Any future parameter change still requires approval and belongs in rules.yaml with regenerated rulebook.

## Other readiness checkpoint — b599a71

The complete cells-on audit exposed and fixed late legality issues: A112 fallback normals started inside the enclosing obstacle box; A167/A168/A169 late title-band repair grew derived frame pixels into neighbours. The cell path now repairs legal routes without growing derived frames, and validates complete fallback candidates. Empty frame projection now uses the same explicit reflection origin as its member-backed peers. Nonfinite native placeholder guides cannot poison the bound-node reflection origin.

The cell grid gate verifies actual whole-cell reservations and their forward-derived pixels, including derived empty frames. It does not apply the old centre-coordinate lattice to whole-cell spans. Independent corruption tests require stale node/frame pixels to be rejected.

Cell quality moves are scored after final port allocation and route completion. Rejected candidates restore painted guides and diagnostic traces too. This fixes provisional/final score mismatch but costs time and has not established production quality acceptance. Original bend-attribution groups remain optimization work; an increase is not assumed necessary for legality merely because a bounded search did not improve it.

## Audit results

All 673 summaries emitted in each mode. Cells-off summaries are byte-identical in original order; exact geometry and ordered diagnostics changed in zero of 673 scenarios (51.130-second comparison run).

| Counter | Cells off | Cells on |
| --- | ---: | ---: |
| 4.2 | 44 | 51 |
| 5.9 | 1,694 | 1,244 |
| 3.1 | 4 | 4 |
| 4.1 | 0 | 0 |
| 3.2/4.7 | 0 | 0 |
| 3.3/5.2 | 5 | 5 |
| 4.9 | 6 | 6 |
| 4.11 | 3 | 3 |
| 5.8 | 0 | 3 |
| Shared-track pairs | 2,089 | 345 |
| Port findings | 56 | 66 |
| Aligned port jogs | 15 | 17 |
| Nonaligned port jogs | 1,323 | 1,254 |
| Off-slot node/group ports | 0 / 0 | 0 / 0 |
| Grid / off-grid nodes / off-grid frame edges | 0 / 0 / 0 | 0 / 0 / 0 |
| Hint accounting | 0 | 0 |
| Title-band / unrelated-frame overlaps | 0 / 0 | 0 / 0 |
| Missing / no-legal-route / illegal paths | 0 / 0 / 0 | 0 / 0 / 0 |
| Cleanup corrections | 0 | 0 |
| Unmeasured native skips/empty cases | 5 | 5 |
| Failing/skip audit cases | 25 | 29 |

Cells-on audit: 93.42 seconds; A142 36.3332 seconds. Cells-off measured 52.56 seconds / A142 16.9561 seconds while other test jobs were running; this is not a fair speed comparison. The subsequent serial cells-off audit took 48.82 seconds, with A142 at 15.9000 seconds; all 673 summaries are again byte-identical. No speed improvement is claimed. The experimental final-route scoring, not frame fitting, accounts for the major cells-on runtime increase.

305 focused tests and 30 real-source cell regression tests pass. The broader route regression selection passes 64 of 66, retaining the known F153 and large-fan shared-track failures. Production activation is not approved or performed. DOCX files are excluded from the checkpoints; nothing pushed.

The full Layout test project has 1,082 passes and 216 failures (1,298 cases). An untouched 49f9da9 checkout has 1,073 passes and the identical 216 failing test names (1,289 cases). The nine new cases pass; no existing failure was added or removed. The broader suite is therefore not described as all green.

For the 660 original completed cell cases, total bends are now 3,183, compared with 3,211 before this readiness work and 3,270 before the placement-policy review. There are 85 improved, 92 increased and 483 unchanged scenarios against the original review baseline. Of the original 81 increases, three are no longer above baseline and 78 remain above. Fourteen other cases are now above baseline. The seven historical attribution groups (34 shared-fan/scoring, 28 main-chain columns, 14 fan-in median, two chord swaps, one nested group, one partner rank and one junction branch) identify where differences first appeared, not proof that those bends are unavoidable. This broader per-route diagnosis remains open and blocks a production-readiness claim.

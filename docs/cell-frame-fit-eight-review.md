# Eight cell-bound frame-fit failures — 2026-10-18

The snapping checkpoint already exists as `f046ae7`. Production remains cells-off. This repair changes only the experimental cell pipeline, not rule conditions, checker logic, baseline rows, score weights, or independent text-span activation.

## Measured causes and accepted repairs

All eight failures were clearance failures around correctly derived frames, not undersized node spans or lost member containment. A title strip and padding can extend beyond member reservations, so adjacent exclusive node cells alone do not establish legal frame clearance.

| Scenario | Exact shortage at frame fitting | Accepted complete assignment |
| --- | --- | --- |
| F42 | `R0S0` versus `IN` and `OUT`: 60 px horizontal clearance missing on each side. The old translation search spent its candidate budget clearing one side and never offered shared-gap growth. | Grow the external column gaps after columns 1 and 3 by 60 px each. Every node keeps its region and spans. |
| F141 | `Zone` versus `Gate`: 90 px vertical shortage; `Zone` versus `Db`: 30 px; outer `Region` versus `Client`: 120 px. Nested title/padding clearance was missing above and below the inner frame. | Move unhinted `Client` from row 0 to row 5; grow row gaps after rows 1, 3 and 4 by 90, 30 and 60 px. This scores 44.3251 versus 47.0799 for the legal gap-only option. The lower-scoring illegal option is rejected. |
| A162 | `box` versus `out`: 60 px horizontal clearance missing. All four nodes were treated as absolute pixel pins, so the old search offered no candidate. | Grow the external gap after column 1 by 60 px. Every hinted cell start and span stays exact. |
| A167 | `mgmtRoles`/`regRoles` overlap by 30 px; role-frame clearance to `admin`/`owner` is short by 60 px, to `explorer`/`publicUser` by 30 px horizontally, and to `sdt` by 30 px vertically. `publicUser` also has a 60 px vertical shortage. Absolute hint locks wrongly blocked external gaps. | Grow column gaps after columns 31 and 41 by 30 and 60 px, and row gaps after rows 3 and 4 by 60 and 30 px. No node region moves or resizes. All logical hint relationships remain honoured. |
| A169 | DAW frame versus the agency row: 30 px vertical shortage; registered-role frame versus the agency row: 90 px. `otherAgencies` is an outside node interleaved inside the registered-role frame's member envelope, requiring 180 px vertical separation from its original location. Gap growth alone cannot remove that interleaving. | Move `otherAgencies` from row 4 to row 5 at its unchanged starting column 8. Grow row gaps after rows 2, 3 and 4 by 30, 90 and 30 px. Its declared `below localGov` alignment/direction is preserved; the extra row is needed for legality. |
| EIP78 | Empty `alt-0`/`else-1` frames remain obstacles around the origin. The foreign `R`/`V` nodes conflict with them: outer-frame vertical shortages are 210/60 px, and the nested-frame shortage for `R` is 120 px. Individual node moves leave another node blocked. | Move the complete `R`–`V`–`I` connected component four whole rows north in one assignment. Empty frames, memberships and node spans remain unchanged. |
| EIP266 | Empty `loop-0`/`alt-1`/`else-2` frames conflict with `C` and `Q`. Outer loop vertical shortages are 270/120 px; inner alternative shortages are 210/60 px. The former search used chain depth as a distance cap and moved one node at a time. | Move connected `C` and `Q` two whole columns east atomically. No node growth or frame-membership change. |
| EIP271 | Empty `alt-0` frame versus both `B` and `H`: 30 px horizontal shortage. The individual repairs could not finish the pair within the shared budget. | Move connected `B` and `H` three whole columns west atomically. No gap or span growth. |

No requested scenario requires a rule change. No member is moved out of its group, no unrelated node is resized, and no checker condition is relaxed. Every gap increase is a whole 30-px fine-grid step, while region translations are whole logical cells.

## Engine changes

The search generates column-gap, row-gap, combined-gap and translated-unit alternatives, including connected components beside empty frames. It prepares complete immutable ownership/gap candidates, projects their frames, and rejects every candidate retaining a frame conflict. It compares legal complete results using the existing aspect and raster empty-space placement score over both nodes and frames, rather than smallest translation distance or a fixed gap/move priority. No route penalties are invented before routes exist.

The named limits remain unchanged: `repair_candidate_moves` bounds examined final alternatives, `repair_chain_depth` bounds combined gap rounds and accepted repairs, and `repair_chain_lines` limits affected lines when routes already exist. Graph degree alone no longer prevents an initial pre-routing placement repair. Chain depth is not used as the maximum physical displacement in cells.

Actual cell hints now validate whole logical relationships. External gap growth may change derived pixel positions without changing the hinted regions. Moving a hinted region is allowed only for a frame-legality candidate that still satisfies every active hint; normal optimizations retain their locks. Explicit manual absolute locks remain strict. Candidate construction never publishes a partial state; acceptance publishes once, and a late rejection restores the original region/gap snapshot and exact frame objects.

## Verification and remaining work

* 277 focused layout tests pass, including the original 163. Seven new cases cover logical hint preservation, manual/ordinary locks, minimum offsets, wrong-side/start-row rejection, exact rollback, and both directions of the move-versus-gap score choice.
* Eight new non-explicit catalogue regressions require the complete real cell-bound pipeline to finish. A first guard catch cannot count as a regression pass. All eight pass.
* Full cells-on census: all 673 scenarios, zero writer catches, hard frame-fit failures **17 → 2**, completed bound cases **645 → 660**. Six open-mode cases remain unbound and complete; five known pre-binding skips remain. Final census time: **26.4261 s**. The eight reviewed cases all complete; seven additional frame cases improve incidentally.
* Remaining failures are **A164 and A165**, both in `GroupFrameStage` after refreshing relative placement hints. They remain unresolved under this bounded search; they are not established rule conflicts and are not silently accepted. Full cells-on legality acceptance is still pending them.
* Cells-off audit: **673 byte-identical summaries**, **35.05 s**, A142 **10.6321 s**, every prior zero gate still zero. Totals remain 4.2=44, 5.9=1694, 3.1=4, 4.1=0, 4.11=3, 5.8=0 and shared-track pairs=2089. The same 25 known audit failures and five unmeasured pre-binding cases remain.
* Exact serialized comparison of page geometry and ordered diagnostic findings: **673 scenarios, zero changes, zero missing**, 33.859 s. Production cells and independent spans remain off. F32/EIP261 stays parked.

Evidence is under `out/logical-grid-prerequisite`: `frame-eight-before-trace.log`, `frame-eight-regressions-final.log`, `frame-search-final-focused-tests.log`, `census-scored-frame-search-final.log`, `scored-frame-search-off-audit.log`, and `scored-frame-search-off-geometry-and-diagnostics.log`.

# Route crossings and search speed, 16 October 2026

The final audit emitted all 673 summaries in **54.14 seconds**. It measured 668 successful layouts and explicitly reported the unchanged five failed layouts (F65, F70, F87, F88 and A01). Failed layouts are not counted as successful zero-finding diagrams. The final A142 audit case took **24.7305 seconds**; the isolated prepared-geometry profile took **23.90 seconds**. The c6605bd comparison was 48.78 seconds for the audit and 25.80 seconds for A142. The checkpoint audit at 404ce4c took 84.52 seconds.

| Metric | c6605bd | 404ce4c | Current |
| --- | ---: | ---: | ---: |
| 4.2 | 39 | 39 | 39 |
| 5.9 | 1794 | 2430 | 1788 |
| 3.1 | 13 | 4 | 4 |
| 4.1 | 3 | 0 | 0 |
| Shared-track pairs, same checker | 2333 | 1796 | 2271 |

Illegal paths, title-band crossings, off-slot ports, off-grid nodes and frames, frame overlaps, missing routes, NoLegalRoute failures and cleanup corrections are all zero among successful layouts. The 13 passing regressions and both illegal-path tests pass. Shared tracks remain explicitly reported failures, not waived legality findings.

## What caused the slowdown

The added terminal search called the full rule checker for every route and some candidates merely to obtain port-distribution findings. It now calls the same distribution check directly; regression assertions compare its findings with the full checker. The full checker also rebuilt the same route-point arrays inside every shared-track pair test. It now builds each array once, and rejects geometrically separated proper-crossing pairs before the exact segment test. The exact pair predicate and its tolerances are unchanged.

Each port option built another obstacle dictionary after removing traversable frame rectangles. The title-band helper consequently reconstructed the missing frames again for each dictionary. Port options now share the same immutable prepared obstacle geometry, including the same title-strip checks; diamond endpoint filtering remains separate. A focused test compares all prepared and unprepared candidate paths. Immutable path costs and ordered path interactions are also reused within one side search.

The segment obstacle check previously resolved group membership for every distant node before checking whether its rectangle intersected the segment. The identical geometric rejection now comes first. Pair intersections allocate their crossing deduplication set only when a crossing exists, and measure the first segment once per outer loop.

The audit now uses one host, matching the pinned audit, instead of repeating process startup, recognizer warm-up and JIT compilation three times. The existing progress watchdog still requires every summary and reports any stalled or failed case. Existing rule limits remain unchanged: repair_candidate_moves = 16, repair_chain_depth = 3 and repair_chain_lines = 12. No new search cap, weight or timeout was introduced.

The audit is substantially faster, but it remains **5.36 seconds above** the 48.78-second reference. A142 remains well above its 10-second target.

## Crossing attribution

The rule 5.9 algorithm did not change between c6605bd and 404ce4c. Applying the current checker to all stored c6605bd models reproduces **1794**. Therefore **none of the 636 net increase was caused by a stronger checker counting the old geometry differently**. The increases are geometric. Some replace illegal shared tracks or node crossings; that does not prove they are the fewest crossings achievable by a legal layout.

The largest fault was the final track seal: it accepted any reduction in shared pairs, even if the incomplete repair introduced crossings. It now rejects that exchange. A fully separated assignment can still replace an illegal shared-track assignment, preserving legality and the grouped 3×3 regression. The final port search also compares actual rule 5.9 proper crossings, including common graph-endpoint pairs; quality improvements alone cannot justify additional crossings, while strict path/shared-track legality improvements remain eligible.

The following are the 20 largest increases at 404ce4c, sorted by increase and then scenario id. Counts are crossing pairs, not raw intersection points. Stage attribution comes from complete route/bounds snapshots before and after the named stage; no scenario-specific engine rule was introduced. The joint channel search did not increase crossings in these traces.

| Scenario | c6605bd | 404ce4c | Current | Cause of the checkpoint increase |
| --- | ---: | ---: | ---: | --- |
| A163 | 821 | 1269 | 821 | Final partial track seal added 448; node bounds and sides were unchanged. Fixed. |
| A153 | 24 | 141 | 24 | Final partial track seal added 117. Fixed. |
| A110 | 258 | 292 | 256 | Track seal added 36 to a pre-seal count of 256. Fixed. |
| A113 | 120 | 146 | 120 | Final partial track seal added 26. Fixed. |
| A117 | 45 | 58 | 55 | Placement/port redistribution and rebuild reached 54; terminal repair added one; the partial track seal added three. The last three are fixed. |
| F111 | 6 | 15 | 15 | Grouped 3×3 placement/port rebuilding reached 11; terminal repair added three and completed separation added one. The baseline also had 18 node-interior findings and 10 shared pairs. |
| A104 | 18 | 23 | 18 | Final partial track seal added five. Fixed. |
| A116 | 13 | 17 | 16 | Terminal repair added three while reducing shared tracks; partial final separation added one. The last one is fixed. |
| F136 | 0 | 3 | 3 | Grouped many-to-many placement and port/track assignment. The old zero-crossing geometry had four shared pairs; the new layout is legal and separated. |
| A169 | 2 | 4 | 4 | Node-interior legality fallback added two. The old geometry contained a node crossing. |
| A24 | 3 | 5 | 5 | Changed side/port allocation reached four; completed shared-track separation added one. |
| F113 | 56 | 58 | 58 | Completed shared-track separation added two. |
| A141 | 0 | 1 | 1 | The crossing appears in coupled repair/post-repair port redistribution after draft/approve move. The exact responsible move is not yet isolated. |
| A161 | 3 | 4 | 4 | Missing-route side search adds a crossing while preserving the north-leave hints. |
| A165 | 0 | 1 | 0 | Final partial track seal added one. Fixed. |
| A167 | 42 | 43 | 43 | Terminal-normal repair reached 44; node-interior fallback reduced it to 43. The old model had four square-terminal failures and five node-interior findings. |
| A18 | 0 | 1 | 1 | Changed node placement and side assignment; the crossing predates final completion. The exact responsible placement change remains unresolved. |
| A52 | 0 | 1 | 1 | Final terminal candidate adds one while reducing shared-track illegality. |
| A69 | 0 | 1 | 1 | Final port allocation/rebuild after node moves introduces one. The exact allocation choice remains unresolved. |
| A70 | 0 | 1 | 0 | Final partial track seal added one. Fixed. |

Two other tied increases remain outside that top-20 selection: **EIP142, 0 → 1**, introduced during fan/branch repair and retained by the aligned-route rebuild; and **EIP157, 0 → 1**, introduced by the final complete shared-track separation. Their proper-crossing definition is unchanged.

Thus 15 scenarios remain above their old crossing counts: the 13 above whose current count exceeds the first count, plus EIP142 and EIP157. Their remaining increases are not declared unavoidable or fully fixed. The stronger checker did not cause them.

## Validation and open work

The original named regression selection still has 13 passes and four known failures; F29 and F119 both pass their illegal-path tests. Eight additional catalogue regressions cover the restored crossing ceilings in A163, A153, A110, A113, A104, A165 and A70, plus the exact single projected port in F147. The prepared-geometry and diamond candidate selection passes 15/15; focused routing/placement hint checks pass 8/8 (23/23 in the combined final selection). Baseline rows, rules.yaml and score weights are unchanged. Full-audit hard gates remain zero. The source diff contains no diagram-specific engine condition.

Open work remains: the audit time against c6605bd, A142 against 10 seconds, the individual crossing increases above, F153's legal bend/crossing regression, and shared-track failures in F148, A147 and the 20-way and 30-way fans. No column-grid fan placement was started. No push or DOCX regeneration was performed.

Raw audit evidence is in ignored out/route-cost/checkpoint-complete-final-audit.log, with stage snapshots and per-case comparisons in the same directory. The audit test suite reports additional failures for the acknowledged nonzero quality rules, as well as the five pre-existing failed layouts; the all-zero claim applies specifically to the listed hard gates.
## Summary coverage and exact port restoration

The summary previously omitted off-slot **group** findings even though it reported off-slot individual ports. Rejecting a track-growth repair exposed six such findings in A110/A113. The port-repair copy helper rounded exact fractional k/(n+1) slots even when restoring a rejected option. Copies and snapshots now preserve the original doubles. A projected single cylinder port also rounded an open layout's fractional centre in F147; allocation now retains the exact cardinal side centre. These corrections restore zero off-slot group findings and retain the 4.2 ceiling without changing checker tolerances or baseline rows.

The audit now aggregates every emitted checker rule automatically, plus composite port/grid and jog diagnostics. It includes hint-accounting failures and records every failed scenario and exact case duration from the test result file, so failures of scenario-specific assertions cannot be hidden behind aggregate zero fields. The final result has offSlotGroups = 0, gridFindings = 0 and hintAccountingFailures = 0. Thirty audit tests still fail for known layout/shared-track/quality findings; this is not a fully passing suite.

| Previously omitted rule category | c6605bd | Current |
| --- | ---: | ---: |
| Port distribution, 3.2/4.7 | 0 | 0 |
| Port ordering, 3.3/5.2 | 5 | 5 |
| Frame-edge runs, 4.9 | 6 | 6 |
| Convergence sides, 4.11 | 6 | 57 |
| Fan sides, 5.8 | 7 | 48 |

The convergence-side and fan-side increases are additional open quality faults. They were not previously zero gates and are not waived or described as checker-counting changes. Composite port findings = 153, aligned-port jog diagnostics = 17 and nonaligned-port jog diagnostics = 1390 are also visible; those diagnostics are not claims of zero.
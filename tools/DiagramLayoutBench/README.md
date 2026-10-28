# Diagram layout experiments

Serial, cold-process Enterprise measurements for the incremental layout plan. Records final SVG dimensions, a reference 12 px text size at fit-to-width, route counts, bends, crossings, hard findings, geometry/findings/SVG hashes, wrap alternatives and render stage timings. Saves an SVG beside each JSONL record. Parsing and fixture setup are outside the render timer.

```powershell
dotnet build tools/DiagramLayoutBench/DiagramLayoutBench.csproj -c Release -m:1
dotnet tools/DiagramLayoutBench/bin/Release/net10.0/DiagramLayoutBench.dll out/incremental-layout/a-fixtures.jsonl '@fixtures'
dotnet tools/DiagramLayoutBench/bin/Release/net10.0/DiagramLayoutBench.dll out/incremental-layout/a-complete.jsonl '@complete'
# An explicit width enables AH for this request. AF and AG remain off.
dotnet tools/DiagramLayoutBench/bin/Release/net10.0/DiagramLayoutBench.dll out/incremental-layout/b-fixtures.jsonl '@fixtures' 1200
# A saved Enterprise source can replace the fixture selector.
dotnet tools/DiagramLayoutBench/bin/Release/net10.0/DiagramLayoutBench.dll out/incremental-layout/custom.jsonl path/to/source.mmd 900
```

Quote selectors beginning with `@` in PowerShell. Use a fresh process per invocation: geometry caching and diagnostic observers belong to the process. Observers are restored after each case. A render exception makes the command fail and is retained in the JSONL. Legality findings are measurements, so compare them explicitly rather than interpreting a successful render as a clean quality gate.

`docs/bench/incremental-layout-baseline.json` records the first all-flags-off capture, including hashes and the fact that pre-existing uncommitted workspace changes were present. It is a baseline for this experiment, not a replacement for the production catalogue audit. Timings are individual observations, not speed claims.

## Initial diagnosis

The Complete Enterprise sample renders at 2146 x 1666 with 25 visible nodes, 20 routes, 19 bends, one crossing and zero hard route findings. Its seven wrapping alternatives leave the footprint near 2130 model pixels wide; one is illegal and narrower column budgets mostly add height and bends. Existing wrapping therefore cannot solve this case simply by choosing another available wrap.

The production band path currently excludes single-layer Enterprise graphs. Dense linked ranks, solid group blocks, explicit horizontal chains and placement hints also restrict which regions can wrap. These cases must be distinguished before changing selection policy. The fixtures include single-layer free nodes, a same-layer fan, a cross-layer fan, nested groups with parallel endpoints, and an explicit LR chain.

The existing band tests `CrowdedBandGapGrowsAtomicallyBeforeRouting` and `WrapSelectionPublishesTheCheapestFullyRoutedCandidate` fail both before and after the option plumbing. A separate build using the pre-option context, routing entry point and wrap/corridor files reproduced both failures. Captures are in `out/incremental-layout-tests/pre-options-failures.trx` and `layout-options-regression.trx`. The seven new engine option tests and the LiveEditor flag test pass.

## First AH experiment

AH now offers single-layer Enterprise band candidates and can split peers in vertical ranks into additional whole-cell rows. Candidate columns include the requested width and a narrower neighbour, within the existing candidate limit. The additional rank folding retains the existing producer for groups, explicit placement hints and horizontal main-axis chains. All complete alternatives still pass the existing legality checks; accepted candidates retain every hint honoured by the unwrapped baseline.

The experimental selection policy minimises overflow of the requested width, then uses the existing weighted bend/crossing cost, then height, then the existing complete score. This makes the width trade-off explicit and leaves canonical rule values unchanged. Candidate bounds include nodes, frames, route points and diagram padding. Final SVG dimensions are measured separately: label/title postprocessing and later route completion can still extend the footprint. The target is a preference, and the benchmark reports whether the final SVG meets it; it is not a proof of minimum feasible width.

| Fixture | A: current | B: AH at 700 px | Bends A -> B | Crossings A -> B |
| --- | --- | --- | --- | --- |
| Single-layer free nodes | 166 x 1756 | 526 x 556 | 0 -> 0 | 0 -> 0 |
| Single-layer fan | 1006 x 1396 | 586 x 736 | 4 -> 13 | 0 -> 0 |
| Cross-layer fan | 526 x 1516 | 526 x 1516 | 6 -> 6 | 0 -> 0 |
| Nested groups / parallel endpoints | 346 x 706 | 346 x 706 | 0 -> 0 | 0 -> 0 |
| Explicit LR chain | 1426 x 106 | 1426 x 106 | 0 -> 0 | 0 -> 0 |

All relationships remain present; these cases have zero hard route findings. The Complete sample with AH at 1200 px remains 2146 x 1666, with 19 bends and one crossing. This experiment does not solve its group/hint constraints. All six flag-off geometry, findings and SVG hashes match the initial baseline exactly. These are focused fixture results, not a full production catalogue certification or performance improvement claim.

Seven new width tests cover rank folding, mixed measured spans, route legality, inactive options, explicit direction/hints and alternating A/B cache entries. With the seven option tests and existing band/render tests, the focused suite has 51 passes and the same two reproduced pre-existing failures. The LiveEditor flag test also passes.

Next width tasks are presets and fit-to-width viewing, final label-aware candidate comparison, and generation of narrower constrained group layouts. Incremental relationship editing still requires the retained-state foundation described in the plan.

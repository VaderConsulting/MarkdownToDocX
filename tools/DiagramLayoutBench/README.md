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

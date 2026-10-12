# Brief: code slop cleanup

## Goal
Remove clutter from the layout source without changing any output. Every step is a pure refactor or a removal. This brief comes from a read-only review of the source. The layout code is about 77,000 lines in 180 files. The main problem is size and layered patches, not style.

## Ground rules
- No behaviour change. No step may alter a diagram.
- Run the five test suites before and after each step. Compare the counts. There must be no new failure.
- Never change an existing test to make it pass. If a test depended on removed code, list it for Dave to decide.
- Regenerate nothing, except as working rule 8 in `currentstatus.md` requires.
- Before and after each step, measure the scenario metric table for `flow.md`, `enterprise.md` and `EIP.md`: size, crossings and score of every diagram. The two tables must be identical.
- One commit per step. Push after each commit. Update `currentstatus.md` in the same commit.
- Line numbers below were checked on 12 October 2026. They drift. Search by name if a line has moved.
- If a step is not safe, stop that step, record the reason, and go to the next step.

## Step 1: remove scenario-specific layout
- File: `src/MermaidRecognition.Layout/FlowchartSample03OracleLayout.cs` (220 lines).
- It is keyed to the node ids `dc`, `appNode`, `jvm`, `war`, `pg` and to `webShop` (lines 46 to 50 and 53 to 123).
- It is called through `MatchesSample03` at `FlowchartExternalNodePlacementSearch.cs` lines 60 to 62.
- Delete the file and the gate. The layout must come from the generic rules or from hints.
- List every test that depended on it. Do not edit those tests.

## Step 2: remove fixed caps
Dave's rule: no fixed limits. Use the stop rule of rules 2.11 and 2.12. Stop when a full pass changes nothing or repeats an earlier layout. Keep the best score. Where a loop needs a termination guarantee, use progress: each pass must strictly improve the score, or stop. Do not use a count.

Caps to remove:
- `DiagramGridLayout.cs:19` `MAX_SEPARATION_PASSES = 32`, used at lines 1741 and 1978.
- `DiagramGridLayout.cs:4369` `Guard < 32`.
- `FlowchartIntergroupNodePlacement.cs:74` `Step < 32`.
- `Enterprise/EnterpriseJunctionCaptionLayout.cs:31` `Step < 100`.
- `FlowchartSharedRouteQualityHelper.cs:1849` `Iteration < 6`.
- `FlowchartDagrePresentation.cs:565` `Pass < 4`.
- `FlowchartSharedEdgeRouter.cs:1340` `Pass < 4` and `:107` `Pass < 2`.
- `FlowchartCrossGroupRouteSimplifier.cs:21` `Pass < 3`.
- `FlowchartExternalNodePlacementSearch.cs:82` `Pass < 3`.
- `FlowchartEdgeLabelClearanceHelper.cs:224` `ShiftAttempt < 2` and `:262` `Attempt < 2`.
- `FlowchartDiamondPorts.cs:153` `MAXIMUM_EDGE_PORTS = 4`, used at lines 194 and 290. Derive the count from the number of edges and the length of the box side.
- `MAX_CHARS = 22` in both `Enterprise/EnterpriseLayoutEngine.cs:28` and `Enterprise/EnterpriseNodeSizer.cs:22`. Define it once. Derive it from the font metrics and the wrap rule.

The vendored Sugiyama port caps are out of scope.

## Step 3: remove static mutable state
Pass a render context object instead. Statics:
- `FlowchartSharedEdgeRouter.cs:56` `_LastJustifications`.
- `FlowchartSvgLineJump.cs:26` `_LastJumps`.
- `FlowchartEdgeRouteResolver.cs:16` `_CachedRouteContract` and `:18` `_CachedRouteGuides`.
- `FlowchartSugiyamaSvgRenderer.cs:38` `_ActiveRenderSession`.

These are not thread-safe. Two renders at once can corrupt each other.

## Step 4: replace the silent catch
- `Enterprise/EnterpriseLayoutEngine.cs:315` has `catch (Exception)` and falls back to `PlaceInGrid`.
- Catch specific exceptions. Record a diagnostic. Never hide a layout bug.

## Step 5: one source for repeated constants
Copied geometry thresholds:
- `FlowchartRenderedSvgQualityGate.cs`: line 723 (`160.0`), line 1147 (`180.0` and `40.0`), line 3672 (`2.0` and `12.0`).
- `FlowchartSharedPictureCleaner.cs`: line 1254 (`200.0`), line 1958 (`16.0` and `1.0`), line 3112 (`>= 100.0 ? 28.0`). Lines 1254 and 1958 are near copies, not exact. Check each before merging.
- `FlowchartCompoundEndpointHelper.cs:47` to `:58`: `Math.Min(15 ...)` and `Math.Min(40 ...)` repeated four times.
- `FlowchartDagrePresentation.cs:425` and `:474`: `< 100.0`.

Move them into one named policy class tied to the rulebook. About 317 `const` declarations and about 384 literals of two to four digits exist overall. Convert only values that are repeated or linked to a rule.

## Step 6: merge near-duplicate allocators
- `FlowchartFanOutPortAllocator` and `FlowchartFanInPortAllocator`: about 30 deeply nested lines each.
- `FlowchartDecisionPortAllocator`: 22 nested lines.
- Extract one shared port allocator that follows rules 4.7 to 4.10. Reduce nesting with early returns.

## Step 7: split the largest methods
- `FlowchartSugiyamaSvgRenderer.cs:65` `RenderToSvg` (743 lines). Split into named stages.
- `FlowchartContainerPacking.cs:18` `Apply` (277 lines).
- `DiagramGridLayout.cs:28` `Apply` (173 lines).
- `FlowchartDagrePlacement.cs:29` `TryPlace` (166 lines).

Method lengths come from an approximate parser. Measure again before you start.

## Step 8: propose folding the repair classes
About 21 classes repair routes one after another. Examples: `FlowchartAlignedRouteStraightener`, `FlowchartTwoCornerRouteRepair`, `FlowchartObstacleRouteRepair`, `FlowchartPortSideRouteRepair`, `FlowchartExteriorCrossingRepair`, `FlowchartCrossGroupRouteSimplifier`, `FlowchartForwardRouteSimplifier`, `FlowchartWrappedChainRoutes`, `FlowchartWrappedFanRoutes`, `FlowchartSharedPictureCleaner`.
- For each class, write down what it does and which rule number it implements.
- Merge into one ordered route-normalisation pass only where the output stays identical.
- This step is a proposal first. Do not force a merge. List what you did not merge and why.

## Step 9: split the catch-all files
Split by rule family: one class per rule number or family.
- `DiagramGridLayout.cs`: 4,765 lines.
- `FlowchartSharedEdgeRouter.cs`: 4,677 lines.
- `FlowchartSharedPictureCleaner.cs`: 4,596 lines.
- `FlowchartRenderedSvgQualityGate.cs`: 4,165 lines.
- `DocxWriter.cs`: 3,163 lines. Split by block type: paragraph, table, image, list.
- `FlowchartSugiyamaSvgRenderer.cs`: 3,105 lines.

Other large files, for reference: `FlowchartSubgraphLayoutPostProcessor` 2,252, `FlowchartSharedRouteQualityHelper` 2,122, `FlowchartLayoutPostProcessor` 2,092, `FlowchartDecisionPortAllocator` 1,615. In all, 105 Layout files exceed 500 lines.

## Step 10: dead code and unused parameters
Not yet done. Enable the .NET analysers IDE0051, IDE0052, IDE0060 and CA1822 locally. List every finding. Remove only what is provably unused. Do not commit the analyser settings unless Dave agrees.

## Healthy: keep as it is
- No TODO, HACK or FIXME.
- No commented-out code.
- No scenario ids (`A###`, `F###`) in source.
- Tests have almost no pixel assertions.
- The converter and the CLI are small and clean.

## Done when
- Each step is committed and pushed separately, with test counts before and after.
- The scenario metric table is unchanged.
- A short table shows the lines removed or moved per step.
- Steps that were not safe are listed with the reason.

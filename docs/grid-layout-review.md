# Grid layout review — 9 October 2026

This review records the code at `6fc1d41` on PR #9, branch `cursor/many-to-many-routing-3834`. It is a measured improvement, but the visual acceptance work and the rulebook implementation are incomplete. Master remains at `f09cb89`. No merge or rebase was performed.

## 1. Fresh test comparison

Every suite was run once on master and once on the starting branch, using plain `dotnet test` with build and restore. Focused tests covered each subsequent fix. All five suites were then run once on the final source. Failure names were compared as well as counts.

| Suite | Master passed / failed | Starting branch passed / failed | Final passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 289 / 33 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 932 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 36 / 0 |

There are no new failing test names against the starting branch. Recognition has twelve explicit fixture-writing tests that are not executed by the ordinary suite. The old handoff counts of 33/11/11/0/7 were not reproduced: the table above is the new comparison.

Eight final Layout failures are absent from master's failing-name list. They were all present at the start of this pass. Six are tests added on the grid branch; two are existing tests that master passes. No extra rule-validation failures remain against master.

| Final failure absent from master | Comparison |
| --- | --- |
| `LiveSamples_ClearTheListedPictureDefects` | Existing test; still a regression against master. The original corner defect is corrected, but the combined test reaches bypass/outline defects. |
| `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules` | Existing test; still a regression against master. The HTTPS label/stroke-distance conflict remains. |
| `RenderToSvg_ValidDecision_BranchesUseOrthogonalRunsWithDiamondStub` | Grid-branch test; retains absolute coordinate expectations. |
| `TryLayout_InspectContent_BranchesUseSingleElbowOrthogonalPaths` | Grid-branch test; expects one elbow where the provisional default allows a Z. |
| `RenderToSvg_TypeDecision_NoSharedBottomStub` | Grid-branch test; retains absolute coordinate expectations. |
| `RenderToSvg_InspectContent_BranchesUseOrthogonalRunsWithDiamondStub` | Grid-branch test; retains absolute coordinate expectations. |
| `RenderToSvg_LayeredView_FarChildRouteIsOrthogonal` | Grid-branch test; has absolute coordinates and forbids cubic commands used by rounded corners. |
| `RenderToSvg_MessageFilter_BranchesEnterLeftSideWithRoundedZ` | Grid-branch test; retains absolute coordinate expectations. |

Final logs and TRX files are in `out/grid-final/`. Starting-branch logs are in `out/grid-baseline/`; master logs are in the sibling `MarkdownToDocX-master-baseline/out/grid-baseline/` worktree. Focused logs and TRX files are in `out/grid-fixes/`.

## 2. Changes pushed to PR #9

| Commit | Product change |
| --- | --- |
| `f41081d` | Reject straight shortcuts that run along a box face; preserve terminal normals and space for corners and arrows. |
| `0791d59` | Move complete internal tracks instead of individual bends, and reject moves through unrelated nodes. |
| `bf140a7` | Put labels and their stroke breaks on the same longest fitting straight run; retain adjoining rounded corners. |
| `4f2e7fb` | Use free facing diamond vertices before slanted edge centres. |
| `6103ae9` | Start with a two-unit rank gap and grow offset connections enough to hold rounded turns. |
| `9d046b8` | Use the drawn grid routes when computing validation coordinates. |
| `f10c878` | Match arrowhead checks to tagged connector strokes, including label-gap fragments. |
| `3b2b21c` | Keep turns outside ancestor-frame borders and recognise nested group endpoints. |
| `81fbc4a` | Reserve centre ports for straight continuations and retain allocated fan-out ports. |
| `6ec966a` | Keep grid retry routes and omit hop arcs from grid output. |
| `6fc1d41` | Use shared element extents to give sequence notes and consecutive messages equal clear space. |

Four new Layout tests cover terminal direction, obstacle rejection during track translation, nested-frame exits and centre-slot preservation. Two new rule-validation cases verify stroke association and rejection of a misaligned arrow. Four added converter cases cover note placement, wrapping and a note after a self message. Existing tests, thresholds and fixtures were retained. The earlier authorised curve-test rewrites remain on the branch.

The engine diff was checked for rules keyed to specific diagrams, labels or identifiers. New checks use geometry, port sides, rank membership or structural endpoint identity; no diagram-specific rule was added.

## 3. Original handoff failures

| Reported problem | Verified outcome |
| --- | --- |
| Rank/order comparison | Already passed in the fresh starting-branch baseline; still passes. |
| Store Cluster outside node/frame overlap | Already passed in the fresh starting-branch baseline; still passes. |
| Corner arrowhead in the live samples | The original tangential terminal is corrected. The combined live-sample test now reaches other defects, including an outline-gap report on the order-details bypass. It still fails. |
| Webshop label/stroke quality | Still fails: the HTTPS label's measured distance to visible stroke is 31.6403 px against a 28 px limit. The required broken stroke and character-width whitespace need to be reconciled with this assertion. |
| Diagonal group-border crossings | `TryResolve_Sample03_EdgesKeepGroupBorderClearance` now passes. |
| Extra rule-validation failures | Both freshly measured extras, hub-arrow alignment and catalogue label alignment, now pass. Catalogue marker-tangent validation also now passes. |

The five Layout tests that changed from failing to passing are:

- `TryResolveLayoutGuidePoints_F113_ParallelBranchFanIn_UsesExteriorCorridor`
- `TryLayout_MatchesFilter_UsesLeftAndRightVertices`
- `Render_FormatV1Diagram_BreaksStrokeAroundEdgeLabels`
- `TryResolve_Sample03_EdgesKeepGroupBorderClearance`
- `TryLayout_InspectContent_UsesFacingSidePorts`

## 4. Remaining rulebook work

| Section | State and remaining work |
| --- | --- |
| 1: grid and gaps | Partial. Shared sizes and larger rank gaps exist. Some placement adjustments still move individual nodes or columns without enforcing a single 30 px lattice. Label growth can still enlarge all rank gaps instead of only the gap concerned. |
| 2: placement | Partial. Spine and rejoin code exists. Many-to-many ordering, crossing-minimising swaps, nested-chain alignment and the target webshop spine are not fully verified. |
| 3: ports | Improved. Centre-slot retention and free diamond vertices are covered. Some routes retain legacy side choices; complete downstream-port preference and outline attachment across every shape remain open. |
| 4: line shape | Improved. General grid routes have normal terminals, rounded corners and no hop pass. Legacy loop/fan-in paths can still introduce diagonals; some routes exceed the forward/back corner limits or use reduced-radius corners. |
| 5: routing | Partial. Track moves preserve orthogonality. Complete port/track crossing optimisation, local gap growth and a truthful failure result instead of the unchecked last-resort route remain open. |
| 6: labels | Partial. Longest fitting run selection and actual stroke breaks share an anchor, with corner/arrow clearance. Local gap growth when no run fits, collision handling and an explicit white background on non-white themes remain open. |
| 7: frames | Partial. Title padding and containment tests pass for the reported cases, and nested exits are square to borders. Frame fitting still contains individual-node eviction; complete sibling-frame clearance and internal return-track containment remain open. The midline rule remains on hold. |
| 8: sequence | Shared row extents now provide equal conservative clearance above/below notes and between consecutive messages. Short, wrapped, left/right/over and post-self-message note cases pass. Existing horizontal clipping of a note beyond the final participant still needs correction. |

The supplied files in the original laptop folder's `docs/Instructions/` and `docs/briefs/` were read and left untouched. Their top “Decisions applied” block takes precedence over older wording beneath it.

## 5. Bounded investigations and unresolved conflicts

- **Tangential terminals:** checking endpoint alignment alone was insufficient. Normal-direction checks and explicit terminal stubs removed the original corner problem. The live-sample test then exposed a separate bypass/outline problem; the complete test is not reported as fixed.
- **Frame crossings:** terminal normals, whole-run cleanup and ancestor-frame exit clearance were checked in successive focused runs. A later centre-port change exposed a 14 px frame-clearance problem; reserving a full grid unit beyond the border restored the group-border test. The nested-frame regression test records the required geometry.
- **HTTPS stroke distance:** label anchoring, preservation of the adjoining corners and coordinate mapping were checked. The remaining 31.6403 px result is consistent with the visible gap around the label. Further variations on this assertion stopped; font sizes, whitespace requirements and the 28 px threshold were retained.
- **Branch render assertions:** several branch-only tests encode absolute coordinates such as 477, 308, 278 and 233. One forbids cubic commands even for rounded quarter-circle corners; another requires a single elbow where the later default permits a Z. These need semantic reconciliation with the supplied rulebook and the pending defaults. They were retained.
- **Diagnostics:** some picture gates classify samples from rounded corners as long diagonals, or treat fragments around label gaps as detached strokes. Actual node intersections and loop-return diagonals are also visible. The report preserves gate failures and distinguishes these concerns instead of treating the failure count as visual acceptance.

## 6. Rendered evidence

The final source produced ten sample SVG/PNG pairs and all active catalogue SVG/PNG pairs. Files are local build artifacts under `out/artifacts/grid-layout/6fc1d41/`: `samples/`, `catalogue/` and `gates/`. Each sample has a picture/live gate log. All ten logs report zero hop arcs. The historical `before/` and `after/` folders were retained as evidence from their original commits.

`out/artifacts/grid-layout/6fc1d41/manifest.txt` lists every rendered SVG, PNG and picture-gate log path relative to the PR worktree. These generated artifacts remain local; the comparison pictures below are committed for review.

Four sequence SVG/PNG pairs in the same artifact root's `sequence/` folder cover `short-note`, `wrapped-note`, `right-note` and `self-message-note`. They were visually inspected after the suite run. Vertical clearance is improved, including the returning self arrow; the [TTL note render](flowchart/layout-fixes/grid-review/sequence-note.png) is committed. The right-of-final-participant note is clipped horizontally by the existing canvas-width calculation; a master render confirms that this already happens on master. Vertical-spacing tests do not establish that the complete sequence output is correct.

Committed comparison pictures show the starting PR #9 render, the final render and supplied reference guidance. Only the webshop reference has the same topology; the other supplied pictures illustrate the relevant spine or frame rules.

- [Webshop comparison](flowchart/layout-fixes/grid-review/03-comparison.png)
- [Order-management spine comparison](flowchart/layout-fixes/grid-review/01-comparison.png)
- [Store Cluster frame comparison](flowchart/layout-fixes/grid-review/11-comparison.png)
- [Board and revenue spine comparison](flowchart/layout-fixes/grid-review/12-comparison.png)

Visual review found the following remaining defects:

| Sample | Findings |
| --- | --- |
| 01 | The spine is clearer, but bypasses pass through intermediate nodes; a long exterior route remains. The label-gap fragments also trigger endpoint diagnostics. |
| 02 | The diagram remains excessively wide, with long cross-diagram connections and inconsistent corner treatment. |
| 03 | Border exits and the HTTPS text gap are clearer. Java VM and Hosting still occupy different columns; the target webshop spine is not achieved, and a deployment line crosses another route. |
| 06 | A loop return remains diagonal; incoming links and long horizontal routes remain crowded. |
| 07 | A bypass crosses No Cash Handling and must be routed around it. |
| 08 | The reciprocal return is still diagonal and its arrow runs along Selling's boundary. |
| 09 | The Warehouse-to-Carton bypass passes through Packing Press. |
| 10 | The spine is clear; the branch needs review against downstream-port preference. |
| 11 | London and the junctions clear the frame, but return routes are excessive and one path enters the title band. |
| 12 | The main chain is straight; the final Hosting connection needs review against the provisional downstream-port choice. |

Gate failure counts are evidence to investigate, not an acceptance score: 01=33, 02=29, 03=23, 06=20, 07=12, 08=8, 09=1, 10=3, 11=24, 12=3. No gate failures were reclassified as tolerated in this pass.

## 7. Decisions before a future merge

The following six supplied defaults still need the owner's approval:

1. Offset next-rank children use downstream-to-upstream Z routes while downstream ports are free; facing-side elbows follow only when those ports are occupied.
2. The 50 px pre-corner straight minimum applies to diamond branches. Other terminals reserve the corner radius at the source and the arrowhead length at the target. Base rank gap is 60 px.
3. Tracks are at least 6 px apart, and exhausted gaps grow in 30 px steps.
4. A rejoin stays on the spine.
5. Tie choices are branches left, returns right, facing diamond vertex before lower edge centre, one “n” width of label whitespace, and 30 px frame padding.
6. Arrangement checks allow the specific reordering in rulebook section 2.

The frame-midline placement and reserved-midline-track rules remain unimplemented pending redefinition. These decisions do not establish visual acceptance: the defects and incomplete sections above must also be resolved or explicitly accepted before a merge.

## 8. Final failure names

### MermaidRecognition.Layout.Tests

- `BuildCorridorRoutePathData_UsesCurveBasisNotLinearPolyline`
- `CatalogueAndEnterprise_FinalSvg_ReportsJustifiedFailuresOnly`
- `KnownBadWebShopEnterprisePicture_ReportsEveryVisibleDefect`
- `Layout_F81_TrapezoidBracketGuidePointsTerminateOnVisibleBoundary`
- `LiveSamples_ClearTheListedPictureDefects`
- `RenderToSvg_Chain_Fixture_ContainsThreeNodesAndTwoEdges`
- `RenderToSvg_F21_CircleMarkerLeadingEdgeMeetsTargetBoundary`
- `RenderToSvg_F21_CrossMarkerLeadingEdgeMeetsTargetBoundary`
- `RenderToSvg_F22_BidirectionalArrowTipsMeetNodeBoundaries`
- `RenderToSvg_F33_SideFanRoutes_UseBoundarySRouteWithoutHookCubics`
- `RenderToSvg_F95_ArrowTipInsetsIntoTargetNodeAndLineMeetsBoundary`
- `RenderToSvg_InspectContent_BranchesUseOrthogonalRunsWithDiamondStub`
- `RenderToSvg_LayeredView_FarChildRouteIsOrthogonal`
- `RenderToSvg_MessageFilter_BranchesEnterLeftSideWithRoundedZ`
- `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules`
- `RenderToSvg_TypeDecision_NoSharedBottomStub`
- `RenderToSvg_ValidDecision_BranchesUseOrthogonalRunsWithDiamondStub`
- `TryLayout_F60_SiblingSubgraphsShareCrossBoundarySpineAndHorizontalPrimaryFlow`
- `TryLayout_InspectContent_BranchesUseSingleElbowOrthogonalPaths`
- `TryResolve_Sample03_CompoundEdgesClipToGroupBoundary`
- `TryResolveLayoutGuidePoints_F124_TwoBranchesUseSlotsOneAndThree`
- `TryResolveLayoutGuidePoints_F125_MultiIncoming_UsesBottomCentreSourcesAndDistinctDiamondPorts`
- `TryResolveLayoutGuidePoints_F32_FanIn_SelectsTopFaceSourcePortsAndBottomCentreTarget`
- `TryResolveLayoutGuidePoints_F33_LrFan_SelectsInboardSidePorts`
- `TryResolveLayoutGuidePoints_F35_MultiIncoming_UsesBottomCentreSourcesAndDistinctDiamondPorts`
- `TryResolveLayoutGuidePoints_F53A_StadiumMerge_UsesCentreSourceSlots`
- `TryResolveLayoutGuidePoints_F53Full_StadiumMerge_EndBelowSources`
- `TryResolveLayoutGuidePoints_F58_DecisionBranches_UseCorrectSourcePorts`
- `TryResolveLayoutGuidePoints_F58_HasMermaidBranches_UseCorrectRouteStyles`
- `TryResolveLayoutGuidePoints_F58_Routes_LeaveFacingShoulderAndClearOtherNodes`
- `TryResolveLayoutGuidePoints_F59_NoBranch_UsesSidePortFacingWaitAndWaitCentreTarget`
- `ValidateTortureFixture_AgainstRuleset_AllDiagramsPassIntentValidation`
- `ValidateTortureFixture_SeparatesIntentAndRenderFailures`

### MermaidRecognition.RuleValidation.Tests

- `FlowMarkdown_AllActiveDiagrams_PassTerminalStubRules`
- `FlowMarkdown_F34_PassesLayoutOverlapAndRoutingValidation`
- `FlowMarkdown_F58_PassesInFullCatalogueRun`
- `FlowMarkdown_F58_PassesRenderedPathAndRoutingValidation`
- `Pipeline_F34_RetryLoopDecisionLabelsClearOfConnector`
- `Pipeline_F53_StadiumMerge_EndEdgesUseShoulderAttachAndVisibleArrows`
- `Pipeline_F58_PassesRenderedPathAndLongBypassRouting`
- `Pipeline_F59_PrimarySubgraphColumnCenteredInWorkerCluster`
- `Pipeline_F60_SiblingSubgraphsAlignedForHorizontalCrossStageRouting`
- `Pipeline_F81_TrapezoidBracketEdgesTerminateAtVisibleBoundary`

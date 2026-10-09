# Grid layout review — 9 October 2026

This review records the final code at `da36d97` on PR #9, branch `cursor/many-to-many-routing-3834`. It is a measured improvement, but the visual acceptance work and the rulebook implementation are incomplete. Master remains at `f09cb89`. No merge or rebase was performed.

## 1. Fresh test comparison

Every suite was run once on master and once on the starting branch, using plain `dotnet test` with build and restore. Focused tests covered each subsequent fix. An initial full verification at `6fc1d41` was followed by the required picture review, which led to two additional fixes. All five suites were run again on the final pushed code at `da36d97`; master was not rerun. Failure names were compared as well as counts.

| Suite | Master passed / failed | Starting branch passed / failed | Final passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 293 / 33 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 932 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 39 / 0 |

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

Final logs, TRX files and failing-name comparisons are in `out/grid-final-da36d97/`. The earlier `6fc1d41` verification is retained in `out/grid-final/`. Starting-branch logs are in `out/grid-baseline/`; master logs are in the sibling `MarkdownToDocX-master-baseline/out/grid-baseline/` worktree. Focused logs and TRX files are in `out/grid-fixes/`.

## 2. Changes pushed to PR #9

| Commit | Subject |
| --- | --- |
| `f41081d` | Keep connector ends square to their assigned ports |
| `0791d59` | Preserve right angles when separating connector tracks |
| `bf140a7` | Centre edge labels in the break in their connector |
| `4f2e7fb` | Use a free facing vertex for decision branches |
| `6103ae9` | Leave enough rank space for rounded connector turns |
| `9d046b8` | Map validation coordinates from the drawn grid routes |
| `f10c878` | Match arrowhead checks to their tagged connector strokes |
| `3b2b21c` | Keep connector turns outside enclosing frame borders |
| `81fbc4a` | Reserve centre ports for straight spine connections |
| `6ec966a` | Render grid crossings without hop arcs |
| `6fc1d41` | Give sequence notes the same clearance as message rows |
| `3dabccb` | Record the grid improvements and remaining defects |
| `e07570e` | Keep legacy return guides on orthogonal grid runs |
| `da36d97` | Keep sequence notes inside the diagram canvas |

Eight new Layout cases cover terminal direction, obstacle rejection during track translation, nested-frame exits, centre-slot preservation and reciprocal-return geometry in all four directions. Two new rule-validation cases verify stroke association and rejection of a misaligned arrow. Seven added converter cases cover note placement, wrapping, a note after a self message and canvas containment on both sides and over two participants. Existing tests, thresholds and fixtures were retained. The earlier authorised curve-test rewrites remain on the branch.

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
| 1: grid and gaps | Partial. Shared sizes and larger rank gaps exist. Some placement adjustments still move individual nodes or columns without enforcing a single 30 px lattice. Label growth can still enlarge all rank gaps instead of only the gap concerned. Standard-box measurement rounds to the nearest grid unit rather than always up, so text fit also needs an audit. |
| 2: placement | Partial. Spine and rejoin code exists. Many-to-many ordering, crossing-minimising swaps, nested-chain alignment and the target webshop spine are not fully verified. |
| 3: ports | Improved. Centre-slot retention and free diamond vertices are covered. Some routes retain legacy side choices; complete downstream-port preference and outline attachment across every shape remain open. |
| 4: line shape | Improved. General grid routes have normal terminals, rounded corners and no hop pass. Diagonal back-edge guides are now reconstructed when an obstacle-free route fits the allocated ports and three-corner limit, without leaving a shared frame. Unreconstructable legacy paths remain; some other routes exceed corner limits or use reduced-radius corners. |
| 5: routing | Partial. Track moves preserve orthogonality. Complete port/track crossing optimisation, local gap growth and a truthful failure result instead of the unchecked last-resort route remain open. |
| 6: labels | Partial. Longest fitting run selection and actual stroke breaks share an anchor, with corner/arrow clearance. Local gap growth when no run fits, collision handling and an explicit white background on non-white themes remain open. |
| 7: frames | Partial. Title padding and containment tests pass for the reported cases, and nested exits are square to borders. Frame fitting still contains individual-node eviction; complete sibling-frame clearance and internal return-track containment remain open. The midline rule remains on hold. |
| 8: sequence | Shared row extents now provide equal conservative clearance above/below notes and between consecutive messages. Short, wrapped, left/right/over and post-self-message note cases pass. The canvas now grows to include notes beyond either outer participant, using the same horizontal placement calculation as drawing. |

The supplied files in the original laptop folder's `docs/Instructions/` and `docs/briefs/` were read and left untouched. Their top “Decisions applied” block takes precedence over older wording beneath it.

## 5. Bounded investigations and unresolved conflicts

- **Tangential terminals:** checking endpoint alignment alone was insufficient. Normal-direction checks and explicit terminal stubs removed the original corner problem. The live-sample test then exposed a separate bypass/outline problem; the complete test is not reported as fixed.
- **Frame crossings:** terminal normals, whole-run cleanup and ancestor-frame exit clearance were checked in successive focused runs. A later centre-port change exposed a 14 px frame-clearance problem; reserving a full grid unit beyond the border restored the group-border test. The nested-frame regression test records the required geometry.
- **HTTPS stroke distance:** label anchoring, preservation of the adjoining corners and coordinate mapping were checked. The remaining 31.6403 px result is consistent with the visible gap around the label. Further variations on this assertion stopped; font sizes, whitespace requirements and the 28 px threshold were retained.
- **Branch render assertions:** several branch-only tests encode absolute coordinates such as 477, 308, 278 and 233. One forbids cubic commands even for rounded quarter-circle corners; another requires a single elbow where the later default permits a Z. These need semantic reconciliation with the supplied rulebook and the pending defaults. They were retained.
- **Diagnostics:** some picture gates classify samples from rounded corners as long diagonals, or treat fragments around label gaps as detached strokes. Actual node intersections are also visible, independently of those diagnostic limitations. The report preserves gate failures and distinguishes these concerns instead of treating the failure count as visual acceptance.
- **Remaining loop routes:** the picture review exposed diagonal guides that bypassed ordinary grid reconstruction. A checked reconstruction fixed the visible returns in samples 06 and 08; existing orthogonal paths are retained. This does not solve downstream-source preference, shared return-track allocation, or legacy paths for which no valid route fits the current placement and ports.

## 6. Rendered evidence

The final pushed source at `da36d97` produced ten sample SVG/PNG pairs and all active catalogue SVG/PNG pairs. Files are local build artifacts under `out/artifacts/grid-layout/da36d97/`: `samples/`, `catalogue/` and `gates/`. Each sample has a picture/live gate log. All ten logs report zero hop arcs. The historical `before/` and `after/` folders and the earlier `6fc1d41` artifacts were retained as evidence from their original commits.

`out/artifacts/grid-layout/da36d97/manifest.txt` lists every rendered SVG, PNG and picture-gate log path relative to the PR worktree. `index.html` in that folder provides a local sample/sequence gallery and catalogue links. These generated artifacts remain local; the comparison pictures below are committed for review.

Five sequence SVG/PNG pairs in the same artifact root's `sequence/` folder cover `short-note`, `wrapped-note`, `right-note`, `left-note` and `self-message-note`. They were visually inspected. Vertical clearance is improved, including the returning self arrow; the [TTL note render](flowchart/layout-fixes/grid-review/sequence-note.png) is committed. The picture review also exposed clipping beyond the final participant, confirmed on master. Canvas measurement now fixes it on both sides; the final [right note](flowchart/layout-fixes/grid-review/sequence-right-note.png) and [left note](flowchart/layout-fixes/grid-review/sequence-left-note.png) show complete note boxes.

Committed comparison pictures show the starting PR #9 render, the `6fc1d41` checkpoint render and supplied reference guidance. Final PNGs for those four samples are byte-identical to the checkpoint PNGs. Only the webshop reference has the same topology; the other supplied pictures illustrate the relevant spine or frame rules. Separate final renders show the newly corrected [order-taking return](flowchart/layout-fixes/grid-review/06-final.png) and [Selling return](flowchart/layout-fixes/grid-review/08-final.png).

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
| 06 | The visible loop return is now orthogonal. Incoming links and long horizontal routes remain crowded. |
| 07 | A bypass crosses No Cash Handling and must be routed around it. |
| 08 | The reciprocal return is now orthogonal and its arrow meets Selling square-on. Source-side selection still needs review against downstream-port preference. |
| 09 | The Warehouse-to-Carton bypass passes through Packing Press. |
| 10 | The spine is clear; the branch needs review against downstream-port preference. |
| 11 | London and the junctions clear the frame, but return routes are excessive and one path enters the title band. |
| 12 | The main chain is straight; the final Hosting connection needs review against the provisional downstream-port choice. |

Gate failure counts are evidence to investigate, not an acceptance score: 01=33, 02=29, 03=23, 06=17, 07=12, 08=5, 09=1, 10=3, 11=24, 12=3. No gate failures were reclassified as tolerated in this pass.

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

## 9. Side-exit corner regression

Commit `d214652` fixes the sharp bends reported in F24 of the regenerated DOCX. A side-exit grid route used the legacy linear corridor renderer whenever its label could not fit a stroke gap. Grid routes now use the shared rounded-corner builder in that branch too. This is a generic route-kind correction; no engine check depends on an example, identifier or label.

The owner explicitly authorised updating the existing test that prohibited rounded corners. Its replacement checks rounded bends, axis-aligned runs, the actual target's top-centre attachment and a straight terminal approach long enough for the arrowhead. Twelve additional rendering cases cover four flow directions with absent, short and longer labels. All sixteen focused cases passed.

All five suites were rerun with build and restore on the source committed as `d214652`:

| Suite | Master passed / failed | Starting branch passed / failed | Latest passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 306 / 32 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 932 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 39 / 0 |

There are no newly failing names relative to `da36d97`. The failure lists in section 8 remain current except that `RenderToSvg_LayeredView_FarChildRouteIsOrthogonal` now passes. The two regressions against master identified earlier remain unresolved.

Logs are in `out/grid-corner-fix/*.log`; TRX results are in `out/grid-corner-fix/final/`. The catalogue SVG/PNG pairs are in `out/grid-corner-fix/catalogue/`, the ten sample pairs in `out/grid-corner-fix/samples/`, and the focused F24 pair in `out/grid-corner-fix/render/`. `out/grid-corner-fix/manifest.txt` lists the generated paths.

The rebuilt `md2docx.exe` was called directly with the original folder's `flow.md` and a temporary output filename. F24's actual embedded DOCX image was extracted and visually checked before replacing the original folder's `flow.docx`. The checked image is retained as [F24 with rounded corners](flowchart/layout-fixes/grid-review/F24-rounded.png). Label overlap is still visible where the label-gap calculation cannot fit a break; the corner fix does not resolve that separate defect. The refreshed catalogue and samples were rendered but have not all received a new visual acceptance review. Merge approval remains outstanding.

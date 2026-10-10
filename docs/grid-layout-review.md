# Grid layout review — 9 October 2026

This review records the initial verification at `da36d97` and the subsequent picture-driven corrections in sections 9–14 and the approved pipeline expectation in section 15 on PR #9, branch `cursor/many-to-many-routing-3834`. The latest behavioural code is `591adcf`; section 16 records the live-picture regression investigation. It is a measured improvement, but the visual acceptance work and the rulebook implementation are incomplete. Master remains at `f09cb89`. No merge or rebase was performed.

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

## 10. Label spacing along the connector

Commit `3570465` corrects the excessive breaks around F25's labels. The label-gap helper had used text width for both horizontal and vertical connectors. It now measures the text along the connector: width horizontally, rendered height vertically, with the existing character-width clearance on each side. Run selection, grid rank spacing and the older layout postprocessor share this calculation. No engine condition uses a diagram name, identifier or label; the diff was checked before committing.

F25's two stroke breaks fell from 147.456 px and 153.6 px to 24.288 px each. The clear rank gaps fell from 180 px to 120 px. This also lets F24's vertical runs hold their labels without drawing through the text. Both actual embedded images were extracted from the regenerated DOCX and visually checked: [F25 label gaps](flowchart/layout-fixes/grid-review/F25-label-gap.png) and [F24 label gaps](flowchart/layout-fixes/grid-review/F24-label-gap.png). The F24 label-overlap finding in section 9 describes the earlier output and is now resolved.

The owner authorised correcting the existing label-gap checks to use the appropriate text dimension. Those checks now verify that the stroke resumes on both sides of the label. Ten additional cases cover single-line and multiline breaks in all four directions and confirm that wider single-line text does not inflate vertical rank gaps. All thirty focused checks passed, including the earlier corner checks.

All five suites were rerun with build and restore on the source committed as `3570465`:

| Suite | Master passed / failed | Starting branch passed / failed | Latest passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 316 / 32 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 932 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 39 / 0 |

The failing names are unchanged from `d214652`; section 8's lists still apply except for the corrected side-exit test noted in section 9. The two pre-existing regressions against master remain unresolved. Logs, current failure-name lists and focused results are in `out/grid-label-size/`; full TRX results are in `out/grid-label-size/final/`. Refreshed catalogue SVG/PNG pairs are in `out/grid-label-size/catalogue/`, the ten sample pairs in `out/grid-label-size/samples/`, and the focused F25 pair in `out/grid-label-size/render/`. `out/grid-label-size/manifest.txt` lists the generated paths. This follow-up visually checked F24 and F25; it does not constitute visual acceptance of the whole catalogue.

The rebuilt executable was called directly to convert the original folder's `flow.md`. After the embedded-image checks, the new DOCX replaced that folder's `flow.docx`. The earlier pending defaults and frame-midline decision still require approval before any merge.

## 11. Terminal decision outcomes

Commit `74fa83b` implements the owner's F29 markup: Accept moves underneath the Yes branch, so that branch has one rounded elbow and enters the top of its target. Two independent terminal children of a diamond, in the same rank and group, now occupy opposite sides of that diamond in their original cross-axis order. The placement leaves room for the diamond's outward run. Decisions with continuing or shared children retain their other placement rules. The diff was checked for diagram-specific engine conditions; none were added.

Four new geometry checks pass for TB, BT, LR and RL. They verify opposite-side placement, facing diamond vertices, one elbow and normal entry at each target's upstream face. The rebuilt executable converted the original folder's `flow.md` directly; the actual F29 image was extracted and visually checked before the DOCX replaced the original `flow.docx`. The checked image is retained as [F29 terminal targets](flowchart/layout-fixes/grid-review/F29-terminal-targets.png).

All five suites were rerun with build and restore on the source committed as `74fa83b`:

| Suite | Master passed / failed | Starting branch passed / failed | Latest passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 320 / 32 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 932 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 39 / 0 |

The totals are unchanged from the preceding correction, but one failing name changed. `TryLayout_InspectContent_BranchesUseSingleElbowOrthogonalPaths` now passes. `ResolveEdgeLabelPosition_F29_DecisionBranch_LabelHorizontallyCentredOnConnector` now fails: its older helper chooses a horizontal-run midpoint while the test also requires that point to equal the midpoint of the entire polyline. Those points differ after the geometry is corrected. The rendered label follows the longest straight run, as the supplied rulebook requires. At this checkpoint the existing test was left untouched and a request to update it was pending; section 12 records the subsequent approval and passing replacement. It passed on master, so the report counts it explicitly alongside the two earlier regressions; the unchanged total must not hide it. No new failures appeared in the other suites.

Section 8's Layout failure list applies with the resolved elbow check removed, the earlier side-exit check removed as recorded in section 9, and the newly failing F29 label-position check added. Rule-validation failures are unchanged. Logs, current failure-name lists and focused results are in `out/grid-terminal-decision/`; full TRX results are in `out/grid-terminal-decision/final/`. Refreshed catalogue SVG/PNG pairs are in `out/grid-terminal-decision/catalogue/`, the ten sample pairs in `out/grid-terminal-decision/samples/`, and the focused F29 pair in `out/grid-terminal-decision/render/`. `out/grid-terminal-decision/manifest.txt` lists the generated paths. Only F29 received a new visual review in this correction.

At this checkpoint the fix was pushed and the conflicting assertion still needed a decision; that assertion is now resolved in section 12. The six provisional defaults and frame-midline rule also remain pending; nothing was merged.

The subsequent F30 markup showed excessive breaks around Get money, One, Two and Three. The general label-dimension correction from section 10 already resolves all four in the current DOCX: each break is 24.288 px, and the connector resumes on both sides. The actual embedded image was extracted from `out/grid-terminal-decision/flow-terminals.docx` and visually checked; it is retained as [F30 label gaps](flowchart/layout-fixes/grid-review/F30-label-gaps.png). This verification required no further code changes; the measured source remains `74fa83b`.

## 12. Fan, merge-input and nested-frame placement

This follow-up implements the F31, F34, F35 and F46 markup and the authorised F29 label-check correction. The changes are generic structural and geometric rules. The engine diffs were checked for diagram-specific identifiers, labels, filenames and fixture conditions; none were introduced.

| Commit | Subject |
| --- | --- |
| `dcca8dd` | Check decision labels against their fitting straight run |
| `d4af9cf` | Centre ordinary terminal fans on their middle child |
| `085c762` | Align an unbranched input with its decision |
| `b41bb54` | Centre nested groups across the direction of flow |
| `c05455a` | Keep independent merge inputs close and use distinct ports |
| `d0436e0` | Check the middle child for a terminal fan continuation |

F31 now centres Hub over Worker 2, with a straight centre connection and matching rounded outer branches. The same placement applies to odd fans of independent terminal children in one rank and group, including an unbranched ordinary input chain. The owner's middle-child markup replaces the first-created-child assumption in the directly conflicting fan checks; the straight-route, attachment and distinct-port assertions remain. Six new cases cover three- and five-child fans and all four flow directions.

F34 now places Receive level with its decision and uses a straight input into the upstream vertex. A return edge into an ordinary input no longer pulls it off that row or column. Four new retry-loop cases check alignment, straight input and the diamond attachment in all four directions. This correction does not establish acceptance of every retry-loop rule; the existing F34 rule-validation failures remain.

F35's independent input nodes now occupy adjacent columns around Merge. Their spacing fell from 400 px to 180 px in the catalogue render. Its centre incoming edge uses the upstream diamond vertex; the outer edges use the side vertices. Outgoing branches respect those actual allocated incoming ports and use the downstream slanted-edge centres when the side vertices are occupied. Continue already benefits from the opposite-side terminal placement introduced in section 11. Four placement cases and two distinct-port cases cover the new rules.

The older F35 guide test still fails, as it did before this pass. Three stages were checked: compacting the inputs; respecting actual incoming endpoints when allocating outgoing ports; and assigning the three co-ranked incoming vertices together. These stages corrected the rendered geometry, but the older guide test still requests route kind `Decision` while the shared legacy resolver returns `Corridor`. It was left unchanged after the third attempt. The final suite records its failure; no further variations were attempted.

F46's nested-group centring had measured X bounds and applied the resulting distance to Y during horizontal flow. It now measures and moves on the same cross axis. All five chain nodes share one row and each connection crosses the inner frame straight. Four new directional cases verify the chain and frame padding.

The approved F29 replacement checks the actual drawn route independently for its longest fitting run, requires the label at that run's midpoint, and verifies character-width clearance on both sides of the stroke break. All eleven F29-focused checks pass. The temporary failing fan assertion found during the full run was corrected to select the middle child required by the markup, retaining its original straight-route and distinct-port checks.

All five suites were run with build and restore on the final behavioural source at `c05455a`. Layout was run again after the directly conflicting centre-child assertion was corrected and pushed as `d0436e0`; no application source changed between those two commits. Master was not rerun.

| Suite | Master passed / failed | Starting branch passed / failed | Current passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 341 / 31 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 932 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 39 / 0 |

There are no newly failing names against the preceding `74fa83b` source; the authorised F29 check is now resolved. Six current Layout failures are absent from master's failing list:

- `LiveSamples_ClearTheListedPictureDefects` — existing regression, still unresolved.
- `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules` — existing regression, still unresolved; its earlier three-attempt investigation remains recorded in section 5.
- `RenderToSvg_InspectContent_BranchesUseOrthogonalRunsWithDiamondStub` — test added on the grid branch.
- `RenderToSvg_MessageFilter_BranchesEnterLeftSideWithRoundedZ` — test added on the grid branch.
- `RenderToSvg_TypeDecision_NoSharedBottomStub` — test added on the grid branch.
- `RenderToSvg_ValidDecision_BranchesUseOrthogonalRunsWithDiamondStub` — test added on the grid branch.

Rule-validation failing names are unchanged and none are additional to master's list. Full logs, TRX files and current failure-name comparisons are under `out/grid-picture-placement/final/`. Section 8's Layout list remains applicable after removing the two resolved checks recorded in sections 9 and 11. The F29 check added in section 11 also now passes.

The rebuilt `md2docx.exe` was called directly with the original folder's `flow.md` and `out/grid-picture-placement/flow-placement.docx`. Its actual embedded images were extracted and visually checked at F24, F25, F29, F31, F34, F35 and F46. The checked output replaced the original folder's `flow.docx`. The copy completed and the destination size matches; an additional destination hash check was blocked by the open document's file lock. The new retained comparison images are [F31](flowchart/layout-fixes/grid-review/F31-centred-hub.png), [F34](flowchart/layout-fixes/grid-review/F34-aligned-input.png), [F35](flowchart/layout-fixes/grid-review/F35-compact-merge.png) and [F46](flowchart/layout-fixes/grid-review/F46-aligned-chain.png).

The active catalogue and ten samples were also regenerated as SVG/PNG pairs under `out/grid-picture-placement/catalogue/` and `out/grid-picture-placement/samples/`. The manifest at `out/grid-picture-placement/manifest.txt` lists every generated picture path. These broader renders do not constitute a new visual acceptance review of every diagram. No merge was performed; the remaining defects, six provisional defaults and on-hold frame-midline decision remain outstanding.

## 13. Square attachments on sloping diamond edges

Commit `0ce271a` — **Meet sloping diamond edges along their surface normals** — corrects the F35 outgoing attachments identified after the placement review. The previous picture left the lower diamond slopes horizontally. Those runs were not perpendicular to the actual outline.

The owner's latest instruction requires every line to meet the node edge at a right angle. At a sloping edge, that requires a diagonal terminal run, so this instruction supersedes rulebook 4.1's prohibition on diagonals and rule 3.8's horizontal exit from a sloping-edge centre at the attachment. The supplied instruction files remain unchanged. Diamond vertices keep their existing axis/bisector convention.

The grid route now replaces an eligible axis-aligned terminal with a surface-normal run and joins it to the adjoining track. The rule applies geometrically to sloping diamond source and target attachments; it contains no diagram-specific identifiers or labels. Circular fillets use the actual turn angle for these joins while existing 90-degree corners retain their previous geometry. Label placement continues to choose a fitting straight run. Existing allocation, placement and node sizes are preserved.

Eight new cases check both source and target attachments across all four diamond quadrants, using a non-square diamond. They verify the actual outline normal, outward straight-run length, the retained opposite endpoint and horizontal/vertical body segments. The merge check now requires the sloping terminal to follow that normal; only its directly conflicting assertion that every segment must be horizontal/vertical was changed under the owner's new instruction. All 27 focused cases passed.

All five suites were run once with build and restore on the pushed application source at `0ce271a`:

| Suite | Master passed / failed | Starting branch passed / failed | Current passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 349 / 31 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 932 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 39 / 0 |

Failing names are unchanged from section 12. The existing master-passing regressions remain `LiveSamples_ClearTheListedPictureDefects` and `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules`; section 12 lists the four additional grid-only failures absent from master. No existing failing test was weakened or removed to improve these totals. This correction does not resolve the broader routing failures or establish square attachment for every remaining defective diagram.

Full logs, TRX files and failing-name lists are under `out/grid-surface-normals/final/`. The active catalogue and ten samples were regenerated into `out/grid-surface-normals/catalogue/` and `out/grid-surface-normals/samples/`; `out/grid-surface-normals/manifest.txt` lists every generated picture path. This pass visually reviewed F35, rather than accepting the whole catalogue.

The rebuilt executable converted the original folder's `flow.md` directly to `out/grid-surface-normals/flow-normals.docx`. Its actual embedded F35 picture was extracted and checked, then the output replaced the original folder's `flow.docx` after the owner closed Word. Source and destination DOCX hashes match. The checked [F35 square attachments](flowchart/layout-fixes/grid-review/F35-square-attachments.png) supersede the outgoing attachment geometry in section 12's earlier picture. Extracted F24, F25, F29, F31, F34 and F46 images are byte-identical to the preceding checked output. README and current status describe the terminal exception. The generic engine diff was checked for diagram-specific conditions before committing. No merge was performed; the previous unresolved decisions remain outstanding.

## 14. Retry outcomes, detours, group clearance and crossings

The subsequent marked pictures define the following generic situations:

- A horizontal retry decision keeps its terminal outcome straight ahead and places its returning sibling below. The return leaves the sibling's bottom and enters the input from below. The declared LR/RL direction is preserved.
- Independent binary outcomes feeding one terminal collector straddle their decision. The collector continues below one branch and receives the other through its facing side.
- A local detour sits beside the continuation it rejoins. Direct continuations use upstream centre ports; detours use a single elbow into the rejoin's side. A final optional detour and a collector with an earlier exit occupy opposite sides of their decision, with the earlier exit beside the collector in the outer column. Simple two-input rejoins remain on the spine.
- A vertical loop branch and its continuing sibling straddle the decision, with the continuing chain under its child. Shared frames contain both branches. A free facing diamond vertex reaches its child with one clear elbow; shared containing frames are not treated as solid node obstacles.
- Sequential sibling frames leave room for rounded bends and straight border crossings. Parallel frames retain a grid step of clearance. Unbranched external ends move with their group.
- Paired ranks with two parents sharing two children align one child under each parent. Direct links use centre ports. Crossed links use separate normal ports and staggered orthogonal tracks, with their crossing outside rounded corners.
- Ordinary hubs with more than three widely spread branches use their facing top/bottom or left/right sides, and large collectors receive those branches through their facing cross-axis sides.
- Mermaid fences containing only comments and blank lines remain code blocks without render diagnostics. Active invalid content still reports its rendering failure.

The rules inspect edges, degree, rank, group membership, free vertices and coordinates. The engine diff was checked for conditions on catalogue numbers, node IDs, node text and diagram names; none were introduced. New regression fixtures use structural names and cover mirrored directions. The five directly conflicting retry expectations were updated with explicit owner approval, preserving spacing, vertex exits, rounded returns and square endpoints. The existing pipeline-column expectation has not been changed: approval is pending because it requires the terminal on the central spine and the early exit above it, whereas the marked arrangement places the terminal below-left and the early exit beside it.

Pushed commits in this pass:

| Commit | Subject |
| --- | --- |
| `92c6b4a` | Place horizontal retry outcomes on their natural axes |
| `7b0883f` | Keep independent decision branches beside their shared exit |
| `a52e41e` | Match retry layout checks to the approved arrangement |
| `9ce2972` | Keep detours beside the continuation they rejoin |
| `cd7eb71` | Keep grouped loop branches on opposite sides of their decision |
| `da003ea` | Preserve fully commented Mermaid blocks as source text |
| `4ba7af7` | Use facing sides for widely spread parallel branches |
| `503b036` | Leave clearance between sibling group frames |
| `7291be3` | Align paired ranks and separate their crossing tracks |
| `65deb18` | Keep simple detour rejoins on the spine |

All five suites ran on the final pushed application source, `65deb18`, with build and restore. Concurrent project builds initially collided on shared DLL outputs; those attempts did not execute tests. The affected suites were run sequentially. The later scope correction required a final verification on the corrected source. Master was not rerun.

| Suite | Master passed / failed | Starting branch passed / failed | Current passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 370 / 32 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 932 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 42 / 0 |

The only additional failure relative to section 13 is the pending pipeline-column check. Together with the two existing regressions `LiveSamples_ClearTheListedPictureDefects` and `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules`, it makes three master-passing regressions. Rule-validation failing names are unchanged. Logs, TRX files, failure lists and master regression lists are under `out/grid-retry-arrangement/verified/`. Earlier diagnostic runs are retained separately in `final/` and `completed/`; they are not the final comparison. The previously exhausted three-attempt investigations in section 5 remain unchanged.

The rebuilt executable directly converted the original folder's `flow.md` to `out/grid-retry-arrangement/flow-corrected.docx`. The actual embedded pictures were extracted and inspected, then the checked output replaced the original folder's `flow.docx`. Both files have SHA256 `B35DB296E39EF809EF6F75E17D72FB2808BAD7DFB7D3B56CD6920D6C81F63C49`. The older F24, F25, F29, F31, F35 and F46 embedded pictures are byte-identical to section 13's checked output. The fully commented F65 and F70 fences contain preserved source without the old warning.

Checked embedded picture copies:

- [Horizontal retry](flowchart/layout-fixes/grid-review/F34-current.png)
- [Independent outcomes and shared exit](flowchart/layout-fixes/grid-review/F53-current.png)
- [Pipeline detours and early exit](flowchart/layout-fixes/grid-review/F58-current.png)
- [Grouped loop branches](flowchart/layout-fixes/grid-review/F59-current.png)
- [Sequential group crossings](flowchart/layout-fixes/grid-review/F60-current.png)
- [Paired rank crossings](flowchart/layout-fixes/grid-review/F61-current.png)
- [Parallel group clearance](flowchart/layout-fixes/grid-review/F73-current.png)
- [Wide parallel fan](flowchart/layout-fixes/grid-review/F113-current.png)

The active catalogue has 126 rendered diagrams under `out/grid-retry-arrangement/catalogue/`; ten sample pairs are under `out/grid-retry-arrangement/samples/`. `out/grid-retry-arrangement/manifest.txt` lists every SVG and PNG path. This pass visually inspected the eight changed embedded pictures; regeneration of the rest is not acceptance of every picture. The PR branch's catalogue prose now describes the graph-based grid rules instead of obsolete curve-basis requirements. The original worktree's Markdown and the supplied instruction files remain untouched.

No merge was performed. The six defaults, frame-midline decision, broader unresolved acceptance failures and the pending pipeline expectation still require resolution before merge approval.

## 15. Approved pipeline expectation — 10 October 2026

Commit `b31b498` updates the existing pipeline expectation following explicit approval. The check now describes its structural situation in its name. The final collector and optional detour occupy opposite sides of their decision, and the early exit sits beside the collector with at least one grid step of clearance. The other spine alignment, detour separation and vertical clearance assertions remain. No application source or rendering rule changed; the engine still uses the generic topology and geometry described in section 14.

All five suites ran sequentially with build and restore on pushed commit `b31b498`. Master was not rerun.

| Suite | Master passed / failed | Starting branch passed / failed | Current passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 371 / 31 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 932 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 42 / 0 |

The two remaining master-passing regressions are `LiveSamples_ClearTheListedPictureDefects` and `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules`. The four pre-existing grid-only failing names absent from master remain unchanged. Rule validation has no regression against master. Logs, TRX files, complete failure names and comparisons are under `out/approved-pipeline/`, with one `<Suite>.log`, `<Suite>.trx`, `<Suite>.failures.txt`, `<Suite>.regressions.txt` and `<Suite>.new-failures.txt` per suite. Counts above are the ordinary test runner results.

The application source is unchanged from `65deb18`, so section 14's inspected DOCX and picture artifacts remain the applicable rendering evidence. This test-only update did not regenerate or replace the document. The supplied instructions and untracked files were left untouched.

This approval resolves the pending pipeline expectation in section 14. It does not approve a merge, the six provisional defaults or the on-hold frame-midline rule. The broader acceptance failures and visual defects remain outstanding.

## 16. Live-picture regression investigation — 10 October 2026

`LiveSamples_ClearTheListedPictureDefects` now passes. This is the combined enterprise-sample check for 01, 03 and 06, rather than a test of an F-numbered catalogue entry. No existing assertion, threshold or fixture was changed.

The bounded investigation proceeded as follows:

1. The SVG parser kept only the longest visible fragment of a connector broken around its label. The outline check consequently reported a false 438 px target gap. Retaining all fragments lets that check inspect actual terminal endpoints without inventing a stroke across the text break. New structural cases verify both a valid split connector and a genuinely missing terminal. Sample 01 then cleared the listed forbidden kinds. Its other reported intersections remain unresolved.
2. Sample 03 then exposed a real top-centre group attachment beside its nested frame title. Group port allocation now reserves the measured title band and allocates distinct positions outside it before rebuilding routes. Two new structural cases cover attachments to differently sized titles inside a containing group. The combined live-picture check then passed. This does not implement general obstacle routing around an ancestor title when an edge crosses that ancestor on its way to a deeper target.
3. Picture inspection found a small sideways step where a moved group port almost aligned with its source. The allocation now prefers the source column when it is inside the free part of the frame. The final picture retains a straight approach and the combined check passes.

The HTTPS stroke-distance investigation had already exhausted three different attempts in section 5. No further variations were attempted. Final verification still reports **31.640302400577646 px against the unchanged 28 px limit** in `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules`. Its font, label whitespace and assertions remain unchanged. This is now the only failing name that passes on master.

| Commit | Subject |
| --- | --- |
| `58c6a76` | Check connector outlines across label break fragments |
| `1413f9f` | Keep group attachments clear of their titles |
| `591adcf` | Preserve straight approaches to clear group ports |

All five suites ran sequentially with build and restore on pushed commit `591adcf`. An earlier verification on `1413f9f` was superseded after picture inspection exposed the small approach step. Master was not rerun.

| Suite | Master passed / failed | Starting branch passed / failed | Current passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 376 / 30 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 932 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 42 / 0 |

There are no new failing names against the preceding checkpoint, and the four pre-existing grid-only failures absent from master remain. Logs, TRX files and complete failure/regression lists are under `out/live-picture-investigation/verified/`. Earlier `final/` logs describe the pre-refinement checkpoint, not the final source.

The rebuilt rendering executable produced ten sample SVG/PNG pairs under `out/live-picture-investigation/samples/` and 126 catalogue pairs under `out/live-picture-investigation/catalogue/`. `out/live-picture-investigation/manifest.txt` lists every absolute artifact path. The [final sample 03 picture](flowchart/layout-fixes/grid-review/03-title-ports.png) was visually inspected: the title is clear and the source approach is straight. Sample 01 and 06 PNGs, and all catalogue PNGs, are byte-identical to section 14's output. Their broader unresolved visual defects remain; passing the listed-kind check is not acceptance of the whole diagram.

The engine diff was checked for diagram selectors: the rules use frame geometry, measured title width, source position and occupied slots. No sample number, label value or particular node ID selects behaviour. The supplied instructions, briefs and editor settings remain untouched. No merge was performed.

The requested regeneration of the original folder's `enterprise.md` used the rebuilt WinForms executable directly, with the input filename and a short lock timeout. It successfully replaced `enterprise.docx`, but DOCX inspection found zero embedded media files and 130 render warnings saying `Unknown diagram type in: enterprise`. The current PR #9 converter does not support that diagram kind; the separate enterprise renderer work from PR #1 has not been integrated. This regeneration is evidence of the current limitation, not a successful diagram render. The original Markdown was unchanged.

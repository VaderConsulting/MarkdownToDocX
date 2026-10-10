# Grid layout review — 9 October 2026

This review records the initial verification at `da36d97` and the subsequent picture-driven corrections in sections 9–14 and the approved pipeline expectation in section 15 on PR #9, branch `cursor/many-to-many-routing-3834`. The latest behavioural code is `db30bd7`; section 31 records nested return corridors and short-run caption placement. Section 30 records rounded behaviour box ports and equivalent source. Section 29 records visible internal parent-child relationships. Section 28 records facing container ports and visible compound attachments. Section 27 records quoted comments and entity decoding. Section 26 records explicit label breaks. Section 25 records title canvas sizing. Section 24 records relationship caption staggering. Section 23 records aligned vertical return ports. Section 22 records shared input fan placement and continuation alignment. Section 21 records junction caption clearance. Section 20 records plain relationship label measurement and elliptical attachments. Section 19 records the generic forward-dogleg correction. Section 18 records approved decisions, native Enterprise/pie integration, local spacing and frame fixes. Sections 16–17 retain the earlier routing evidence. It is a measured improvement, but the visual acceptance work and the rulebook implementation are incomplete. Master remains at `f09cb89`. No merge or rebase was performed.

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

Five sequence SVG/PNG pairs in the same artifact root's `sequence/` folder cover `short-note`, `wrapped-note`, `right-note`, `left-note` and `self-message-note`. They were visually inspected. Vertical clearance is improved, including the returning self arrow; the TTL note render is committed. The picture review also exposed clipping beyond the final participant, confirmed on master. Canvas measurement now fixes it on both sides; the final right note and left note show complete note boxes.

Committed comparison pictures show the starting PR #9 render, the `6fc1d41` checkpoint render and supplied reference guidance. Final PNGs for those four samples are byte-identical to the checkpoint PNGs. Only the webshop reference has the same topology; the other supplied pictures illustrate the relevant spine or frame rules. Separate final renders show the newly corrected order-taking return and Selling return.

- Webshop comparison
- Order-management spine comparison
- Store Cluster frame comparison
- Board and revenue spine comparison

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

The owner approved all six supplied defaults on 10 October 2026:

1. Offset next-rank children use downstream-to-upstream Z routes while downstream ports are free; facing-side elbows follow only when those ports are occupied.
2. The 50 px pre-corner straight minimum applies to diamond branches. Other terminals reserve the corner radius at the source and the arrowhead length at the target. Base rank gap is 60 px.
3. Tracks are at least 6 px apart, and exhausted gaps grow in 30 px steps.
4. A rejoin stays on the spine.
5. Tie choices are branches left, returns right, facing diamond vertex before lower edge centre, one “n” width of label whitespace, and 30 px frame padding.
6. Arrangement checks allow the specific reordering in rulebook section 2.

The owner approved replacing the held frame-midline rules: fit frames around their members and title on the shared grid, grow shared gaps to preserve padding and routing clearance, and treat frame edges as obstacles without reserving the centre track. The maintained [approved decisions](layout-engine/APPROVED-GRID-DECISIONS.md) record this replacement. Approval does not establish visual acceptance or authorise a merge. Earlier sections record historical checkpoints; section 18 supersedes their pending-decision status.

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

The rebuilt `md2docx.exe` was called directly with the original folder's `flow.md` and a temporary output filename. F24's actual embedded DOCX image was extracted and visually checked before replacing the original folder's `flow.docx`. The checked image is retained as F24 with rounded corners. Label overlap is still visible where the label-gap calculation cannot fit a break; the corner fix does not resolve that separate defect. The refreshed catalogue and samples were rendered but have not all received a new visual acceptance review. Merge approval remains outstanding.

## 10. Label spacing along the connector

Commit `3570465` corrects the excessive breaks around F25's labels. The label-gap helper had used text width for both horizontal and vertical connectors. It now measures the text along the connector: width horizontally, rendered height vertically, with the existing character-width clearance on each side. Run selection, grid rank spacing and the older layout postprocessor share this calculation. No engine condition uses a diagram name, identifier or label; the diff was checked before committing.

F25's two stroke breaks fell from 147.456 px and 153.6 px to 24.288 px each. The clear rank gaps fell from 180 px to 120 px. This also lets F24's vertical runs hold their labels without drawing through the text. Both actual embedded images were extracted from the regenerated DOCX and visually checked: F25 label gaps and F24 label gaps. The F24 label-overlap finding in section 9 describes the earlier output and is now resolved.

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

Four new geometry checks pass for TB, BT, LR and RL. They verify opposite-side placement, facing diamond vertices, one elbow and normal entry at each target's upstream face. The rebuilt executable converted the original folder's `flow.md` directly; the actual F29 image was extracted and visually checked before the DOCX replaced the original `flow.docx`. The checked image is retained as F29 terminal targets.

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

The subsequent F30 markup showed excessive breaks around Get money, One, Two and Three. The general label-dimension correction from section 10 already resolves all four in the current DOCX: each break is 24.288 px, and the connector resumes on both sides. The actual embedded image was extracted from `out/grid-terminal-decision/flow-terminals.docx` and visually checked; it is retained as F30 label gaps. This verification required no further code changes; the measured source remains `74fa83b`.

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

The rebuilt `md2docx.exe` was called directly with the original folder's `flow.md` and `out/grid-picture-placement/flow-placement.docx`. Its actual embedded images were extracted and visually checked at F24, F25, F29, F31, F34, F35 and F46. The checked output replaced the original folder's `flow.docx`. The copy completed and the destination size matches; an additional destination hash check was blocked by the open document's file lock. The new retained comparison images are F31, F34, F35 and F46.

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

The rebuilt executable converted the original folder's `flow.md` directly to `out/grid-surface-normals/flow-normals.docx`. Its actual embedded F35 picture was extracted and checked, then the output replaced the original folder's `flow.docx` after the owner closed Word. Source and destination DOCX hashes match. The checked F35 square attachments supersede the outgoing attachment geometry in section 12's earlier picture. Extracted F24, F25, F29, F31, F34 and F46 images are byte-identical to the preceding checked output. README and current status describe the terminal exception. The generic engine diff was checked for diagram-specific conditions before committing. No merge was performed; the previous unresolved decisions remain outstanding.

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

- Horizontal retry
- Independent outcomes and shared exit
- Pipeline detours and early exit
- Grouped loop branches
- Sequential group crossings
- Paired rank crossings
- Parallel group clearance
- Wide parallel fan

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

The rebuilt rendering executable produced ten sample SVG/PNG pairs under `out/live-picture-investigation/samples/` and 126 catalogue pairs under `out/live-picture-investigation/catalogue/`. `out/live-picture-investigation/manifest.txt` lists every absolute artifact path. The final sample 03 picture was visually inspected: the title is clear and the source approach is straight. Sample 01 and 06 PNGs, and all catalogue PNGs, are byte-identical to section 14's output. Their broader unresolved visual defects remain; passing the listed-kind check is not acceptance of the whole diagram.

The engine diff was checked for diagram selectors: the rules use frame geometry, measured title width, source position and occupied slots. No sample number, label value or particular node ID selects behaviour. The supplied instructions, briefs and editor settings remain untouched. No merge was performed.

The requested regeneration of the original folder's `enterprise.md` used the rebuilt WinForms executable directly, with the input filename and a short lock timeout. It successfully replaced `enterprise.docx`, but DOCX inspection found zero embedded media files and 130 render warnings saying `Unknown diagram type in: enterprise`. The current PR #9 converter does not support that diagram kind; the separate enterprise renderer work from PR #1 has not been integrated. This regeneration is evidence of the current limitation, not a successful diagram render. The original Markdown was unchanged.

## 17. Bypasses and shorter frame entries — 10 October 2026

The owner deferred reconciliation of conflicting checks and requested progress on generic visual routing defects. No existing test assertion, threshold or fixture was changed. The stopped HTTPS investigation was not resumed.

Four fixes use topology and geometry:

1. An ordinary fan-out shortcut no longer replaces an obstacle-aware route when its targets are connected by a forward path. This keeps skip links alongside a chain clear of intermediate nodes.
2. Grid validation now identifies the final segment by its actual segment index. A target port inside a round node's rectangular bounds can retain its valid normal attachment instead of falling back to an unchecked route through another node.
3. Extended frame reservations stop before unrelated internal members, while retaining the minimum terminal run. The first variation also capped reservations against external nodes and introduced a border-hugging approach; the second restricts the cap to internal members.
4. A terminal reserves space beyond a frame face only when the other endpoint lies beyond that face. Entry from beside a frame can turn inside it after crossing the nearer face instead of detouring beyond the opposite side. Existing nested-frame exit checks still pass. This does not implement the on-hold midline rule.

| Commit | Subject |
| --- | --- |
| `b72e274` | Keep chain bypasses on obstacle-aware routes |
| `93e66e7` | Validate the final route segment as a target attachment |
| `ab76278` | Keep frame reservations clear of unrelated members |
| `98af3a8` | Enter group frames through the nearer face |

Ten new structural cases cover forward-chain bypasses in four directions, mirrored round-target attachments, mirrored frame-member clearance and mirrored entry from beside a frame. The bypass, target-segment and nearer-face fixes each took one implementation attempt after reproduction; the internal reservation fix took two. No new failing test exhausted the three-attempt allowance. The engine diff was checked for sample numbers, node labels and diagram selectors; none selects these rules.

All five suites ran sequentially with build and restore on pushed application commit `98af3a8`. Master was not rerun.

| Suite | Master passed / failed | Starting branch passed / failed | Current passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 386 / 30 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 932 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 42 / 0 |

Failure names are unchanged from section 16. The sole master-passing regression remains `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules`; its assertions remain untouched. The four pre-existing grid-only failures absent from master remain. Final logs, TRX files and failure comparisons are under `out/bypass-routing/verified/`. Reproductions and focused attempts are retained under `out/bypass-routing/`.

The rebuilt rendering executable regenerated ten sample SVG/PNG pairs and 126 catalogue pairs under `out/bypass-routing/samples/` and `out/bypass-routing/catalogue/`. `out/bypass-routing/manifest.txt` lists every absolute SVG, PNG and sample gate-log path. Only four sample PNGs and one catalogue PNG changed against section 16; all five were visually inspected:

- Forward-chain bypasses: skip routes clear the intermediate nodes; long tracks and crowded crossings remain.
- Round-target bypass: the connector clears the intervening node; wide spacing and convergence crossings remain.
- Chain skip link: the route clears the intermediate node but still has four rounded bends with its allocated ports. The corner-limit and line-alignment issue is unresolved.
- Shorter frame entries: the previous body intersections and far-face detours are gone. A crossing between other connectors and circle-attachment diagnostics remain.
- Dense frame entries: routes changed under the nearer-face rule. Crowded corners, overlapping arcs and a crossing through the right frame title remain; this picture is not accepted.

None of the ten sample gate logs reports `NodeIntersection`. Other diagnostics remain, including mixed line styles, attachment reports and line-shape failures. Some diagonal reports describe flattened rounded bends. The chain-skip canvas report uses a node-union margin although the actual expanded SVG contains the route. These diagnostics were recorded without changing their thresholds or treating them as resolved acceptance checks.

The original `flow.docx` was not regenerated and remains section 14's checked output from `65deb18`. The unsupported enterprise-renderer state is unchanged. Supplied instructions, briefs and editor settings were left untouched. No merge was performed; the six defaults, midline decision and broader acceptance work remain outstanding.

## 18. Approved decisions and native renderers — 10 October 2026

The owner approved the six defaults in section 7, the replacement frame rule,
and changing conflicting uniform-rank-gap assertions to check local growth.
The supplied Instructions files remain untouched. The maintained
[approved decisions](layout-engine/APPROVED-GRID-DECISIONS.md) record precedence
and implementation limits. The owner's later instruction was to move forward
and fix forward; this checkpoint does not approve a merge or accept defects.

### Commits pushed

| Commit | Subject |
| --- | --- |
| `7e17a14` | Render Enterprise diagrams through the native pipeline |
| `21155ac` | Render recognised pie data with a native SVG adapter |
| `7057418` | Report invalid pie statements before legacy fallback |
| `e008601` | Grow rank gaps only where routing or labels need space |
| `1ab95ab` | Grow frame clearance and contain internal return tracks |
| `99c7cc8` | Respect configured rank clearance during local gap planning |
| `a34ebb9` | Preserve nested frame clearance during grid compaction |

Enterprise integration selected the parser, validator, notation renderer,
relationship tables, samples and tests from PR #1 without merging that branch
or replacing PR #9's routing with its older code. Native Enterprise paths use
rounded bends, break visible strokes around labels and include junction
captions in canvas bounds. Accessibility metadata reaches SVG and DOCX.
Imported tests were adapted to generic structural fixtures rather than
assertions keyed to a sample name. Complete ArchiMate conformance is not claimed.

The pie adapter consumes the recognition contract. It preserves declaration
order, first duplicate values, original-total percentages and legends for tiny
slices, while omitting arcs below the vendored renderer's one-percent cutoff.
Zero/empty data and a full-circle slice remain finite. Explicit configuration
falls back. Malformed input preserves a source diagnostic instead of the
legacy renderer's blank image. Colours are native defaults; pixel parity is
not claimed. Existing non-native sequence conversion still passes.

Grid gap planning now retains 60 px base clearance and grows only affected
rank gaps. Larger explicit rank spacing remains a minimum. Box heights keep
mixed diamond/box centres on whole grid coordinates. Label rank planning
includes corner clearance. Frame fitting moves aligned outside columns/rows
together, reserves extra internal-return padding and rebuilds clear contained
returns. Nested frame/member clearance uses the shared spacing thresholds.

### Final suite comparison

All five projects were run with plain `dotnet test` on pushed application
commit `a34ebb9`, including build and restore. Master was not rerun.
Earlier full verification exposed an ignored explicit rank-spacing setting
and nested-frame clearance regressions; both were fixed and focused checks
passed before the final source verification below. Existing nested-clearance
assertions were preserved. The additionally corrected retry rank-gap assertion
was covered by the owner's explicit uniform-gap approval.

| Suite | Master passed / failed | Starting PR #9 passed / failed | Final passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 442 / 32 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 1008 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 52 / 0 |

Two existing master-passing failures remain:

- `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules`: the previous HTTPS
  investigation stays deferred under its already-recorded three-attempt stop.
- `TryResolve_Sample03_EdgesKeepGroupBorderClearance`: the group-target route
  still crosses its target frame more than once. This is a new regression
  against the preceding PR #9 checkpoint.

Four branch-only policy/coordinate checks still fail:
`RenderToSvg_InspectContent_BranchesUseOrthogonalRunsWithDiamondStub`,
`RenderToSvg_MessageFilter_BranchesEnterLeftSideWithRoundedZ`,
`RenderToSvg_TypeDecision_NoSharedBottomStub`, and
`RenderToSvg_ValidDecision_BranchesUseOrthogonalRunsWithDiamondStub`.
The new failing name `TryResolveLayoutGuidePoints_F113_ParallelBranchFanIn_UsesExteriorCorridor`
already failed on master; its centre-side departure expectation now differs
from the compact downstream route. It was not weakened. No extra
rule-validation failure name remains against master or the previous checkpoint.

### Bounded group-border investigation

1. Whole-grid half-space eviction plus internal-return padding left six border
   failures: re-entry and runs along borders. It also exposed a store-frame
   member overlap where no safe half-space could move.
2. Moving aligned non-member rows/columns when no safe half-space exists fixed
   the store-frame overlap. The six border failures remained.
3. Reserving 30 px exterior clearance, or 60 px where a connector occupies the
   gap, cleared five border failures. The group-target re-entry remains because
   its route detours back through the target frame after its first crossing.

The test was stopped after these three implementation attempts. The later
nested-frame fix addresses a separate shared-threshold regression; it does
not claim to solve the stopped route. A fallback row/column mutation-order
bug was also corrected by capturing the original grid coordinate before moves.
No target-specific selector or assertion relaxation was introduced.

### Pictures, conversion and remaining acceptance

Final source renders and logs are under `out/rulebook-completion/a34ebb9/`:
`catalogue/`, `samples/`, `enterprise/`, `pie/`, and the two DOCX files. The
picture manifest lists every generated SVG/PNG path. Ten flowchart samples and
126 active catalogue entries render; ten valid Enterprise samples render.
Enterprise sample 04 deliberately rejects invalid relationships; sample 05
rejects a provisional 3.2 relationship. Rejections are retained as evidence.

The built WinForms `Converter.exe` was called directly with `-in` and `-out`.
The supplied Enterprise catalogue produces 94 embedded diagrams and 36
source-preserving warnings; its previous output had zero diagrams and 130
unknown-type warnings. Unsupported quoted-comment, styling and configuration
syntax remain alongside invalid/provisional relationship cases. This result
must not be reported as complete Enterprise catalogue support. The flow DOCX
contains 128 drawings and no render warnings. Counts come from inspecting the
actual DOCX package; they do not certify Word pagination or visual acceptance.

Picture checks confirm rounded bends and label breaks in the side-exit case,
compact height-based breaks on vertical labels, the approved horizontal retry
arrangement and the pipeline early exit beside its collector. Native Enterprise
pictures show notation and relationship markers with rounded paths, but wide
layouts, target-frame attachment gaps and awkward/shared tracks remain. The
junction caption now stays inside the canvas. The pie legend retains the tiny
slice while the visible wedges follow the cutoff. These are representative
checks, not approval of every picture. Later final-source picture notes and
DOCX inspection results are in the local evidence folder.

Rulebook implementation remains incomplete: global label/connector collision
planning, automatic growth for exhausted label runs, crowded multi-return
allocation and remaining placement/port/crossing defects still need work.
Only Enterprise and ordinary pie adapters were added in this pass; the other
native replacements remain future work. All engine diffs were checked for
sample, node-label and diagram-specific selectors. Supplied files and editor
settings were preserved. No merge, rebase or new PR was performed.

## 19. Clear forward doglegs — 10 October 2026

The circled technology-layer connector retained four bends, including a short
backward step, although its facing ports allowed a two-bend route. The generic
post-routing simplifier now preserves both ports and removes the unnecessary
step only when the alternative clears node bodies, shared frames and reserved
tracks. Existing shortened terminal tracks do not count as new conflicts.

An initial simplification exposed insufficient straight clearance beside the
rounded corners. The follow-up grows a genuine shared gap by whole 30 px steps
before simplifying; it rejects cuts through node bodies. Aligned nodes, frame
bounds and existing routes move together. The result retains 20 px corners,
at least 20 px of straight source run and at least 10 px before the target.
The marked connector now has two bends and 25 px straight runs at both ends.
A source relationship diamond therefore remains clear of the first corner.

- `b03bf69` — Remove unnecessary backward steps from forward routes.
- `23787e8` — Grow narrow route gaps before removing backward steps.

Enterprise rendering also passes the completed grid layout to the shared route
resolver rather than resolving from bounds alone. No engine rule or new test
selects a catalogue number, node label or named diagram. New structural checks
cover horizontal/vertical and mirrored simplification, blocked shortcuts,
reserved tracks and whole-column gap growth. Existing assertions are intact.

Focused verification passed 43 Enterprise/simplification checks, followed by
8 structural checks including the new shared-gap cases. An earlier broad
Enterprise-name filter also selected two already-failing legacy quality tests;
those were recorded and were not changed. All five full suites were rerun on
pushed application commit `23787e8`; no new failing names were introduced:
Layout 450 passed / 32 failed, rule validation 49 / 10, recognition 1008 / 0,
Naiad adapter 47 / 0, converter 52 / 0. Master's baseline remains unchanged.
The two master-passing Layout regressions recorded in section 18 remain.

Final-source evidence is local under `out/enterprise-route-context/final/`:
full-suite logs/TRX, failure-name comparison, complete picture manifest,
flowchart catalogue/sample renders, Enterprise samples, the marked technology
source render and regenerated `enterprise.docx`. The WinForms executable was
rebuilt and invoked directly. The DOCX still has 94 drawings and 36 explicit
warnings; this routing change does not extend Enterprise syntax support.
The actual embedded technology-layer image was extracted for the visual check.
Broader acceptance failures and remaining visual defects are not resolved by
this correction. No merge was performed and supplied files remain untouched.

## 20. Plain relationship labels and elliptical attachments — 10 October 2026

The marked motivation diagram exposed two generic notation mismatches. Plain
SVG relationship text inherited the minimum width of an HTML label box, so a
single influence sign reserved a disproportionately large break. The renderer
now estimates plain glyph width at its actual 12 px font size, retains one
character of clearance on each side, and uses the same measurement to select
the label position and break. Node label sizing is unchanged.

Elliptical elements were mapped to circular routing outlines. The mapper now
preserves the elliptical shape, and shared grid port projection uses both
ellipse radii. The shared ray/outline helper also understands ellipses. The
geometry parity adapter preserves that actual outline when applying Enterprise
sizes. Incoming and outgoing connectors therefore end on the drawn outline.

`f0cccc1` — Match relationship gaps and attachments to the drawn notation.

The focused Enterprise run passed all 43 checks. Six new generic checks cover
positive/negative influence signs and multiple incoming/outgoing relationships
on ellipses in both horizontal and vertical layouts. Existing tests and their
assertions were not weakened. The engine diff was searched for diagram names,
catalogue numbers, node ids and labels; no diagram-specific rule was added.

All five suites ran on pushed application commit `f0cccc1`: Layout 456 passed
/ 32 failed, rule validation 49 / 10, recognition 1008 / 0, Naiad adapter 47 / 0,
and converter 52 / 0. There are no new failing names against `23787e8`.
Master's baseline was not rerun. The two master-passing Layout regressions
recorded above remain stopped; this correction does not resolve them.

Final evidence is under `out/enterprise-outline-label/final/`: full-suite logs
and TRX files, failure comparison, picture manifest, 126 rendered flowchart
catalogue cases (four commented cases skipped), ten rendered Enterprise samples
and the same two expected sample rejections. The marked motivation source and
its rendered picture are included. The rebuilt WinForms executable generated
`enterprise.docx` directly using `-in` and `-out`; it retains 94 drawings and
36 explicit warnings. The actual embedded motivation picture was extracted
and inspected: the influence break is compact, and the ellipse connector stops
at its left outline. This is not a full Word pagination review or acceptance
of all catalogue diagrams. No merge was performed.

## 21. Junction caption clearance — 10 October 2026

A junction caption occupied a fixed position beside its circle, independent of
routing, and the outgoing dashed connector ran through the text. External
captions now choose a nearby position after routes are complete. The search
reserves clearance from route segment envelopes, node bodies, frame title
strips, relationship labels and earlier junction captions. The canvas grows to
include the selected caption bounds. Connector geometry and markers are
unchanged; this is caption placement rather than an edge-label break.

`9d36297` — Keep junction captions clear of routed connectors.

Four new generic split/join checks cover filled and hollow junctions in both
layout directions. They inspect the rendered paths, including arrowheads,
for caption clearance and verify canvas containment. All 47 focused Enterprise
checks pass. Existing assertions were preserved, and the engine diff contains
no catalogue, diagram-name or node-label selectors. The bounded caption search
retains visible text if it exhausts its candidates; global label planning and
exhausted-space growth remain part of the incomplete rulebook work.

All five suites ran on pushed application commit `9d36297`: Layout 460 passed
/ 32 failed, rule validation 49 / 10, recognition 1008 / 0, Naiad adapter 47 / 0,
and converter 52 / 0. No new failing names appeared against `f0cccc1`.
The two master-passing Layout regressions remain stopped as recorded above;
master was not rerun. No test expectations were changed.

Evidence is under `out/junction-caption/final/`: suite logs/TRX, failure-name
comparison, picture manifest, 126 rendered flowchart catalogue cases with four
commented skips, ten rendered Enterprise samples and the same two expected
sample rejections. The marked junction example is rendered separately.

The rebuilt WinForms executable generated `enterprise.docx` directly with
`-in` and `-out`. The DOCX still contains 94 drawings and 36 explicit warnings.
Its embedded junction picture was extracted and inspected: the caption is
above the outgoing dashed stroke and is no longer crossed by it. This is a
picture check, not a full Word pagination review or catalogue acceptance.
Supplied files and editor settings remain untouched, and no merge was made.

## 22. Shared input fans and continuation alignment — 10 October 2026

The influence diagram retained a branching source outside its two targets and
an unbranched continuation offset from its sole parent. Generic placement now
recognises a root fan whose targets also have independent same-rank inputs.
It centres that source between its targets on the shared grid and aligns each
single-parent, unbranched tail with its parent. Moves are rejected when they
would violate node clearance. Group membership and flow-axis ranks remain
unchanged; independent inputs and the shared targets retain their positions.

The centred source uses free downstream ports into the targets' upstream
ports. This avoids an inherited side exit introducing an unnecessary detour
after the source move. Rounded bends and normal endpoint runs are retained.
No rule selects an Enterprise type, influence sign, diagram name or node label.

`4ce8e85` — Centre shared branch sources and align their continuations.

Focused verification passed 52 placement/Enterprise checks, followed by nine
structural checks including complete routing in all four flow directions.
The checks cover mirrored placement, occupied destinations and downstream
port use with no more than two bends. Existing assertions were not changed.
The engine diff was checked for diagram-specific selectors before committing.

The supplied work architecture picture is a visual reference for subsequent
improvements: compact titled groups, consistent node sizes and alignment,
local routing within groups, readable nested integration groups and cross-group
routes through clear space. It is a visual target, not evidence that these
qualities have been achieved across the catalogue; its business-specific
names must not become layout selectors.

All five suites ran on pushed application commit `4ce8e85`: Layout 469 passed
/ 32 failed, rule validation 49 / 10, recognition 1008 / 0, Naiad adapter 47 / 0,
and converter 52 / 0. No new failing names appeared against `9d36297`.
The two master-passing Layout regressions remain stopped as recorded above;
master's baseline was not rerun.

Final evidence is under `out/influence-placement/final/`: suite logs/TRX,
failure comparison and picture manifest, 126 rendered flowchart catalogue
cases with four commented skips, ten rendered Enterprise samples with the
same two expected rejections, and the marked influence example. The rebuilt
WinForms executable generated `enterprise.docx` with `-in` and `-out`.
It retains 94 drawings and 36 explicit warnings. The embedded influence
picture was extracted and inspected: its branching source sits between the
other inputs, the continuation is under its parent, and both branches have
two rounded bends with label breaks. This is not full catalogue acceptance
or a Word pagination review. Supplied files remain untouched; no merge was
performed.

## 23. Aligned vertical return ports — 10 October 2026

The marked association return inherited inward-facing top/bottom ports and
approached both rectangular elements diagonally. Aligned vertical returns
between non-diamond nodes now use shared side ports. A clear orthogonal route
keeps those allocated ports when the legacy loop pass runs later. The marked
return uses the right-hand corridor with two rounded bends, straight terminal
runs and square attachments; it clears the intervening object.

`caead11` — Route aligned vertical returns through side ports.

The rule uses back-edge topology, alignment and shape geometry. It does not
select an Enterprise relationship type, catalogue number, diagram name or node
label. The engine diff was audited before committing. Existing assertions and
fixtures were not changed. Focused verification passed 57 structural and
Enterprise checks; new checks cover vertical cyclic returns in both directions,
node clearance, orthogonal segments and square terminal runs.

Horizontal exploration was stopped and excluded from this fix. Allocating
side ports alone did not survive legacy loop routing; preserving completed
side routes still left the straight-route prepass and other horizontal loop
forms outside that condition. A controlled alignment fixture was then moved
by the chord-blocker placement rule, so it did not establish the intended
blocked-chord precondition. The observed horizontal routes can still contain
seven guide points. Those exploratory cases are retained in local logs rather
than being presented as passing coverage. The final regression tests cover
the corrected vertical situation; no existing test was weakened. A separate
horizontal return investigation is deferred rather than trying more variants.

All five suites ran on pushed application commit `caead11`: Layout 471 passed
/ 32 failed, rule validation 49 / 10, recognition 1008 / 0, Naiad adapter 47 / 0,
and converter 52 / 0. No new failing names appeared against `4ce8e85`.
Master was not rerun. The two master-passing Layout regressions recorded in
section 18 remain stopped; this change does not resolve them.

Final local evidence is under `out/association-return/final/`: suite logs/TRX,
failure comparison, 126 rendered flowchart catalogue cases with four commented
skips, ten Enterprise samples with the same two expected rejections, and the
marked association example plus the exploratory horizontal cyclic example.

The rebuilt WinForms executable regenerated `final/enterprise.docx` successfully
from the supplied Markdown. It contains 94 drawings and 36 source-preserving
warnings, unchanged from the previous conversion. The marked association's
embedded picture was extracted and visually inspected: the return runs around
the right side with two rounded bends and square side attachments, clearing
the intervening object. This is not a full Word pagination review or catalogue
acceptance. The picture inventory is in `final/picture-manifest.txt`. Supplied
files remain untouched; no merge was performed.

## 24. Relationship caption staggering — 10 October 2026

Nearby relationships previously put their labels at the same straight-run
midpoint. The marked long caption overlapped the adjacent return's caption.
Relationship text now wraps at word boundaries, tries progressively narrower
lines when necessary, and searches straight runs from their midpoint outward.
Placement reserves node bodies, frame titles, other captions and neighbouring
connector envelopes. Captions on nearby parallel runs use distinct levels.
Each connector's break follows its chosen text bounds, and multiline bounds
also feed canvas expansion and external junction-caption placement.

`36c152b` — Stagger relationship labels along clear straight runs.

The engine diff was checked for diagram-specific selectors. The rule uses
text dimensions, segment directions, node/frame bounds and available clearance;
it does not select diagram names, catalogue numbers or node labels/identifiers.
Existing tests and fixtures remain unchanged. Two new synthetic structural
checks cover horizontal and vertical parallel corridors, label separation,
other-line clearance, word preservation and breaks at the selected caption.
Twelve focused label, outline and junction-caption checks passed.

The initial exploratory synthetic vertical fixture placed full-length parallel
lines only 15 pixels apart. No centred word-sized caption could clear the other
line anywhere in that corridor, so the new neighbour-line check correctly
exposed an exhausted corridor. The new fixture was corrected to provide usable
space while still requiring wrapping and staggering; existing fixtures were not
changed. No claim is made that every crowded corridor can be solved locally.
If the bounded search is exhausted, visible fallback text remains rather than
being hidden; automatic route-gap growth and global label planning remain open.

All five suites ran once on pushed application commit `36c152b`: Layout 473
passed / 32 failed, rule validation 49 / 10, recognition 1008 / 0, Naiad adapter
47 / 0, converter 52 / 0. There are no new failing names against `caead11`.
The master baseline was not rerun. The two stopped master-passing Layout
regressions listed in section 18 remain unchanged.

Evidence is under `out/relationship-labels/final/`: all suite logs/TRX,
failure-name comparison, the marked relationship picture, the flowchart
catalogue and Enterprise samples. Rendering again produced 126 flowcharts
with four commented skips, and ten Enterprise samples with the same two
expected rejections. Picture paths are listed in `picture-manifest.txt`.

The rebuilt WinForms executable regenerated `final/enterprise.docx` with 94
drawings and 36 source-preserving warnings, unchanged from the previous pass.
The marked diagram's embedded picture was extracted and visually inspected:
the long caption wraps, the neighbouring return caption sits above it, and
all displayed captions have breaks in their own connectors. This is not a
full catalogue acceptance or Word pagination review. Supplied files remain
untouched and nothing was merged.

## 25. Literal title canvas sizing — 10 October 2026

Long titles were centred inside a viewport sized only for nodes and routes.
The rasterizer clipped both ends. The renderer now reserves width for the
literal bold title with outer padding and translates the unchanged diagram
horizontally to centre it below the title. Punctuation, quotes and Unicode
remain literal SVG-escaped text rather than inline Markdown. The conservative
glyph-width estimate avoids adding a native font dependency to the layout
library; exact typographic measurement remains outside this change.

`d4b6206` — Fit enterprise titles inside the diagram canvas.

The engine diff was checked for diagram-specific selectors. Title sizing
uses only text glyphs and bounds; no title content or diagram identifier is
selected. Existing assertions and fixtures remain unchanged. Three generic
checks cover long literal punctuation/Unicode titles, wide Latin glyphs and
CJK text, viewport growth, title centring and preservation of node geometry.
All 24 focused title and Enterprise renderer checks passed. The marked title
was rendered and visually inspected before committing; every character is
visible, including the final check mark.

All five suites ran once on pushed application commit `d4b6206`: Layout 476
passed / 32 failed, rule validation 49 / 10, recognition 1008 / 0, Naiad adapter
47 / 0, converter 52 / 0. Failure names are unchanged from `36c152b`; the two
stopped master-passing Layout regressions in section 18 remain. The retained
master baseline was not rerun.

Final evidence is under `out/enterprise-title/final/`: suite logs/TRX,
failure-name comparison, the marked title picture, 126 flowchart renders with
four commented skips, and ten Enterprise samples with the same two expected
rejections. `picture-manifest.txt` lists all rendered picture paths.

The rebuilt WinForms executable regenerated `final/enterprise.docx` successfully
with 94 drawings and 36 source-preserving warnings. The marked title's embedded
picture was extracted and visually inspected: the complete literal title is
visible, and both nodes remain centred below it. This is not a full catalogue
acceptance or Word pagination review. Supplied files remain untouched and
nothing was merged.

## 26. Explicit label breaks — 10 October 2026

The quoted-label parser removed the backslash from newline escapes, node and
relationship wrapping ignored explicit lines, and group tabs wrote raw tags
as one line. Quoted labels now decode supported escapes once, preserving
escaped literal backslashes and unknown escape sequences. Case-insensitive
break tags become explicit newlines. A shared wrapper preserves each explicit
line, including blank lines, and applies automatic wrapping independently.
Element measurements, group tabs, relationship gaps and external junction
captions use the resulting lines. Shared frame title bands grow with the
line count, while relationship captions also avoid group side/bottom borders.

`4c6505b` — Render explicit breaks consistently in enterprise labels.

The engine diff was audited for diagram-specific selectors. Rules use only
escape syntax, text dimensions, line counts and geometry; they do not select
catalogue numbers, diagram names or node labels/identifiers. Existing tests
and fixtures remain unchanged. Eight new recognition checks cover all label
statement kinds, break variants, escaped literals, unknown backslashes,
quotes, independent wrapping and blank lines. Six new layout checks cover
separate SVG lines, member/tab clearance, growing node height and external
junction captions without increasing the symbol's normal layout size.
The initial new junction assertion assumed its nominal 26-pixel size; shared
grid quantisation makes the normal isolated layout size 60 pixels, so the new
assertion compares against the unchanged single-line layout instead. Existing
assertions were not changed. All 62 focused parser checks and 33 focused
layout/render checks passed.

The marked example was rendered and visually inspected before committing:
no break tags remain visible, both node labels and the group tab break at the
requested places, and relationship captions have separate lines and matching
connector gaps. Local caption search can still exhaust a crowded corridor;
automatic gap growth/global label planning remains open.

All five suites ran once on pushed application commit `4c6505b`: Layout 482
passed / 32 failed, rule validation 49 / 10, recognition 1016 / 0, Naiad adapter
47 / 0, converter 52 / 0. No new failing names appeared against `d4b6206`.
The two stopped master-passing Layout regressions in section 18 remain
unchanged. Master was not rerun.

Final local evidence is under `out/explicit-label-breaks/final/`: suite
logs/TRX, failure-name comparison, the marked break example, 126 flowchart
renders with four commented skips, and ten Enterprise samples with the same
two expected rejections. `picture-manifest.txt` lists all picture paths.

The rebuilt WinForms executable regenerated `final/enterprise.docx` with 94
drawings and 36 source-preserving warnings. The marked diagram's embedded
picture was extracted and visually inspected: group, node and relationship
labels show explicit lines instead of raw tags or a dropped newline escape.
The right-hand relationship caption still crosses the group border when the
local placement search exhausts its available straight runs. The search now
reserves that border, but its visible fallback is not a clearance guarantee;
this remains a fix-forward item for automatic corridor growth. This is not a
full catalogue acceptance or Word pagination review. Supplied files remain
untouched; nothing was merged. This commit builds on the previous PR #9 work.

## 27. Quoted comments and label entities — 10 October 2026

Comment stripping previously truncated every statement at the first percent
pair, including pairs inside quoted labels. The scanner now respects quoted
labels, escaped quotes and escaped backslashes. Percent pairs outside quotes
still start comments. Titles remain rest-of-line text and retain their special
comment rule even when the title contains quote characters.

Quoted label decoding now resolves named HTML and decimal Unicode entity
codes once, after escape and explicit-break handling. Unknown names, numeric
overflow, invalid Unicode scalars and XML-invalid controls remain visible as
source text. Decoded angle brackets are escaped when SVG is written, and
entity results are not decoded again or reinterpreted as break tags/escapes.
The named-code set is the runtime HTML decoder's supported set; this change
does not claim every HTML5 named entity is supported.

`2f29eab` — Preserve quoted comments and decode label entity codes.

This builds directly on `8357c88`, preserving the prior PR #9 fixes. The engine
diff was audited for diagram-specific selectors: parsing depends only on
quote/escape syntax and entity validity, with no catalogue numbers, diagram
names or node labels/identifiers. Existing tests and fixtures were unchanged.
Nine new parser checks cover group/element/relationship comments, quote and
backslash escapes, title comments, named/decimal/supplementary entities, invalid
codes and decoding exactly once. A new renderer check verifies safe literal
text across groups, nodes and relationships. All 71 focused parser checks and
28 focused render/explicit-break checks passed. The marked example rendered
and was visually inspected: percent pairs, embedded quotes, decoded symbols
and the Windows path are visible, and the title comment is omitted.

Regeneration reports now include the full absolute DOCX output path, as
requested. This pass saves to
`C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX-pr9\out\quoted-labels\final\enterprise.docx`.

All five suites ran once on pushed application commit `2f29eab`: Layout 483
passed / 32 failed, rule validation 49 / 10, recognition 1025 / 0, Naiad adapter
47 / 0, converter 52 / 0. No new failing names appeared against `4c6505b`.
The retained master baseline was not rerun. The two stopped master-passing
Layout regressions listed in section 18 remain unchanged.

Final evidence is under `out/quoted-labels/final/`: suite logs/TRX, exact
failure-name comparison, the marked quoted-label example, 126 flowchart
renders with four commented skips, and ten Enterprise samples with the same
two expected rejections. `picture-manifest.txt` lists all rendered picture paths.

The rebuilt WinForms executable regenerated the DOCX at the absolute path
above with 96 drawings and 34 source-preserving warnings, improving the prior
94/36 split by two diagrams. The marked diagram's embedded picture was
extracted and visually inspected: it renders with quoted text, percent pairs,
decoded entities and preserved Windows backslashes. This is not full catalogue
acceptance or a Word pagination review. Supplied files remain untouched and
nothing was merged.

## 28. Facing container ports — 10 October 2026

The marked cross-container return had a small backstep because title clearance
moved its source port without moving the facing target port. Aligned vertical
frames now share that displaced track when the facing port is free and the
segment clears unrelated bodies and frames. Ancestor frames remain legitimate
crossings. This preserves the separate track used by the internal forward flow.

The picture inspection also exposed gaps between compound terminals and the
visible boxes: the layout frame includes a title band that differs from the
drawn compound bounds. Normal terminal runs now meet the visible rectangular
compound border, with a margin around rounded corners. Other shape outlines
retain their existing attachment rules.

`76da489` — Align container return lines with their facing ports.

This builds directly on `20d64f9`, retaining all prior application fixes. The
engine diff was checked for diagram-specific selectors; the rules use frame
geometry, nesting, port occupancy and box shape, without catalogue numbers,
diagram names, identifiers or labels as selectors. Existing assertions and
fixtures were unchanged. Two new structural checks cover aligned containers
with an internal forward flow and different title widths, verifying a straight
separate return and square attachments to the visible borders. The focused
run had 72 passes and the same two pre-existing Enterprise quality failures.

All five suites ran once on pushed application commit `76da489`: Layout 485
passed / 32 failed, rule validation 49 / 10, recognition 1025 / 0, Naiad adapter
47 / 0, converter 52 / 0. No new failing names appeared against `2f29eab`.
The retained master baseline was not rerun. The two stopped master-passing
Layout regressions in section 18 remain unchanged; this does not establish
merge readiness or complete visual acceptance.

Final evidence is under `out/container-return/final/`: suite logs/TRX, exact
failure-name comparison, the marked container example, 126 flowchart renders
with four commented skips and ten Enterprise samples with the same two
expected rejections. `picture-manifest.txt` lists every rendered picture path.
The rebuilt WinForms executable saves the regenerated document to
`C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX-pr9\out\container-return\final\enterprise.docx`.
Package inspection found 96 drawings and 34 source-preserving warnings. The
marked diagram's embedded picture was extracted and visually inspected: the
return is straight beside the labelled forward flow and meets both visible
compound borders squarely. This is not full catalogue acceptance or a Word
pagination review. Supplied files remain untouched and nothing was merged.
## 29. Visible internal parent-child relationships — 10 October 2026

The marked parent-child relationships were treated as ordinary external
compound edges. One route crossed its child body, and the other approached
its child diagonally. Caption placement also treated the entire compound
interior as an obstacle and retained overlapping fallback text.

Frames with visible ancestor-descendant edges now reserve internal side and
lower corridors sized from their labels and terminal clearance. The shared
frame fitting and rendered bounds both include that space. Enterprise routes
prefer a clear vertical normal to the lower border; a blocked direct route
uses a clear side corridor with a rounded bend. Caption placement permits
clear compound interiors while preserving title, border and child-body
clearance. Hidden nesting assignments remain hidden.

`303ff6d` — Keep visible parent-child relationships inside containers.

This builds directly on `3ef1fbb`, retaining the previous fixes. The engine
diff was checked for diagram-specific selectors; routing and frame space
use ancestry, geometry, labels as measured text and obstacles. No catalogue
numbers, diagram names or node identifiers/labels select a rule. Existing
tests and fixtures were unchanged. Four new structural cases cover both
relationship directions in vertical and horizontal diagram arrangements.
The horizontal child-to-parent case exposed a sibling blocking the direct
route; the side corridor corrected that case on the second test attempt.
All 29 focused checks passed after the correction.

All five suites ran once on pushed application commit `303ff6d`: Layout 489
passed / 32 failed, rule validation 49 / 10, recognition 1025 / 0, Naiad adapter
47 / 0, converter 52 / 0. No new failing names appeared against `76da489`.
The retained master baseline was not rerun. The two stopped master-passing
Layout regressions in section 18 remain unchanged. This does not establish
merge readiness or complete rulebook acceptance.

Final evidence is under `out/parent-child/final/`: logs/TRX, exact failure-name
comparison, the marked example, 126 flowchart renders with four commented
skips and ten Enterprise samples with the same two expected rejections.
`picture-manifest.txt` lists every rendered picture path. The rebuilt WinForms
executable regenerated the document at
`C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX-pr9\out\parent-child\final\enterprise.docx`.

Package inspection found 96 drawings and 34 source-preserving warnings. The
marked diagram's embedded picture was extracted and visually inspected. Both
captions have line breaks on their own internal routes; the child-to-child
line remains separate and the visible terminal runs meet borders squarely.
This is not full catalogue acceptance or a Word pagination review. More
crowded parent-child cases that exhaust these corridors retain existing
fallback behaviour and need further routing work. Supplied files remain
untouched and nothing was merged.
## 30. Rounded behaviour box ports — 10 October 2026

The marked high-fan picture showed terminals starting inside both sides of
the service box. Enterprise behaviour elements were mapped to stadiums for
routing, although their drawn boxes have short rounded corners. The mapper
now selects a rounded rectangle. Rounded port allocation reserves a corner
inset so crowded ports remain on flat edges and terminal runs meet those
edges normally. The inset is a shared geometric rule for rounded boxes.

`22fd00b` — Attach behaviour relationships to their visible borders.

`ca5adab` — Preserve rounded boxes in equivalent flowchart source.

Both commits build on `cb23b2d`, retaining the previous application work.
The engine diffs were checked for diagram-specific selectors: shape geometry
and port occupancy determine placement, without catalogue numbers, diagram
names, identifiers or labels selecting a rule. Existing tests and fixtures
were unchanged. Eight new structural cases cover crowded inputs and outputs
in both diagram directions, with two fan sizes. The first focused run exposed
ports too close to corners; the inset corrected all cases on the second run.
All 35 focused render/outline checks then passed.

The initial full verification found three new equivalent-flowchart geometry
failures in the business, strategy and grouping samples. The source writer
had no rounded-shape branch and emitted plain rectangles after the mapper
changed. It now writes the corresponding Mermaid rounded-node syntax. All
24 focused parity/rounded-port checks passed without assertion changes.
The initial verification is retained under `out/fan-ports/final/`.

The corrected application commit is verified separately under
`out/fan-ports/verified/`. All five suites ran once on pushed commit `ca5adab`:
Layout 497 passed / 32 failed, rule validation 49 / 10, recognition 1025 / 0,
Naiad adapter 47 / 0, converter 52 / 0. No new failing names remain against
`303ff6d`. The retained master baseline was not rerun. The two stopped
master-passing Layout regressions in section 18 remain unchanged; full
rulebook and visual acceptance remain incomplete.

The verified evidence includes suite logs/TRX, exact failure-name comparison,
the marked fan example, 126 flowchart renders with four commented skips and
ten Enterprise samples with the same two expected rejections. Every rendered
picture path is listed in `picture-manifest.txt`. The rebuilt WinForms
executable saves the final document to
`C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX-pr9\out\fan-ports\verified\enterprise.docx`.
Package inspection found 96 drawings and 34 source-preserving warnings. The
marked diagram's embedded image was extracted and viewed; SVG coordinates
and structural checks confirm distinct ports on flat borders, clear of the
rounded corners, with normal terminal runs. The panoramic row remains wide
and is reduced substantially on a document page; this pass does not change
that placement or complete Word pagination review. Supplied files remain
untouched and nothing was merged.
## 31. Nested return corridors and short captions — 10 October 2026

The marked cyclic graph had its long chain return crowded between components,
a diagonal circular feedback approach and overlapping captions on neighbouring
flows. The longest aligned return to a shared ancestor now uses the opposite
side from shorter aligned returns. Allocated clear side routes on either side
survive the legacy loop-routing pass. Circular back edges bypass inward-facing
straight port selection and use a side corridor with normal terminal runs.

Caption searches now advance by the existing clearance distance rather than
coarse fifteen-pixel steps. A two-point straight connector reserves arrowhead
clearance without an unused corner allowance, allowing nearby captions to
stagger in a bounded gap. Wrapped captions and their line breaks remain tied
to their selected straight segments.

`db30bd7` — Separate nested returns and clear their terminal runs.

This builds directly on `89142c7`, preserving previous application fixes. The
engine diff was checked for diagram-specific selectors; rules use aligned
bounds, return spans, shared targets, circular outlines and text clearance.
No catalogue number, diagram name, identifier or label selects a rule.
Existing assertions and fixtures were unchanged. Four new structural checks
cover the aligned long/short return, circular returns in both vertical
directions and caption staggering on short parallel runs. All 29 focused
checks passed.

An exploratory bottom-to-top version of the long/short test did not meet its
alignment precondition: the shorter return's source was in a different column.
That uncommitted fixture was excluded from the aligned-case check rather than
broadening the rule to unrelated geometry. The implemented rule remains limited
to aligned nested returns; the two circular-return direction checks remain.

All five suites ran once on pushed application commit `db30bd7`: Layout 501
passed / 32 failed, rule validation 49 / 10, recognition 1025 / 0, Naiad adapter
47 / 0, converter 52 / 0. No new failing names appeared against `ca5adab`.
The retained master baseline was not rerun. The two stopped master-passing
Layout regressions in section 18 remain unchanged. Rulebook acceptance and
full visual correctness remain incomplete.

Final evidence is under `out/cycle-corridors/final/`: logs/TRX, exact failure-name
comparison, the marked example, 126 flowchart renders with four commented
skips and ten Enterprise samples with the same two expected rejections.
`picture-manifest.txt` lists every rendered picture path. The rebuilt WinForms
executable saves the regenerated document to
`C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX-pr9\out\cycle-corridors\final\enterprise.docx`.
Package inspection found 96 drawings and 34 source-preserving warnings. The
marked diagram's embedded picture was extracted and visually inspected: the
long return is outside on the left, the shorter return stays right, circular
feedback has rounded bends and square terminal runs, and order/status text
is separate. The small retry self-loop caption still crosses its short path;
that exhausted local label corridor needs additional space and remains open.
This is not full catalogue acceptance or a Word pagination review. Supplied
files remain untouched and nothing was merged.
## 32. Parallel relationship identities and caption corridors — 10 October 2026

The marked two-component graph drew repeated relationships on the first
matching route. The shared resolver now matches nonempty relationship ids
exactly; endpoint matching remains the fallback when an identity is absent.
Repeated forward relationships reserve side length from measured caption
words and spread same-peer ports symmetrically. Parallel four-point returns
reserve caption space between tracks only when their candidate paths clear
unrelated nodes and remain inside shared frames. Blocked corridors retain
their previous routes and still need broader gap growth.

Caption placement retains its six-pixel text and connector clearance. A
second, excessive exclusion band was removed because it rejected positions
that already met those requirements and forced overlapping fallback text.

`a406884` — Keep parallel relationships and their captions distinct.

This builds directly on `45a4e0b`, preserving previous application fixes.
Engine changes were audited for diagram-specific selectors: identity matching,
relationship multiplicity, side geometry, measured text and obstacle bounds
select the rules. No catalogue number, diagram name, fixed identifier or label
selects a rule. Existing tests, fixtures and assertions were unchanged. Two
new structural cases cover repeated forward and return relationships in both
supported Enterprise directions, requiring distinct paths, caption gaps and
clearance from every neighbouring connector. All 18 focused checks passed.

All five suites ran once on pushed application commit `a406884`: Layout 503
passed / 32 failed, rule validation 49 / 10, recognition 1025 / 0, Naiad adapter
47 / 0, converter 52 / 0. No new failing names appeared against `db30bd7`.
The retained master baseline was not rerun. The two stopped master-passing
Layout regressions in section 18 remain unchanged. Rulebook acceptance and
full visual correctness remain incomplete.

Final evidence is under `out/parallel-relations/final/`: logs/TRX, exact
failure-name comparison, the marked example, flowchart and Enterprise sample
renders, package inspection and `picture-manifest.txt`, which lists every
rendered picture path. The rebuilt WinForms executable saves the document to
`C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX-pr9\out\parallel-relations\final\enterprise.docx`.
Supplied files remain untouched and nothing was merged.
Package inspection found 96 drawings and 34 source-preserving warnings. The
marked diagram's embedded image was extracted and viewed: forward captions
have separate gaps, return captions are distinct, and the parallel return
lines retain separate ports and rounded bends. The boxes are wider to reserve
these corridors. The render set contains 126 flowchart pictures with four
commented skips and ten Enterprise samples with the same two expected
rejections. This is not full catalogue acceptance or a Word pagination review.
## 33. Visible nested-container ports and final track separation — 10 October 2026

The marked deep nesting graph had two external network relationships ending
at obsolete routing-frame boundaries, outside their drawn cluster targets.
Shared routing now reattaches such endpoints to facing visible borders,
reserves distinct repaired ports and rebuilds orthogonal approaches with
normal terminal runs. The Enterprise adapter passes its final visible frame
bounds to the shared helper rather than leaving dangling endpoints.

The final visible routes also run through shared track separation. Caption
measurements enlarge separation where a labelled connector competes with
another run. Ancestor frames remain legitimate boundary crossings while
unrelated node bodies block movement. This removes the packed central bends
and separates the two incoming arrows and replication caption in the marked
example. The artifact-to-artifact association still descends through a parent
container title; title obstacles and exhausted corridors remain open work.

`02e1610` — Join nested relationships to their visible containers.

This builds directly on `f2ad523`, retaining previous application fixes.
The engine diff was audited for diagram-specific selectors: visible bounds,
group ancestry, free port positions, normals and caption dimensions select
these rules. No catalogue number, diagram name, fixed identifier or label
selects a rule. Existing assertions and fixtures were unchanged. Four new
structural cases exercise incoming and outgoing links through two nesting
depths; they check every compound terminal, square approaches, distinct ports,
orthogonal interior runs and caption clearance from the surrounding bundle.
The focused Enterprise run passed 81 checks with the same two existing
quality-gate failures; the broader focused routing set passed all 15 checks.

All five suites ran once on pushed application commit `02e1610`: Layout 507
passed / 32 failed, rule validation 49 / 10, recognition 1025 / 0, Naiad adapter
47 / 0, converter 52 / 0. No new failing names appeared against `a406884`.
The retained master baseline was not rerun. The two stopped master-passing
Layout regressions in section 18 remain unchanged. Full rulebook and catalogue
acceptance remain incomplete.

Final evidence is under `out/deep-containers/final/`: logs/TRX, exact
failure-name comparison, the marked example, flowchart and Enterprise sample
renders, package inspection and the complete `picture-manifest.txt`.
The rebuilt WinForms executable saves the regenerated document to
`C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX-pr9\out\deep-containers\final\enterprise.docx`.
Supplied files remain untouched and nothing was merged.
Package inspection found 96 drawings and 34 source-preserving warnings. The
marked diagram's embedded image was extracted and viewed, confirming visible
cluster attachments, separated incoming arrows and a clear replication
caption. The remaining artifact path through its parent title is visible and
recorded above. The render set contains 126 flowcharts with four commented
skips and ten Enterprise samples with the same two expected rejections.
This does not establish full catalogue correctness or Word pagination acceptance.

## 34. Circular fans and crossing-free branch swaps — 10 October 2026

The marked expense-claim mesh exposed inward-looking split bends, overlapping
merge approaches and inverted near/far branch tracks. The owner confirmed the
route swap and directed a generic rule: swap when doing so removes crossings.
Shared routing now exchanges allocated branch ports only when rebuilt routes
remove the pair's crossings, do not increase crossings or shared runs against
any other connector, and retain node clearance. Relationship identities and
destinations stay intact. Ordinary boxes retain their allocated sides; circular
junctions may exchange distinct cardinal sides. Swaps on the same box side
remain available. Labels are placed after routing against the final paths.

Small circular split and merge fans centre on their peers when the rank has
space. Unbranched upstream and downstream tails follow when clear. Fans with
two or three distinct cardinal directions use radial terminal runs rather than
packed same-side slots. Larger fans and obstructed swaps retain their existing
routing; this is not a global crossing-free layout guarantee.

Application commits, both built directly on the previous work:
- `3ca494f` — Remove avoidable crossings between branch routes.
- `9e34f59` — Preserve allocated box sides when swapping branch ports.

No existing assertion or fixture changed. Ten new structural cases cover
incoming/outgoing swaps, rotated geometry, an external crossing that must block
a swap, protected rectangle faces, and three-way circular splits/merges.
The focused correction set passed all 19 checks. The first circular placement
attempt left the split on a boundary column; centring only the collector did
not fix it. Centring both fans and their clear unbranched tails resolved that
failure. The first full verification exposed one new bottom-to-top fan-side
regression. Preserving ordinary box sides fixed it without changing that check.
The first milestone's logs remain under `out/junction-fans/final/`.

All five suites then ran once on pushed application commit `9e34f59`: Layout
518 passed / 31 failed, rule validation 49 / 10, recognition 1025 / 0, Naiad
adapter 47 / 0, converter 52 / 0. No new failing names remain against `02e1610`.
The master baseline was retained, not rerun. The previously stopped group-border
clearance check now passes as a result of these generic changes; its bounded
investigation was not resumed. The stopped stroke-quality check remains the
one master-passing regression. Other prior acceptance failures remain recorded.

Engine changes were checked for diagram-specific selectors: fan connectivity,
shape, rank, free geometry, normals, intersections and overlap select the rules.
No catalogue number, diagram name, fixed identifier or text selects behaviour.
Supplied Markdown, Instructions files and editor settings were left untouched.
Nothing was merged.

Verified evidence is under `out/junction-fans/verified/`: full logs/TRX, exact
failure-name comparison, the marked picture, Enterprise and flowchart sample
renders, package inspection and `picture-manifest.txt` listing every image path.
The rebuilt executable saves the latest document to
`C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX-pr9\out\junction-fans\verified\enterprise.docx`.
The embedded marked image was extracted and viewed: split branches depart
outward, merge arrows use separate normal ports, and the policy-breach route
is above the under-limit route with no crossing. The package contains 96
diagrams and 34 source-preserving warnings. The sample render set contains
126 flowcharts with four commented skips and ten Enterprise pictures with
the same two expected rejections. This is not full catalogue acceptance or
Word pagination review. Exhausted caption corridors, container-title obstacles
and the remaining acceptance failures still require work.

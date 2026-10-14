# Layout rules as asserted by the tests

This file describes what the tests assert today, not what they should assert. `LAYOUT-RULEBOOK.md` is the authority. Where a test disagrees with the rulebook, the test is the defect.

The descriptions come from reading the test code (snapshot 13 Oct 2026), not from test names or comments. Several test names describe behaviour their assertions no longer check. Statuses use the same terms as `code_rules.md`: **Matches**, **Partial**, **Conflicts** and **No rule**.

## 1. Test projects, how they run, and checkers

### 1.1 Projects

| Project | Framework | Tests | Scope |
|---|---|---|---|
| `tests/MermaidRecognition.Layout.Tests` | NUnit | 403 methods (plus cases) | Grid layout, routing, rendering, enterprise, quality gate. This is where almost all layout rules are tested. |
| `tests/MermaidRecognition.RuleValidation.Tests` | NUnit | 77 | Rule-validation pipeline, the bend checker and its baseline, and catalogue runs over `flow.md`, `enterprise.md` and `EIP.md`. |
| `tests/MermaidRecognition.NaiadAdapter.Tests` | NUnit | 44 | Naiad renderer bridge, including the F61 two-by-two tests. |
| `tests/MermaidRecognition.Tests` | NUnit | 447 | Parser and recognition, mostly fixture snapshots (`*.verified.json`). These contain no layout rules. |
| `tests/MDConverter.Tests` | MSTest | 52 | Docx conversion, raster markers and sequence note spacing. |

All projects run under `dotnet test`. No test code lives in `src`. The checkers the tests call are in `src`, listed in 1.2.

### 1.2 Checkers and helpers

| Checker | Where | What it checks |
|---|---|---|
| `DiagramGridQuality.Collect` | `src/MermaidRecognition.Layout` | Whole-pixel coordinates, standard box sizes, line shape (perpendicular ends, one segment when aligned, no diagonals, at most 2 corners), node overlap, group frames, and Mermaid arrangement. |
| `FlowchartLayoutScore.Measure` | `src/MermaidRecognition.Layout` | Rule 2.13 score, crossings, forced minimum, bends, disconnected ends and hints. A null score means the candidate was rejected. |
| `UnnecessaryBendChecker` | `src/MermaidRecognition.RuleValidation` | Task 5 checks (a) to (j): fewer corners (a), cancelling pair (b), short side step (c), meaningfully shorter route of more than 30 px (d), crossing-avoidance bend (e), diamond branch (f), label jog (g), convergence (h), fan route (i) and avoidable crossing (j). |
| `FlowchartRenderedSvgQualityGate` | `src/MermaidRecognition.Layout` | Rendered-SVG defects: node intersection, overlaying edges, long diagonals, mixed style, border hugs, title strikes, corner attach, canvas margin, doglegs, shared stretches and clean crossings (at least 50°). |
| `FlowchartSharedRouteQualityHelper` | `src/MermaidRecognition.Layout` | Piercing, stacked arrowheads, label on own edge, hooks, group border clearance, parallel separation. |
| `FlowchartPerpendicularAttachValidator`, `FlowchartMarkerTangentValidator` | `src/MermaidRecognition.Layout` | Perpendicular terminal runs; marker tangent dot product of at least 0.98. |
| `MermaidRuleValidationPipeline` with `RulesEngine`, `ActualResultsGenerator` and the `Flowchart*FactsHelper` classes | `src/MermaidRecognition.RuleValidation` | Fact-based rules from the validation spec bundle (gaps, containment, markers, paths). |
| `OrthogonalRouteTestHelper`, `SvgStraightConnectorReader`, `DrawnEdgeLabelLocator` | `tests/MermaidRecognition.Layout.Tests` | Parse SVG paths and check that they are orthogonal with rounded corners; find labels. |
| `FlowchartEdgeRouteDiagnosticAnalyzer`, `FlowchartRoutingIntentPipeline` | `src/MermaidRecognition.Layout` | Route-class intent checked against `RoutingIntent/ruleset*.json`. |

## 2. Rules asserted by tests

Locations are relative to `tests/MermaidRecognition.Layout.Tests` unless another project is named.

### 2.1 Grid and spacing

| # | Rule asserted | Location | Values | Status |
|---|---|---|---|---|
| TG1 | Short labels keep the standard box; a long label grows every node by whole units. | `DiagramGridLayoutTests`: `ResolveStandardBox_ShortLabels_StayTheStandardSize`, `ResolveStandardBox_LongLabel_GrowsEveryNodeInWholeUnits` | 150 × 60, 30 px steps | Matches 1.3 |
| TG2 | A decision is standard width and twice the height. | `DiagramGridLayoutTests.TryLayout_Decision_IsStandardWidthAndTwiceTheHeight` | ×2 | Matches 1.3 |
| TG3 | Only the rank gaps that need offset routes or labels grow; the others keep the base gap. | `DiagramGridRankGapTests` (5 tests), `LocalLabelGapGrowthTests`, `FlowchartLabelAxisGapTests.Layout_VerticalSingleLineLabel_WidthDoesNotInflateRankGap` | base gap | Matches 1.5 |
| TG4 | An outside column grows as a whole without moving frame members. | `SharedFrameGapGrowthTests` | – | Matches 1.4, 7.2 |
| TG5 | A narrow facing gap grows a whole column before a dogleg is simplified. | `ForwardDoglegSimplificationTests.NarrowFacingGap_GrowsWholeColumnBeforeSimplifying` | – | Matches 1.7 |
| TG6 | A terminal same-rank target moves by the measured label room. | `Enterprise/SameRankRelationshipLabelGapTests.TerminalSameRankTarget_MovesByMeasuredLabelRoom` | – | Partial 6.1 (terminal targets only, as in the code) |
| TG7 | Sibling ranks keep a fixed pitch. | `CollectorInputPlacementTests` (`== 210`), `TerminalBranchPlacementTests` and `CollectorBranchPlacementTests` (`>= 210`) | 210 px hard-coded | Partial 1.4 (rulebook: tests never hard-code pixel sizes) |
| TG8 | Rank spacing settings and direction change the span. | `FlowchartSugiyamaLayoutEngineTests` (10 tests) | – | Matches 1.1 |
| TG9 | The base grid gives the fixed slot centres `{15,45,75,105,135}` and `{6,18,30,42,54}`, and one track sits at 15. | `DiagramGridLayoutTests`: `EqualCentres_StandardBox_UsesTheFiveSlotPattern`, `TrackOffsets_FollowTheCentreOutwardPattern` | 5 slots | Conflicts 3.2 (k/(n+1) positions) |

### 2.2 Placement, compaction, wrapping and scoring

| # | Rule asserted | Location | Values | Status |
|---|---|---|---|---|
| TP1 | Mermaid rank order, clusters and sides are preserved. | `FlowchartDagreSimilarityTests.Catalogue_MatchesMermaidRankOrderClusterAndSides`, `DagreLayoutParityTests` (6) | snapshot `DagreReference/reference.json` | Partial 2.1 (snapshot of the starting point, not of rule-based changes) |
| TP2 | The main chain stays in one column, and spines are straight and centred. | `DiagramGridLoopBackRouteTests.TryLayout_RoutingLoop_KeepsMainChainInOneColumn`, `CompactLayoutMetricTests.PicturedMainSpines_AreStraightAndCentred`, `DiagramGridF11StraightChordTests` | – | Matches 2.18 |
| TP3 | A detour rejoins from the side and the rejoin stays on the spine. | `DecisionDetourPlacementTests`, `DiagramGridBypassRejoinRouteTests` | – | Matches 2.6 |
| TP4 | Two terminal outcomes straddle the decision. | `TerminalDecisionPlacementTests`, `DecisionMergePlacementTests` | – | Matches 2.6 |
| TP5 | A loop and its continuation straddle the decision; the retry stays below. | `GroupedLoopBranchTests`, `RetryOutcomePlacementTests`, `HorizontalRetryReturnTests`, `DecisionInputPlacementTests` | – | Partial 2.7 (the "one rank lower" cell is not asserted) |
| TP6 | A merge decision is centred under the middle path. | `F125NodesFirstDiamondRenderTests.TryLayout_F125_MergeDecisionCentredUnderMiddlePath` | – | Conflicts 2.4 (first eligible parent, or the 2.10 source with the most lines) |
| TP7 | Independent inputs use neighbouring columns around their merge. | `IndependentFanInPlacementTests`, `CollectorInputPlacementTests` | – | No rule |
| TP8 | An independent terminal fan centres its parent on the median child. | `TerminalFanPlacementTests` | – | No rule |
| TP9 | Terminal children use the nearest free columns beside the straight continuation. | `TerminalBranchPlacementTests`, `CollectorBranchPlacementTests`, `TerminalRowPlacementTests` | – | Matches 2.17 |
| TP10 | A chain through a nested group keeps one coordinate. | `NestedChainAlignmentTests` | – | Matches 2.8 |
| TP11 | Complete partner ranks keep aligned drops, distinct ports and nested tracks. | `PartnerRankRoutingTests` (3), `PairedRankRoutingTests` | track pitch ≥ 6 px | Partial 2.10 (complete blocks only; 6 px is not a grid step) |
| TP12 | Empty strips close while alignment, reserved corridors and crossing bands are kept. | `GridCompactionTests` (3) | – | Partial 2.11 (no score test) |
| TP13 | Disjoint ranks reuse columns; junction branches close locally. | `SparseRankCompactionTests`, `JunctionBranchCompactionTests` | – | No rule |
| TP14 | Related container pairs are placed along the flow axis and compacted. | `ContainerCompactionTests` | – | No rule |
| TP15 | A long chain wraps near 4:3 without overlap and reserves label room. | `FlowchartChainWrappingTests` (2), `FlowchartWrapPlanTests` | 4:3 | Partial 2.12 (only isolated chains) |
| TP16 | Isolated group members pack across rows and columns. | `CompactLayoutMetricTests.A64_IsolatedGroupMembers_PackAcrossRowsAndColumns` | – | Matches 2.15 |
| TP17 | Disconnected rows keep declared order. | `FlowchartCatalogueRenderTests.TryLayout_F18_DisconnectedRows_FirstRowAboveSecond`; RuleValidation `FlowchartDisconnectedComponentOrderHelperTests` | – | Matches 2.15 |
| TP18 | Each bend adds 10 points and each disconnected end 20. | `FlowchartLayoutScoreTests`: `EachBendAddsTenPoints`, `DisconnectedRelationshipEnds_AddTwentyPointsEach` | 10, 20 | Matches 2.13 |
| TP19 | The crossing count ignores shared endpoints and handles many pairs. | `FlowchartLayoutScoreTests` (2) | – | Matches 2.13 |
| TP20 | Honoured hints are subtracted and hinted bends are counted. | `Enterprise/RoutingHintLayoutTests.Score_SubtractsHonouredHintsAndCountsHintedBends` | −30, −15 | Matches 2.21(g) |
| TP21 | The forced minimum comes from complete adjacent-rank blocks. | `CompactLayoutMetricTests.A110_ForcedMinimum_ComesFromItsCompleteAdjacentRankBlocks` | – | Matches 2.13 |
| TP22 | Catalogue scenarios produce a non-null score, but only for scenario numbers 154 and above. | `CompactLayoutMetricTests.CatalogueDiagram_ReportsCompactLayoutMetrics` (19 cases) | `Number >= 154` | Partial 2.13 (older scenarios may be rejected without failing) |
| TP23 | Relative node and group hints are honoured before routing, kept in source order, carried by later peer moves, and move groups whole. | `Enterprise/PlacementHintLayoutTests` (7) | – | Matches 2.21(b) to (e) |
| TP24 | Layout mode: explicit grid renders like the default; an unknown mode warns and falls back. | `LayoutModeTests`, `LayoutModeCatalogueTests` | – | Matches preamble |
| TP25 | Outside, grouped and intergroup leaves are placed beside their neighbours. | `ExternalLeafPlacementTests`, `GroupedLeafPlacementTests`, `IntergroupConnectorTests` | – | No rule |

### 2.3 Ports and diamonds

| # | Rule asserted | Location | Values | Status |
|---|---|---|---|---|
| TD1 | Diamond edge ports sit at the half, thirds, quarters and fifths; the vertices are the four tips; opposite edges mirror. | `FlowchartDiamondPortTests` (12) | – | Matches 3.6 |
| TD2 | Cardinal lines use only vertices; a second line takes the adjacent free vertex first; more than four lines redistribute evenly. | `FlowchartDiamondPortTests.TryAssign_*` | – | Matches 3.6 |
| TD3 | Offset decision branches leave the facing vertex with one corner. | `FlowchartDecisionBranchRouteRepairTests` (2), `DiagramGridDecisionBranchRouteTests` (7), `DiagramGridThreeWayDecisionRouteTests` (4) | – | Matches 3.8 |
| TD4 | The no branch is straight from the bottom vertex; the yes branch leaves the right vertex and enters the target's right side. | `DiagramGridLoopBackRouteTests` | `"right"` | Partial 3.8, 5.3 (fixed side for one fixture) |
| TD5 | Two branches use slots 1 and 3, and three use slots 1, 2 and 3. | `F124DecisionPortRenderTests` (2) | 5-slot pattern | Conflicts 3.2, 3.8 (the 5-slot pattern instead of vertices or k/(n+1)) |
| TD6 | F61 (A & B --> C & D): the cross edges are two-point straight routes from corner ports (source south-east, target north-west). | NaiadAdapter `TbTwoByTwoCompleteBipartiteRenderTests`: `Render_F61_AfterLayout_CrossEdges_UseTwoPointStraightRoutes`, `Layout_F61_GroupedTwoByTwo_UsesCentreVerticalAndCornerCrossRoutes`; `FlowchartPerpendicularAttachTests.ValidateAllVisibleEdges_F61_GroupedTwoByTwo_AllowsDiagonalCrossEdges` | – | Conflicts 4.1, 3.1, 2.10 (diagonals and corner endpoints; old two-by-two rule, now 5.5 removed) |
| TD7 | Fan ports use distinct facing slots; dense bipartite edges use distributed slots and never share terminal coordinates. | `WideFanPortTests`, `F111DenseBipartiteRenderTests`, `Enterprise/RoutingHintLayoutTests.FanHints_UseDistinctSouthAndNorthPorts` | – | Matches 3.1, 3.3 |
| TD8 | Lines end on the drawn outline of stadium, diamond, circle, cylinder and rounded shapes, and arrow tips touch the outline. | `FlowchartShapeBoundaryAttachTests` (6), `FlowchartCatalogueRenderTests` (F13, F22), MDConverter `FlowchartRasterMarkerTests.ExpandSvgFlowchartMarkers_F35MultiIncoming_ArrowTipsTouchDiamondOutline`, `Enterprise/EnterpriseRoundedPortTests`, `Enterprise/EnterpriseOutlineAndLabelTests` | – | Matches 3.1 |
| TD9 | Terminal runs are perpendicular, and markers follow the path tangent. | `FlowchartPerpendicularAttachTests.ValidateAllVisibleEdges_*` (catalogue batch), `FlowchartMarkerTangentTests` | dot ≥ 0.98 | Matches 3.1 |
| TD10 | A sloping diamond terminal keeps the surface normal, giving a diagonal terminal run of at least 50 px. | `SlopingDiamondTerminalTests.Normalise_SlopingEdge_KeepsSurfaceNormalAndAxisAlignedBody` | ≥ 50 | Conflicts 4.1 |
| TD11 | A stadium merge starts at centre source slots. | `FlowchartR1RenderGateTests` (F53A, F53Full) | – | No rule |
| TD12 | A three-way split and merge use separate cardinal junction ports. | `Enterprise/EnterpriseJunctionFanTests` | – | No rule |

### 2.4 Line shape and routing

| # | Rule asserted | Location | Values | Status |
|---|---|---|---|---|
| TR1 | Aligned chains and aligned ports draw one straight run. | `DiagramGridLayoutTests.TryLayout_AlignedChain_DrawsAStraightRun`, `FlowchartAlignedRouteStraightenerTests`, `F125...PathB_IsStraightVertical`, `FlowchartPerpendicularAttachTests.RenderToSvg_F35_PathB_IsStraightVertical`, `FlowchartEdgeRouteClassifierTests` (F58) | – | Matches 4.2 |
| TR2 | Routes are orthogonal with rounded corners, including decision branches whose test names still say "curved". | `OrthogonalRouteTestHelper.AssertOrthogonalPathWithRoundedCorners` in `FlowchartPerpendicularAttachTests` (F29, F30), `FlowchartGridCornerRenderTests`, `Enterprise/EnterpriseRenderTests.IndependentFan_RendersRoundedBends` | 20 px corners | Matches 4.1 (test names are misleading) |
| TR3 | The curve-basis builder emits smooth cubics, and the corridor path builder must contain a `C` curve rather than a linear polyline. | `FlowchartPerpendicularAttachTests.BuildCurveBasisPathData_*` (4), `FlowchartCorridorPathTests.BuildCorridorRoutePathData_UsesCurveBasisNotLinearPolyline`, `FlowchartEdgeGuidePointBuilderTests.BuildCurvedGuidePoints_WhenNotAligned_ProducesMoreThanTwoPoints`, `FlowchartAxisAlignedEdgeTests.OffsetEndpoints_KeepTheBasisWaypoints` | – | Conflicts 4.1 |
| TR4 | The rule-validation config reports `RequiresCurveBasisPaths = true` for a catalogue diagram. | RuleValidation `FlowCatalogueRuleValidationTests` (around line 185) | `true` | Conflicts 4.1 |
| TR5 | An aligned chain stays straight even when a fan is involved; a fan-out from a source is never a straight segment even if aligned. | `FlowchartEdgeGuidePointBuilderTests.ShouldUseStraightSegment_*` | – | Conflicts 4.2 (aligned ports must be straight) |
| TR6 | Bend-checker unit tests: straight when aligned, 1 corner for perpendicular sides, 2 for same-direction sides, zig-zags rejected unless blocked, hint routes accepted. | RuleValidation `UnnecessaryBendCheckerTests` (20) | – | Matches 4.2, 4.3, 4.8, 2.21(f) |
| TR7 | Bend checker: decision outer branch uses side vertex, one corner, top centre or facing side. | `UnnecessaryBendCheckerTests.DecisionOuterBranch_*` | – | Matches 3.8, 4.11 |
| TR8 | Bend checker: convergence uses a straight aligned feed and the facing side; block lines use block rules (2×3, 3×3, 4×2). | `UnnecessaryBendCheckerTests`: `Convergence_*`, `AdjacentRankBlocks_AreCheckedByBlockRules(2,3)(3,3)(4,2)` | – | Matches 4.11, 2.10 |
| TR9 | An offset source keeps the occupied top port and uses one corner into the facing side. | `FlowchartConvergenceRouteRepairTests` | – | Matches 4.11 |
| TR10 | The catalogue run (flow, enterprise, EIP) must not exceed the bend baseline. | RuleValidation `UnnecessaryBendCatalogueTests.EveryCatalogueDiagram_DoesNotExceedBendBaseline` | `UnnecessaryBendBaseline.txt`: 321 rows, one allowed fault `F136 d:L-Web-Index-0` | Partial brief (a) to (j) (see notes below) |
| TR11 | Back edges route orthogonally on side corridors, below all nodes for a horizontal retry, nested longest-outside. | `DiagramGridLoopBackRouteTests`, `HorizontalRetryReturnTests`, `NestedReturnCorridorTests`, `AlignedReturnPortTests`, `ReturnRouteOrderTests`, `FlowchartRetryLoopPathTests` | – | Partial 5.3, 5.4 |
| TR12 | Routes avoid unrelated bodies and frames. | `DiagramGridBypassObstacleTests` (4), `ObstacleRouteRepairTests`, `FlowchartPaddedVisibilityRouterTests`, `Enterprise/DeploymentCorridorTests`, `FlowchartSharedRouteQualityTests.TryResolve_Sample03_NoEdgePiercesNonEndpointNode` | – | Matches section 10 |
| TR13 | Forward simplifications remove wrong-side hooks and backward steps without moving endpoints. | `ForwardRouteSimplifierTests` (4), `ForwardDoglegSimplificationTests` | – | Matches 4.3 |
| TR14 | Line hints keep their sides and ports after enterprise track repair, may use extra corners, and travel picks the middle run. | `Enterprise/RoutingHintLayoutTests` (5), `Enterprise/PlacementHintLayoutTests.AgainstFlowLineHints_NeverRouteThroughEndpointBodies` | – | Matches 2.21(b), (f) |
| TR15 | A group endpoint meets its visible border; parent-to-nested-scope lines use at most one corner. | `Enterprise/EnterpriseDeepContainerRoutingTests`, `Enterprise/NestedScopeRouteTests`, `FlowchartLayoutFixTests.TryResolve_Sample03_CompoundEdgesClipToGroupBoundary` | – | Matches 4.9 |
| TR16 | Enterprise and flowchart share the same connector geometry. | `Enterprise/EnterpriseEdgeClearanceTests` (4) | – | No rule |

### 2.5 Tracks and crossings

| # | Rule asserted | Location | Values | Status |
|---|---|---|---|---|
| TT1 | A hop arc of radius 6 (`A6,6`) is drawn at each perpendicular crossing, including partner-rank crossings and both layout modes. | `CrossingBridgeTests` (6), `FlowchartRenderedSvgQualityGateTests.LineJump_DrawsOneHopAtAPerpendicularCrossing`, `LayoutModeTests.BothModes_BridgeCrossingsWithoutChangingConnectorRules` | radius 6 | Conflicts 4.1, 5.9, 2.10, section 10 |
| TT2 | Parallel returns nest ports and tracks without crossings. | `ParallelReturnTrackOrderTests` | – | Matches 5.2, 5.4 |
| TT3 | An internal back edge stays inside its frame. | `ContainedReturnTrackTests` | – | Matches 7.5 |
| TT4 | Opposing relationships use distinct straight tracks. | `Enterprise/AlignedCounterflowTests`, `NestedCounterflowTrackTests` (3) | – | Matches 5.1 |
| TT5 | Crossed fan ports are swapped only when other routes stay clear. | `FlowchartBranchRouteOrderTests` | – | Partial 5.9 (ports only) |
| TT6 | A complete 2×3 block (F136) uses only forced crossings and clear routes. | `FlowchartManyToManyRouteTests.Layout_F136_CompleteTwoByThreeBlockUsesOnlyForcedCrossingsAndClearRoutes` | – | Matches 2.10 (one layout case; 3×3 and 4×2 exist only as bend-checker unit tests) |
| TT7 | Every avoidable crossing is removed in A52. | `CompactLayoutMetricTests.A52_EnterprisePostRoutingPass_RemovesEveryAvoidableCrossing` | – | Matches 5.9 |
| TT8 | A crossed terminal branch moves to a clear track beyond the data row. | `Enterprise/TerminalFanBypassTests` | – | No rule |
| TT9 | Reversed hint rows nest tracks without crossings. | `FlowchartManyToManyRoutesTests` | – | Matches 2.10, 2.21 |
| TT10 | Parallel relationships between the same nodes keep distinct routes and captions. | `Enterprise/EnterpriseParallelRelationshipTests`, `FlowchartSharedRouteQualityTests.TryResolve_Sample03_WebShopParallelEdgesAreSeparated` | – | Matches 5.1 |

### 2.6 Labels

| # | Rule asserted | Location | Values | Status |
|---|---|---|---|---|
| TL1 | The stroke breaks around an edge label, and the break matches the text extent. | `FlowchartEdgeLabelGapTests`, `FlowchartLabelAxisGapTests.Render_StraightLabel_BreakMatchesTextExtentAlongConnector`, `Enterprise/EnterpriseRenderTests.LabelledStraightRelationship_HasTwoVisibleStrokeFragments`, `Enterprise/RelationshipLabelClearanceTests` | – | Matches 6.1 |
| TL2 | Labels are centred on their connector or on the node mid-span (F25, F26, F94, F125). | `FlowchartCatalogueRenderTests`, `F94EdgeLabelRenderTests`, `F125...DecisionBranchLabels_*` | within 1.5 px | Matches 6.1 |
| TL3 | A label may sit offset to one side of the connector at half the polyline distance. | `FlowchartPerpendicularAttachTests`: `ComputeOffsetLabelPosition*` (3) | `DEFAULT_OFFSET_PX=14` | Conflicts 6.1 (labels sit on their line) |
| TL4 | Enterprise labels on short parallel runs are staggered within the terminal clearance. | `Enterprise/EnterpriseRelationshipLabelLayoutTests` (2) | – | Partial 6.2 |
| TL5 | Bend checker (g): a label without one long straight run is a label jog. | `UnnecessaryBendCheckerTests.LabelWithoutOneLongStraightRun_IsRejectedAsLabelJog`, `LabelledRoute_IsNotShortenedToALineWithoutLabelRoom` | – | Matches 6.1, 6.2 |
| TL6 | A labelled self-loop chooses a side with label room. | `FlowchartSelfLoopLabelRouteRepairTests` | – | No rule |
| TL7 | The HTTPS label stays on its own edge in sample 03. | `FlowchartSharedRouteQualityTests` (2) | – | Matches 6.1 |

### 2.7 Groups and frames

| # | Rule asserted | Location | Values | Status |
|---|---|---|---|---|
| TF1 | The title stays clear of the box; a nested group sits inside the outer padding below the outer title. | `DiagramGridLayoutTests` (2), `PartnerRankRoutingTests.GroupedPartnerRanks_RenderTitlesClearOfEveryConnector`, `FlowchartRenderedSvgQualityGateTests.ConnectorToTitledGroup_KeepsTitleClearance` | – | Matches 7.1 |
| TF2 | Non-members stay outside; the cluster fits its members. | `FlowchartLayoutFixTests` (Sample03, Sample11) | – | Matches 7.3 |
| TF3 | Sibling and nested subgraphs keep a minimum exterior gap. | `F112NestedSubgraphGapTests`; RuleValidation `FlowCatalogueRuleValidationTests` (F36, F44, F46, F111, F112) | spec gap values (24 px) | Partial 7.3 (rule asks for 1 or 2 grid steps) |
| TF4 | Parallel grouped chains keep frames apart; sequential groups leave room for rounded border crossings. | `SiblingFrameClearanceTests` (2) | – | Matches 7.3, 4.6 |
| TF5 | A line leaves a group once, at a right angle. | `FlowchartGroupBoundaryCrossingHelperTests` | – | Matches 4.6 |
| TF6 | A wide title expands the canvas and centres the diagram. | `Enterprise/EnterpriseTitleBoundsTests` | – | Matches 7.1 |

### 2.8 Legality checks and the quality gate

| # | Rule asserted | Location | Values | Status |
|---|---|---|---|---|
| TQ1 | The quality gate detects listed defects in stored "known bad" and "current" pictures. | `FlowchartRenderedSvgQualityGateTests` (Known/Current/Visible tests) | fixture SVGs | No rule (tests of the checker, not of the layout) |
| TQ2 | Live pictures only report counts; the assertion is `Failures` not null. | `FlowchartRenderedSvgQualityGateTests.LivePictures_ReportUnjustifiedCounts` | – | Partial section 10 (no fault fails the test) |
| TQ3 | Sample 03: arrowheads do not stack, no hooks or disconnected routes, routes stay in bounds, border clearance kept, one routing style. | `FlowchartSharedRouteQualityTests` (14) | `ARROWHEAD_SEPARATION_PX=10`, `GROUP_BORDER_CLEARANCE_PX=12` | Matches section 10 |
| TQ4 | Routing intent of the torture fixture matches the ruleset. | `FlowchartRoutingIntentTests` | snapshot `RoutingIntent/ruleset*.json` | No rule (snapshot) |
| TQ5 | F58 has no structural routing defects; F35 and F125 report no centre-spine regression or unnecessary detour. | `FlowchartEdgeRouteDiagnosticTests`; RuleValidation `FlowMarkdownRuleValidationTests`, `FlowF34RuleValidationTests`, `FlowF58RuleValidationTests` | – | Partial section 10 (fixed scenarios) |
| TQ6 | Overlap detection ignores touching edges within tolerance. | RuleValidation `FlowchartLayoutOverlapHelperTests` | tolerance | Matches section 10 |
| TQ7 | A crossing counts as clean at 50° or more. | quality gate constant used by the gate tests | `CLEAN_CROSSING_MIN_DEGREES=50` | Conflicts section 10 (right angles only) |
| TQ8 | Both modes keep orthogonal routes and non-overlapping boxes. | `LayoutModeTests.BothModes_KeepOrthogonalRoutesAndNonoverlappingBoxes` | – | Matches 4.1, section 10 |

### 2.9 Renderer, legend and sequence

| # | Rule asserted | Location | Values | Status |
|---|---|---|---|---|
| TX1 | A positioned legend renders outside the main diagram and does not move elements; non-image modes draw no legend. | `Enterprise/EnterpriseLegendTests` (4) | – | Matches legend setting |
| TX2 | Sequence notes keep the same clearance above and below as between messages, and stay inside the canvas. | MDConverter `SequenceNoteSpacingTests` | equal within 0.5 px | Matches 8.1 |
| TX3 | Shapes, markers, styles, markdown labels and typography render as declared. | `FlowchartR1RenderGateTests`, `FlowchartCatalogueRenderTests` (shape tests), `Enterprise/EnterpriseRenderTests`, `Enterprise/EnterpriseStyleRenderTests`, `Enterprise/EnterpriseMarkdownRenderTests`, `Enterprise/EnterpriseConfigurationRenderTests` | – | No rule (syntax and rendering) |
| TX4 | An after-PNG exists for every after-SVG in the docs. | `FlowchartAfterDocsFreshnessTests` | – | No rule |

### 2.10 Notes on scenarios, pixels, baselines and skipped tests

- **Scenario-specific tests:** most layout tests are fixed to one catalogue scenario, for example F11, F13, F18, F29, F30, F35, F53A, F58, F61, F104, F111, F112, F124, F125, F136, A52, A54, A64, A72, A100, A110, A112, A117 and A168, or to sample 03. `FlowchartCatalogueRenderTests` (151 KB) is almost entirely per-scenario.
- **Hard-coded pixel values:** about 62 `Is.EqualTo(<integer>)` assertions. Examples are the 210 px pitch (TG7), the slot centres `{15,45,75,105,135}` (TG9), track pitch of at least 6 px (TP11) and a sloping terminal of at least 50 px (TD10). The rulebook (2.11, 2.12, 2.13) says tests compute sizes from measured layouts.
- **Baselines and allow-lists that permit faults:**
  - `UnnecessaryBendBaseline.txt` allows `F136 d:L-Web-Index-0`.
  - If the baseline file is missing, the test writes a new one and calls `Assert.Pass`.
  - A scenario whose layout fails is recorded with 0 lines and passes silently, which hides "engine gave up" cases (2.14).
  - The baseline lists A and F scenarios only; EIP diagrams are compared against an empty allow-list.
  - `CompactLayoutMetricTests` requires a score only for scenarios numbered 154 and above.
  - `LivePictures_ReportUnjustifiedCounts` asserts nothing about faults.
  - `DagreReference/reference.json` and `RoutingIntent/ruleset*.json` are snapshots.
- **Skipped and ignored tests:** there are no `[Ignore]` attributes. Ten fixture writers are `[Explicit]`. Fourteen recognition fixture tests call `Assert.Ignore` when their fixture has not been generated. These are parser tests only; no layout test is skipped. Two `Debug_Print*` tests in `FlowchartRoutingIntentTests` print output without real assertions.
- **Removed rules 3.4, 3.5 and 5.5:** no test cites them by number. The F61 tests (TD6) still encode the old two-by-two corner-crossing behaviour that 5.5 described before it was folded into 2.10.
- **Edge-midpoint ports (F35):** no remaining assertion requires edge-midpoint ports. The F35 tests assert straight path B, outline contact and marker tangents.

## 3. Summary tables

### 3.1 Conflicting tests

| Item | Rule | Test |
|---|---|---|
| TT1 | 4.1, 5.9, 2.10 | `CrossingBridgeTests` (6), `FlowchartRenderedSvgQualityGateTests.LineJump_DrawsOneHopAtAPerpendicularCrossing`, `LayoutModeTests.BothModes_BridgeCrossingsWithoutChangingConnectorRules` require hop arcs. |
| TR3 | 4.1 | `FlowchartCorridorPathTests.BuildCorridorRoutePathData_UsesCurveBasisNotLinearPolyline`, `BuildCurveBasisPathData_*` (4), `BuildCurvedGuidePoints_*`, `OffsetEndpoints_KeepTheBasisWaypoints` require curves. |
| TR4 | 4.1 | `FlowCatalogueRuleValidationTests` expects `RequiresCurveBasisPaths = true`. |
| TD6 | 4.1, 3.1, 2.10 | F61 tests require diagonal two-point cross edges from corner ports. |
| TD10 | 4.1 | `SlopingDiamondTerminalTests` requires a diagonal terminal run. |
| TD5, TG9 | 3.2 | `F124DecisionPortRenderTests`, `EqualCentres_StandardBox_UsesTheFiveSlotPattern`, `TrackOffsets_FollowTheCentreOutwardPattern` fix the 5-slot pattern. |
| TR5 | 4.2 | `ShouldUseStraightSegment_WhenFanOutFromSource/FanInToTarget_ReturnsFalseEvenIfAligned`. |
| TP6 | 2.4 | `TryLayout_F125_MergeDecisionCentredUnderMiddlePath`. |
| TL3 | 6.1 | `ComputeOffsetLabelPosition*` (3) place labels beside the line. |
| TQ7 | section 10 | The quality gate accepts 50° crossings. |

### 3.2 Rulebook rules with no test

| Rule | Note |
|---|---|
| 1.6 | Snapping order is not asserted. |
| 1.7, 5.7 | No test grows space in a loop until a legality rule holds; only single-step growth cases are tested. |
| 2.5 | The longest-path child below a branching parent is not asserted. |
| 2.11 | The coupled repair and strict score improvement are not tested. |
| 2.12 | Wide and deep wrapping of general rows and columns, re-compaction after wrapping. |
| 2.14 | The keep-best loop and the "never gives up" guarantee are not tested (the bend test hides layout failures). |
| 2.16 | Fan packing (`FlowchartFanPacking`) has no test. |
| 2.19 | Free direction. |
| 2.20 | Clusters. |
| 2.21(d) | Hint conflicts (circular peers, same port) are not asserted. |
| 2.22 | Notes are only rendered (`NoteAndPlainConnection_RenderAsDrawingItemsWithHints`); their placement is not asserted. |
| 3.9 | Mirror-image branches are not asserted. |
| 4.4 | The minimum straight run is asserted only through the bend checker's short side step (c). |
| 4.10 | Plain connections are only rendered. |
| 5.8 | Asserted only in bend-checker unit tests (`FanBetweenColumns_*`, `FanToNextRank_*`), not in layout tests. |
| 7.4 | Lone nested group centring. |
| 9.6 | No test checks the execution order. |

### 3.3 Rules tested only by snapshot or baseline

| Rule | Snapshot or baseline |
|---|---|
| 2.1 | `DagreReference/reference.json` (Mermaid rank and order parity) |
| Task 5 checks (a) to (j) on the catalogue | `UnnecessaryBendBaseline.txt` |
| Route intent classes | `RoutingIntent/ruleset.json`, `ruleset1.json`, `ruleset2.json` |
| Parser output | `*.verified.json` fixtures in `MermaidRecognition.Tests` |
| Quality-gate defect lists | stored `*.current.svg` and known-bad fixtures |

### 3.4 `code_rules.md` items enforced in code but untested

| Item | Code | Note |
|---|---|---|
| G2 | `MIN_LR_RANK_GAP_PX=49` | Not asserted. |
| G6 | `SeparateOverlaps`, 32-pass cap | Not referenced by any test. |
| G8 | `GrowCrowdedSides` | No direct test. |
| G12 | Enterprise fallback `PlaceSiblings`, `PlaceInGrid` | Not referenced. |
| P19 | `FlowchartFanPacking` | Not referenced. |
| P20, T7 | `FlowchartComponentWrapping`, `FlowchartExteriorCrossingRepair` | Not referenced. |
| P21 | `FlowchartDisconnectedPacking` | Only through F18. |
| P28 | `ApplyScoreGuardedTwoCornerRepair` and its skip when diamonds exist | Not referenced. |
| P29 | `FlowchartExternalNodePlacementSearch`, `TryLayoutSugiyama`, `ReduceSameRankInversions` | Not referenced. |
| R3 | `FlowchartTwoCornerRouteRepair` | Not referenced directly. |
| D9 | `FlowchartCircularJunctionLayout` | Only through the enterprise junction tests. |
| D11 | `FlowchartPortSideRouteRepair` | Only through A117. |
| T8 | `FlowchartWrappedFanRoutes` | Not referenced. |
| L2 | `MoveClearOfConnector` label move-off | Not referenced directly. |
| R12 | Many-to-many order relative to hints by direction | Not tested. |
| R14 | Enterprise repairs after hint restore | Only `RenderedEnterpriseRoutes_PreserveHintedSidesAndPortsAfterTrackRepair` covers it, for one fixture. |

### 3.5 Counts

Matches 56, Partial 16, Conflicts 11, No rule 14 (97 items in section 2). Ten conflict groups are listed in 3.1.

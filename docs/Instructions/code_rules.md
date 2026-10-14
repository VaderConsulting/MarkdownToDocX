# Layout rules as implemented in the code

This file describes what the code does today, not what it should do. `LAYOUT-RULEBOOK.md` is the authority. Where the two differ, the rulebook wins and the code is the defect.

The descriptions come from reading the code under `src/MermaidRecognition.Layout` and `tests/MermaidRecognition.Layout.Tests` (snapshot 13 Oct 2026), not from code comments. Statuses compare the code with the rulebook of the same date:

- **Matches** – the code does what the rule says.
- **Partial** – the code does part of it; the difference is stated.
- **Conflicts** – the code does something the rule forbids or contradicts.
- **No rule** – code-only behaviour that the rulebook does not describe.

Paths are relative to `src/MermaidRecognition.Layout` unless stated otherwise.

## 1. Execution order

### 1.1 Flowchart (`FlowchartSugiyamaLayoutEngine.TryLayout`)

1. `NativeRenderCapabilities.CanRender`. Unsupported contracts return false and the caller falls back to another renderer.
2. `FlowchartDagrePlacement.TryPlace`: the Dagre-style ranks and positions used as the starting point.
3. `DiagramGridLayout.Apply`, in this order:
   1. `ResolveStandardBox` sets one box for all nodes; diamonds get twice the height. In `open` mode each node keeps its measured size.
   2. `AssignRanks`, `AssignColumns`.
   3. Grid mode only: `ResolveVerticalChordBlockers`, `AlignForwardMainChainColumn` (unless the spine layout applies), `AlignFanInMergeColumn`, `AlignForwardMainChainColumn` again, `AlignTwoWayDecisionSideTargets` (spine layout only), `AlignBypassDetourColumns`, `AlignDecisionCentreChildColumn`, `AlignTerminalDecisionTargets`, `CentreTerminalFans`, `AlignSingleTerminalChildren`, `CascadeColumns`.
   4. `PlaceOnGrid` turns columns and ranks into pixels, using `BuildRankPairGapPlans`.
   5. Grid mode only: `ApplySpineColumnAdjustments` (TB/BT only); the terminal alignments again, now on coordinates; `AlignUnbranchedDecisionInputs`, `AlignHorizontalRetryOutcomes`, `AlignSimpleDecisionMerges`, `AlignDecisionDetours`, `AlignVerticalLoopBranches`; `FlowchartPartnerRankLayout.Find` and `Prepare`; `CompactIndependentFanIn`; `AlignSharedFanSourcesAndTails`; `FlowchartCircularJunctionLayout.AlignFans`. In open mode only `FlowchartPartnerRankLayout.Find` and `Prepare(Align: false)` run.
   6. `GrowCrowdedSides`, `GrowParallelRelationshipSides`, `SeparateOverlaps`.
   7. Grid mode only: `FlowchartTerminalBranchPlacement`, `FlowchartTerminalInputPlacement`, `FlowchartGridCompaction`, `FlowchartSparseRankCompaction`, `FlowchartJunctionBranchCompaction`, `FlowchartTerminalRowPlacement`, `FlowchartChainWrapping`, `FlowchartFanPacking`, `FlowchartComponentWrapping`, `FlowchartDisconnectedPacking`, `FlowchartGroupMemberPacking`.
   8. Frames: `CenterLoneNestedGroups`, `CompactOverlappingRootGroups`, `FitFrames`; `FlowchartPartnerRankLayout.GrowGaps`; `FlowchartGroupedLeafPlacement`; `FlowchartExternalLeafPlacement`; `FlowchartIntergroupNodePlacement`; `BuildFrames`.
   9. **`FlowchartPlacementHints.Apply`** (grid mode only). This is the first point at which node and group hints act, after all automatic placement, compaction and wrapping.
   10. `GrowTerminalSameRankLabelGaps`, `FlipToYUp`, `RestoreContractRouteEndpoints`, `EnsureBackEdgeRoutes`.
   11. `AssignPortSides` (ports).
   12. Route building: `DiagramGridRouteBuilder.RebuildAllRoutes`, `RebuildDecisionFanOutRoutes`, `RebuildSoleForwardDecisionBranchRoutes`, `RebuildOrdinaryFanOutRoutes`, `FlowchartForwardRouteSimplifier`, `FlowchartPartnerRankLayout.Route`, `RebuildSimpleDecisionMergeRoutes`, `RebuildDecisionDetourRoutes`, `RebuildLoopBackRoutes`, `DiagramGridRouteBuilder.RebuildDiagonalBackRoutes`, `FlowchartCircularJunctionLayout.RebuildFans`.
   13. Tracks and repairs: `SeparateTracks`, `SeparateParallelReturnTracks`, `PushPointsOutOfNodes`, `DiagramGridRouteBuilder.RebuildContainedBackRoutes`, `FlowchartGridTerminalNormalizer`, `GrowFacingDoglegGaps`, `DiagramGridRouteBuilder.RemoveClearForwardDoglegs`, `FlowchartBranchRouteOrder.RemoveAvoidableCrossings`, `FlowchartManyToManyRoutes` (TB/BT, grid mode), `FlowchartObstacleRouteRepair`, `FlowchartReturnRouteOrder`, `FlowchartNestedCounterflowTracks`, `FlowchartCrossGroupRouteSimplifier`, `FlowchartAlignedCounterflowRoutes`, `FlowchartTerminalFanBypass`, `FlowchartExteriorCrossingRepair` (only when component wrapping ran), `FlowchartWrappedChainRoutes`, `FlowchartWrappedFanRoutes`, `FlowchartAlignedRouteStraightener.Apply` (`ApplyNearlyAlignedFacing` in open mode), `FlowchartTwoCornerRouteRepair`.
   14. **`FlowchartRoutingHints.Apply`** (first pass, grid mode only, with automatic repair enabled), then `WriteBack`.
   15. More repairs, in grid mode: `FlowchartDecisionBranchRouteRepair`, `FlowchartConvergenceRouteRepair`, `FlowchartSelfLoopLabelRouteRepair`, `ApplyScoreGuardedTwoCornerRepair`, `FlowchartAlignedRouteStraightener.RepairSameSideDirectRoutes`, `ApplyOneCornerCandidates`, `ApplyCenteredFacing`. Then `FlowchartCircularJunctionLayout.RebuildFans` (both modes), `FlowchartPortSideRouteRepair` (grid mode) and `FlowchartManyToManyRoutes` (LR/RL, grid mode).
   16. **`FlowchartRoutingHints.Apply`** (final pass, `RepairAutomaticRoutesEnabled: false`), in both modes.
4. Rendering: `FlowchartSugiyamaSvgRenderer` draws the routes with `FlowchartCurvePathBuilder.BuildRoundedCornerPathData` and other path builders, places labels with `FlowchartEdgeLabelGapHelper` and `FlowchartEdgeLabelPlacementHelper`, and finally runs `FlowchartSvgLineJump.Apply`, which inserts hop arcs.

`FlowchartSugiyamaLayoutEngine.TryLayoutSugiyama` is a second path (MSAGL `LayeredLayout`, then `FlowchartLayoutPostProcessor`, `FlowchartLayoutCrossingSearch.ReduceSameRankInversions`, `FlowchartExternalNodePlacementSearch.Search`, then `ReduceSameRankInversions` again). It never calls `DiagramGridLayout.Apply`.

### 1.2 Enterprise (`EnterpriseSvgRenderer`)

1. `EnterpriseLayoutEngine.Layout` builds nodes, groups and notes and sizes them with `EnterpriseNodeSizer`.
2. `TryApplyFlowchartLayout` calls `EnterpriseFlowchartEdgeRouter.TryBuildRoutingContext`, which maps the document to a flowchart contract (`EnterpriseFlowchartContractMapper`) and runs `FlowchartSugiyamaLayoutEngine.TryLayout`, so all of section 1.1 runs.
3. If that fails, a non-grid fallback runs instead: `LayoutChildren`, `PlaceSiblings` (MSAGL; exceptions caught by `TraceSugiyamaFallback`), `PlaceInGrid`, `AssignAbsolute`.
4. `EnterpriseFlowchartEdgeRouter.Route`, for each relationship in drawing order:
   1. `FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints`, `FlowchartCompoundEndpointHelper.ReattachVisiblePorts`, conversion to SVG coordinates, `TryRouteInsideContainer` or `AttachCompoundEnd`.
   2. Path data from `BuildRoundedCornerPathData`, falling back to `BuildPathDataFromGuidePoints` (curves) when that is empty.
   3. Label centre from `FlowchartSugiyamaSvgRenderer.ResolveDrawnEdgeLabelCentre`, replaced by `FlowchartEdgeLabelGapHelper.TryResolveLabelSegmentBreak` when a run fits.
5. `SeparateVisibleTracks`: `DiagramGridLayout.SeparateTracks`, `FlowchartObstacleRouteRepair`, `FlowchartNestedCounterflowTracks`, `FlowchartCrossGroupRouteSimplifier`, `FlowchartAlignedCounterflowRoutes`, `FlowchartTerminalFanBypass`. These run again, after the shared stage's final hint pass.
6. `RestoreHintedRoutesAfterTrackRepair`: `FlowchartRoutingHints.Apply` (no automatic repair) restores the hinted routes.
7. `RepairRoutesAfterEnterpriseTrackSeparation`: `FlowchartBranchRouteOrder.RemoveAvoidableCrossings`, then `FlowchartPortSideRouteRepair.Apply`. Both run after the hints were restored and are not barred from changing hinted lines.
8. `EnterpriseRelationshipLabelLayout.Place` places the labels.
9. `FlowchartSvgLineJump.Apply` (hops), then `EnterpriseStyleSvg`, `EnterpriseMarkdownSvg` and `EnterpriseSvgConfiguration`.

### 1.3 EIP

EIP has no layout code of its own. Its flowcharts use the flowchart path in 1.1.

## 2. Rules in the code

### 2.1 Grid and spacing

| # | Rule in code | Location | Constants | Status |
|---|---|---|---|---|
| G1 | The grid unit is 30 px. The base box is 150 × 60, the node gap is 30, and pitch is box plus gap. | `DiagramGridMetrics` | `GRID_UNIT_PX=30`, `NODE_WIDTH_PX=150`, `NODE_HEIGHT_PX=60`, `NODE_GAP_PX=30` | Matches 1.2 |
| G2 | In LR flows the main pitch uses at least 49 px between ranks. 49 is not a grid multiple. | `DiagramGridLayout.Apply`, `DiagramGridMetrics` | `MIN_LR_RANK_GAP_PX=49` | Conflicts 1.4 (gap is not a whole number of grid steps) |
| G3 | All nodes share one box, grown to the next grid multiple for the longest label. Diamonds are twice the height. Circles do not force a square. | `DiagramGridLayout.ResolveStandardBox`, `DiagramGridMetrics.GrowBox` | `DIAMOND_HEIGHT_FACTOR=2` | Matches 1.3 |
| G4 | In `open` mode each node keeps its own measured size, and most placement passes are skipped. | `DiagramGridLayout.Apply` | – | Matches preamble (layout setting) |
| G5 | Coordinates are snapped with `CeilToGrid` and `RoundToGrid`. | `DiagramGridMetrics` | – | Matches 1.6 |
| G6 | Same-rank overlaps are pushed apart by at least one grid step, for at most 32 passes. | `DiagramGridLayout.SeparateOverlaps` | `MAX_SEPARATION_PASSES=32` | Conflicts 1.7, 2.14 (fixed pass cap) |
| G7 | Rank gaps are planned per rank pair. Only that pair grows for a label. | `DiagramGridLayout.BuildRankPairGapPlans`, `PlaceOnGrid` | – | Matches 1.5 |
| G8 | A box side carrying more than 5 lines makes the node grow. | `DiagramGridLayout.GrowCrowdedSides` | `TRACKS_PER_GAP=5` | Partial 3.2 (grows at a fixed count, not when the ports stop fitting) |
| G9 | A terminal target in the same rank moves outward by whole grid steps until a labelled straight line fits. Other nodes are never moved for this. | `DiagramGridLayout.GrowTerminalSameRankLabelGaps` | `CORNER_RADIUS_PX=20` added | Partial 6.1, 6.2 (terminal targets only) |
| G10 | Facing doglegs get wider gaps, but only when the diagram has no placement hints. | `DiagramGridLayout.GrowFacingDoglegGaps` | 2 × `CORNER_RADIUS_PX` | Partial 1.7 (switched off by any hint) |
| G11 | Enterprise elements are sized at 6.6 px per character, at least 120 × 55, wrapped at 22 characters. 55 is not a grid multiple. | `Enterprise/EnterpriseNodeSizer`, `EnterpriseLayoutEngine` | `MIN_WIDTH=120`, `ELEMENT_HEIGHT=55`, `MAX_CHARS=22`, `JUNCTION_SIZE=26` | Partial 1.3 (the grid box later replaces them on the shared path; the fallback keeps them) |
| G12 | The enterprise fallback places nodes with fixed 55/35 px separations, off the grid. | `Enterprise/EnterpriseLayoutEngine.PlaceSiblings`, `PlaceInGrid` | `LAYER_SEPARATION=55`, `NODE_SEPARATION=35` | Conflicts 1.2, 1.4 |

### 2.2 Placement, compaction, wrapping and scoring

| # | Rule in code | Location | Constants | Status |
|---|---|---|---|---|
| P1 | The starting ranks and order come from Dagre placement. | `FlowchartDagrePlacement.TryPlace` | `DAGRE_EDGE_SEPARATION_PX=20`, `SUBGRAPH_RANKSEP_EXTRA_PX=25` | Matches 2.1 |
| P2 | The forward main chain stays in one column, and back edges do not pull nodes sideways. | `DiagramGridLayout.AlignForwardMainChainColumn` | – | Partial 2.18 (no "strongest converging flow" choice) |
| P3 | The spine follows "Yes", then "No", by label text. Side branches go to margin columns. | `FlowchartSpineGraphHelper`, `FlowchartSpineColumnPostProcessor`, `DiagramGridLayout.ApplySpineColumnAdjustments` | `SPINE_COLUMN_TOLERANCE_PX=30`, `FAR_MARGIN_COLUMN_FACTOR=1.75`, collector threshold 3 | Partial 2.18, 2.6 (TB/BT only; label matching is English-only) |
| P4 | A child with several same-rank parents is centred on the middle parent. | `DiagramGridLayout.AlignFanInMergeColumn` | – | Conflicts 2.4 (first eligible parent, or the 2.10 source with the most lines) |
| P5 | A detour goes beside the spine, and the rejoin node stays on it. | `DiagramGridLayout.AlignBypassDetourColumns`, `AlignDecisionDetours` | – | Matches 2.6 |
| P6 | Two terminal outcomes go on opposite sides of their diamond. | `DiagramGridLayout.AlignTerminalDecisionTargets`, `AlignTwoWayDecisionSideTargets` | – | Matches 2.6 |
| P7 | A three-way decision keeps its centre child in its own column. | `DiagramGridLayout.AlignDecisionCentreChildColumn` | – | Matches 3.8 |
| P8 | A loop branch from a decision is placed beside it. | `DiagramGridLayout.AlignVerticalLoopBranches`, `AlignHorizontalRetryOutcomes` | – | Partial 2.7 (no check for the "one rank lower" cell) |
| P9 | A same-rank node that blocks a parent–child vertical chord swaps aside. | `DiagramGridLayout.ResolveVerticalChordBlockers` | – | No rule |
| P10 | An odd fan of terminal children centres the parent on the middle child. | `DiagramGridLayout.CentreTerminalFans` | – | No rule (close to 2.16) |
| P11 | Independent inputs are packed into neighbouring columns around their merge. | `DiagramGridLayout.CompactIndependentFanIn` | – | No rule |
| P12 | Complete many-to-many blocks (each source linked to the same distinct target set) are aligned with nested tracks. | `FlowchartPartnerRankLayout.Find`, `Prepare`, `GrowGaps`, `Route` | `TRACK_PITCH=6`, `TERMINAL_RESERVE=40` | Partial 2.10 (complete blocks only; targets are not chosen by most lines; 6 px track pitch is not a grid step) |
| P13 | Compaction removes empty strips without changing ranks, order or alignment, using a minimum gap of 60 px when groups exist. It is not scored. | `FlowchartGridCompaction.Apply` | gap `max(CrossPitch, 60 or 30)` | Partial 2.11 (no per-node moves, no strict-score check, no coupled repair) |
| P14 | Columns are reused across disjoint ranks in sparse grids. | `FlowchartSparseRankCompaction` | – | No rule |
| P15 | Unused space beside junctions is closed. | `FlowchartJunctionBranchCompaction` | – | No rule |
| P16 | Terminal branches and inputs go beside a parent or collector. | `FlowchartTerminalBranchPlacement`, `FlowchartTerminalInputPlacement`, `FlowchartTerminalRowPlacement` | – | Partial 2.17 |
| P17 | Wrap length is chosen by the ratio closest to 4:3, with a first run of at least 4 nodes. | `FlowchartWrapPlan.Choose` | `MinimumRun=4` | Partial 2.12 (chosen by ratio, not by score) |
| P18 | Only isolated simple chains of 5 or more nodes are wrapped. | `FlowchartChainWrapping` | `MinimumRun+1=5` | Partial 2.12 (general rows and columns are never wrapped) |
| P19 | A simple fan with 7 or more leaves and no groups is packed beside its hub. | `FlowchartFanPacking` | `MinimumFan=TRACKS_PER_GAP+1=6` (requires more than 6 nodes) | Partial 2.16 (fixed fan size; grouped fans skipped) |
| P20 | Long tails beneath a wide branch (4 or more children) are wrapped. | `FlowchartComponentWrapping` | `MinimumRun=4`, target 4:3 | No rule |
| P21 | Disconnected top-level pieces are packed in declared order. | `FlowchartDisconnectedPacking` | – | Matches 2.15 |
| P22 | Direct members of an isolated group are packed into a block. | `FlowchartGroupMemberPacking` | – | Matches 2.15 |
| P23 | Leaves are moved next to grouped neighbours, and connectors into the gaps between groups. | `FlowchartGroupedLeafPlacement`, `FlowchartExternalLeafPlacement`, `FlowchartIntergroupNodePlacement` | `IntergroupNodePlacement`: at most 32 steps | No rule; fixed cap conflicts 1.7 |
| P24 | Node and group hints are applied in source order, after all automatic placement. | `FlowchartPlacementHints.Apply` | – | Conflicts 9.6 (hints must come first) |
| P25 | Score = shape (0.4 per % off 4:3) + empty (2 per % up to 30 %, then 8 per %) + 20 per crossing above the forced minimum + 10 per bend + 20 per disconnected end − 30 per honoured node or group hint − 15 per honoured line hint. Overlaps and quality failures reject the candidate. | `FlowchartLayoutScore.Measure`, `Calculate`, `MeasureHints` | `BEND_PENALTY=10` | Matches 2.13, 2.21(g) |
| P26 | A line with more than two corners is not rejected when it is hinted, has a group end, or is a back edge with at most 3 corners. | `FlowchartLayoutScore.Rejects` | 3 | Partial 4.3 (exempts group ends, which the rulebook does not) |
| P27 | The score is used only inside three route repairs, not to choose between placement candidates. There is no candidate loop. | `ApplyScoreGuardedTwoCornerRepair`, `FlowchartPortSideRouteRepair`, `FlowchartConvergenceRouteRepair` | – | Partial 2.14 |
| P28 | The score-guarded two-corner repair is skipped whenever the diagram has any diamond. | `DiagramGridLayout.ApplyScoreGuardedTwoCornerRepair` | – | No rule |
| P29 | Outside nodes on the MSAGL path are placed by a weighted search. | `FlowchartExternalNodePlacementSearch` | overlap 1e6, invasion 1e6, pierce 5e5, crossing 5e4, foreign group 8e4; 3 passes | Conflicts 2.13 (separate weights); fixed passes conflict 2.14 |

### 2.3 Ports and diamonds

| # | Rule in code | Location | Constants | Status |
|---|---|---|---|---|
| D1 | Side ports use slot centres at (i+0.5)/n of the side, with at least 5 slots, and fixed patterns for 1 to 4 lines: slot 2; slots 1 and 3; slots 1, 2 and 3; slots 0, 1, 3 and 4. | `DiagramGridLayout` port allocation, `DiagramGridMetrics.EqualCentres`, `SelectSlotIndexes` | `TRACKS_PER_GAP=5` | Conflicts 3.2 (k/(n+1) positions; two lines should use the thirds) |
| D2 | One line can keep the centre while the others shift (`KeepsCentre`). | `DiagramGridLayout` port allocation | – | Partial 4.7 |
| D3 | Rounded boxes inset their ports from the corners. | same | `STUB_PX=15` | No rule |
| D4 | Ports on one side are allocated together, ordered by where the other end is. | `DiagramGridLayout.AssignPortSides` | – | Matches 3.2, 3.3 |
| D5 | A diamond has 4 vertices plus at least 3 evenly spaced ports per edge. | `FlowchartDiamondPorts` | `MINIMUM_EDGE_PORTS=3` | Matches 3.6 |
| D6 | Decision branch routes use side vertices, and the centre child takes the bottom vertex. | `DiagramGridLayout.RebuildDecisionFanOutRoutes`, `RebuildSoleForwardDecisionBranchRoutes`, `FlowchartDecisionPortAllocator` | `SIDE_BRANCH_CROSS_AXIS_EPSILON_PX=40`, `SAME_COLUMN_CROSS_AXIS_EPSILON_PX=30` | Matches 3.7, 3.8 |
| D7 | After the general passes, an offset branch is rebuilt as a facing-vertex route with one corner. | `FlowchartDecisionBranchRouteRepair` | `MinimumRun` from `CORNER_RADIUS_PX` | Matches 3.8, 3.9 |
| D8 | Sloping diamond edges may end in a diagonal terminal run along the surface normal. | `FlowchartGridTerminalNormalizer` | – | Conflicts 4.1 (a diagonal run) |
| D9 | Circular junctions get distinct cardinal ports. | `FlowchartCircularJunctionLayout` | – | No rule |
| D10 | Ports on group frames are inset from the frame corner and title. | `FlowchartCompoundEndpointHelper`, `FlowchartGeometryPolicy` | `CompoundPortInsetPx=15`, `CompoundTitleInsetPx=40`, `CompoundPortSpacingPx=15` | Partial 4.9 (fixed spacing instead of 3.2 positions) |
| D11 | Before accepting a bent ordinary line, other legal sides and slots are tried and kept if the score improves. | `FlowchartPortSideRouteRepair` | – | Matches 4.7, 5.6 |

### 2.4 Line shape and routing

| # | Rule in code | Location | Constants | Status |
|---|---|---|---|---|
| R1 | Routes are rebuilt from the assigned ports with obstacle-aware orthogonal routing. | `DiagramGridRouteBuilder.RebuildAllRoutes`, `FlowchartPaddedVisibilityRouter` | `OBSTACLE_PADDING_PX=10`, `BEND_PENALTY_FRACTION=0.04`, `PORT_STUB_PX=12` | Matches 4.1, 4.8 |
| R2 | Clear aligned routes collapse to one straight segment. | `FlowchartAlignedRouteStraightener.Apply`, `ApplyCenteredFacing` | – | Matches 4.2 |
| R3 | Routes with more than two corners are replaced by a clear route with at most two. | `FlowchartTwoCornerRouteRepair`, `ApplyOneCornerCandidates` | – | Matches 4.3, 4.8 |
| R4 | Back edges get placeholder routes, then loop-back routes; diagonal back guides are replaced when 3 corners or fewer fit. | `EnsureBackEdgeRoutes`, `RebuildLoopBackRoutes`, `RebuildDiagonalBackRoutes` | `LOOP_CORRIDOR_CLEARANCE_PX=20`, `MINIMUM_SIDE_STUB_PX=24` | Partial 5.3, 5.4 (side choice is not the downstream-side rule) |
| R5 | Unneeded backward steps between facing ports are removed. | `DiagramGridRouteBuilder.RemoveClearForwardDoglegs` | – | Matches 4.3 |
| R6 | Interior points inside unrelated nodes are pushed out. | `DiagramGridLayout.PushPointsOutOfNodes`, `FlowchartObstacleRouteRepair` | – | Matches section 10 (line through a node) |
| R7 | Convergence S-jogs are replaced by a one-corner route, kept only if the score does not worsen. | `FlowchartConvergenceRouteRepair` | `EPSILON=0.5` | Matches 4.11 |
| R8 | Forward approaches are shortened when no crossing or shared port results. | `FlowchartForwardRouteSimplifier` | – | No rule |
| R9 | Nested-group connectors use free facing ports, for at most 3 passes. | `FlowchartCrossGroupRouteSimplifier` | 3 passes | Partial 4.9; fixed passes conflict 2.14 |
| R10 | A labelled self-loop uses a box side with enough straight room. | `FlowchartSelfLoopLabelRouteRepair` | – | No rule |
| R11 | Wrapped chain pieces are joined by a two-corner Z. | `FlowchartWrappedChainRoutes` | – | Matches 2.12 |
| R12 | Dense adjacent layers route from distinct bottom ports to distinct top ports. This runs before the hint pass in TB/BT but after it in LR/RL. | `FlowchartManyToManyRoutes.Apply` | at least 2 edges | Partial 2.10 (block ordering is not set here; the order relative to hints depends on direction) |
| R13 | Line hints run twice: once after the track repairs, then again at the very end with automatic repair off. | `FlowchartRoutingHints.Apply` | – | Conflicts 9.6 (line hints must lead routing, not patch it afterwards) |
| R14 | Enterprise reruns track separation, crossing swaps and port-side repair after the shared hint pass. The last two run after the hints were restored. | `Enterprise/EnterpriseFlowchartEdgeRouter.Route` and helpers | – | Conflicts 2.21(e), 9.6 (a later pass can undo a hint) |
| R15 | Lines to or from groups attach to the frame, or to the inner edge for a member. | `FlowchartCompoundEndpointHelper.ReattachVisiblePorts`, `EnterpriseFlowchartEdgeRouter.TryRouteInsideContainer`, `AttachCompoundEnd` | – | Matches 4.9 |

### 2.5 Tracks and crossings

| # | Rule in code | Location | Constants | Status |
|---|---|---|---|---|
| T1 | Overlapping internal runs of different lines are translated apart by a track separation. | `DiagramGridLayout.SeparateTracks` | 5 tracks per gap (`TRACKS_PER_GAP`), offsets from `TrackOffsets` | Partial 5.1 (no gap growth when tracks run out; fixed 5 tracks per 30 px unit) |
| T2 | Parallel return tracks from one side are spread and reordered. | `DiagramGridLayout.SeparateParallelReturnTracks`, `FlowchartReturnRouteOrder` | needed = `CeilToGrid(n×pitch+30)` | Matches 5.4 |
| T3 | Internal returns stay inside their common frame. | `DiagramGridRouteBuilder.RebuildContainedBackRoutes` | – | Matches 7.5 |
| T4 | Opposing container and descendant connectors get distinct tracks. | `FlowchartNestedCounterflowTracks`, `FlowchartAlignedCounterflowRoutes` | offset ≥15, gap ≥20 | Matches 5.1 |
| T5 | Fan ports are exchanged only when that removes crossings safely. | `FlowchartBranchRouteOrder.RemoveAvoidableCrossings` | – | Partial 5.9 (ports only, not tracks or sides) |
| T6 | A crossed terminal fan branch moves to a clear track beyond the terminal row. | `FlowchartTerminalFanBypass` | – | No rule |
| T7 | A crossing route moves to an exterior track when that strictly reduces crossings, but only after component wrapping. | `FlowchartExteriorCrossingRepair` | – | Partial 5.9 |
| T8 | Inner rows of a wrapped fan use distinct tracks between leaf columns. | `FlowchartWrappedFanRoutes` | `MinimumFan=6` | Matches 2.16, 5.2 |
| T9 | Hop arcs of radius 6 are drawn where two lines cross, in flowchart and enterprise SVG. Tests assert them. | `FlowchartSvgLineJump.Apply`; tests `CrossingBridgeTests`, `FlowchartRenderedSvgQualityGateTests.LineJump_DrawsOneHopAtAPerpendicularCrossing` | `HOP_RADIUS_PX=6` | Conflicts 4.1, 5.9, 2.10, section 10 |
| T10 | The forced crossing minimum is computed per block. | `FlowchartLayoutScore.ForcedCrossingMinimum` | – | Matches 2.13 |

### 2.6 Labels

| # | Rule in code | Location | Constants | Status |
|---|---|---|---|---|
| L1 | A label goes on the longest straight run that fits its length plus 40 px of corner clearance (20 px when the run touches neither end). On a tie, the run nearer the source wins. The line is broken around the label. | `FlowchartEdgeLabelGapHelper.TryResolveLabelSegmentBreak` | clearance 2 × `CORNER_RADIUS_PX`, or 20; `LABEL_CHAR_WIDTH_EM=0.512` | Matches 6.1, 6.2 |
| L2 | When no run fits, the label is moved off the line by searching up to 90 px away, or moved along the flow by up to 60 px. No gap is grown. | `FlowchartEdgeLabelClearanceHelper.MoveClearOfConnector`, `FlowchartEdgeLabelPlacementHelper.ResolveEdgeLabelPosition`, `FlowchartSugiyamaSvgRenderer` | `MAX_SEARCH_DISTANCE_PX=90`, `MAX_ALONG_FLOW_SHIFT_PX=60`, `MAX_DETACHED_LABEL_GAP_PX=40` | Conflicts 6.1, 6.2 (gap should grow; label leaves its line) |
| L3 | A label far from its drawn path (more than 6.5 px) is pulled back onto its own line. | `FlowchartSugiyamaSvgRenderer` label centre | 6.5, 6.0 | Partial 6.1 |
| L4 | Enterprise labels are 12 px and junction captions 11 px, both with 6 px clearance. | `Enterprise/EnterpriseRelationshipLabelLayout`, `EnterpriseJunctionCaptionLayout` | `FontSize=12`, `11`; `Clearance=6` | Partial 6.1 (fixed clearance, no gap growth) |

### 2.7 Groups and frames

| # | Rule in code | Location | Constants | Status |
|---|---|---|---|---|
| F1 | Frames are fitted around their members with padding and a title band. | `DiagramGridLayout.FitFrames`, `BuildFrames`, `FlowchartSubgraphBoundsBuilder` | `PADDING_PX=18`, `TITLE_BAND_PX=26`, `NESTED_CLUSTER_GAP_PX=24` | Partial 7.1, 7.2 (18 and 26 are not grid steps) |
| F2 | A lone nested group is centred in its parent. | `DiagramGridLayout.CenterLoneNestedGroups` | – | Matches 7.4 |
| F3 | Overlapping root groups are compacted apart, and a non-member is moved out of a frame with at least one grid step of clearance. | `DiagramGridLayout.CompactOverlappingRootGroups`, `TryEvictNodeFromFrame` | – | Matches 7.3 |
| F4 | The external gap around a subgraph is 24 px. | `FlowchartSubgraphLayoutPostProcessor` | `SUBGRAPH_EXTERNAL_GAP_PX=24` | Partial 7.3 (rule asks for 1 or 2 grid steps) |

### 2.8 Legality checks

| # | Rule in code | Location | Constants | Status |
|---|---|---|---|---|
| Q1 | No fractional node or route coordinate. | `DiagramGridQuality.CheckWholePixels` | – | Matches section 10 |
| Q2 | Node sizes equal the standard box, or the diamond box. | `DiagramGridQuality.CheckSizes` | – | Matches 1.3 |
| Q3 | Lines leave and enter at right angles; aligned lines are one segment; no diagonals; orthogonal polylines; at most 2 corners. | `DiagramGridQuality.CheckLineShape` | – | Matches 4.1 to 4.3 |
| Q4 | No node overlap. | `DiagramGridQuality.CheckOverlaps` | – | Matches section 10 |
| Q5 | Every group has a frame that holds its members and excludes others. | `DiagramGridQuality.CheckGroupFrames` | – | Matches 7.3 |
| Q6 | Ranks and in-rank order match the Mermaid placement. Reported, but never rejects a candidate. | `DiagramGridQuality.CheckArrangement` | – | No rule (stricter than 2.1) |
| Q7 | The scorer's legality set does not check shared ports or tracks, label contact, minimum straight runs, corner endpoints, frame crossing angles, unequal rank gaps or avoidable crossings. | `DiagramGridQuality.Collect` | – | Partial section 10 |
| Q8 | The rendered-SVG gate checks arrowhead, parallel-line, border-hug, corner-attach, title, label and clean-crossing thresholds. It is used by tests, not by the layout. | `FlowchartRenderedSvgQualityGate`, `FlowchartSharedRouteQualityHelper` | `ARROWHEAD_SEPARATION_PX=10`, `PARALLEL_SEPARATION_PX=10`, `BORDER_HUG_PX=6`, `CORNER_ATTACH_PX=12`, `CLEAN_CROSSING_MIN_DEGREES=50`, `DOGLEG_STUB_PX=16` and others | Partial section 10 (a crossing at 50° passes; the rule demands 90°) |
| Q9 | Arrow and terminal tangents must line up within a dot product of 0.98. | `FlowchartMarkerTangentValidator`, `FlowchartPerpendicularAttachValidator` | `MIN_TANGENT_ALIGNMENT_DOT=0.98`, `FACE_TOLERANCE_PX=2.5` | Matches 3.1 |

### 2.9 Renderer

| # | Rule in code | Location | Constants | Status |
|---|---|---|---|---|
| X1 | Corners are drawn as cubic arcs of up to 20 px radius. | `FlowchartCurvePathBuilder.BuildRoundedCornerPathData` | `CORNER_RADIUS_PX=20`, handle 0.5523 | Matches section 10 (20 px corner) |
| X2 | Fan, stadium-merge, decision-branch and corridor builders fall back to basis curves and S-curves. These are still called by `BuildCorridorRoutePathData`, the MSAGL path, label sampling and rule validation. | `FlowchartCurvePathBuilder.BuildVerticalFanRoutePathData`, `BuildHorizontalFanRoutePathData`, `BuildCurveBasisPathData`, `BuildPureD3CurveBasisPathData`, `TryBuildStubBridgeCurvePathData`; `FlowchartDagrePresentation` | blend ratios 0.22 to 0.55, `RETRY_LOOP_CORNER_RADIUS_PX=28` | Conflicts 4.1 |
| X3 | Enterprise falls back to `BuildPathDataFromGuidePoints`, which draws a basis curve unless the style is "linear". | `Enterprise/EnterpriseFlowchartEdgeRouter.Route` | – | Conflicts 4.1 |
| X4 | Arrowheads are 10 × 7 px and overlap the outline by 1 px. | `FlowchartSugiyamaSvgRenderer` | `ARROWHEAD_LENGTH_PX=10`, `ARROWHEAD_WIDTH_PX=7`, `ARROW_TIP_NODE_OVERLAP_PX=1` | Partial 3.1 (the line end sits 1 px inside the outline) |
| X5 | The canvas margin is 10 px. | `FlowchartSugiyamaSvgRenderer` | `CANVAS_MARGIN_PX=10` | No rule |
| X6 | Diagram legends are drawn by separate renderers with fixed sizes. | `FlowchartLegendRenderer`, `Enterprise/EnterpriseNotationLegendRenderer` | `Width=360`; `BOX_WIDTH=196`, `COLUMNS=4` | Partial legend setting |

## 3. Summary tables

### 3.1 Conflicts

| Item | Rule | How it conflicts |
|---|---|---|
| P24 | 9.6, 2.21 | Placement hints are applied after all automatic placement, compaction and wrapping. |
| R13 | 9.6 | Line hints are applied after the route repairs and then again at the end. |
| R14 | 2.21(e), 9.6 | Enterprise reruns crossing and port-side repair after restoring hints, so a hint can be undone. |
| T9 | 4.1, 5.9, section 10 | Hop arcs are drawn at crossings, and tests require them. |
| X2, X3 | 4.1 | Curve and S-curve builders are still reachable. |
| D8 | 4.1 | Diagonal terminal runs on diamond edges. |
| D1 | 3.2 | Ports at (i+0.5)/n with fixed patterns, not k/(n+1). |
| P4 | 2.4 | A multi-parent child is centred on the middle parent. |
| L2 | 6.1, 6.2 | Labels are moved off the line instead of growing the gap. |
| G6 | 1.7, 2.14 | The overlap separation stops after 32 passes. |
| G2 | 1.4 | The 49 px LR rank gap is not a whole number of grid steps. |
| G12 | 1.2, 1.4 | The enterprise fallback layout is off the grid. |
| P29 | 2.13, 2.14 | The MSAGL path uses its own penalty weights and 3 passes. |

### 3.2 No-rule items

P9 chord blockers; P10 terminal fan centring; P11 independent fan-in compaction; P14 sparse rank compaction; P15 junction branch compaction; P20 component wrapping; P23 grouped, external and intergroup leaf placement; P28 two-corner repair skipped when diamonds exist; D3 rounded-box port inset; D9 circular junction ports; R8 forward simplifier; R10 self-loop side choice; T6 terminal fan bypass; Q6 arrangement check; X5 canvas margin.

### 3.3 Fixed caps and magic numbers

| Value | Where | Note |
|---|---|---|
| 32 passes | `DiagramGridLayout.MAX_SEPARATION_PASSES` (two loops) | Pass cap |
| 32 steps | `FlowchartIntergroupNodePlacement` | Step cap |
| 32 hops | `DiagramGridLayout` near line 4667 (`Guard < 32`) | Walk cap |
| 3 passes | `FlowchartCrossGroupRouteSimplifier`, `FlowchartExternalNodePlacementSearch` | Pass cap |
| 4 passes | `FlowchartDagrePresentation`, `FlowchartSharedEdgeRouter`, `FlowchartSharedPictureCleaner` | Pass cap |
| 2 passes or attempts | `FlowchartSharedEdgeRouter`, `FlowchartEdgeLabelClearanceHelper` | Pass cap |
| 6 iterations | `FlowchartSharedRouteQualityHelper` | Iteration cap |
| 5 slots per side or gap | `DiagramGridMetrics.TRACKS_PER_GAP` | Drives ports, tracks and fan thresholds |
| fan > 6, run ≥ 4, chain ≥ 5 | `FlowchartFanPacking`, `FlowchartWrapPlan`, `FlowchartChainWrapping` | Size thresholds (2.12 and 2.16 forbid node limits) |
| 49 px | `MIN_LR_RANK_GAP_PX` | Off-grid |
| 60 px | `DiagramGridLayout.Apply`, compaction gap when groups exist | Unexplained |
| 6 px | `FlowchartPartnerRankLayout.TRACK_PITCH` | Off-grid track pitch |
| 18, 24, 26 px | Frame padding, external gap, title band | Off-grid |
| 50° | `CLEAN_CROSSING_MIN_DEGREES` | Rule demands right angles |
| 90, 60, 40 px | Label search limits | Label search caps |
| 1e6, 5e5, 8e4, 5e4 | `FlowchartExternalNodePlacementSearch` | Separate weights from 2.13 |
| 120, 55, 22, 26 | `EnterpriseNodeSizer` | Off-grid sizes |

### 3.4 Scenario-specific code

No code branches on a scenario ID. Scenario numbers (F24, F29 to F35, F53, F59 to F61, F113 and others) appear only in comments, in `FlowchartCurvePathBuilder`, `FlowchartEdgeLabelPlacementHelper`, `FlowchartHorizontalRouteGuideBuilder`, the port allocators and `FlowchartSugiyamaSvgRenderer`, and in test names. Label text is matched as data in `FlowchartSpineGraphHelper` and `FlowchartSpineColumnPostProcessor`, which compare edge labels with "Yes" and "No". Several passes are written for one pattern and switch on narrow shape tests, for example `AlignHorizontalRetryOutcomes`, `FlowchartTerminalFanBypass` and the stadium-merge path builders.

### 3.5 Rulebook rules with no code

| Rule | Note |
|---|---|
| 2.11 coupled repair | No node or group move is tried to remove a crossing or hump. |
| 2.14 candidate loop | No keep-best loop over compaction and wrapping candidates. |
| 2.19 free direction | No trial of the four directions when none is declared. |
| 2.20 clusters | No cluster detection. |
| 1.7 and 5.7 general space insertion | Space is grown only in specific passes (G8 to G10, T2), never as a general retry-until-legal loop. |
| 8.1 sequence notes | Drawn by Naiad `SequenceRenderer`; not part of this layout code and not audited. |

### 3.6 Counts

Matches 40, Partial 31, Conflicts 14, No rule 15 (100 items in section 2, each counted once by its main status).

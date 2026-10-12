# Route normalisation proposal

The current pipeline is ordered and stateful. Several passes revisit the same
route after placement, port assignment or another repair has changed it. A
single normaliser should therefore be introduced only after each pass has an
explicit input contract and can be compared stage by stage. Merging them now
would risk changing output, which task 10 forbids.

| Pass | Purpose | Rule family | Merge decision |
| --- | --- | --- | --- |
| `FlowchartAlignedRouteStraightener` | Collapse a clear aligned route to one run | 4.2, 4.7 | Candidate for a straightening stage |
| `FlowchartForwardRouteSimplifier` | Remove needless forward approaches | 4.2, 4.3, 4.8 | Candidate for the same stage |
| `FlowchartTwoCornerRouteRepair` | Find a clear route with no more than two corners | 4.3, 4.8 | Keep after obstacle repair |
| `FlowchartObstacleRouteRepair` | Repair routes invalidated by moved boxes or tracks | 4.6, 5.1, 5.7 | Keep as the legality gate |
| `FlowchartPortSideRouteRepair` | Change terminal sides when obstacles force extra corners | 4.7, 4.8 | Keep after final placement |
| `FlowchartExteriorCrossingRepair` | Move a crossing to a clear exterior track | 5.5, 5.6 | Keep separate; score-dependent |
| `FlowchartCrossGroupRouteSimplifier` | Shorten routes across nested group frames | 4.6, 4.9 | Keep separate; group semantics |
| `FlowchartAlignedCounterflowRoutes` | Give opposing aligned edges distinct ports | 4.2, 4.7 | Candidate for port normalisation |
| `FlowchartWrappedChainRoutes` | Join wrapped chain rows with a two-corner Z | 2.12, 4.3, 4.5 | Keep separate; placement-specific |
| `FlowchartWrappedFanRoutes` | Route inner fan rows through distinct gap tracks | 2.16, 4.5, 4.7 | Keep separate; fan-specific |
| `FlowchartManyToManyRoutes` | Allocate dense adjacent ranks distinct ports and tracks | 2.18, 4.5, 4.7 | Keep separate; forced crossings |
| `FlowchartSharedPictureCleaner` | Remove border hugs, duplicate corridors and corner faults | 4.4, 4.6, 5.1 | Split by rule before any merge |
| `FlowchartGridTerminalNormalizer` | Restore square terminal attachment after track moves | 3.1, 4.4 | Candidate for final port normalisation |
| `FlowchartBranchRouteOrder` | Swap branch routes to remove avoidable crossings | 5.5, 5.6 | Keep as a scored ordering stage |
| `FlowchartReturnRouteOrder` | Order return tracks around forward routes | 5.3 to 5.6 | Keep separate; back-edge precedence |
| `FlowchartNestedCounterflowTracks` | Separate opposing tracks inside nested groups | 4.6, 4.9, 5.4 | Keep separate; group/back-edge rules |
| `FlowchartTerminalFanBypass` | Move terminal fan bypasses to a free outer side | 4.3, 4.8, 5.1 | Candidate for obstacle routing |
| `FlowchartCompoundEndpointHelper` | Reattach routes to visible compound and group frames | 4.7, 4.9 | Keep as endpoint normalisation |
| `FlowchartGroupBoundaryCrossingHelper` | Make group-boundary crossings square and clear | 4.6, 4.9 | Candidate for group endpoint stage |
| `FlowchartEdgeLabelClearanceHelper` | Move labels and routes until labels clear boxes and lines | 5.1, 6.1 to 6.3 | Keep separate; label geometry |
| `FlowchartPaddedVisibilityRouter` | Search clear orthogonal paths around padded obstacles | 4.1 to 4.4, 5.1 | Keep as the shared search primitive |

The eventual order should follow rule 9.6: apply hinted ports, allocate normal
ports, search for a legal path, normalise group endpoints, repair obstacles,
straighten, order crossings, then clear labels. Each stage should return a new
route candidate plus the rule that accepted or rejected it. That makes a future
merge testable without relying on the current incidental call sequence.

No classes were merged in this step. The safe first merge is the two aligned
straightening passes, but only after their intermediate route snapshots are
covered by tests; the catalogue's final metrics alone cannot prove that later
passes did not conceal a changed intermediate route.

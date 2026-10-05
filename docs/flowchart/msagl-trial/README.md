# Adopted MSAGL routing trial

This folder compares three engines on the same Mermaid sources. The default product path is unchanged.

| Folder | Engine |
| --- | --- |
| `custom/` | Product path: adopted Sugiyama node placement, then `FlowchartSharedEdgeRouter` |
| `msagl-rectilinear/` | `LayeredLayout` with clusters, then adopted `RectilinearEdgeRouter` |
| `msagl-spline/` | `LayeredLayout` with clusters, then adopted `SplineRouter` |

Profiles pick only router style, padding, arrowhead size, and line stroke. Flowchart and enterprise share that path. There is no ArchiMate painter on this branch, so `enterprise-03-enterprise` is the same Mermaid and the same drawing as `enterprise-03-flowchart`.

Regenerate with:

```text
dotnet run --project tools/FlowchartLayoutFixRender -- trial-msagl
```

Picture-gate counts (no justifications) are in `gate-counts.md`.

## Gate counts

`EvaluatePicture` on the stored SVG, 34 diagrams (23 catalogue subgraph fixtures, ten enterprise flowchart samples, and the web-shop diagram twice):

| Engine | Failures | Diagrams |
| --- | --- | --- |
| Custom shared router | 87 | 34 |
| Adopted rectilinear | 89 | 34 |
| Adopted spline | 90 | 34 |

Key diagrams:

| Diagram | Custom | Rectilinear | Spline |
| --- | --- | --- | --- |
| Web shop (03), both versions | 13 | 23 | 32 |
| Order suite (01) | 9 | 3 | 5 |
| Sales team (06) | 7 | 6 | 2 |
| Worker retry (F59) | 10 | 4 | 3 |
| Nested worker (F60) | 5 | 2 | 2 |

The adopted routers improve some ungrouped or lightly grouped trees. They make the web-shop diagram worse.

## What still looks wrong

### Web shop (sample 03)

The custom picture still has the defects the picture gate already records: Database Service through the orders table, crossings and overlays of LAN / HTTPS / load balancer, long diagonals out of the data centre, HTTPS sitting on PostgreSQL, and mixed straight and curved style.

The adopted pictures fail a more basic test. `LayeredLayout` places every leaf in a flat ranking. Cluster bounds are then fitted around those scattered members. The data-centre rectangle becomes a large wash that swallows nodes that do not belong in it (Database Service, Java Application Hosting, the orders table). The nested App Server Node box disappears into the same wash. Several edges are missing, stacked on one horizontal channel, or labelled in the wrong place (HTTPS under the group, or on the load balancer). Spline routing then draws long diagonals across that wash, which is why the gate count rises from 13 to 23 and 32.

### Order suite (sample 01)

The custom picture still has the rank gap, long diagonals, and the “order details” label sitting on Checkout.

Rectilinear and spline tighten the ranks and remove most of those long jumps. What remains is the “order details” label sitting on Order Placed, an unlabelled Order box at the bottom, and a long drop from the suite to Order Synchronisation.

### Sales team (sample 06)

The custom picture still has Fulfil hugging Selling and a cramped Clerk to Seller arrow.

Rectilinear separates Fulfil but uses long shared orthogonal channels. Spline is the cleanest of the three (two gate failures). Fulfil still looks detached, and Shop Front to Selling is a long diagonal.

## Recommendation

Do not replace the custom router as the default.

The adopted routers can avoid node obstacles and can cross cluster boundaries when the cluster geometry is already correct. They cannot do the thing this product needs first: keep subgraph members packed inside their groups. Adopted `LayeredLayout` is a flat Sugiyama. It does not run a clustered layout. `InitialLayoutByCluster` exists in the adopted tree and was not used here, because the trial asked for `LayeredLayout` plus the two routers.

The adopted routers also cannot:

- Draw the Mermaid shape catalogue. This trial paints rectangles only.
- Honour the specification’s decision-branch ports, fan-out slots, corridor and bypass styles, or shape-aware attach for diamonds and cylinders.
- Keep Word-first edge style (smooth curves with straight aligned connectors, not MSAGL orthogonal channels or unconstrained splines).
- Paint ArchiMate enterprise diagrams. That painter is not on this branch.

A later trial that kept the existing subgraph placement and only swapped the final route step for `SplineRouter` or `RectilinearEdgeRouter` would test the routers more fairly. Using the adopted routers as a drop-in for both placement and routing is not good enough for grouped diagrams.

## Custom routing code that would be deleted

If MSAGL routing were adopted, these product files would go (the SVG renderer would keep node painting and drop the custom path builders):

- `FlowchartSharedEdgeRouter.cs`
- `FlowchartSharedRouteQualityHelper.cs`
- `FlowchartEdgeRouteResolver.cs`
- `FlowchartEdgeRouteClassifier.cs`
- `FlowchartEdgeRouteStrategyRegistry.cs`
- `FlowchartEdgeSemanticRouteClass.cs`
- `FlowchartRoutingIntentRouteClass.cs`
- `FlowchartCurvePathBuilder.cs`
- `FlowchartCurveBulgeHelper.cs`
- `FlowchartEdgePathBuilder.cs`
- `FlowchartGroupedVertexEdgePathBuilder.cs`
- `FlowchartEdgeGuidePointBuilder.cs`
- `FlowchartVerticalRouteGuideBuilder.cs`
- `FlowchartHorizontalRouteGuideBuilder.cs`
- `FlowchartPerpendicularRouteBuilder.cs`
- `FlowchartPerpendicularAttachValidator.cs`
- `FlowchartCentreSpineRouteBuilder.cs`
- `FlowchartSpineCorridorHelper.cs`
- `FlowchartSkipBypassRouteBuilder.cs`
- `FlowchartMergeBackRouteBuilder.cs`
- `FlowchartLocalDecisionBranchRouteBuilder.cs`
- `FlowchartLongExceptionRouteBuilder.cs`
- `FlowchartLoopBackRouteHelper.cs`
- `FlowchartCrossSubgraphRouteHelper.cs`
- `FlowchartFanOutPortAllocator.cs`
- `FlowchartFanInPortAllocator.cs`
- `FlowchartDecisionPortAllocator.cs`
- `FlowchartDecisionPortRole.cs`
- `FlowchartHubSidePortHelper.cs`
- `FlowchartParallelBranchHubPortHelper.cs`
- `FlowchartMultiIncomingTargetPortAllocator.cs`
- `FlowchartTerminalCollectorPortAllocator.cs`
- `FlowchartEdgeLabelPlacementHelper.cs`
- `FlowchartEdgeLabelClearanceHelper.cs`
- `FlowchartShapeBoundaryAttachHelper.cs`
- `FlowchartDiamondAttachHelper.cs`
- `FlowchartEdgeAttachFace.cs`
- `FlowchartEdgeAttachConstants.cs`
- `FlowchartEdgeRouteDiagnosticAnalyzer.cs`
- `FlowchartEdgeRouteDiagnosticFinding.cs`
- `FlowchartEdgeRouteDiagnosticKind.cs`
- `FlowchartEdgeRouteDiagnosticReportFormatter.cs`

`FlowchartSugiyamaSvgRenderer` would keep node and group painting. `FlowchartSugiyamaLayoutEngine` would stay if node placement stayed native, or it would be replaced by the clustered MSAGL layout if that later trial succeeded. The quality gate can stay: it reads the finished SVG.

# Exact relationship connection points

Enterprise endpoint hints accept an optional signed grid step after their side:

```mermaid
enterprise
application-component source
application-service target below source +3
source -serving-> target [leave S@-2, enter N@1]
```

`@0` selects the centre of the side's connection segment. Negative steps go towards its start (left for N/S, top for E/W); positive steps go right or down. Each step is the rulebook connection spacing, independent of the number of attached relationships. The renderer's connection boundary owns the usable points, including rounded sides. A point is not an arbitrary pixel location or a fraction of a bounding box. Existing side-only hints retain their automatic allocation. `travel` accepts a direction only.

The shared `FlowchartConnectionPoints` inventory provides layout coordinates for snapping and verifies explicit requests. Initial placement, joint allocation, final slot allocation, routing-hint restoration, source classification, detached copies, identity reconciliation and SVG cache identity preserve the selected steps. Endpoint-changing side repairs cannot replace explicit points with side centres. Native source save/reload retains the hint. Archi relationship-hint persistence is not yet implemented.

Retained edits validate point availability and conflicting explicit reservations before routing and validate exact attachment again before publication. Unsupported shape inventories, frame endpoints, missing sides, out-of-range steps, collisions and local routing failures reject the transaction while preserving accepted source/geometry. Invalid initial requests produce a routing diagnostic instead of claiming the point was honoured. Automatic neighbours share the remaining point inventory and enter the affected set when their terminals change. The existing local route budget still applies; no initial placement or global routing fallback is allowed.

The first inventory covers rectangular and renderer-owned flat/rounded sides of regular nodes. Diamond/circle inventories and exact frame points require their own geometry representation and are not exposed as eligible targets yet. Mouse gestures are the next integration task; this commit supplies their engine/source prerequisite.

Regressions cover signed and zero steps, invalid syntax/overflow, exact retained movement and untouched component geometry, conflicting/unavailable points, initial component/service/data-object boundaries and invalid initial diagnostics. Current commands and results are recorded in [the plan](incremental-diagram-layout-plan.md).

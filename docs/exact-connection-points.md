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

With AF enabled and a current Enterprise grid preview, select a relationship and drag either circular endpoint handle. Crosses show nearby eligible actual points, and the highlighted circle is the proposed destination. The dashed preview uses orthogonal segments; the committed relationship remains visible throughout the gesture. Release near an eligible point writes one complete source declaration through the same retained worker as typing. Release elsewhere cancels. A drop at the original point makes no edit. Escape, capture/focus loss, source/selection/view changes and document/options changes cancel. The preview image, export bytes and camera are not changed during pointer motion.

The gesture can move a point on its existing node or reconnect that endpoint to another eligible node. It pins the opposite endpoint at its existing point, preserves the label, relationship kind, qualifier, arrow length and travel hint, and submits one undoable source replacement. Semantic rules, self-loop restrictions, duplicate declarations and other explicit point reservations determine eligibility; display labels have no capability semantics. Plain note connections keep their plain syntax and must retain a note at one or both ends. Stable relationship identity restores selection after reconnect and source undo/redo. Reconnect plus a point change is reconciled only when the remaining declaration and one shared endpoint identify a unique prior relationship; ambiguous siblings reject instead of being guessed.

The first gesture inventory covers ungrouped rectangular/rounded regular nodes and notes where the renderer exposes usable side points. Diamond/circle inventories, exact frame points and grouped endpoints require their own geometry representation and are not exposed as eligible gesture targets yet. AF-off, explicit open-layout, stale and legacy previews offer no endpoint gesture. [Relationship add/delete commands](relationship-commands.md) use the same source worker and actual point inventory.

Regressions cover signed and zero steps, invalid syntax/overflow, exact retained movement and untouched component geometry, conflicting/unavailable points, initial component/service/data-object boundaries and invalid initial diagnostics. Current commands and results are recorded in [the plan](incremental-diagram-layout-plan.md).

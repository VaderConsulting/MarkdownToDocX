# Placement without a reference peer

An ungrouped regular Enterprise element can use `at <row> <column>` to pin its top-left occupied logical cell. Coordinates are signed integers; the node keeps its measured row and column spans.

```mermaid
enterprise
application-component root "Root" at -2 5
application-service dependent "Dependent" below root +1
root -serving-> dependent
```

With AF enabled, node dragging still prefers cardinal and quadrant hints against independent same-layer peers. If no peer remains after excluding hint cycles, it projects the nearest row and column directly from the retained grid, including expanded gaps and multi-cell spans, and writes an `at` hint on release. This allows a lone node or a placement anchor to start a drag and show its dashed outline. Gesture projection is bounded to 10000 cells per axis from the current origin. Display labels never determine drag eligibility, peer selection or routing behavior; stable identities, kinds, containment and hint dependencies do.

The shared source operation moves existing relative-hint dependents with the anchor and routes its dependency closure. Unrelated geometry remains fixed. Occupied cells, semantic violations and routing failures reject the transaction without initial placement or a global routing fallback. Connections retain horizontal and vertical segments with bends. The gesture covers the viewport canvas, including space beyond the bitmap.

Initial grid placement reserves explicit cells before placing automatic nodes; overlapping explicit pins fail. The Enterprise renderer selects cell geometry for this syntax even if its legacy cell switch is disabled. Group, note and grouped-element pins remain unsupported. Completion, help, detached copies, Archi properties/source and SVG cache identity preserve the coordinates. The SVG cache also now includes quadrant horizontal offsets, fixing reuse after a horizontal-only hint change.

LiveEditor remembers each first hint's pre-hint origin in the retained session. Undo removes the hint, restores that origin and resolves dependents; redo returns to the same moved geometry. This also covers typed edits coalesced with comments. A fresh session containing a pre-existing hint has no earlier origin to restore. See [diagram-source-updates.md](diagram-source-updates.md) for the shared service's opt-in and default removal behavior.

Validation covers the default sample, an anchor with only dependent peers, a single-node diagram, renamed and absent display labels, first-hint undo/redo, initial pin reservation, local routing/collision rollback, negative coordinates, Archi persistence and geometry-cache isolation. Worker tests use the existing WinForms handle/message-pump harness; this does not claim manual full-editor review or large-diagram performance measurements. Commands/results are recorded in [the implementation plan](incremental-diagram-layout-plan.md).

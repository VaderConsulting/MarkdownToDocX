# Placement in two axes

Enterprise declarations accept `NW`, `NE`, `SE` and `SW` relative placement hints. These select the node's destination quadrant. Relationships continue to use horizontal/vertical segments and bends; these hints do not introduce 45-degree relationship segments or constrain a drag to a 45-degree ray.

```mermaid
enterprise
application-component anchor "Anchor"
application-component other "Other" SE anchor +2 +5
anchor -flow-> other
```

The first offset is the vertical gap in whole logical cells; the second is the horizontal gap. `SE anchor +2 +5` places the owner's region below the peer with two empty rows between them and to its right with five empty columns between them. The owner's existing row/column spans are retained. `NW`/`NE` use the peer's upper boundary; `SW`/`SE` use its lower boundary. `NW`/`SW` use its left boundary; `NE`/`SE` use its right boundary.

Both offsets are nonnegative. With no offset, both gaps are zero. With one offset, it applies to both axes: `NW anchor +2` means two rows and two columns. With two offsets, either can be zero: `NE anchor +0 +3`. Direction tokens are case insensitive. Existing `above`, `below`, `left-of`, `right-of` and `push N/S/E/W` syntax is unchanged. Relationship port hints remain `N`, `S`, `E`, `W`.

With AF enabled, an eligible node drop evaluates all four cardinal and four quadrant placements against same-layer peers. For quadrant candidates it snaps rows and columns independently against the retained grid, including expanded gaps and multi-cell spans. The generated hint writes both offsets when they differ, in one undoable source edit. This removes the cardinal-only destination restriction; grid snapping and hint-peer/cycle rules still apply. When no independent peer is available, including a lone node or an anchor whose peers depend on it, the gesture uses `at <row> <column>` instead. Grouped node gestures remain unsupported. Display labels do not determine capability or placement. See [anchor-free-placement.md](anchor-free-placement.md).

Typed hints and generated hints use the same source update transaction and local routing dependency closure. A supported local failure retains source and the accepted picture with its stale-preview reason, without initial placement or a whole-diagram routing fallback. Undo/redo uses the same path. Initial layout also understands these hints, so reparsing or reopening retains their relative placement intent. Existing structural changes still require an identified rebuild.

The placement contract stores the vertical gap as `Offset` and an optional horizontal gap as `ColumnOffset`; omission means use `Offset` on both axes. Retained input copies, source classification, hint checks and Archi property/source round-trips preserve the second offset. Completion and help offer the new tokens. Existing syntax and experiment defaults are unchanged.

Broader editor validation also repairs the help/selection bugs it exposes: element hover help omits insertion-only examples and relationship lists; identifier help checks declarations before the current line even when the parser merges repeated declarations; node hover uses the complete element relationship story. Rendered non-Enterprise hit indexes bind source metadata only to supplied edge geometry, without adding invisible selectable nodes or duplicate edge objects. Source-only item construction still retains its complete inventory.

# Unified layout engine - design

**Status:** Design only. No implementation in this document.  
**Branch:** `flowchart-layout-fixes`  
**Date:** 5 October 2026  
**Audience:** Anyone extending MarkdownToDocX layout

**Goal:** **one configurable layout engine** in MarkdownToDocX that understands every diagram type, instead of a separate layout per type.

**Extra rule:** wherever diagram types overlap - containment, ranking, edge routing, arrowheads, label placement, text measuring, padding, colours, and fonts - there is **exactly one implementation and one set of decisions**, shared by every type. No duplicated or parallel code paths for the same job. A per-type profile may only **choose options** from that shared code (spacing, routing style, marker set, font size). It must not reimplement the job, copy the formula, or keep a private helper that does the same work.

This note surveys what the product renders today, lists every current duplication of those overlapping jobs (file paths and the single replacement), and how to grow the native flowchart Sugiyama path into that engine without breaking existing pictures. The gate for every stage is **before/after renders**, the same way `docs/flowchart/layout-fixes/` proved the shared flowchart fixes.

---

## 1. What markdown2docx renders today

There are **two live render paths** plus one forthcoming consumer.

| Path | When it runs | Layout |
| --- | --- | --- |
| **Native flowchart Sugiyama** | Default for `flowchart` / `graph` fences (`MermaidOptions.UseNativeSugiyamaRenderer`, default true) | `FlowchartSugiyamaLayoutEngine` → post-processors → `FlowchartSugiyamaSvgRenderer` |
| **Naiad (legacy)** | Every other recognised Mermaid kind, and flowchart when native is off or fails | Per-type `*Renderer` in `extern/Naiad`. Graph kinds call `DagreLayoutEngine`; the rest pack their own geometry |
| **Enterprise (PR #1, not on this branch)** | `enterprise` fences on `enterprise-diagram` | Maps ArchiMate compounds onto flowchart subgraphs and **must keep using the flowchart layout and routing exactly** |

DOCX embedding (`DocxWriter.RenderMermaidSvg`) tries the recognition flowchart bridge first, then `Mermaid.Render` (Naiad). Native render eligibility (`NativeRenderCapabilities`) is flowchart-only for a real SVG today; sequence, class, state, ER, pie, gantt, mindmap, and gitGraph are listed as “can attempt” but have **no native Sugiyama renderer** yet.

Recognition detects more kinds than Naiad draws. Detectors exist for `eventmodeling`, `treeView`, `ishikawa`, `venn`, `wardley`, and `info`. Those fences fail at `Mermaid.DetectDiagramType` and fall back to a code block in the DOCX. They are out of scope until Naiad or a native renderer exists.

---

## 2. Survey: every type that actually draws

### 2.1 Share a graph layout today

These are **node + edge** diagrams. They already think in ranks, order, and connectors. They are the natural clients of a unified engine.

| Type | Code that draws | Layout today | Routing today | Containment |
| --- | --- | --- | --- | --- |
| **Flowchart** (native, default) | `src/MermaidRecognition.Layout` | Adopted Sugiyama (`LayeredLayout`) + `FlowchartLayoutPostProcessor` + `FlowchartSubgraphLayoutPostProcessor` | Orthogonal visibility-graph polyline for every edge; two aligned ports stay a straight right-angle run; compound edges clip to the cluster rectangle | Subgraphs are clusters. Dummy group-id nodes are omitted. Non-members stay outside the box |
| **Flowchart** (legacy) | `Naiad` `FlowchartRenderer` + `DagreLayoutEngine` | Naiad Dagre (acyclic → rank → order → coordinates). Compound subgraphs are a **bottom-up approximation**, not a real compound graph | Dagre route points + a long list of flowchart-only snaps (retry loops, stadium merge, terminal collector, cross-boundary) | Recursive per-subgraph layout, then pack |
| **Class** | `Naiad` `ClassRenderer` | Convert to a `GraphModel`, then **same** `DagreLayoutEngine`. Spacing 60 / 80 (×2 scale) | Straight centre-to-centre lines plus UML markers | No clusters. Packages are not compound boxes |
| **State** | `Naiad` `StateRenderer` | Convert to graph, **same** Dagre. Spacing 120 / 80. Extra passes align start/end and notes | Dagre routes; back-edges curve; notes sit beside states | Composite states are flattened into the graph, not nested clusters |
| **ER** | `Naiad` `ERRenderer` | Convert to graph, **same** Dagre. Spacing 100 / 100 (Mermaid `erRenderer.js`) | Dagre routes; cardinality markers; slotted face attach | No clusters |
| **Enterprise** (PR #1) | `EnterpriseSvgRenderer` on the flowchart contract | **Flowchart Sugiyama exactly.** Mapper emits subgraphs for groups/locations/junctions; leaf sizes come from ArchiMate boxes | Same resolver and curve builder as flowchart. Only markers and dashes stay ArchiMate | Same membership / compound-edge rules as flowchart |

**Important:** class, state, and ER already share **Naiad Dagre**, not native Sugiyama. Unifying them means moving those three off Dagre onto the native engine, with a profile - not inventing graph layout for them from scratch.

### 2.2 Own layout code (graph-shaped, but not Dagre / Sugiyama)

These still have nodes, groups, and edges, but they pack with a **specialised algorithm**. They can become engine **profiles** only after the core grows the right constraint (grid, nested boundary, tree, or weighted flow).

| Type | Layout today | Edges | Fit |
| --- | --- | --- | --- |
| **C4** | Bottom-up nested **boundary packing**; elements in rows inside each boundary | Straight connectors between element boxes | Medium. Containment is real (boundaries ≈ subgraphs). Ranking is a simple row pack, not Sugiyama |
| **Architecture** | Fixed **service grid** (columns × rows) plus group padding | Straight arrows, optional both ends | Medium. Groups ≈ clusters. Grid is a rank/order special case |
| **Requirement** | Two **columns** (requirements left, elements right), stacked by index | Straight arrows between the two sides | Easy as a two-column profile; poor as a free Sugiyama graph |
| **Block** | Author **column count** → cell grid | Straight, optional arrow | Easy as a grid profile |
| **Mindmap** | Recursive **tree**: measure, subtree height, then place | Parent-child curves | Tree profile (one root, no cycles). Not a general DAG |
| **Git graph** | Compute commit DAG, then place on a **branch-row × time-column** grid | Branch lines and merges | Special layered profile (rows = branches, ranks = time) |
| **Sankey** | Assign columns from the flow, scale node **height by value** | Width-coded ribbons | Related to layered layout, but geometry is driven by values, not rank gaps |

### 2.3 Not graph layouts - stay specialised

These do not rank nodes or route free edges. They must **not** be forced through Sugiyama. They plug in as **specialised layout modules** behind the same façade (same options object, same before/after gate, own `Layout` method).

| Type | What it actually lays out | Why it is not a graph |
| --- | --- | --- |
| **Sequence** | Participant **columns** + message **rows** on a time axis; activations and notes hang off lifelines | Order is declaration order and time, not connectivity. Crossing messages are a different problem from edge routing |
| **Gantt** | Calendar **x** × task **rows**; bars and milestones | Geometry is dates, not edges |
| **Pie** | Polar slices + legend | No nodes |
| **Timeline** | Horizontal **periods** and stacked events | Time line, not a DAG |
| **User journey** | Section columns × scored task cells | Score board, not routing |
| **Kanban** | Column stack of cards | Board, not a graph |
| **Quadrant** | 2×2 chart + plotted points | Coordinates are author values |
| **XY chart** | Cartesian series | Chart |
| **Packet** | Bit-field rows | Protocol dump |
| **Radar** | Polar axes + series polygons | Chart |
| **Treemap** | Nested area partition | Space-filling, not edges |

---

## 3. One implementation, one set of decisions

This is a **hard rule**, not a preference. If two types need ranking, they call the same ranker. If two types draw an arrowhead, they call the same marker painter with a `MarkerSet` option. If a specialised type (sequence, gantt, pie) needs text width, a theme colour, or a font, it still calls the shared measurer / theme / text writer. Specialised modules skip ranking and graph routing; they do **not** get a private copy of measuring, padding, colours, fonts, or arrowheads.

A profile is a bag of **options**. It is not a place to put a second `MeasureText`, a second cluster box formula, or a second triangle.

When a stage ports a type, the old copy is deleted or reduced to a thin call into the shared implementation. Leaving both paths “until later” is a failed stage.

### 3.1 Current duplications and the single replacement

Each row is one overlapping job. **Keep** is the implementation every type must use. **Delete or fold** are the parallel copies found today.

#### Ranking and ordering

| Current copies | Keep (one implementation) |
| --- | --- |
| `src/MermaidRecognition.Layout/FlowchartSugiyamaLayoutEngine.cs` → `Sugiyama/Layout/Layered/LayeredLayout.cs` (native flowchart) | **This.** Expose as the engine’s rank + order. Profiles set `Direction`, `RankSpacingPx`, `NodeSpacingPx`, `RankRoots`. |
| `extern/Naiad/src/Naiad/Layout/DagreLayoutEngine.cs` plus `Layout/Acyclic.cs`, `Layout/Ranker.cs`, `Layout/Ordering.cs`, `Layout/CoordinateAssignment.cs` (legacy flowchart, class, state, ER) | Fold callers onto `FlowchartSugiyamaLayoutEngine`. Retire `DagreLayoutEngine` for graph types at Stage 7. |
| Class / state / ER each build a private `GraphModel` / `StateLayoutGraph` / `ERLayoutGraph` then call Dagre (`extern/Naiad/src/Naiad/Diagrams/Class/ClassRenderer.cs`, `…/State/StateRenderer.cs`, `…/EntityRelationship/ERRenderer.cs`) | One mapper to the shared layout graph. No per-type ranker. |

#### Compound and subgraph containment

| Current copies | Keep |
| --- | --- |
| `src/MermaidRecognition.Layout/FlowchartSubgraphBoundsBuilder.cs` (padding 18, title 26, slack **8**, nested gap 24) + `FlowchartSubgraphLayoutPostProcessor.cs` + `FlowchartSubgraphMembershipHelper.cs` + `FlowchartCompoundEndpointHelper.cs` | **This.** Membership, evict-non-members, spine expand, facing-side clip. Profiles set `Cluster.PaddingPx`, `TitleBandPx`, `BottomSlackPx`, `NestedGapPx` only. |
| `extern/Naiad/src/Naiad/Layout/DagreLayoutEngine.cs` `LayoutFlowchartWithSubgraphs` and pack helpers (padding 18, title 26, slack **26** - already a second decision) | Delete the Naiad pack. Slack 8 is the one decision (this branch). |
| `extern/Naiad/src/Naiad/Diagrams/C4/C4Renderer.cs` (`BoundaryPadding = 15`, bottom-up boundary pack) | Same cluster builder. C4 supplies a **row-pack post-pass** option, not its own box math. |
| `extern/Naiad/src/Naiad/Diagrams/Architecture/ArchitectureRenderer.cs` (`GroupPadding = 20`) | Same cluster builder. Grid ranker is an option, not a second containment path. |
| Enterprise (PR #1) already maps groups onto flowchart subgraphs | Keep mapping; **no** enterprise cluster formula. |

#### Edge routing and attach

| Current copies | Keep |
| --- | --- |
| `src/MermaidRecognition.Layout/FlowchartEdgeRouteResolver.cs` + `FlowchartCurvePathBuilder.cs` + `FlowchartPerpendicularRouteBuilder.cs` + `FlowchartShapeBoundaryAttachHelper.cs` + corridor / skip-bypass / merge-back builders | **This.** One resolver. Profile picks `RoutingStyle` (`Curved`, `Straight`, `Orthogonal`, `Direct`). |
| `src/MermaidRecognition/Render/FlowchartRouter.cs` (legacy classify-only pilot, parallel to the resolver) | Fold classify into the resolver; delete the pilot. |
| `extern/Naiad/src/Naiad/Layout/CoordinateAssignment.cs` `RouteEdges` plus the `ApplyFlowchart*` snap list in `DagreLayoutEngine.cs` | Delete once flowchart is native-only. |
| Extra snaps in `extern/Naiad/src/Naiad/Diagrams/Flowchart/FlowchartRenderer.cs` | Delete with legacy flowchart. |
| Class: centre-to-centre `AddLine` in `ClassRenderer.cs`. State: custom curves in `StateRenderer.cs`. ER: slotted `AddLine` / `AddPath` in `ERRenderer.cs`. C4 / architecture / requirement: each clips a chord in its own renderer | `RoutingStyle = Direct` (or `Curved` for state back-edges) on the **same** resolver and `FlowchartShapeBoundaryAttachHelper`. ER slotted faces are a `PortMode` option, not a second router. |

MSAGL `src/MermaidRecognition.Layout/Sugiyama/Core/Layout/Arrowheads.cs` stays **internal** to layered layout (curve trim). It is not a second painter.

#### Arrowheads and endpoint markers

| Current copies | Keep |
| --- | --- |
| `src/MermaidRecognition.Layout/FlowchartSugiyamaSvgRenderer.cs` (`AppendCircleMarker`, `AppendCrossMarker`, inlined point heads) + `FlowchartRenderedPathMarkerHelper.cs` + `FlowchartEdgeAttachConstants.GetExteriorGapPx` | Grow into one **`DiagramMarkerPainter`**. Every type that draws a tip calls this. `MarkerSet` chooses the glyph (flowchart point/circle/cross, UML, ER cardinality, ArchiMate, sequence). |
| `extern/Naiad/src/Naiad/Rendering/SvgBuilder.cs` `AddArrowMarker`, `AddMermaidArrowMarker`, `AddCircleMarker`, `AddCrossMarker` (SVG `<marker>`) | Stop using `<marker>` for diagram tips. Same painter, inlined paths (Word/Skia already need this). |
| `src/MDConverter/Pipeline/DocxWriter.cs` (third copy: inlines `marker-end` for Skia) | Raster path **consumes** `DiagramMarkerPainter`. No private triangle. |
| `StateRenderer.DrawArrowhead` (`extern/Naiad/src/Naiad/Diagrams/State/StateRenderer.cs`) | Delete. |
| Near-identical 8 px / π/6 triangles in `C4Renderer.cs`, `ArchitectureRenderer.DrawArrow`, `RequirementRenderer.cs` | Delete. Call the painter. |
| `ClassRenderer.cs` (`AddArrowMarker` / `arrowhead-open`). `SequenceRenderer.cs` (`mermaid-svg_sequence-arrowhead`) | Same painter, `MarkerSet = uml` or `sequence`. |
| `ERRenderer.cs` crow’s-foot / circle / bar glyphs | Glyphs live **in** `DiagramMarkerPainter` as the `er` marker set. Attach and inset stay shared. |
| Enterprise ArchiMate markers (PR #1) | `MarkerSet = archimate` on the same painter. No enterprise-only tip path. |

#### Label placement

| Current copies | Keep |
| --- | --- |
| `src/MermaidRecognition.Layout/FlowchartEdgeLabelPlacementHelper.cs` + `FlowchartEdgeLabelClearanceHelper.cs` | Grow into **`DiagramLabelPlacer`**. Rule: beside the route, never on the stroke, arrowhead, or node boundary. |
| `extern/Naiad/src/Naiad/Diagrams/Flowchart/FlowchartRenderer.cs` `ResolveFlowchartEdgeLabelPosition`, `TryResolveFlowchartDecisionBranchEdgeLabelPosition`, `TryResolveFlowchartLrSideFanoutEdgeLabelPosition` | Delete with legacy flowchart. |
| Class mid-segment text; state mid-curve `t` samples in `StateRenderer.cs`; ER relationship label box; C4 / requirement `midX` / `midY - 8` | Same placer. Profile chooses offset (beside, above-horizontal, mid-branch). |
| Sequence message text above the arrow | Specialised module still calls `DiagramLabelPlacer` for any stroke-adjacent label. |

#### Text measuring

Every type that sizes a box from a string has invented its own `length × fontSize × factor`. Those factors **disagree**.

| Current copies | Factor today |
| --- | --- |
| `src/MermaidRecognition/Render/FlowchartNodeSizeEstimator.cs` | 0.512 em (flowchart HTML bbox; spec-traced) |
| `src/MermaidRecognition/Render/FlowchartRichLabelSvgWriter.cs` | separate regular / bold / mono em widths |
| `extern/Naiad/src/Naiad/Diagrams/Flowchart/FlowchartRenderer.cs` `MeasureText` | 0.62; short-word variants |
| `extern/Naiad/src/Naiad/Diagrams/Class/ClassRenderer.cs` `MeasureText` | 0.55; bold 0.65 |
| `extern/Naiad/src/Naiad/Diagrams/EntityRelationship/ERRenderer.cs` `MeasureText` | 0.62; bold 0.72 |
| `extern/Naiad/src/Naiad/Diagrams/State/StateRenderer.cs` `MeasureText` | 0.60 |
| `extern/Naiad/src/Naiad/Diagrams/Sequence/SequenceRenderer.cs` wrap / note width | 0.62 |
| `extern/Naiad/src/Naiad/Diagrams/Gantt/GanttRenderer.cs` `EstimateTextWidthPx` | 0.75 × 1.08 bold |
| `extern/Naiad/src/Naiad/Diagrams/Mindmap/MindmapRenderer.cs` `MeasureText` | 0.55 |
| `extern/Naiad/src/Naiad/Diagrams/Timeline/TimelineRenderer.cs` `MeasureText` | 0.55 |
| `extern/Naiad/src/Naiad/Diagrams/GitGraph/GitGraphRenderer.cs` `MeasureText` | 0.55 |
| `extern/Naiad/src/Naiad/Diagrams/Treemap/TreemapRenderer.cs` wrap | 0.60 |
| `extern/Naiad/src/Naiad/Diagrams/Quadrant/QuadrantRenderer.cs` axis margin | 0.60 |

**Keep:** one **`DiagramTextMeasurer`**, grown from `FlowchartNodeSizeEstimator` and `FlowchartRichLabelSvgWriter` (those are the spec-traced metrics). Input is text, font size, weight, family, wrap width. Every renderer - including sequence, gantt, pie, timeline - calls it. Private `MeasureText` methods are deleted. A profile may pass size and weight; it may not pick a different em factor.

Node **shape** extras (diamond `S = W0 + H0`, class compartments, ER header + rows, ArchiMate box sizes on PR #1) are profile `NodeMeasurer` arithmetic **on top of** the shared text measure and shared padding. `EnterpriseNodeSizer` (PR #1) must not remain a second text path.

#### Padding and spacing

| Current copies | Keep |
| --- | --- |
| `FlowchartSubgraphBoundsBuilder` cluster constants; `FlowchartRenderOptions` node/rank 50, diagram padding 8 | **`LayoutProfile`** fields applied by the cluster builder and the engine. One set of decisions for “how a box grows around content”. |
| `extern/Naiad/src/Naiad/LayoutOptions.cs` + `RenderOptions.Padding` (default 20) | Same profile fields. Do not keep a second options type for the same numbers. |
| Naiad Dagre slack 26 vs native slack 8 | Slack **8** (this branch). |
| Private constants: `ClassRenderer.ClassPadding`, `ERRenderer.EntityPadding` / `DiagramPadding`, `C4Renderer.BoundaryPadding`, `ArchitectureRenderer.GroupPadding`, `BlockRenderer.CellPadding`, `StateRenderer.statePadding` / `notePadding`, `MindmapRenderer.NodePadding`, `SankeyRenderer.NodePadding`, `KanbanRenderer.ColumnPadding` / `TaskPadding`, `SequenceRenderer` participant/note padding, `TimelineRenderer` event/section padding | Become **profile numbers**. The code that adds padding to a measured label or a cluster AABB is the shared builder, not a per-file `+ 2 * padding`. |

#### Colours and theme

| Current copies | Keep |
| --- | --- |
| `src/MermaidRecognition/Config/MermaidPinnedDefaults.generated.cs` + `MermaidEffectiveConfigBuilder.cs` (canonical theme keys) | **`DiagramTheme`**, resolved once from effective config. Shared roles: node fill, node stroke, edge, text, note fill/stroke, arrowhead, cluster fill. |
| `src/MermaidRecognition/Render/FlowchartRenderOptions.cs` (`nodeBkg` `#ECECFF`, `nodeBorder` `#9370DB`, `defaultLinkColor` `#333333`) | Becomes a reader of `DiagramTheme`, not a second palette. |
| `extern/Naiad/src/Naiad/Rendering/MermaidThemeVariables.cs` + `MermaidThemeResolver.cs` + `MermaidStyles.cs` (per-type CSS strings repeating the same hex) | Merge into `DiagramTheme`. CSS, if still needed, is generated from the theme, not hand-copied per kind. |
| Hard-coded hex in renderers: class compartment `#9370DB`; state notes `#FFFFCC` / `#AAAA33`; C4 / architecture / requirement edges `#666`; sequence notes `#FFF2B3` | Deleted. Use theme keys (`nodeBorder`, `noteBkg`, `lineColor`). Type-specific keys (Gantt task, ER stripe) live on the **same** theme object. |

#### Fonts and text emission

| Current copies | Keep |
| --- | --- |
| `FlowchartRenderOptions.FontFamily`; `FlowchartSugiyamaSvgRenderer` / `FlowchartSubgraphSvgRenderer` emit `<text font-family=…>` | Grow `FlowchartRichLabelSvgWriter` into **`DiagramTextWriter`**. One place writes SVG text (plain and rich). |
| `extern/Naiad/src/Naiad/RenderOptions.cs` `FontFamily`; `MermaidThemeVariables.FontFamily`; `MermaidStyles.cs` font-family repeated per type | `DiagramTheme.FontFamily`. |
| `extern/Naiad/src/Naiad/Rendering/SvgForeignObject.cs` hard-codes `trebuchet ms, verdana, arial, sans-serif` | Must take the theme family. Prefer `DiagramTextWriter` over a second foreignObject stack. |
| `extern/Naiad/src/Naiad/Rendering/SvgText.cs` | Thin wrapper over `DiagramTextWriter`, or deleted. |
| Per-type config keys (`sequence.actorFontFamily`, `c4.personFontFamily`, `journey.taskFontFamily`) | **Options** on the profile / theme. Emission still goes through `DiagramTextWriter`. |

#### Shape path data

| Current copies | Keep |
| --- | --- |
| `src/MermaidRecognition/Render/FlowchartShapePathGenerator.cs` | **`DiagramShapePathGenerator`**. Rect, rounded, diamond, cylinder, hex, stadium, circle. |
| `extern/Naiad/src/Naiad/Rendering/ShapePathGenerator.cs` (Naiad comment: native is aligned with this) | Delete after callers move. |
| Ad-hoc diamonds / hex in `BlockRenderer.cs`; diamonds in `StateRenderer.cs` | Call the shared generator. |

#### SVG document shell

| Current copies | Keep |
| --- | --- |
| Native `StringBuilder` in `FlowchartSugiyamaSvgRenderer.cs` | One **`DiagramSvgBuilder`**. Use Naiad `extern/Naiad/src/Naiad/Rendering/SvgBuilder.cs` as the document/defs shell if it is the more complete model, but path, marker, label, and style **must** come from the shared helpers above - not a second write of the same geometry. |
| Naiad `SvgBuilder` / `SvgDocument` used by every Naiad renderer | Same builder. Do not keep two ways to emit a path, a rectangle, or a viewBox. |

### 3.2 What a profile may and may not do

**May:** set `Direction`, spacings, `RoutingStyle`, `PortMode`, `MarkerSet`, `Containment`, cluster numbers, font size, theme key overrides, named post-passes that already exist on the core (decision order, parallel columns, row pack, grid ranker, tree order).

**Must not:** copy a measurer, a cluster AABB, a route builder, a triangle, a label offset, a hex colour, a font-family string emit, or a shape path. If the core cannot express a need yet, add **one** option or **one** named post-pass to the core, then use it. Do not add a file-local helper “just for this type”.

---

## 4. Shared core

The engine is the **native flowchart pipeline**, generalised. Naiad Dagre stays as a fallback until each type’s before/after gate is green. Enterprise does not get a second router. Every overlapping job in §3 has a single owner in this core.

### 4.1 Input

One **layout graph**:

- **Nodes** with measured width/height, shape, optional parent group.
- **Edges** with source, target, min-length, stroke, markers, optional label size.
- **Groups** (clusters) with title, parent, child node ids, optional local direction.
- A **profile** (section 5) that turns those facts into settings and post-passes. The profile does not implement ranking, routing, measuring, or paint.

`DiagramContract` already holds this for flowchart. Class, state, ER, C4, architecture, requirement, and block should map into the same contract (or a thin `LayoutGraph` DTO that contract already satisfies).

### 4.2 Ranking

Sugiyama longest-path / tight-tree ranking along the profile **direction** (TB, BT, LR, RL).

- Rank gap = `RankSpacingPx` (flowchart default 50).
- Invisible edges do not create ranks.
- Compound endpoints (edge to a group id) do **not** insert a dummy group node into the ranking graph. The edge is attached after placement.
- **Roots** sit one rank beyond their facing successor in the diagram direction. They are not forced onto a shared rank. Keep any old “align all roots” behaviour only when a test on that type proves it is required (flowchart: no such test).

Types that are not DAGs (sequence, gantt, pie, …) skip this stage.

### 4.3 Ordering

Barycentre ordering inside each rank, then type-specific order locks:

- Flowchart: decision fan-out order, parallel columns, spine-column overlap guard.
- Class / ER: stable declaration order when barycentre ties.
- State: start and end pinned; notes do not take a rank slot.
- Git graph: branch row is author/computed, not barycentre.

The core exposes **order constraints** (keep A left of B, pin to a column). Profiles add constraints; they do not fork the sorter.

### 4.4 Compound and subgraph containment

This is the rule the flowchart-layout-fixes work just locked. It becomes a **core invariant**, not a flowchart quirk.

1. A cluster rectangle is the member AABB plus profile padding, title band, and slack.
2. The rectangle contains **only members** (including nested groups).
3. Dummy nodes that exist only because a subgraph id appears in the contract are omitted from placement and paint.
4. If members spread so a hole would swallow a non-member, pack the hole, then evict remaining overlaps on the shortest axis that does not enter another cluster.
5. Spine-centre expand (column or row) is skipped when the extra empty region would cover a non-member.
6. Edges whose endpoint is a group id attach to the **facing side** of the cluster rectangle, clipped, two-point, no exterior loop.

C4 boundaries, architecture groups, and enterprise groupings reuse this invariant. Sequence `box` / `rect` regions are **not** clusters in this sense; they are decorations on a time grid.

### 4.5 Edge routing styles

The resolver already classifies routes (`FlowchartEdgeRouteKind`). The unified engine keeps **one resolver** with a profile-selected style:

| Style | When | Implementation already |
| --- | --- | --- |
| **Straight** | Two aligned ports on a clear right-angle run | `M… L…` axis-aligned |
| **Orthogonal** | Every other flowchart and enterprise edge | Visibility-graph polyline with ordered side ports and a nudge gap |
| **Curved** | A later profile option only | Draw the same orthogonal path with a curve; not a second router |
| **Direct (UML/ER)** | Class and ER today | Straight clipped chord + type markers. Profile may set `RoutingStyle = Direct` |

Ports: facing-side selection from relative geometry; `2N−1` slots on a shared face; decision diamonds use side exits; fan-out/fan-in allocators stay in the core. A profile can disable fan ports (class, ER) or force side-only (C4).

The **shared router** (`FlowchartSharedEdgeRouter`) owns every edge. The padded orthogonal visibility graph is the primary router, not a rescue. It does not call the adopted MSAGL routers for output. It does reuse their ideas, written in our own types. `curveBasis` and free diagonals are not used on the shared stroke; a later profile may draw the same right-angle path with a curve if needed.

| Technique | Our code | Inspired by |
| --- | --- | --- |
| Padded boxes + shortest path that misses foreign nodes | `FlowchartPaddedVisibilityRouter` | `VisibilityGraph`, `SingleSourceSingleTargetShortestPathOnVisibilityGraph` |
| Leave or enter a group once, at a right angle | `FlowchartGroupBoundaryCrossingHelper` | `GroupBoundaryCrossing`, `ObstacleTree` |
| Spread overlapping parallel interiors (edges that share a node stay on port spread) | `SeparateParallelRenderedSegments` | `Nudger` |
| Cost per change of direction on the visibility path | `FlowchartPaddedVisibilityRouter` Dijkstra bend cost (4% of Manhattan length) | `SsstRectilinearPath.BendsImportance`, `MsmtRectilinearPath` |
| Snap loose ends onto the box they join | `SnapAllEndpointsToBoxes` | `Arrowheads.TrimSplineAndCalculateArrowheads` |
| Keep labels off boxes and other strokes | `FlowchartEdgeLabelClearanceHelper` | `EdgeLabelPlacement` |

Markers stay **per-type paint**: flowchart arrows/cross/circle; class UML; ER cardinalities; ArchiMate serving/realization. The engine returns attach points; the renderer draws the marker.

### 4.6 Label placement

Core rule (already flowchart): measure the label before layout; `minlen` comes from stroke length, not label width; place the label beside the route, never on the stroke, arrowhead, or node boundary. The clearance helper is the one implementation, following `EdgeLabelPlacement`'s obstacle map (nodes, then other edges, then the own stroke).

Profiles add:

- Flowchart / enterprise: offset from the mid-arc; decision labels mid-branch.
- Class / ER: mid-segment, above the line.
- Sequence: above the message arrow (not an engine concern until sequence stays specialised).
- Gantt / pie / charts: labels are part of the specialised module.

---

## 5. Per-type configuration (profile)

One options object, filled by the type. The engine does not `switch` on kind except to load a profile. The profile **chooses**; it does not reimplement anything in §3.

```text
LayoutProfile
  KindId                  flowchart | classDiagram | stateDiagram | erDiagram |
                          enterprise | c4 | architecture | requirement | block |
                          mindmap | gitGraph | sankey | specialized
  Direction               TB | BT | LR | RL | None
  NodeSpacingPx
  RankSpacingPx
  EdgeSeparationPx
  DiagramPaddingPx
  Cluster.PaddingPx
  Cluster.TitleBandPx
  Cluster.BottomSlackPx
  Cluster.NestedGapPx
  RoutingStyle            Curved | Straight | Orthogonal | Direct
  Curve                   basis | linear | …   (ignored unless Curved)
  PortMode                FlowchartFan | FaceCentre | Slotted | None
  RankRoots               FacingSuccessor | SharedRank | None
  Containment             Enforce | None
  AllowCompoundEndpoints  true | false
  NodeMeasurer            delegate(node) → (width, height, shape)
  MarkerSet               flowchart | uml | er | archimate | none
  PostPasses              named list (decision order, parallel columns, note park, …)
  SpecializedModule       null | Sequence | Gantt | Pie | Timeline | …
```

### 5.1 Profiles that fit the core immediately

| Kind | Direction | Spacing (node / rank) | Routing | Ports | Containment | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Flowchart | Author | 50 / 50 | Curved | FlowchartFan | Enforce | Current defaults. Slack 8, title 26, padding 18 |
| Enterprise | TB | Same as flowchart | Curved | FlowchartFan | Enforce | **No extra router.** Only `MarkerSet = archimate` and ArchiMate node sizes |
| Class | Author (often TB) | 120 / 160 (today’s 60/80 ×2) | Direct | FaceCentre | None (until packages) | Compartment boxes from `NodeMeasurer` |
| State | Author | 120 / 80 | Curved | FaceCentre | Optional (composites later) | Start/end size; notes as a post-pass |
| ER | Author | 100 / 100 | Direct | Slotted faces | None | Header + attribute height in `NodeMeasurer` |

### 5.2 Profiles that need extra core features first

| Kind | Extra core feature | Until then |
| --- | --- | --- |
| C4 | Nested **boundary pack** as a cluster strategy (row pack inside, not barycentre) | Keep `C4Renderer` |
| Architecture | Grid ranker (fixed columns) | Keep `ArchitectureRenderer` |
| Requirement | Two-column ranker | Keep `RequirementRenderer` |
| Block | Author column grid | Keep `BlockRenderer` |
| Mindmap | Tree order + optional radial | Keep `MindmapRenderer` |
| Git graph | Time-column ranker + branch rows | Keep `GitGraphRenderer` |
| Sankey | Value-weighted node height + ribbon routing | Keep `SankeyRenderer` |

### 5.3 Specialised modules (`KindId = specialized`)

Sequence, gantt, pie, timeline, journey, kanban, quadrant, xychart, packet, radar, and treemap set `SpecializedModule` and **do not run** ranking, ordering, or the graph router. They **do** use `DiagramTextMeasurer`, `DiagramTheme`, `DiagramTextWriter`, `DiagramPadding`, and `DiagramMarkerPainter` for every overlapping job. The façade still:

- accepts the same profile object (padding, fonts, theme);
- returns a `LayoutResult` (bounds + draw list);
- writes SVG through the shared painter;
- is covered by before/after PNGs.

That is “one engine” from the product’s point of view without lying about geometry, and without a second copy of measure / theme / font / arrow.

---

## 6. What cannot be a graph - how it plugs in

| Type | Plug-in | Stay separate forever? |
| --- | --- | --- |
| Sequence | `ISpecializedLayout`. Columns from participants; rows from messages. Optional later: treat `box` as a non-routing region | **Yes** for ranking. Do not Sugiyama lifelines |
| Gantt | `ISpecializedLayout` on a date axis | **Yes** |
| Pie | Polar module | **Yes** |
| Timeline | Period module (close to journey) | **Yes** |
| Journey / kanban | Board modules | **Yes** |
| Quadrant / XY / radar | Chart modules | **Yes** |
| Packet | Bit-grid module | **Yes** |
| Treemap | Area-partition module | **Yes** |
| Mindmap | Tree profile on the core **or** specialised until tree order exists | Move later |
| Git graph | Layered profile once time-columns exist | Move later |
| Sankey | Weighted-flow profile | Move later |

The test for “move onto the core” is: **can we express the picture as nodes, edges, groups, and a profile without a new layout algorithm?** If the algorithm is “place by time / angle / area / score”, it stays specialised.

Specialised does **not** mean a private theme, font, measurer, or arrow. Those jobs overlap with graph types, so they stay on the single implementations in §3.

---

## 7. Staged migration (renders stay stable)

Every stage lands on its own branch, with **before PNGs from the previous green renderer** and **after PNGs from the new path**, plus tests. Do not flip the DOCX default for a type until its catalogue is identical within the agreed pixel tolerance (flowchart/enterprise geometry: **2 px**; visual slack for charts can be looser if labels match).

### Stage 0 - Substrate (this branch)

Native flowchart Sugiyama is the core: ranking, facing-successor roots, membership containment, compound-edge clip, curved/straight/orthogonal routing, label offset. Before/after already lives in `docs/flowchart/layout-fixes/`. **Do not start type ports until this is merged or explicitly baselined.**

### Stage 1 - Façade, no behaviour change

Introduce `LayoutProfile` + `IUnifiedLayoutEngine.TryLayout(graph, profile)` as a wrapper around the existing flowchart pipeline. Name the shared owners from §3 (`DiagramTextMeasurer`, `DiagramTheme`, `DiagramMarkerPainter`, `DiagramLabelPlacer`, `DiagramTextWriter`, `DiagramShapePathGenerator`) even if they first wrap today’s flowchart files. Flowchart and enterprise (when PR #1 is rebased onto this work) call the façade. Default profile values are today’s flowchart numbers. Gate: byte-stable flowchart fixtures (or 2 px). No second copy of any §3 job is added.

### Stage 2 - Class, state, ER onto the core

Map each Naiad model to the layout graph. Profiles from §5.1. Delete each type’s private `MeasureText`, `DrawArrowhead`, and centre-to-centre router in the same change as the port. Keep Naiad as fallback only until the before/after gate is green.

Gate per type:

- Before: current Naiad PNG/SVG of every recognition fixture and smoke sample.
- After: native engine.
- Must match: node identity, edge identity, direction of every connector, no missing edges.
- Geometry: start with a generous visual review, then tighten toward 2 px on node centres once ports are right.

Class and ER start with `RoutingStyle = Direct` so we do not suddenly curve UML/ER lines. State may keep curves for back-edges via `Orthogonal` or `Curved` on those edges only.

### Stage 3 - Enterprise stays a profile

When PR #1 needs these layout fixes, **rebase or merge `flowchart-layout-fixes` into `enterprise-diagram`** (not the other way around). Enterprise profile = flowchart + ArchiMate markers and sizes. Re-run `docs/enterprise/rendered/parity/` and the 2 px geometry tests. No enterprise-only router.

### Stage 4 - Grid / nested-boundary types

C4, architecture, requirement, block. Add cluster strategies (row pack, fixed columns) as **named post-passes**, not new engines. Same before/after gate against today’s Naiad pictures.

### Stage 5 - Tree and weighted flow (optional)

Mindmap, git graph, sankey. Only if the core has tree-order and value-weighted sizes. Otherwise leave them specialised.

### Stage 6 - Specialised modules behind the façade

Sequence, gantt, pie, timeline, and the charts. Move code onto the façade and **replace** each private `MeasureText`, hex colour, font-family emit, and message arrow with the shared implementations. Do not change geometry except where the old private factor was a bug relative to the one measurer. Gate: identical rasters, then tighten once the shared measurer is the only one. The win is one options object, one DOCX entry point, one place to hang before/after.

### Stage 7 - Retire Naiad Dagre for graph types

When flowchart, class, state, ER, and enterprise are native-only and green, stop calling `DagreLayoutEngine` for those kinds. Keep Naiad renderers for specialised types until Stage 6 is done. Do not delete Naiad in the same change as a layout port.

### Stability rules (every stage)

1. Capture **before** from the currently shipping renderer, not from a dirty tree.
2. Check in before/after PNGs under `docs/layout-engine/<stage>/<type>/before|after/`.
3. Existing unit and catalogue tests stay green.
4. Add tests for the invariant that stage claims (membership, root rank, compound clip, edge direction - already the flowchart pattern).
5. DOCX default flips **last**, behind the existing `UseNativeSugiyamaRenderer`-style flag if needed.
6. If a before/after pair regresses, the type stays on the old path.
7. A stage that ports a type must **delete** that type’s copy of every overlapping job in §3 in the same change. Two live implementations of ranking, routing, measuring, markers, labels, padding, colours, or fonts is a failed stage.

---

## 8. What we will not do

- A second enterprise router, or any type-specific “corridor” engine that bypasses the shared resolver.
- A second copy of ranking, containment, routing, arrowheads, label placement, text measuring, padding, colours, or fonts - including “just for this type” helpers and leftover Naiad paths after a port.
- Profiles that reimplement a shared job instead of choosing an option.
- Running sequence, gantt, pie, or timeline through Sugiyama “so everything is a graph”.
- Changing flowchart defaults to please a later type. Later types get a profile.
- Silent deviations from `Mermaid_Specification.md` on flowchart geometry. New core rules go in that file first (as this branch already did for slack, roots, and compound edges).

---

## 9. Survey summary

**Native Sugiyama (one path):** flowchart (default), and enterprise on PR #1.

**Naiad Dagre (shared among themselves, not with native):** class, state, ER, plus legacy flowchart.

**Own packers (graph-shaped):** C4, architecture, requirement, block, mindmap, git graph, sankey.

**Not graphs:** sequence, gantt, pie, timeline, journey, kanban, quadrant, xychart, packet, radar, treemap.

**Detected but not drawn:** eventmodeling, treeView, ishikawa, venn, wardley, info.

The unified engine is the native flowchart core plus a **profile**. Graph types migrate onto it in stages with before/after PNG gates. Calendar, polar, and chart types stay specialised modules behind the same door.

Overlapping jobs already exist in two or more copies (see §3): two rankers (native Sugiyama vs Naiad Dagre), two cluster packers (slack 8 vs 26), several routers, at least six arrowhead drawers, thirteen private text-width factors, two theme stacks, and two shape-path generators. The rule is one implementation per job. Profiles choose options from that code; they do not reimplement it.

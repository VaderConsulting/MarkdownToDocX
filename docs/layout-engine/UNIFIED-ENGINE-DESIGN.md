# Unified layout engine — design

**Status:** Design only. No implementation in this document.  
**Branch:** `flowchart-layout-fixes`  
**Date:** 5 October 2026  
**Audience:** Dave and anyone extending markdown2docx layout

Dave’s goal: **one configurable layout engine** in markdown2docx that understands every diagram type, instead of a separate layout per type.

This note surveys what the product renders today, what already shares a graph engine, and how to grow the native flowchart Sugiyama path into that single engine without breaking existing pictures. The gate for every stage is **before/after renders**, the same way `docs/flowchart/layout-fixes/` proved the shared flowchart fixes.

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
| **Flowchart** (native, default) | `src/MermaidRecognition.Layout` | Adopted Sugiyama (`LayeredLayout`) + `FlowchartLayoutPostProcessor` + `FlowchartSubgraphLayoutPostProcessor` | Straight two-point; `curveBasis` on multi-point; orthogonal polyline only for retry/corridor; compound edges clip to the cluster rectangle | Subgraphs are clusters. Dummy group-id nodes are omitted. Non-members stay outside the box |
| **Flowchart** (legacy) | `Naiad` `FlowchartRenderer` + `DagreLayoutEngine` | Naiad Dagre (acyclic → rank → order → coordinates). Compound subgraphs are a **bottom-up approximation**, not a real compound graph | Dagre route points + a long list of flowchart-only snaps (retry loops, stadium merge, terminal collector, cross-boundary) | Recursive per-subgraph layout, then pack |
| **Class** | `Naiad` `ClassRenderer` | Convert to a `GraphModel`, then **same** `DagreLayoutEngine`. Spacing 60 / 80 (×2 scale) | Straight centre-to-centre lines plus UML markers | No clusters. Packages are not compound boxes |
| **State** | `Naiad` `StateRenderer` | Convert to graph, **same** Dagre. Spacing 120 / 80. Extra passes align start/end and notes | Dagre routes; back-edges curve; notes sit beside states | Composite states are flattened into the graph, not nested clusters |
| **ER** | `Naiad` `ERRenderer` | Convert to graph, **same** Dagre. Spacing 100 / 100 (Mermaid `erRenderer.js`) | Dagre routes; cardinality markers; slotted face attach | No clusters |
| **Enterprise** (PR #1) | `EnterpriseSvgRenderer` on the flowchart contract | **Flowchart Sugiyama exactly.** Mapper emits subgraphs for groups/locations/junctions; leaf sizes come from ArchiMate boxes | Same resolver and curve builder as flowchart. Only markers and dashes stay ArchiMate | Same membership / compound-edge rules as flowchart |

**Important:** class, state, and ER already share **Naiad Dagre**, not native Sugiyama. Unifying them means moving those three off Dagre onto the native engine, with a profile — not inventing graph layout for them from scratch.

### 2.2 Own layout code (graph-shaped, but not Dagre / Sugiyama)

These still have nodes, groups, and edges, but they pack with a **specialised algorithm**. They can become engine **profiles** only after the core grows the right constraint (grid, nested boundary, tree, or weighted flow).

| Type | Layout today | Edges | Fit |
| --- | --- | --- | --- |
| **C4** | Bottom-up nested **boundary packing**; elements in rows inside each boundary | Straight connectors between element boxes | Medium. Containment is real (boundaries ≈ subgraphs). Ranking is a simple row pack, not Sugiyama |
| **Architecture** | Fixed **service grid** (columns × rows) plus group padding | Straight arrows, optional both ends | Medium. Groups ≈ clusters. Grid is a rank/order special case |
| **Requirement** | Two **columns** (requirements left, elements right), stacked by index | Straight arrows between the two sides | Easy as a two-column profile; poor as a free Sugiyama graph |
| **Block** | Author **column count** → cell grid | Straight, optional arrow | Easy as a grid profile |
| **Mindmap** | Recursive **tree**: measure, subtree height, then place | Parent–child curves | Tree profile (one root, no cycles). Not a general DAG |
| **Git graph** | Compute commit DAG, then place on a **branch-row × time-column** grid | Branch lines and merges | Special layered profile (rows = branches, ranks = time) |
| **Sankey** | Assign columns from the flow, scale node **height by value** | Width-coded ribbons | Related to layered layout, but geometry is driven by values, not rank gaps |

### 2.3 Not graph layouts — stay specialised

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

## 3. Shared core

The engine is the **native flowchart pipeline**, generalised. Naiad Dagre stays as a fallback until each type’s before/after gate is green. Enterprise does not get a second router.

### 3.1 Input

One **layout graph**:

- **Nodes** with measured width/height, shape, optional parent group.
- **Edges** with source, target, min-length, stroke, markers, optional label size.
- **Groups** (clusters) with title, parent, child node ids, optional local direction.
- A **profile** (section 4) that turns those facts into settings and post-passes.

`DiagramContract` already holds this for flowchart. Class, state, ER, C4, architecture, requirement, and block should map into the same contract (or a thin `LayoutGraph` DTO that contract already satisfies).

### 3.2 Ranking

Sugiyama longest-path / tight-tree ranking along the profile **direction** (TB, BT, LR, RL).

- Rank gap = `RankSpacingPx` (flowchart default 50).
- Invisible edges do not create ranks.
- Compound endpoints (edge to a group id) do **not** insert a dummy group node into the ranking graph. The edge is attached after placement.
- **Roots** sit one rank beyond their facing successor in the diagram direction. They are not forced onto a shared rank. Keep any old “align all roots” behaviour only when a test on that type proves it is required (flowchart: no such test).

Types that are not DAGs (sequence, gantt, pie, …) skip this stage.

### 3.3 Ordering

Barycentre ordering inside each rank, then type-specific order locks:

- Flowchart: decision fan-out order, parallel columns, spine-column overlap guard.
- Class / ER: stable declaration order when barycentre ties.
- State: start and end pinned; notes do not take a rank slot.
- Git graph: branch row is author/computed, not barycentre.

The core exposes **order constraints** (keep A left of B, pin to a column). Profiles add constraints; they do not fork the sorter.

### 3.4 Compound and subgraph containment

This is the rule the flowchart-layout-fixes work just locked. It becomes a **core invariant**, not a flowchart quirk.

1. A cluster rectangle is the member AABB plus profile padding, title band, and slack.
2. The rectangle contains **only members** (including nested groups).
3. Dummy nodes that exist only because a subgraph id appears in the contract are omitted from placement and paint.
4. If members spread so a hole would swallow a non-member, pack the hole, then evict remaining overlaps on the shortest axis that does not enter another cluster.
5. Spine-centre expand (column or row) is skipped when the extra empty region would cover a non-member.
6. Edges whose endpoint is a group id attach to the **facing side** of the cluster rectangle, clipped, two-point, no exterior loop.

C4 boundaries, architecture groups, and enterprise groupings reuse this invariant. Sequence `box` / `rect` regions are **not** clusters in this sense; they are decorations on a time grid.

### 3.5 Edge routing styles

The resolver already classifies routes (`FlowchartEdgeRouteKind`). The unified engine keeps **one resolver** with a profile-selected style:

| Style | When | Implementation already |
| --- | --- | --- |
| **Straight** | Two aligned ports, or any two-point compound/group chord | `M… L…` — no `curveBasis` bow |
| **Curved** | Default multi-point flowchart (`flowchart.curve` = `basis`) | Stub + `curveBasis` body, smooth terminal approach |
| **Orthogonal** | Retry loops, under-corridors, explicit step syntax | Rounded-corner polyline; **not** the default |
| **Direct (UML/ER)** | Class and ER today | Straight clipped chord + type markers. Profile may set `RoutingStyle = Direct` |

Ports: facing-side selection from relative geometry; `2N−1` slots on a shared face; decision diamonds use side exits; fan-out/fan-in allocators stay in the core. A profile can disable fan ports (class, ER) or force side-only (C4).

Markers stay **per-type paint**: flowchart arrows/cross/circle; class UML; ER cardinalities; ArchiMate serving/realization. The engine returns attach points; the renderer draws the marker.

### 3.6 Label placement

Core rule (already flowchart): measure the label before layout; `minlen` comes from stroke length, not label width; place the label beside the route, never on the stroke, arrowhead, or node boundary.

Profiles add:

- Flowchart / enterprise: offset from the mid-arc; decision labels mid-branch.
- Class / ER: mid-segment, above the line.
- Sequence: above the message arrow (not an engine concern until sequence stays specialised).
- Gantt / pie / charts: labels are part of the specialised module.

---

## 4. Per-type configuration (profile)

One options object, filled by the type. The engine does not `switch` on kind except to load a profile.

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

### 4.1 Profiles that fit the core immediately

| Kind | Direction | Spacing (node / rank) | Routing | Ports | Containment | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Flowchart | Author | 50 / 50 | Curved | FlowchartFan | Enforce | Current defaults. Slack 8, title 26, padding 18 |
| Enterprise | TB | Same as flowchart | Curved | FlowchartFan | Enforce | **No extra router.** Only `MarkerSet = archimate` and ArchiMate node sizes |
| Class | Author (often TB) | 120 / 160 (today’s 60/80 ×2) | Direct | FaceCentre | None (until packages) | Compartment boxes from `NodeMeasurer` |
| State | Author | 120 / 80 | Curved | FaceCentre | Optional (composites later) | Start/end size; notes as a post-pass |
| ER | Author | 100 / 100 | Direct | Slotted faces | None | Header + attribute height in `NodeMeasurer` |

### 4.2 Profiles that need extra core features first

| Kind | Extra core feature | Until then |
| --- | --- | --- |
| C4 | Nested **boundary pack** as a cluster strategy (row pack inside, not barycentre) | Keep `C4Renderer` |
| Architecture | Grid ranker (fixed columns) | Keep `ArchitectureRenderer` |
| Requirement | Two-column ranker | Keep `RequirementRenderer` |
| Block | Author column grid | Keep `BlockRenderer` |
| Mindmap | Tree order + optional radial | Keep `MindmapRenderer` |
| Git graph | Time-column ranker + branch rows | Keep `GitGraphRenderer` |
| Sankey | Value-weighted node height + ribbon routing | Keep `SankeyRenderer` |

### 4.3 Specialised modules (`KindId = specialized`)

Sequence, gantt, pie, timeline, journey, kanban, quadrant, xychart, packet, radar, and treemap set `SpecializedModule` and **do not run** ranking, ordering, or the graph router. The façade still:

- accepts the same profile object (padding, fonts, theme);
- returns a `LayoutResult` (bounds + draw list);
- writes SVG through the shared painter where possible;
- is covered by before/after PNGs.

That is “one engine” from the product’s point of view without lying about geometry.

---

## 5. What cannot be a graph — how it plugs in

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

---

## 6. Staged migration (renders stay stable)

Every stage lands on its own branch, with **before PNGs from the previous green renderer** and **after PNGs from the new path**, plus tests. Do not flip the DOCX default for a type until its catalogue is identical within the agreed pixel tolerance (flowchart/enterprise geometry: **2 px**; visual slack for charts can be looser if labels match).

### Stage 0 — Substrate (this branch)

Native flowchart Sugiyama is the core: ranking, facing-successor roots, membership containment, compound-edge clip, curved/straight/orthogonal routing, label offset. Before/after already lives in `docs/flowchart/layout-fixes/`. **Do not start type ports until this is merged or explicitly baselined.**

### Stage 1 — Façade, no behaviour change

Introduce `LayoutProfile` + `IUnifiedLayoutEngine.TryLayout(graph, profile)` as a wrapper around the existing flowchart pipeline. Flowchart and enterprise (when PR #1 is rebased onto this work) call the façade. Default profile values are today’s flowchart numbers. Gate: byte-stable flowchart fixtures (or 2 px).

### Stage 2 — Class, state, ER onto the core

Map each Naiad model to the layout graph. Profiles from §4.1. Keep Naiad as fallback.

Gate per type:

- Before: current Naiad PNG/SVG of every recognition fixture and smoke sample.
- After: native engine.
- Must match: node identity, edge identity, direction of every connector, no missing edges.
- Geometry: start with a generous visual review, then tighten toward 2 px on node centres once ports are right.

Class and ER start with `RoutingStyle = Direct` so we do not suddenly curve UML/ER lines. State may keep curves for back-edges via `Orthogonal` or `Curved` on those edges only.

### Stage 3 — Enterprise stays a profile

When PR #1 needs these layout fixes, **rebase or merge `flowchart-layout-fixes` into `enterprise-diagram`** (not the other way around). Enterprise profile = flowchart + ArchiMate markers and sizes. Re-run `docs/enterprise/rendered/parity/` and the 2 px geometry tests. No enterprise-only router.

### Stage 4 — Grid / nested-boundary types

C4, architecture, requirement, block. Add cluster strategies (row pack, fixed columns) as **named post-passes**, not new engines. Same before/after gate against today’s Naiad pictures.

### Stage 5 — Tree and weighted flow (optional)

Mindmap, git graph, sankey. Only if the core has tree-order and value-weighted sizes. Otherwise leave them specialised.

### Stage 6 — Specialised modules behind the façade

Sequence, gantt, pie, timeline, and the charts. Move code, do not change geometry. Gate: identical rasters. The win is one options object, one DOCX entry point, one place to hang before/after.

### Stage 7 — Retire Naiad Dagre for graph types

When flowchart, class, state, ER, and enterprise are native-only and green, stop calling `DagreLayoutEngine` for those kinds. Keep Naiad renderers for specialised types until Stage 6 is done. Do not delete Naiad in the same change as a layout port.

### Stability rules (every stage)

1. Capture **before** from the currently shipping renderer, not from a dirty tree.
2. Check in before/after PNGs under `docs/layout-engine/<stage>/<type>/before|after/`.
3. Existing unit and catalogue tests stay green.
4. Add tests for the invariant that stage claims (membership, root rank, compound clip, edge direction — already the flowchart pattern).
5. DOCX default flips **last**, behind the existing `UseNativeSugiyamaRenderer`-style flag if needed.
6. If a before/after pair regresses, the type stays on the old path.

---

## 7. What we will not do

- A second enterprise router, or any type-specific “corridor” engine that bypasses the shared resolver.
- Running sequence, gantt, pie, or timeline through Sugiyama “so everything is a graph”.
- Changing flowchart defaults to please a later type. Later types get a profile.
- Silent deviations from `Mermaid_Specification.md` on flowchart geometry. New core rules go in that file first (as this branch already did for slack, roots, and compound edges).

---

## 8. Survey summary

**Native Sugiyama (one path):** flowchart (default), and enterprise on PR #1.

**Naiad Dagre (shared among themselves, not with native):** class, state, ER, plus legacy flowchart.

**Own packers (graph-shaped):** C4, architecture, requirement, block, mindmap, git graph, sankey.

**Not graphs:** sequence, gantt, pie, timeline, journey, kanban, quadrant, xychart, packet, radar, treemap.

**Detected but not drawn:** eventmodeling, treeView, ishikawa, venn, wardley, info.

The unified engine is the native flowchart core plus a **profile**. Graph types migrate onto it in stages with before/after PNG gates. Calendar, polar, and chart types stay specialised modules behind the same door.

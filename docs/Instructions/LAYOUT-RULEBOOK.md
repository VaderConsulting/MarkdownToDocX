# Diagram layout and routing rulebook

## Layout setting

Enterprise and flowchart diagrams default to `grid`. Diagram frontmatter may set `config.layout` to `grid` or `open`. An unknown value gives one warning and uses `grid`. The command-line option `--layout grid|open` sets the run default; the diagram's own setting takes precedence.

`grid` uses the shared grid and placement rules below. `grid` includes the compaction step in rule 2.11. `open` skips shared-grid placement and permits free node and group positions. Both modes use the same routing, ports, straight runs, rounded corners and crossing rules. Boxes and unrelated group frames must not overlap in either mode.

## Legend setting

Diagram frontmatter may set `config.legend` to `off`, `on`, `bottom-right`, `bottom-left`, `top-right`, `top-left`, `below`, `right`, `table`, or a placement hint such as `right-of id`. It defaults to `off`; `on` means `bottom-right`. The command-line option `--legend` sets the run default and diagram frontmatter wins. An image legend is placed after the main layout and never changes its nodes, routes, labels or score. It contains only the distinct visible node and line types used by the diagram, in first-use order; invisible connectors are omitted. It sits outside the main bounds with one grid step of clearance. `table` draws no image legend and adds a two-column Symbol/Type table after the diagram. Main-layout legality and quality checks exclude legend samples; the legend may not overlap itself or the main diagram.

> **Consolidated decisions (14 Oct 2026).** The operative rules below incorporate these decisions; this note is a history summary, not a separate precedence layer.

> 1. Offset child in the next rank: use one corner into the target, entering as rule 4.11 sets, as rule 4.8 says.

> 2. Diamond branches require 50 px before the first corner. Other source runs require at least the 20 px corner radius; terminal runs require at least the arrowhead length. A base rank gap remains 2 grid steps.

> 3. A gap grows by a whole grid step when it runs out of tracks.

> 4. A node that rejoins the main path stays on the main path.

> 5. Ties: branches go left of the main path, return tracks go right, diamond branches use symmetric edge ports, label clearance is the width of "n", and frame padding is 1 grid step.

> 6. The "Arrangement kept" check of brief section 9 allows the reordering in section 2 of this rulebook.

## Terms

- **Grid step**: 30 px. Node centres, frame edges, rank and column positions, gap growth and layout moves use whole grid steps. Ports, arrowheads, rounded-corner geometry and text bounds are continuous drawing coordinates and may be fractional.

- **Grid**: one set of column lines (vertical) and row lines (horizontal) for the whole grid-mode diagram. Every node centre sits on one column and one row line. A **column** is the nodes on one column line; a **row** is the nodes on one row line. In `open` mode, these grid-alignment requirements do not apply.

- **Rank**: a node's row in flow order. Mermaid's ranking gives the starting rank. A line is **forward** when its target is in a later rank.

- **Gap**: the clear space between two neighbouring ranks (rank gap) or columns (column gap), measured between the facing outlines of the largest nodes. Base rank gap: 2 grid steps, providing a default routing corridor for 20 px rounded corners, arrowhead clearance and independently allocated tracks; it is not derived from a universal 50 px straight-run requirement. Base column gap: 1 grid step.

- **Track**: a line inside a gap, parallel to the gap, on which a straight run travels. Tracks are 1 grid step apart; the first track is the gap's centre line. Each track carries one line where runs overlap (rule 5.1).

- **Port**: the point where one line meets a node outline. Box and diamond edges each accept any number of distinct ports, distributed symmetrically at k/(n+1) along the edge (3.2); a single line uses the edge centre. Diamond vertices are the four tips, not mandatory branch ports. Every port lies exactly on the visible outline.

- **Sides**: the **upstream side** faces earlier ranks (top); the **downstream side** faces later ranks (bottom). The **side that faces the target** (on a source) or **the side that faces the source** (on a target) is whichever of top, bottom, left or right faces the other end; the **far side** is the opposite side. A side or port is **free** when no line uses it.

- **Straight run**: a straight segment. Ordinary runs are horizontal or vertical; the initial run leaving or entering a sloped diamond edge is the sole exception and follows the edge normal (3.1). Diamond branches travel at least 50 px along that normal before turning onto the grid; other lines require at least 20 px before a corner and an arrowhead-length terminal run. A straight line with no corner needs no artificial source-side clearance.

- **Corner**: normally a quarter-circle of radius 20 px joining two orthogonal straight runs at a right angle. A diamond-edge-normal run turning onto the grid uses a tangent transition for its actual change of direction, with the same 20 px minimum bend radius; it need not be a quarter-circle. An **elbow** has 1 corner. A **Z** has 2 corners, and its first and last runs point the same way.

- **Main path**: see rule 2.18.

- **Branch**: a path that leaves a main-path node and is not on the main path. A **detour** rejoins the main path at the next main-path node. An **early exit** ends, or rejoins the main path further on. A **loop branch** ends in a back edge.

- **Back edge**: a line whose target is in an earlier rank than its source. Its **return track** is the track it runs on, outside every node it passes.

- **Fan**: 2 or more lines on one side of one node that go to, or come from, nodes in one rank.

- **Group / frame**: a group is a Mermaid subgraph; its frame is the rectangle drawn round its members. Every frame has a title strip at the top and 1 grid step of padding on each side.

- **Label gap**: the width of the letter "n" in the label font. It is the clear space between a label's text and its line on each side.

- **Main link**: the line to or from the node's one parent, or its first declared incoming line when it has several.

- **Strongest flow** (the longest decision-free run): the flow with the most nodes from its start, or from the last decision, to the converging node; the first declared wins a tie.

- **Cluster**: see rule 2.20.

- **Peer**: the named node or group of a node or group hint; see rule 2.21.

- **Honoured**: a hint is honoured when the final layout satisfies it; see rule 2.21.

- **Note**: a free-text box with a dog-ear corner; see rule 2.22. A **plain connection** is an undirected line to or from a note; see rule 4.10.

- **Nearest free cell**: the free cell with the fewest grid steps from the target cell, trying the same rank first, then earlier ranks, then left before right.

- **Forced minimum**: see rule 2.13.

- **Wrapped**: cut into rows or columns by rule 2.12.

Every rule is generic: it uses structure and geometry, never labels, samples, tests or group titles; only hints identify peers by id (2.21). **Precedence has four distinct levels:** (1) hard legality, including geometry and section 10; (2) honoured hints, which override automatic placement and eligible routing faults; (3) routing-fault rejection for routes not explicitly permitted by hints; (4) scored optimisation. A hint cannot override hard legality, but can override automatic placement, cluster membership preferences and eligible routing faults. Rules are expressed for top-to-bottom (TB); rule 1.1 maps them to other directions. `webshop-target-rules.md` retains historical pictures and decisions.

## 1. Grid and spacing

1.1 For LR, read left for top, right for bottom, top for left and bottom for right. BT and RL mirror TB and LR.

1.2 In `grid` mode, use one grid for the whole diagram. Every node centre is grid-aligned after placement, compaction, wrapping, hints and repairs. Every frame edge is rounded **outward** to the next grid line while preserving title and padding clearances. Node outlines need not lie on grid lines, and ports, curves, labels and arrowheads may use fractional drawing coordinates. In `open` mode, positions are free, but all non-grid legality and routing requirements remain.

1.3 All rectangular nodes in a diagram share one common width and height; diamonds have the common initial width and twice the common initial height. Measure labels and port capacity before routing. If a rectangular node needs more capacity, grow the common rectangular dimensions by whole grid steps for all rectangles. A diamond may grow individually by whole grid steps under 1.7 when its edge ports or initial runs need additional room. Notes are sized separately (2.22).

1.4 Move nodes and grow gaps only in whole grid steps, across the whole rank or column. Never shrink a corner, straight run, label gap or padding to make something fit (see rank-gap-markup-2.png).

1.5 Make every rank gap equal to the base rank gap of 2 grid steps. This value provides a default corridor for rounded corners, arrowhead clearance and independently allocated tracks; it is not intended to satisfy the longer diamond straight-run minimum. A rank gap grows only to hold a label, a Z, extra tracks, required run/corner clearance or a frame, and that growth changes no other gap (see rank-gap-markup.png).

1.6 Snap Mermaid's positions to the grid one whole rank or column at a time. If two nodes need the same place, push the second one 1 grid step on in rank order, and repeat until nothing overlaps.

1.7 Repair geometric shortfalls monotonically: measure the exact missing clearance, track capacity, port separation or run length, then move affected unhinted nodes by whole grid columns/rows or enlarge the relevant gap by the smallest whole-step increment that strictly reduces that shortfall. Recompute the shortfall after each repair; never retry an unchanged state. A shortfall caused by an off-centre port and insufficient corner clearance is repaired by moving the unhinted child to a different grid column, not by growing the rank gap. If no legal repair exists, draw the deterministic fallback of rule 5.7 and issue a diagnostic; never omit the diagram.

## 2. Placement

2.1 Start from Mermaid's ranks and in-rank order, and change them only as rules 2.4 to 2.21 say, honoured hints (rule 2.21) first (rules 2.2 and 2.3 are pointers).

2.2 See rule 2.18.

2.3 See rule 2.17.

2.4 Put a node with several parents under its first parent that has no other child directly below it, taking parents in Mermaid's order. If every parent has one, keep the node's snapped column (1.6). Inside a 2.10 block, 2.10 decides which source a target sits under.

2.5 If a node has several children, prefer directly below it the main-path child (2.18), or off the main path the longest-path child (the child with the longest path to a terminal node). Place remaining children by 2.17, subject to clearance and honoured hints.

2.6 Put detours in the column next to the main path, in the ranks between the two main-path nodes they join. Put early exits in the column beyond the detours. Put both on the side of the main path where Mermaid placed most of them; on a tie, use the left side. A second branch from the same diamond goes on the opposite side.

2.7 When a diamond has two children, one on the main path and one starting a loop branch, keep the main-path child directly under the diamond (rule 2.18) and put the loop child in the next column, one rank lower, on the side Mermaid placed it. Prefer a straight main-path connection only when the allocated symmetric ports align; otherwise use the fewest legal corners (see wait-loop-markup.png, cycle-grid-markup.png).

2.8 A chain of nodes that each have one parent and one child stays in one column as it passes into and out of a group (see nested-chain-markup.png).

2.9 See rule 2.10 (two-by-two is one case of it).

2.10 Block of lines between adjacent ranks (any number of sources and targets). Swap nodes within a rank when that improves the score without breaking higher-priority placement or hints. Order targets by the source supplying the most lines (tie: mean source column, then Mermaid order). Prefer a straight connection when the **allocated symmetric ports actually align**; otherwise route with the fewest legal corners. Every other line leaves and enters at ports allocated jointly and symmetrically (3.2, 3.3). Use separate overlapping tracks (5.1), remove avoidable crossings (5.9), and allow only right-angle crossings inside gaps. Do not introduce hops, side detours or frame-edge runs. Grow gaps only for legality (1.7).

2.11 Compaction (`grid` only). After initial placement and routing, visit nodes in rank order, then left to right. Consider moving an unhinted node by whole grid cells to a nearer legal position, including beside a parent when 2.17 permits it. **Never move a main-path node sideways for compaction**; a deliberate wrap fold (2.12) is the sole automatic exception. Retain a move only when the complete candidate satisfies the applicable rules and has a strictly lower score (2.13). After a move, reroute affected connections under 4.2. Move clusters as units unless an honoured hint requires a different arrangement (2.20–2.21); group membership and frame containment remain governed by 7.8. Remove only genuinely unused rows/columns; never shrink a gap needed for labels, tracks, corners or frames. Repeat until rule 2.14 stops the search.

2.12 Wrapping (`grid` only). After compaction, wrap overlong rows or columns into successive rows/columns to approach 4:3 without worsening the complete legal-layout score. There is no fixed diagram-size or node-count limit. Preserve source order and honoured hints. A long main path **may fold only at an explicit wrap boundary**: each uninterrupted section remains aligned, and the connecting edge is routed under sections 3–5. The fold is not an ordinary sideways compaction move. Wrapping never splits a group unless the group itself exceeds the selected wrap length. Run compaction after wrapping and keep only strictly improving candidates (2.14). Tests compute expected dimensions from measured node sizes and gaps and never hard-code pixel dimensions.

- The wrap length is the number of nodes across, or down, that brings the whole diagram closest to 4:3 (width 4 parts to height 3 parts), without raising the rule 2.13 score. Never wrap a row or column that already fits within the wrap length. The shape is a ratio of the diagram's own computed size, with no fixed pixel size or node limit.

- Wide: cut a row longer than the wrap length into rows of at most that length. Draw the first nodes across, then start the next row directly under the previous row, in the same columns, one rank gap below.

- Deep: cut a column longer than the wrap length into columns of at most that length. Draw the first nodes down, then start the next column directly to the right of the previous column, one column gap away.

- Keep the order of the nodes: the next row or column continues from the node after the last one in the previous row or column.

- One line joins the pieces. For a wrapped row, it leaves the bottom of the far-right node, runs back along a track in the gap, and enters the top of the first node of the next row. For a wrapped column, it leaves the right side of the bottom node, runs along a track in the gap, and enters the left side of the first node of the next column. This line follows sections 3 to 5 like any other line.

- Wrapping never splits a group frame unless the group is itself longer than the wrap length. Nodes of one group stay together.

- After wrapping, run compaction (rule 2.11) again, so that wrapped rows and columns pack closer. A diagram can need both wide and deep wrapping. Compaction and wrapping repeat in passes; candidates are scored and kept, and the loop stops, as set by rules 2.13 and 2.14.

- The wrap length is counted in nodes of the standard box size (rule 1.3). Tests compute the expected size from measured node sizes and the gaps, and never hard-code a pixel size or a node limit.

2.13 Layout score. Score each **legal, routing-fault-free (unless hinted)** candidate of the same diagram: shape + empty + crossings + bends + disconnected ends + hints; lower is better. Shape: 2 points per 5% deviation from 4:3, prorated. Empty: 10 points per 5% uncovered area through 30%, then 40 per 5% above 30%, prorated; measure against node boxes, group frame/title space, labels and occupied tracks. Crossings: 20 points per crossing **above a defensible forced minimum**, prorated only where meaningful. The forced minimum is the minimum under the permitted node/port/track orderings; do not assume a closed-form complete-bipartite formula applies to unrestricted orthogonal routing. Where it cannot be established, use zero as a conservative lower bound. Crossings up to that minimum are reported but unpenalised. Bends: 10 points per corner. Disconnected ends: 20 per unmatched drawn line end (but such ends are prohibited by section 10, so legal candidates normally have zero). Hints: -30 per honoured node/group hint and -15 per honoured line hint (2.21). Tests calculate expected scores from the measured layout and never hard-code a pixel size. The score cannot legalise a forbidden crossing, routing fault or overlap; it is not a pass mark. Keep weights unchanged until measured examples justify revision.

2.14 Keep the best. Compare complete legal candidates; accept only a strictly lower score. Stop after a full pass without improvement or a repeated layout signature. A finite deterministic candidate enumeration and a visited-layout set prevent cycles; do not impose an arbitrary diagram-size limit. Report any independently unsatisfiable hard constraint rather than expanding indefinitely (1.7). Empty space above 30% is a reported quality result, not a conversion failure.

2.15 Unconnected pieces. Arrange disconnected nodes or connected pieces in declared order, left-to-right then top-to-bottom. Compute the block’s own wrap length from its measured member dimensions and spacing by selecting the legal arrangement nearest 4:3; do not depend on the whole-diagram wrap length (2.12). A cluster is initially one piece. Every member stays within its owning group (7.8), whose frame grows to fit.

2.16 Fans. Arrange fan members into a local block whose wrap length is chosen from the fan block’s own measured dimensions and gaps to approach 4:3; do not depend on the overall diagram wrap length. Centre the source or target beside the block where permitted. This specific rule overrides generic 2.17 placement, subject to hints and legality. Route fan lines by 5.2 and 5.8, including nested tracks. Avoid removable crossings and hops.

2.17 Under the parent. Prefer a child directly below its parent when its allocated ports permit a legal route. For multiple children retain symmetric port allocation (3.2). If an off-centre port and directly aligned child would require two corners with insufficient horizontal clearance, move the unhinted child outward by whole grid columns until the required horizontal and vertical runs fit; adding vertical rank-gap space alone does not fix horizontal clearance. Keep uniform rectangular dimensions. Main-path alignment is preferred but not forced against port geometry. Decisions follow 3.8, fans follow 2.16, and multiple parents follow 2.4.

2.18 Main path. Select the path by longest decision-free continuation, with Mermaid order breaking ties; convergence follows the strongest incoming flow. Prefer a common centre column and straight edges when allocated ports align. At convergence, centre the rejoining node beneath the main-path node feeding it when legal, otherwise use the nearest legal grid position and 4.11. Do not distort symmetric ports to straighten a main-path edge. Reposition unhinted children by whole columns to make off-centre port routes legal (2.17). Neither compaction nor coupled repair may move a main-path node sideways; only an explicit wrap fold (2.12) or honoured hint may do so. A node that rejoins remains on the main path.

2.19 Free direction. When a diagram declares no direction, the engine tries each flow direction (down, right, up, left) and keeps the layout with the lowest score (rules 2.13 and 2.14), so a simple chain comes out as a straight run with the fewest corners. When a direction is declared, it is used and not changed. Hints (rule 2.21) apply inside the direction in use and never change a declared direction; with none declared, each trial scores its own honoured hints. There is no size threshold: the same rule applies to small and large diagrams. Each trial applies rule 1.1 to its direction, and Mermaid's ranks are recomputed for it. Trials may run in parallel and reuse results.

2.20 Clusters. Identify clusters from relationship connectivity only, never labels. A cluster does not cross a group frame; nodes belong to at most one cluster, and otherwise form single-node clusters. Move and wrap clusters as units **unless an honoured hint requires a different legal arrangement**; hints outrank clustering and must not be refused merely because they distort or split an automatic cluster. If needed, dissolve or recompute the affected cluster around the honoured hint while preserving group membership and geometry. Choose internal order to minimise crossings under higher-priority rules. Between clusters use 2.10. Cluster detection must have a deterministic tie-break based on source order.

2.21 Hints. Hints override automatic placement and routing, but never hard legality. An honoured line hint may explicitly permit the routing fault it identifies. An honoured node or group placement hint can make a tier-3 routing fault unavoidable; keep the hint, report and score that fault under 2.13 rather than dropping the hint to remove the fault. A placement hint never permits a section 10 legality fault. Hints outrank automatic clusters, compaction, wrapping and main-path preferences. Resolve contradictory hints in source order (d). Report impossible hints alongside the fallback diagram.

- (a) Written: a node or group hint is words at the end of its declaration line: above, below, left-of or right-of a named peer, optionally followed by an offset +n; or push N, S, E or W. A line hint is written in square brackets after the relationship: leave N|S|E|W, enter N|S|E|W, travel N|S|E|W, separated by commas. Every direction word in a hint—up, down, left, right, above, below, north, south, east, west, N, S, E and W—is relative to the reader's view of the finished diagram: north/up is the top of the page, south/down is the bottom, east/right is the right, and west/left is the left. This page-coordinate meaning is unchanged by the diagram direction, an internal coordinate axis, or rule 1.1.

- (b) Meaning: "right-of P" places the node one grid step to the right of P on the same row; "left-of P" does the same to the left. "below P" places it one grid step below P in the same column; "above P" does the same above. The separation is measured between the node and peer outlines, with the grid step as the minimum gap. An offset +n adds n grid steps in that direction. If the next cell is occupied, move further in the same direction by whole grid steps until a free cell is found; never change row or column to resolve the collision. A chain of relative hints therefore remains in one row or column. "push D" puts the node in the extreme free cell in direction D inside its own group, or inside the diagram when it has no group. "leave D" and "enter D" name the side of the source box and of the target box that the line uses. "travel D" is the direction of the middle (track) run of the line; a straight line has no middle run, so travel on it is not honoured.

- (c) Peers: a peer must be a node or group at the same nesting level in the same group. Missing or cross-group peers cause the hint to be ignored and reported. A valid peer may belong to another automatically detected cluster: honour the hint first, then move, split or recompute clusters as necessary without violating group membership or geometry. Cluster distortion alone is never grounds for rejecting a hint.

- (d) Order and conflicts: take hints in source order and add each to the set. A hint that would make the set impossible (for node and group hints, a circle of peers; for line hints, two lines hinted to the same port on one box side) is dropped and reported, and the earlier hints stay. Two nodes hinted to the same cell are not a conflict: the later one takes the next free cell further on, on that side (as in (b)), and its hint is still honoured.

- (e) Hinted nodes retain their peer relation during placement and repairs. Reposition, dissolve or recompute automatic clusters as needed to honour the hint; a cluster is not required to move whole when doing so would break an honoured hint. Group membership (7.8) and hard legality remain binding.

- (f) Legality and routing faults: hints never override legality (section 10); only the marked 5.7 fallback is excepted when no legal route exists. A line hint may expressly permit its named tier-3 routing fault. A node/group placement hint may make a tier-3 routing fault unavoidable, but never permits a legality fault: keep the honoured placement hint, report and score the routing fault under 2.13, and do not drop the hint to hide it. Evaluate feasible alternative routes and placements first. Add space when it can honour the hint (1.7); otherwise report the impossible hint and render the deterministic fallback.

- (g) Score: each honoured node or group hint scores -30 and each honoured line hint scores -15 in rule 2.13, so honoured hints may outweigh a few crossings. The score only compares candidates; it never drops an honoured hint. This is intended: the structure of an original is preferred to calculated layout. A hint that cannot be honoured earns nothing, is reported with the reason, and layout continues; nothing fails.

- (h) Direction: see rule 2.19. A node, group or line without a hint is laid out as before.

2.22 Notes. A note is a free-text dog-ear box, not an ArchiMate element or semantic relationship endpoint. Notes may be grouped or top-level, are sized independently from their text and use grid-centre and clearance rules in `grid` mode. A connected note sits beside its connected node or group on the same row in the nearest free cell unless an honoured hint moves it; an unconnected note follows 2.15. Group membership and frame containment follow 7.8. Plain note connections have no arrowhead or semantic relationship meaning (4.10); their routes follow 4.2 like every other connection, and they **do not count in the forced-crossing minimum** under 2.13. A line label is not a note.

## 3. Ports and diamonds

3.1 Each line has its own distinct port and non-overlapping first run. A line ends exactly on the actual drawn outline at its allocated port and meets the edge there at a right angle: perpendicular to a rectangular edge or perpendicular to a sloped diamond edge. This requirement applies to diamonds under this rule; a diamond's initial or final edge-normal run is the sole permitted non-horizontal/non-vertical straight run, then turns onto the orthogonal grid after its required minimum length (4.1, 4.4). Diamond ports are allocated symmetrically by 3.2, 3.3 and 4.7. Port assignment and route legality are checked in the layout and routing model, not by an SVG checker. The painter draws only the route supplied by that model; a missing route is reported as an error and is never replaced by a straight centre-to-centre line.

3.2 Allocate lines on each rectangular or diamond edge jointly. For n lines on an edge, use positions k/(n+1), k=1..n, along that edge; one line uses the edge centre. Preserve target order (3.3). Port coordinates may be fractional and are not grid-placement coordinates. No centre port is reserved for a main path when n is even. Never shift one port solely to straighten an edge. For rectangular nodes, insufficient spacing grows the common rectangular dimensions (1.3). For a diamond, insufficient port spacing or first-run clearance grows that diamond by whole grid steps (1.7). Hinted side choices participate in joint allocation.

3.3 On each side, order the ports by where each line's other end is, so no two lines cross next to the node.

3.4 Removed; see 4.8.

3.5 Removed; see 4.8.

3.6 A diamond has four sloped edges, each accepting any number of distinct ports allocated symmetrically at k/(n+1) under 3.2, 3.3 and 4.7, exactly as box edges do. One line on an edge uses its centre. Every diamond line enters or leaves perpendicular to its allocated sloped edge for the minimum straight run, then turns onto the horizontal/vertical grid (3.1, 4.1, 4.4). Grow the diamond by whole grid steps under 1.7 if its ports or initial runs do not fit.

3.7 For a diamond branch to a target directly below, choose a downward-facing edge port and route its perpendicular initial run for at least 50 px before turning onto the grid. Continue by the fewest legal corners; move an unhinted child by whole grid columns if required for corner clearance (2.17). A route is straight only if the required normal departure and target geometry align.

3.8 Decision branches. Allocate each branch a distinct port on a suitable diamond edge using 3.2, 3.3 and 4.7, preferring the edge facing its target subject to hints and legality. Each branch departs perpendicular to its sloped edge, runs straight for at least 50 px, then turns onto horizontal or vertical routing (3.1, 4.1, 4.4). Grow the diamond and surrounding gaps by whole grid steps to reduce measured port-spacing or initial-run shortfalls (1.7). Routes are independent and never share a port or first run.

3.9 Diamond lines use the same symmetric-port, track, corner-limit and crossing rules as other lines, except for their perpendicular-to-edge initial straight run and transition to the orthogonal grid (3.1, 4.1). No line may travel through the diamond interior. Prefer mirrored routing for mirrored destinations when compatible with hints and legality.

3.10 Arrowhead tips end exactly on the visible shape outline and never overlap or cross into the box. A cross endpoint marker keeps the exterior clearance in parameter cross_endpoint_standoff; its line ends at the marker anchor, outside the box.

## 4. Line shape

4.1 Build ordinary line paths from horizontal/vertical straight runs and 20 px rounded right-angle corners. **Sole exception:** the first straight run leaving, or final straight run entering, a sloped diamond edge is perpendicular to that edge and may be diagonal. After the required minimum straight run, it turns onto the horizontal/vertical grid; that transition is an allowed diamond-edge direction change, not a forbidden diagonal grid run. Do not use arbitrary diagonal detours, S-curves, free curves or hop arcs.

4.2 Allocate ports before routing under 3.2, 3.3 and 4.7, using the relative positions of the two endpoint nodes to determine port order and sides. A connection is a single straight run only when its allocated ports line up and their required departure and arrival directions agree; diamond edge normals are measured against the actual sloped edge (3.1). Never move a port off its allocated slot or reserve a centre port to force a straight main-path edge. Instead consider legal node movement (2.17–2.18), then use the fewest permitted direction changes (4.8). A legal line hint can override automatic route choice, not hard legality.

4.3 A line has at most 2 corners, 3 for a back edge; insert space until met (rule 5.7). A line with a hint (rule 2.21) may use the corners its hint needs. A line never loops on itself, and never runs past the side of its target that faces the source and turns back.

4.4 A diamond decision branch travels at least 50 px **perpendicular to its sloped edge** before its first change of direction onto the grid. A line entering a diamond likewise follows the edge normal for its required terminal straight run; its length must accommodate the arrowhead and any transition geometry. Other lines have at least 20 px of straight travel after their source port before a corner, and every arrowheaded line has a terminal straight run at least as long as its arrowhead. Do not impose a universal 50 px source run on ordinary lines.

4.5 Put a Z's middle run on a track in the gap between the source and target ranks (see lr-fanout-markup.png).

4.6 A line never runs along a node outline or a frame edge. It crosses a frame only at a right angle. A line to or from a group follows rule 4.9.

4.7 Allocate all automatic lines on each rectangular side and each diamond edge together at symmetric k/(n+1) ports (3.2), then assign ports in the order of the other endpoints (3.3). With an odd count the centre is occupied; with an even count **there is no centre port**, even for a main-path line. A legal side hint fixes the chosen side but does not exempt its line from joint port allocation. Reassign affected ports and reroute affected lines together to eliminate avoidable crossings (5.9), preserving symmetry and hints. A port is never moved solely to create a straight line.

4.8 Prefer the fewest legal corners after port allocation: zero only for aligned ports, otherwise one if clear, then a two-corner Z when necessary. Prefer sides facing the opposite endpoint where the endpoint lies wholly beyond that side; break equal-corner ties in flow direction. Rule 4.11 is the specific convergence exception; 3.8 controls decision branches; 5.3–5.4 control back edges. A line hint may override automatic side and route preferences within hard legality. Do not run through boxes or turn back past the facing side of the target.

4.9 Group endpoints. A relationship may name a group as its source or its target. The endpoint is the group's frame, and the frame is treated as a box in rules 3.1 to 3.3, 4.2, 4.4, 4.7, 4.8, 5.1 to 5.6 and 2.13, and in line hints (rule 2.21): the line leaves or enters the frame on the side that faces the other end, at a port allocated symmetrically by rules 3.2 and 4.7, square-on to the frame. The line starts or ends on the outer edge of the frame and never begins inside it, except a line between a group and one of its own members, which meets the inner edge of the frame and crosses nothing else needlessly. A line to a group never runs along the frame edge. A frame endpoint that needs more ports or tracks adds room as rule 1.7 sets (legality).

4.10 Plain connection. A plain connection is an undirected line, with no arrowheads and no relationship type, between a note and a node or group, or between two notes. It is drawn like any other line: sections 3 to 5 apply, a group end follows rule 4.9, its corners and crossings are scored by rule 2.13, and it may carry a line hint (rule 2.21). It is not a relationship and never enters the model.

4.11 Convergence. Allocate all target ports jointly (3.2, 4.7). A source directly above a target uses a straight top entry **only if its allocated source and target ports align**. An offset source uses the fewest-corner legal entry: top when reachable without a prohibited gap traverse, otherwise the side facing the source. Do not reserve or move a top-centre port to force convergence. A converging line never runs along the gap immediately above the target or uses an S-jog into its top unless a legal line hint explicitly allows that routing fault. Within a 2.10 block, follow 2.10 instead.

## 5. Tracks and crossings

5.1 Two lines never use the same track where their runs overlap. If a gap has fewer free tracks than lines, insert space as set by rule 1.7 and route again.

5.2 In a fan, the line to the node furthest out takes the outermost track and port, and the line to the nearest node takes the innermost. Apply this to fan-out and fan-in, and to both halves of the fan (see fan-nesting-markup.png). Fans between columns follow rule 5.8.

5.3 A back edge leaves its source's downstream side if that side is free; if not, it leaves the side facing its return track. It enters the middle of the target's side that faces the return track (see loop-back-markup.png).

5.4 Put the return track on the same side of the main path as the back edge's source. If the source is on the main path, use the side with no branches; if that is still tied, use the right side. When several back edges share a side, the longest takes the outermost track.

5.5 Removed; see 2.10.

5.6 Reconsider affected ports, tracks, sides and unhinted node positions together to remove avoidable crossings and humps. Additional corners are allowed within 4.3 unless an applicable hint permits more. Do not move a main-path node sideways during coupled repair; only a wrap fold or honoured hint can change its centre column. Recompute all affected routes, apply legality, then hints, then non-exempt routing faults, then score. Remaining unavoidable crossings must be right-angled and inside gaps.

5.7 Every input produces a diagram and diagnostics, even when requested geometry cannot be satisfied. For each repair, measure the outstanding geometric shortfall and accept only a change that strictly reduces it; use whole-step gap growth or whole-column child movement as appropriate (1.7). Port assignment and route validation happen in the layout and routing model. The painter draws only model-supplied routes; it never invents a straight centre-to-centre line for a link with no route. A missing route is an error and must be reported. For non-geometric impossibility, stop retrying and draw a deterministic fallback: retain all nodes and group frames without overlap, preserve group membership, draw every original relationship as an explicitly marked fallback connector using the shortest obstacle-avoiding orthogonal path available, and label the affected connectors as routing fallbacks in the diagnostics. A fallback connector may violate normal corner limits or routing preferences, but must not pass through nodes, hide an endpoint or change semantics. If even an obstacle-avoiding connector cannot be constructed, draw a visible dashed unresolved-connection stub at each endpoint and report the pair; never silently drop an edge or the diagram.

5.10 Move before detour. For every routing fault, compare the legal options: move the causing node or group by whole grid steps, reroute either clashing line, or keep the smallest legal hump. Reroutes may chain when one reroute creates another clash; score the whole chain as one option. Choose the option with the lowest overall repair score, counting added route length (5.10 length weight), each bend (2.13) and every line that must be rerouted (5.10 line weight). A move must obey hints (hints win), preserve the grid and group membership, add no legality or routing fault, and lower the score. Route options obey named limits for candidate moves, chain depth, lines per chain, added length and added bends; those values protect the 10 second per-diagram target and are set from catalogue measurements. If no move qualifies, route around it as normal. Report when a named limit prevents further candidates from being examined.

5.8 Fans between columns. When lines fan out from one source to targets that lie wholly beyond its left or right side, or fan in to one target from such sources, each line leaves the side of the source that faces the target and enters the side of the target that faces the source, at spread ports (rules 3.2 and 3.3). Each line has its own track in the gap between the columns, and the tracks are nested as rule 5.2 sets so that the lines do not cross each other. A fan line never leaves the source's far side, top or bottom to go round the source, never enters at a box corner, and never shares a track with another fan line, unless a line hint (rule 2.21) names the side. Fans to the next rank use the bottom and top sides (rules 2.10 and 5.2). Insert space for the tracks as rule 1.7 sets (see pictures/markup-fan-out-bad.png, which shows what is forbidden).

5.9 Avoidable crossings are **routing faults, not score terms**. If a legal alternative assignment of tracks, ports, sides or permitted extra corners removes a crossing while preserving hints and sections 3–5 (including 4.3), reject the crossing route and use the alternative. Evaluate affected lines together. A crossing with no such alternative remains as a right-angle crossing inside a gap and is reported/scored under 2.13. No hops.

## 6. Labels

6.1 Centre each label on a straight run of its own line, on a white background, with the line broken round it and one label gap on each side (see edge-label-markup.png). The line keeps its own course through the label: it is never bent, jogged or moved sideways to make room for a label, and a label never sits on a corner. If no straight run is long enough, insert space as rules 1.7 and 6.2 set (label room is legality) (see pictures/markup-decision-branches.png, pictures/markup-three-way-decision.png).

6.2 Use the longest straight run that holds the label and both label gaps clear of every corner and arrowhead. On a tie, use the run nearer the source. If no run is long enough, grow the gap it crosses by whole grid steps.

6.3 Centre every node's label horizontally and vertically inside its visible box. Measure the rendered label bounds; neither the text baseline nor an icon changes the centring point.

## 7. Groups and frames

7.1 Never cover or shorten the title strip. If the title is wider than the members, widen the frame by whole grid steps.

7.2 Members stay on the shared grid. If a frame needs more room, grow the whole rank gap or column gap; never move a member off its grid line.

7.3 Frames never overlap each other or cross a node that is not a member. When two frames, or a frame and an outside node, sit in neighbouring columns or ranks, grow that gap by 1 grid step, or by 2 when a line also runs through it (see frame-overlap-markup.png).

7.4 Centre a group across its parent group when it is the only group nested in that parent.

7.5 If both ends of a back edge are members of a group, keep its return track inside that group's frame.

7.6 Keep at least one grid step between a frame outline and every member node on all four sides, below the title band. Grow the frame or its surrounding gap in whole grid steps; never move a member off-grid.

7.7 Centre a group's member layout within its frame horizontally and vertically when the group has no placement hint. An honoured hint (rule 2.21) determines the placement and takes precedence.

7.8 Frame membership. A member that conflicts with its frame grows the frame and is never moved out. A non-member that conflicts with a frame is placed outside it once, and no later step, including 2.11 and 5.6, moves it back inside.

## 8. Sequence diagrams

8.1 Leave the same clear space above and below a note as between two consecutive messages. A note never touches a message line, an arrowhead or a label. Set this in the shared row layout, never for one diagram (see sequence-note-markup.png).

## 9. Precedence

9.1 Apply precedence in this order: (1) hard legality (section 10); (2) honoured hints, subject only to hard legality; (3) routing-fault rejection unless an applicable line hint explicitly permits the fault; (4) score-based optimisation. Mandatory structural constraints are applied during candidate construction; automatic placement preferences yield to hints. Avoidable crossings are routing faults under 5.9, not a score trade-off. Insert space only for legality or to honour hints.

9.2 Grow space only to reduce a measured legality or hint-clearance shortfall. If a port offset leaves insufficient horizontal room for corners, move the unhinted child by whole columns (2.17); growing a vertical gap does not fix that defect. When normal geometry is unsatisfiable, use the visible diagnostic fallback (5.7), not an unbounded retry or omitted diagram.

9.3 A specific rule beats a general automatic rule within one step of 9.6: diamond-specific first-run and sizing rules (3.6–3.9, 4.4) supplement the shared port rules (3.1–3.3, 4.7), while back-edge rules (5.3–5.4) supplement forward-line rules. Hints override all automatic rules except hard legality.

9.4 Evaluate each candidate in the order of 9.1: hard legality, honoured hints, non-exempt routing faults, then complete score (2.13). Specific geometric rules take precedence over general automatic rules, but never over an honoured legal hint. Automatic ties prefer grid placement, main-path continuity, fewer crossings, fewer corners, label placement, then Mermaid source order.

9.5 If a choice is still tied, keep Mermaid's order.

9.6 Execution order. Parse and validate hints; determine direction, main path, decisions, fan/block placement, clusters and disconnected pieces. Apply hints as constraints throughout, ahead of automatic placement. Allocate ports jointly, route decision branches, convergence, fans and back edges, then minimise corners. Check hard legality, honour legal hints, reject non-exempt routing faults and only then score candidates. Apply compaction and wrapping. For each routing fault, apply the bounded move-before-detour repair (5.10) before changing the route; then run coupled repairs and reroute all affected lines. Coupled repairs never move main-path nodes sideways except at an explicit wrap fold or by hint. Repair only measurable shortfalls; if unresolved, render the fallback and diagnostics (5.7). Direction trials select among completed candidates; Mermaid order resolves ties.

## 10. Must never (normal candidates are rejected for these violations; a marked rule 5.7 fallback connector is excepted only when no legal route exists, remains a reported diagnostic, and is never synthesized by the painter from node centres)

- In `grid` mode, a node centre or frame edge off the grid, or a non-whole-step layout move or gap growth. Fractional **rendered** port, curve, text and arrowhead coordinates are permitted. In `open` mode, grid-alignment constraints do not apply. Two nodes overlapping, or unequal rank gaps not justified by rule 1.5, are forbidden in either mode.

- A diagonal run **except the single edge-normal initial/final straight run at a sloped diamond edge** (3.1, 4.1), a curve other than an allowed 20 px orthogonal corner or diamond-to-grid direction change, an S-curve, a hop arc, or more corners than 4.3 permits (except a hinted line or a marked fallback connector under 5.7).

- A line whose allocated ports align but is drawn as anything other than one straight run (unless a legal line hint overrides its route), or a source/terminal run shorter than the applicable minimum in rule 4.4.

- A line end short of or inside the drawn outline, or on a box corner.

- Two lines sharing a port, first run or overlapping track.

- A line through a node or label it does not connect to, or along a node outline or frame edge.

- A line crossing a frame other than square-on, or a crossing of two orthogonal line runs other than at a right angle within a gap. Avoidable crossings are governed separately by the routing-faults list and rule 5.9.

- A line running past the target’s facing side and turning back, or a diamond line crossing its own diamond interior. Diamond branches may leave from any allocated edge port.

- Frames overlapping, a frame crossing a non-member node, a member placed outside its owning frame, or a title strip covered or cut short.

- A label touching a line, arrowhead, corner, outline, frame or another label.

- A sequence note touching a message line, arrowhead or label.

## Routing faults (a candidate with one is rejected unless an applicable line hint expressly permits it, or an honoured node/group hint makes it unavoidable under 2.21; these are not hard legality)

- A line bent, jogged or displaced solely for a label (6.1); a convergence line travelling along the gap above its target or S-jogging into its top (4.11); a fan line using the far side or going around its source, or sharing an overlapping track (5.8); any crossing removable by a legal reassignment or permitted extra corner (5.9). Reject these unless an applicable hint permits the fault under 2.21. The section 10 hard constraints still apply to normal connectors.

## On hold (do not build until Dave redefines them)

- Drawing a frame on the centre line of the gap between its members and outside nodes, and reserving that centre line as a frame track that no line may use. Both break when two groups sit side by side (see adjacent-groups-markup.png).

## Resolved conflicts

1\. Diamond ports: Diamonds have four edges, each accepting any number of symmetric k/(n+1) ports; one line on an edge uses its centre. Every line meets the sloped outline square to its edge and follows the edge normal for the minimum straight run before turning onto the horizontal/vertical grid (3.1, 3.6–3.9, 4.1). Diamonds grow by whole grid steps when necessary (1.7). No limit applies to outgoing branches.

2\. Elbow or Z: B3 and brief section 8 took an elbow first; B14, A14 and A16 took a Z first. Superseded by rule 4.8: fewest corners, a Z only when no one-corner route is clear (3.4 removed).

3\. Side exits: B5, B11 and A14 disagreed on side exits. Superseded by rule 4.8: a line uses the side that faces the other end when that end lies wholly beyond it (3.5 removed).

4\. Corner limit: B3 set 2 corners for every line, but the back edges in B17 and A13 need 3. Forward lines have 2 and back edges 3 (4.3).

5\. Rearranging: A1 and brief sections 1 and 9 keep Mermaid's rank and order; A5, A12, A13, A14 and A16 change them. The later rules win, and Mermaid's arrangement is the starting point and the last tie-break (2.1, 9.4).

6\. Sides and paths: brief sections 6 and 8 follow Mermaid's sides and paths; the rules here replace them.

7\. Crossings: B8 banned all crossings; A14, A16 and brief section 8 allow one when nothing removes it. Kept the later rule: a swap that removes a crossing is used (5.6, 5.9); the rest are scored (2.13).

8\. Track spacing: brief sections 3 and 7 set 5 tracks 6 px apart; the later A16 sets 1 grid step. Kept A16.

9\. Straight-line port: two lines on one side are spaced k/(n+1), and a single line keeps the centre port so it stays straight. Kept B6 (3.2).

10\. Spine versus a loop branch: A12 keeps the spine in one column; the later A13 puts a loop child and the spine child on either side. The spine child stays under the diamond and the loop child goes beside it (2.7, 2.18).

11\. A bypass that rejoins: A9 moved the rejoin node up beside the detour; the later A12 keeps it on the spine. Kept A12 (2.6).

12\. Back-edge sides and labels: B15 and B17 described the entry side differently, and B12b and B15 placed labels differently. Replaced by one rule each (5.3, 6.2).

13\. Rank gap: brief section 3 sets a 1-step gap. The base rank gap is now 2 grid steps to provide a routing corridor for rounded corners, arrowhead clearance and separate tracks, rather than to impose a universal 50 px source run.

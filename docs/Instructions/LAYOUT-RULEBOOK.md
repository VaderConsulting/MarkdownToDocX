# Diagram layout and routing rulebook

## Layout setting

Enterprise and flowchart diagrams default to `grid`. Diagram frontmatter may set `config.layout` to `grid` or `open`. An unknown value gives one warning and uses `grid`. The command-line option `--layout grid|open` sets the run default; the diagram's own setting takes precedence.

`grid` uses the shared grid and placement rules below. `grid` includes the compaction step in rule 2.11. `open` skips shared-grid placement and permits free node and group positions. Both modes use the same routing, ports, straight runs, rounded corners and crossing rules. Boxes and unrelated group frames must not overlap in either mode.

> **Decisions applied (7 Oct 2026, these override any conflicting text below)**
> 1. Offset child in the next rank: use the side facing the child (elbow into its top), as rule 4.8 says.
> 2. Minimum straight run of 50 px applies to diamond branches before their first corner. Every other line end needs a straight run of at least the arrowhead length before its arrowhead and at least the corner radius after leaving a node. The base rank gap stays 2 grid steps.
> 3. A gap grows by a whole grid step when it runs out of tracks.
> 4. A node that rejoins the main path stays on the main path.
> 5. Ties: branches go left of the main path, return tracks go right, a diamond side branch uses the side vertex first and the lower edge centre second, the label gap is the width of "n", and frame padding is 1 grid step.
> 6. The "Arrangement kept" check of brief section 9 allows the reordering in section 2 of this rulebook.


Every rule is generic: it uses only structure and geometry, never a label, sample, test or group title; rules never depend on the ids themselves; only hints name a peer by its id (rule 2.21). A hint overrides every automatic placement rule (2.4 to 2.20), including 4:3 shape, compaction, wrapping, clusters and main path; only legality overrides a hint. Legality means overlaps, the legality rules of sections 3 to 5, section 10 and room for lines. A line hint likewise outranks the routing choices of sections 3 to 5, within legality. Rules are written for top-to-bottom (TB) diagrams; rule 1.1 maps them to other directions. `webshop-target-rules.md` keeps the detailed history and pictures.

## Terms
- **Grid step**: 30 px. Every position, size, gap and move is a whole number of grid steps.
- **Grid**: the one set of column lines (vertical) and row lines (horizontal) for the whole diagram. Every node centre sits on one column line and one row line. A **column** is the nodes on one column line; a **row** is the nodes on one row line.
- **Rank**: a node's row in flow order. Mermaid's ranking gives the starting rank. A line is **forward** when its target is in a later rank.
- **Gap**: the clear space between two neighbouring ranks (rank gap) or columns (column gap), measured between the facing outlines of the largest nodes. Base rank gap: 2 grid steps. Base column gap: 1 grid step.
- **Track**: a line inside a gap, parallel to the gap, on which a straight run travels. Tracks are 1 grid step apart; the first track is the gap's centre line.
- **Port**: the point where one line meets a node's outline. A box side has 5 evenly spaced port slots, numbered 1 to 5. A diamond has 4 **vertices** (its corner points), then 4 **edge centres** (the midpoints of its slanted edges). Any other shape uses box slots, moved along the line until they touch the drawn outline.
- **Sides**: the **upstream side** faces earlier ranks (top); the **downstream side** faces later ranks (bottom). A target's **near side** is its left or right side that faces the source. A side or port is **free** when no line uses it.
- **Straight run**: one horizontal or vertical segment of a line. **Minimum straight run**: corner radius + 1 grid step = 50 px.
- **Corner**: a quarter-circle of radius 20 px joining two straight runs at a right angle. An **elbow** has 1 corner. A **Z** has 2 corners, and its first and last runs point the same way.
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
- **Nearest free cell**: the free cell with the fewest grid steps from the target cell, trying the same rank first, then earlier ranks, then left before right.
- **Forced minimum**: see rule 2.13.
- **Wrapped**: cut into rows or columns by rule 2.12.

## 1. Grid and spacing
1.1 For LR, read left for top, right for bottom, top for left and bottom for right. BT and RL mirror TB and LR.
1.2 Use one grid for the whole diagram. Every node sits on it, including diamond targets and group members.
1.3 Draw every node in one box size. A diamond is the same width and twice the height. If the longest label does not fit, grow the box for every node by whole grid steps.
1.4 Move nodes and grow gaps only in whole grid steps, across the whole rank or column. Never shrink a corner, straight run, label gap or padding to make something fit (see rank-gap-markup-2.png).
1.5 Make every rank gap equal to the base rank gap. A rank gap grows only to hold a label, a Z, extra tracks or a frame, and that growth changes no other gap (see rank-gap-markup.png).
1.6 Snap Mermaid's positions to the grid one whole rank or column at a time. If two nodes need the same place, push the second one 1 grid step on in rank order, and repeat until nothing overlaps.
1.7 Always move forward. Insert space only to meet a legality rule or to make room for a hint (rule 2.21): no overlap, and a track, a port slot and label room for every line. When a legality rule cannot be met in the space available, insert one whole grid step of space (rule 1.4) in the gap or rank concerned and apply the rule again. Stop when the gap holds every line and label it must carry; the amount needed is computed from them. No rule ends in a failure, and no rule depends on a diagram's size. Quality (crossings, shape, empty area) is decided by the score of rules 2.13 and 2.14, never by adding space.

## 2. Placement
2.1 Start from Mermaid's ranks and in-rank order, and change them only as rules 2.4 to 2.21 say, honoured hints (rule 2.21) first (rules 2.2 and 2.3 are pointers).
2.2 See rule 2.18.
2.3 See rule 2.17.
2.4 Put a node with several parents under its first parent that has no other child directly below it, taking parents in Mermaid's order. If every parent has one, keep the node's snapped column (1.6).
2.5 If a node has several children, keep directly below it the child on the main path of rule 2.18 or, off the main path, the longest-path child (the child with the longest path to a node with no outgoing line). Place the other children as rule 2.17 sets: the next child goes beside the parent, on the parent's own row.
2.6 Put detours in the column next to the main path, in the ranks between the two main-path nodes they join. Put early exits in the column beyond the detours. Put both on the side of the main path where Mermaid placed most of them; on a tie, use the left side. A second branch from the same diamond goes on the opposite side.
2.7 When a diamond has two children, one on the main path and one starting a loop branch, put one in the column on each side of the diamond's column, in Mermaid's order. The main path continues straight under its child (see wait-loop-markup.png, cycle-grid-markup.png).
2.8 A chain of nodes that each have one parent and one child stays in one column as it passes into and out of a group (see nested-chain-markup.png).
2.9 When two nodes in one rank both link to the same two nodes in the next rank, put each child directly under one parent. Choose the pairing with fewer crossings (see cross-links-markup.png).
2.10 Within a rank, swap two nodes if that lowers the rule 2.13 score. Where several lines run between two ranks, first order the lower rank by the mean column of each node's parents. Then put each lower node under the parent it has the most lines from; on a tie, use Mermaid's order (see many-to-many-markup.png).

2.11 Compaction (applies in `grid`; `open` skips it). After honoured hints and rules 2.2 to 2.10, 2.15 to 2.18 and 2.20 (the order of rule 9.6), visit nodes in rank order and then left to right. Move each node up by whole rows and left by whole columns into the nearest free grid cell, so that it shares a row with its parent where the next cell is free. Keep a move only if all of these hold: no box or frame overlaps; every line of the node still meets sections 3 to 5; the move does not raise the rule 2.13 score; the node stays in the cell of rule 2.17, or in the column of one of its parents or beside it where rule 2.17 does not apply; and the move breaks no honoured hint (rule 2.21). Compaction removes only rows and columns that hold no node, label, track or frame. It never shrinks a gap that holds a label, a Z, tracks or a frame (rule 1.4). The expected size of any compacted diagram is computed from the measured sizes of its nodes plus the gaps in these rules, and a test never hard-codes a pixel size. A node that belongs to a group stays in that group; a node that belongs to no group is not restricted. Passes repeat and the best layout is kept as set by rules 2.13 and 2.14. A cluster (rule 2.20) is one unit: it moves whole, by the same steps, and its nodes keep their places inside it. Rule 9.6 orders this rule against rules 2.15 to 2.18 and 2.20.

2.12 Wrapping (applies in `grid`; `open` skips it). After rule 2.11, a row or column that is longer than the wrap length is wrapped. There is no fixed limit on nodes and no failure for size: a diagram of any size is wrapped for as many rows and columns as it needs. No honoured hint (rule 2.21) is broken.
- The wrap length is the number of nodes across, or down, that brings the whole diagram closest to 4:3 (width 4 parts to height 3 parts), without raising the rule 2.13 score. Never wrap a row or column that already fits within the wrap length. The shape is a ratio of the diagram's own computed size, with no fixed pixel size or node limit.
- Wide: cut a row longer than the wrap length into rows of at most that length. Draw the first nodes across, then start the next row directly under the previous row, in the same columns, one rank gap below.
- Deep: cut a column longer than the wrap length into columns of at most that length. Draw the first nodes down, then start the next column directly to the right of the previous column, one column gap away.
- Keep the order of the nodes: the next row or column continues from the node after the last one in the previous row or column.
- One line joins the pieces. For a wrapped row, it leaves the bottom of the far-right node, runs back along a track in the gap, and enters the top of the first node of the next row. For a wrapped column, it leaves the right side of the bottom node, runs along a track in the gap, and enters the left side of the first node of the next column. This line follows sections 3 to 5 like any other line.
- Wrapping never splits a group frame unless the group is itself longer than the wrap length. Nodes of one group stay together.
- After wrapping, run compaction (rule 2.11) again, so that wrapped rows and columns pack closer. A diagram can need both wide and deep wrapping. Compaction and wrapping repeat in passes; candidates are scored and kept, and the loop stops, as set by rules 2.13 and 2.14.
- The wrap length is counted in nodes of the standard box size (rule 1.3). Tests compute the expected size from measured node sizes and the gaps, and never hard-code a pixel size or a node limit.

2.13 Layout score. Every candidate layout of one diagram gets a score; lower is better. Score = shape + empty + crossings + bends + hints. Shape: 2 points for every 5 percent the width-to-height ratio is away from 4:3 landscape (4 wide to 3 high), measured as a percentage of 4:3; too tall counts the same as too wide. Empty: the share of the diagram's bounding area not covered by node boxes, group frame and title space, labels or line tracks; 10 points for every 5 percent up to 30 percent, then 40 points for every 5 percent above 30 percent. Crossings: 20 points for every line crossing above the forced minimum. The forced minimum is the fewest crossings that the diagram's links make unavoidable. For a complete block of n boxes linked to m boxes in two adjacent ranks it is n(n-1)/2 times m(m-1)/2. Elsewhere it is the number of pairs of lines that cross in every possible order, or zero when that cannot be shown. Crossings up to the forced minimum score no points but are still reported. Bends: 2 points for every corner on a line. Hints: see rule 2.21(g). Shape, empty and crossing points are prorated between steps. An overlap, or any line that breaks sections 3 to 5, rejects the candidate; it gets no score. The score only compares layouts of the same diagram; it is never a pass mark and never a size limit. Tests compute scores from the measured layout and never hard-code a pixel size.

2.14 Keep the best. Compaction and wrapping (rules 2.11 and 2.12) produce candidates. Keep a candidate only if its score is strictly lower than the best so far. Stop when a full pass finds no lower score, or when a pass repeats an earlier layout. Then return the best layout found. The engine never fails or gives up because of diagram size or pass count. The 30 percent empty figure is a goal and a score step (rule 2.13), not a limit. The goal is no needless crossings (crossings above the forced minimum) and under 30 percent empty; a diagram that ends above 30 percent reports its empty share and the reason, and this is not a failed attempt.

2.15 Unconnected pieces. Nodes, or small connected pieces, that have no line to other pieces (inside one group or in the whole diagram) are arranged as a block of rows and columns in declared order, left to right and then top to bottom, sized by the wrap length of rule 2.12. The group frame grows to fit the block. Pieces in a group stay in that group. A cluster (rule 2.20) is one piece and is placed whole.

2.16 Fans. When one node has lines to or from many nodes on one side (a fan, one-to-many or many-to-one), the many nodes wrap into rows sized by the wrap length of rule 2.12, with the single node centred beside the block. Lines of inner rows run in the gaps between boxes of the outer rows. Each line has its own port and track, and the furthest box gets the outermost track and port (fan-out track rule). No avoidable crossings and no hops.

2.17 Under the parent. A node whose main link is to one parent is placed directly under that parent (or, in a left-to-right flow, to its right), centred on it, when that grid cell is free. Compaction (rule 2.11) keeps this cell or, when it is taken, the cell beside the parent. When the cell under the parent is taken by another child, the next child goes beside the parent, on the parent's own row (its own rank), in the nearest free cell, so that its line is straight. The only child of a node goes directly under it in the same way, unless rule 2.4 places that child under another parent.

2.18 Main path. The main path is the path of least resistance: the route a reader's eye follows. Start at a node with no incoming line. At each node follow its one outgoing line when it has only one; at a decision follow the line labelled yes (or, with no label, the first declared line in source order); where several flows converge, the converging node is on the main path of the strongest of them, the one that carries the longest decision-free run. Stop at a node with no outgoing line or one already on the path. Nodes on the main path share one centre line along the flow, so lines between them are straight (rule 4.7), except where a hint (rule 2.21) places the node. The main path never moves sideways to make room (see main-spine-markup.png). A node with one parent sits under that parent (rule 2.17); other nodes sit beside the main path. A node where flows converge is centred under (or beside) the main-path node that feeds it, and the other feeding lines use the remaining ports of rule 4.7. This rule outranks rule 2.11 for nodes on the main path: compaction never moves a main-path node off the centre line. Where a node with one parent hangs off a main-path node, rule 2.17 decides its place.

2.19 Free direction. When a diagram declares no direction, the engine tries each flow direction (down, right, up, left) and keeps the layout with the lowest score (rules 2.13 and 2.14), so a simple chain comes out as a straight run with the fewest corners. When a direction is declared, it is used and not changed. Hints (rule 2.21) apply inside the direction in use and never change a declared direction; with none declared, each trial scores its own honoured hints. There is no size threshold: the same rule applies to small and large diagrams. Each trial applies rule 1.1 to its direction, and Mermaid's ranks are recomputed for it. Trials may run in parallel and reuse results.

2.20 Clusters. A cluster is a set of nodes in which the share of possible links that exist inside the set is higher than the share between the set and the rest of the diagram. Find clusters from the links alone. A cluster never crosses a group frame, and every node is in at most one cluster. A node with no cluster is a cluster of one. Placement, compaction (rule 2.11), wrapping (rule 2.12), unconnected pieces (rule 2.15) and the 4:3 shape (rule 2.13) treat each cluster as one unit and move it whole. Inside a cluster, the nodes keep the order that gives the fewest crossings. A cluster linked to another cluster faces it, rank against rank, and the lines between them follow the many-to-many rule. A cluster longer than the wrap length is wrapped as a whole by rule 2.12, with its inner order unchanged. A cluster that contains a hinted node keeps that hint (rule 2.21): the cluster moves whole with the hinted node's peer relation intact; a hint that would split a cluster across a frame is a legality case (a group frame never holds a non-member, rule 7.3) and is handled as rule 2.21(f) sets; in every other case the cluster moves whole around the hinted node.

2.21 Hints. A node, a group or a line may carry a hint on its own declaration line, so that a diagram can keep the structure of an original. A hint overrides automatic placement as the preamble sets. Hints are used as follows.
- (a) Written: a node or group hint is words at the end of its declaration line: above, below, left-of or right-of a named peer, optionally followed by an offset +n; or push N, S, E or W. A line hint is written in square brackets after the relationship: leave N|S|E|W, enter N|S|E|W, travel N|S|E|W, separated by commas. N, S, E and W are screen directions and are never rotated by rule 1.1.
- (b) Meaning: "above P" or "below P" puts the node in the same column as P, and "left-of P" or "right-of P" puts it in the same row, on that side, in the nearest free cell. With an offset +n it is exactly n cells further when that cell is free, otherwise the next free cell further on (+0 is the adjacent cell). "push D" puts the node in the extreme free cell in direction D inside its own group, or inside the diagram when it has no group. "leave D" and "enter D" name the side of the source box and of the target box that the line uses. "travel D" is the direction of the middle (track) run of the line; a straight line has no middle run, so travel on it is not honoured.
- (c) Peers: a peer must be a node or group at the same level, in the same group. Any other peer (missing, or in another group) means the hint is ignored and reported. When the peer is in another cluster (rule 2.20), the two clusters are placed relative to each other as the hint says. A hint that would split a cluster across a frame is a legality case (group frames never hold non-members, rule 7.3): it is not honoured under (f), and it is reported.
- (d) Order and conflicts: take hints in source order and add each to the set. A hint that would make the set impossible (for node and group hints, a circle of peers; for line hints, two lines hinted to the same port on one box side) is dropped and reported, and the earlier hints stay. Two nodes hinted to the same cell are not a conflict: the later one takes the next free cell further on, on that side (as in (b)), and its hint is still honoured.
- (e) Hinted nodes keep their relation to their peer and move together with it; rules 2.11, 2.12, 2.15 and 2.20 never break an honoured hint (a cluster moves whole around a hinted node).
- (f) Legality: a hint never overrides legality (overlaps, sections 3 to 5, section 10). Space is inserted as rule 1.7 sets, to make room for a hint. A hinted line may use the corners its hint needs, without the corner limit of rule 4.3; its corners are still scored (rule 2.13).
- (g) Score: each honoured node or group hint scores -30 and each honoured line hint scores -15 in rule 2.13, so honoured hints may outweigh a few crossings. The score only compares candidates; it never drops an honoured hint. This is intended: the structure of an original is preferred to calculated layout. A hint that cannot be honoured earns nothing, is reported with the reason, and layout continues; nothing fails.
- (h) Direction: see rule 2.19. A node, group or line without a hint is laid out as before.

## 3. Ports and diamonds
3.1 Use one port per line, so lines never share a port or a first run. A line meets its port at a right angle and ends exactly on the drawn outline (see f11-straight-markup.png).
3.2 Fill box slots by the number of lines on the side: 1 line uses slot 3; 2 use 2 and 4; 3 use 2 to 4; 4 use 1, 2, 4 and 5; 5 use all five. A straight line always keeps slot 3. More than 5 lines grows the node along that side by whole grid steps. Rule 4.7 overrides this table: a line whose centres align takes the centre slot, and the other lines take the remaining slots in order.
3.3 On each side, order the ports by where each line's other end is, so no two lines cross next to the node.
3.4 Subject to rule 4.8, route a forward line between boxes in this order. First, bottom to top, as a straight line when the ends line up. Otherwise, bottom to top as a Z. Use an elbow from the bottom into the target's near side only when the target's top has no free port, or when the line ends a detour.
3.5 Subject to rule 4.8, send a line out of a box's left or right side only for a back edge, a target in the same rank (into its near side), or when the bottom has no free port. Even then, the target must lie wholly beyond that side.
3.6 A forward line enters a diamond at the top vertex, unless a line hint (rule 2.21) names another vertex or side. Further incoming lines use the side vertices.
3.7 A branch to a target directly below the diamond leaves the bottom vertex as a straight line.
3.8 A branch to a target in another column first uses the side vertex facing it: out horizontally, one corner, then down into the target's top. If that vertex is taken, or its run before the corner would be shorter than the minimum straight run, use the edge centre between that vertex and the bottom vertex, with the same shape (see decision-accepted.png, merge-decision-markup.png).
3.9 Never leave the bottom vertex and then turn sideways, unless a line hint (rule 2.21) names another vertex or side. Two branches to mirror-image targets are mirror images (see decision-three-way-markup.png).

## 4. Line shape
4.1 Build every line from straight runs and corners only. No diagonals, S-curves, free curves or hop arcs.
4.2 When the ports line up on one column or row line, draw a single straight run, whatever the two shapes' sizes, unless a line hint (rule 2.21) says otherwise.
4.3 A line has at most 2 corners, 3 for a back edge; insert space until met (rule 5.7). A line with a hint (rule 2.21) may use the corners its hint needs. A line never loops on itself, and never runs past its target's near side and turns back.
4.4 Every line runs straight for at least the minimum straight run after leaving its source and before its arrowhead, so no corner sits closer than that to either end.
4.5 Put a Z's middle run on a track in the gap between the source and target ranks (see lr-fanout-markup.png).
4.6 A line never runs along a node outline or a frame edge. It crosses a frame only at a right angle. A line to or from a group follows rule 4.9.
4.7 Port placement. When the horizontal or vertical centres of two linked boxes are aligned, the line between them uses the centre port on both facing sides, as a straight line (see rule 4.2). A centre port is used when it is free. Other lines on the same side take the remaining ports from left to right, or top to bottom, in the order of their targets, so that no line runs back past another. Each line has its own port, using the slots of rule 3.2. When no centres align, the lines take the ports in target order, in the slots of rule 3.2. When two lines want the centre port of one side, the nearer target gets it. A legal line hint (rule 2.21) that names a side is applied first, and the other lines take the remaining ports. An end marker sits on the port and takes no extra room.
4.8 Fewest corners. A line leaves and enters the sides that give it the fewest corners: none when the centres align (rule 4.7), otherwise one, by leaving the side that faces the target and entering the target on the side that faces the source. When two choices have the same number of corners, the one along the flow direction wins. A line never uses a side that makes it cross a box. This replaces the Z-first order of rule 3.4 and the side-exit limits of rule 3.5 for forward lines. A Z is used only when no one-corner route is clear. A legal line hint (rule 2.21) that names a side overrides this choice.
4.9 Group endpoints. A relationship may name a group as its source or its target. The endpoint is the group's frame, and the frame is treated as a box in rules 3.1 to 3.3, 4.2, 4.4, 4.7, 4.8, 5.1 to 5.6 and 2.13, and in line hints (rule 2.21): the line leaves or enters the frame on the side that faces the other end, at a free port on that side (the central port first, as rule 4.7), square-on to the frame. The line starts or ends on the outer edge of the frame and never begins inside it, except a line between a group and one of its own members, which meets the inner edge of the frame and crosses nothing else needlessly. A line to a group never runs along the frame edge. A frame endpoint that needs more ports or tracks adds room as rule 1.7 sets (legality).

## 5. Tracks and crossings
5.1 Two lines never use the same track where their runs overlap. If a gap has fewer free tracks than lines, insert space as set by rule 1.7 and route again.
5.2 In a fan, the line to the node furthest out takes the outermost track and port, and the line to the nearest node takes the innermost. Apply this to fan-out and fan-in, and to both halves of the fan (see fan-nesting-markup.png).
5.3 A back edge leaves its source's downstream side if that side is free; if not, it leaves the side facing its return track. It enters the middle of the target's side that faces the return track (see loop-back-markup.png).
5.4 Put the return track on the same side of the main path as the back edge's source. If the source is on the main path, use the side with no branches; if that is still tied, use the right side. When several back edges share a side, the longest takes the outermost track.
5.5 Each crossed line from rule 2.9 leaves its parent's bottom at the slot beside the centre, on the side towards its target. It takes its own track, crosses the other line once at a right angle, and enters its target's top at the slot beside the centre, on the side towards its source.
5.6 Before accepting a route, swap any two lines' tracks or ports if that lowers the rule 2.13 score. A crossing that remains is allowed only at a right angle inside a gap.
5.7 If no route meets every legality rule, insert space for legality rules, or to make room for a hint (rule 2.21), as rule 1.7 sets, in the gap or rank concerned and route again. Nothing is reported as failed, and there is no size or pass limit. Never draw a tangle.

## 6. Labels
6.1 Centre each label on a straight run of its own line, on a white background, with the line broken round it and one label gap on each side (see edge-label-markup.png).
6.2 Use the longest straight run that holds the label and both label gaps clear of every corner and arrowhead. On a tie, use the run nearer the source. If no run is long enough, grow the gap it crosses by whole grid steps.

## 7. Groups and frames
7.1 Never cover or shorten the title strip. If the title is wider than the members, widen the frame by whole grid steps.
7.2 Members stay on the shared grid. If a frame needs more room, grow the whole rank gap or column gap; never move a member off its grid line.
7.3 Frames never overlap each other or cross a node that is not a member. When two frames, or a frame and an outside node, sit in neighbouring columns or ranks, grow that gap by 1 grid step, or by 2 when a line also runs through it (see frame-overlap-markup.png).
7.4 Centre a group across its parent group when it is the only group nested in that parent.
7.5 If both ends of a back edge are members of a group, keep its return track inside that group's frame.

## 8. Sequence diagrams
8.1 Leave the same clear space above and below a note as between two consecutive messages. A note never touches a message line, an arrowhead or a label. Set this in the shared row layout, never for one diagram (see sequence-note-markup.png).

## 9. Precedence
9.1 Never break an item in section 10. If a layout would break a legality item, insert space for legality rules, or to make room for a hint (rule 2.21), as rule 1.7 sets, and route again; a quality item (crossings, shape, empty area) is settled by the score of rules 2.13 and 2.14, not by space.
9.2 Before breaking a legality rule, grow the gap concerned by whole grid steps and route again, as rule 1.7 sets. Space is never grown for quality.
9.3 A specific rule beats a general one within one step of rule 9.6: diamond rules (3.6 to 3.9) beat box rules (3.4 to 3.5), and back-edge rules (5.3 to 5.4) beat forward-line rules. Hints rank above all of these (see the preamble).
9.4 Within rules 2.11 to 2.21, candidates are compared only by the score of rule 2.13, except that an honoured hint (rule 2.21) is kept; the order below applies to the routing and placement choices inside one candidate. Otherwise the earlier item in this list wins: (1) one grid and whole grid steps; (2) placement (section 2); (3) ports and line shape (sections 3 and 4); (4) fewer crossings; (5) fewer corners; (6) label position; (7) Mermaid's original rank and order.
9.5 If a choice is still tied, keep Mermaid's order.
9.6 Precedence. Hints: see the preamble and rule 2.21. Placement: honoured hints (2.21) first; then 2.18; then 2.7 (compatible with 2.18, as it keeps the main path straight); then 2.4 to 2.6 and 2.8 to 2.10 (rule 2.4 applies within this step, so a child that rule 2.4 places under another parent is placed there, not by 2.17); then 2.17, 2.16, 2.20 and 2.15; then compaction 2.11 (which keeps the cell of 2.17); then wrapping 2.12; then 2.19 picks among the resulting candidates by score and never overrides a declared direction. Routing: line hints (2.21), then 4.7, then 4.8, then 3.4 and 3.5, then 4.3. A higher item is never moved by a lower one. Section 10 items and overlaps are never broken. Ties go to Mermaid's order (9.5).

## 10. Must never (a candidate that breaks one is rejected; space is inserted for legality rules, or to make room for a hint (rule 2.21), as rules 1.7 and 5.7 set)
- A node centre off the grid, a fractional coordinate, two nodes overlapping, or unequal rank gaps beyond rule 1.5.
- A diagonal run, a curve that is not a 20 px corner, an S-curve, a hop arc, or more corners than rule 4.3 allows (a hinted line may use the corners its hint needs).
- A line whose ports line up drawn as anything but one straight run (unless a line hint (rule 2.21) says otherwise), or a run shorter than the minimum straight run after a port, before a first corner or before an arrowhead.
- A line end short of or inside the drawn outline, or on a box corner.
- Two lines sharing a port, a first run or a track where their runs overlap.
- A line through a node or label it does not connect to, or along a node outline or frame edge.
- A line crossing a frame or another line at anything but a right angle, or a crossing that breaks sections 3 to 5 (for example one that is not inside a gap). Crossings above the forced minimum that meet sections 3 to 5 are scored by rule 2.13, not rejected.
- A line that runs past its target's near side and turns back, or a diamond branch that leaves the bottom vertex and turns sideways.
- Frames overlapping, a frame crossing a non-member node, or a title strip covered or cut short.
- A label touching a line, arrowhead, corner, outline, frame or another label.
- A sequence note touching a message line, arrowhead or label.

## On hold (do not build until Dave redefines them)
- Drawing a frame on the centre line of the gap between its members and outside nodes, and reserving that centre line as a frame track that no line may use. Both break when two groups sit side by side (see adjacent-groups-markup.png).

## Resolved conflicts
1. Diamond ports: B12 and B12a used edge centres first; B12b uses vertices first, and B16 adds the run-length check. Kept B12b with B16 (3.8).
2. Elbow or Z: B3 and brief section 8 took an elbow before a Z; the later B14, A14 and A16 route bottom to top as a Z. Kept the later rules (3.4).
3. Side exits: B5 and B11 let a box line leave its left or right side while its bottom had free ports; the later A14 forbids that. Kept A14 (3.5).
4. Corner limit: B3 set 2 corners for every line, but the back edges in B17 and A13 need 3. Forward lines have 2 and back edges 3 (4.3).
5. Rearranging: A1 and brief sections 1 and 9 keep Mermaid's rank and order; A5, A12, A13, A14 and A16 change them. The later rules win, and Mermaid's arrangement is the starting point and the last tie-break (2.1, 9.4).
6. Sides and paths: brief sections 6 and 8 follow Mermaid's sides and paths; the rules here replace them.
7. Crossings: B8 banned all crossings; A14, A16 and brief section 8 allow one when nothing removes it. Kept the later rule (5.6).
8. Track spacing: brief sections 3 and 7 set 5 tracks 6 px apart; the later A16 sets 1 grid step. Kept A16.
9. Straight-line slot: brief section 6 uses slots 2 and 4 for two lines; B6 keeps a straight line in the centre. Kept B6 (3.2).
10. Spine versus a loop branch: A12 keeps the spine in one column; the later A13 puts a loop child and the spine child on either side. Kept A13 for loop branches (2.7).
11. A bypass that rejoins: A9 moved the rejoin node up beside the detour; the later A12 keeps it on the spine. Kept A12 (2.6).
12. Back-edge sides and labels: B15 and B17 described the entry side differently, and B12b and B15 placed labels differently. Replaced by one rule each (5.3, 6.2).
13. Rank gap: brief section 3 sets a 1-step gap, which cannot hold a 50 px minimum straight run. The base rank gap is now 2 grid steps.

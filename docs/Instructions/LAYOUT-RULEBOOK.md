# Diagram layout and routing rulebook

## Layout setting

Enterprise and flowchart diagrams default to `grid`. Diagram frontmatter may set `config.layout` to `grid` or `open`. An unknown value gives one warning and uses `grid`. The command-line option `--layout grid|open` sets the run default; the diagram's own setting takes precedence.

`grid` uses the shared grid and placement rules below. `open` skips shared-grid placement and permits free node and group positions. Both modes use the same routing, ports, straight runs, rounded corners and crossing rules. Boxes and unrelated group frames must not overlap in either mode.

> **Decisions applied (7 Oct 2026, these override any conflicting text below)**
> 1. Offset child in the next rank: leave the downstream side and Z into the child's upstream side while the downstream side has a free port. Use the side facing the child (elbow into its top) only when every downstream port is taken.
> 2. Minimum straight run of 50 px applies to diamond branches before their first corner. Every other line end needs a straight run of at least the arrowhead length before its arrowhead and at least the corner radius after leaving a node. The base rank gap stays 2 grid steps.
> 3. Tracks inside a gap are 6 px apart (the brief's constants). A gap grows by a whole grid step when it runs out of tracks.
> 4. A node that rejoins the spine stays on the spine.
> 5. Ties: branches go left of the spine, return tracks go right, a diamond side branch uses the side vertex first and the lower edge centre second, the label gap is the width of "n", and frame padding is 1 grid step.
> 6. The brief's "Arrangement kept" check (section 9) allows the reordering in section 2 of this rulebook.


Every rule is generic: it uses only structure and geometry, never a node id, label, sample, test or group title. Rules are written for top-to-bottom (TB) diagrams; rule 1.1 maps them to other directions. `webshop-target-rules.md` keeps the detailed history and pictures.

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
- **Spine**: the main path. Start at a node with no incoming line. At each node, follow the line to the child with the longest path to a node with no outgoing line. If one child can also be reached through another child, follow the direct line. On a tie, take the child that comes first in Mermaid's order.
- **Branch**: a path that leaves a spine node and is not on the spine. A **detour** rejoins the spine at the next spine node. An **early exit** ends, or rejoins the spine further on. A **loop branch** ends in a back edge.
- **Back edge**: a line whose target is in an earlier rank than its source. Its **return track** is the track it runs on, outside every node it passes.
- **Fan**: 2 or more lines on one side of one node that go to, or come from, nodes in one rank.
- **Group / frame**: a group is a Mermaid subgraph; its frame is the rectangle drawn round its members. Every frame has a title strip at the top and 1 grid step of padding on each side.
- **Label gap**: the width of the letter "n" in the label font. It is the clear space between a label's text and its line on each side.

## 1. Grid and spacing
1.1 For LR, read left for top, right for bottom, top for left and bottom for right. BT and RL mirror TB and LR.
1.2 Use one grid for the whole diagram. Every node sits on it, including diamond targets and group members.
1.3 Draw every node in one box size. A diamond is the same width and twice the height. If the longest label does not fit, grow the box for every node by whole grid steps.
1.4 Move nodes and grow gaps only in whole grid steps, across the whole rank or column. Never shrink a corner, straight run, label gap or padding to make something fit (see rank-gap-markup-2.png).
1.5 Make every rank gap equal to the base rank gap. A rank gap grows only to hold a label, a Z, extra tracks or a frame, and that growth changes no other gap (see rank-gap-markup.png).
1.6 Snap Mermaid's positions to the grid one whole rank or column at a time. If two nodes need the same place, push the second one 1 grid step on in rank order, and repeat until nothing overlaps.

## 2. Placement
2.1 Start from Mermaid's ranks and in-rank order, and change them only as rules 2.2 to 2.10 say.
2.2 Put every spine node in one column, each directly under the one before. The spine never moves sideways to make room (see main-spine-markup.png).
2.3 If a node has exactly one child, put the child directly under it, unless rule 2.4 places the child under another parent.
2.4 Put a node with several parents under its first parent that has no other child directly below it, taking parents in Mermaid's order. If every parent has one, keep the node's snapped column (1.6).
2.5 If a node has several children, keep directly below it the child with the longest path to a node with no outgoing line. Put the others in the neighbouring columns on the side where their own children are.
2.6 Put detours in the column next to the spine, in the ranks between the two spine nodes they join. Put early exits in the column beyond the detours. Put both on the side of the spine where Mermaid placed most of them; on a tie, use the left side. A second branch from the same diamond goes on the opposite side.
2.7 When a diamond has two children, one on the spine and one starting a loop branch, put one in the column on each side of the diamond's column, in Mermaid's order. The spine continues straight under its child (see wait-loop-markup.png, cycle-grid-markup.png).
2.8 A chain of nodes that each have one parent and one child stays in one column as it passes into and out of a group (see nested-chain-markup.png).
2.9 When two nodes in one rank both link to the same two nodes in the next rank, put each child directly under one parent. Choose the pairing with fewer crossings (see cross-links-markup.png).
2.10 Within a rank, swap two nodes if that removes a crossing and adds none. Where several lines run between two ranks, first order the lower rank by the mean column of each node's parents. Then put each lower node under the parent it has the most lines from; on a tie, use Mermaid's order (see many-to-many-markup.png).

## 3. Ports and diamonds
3.1 Use one port per line, so lines never share a port or a first run. A line meets its port square-on and ends exactly on the drawn outline (see f11-straight-markup.png).
3.2 Fill box slots by the number of lines on the side: 1 line uses slot 3; 2 use 2 and 4; 3 use 2 to 4; 4 use 1, 2, 4 and 5; 5 use all five. A straight line always keeps slot 3. More than 5 lines grows the node along that side by whole grid steps.
3.3 On each side, order the ports by where each line's other end is, so no two lines cross next to the node.
3.4 Route a forward line between boxes in this order. First, bottom to top, as a straight line when the ends line up. Otherwise, bottom to top as a Z. Use an elbow from the bottom into the target's near side only when the target's top has no free port, or when the line ends a detour.
3.5 Send a line out of a box's left or right side only for a back edge, a target in the same rank (into its near side), or when the bottom has no free port. Even then, the target must lie wholly beyond that side.
3.6 A forward line enters a diamond at the top vertex. Further incoming lines use the side vertices.
3.7 A branch to a target directly below the diamond leaves the bottom vertex as a straight line.
3.8 A branch to a target in another column first uses the side vertex facing it: out horizontally, one corner, then down into the target's top. If that vertex is taken, or its run before the corner would be shorter than the minimum straight run, use the edge centre between that vertex and the bottom vertex, with the same shape (see decision-accepted.png, merge-decision-markup.png).
3.9 Never leave the bottom vertex and then turn sideways. Two branches to mirror-image targets are mirror images (see decision-three-way-markup.png).

## 4. Line shape
4.1 Build every line from straight runs and corners only. No diagonals, S-curves, free curves or hop arcs.
4.2 When the ports line up on one column or row line, draw a single straight run, whatever the two shapes' sizes.
4.3 A back edge has at most 3 corners and every other line at most 2. A line never loops on itself, and never runs past its target's near side and turns back.
4.4 Every line runs straight for at least the minimum straight run after leaving its source and before its arrowhead, so no corner sits closer than that to either end.
4.5 Put a Z's middle run on a track in the gap between the source and target ranks (see lr-fanout-markup.png).
4.6 A line never runs along a node outline or a frame edge. It crosses a frame only at a right angle, and a line whose target is a group ends on the frame edge, square-on.

## 5. Tracks and crossings
5.1 Two lines never use the same track where their runs overlap. If a gap has fewer free tracks than lines, grow it by 1 grid step and route again.
5.2 In a fan, the line to the node furthest out takes the outermost track and port, and the line to the nearest node takes the innermost. Apply this to fan-out and fan-in, and to both halves of the fan (see fan-nesting-markup.png).
5.3 A back edge leaves its source's downstream side if that side is free; if not, it leaves the side facing its return track. It enters the middle of the target's side that faces the return track (see loop-back-markup.png).
5.4 Put the return track on the same side of the spine as the back edge's source. If the source is on the spine, use the side with no branches; if that is still tied, use the right side. When several back edges share a side, the longest takes the outermost track.
5.5 Each crossed line from rule 2.9 leaves its parent's bottom at the slot beside the centre, on the side towards its target. It takes its own track, crosses the other line once at a right angle, and enters its target's top at the slot beside the centre, on the side towards its source.
5.6 Before accepting a route, swap any two lines' tracks or ports if that removes a crossing and adds none. A crossing that remains is allowed only at a right angle inside a gap.
5.7 If no route meets every rule after the gaps have grown, report a routing failure. Never draw a tangle.

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
9.1 Never break an item in section 10. If no layout avoids all of them, report a failure (5.7).
9.2 Before breaking any other rule, grow the gap concerned by whole grid steps and route again.
9.3 A specific rule beats a general one: diamond rules (3.6 to 3.9) beat box rules (3.4 to 3.5), back-edge rules (5.3 to 5.4) beat forward-line rules, and 2.7 beats 2.2.
9.4 Otherwise the earlier item in this list wins: (1) one grid and whole grid steps; (2) placement (section 2); (3) ports and line shape (sections 3 and 4); (4) fewer crossings; (5) fewer corners; (6) label position; (7) Mermaid's original rank and order.
9.5 If a choice is still tied, keep Mermaid's order.

## 10. Must never (each one fails the diagram)
- A node centre off the grid, a fractional coordinate, two nodes overlapping, or unequal rank gaps beyond rule 1.5.
- A diagonal run, a curve that is not a 20 px corner, an S-curve, a hop arc, or more corners than rule 4.3 allows.
- A line whose ports line up drawn as anything but one straight run, or a run shorter than the minimum straight run after a port, before a first corner or before an arrowhead.
- A line end short of or inside the drawn outline, or on a box corner.
- Two lines sharing a port, a first run or a track where their runs overlap.
- A line through a node or label it does not connect to, or along a node outline or frame edge.
- A line crossing a frame or another line at anything but a right angle, or more crossings than Mermaid's reference.
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

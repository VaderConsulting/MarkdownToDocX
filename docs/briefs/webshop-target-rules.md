# Layout and routing rules taken from the web shop target picture

Dave approved `webshop-target-layout.png` (drawn by `webshop-target-layout.py`) as the way a finished diagram should look on 7 October 2026. The rules below describe how every node and line in that picture was placed, written generally so the engine can apply them to any flowchart. Each rule ends with where it shows up in the picture. The numbers are for 140×50 boxes; the engine uses its own grid constants.

## A. Placing nodes

**A1. Keep Mermaid's ranks.** Every node stays on the rank (row, for a top-down diagram) that Mermaid gives it, and all nodes on the same rank share the same centre line. In the picture, Java Application Hosting and Database Service share a row, as do Checkout and orders table, and Shop Web UI and Order.

**A2. Keep chains in one column.** When a node has one line going into the next rank, the child sits directly beneath it, so the line is straight. Chains of these stay in a single column for as long as they last. In the picture, Java VM, Java Application Hosting, Checkout and Online Ordering form one column, and so do PostgreSQL, orders table and Order.

**A3. A node with several parents goes under the parent that gives it a straight line.** If one of its parents is directly above in a column with nothing else heading down it, the node goes under that parent. In the picture, Order has two parents, Checkout and orders table. It sits under orders table, so that line is straight, and the line from Checkout is the one that bends.

**A4. A node with several children keeps one child in its column.** The child that continues the longest chain sits straight below, and the others move to neighbouring columns on the side where their own later lines go. In the picture, DC LAN keeps App Server Node below it, and Load Balancer moves to the left because its route continues into the left half of App Server Node. Checkout keeps Online Ordering below it, and Shop Web UI moves to the right.

**A5. Order each rank to remove crossings.** Within a rank, nodes keep Mermaid's left-to-right order unless swapping two of them removes a line crossing without adding one. In the picture, swapping Order and Shop Web UI removed the last crossing.

**A6. Make room rather than squeeze.** If a gap between two nodes is too narrow for a line to make its corners and still have a straight run before the arrowhead (rule B4), move the outer column or the lower rank further out in whole grid steps. Never shrink the corners or the end runs to fit. In the picture, PostgreSQL, orders table and Order moved right, and Online Ordering moved down, for this reason.

**A7. Group frames fit their contents.** Each group frame has a title strip that is never covered, with even padding around its contents. A group that is the only group nested inside its parent is centred across the parent (vertically for left-to-right diagrams). In the picture, App Server Node is centred in Primary data centre.

## B. Drawing lines

**B1. One line style.** Every line is made only of horizontal and vertical straight runs, joined by small rounded corners of the same radius (about 20 px). There are no S-curves, diagonal runs or free-flowing curves anywhere.

**B2. Straight when the ends line up.** If the exit point and the entry point share a column or a row, the line is one straight run with no corners. In the picture, this applies to DC LAN to App Server Node, Load Balancer to App Server Node, Java VM down to Online Ordering, and PostgreSQL down to Order.

**B3. Use as few corners as possible.**
- **No corners** when the ends line up (B2).
- **One corner (an elbow)** otherwise. The line leaves one side of the source and enters a side of the target at right angles to it, for example out of the bottom and into the left or right side, or out of a side and into the top. In the picture, this applies to DC LAN to Load Balancer, PostgreSQL to Database Service, shop.war to Checkout, Database Service to Checkout, and Shop Web UI to Online Ordering.
- **Two corners (a Z shape)** only when no elbow fits, because the side an elbow needs is already taken or too close. The line leaves and enters facing sides (bottom to top, or right to left), and the middle run sits exactly halfway across the gap it crosses. In the picture, Checkout to Shop Web UI crosses halfway down the gap between the two rows, and Checkout to Order drops halfway across the gap between Shop Web UI and Order.
- Never more than two corners, and never a loop or a detour back on itself.

**B4. Leave and arrive square-on.** Every line leaves its source at right angles to the side it starts on, and enters its target at right angles to the side it ends on. Before each arrowhead there is a straight run at least as long as the arrowhead, so the head lines up with the line. In the picture, every line follows this rule.

**B5. Choosing the elbow.** Where both elbows would work (out of the bottom and into a side, or out of a side and into the top), use the one whose sides are still free once straight lines have taken their ports (B6). For a top-down diagram, prefer leaving from the bottom and entering a side when both are free. In the picture, DC LAN's bottom is taken by its straight line, so its line to Load Balancer leaves from its left side and enters Load Balancer's top. PostgreSQL's bottom is taken by its straight line to orders table, so its line to Database Service leaves from its left side and enters Database Service's top. Database Service's bottom is free and Checkout's top is taken, so that line leaves from the bottom and enters Checkout's right side.

**B6. Ports.** A side with one line uses its centre. Straight lines take their ports first and keep the centre, because moving them would bend them. Other lines on the same side take the next slot towards their other end, and lines that share a side are ordered so they don't cross: the line coming from higher up enters higher, and the line going further down leaves lower. In the picture, Checkout's bottom has the straight line to Online Ordering at the centre, and the line to Shop Web UI in the slot to the right. Checkout's right side takes the line from Database Service in its upper slot and sends the line to Order from its lower slot.

**B7. Real outlines.** Lines start and end on the shape's actual outline, including the rounded ends of stadium shapes and the slanted edges of diamonds, never on an invisible bounding box. In the picture, both lines on Checkout's right side meet its rounded end.

**B8. No overlaps.** No line passes through a node, runs along a node or frame border, shares a run with another line, or crosses another line. A line may cross a group frame only at right angles. In the picture, shop.war's line and PostgreSQL's line cross the frame borders straight on.

**B9. Lines into a group** end on the frame edge, with the same square-on arrival as a node. If two lines enter the same side of a group, each comes straight down from its own source. In the picture, Load Balancer and DC LAN both enter App Server Node's top this way.

**B10. Labels** sit centred on a straight run of their line, on a white background, and never on a corner. In the picture, the HTTPS label sits on Load Balancer's line to App Server Node.

## C. Order of work

1. Rank and order the nodes the way Mermaid does (A1 and A5).
2. Assign columns, keeping chains straight (A2 to A4).
3. Give straight lines their ports, then route everything else as elbows, using a Z shape only where no elbow fits (B2 to B6).
4. Where a corner or end run doesn't fit, move nodes out by whole grid steps and route again (A6).
5. Size the group frames last (A7), then run the checks.

Note: the node names above only illustrate each rule. The engine must apply every rule from generic attributes (rank, parents and children, ports, sides, bounds, nesting), never from node ids, labels, sample numbers or test names.

## B11. A line to a far child in the next rank (from Dave's "Layered or staged view" markup, 7 Oct 2026)

Picture: `layered-view-markup.png`. Headers / metadata has three children in the next rank. Two sit below it and take its bottom ports. The third, "Type: command / document / event", is far to the right, past the Payload column.

Wrong: the line leaves from the bottom corner of the parent, hugs the bottom edge of the row and lands at an angle on the child's corner.

Right: the line leaves from the middle of the parent's free side that faces the child (here the right side), square-on. Then it does one of two things:
- (a) It runs horizontally in the gap between the two ranks and turns down into the middle of the child's top, square-on, with a straight run before the arrowhead.
- (b) It drops down and runs horizontally at the child's centre height into the middle of the child's near side (here the left side), square-on.

Prefer (a) when the child's top port is free. Use (b) when the top is taken or when (a) would cross a node or another line. Both are generic rules: they depend only on which ports are taken and where the child is, never on names.

## B12. Decision branches to targets above and below (from Dave's "Valid?" markup, 7 Oct 2026)

Picture: `decision-branch-markup.png`. In an LR flow, the Valid? diamond has a Yes target (Normal channel) up and to the right, and a No target (Invalid message channel) down and to the right.

Wrong: the Yes line leaves the right vertex, runs horizontally past it, and then doubles back up into the target's bottom-left corner. The No line leaves the lower-right edge as a soft curve.

Right: each branch leaves from the port on the diamond side that faces its target. That means the centre of the upper-right edge for a target above, and the centre of the lower-right edge for a target below, following the diamond port rule. The line leaves vertically (up or down), makes one rounded elbow, and runs horizontally into the middle of the target's near side (here the left side), square-on, with a straight run before the arrowhead. The two branches mirror each other. The right vertex stays free for a target level with the diamond.

General rules:
- Never run past a target's near side and double back.
- Never end on a corner.
- Decision branches use the same straight-runs-with-rounded-corners style as every other line, with no curves.
- Labels (Yes or No) sit on a straight run of their own line.

## S1. Sequence diagrams: spacing around notes (from Dave's markup, 7 Oct 2026)

Picture: `sequence-note-markup.png`. The "TTL elapsed" note starts right under the "Message (expiresAt)" arrow, so its top touches the arrowhead.

Rule: every note keeps the same vertical clearance above and below it as messages keep from each other. A note never touches or overlaps a message line, an arrowhead or a label. The gap above a note matches the gap between the note and the next message's label. Spacing comes from the row layout (Mermaid's note and box margins), not from moving one diagram's note.

## B13. Edge labels sit in a clean gap (from Dave's "Format v1 / v2 / v3?" markup, 7 Oct 2026)

Picture: `edge-label-markup.png`.

Good: "v1" sits centred on a straight run, with the line broken around it and the same clear gap on both sides.

Bad: with "unknown", the line runs right up to the text and the label background clips it. "v2" is squeezed against the arrowhead.

Rule: every edge label is centred on a straight run of its own line, with an even gap between the text and the line on each side. The gap is the same for every label, about one character width. A label never touches an arrowhead, a corner, a node outline or another label. If the straight run is too short for the label plus its gaps, lengthen the run by making room in whole grid steps, rather than squeezing the label. Size the gap from the measured text width, so long labels such as "unknown" get the same gap as short ones such as "v1".

## B12a. Decision branches in a top-to-bottom flow (from Dave's "Inspect content" markup, 7 Oct 2026)

Picture: `decision-tb-markup.png`. The Inspect content diamond has two targets below it, Destination A to the lower left and Destination B to the lower right.

Wrong: Rule A leaves the bottom vertex and doubles back to the left, crossing under the diamond. Rule B leaves the lower-right edge, drops and then runs sideways onto Destination B's top-left corner. The two lines cross each other's paths.

Right: this is the top-to-bottom mirror of B12. Each branch leaves from the centre of the diamond edge that faces its target: the lower-left edge for a target down-left, and the lower-right edge for a target down-right. It leaves horizontally outward, makes one rounded elbow, and drops vertically into the middle of the target's top, square-on, with a straight run before the arrowhead. The two branches mirror each other and never cross. The bottom vertex stays free for a target directly below. Each label (Rule A, Rule B) sits on the straight run of its own line, as in B13.

## B12b. Correction to B12 and B12a: diamond vertices come first (Dave, 7 Oct 2026)

Picture: `decision-accepted.png` ("Matches filter?"). Dave accepted this as the pattern.

The incoming line enters the top vertex. Yes leaves the left vertex horizontally, makes one rounded corner and drops vertically into the middle of Forward's top. No mirrors it from the right vertex. Each label sits on its straight drop with clear gaps.

This follows the existing diamond port rule: the four vertices are the first ports, and edge centres are used only when the suitable vertices are already taken. So for both B12 and B12a, a branch leaves from the free vertex on the side towards its target, leaving straight out from that vertex, and makes one rounded corner into the middle of the target's near side.
- For Inspect content (TB), that means the left and right vertices, exactly like this picture.
- For Valid? (LR), with the input on the left vertex, Yes leaves the top vertex upwards and turns right into the middle of Normal channel's left side, and No mirrors it from the bottom vertex.

Use edge-centre ports only when the needed vertices are taken. This supersedes the "edge centre first" wording in B12 and B12a.

## B14. Left-to-right fan-out to targets above and below (from Dave's "Message filter" markup, 7 Oct 2026)

Picture: `lr-fanout-markup.png`. In an LR flow, Message filter has two targets in the next rank: Core consumer up and to the right, and Telemetry only down and to the right.

Wrong: both lines leave the right side, run right, and then turn up or down to land on the targets' bottom or top edge near the corner. Both labels are cramped against their lines.

Right: in the flow direction, lines leave the source's downstream side (right side for LR) and enter the target's upstream side (left side). When the two sides don't line up, use a Z:
1. A short horizontal run out of the source, square-on.
2. One rounded corner, then a vertical run placed halfway across the gap between the ranks.
3. A second rounded corner, then a horizontal run into the middle of the target's left side, square-on, with a straight run before the arrowhead.

When one box has several lines leaving the same side, their ports spread evenly along that side, ordered so the lines don't cross. Here, the upper target takes the upper port and the lower target the lower port. Labels sit on a straight run of their own line with even gaps, as in B13. Lines that leave from the same side never share a run.

This applies in every direction, mirrored for TB, BT and RL. It applies to ordinary boxes; decision diamonds follow B12b.

## B15. Loops back to an earlier node (from Dave's "More routing?" markup, 7 Oct 2026)

Picture: `loop-back-markup.png`. The flow is TB: Incoming message, Determine next destination, Send, More routing?, then No to Complete. Yes loops back up to Determine next destination.

Wrong (left half of the picture):
- Send is pushed off to the left, so the main chain zig-zags.
- The Yes line leaves the diamond's top vertex, which is the input side.
- It wanders up through the gap and lands on Determine's bottom-right corner, with Send's line coming into the diamond's left vertex.

Right (Dave's sketch on the right):
- The main chain stays in one straight column (A2). Each node feeds straight down into the next: Determine to Send to the top vertex of More routing?, then the bottom vertex to Complete with No.
- A back edge, meaning a line to a node in an earlier rank, never displaces the main chain.
- It leaves the free side vertex of the diamond (or the free side of a box) facing the outside of the drawing, here the right vertex, horizontally.
- It runs out to a return track just outside the widest node it passes, then vertically up past the intermediate nodes, then horizontally into the middle of the target's same side (here Determine's right side), square-on, with a straight run before the arrowhead.
- So it's three straight runs and two rounded corners, a "C" shape that hugs nothing and crosses nothing.
- Several back edges on the same side take separate parallel tracks, with the outermost track for the longest loop.
- Its label (Yes) sits on the first horizontal run or on the vertical run, per B13.

## A8. Equal gaps between ranks (from Dave's "Load balance" markup, 7 Oct 2026)

Picture: `rank-gap-markup.png`. The gaps from Queue to Load balance and from Load balance to the Instance row are the same. The gap from the Instance row to Shared DLQ policy is visibly smaller, about two-thirds.

Rule: the clear gap between the bottom of one rank and the top of the next is the same for every pair of ranks in a diagram (left-to-right gaps for LR). Measure it from the rank's tallest node, so a double-height diamond doesn't eat into the gap. A gap only grows, in whole grid steps, when it must hold labels, a Z middle run or several parallel tracks. When one gap grows, the base gap stays the same everywhere else. A gap never shrinks below the base gap because its lines enter from the side.

## B12c. A three-way decision (from Dave's "Type?" markup, 7 Oct 2026)

Picture: `decision-three-way-markup.png`. The Type? diamond has three targets below it: Create handler (left), Update handler (directly below) and Unknown handler (right).

Wrong: all three lines leave the bottom vertex and split sideways just under it, so they share a stub and the side lines run along under the diamond.

Right (per B12b): the target directly below gets the bottom vertex as a straight line. The other two each leave from their own free side port, then make one rounded corner and drop vertically into the middle of their target's top. Dave's sketch starts them on the lower slanted edges near the side vertices; the side vertices themselves are acceptable, as in "Matches filter?". Lines never share a port or stub, and labels sit on each line's own straight run.

## A9 and B16. A bypass that rejoins (from Dave's "Detour flag?" markup, 7 Oct 2026)

Picture: `bypass-rejoin-markup.png`. The flow is Message, then Detour flag?. On goes to Inspection / shadow, and Inspection goes to Rejoin main path. Off goes directly to Rejoin main path.

Wrong:
- The on line leaves the left vertex, and its rounded corner is cramped right up against the vertex (circled, "why?").
- Off drops straight down through the gap so that Rejoin sits under the diamond (crossed out).
- Inspection's line runs down and wraps around into Rejoin's left side ("ugh").

Right (Dave's sketch):
- A9: when a node C has two parents, a decision D and one of D's own children A (so D to C bypasses A), place C in A's rank, beside A on the side away from A's branch. Then A to C is a straight horizontal line into C's near side, and D to C is the decision's other branch dropping into C's top. Do this whenever it removes a wrap-around and adds no crossing.
- B16: each branch leaves the diamond with a straight run of at least the corner radius plus one grid step before its first corner, so the corner never sits on top of the vertex. When a branch would leave from a side vertex and turn down straight away, use the lower slanted edge's centre port instead, as in the sketch. Here, on leaves the lower-left edge, turns left and drops into Inspection's top. Off leaves the lower-right edge, turns right and drops into Rejoin's top. Inspection to Rejoin runs straight across into Rejoin's left side.
- Labels follow B13.

A8 second check: `rank-gap-markup-2.png` ("Purge request"). The rows Export optional archive, Remove messages and Log counts + ticket ref are stacked with gaps about a third of the normal gap ("Grid?"). Every row gap must equal the base gap, so the chain below Export must keep the same spacing as the rows above it. A short straight line is no reason to squeeze a gap.

## B2 check: aligned ends are dead straight (from Dave's F11 markup, 7 Oct 2026)

Picture: `f11-straight-markup.png` (F11, basic shapes in LR). The Diamond decision to Circle line leaves the right vertex, dips down and hooks back up into the circle, even though the two centres share a row.

Rule B2 applies to every shape pair: when the source's port and the target's centre line up on the flow axis, the line is one straight segment from the source outline (here the vertex) to the target's real outline (here the circle's edge), with the arrowhead on that segment. Different shape sizes (a 150×120 diamond next to a circle) must not offset the centre line. Every node in a rank shares one centre line (A1).

## A10 and B17: The grid applies to every node, and a cycle back to the start (picture: cycle-grid-markup.png)
- The fault: the grid is used for only some nodes and then ignored for the rest. Every node, including every target of a decision, must be placed on the same grid, centred on its rank's column line (and row line), with no node left at a free position off the grid.
- The bottom sketch in the picture is the target arrangement: the start box, then the decision, then its two targets stacked above and below in the next column. The forward edge leaves the start box's right side into the decision's left vertex. Yes leaves the vertex facing its target and enters that target's near side, and No leaves the lower vertex and enters the near side of the lower target.
- An edge back to the first node of a cycle never enters the side the forward edge leaves. It leaves the free far side of its source (facing away from the graph), runs on a return track outside the drawing (below for LR, beside it for TB), and comes up into the middle of the target's free side facing that track (its bottom in LR). That gives a U shape with rounded corners which crosses no other line or node.

## B12d check: a decision whose side vertices are taken by incoming edges (picture: merge-decision-markup.png)
- When the top and both side vertices carry incoming lines and two branches go to targets below-left and below-right, each branch leaves the centre of the lower slanted edge facing its target, takes one rounded corner, and drops straight into the middle of the target's top, with its label on the straight drop. Branches never come out of the bottom vertex and then run sideways under the diamond, and they never cross each other.

## A11: A chain through a nested group stays on one line (picture: nested-chain-markup.png)
- When a single chain of nodes (each with one parent and one child in the flow direction) passes into a nested group and out again, every node in the chain, both outside and inside the group, sits on the same grid row (same column for TB). The lines between them are dead straight, including the ones that cross the group frame, with no drops, elbows or Z-bends.
- The nested group is sized and positioned around its nodes on that row; the outer nodes are not pushed to a different row to make room for the frame or its title. The title strip grows the frame upward (or sideways for TB), not the chain off its line.

## A12: The main path is one straight spine, and branches hang off it (picture: main-spine-markup.png)
- Find the longest path from the start node to the end node (the spine). Every node on it sits in one grid column (one row for LR), directly under the previous one, and every spine edge is a dead-straight drop between them. A decision on the spine sends its spine branch straight out of the vertex facing the next spine node.
- Side branches leave the spine and go to a side column. A branch that ends the flow early (for example a reject or error path with no way back) goes to the outermost side column, leaves the decision's free side vertex, and runs down that column to its node. Its line into the end node enters the end node's near side.
- A short detour that leaves one spine node and rejoins a later spine node (for example an optional step) sits in a side column next to the spine, level with the spine nodes it spans. It leaves its decision's side vertex, drops into the middle of the detour node's top, and rejoins the later spine node by entering its near side, square-on.
- Detours and early exits go on the same side when they fit, nearest to the spine first, and lines never cross. The spine never jogs sideways to make room for a branch.
- The diamond rules (B12b) still apply. In the example, Start, Ingest, Valid?, Parse, Mermaid fence?, Build, Archive? and Done form the spine. Render SVG and Optional archive are detours on the left, and Reject is the early exit in the outermost left column.

## A13 and B18: A decision with a wait loop (picture: wait-loop-markup.png)
- A node with exactly one outgoing edge sits directly under its parent, and its single edge is a dead-straight drop into the next node's top (or its top vertex, for a diamond). An edge never splits sideways halfway along; each edge has exactly one start and one end.
- A decision's two branches go to the next rank, one child below-left and one below-right, centred under the diamond's grid column. Each branch leaves the side vertex facing its child, takes one rounded corner, and drops into the middle of the child's top (B12b). The child that continues forward keeps its own chain straight under it.
- A child whose only edge loops back to an earlier node (a wait or retry) leaves the child's free outward side, runs on a return track outside the widest node and outside the group frame's content, and enters the middle of the earlier node's same side: a C shape with rounded corners (B15). It never crosses or joins another line.
- A group frame wraps all its member nodes, including the loop's return track, with the usual padding.
- ON HOLD (7 Oct 2026, to be redefined with Dave): the frame-on-the-midline part of this rule breaks when two groups sit side by side, because both frames claim the same midline and touch (picture: adjacent-groups-markup.png). Do not implement frame placement on midlines or the reserved-track rule below until it is redefined. One grid for the whole diagram still stands.
- One grid for the whole diagram. Nodes inside and outside groups share the same grid columns, rows and rank gaps; a group never creates its own inner grid or shifts its members off the shared grid. A group's frame line runs at the halfway mark of the gap between its member nodes and the neighbouring outside nodes (the same midline that routing tracks use), so an outside node never touches or crowds the frame. Where a frame needs room for its title strip or for nested frames, the shared gap grows in whole grid steps for the whole rank or column, never just for the group.
- (ON HOLD, see above) A midline used by a frame is reserved for that frame. No line runs along it: lines may only cross a frame square-on, and any parallel run takes a different routing track. If that gap has no other free track, it grows by a whole grid step across the whole rank or column to make one.

## A14 and B19: Crossed links between two parents and two children (picture: cross-links-markup.png; the sketch is a guide, not exact)
- When two nodes in one rank each link to the same two nodes in the next rank, put each child directly under one parent (keeping the order that gives the fewest crossings), so the parent-to-own-child lines are dead-straight drops between centres. Don't spread the children outward away from their parents.
- Each cross line leaves its parent's bottom at a port offset toward the other parent (spread ports per B14, never the centre already in use), runs down to the middle of the rank gap, crosses to the other column on that horizontal track with rounded corners, and drops square-on into the target's top at a port offset toward the source side. The two cross lines use separate horizontal tracks or cross once at a clean right angle, never sharing a run.
- No line leaves a node's side to dive down past the node when its bottom has free ports.

## A15: Neighbouring groups never overlap (picture: frame-overlap-markup.png). This partly answers the on-hold frame rule.
- Group frames never overlap each other, and no frame crosses a node that isn't one of its members. In the picture, frames one and two interlock and cut through Ingress, Delta and Egress.
- When two groups (or a group and an outside node) sit in neighbouring columns, the gap between those columns grows by one or two whole grid steps. That leaves room for each frame's own padding plus a clear space between the two frames, so they never touch or share a line. The growth applies to the whole column gap across the diagram, keeping the one shared grid. Do the same for neighbouring rows in TB/LR as needed.
- Prefer one extra grid step, and use two when a line also has to run through that gap.

## A16 and B20: Many links between two groups (picture: many-to-many-markup.png, Dave: "Yuck", it looks like the router gave up)
- Order the nodes in the lower rank to minimise crossings against the upper rank (barycentre or median ordering), and put each lower node under the upper node it is most linked to. Keep the one shared grid, and use A15 spacing between the frames.
- Every link leaves its source's facing side (bottom for TB) and enters its target's facing side (top), square-on. Ports on a side are spread evenly and ordered by where their other ends lie, so lines leaving one node never cross each other at the node.
- The gap between the two ranks (and between the two frames) gets one horizontal track per crossing line, separated by a grid step, and grows in whole grid steps until every line has its own track. Lines never run along a frame edge, never jump over each other with hop arcs, never leave a side and loop back, and never enter a node at an angle.
- Crossings between lines are allowed only as clean right angles in the track area, and the ordering keeps them to a minimum.
- If the router can't find a clean route, that's a bug to report. It must not fall back to a tangle.

## B21: Nest fan tracks so they never cross (picture: fan-nesting-markup.png, Dave: "Crossing over was not necessary")
- When one node fans out to many nodes (or many fan in to one), assign the parallel tracks and the ports by distance: the node furthest out gets the outermost track and the outermost port, and the nearest gets the innermost. Ports on the source and target sides are ordered to match, so every line is nested inside the next and none crosses another. Zero hop arcs.
- Do this on both sides of the fan (fan-out from the start, fan-in to the end) and on both halves (above and below the centre line). The red lines in the picture show the nesting.
- General check: before accepting a routing, if two lines cross and swapping their tracks or ports removes the crossing without adding another, swap them.

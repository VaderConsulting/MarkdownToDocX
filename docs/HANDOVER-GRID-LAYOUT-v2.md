# Grid layout and routing: what is required

## Goal
Every diagram is laid out on one shared grid. The result is compact and tidy, like the application landscape picture: many groups side by side, nested groups, and many lines between groups, with no wasted space and no tangles.

## What tight means
1. Every node, group frame and line track sits on the same grid.
2. Nodes in a row or column line up exactly.
3. The gap between two neighbours is as small as the rules allow, and no smaller.
4. A gap grows only when it needs room for a label, a group title, a nested frame or more line tracks. It grows by whole grid steps, across the whole row or column, and never shrinks.
5. All rows have the same gap, measured from the tallest box in each row.
6. Sibling groups sit side by side in rows and columns. They are packed against each other, with the same gap between any two frames.
7. A nested group sits inside its parent with one grid step of padding on every side, plus room for the title.

## Lines
1. Every line is made of horizontal and vertical straight runs joined by rounded corners. There are no curves and no diagonals.
2. Every line leaves a box straight out of a side and arrives straight into a side, with a final straight run at least the length of the arrowhead.
3. A line never cuts through a box and never runs along a box edge or a group frame. It crosses a frame only at a right angle.
4. Lines do not cross unless there is no other route. Two lines that cross are swapped if swapping removes the crossing.
5. When one node fans out to many, the furthest target gets the outermost port and track, and the nearest gets the innermost. This gives no crossings.
6. When several nodes in one group link to several nodes in another, the target row is reordered to match the source order. Every line leaves the bottom of its box, enters the top of its target, and has its own track in the gap.
7. Lines have their own tracks in a gap, a fixed distance apart. A gap grows by a whole grid step when it runs out of tracks.
8. Edge labels sit on a straight run of their line, in a break of the line, close to it, and never on a corner or on another line.

## Layout
1. The longest path from start to end is one straight centre column.
2. Paths that leave and rejoin it sit in a column beside it. Dead ends go furthest out.
3. A node outside a group sits at least one grid step from the group's frame.
4. Group frames never overlap each other or any node that is not a member.
5. Where two groups, or a group and an outside node, are neighbours, the shared gap grows by one whole grid step, or two when a line also runs through it.

## Layout modes
1. A diagram chooses its mode in its frontmatter with `layout: grid` or `layout: open`. The default is `grid`.
2. `grid` follows every rule above.
3. `open` places nodes and groups freely, with no shared grid. The line rules above still apply.
4. An unknown value gives one warning and falls back to `grid`.

## Rules on hold
Do not build these. They will be redefined later.
1. A group frame running on the midline of the gap between its members and the outside nodes.
2. Reserving that midline for the frame.

## Target
The test diagram "Grid stress test: application landscape" in `enterprise.md` is the target. It has about 20 groups, 5 of them nested, 166 elements and 180 relationships. It must lay out compactly on one grid, with no overlaps and no needless crossings.

## Working rules
1. Every rule is generic. It depends only on rank, parents and children, ports, box and frame edges and group nesting. It never depends on a test name, sample number, file name, node id, node label or group title.
2. Never edit, weaken or delete an existing test to make it pass.
3. Follow `CLOUD-AGENT-WORKING-PRACTICES.md`.

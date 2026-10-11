# Briefing: grid layout pass on top of the Mermaid layout

Repository: https://github.com/VaderConsulting/MarkdownToDocX
Owner: Dave Robinson. He reviews pictures and test results before anything is merged.
When this runs: now. The Mermaid-faithful layout work (the faithful dagre layout in `src/MermaidRecognition.Layout/Dagre/`, with curved lines and double-height decision diamonds) was merged into `master` as commit `fcdecf5` on 7 October 2026. Start from `master` on a new branch.

## 1. What this pass is

Dave hand-draws his diagrams on a grid, and they look great: every box is the same size (apart from decision diamonds, which are twice as tall), boxes line up in rows and columns, and lines run on tidy, evenly spaced tracks. This pass makes markdown2docx draw diagrams the same way.

The input is the layout real Mermaid would draw, which the earlier work produces and checks against mermaid-cli. The grid pass **tidies that layout onto a grid. It never re-arranges it.** Mermaid decides the arrangement: which rank each node is in, the order within each rank, which group each node belongs to, and which side each line leaves and enters. The grid pass decides only exact sizes, positions, ports and tracks.

This is the most important rule in the briefing. An earlier attempt built grid placement from scratch, and Dave judged the result terrible (see section 10).

## 2. Scope

- The rules are shared engine rules for **all diagram types** markdown2docx lays out: flowchart now, and `enterprise` (ArchiMate) and others later through the same engine. Build them in the shared layout and routing code, not in a flowchart-only class.
- ArchiMate colours, shapes, icons and line styles are out of scope. They belong to the separate enterprise work.
- Only grid geometry, ports, tracks and routing are in scope.

## 2a. Dave's own reference drawing

Dave's hand-drawn ArchiMate application landscape is attached as `dave-archimate-reference.png`. It shows the look this pass should produce. Study it before starting, and match its geometry, not its colours.

- Every element box is the same size, and boxes sit in clean rows and columns inside each group.
- Groups are tidy rectangles with a title tab and even padding. They're laid out as a grid of panels, and neighbouring groups are separated by clear gutters.
- Lines leave and enter on the facing side, and several lines on one side are spread out instead of all meeting at one point. (Lines use straight runs joined by rounded corners; see section 8.)
- Lines run in the gutters between boxes. They don't hug group borders or run on top of each other, and arrowheads have room.
- Elements nested inside an orange Location container keep the same box size and grid inside it.
- Every line style follows the same routing rules: dotted data flows (single or two-way), dependencies and triggers.

The legend at the bottom right of the drawing defines the notation. Application Service, Application Component and Data store boxes are light blue (a service with details provided has a brighter outline). Location is an orange container with a pin icon. Device is a green 3D box. Data flows are dotted black lines with an arrowhead at one or both ends. Dependency is a pale green-grey line with an open arrowhead, and Trigger is a darker green line with a filled arrowhead.

ArchiMate colours, icons and notation stay out of scope for this pass. The drawing is a target for spacing, alignment and line routing.

## 3. Units and constants

All spacing derives from one base unit. Every value is a whole number of pixels: no fractions anywhere, and nothing below 1 px. Each value is a **named constant**, and constants are never adjusted to make a diagram pass.

| Constant | Value | Meaning |
|---|---|---|
| `GridUnit` (U) | 30 px | The base unit. |
| `NodeWidth` | 5U = 150 px | Standard node width. |
| `NodeHeight` | 2U = 60 px | Standard node height. |
| `DiamondHeight` | 2 × `NodeHeight` = 120 px | Decision diamond height (same width as a standard node). |
| `NodeGap` | 1U = 30 px | Gap between neighbouring nodes. |
| `PitchX` | 6U = 180 px | Column pitch (node plus gap). |
| `PitchY` | 3U = 90 px | Row pitch (node plus gap). |
| `TracksPerGap` | 5 | Line tracks available in one 30 px gap. |
| `TrackOffsets` | 3, 9, 15, 21, 27 px | Track centres across a 30 px gap (6 px slots). |

The pitch is deliberately larger than a node. The gap between nodes is a built-in gutter that holds line tracks and clearance. When something doesn't fit, the answer is always to **add whole grid steps**. Never nudge a node by a few pixels, and never lower a clearance.

For LR diagrams, ranks run left to right instead of top to bottom. Swap the roles of rows and columns throughout; the constants stay the same.

## 4. Node size

1. Every node in a diagram is drawn in the same size box, whatever its shape (rectangle, rounded, stadium, circle, hexagon and so on). The one exception is the decision diamond, which is the same width as the standard box and exactly twice its height (150×120 when the standard box is 150×60). This already exists as `DIAMOND_HEIGHT_FACTOR = 2` on `master`; keep it.
2. The standard box is 150×60. If the longest label in the diagram doesn't fit after the normal wrap rules, the box for that diagram grows in **whole units** (width in steps of 30 px, height in steps of 30 px) until the label fits. Every node in that diagram then uses the grown size, diamonds stay at twice the grown height, and the pitch grows to match (box plus one unit of gap). A diamond spans two row pitches' worth of height, so place it centred on a grid row and reserve whatever extra whole grid steps it needs so it never overlaps the rows above or below.
3. Each shape is drawn to fit inside its box. A circle must **never** force the box to become square, and a diamond must never be wider than the standard box or force other nodes to grow. That was a visible fault in an earlier attempt.
4. A side needing more than five line connections grows that node by whole units along that side (see section 6). Apart from the diamond height, this is the only case where one node differs from the others.

## 5. Placing nodes on the grid

1. **Rows from ranks.** Each Mermaid rank becomes one grid row (one grid column for LR), in the same order. If Mermaid's spacing between two ranks is larger than one pitch (for example to make room for a group title or many crossing lines), keep that extra space as whole extra grid steps.
2. **Columns from order.** Within each rank, nodes keep Mermaid's order exactly. Each node goes to the grid column nearest its Mermaid centre, subject to that order.
3. **Keep Mermaid's alignments.** Nodes that Mermaid placed nearly in line (centres within a named tolerance) go into exactly the same column. A node that moves must line up with at least one other node.
4. **Snap whole rows and columns together.** Never snap nodes one at a time. An earlier attempt did, and it squashed the spacing between rows, fan-outs and groups, doubling the test failures. Move a whole rank or column as a unit, so relative spacing survives.
5. **Collisions cascade.** If a node needs a cell that another node occupies, push the occupant one grid step away, in the direction that keeps the rank order. If that cell is also occupied, push again, cascading until nothing overlaps. Pushes never swap the order of two nodes and never move a node out of its group.
6. **Line up partner nodes so lines go straight.** A line whose two connection points line up vertically or horizontally is drawn dead straight; every other line is an elbow or a Z shape with rounded corners (section 8). If a line's two ends are one column apart and lining them up would let the line be straight, move one end by that grid step, preferring the end with fewer other connections, as long as the move breaks no order, group or collision rule. If moving a node would break a straight line elsewhere, consider moving the node at the far end of that line by the same step.
7. **Grow the grid, never squeeze it.** If clearances can't be met, insert a whole grid row or column. Never reduce a clearance or a gap.

## 6. Ports (where lines meet a node)

All port positions are whole pixels measured from the node's top-left corner. Incoming and outgoing lines on the same side share the same set of slots.

**Rectangular sides.** Each side has five equal slots, used in this pattern:

| Lines on the side | Slots used |
|---|---|
| 1 | 3 (the centre) |
| 2 | 2 and 4 |
| 3 | 2, 3 and 4 |
| 4 | 1, 2, 4 and 5 |
| 5 | all five |

For a 150×60 node, the slots sit at 15, 45, 75, 105 and 135 px on the top and bottom, and at 6, 18, 30, 42 and 54 px on the left and right. A line leaves or enters a port heading straight out from that side (perpendicular to it), and then curves. For a grown node, recompute the five equal slots from its size, rounding to whole pixels symmetrically about the centre. A side needing more than five connections grows the node by whole units along that side, and the slots are recomputed.

Lines are assigned to slots in the order of where they go, so lines don't cross just outside the node. For example, on a bottom side the line going furthest left takes the leftmost slot.

**Diamonds.**
1. The four vertices are the first ports.
2. If more are needed, the next ports are the centres of the four slanted edges.
3. Beyond that, each slanted edge independently gets 1 to 4 evenly spaced ports, not counting the vertices, rounded to whole pixels symmetrically.
4. Lines leave a diamond port heading horizontally or vertically, never along the slant, and then curve.
5. The diamond is 150×120 (twice the standard height), so its vertices sit at the centre of each side of that box: top (75, 0), bottom (75, 120), left (0, 60) and right (150, 60).

This diamond code already exists as `FlowchartDiamondPorts`, with tests. Reuse it.

**Other shapes** (circle, stadium, hexagon and so on) use the same five-slot pattern on their bounding box, with the line end moved along its own direction until it touches the drawn outline exactly.

**Facing side.** A line leaves its source on the side facing its target and enters its target on the side facing its source, unless Mermaid's own route uses a different side, in which case Mermaid's side wins.

## 7. Tracks (lines running through gaps)

Every gap between neighbouring rows or columns is a channel holding up to five parallel tracks, filled from the centre outwards. Lines are curves, so a track is where a line crosses or runs along a gap: where several lines pass through the same gap, their paths are spaced onto separate tracks instead of bunching together or overlapping.

| Lines in the gap | Track offsets across the 30 px gap |
|---|---|
| 1 | 15 |
| 2 | 9 and 21 |
| 3 | 9, 15 and 21 |
| 4 | 3, 9, 21 and 27 |
| 5 | 3, 9, 15, 21 and 27 |

A gap needing more than five tracks gets an **extra grid step** (one more unit of gap), not narrower spacing. Tracks are assigned in an order that keeps lines from crossing each other inside the channel where possible.

**Group borders** (subgraphs) sit in the middle of a gutter. A line passing between two groups always has a proper track, and no line ever runs along a group border.

## 7a. Group frames (subgraphs) must fit their title and contents

This is a known bug on `master`, and fixing it is part of this pass. The dagre layout currently draws each group frame only a few pixels larger than the boxes inside it, with no room for the group's title, so the title is hidden behind the first box. Dave spotted it in his `EIP.md` document, which has 226 groups. A minimal example:

```
flowchart LR
    subgraph R0S0["Manifests and checksums"]
        direction TB
        r0n0["Publish a manifest (file list, sizes, hashes) with each drop so the consumer can reject incomplete batches"]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
```

Rules, all general and applied to every group in every diagram type:

1. Every group frame reserves a title strip at the top, tall enough for its title at the normal font, plus even padding on all four sides between the frame and the boxes inside it. Real Mermaid does this (its cluster label space and padding); match its behaviour, expressed in whole grid units.
2. The title is never covered by a box, a line or another group's frame, and it is never truncated. If the title is wider than the contents, the frame grows (in whole units) to fit the title.
3. Nested groups follow the same rule at each level, so an inner group's frame and title sit fully inside the outer group's padding, below the outer title.
4. The extra space comes from whole grid steps, so boxes inside and outside groups stay on the grid.
5. A group that is the only group nested inside its parent is centred horizontally within the parent (vertically for LR diagrams), snapped to the grid. Example: in the web shop sample, "App Server Node" is the only group inside "Primary data centre", so it sits centred across that frame. Other boxes at the same rank keep Mermaid's left-to-right order around it, with the parent growing in whole grid steps if needed.
6. Add this example and a nested-group example as synthetic tests, and include a frame check in the quality gate (section 9).


## 8. Routing rules

These are Dave's standing routing rules, in priority order. Each is a general measured condition in the engine, never a per-diagram choice.

1. Leave and enter on the facing side.
2. **Line shape (changed 7 Oct 2026, supersedes the earlier curve rule).** Every line is made of horizontal and vertical straight runs joined by small rounded corners of one radius (about 20 px). It is dead straight when its ends line up, has one corner (an elbow) otherwise, and has two corners (a Z shape, with the middle run halfway across the gap) only when no elbow fits. There are no S-curves, diagonal runs or free-flowing curves. Dave approved this style from the web shop target picture (section 8a).
3. Never run along a node or group border, or beside another line in the same track.
4. Bend well clear of the node just left. A line heads straight out of its port before it starts to curve, rather than bending hard against the node.
5. Give arrowheads room. The last segment before an arrowhead is long enough to show the arrowhead in full, and entry points are spread using the port slots, so arrowheads never stack.
6. Moving a node by whole grid steps, or taking a slightly longer route, is fine if it gives cleaner lines, as long as the node stays aligned with others and Mermaid's arrangement is kept.
7. A crossing is a last resort. When no clean route exists, two lines may cross as close to square-on as the curves allow, in preference to any overlapping or shared run.

The route for each line follows the corridor and sides of Mermaid's own route for that line. The grid pass redraws it as straight runs joined by rounded corners from the new port positions, spaced onto tracks where it shares a gap, and straight when its ends line up. It doesn't invent a new path around the diagram.

## 8a. The web shop target picture and its rules

Dave approved `webshop-target-layout.png` as what a finished diagram should look like. The full set of node placement and line routing rules taken from it is in `webshop-target-rules.md`, and those rules are part of this briefing. The web shop sample must come out matching that picture's arrangement and line shapes, and the same rules must hold across every other sample, not just this one.

## 9. Checks the pass must enforce

Add these to the rendered-diagram quality gate. Each one fails the diagram with a precise message:

- **Arrangement kept:** rank, order within rank, group membership, and each line's entry and exit sides all match the Mermaid reference. This check is the guard against drifting away from Mermaid.
- **Crossings:** no more than the Mermaid reference has.
- **On the grid:** every node centre and every track lies on a value the constants allow.
- **Sizes:** every node uses the diagram's standard box, diamonds are the standard width at exactly twice the standard height, and the only other difference is the growth allowed in section 6.
- **Whole pixels:** no fractional coordinate in the output.
- **Line shape:** every line is built only from horizontal and vertical runs joined by rounded corners of the standard radius, with no corners when its ends line up, at most two corners otherwise, and a straight run at least as long as the arrowhead at each end. Any diagonal run, S-curve or third corner fails.
- **Ports:** every line end is on an allowed slot for that side and line count.
- **Line ends:** every line starts and ends exactly on its node's drawn outline, never short of it or inside it.
- **Tracks:** lines sharing a channel sit on different allowed tracks.
- **Clean routing:** no stacked arrowheads, no shared runs, no runs along a border, no wiggles or hooks, and no doubling back beside a diamond.
- **No overlaps:** no two nodes overlap, and no line passes through a node or label it doesn't connect to.
- **Group frames:** every group frame fully contains its contents with even padding, and its title is fully visible, untruncated, and not overlapped by any box, line or other frame.

## 10. Lessons from the failed attempt (don't repeat these)

- Grid placement was built from scratch instead of from Mermaid's layout, so rank order, grouping and line sides drifted. The layouts sprawled, and Dave judged them terrible.
- Nodes were snapped one at a time, which squashed row, fan-out and group spacing.
- Line ends were moved after routing, which turned designed branches into hooks.
- Circles and diamonds forced square boxes. (Diamonds are now deliberately twice the standard height at the same width; that is the rule, not a fault.)
- Lines ended short of their boxes.
- Pictures were rendered before the full test suite was rerun on the final code.

## 11. How to work

1. **Measure first.** Record the baseline for all five test projects (Layout, Recognition, Naiad adapter, Rule validation, Converter) before changing anything.
2. **Build in separate small steps, each with its own synthetic unit tests:** constants and node sizing (including the double-height diamond); grid placement with the cascade; partner-node alignment; ports; tracks; curved routing; gate checks.
3. **General logic only.** Any reasoning such as "this gap is 22 px, so a line can't keep its clearance; move the row one grid step" must be coded as a general measured condition plus response, with a synthetic unit test. No per-diagram, per-node or per-edge tweaks, and no hand-placed layouts.
4. **Never weaken a test, threshold or fixture.** If an existing check conflicts with these rules, list the conflict for Dave instead of changing it.
5. **Regression rule:** no test that passed on the baseline may fail when you commit.
6. **Validate across many diagrams, not a few samples.** Use every F-series and enterprise sample, so the fixes are general and the results consistent.
7. **Commits** follow `docs/COMMIT-MESSAGE-STYLE.md`: plain native English; a short imperative subject with no full stop; a body of complete sentences on what changed and why. No private codes, round numbers or nicknames, and no mention of Dave, Bob or agents. Never commit `extern/Naiad/.editorconfig` or `extern/Naiad/src/Shared.sln.DotSettings`. Push to the working branch only. Never merge.
8. **Cost matters.** No helper or CI-watcher agents and no side experiments. Run filtered tests while working, with one full run per milestone. Stop and report a blocker rather than trying many variations.

## 11a. Working practices, spelled out

Follow every point in `CLOUD-AGENT-WORKING-PRACTICES.md` (attached with this briefing) exactly. These points matter most:
- The baseline counts are plain `dotnet test` results for each test project: Layout 235 passed / 33 failed, Rule validation 46 / 11, Recognition 921 / 11, Naiad adapter 47 / 0, Converter 13 / 1. Don't build any other counter.
- Compare with `master` in a separate git worktree. Never check out, stash or reset `master` in your working tree.
- Commit and push to the working branch after every change that builds and doesn't make the test counts worse.
- After three failed attempts at one problem, write it down and move on instead of trying more variations.
- Don't finish early. Keep going until the counts match master or you are genuinely stuck, then report exactly what still fails.

## 12. Conflicts to report, not decide

Some existing checks (for example dogleg, close-turn or right-angle checks) may clash with the rounded-corner line style or the web shop rules. Leave those checks unchanged and list every case where they clash, so Dave can decide.

## 13. What to report

1. Side-by-side PNGs (Mermaid reference, the Mermaid-faithful layout before the grid pass, and the gridded result), covering Sample03 web shop, Sample06 sales team, enterprise-01 order suite, F58, F29, F30, F11 and the group-frame example from section 7a, plus a folder with the rest.
2. A per-diagram table of every check in section 9.
3. The test table against the baseline for all five projects.
4. The constants used, by name and value.
5. Every conflict with an existing check.
6. Commits pushed, with hashes and subjects.

## 11b. Rules must be generic

No layout or routing rule may depend on a test name, sample number, file name, node id, node label, group title or any other attribute that identifies a particular diagram. Every rule is based only on generic structural or geometric attributes (rank, number of parents and children, node side, free or taken ports, whether ends line up, box and frame bounds, group nesting). The names in `webshop-target-rules.md` are examples of each rule, not conditions to code against. Check the diff for any id, label, sample or test-name checks in engine code before each commit, and report that you checked.

## 11c. Test only what the change covers

While working, run only the tests that cover the code you changed, using `dotnet test --filter`. Record the master baseline once at the start and don't repeat it. Run the full suites once, on the final commit, for the report.

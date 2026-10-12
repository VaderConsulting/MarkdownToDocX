# Brief: bend tests

## Goal
Dave's rule: unnecessary bends are never created. The regenerated enterprise.docx shows many lines with extra corners, jogs and detours in its first diagrams. Add one generic test that finds every unnecessary bend in every diagram of `flow.md`, `enterprise.md` and `EIP.md`, record today's count as a baseline, and then remove the causes in the router. The target is zero.

## What exists today (read-only findings)
- `DiagramGridQuality.cs` (about line 174) fails a line only when it has more than 2 corners (rule 4.3). A line with 2 corners that could have 0 or 1 passes.
- `FlowchartLayoutScore.cs` (about line 60) treats more than 3 corners as a problem. It is a score input, not a check.
- `FlowchartEdgeRouteDiagnosticAnalyzer.AppendUnnecessaryHardElbowFindings` flags only routes whose semantic class is CentreSpine or LocalDecisionBranch. It skips Corridor, CrossGroup, SkipBypass and LongExceptionRoute routes, and any route with fewer than 4 points. Enterprise lines, group lines and long lines are never examined.
- `FlowchartEdgeRouteDiagnosticTests` runs that analyser on the F58 fixture only.
- Rule validation facts (`Path.NoUnnecessaryDetour`) are set from the same analyser, so they inherit the same blind spots.
- `FlowchartAlignedRouteStraightener` and `ForwardRouteSimplifier` repair some cases, with tests on small fixtures. Nothing checks the whole of `enterprise.md`.
- Rule 2.13 scores 2 points per corner, but a score only compares candidates and never rejects a line. Rule 4.8 (fewest corners) and rule 4.2 (straight when ports line up) are written as rules; no test measures a line against them across the catalogues.
Why defects get through: the checks are corner limits and class-restricted diagnostics, not a comparison with the best route that was possible. Nothing asks whether a better route existed.

## Rules
Rules 4.1 to 4.10, 2.13, 2.21(f), section 5 and section 10 of `Instructions/LAYOUT-RULEBOOK.md`. Read them first. In particular: 4.2 (straight when ports line up), 4.3 (corner limit), 4.4 (minimum straight run), 4.8 (fewest corners), 4.9 (group ends) and 2.21(f) (a hinted line may use the corners its hint needs).

## Definition: unnecessary bend
A line has an unnecessary bend when the routed polyline is worse than a legal alternative that has the same two end boxes (or frames), the same end sides and the same ports, in any of these ways. Every check is geometric, uses the measured layout, and uses no fixed pixel size; distances are in grid steps and rule constants (corner radius, minimum straight run).
- (a) Too many corners. Fewest corners for the chosen end sides: 0 when the two ports line up (rule 4.2); 1 when the sides are perpendicular and the port of each end lies on the other's axis; 2 for sides that face the same direction or opposite sides with an offset. A line with more corners fails, unless a line hint (rule 2.21) needs the extra corners. The hint's need is computed, not assumed: remove the hint's side or travel request and re-test.
- (b) Zig-zag. Two consecutive corners that cancel, so the line could go straight or with one corner without crossing a box, a frame or another line.
- (c) Jog. A sideways step shorter than one grid step on a line whose two end boxes could be aligned by moving one of them by whole grid steps (rule 1.4) without an overlap. Report it as a placement cause as well as a routing one.
- (d) Detour. The line is longer than the shortest legal orthogonal path between the same ports by more than one grid step, and the shorter path is free of boxes, frames and other lines.
- (e) Crossing-avoidance bend. A bend that exists only to avoid a crossing that is within the forced minimum (rule 2.13). Crossings within the forced minimum are free, so they never justify a bend.
Not unnecessary: the corners of a back edge up to the limit of rule 4.3 when no shorter legal route exists; corners a hint needs; corners forced by a box or frame in the way; the corner of a group end that must meet the frame square-on (rule 4.9).

## Checker algorithm
Place the checker in the rule validation project (a new class), with no scenario ids, labels or node names in code. For each diagram, for each routed polyline:
1. Read the end sides, ports, route points, and obstacles (all boxes, group frames except those that contain an end, and all other routed lines) from the measured layout.
2. Count corners. Compute the fewest corners for these sides and ports as in (a).
3. Search for a better legal route with the same end sides and ports: (i) try removing each corner pair; (ii) try a straight route when the ports line up; (iii) try a one-corner and a two-corner route through free space, using the corner positions given by the port coordinates and the track positions of the gaps it passes; (iv) run a shortest orthogonal path search on the grid of box edges, frame edges and ports, with boxes and frames as obstacles. A candidate is legal when it keeps rules 4.1 to 4.4, meets its ports at a right angle, crosses no box and frame, adds no crossing above the forced minimum and shares no port.
4. Fail the line when a candidate has strictly fewer corners, or the same corners and a shorter length by more than one grid step, or removes a jog or zig-zag. Never use a tolerance that makes a real defect pass.
5. Report for each violation: diagram id, line id (source, target, label), which check (a) to (e) failed, the routed corners and length, and the better route as a list of points.
6. Scale with size. No cap on lines, nodes or search steps, and no time-out that returns "pass". Build one obstacle index per diagram (sorted edges or a grid index) so large diagrams stay fast; the search stops when the shortest path is proved, not after a fixed number of tries.

## Test files
1. `tests/MermaidRecognition.RuleValidation.Tests/UnnecessaryBendCheckerTests.cs` (unit tests, new). Small hand-built diagrams and routes, no catalogue input:
   - two boxes with aligned centres: the line is one straight run, and a version with a corner is rejected;
   - two boxes on perpendicular sides: one corner is accepted, and a three-corner route that could have one is rejected;
   - same-direction sides with an offset: two corners accepted, four rejected;
   - a zig-zag and a jog are each rejected, and a route with a box in the way is accepted;
   - a hinted line with the corners its hint needs is accepted, and the same line without the hint is rejected;
   - the checker's better route is returned and is itself accepted by the checker.
2. `tests/MermaidRecognition.RuleValidation.Tests/UnnecessaryBendCatalogueTests.cs` (new). Runs the checker over every diagram in `flow.md`, `enterprise.md` and `EIP.md`, using the same layout and routing as the converter. Written as a generic test over the whole catalogue, with no scenario ids in code.
3. `tests/MermaidRecognition.RuleValidation.Tests/UnnecessaryBendBaseline.txt` (new text file, one line per scenario: id and number of violations, then the line ids). Treat it as data, not code.

## Ratchet
- The first run lists the violations per scenario and writes the baseline file. It does not fail on the first run.
- After that the test fails when any scenario has more violations than its baseline, or a violation that is not in its baseline list.
- A scenario with a baseline of zero is on the clean list: any violation in it fails the test.
- The baseline may only shrink. Never add a line to the baseline or raise a number to make the test pass; a change that raises a count is a regression to fix.
- The test prints the count per scenario (table: scenario, lines, violations by check (a) to (e), baseline).
- The target is zero for every scenario. Done means the baseline file is empty, or each remaining item is listed with the reason a generic fix could not remove it.

## Ground rules
- Do not edit existing tests.
- Never loosen the checker to make a scenario pass. A false positive is fixed by making the checker more exact, with a unit test that shows the case.
- No fixed limits and no fixed pixel sizes. No scenario-specific code in the checker or in the router.
- Follow the working rules at the top of `currentstatus.md`.

## Steps, in order
1. Build the checker and the unit tests. Commit and push.
2. Run the checker over `flow.md`, `enterprise.md` and `EIP.md`. Record the baseline file. Report the count per scenario and per check (a) to (e), and the 10 scenarios with most violations. Commit and push.
3. Fix the routing causes generically, in order of count, never per scenario. Typical causes to look for: corner pairs left by repair passes, ports chosen before the sides were known, a side taken when a facing side was free, group-end lines that leave a frame by a corner, and placement that leaves boxes one step out of line. One commit per step. After each step re-run the whole scenario table, and confirm that overlaps, crossings and scores have not got worse (compare before and after, as in `BRIEF-COMPACT-LAYOUT.md`). Shrink the baseline file in the same commit.
4. Report the count per scenario before and after, and any remaining item with its reason. Do not ask Dave to review diagrams.

## Where the work stops
Done when the baseline is zero, or every remaining item is listed with the reason. After three failed attempts at one cause, stop on that cause, record why, and move to the next (working rule 7).

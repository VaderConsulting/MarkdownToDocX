# Brief: compact placement

## Goal
Diagrams are too wide and too deep. Nodes are ranked by dependency depth, and nothing pulls them back together afterwards, so a chain steps down one row per node and unconnected parts sit far apart. Add a compaction step that packs nodes into the nearest free grid cell.

## Example
Four nodes: Price Lookup links to Stock Lookup and to Place Order, Stock Lookup links to Place Order and to Take Phone Order.
- Today the nodes step down diagonally, one row each.
- Required: Price Lookup, Stock Lookup and Take Phone Order share the top row. Place Order sits directly under Price Lookup. Price Lookup drops straight down to Place Order. Stock Lookup leaves its bottom, turns left and enters the right side of Place Order. All line rules stay the same.

## Setting
Compaction is part of `grid` and is on by default. `open` does not use it. There is no separate layout value.

## Checking
Do not ask Dave to review diagrams. Check the results yourself, with tests and measurements:
- No box or frame overlaps and no new crossing in any diagram of `flow.md`, `enterprise.md` and `EIP.md`.
- Measure the width, height and layout score of every diagram before and after, and keep a layout only if its score is lower.
- Report only a summary: the number of diagrams checked, the total change in area, and every diagram that did not shrink, with the reason. Fix any diagram that got bigger.

## Rules
The rules are section 2.11 of `Instructions/LAYOUT-RULEBOOK.md`. Read it first. Do not change the other rules.

## What to change
1. Keep the layout setting as it is: `grid` (with compaction) and `open`.
2. After placement (rules 2.2 to 2.10), run the compaction step in rule 2.11.
3. Place the parts of a diagram that have no line between them side by side, one column gap apart, and not far away.
4. Add tests in both modes for the example above, for unconnected parts, for a diagram where compaction would add a crossing (it must not move that node), and for a diagram with a group frame.
5. Update the spec in short, unambiguous sentences to match section 2.11 of the rulebook.
6. Add wrapping (rule 2.12 of the rulebook): wrap long rows and columns, with one joining line, using a wrap length chosen from the diagram so that it ends up as close to a 4:3 landscape shape (width 4 parts to height 3 parts) as possible, without adding a crossing (a ratio of the diagram's own size; no fixed limit, and no failure for size), so that very wide or deep diagrams such as the large ones in `flow.md` stay compact. After wrapping, run compaction again. Repeat in passes and stop when a full pass moves no node; accept only moves that add no crossing and do not enlarge the diagram; follow rules 2.13 and 2.14; never fail because of size or pass count. Add tests for a long row, a long column, a long row inside a group, a diagram that needs both wide and deep wrapping, and a very large diagram (several hundred nodes) that must still lay out. Judge every attempt by the layout score in rules 2.13 to 2.16 and report size, ratio, overlaps, crossings, empty share and score.

## Rule order
Rule 2.11 wins over rule 2.2, so the longest path no longer has to stay in one column when compaction packs nodes closer. Update any existing test that asserts the old wide placement, and list each one in the report.

## Rules for the work
- Follow `Instructions/CLOUD-AGENT-WORKING-PRACTICES.md`.
- Every rule is generic. Nothing may depend on a scenario number, id or label.
- `open` renders exactly as before.
- Do not commit generated images.

## Done when
- The example renders as required in `grid`.
- `flow.md`, `enterprise.md` and `EIP.md` render with no overlaps and no new crossings, and no diagram is bigger than before.
- The five test suites show no new failures against the baseline.
- The report is a short summary of the size change, as in "Checking".
# Brief: optional diagram legend

The source already has EnterpriseNotationLegendRenderer.cs; reuse or extend it rather than writing a second legend renderer.

## Purpose
A diagram may show a legend: one example of each node type and line type it uses, each labelled with its type name. The legend is optional and off by default. It never changes the main layout. Read rules 1.7, 2.13, 2.21, 2.22, 4.10, 9.6 and section 10 of `Instructions/LAYOUT-RULEBOOK.md` first.

## Syntax
The setting goes in the diagram's own frontmatter, like `layout` (`BRIEF-LAYOUT-MODE.md`). Use the same frontmatter reader; do not write a second one.

```
---
config:
  legend: bottom-right
---
```

- `legend: off` is the default. `legend: on` means `bottom-right`.
- Positions: `bottom-right`, `bottom-left`, `top-right`, `top-left`, `below` and `right`. `below` centres the legend under the diagram; `right` centres it beside the diagram.
- `legend: table` renders the legend as a Word table instead (see Table mode).
- A placement hint may replace the position: `legend: right-of b1`, `legend: below g2`, with the hint words of rule 2.21(a). The peer must be a top-level node or group.
- A command-line option `--legend off|on|<position>|table` sets the default for a whole run. The diagram's own setting wins over it.
- An unknown value gives one warning and falls back to `off`. A hint with an unknown peer gives one warning and falls back to `bottom-right`.
- One setting covers all diagram kinds. Do not add a keyword per kind.

## Content
1. Include one example of each node type and each line type that is drawn in this diagram. Notes (rule 2.22) and plain connections (rule 4.10) count. Group frames count as a node type. Invisible connectors do not count.
2. Include nothing that the diagram does not draw.
3. Label each example with its type name, as the syntax writes it (for example `application-component`, `-flow->`), shown in readable words (`Application component`, `Flow`).
4. Order: node types in order of first use in the source, then line types in order of first use.
5. A type that appears with a different style (for example a junction, or a dashed line) is a separate entry only when it is drawn differently.

## Rendering
1. The legend is a framed group titled `Legend`.
2. Node examples come first, in a grid of small sample boxes, each with its type name inside or under it. Use the real shape, icon, colour and border of that type, at the smallest size that fits the name.
3. Each line type follows, one per row: a short horizontal line between two small sample boxes, with the type name beside it. Use the real line style, colour and arrowheads.
4. The legend follows the grid, spacing and label rules of the rulebook inside its frame.

## Layout
1. Lay out, route and score the main diagram first, exactly as with no legend.
2. Then place the legend in the chosen position, outside the main bounds, with one grid step of clearance (rule 1.4).
3. The legend never moves a node, group, line or label of the main diagram, never adds a crossing and never changes the main score.
4. If the legend does not fit, grow the canvas. The legend never overlaps the main diagram.
5. A legend hint places the legend beside its peer, on the outside of the main bounds on that side. It is otherwise placed as above.
6. The main checks (scoring, section 10, routing faults, bend tests) exclude the legend. The legend has its own checks: no overlap inside it and none with the main diagram.

## Table mode
With `legend: table`, no legend is drawn in the image. A small Word table follows the diagram, with two columns: `Symbol` (a small image of the sample) and `Type`. The rows follow the Content order. Use the document's table style.

## Tests
1. With no setting, no legend is drawn and output is byte-identical to today.
2. For each diagram kind (flowchart, enterprise, EIP), the legend lists exactly the types drawn, in the Content order, including notes and plain connections.
3. With the legend on, every main node, group, line and label has the same coordinates and the main score is the same; only the canvas size may differ.
4. Every position and a hinted legend place the legend outside the main bounds with one grid step of clearance and no overlap.
5. Table mode adds one table with the right rows and draws no legend in the image.
6. Unknown values and unknown peers give one warning and the fallback above. The command-line default is overridden by the diagram setting.
7. Tests compute expected sizes from measured sizes; no test hard-codes a pixel size.

## Rules
- Follow `CLOUD-AGENT-WORKING-PRACTICES.md`.
- Keep every rule generic. Nothing may depend on a scenario number, id or label.
- No fixed limit on the number of entries; the legend grows as needed.
- Add the setting to the rulebook and the spec in short, unambiguous sentences.

## Results to report
- The commits and the new tests.
- For each diagram kind, one scenario rendered with each position and with table mode.
- Confirmation that main coordinates and scores are unchanged with the legend on.
- Any type that could not be drawn as a sample, with the reason.
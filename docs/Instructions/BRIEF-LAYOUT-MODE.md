# Brief: choose between grid layout and open layout

## Goal
Let each diagram choose how its nodes are placed. The default is `grid`.

## The setting
The choice goes in the diagram's own frontmatter:

```
---
config:
  layout: grid
---
```

- `grid` places nodes and groups on one shared grid, following every rule in the layout rulebook.
- `open` places nodes and groups freely, with no shared grid. It changes node and group placement only.

Line routing is the same in both modes. Lines keep their straight runs, rounded corners, ports and crossing rules. Boxes and group frames never overlap in either mode.

## Rules for the setting
1. The default is `grid` for `enterprise` and `flowchart` diagrams when nothing is set.
2. An unknown value gives one warning and falls back to `grid`.
3. A command-line option `--layout grid|open` sets the default for a whole run. The diagram's own setting wins over it.
4. The setting uses the frontmatter reader from the syntax gaps brief (item 5). Build it on top of that work, or after it, and do not write a second reader.
5. Do not add a different keyword for each diagram kind. One setting covers all kinds.

## What to change
1. Read `layout` from frontmatter and from the command line, and pass the result to the layout engine as a mode.
2. In `open` mode, skip the shared-grid placement steps only. Do not touch routing.
3. Add the setting and both modes to the layout rulebook and the spec, in short, unambiguous sentences.
4. Add tests that render the same diagram in both modes and check that the routing rules hold in each. Add one test for the default, one for an unknown value, and one for the command-line override.

## Rules
- Follow `CLOUD-AGENT-WORKING-PRACTICES.md`, including rules 20 to 27.
- Keep every rule generic. Nothing may depend on a scenario number, id or label.
- Existing diagrams render exactly as before when no setting is given.
- Do not commit generated images.

## Done when
- A diagram with `layout: grid` renders as it does today.
- A diagram with `layout: open` renders without the shared grid, with the same line rules.
- The five test suites show no new failures against the baseline.
- The report lists the commits, the new tests and any case where `open` breaks a line rule, with the reason.

# MarkdownToDocX — current solution status

Updated 9 October 2026. This is the single maintained status file for the solution. Detailed validation evidence for this work is in the [grid layout review](grid-layout-review.md).

## 1. Repository and delivery state

| Area | Current state |
| --- | --- |
| Master | `f09cb89`; the earlier documentation relocation and spec trim are merged. |
| Grid routing | PR #9, branch `cursor/many-to-many-routing-3834`; code fixes through `74fa83b` are pushed. This work is not merged. |
| Active local checkout | The PR #9 branch is in the sibling `MarkdownToDocX-pr9` worktree. The original laptop folder remains on its pre-existing documentation branch. |
| Earlier documentation refresh | The original worktree retains its earlier documentation commits; this pass did not publish or merge them. PR #9 predates the spec relocation, so some normative files still sit at the root on this branch. |
| Enterprise / ArchiMate | Existing work is on PR #1. It is not integrated into the measured master or grid branch. |
| Supplied files | The original folder's untracked instructions, briefs, documents and editor settings remain untouched. |

## 2. What the solution currently does

- The converter builds DOCX output using OpenXml.
- Mermaid recognition covers flowchart, sequence, class, state, ER, Gantt, pie, mindmap and git graph contracts. Passing recognition tests do not establish rendering parity for every kind.
- Flowcharts use the native Sugiyama layout. PR #9 adds grid placement and horizontal/vertical routing with rounded corners; this implementation remains incomplete.
- Other diagram kinds still use the vendored Naiad renderer. Native replacement of those renderers remains future work.
- The grid branch now preserves normal terminal runs, whole-run track movements, free facing diamond vertices and centre ports for straight continuations. Labels share an anchor with their connector breaks. Grid output omits hop arcs. Diagonal back-edge guides are replaced when a valid orthogonal route fits their allocated ports and the three-corner limit.
- Side-exit grid connectors now retain rounded bends when their edge label cannot fit a stroke gap. Label breaks and rank-space calculations use rendered text height for vertical connectors and text width for horizontal connectors. The regenerated `flow.docx` was checked at F24 and F25: the excessive F25 breaks and the F24 label overlap are resolved.
- Binary decisions with two independent terminal outcomes in the same group place those outcomes on opposite sides. F29 now puts Accept beneath Yes and enters its top with one rounded elbow. The actual embedded DOCX image was checked; approval to update a conflicting older label-position assertion is pending.
- Sequence rows now measure message labels, arrowheads and note boxes to give notes the same conservative clear spacing as consecutive messages, including returning self messages. The canvas includes notes beyond either outer participant.

## 3. Verified test state

Measured on Windows with plain `dotnet test` on each project, including build and restore. Master and starting-branch baselines were collected before behavioural edits. All five suites were rerun on the source committed as `74fa83b` after the marked-up F29 terminal placement was implemented.

| Suite | Master failures | Starting PR #9 failures | Final PR #9 passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 34 | 38 | 320 / 32 |
| Rule validation | 11 | 13 | 49 / 10 |
| Recognition | 0 | 0 | 932 / 0 |
| Naiad adapter | 0 | 0 | 47 / 0 |
| Converter | 0 | 0 | 39 / 0 |

The F29 placement fixes `TryLayout_InspectContent_BranchesUseSingleElbowOrthogonalPaths` and introduces one failing name: `ResolveEdgeLabelPosition_F29_DecisionBranch_LabelHorizontallyCentredOnConnector`. The latter asserts a midpoint of the entire connector rather than the longest straight run; permission to update that existing expectation is pending. Together with the two earlier regressions, three existing tests now fail while passing on master. Twelve explicit recognition fixture-writing tests are not executed by the ordinary suite. Latest logs, failure names and TRX files are in `out/grid-terminal-decision/` and `out/grid-terminal-decision/final/`; the [review](grid-layout-review.md#11-terminal-decision-outcomes) records the evidence. Equal failure totals do not resolve the changed failing names.

The final counts meet the fresh master-count comparison, but **the solution does not pass all acceptance tests and the rendered diagrams are not ready for merge approval**.

## 4. Remaining work

1. Resolve the remaining layout and rule-validation failures without weakening their assertions. Some existing tests still describe older routing policies; reconcile those explicitly with the supplied rulebook.
2. Fix the visible bypasses through nodes, remaining legacy return routing, wrong or detached outline attachments, excessive return tracks and the webshop spine alignment. Ten samples and the active flowchart catalogue have been rendered. All ten samples were inspected; the [review](grid-layout-review.md#6-rendered-evidence) identifies the remaining defects.
3. Finish rulebook sections 1–5, local label gap growth and collision handling in section 6, and whole-grid frame growth and internal return containment in section 7. Section 8 now has shared sequence-row spacing, canvas growth for notes and regression coverage. The frame-midline rule remains on hold.
4. Obtain approval for the [six provisional defaults](grid-layout-review.md#7-decisions-before-a-future-merge) and a redefinition of the on-hold frame-midline rules before a future merge. Approval of the defaults does not by itself accept the remaining visual defects.
5. Integrate and verify Enterprise / ArchiMate work separately. Continue native renderer replacement for other diagram kinds and verify fallback, accessibility and unsupported-feature diagnostics.

## 5. Documentation ownership

| Material | Purpose |
| --- | --- |
| [README](../README.md) | Entry point, build structure and documentation navigation. |
| This file | Current solution status and outstanding work. |
| [Grid layout review](grid-layout-review.md) | Dated evidence, commits, failure names, picture comparisons and unresolved decisions for PR #9. |
| [Recognition specification](../spec.md) | Normative recognition, contract and acceptance requirements at this branch's existing path. |
| [Parity roadmap](../parity-roadmap.md) | Remaining native-rendering plan. |
| [Flowchart catalogue](../flow.md) | Diagram inputs and catalogue scenarios. |
| [Historical comparison images](flowchart/layout-fixes/README.md) | Earlier render evidence; their recorded commits and routing policy identify their scope. |

The supplied files under `docs/Instructions/` and `docs/briefs/` belong to the original laptop folder and are left as supplied. This branch does not copy or rewrite them. The old root `status.md` was replaced by this file because its pass history and test claims no longer described the current solution.

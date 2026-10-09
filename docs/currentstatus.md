# MarkdownToDocX — current solution status

Updated 9 October 2026. This is the single maintained status file for the solution. Detailed validation evidence for this work is in the [grid layout review](grid-layout-review.md).

## 1. Repository and delivery state

| Area | Current state |
| --- | --- |
| Master | `f09cb89`; the earlier documentation relocation and spec trim are merged. |
| Grid routing | PR #9, branch `cursor/many-to-many-routing-3834`; code fixes through `da36d97` are pushed. This work is not merged. |
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
- Sequence rows now measure message labels, arrowheads and note boxes to give notes the same conservative clear spacing as consecutive messages, including returning self messages. The canvas includes notes beyond either outer participant.

## 3. Verified test state

Measured on Windows with plain `dotnet test` on each project, including build and restore. Master and starting-branch baselines were collected before behavioural edits. An initial verification exposed further defects during picture review; after the two resulting fixes, all five suites were run on the final source at `da36d97`.

| Suite | Master failures | Starting PR #9 failures | Final PR #9 passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 34 | 38 | 293 / 33 |
| Rule validation | 11 | 13 | 49 / 10 |
| Recognition | 0 | 0 | 932 / 0 |
| Naiad adapter | 0 | 0 | 47 / 0 |
| Converter | 0 | 0 | 39 / 0 |

No new failing test names were introduced against the starting branch. Twelve explicit recognition fixture-writing tests are not executed by the ordinary suite. Final logs and TRX files are in `out/grid-final-da36d97/` in the PR worktree; the [review](grid-layout-review.md#8-final-failure-names) records all remaining failure names and the baseline provenance. Two existing Layout tests still fail on the branch while passing on master; lower totals alone do not resolve those regressions.

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

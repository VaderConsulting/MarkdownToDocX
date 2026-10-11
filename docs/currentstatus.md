# MarkdownToDocX - current status

**Last updated:** 11 October 2026. This is the only status file; it describes what is true today. History stays in git.

## What the product does today

- `md2docx` converts Markdown with Mermaid diagrams to DOCX.
- Flowcharts and enterprise (ArchiMate) diagrams use the native path: Mermaid recognition, one shared grid layout, native routing and an SVG render. If a flowchart fails natively, the converter falls back to the frozen Naiad renderer.
- The grid layout draws straight runs with rounded corners and a straight centre spine, and it avoids needless crossings. The rules are in `layout-engine/APPROVED-GRID-DECISIONS.md`; the dated evidence is in `grid-layout-review.md`.
- Enterprise diagrams use a `mermaid` code fence only. The `enterprise` fence tag is not supported.
- Every diagram uses the grid. There is no layout setting yet.
- All other diagram kinds still render through Naiad.
- Recognition reaches level L4 for the nine started kinds (flowchart, sequence, class, state, ER, pie, gantt, mindmap, git graph) and L1 for the rest.

## Known failing Layout tests

These five Layout tests expect the old branch layout and fail now. They are known and accepted.

- `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules`
- `RenderToSvg_InspectContent_BranchesUseOrthogonalRunsWithDiamondStub`
- `RenderToSvg_ValidDecision_BranchesUseOrthogonalRunsWithDiamondStub`
- `RenderToSvg_TypeDecision_NoSharedBottomStub`
- `RenderToSvg_MessageFilter_BranchesEnterLeftSideWithRoundedZ`

## Open work

1. **Enterprise syntax gaps**: 14 scenarios in `enterprise.md` do not render (A100, A101, A118 to A130). The converter lacks hyphenated ids, semicolons, enterprise headers and direction, frontmatter and init directives, `classDef`, `class`, `style` and `linkStyle`, quoted labels, and redeclared ids. See `BRIEF-ENTERPRISE-SYNTAX-GAPS.md`.
2. **Layout mode**: each diagram will choose `layout: grid` or `layout: open` in its frontmatter. The default is `grid`. `open` changes only how nodes and groups are placed; line rules stay the same. Scenarios F144 to F150 in `flow.md` and A143 to A149 in `enterprise.md` test it. It needs the frontmatter reader from item 1. See `BRIEF-LAYOUT-MODE.md`.
3. **Visual checks**: the grid stress test A142 in `enterprise.md` converts but has not been checked by eye for compactness and crossings. F111 is the one `flow.md` diagram that does not render properly.
4. **Rulebook gaps**: the label, group-frame and sequence sections are unfinished. The frame-midline rule is on hold, and six defaults await confirmation. See `Instructions/LAYOUT-RULEBOOK.md`.
5. **Native render for the other kinds** (sequence, class, state, ER, pie, gantt, mindmap, git graph).
6. **Housekeeping**: the vendored Mermaid pin is dirty and should be cleaned.
7. **`flowchart-elk`** is deferred.

## Test results

Measured on Linux on 11 October 2026 with `dotnet test`. Windows runs are still to do, and the counts may differ.

| Suite | Failing |
| --- | --- |
| `MermaidRecognition.Tests` | 11 |
| `MermaidRecognition.Layout.Tests` | 30 (includes the five above) |
| `MermaidRecognition.RuleValidation.Tests` | 10 |
| `MermaidRecognition.NaiadAdapter.Tests` | 0 |
| `MDConverter.Tests` | 1 |

## Compatibility pin

```text
Mermaid package version: 11.14.0
Vendored source commit: 1bb6ba52937c21999e9ac89d11b82b0c56fd2739
Vendored source state: dirty - capture a clean baseline before claiming fixture immutability
```

If the vendored Mermaid changes, update this pin, the recognition fixtures and `spec.md` together.

## Where things are

- `spec.md` is the normative recognition and rendering specification.
- `Mermaid_Specification.md` holds the flowchart geometry and routing rules.
- `Rule Specification/` (repository root) holds the rule validation rules and scripts.
- `HANDOVER-GRID-LAYOUT-v2.md` describes the grid layout that is required, including what "tight" means.
- `Instructions/CLOUD-AGENT-WORKING-PRACTICES.md` holds the working rules for agents.
- `Instructions/LAYOUT-RULEBOOK.md` is the layout rulebook.
- `BRIEF-ENTERPRISE-SYNTAX-GAPS.md` and `BRIEF-LAYOUT-MODE.md` are the briefs for the two next build tasks.
- `flow.md`, `enterprise.md` and `EIP.md` (repository root) and `enterprise/samples/` in this folder are the stress-test inputs. `sequence.md`, `class.md`, `state.md` and `entityrelationship.md` in this folder are the other catalogues.

## Maintenance rule

Update this file at the end of every completed task. Keep it short, and delete anything that is no longer true instead of adding history.
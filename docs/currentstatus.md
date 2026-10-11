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

## Working rules for the agent

1. Follow `Instructions/CLOUD-AGENT-WORKING-PRACTICES.md`.
2. Work in the main `MarkdownToDocX` folder only. Do not create other local folders.
3. Use one branch from master and one pull request. Do not merge; Dave approves the merge.
4. Run the baseline test suites first, so that no new failure goes unnoticed.
5. Work in small steps. After each working step, commit and push, update this file in the same commit, and start the next step from that pushed commit.
6. Never change an existing test to make it pass, except where a task below says to rewrite it. List clashes for Dave to decide.
7. Stop on a task after three failed attempts, and say what failed and why.

## Tasks, in order

1. **Enterprise syntax gaps.** Catalogue checks now confirm that A100, A101 and A118 to A130 render or diagnose exactly as declared, and the 17 deliberately invalid scenarios keep their error counts. A90 produces four diagnostics, but its DOCX fallback still needs one visible line per diagnostic. See `BRIEF-ENTERPRISE-SYNTAX-GAPS.md`.
2. **Layout mode.** Each diagram chooses `layout: grid` or `layout: open` in its frontmatter. The default is `grid`. `open` changes only how nodes and groups are placed; line rules stay the same. Use the frontmatter reader from task 1. Scenarios F144 to F150 in `flow.md` and A143 to A149 in `enterprise.md` must behave as their Expected lines say. See `BRIEF-LAYOUT-MODE.md`.
3. **Old-layout Layout tests.** The five tests under "Known failing Layout tests" expect the old branch layout or the old stroke check. Rewrite each to assert rounded straight runs, using the rulebook, and report each change.
4. **Visual fixes.**
   - Render the A142 grid stress test and check it against `HANDOVER-GRID-LAYOUT-v2.md` for compactness, overlaps and needless crossings.
   - Fix `flow.md` scenario F111, the one diagram that does not render properly.
   - Fix the A14 diagram in `enterprise.md`.
   - Fix the tangled lines in the Web Shop, Order Manager and Payment Hub diagram, the curved retry loop with its label on the line, and the very wide strips that cannot be read at document size.
5. **Remaining test failures.** Work through the rule-validation failures (10), the recognition failures (11) and the converter failure `ConvertFile_LockedDestinationRemainsIntact`. Fix real faults. If a failure comes only from Linux behaviour, say so and leave it.
6. **Housekeeping.** Clean the vendored Mermaid pin and update the compatibility pin below. `flowchart-elk` stays deferred.
7. **Final check.** Run all five suites on Windows, report the counts next to the baseline, and confirm this file is short and true.

## Stretch goals

These must be done, but not yet. Start them only when Dave says so.

1. **Rulebook gaps.** Finish the label, group-frame and sequence sections of `Instructions/LAYOUT-RULEBOOK.md`. Do not build the two rules on hold (frame on the midline, reserved midline). Ask Dave about the six unconfirmed defaults and wait for the answer.
2. **Native render for the other kinds** (sequence, class, state, ER, pie, gantt, mindmap, git graph). Do one kind at a time, in its own commit, and keep the Naiad fallback until a kind passes its scenarios.
## Test results

Windows baseline measured on 11 October 2026 with `dotnet test`, before task 1 implementation.

| Suite | Passing | Failing |
| --- | ---: | ---: |
| `MermaidRecognition.Tests` | 1069 | 0 |
| `MermaidRecognition.Layout.Tests` | 724 | 31 |
| `MermaidRecognition.RuleValidation.Tests` | 49 | 10 |
| `MermaidRecognition.NaiadAdapter.Tests` | 47 | 0 |
| `MDConverter.Tests` | 59 | 0 |

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

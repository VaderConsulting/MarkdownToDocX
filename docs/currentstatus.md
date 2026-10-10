# MarkdownToDocX - current status

**Last updated:** 10 October 2026. This is the only status file; it describes what is true today. History stays in git.

## What the product does today

- `md2docx` converts Markdown with Mermaid diagrams to DOCX.
- Flowcharts use the native path: Mermaid recognition, shared grid layout, native routing and an SVG render. If that fails, the converter falls back to the frozen Naiad renderer.
- The grid layout draws straight runs with rounded corners and a straight centre spine, and it avoids needless crossings. The rules are in `layout-engine/APPROVED-GRID-DECISIONS.md`; the dated evidence is in `grid-layout-review.md`.
- Enterprise (ArchiMate) diagrams render natively. They use a `mermaid` code fence only. The `enterprise` fence tag is not supported.
- All other diagram kinds still render through Naiad.
- Recognition reaches level L4 for the nine started kinds (flowchart, sequence, class, state, ER, pie, gantt, mindmap, git graph) and L1 for the rest.

## Known failing Layout tests

These five Layout tests pass on master and fail now. They are known and accepted.

- `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules`
- `RenderToSvg_InspectContent_BranchesUseOrthogonalRunsWithDiamondStub`
- `RenderToSvg_ValidDecision_BranchesUseOrthogonalRunsWithDiamondStub`
- `RenderToSvg_TypeDecision_NoSharedBottomStub`
- `RenderToSvg_MessageFilter_BranchesEnterLeftSideWithRoundedZ`

Four of them still expect the old branch layout (orthogonal runs and stubs). The grid layout routes branches differently. The same change fixes eight other Layout tests that fail on master.

## Open work

1. **Grid layout and routing**: the label, group-frame and sequence parts of the rulebook are unfinished. Crowded corners and container-title obstacles remain.
2. **Enterprise syntax**: styling and configuration are only partly supported, and the ArchiMate 3.2 relationship table needs verifying.
3. **Native render for the other kinds** (sequence, class, state, ER, pie, gantt, mindmap, git graph).
4. **Housekeeping**: the vendored Mermaid pin is dirty and should be cleaned.
5. **`flowchart-elk`** is deferred.

## Test results

Measured on Linux on 10 October 2026 with `dotnet test`. Windows counts differ.

| Suite | Passing | Failing | Failing on master |
| --- | --- | --- | --- |
| `MermaidRecognition.Tests` | 1014 | 11 | 11 |
| `MermaidRecognition.Layout.Tests` | 519 | 30 | 33 |
| `MermaidRecognition.RuleValidation.Tests` | 49 | 10 | 11 |
| `MermaidRecognition.NaiadAdapter.Tests` | 47 | 0 | 0 |
| `MDConverter.Tests` | 18 | 1 | 7 |

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
- `flow.md` and `EIP.md` (repository root) and `enterprise/samples/` in this folder are the stress-test inputs. `sequence.md`, `class.md`, `state.md` and `entityrelationship.md` in this folder are the other catalogues.

## Maintenance rule

Update this file at the end of every completed task. Keep it short, and delete anything that is no longer true instead of adding history.
# MarkdownToDocX - current status

**Last updated:** 9 October 2026. Master is at `911d7c0`. This is the only status file; it describes what is true today and replaces the old pass-by-pass history, which remains in git.

## What the product does today

- `md2docx` converts Markdown with Mermaid diagrams to DOCX.
- Flowcharts use the native path: Mermaid recognition, dagre placement, native routing and an SVG render. If that fails, the converter falls back to the frozen Naiad renderer.
- All other diagram kinds still render through Naiad.
- Recognition reaches level L4 for the nine started kinds (flowchart, sequence, class, state, ER, pie, gantt, mindmap, git graph) and L1 for the rest.

## Open work

1. **Grid layout and routing** (PR #9, unmerged). One shared grid, straight runs with rounded corners, a straight centre spine and no needless crossings, following the rulebook. At the last check Layout had 5 failures that master does not have (rank order, store cluster frame clearance, a corner arrowhead, one edge label distance, diagonal routes through group borders) and 3 extra rule-validation failures. The label, group-frame and sequence sections of the rulebook are unfinished.
2. **Native render for the other kinds** (sequence, class, state, ER, pie, gantt, mindmap, git graph).
3. **Enterprise (ArchiMate) renderer** in PR #1, and the later Mermaid plugin.
4. **Housekeeping**: the vendored Mermaid pin is dirty and should be cleaned.
5. **`flowchart-elk`** is deferred.

## Test results on master (last measured 7 October 2026)

| Suite | Passing | Failing |
| --- | --- | --- |
| `MermaidRecognition.Tests` | 921 | 11 |
| `MermaidRecognition.Layout.Tests` | 235 | 33 |
| `MermaidRecognition.RuleValidation.Tests` | 46 | 11 |
| `MermaidRecognition.NaiadAdapter.Tests` | 47 | 0 |
| `MDConverter.Tests` | not recorded | 7 |

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
- `flow.md`, `EIP.md` and `enterprise.md` (repository root) are the stress-test inputs. `sequence.md`, `class.md`, `state.md` and `entityrelationship.md` in this folder are the other catalogues.

## Maintenance rule

Update this file at the end of every completed task. Keep it short, and delete anything that is no longer true instead of adding history.
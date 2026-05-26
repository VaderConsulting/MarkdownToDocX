# Flowchart scenario status (flow.md F01–F60)

Last updated: 2026-05-19 (continued pass).

## Priority fix queue (Word review → `out/flow-mermaid-ink/report.md`)

Fix in this order first:

1. **F24** — edge labels (short)
2. **F29** — binary decision (Yes / No)
3. **F30** — multi-way decision
4. **F31** — fan-out
5. **F32** — fan-in
6. **F34** — retry loop
7. **F35** — multi-incoming diamond
8. **F37** — subgraph `direction TB`
9. **F38** — subgraph `direction LR` inside TB
10. **F40** — sibling subgraphs: interior nodes flow **left–right (LR)**
11. **F43** — nested subgraphs
12. **F48** — incorrect label text (entity codes)
13. **F53** — `style` directive (same class as F29)
14. **F59** — worker pool + decision + retry
15. **F60** — LR + nested TB integration
16. **F58** — full pipeline (defer until above)

## Code fixes (Scenario Fxx commits)

| Scenario | Commit | Notes |
| --- | --- | --- |
| F28 | `6233ec4` | Minlen arrowheads on `--->` / `---->` |
| F30 | `e207263` | Diamond fan-out: Manhattan polylines, not guided curveBasis |
| F31 | `bd0ee60` | TB fan-out: inboard ports + top-centre entry |
| F34 | `96c7594` | Cycle edges: acyclic swap + route after undo |
| F35 | `008158a` | Multi-incoming to diamond |
| F40 | `0ba0201` | LR cross-boundary: directional Manhattan in `TryComputeStraightFlowchartEdgePoints` |

## Structural regression

- **SvgHarness** (`flow.md`, `--no-reference`): **58/58 OK** after F40 fix.
- **`flow.docx`** regenerated from `flow.md`.

## Remaining visual / Ink diffs (non-blocking)

| Scenario | Notes |
| --- | --- |
| F44 | Nested subgraph: renders; cross-boundary routing uses same straight-edge helper as F40 |
| F50 | Emoji glyph vs monochrome in PNG diff when Ink available |
| F51 | Wrap line count may differ slightly from Ink; node uses ~10 lines at 200px width |
| F55 | `linkStyle` stroke weights; verify in Word |
| F58 | Full pipeline renders; optional archive → Done may stay long diagonal |

## Commands

```powershell
dotnet build extern/Naiad/src/Naiad/Naiad.csproj -c Release
dotnet run --project src/md2docx/md2docx.csproj -c Release -- flow.md flow.docx
dotnet run --project src/SvgHarness/SvgHarness.csproj -c Release -- flow.md out/harness --no-reference
```

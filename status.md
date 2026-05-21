# Flowchart scenario status (flow.md F01–F60)

Last updated: 2026-05-19 (session: F28→F60 workflow pass).

## Commits with code changes

| Scenario | Commit | Notes |
| --- | --- | --- |
| F28 | `6233ec4` | Minlen links: graph edge removal + endpoint snap; arrowheads on `--->` / `---->` |
| F30 | `e207263` | Diamond fan-out: keep dagre Manhattan polylines, skip guided curveBasis |
| F31 | `bd0ee60` | TB fan-out: inboard 3-way hub ports + top-centre target entry |
| F34 | `96c7594` | Cycles: swap acyclic endpoints, route after undo, empty-edge fallback |
| F35 | `008158a` | Multi-incoming to diamond: straight top-outline entry |

## Verified (empty or prior commits; SvgAudit render OK)

F29, F32, F33, F36–F43, F45–F60 (structural render). F53 style colours render in SVG.

## Unfinished / needs manual Word + mermaid.live

| Scenario | Reason |
| --- | --- |
| F44 | Render OK; nested subgraph cross-boundary routing not re-diffed (Ink 503) |
| F40 | Harness diff: LR subgraph spine vs diagonal cross-links — not fixed this pass |
| F50 | Harness diff: emoji glyph vs monochrome — cosmetic |
| F51 | Harness diff: wrap line breaks — measure/wrap tuning |
| F55 | Harness diff: linkStyle stroke weights — mostly background noise |
| F58 | Harness diff 0.86: large pipeline layout — visually OK in SvgAudit; Word check pending |
| F01–F27 | Not individually re-committed this pass; included in `flow.docx` regeneration |

## Quick commands

```powershell
dotnet build extern/Naiad/src/Naiad/Naiad.csproj -c Release
dotnet run --project src/md2docx/md2docx.csproj -c Release -- flow.md flow.docx
Get-Content .\fixture.mmd -Raw | dotnet run --project src/SvgAudit/SvgAudit.csproj -c Release -- render out.svg out.png --no-stdout
```

## Next loop

1. Close **flow.docx** in Word and visually confirm F01–F60 (especially F28, F40, F58).
2. Re-run SvgHarness when Mermaid Ink is available for diff baselines.
3. Fix any regressions found in Word; one `Scenario Fxx:` commit per fix.

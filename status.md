# Status briefing (for next iteration)

Date/time context: Thu 30 Apr 2026, ~12:01 AM (UTC+8).

This repo’s goal (per `AI-Instructions.md`) is Mermaid parity in DOCX: convert Markdown Mermaid diagrams to SVG and embed into Word, matching Mermaid.js output visually in Microsoft Word. The current workstream is **flowchart edge routing/ports** parity (symmetry, “vertical if possible”, fork/join aesthetics) across **all directions** (`TD`, `LR`, `BT`, `RL`).

## Where we are right now

- **Diagrams 1 and 2** in `SymmetryStressProgression.md` are now visually correct (per user confirmation).
- User feedback before stopping: **“8 down”** (i.e., from diagram 8 onward in the progression, issues remain and need further work).
- `stress.docx` has been regenerated from `SymmetryStressProgression.md` after each fix, using `stress.docx` first and `stress-1.docx` as fallback (file locks).

## Key fixes completed this session

### 1) BT arrowheads “missing”

Symptom: In `flowchart BT`, arrows looked like they had **no arrowhead**.

Cause: For BT, some flowchart edges were attaching to the **wrong face** because vertical attach-point “facing” classification used **rank-based** logic (appropriate for ER diagrams), but BT flips coordinates after ranking. That can invert which side is “up/down” in terms of actual geometry.

Fix:
- Added a **position-based** facing classifier for flowcharts:
  - `ClassifyFacingByPosition(LayoutNode Hub, LayoutNode Other)`
- Updated flowchart vertical attach allocation to use position-based facing instead of rank-based facing.

Verification:
- `FocusBT.md` minimal fixture now clearly shows an arrowhead in PNG output.

### 2) RL must mirror LR (horizontal ports)

Symptom: `flowchart LR` looked correct; `flowchart RL` arrow exit/entry locations were wrong (not the mirror of LR).

Cause: `BuildFlowchartHorizontalAttachOverrides(...)` was **LR-biased**:
- Fan-in assumed sources are left of target
- Fan-out assumed targets are right of source
- Always used right-edge exits and left-edge entries

Fix:
- Made horizontal attach overrides **direction-aware** for `Direction.RightToLeft`:
  - For RL, invert “left/right” tests and swap which face is used (left vs right edge).

Verification:
- `FocusRL.md` minimal fixture now mirrors LR correctly in rendered PNG.

## Files changed (most relevant)

- `extern/Naiad/src/Naiad/Layout/CoordinateAssignment.cs`
  - Added `ClassifyFacingByPosition(...)`
  - Flowchart vertical attach allocation now uses position-based facing
  - Horizontal flowchart overrides now accept direction and mirror for `RightToLeft`

Output and fixtures used:
- `SymmetryStressProgression.md` (primary visual progression set)
- `FocusBT.md` (minimal BT arrow check)
- `FocusRL.md` (minimal RL mirror check)
- `stress.docx` (latest regenerated output)

## How to reproduce quickly (next session)

### Build

```powershell
dotnet build -c Release
```

### Minimal BT arrow check

```powershell
dotnet run --project .\src\SvgHarness\SvgHarness.csproj -c Release -- .\FocusBT.md .\out\svg-harness-focus-bt --no-reference --emit-png --parallel 1
```

Expected: the arrowhead is visible (upward edge into the upper node).

### Minimal RL mirror check

```powershell
dotnet run --project .\src\SvgHarness\SvgHarness.csproj -c Release -- .\FocusRL.md .\out\svg-harness-focus-rl --no-reference --emit-png --parallel 1
```

Expected: `RL` looks like the mirror of `LR` (ports on opposite faces, clean symmetric routing).

### Regenerate DOCX (primary)

```powershell
dotnet run --project .\src\md2docx\md2docx.csproj -c Release -- .\SymmetryStressProgression.md .\stress.docx
```

If locked:

```powershell
dotnet run --project .\src\md2docx\md2docx.csproj -c Release -- .\SymmetryStressProgression.md .\stress-1.docx
```

## What is still failing / open work

User-confirmed: **Diagram 8 onward** in `SymmetryStressProgression.md` still has parity issues.

Likely categories (based on earlier iterations and where complexity spikes around 6–8):
- **Multi-level fork/join**: symmetric columns, short perpendicular stems, and “vertical if possible” routing through congestion.
- **Port/slot consistency**: ensure that “slot rule” (2N-1 slots) and inboard port logic remain deterministic for wide/narrow nodes.
- **Direction completeness**: ensure the same aesthetic rules apply for `TD`, `BT`, `LR`, `RL` (not just port faces but curve/stem behaviour).

### Next-step plan for “diagram 8 down”

1) Identify exactly what diagram 8 is (in `SymmetryStressProgression.md`) and whether failures are:
   - wrong port face
   - wrong port slot (wrong inboard/outboard choice)
   - missing/incorrect stems
   - non-vertical routes where vertical is clear
   - curveBasis control point oddities (flat runs / sharp turns)

2) Create a dedicated minimal fixture for the specific diagram 8 failing pattern:
   - `FocusDiag08_TD.md`
   - `FocusDiag08_BT.md`
   - `FocusDiag08_LR.md`
   - `FocusDiag08_RL.md`

3) Use `SvgHarness --emit-png --no-reference` for fast visual iteration.

4) Patch Naiad in the narrowest location possible (likely `CoordinateAssignment.ApplyVerticalForkExitGapToDiagramEdges(...)` and/or pass-through alignment), then regenerate `stress.docx`.

## Important implementation notes (avoid regressions)

- Flowchart vertical attach allocation must remain **position-based** for BT/RL correctness because the graph is flipped after ranking.
- Horizontal attach overrides are intentionally **small-N scoped**. Keep changes constrained to avoid broad routing shifts.
- Visual tests are the real tests; unit tests won’t catch these. Use fixtures + PNG output for iteration.
- Maintain deterministic ordering (node insertion order and stable edge ordering) to avoid random left/right arm assignment.


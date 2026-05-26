# Flowchart parity workflow

This document describes the **exact workflow** used in this repository to understand and fix each flowchart regression scenario (F01–F60 in `flow.md`). It is the handoff guide for “how we work a scenario,” not a catalogue of scenarios themselves.

**Related files**

| File | Role |
| --- | --- |
| `flow.md` | Progressive regression catalogue (F01–F60) |
| `AI-Instructions.md` | Standing rules: parity definition, build/test, sources of truth |
| `Mermaid_Specification.md` | Confirmed rules traced from Mermaid (update when a rule is proven) |
| `status.md` | Latest “where we got to” (read before resuming) |
| `boot.md` | Points agents at `AI-Instructions.md` |
| `extern/Naiad/` | Parser, layout, renderer (DOCX SVG path) |
| `mermaid/` | Local Mermaid checkout for source tracing |

---

## 1. What “done” means

**Parity target:** Naiad output in **Microsoft Word** (via `md2docx`) should match **[mermaid.live](https://mermaid.live)** for the same diagram text — layout, geometry, labels, arrowheads, and default styling.

**Not sufficient on its own**

- VS Code Mermaid preview alone (different runtime path)
- SVG that looks fine in a browser but breaks in Word
- A fix that helps one scenario but breaks a previously confirmed one

**Product path**

```text
flow.md  →  md2docx  →  flow.docx  →  Word (embedded SVG from Naiad)
```

**Fast iteration path** (single diagram, no Word):

```text
Focus fixture .md  →  SvgAudit / SvgHarness  →  .svg (+ optional .png)
```

---

## 2. Regression catalogue structure

`flow.md` is ordered by **complexity**, not by fix order:

| Level | IDs | Topic |
| --- | --- | --- |
| 1 | F01–F10 | Syntax, direction, basics |
| 2 | F11–F17 | Node shapes |
| 3 | F18–F28 | Edge types, labels, link length |
| 4 | F29–F35 | Branching, decisions, cycles |
| 5–9 | F36–F60 | Subgraphs, stress, integration |

Work **one scenario at a time** (or a small cluster with the same root cause). Do not skip ahead without locking baselines: scenarios already marked correct must not regress.

---

## 3. Per-scenario workflow (step by step)

### Step A — Reproduce the failure

1. Open the scenario in `flow.md` (e.g. `### F28 — Min-length / multi-segment link`).
2. Compare in **mermaid.live** (paste the fenced `mermaid` block).
3. Regenerate DOCX and compare in **Word**:

   ```powershell
   dotnet run --project src/md2docx/md2docx.csproj -- flow.md flow.docx
   ```

   If Word has `flow.docx` open, close it and retry (or write `flow-1.docx` temporarily).

4. Note **what** is wrong in plain language, e.g.:
   - wrong node size
   - edge bows / not straight
   - rank gap too large or too small
   - label on one line instead of wrapped
   - arrowhead on wrong face
   - missing subgraph border alignment

### Step B — Isolate (minimal fixture)

Create a **tiny** markdown file with only that diagram, e.g. `f28-only.md`:

```markdown
flowchart TB
A[A] --> B[B]
C[C] ---> D[D]
E[E] ----> F[F]
```

Use isolation when:

- `flow.docx` is slow to regenerate
- other scenarios clutter the view
- you need a stable repro for SvgAudit

Delete or keep focus files after the fix; prefer not to accumulate dozens of permanent `Focus*.md` unless they document a locked baseline.

### Step C — Classify the failure layer

Most flowchart bugs fall into one of four layers. Classify before coding.

| Layer | Typical symptoms | Primary Naiad locations |
| --- | --- | --- |
| **Parse** | Wrong edge type, wrong `minlen`, label not captured | `FlowchartParser.cs` |
| **Measure / size** | Box too wide/narrow/short; wrong diamond size | `FlowchartRenderer.cs` (pre-layout measurement) |
| **Layout** | Wrong ranks, gaps, node positions; dummy nodes | `DagreLayoutEngine.cs`, `Ranker.cs`, `CoordinateAssignment.cs` |
| **Render** | Path shape, markers, label SVG, colours | `FlowchartRenderer.cs`, `MermaidStyles.cs`, `SvgBuilder.cs` |

**Rule:** Trace Mermaid first; do not guess Naiad behaviour. A layout bug is often mistaken for a render bug (e.g. wavy edges from `curveBasis` on points that layout already made non-straight).

### Step D — Trace Mermaid (source of truth)

Use this order:

1. **Rendered output** — mermaid.live (authoritative for “what it should look like”).
2. **Local source** — `mermaid/packages/mermaid/src/...` for the pinned tree in this repo.
3. **Spec** — `Mermaid_Specification.md` (if already documented).
4. **Update spec** — when you confirm a rule, add or correct a section (e.g. §6.2 edge labels, §6.3 `minlen`).

**Common trace paths**

| Concern | Mermaid starting points |
| --- | --- |
| Link stroke → `minlen` | `diagrams/flowchart/flowDb.ts` (`destructEndLink`, `edge.length` cap 10) |
| Dagre layout + edge dimensions | `rendering-util/layout-algorithms/dagre/index.js`, `edges.js` |
| Node label / wrap width | `createText.ts`, `flowchart.wrappingWidth` (default 200) |
| Edge path / curveBasis | `rendering-util/rendering-elements/edges.js`, `lineWithOffset.ts` |
| Shape geometry | `dagre-wrapper/shapes/`, `flowChartShapes.js` |

Search locally, e.g. `minlen`, `destructEndLink`, `insertEdgeLabel`, `curveBasis`, `wrappingWidth`.

### Step E — Map to Naiad pipeline

Flowchart end-to-end in Naiad:

```text
Parse (FlowchartParser)
  → model: nodes, edges (MinRankSpan, labels, shapes)
Measure nodes + edge labels (FlowchartRenderer, before layout)
  → node width/height; Edge.LabelLayoutWidthPx / LabelLayoutHeightPx
Layout (DagreLayoutEngine + Ranker + CoordinateAssignment)
  → ranks, positions, edge.Points polylines
Render SVG (FlowchartRenderer)
  → paths, markers, labels, theme
```

When debugging, ask:

- Is the **model** wrong? → parser.
- Is **layout input** wrong (sizes, minlen, label metrics)? → pre-layout in renderer.
- Are **ranks/gaps** wrong? → ranker / coordinate assignment (do not confuse `minlen` with label width — F25).
- Are **points** wrong but ranks OK? → routing / dummy nodes / `CollectEdgePoints`.
- Are **points** OK but SVG ugly? → path builder (`curveBasis` vs linear, `FixCorners`, markers).

### Step F — Implement a narrow fix

Principles (from `AI-Instructions.md`):

- **One hypothesis per cycle** — change, build, verify.
- **Smallest diff** that fixes the scenario without breaking locked baselines.
- **No Naiad-only invented rules** — behaviour should match traced Mermaid logic.
- **Direction-aware** — `TD`, `BT`, `LR`, `RL` often need explicit handling (BT flips Y after rank assignment; RL mirrors LR on horizontal ports).

Typical fix locations seen in recent work:

| Scenario | Root cause | Fix pattern |
| --- | --- | --- |
| **F14** Hexagon | Label measure used wrong em width/height | Adjust `MeasureFlowchartHexagonLabelBBox` to match dagre-wrapper |
| **F25** TB edge label spaces | `minlen` inflated from label **width** | Remove label→`minlen` logic; `minlen` = stroke only |
| **F26** LR long label wrap | Layout ignored label bbox; no wrap | Wrap at 200px; set layout metrics before dagre; rank-axis gap from label **height** (LR) or **width** (TB) correctly |
| **F28** Long hyphens `--->` | Original edge left in `graph.Edges`; `curveBasis` on elbows | Remove split edge from graph; align dummies; straight segments; linear SVG path; `LongestPath` ranker |

### Step G — Verify

**Build** (minimum when Naiad changed):

```powershell
dotnet build extern/Naiad/src/Naiad/Naiad.csproj
```

**Fast SVG check** (isolated fixture):

```powershell
Get-Content f28-only.md -Raw | dotnet run --project src/SvgAudit/SvgAudit.csproj -- render out.svg out.png --no-stdout
```

Inspect path `d` attributes for flowchart links, e.g. straight `M…L…` vs curved `C…`.

**Optional automated check** (Naiad tests — when test project builds):

- `extern/Naiad/src/Tests/Flowchart/FlowchartTests.cs` — `VerifySvg` snapshots or assertions (e.g. F28 straight segments).

**Product check**:

```powershell
dotnet run --project src/md2docx/md2docx.csproj -- flow.md flow.docx
```

Open **Word** and check:

- the fixed scenario
- a few **locked** scenarios either side in the catalogue (regression guard)

**SvgHarness** (batch PNG, optional reference to Mermaid Ink):

```powershell
dotnet run --project src/SvgHarness/SvgHarness.csproj -c Release -- FocusBT.md out/focus-bt --no-reference --emit-png
```

Use for symmetry / direction stress (`SymmetryStressProgression.md`, `FocusBT.md`, `FocusRL.md`) — see `status.md`.

### Step H — Document

When the rule is confirmed:

1. Add or update **`Mermaid_Specification.md`** (numbered section, Mermaid file paths, Naiad file mapping).
2. Optionally note resolution in **`Naiad issues.md`** / gap checklist if you use them.
3. Update **`status.md`** with “what’s fixed / what’s next” if stopping mid-stream.

Do **not** commit unless the user asks.

---

## 4. Decision tree (quick reference)

```text
Symptom in Word vs mermaid.live
│
├─ Wrong text / link type / missing node?
│     └─ FlowchartParser + flowDb.ts destructLink / destructEndLink
│
├─ Node box wrong size/shape?
│     └─ FlowchartRenderer measurement + dagre-wrapper shapes
│
├─ Nodes overlap or ranks too tight/loose?
│     ├─ Labelled edge? → edge label layout metrics + rank-axis gap (§6.2)
│     ├─ Long hyphen link? → MinRankSpan / minlen only (§6.3), NOT label width
│     └─ Ranker (LongestPath vs TightTree), ranksep/nodesep config
│
├─ Edge wavy / wrong elbow / wrong port?
│     ├─ CoordinateAssignment (attach points, fork slots, BT/RL direction)
│     ├─ Dummy chain: Ranker removes original edge; CollectEdgePoints
│     └─ FlowchartRenderer: linear vs curveBasis
│
└─ Colour/font/marker wrong?
      └─ MermaidStyles, SvgBuilder markers, theme variables
```

---

## 5. Commands cheat sheet

| Intent | Command |
| --- | --- |
| Full flowchart DOCX | `dotnet run --project src/md2docx/md2docx.csproj -- flow.md flow.docx` |
| Build Naiad only | `dotnet build extern/Naiad/src/Naiad/Naiad.csproj` |
| Render stdin → SVG | `Get-Content fix.md -Raw \| dotnet run --project src/SvgAudit/SvgAudit.csproj -- render out.svg --no-stdout` |
| Harness folder PNG | `dotnet run --project src/SvgHarness/SvgHarness.csproj -- Stress.md out/stress --no-reference --emit-png` |
| MDConverter tests | `dotnet test tests/MDConverter.Tests/MDConverter.Tests.csproj` |

**Analyser note:** Naiad may treat `IDE0007` (`var`) as error; use localised `#pragma warning disable IDE0007` around small blocks if needed.

---

## 6. Worked examples (how we fixed scenarios)

### F14 — Hexagon size

- **Symptom:** Hexagon path shape OK; box too wide/short vs mermaid.live.
- **Trace:** dagre-wrapper label line box and char width for polygon shapes.
- **Fix:** `MeasureFlowchartHexagonLabelBBox` — taller line box (3em), capped width vs height so bevel does not over-widen.
- **Verify:** F14 in `flow.docx` + hexagon aspect ratio ~ mermaid.live.

### F25 — Edge labels with spaces (TB)

- **Symptom:** Huge vertical gaps; curved edges bowing left.
- **Trace:** Mermaid `minlen` from `destructEndLink` (stroke length only); not label width.
- **Fix:** Remove `ApplyLabelAwareMinRankSpan` (or equivalent) that used label width for rank span.
- **Lesson:** **Never map label width → `minlen`.**

### F26 — Long edge label (LR)

- **Symptom:** Single-line label; nodes too close; overlap.
- **Trace:** `insertEdgeLabel` before `dagreLayout`; `edge.width`/`height`; `flowchart.wrappingWidth` default 200.
- **Fix:** Wrap label before layout; set `LabelLayoutWidthPx`/`LabelLayoutHeightPx`; extra rank-axis gap along **layout direction** (LR → width).
- **Lesson:** Measure labels **before** layout; gap on **rank axis**, not always vertical.

### F28 — Multi-segment links (`--->`, `---->`)

- **Symptom:** Rank gaps partly OK but edges **wavy**; not straight like mermaid.live.
- **Trace:** `minlen = line.length - 1` (cap 10); dagre dummy nodes for `rankDiff > 1`.
- **Fix:**
  - Parser: `MinRankSpan = strokeLength - 1` (cap 10).
  - Ranker: remove original edge from `graph.Edges` after splitting into dummy chain.
  - Route: align dummies to source column; collapse axis-aligned polylines.
  - Render: `ShouldUseLinearFlowchartPath` — use `L`, not `curveBasis`.
  - Ranker: `LongestPath` so tight-tree does not collapse minlen gaps.
- **Verify:** SVG paths `M122,24L122,216` style (only `M`/`L`, no `C`).

---

## 7. Symmetry / direction stress (parallel track)

Besides `flow.md`, use **`SymmetryStressProgression.md`** for fork/join, port slots, and **all four directions** (`TD`, `BT`, `LR`, `RL`).

Workflow is the same (isolate → trace → `CoordinateAssignment` → verify PNG/DOCX), but failures are often **attach-point / facing** bugs:

- **BT:** position-based facing, not rank-only (coordinates flip after layout).
- **RL:** mirror horizontal attach overrides of LR.

See `status.md` for current diagram number (“8 down”, etc.) and focus files (`FocusBT.md`, `FocusRL.md`).

---

## 8. Anti-patterns (avoid)

| Anti-pattern | Why it fails |
| --- | --- |
| Fix render only when layout points are wrong | `curveBasis` will still bow |
| Use label width for `minlen` | F25-style giant rank gaps |
| Change global ranker for one edge type | Breaks locked F01–F24 baselines |
| Verify only in browser SVG | Word may differ; DOCX is the product |
| Large refactors across diagram families | Flowchart ≠ sequence ≠ ER routing |
| Skip `Mermaid_Specification.md` update | Next session re-discovers the same rule |

---

## 9. Session start checklist

1. Read `status.md` (if present).
2. Read `AI-Instructions.md` (apply, do not paste).
3. Confirm target scenario ID in `flow.md`.
4. Reproduce on mermaid.live + Word (`flow.docx`).
5. Isolate → trace Mermaid → map Naiad layer → narrow fix → verify → document.

---

## 10. When to regenerate what

| Change affects | Regenerate |
| --- | --- |
| Naiad parser/renderer/layout | `flow.docx` (and isolated fixture SVG) |
| Only `workflow.md` / spec docs | Nothing |
| Locked `test.md` scenarios | `test.docx` per `AI-Instructions.md` before asking user to validate Word |

This workflow is intentionally repetitive: **compare → isolate → trace Mermaid → fix one layer → verify in Word → write the rule down.** That is how each scenario in `flow.md` is meant to be driven to parity.

# MarkdownToDocX — programme status

**Last updated:** 2026-05-31 (pass 20 — truthfulness checklist; native render pilot)

**Specification:** [`spec.md`](spec.md) (normative — what the solution must do)

**Parity plan (started kinds):** [`parity-roadmap.md`](parity-roadmap.md)

**G12 requirement inventory:** [`spec-gates/g12-backlog-template.md`](spec-gates/g12-backlog-template.md) + per-kind `spec-gates/{kind}-g12.md`

**This file:** implementation progress, gate registry, and operational notes only.

Regenerate manifest summary after manifest changes:

```powershell
dotnet test new_src/MermaidRecognition.Tests/MermaidRecognition.Tests.csproj
# Optional: tools/Generate-DiagramGateRegistry.ps1 → spec-gates/registry.generated.md
```

---

## Summary

| Area | Status |
| --- | --- |
| **Stage 11.A recognition exit (L1 all §3.18.2 kinds)** | **Met** — smoke detect + diagnostics + fixtures |
| **Flowchart recognition depth** | **L4** — G12 front-matter theme + spacing; F87 metadata |
| **Wave kinds (sequence, class, state, ER)** | **L4** — minimum G12 slice transcribed; `*L4AxisGateTests` |
| **Secondary kinds (pie, gantt, mindmap, gitGraph)** | **L4** — L3 closed + minimum G12 slice; `*L4AxisGateTests` |
| **Started kinds recognition parity** | **9/9 at L4** — minimum G12 slice per [`spec-gates/{kind}-g12.md`](spec-gates/g12-backlog-template.md) |
| **G12 requirement docs** | **Done** — template + 9 backlogs (flowchart exemplar + 8 kinds) |
| **Catalogue back-fill (S/C/ST/E)** | **100%** active scenarios mapped (remarked: C24, ST19, E10) |
| **Default DOCX render path** | **Legacy** — `extern/Naiad` (frozen); native render pilot **started** (flowchart only) |
| **Native render (product path)** | **Pilot** — `Render/FlowchartRouter` + `NativeFlowchartSvgRenderer`; per-kind adapters + layout remain open |
| **Tests (`MermaidRecognition.Tests`)** | **799** passing |

**Progress shorthand:** `L1: 22/22` · `started kinds L4: 9/9` · `recognition fixtures: 183` · `L4 axis tests: 62` · `truthfulness tests: 13` · `tests: 799` (+2 vs pass 20 initial) · [`parity-roadmap.md`](parity-roadmap.md)

---

## Compatibility pin (operational)

```text
Mermaid package version: 11.14.0
Vendored source commit: 1bb6ba52937c21999e9ac89d11b82b0c56fd2739
Vendored source state: dirty — capture clean baseline before claiming fixture immutability
Minimum feature context: edge-level curve styling via edge ids (Mermaid v11.10.0+)
```

If vendored Mermaid changes, update this pin, recognition fixtures, and `spec.md` §11.1 together.

---

## Per-diagram gate registry

Authoritative **implementation** tracking for §7.4 gates. Normative gate definitions remain in [`spec.md`](spec.md) §7.4 and §11.1.14.x line-type catalogues.

| Diagram kind | Priority | Level | G0 | G1 | G2 | G3 | G4–G9 | G11 (fixtures) | G12 | R0 legacy |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Flowchart / graph | Current | **L4** | Done | Done | Done | Done | Done | **32** | Done | Yes |
| Flowchart ELK | Required | L1 | Done | Done | diag | diag | — | 1 | — | No |
| Sequence | Wave | **L4** | Done | Done | Done | Done | Done | **34** | Done | Yes |
| Class | Wave | **L4** | Done | Done | Done | Done | Done | **34** | Done | Yes |
| State | Wave | **L4** | Done | Done | Done | Done | Done | **23** | Done | Yes |
| ER | Wave | **L4** | Done | Done | Done | Done | Done | **34** | Done | Yes |
| Gantt | Backlog | **L4** | Done | Done | Done | Done | Done | **9** | Done | Yes |
| Pie | Backlog | **L4** | Done | Done | Done | Done | Done | **6** | Done | Yes |
| Git graph | Backlog | **L4** | Done | Done | Done | Done | Done | **6** | Done | Yes |
| Mindmap | Backlog | **L4** | Done | Done | Done | Done | Done | **5** | Done | Yes |
| Timeline | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| User journey | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| Quadrant | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| XY chart | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| Requirement | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| Architecture | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| Kanban | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| Sankey | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| Packet | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| Block | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| Radar | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| Treemap | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| C4 | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | Yes |
| Event modelling | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | No |
| Tree view | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | No |
| Ishikawa | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | No |
| Venn | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | No |
| Wardley | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | No |
| Info | Backlog | L1 | Done | Done | Partial | Partial | — | 1 | — | No |
| YAML `---` trap | Backlog | L1 | Done | — | Partial | diag | — | 1 | — | N/A |

**Column key:** G2 = line-type catalogue published; G3 = each line type parsed or diagnosed; G11 = verified JSON fixture count; G12 = parser parity with pinned Mermaid; **R0 legacy** = legacy Naiad can render a smoke sample (comparison baseline only — not the product render target).

Manifest paths: `new_src/MermaidRecognition.Tests/Gates/manifests/*.gate-manifest.json`

---

## Stage 11.A progress (recognition)

**Recognition exit glue:** `MermaidRecognizer` + per-kind recognisers + verified JSON + gate manifests. Legacy `MermaidRecognition.NaiadAdapter` exists **only** for optional comparison of recognition output against legacy Naiad parse/render — not the product path.

| Item | Status | Notes |
| --- | --- | --- |
| Multi-kind detect + parse dispatch | Done | `DiagramKindDetector` order matches vendored orchestration |
| Contract types in `new_src/MermaidRecognition/` | Done | §4 DTOs |
| No recognition fields in Naiad models | Done | Mapping via legacy adapter only; Naiad frozen |
| Legacy Naiad render (comparison only) | Unchanged | MDConverter still uses legacy path until native render ships |
| All kinds gate **L1** smoke | Done | 22 / 22 — `MermaidRecognizerL1Tests` |
| Flowchart **L3** (G4–G11) | Done | `FlowchartL3AxisGateTests`; 32 fixtures |
| Flowchart **L4** (G12) | Done | theme front-matter; spacing; F87/F88; manifest L4 |
| Sequence / class / state / ER **L4** | Done | Minimum G12 slice; see `spec-gates/{kind}-g12.md` |
| Pie / gantt / mindmap / gitGraph **L4** | Done | L3 + `*L4AxisGateTests`; manifests **L4** |
| Catalogue scenario coverage test | Done | `CatalogueScenarioCoverageTests` (S/C/ST/E) |
| §9.1 scenario vs fixture glossary | Done | In `spec.md` §9.1 |
| Mermaid-only + YAML trap L1 | Done | `MermaidOnlyRecognition/` |
| G12 backlog docs | Done | `spec-gates/g12-backlog-template.md`, `spec-gates/{kind}-g12.md` |
| Naiad baseline inventory (§12) | Done | [`spec-gates/naiad-render-baseline.md`](spec-gates/naiad-render-baseline.md) — **legacy comparison only** |

**L4 axis test counts (started kinds):** flowchart 6 · sequence 8 · class 8 · state 8 · ER 10 · pie 5 · gantt 6 · mindmap 5 · gitGraph 6 = **62** total in `*L4AxisGateTests.cs`.

### Flowchart L4 (G12) detail

| Item | Status |
| --- | --- |
| Edge `animate` + typed `curve` metadata | Done |
| F86 YAML front-matter `config.flowchart.curve` | Done — `config-front-matter` |
| F86 YAML front-matter spacing keys | Done — `config-front-matter-spacing` |
| F87 edge metadata on thick link (`@==>`) | Done — `edge-metadata-f87` |
| F88 `classDef` animation on edge id | Done — `edge-class-animate` (recognition; F88 remarked in `flow.md` render gate) |
| Broader front-matter keys beyond spacing | Not started (extended backlog) |
| Started-kind L4 (pass 19) | Done — eight parallel paths; see below |

### Pass 19 — started kinds L4 (2026-05-31)

Eight parallel agents (batch 19) closed the minimum G12 slice for each incomplete kind. All manifests promoted to **L4**; extended backlog rows may remain **planned/deferred** in `spec-gates/*-g12.md`.

| Kind | L4 tests | G12 backlog | Notable fixtures / code |
| --- | --- | --- | --- |
| Sequence | 8 | [`sequence-g12.md`](spec-gates/sequence-g12.md) | `par-over`; acc, autonumber, control, box, lifecycle |
| Class | 8 | [`class-g12.md`](spec-gates/class-g12.md) | namespace, accessibility, direction (fixtures pre-existing) |
| State | 8 | [`state-g12.md`](spec-gates/state-g12.md) | `transition-colons` (#7418); composite, choice, note, style, acc |
| ER | 10 | [`er-g12.md`](spec-gates/er-g12.md) | `relation-cardinality`; direction, style, acc, entity alias |
| Pie | 5 | [`pie-g12.md`](spec-gates/pie-g12.md) | `PieL4AxisGateTests`; `pie-accessibility`, `edge-values`; showData in mapper |
| Gantt | 6 | [`gantt-g12.md`](spec-gates/gantt-g12.md) | relative, milestone, accessibility, `inclusive-end`; acc/excludes patterns |
| Mindmap | 5 | [`mindmap-g12.md`](spec-gates/mindmap-g12.md) | `hierarchy`, `shapes`; `MindmapNodeLineParser` shape suffixes |
| Git graph | 6 | [`gitGraph-g12.md`](spec-gates/gitGraph-g12.md) | checkout/merge patterns; `merge`, `commit-args`, accessibility |

**Suite after pass 19:** 786 tests passing (was 702 at pass 18).

### Pass 20 — truthfulness checklist + native render pilot (2026-05-31)

Closed the gap between `status.md` claims and repository reality for Stage **11.B** flowchart routing and a first native SVG path. Automated checks live in `TruthfulnessChecklistTests` and `LegacyNaiadLabelSemanticsTests`.

| Deliverable | Location | Tests |
| --- | --- | --- |
| Structural flowchart router (label-invariant) | `new_src/MermaidRecognition/Render/FlowchartRouter.cs` | `FlowchartRouterTests`, `FlowchartRouterLabelInvarianceTests` |
| Render eligibility gate | `NativeRenderCapabilities.cs` | `TruthfulnessChecklistTests` |
| Flowchart SVG pilot (grid layout) | `NativeFlowchartSvgRenderer.cs` | `NativeFlowchartSvgRendererTests` |
| Legacy Naiad Yes/No baseline proof | reads `extern/Naiad/.../CoordinateAssignment.cs` | `LegacyNaiadLabelSemanticsTests` |
| Checklist automation | `Truthfulness/TruthfulnessChecklistTests.cs` | L4 manifests, router type, `CanRender`, SVG smoke |

**Suite after pass 20:** 799 tests passing (+13 vs pass 19; label-invariance expanded to all edge labels).

---

### Catalogue back-fill

| Catalogue | Active scenarios | Fixture column |
| --- | --- | --- |
| [`sequence.md`](sequence.md) | S01–S32 | 100% |
| [`class.md`](class.md) | C01–C35 (C24 remarked) | 100% |
| [`state.md`](state.md) | ST01–ST27 (ST19 remarked) | 100% |
| [`entityrelationship.md`](entityrelationship.md) | E01–E35 (E10 remarked) | 100% |
| [`flow.md`](flow.md) | F01–F88 | Not index-enforced (capability-named fixtures) |

### Visual catalogues not started

`pie.md`, `gantt.md`, `mindmap.md`, `gitgraph.md` — L2 recognition catalogues exist in spec; Markdown visual catalogues pending render-gate priority.

---

## Fixture inventory (recognition)

Regenerate: `dotnet test ... --filter "FullyQualifiedName~Write_"`

| Directory | Fixtures | Gate level | G12 backlog |
| --- | --- | --- | --- |
| `FlowchartRecognition/` | 32 | L4 | [`flowchart-g12.md`](spec-gates/flowchart-g12.md) |
| `SequenceRecognition/` | 34 | L4 | [`sequence-g12.md`](spec-gates/sequence-g12.md) |
| `ClassRecognition/` | 34 | L4 | [`class-g12.md`](spec-gates/class-g12.md) |
| `StateRecognition/` | 23 | L4 | [`state-g12.md`](spec-gates/state-g12.md) |
| `ErRecognition/` | 34 | L4 | [`er-g12.md`](spec-gates/er-g12.md) |
| `PieRecognition/` | 6 | L4 | [`pie-g12.md`](spec-gates/pie-g12.md) |
| `GanttRecognition/` | 9 | L4 | [`gantt-g12.md`](spec-gates/gantt-g12.md) |
| `MindmapRecognition/` | 5 | L4 | [`mindmap-g12.md`](spec-gates/mindmap-g12.md) |
| `GitGraphRecognition/` | 6 | L4 | [`gitGraph-g12.md`](spec-gates/gitGraph-g12.md) |
| **Started kinds total** | **183** | **L4** | — |
| `*Recognition/simple` (timeline, C4, …) | 1 each | L1 |
| `MermaidOnlyRecognition/` | 7 | L1 |

---

## Implementation components

| Component | Status | Location |
| --- | --- | --- |
| `MermaidRecognizer` / `DiagramKindDetector` | Done | `new_src/MermaidRecognition/Core/` |
| `FlowchartDb` / `FlowchartRecognizer` | Done | `Kinds/Flowchart/` |
| Wave kind contract mappers | Done (L4) | `Kinds/*/*ContractMapper.cs` |
| `*L4AxisGateTests` (started kinds) | Done | `MermaidRecognition.Tests/*/` |
| `ContractToFlowchartModel` (legacy compare) | Done | `new_src/MermaidRecognition.NaiadAdapter/` — not product render |
| Per-kind native render adapters | Not started | `DiagramContract` → SVG/layout in `new_src/` |
| `FlowchartRouter` + native layout | Not started | Stage 11.B–C in `new_src/` (not `extern/Naiad`) |
| MSAGL Sugiyama layout (Dagre replacement candidate) | Deferred | §11.1.11.1; evaluate after native routing gates |
| ELK layout port (`flowchart-elk`) | Deferred | §11.1.11.2; vendored MIT `mermaid/packages/mermaid-layout-elk/` + `elkjs` |
| §11.E Mermaid.js fallback | Not started | Official renderer fallback when native path unavailable |

---

## Phased work — what is next

Recognition minimum slice for all started kinds is **complete**. **Naiad is not a forward option** — it remains frozen under `extern/Naiad` and is used only when comparing legacy output against a new native render path (SvgHarness, DOCX review). See [`parity-roadmap.md`](parity-roadmap.md).

1. **Native render — flowchart depth** — MSAGL Sugiyama (or equivalent) layout on `FlowchartRouter` output; SvgHarness R1 parity slice. Pilot grid SVG exists; not product-ready.
2. **Native render — started kinds** — per-kind `DiagramContract` → render adapters (sequence, class, state, ER, pie, gantt, mindmap, gitGraph).
3. **Visual gates** — visual catalogues (`pie.md`, `gantt.md`, …); SvgHarness R1; optional §11.E Mermaid.js fallback.
4. **MDConverter default path** — `MermaidRecognizer` + native adapters once R1 gates pass.
5. **Extended G12 backlog** — optional parser depth in `spec-gates/*-g12.md`.
6. **Operational** — clean vendored Mermaid pin (currently **dirty**); regen `spec-gates/registry.generated.md`.
7. **`flowchart-elk`** — separate ELK track (§11.1.11.2).

Stage 11.A recognition exit is **met and exceeded**. Stages **11.B–E** target **native render from `DiagramContract`**, not Naiad maintenance.

---

## Truthfulness checklist (repository vs claims)

Automated: `new_src/MermaidRecognition.Tests/Truthfulness/TruthfulnessChecklistTests.cs` (+ `LegacyNaiadLabelSemanticsTests`, `FlowchartRouterLabelInvarianceTests`).

| Statement | As of pass 20 |
| --- | --- |
| `flow.md` visual catalogue is valuable | Yes |
| Legacy Naiad renders most F01–F60 (comparison baseline) | Largely yes — not a product target |
| Legacy Naiad `FlowchartParser` matches Mermaid `flowDb` | No — why recognition replaced parse authority |
| `MermaidRecognition` flowchart parser matches contract spec | L4 |
| Started kinds (9) at recognition L4 minimum G12 slice | Yes — manifests + `*L4AxisGateTests` |
| `MermaidRecognizer` multi-kind entry exists | Yes |
| `FlowchartRouter` exists | **Yes** — `Render/FlowchartRouter.cs`; structural routing on `DiagramContract` |
| Label `Yes`/`No` affects legacy Naiad routing | **Yes** — documented by `LegacyNaiadLabelSemanticsTests` (`CoordinateAssignment.cs`) |
| Native router ignores all edge label text | **Yes** — `FlowchartRouterLabelInvarianceTests` (variants, mutation, static gate) |
| Native render from `DiagramContract` | **Partial (pilot)** — flowchart grid SVG via `NativeFlowchartSvgRenderer`; other kinds recognition-only |
| ELK native layout in scope | No (deferred — §11.1.10, §11.1.11.2) |
| MSAGL Sugiyama native layout in scope | No (deferred — §11.1.11.1) |
| Stage 11.A changes default DOCX images | No — legacy path until native render gates pass |
| Stage 11.A L1 recognition for all kinds | Yes |
| Secondary kinds at L4 (minimum G12) | Yes (pie, gantt, mindmap, gitGraph) |
| Wave kinds at L4 (minimum G12) | Yes (sequence, class, state, ER) |
| Extended G12 backlog fully transcribed | No — minimum slice only |

Update this table when implementation or claims change.

---

## Decision gates (not yet met)

- **Adopt MSAGL Sugiyama for native layout:** `FlowchartRouter` on `DiagramContract` **exists (pilot)**; still requires SvgHarness parity on agreed fixture slice (§11.1.11.1). Legacy Naiad output may be used **only** for side-by-side comparison during transition.
- **Port native ELK for `flowchart-elk`:** requires vendored Mermaid adapter parity (§11.1.11.2), `config.elk.*` mapping, and ELK-specific acceptance fixtures.
- **Enable native render as MDConverter default:** requires `MermaidRecognizer` + per-kind native adapters passing SvgHarness/DOCX acceptance (or documented §11.E fallback).
- **Use legacy Naiad for product render:** **Out of scope** — frozen; comparison and regression baseline only (§12).
- **Claim “recognition complete” for a started kind:** **Met** for all 9 started kinds — manifest **L4** and minimum G12 slice **transcribed** in `spec-gates/{kind}-g12.md` (extended backlog may remain open).

---

## Repository layout note

**Breadcrumb (2026-05-27):** Stage 11.A+ recognition code lives in **`new_src/`** (`MermaidRecognition`, `MermaidRecognition.Tests`). Legacy product pipeline: `src/` MDConverter and **`extern/Naiad`** (frozen — comparison baseline only).


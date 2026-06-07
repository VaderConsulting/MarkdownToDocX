# MarkdownToDocX — programme status

**Last updated:** 2026-06-07 (pass 42 — rule validation harness, F58 routing, layout contracts)

**Specification:** [`spec.md`](spec.md) (normative — what the solution must do)

**Parity plan (started kinds):** [`parity-roadmap.md`](parity-roadmap.md)

**G12 requirement inventory:** [`spec-gates/g12-backlog-template.md`](spec-gates/g12-backlog-template.md) + per-kind `spec-gates/{kind}-g12.md`

**This file:** implementation progress, gate registry, and operational notes only.

**Maintenance rule:** update this file at the end of **every completed task or requested change** (pass heading, summary table, truthfulness checklist, and decision gates as applicable). Do not leave implementation claims stale across sessions.

Regenerate manifest summary after manifest changes:

```powershell
dotnet test new_src/MermaidRecognition.Tests/MermaidRecognition.Tests.csproj
# Optional: tools/Generate-DiagramGateRegistry.ps1 → spec-gates/registry.generated.md
```

---

## Summary

| Area | Status |
| --- | --- |
| **Stage 11.B flowchart routing (label-invariant)** | **Pilot** — `FlowchartRouter`; all edge labels ignored (§11.8) |
| **MSAGL Sugiyama layout** | **Adopted** — flat `Sugiyama/` in `MermaidRecognition.Layout` (503 files); no `extern` project reference |
| **Stage 11.A recognition exit (L1 all §3.18.2 kinds)** | **Met** — smoke detect + diagnostics + fixtures |
| **Flowchart recognition depth** | **L4** — G12 front-matter theme + spacing; F87 metadata |
| **Wave kinds (sequence, class, state, ER)** | **L4** — minimum G12 slice transcribed; `*L4AxisGateTests` |
| **Secondary kinds (pie, gantt, mindmap, gitGraph)** | **L4** — L3 closed + minimum G12 slice; `*L4AxisGateTests` |
| **Started kinds recognition parity** | **9/9 at L4** — minimum G12 slice per [`spec-gates/{kind}-g12.md`](spec-gates/g12-backlog-template.md) |
| **G12 requirement docs** | **Done** — template + 9 backlogs (flowchart exemplar + 8 kinds) |
| **Catalogue back-fill (S/C/ST/E)** | **100%** active scenarios mapped (remarked: C24, ST19, E10) |
| **Default DOCX render path (flowcharts)** | **Native pilot** — `md2docx` default: MermaidRecognition → **Sugiyama SVG**; legacy Naiad fallback when native fails |
| **Default DOCX render path (other kinds)** | **Legacy** — direct Naiad until per-kind native adapters exist |
| **Native render (product path)** | **Pilot** — Sugiyama SVG uses `FlowchartRenderOptions` (theme + style + §3.17 layout/typography); comparison DOCX |
| **Tests (`MermaidRecognition.Tests`)** | **943** passing |
| **Tests (`MermaidRecognition.Layout.Tests`)** | **208** total (**194** passing; 14 failing — see pass 42) |
| **Tests (`MermaidRecognition.RuleValidation.Tests`)** | **48** passing |
| **Tests (`MermaidRecognition.NaiadAdapter.Tests`)** | **47** passing |
| **Rule validation harness (`flow.md`)** | **Pass** — 132 scenarios, 128 evaluated, 4 skipped (F65/F70/F87/F88), **0** errors |

**Progress shorthand:** `L1: 22/22` · `started kinds L4: 9/9` · `rule harness: 0 errors` · pass 42 · [`parity-roadmap.md`](parity-roadmap.md)

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
| Legacy Naiad render (comparison only) | Unchanged | Explicit via `--use-legacy-flowchart-renderer` or `TryRenderLegacyNaiadFlowchartSvg`; frozen baseline |
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

**Commit:** `5a595d8` — closes gap between `status.md` claims and repository reality for Stage **11.B** flowchart routing and a first native SVG path.

| Deliverable | Location | Tests |
| --- | --- | --- |
| Structural flowchart router (topology-only) | `Render/FlowchartRouter.cs` | `FlowchartRouterTests` (2) |
| All-edge label invariance (§11.8) | same | `FlowchartRouterLabelInvarianceTests` (4) — variants, mutation, subgraph, static `.Label` gate |
| Render eligibility gate | `NativeRenderCapabilities.cs` | `TruthfulnessChecklistTests` |
| Flowchart SVG pilot (grid layout) | `NativeFlowchartSvgRenderer.cs` | `NativeFlowchartSvgRendererTests` (1) |
| Legacy Naiad Yes/No baseline proof | reads `extern/Naiad/.../CoordinateAssignment.cs` | `LegacyNaiadLabelSemanticsTests` (1) |
| Checklist automation | `Truthfulness/TruthfulnessChecklistTests.cs` | L4 manifests, router type, `CanRender`, SVG smoke (5) |

**Routing rule (normative):** native `FlowchartRouter` must ignore **all** edge label text — empty, English, numeric, unicode, long text. Legacy Naiad still branches on `"Yes"`/`"No"` (comparison baseline only; must not be copied).

**Suite after pass 20:** 799 tests passing (+13 vs pass 19).

### Pass 21 — MSAGL Sugiyama layout + diagram edge-case gates (2026-05-31)

**Uncommitted** — extends pass 20 with vendored Microsoft.AGL, layout adapter, Sugiyama SVG pilot, comprehensive L1/started-kind edge-case tests, and SvgHarness native emit.

| Deliverable | Location | Tests |
| --- | --- | --- |
| Vendored MSAGL (transitional spike) | `extern/Microsoft.AGL/` | build gate until adopted slice copied to `new_src/.../Sugiyama/` |
| DiagramContract → GeometryGraph adapter | `MermaidRecognition.Layout/FlowchartGeometryGraphBuilder.cs` | layout engine tests |
| Sugiyama layout engine | `FlowchartSugiyamaLayoutEngine.cs` | `FlowchartSugiyamaLayoutEngineTests` (3) |
| Sugiyama SVG renderer (R1 pilot) | `FlowchartSugiyamaSvgRenderer.cs` | `FlowchartR1RenderGateTests` (5) |
| L1 + started-kind edge cases | `Gates/DiagramKindEdgeCaseTests.cs`, `StartedKindEdgeCaseTests.cs`, `DiagramKindEdgeCaseCatalog.cs` | positive + negative for all 22 L1 kinds, 9 started kinds, 7 Mermaid-only fixtures, yaml trap |
| SvgHarness native emit | `src/SvgHarness/Program.cs` `--emit-native` | manual / R1 fixture slice |

**MSAGL policy:** Required MSAGL classes are **copied into** `new_src/MermaidRecognition.Layout/Sugiyama/` as first-party code (MIT preserved). `extern/Microsoft.AGL/` is a **transitional** spike until that slice compiles without it. See `SUGIYAMA_ADOPTION.md`.

**Suite after pass 21:** **908** recognition tests + **8** layout tests = **916** total (+117 vs pass 20).

### Pass 22 — comparison DOCX for visual review (2026-05-31)

**Uncommitted** — first viewable side-by-side legacy vs native output in Word.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Native render bridge | `NativeFlowchartRenderBridge.cs` | Recognition → Sugiyama SVG |
| Comparison DOCX generator | `MDConverter/MermaidCompareDocxGenerator.cs` | Renders both paths to PNG, embeds in DOCX |
| CLI | `src/NativeCompareDocx/` | Default output: `out/r1-native-compare.docx` |
| Layout tests | `NativeFlowchartRenderBridgeTests.cs` | +2 tests |

**Generate comparison DOCX:**

```powershell
dotnet run --project src/NativeCompareDocx/NativeCompareDocx.csproj
# Opens: out/r1-native-compare.docx
# Artefacts (PNG + markdown): out/r1-native-compare/
```

Scenarios: chain A-B-C, subgraph membership, decision fan-out, F02 LR chain. Each section shows Mermaid source, **Legacy (Naiad)** PNG, **Native (Sugiyama)** PNG.

**Not yet:** full R1 visual parity on `flow.md`; SvgHarness structural compare native vs reference; non-flowchart native render in md2docx.

**Suite after pass 22:** **908** recognition + **10** layout = **918** total.

### Pass 23 — MSAGL adopted into flat `Sugiyama/` (2026-05-31)

**Uncommitted** — copy/adopt Phase 1–2 complete; nested `Sugiyama/Msagl/` removed in favour of flat tree.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Adopted MSAGL slice | `MermaidRecognition.Layout/Sugiyama/` | 503 files: `Core/`, `Layout/`, `Routing/`, `DebugHelpers/`, `Miscellaneous/` |
| No `extern` project reference | `MermaidRecognition.Layout.csproj` | Builds on `net10.0` only |
| Re-copy tool | `tools/Copy-SugiyamaSlice.ps1` | Target: flat `Sugiyama/` |
| Adoption docs | `SUGIYAMA_ADOPTION.md`, `THIRD_PARTY_NOTICES.md`, `new_src/README.md` | Namespace map + phase table aligned to flat layout |

**Suite after pass 23:** **918** total (unchanged test count).

### Pass 24 — Sugiyama default md2docx renderer (2026-05-31)

**Uncommitted** — product default flowchart SVG path switched from legacy Naiad to native Sugiyama.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Default render orchestration | `FlowchartRecognitionRenderBridge.cs` | Sugiyama first via `NativeFlowchartRenderBridge`; Naiad fallback on failure |
| Legacy render API (comparison/tests) | `TryRenderLegacyNaiadFlowchartSvg` | NaiadAdapter fidelity tests + `NativeCompareDocx` legacy column |
| DOCX options | `MermaidOptions.UseNativeSugiyamaRenderer` | Default **true** in `OptionsResolver` |
| md2docx CLI | `--use-legacy-flowchart-renderer` | Skips Sugiyama; Naiad-only recognition render |
| DOCX writer | `DocxWriter.RenderMermaidSvg` | Passes native/legacy flag from resolved options |

**Default `md2docx` flowchart path:** MermaidRecognition parse → **Sugiyama SVG** → legacy Naiad fallback → direct Naiad only when `--use-legacy-flowchart-parser`.

```powershell
dotnet run --project src/md2docx/md2docx.csproj -- flow.md flow.docx
dotnet run --project src/md2docx/md2docx.csproj -- flow.md flow.docx --use-legacy-flowchart-renderer
```

**Not yet:** full R1 visual parity on `flow.md` (subgraphs, styles, edge curves); non-flowchart native render.

**Suite after pass 24:** **908** recognition + **10** layout + **47** NaiadAdapter = **965** total.

### Pass 41 — F29 branch order, F33 spine, F34 retry route (2026-05-31)

**Uncommitted** — decision/fan-out targets ordered by creation index; join row on spine; orthogonal retry loop above nodes.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Branch order | `FlowchartLayoutPostProcessor.cs` | `OrderDecisionFanOutTargets` — Accept left of Reject (F29) |
| Join row spine | `FlowchartLayoutPostProcessor.cs` | `SpreadFanOutTargetsAroundHub`, shared-hub fan-in, LR single-successor target align (F33) |
| Retry loop route | `FlowchartEdgePathBuilder.cs`, `FlowchartSugiyamaSvgRenderer.cs` | Orthogonal path above diagram (Naiad constants, F34) |
| Back-edge detect | `FlowchartTopologyIndex.cs`, `FlowchartRouter.cs` | DFS cycle-back classification (no label reads) |
| Tests | `FlowchartCatalogueRenderTests.cs` | F29, F33, F34 |

**Suite after pass 41:** **930** recognition + **47** layout + **47** NaiadAdapter = **1024** total.

### Pass 42 — Rule validation harness, F58 corridor routing, layout contracts (2026-06-07)

**Commit:** `de1a0e8` — structural rule-validation pipeline, Sugiyama routing depth, specification back-fill.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Rule validation library | `new_src/MermaidRecognition.RuleValidation/` | `RulesEngine`, evaluators, `ActualResultsGenerator` |
| Rule validation tests | `new_src/MermaidRecognition.RuleValidation.Tests/` | 48 passing |
| CLI harness | `src/RuleValidationHarness/` | `flow.md` → `actual-results.json` + `validation-report.json` |
| Rule artefacts | `Rule Specification/` | `ruleset.json` (generated), `ruleset-org.json` (catalogue overlay), `Build-Ruleset.ps1` |
| F58 skip-bypass | `FlowchartDecisionPortAllocator`, `FlowchartSkipBypassRouteBuilder` | Right `No` port when `HasInterveningSpineColumnNode`; obstacle-aware return chord |
| F58 corridor SVG | `FlowchartCurvePathBuilder` | `BuildCorridorCurveBasisPathData`; orthogonal polylines only for `RetryLoop` (F34) |
| F53A stadium merge | `FlowchartTerminalCollectorPortAllocator` | Centre-bottom exit; shoulder attach on upper cap |
| F60 cross-subgraph | `FlowchartCrossSubgraphRouteHelper`, `FlowchartSubgraphLayoutPostProcessor` | Spine-aligned sibling rows; exterior dotted cross-link corridor |
| F73 parallel columns | `FlowchartSubgraphLayoutPostProcessor` | `AlignParallelSubgraphColumns` |
| F83 LR fan-in order | Layout post-processor | First-declared ampersand source above second |
| Specifications | `spec.md` §5.5–§5.6, §9.1; `flow.md`; `Mermaid_Specification.md` §17.5; rule-validation spec §3.1, §20 | Catalogue harness contracts + `Build-Ruleset` workflow |

**`flow.md` rule-validation gate:** **132** scenarios · **128** evaluated · **4** skipped (fully remarked F65, F70, F87, F88) · **0** errors · `OverallResult: Pass`.

```powershell
dotnet run --project src/RuleValidationHarness/RuleValidationHarness.csproj -c Release -- flow.md out/flow-rule-validation "Rule Specification"
powershell -File "Rule Specification/Build-Ruleset.ps1"
```

**Suite after pass 42:** **943** recognition + **194**/208 layout + **48** rule validation + **47** NaiadAdapter. Layout: **14** tests failing (investigate before claiming full layout green).

### Pass 40 — F21 cross marker, F28 minlen, F32 fan-in (2026-05-31)

**Uncommitted** — cross end outside target; minlen pushes targets; fan-in target centred on source row.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Cross end marker | `FlowchartSugiyamaSvgRenderer.cs` | Centre offset outward from boundary (same as circle) |
| Minlen | `FlowchartLayoutPostProcessor.cs` | `PushTargetAwayFromSource` — aligned sources, staggered destinations (F28) |
| Fan-in centring | `FlowchartLayoutPostProcessor.cs` | `CenterFanInTargets` — Aggregate centred over workers (F32) |
| Tests | `FlowchartCatalogueRenderTests.cs` | F21 cross boundary, F28 source align, F32 Agg centre |

**Suite after pass 40:** **930** recognition + **44** layout + **47** NaiadAdapter = **1021** total.

### Pass 39 — F13 cylinder winding, F18 row order (2026-05-31)

**Uncommitted** — Mermaid dagre-wrapper cylinder paths; LR disconnected rows stack top-down in declaration order.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Cylinder fill/outline | `FlowchartShapePathGenerator.cs` | Both top arcs (sweep 0) + bottom arc; removed incorrect back-rim overlay |
| LR row stacking | `FlowchartLayoutPostProcessor.cs` | Trailing-edge pack on cross axis — first row above second (F18) |
| Tests | `FlowchartCatalogueRenderTests.cs` | F13 Mermaid winding, F18 row order |

**Suite after pass 39:** **930** recognition + **43** layout + **47** NaiadAdapter = **1020** total.

### Pass 38 — F28 minlen stagger and column spacing (2026-05-31)

**Uncommitted** — per-edge minlen pushes sources (not targets); bottom rank aligned; columns packed by bounding box.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Minlen direction | `FlowchartLayoutPostProcessor.cs` | `PushSourceAwayFromTarget` — stagger A/C/E upward; B/D/F share bottom rank |
| Root rank align | `FlowchartLayoutPostProcessor.cs` | `AlignRootNodesToCommonRank` — baseline rank for edge sources only (skips F12) |
| Component pack | `FlowchartLayoutPostProcessor.cs` | `GetComponentPackBounds` — leading edge + extent (no overlap) |
| Tests | `FlowchartCatalogueRenderTests.cs` | F28 bottom alignment, top stagger, column order |

**Suite after pass 38:** **930** recognition + **42** layout + **47** NaiadAdapter = **1019** total.

### Pass 37 — F12 spacing, F13 cylinder rim, F22 start markers (2026-05-31)

**Uncommitted** — disconnected LR nodes in a row; cylinder back rim; start circle/cross outside source.

| Deliverable | Location | Notes |
| --- | --- | --- |
| F12 layout | `FlowchartLayoutPostProcessor.cs` | Single-node components pack on flow axis (X) + Y alignment |
| Cylinder stroke | `FlowchartShapePathGenerator.cs` | Back rim arc added (curved top edge, no horizontal chord) |
| Start markers | `FlowchartSugiyamaSvgRenderer.cs` | Circle/cross offset outward from source boundary (F22 o--o, x--x) |
| Tests | `FlowchartCatalogueRenderTests.cs` | F12 horizontal, F13 three-arc outline, F22 arrow tips |

**Suite after pass 37:** **930** recognition + **41** layout + **47** NaiadAdapter = **1018** total.

### Pass 36 — Cylinder, diamond clip, fan-in, F28 order (2026-05-31)

**Uncommitted** — cylinder front-rim only; shape-aware edge clip; fan-in layout; disconnected component LR order; edge-label viewBox.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Cylinder fill | `FlowchartShapePathGenerator.cs` | Front rim + body + bottom arc only (no back-top chord) |
| Edge clip | `FlowchartShapeLayoutHelper.cs` | Diamond/circle/cylinder ray exit (F29/F30 touch source edge) |
| Fan-in | `FlowchartLayoutPostProcessor.cs` | Skip single-successor centring for fan-in sources (F32) |
| Component order | `FlowchartLayoutPostProcessor.cs` | Disconnected pairs ordered A→B, C→D, E→F left-to-right (F28) |
| Edge label clip | `FlowchartSugiyamaSvgRenderer.cs` | viewBox expanded for wrapped edge labels (F26) |
| Tests | `FlowchartCatalogueRenderTests.cs` | F13 fill, F28 column order, F32 fan-in separation |

**Suite after pass 36:** **930** recognition + **39** layout + **47** NaiadAdapter = **1016** total.

### Pass 35 — Marker placement, edge labels, F28/F29 layout (2026-05-31)

**Uncommitted** — circle/cross leading edge on node boundary; transparent edge labels; per-edge minlen; Start centred above decision.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Marker placement | `FlowchartSugiyamaSvgRenderer.cs` | Circle/cross offset so outer edge meets boundary (F21) |
| Edge labels | SVG renderer, `FlowchartRenderOptions` | No background rect; 75% font size (`EdgeLabelFontSizePx`) |
| Minlen (multi-component) | `FlowchartLayoutPostProcessor.cs` | Push target subtree only — F28 A–B / C–D / E–F differ |
| Branch centring | `FlowchartLayoutPostProcessor.cs` | Fan-out first, then single-successor (F29 Start over Check) |
| Tests | `FlowchartCatalogueRenderTests.cs` | F21 marker, F24 no rect, F28 combined, F29 Start centre |

**Suite after pass 35:** **930** recognition + **37** layout + **47** NaiadAdapter = **1014** total.

### Pass 34 — Minlen, edge-label wrap, spacing (2026-05-31)

**Uncommitted** — F24–F28 spacing/label parity: minlen gaps, wrapped edge labels, clearance from nodes.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Minlen spacing | `FlowchartLayoutPostProcessor.cs` | `(MinLength - 1) × rankSpacing` pushed along flow axis (F28) |
| Edge label wrap | `FlowchartNodeSizeEstimator.MeasureWrappedLabel`, SVG renderer | Uses `flowchart.wrappingWidth` + multiline `<tspan>` (F25/F26) |
| Label clearance | `FlowchartLayoutPostProcessor.cs` | Expands flow-axis gap when wrapped label wider than edge span |
| Tests | `FlowchartCatalogueRenderTests` | F26 wrap, F28 minlen rank gaps |

**Suite after pass 34:** **930** recognition + **33** layout + **47** NaiadAdapter = **1010** total.

### Pass 33 — Catalogue F11–F24 shape and edge parity (2026-05-31)

**Uncommitted** — fixes shape order, cylinder outline, asymmetric clipping, endpoint markers, edge labels, fan-out centring.

| Deliverable | Location | Notes |
| --- | --- | --- |
| F11 fixture | `flow.md` | LR chain `A→B→C→D` (Rectangle … Circle) |
| Cylinder stroke | `FlowchartShapePathGenerator.cs` | Front rim + bottom arc only (no back-top chord) |
| Asymmetric bounds | `FlowchartShapeLayoutHelper.cs`, SVG renderer | Left notch included in viewBox and edge clip |
| Edge markers | `FlowchartSugiyamaSvgRenderer.cs` | Circle/cross + start markers; bidirectional `<-->`, `o--o`, `x--x` |
| Edge labels | `FlowchartSugiyamaSvgRenderer.cs` | Mid-edge text + `edgeLabelBackground` |
| Fan-out centring | `FlowchartLayoutPostProcessor.cs` | F24 Request centred between Approved/Rejected |
| Tests | `FlowchartCatalogueRenderTests.cs` | F11, F13, F15, F21, F22, F24 gates |

**Suite after pass 33:** **930** recognition + **31** layout + **47** NaiadAdapter = **1008** total.

### Pass 32 — LR rank spacing (2026-05-31)

**Uncommitted** — F10 LR nodes overlapped because rank spacing was computed from node height after pass 31.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Layout input swap | `FlowchartGeometryGraphBuilder.cs` | Transpose w/h for Sugiyama on LR/RL so `rankSpacing` applies along flow axis |
| Measured bounds | `FlowchartSugiyamaLayoutEngine.cs` | Restore display dimensions from estimator after layout |
| Test | `FlowchartSugiyamaLayoutEngineTests` | F10 gap ≥ 49 px between adjacent nodes |

**Suite after pass 32:** **930** recognition + **25** layout + **47** NaiadAdapter = **1002** total.

### Pass 31 — LR node box dimensions (2026-05-31)

**Uncommitted** — F10/F02 LR diagrams showed tall narrow nodes with overflowing quoted labels.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Remap fix | `FlowchartDirectionRemapper.cs` | Swap rank positions only; keep measured width/height (horizontal text stays horizontal) |
| Tests | `FlowchartSugiyamaLayoutEngineTests`, `FlowchartR1RenderGateTests` | F10 wider-than-tall; rendered path width matches estimate |

**Suite after pass 31:** **930** recognition + **24** layout + **47** NaiadAdapter = **1001** total.

### Pass 30 — LR/RL direction remap fix (2026-05-31)

**Uncommitted** — F02 `flowchart LR` was rendering as RL (Start on the right); F06 RL was inverted too.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Axis mirror | `FlowchartDirectionRemapper.cs` | Mirror X after TB→LR swap; RL uses swap only (removed incorrect RL mirror) |
| Tests | `FlowchartSugiyamaLayoutEngineTests` | F02 Start left of End; F06 East right of West |

**Suite after pass 30:** **930** recognition + **22** layout + **47** NaiadAdapter = **999** total.

### Pass 29 — Arrow tips meet node borders (2026-05-31)

**Uncommitted** — fixes visible gap between arrowheads and target nodes in vertical/horizontal chains.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Edge tip vs line end | `FlowchartSugiyamaSvgRenderer.cs` | Boundary point kept for arrowhead tip; line shortened separately by `ARROWHEAD_LENGTH_PX` |
| Draw order | `FlowchartSugiyamaSvgRenderer.cs` | Node shapes → edges (incl. arrowheads) → labels so tips are not hidden under fills |
| Test | `FlowchartR1RenderGateTests` | `RenderToSvg_Chain_ArrowTipsMeetTargetNodeBorders` |

**Suite after pass 29:** **930** recognition + **20** layout + **47** NaiadAdapter = **997** total.

### Pass 28 — Flow direction and rounded-rect shape (2026-05-31)

**Uncommitted** — fixes native layout ignoring declared direction; documents rounded bracket shape vs rect.

| Deliverable | Location | Notes |
| --- | --- | --- |
| Direction normaliser | `Render/FlowchartDirectionNormalizer.cs` | Accepts contract enum names (`LeftToRight`) and Mermaid tokens (`LR`, `TD`→`TB`) |
| Post-layout remap | `FlowchartDirectionRemapper.cs` | TB Sugiyama output remapped to LR/RL/BT (avoids MSAGL plane-transform crash) |
| Layout engine | `FlowchartSugiyamaLayoutEngine.cs` | Always lays out TB; applies direction remap before SVG |
| Tests | `FlowchartDirectionNormalizerTests`, `FlowchartSugiyamaLayoutEngineTests` | LR spreads horizontally; default/TB spreads vertically |

**Rounded corners:** `A(text)` is shape **`rounded`** (spec §3.5) — a distinct bracket shape, not a CSS corner style on `A[text]`. Pass 27 renders it with 5px corner radius. `A([text])` is **stadium/pill** (different shape).

**Not yet:** per-subgraph direction rotation; symbolic headers (`flowchart >`) affect contract only — layout uses diagram-level direction.

**Suite after pass 28:** **930** recognition + **19** layout + **47** NaiadAdapter = **996** total.

### Pass 27 — Flowchart shapes and edge/node stroke rendering (2026-05-31)

**Uncommitted** — native SVG renders Mermaid node shapes and author line/border styles (not just theme fill).

| Deliverable | Location | Notes |
| --- | --- | --- |
| Shape path generator | `Render/FlowchartShapePathGenerator.cs` | rect, rounded, diamond, circle, doublecircle, stadium, hexagon, cylinder, subroutine, asymmetric, trapezoids |
| SVG shape output | `FlowchartSugiyamaSvgRenderer.cs` | `<path>` per shape; cylinder/subroutine stroke overlays; edge clip + arrowheads |
| Edge class styles | `FlowchartRenderOptions.cs` | `classDef`/`class` on edges via `ApplyEdgeClassDefStyles` |
| Tests | `FlowchartR1RenderGateTests` | F11 shapes, F12 stadium arc, F19 dotted, F54 strokes, F55 linkStyle |

**Applies today:** distinct node shapes; `style`/`classDef` fill **and stroke** on nodes; `linkStyle` colours/dasharrays; dotted/thick link tokens; arrowheads on `-->` edges.

**Not yet:** circle/cross/bidirectional markers; curved edge routing; subgraph boxes; full Naiad-grade edge attachment for stadium merges.

**Suite after pass 27:** **917** recognition + **17** layout + **47** NaiadAdapter = **981** total.

### Pass 26 — Flowchart layout defaults from effectiveConfig (2026-05-31)

**Uncommitted** — native Sugiyama path reads spec §3.17.2–§3.17.3 / §3.17.28 layout and typography defaults (not just colours).

| Deliverable | Location | Notes |
| --- | --- | --- |
| Layout config on render options | `Render/FlowchartRenderOptions.cs` | `nodeSpacing`, `rankSpacing`, `diagramPadding`, `padding`, `wrappingWidth`, `curve`, `fontWeight`, `htmlLabels` |
| Label measurement | `Render/FlowchartNodeSizeEstimator.cs` | Wrap + bbox heuristics (Naiad-aligned); shape-aware sizing (rect, diamond, circle, hexagon, stadium) |
| Geometry graph sizing | `FlowchartGeometryGraphBuilder.cs` | Replaces fixed 120×48 with measured node boxes |
| Sugiyama spacing | `FlowchartSugiyamaLayoutEngine.cs` | `LayerSeparation` ← `rankSpacing`; `NodeSeparation` ← `nodeSpacing` |
| SVG viewport + labels | `FlowchartSugiyamaSvgRenderer.cs` | `diagramPadding` margin; `font-weight`; multi-line `<tspan>` labels |
| Tests | `FlowchartRenderOptionsTests`, `FlowchartNodeSizeEstimatorTests`, `FlowchartSugiyamaLayoutEngineTests` | Pinned defaults, init overrides, wrap height, rank spacing |

**Applies today:** label-driven node dimensions, config-driven rank/node spacing, diagram padding, Trebuchet 16px / normal weight defaults, wrapped labels at `wrappingWidth`.

**Not yet:** alternate `init.theme` palette resolution; subgraph/cluster boxes; MSAGL edge curves per `flowchart.curve`; full shape SVG paths (stadium, hexagon, cylinder).

**Suite after pass 26:** **917** recognition + **12** layout + **47** NaiadAdapter = **976** total.

### Pass 25 — Native SVG theme and style options (2026-05-31)

**Uncommitted** — Sugiyama renderer consumes recognition `effectiveConfig` and style directives (spec §3.17, §4.1).

| Deliverable | Location | Notes |
| --- | --- | --- |
| Typed render options | `Render/FlowchartRenderOptions.cs` | Reads `themeVariables.*` from `DiagramConfigContract.EffectiveValues` |
| Resolved paint DTO | `Render/FlowchartResolvedStyle.cs` | Fill, stroke, typography for SVG output |
| SVG integration | `FlowchartSugiyamaSvgRenderer.cs` | Theme defaults + `style` / `classDef` / `linkStyle` on nodes and edges |
| Tests | `FlowchartRenderOptionsTests`, `FlowchartR1RenderGateTests` | Default theme colours, F53 fills, linkStyle colours |

**Applies today:** pinned default theme (`#ECECFF` nodes, `#9370DB` borders, `#333333` edges), inline `style`, `classDef`/`class`, indexed `linkStyle`, thick/dotted stroke tokens.

**Not yet:** alternate `init.theme` palette resolution (e.g. `forest`, `dark` — only `theme` key stored today); subgraph/cluster boxes; MSAGL edge curves.

**Suite after pass 25:** **912** recognition + **11** layout + **47** NaiadAdapter = **970** total.

### Stage 11.B progress (native flowchart routing)

| Item | Status | Notes |
| --- | --- | --- |
| `FlowchartRouter` structural classification | **Pilot done** | `SimpleLink`, `DecisionFanOut`, `DecisionFanIn`, `InvisibleLink`, `BackEdgeLoop`, `CrossGroupBoundary` |
| Label-invariance gate | **Done** | Behavioural + static source gate; spec §11.B exit criterion partially met |
| `NativeFlowchartSvgRenderer` | **Pilot done** | Grid layout only — superseded for product path by Sugiyama pilot |
| MSAGL Sugiyama / coordinate assignment | **Adopted (pilot)** | Flat `Sugiyama/`; default md2docx flowchart renderer; §11.1.11.1 |
| SvgHarness R1 parity slice | **Pilot started** | `--emit-native` writes `*.native.svg`; **comparison DOCX** at `out/r1-native-compare.docx` |
| Strangler route-kind migration (11.C) | Not started | Per route kind vs legacy comparison |

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
| `ContractToFlowchartModel` (legacy compare) | Done | `NaiadAdapter/` — used by `TryRenderLegacyNaiadFlowchartSvg` only |
| `FlowchartRouter` (structural, label-invariant) | **Pilot done** | `Render/FlowchartRouter.cs` — Stage 11.B entry |
| `NativeFlowchartSvgRenderer` | **Pilot done** | `Render/NativeFlowchartSvgRenderer.cs` — grid layout only (superseded by Sugiyama for md2docx) |
| `NativeRenderCapabilities` | **Pilot done** | `Render/NativeRenderCapabilities.cs` — eligibility gate |
| `FlowchartRecognitionRenderBridge` | **Done (default Sugiyama)** | `NaiadAdapter/` — native first; `TryRenderLegacyNaiadFlowchartSvg` for comparison |
| `NativeFlowchartRenderBridge` / Sugiyama SVG | **Pilot done** | `MermaidRecognition.Layout/` — theme + style via `FlowchartRenderOptions` |
| MSAGL Sugiyama layout (Dagre replacement candidate) | **Adopted (pilot)** | Flat `Sugiyama/`; Phase 3–4 trim pending — §11.1.11.1 |
| Per-kind native render adapters | Not started | `DiagramContract` → SVG/layout for non-flowchart kinds |
| ELK layout port (`flowchart-elk`) | Deferred | §11.1.11.2; vendored MIT `mermaid/packages/mermaid-layout-elk/` + `elkjs` |
| §11.E Mermaid.js fallback | Not started | Official renderer fallback when native path unavailable |

---

## Phased work — what is next

Recognition minimum slice for all started kinds is **complete**. **Naiad is not a forward option** — it remains frozen under `extern/Naiad` and is used only when comparing legacy output against a new native render path (SvgHarness, DOCX review). See [`parity-roadmap.md`](parity-roadmap.md).

1. **Native render — flowchart depth** — extend Sugiyama SVG (subgraphs, styles, MSAGL edge curves); SvgHarness R1 parity on `flow.md` slice.
2. **Native render — started kinds** — per-kind `DiagramContract` → render adapters (sequence, class, state, ER, pie, gantt, mindmap, gitGraph).
3. **Visual gates** — visual catalogues (`pie.md`, `gantt.md`, …); SvgHarness R1; optional §11.E Mermaid.js fallback.
4. **MDConverter default path** — flowcharts on Sugiyama (pass 24); remaining kinds + full R1 gates before claiming render complete.
5. **Extended G12 backlog** — optional parser depth in `spec-gates/*-g12.md`.
6. **Operational** — clean vendored Mermaid pin (currently **dirty**); regen `spec-gates/registry.generated.md`.
7. **`flowchart-elk`** — separate ELK track (§11.1.11.2).

Stage 11.A recognition exit is **met and exceeded**. Stage **11.B** routing pilot is **started** (structural router + label-invariance gates). Stages **11.C–E** target **native render from `DiagramContract`**, not Naiad maintenance.

---

## Truthfulness checklist (repository vs claims)

Automated: `new_src/MermaidRecognition.Tests/Truthfulness/TruthfulnessChecklistTests.cs` (+ `LegacyNaiadLabelSemanticsTests`, `FlowchartRouterLabelInvarianceTests`).

| Statement | As of pass 42 |
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
| Native render from `DiagramContract` | **Partial (pilot)** — Sugiyama SVG with shapes, structural routing (corridor/bypass/fan), theme + style/classDef/linkStyle strokes, §3.17 layout defaults |
| `flow.md` rule-validation harness passes | **Yes** — 0 errors on 128 active diagrams (4 remarked skipped) |
| Rules generated from `ruleset-org.json` | **Yes** — `Build-Ruleset.ps1` → `ruleset.json` v2.0.0 |
| md2docx default flowchart renderer is Sugiyama | **Yes** — `FlowchartRecognitionRenderBridge` → `NativeFlowchartRenderBridge`; Naiad fallback |
| Native SVG uses Mermaid theme defaults | **Yes (default theme)** — `FlowchartRenderOptions` reads `effectiveConfig` / `themeVariables.*` |
| Native layout uses flowchart spacing defaults | **Yes (pilot)** — measured nodes + `nodeSpacing` / `rankSpacing` / `diagramPadding` from `effectiveConfig` |
| ELK native layout in scope | No (deferred — §11.1.10, §11.1.11.2) |
| MSAGL Sugiyama native layout in scope | **Adopted (pilot)** — flat `Sugiyama/` (503 files); default md2docx path; trim + SvgHarness parity remain (§11.1.11.1) |
| Stage 11.A changes default DOCX flowchart images | **Partial** — Sugiyama default (pass 24); full visual parity not claimed |
| Stage 11.A L1 recognition for all kinds | Yes |
| Secondary kinds at L4 (minimum G12) | Yes (pie, gantt, mindmap, gitGraph) |
| Wave kinds at L4 (minimum G12) | Yes (sequence, class, state, ER) |
| Extended G12 backlog fully transcribed | No — minimum slice only |

Update this table when implementation or claims change.

---

## Decision gates (not yet met)

- **Stage 11.B label-invariance exit:** **Partially met** — `FlowchartRouter` ignores all edge label text; full §11.B exit still requires layout integration and SvgHarness proof that port roles are unchanged when labels vary.
- **Adopt MSAGL Sugiyama for native layout:** **Phase 1–2 done** — `FlowchartSugiyamaLayoutEngine` + flat `Sugiyama/` (503 files); no `extern` project reference. Remaining: Phase 3–4 trim, modernisation, SvgHarness parity on agreed fixture slice (§11.1.11.1). Transitional re-copy reference: `extern/Microsoft.AGL`.
- **Port native ELK for `flowchart-elk`:** requires vendored Mermaid adapter parity (§11.1.11.2), `config.elk.*` mapping, and ELK-specific acceptance fixtures.
- **Enable native render as MDConverter default:** **Partially met (flowcharts)** — pass 24: md2docx uses Sugiyama by default with Naiad fallback. Exit gate still requires R1 visual parity on agreed fixture slice and native adapters for other kinds.
- **Use legacy Naiad for product render:** **Out of scope** — frozen; comparison and regression baseline only (§12).
- **Claim “recognition complete” for a started kind:** **Met** for all 9 started kinds — manifest **L4** and minimum G12 slice **transcribed** in `spec-gates/{kind}-g12.md` (extended backlog may remain open).

---

## Repository layout note

**Breadcrumb (2026-05-31):** Stage 11.A+ recognition and Stage 11.B routing/layout pilot live in **`new_src/`**. MSAGL adopted under flat `MermaidRecognition.Layout/Sugiyama/` (`SUGIYAMA_ADOPTION.md`); **md2docx flowcharts default to Sugiyama**. `extern/Microsoft.AGL` is transitional re-copy reference only. Legacy Naiad: `extern/Naiad` (frozen — comparison baseline only).


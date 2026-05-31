# G12 backlog template — parser parity inventory

**Authority:** [`spec.md`](../spec.md) §7.4.1 (G12), §7.4.2 (L4), §11.1.14.x (per-kind L4 exit criteria).  
**Programme:** [`parity-roadmap.md`](../parity-roadmap.md).

Each started diagram kind maintains **`spec-gates/{kind}-g12.md`** listing which vendored Mermaid parser tests are **in scope** for recognition L4, how they map to line types and fixtures, and whether they are **transcribed** yet.

---

## When to create / update

| Trigger | Action |
| --- | --- |
| Starting L4 work on a kind | Create `{kind}-g12.md` from this template |
| Adding a recognition fixture for G12 | Add or update a row; set status **transcribed** |
| Promoting manifest `GateLevel` to **L4** | Every row in the kind’s **minimum L4 slice** must be **transcribed**; no **planned** rows in that slice |
| Vendored Mermaid pin changes | Re-audit upstream `*.spec.js` / `*.spec.ts`; update cluster list |

---

## Column schema

| Column | Required | Meaning |
| --- | --- | --- |
| **Cluster id** | Yes | Stable id within the file (e.g. `S-G12-01`, `FC-G12-F86`) |
| **Upstream source** | Yes | Vendored path under `mermaid/packages/mermaid/src/diagrams/{kind}/` |
| **Upstream test** | Yes | `describe` / `it` name, or Mermaid test id (e.g. `MMP-7`, `apa12`) |
| **Line type id(s)** | Yes | §11.1.14.x row(s) this cluster exercises |
| **Catalogue scenario** | If exists | Visual catalogue id (`S14`, `F86`, …) or `—` |
| **Recognition fixture** | If transcribed | Basename under `*Recognition/` (no extension) |
| **L4 axis test** | If transcribed | `*L4AxisGateTests` method name or `G11 manifest parse` |
| **Status** | Yes | See status values below |
| **Notes** | Optional | Gap, deferral reason, render remark (§9.1) |

### Status values

| Status | Meaning |
| --- | --- |
| **transcribed** | Fixture + verified JSON exist; L4 test asserts the axis (or manifest-only for low-risk clusters) |
| **partial** | Fixture exists but contract field or L4 assertion incomplete — document gap in Notes |
| **planned** | In minimum L4 slice; not yet implemented |
| **deferred** | Out of L4 scope for this programme phase (document why) |
| **N/A** | Render/DB test, not parser recognition |

---

## Minimum L4 slice vs full backlog

Each `{kind}-g12.md` has two sections:

1. **Minimum L4 slice** — rows that **must** be **transcribed** before manifest `GateLevel: L4` (mirrors §11.1.14.x L4 exit criteria prose).
2. **Extended G12 backlog** — additional upstream clusters tracked for completeness; may remain **planned** or **deferred** without blocking L4 if excluded from minimum slice.

**Flowchart exemplar:** [`flowchart-g12.md`](flowchart-g12.md) (complete).

---

## Worked example (flowchart cluster)

| Cluster id | Upstream source | Upstream test | Line type id(s) | Catalogue scenario | Recognition fixture | L4 axis test | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| FC-G12-F86 | `flowchart/parser/flow.spec.js` | front-matter `config` | `flowchart.init.frontMatter` | F86 | `config-front-matter` | `G12_F86_FrontMatterConfig_Sets_FlowchartCurve` | **transcribed** | |
| FC-G12-F86-spacing | `flowchart/parser/flow.spec.js` | spacing keys | `flowchart.init.frontMatter` | F86 | `config-front-matter-spacing` | `G12_F86_FrontMatterConfig_Sets_FlowchartSpacing` | **transcribed** | nodeSpacing, rankSpacing, diagramPadding |
| FC-G12-F86-theme | `flowchart/parser/flow.spec.js` | theme + config | `flowchart.init.frontMatter` | — | `config-front-matter-theme` | `G12_F86_FrontMatterConfig_Sets_Theme_And_FlowchartCurve` | **transcribed** | |
| FC-G12-F87 | `flowchart/parser/flow-node-data.spec.js` | edge metadata on `@==>` | `flowchart.edge.metadata` | F87 (remarked render) | `edge-metadata-f87` | `G12_F87_EdgeMetadataAnimate_On_ThickLink` | **transcribed** | §9.1 render remark |
| FC-G12-F88 | `flowchart/parser/flow-style.spec.js` | `classDef` animate on edge id | `flowchart.edge.metadata`, `flowchart.classDef` | F88 (remarked render) | `edge-class-animate` | `G12_F88_EdgeClassAnimate_Fixture_Produces_Animation_Metadata` | **transcribed** | |

---

## Per-path agent checklist

When executing one parallel path (see [`parity-roadmap.md`](../parity-roadmap.md)):

1. Read §11.1.14.x L4 exit criteria for the kind.
2. Inventory upstream parser tests; fill **minimum L4 slice** table first.
3. Add fixtures via `*FixtureWriter` tests; regenerate `.verified.json`.
4. Add `*L4AxisGateTests` methods (one per minimum-slice cluster).
5. Set row status **transcribed**; run full test suite.
6. Promote manifest only when minimum slice is complete.

---

## File index

| Kind | G12 backlog | Minimum slice complete? |
| --- | --- | --- |
| Flowchart | [`flowchart-g12.md`](flowchart-g12.md) | Yes |
| Sequence | [`sequence-g12.md`](sequence-g12.md) | No |
| Class | [`class-g12.md`](class-g12.md) | No |
| State | [`state-g12.md`](state-g12.md) | No |
| ER | [`er-g12.md`](er-g12.md) | No |
| Pie | [`pie-g12.md`](pie-g12.md) | No |
| Gantt | [`gantt-g12.md`](gantt-g12.md) | No |
| Mindmap | [`mindmap-g12.md`](mindmap-g12.md) | No |
| Git graph | [`gitGraph-g12.md`](gitGraph-g12.md) | No |

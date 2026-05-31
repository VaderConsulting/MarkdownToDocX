# Flowchart G12 backlog

**Kind:** flowchart / graph  
**Pin:** Mermaid 11.14.0 (`spec.md` §11.1)  
**Manifest:** `GateLevel: L4`  
**L4 tests:** `FlowchartL4AxisGateTests.cs`  
**Spec:** §11.1.14.1 L4 exit criteria

---

## Minimum L4 slice

| Cluster id | Upstream source | Upstream test | Line type id(s) | Catalogue scenario | Recognition fixture | L4 axis test | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| FC-G12-F86 | `flowchart/parser/flow.spec.js` | YAML front-matter `config.flowchart.curve` | `flowchart.init.frontMatter` | F86 | `config-front-matter` | `G12_F86_FrontMatterConfig_Sets_FlowchartCurve` | **transcribed** | |
| FC-G12-F86-spacing | `flowchart/parser/flow.spec.js` | spacing keys | `flowchart.init.frontMatter` | F86 | `config-front-matter-spacing` | `G12_F86_FrontMatterConfig_Sets_FlowchartSpacing` | **transcribed** | |
| FC-G12-F86-theme | `flowchart/parser/flow.spec.js` | theme + nested config | `flowchart.init.frontMatter` | — | `config-front-matter-theme` | `G12_F86_FrontMatterConfig_Sets_Theme_And_FlowchartCurve` | **transcribed** | |
| FC-G12-F87 | `flowchart/parser/flow-node-data.spec.js` | edge id metadata on thick link | `flowchart.edge.metadata` | F87 | `edge-metadata-f87` | `G12_F87_EdgeMetadataAnimate_On_ThickLink` | **transcribed** | Render remarked §9.1 |
| FC-G12-F88 | `flowchart/parser/flow-style.spec.js` | `classDef` animation on edge id | `flowchart.edge.metadata` | F88 | `edge-class-animate` | `G12_F88_EdgeClassAnimate_Fixture_Produces_Animation_Metadata` | **transcribed** | Render remarked §9.1 |
| FC-G12-edge-meta | `flowchart/parser/flow-node-data.spec.js` | `e1@{ curve, animate }` | `flowchart.edge.metadata` | — | `edge-metadata`, `edge-animate` | L3 fixtures | **transcribed** | Covered at L3; L4 minimum uses F87/F88 |
| FC-G12-grouped-id | `flowchart/parser/flow-vertice-chaining.spec.js` | `A & B e1@--> C & D` | `flowchart.link.grouped`, `flowchart.link.edge_id` | — | `edge-identity` | L3 | **transcribed** | §11.1.2 grouped edge id semantics |

**L4 promotion:** minimum slice complete (manifest **L4**).

---

## Extended G12 backlog

| Cluster id | Upstream source | Upstream test | Line type id(s) | Status | Notes |
| --- | --- | --- | --- | --- | --- |
| FC-G12-chain | `flow-vertice-chaining.spec.js` | `A --> B --> C`, `A & B --> C` | `flowchart.link.chained`, `flowchart.link.grouped` | **transcribed** | L3 fixtures |
| FC-G12-arrows | `flow-arrows.spec.js` | arrow variant matrix | `flowchart.link.arrow_variants` | **transcribed** | `arrow-variants` |
| FC-G12-subgraph | `subgraph.spec.js` | nested, direction | `flowchart.subgraph.*` | **transcribed** | L3 |
| FC-G12-interactions | `flow-interactions.spec.js` | href / callback | `flowchart.click.*` | **partial** | callback diagnostic only |
| FC-G12-elk | `flow.spec.js` | `flowchart-elk` header | `flowchart.header.elk` | **transcribed** | Separate ELK track §11.1.11.2 |
| FC-G12-frontmatter-extra | `flow.spec.js` | keys beyond spacing/theme | `flowchart.init.frontMatter` | **planned** | Maintenance; not in minimum slice |

# Class G12 backlog



**Kind:** class  

**Pin:** Mermaid 11.14.0  

**Manifest:** `GateLevel: L4`  

**L4 tests:** `ClassL4AxisGateTests.cs`  

**Primary upstream:** `classDiagram.spec.ts`, `class/parser/class.spec.js`  

**Spec:** §11.1.14.3 L4 exit criteria



---



## Minimum L4 slice



| Cluster id | Upstream source | Upstream test | Line type id(s) | Catalogue scenario | Recognition fixture | L4 axis test | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- | --- | --- |

| C-G12-01 | `classDiagram.spec.ts` | basic class diagram parse | `classDiagram.header` | C01 | `simple` | `G11_Manifest_Fixtures_All_Parse_Without_Error` | **transcribed** | |

| C-G12-init | `classDiagram.spec.ts` | init / theme | `classDiagram.init.leading` | C35 | `init-leading` | `G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues` | **transcribed** | |

| C-G12-acc | `classDiagram.spec.ts` | accTitle + accDescr | `classDiagram.accTitle`, `classDiagram.accDescr` | — | `accessibility` | `G12_Accessibility_Fixture_Populates_AccTitle_And_AccDescr` | **transcribed** | Block accDescr |

| C-G12-classApply | `classDiagram.spec.ts` | `class Animal highlight` apply semantics | `classDiagram.classApply` | C13 | `classDef-style` | `G12_ClassApply_Fixture_Applies_ClassDef_To_Target_Node` | **transcribed** | Split from `.class` per §11.1.14.3 |

| C-G12-relation | `classDiagram.spec.ts` | relation token matrix | `classDiagram.relation` | C02–C08 | `inheritance`, `relation-*` | `G12_Relation_Fixtures_Produce_Edges_With_Expected_Tokens` | **transcribed** | Nine relation fixtures |

| C-G12-members | `classDiagram.spec.ts` | members, methods, generics in brackets | `classDiagram.member`, `classDiagram.structOpen` | C09 | `members` | `G12_Members_Fixture_Attaches_Member_Declarations` | **transcribed** | |

| C-G12-namespace | `classDiagram.spec.ts` | namespaces nested / compact | `classDiagram.namespace` | — | `namespace` | `G12_Namespace_Fixture_Assigns_ParentGroup_And_Groups` | **transcribed** | |

| C-G12-direction | `classDiagram.spec.ts` | `direction TB/LR/...` | `classDiagram.direction` | — | `direction` | `G12_Direction_Fixture_Sets_Diagram_Direction` | **transcribed** | |



**L4 promotion:** all minimum-slice rows **transcribed**.



---



## Extended G12 backlog



| Cluster id | Upstream source | Upstream test | Line type id(s) | Status | Notes |

| --- | --- | --- | --- | --- | --- |

| C-G12-notes | `classDiagram.spec.ts` | note for / note | `classDiagram.note` | **partial** | |

| C-G12-click | `classDiagram.spec.ts` | href, callback, tooltip | `classDiagram.click` | **deferred** | Callback → diagnostic at render policy |

| C-G12-style | `classDiagram-styles.spec.js` | classDef, style, cssClass | `classDiagram.classDef`, `classDiagram.style` | **partial** | C24 remarked render |

| C-G12-backtick | `class/parser/class.spec.js` | backtick-quoted namespace | `classDiagram.namespace` | **planned** | |

| C-G12-types | `classTypes.spec.ts` | method/attribute parsing | `classDiagram.member` | **N/A** | DB/display helpers; contract uses raw member lines at L3 |


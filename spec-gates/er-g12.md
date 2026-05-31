# ER G12 backlog



**Kind:** er  

**Pin:** Mermaid 11.14.0  

**Manifest:** `GateLevel: L4`  

**L4 tests:** `ErL4AxisGateTests.cs`  

**Primary upstream:** `er/parser/erDiagram.spec.js`  

**Spec:** §11.1.14.5 L4 exit criteria



---



## Minimum L4 slice



| Cluster id | Upstream source | Upstream test | Line type id(s) | Catalogue scenario | Recognition fixture | L4 axis test | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- | --- | --- |

| E-G12-01 | `erDiagram.spec.js` | stand-alone entities | `erDiagram.header`, `erDiagram.entityBlockOpen` | E30 | `entity-attributes` | `G11_Manifest_Fixtures_All_Parse_Without_Error` | **transcribed** | |

| E-G12-init | `erDiagram.spec.js` | init / theme | `erDiagram.init.leading` | E17 | `init-leading` | `G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues` | **transcribed** | |

| E-G12-cardinality | `erDiagram.spec.js` | relationship cardinality matrix | `erDiagram.relationship` | E01–E06, E18 | `relationship`, `relation-cardinality` | `G12_RelationshipCardinality_Fixtures_Produce_Edges_With_Expected_Markers` | **transcribed** | |

| E-G12-attributes | `erDiagram.spec.js` | PK/FK/UK, comments, generics | `erDiagram.attribute` | E07–E09, E12 | `entity-attributes` | `G12_EntityAttributes_Fixture_Attaches_Attribute_Declarations` | **transcribed** | |

| E-G12-direction | `erDiagram.spec.js` | direction TB/LR | `erDiagram.direction` | E11 | `direction` | `G12_Direction_Fixture_Sets_Diagram_Direction` | **transcribed** | |

| E-G12-style | `erDiagram.spec.js` | classDef, class, style | `erDiagram.classDef`, `erDiagram.style` | E13–E14 | `classDef-style`, `inline-style` | `G12_ClassDefStyle_Fixture_Applies_ClassDef_And_Class_To_Entity`, `G12_InlineStyle_Fixture_Applies_Fill_To_Entity` | **transcribed** | |

| E-G12-acc | `erDiagram.spec.js` | accTitle / accDescr | `erDiagram.accTitle`, `erDiagram.accDescr` | E16, E31 | `accessibility`, `accDescr-block` | `G12_Accessibility_Fixture_Populates_AccTitle_And_AccDescr`, `G12_AccDescrBlock_Fixture_Populates_Multiline_Description` | **transcribed** | |

| E-G12-alias | `erDiagram.spec.js` | entity alias `[Display Name]` | `erDiagram.entityAlias` | E10 | `catalogue-remarked-entity-alias` | `G12_EntityAlias_Fixture_Classifies_EntityAlias_LineType` | **transcribed** | Recognition only; render remarked §9.1 |



**L4 promotion:** all minimum-slice rows **transcribed** (manifest **L4**).



---



## Extended G12 backlog



| Cluster id | Upstream source | Upstream test | Line type id(s) | Status | Notes |

| --- | --- | --- | --- | --- | --- |

| E-G12-entity-name | `erDiagram.spec.js` | quoted / special entity names | `erDiagram.entityBlockOpen` | **partial** | E02, E34 |

| E-G12-identifying | `erDiagram.spec.js` | identifying vs non-identifying `--` vs `..` | `erDiagram.relationship` | **partial** | E03, E05 |

| E-G12-split-blocks | `erDiagram.spec.js` | attribute blocks split across entities | `erDiagram.attribute` | **planned** | |

| E-G12-recursive | `erDiagram.spec.js` | recursive edges E28/E29 | `erDiagram.relationship` | **partial** | Catalogue scenarios exist |


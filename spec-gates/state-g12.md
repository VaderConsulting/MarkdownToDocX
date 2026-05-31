# State G12 backlog



**Kind:** state  

**Pin:** Mermaid 11.14.0  

**Manifest:** `GateLevel: L4`  

**L4 tests:** `StateL4AxisGateTests.cs`  

**Primary upstream:** `state/parser/state-parser.spec.js`, `state/parser/state-style.spec.js`, `stateDiagram.spec.js`  

**Spec:** §11.1.14.4 L4 exit criteria



---



## Minimum L4 slice



| Cluster id | Upstream source | Upstream test | Line type id(s) | Catalogue scenario | Recognition fixture | L4 axis test | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- | --- | --- |

| ST-G12-01 | `stateDiagram.spec.js` | basic state diagram | `stateDiagram.header` | ST01 | `simple` | `G11_Manifest_Fixtures_All_Parse_Without_Error` | **transcribed** | |

| ST-G12-init | `stateDiagram.spec.js` | init / theme | `stateDiagram.init.leading` | — | `init-leading` | `G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues` | **transcribed** | |

| ST-G12-composite | `state-parser.spec.js` | groups / composite states | `stateDiagram.composite` | ST05–ST08 | `composite` | `G12_Composite_Fixture_Produces_Composite_Group` | **transcribed** | |

| ST-G12-choice | `stateDiagram.spec.js` | choice / fork (if in pinned grammar) | `stateDiagram.choice` | ST09 | `choice` | `G12_Choice_Fixture_Produces_Choice_Pseudo_State` | **transcribed** | `<<choice>>` pseudo-state |

| ST-G12-note | `state-parser.spec.js` | note parsing (#7089) | `stateDiagram.note` | ST11 | `note` | `G12_Note_Fixture_Produces_Note_Node` | **transcribed** | |

| ST-G12-style | `state-style.spec.js` | classDef / style / `:::` on states | `stateDiagram.classDef`, `stateDiagram.style` | ST12 | `classDef-style` | `G12_ClassDefStyle_Fixture_Applies_ClassDef_To_State` | **transcribed** | |

| ST-G12-transition | `state-parser.spec.js` | colons in transition text (#7418) | `stateDiagram.transition` | ST04 | `transition-colons` | `G12_TransitionColons_Fixture_Preserves_Colons_In_Label` | **transcribed** | Dedicated colon-in-label fixture |

| ST-G12-acc | `stateDiagram.spec.js` | accTitle / accDescr | `stateDiagram.accTitle`, `stateDiagram.accDescr` | — | `accessibility` | `G12_Accessibility_Fixture_Populates_Title_And_Description` | **transcribed** | |



**L4 promotion:** all minimum-slice rows **transcribed** (manifest **L4**).



---



## Extended G12 backlog



| Cluster id | Upstream source | Upstream test | Line type id(s) | Status | Notes |

| --- | --- | --- | --- | --- | --- |

| ST-G12-id-label | `state-parser.spec.js` | stateID as "name", colon forms | `stateDiagram.state` | **partial** | |

| ST-G12-concurrent | `stateDiagram.spec.js` | concurrent regions | `stateDiagram.composite` | **deferred** | `--` divider not separate L2 line type |

| ST-G12-v2 | `stateDiagram-v2.spec.js` | v2 renderer paths | — | **N/A** | Render-focused |

| ST-G12-unsafe | `state-parser.spec.js` | unsafe property names | `stateDiagram.state` | **deferred** | Diagnostic policy TBD |


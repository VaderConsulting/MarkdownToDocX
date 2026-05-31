# Sequence G12 backlog



**Kind:** sequence  

**Pin:** Mermaid 11.14.0  

**Manifest:** `GateLevel: L4`  

**L4 tests:** `SequenceL4AxisGateTests.cs`  

**Primary upstream:** `mermaid/packages/mermaid/src/diagrams/sequence/sequenceDiagram.spec.js`  

**Spec:** §11.1.14.2 L4 exit criteria



---



## Minimum L4 slice



| Cluster id | Upstream source | Upstream test | Line type id(s) | Catalogue scenario | Recognition fixture | L4 axis test | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- | --- | --- |

| S-G12-01 | `sequenceDiagram.spec.js` | `should handle a sequenceDiagram definition` | `sequence.header` | S01 | `simple` | `G11_Manifest_Fixtures_All_Parse_Without_Error` | **transcribed** | Baseline parse |

| S-G12-init | `sequenceDiagram.spec.js` | init / theme before header | `sequence.init.leading` | S24 | `init-leading` | `G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues` | **transcribed** | |

| S-G12-acc | `sequenceDiagram.spec.js` | accTitle + accDescr (inline + block) | `sequence.accTitle`, `sequence.accDescr` | S21, S28 | `sequence-accessibility` | `G12_SequenceAccessibility_Fixture_Populates_Title_And_Description` | **transcribed** | |

| S-G12-autonumber | `sequenceDiagram.spec.js` | autonumber enabled / decimal places | `sequence.autonumber` | S13 | `autonumber` | `G12_Autonumber_Fixture_Sets_SequenceAutonumber_In_Config` | **transcribed** | |

| S-G12-par-over | `sequenceDiagram.spec.js` | `it should handle par_over statements` | `sequence.block` | — | `par-over` | `G12_ParOver_Fixture_Produces_ParOver_Block_Group` | **transcribed** | Upstream `par_over` overlap block |

| S-G12-control | `sequenceDiagram.spec.js` | alt/else, loop, opt, par, critical, break | `sequence.block`, `sequence.end` | S14–S18, S25–S27, S32 | `alt-else-blocks`, `loop-control`, etc. | `G12_AltElseBlocks_Fixture_Produces_Alt_And_Else_Groups` | **transcribed** | Minimum slice uses `alt-else-blocks` |

| S-G12-box | `sequenceDiagram.spec.js` | `should handle box` / `box without color` | `sequence.box` | S19 | `box-rgb` | `G12_BoxRgb_Fixture_Produces_Box_Group_With_Participants` | **transcribed** | |

| S-G12-lifecycle | `sequenceDiagram.spec.js` | create / destroy participant | `sequence.lifecycle` | S20 | `lifecycle-create` | `G12_LifecycleCreate_Fixture_Classifies_Create_And_Destroy` | **transcribed** | |



**L4 promotion:** all minimum-slice rows **transcribed**; manifest → `L4`.



---



## Extended G12 backlog



| Cluster id | Upstream source | Upstream test | Line type id(s) | Status | Notes |

| --- | --- | --- | --- | --- | --- |

| S-G12-central | `sequenceDiagram.spec.js` | Central Connection Parsing | `sequence.message` | **deferred** | Advanced arrow tokens; extend MessageRegex later |

| S-G12-arrows | `sequenceDiagram.spec.js` | async/sync/dotted/bidirectional messages | `sequence.message` | **partial** | S07–S08; `simple`, `activation` |

| S-G12-activation | `sequenceDiagram.spec.js` | activation, stacked, inline +/- | `sequence.activate`, `sequence.message` | **partial** | S09–S10 |

| S-G12-notes | `sequenceDiagram.spec.js` | notes single/multiple actors, wrap | `sequence.note` | **partial** | S11 |

| S-G12-title | `sequenceDiagram.spec.js` | title with/without colon | `sequence.title` | **partial** | S12 |

| S-G12-participant | `sequenceDiagram.spec.js` | alias, dashes, spaces in names | `sequence.participant` | **partial** | S04–S06, apa12/apa13 |

| S-G12-rect | `sequenceDiagram.spec.js` | rect, nested rects | `sequence.block` | **partial** | S26–S27 |

| S-G12-links | `sequenceDiagram.spec.js` | `should handle links` | `sequence.message` | **deferred** | EXPERIMENTAL upstream |

| S-G12-wrap | `sequenceDiagram.spec.js` | wrap / nowrap on messages | `sequence.message`, `sequence.note` | **deferred** | Low priority for recognition contract |


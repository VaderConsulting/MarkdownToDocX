# Pie G12 backlog



**Kind:** pie  

**Pin:** Mermaid 11.14.0  

**Manifest:** `GateLevel: L4`  

**L4 tests:** `PieL4AxisGateTests.cs`  

**Primary upstream:** `pie/pie.spec.ts`  

**Spec:** §11.1.14.6 L3/L4 exit criteria



---



## L3 completion (prerequisite for L4)



| Cluster id | Upstream source | Upstream test | Line type id(s) | Recognition fixture | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- |

| P-L3-01 | `pie.spec.ts` | very simple / simple pie | `pie.header`, `pie.section` | `simple` | **transcribed** | Slice nodes + pieValue |

| P-L3-init | `pie.spec.ts` | init leading | `pie.init.leading` | `init-leading` | **transcribed** | |

| P-L3-title | `pie.spec.ts` | title | `pie.title` | `title-showData` | **transcribed** | |

| P-L3-showData | `pie.spec.ts` | showData | `pie.header` | `title-showData` | **transcribed** | Assert showData in contract metadata |

| P-L3-acc | `pie.spec.ts` | accTitle / accDescr | `pie.accTitle`, `pie.accDescr` | `pie-accessibility` | **transcribed** | Inline accTitle + accDescr |

| P-L3-comment | `pie.spec.ts` | comments | `pie.comment` | `comment` | **transcribed** | |

| P-L3-decimals | `pie.spec.ts` | positive/negative/zero slice values | `pie.section` | `edge-values` | **transcribed** | Decimal and zero slice values |



**L3 promotion:** manifest **L3** stable; `PieL3AxisGateTests` covers G4 entity + applicable G9.



---



## Minimum L4 slice



| Cluster id | Upstream source | Upstream test | Line type id(s) | Recognition fixture | L4 axis test | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- | --- |

| P-G12-01 | `pie.spec.ts` | `should handle simple pie` | `pie.section` | `simple` | `G12_SimplePie_Fixture_Produces_Slice_Nodes_With_Values` | **transcribed** | |

| P-G12-showData | `pie.spec.ts` | `should handle simple pie with showData` | `pie.header` | `title-showData` | `G12_ShowData_Fixture_Sets_PieShowData_In_RawDirectiveValues` | **transcribed** | |

| P-G12-acc | `pie.spec.ts` | accTitle + accDescr (inline + block) | G9 | `pie-accessibility` | `G12_Accessibility_Fixture_Populates_Title_And_Description` | **transcribed** | Inline form; block deferred |

| P-G12-init | `pie.spec.ts` | init config | `pie.init.leading` | `init-leading` | `G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues` | **transcribed** | Mirror wave init-leading L4 test |



**L4 promotion:** L3 complete + all minimum-slice rows **transcribed**.



---



## Extended G12 backlog



| Cluster id | Upstream source | Upstream test | Status | Notes |

| --- | --- | --- | --- | --- |

| P-G12-unsafe | `pie.spec.ts` | unsafe properties | **deferred** | Diagnostic policy |

| P-G12-config | `pie.spec.ts` | getConfig | **N/A** | Render config |

| P-G12-acc-block | `pie.spec.ts` | multiline accDescr | **deferred** | Block form not in minimum slice |


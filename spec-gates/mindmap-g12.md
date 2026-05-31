# Mindmap G12 backlog



**Kind:** mindmap  

**Pin:** Mermaid 11.14.0  

**Manifest:** `GateLevel: L4`  

**L4 tests:** `MindmapL4AxisGateTests.cs`  

**Primary upstream:** `mindmap/mindmap.spec.ts`  

**Spec:** §11.1.14.8 L3/L4 exit criteria



---



## L3 completion (prerequisite for L4)



| Cluster id | Upstream source | Upstream test | Line type id(s) | Recognition fixture | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- |

| M-L3-01 | `mindmap.spec.ts` | MMP-1 simple root | `mindmap.node` | `simple` | **transcribed** | Tree nodes in contract |

| M-L3-hierarchy | `mindmap.spec.ts` | MMP-2, MMP-4 deeper hierarchy | `mindmap.node` | `hierarchy` | **transcribed** | Multi-level indent |

| M-L3-init | `mindmap.spec.ts` | init leading | `mindmap.init.leading` | `init-leading` | **transcribed** | |

| M-L3-comment | `mindmap.spec.ts` | MMP-21–MMP-22 comments | `mindmap.comment` | `comment` | **transcribed** | |

| M-L3-shapes | `mindmap.spec.ts` | MMP-7–MMP-12 shape suffixes | `mindmap.node` | `shapes` | **transcribed** | circle, cloud, bang, hexagon |

| M-L3-icon-class | `mindmap.spec.ts` | MMP-13–MMP-16 icon/classes | `mindmap.node` | — | **planned** | May need G6/G7 line types |



**L3 promotion:** manifest **L3** stable; tree parentage in `MindmapL3AxisGateTests`.



---



## Minimum L4 slice



| Cluster id | Upstream source | Upstream test | Line type id(s) | Recognition fixture | L4 axis test | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- | --- |

| M-G12-01 | `mindmap.spec.ts` | MMP-2 hierarchical definition | `mindmap.node` | `hierarchy` | `G12_Hierarchy_Fixture_Produces_Three_Level_Tree` | **transcribed** | |

| M-G12-init | `mindmap.spec.ts` | init leading | `mindmap.init.leading` | `init-leading` | `G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues` | **transcribed** | |

| M-G12-shapes | `mindmap.spec.ts` | MMP-10–MMP-12 shape types | `mindmap.node` | `shapes` | `G12_Shapes_Fixture_Parses_Circle_Cloud_Bang_And_Hexagon` | **transcribed** | |

| M-G12-invalid | `mindmap.spec.ts` | MMP-5 multiple roots illegal | `mindmap.node` | — | `G10_Multiple_Roots_Emits_Orphan_Node_Diagnostic` | **transcribed** | G10 diagnostic |



**L4 promotion:** L3 complete + minimum slice **transcribed**.



---



## Extended G12 backlog



| Cluster id | Upstream source | Upstream test | Status | Notes |

| --- | --- | --- | --- | --- |

| M-G12-decorations | `mindmap.spec.ts` | icon + class on node | **planned** | MMP-13–16 |

| M-G12-descriptions | `mindmap.spec.ts` | node syntax in descriptions | **deferred** | MMP-17–18 |

| M-G12-blank-rows | `mindmap.spec.ts` | MMP-20–25 whitespace | **partial** | Comment fixture covers MMP-21 |


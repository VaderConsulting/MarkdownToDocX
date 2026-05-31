# Gantt G12 backlog



**Kind:** gantt  

**Pin:** Mermaid 11.14.0  

**Manifest:** `GateLevel: L4`  

**L4 tests:** `GanttL4AxisGateTests.cs`  

**Primary upstream:** `gantt/parser/gantt.spec.js`  

**Spec:** §11.1.14.7 L3/L4 exit criteria



---



## L3 completion (prerequisite for L4)



| Cluster id | Upstream source | Upstream test | Line type id(s) | Recognition fixture | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- |

| G-L3-01 | `gantt.spec.js` | section + task | `gantt.section`, `gantt.task` | `section`, `simple` | **transcribed** | Sections/tasks in contract |

| G-L3-dateFormat | `gantt.spec.js` | dateFormat | `gantt.dateFormat` | `dateFormat` | **transcribed** | |

| G-L3-excludes | `gantt.spec.js` | excludes | `gantt.excludes` | `excludes` | **transcribed** | |

| G-L3-init | `gantt.spec.js` | init leading | `gantt.init.leading` | `init-leading` | **transcribed** | |

| G-L3-title | `gantt.spec.js` | title | `gantt.title` | `simple` | **transcribed** | |

| G-L3-relative | `gantt.spec.js` | task relative to other tasks | `gantt.task` | `relative` | **transcribed** | after/until task refs |

| G-L3-acc | `gantt.spec.js` | accTitle / accDescr | `gantt.accTitle`, `gantt.accDescr` | `accessibility` | **transcribed** | G9 inline acc |

| G-L3-milestone | `gantt.spec.js` | milestone task form | `gantt.task` | `milestone` | **transcribed** | `ganttMilestone` shape when id is milestone |



**L3 promotion:** manifest **L3** stable; `GanttL3AxisGateTests` covers G4, G9, relative, milestone.



---



## Minimum L4 slice



| Cluster id | Upstream source | Upstream test | Line type id(s) | Recognition fixture | L4 axis test | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- | --- |

| G-G12-01 | `gantt.spec.js` | section + task definition | `gantt.section`, `gantt.task` | `section` | `G12_Section_Task_Fixture_Produces_Group_And_Task_Node` | **transcribed** | |

| G-G12-dateFormat | `gantt.spec.js` | dateFormat | `gantt.dateFormat` | `dateFormat` | `G12_DateFormat_Fixture_Classifies_DateFormat_And_Configures_Format` | **transcribed** | |

| G-G12-excludes | `gantt.spec.js` | excludes weekends | `gantt.excludes` | `excludes` | `G12_Excludes_Fixture_Classifies_Excludes_And_Configures_Rule` | **transcribed** | |

| G-G12-init | `gantt.spec.js` | init / theme | `gantt.init.leading` | `init-leading` | `G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues` | **transcribed** | |

| G-G12-inclusive | `gantt.spec.js` | inclusive end date | `gantt.inclusiveEndDates` | `inclusive-end` | `G12_InclusiveEnd_Fixture_Classifies_InclusiveEndDates_And_Configures_Flag` | **transcribed** | |



**L4 promotion:** L3 complete + minimum slice **transcribed**.



---



## Extended G12 backlog



| Cluster id | Upstream source | Upstream test | Status | Notes |

| --- | --- | --- | --- | --- |

| G-G12-callback | `gantt.spec.js` | click callback on task | **deferred** | Interaction; diagnostic at render |

| G-G12-todayMarker | `gantt.spec.js` | todayMarker | **planned** | May need new line type |

| G-G12-multiline-section | `gantt.spec.js` | multiline section titles | **planned** | |

| G-G12-acc-block | `gantt.spec.js` | accDescr multiline block | **planned** | Inline acc covered at L3 |


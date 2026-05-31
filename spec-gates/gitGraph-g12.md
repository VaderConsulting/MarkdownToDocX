# Git graph G12 backlog



**Kind:** gitGraph  

**Pin:** Mermaid 11.14.0  

**Manifest:** `GateLevel: L4`  

**L4 tests:** `GitGraphL4AxisGateTests.cs`  

**Primary upstream:** `git/gitGraph.spec.ts`  

**Spec:** §11.1.14.9 L3/L4 exit criteria



---



## L3 completion (prerequisite for L4)



| Cluster id | Upstream source | Upstream test | Line type id(s) | Recognition fixture | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- |

| GG-L3-01 | `gitGraph.spec.ts` | basic definition + commits | `gitGraph.commit` | `simple` | **transcribed** | Commit entities |

| GG-L3-branch | `gitGraph.spec.ts` | branch + checkout | `gitGraph.branch` | `branch-history` | **transcribed** | checkout/switch patterns |

| GG-L3-init | `gitGraph.spec.ts` | init leading | `gitGraph.init.leading` | `init-leading` | **transcribed** | |

| GG-L3-merge | `gitGraph.spec.ts` | merge with 2 parents | `gitGraph.commit` | `merge` | **transcribed** | `merge` line pattern |

| GG-L3-checkout | `gitGraph.spec.ts` | checkout / switch branch | `gitGraph.branch` | `branch-history` | **transcribed** | |

| GG-L3-commit-args | `gitGraph.spec.ts` | commit id, tag, type, msg | `gitGraph.commit` | `commit-args` | **transcribed** | |



**L3 promotion:** manifest **L3** stable; branch/commit graph in `GitGraphL3AxisGateTests`.



---



## Minimum L4 slice



| Cluster id | Upstream source | Upstream test | Line type id(s) | Recognition fixture | L4 axis test | Status | Notes |

| --- | --- | --- | --- | --- | --- | --- | --- |

| GG-G12-01 | `gitGraph.spec.ts` | basic gitGraph + commits | `gitGraph.commit` | `simple` | `G12_Simple_Fixture_Produces_Commit_Nodes_And_Edges` | **transcribed** | |

| GG-G12-branch | `gitGraph.spec.ts` | branch creation + checkout | `gitGraph.branch` | `branch-history` | `G12_BranchHistory_Fixture_Classifies_Checkout_And_Branch_Lines` | **transcribed** | |

| GG-G12-init | `gitGraph.spec.ts` | init leading | `gitGraph.init.leading` | `init-leading` | `G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues` | **transcribed** | |

| GG-G12-merge | `gitGraph.spec.ts` | merge with custom ids/tags | `gitGraph.commit` | `merge` | `G12_Merge_Fixture_Classifies_Merge_As_Commit_And_Produces_Topology` | **transcribed** | |

| GG-G12-acc | `gitGraph.spec.ts` | accessibility accDescr | `gitGraph.accTitle`, `gitGraph.accDescr` | `accessibility` | `G12_Accessibility_Fixture_Populates_Title_And_Description` | **transcribed** | |



**L4 promotion:** L3 complete + minimum slice **transcribed**.



---



## Extended G12 backlog



| Cluster id | Upstream source | Upstream test | Status | Notes |

| --- | --- | --- | --- | --- |

| GG-G12-cherry | `gitGraph.spec.ts` | cherry-pick commits | **deferred** | Advanced topology |

| GG-G12-direction | `gitGraph.spec.ts` | direction TB/BT | **planned** | May need line type |

| GG-G12-errors | `gitGraph.spec.ts` | duplicate branch, unknown checkout | **planned** | G10 diagnostics |

| GG-G12-config | `gitGraph.spec.ts` | gitGraph config directives | **N/A** | Render config |


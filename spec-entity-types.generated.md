This section documents **all entity types** the recogniser and renderer must be able to represent, classify, or diagnose. Every row includes a vendored Mermaid source reference. Diagram registration order is defined in `mermaid/packages/mermaid/src/diagram-api/diagram-orchestration.ts:84-115`.

##### 3.18.1 Recognition contract model entities

| Contract entity | Role | Required fields (summary) | Spec reference |
| --- | --- | --- | --- |
| `DiagramContract` | Root per Mermaid block | Kind, Header, Direction, Config, collections, Diagnostics | spec.md section 4 |
| `NodeContract` | Vertex / node | Id, Declarations[], Label, LabelType, Shape, ParentGroupId?, Classes, Styles, Metadata | spec.md section 4.1 |
| `EdgeContract` | Directed link | Id, endpoints, markers, stroke, minlen, label, CreationIndex, ExpansionTrace | spec.md sections 4.2, 3.8, 7.1.1 |
| `GroupContract` | Subgraph / cluster | Id, Title, parent/children, Direction?, Classes | spec.md section 4.3 |
| `ClassContract` | classDef | Class id, style list | spec.md section 3.12 |
| `StyleMutation` | style / linkStyle / class | Target ids, merged styles | spec.md section 3.12 |
| `InteractionContract` | click / href / tooltip | Target id, callback or url, target | spec.md section 3.14 |
| `AccessibilityContract` | accTitle / accDescr | Title and description text | spec.md section 3.15 |
| `Diagnostic` | unknown vs unsupported | Severity, Code, Message, SourceSpan | spec.md section 4.4 |

##### 3.18.2 Mermaid diagram kinds (detector registration)

| Diagram kind | Detector `id` | Typical source header | `%%{init}%%` key | Notes | Reference |
| --- | --- | --- | --- | --- | --- |
| Flowchart (legacy `graph`) | flowchart | `graph` | flowchart | No | `mermaid/packages/mermaid/src/diagrams/flowchart/flowDetector.ts:7-18` |
| Flowchart (`flowchart`) | flowchart-v2 | `flowchart` | flowchart | No | `mermaid/packages/mermaid/src/diagrams/flowchart/flowDetector-v2.ts:7-22` |
| Flowchart ELK | flowchart-elk | `flowchart-elk` | flowchart | Lazy-load feature | `mermaid/packages/mermaid/src/diagrams/flowchart/elk/detector.ts:7` |
| Sequence | sequence | `sequenceDiagram` | sequence | No | `mermaid/packages/mermaid/src/diagrams/sequence/sequenceDetector.ts:7` |
| Class (legacy) | class | `classDiagram` (v1) | class | No | `mermaid/packages/mermaid/src/diagrams/class/classDetector.ts:7` |
| Class (v2) | classDiagram | `classDiagram` | class | No | `mermaid/packages/mermaid/src/diagrams/class/classDetector-V2.ts:7` |
| State (legacy) | state | `stateDiagram` (v1) | state | No | `mermaid/packages/mermaid/src/diagrams/state/stateDetector.ts:7` |
| State (v2) | stateDiagram | `stateDiagram` | state | No | `mermaid/packages/mermaid/src/diagrams/state/stateDetector-V2.ts:7` |
| Entity Relationship | er | `erDiagram` | er | No | `mermaid/packages/mermaid/src/diagrams/er/erDetector.ts:7` |
| Gantt | gantt | `gantt` | gantt | No | `mermaid/packages/mermaid/src/diagrams/gantt/ganttDetector.ts:7` |
| User Journey | journey | `journey` | journey | No | `mermaid/packages/mermaid/src/diagrams/user-journey/journeyDetector.ts:7` |
| Timeline | timeline | `timeline` | timeline | No | `mermaid/packages/mermaid/src/diagrams/timeline/detector.ts:7` |
| Git Graph | gitGraph | `gitGraph` | gitGraph | No | `mermaid/packages/mermaid/src/diagrams/git/gitGraphDetector.ts:4` |
| Pie | pie | `pie` | pie | No | `mermaid/packages/mermaid/src/diagrams/pie/pieDetector.ts:7` |
| Quadrant Chart | quadrantChart | `quadrantChart` | quadrantChart | No | `mermaid/packages/mermaid/src/diagrams/quadrant-chart/quadrantDetector.ts:7` |
| XY Chart | xychart | `xychart-beta` / xychart | xyChart | No | `mermaid/packages/mermaid/src/diagrams/xychart/xychartDetector.ts:7` |
| Requirement | requirement | `requirementDiagram` | requirement | No | `mermaid/packages/mermaid/src/diagrams/requirement/requirementDetector.ts:7` |
| Architecture | architecture | `architecture-beta` | architecture | Lazy-load feature | `mermaid/packages/mermaid/src/diagrams/architecture/architectureDetector.ts:7` |
| Mindmap | mindmap | `mindmap` | mindmap | Lazy-load feature | `mermaid/packages/mermaid/src/diagrams/mindmap/detector.ts:6-9` |
| Kanban | kanban | `kanban` | kanban | No | `mermaid/packages/mermaid/src/diagrams/kanban/detector.ts:6` |
| Sankey | sankey | `sankey-beta` / sankey | sankey | No | `mermaid/packages/mermaid/src/diagrams/sankey/sankeyDetector.ts:3` |
| Packet | packet | `packet-beta` / packet | packet | No | `mermaid/packages/mermaid/src/diagrams/packet/detector.ts:7` |
| Block | block | `block-beta` / block | block | No | `mermaid/packages/mermaid/src/diagrams/block/blockDetector.ts:3` |
| Event Modeling | eventmodeling | `eventmodeling` | eventmodeling | No | `mermaid/packages/mermaid/src/diagrams/eventmodeling/detector.ts:4` |
| Tree View | treeView | `treeview` / treeView | treeView | No | `mermaid/packages/mermaid/src/diagrams/treeView/detector.ts:4` |
| Radar | radar | `radar-beta` / radar | radar | No | `mermaid/packages/mermaid/src/diagrams/radar/detector.ts:7` |
| Ishikawa | ishikawa | `ishikawa` | ishikawa | No | `mermaid/packages/mermaid/src/diagrams/ishikawa/ishikawaDetector.ts:7` |
| Treemap | treemap | `treemap` | (treemap in defaultConfig.ts only) | No | `mermaid/packages/mermaid/src/diagrams/treemap/detector.ts:7` |
| Venn | venn | `venn` | venn | No | `mermaid/packages/mermaid/src/diagrams/venn/vennDetector.ts:7` |
| Wardley | wardley-beta | `wardley-beta` | wardley-beta | Yes (beta id) | `mermaid/packages/mermaid/src/diagrams/wardley/wardleyDetector.ts:7` |
| C4 | c4 | `C4Context` / `C4Container` / etc. | c4 | No | `mermaid/packages/mermaid/src/diagrams/c4/c4Detector.ts:7` |
| Info | info | `info` | (none) | No | `mermaid/packages/mermaid/src/diagrams/info/infoDetector.ts:7` |
| YAML front-matter trap | `---` | starts with `---` | (none) | No | `mermaid/packages/mermaid/src/diagram-api/diagram-orchestration.ts:49-77` |

##### 3.18.3 Flowchart vertex shape entities (`@{ shape: ... }` and legacy brackets)

Canonical shape ids are defined in `shapesDefs` and exposed through `shapes` / `isValidShape` (`shapes.ts:92-542`).

| Semantic name | Canonical `shortName` | Legacy bracket (if any) | Public aliases (subset) | Reference |
| --- | --- | --- | --- | --- |
| Process | `rect` | A[text] | `'proc', 'process', 'rectangle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:94` |
| Event | `rounded` | A(text) | `'event'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:103` |
| Terminal Point | `stadium` | A([text]) | `'terminal', 'pill'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:112` |
| Subprocess | `fr-rect` | A[[text]] | `'subprocess', 'subproc', 'framed-rectangle', 'subroutine'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:120` |
| Database | `cyl` | A[(text)] | `'db', 'database', 'cylinder'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:128` |
| Data Store | `datastore` | (metadata only; or A@{ shape: ... }) | `'data-store'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:136` |
| Start | `circle` | A((text)) | `'circ'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:144` |
| Bang | `bang` | (metadata only; or A@{ shape: ... }) | `'bang'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:152` |
| Cloud | `cloud` | (metadata only; or A@{ shape: ... }) | `'cloud'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:160` |
| Decision | `diam` | A{text} | `'decision', 'diamond', 'question'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:168` |
| Prepare Conditional | `hex` | A{{text}} | `'hexagon', 'prepare'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:176` |
| Data Input/Output | `lean-r` | (metadata only; or A@{ shape: ... }) | `'lean-right', 'in-out'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:184` |
| Data Input/Output | `lean-l` | (metadata only; or A@{ shape: ... }) | `'lean-left', 'out-in'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:193` |
| Priority Action | `trap-b` | (metadata only; or A@{ shape: ... }) | `'priority', 'trapezoid-bottom', 'trapezoid'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:202` |
| Manual Operation | `trap-t` | (metadata only; or A@{ shape: ... }) | `'manual', 'trapezoid-top', 'inv-trapezoid'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:210` |
| Stop | `dbl-circ` | A(((text))) | `'double-circle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:219` |
| Text Block | `text` | (metadata only; or A@{ shape: ... }) | `(none listed)` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:228` |
| Card | `notch-rect` | (metadata only; or A@{ shape: ... }) | `'card', 'notched-rectangle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:235` |
| Lined/Shaded Process | `lin-rect` | (metadata only; or A@{ shape: ... }) | `'lined-rectangle', 'lined-process', 'lin-proc', 'shaded-process'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:243` |
| Start | `sm-circ` | (metadata only; or A@{ shape: ... }) | `'start', 'small-circle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:251` |
| Stop | `fr-circ` | (metadata only; or A@{ shape: ... }) | `'stop', 'framed-circle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:260` |
| Fork/Join | `fork` | (metadata only; or A@{ shape: ... }) | `'join'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:269` |
| Collate | `hourglass` | (metadata only; or A@{ shape: ... }) | `'hourglass', 'collate'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:278` |
| Comment | `brace` | (metadata only; or A@{ shape: ... }) | `'comment', 'brace-l'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:286` |
| Comment Right | `brace-r` | (metadata only; or A@{ shape: ... }) | `(none listed)` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:294` |
| Comment with braces on both sides | `braces` | (metadata only; or A@{ shape: ... }) | `(none listed)` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:301` |
| Com Link | `bolt` | (metadata only; or A@{ shape: ... }) | `'com-link', 'lightning-bolt'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:308` |
| Document | `doc` | (metadata only; or A@{ shape: ... }) | `'doc', 'document'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:316` |
| Delay | `delay` | (metadata only; or A@{ shape: ... }) | `'half-rounded-rectangle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:324` |
| Direct Access Storage | `h-cyl` | (metadata only; or A@{ shape: ... }) | `'das', 'horizontal-cylinder'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:332` |
| Disk Storage | `lin-cyl` | (metadata only; or A@{ shape: ... }) | `'disk', 'lined-cylinder'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:340` |
| Display | `curv-trap` | (metadata only; or A@{ shape: ... }) | `'curved-trapezoid', 'display'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:348` |
| Divided Process | `div-rect` | (metadata only; or A@{ shape: ... }) | `'div-proc', 'divided-rectangle', 'divided-process'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:356` |
| Extract | `tri` | (metadata only; or A@{ shape: ... }) | `'extract', 'triangle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:364` |
| Internal Storage | `win-pane` | (metadata only; or A@{ shape: ... }) | `'internal-storage', 'window-pane'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:372` |
| Junction | `f-circ` | (metadata only; or A@{ shape: ... }) | `'junction', 'filled-circle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:380` |
| Loop Limit | `notch-pent` | (metadata only; or A@{ shape: ... }) | `'loop-limit', 'notched-pentagon'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:388` |
| Manual File | `flip-tri` | (metadata only; or A@{ shape: ... }) | `'manual-file', 'flipped-triangle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:396` |
| Manual Input | `sl-rect` | (metadata only; or A@{ shape: ... }) | `'manual-input', 'sloped-rectangle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:404` |
| Multi-Document | `docs` | (metadata only; or A@{ shape: ... }) | `'documents', 'st-doc', 'stacked-document'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:412` |
| Multi-Process | `st-rect` | (metadata only; or A@{ shape: ... }) | `'procs', 'processes', 'stacked-rectangle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:420` |
| Stored Data | `bow-rect` | (metadata only; or A@{ shape: ... }) | `'stored-data', 'bow-tie-rectangle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:428` |
| Summary | `cross-circ` | (metadata only; or A@{ shape: ... }) | `'summary', 'crossed-circle'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:436` |
| Tagged Document | `tag-doc` | (metadata only; or A@{ shape: ... }) | `'tag-doc', 'tagged-document'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:444` |
| Tagged Process | `tag-rect` | (metadata only; or A@{ shape: ... }) | `'tagged-rectangle', 'tag-proc', 'tagged-process'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:452` |
| Paper Tape | `flag` | (metadata only; or A@{ shape: ... }) | `'paper-tape'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:460` |
| Odd | `odd` | A>text] | `(none listed)` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:468` |
| Lined Document | `lin-doc` | (metadata only; or A@{ shape: ... }) | `'lined-document'` | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:476` |

Additional renderer-only shape handlers (not in `shapesDefs` but registered in `undocumentedShapes`):

| Shape id | Used by diagram | Reference |
| --- | --- | --- |
| state, choice, note | state diagram | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:487-491` |
| icon, iconSquare, iconCircle, iconRounded, imageSquare, anchor | flowchart metadata | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:497-503` |
| kanbanItem | kanban | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:505-506` |
| mindmapCircle, defaultMindmapNode | mindmap | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:508-510` |
| classBox | class | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:511-512` |
| erBox | er | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:514-515` |
| requirementBox | requirement | `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:517-518` |

##### 3.18.4 Flowchart edge / line entity types

See sections 3.8, 3.17.38, and 7.1.1. Normalised edge fields:

| Field | Allowed values | Reference |
| --- | --- | --- |
| `startArrow` / `endArrow` | none, point, circle, cross | spec.md 3.8; `mermaid/packages/mermaid/src/diagrams/flowchart/flowDb.ts:998-1011` |
| `stroke` | normal, dotted, thick, invisible | spec.md 3.8; `mermaid/packages/mermaid/src/diagrams/flowchart/flowDb.ts:832-901` |
| `minlen` | integer 1..10 (from token length) | `mermaid/packages/mermaid/src/diagrams/flowchart/flowDb.ts:304` |
| `labelType` | text, string, markdown | spec.md 3.4 |

##### 3.18.5 Per-diagram domain entity types

**C4** (`mermaid/packages/mermaid/src/diagrams/c4/parser/c4Diagram.jison:277-309`, `mermaid/packages/mermaid/src/diagrams/c4/c4Db.js`)

| Category | `typeC4Shape` / rel tokens (complete set) | Reference |
| --- | --- | --- |
| C4 element shapes | person, external_person, system, system_db, system_queue, external_system, external_system_db, external_system_queue, container, container_db, container_queue, external_container, external_container_db, external_container_queue, component, component_db, component_queue, external_component, external_component_db, external_component_queue | `mermaid/packages/mermaid/src/diagrams/c4/parser/c4Diagram.jison:277-309` |
| C4 boundaries | ENTERPRISE boundary, SYSTEM boundary, BOUNDARY, CONTAINER boundary | `mermaid/packages/mermaid/src/diagrams/c4/parser/c4Diagram.jison:277-309` |
| C4 relationships | rel, birel, rel_u, rel_d, rel_l, rel_r, rel_b | `mermaid/packages/mermaid/src/diagrams/c4/parser/c4Diagram.jison:277-309` |

**Block** (`mermaid/packages/mermaid/src/diagrams/block/blockTypes.ts:3-30`)

| `BlockType` union members | Reference |
| --- | --- |
| na, column-setting, edge, round, block_arrow, space, square, diamond, hexagon, odd, lean_right, lean_left, trapezoid, inv_trapezoid, rect_left_inv_arrow, odd_right, circle, ellipse, stadium, subroutine, cylinder, group, doublecircle, classDef, applyClass, applyStyles, composite | `mermaid/packages/mermaid/src/diagrams/block/blockTypes.ts:3-30` |

**Architecture** (`mermaid/packages/mermaid/src/diagrams/architecture/architectureTypes.ts`)

| Entity | `type` field | Reference |
| --- | --- | --- |
| Service node | `service` | `mermaid/packages/mermaid/src/diagrams/architecture/architectureTypes.ts:193-203` |
| Junction node | `junction` | `mermaid/packages/mermaid/src/diagrams/architecture/architectureTypes.ts:205-212` |
| Group | (group record, no type enum) | `mermaid/packages/mermaid/src/diagrams/architecture/architectureTypes.ts:226-231` |
| Edge | direction pair L/R/T/B | `mermaid/packages/mermaid/src/diagrams/architecture/architectureTypes.ts:233-243` |

**Requirement** (`mermaid/packages/mermaid/src/diagrams/requirement/types.ts:1-45`)

| Entity | Values | Reference |
| --- | --- | --- |
| `Requirement` | RequirementType: Requirement, Functional Requirement, Interface Requirement, Performance Requirement, Physical Requirement, Design Constraint | `mermaid/packages/mermaid/src/diagrams/requirement/types.ts:1-22` |
| `Element` | name, type (free text), docRef | `mermaid/packages/mermaid/src/diagrams/requirement/types.ts:39-45` |
| `Relation` | contains, copies, derives, satisfies, verifies, refines, traces | `mermaid/packages/mermaid/src/diagrams/requirement/types.ts:24-37` |
| `RiskLevel` | Low, Medium, High | `mermaid/packages/mermaid/src/diagrams/requirement/types.ts:9` |
| `VerifyType` | Analysis, Demonstration, Inspection, Test | `mermaid/packages/mermaid/src/diagrams/requirement/types.ts:11` |

**Sequence** (`mermaid/packages/mermaid/src/diagrams/sequence/sequenceDb.ts`)

| Entity | Reference |
| --- | --- |
| Actor / participant | `mermaid/packages/mermaid/src/diagrams/sequence/sequenceDb.ts` (`addActor`) |
| Message | `mermaid/packages/mermaid/src/diagrams/sequence/sequenceDb.ts` message list |
| Note, loop, alt, opt, par, rect, activate/deactivate | `mermaid/packages/mermaid/src/diagrams/sequence/parser/sequenceDiagram.jison` |

**Other diagram kinds (compact index):**

| Diagram | Domain entities (recogniser must classify) | Reference |
| --- | --- | --- |
| Sequence | Actor / participant, Message, Note, loop, alt, opt, par, rect, activate/deactivate | `mermaid/packages/mermaid/src/diagrams/sequence/sequenceDb.ts`; `mermaid/packages/mermaid/src/diagrams/sequence/parser/sequenceDiagram.jison` |
| ER | Entity, Attribute, Relationship | `mermaid/packages/mermaid/src/diagrams/er/erDb.ts`; `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:514-515` |
| State | State, composite, choice, fork, join, note | `mermaid/packages/mermaid/src/diagrams/state/stateDb.ts`; `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:487-491` |
| Class | Class, Interface, Namespace, relationship | `mermaid/packages/mermaid/src/diagrams/class/classDb.ts`; `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:511-512` |
| Gantt | Section, Task, Milestone, excludes | `mermaid/packages/mermaid/src/diagrams/gantt/ganttDb.js` |
| Venn | Set region, intersection item (id, sets[], label) | `mermaid/packages/mermaid/src/diagrams/venn/parser`; `mermaid/packages/mermaid/src/diagrams/venn/venn.spec.ts` |
| Sankey | Node, Link (gradient/source/target colour) | `mermaid/packages/mermaid/src/diagrams/sankey/sankeyDb.ts` |
| GitGraph | Commit, Branch, Tag, CherryPick, Merge | `mermaid/packages/mermaid/src/diagrams/git/gitGraphAst.ts` |
| Event modeling | Frame kinds: cmd/command, evt/event, pcr/processor, rmo/readmodel; swimlanes Command/Read Model and Events | `mermaid/packages/mermaid/src/diagrams/eventmodeling/db.ts:96-120`; `mermaid/packages/mermaid/src/themes/theme-default.js:418-425` |
| Kanban | Section, Ticket | `mermaid/packages/mermaid/src/diagrams/kanban/kanbanDb.ts`; `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:505-506` |
| Mindmap | Node (hierarchy, mindmapCircle shape) | `mermaid/packages/mermaid/src/diagrams/mindmap/mindmapDb.ts:239-269`; `mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts:508-510` |
| Pie | Slice (label, value) | `mermaid/packages/mermaid/src/diagrams/pie/pieDb.ts` |
| Packet | Row, Bit field | `mermaid/packages/mermaid/src/diagrams/packet/packetDb.ts` |
| Radar | Axis, Curve, Legend entry | `mermaid/packages/mermaid/src/diagrams/radar/radarDb.ts` |
| TreeView | Tree node | `mermaid/packages/mermaid/src/diagrams/treeView/treeViewDb.ts` |
| QuadrantChart | Quadrant, Point, Axis labels | `mermaid/packages/mermaid/src/diagrams/quadrant-chart/quadrantDb.ts` |
| XYChart | Series, Axis, Bar/Line plot | `mermaid/packages/mermaid/src/diagrams/xychart/chartBuilder/` |
| Timeline | Section, Event | `mermaid/packages/mermaid/src/diagrams/timeline/timelineDb.ts` |
| Journey | Section, Task | `mermaid/packages/mermaid/src/diagrams/user-journey/journeyDb.ts` |
| Ishikawa | Category, Cause | `mermaid/packages/mermaid/src/diagrams/ishikawa/ishikawaDb.ts` |
| Wardley (beta) | Node, Axis, Anchor | `mermaid/packages/mermaid/src/diagrams/wardley/wardleyDb.ts` |
| Treemap | Hierarchy node (width/height/value) | `mermaid/packages/mermaid/src/diagrams/treemap/`; `mermaid/packages/mermaid/src/defaultConfig.ts:283-294` |
| Info | Info / showInfo box | `mermaid/packages/mermaid/src/diagrams/info/` |

##### 3.18.6 Naiad `DiagramType` enum (native renderer subset)

Naiad currently exposes a smaller native enum than full Mermaid (`extern/Naiad/src/Naiad/DiagramType.cs`). Unlisted Mermaid kinds must be recognised in the contract and handled via diagnostics or official fallback until native support exists.

| `DiagramType` | Mermaid detector id | Reference |
| --- | --- | --- |
| Flowchart | flowchart / flowchart-v2 | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Sequence | sequence | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Class | class / classDiagram | `extern/Naiad/src/Naiad/DiagramType.cs` |
| State | state / stateDiagram | `extern/Naiad/src/Naiad/DiagramType.cs` |
| EntityRelationship | er | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Gantt | gantt | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Pie | pie | `extern/Naiad/src/Naiad/DiagramType.cs` |
| GitGraph | gitGraph | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Mindmap | mindmap | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Timeline | timeline | `extern/Naiad/src/Naiad/DiagramType.cs` |
| C4Context | C4Context | `extern/Naiad/src/Naiad/DiagramType.cs` |
| C4Container | C4Container | `extern/Naiad/src/Naiad/DiagramType.cs` |
| C4Component | C4Component | `extern/Naiad/src/Naiad/DiagramType.cs` |
| C4Deployment | C4Deployment | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Block | block | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Kanban | kanban | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Quadrant | quadrantChart | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Requirement | requirement | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Sankey | sankey | `extern/Naiad/src/Naiad/DiagramType.cs` |
| UserJourney | journey | `extern/Naiad/src/Naiad/DiagramType.cs` |
| XYChart | xychart | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Architecture | architecture | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Packet | packet | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Radar | radar | `extern/Naiad/src/Naiad/DiagramType.cs` |
| Treemap | treemap | `extern/Naiad/src/Naiad/DiagramType.cs` |
| ZenUML | (not in diagram-orchestration.ts 11.14) | `extern/Naiad/src/Naiad/DiagramType.cs` |


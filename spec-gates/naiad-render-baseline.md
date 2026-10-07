# Naiad native render baseline (§12 — legacy comparison only)

**Purpose:** inventory of what **legacy Naiad** (`extern/Naiad`) can render today. Use this table **only** when comparing legacy output against native render from `DiagramContract` — not as a product roadmap or parse authority.

Source: `extern/Naiad/src/test-renders/*.md` and `extern/Naiad/src/Naiad/DiagramType.cs`.

| Diagram kind | Naiad `DiagramType` | Sample render doc | Recognition L1 | Recognition fixtures | Regression catalogue (§9.1) |
| --- | --- | --- | --- | --- | --- |
| Flowchart | Flowchart | `Flowchart.md` | Legacy compare | `FlowchartRecognition/` | [`flow.md`](../flow.md) |
| Sequence | Sequence | `Sequence.md` | Legacy compare | `SequenceRecognition/` | [`sequence.md`](../sequence.md) |
| Class | Class | `Class.md` | Legacy compare | `ClassRecognition/` | [`class.md`](../class.md) |
| State | State | `State.md` | Legacy compare | `StateRecognition/` | [`state.md`](../state.md) |
| ER | EntityRelationship | `EntityRelationship.md` | Legacy compare | `ErRecognition/` | [`entityrelationship.md`](../entityrelationship.md) |
| Gantt | Gantt | `Gantt.md` | Legacy compare | `GanttRecognition/` | — |
| Pie | Pie | `Pie.md` | Legacy compare | `PieRecognition/` | — |
| Git graph | GitGraph | `GitGraph.md` | Legacy compare | `GitGraphRecognition/` | — |
| Mindmap | Mindmap | `Mindmap.md` | Legacy compare | `MindmapRecognition/` | — |
| Timeline | Timeline | `Timeline.md` | Legacy compare | `TimelineRecognition/` | — |
| User journey | UserJourney | `UserJourney.md` | Legacy compare | `JourneyRecognition/` | — |
| Quadrant | Quadrant | `Quadrant.md` | Legacy compare | `QuadrantRecognition/` | — |
| XY chart | XYChart | `XYChart.md` | Legacy compare | `XychartRecognition/` | — |
| Requirement | Requirement | `Requirement.md` | Legacy compare | `RequirementRecognition/` | — |
| Architecture | Architecture | `Architecture.md` | Legacy compare | `ArchitectureRecognition/` | — |
| Kanban | Kanban | `Kanban.md` | Legacy compare | `KanbanRecognition/` | — |
| Sankey | Sankey | `Sankey.md` | Legacy compare | `SankeyRecognition/` | — |
| Packet | Packet | `Packet.md` | Legacy compare | `PacketRecognition/` | — |
| Radar | Radar | `Radar.md` | Legacy compare | `RadarRecognition/` | — |
| Treemap | Treemap | `Treemap.md` | Legacy compare | `TreemapRecognition/` | — |
| C4 | C4 | `C4.md` | Legacy compare | `C4Recognition/` | — |
| Block | Block | `Block.md` | Legacy compare | `BlockRecognition/` | — |

Kinds without a legacy Naiad path (recognition still required at L1): see §3.18.2 Mermaid-only kinds (`eventmodeling`, `treeview`, `ishikawa`, `venn`, `wardley-beta`, `info`).

**Policy:** do not extend Naiad parsers or renderers for programme gaps. Native render is built in `src/` and `tests/` (see §11.1.8.2)  from `DiagramContract`.

Update this table when adding `*Recognition/` fixture folders or regression catalogues — not when planning product features in Naiad.

# Current status

MarkdownToDocX is a .NET 10 Markdown-to-DOCX converter. The [README](../README.md)
is the product overview and the source of truth for supported output paths,
build commands, and test projects. The normative recognition and rendering
contracts are in [spec.md](spec.md).

## Recognition gates

The parser manifests and recognition checklist tests report **L4** for these
nine Mermaid kinds:

| Kind | Source header |
| --- | --- |
| Flowchart | `flowchart`, `graph` |
| Sequence | `sequenceDiagram` |
| Class | `classDiagram` |
| State | `stateDiagram` |
| Entity relationship | `erDiagram` |
| Pie | `pie` |
| Gantt | `gantt` |
| Mindmap | `mindmap` |
| Git graph | `gitGraph` |

Other recognized kinds have shallower recognition coverage. Recognition level
does not imply native rendering support; see the README's renderer table and
the gate definitions in [spec.md](spec.md), sections 11.1.13–11.1.14.

## Active engineering work

- [Incremental diagram layout plan](incremental-diagram-layout-plan.md) records
  the current implementation status, remaining work, and acceptance criteria
  for retained diagram edits.
- [View layers roadmap](view-layers-roadmap.md) tracks the separate proposal
  for filtering diagram content before layout.
- [Layout rulebook](Instructions/LAYOUT-RULEBOOK.md) defines the authoritative
  geometry, routing, and legality rules.
- [Benchmark instructions](bench/README.md) document how to capture and compare
  catalogue output and timing.

This page is a concise status index, not a dated activity log. Historical
experiment reports and machine-specific generated outputs are intentionally
not maintained here; use the repository history for past decisions.

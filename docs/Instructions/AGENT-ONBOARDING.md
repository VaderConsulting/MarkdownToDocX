# Contributor onboarding

MarkdownToDocX is a .NET Markdown-to-DOCX converter with native diagram
recognition and rendering. Start with the [README](../../README.md) for
supported features, build commands, and test projects.

## Project references

- [`../spec.md`](../spec.md) defines recognition and rendering contracts.
- [`LAYOUT-RULEBOOK.md`](LAYOUT-RULEBOOK.md) is authoritative for layout and
  routing rules; its illustrations are in [`pictures/`](pictures/).
- [`../currentstatus.md`](../currentstatus.md) summarizes current recognition
  gates and active engineering work.
- [`../incremental-diagram-layout-plan.md`](../incremental-diagram-layout-plan.md)
  records the status and acceptance criteria for retained, incremental edits.
- The root catalogues `flow.md`, `enterprise.md`, and `EIP.md` provide source
  examples and regression scenarios.
- [`CLOUD-AGENT-WORKING-PRACTICES.md`](CLOUD-AGENT-WORKING-PRACTICES.md)
  documents the repository's coding-agent workflow.

Keep changes general: engine behavior should follow structural and geometric
properties, not fixture names, diagram labels, or other scenario-specific data.

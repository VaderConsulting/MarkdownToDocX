# View layers (roadmap)

Visio-style **document layers**: hide or show slices of the model before layout so render cost drops with visible node and edge count. Not started in code; see `docs/currentstatus.md` section **View layers and viewpoints**.

## Order of work

| Step | Deliverable | Status |
|------|-------------|--------|
| VL-1 | View model in `docs/spec.md`: repository, view, derived diagram | Not started |
| VL-2 | Interaction closure rules per layer | Not started |
| VL-3 | Cross-layer boundary behaviour | Not started |
| VL-4 | Syntax + `DiagramContract` layer membership | Not started |
| VL-5 | **Filter before layout** (`EnterpriseLayoutEngine` / grid never sees hidden items) | Not started |
| VL-6 | LiveEditor layer list UI + export notes | Not started |
| VL-7 | Layout cache keys include visible layer set | Not started |
| VL-8 | Bench fixtures with layers on vs off | Not started |

## Azure import note

Resource Graph import can mimic a coarse "layer" today by **category checkboxes** in the import dialog (fewer elements and relationships). View layers will generalise that for any enterprise source.

## Engine interaction

View layers complement Phase 5 / 4.12 engine work: when all layers are on, full-graph nested completion still applies (`docs/currentstatus.md` DR-08).

# Naiad edge/line parity fixes (handover runbook)

## Purpose

This file is a **step-by-step implementation runbook** for bringing Naiad’s edge/line rendering into **visual parity with Mermaid.js** (the reference output from `mermaid.live` / VS Code Mermaid preview) for DOCX-in-Word output.

It is written so that **another AI can take over** and execute the fixes without needing prior chat context.

## Hard rules (must follow)

- **Spec-first (mandatory)**: when you confirm a discrepancy by tracing Mermaid upstream source, **update `Mermaid_Specification.md` first**, then implement the Naiad fix.
- **No browser rendering in the DOCX pipeline**: Naiad is the renderer; do not introduce a headless Mermaid.js rendering dependency.
- **Verification is Word-first**: always validate by regenerating `test.docx` and opening in Word.
- **Do not use Python/Node**: use `dotnet` + PowerShell only.

## Glossary

- **Upstream Mermaid source**: local checkout under `.\mermaid\`.
- **Naiad**: vendored C# renderer under `extern/Naiad/`.
- **Markers**: SVG `<marker>` definitions + `marker-start` / `marker-end` on `<path>` / `<line>`.
- **Routed edge points**: layout-produced polyline points (for Dagre) that Mermaid converts to an SVG path (often with `curveBasis`).

## Regen / verification workflow (repeat every change-set)

1. Build (minimum):
   - `dotnet build .\extern\Naiad\src\Naiad\Naiad.csproj`
   - `dotnet build .\src\MDConverter\MDConverter.csproj`
2. Regen:
   - `dotnet run --project .\src\md2docx\md2docx.csproj -- .\test.md .\test.docx`
3. Open `.\test.docx` in Word and compare against Mermaid reference output (VS Code preview or `mermaid.live`).
4. If an SVG-level comparison is needed, extract:
   - `Copy-Item .\test.docx .\test.zip -Force; Expand-Archive .\test.zip -DestinationPath .\_docx_extract -Force`
   - Inspect `.\_docx_extract\word\media\image*.svg`

## Primary references (use these files, do not guess)

- Mermaid marker definitions:
  - `mermaid\packages\mermaid\src\rendering-util\rendering-elements\markers.js`
- Mermaid marker offsets / line shortening rules:
  - `mermaid\packages\mermaid\src\utils\lineWithOffset.ts`
- Mermaid class edges:
  - `mermaid\packages\mermaid\src\diagrams\class\svgDraw.js`
- Mermaid state edges:
  - `mermaid\packages\mermaid\src\diagrams\state\stateRenderer.js`
- Mermaid sequence edges:
  - `mermaid\packages\mermaid\src\diagrams\sequence\svgDraw.js`
- Mermaid C4 relationships:
  - `mermaid\packages\mermaid\src\diagrams\c4\svgDraw.js`
- Naiad SVG building primitives:
  - `extern\Naiad\src\Naiad\Rendering\SvgBuilder.cs`
  - `extern\Naiad\src\Naiad\Rendering\SvgLine.cs`
  - `extern\Naiad\src\Naiad\Rendering\SvgPath.cs`
  - `extern\Naiad\src\Naiad\Rendering\SvgMarker.cs`

## Work order (do these in this order)

The highest leverage fix is to **standardise markers and edge path rendering** so all diagram families can share the same behaviour.

### Phase 0 — Create a single “marker parity” implementation surface

#### 0A. Add marker support where it is missing

**Goal:** Naiad must be able to attach markers to the same primitives Mermaid uses.

- **If `SvgLine` cannot emit `marker-start` / `marker-end`**:
  - Add `MarkerStart` and `MarkerEnd` properties to `SvgLine` (plus serialisation to SVG attributes).
  - Ensure it round-trips into output SVG as:
    - `marker-start="url(#...)"` and/or
    - `marker-end="url(#...)"`.

**Acceptance criteria:**
- A minimal test SVG produced by Naiad includes a `<line ... marker-end="url(#arrowhead)">` and Word renders the marker correctly.

#### 0B. Ensure `SvgBuilder` exposes Mermaid-equivalent marker definitions

**Goal:** marker geometry comes from Mermaid upstream, not ad-hoc polygons.

- Confirm Naiad’s current flowchart marker methods already match upstream:
  - `SvgBuilder.AddMermaidArrowMarker(...)`
  - `SvgBuilder.AddMermaidCircleMarker(...)`
  - `SvgBuilder.AddMermaidCrossMarker(...)`
- Add any missing marker definitions required by the diagram families below by following `markers.js`:
  - Class: `extension`, `composition`, `aggregation`, `dependency`, `lollipop` (+ start/end forms)
  - Requirement: `requirement_arrow`, `requirement_contains`
  - C4: `arrowhead` and `arrowend` equivalents (see Mermaid C4 code)

**Instruction:** when copying marker geometry from upstream:
- Match **all** of: `viewBox`, `refX`, `refY`, `markerWidth`, `markerHeight`, `markerUnits`, and the exact path/polygon/circle geometry.

**Acceptance criteria:**
- Marker IDs used by renderers exist in `<defs>` in the final SVG.
- Marker attachment does not leave visible gaps or overlaps inconsistent with Mermaid defaults.

### Phase 1 — Fix each diagram family (validated issues)

Each item below is validated against Mermaid upstream and recorded in `Naiad_Mermaid_Spec_Gap_Checklist.md`.

#### 1. Sequence diagram message edges (SEQ-EDGE-001, SEQ-EDGE-002)

**Mermaid reference:**
- `mermaid\packages\mermaid\src\diagrams\sequence\svgDraw.js`
  - Uses markers (`insertArrowHead`, `insertArrowCrossHead`, open arrow variants, etc.).

**Naiad current code:**
- `extern\Naiad\src\Naiad\Diagrams\Sequence\SequenceRenderer.cs`
  - `DrawMessage(...)` uses `AddLine(...)` then `DrawArrowhead(...)` for non-self messages.
  - Self-messages use `AddPath(...)` and may already support markers.

**Required changes:**
- Stop drawing arrowheads manually for message endpoints.
- Attach markers to the rendered message element (line/path), matching Mermaid:
  - Solid vs dotted line styles
  - Cross end markers
  - Open arrow end behaviour
  - Async variants (confirm exact marker type per Mermaid output)

**Implementation approach:**
- Prefer one unified representation for all messages:
  - Either always use `SvgPath` (even for straight messages), or
  - Make `SvgLine` marker-capable and keep lines as lines.

**Acceptance criteria (visual):**
- In Word, message arrowheads are the same shape/size/placement as Mermaid preview.
- Dotted messages use the same dash pattern behaviour as Mermaid (confirm upstream CSS/classes).

#### 2. Class diagram edges (CLASS-EDGE-001, CLASS-EDGE-002, CLASS-EDGE-003)

**Mermaid reference:**
- `mermaid\packages\mermaid\src\diagrams\class\svgDraw.js`
  - Builds edge path from `path.points` using `d3.line().curve(curveBasis)`.
  - Attaches start/end markers based on relationship type.
  - Applies dashed/dotted via `relation.relation.lineType` (`1` dashed, `10` dotted).
- Marker geometry reference:
  - `mermaid\packages\mermaid\src\rendering-util\rendering-elements\markers.js`

**Naiad current code:**
- `extern\Naiad\src\Naiad\Diagrams\Class\ClassRenderer.cs`
  - `RenderRelationship(...)` draws a single straight line and then draws polygons for markers.

**Required changes:**
- Use routed edge points from layout output (not a straight segment).
- Build a Mermaid-equivalent path:
  - Implement `curveBasis` path generation (if not already shared from ER work, reuse that implementation).
- Replace manual polygons with marker attachments using Mermaid marker geometry.
- Implement line types:
  - dashed vs dotted (do not conflate these into one dash pattern).

**Acceptance criteria (visual):**
- Edge curvature matches Mermaid (including multi-point routes).
- Composition/aggregation diamonds and inheritance triangles match Mermaid geometry and sit on the line correctly.
- Dashed vs dotted look distinct and match Mermaid defaults.

#### 3. State diagram transitions (STATE-EDGE-001, STATE-EDGE-002)

**Mermaid reference:**
- `mermaid\packages\mermaid\src\diagrams\state\stateRenderer.js`
  - Inserts `dependencyEnd` marker and draws edges from Dagre layout output.

**Naiad current code:**
- `extern\Naiad\src\Naiad\Diagrams\State\StateRenderer.cs`
  - Uses custom routing heuristics (`RenderCurvedTransition`, `RenderRoutedTransition`) and manual arrowheads (`DrawArrowhead`).

**Required changes:**
- Use marker-based arrowheads matching Mermaid’s marker geometry.
- Prefer layout-provided edge routing:
  - If Naiad’s layout engine does not supply enough edge points, enhance layout generation rather than layering renderer-side routing heuristics.

**Acceptance criteria (visual):**
- Forward/back/bidirectional transitions match Mermaid’s path shapes more closely than the current bespoke routing.
- Arrowhead placement and orientation matches Mermaid.

#### 4. C4 relationships (C4-EDGE-001, C4-EDGE-002)

**Mermaid reference:**
- `mermaid\packages\mermaid\src\diagrams\c4\svgDraw.js`
  - Uses `marker-end` (and sometimes `marker-start`) for relationships.
  - Does not force dashed relationships by default.

**Naiad current code:**
- `extern\Naiad\src\Naiad\Diagrams\C4\C4Renderer.cs`
  - `DrawRelationship(...)` forces `strokeDasharray: "5,5"` and draws arrowhead polygons manually.

**Required changes:**
- Remove forced dashed style unless Mermaid indicates it for a specific relationship kind.
- Use marker geometry + marker attachment (including start marker for bi-directional relationships if model supports it).

**Acceptance criteria (visual):**
- Relationship lines are solid by default (unless Mermaid output shows otherwise for a given relationship).
- Arrowhead geometry matches Mermaid.

#### 5. Requirement diagram relations (REQ-EDGE-001)

**Mermaid reference:**
- Marker geometry:
  - `mermaid\packages\mermaid\src\rendering-util\rendering-elements\markers.js` (`requirement_arrow`, `requirement_contains`)

**Naiad current code:**
- `extern\Naiad\src\Naiad\Diagrams\Requirement\RequirementRenderer.cs`
  - `DrawRelation(...)` uses a generic arrowhead polygon.

**Required changes:**
- Implement and attach requirement-specific marker shapes.
- Ensure marker placement does not drift relative to Mermaid defaults (match `refX/refY` etc.).

**Acceptance criteria (visual):**
- Requirement relation endpoints match Mermaid’s marker semantics and geometry.

## How to keep the repo consistent while fixing

- Keep `Naiad_Mermaid_Spec_Gap_Checklist.md` updated:
  - If a suspected issue turns out already correct, remove it.
  - If you discover a new edge/line mismatch, add it with upstream file pointers and Naiad file pointers.
- For each fix that changes an upstream-traced rule, update `Mermaid_Specification.md` first (spec-first rule).

## Definition of done (for this workstream)

This workstream is “done” when:

- Naiad renders **marker geometry, marker placement, edge routing, and line styles** for each diagram family above so that:
  - Word rendering of `test.docx` matches Mermaid reference output closely (shape, alignment, line endings, dash patterns, curvature).
- The fixes are captured in:
  - `Mermaid_Specification.md` (rules) and
  - `Naiad_Mermaid_Spec_Gap_Checklist.md` (tracked issues),
  - and the repo can be re-regenerated reliably via the `regen` workflow.


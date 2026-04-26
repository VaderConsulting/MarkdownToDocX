# AI Instructions (Handoff Notes)

## How to use this file

When the user says “read `AI-Instructions.md`”, treat it as **instructions to apply**, not content to repeat back. Do **not** paste the whole file into chat unless the user explicitly asks for the full text; instead, acknowledge and proceed using these rules.

## Purpose

This repository renders Mermaid diagrams from Markdown and embeds them into DOCX. There are two visually distinct rendering paths that must be kept consistent:

- **DOCX output**: Produced by **Naiad** (the C# Mermaid renderer in `extern/Naiad/`) via `MermaidSharp.Mermaid.Render(...)`.
- **Markdown preview**: Produced by **Mermaid.js** inside the editor (typically the VS Code / Cursor Mermaid preview extension).

When diagnosing “colour” or “styling” issues, always confirm which path is being discussed (DOCX vs preview). They can diverge even when the same Mermaid source is used.

**Project goal:** Produce **Microsoft Word `.docx`** files from Markdown such that **every Mermaid diagram becomes an embedded SVG image** in the DOCX, and **Word’s rendered result matches Mermaid’s official defaults**.

Practical parity target:

- **Primary side-by-side reference**: the **official Mermaid preview extension in VS Code** (Mermaid.js).
- **Canonical upstream reference** (default theme/behaviour): `mermaid.live` and Mermaid’s official documentation.

Important nuance:

- “Matches Mermaid” means **matches what Mermaid.js produces** (syntax, layout, geometry, markers, and default styling), but the final viewer is **Microsoft Word**, which has its own SVG feature constraints. Therefore parity work must be validated in **Word**, not only in a browser.

## Primary technical references (must be used together)

Use these as the sources of truth when implementing, diagnosing, or documenting behaviour:

- **Mermaid syntax and supported diagram families** (Mermaid 11.14.0 documentation set): `https://mermaid.js.org/intro/syntax-reference.html`
- **Mermaid upstream source** (authoritative implementation of Mermaid.js): `https://github.com/mermaid-js/mermaid`
- **Local Mermaid upstream checkout** (available in this repo): `.\mermaid\` (use this for source-tracing and parity work)
- **This repo’s consolidated geometry/marker/specification**: `Mermaid_Specification.md` (repo root)

If these disagree:

- Prefer the pinned Mermaid version’s **actual output** (rendered SVG) as the practical “truth”.
- Then update `Mermaid_Specification.md` and/or `Naiad issues.md` with what was learned so the repo stays consistent.

## Official Mermaid references (syntax + behaviour)

Use these as the source of truth for official Mermaid syntax and diagram type list:

- Mermaid (current documentation home): `https://mermaid.ai/open-source/intro/`
- Mermaid home: `https://mermaid.js.org/`
- Getting started: `https://mermaid.js.org/intro/getting-started.html`
- Diagram syntax reference: `https://mermaid.js.org/intro/syntax-reference.html`

When expanding diagram support or investigating a parsing mismatch, confirm:

- Whether Mermaid upstream considers the feature stable (some diagram families are marked beta/experimental).
- Whether the VS Code Mermaid preview extension is using default themes/config for parity checks.

## Scope: diagram families and capability policy

Mermaid supports many diagram families (see Mermaid docs). Naiad implements a substantial subset for DOCX rendering. Treat support as a **capability matrix**, not an assumption that “Mermaid” implies everything.

Rules:

- **Never claim full Mermaid coverage** unless Naiad can detect → parse → render → style-match for that family (and the results are validated in Word).
- Where Naiad does not support a family, behaviour should be explicit: fail with a clear error or implement support end-to-end.
- For beta/experimental families, require **version pinning** and **regression fixtures** before calling parity “done”.

## Naiad structure and supported Mermaid diagram types

Naiad is vendored under `extern/Naiad/` and is responsible for DOCX SVG output.

### Core entry points (DOCX render path)

- `extern/Naiad/src/Naiad/Mermaid.cs`
  - `Mermaid.Render(...)` trims input, strips leading `%%{init: ...}%%` blocks, detects diagram type, then dispatches to a diagram-specific parser + renderer.
- `extern/Naiad/src/Naiad/DiagramType.cs`
  - Defines Naiad’s internal `DiagramType` values.

### Per-diagram organisation

Each supported diagram family generally has:

- Parser: `extern/Naiad/src/Naiad/Diagrams/<Family>/<Family>Parser.cs`
- Renderer: `extern/Naiad/src/Naiad/Diagrams/<Family>/<Family>Renderer.cs`

Some diagrams also share styling via:

- `extern/Naiad/src/Naiad/Rendering/MermaidStyles.cs`

### Supported diagram types (implemented in `Mermaid.Render`)

These diagram types are actively dispatched by `extern/Naiad/src/Naiad/Mermaid.cs` and should have examples in `test.md`:

- Flowchart (`flowchart`, `graph`)
- Sequence (`sequenceDiagram`)
- Class (`classDiagram`)
- State (`stateDiagram`, `stateDiagram-v2`)
- Entity relationship (`erDiagram`)
- Gantt (`gantt`)
- Pie (`pie`)
- Git graph (`gitGraph`)
- Mindmap (`mindmap`)
- Timeline (`timeline`)
- User journey (`journey`)
- Quadrant chart (`quadrantChart`)
- XY chart (`xychart`)
- Sankey (`sankey-*` / `sankey-beta`)
- Block diagram (`block-*` / `block-beta`)
- Kanban (`kanban`)
- Packet diagram (`packet-*` / `packet-beta`)
- C4 (`C4Context`, `C4Container`, `C4Component`, `C4Deployment`)
- Requirement diagram (`requirementDiagram`)
- Architecture diagram (`architecture`, `architecture-beta`)
- Radar chart (`radar`, `radar-beta`)
- Treemap (`treemap`, `treemap-beta`)

### Declared but not currently dispatched

Naiad’s `DiagramType` enum currently includes `ZenUML`, but `Mermaid.Render(...)` does not dispatch it. Treat this as **not supported** in the DOCX pipeline until implemented end-to-end (detect → parse → render → style parity).

### Known Mermaid diagram types not currently detected by Naiad (to be addressed)

These diagram families are supported by Mermaid, but Naiad currently fails to detect them and reports:

```text
No diagram type detected matching given configuration for text: <diagram-type>
```

Known current gaps:

- Venn (`venn`)
- Ishikawa / fishbone (`ishikawa`)
- TreeView (`treeView`)
- ZenUML (`zenuml`)

When these appear in `test.md`, treat failures as expected until Naiad is extended to:

```text
detect -> parse -> render -> style parity (Word-safe SVG) -> validation in Word
```

**Flowchart fork wording:** If the user says links should meet **“above the box”**, they mean **along the view Z axis** (depth: toward the viewer / “in front of” the diagram plane), **not** necessarily a smaller **SVG Y** coordinate. Layout places each **outflow** where the ray from the **node’s geometric centre** toward the **child** meets the **bottom outline** (two exits → two intersections on that edge); continuing those segments backward meets at **centre-middle**. Edges are still painted **before** nodes so connectors sit **under** the boxes.

**Flowchart connector inset:** The reference shows **no** visible gap between the **node border** and the **connector stroke** (0 px). Naiad uses **`FlowchartEdgeNodeGap = 0`** for route attach points on the outline; do not reintroduce an arbitrary inset “for clearance” when matching Mermaid unless the user asks.

**No browser in the product path:** **DOCX output is not produced with a headless browser.** Naiad draws **native C# renderers** and/or **CSS-styled SVG**; the pipeline embeds that SVG in Word. The user’s workflow is **build → `md2docx` → open `test.docx` in Word** and compare to the **editor Mermaid preview**—not Playwright, not Chrome, not `mermaid.js` at server render time for DOCX.

**Naiad’s `extern/Naiad/src/Tests` project** (Playwright / NUnit) is **separate** optional test infrastructure. It can require downloaded browser binaries after NuGet updates. **Do not** treat those tests as required to validate rendering parity, and **do not** run `playwright install` or push the user to install Playwright browsers unless they explicitly want that test project run. For this repository, prefer **`MDConverter.Tests`**, **`dotnet build`**, and **regenerating `test.docx`**.

## Mermaid_Specification.md (repo-local “how to match Mermaid” reference)

`Mermaid_Specification.md` is the consolidated reference for:

- the Mermaid rendering pipeline (parse → measure → layout → draw → marker offset → marker attach → resolve style → final SVG)
- flowchart node geometry and edge/marker geometry (including marker definitions for point/circle/cross and marker offset tables)
- shape catalogue closure status and “what remains runtime-dependent”
- TypeScript/source closure categories (formula vs mapping vs runtime measurement vs layout output vs style resolution vs generated paths)

Rules:

- When fixing parity issues in Naiad, use `Mermaid_Specification.md` to decide **what is deterministic geometry** vs **what must be captured from official rendered SVG**.
- If you discover a corrected/changed rule, update `Mermaid_Specification.md` so the repo’s specification remains the shared baseline.

### Text vertical alignment across diagrams (Word vs Mermaid)

Word’s SVG text rendering tends to place `dominant-baseline="middle"` **slightly high** versus the Mermaid / VS Code reference.

- Prefer the shared label-centre approach used by **`SvgForeignObject`** and **`SvgBuilder.AddBoxCenteredText`**:
  - an **em-scaled** down-nudge (`OpticalVerticalBiasEm`) plus an optional **pixel** trim (`ExtraVerticalOffsetPx`)
- Apply this consistently to **box-based diagram labels**, not just flowcharts (for example Sequence participants, Class/State/ER boxes, etc.).
- When tuning, prefer small incremental changes (1–3 px) and keep the bias values **centralised** per diagram family (constants), rather than scattering ad-hoc `+ 5` offsets.

### DOCX SVG embed sizing policy (avoid forced upscaling)

Embedding size is controlled by **`DocxWriter.CalculateSvgExtents`**. The goal is parity and readability without making small diagrams occupy most of the Word page.

- Do **not** force a minimum “readable width” that upscales small diagrams arbitrarily (this made Class/State/Gantt look **too large** on the page).
- Do cap **maximum width** and **maximum height** so large diagrams still fit on the page.

## Ephemeral chat vs explicit rules

Treat what the user says in two ways:

- **Ephemeral (task-scoped):** Instructions for the current task or turn only—follow them, but **do not** treat them as permanent repo policy unless they say otherwise.
- **Explicit (standing rules):** Expectations they want remembered across sessions—phrased as ongoing conventions (“always…”, “when I say X, do Y”), or they **ask to add something to these instructions**.

**What to do with explicit rules:** Add or update **`AI-Instructions.md`** so future runs apply the same policy. Keep entries **short and actionable**. If something was ephemeral and they later say it should be a rule, promote it into this file then.

### PowerShell: never use bash-style heredocs (mandatory)

This workspace uses **PowerShell**, not bash.

- Do **not** attempt bash heredocs such as `cat <<'EOF' ... EOF` (they fail in PowerShell).
- For multi-line commit messages, use a **PowerShell here-string** assigned to a variable, then pass it to `git commit -m $Message`.
- For single-line commit messages, pass a normal quoted string to `git commit -m "..."`.

### Spec-first rule when discrepancies are discovered (mandatory)

When you discover a **confirmed discrepancy** between:

- Mermaid upstream (use the local checkout in `.\mermaid\` where available), and
- Naiad’s current DOCX output

you must **update `Mermaid_Specification.md` first** to capture the newly confirmed rule/geometry/style/behaviour **before** implementing the corresponding Naiad change.

Then implement the Naiad fix, then **regen** and validate in Word.

### Primary objective: 100% compatibility, minimal Naiad changes

When implementing parity fixes, keep these priorities in order:

- **Match Mermaid.js output exactly** (official renderer behaviour and geometry).
- **Minimise Naiad divergence**: prefer fixes that make Naiad follow Mermaid’s existing rules (especially around layout and routed points) rather than inventing new Naiad-only heuristics.

### Version control (no Git)

This workspace **does not use Git**. There is **no `.git` repository**—do **not** assume `git status`, `git log`, `git diff`, or `git show` will work, and do not instruct the user to use Git for history or rollback.

For prior versions of files and comparing edits over time, use **Cursor/VS Code Local History** (including the **Local History** extension and the editor **Timeline**). Treat that as the substitute for Git history here.

### Scripting and runtimes (no Python, no Node)

Do **not** assume **Python** or **Node.js**/`npm`/`npx` are installed or part of this workflow. Prefer **PowerShell**, **`dotnet`** CLI, **`dotnet-script`** for small **C#** scripts, and normal **C# projects/tools** in the repo.

When ad-hoc scripting or exploration fits better as C# than shell one-liners, **dotnet-script** is the intended option—not Python or Node.

### “Run tests” means MDConverter + output file, not Naiad

When the user asks to **run tests** (or similar: verify with tests, run the test suite for this workflow), execute **in order**:

1. **Regenerate the verification DOCX** — `md2docx` from `.\test.md` to **`.\test.docx`** at the repo root (see “Quick DOCX regeneration”). If the write fails because Word has the file open, tell the user, wait for them to close Word, retry the same path.
2. **`dotnet test` on `tests/MDConverter.Tests/MDConverter.Tests.csproj` only.**

**Do not** run `extern/Naiad/src/Tests` (Playwright) unless they **explicitly** ask for that project.

### Regenerate `test.docx` before any ask for Word validation (mandatory)

The user **cannot** see updated diagram or DOCX output until **`test.docx`** at the repository root is regenerated from **`test.md`**.

- If you want the user to **validate** your work in Word (or to compare DOCX to the Mermaid preview), **you** must run **`md2docx`** in that turn—**do not** ask them to regenerate, and **do not** offer regeneration as an optional follow-up (“say if you want `test.docx` rebuilt,” “regenerate if you’d like to check,” etc.). Regenerate **first**, then hand off.
- Use: `dotnet run --project .\src\md2docx\md2docx.csproj -- .\test.md .\test.docx` from the repo root (see **Quick DOCX regeneration**). If the write fails because Word has the file open, tell them once, **retry the same path** after they close it.
- Doc-only or comment-only changes that do not affect the build may skip regeneration—**any** change that could alter embedded SVG or the conversion pipeline **must** include regeneration when you are asking for a visual check.

### Naiad source fixes and the issues log

This repository **vendors Naiad** under **`extern/Naiad/`**. When Word/DOCX output is wrong relative to the VS Code Mermaid preview (or otherwise does not meet the parity goal), **edit Naiad source as needed**—native renderers, `MermaidStyles.cs`, parsers, and so on—until rendering is correct for the pipeline.

**Also maintain `Naiad issues.md`** at the repository root (`MarkdownToDocX/Naiad issues.md`). When you **identify** a gap, **fix** it, or **resolve** an item, update that document so it stays the running record of known problems, reproducers, and fix status. Do not leave the issues file stale after substantive Naiad work unless the user asks otherwise.

Also keep the parity tracking checklist up to date:

- **`Naiad_Mermaid_Spec_Gap_Checklist.md`** (repo root): tick off gaps and note what was verified in Word vs what still differs.

### Verify before asking for a final human test

Do **not** fire off changes and immediately ask the user to test in Word without doing your own passes first.

**Before presenting work as ready for their final check:**

1. **`dotnet build`** the touched projects (at minimum **Naiad** and **MDConverter** when Naiad changed).
2. **`dotnet test`** on **`tests/MDConverter.Tests`** (see **“Run tests”** above).
3. **Regenerate `test.docx`** whenever the pipeline could be affected **or** you are asking the user to validate in Word (see **“Regenerate `test.docx` before any ask for Word validation”** above); if Word locks the file, say so once and retry after they release it—do not skip verification silently.
4. Where useful for rendering changes, **inspect outputs yourself** (for example embedded SVG inside DOCX, path markup, or targeted greps)—not endless tweaking without checking anything.

**Build note:** `dotnet build` at the repository root may fail in some environments due to missing strong-name key material (for example `key.snk`). If that happens, build the touched projects directly (for example `dotnet build .\\extern\\Naiad\\src\\Naiad\\Naiad.csproj`) and proceed with `MDConverter.Tests` + `md2docx`.

**Build note (strong-name signing):** `extern/Naiad/src/Naiad/Naiad.csproj` may be configured to sign outputs with a strong-name key expected at the repo root (for example `key.snk`). If the key is missing, builds fail with `CS7027`. The pragmatic approach for local iteration is to conditionally disable signing when the key file is not present.

**Build note (analyser warnings as errors):** This repo treats code analysis as errors in some projects. When `dotnet run` fails due to CA rules (for example `CA1865`, `CA1846`, `CA1822`), fix the code rather than suppressing the build. When Naiad fails due to style rule `IDE0007` (prefer `var`), prefer localised `#pragma warning disable IDE0007` blocks around small sections rather than changing type clarity throughout the renderer code.

**Bounded iteration:** Aim for **one coherent hypothesis → change → verify** cycles. **Avoid flip-flopping**: alternating approaches forever without convergence wastes everyone’s time. After **two or three** grounded iterations without satisfying checks, **stop**, summarise what failed and what was ruled out, and ask a **narrow** question—or present the best-so-far state with explicit residual risks—rather than spiralling.

Only ask the user for a **final** Word/visual comparison when you would be reasonably satisfied presenting the outcome yourself.

## Primary expectation

Keep output aligned with **Mermaid’s official defaults** (the same appearance as `mermaid.live` when no custom style/config is applied), so that Naiad’s SVG embedded in Word matches what you see from the **official Mermaid plugin** in VS Code when both use the same default theme configuration.

## Version control: commit after non-trivial updates

After every **non-trivial** update, perform a **Git commit** with a commit message that sufficiently explains the change (focus on the “why”, not just the “what”).

Notes:

- If the current workspace is **not** a Git repository (or Git is not available), use **Cursor/VS Code Local History** / **Timeline** as the fallback record and do not attempt Git operations.

## How to decide “what is correct” (priority order)

When determining the correct output:

1. **Pinned Mermaid.js output** (rendered SVG from the chosen Mermaid version + runtime) is the most authoritative practical reference.
2. Mermaid official docs describe syntax and intended behaviour; confirm with the pinned output for details that docs do not specify.
3. `Mermaid_Specification.md` describes the extraction and closure method and should be kept consistent with the above.
4. Word’s SVG limitations may require implementation choices (for example filter primitives) that preserve appearance while staying Word-safe.

For Mermaid’s built-in **default theme**, the key values (from Mermaid’s upstream `theme-default.js`) are:

- **Node / actor background**: `#ECECFF`
- **Primary border colour**: `#9370DB`
- **Line colour**: `#333333`
- **Sequence diagram** uses the same `mainBkg` + `border1` for actor boxes and lifelines.
- **ER diagram attribute stripes** (from Mermaid’s upstream `erDiagram-oldHardcodedValues.ts`): odd `#ffffff`, even `#f2f2f2`.
- **Gantt** has its own defaults (for example `sectionBkgColor` is `rgba(102, 102, 255, 0.49)`, task bars are `#8a90dd` with border `#534fbc`).

Do **not** invent per-diagram palettes unless explicitly requested. The user wants Mermaid-official behaviour.

## Parity guardrail: correct scenarios never regress (mandatory)

When working on any diagram family, treat any **confirmed-correct scenario** as a **locked baseline**.

Rules:

- **Never “pull changes from an incorrect scenario into a correct one”**. If a change helps a new scenario but harms any previously-correct scenario, the change is wrong and must be reverted or re-scoped.
- Prefer **scoped behaviour** (fixture-specific detection, diagram-type gates, or narrow conditions) that improves the target scenario **without changing** locked baselines.

ER-specific verification for current work:

- After any ER change, regenerate `test.docx` and extract embedded SVGs, then explicitly verify both:
  - scenario 1 SVG contains `CUSTOMER` and `LINE_ITEM`
  - scenario 2 (matrix) SVG contains `ONE_ONLY` and `ONE_ONLY_B`

## How I have been working (behavioural overview)

- **Render correctness over “style experimentation”**: Reverted palette experiments and re-aligned to Mermaid defaults.
- **Treat tests as potentially correct**: If tests fail, assume the code may be wrong until proven otherwise.
- **Prefer simple, debuggable C#**: Explicit types where practical, braces always, block-bodied members, and XML docs where appropriate.
- **Work in small, verifiable steps**: Make a change, build, then check outputs.
- **Documentation**: Avoid churn in **code** comments unless needed. **Do** update `AI-Instructions.md` when the user gives an **explicit rule** or asks to record something here. Do not promote every ephemeral remark into this file.
- **Naiad + `Naiad issues.md`**: Fix Naiad under `extern/Naiad/` when required for correct DOCX rendering; keep **`Naiad issues.md`** in sync (see “Naiad source fixes and the issues log”).
- **Handoff quality**: Self-verify (build, `MDConverter` tests, `test.docx` when relevant) before asking for a final human test; avoid unbounded flip-flop iteration (see “Verify before asking for a final human test”).

## Key repository components

### DOCX pipeline

- `src/MDConverter/Pipeline/DocxWriter.cs`
  - Calls `MermaidSharp.Mermaid.Render(MermaidBlock.Source, RenderOptions.Default)`
  - Embeds the returned SVG into DOCX as an SVG image part.

### Naiad (C# Mermaid renderer)

- `extern/Naiad/src/Naiad/Mermaid.cs`
  - Detects diagram type and dispatches to diagram-specific renderers.
  - Strips `%%{init: ... }%%` blocks from the top of the input before rendering.

- `extern/Naiad/src/Naiad/Rendering/MermaidStyles.cs`
  - Contains CSS blocks intended to match Mermaid’s default theme output for diagram types that use class-based styling.

- **`Naiad issues.md`** (repository root)
  - Running log of Mermaid/DOCX parity items; **update when Naiad rendering work** finds, fixes, or closes issues.

### Editor Markdown preview

The preview is **not** Naiad. It is Mermaid.js from the editor extension.

Workspace settings live in:

- `.vscode/settings.json`
  - Set to Mermaid’s **official themes**:
    - `markdown-mermaid.lightModeTheme`: `default`
    - `markdown-mermaid.darkModeTheme`: `dark`

If preview still does not match `mermaid.live`, confirm which Mermaid extension is active and whether it honours these settings.

## What changed (high-signal summary)

### Theme alignment to Mermaid official defaults

- Re-aligned diagram colours to Mermaid default theme values rather than per-diagram palettes.
- Confirmed Mermaid default theme constants by inspecting Mermaid upstream `theme-default.js`.

### MermaidStyles (CSS)

File: `extern/Naiad/src/Naiad/Rendering/MermaidStyles.cs`

- Ensured Flowchart / Sequence / Class / State / ER style blocks use the Mermaid default theme palette (`#ECECFF`, `#9370DB`, `#333333`) where applicable.
- Restored **Gantt** CSS to the official default theme values (do not force it into lavender; Mermaid default Gantt is not lavender).
- Updated ER CSS to use **official odd/even attribute stripe fills** (`#ffffff` / `#f2f2f2`) and the standard border colour (`#9370DB`).

### Native renderers

- Flowchart, sequence, class, state: kept aligned with the Mermaid default theme’s palette.
- ER: updated to draw header and attribute stripes in a way consistent with Mermaid defaults (header band and alternating row fills).
- Gantt: restored section colour to Mermaid default’s `rgba(102, 102, 255, 0.49)` (not lavender).

### Flowchart line ends (current parity status for Word)

Reference fixture lives in `test.md` under “Flowchart: all supported line / arrow types”.

Current Word-targeted behaviour (Naiad):

- **Arrows (`-->`, `-.->`, `==>`)**: endpoint is trimmed so the stroke does not intrude into the filled arrowhead (Word anti-aliasing can otherwise show the stroke “through” the head).
- **Circles (`--o`, `o--o`)**: no stroke protrusion past the filled circles. Achieved by circle marker anchoring tweaks in `extern/Naiad/src/Naiad/Rendering/SvgBuilder.cs`:
  - `mermaid-svg_flowchart-v2-circleEnd` uses `refX = 10`
  - `mermaid-svg_flowchart-v2-circleStart` uses `refX = 0`
- **Crosses (`--x`, `x--x`)**:
  - The **X must remain anchored** at the node edge.
  - The **visible stroke must stop short** of the X by a tuned gap without moving the marker.
  - Achieved by rendering **two paths** in `extern/Naiad/src/Naiad/Diagrams/Flowchart/FlowchartRenderer.cs`:
    - visible trimmed stroke path (no markers)
    - marker-only path (stroke="none") that reaches the true endpoint
  - Current tuning: `CrossStrokeGapPx = 10.2` (sets the visible stroke end to X≈198 when the node edge is X≈208.2 in the default fixture).

### Drop shadow / visual parity

To match the “official look” (including subtle shadows), Naiad applies a lightweight SVG filter to selected native shapes (for example flowchart nodes and sequence participant boxes).

Implementation detail (important for Word):

- Microsoft Word’s SVG renderer can **omit filtered elements entirely** when the filter uses `<feDropShadow>` (symptom: node shapes vanish; text/edges may remain).
- To keep shadows while staying Word-safe, Naiad uses an SVG 1.1 primitive chain (`feGaussianBlur` + `feOffset` + `feFlood` + `feComposite` + `feMerge`) instead of `<feDropShadow>`.

This is in Naiad’s **native** output. Mermaid.js preview handles shadows differently (CSS `drop-shadow(...)`), so do not expect 1:1 implementation details.

### ER diagrams (native renderer): crow’s foot vs “arrowheads”

Naiad’s ER renderer draws cardinality symbols directly (not SVG marker arrowheads). If crow’s feet still look “arrow-like” in Word, it is usually geometry/spread/offset tuning rather than palette.

Recent findings:

- A **centre prong** in the crow’s foot tends to read like an arrow tip in Word; removing it helped.
- Entity box sizing is driven by simple text-width estimates + padding constants in `extern/Naiad/src/Naiad/Diagrams/EntityRelationship/ERRenderer.cs`. Word often renders SVG text wider than estimates; prefer slightly generous padding/min widths when chasing `mermaid.live` parity.

Additional ER parity rules established while matching the Mermaid standard reference:

- **Curved toes**: Mermaid’s crow’s-foot “toes” render as curved strokes, not straight line segments. Implement prongs as SVG paths (quadratic or cubic Béziers) rather than `<line>`.
- **Centre toe**: Do not draw a separate centre prong; the relationship line itself should read as the centre toe.
- **Marker placement along the relationship**:
  - The **circle centre** (for `o{` / `o|`) and the **bar midpoint** (for `|{` / `||`) must lie **on the relationship line**.
  - Keep circle and bar at the **same offset distance from the entity edge** (measured along the relationship direction).
  - For **curved relationships**, choose a point **on the curve** at the required offset from the entity edge (not by projecting along a straight chord).
- **Top-edge entry constraint**: When the relationship enters the **top edge** of an entity, both outer toes must remain on the outside of the box (above the edge in SVG Y terms). A small clearance (a couple of pixels) avoids “toe on the edge” artefacts in Word.
- **Circle fill**: Cardinality circles are white-filled (`#ffffff`) with a dark stroke (`#333333`).

When iterating ER geometry, keep changes small and always verify by regenerating `test.docx`, extracting the embedded SVG, and inspecting the actual emitted path data and coordinates.

### ER diagrams: routing ownership and `edge.points` authority (mandatory)

For Mermaid ER diagrams, treat the Dagre layout engine’s routed polyline as authoritative:

- **Do not override relationship endpoints in `ERRenderer`** to “fix ports” or “fix slotting”. If endpoints/slots are wrong, fix them in layout routing so the produced `LayoutEdge.Points` match Mermaid.
- **Arc / bulge shaping belongs in the layout stage**: if a relationship route has only two endpoints (no dummy nodes), any additional guide points used to produce Mermaid-like `curveBasis` curvature must be inserted during layout routing, not in the renderer.
- **Direction semantics**: Mermaid ER marker semantics are B-side → A-side for many relationships. Naiad may reverse point order for marker correctness, but must preserve the same underlying coordinates produced by layout.

### Fast visual iteration for ER parity (SVG + PNG) using `SvgAudit`

For parity work where you need to see the rendered image (not SVG text), use `SvgAudit` to render Mermaid input into an SVG file and (optionally) a PNG file for quick visual comparison.

Key workflow rules:

- Prefer running the **compiled executable** rather than `dotnet run` for each iteration (faster, avoids “hangs” caused by rebuilds and large stdout).
- Avoid PowerShell here-strings unless you are certain you will close them correctly; an unclosed here-string will make the shell appear to “hang”.

Practical PowerShell pattern (write input to a file, then pipe raw text):

- Create or overwrite an input file (for example `.\out\er_input.mmd`).
- Pipe with `Get-Content -Raw` into `SvgAudit.exe` and suppress stdout using `--no-stdout`.

Example:

```powershell
$SvgAuditExePath = "C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX\src\SvgAudit\bin\Debug\net10.0\SvgAudit.exe"

New-Item -ItemType Directory -Force -Path ".\out" | Out-Null
Set-Content -Path ".\out\er_input.mmd" -Encoding UTF8 -Value @"
erDiagram
  CUSTOMER ||--o{ ORDER : places
"@

Get-Content -Raw ".\out\er_input.mmd" | & $SvgAuditExePath render ".\out\rendered.svg" ".\out\rendered.png" --no-stdout
```

If output files exist but the console appears stuck, confirm the process has ended and the files are updated (timestamps). Avoid printing full SVG to the terminal during iteration.

### Quick DOCX regeneration while iterating visuals

#### Terminology: “regen”

When the user says **“regen”**, it means:

- **Regenerate** the repo-root verification DOCX (**only**): run the DOCX pipeline to load `.\test.md` and generate `.\test.docx` at the repo root using the **latest built code**.
  - If there were code changes in this turn, build as needed first.
  - **Do not** run tests as part of “regen”. Tests are only run when the user explicitly asks to run tests.

Practical command (from repo root):

- `dotnet run --project .\src\md2docx\md2docx.csproj -- .\test.md .\test.docx`

There is a small CLI wrapper:

- `src/md2docx/Program.cs`
  - Usage: `md2docx <input.md> <output.docx> [--profile <name>]`

Example (from repo root):

- `dotnet run --project .\src\md2docx\md2docx.csproj -- .\test.md .\test.docx`

Use **`test.docx`** at the repository root for iterative visual checks:

- **`C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX\test.docx`**

If a **write fails because the output file is in use** (for example Microsoft Word has `test.docx` open), **tell the user** so they can close Word, then **retry the same command** with the same output file. Do not silently switch to a different output path unless the user asks.

### Inspect embedded SVG in a DOCX (for precise parity checks)

To compare the actual SVG Word will render (not what a browser would render), extract the embedded media from `test.docx`:

- Copy `test.docx` to `test.zip`
- Expand the archive to a scratch folder (for example `_docx_extract`)
- Locate the ER SVG under the extracted `media` folder (commonly `*_docx_extract/media/image*.svg`)

PowerShell example (from repo root):

- `Copy-Item .\\test.docx .\\test.zip -Force; Expand-Archive .\\test.zip -DestinationPath .\\_docx_extract -Force`

Then search for the diagram by label text (for example `LINE_ITEM`, `CUSTOMER`, `appears_in`) and inspect emitted primitives:

- crow’s foot toes should be `<path ...>` (curved)
- circle markers should be `<circle ...>` with centre on the relationship line

### After every regen: extract embedded SVGs (mandatory)

After every regeneration of `test.docx` (a “regen”), automatically:

- copy `test.docx` to `test.zip`
- expand to `_docx_extract`
- locate the relevant `media/image*.svg`
- inspect the emitted SVG (paths, markers, endpoints) as part of verification

Do **not** commit `_docx_extract` or `test.zip`; they are scratch artefacts for inspection only.

## Known gotchas

### Preview vs DOCX mismatch

- The Markdown preview is Mermaid.js; DOCX is Naiad.
- If the preview looks wrong but DOCX looks right, fix `.vscode/settings.json` / Mermaid.js configuration, not Naiad.
- If DOCX looks wrong but preview looks right, fix Naiad renderers and/or `MermaidStyles.cs`.

### Mermaid init blocks are stripped in Naiad

Naiad strips leading `%%{init: ...}%%` blocks (see `Mermaid.StripInitBlock`). That is good for standardisation, but it means per-diagram init-based theming will not affect DOCX output.

### Word has the verification DOCX open

If regenerating **`test.docx`** fails with a file-in-use or access-denied error, Word (or another process) is likely holding the file. **Close Word** (or the file) and retry. The agent should **report** this to the user and **retry the same output path** after the file is released; see the “Quick DOCX regeneration” section above.

### Microsoft Word SVG limitations (DOCX embedding)

Word is the reference client for DOCX SVG embedding. Treat these as hard constraints:

- **CSS inside `<style>` may be partially ignored** compared to browsers; prefer **inline SVG attributes** for critical geometry (fills/strokes) when chasing reliability.
- **SVG filters are the highest-risk feature**; validate any shadow/filter change directly in Word, not only in preview or a browser.

### Tests

- **When the user says “run tests”:** see **“Ephemeral vs explicit rules”** → **“Run tests” means MDConverter + output file, not Naiad** — regenerate **`test.docx`**, then **`MDConverter.Tests` only**.
- **`tests/MDConverter.Tests`** — the pipeline test project; this is what “run tests” refers to.
- **`extern/Naiad/src/Tests`** — uses **Microsoft.Playwright** to compare SVG in a **browser context**. **Not** included in “run tests” unless explicitly requested. Failures here often mean missing Playwright browser payloads, not broken Word output. **Skip or ignore** unless the user asks to fix or run them.

`dotnet test` may be constrained by the repository’s `global.json` test runner choice (Microsoft.Testing.Platform). If running tests fails with “VSTest test runner” warnings, update how tests are invoked or migrate the test project runner (do not silently ignore failures).

## How to verify quickly

Verification is **Word + VS Code preview**, not Playwright. **“Run tests”** (user phrase) = regenerate **`test.docx`** + **`dotnet test`** on **`MDConverter.Tests`** — see above.

### Build Naiad

From `extern/Naiad/src/Naiad/`:

- `dotnet build`

### Build MDConverter

From `src/MDConverter/`:

- `dotnet build`

### Regenerate DOCX

From the repository root (see “Quick DOCX regeneration”):

- `dotnet run --project .\src\md2docx\md2docx.csproj -- .\test.md .\test.docx`

### Visual verification

- Render a small flowchart and sequence diagram through the DOCX pipeline and check:
  - Flowchart node fill `#ECECFF`, stroke `#9370DB`, subtle shadow.
  - Sequence participant boxes fill `#ECECFF`, stroke `#9370DB`.
  - Sequence lifelines are purple (`#9370DB`), message arrows are dark (`#333`), dashed returns are dashed.
  - ER entity frames and rows match default Mermaid: header band + alternating `#ffffff` / `#f2f2f2` stripes.
  - Gantt matches Mermaid default (blue/violet bars, not lavender).

## Current working intent

The correct “north star” is:

- **Match the official VS Code Mermaid extension preview** as closely as practical in DOCX/Word output (that is the target visual parity).
- **Match mermaid.live default behaviour** upstream so colours and structure stay faithful to Mermaid’s shipped defaults.
- Keep preview configured to Mermaid’s default themes (see `.vscode/settings.json`) so the editor is a faithful reference, not a different theme.


# AI Instructions (Handoff Notes)

## How to apply

When the user says **“read `AI-Instructions.md`”**, treat it as **instructions to apply**, not content to repeat. Do **not** paste the full file unless they explicitly ask for the full text.

If `status.md` exists in the repo root, **read it first**. It is the latest “where we got to” briefing for resuming work, and should be used together with this file.

## Goal and parity target

This repo converts Markdown to Word DOCX and must embed **every Mermaid diagram as an SVG image** in the DOCX.

- **DOCX render path (product path)**: Naiad (C#) under `extern/Naiad/` via `MermaidSharp.Mermaid.Render(...)`
- **Markdown preview path (editor)**: Mermaid.js (VS Code / Cursor Mermaid preview extension)

When diagnosing styling/colour/layout, **always confirm which path** (DOCX vs preview). They can diverge.

**Parity definition:** “Matches Mermaid” means matching **Mermaid.js output** (syntax, layout, geometry, markers, default styling). The final viewer is **Microsoft Word**, so parity must be validated in **Word**, not only in a browser.

## Sources of truth (use together)

- Mermaid syntax and supported diagram families (Mermaid 11.14.0 docs): `https://mermaid.js.org/intro/syntax-reference.html`
- Mermaid upstream implementation: `https://github.com/mermaid-js/mermaid`
- Local Mermaid checkout in this repo: `.\mermaid\` (for source tracing / parity work)
- Repo specification: `Mermaid_Specification.md` (root)

If these disagree:

- Prefer the **pinned Mermaid version’s actual rendered output** (SVG) as practical truth.
- Then update `Mermaid_Specification.md` and/or `Naiad issues.md` with the confirmed rule so the repo stays consistent.

Additional official Mermaid references (syntax + behaviour):

- `https://mermaid.ai/open-source/intro/`
- `https://mermaid.js.org/`
- `https://mermaid.js.org/intro/getting-started.html`
- `https://mermaid.js.org/intro/syntax-reference.html`

When expanding support or investigating parsing mismatches, confirm:

- whether Mermaid considers the feature stable (some families are beta/experimental)
- whether the VS Code Mermaid preview extension is using default themes/config for parity checks

## How to decide what is correct (priority order)

1. Pinned Mermaid.js output (rendered SVG from the chosen Mermaid version + runtime) is the most authoritative practical reference.
2. Mermaid official docs describe syntax and intended behaviour; confirm with the pinned output for details docs do not specify.
3. `Mermaid_Specification.md` describes extraction/closure and must be kept consistent with (1) and (2).
4. Word’s SVG limitations can force implementation choices (for example filter primitives) that preserve appearance while staying Word-safe.

## Capability policy (treat as a capability matrix)

- Never claim full Mermaid coverage unless Naiad can **detect → parse → render → style-match** for the family and the result is validated in **Word**.
- For unsupported families: be explicit (clear error) or implement end-to-end.
- For beta/experimental families: require **version pinning** and **regression fixtures** before calling parity “done”.

## Naiad (DOCX SVG renderer): structure and diagram support

Naiad is vendored under `extern/Naiad/` and is responsible for DOCX SVG output.

Core entry points:

- `extern/Naiad/src/Naiad/Mermaid.cs`
  - `Mermaid.Render(...)` trims input, strips leading `%%{init: ...}%%` blocks, detects diagram type, dispatches to a diagram-specific parser + renderer
- `extern/Naiad/src/Naiad/DiagramType.cs` (Naiad internal enum)

Per-diagram organisation (typical):

- Parser: `extern/Naiad/src/Naiad/Diagrams/<Family>/<Family>Parser.cs`
- Renderer: `extern/Naiad/src/Naiad/Diagrams/<Family>/<Family>Renderer.cs`
- Shared CSS: `extern/Naiad/src/Naiad/Rendering/MermaidStyles.cs`

Supported diagram types (actively dispatched by `Mermaid.Render(...)`; ensure examples exist in `test.md`):

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

Declared but not dispatched:

- `DiagramType.ZenUML` exists but `Mermaid.Render(...)` does not dispatch it. Treat as **not supported** in the DOCX pipeline until implemented end-to-end (detect → parse → render → style parity).

Known Mermaid families not currently detected by Naiad (expected failure until implemented):

```text
No diagram type detected matching given configuration for text: <diagram-type>
```

- Venn (`venn`)
- Ishikawa / fishbone (`ishikawa`)
- TreeView (`treeView`)
- ZenUML (`zenuml`)

Implement support as:

```text
detect -> parse -> render -> style parity (Word-safe SVG) -> validation in Word
```

## Key interpretation rules (flowchart wording and constraints)

**Flowchart “above the box” wording:** if the user says links should meet **“above the box”**, they mean along the **view Z axis** (in front of the diagram plane), **not** necessarily a smaller SVG Y coordinate. Layout places each outflow where the ray from the node’s geometric centre toward the child meets the bottom outline (two exits → two intersections on that edge); extending those segments backward meets at centre-middle. Edges are painted before nodes so connectors sit under boxes.

**Flowchart connector inset:** reference shows **0 px** visible gap between node border and connector stroke. Naiad uses `FlowchartEdgeNodeGap = 0`; do not reintroduce a clearance inset when matching Mermaid unless requested.

## Mermaid_Specification.md: how to match Mermaid (repo-local baseline)

`Mermaid_Specification.md` consolidates:

- pipeline (parse → measure → layout → draw → marker offset → marker attach → resolve style → final SVG)
- flowchart node geometry and edge/marker geometry (marker defs for point/circle/cross; marker offset tables)
- shape catalogue closure status and what remains runtime-dependent
- TS/source closure categories (formula vs mapping vs runtime measurement vs layout output vs style resolution vs generated paths)

Rules:

- Use it to decide what is deterministic geometry vs what must be captured from official rendered SVG.
- If you confirm a corrected/changed rule, update it so the repo baseline stays accurate.

Text vertical alignment (Word vs Mermaid):

- Word SVG text tends to place `dominant-baseline="middle"` slightly high vs Mermaid/VS Code reference.
- Prefer the shared label-centre approach used by `SvgForeignObject` and `SvgBuilder.AddBoxCenteredText`:
  - em-scaled down-nudge (`OpticalVerticalBiasEm`) + optional pixel trim (`ExtraVerticalOffsetPx`)
- Apply consistently to box-based labels (not only flowcharts: sequence participants, class/state/ER boxes, etc.).
- Tune in small steps (1–3 px) and keep bias values centralised per diagram family (constants), not scattered `+ 5` offsets.

DOCX SVG embed sizing policy (avoid forced upscaling):

- Sizing is controlled by `DocxWriter.CalculateSvgExtents`.
- Do not force a minimum “readable width” that upscales small diagrams (made Class/State/Gantt too large).
- Do cap maximum width and height so large diagrams fit the page.

## Workflow rules: chat instructions vs standing rules

- **Ephemeral (task-scoped):** follow for the current task/turn only, not standing policy unless stated.
- **Explicit (standing):** ongoing conventions (“always…”, “when I say X, do Y”) or user asks to add to instructions.
- For explicit rules: update `AI-Instructions.md` (short, actionable). Promote ephemeral only if later confirmed as standing.

## Mandatory environment and tooling constraints

PowerShell only (no bash heredocs):

- Do not use bash-style heredocs (`cat <<'EOF' ... EOF`).
- Multi-line commit message: use a PowerShell here-string in a variable, then `git commit -m $Message`.
- Single-line message: `git commit -m "..."`.

No browser in the product path:

- DOCX output is not produced via headless browser. Naiad renders in C# (native renderers and/or CSS-styled SVG) and the pipeline embeds SVG into Word.
- Workflow is build → `md2docx` → open `test.docx` in Word → compare against editor Mermaid preview.

Naiad Playwright tests are optional:

- `extern/Naiad/src/Tests` (Playwright / NUnit) is separate; may require browser binaries after NuGet updates.
- Do not treat those tests as required for rendering parity.
- Do not run `playwright install` or ask the user to install browsers unless they explicitly want that test project run.
- Prefer `MDConverter.Tests`, `dotnet build`, and regenerating `test.docx`.

Scripting and runtimes:

- Do not assume Python or Node.js (`npm`/`npx`) exist.
- Prefer PowerShell, `dotnet` CLI, `dotnet-script` for small C# scripts, and normal C# projects/tools in the repo.

## Mandatory spec-first rule for confirmed discrepancies

When a discrepancy between Mermaid upstream output (use `.\mermaid\` where useful) and Naiad DOCX output is confirmed:

1. Update `Mermaid_Specification.md` first with the confirmed rule/geometry/style/behaviour
2. Implement the Naiad change
3. Regen and validate in Word

Parity priorities:

- Match Mermaid.js output exactly (behaviour + geometry).
- Minimise Naiad divergence (follow Mermaid rules; avoid Naiad-only heuristics).

## Mandatory verification behaviour

“Run tests” means MDConverter + output file (not Naiad Playwright):

1. Regenerate verification DOCX: `.\test.md` → `.\test.docx` at repo root (see “DOCX regen” below). If Word has the file open, report it, wait for close, retry the same path.
2. Run tests: `dotnet test tests/MDConverter.Tests/MDConverter.Tests.csproj` only.

Regenerate `test.docx` before asking for Word validation (mandatory):

- The user cannot see updated output until repo-root `test.docx` is regenerated from `test.md`.
- If asking for Word/visual validation, you must run `md2docx` in that turn (do not ask the user to regen; do not offer as optional).
- Command (repo root):
  - `dotnet run --project .\src\md2docx\md2docx.csproj -- .\test.md .\test.docx`
- If write fails because Word holds the file: tell them once, then retry the same command/path after release.
- Doc-only/comment-only changes that cannot affect output may skip regen; anything affecting embedded SVG/pipeline must regen when requesting a visual check.

Naiad fixes and parity tracking docs:

- This repo vendors Naiad (`extern/Naiad/`). If DOCX output is wrong relative to preview / parity goal, edit Naiad sources as required (renderers, `MermaidStyles.cs`, parsers, etc.).
- Maintain `Naiad issues.md` (repo root) whenever you identify/fix/resolve an item; do not leave it stale after substantive Naiad work unless asked.
- Keep `Naiad_Mermaid_Spec_Gap_Checklist.md` up to date (tick gaps; note verified-in-Word vs still-different).

Verify before asking for the final human check:

- Do not immediately ask the user to test in Word without self-verification.
- Before presenting as ready:
  1. `dotnet build` touched projects (at minimum Naiad + MDConverter when Naiad changed)
  2. `dotnet test` on `tests/MDConverter.Tests` (as above)
  3. regen `test.docx` when pipeline may change or when asking for Word validation; handle file locks by retrying
  4. inspect outputs when useful (embedded SVG in DOCX, path markup, targeted searches), not endless tweaking

Build notes:

- Root `dotnet build` may fail if strong-name key material (for example `key.snk`) is missing. Build touched projects directly, e.g. `dotnet build .\extern\Naiad\src\Naiad\Naiad.csproj`, then proceed with `MDConverter.Tests` + `md2docx`.
- `extern/Naiad/src/Naiad/Naiad.csproj` may require signing with a key at repo root; missing key can fail with `CS7027`. Pragmatic local iteration: conditionally disable signing when key file is absent.
- Some projects treat analyser warnings as errors. If `dotnet run` fails with CA rules (e.g. `CA1865`, `CA1846`, `CA1822`), fix code rather than suppressing. If Naiad fails due to `IDE0007` (prefer `var`), prefer localised `#pragma warning disable IDE0007` blocks around small sections rather than reducing type clarity broadly.

Bounded iteration (mandatory):

- Prefer one coherent hypothesis → change → verify cycles.
- After two or three grounded iterations without passing checks, stop, summarise what failed/was ruled out, and ask a narrow question or present best-so-far with explicit residual risks.
- Ask for final Word comparison only when you would be reasonably satisfied presenting the outcome yourself.

## Default theme reference values (do not invent palettes)

When matching Mermaid’s built-in default theme (from upstream `theme-default.js` and related sources):

- Node / actor background: `#ECECFF`
- Primary border colour: `#9370DB`
- Line colour: `#333333`
- Sequence diagram uses `mainBkg` + `border1` for actor boxes and lifelines
- ER attribute stripes (from `erDiagram-oldHardcodedValues.ts`): odd `#ffffff`, even `#f2f2f2`
- Gantt defaults (example): `sectionBkgColor` is `rgba(102, 102, 255, 0.49)`, task bars `#8a90dd`, border `#534fbc`

## Guardrail: correct scenarios must never regress

Treat any confirmed-correct scenario as locked baseline.

- Never pull changes from an incorrect scenario into a correct one. If a change helps new scenarios but harms a locked baseline, it is wrong: revert or rescope.
- Prefer scoped behaviour (fixture-specific detection, diagram-type gates, narrow conditions) that improves target scenarios without changing locked baselines.

ER-specific verification (current work):

- After any ER change: regen `test.docx`, extract embedded SVGs, explicitly verify:
  - scenario 1 SVG contains `CUSTOMER` and `LINE_ITEM`
  - scenario 2 (matrix) SVG contains `ONE_ONLY` and `ONE_ONLY_B`

## Current state (Apr 2026): recent work, harness, and priorities

Recent sequence-diagram parity work (files):

- `extern/Naiad/src/Naiad/Diagrams/Sequence/SequenceRenderer.cs`
- `extern/Naiad/src/Naiad/RenderOptions.cs`
- `extern/Naiad/src/Naiad/Rendering/SvgForeignObject.cs`
- `extern/Naiad/src/Naiad/Mermaid.cs`

Outcomes:

- fonts and sizes aligned (message 16px; actor/note 14px) using Trebuchet-family stack globally
- fixed invalid SVG `font-family` quoting that made Word show “the picture can't be displayed”
- Mermaid-like self-message curve geometry using one cubic Bézier
- participant node sizing tightened (width ~150; tighter padding)
- spacing constants moved toward Mermaid defaults (margins, actor margin, message margin)
- note sizing/vertical placement improved for “Note over …”, incl. tighter boxes and darker styling for “Drop Zone Sequence” note

SVG comparison harness (`src/SvgHarness`):

- visual comparison (not SVG string comparison):
  - render Naiad SVG to PNG locally (Skia)
  - fetch Mermaid Ink PNG reference (`/img/... ?type=png`)
  - pixel-diff with small tolerance
- key files: `src/SvgHarness/SvgHarness.csproj` (references `SkiaSharp`, `Svg.Skia`), `src/SvgHarness/Program.cs`
- Mermaid Ink can be unstable (HTTP 503); prefer `--no-reference` when only checking parse/render
- full-document runs (e.g. `Stress.md`) are too slow for tight loops

`test.md` stress suite (in-scope types):

- flowchart, sequence, class, state, entity relationship, gantt, pie
- many other Mermaid families are fenced as ```text so they are documented but ignored by converter/harness for now

Next steps (priority order):

1. Flowchart LR + subgraphs: LR + `subgraph ... direction TB`, interior node centring, label wrapping, edges across subgraphs (missing-link cases); fix parser gaps first, then converge layout/routing to Mermaid (no approximation)
2. Sequence convergence: note placement, self-message label centring, spacing constants, validate in Word (not only PNG diff)
3. State parser gaps: at least one `stateDiagram-v2` with `note ... end note` fails to parse; extend parser to accept Mermaid syntax
4. ER / Class / Gantt / Pie: iterate with harness + Word; keep changes scoped to avoid regressions

## Fast iteration commands

SvgHarness (fastest “does Naiad render?” gate):

- Do not create lots of one-off single-diagram Markdown files or per-diagram output folders routinely.
- Prefer running against `Stress.md` or `test.md` and emit numbered outputs into one output folder.
- Only create a tiny standalone fixture for isolated repro; delete after issue is resolved.

Prefer no-reference mode:

```powershell
dotnet run --project .\src\SvgHarness\SvgHarness.csproj -c Release -- .\test.md .\out\svg-harness-testmd --no-reference --parallel 4
```

When Mermaid Ink is behaving:

```powershell
dotnet run --project .\src\SvgHarness\SvgHarness.csproj -c Release -- .\test.md .\out\svg-harness-testmd --reference mermaid-ink --parallel 4
```

DOCX regeneration (visual validation in Word):

```powershell
dotnet run --project .\src\md2docx\md2docx.csproj -- .\test.md .\test.docx
```

Version control:

- After every non-trivial update: make a Git commit with a message explaining the “why” (not only the “what”).
- If not a Git repo / Git not available: use Cursor/VS Code Local History / Timeline.

## “Regen” definition (mandatory)

When the user says **“regen”** it means:

- regenerate **both** repo-root verification DOCX files from latest built code (run in order; do not run tests as part of “regen”)
- if code changed in that turn, build as needed first

Commands (repo root) — run as **separate** invocations and check **exit code** after each (do not chain with `;` and ignore failures):

1. `dotnet run --project .\src\md2docx\md2docx.csproj -- .\test.md .\test.docx`
2. `dotnet run --project .\src\md2docx\md2docx.csproj -- .\stress.md .\stress.docx`

If either command prints `Error:` or returns non-zero, report it explicitly (including full stderr). A file lock must be a clean exit code `1`, not an unhandled `IOException` stack trace.

File locks (`md2docx`, interactive Windows session):

- On write failure due to a lock, `md2docx` shows a modal WinForms prompt naming the output file.
- **Default (no flag):** normal message-style prompt (no countdown, no auto-cancel). **Manual close** (OK or window X) retries the **same** output path until success.
- **Optional timer:** `--lock-timeout-seconds <n>` adds a countdown; **no action for n seconds** auto-cancels with exit code `1`.
- Non-interactive runs (`Environment.UserInteractive == false`): fail immediately with exit code `1` (no dialog).
- Do not silently switch to `stress-1.docx` unless the user explicitly asks for an alternate output path.

Other stress outputs (only when the user names them explicitly, not part of default “regen”):

- `.\SymmetryStressProgression.md` → `.\stress.docx` (overwrites flowchart routing iteration DOCX; use when working that fixture set specifically)

Wrapper:

- `src/md2docx/Program.cs` usage: `md2docx <input.md> <output.docx> [--profile <name>]`

Repo-root verification DOCX paths:

- `C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX\test.docx`
- `C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX\stress.docx`

## Inspect embedded SVG in DOCX (mandatory after every regen)

To compare what Word will actually render, extract embedded media from `test.docx`:

- copy `test.docx` → `test.zip`
- expand to scratch folder (e.g. `_docx_extract`)
- locate relevant `media/image*.svg` (commonly `*_docx_extract/media/image*.svg`)

PowerShell example (repo root):

- `Copy-Item .\test.docx .\test.zip -Force; Expand-Archive .\test.zip -DestinationPath .\_docx_extract -Force`

Then search by label text (e.g. `LINE_ITEM`, `CUSTOMER`, `appears_in`) and inspect primitives:

- crow’s foot toes should be `<path ...>` (curved)
- circle markers should be `<circle ...>` with centre on relationship line

After every regen, automatically:

- copy/expand as above
- locate `media/image*.svg`
- inspect emitted SVG (paths, markers, endpoints)

Do not commit `_docx_extract` or `test.zip` (scratch only).

## Known gotchas

Preview vs DOCX mismatch:

- preview is Mermaid.js; DOCX is Naiad
- preview wrong but DOCX right: fix `.vscode/settings.json` / Mermaid.js config, not Naiad
- DOCX wrong but preview right: fix Naiad renderers and/or `MermaidStyles.cs`

Mermaid init blocks:

- Naiad strips leading `%%{init: ...}%%` blocks (see `Mermaid.StripInitBlock`), so init-based theming will not affect DOCX output.

Word file locks:

- if regen fails with file-in-use, `md2docx` should show the lock prompt (see “File locks” under Regen); close Word and dismiss the prompt to retry the same path.

Word SVG limitations (DOCX embedding):

- CSS inside `<style>` may be partially ignored vs browsers; prefer inline SVG attributes for critical fills/strokes when chasing reliability
- SVG filters are highest risk; validate filter/shadow changes directly in Word

Tests:

- “run tests” refers to `tests/MDConverter.Tests` (pipeline). `extern/Naiad/src/Tests` (Playwright) is not included unless explicitly requested.
- Playwright failures often mean missing browser payloads, not broken Word output; skip/ignore unless asked to run/fix.
- `dotnet test` may be constrained by `global.json` runner choice (Microsoft.Testing.Platform). If tests fail with VSTest runner warnings, update invocation or migrate test runner (do not silently ignore failures).

## Quick verification checklist

Build:

- Naiad: from `extern/Naiad/src/Naiad/` run `dotnet build`
- MDConverter: from `src/MDConverter/` run `dotnet build`

Regen:

- from repo root (both):
  - `dotnet run --project .\src\md2docx\md2docx.csproj -- .\test.md .\test.docx`
  - `dotnet run --project .\src\md2docx\md2docx.csproj -- .\stress.md .\stress.docx`

Visual checks (DOCX in Word):

- flowchart: node fill `#ECECFF`, stroke `#9370DB`, subtle shadow
- sequence: participant boxes fill `#ECECFF`, stroke `#9370DB`; lifelines purple (`#9370DB`); message arrows dark (`#333`); dashed returns dashed
- ER: header band + alternating `#ffffff` / `#f2f2f2` stripes
- gantt: matches Mermaid default (blue/violet bars), not lavender

## Current working intent (“north star”)

- match the official VS Code Mermaid extension preview as closely as practical in Word/DOCX output
- match `mermaid.live` default behaviour upstream (colour + structure faithful to shipped defaults)
- keep preview configured to Mermaid default themes via `.vscode/settings.json`:
  - `markdown-mermaid.lightModeTheme`: `default`
  - `markdown-mermaid.darkModeTheme`: `dark`

## High-signal rendering specifics captured (do not regress)

Theme alignment:

- re-align to Mermaid default theme values (not per-diagram palettes); confirm via upstream `theme-default.js`
- `MermaidStyles.cs` should use `#ECECFF`, `#9370DB`, `#333333` where applicable
- restore Gantt CSS to official defaults (Mermaid default Gantt is not lavender)
- ER CSS uses official odd/even stripe fills (`#ffffff` / `#f2f2f2`) and standard border colour (`#9370DB`)

Flowchart line ends (Word-targeted):

- arrows (`-->`, `-.->`, `==>`) trimmed so stroke does not intrude into filled arrowhead (Word anti-aliasing can show stroke “through” head)
- circles (`--o`, `o--o`): no protrusion past filled circles; marker anchoring tweaks in `extern/Naiad/src/Naiad/Rendering/SvgBuilder.cs`:
  - `mermaid-svg_flowchart-v2-circleEnd`: `refX = 10`
  - `mermaid-svg_flowchart-v2-circleStart`: `refX = 0`
- crosses (`--x`, `x--x`):
  - X remains anchored at node edge
  - visible stroke stops short of X by tuned gap without moving marker
  - implemented as two paths in `extern/Naiad/src/Naiad/Diagrams/Flowchart/FlowchartRenderer.cs`:
    - trimmed visible stroke path (no markers)
    - marker-only path (`stroke="none"`) that reaches true endpoint
  - current tuning: `CrossStrokeGapPx = 10.2` (visible stroke end X≈198 when node edge X≈208.2 in default fixture)

Drop shadow and Word safety:

- Word can omit filtered elements entirely when using `<feDropShadow>` (shapes vanish; text/edges may remain)
- use SVG 1.1 primitive chain instead: `feGaussianBlur` + `feOffset` + `feFlood` + `feComposite` + `feMerge`
- Mermaid.js preview uses CSS `drop-shadow(...)`; do not expect identical implementation, only appearance parity

ER diagrams: crow’s foot parity rules:

- Naiad draws cardinality symbols directly (not SVG marker arrowheads); arrow-like appearance usually means geometry tuning, not palette
- centre prong tends to read like arrow tip in Word; removing it helped
- entity box sizing uses text-width estimates + padding constants in `extern/Naiad/src/Naiad/Diagrams/EntityRelationship/ERRenderer.cs`; Word often renders wider than estimates, so prefer slightly generous padding/min widths when matching `mermaid.live`

Additional ER parity rules:

- curved toes: implement prongs as SVG paths (quadratic/cubic Béziers), not `<line>`
- centre toe: do not draw separate centre prong; relationship line should read as centre toe
- marker placement along relationship:
  - circle centre (`o{` / `o|`) and bar midpoint (`|{` / `||`) lie on relationship line
  - circle and bar use same offset distance from entity edge (measured along relationship direction)
  - for curved relationships, choose point on curve at required offset (not projecting on straight chord)
- top-edge entry: when relationship enters entity top edge, outer toes must remain outside the box (above the edge in SVG Y); small clearance avoids Word artefacts
- circle fill: white `#ffffff` with dark stroke `#333333`

ER routing ownership (mandatory):

- treat Dagre layout engine routed polyline as authoritative
- do not override relationship endpoints in `ERRenderer` to “fix ports/slotting”; fix layout routing so `LayoutEdge.Points` matches Mermaid
- arc/bulge shaping belongs in layout stage: for 2-point routes (no dummy nodes), insert guide points for Mermaid-like `curveBasis` curvature during layout routing, not in renderer
- direction semantics: Mermaid ER marker semantics are often B-side → A-side; Naiad may reverse point order for marker correctness but must preserve the same underlying coordinates

## SvgAudit: fast ER iteration (SVG + PNG)

Use `SvgAudit` when you need rendered image feedback (not only SVG text).

Workflow rules:

- prefer running compiled executable vs `dotnet run` (faster; avoids apparent “hangs” from rebuild/large stdout)
- avoid PowerShell here-strings unless you will close them correctly; an unclosed here-string makes the shell appear “hung”

Practical pattern:

- write Mermaid input to a file (e.g. `.\out\er_input.mmd`)
- pipe raw text via `Get-Content -Raw` into `SvgAudit.exe`
- suppress stdout with `--no-stdout`

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

If output files exist but the console appears stuck, confirm the process ended and files are updated (timestamps). Avoid printing full SVG to the terminal during iteration.

## Flowchart routing + curve learnings (Apr 2026, standing rules)

Mermaid init config affects spacing (flowchart):

- if input includes one or more leading `%%{init: ...}%%` blocks:
  - Naiad must parse and apply relevant values to spacing (node spacing, rank spacing, diagram padding) rather than hard-coded constants
  - treat as global diagram values unless Mermaid specifies per-subgraph behaviour

Flowchart edge labels: support `A -- label --> B`

- parse text between `--` and `-->` as the edge label (e.g. `A -- Yes --> B`, `A -- No after retries --> B`)

Horizontal flowchart (LR/RL) attach/port rules:

- 2 → 1 fan-in (sources left, target right):
  - sources leave from right-centre of their boxes
  - target accepts two edges on left edge using symmetric slots (top/bottom, equidistant from edges)
- 1 → N fan-out (one source, many targets to the right):
  - outgoing edges leave from source right-centre
  - each target accepts at its left-centre
- decision diamonds are special: do not collapse multiple outgoing edges onto one shared source port; allocate distinct exits

Vertical flowchart (TD/BT): decision diamonds multi-outgoing:

- incoming edges:
  - if only one incoming, it may attach at a diamond vertex
- outgoing edges:
  - do not reuse a vertex for more than one outgoing edge
  - two outgoing: allocate to bottom-left and bottom-right faces (edge midpoints), not bottom vertex
  - three outgoing: bottom-left face, bottom vertex, bottom-right face (mirror to top side if fan-out is predominantly upward)
- if layout/slotting also computes diamond intersections “toward target”, ensure the multi-outgoing face-allocation override wins for outgoing edges

Decision branch renderer: do not snap ports that fight layout:

- do not snap targets to side-centre when decision diamond fans out downwards; branches must be allowed to enter destination nodes via top edge when layout requires it
- if dummy nodes split edges, apply port/attach overrides to real-node-adjacent segment (original endpoints) so destination segment honours intended port

Curves: do not mutate routed points for curveBasis in multi-point edges:

- Mermaid flowcharts use `d3.curveBasis` over routed polyline points
- do not inject extra endpoint stub points into multi-point routes before converting to curveBasis (changes spline behaviour; can confuse routed diagrams)
- if ER-style endpoint stubs are needed, apply only to 2-point edges rendered with dedicated cubic path; for routed/multi-point edges keep original points

Obstacles: connectors must not route through nodes:

- treat overlaps as routing failures even if curves look smooth
- prefer deterministic obstacle-aware routing over smoother overlaps
- validate in Word (Word can accentuate overlaps/clipping artefacts)

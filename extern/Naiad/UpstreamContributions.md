# Changes carried in this vendor tree (for upstream Naiad)

This file summarises edits made in the MarkdownToDocX fork of Naiad so they can be proposed back to [NaiadDiagrams/Naiad](https://github.com/NaiadDiagrams/Naiad) as pull requests or issues.

## 1. Flowchart and edge labels: native SVG `<text>` instead of `foreignObject`

**Problem:** Many non-browser consumers (notably Microsoft Word when embedding SVG) do not render HTML inside `foreignObject`; node labels appeared as empty boxes.

**Change:** `SvgForeignObject` now emits centred `<text>` elements with plain text derived from the previous HTML payload (tags stripped, entities decoded). Call sites (`FlowchartRenderer`) stay on `AddForeignObject`; behaviour is preserved for browser-oriented hosts that only needed readable text. For Word, `dominant-baseline: middle` tends to render high inside flowchart shapes; emitted `y` is shifted down by `max(2px, fontSize × 0.12)` (same heuristic as state diagram normal states). `RenderOptions.FontSize` is passed through `AddForeignObject` so SVG `font-size` matches measurement instead of always using 16px.

**Files:** `src/Naiad/Rendering/SvgForeignObject.cs`, `src/Naiad/Rendering/SvgText.cs` (`EscapeXml` exposed as `internal` for reuse).

## 2. Optional remote Font Awesome `@import`

**Problem:** Default SVG injected a `<style>` block with `@import` to a CDN; Word does not resolve that usefully and post-processors removed it anyway.

**Change:** `SvgDocument.FontAwesomeImport` defaults to `null` so no remote stylesheet is emitted unless the host sets it.

**Files:** `src/Naiad/Rendering/SvgDocument.cs`.

## 3. Gantt axis labels respect `axisFormat`

**Problem:** Axis ticks always used `MM/dd` regardless of Mermaid `axisFormat` (for example `%d` for day-only ticks).

**Change:** `GanttRenderer.DrawAxis` passes `GanttModel.AxisFormat` into a small strftime-like formatter (`%Y`, `%y`, `%m`, `%d`, `%H`, `%M`, `%S`, `%b`, `%B`, `%%`).

**Files:** `src/Naiad/Diagrams/Gantt/GanttRenderer.cs`.

## 4. Class diagrams: inheritance arrow semantics, styling, members

**Problem:** Inheritance/realisation needed correct triangle placement for `<|--` vs `--|>` (and `<|..` vs `..|>`): the hollow triangle must sit on the superclass/interface end Mermaid denotes in the syntax. Default class fill/stroke (`#FFFFDE` / `#333`) did not match Mermaid’s usual classDiagram palette (lavender boxes). Type-first members (`+String name`) were formatted like name-first (`+ name : String`). Compartment text sat tight under divider lines in Word.

**Change:** `ClassParser` records `InheritanceMarkerOnFromEndpoint` from the arrow token; `ClassRenderer` draws the hollow triangle on `FromId` when true and on `ToId` when false. Class box fill/stroke are defined in <c>MermaidDiagramColors.ClassDiagram</c> (sage green, distinct from other diagram types; annotations keep their own tints). `FormatMember` emits type-first text when the parse used that branch. Compartments gain a small inner top inset after horizontal rules.

**Files:** `src/Naiad/Diagrams/Class/ClassParser.cs`, `src/Naiad/Diagrams/Class/ClassRenderer.cs`, `src/Naiad/Diagrams/Class/ClassMember.cs`, `src/Naiad/Diagrams/Class/ClassRelationship.cs`.

## 5. Block diagram edges (`-->` / `---`) and line separation

**Problem:** Mermaid block edges could be rejected or mis-parsed because element lines and edge lines overlap syntactically, and “required whitespace” allowed newlines to act like separators.

**Change:** Block diagrams now parse `id --> id` and `id --- id` edges, and element lists require **inline** whitespace (spaces/tabs) so elements cannot silently span lines. This matches Mermaid’s line-oriented grammar.

**Files:** `src/Naiad/Diagrams/Block/BlockParser.cs`, `src/Naiad/CommonParsers.cs`.

## 6. Pie SVG numeric formatting parity

**Problem:** Pie charts differed from Mermaid SVG output due to numeric formatting (excess precision in HSL percentages, translate coordinates, and showData width precision).

**Change:** Pie renderer formats HSL strings and translate coordinates to Mermaid-like precision and uses the same showData width rounding as Mermaid’s verified SVGs.

**Files:** `src/Naiad/Diagrams/Pie/PieRenderer.cs`.

## 7. Flowchart node labels: strip Mermaid delimiter quotes

**Problem:** Rectangle and other bracket shapes capture text literally. For syntax such as `A["Markdown"]`, the captured payload included the delimiter double quotes, so labels rendered as `"Markdown"` instead of `Markdown`. That diverged from Mermaid’s reference SVG and mixed poorly with unquoted sibling nodes (`C[Render SVG]`).

**Change:** `NormalizeFlowchartLabel` in `FlowchartParser` removes a single pair of surrounding `"` characters (and minimal `\"` / `\\` unescaping) after capturing shape text.

**Files:** `src/Naiad/Diagrams/Flowchart/FlowchartParser.cs`.

## 8. Sequence diagrams: participant padding and message label spacing

**Problem:** Participant boxes used a fixed `100×40` size and column spacing independent of label length, so text sat close to the borders compared with Mermaid’s reference output. Message labels used an `8px` offset above the arrow, which looked cramped next to Mermaid’s looser spacing.

**Change:** Each participant column gets a measured box width and height (same heuristic as flowchart text sizing) plus explicit horizontal and vertical padding, with a minimum box size and a fixed gap between adjacent columns. Participant header `<text>` uses the same symmetric ASCII space pad (count configurable) on layout and paint so centred labels clear the stroke slightly in hosts like Word. Message labels sit `15px` above the arrow (was `8px`), and vertical message band spacing is slightly increased.

**Files:** `src/Naiad/Diagrams/Sequence/SequenceRenderer.cs`.

## 9. State diagrams: normal state label vertical centre in Word

**Problem:** Rounded state boxes drew labels at the geometric centre with `dominant-baseline: middle`. In Word (embedded SVG), that baseline often renders visually high inside the rectangle, leaving more gap below the text than above.

**Change:** For `StateType.Normal`, the label `y` is shifted down by `max(2px, fontSize × 0.12)` so centred text aligns more closely with Mermaid browser output and reads centred in Word.

**Files:** `src/Naiad/Diagrams/State/StateRenderer.cs` (`RenderNormalState`).

## 10. Entity–relationship diagrams: curved edges and label vertical centre

**Problem:** ER relationships were always straight segments, so diagonal links (for example ORDER–LINE_ITEM) did not resemble Mermaid’s curved routing. Entity header and attribute rows use `dominant-baseline: middle`; in Word those bands often render the text visually high or low depending on band height—offsets are tuned for DOCX embedding. Relationship labels must sit beside the arc without drifting too far toward the chord or the outer bulge.

**Change:** Axis-aligned chords (within a small epsilon) stay as `<line>` elements; otherwise a quadratic Bézier (`Q`) bows the edge with bulge scaled to chord length. Cardinality markers use tangents along that path at the endpoints (`markerDistance` tightened so markers sit nearer entity edges). Header and attribute `<text>` use the same downward optical nudge as state diagrams (`max(2px, fontSize × 0.12)`). Curve midpoint labels offset along the direction from chord midpoint toward the control (outer side of the bend). Relationship label centres use an extra small downward nudge matching the smaller label font size.

**Files:** `src/Naiad/Diagrams/EntityRelationship/ERRenderer.cs`.

## 11. Gantt charts: vertical centre for section rows, bars, and axis ticks in Word

**Problem:** Section bands, chart title, left-hand task names, and axis tick labels tended to render high relative to their row bands under Word’s SVG text metrics; task ID text inside thin horizontal bars tended to render low inside the pill.

**Change:** Section/title/task-name ticks use a small positive `y` nudge (`max(2px, fontSize × 0.11)`); axis labels use a proportional nudge based on their smaller font size. Task bar labels use a small negative `y` nudge derived from `fontSize − 2`.

**Files:** `src/Naiad/Diagrams/Gantt/GanttRenderer.cs`.

## 12. Per-diagram fill and stroke (not a single shared lavender)

**Problem:** Native renderers and the class-based `MermaidStyles` strings all used the same pair (`#ECECFF` / `#9370DB`), so flowchart, sequence, class, state, and ER outputs looked interchangeable in DOCX.

**Change:** Introduced `MermaidDiagramColors` with separate palettes (for example flowchart amber, sequence blue, class green, state violet, ER teal; Gantt section strip uses its own neutral tint). Each renderer’s `AddRect`/`AddPath` colours and the matching `MermaidStyles` CSS entries were updated to those constants.

**Files:** `src/Naiad/Rendering/MermaidDiagramColors.cs`, `src/Naiad/Rendering/MermaidStyles.cs`, `FlowchartRenderer.cs`, `SequenceRenderer.cs`, `ClassRenderer.cs`, `StateRenderer.cs`, `ERRenderer.cs`, `GanttRenderer.cs`.

---

## Trade-offs (flowchart icons)

Flowchart labels that use Font Awesome syntax (`fa:fa-*`) previously relied on HTML inside `foreignObject` (and optional CDN CSS). Plain `<text>` output cannot reproduce those icons as vector artwork without a different strategy (embedded paths, sprite, or opting back into `foreignObject` when icons are detected). MarkdownToDocX prioritises readable text in Word over icon rendering.

---

**Note:** MarkdownToDocX keeps DOCX-specific **image sizing** (`CalculateSvgExtents`, viewBox handling) in `MDConverter`; that remains consumer policy, not a renderer bug.

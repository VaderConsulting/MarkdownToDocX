# Brief: enterprise syntax the converter should accept

## Goal
Make the 14 scenarios below render. `enterprise.md` already declares this syntax valid in its Syntax decisions section, but the converter rejects it. Fix the converter, not the markdown.

## Scenarios to fix
A100, A101, A118, A119, A121, A122, A123, A124, A125, A126, A127, A128, A129, A130.

## Changes
1. **Identifiers (A100).** Allow hyphens, dots, underscores, digits and Unicode letters in ids, so `order-api` and `2026-orders` parse.
2. **Statement separators (A101, A123).** Treat `;` as a line break between statements, outside quoted text.
3. **Header line (A121, A123).** Recognise `enterprise`, optionally followed by a direction (`LR`, `TB`, `BT`, `RL`) and an optional trailing `;`, as an enterprise diagram before any fallback to Mermaid.
4. **Direction statement (A122).** Accept a `direction` line inside the diagram and inside a group, and pass it to layout.
5. **Frontmatter and init directives (A118, A119).** Read the `---` block and the `%%{init}%%` line. Keep the settings the renderer understands. Ignore the rest without an error.
6. **Styling (A124, A128, A129, A130).** Support `:::class`, `classDef`, `class`, `style` and `linkStyle`, and carry the result into the SVG. Naming an undeclared element gives a warning. An out-of-range `linkStyle` index gives one error and not a hard failure.
7. **Labels (A125, A126).** Accept single-quoted labels and markdown-string labels.
8. **Redeclared ids (A127).** Allow declaring an id again with the same type and merge the declarations. Keep the error for a conflicting type.

## Also check
A90 expects 4 errors but shows 3 placeholder lines. Find out whether two errors are being merged.

## Order of work
Do items 1 to 5 first, then 7 and 8, then 6, which is the largest. Run the A-scenarios after each item. Nothing that renders today may stop rendering.

## Rules
- Follow `CLOUD-AGENT-WORKING-PRACTICES.md`, including rules 20 to 27.
- Keep every rule generic. Nothing may depend on a scenario number, id or label.
- Do not change `enterprise.md` to make a scenario pass.
- Do not commit generated images.

## Done when
- The 14 scenarios render, or produce exactly the warning or error their Expected line states.
- The 17 deliberately invalid scenarios (A78 to A84, A86 to A94, A102) still fail as expected.
- All five test suites show no new failures against the baseline.
- The report lists the commits and the scenarios that still fail, with reasons.

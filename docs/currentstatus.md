# MarkdownToDocX — current solution status

Updated 10 October 2026. This is the single maintained status file for the
solution. Historical evidence is in the [grid layout review](grid-layout-review.md).

## 1. Repository and delivery state

| Area | Current state |
| --- | --- |
| Master | Baseline `f09cb89`; not changed or remeasured in this pass. |
| Grid work | PR #9, `cursor/many-to-many-routing-3834`; application fixes through `4c6505b` pushed, not merged. |
| Active checkout | Sibling `MarkdownToDocX-pr9` worktree. The original folder retains its documentation branch and supplied files. |
| Enterprise / ArchiMate | Selected parser, validation, notation and renderer code from PR #1 integrated into PR #9. Neither PR was merged. |
| Decisions | All six defaults approved. The held frame-midline rule is replaced by fitting frames on the shared grid and growing shared gaps. |

## 2. Current behaviour

- Markdown converts to DOCX through OpenXml and the WinForms executable's CLI.
- Flowcharts use native Sugiyama/grid layout with rounded orthogonal bends.
  Sloping diamond attachments use short terminal normals to meet their outlines
  squarely, as explicitly directed by the owner.
- Rank gaps now grow locally for routing and labels. Explicit larger
  `rankSpacing` settings remain minimums. Label gaps measure text height
  vertically and text width horizontally, with corner clearance reserved.
- Frames reserve title space, nested clearance and additional internal-return
  padding. Outside aligned columns or rows move together. Generic internal
  return cases are contained in their shared frame in all four directions.
- Clear forward doglegs between facing parallel ports are replaced by two
  bends when a shorter path is unobstructed. Narrow facing gaps grow by whole
  grid steps first, preserving straight terminal runs and aligned columns.
- Enterprise relationship breaks use plain text width without the HTML box
  minimum. Ellipse terminals meet the drawn ellipse rather than an inscribed
  circle; both changes have generic structural coverage.
- Relationship captions wrap and search clear straight-run positions. Nearby
  parallel captions stagger, and connector breaks follow their text. Exhausted
  local corridors retain visible fallback text; automatic gap growth remains open.
- Enterprise element, group, relationship and junction captions honour
  explicit break tags and newline escapes. Each line wraps independently;
  escaped literal backslashes survive. Multiline group tabs reserve height.
- Enterprise titles reserve canvas width for literal bold text, including
  punctuation and Unicode; narrow diagrams centre below wide titles.
- External junction captions choose nearby positions clear of routed lines,
  node bodies and other labels. The canvas includes the selected text bounds.
- Shared branching inputs centre between their targets when the other inputs
  are independent. Their unbranched tails align with their parent when clear,
  and the centred source uses free flow-facing ports for two-bend routes.
- Aligned vertical returns between non-diamond nodes use shared side ports,
  preserving clear orthogonal routes and square terminal runs through the
  later legacy loop-routing pass. Horizontal cyclic return routing remains
  incomplete and is outside this correction.
- Sequence row spacing accounts for notes, message labels and arrowheads;
  outer notes enlarge the canvas. This shared spacing work predates this pass.
- Enterprise blocks have native parsing, relationship validation and SVG/DOCX
  rendering, with accessibility metadata and explicit errors. This is partial
  support, not complete ArchiMate conformance. The supplied 130-block catalogue
  produces **94 diagrams and 36 source-preserving warnings**. Unsupported
  styling/configuration, quoted comment handling and rejected relationships
  remain; the provisional 3.2 relationship table needs further verification.
- Ordinary pie diagrams render natively with declaration order, duplicate-key
  handling, tiny-slice filtering, legends and accessibility metadata. Explicit
  pie configuration retains legacy fallback. Invalid pie statements report an
  error instead of silently producing a blank legacy image.
- Sequence, class, state, ER, Gantt, mindmap and git graph still use Naiad for
  rendering. Recognition coverage does not establish rendering parity.

The [approved decisions](layout-engine/APPROVED-GRID-DECISIONS.md) preserve the
owner's choices without modifying the supplied Instructions files. Engine
changes were checked for diagram-specific selectors; rules use topology and
geometry. Only the explicitly approved uniform-gap expectations were changed
in existing PR #9 tests during this pass.

## 3. Verified tests

Plain `dotnet test`, including build/restore, on pushed application commit
`4c6505b`. Baselines were measured before behavioural edits and retained.

| Suite | Master passed / failed | Starting PR #9 passed / failed | Current passed / failed |
| --- | ---: | ---: | ---: |
| Layout | 234 / 34 | 280 / 38 | 482 / 32 |
| Rule validation | 46 / 11 | 44 / 13 | 49 / 10 |
| Recognition | 932 / 0 | 932 / 0 | 1016 / 0 |
| Naiad adapter | 47 / 0 | 47 / 0 | 47 / 0 |
| Converter | 31 / 0 | 32 / 0 | 52 / 0 |

Counts are below or equal to master, but two master-passing Layout tests fail:

- `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules`: prior HTTPS
  stroke-distance investigation remains stopped after three attempts.
- `TryResolve_Sample03_EdgesKeepGroupBorderClearance`: one group-target route
  re-enters its target frame. Three attempts reduced six border failures to
  one; the bounded investigation is stopped and recorded in review section 18.

Four other failing Layout checks are absent from master and retain old branch
coordinate/policy expectations. `TryResolveLayoutGuidePoints_F113_ParallelBranchFanIn_UsesExteriorCorridor`
also regressed against the previous PR #9 checkpoint, although it already
failed on master. No master-passing rule-validation regression remains. The
nested-frame Layout and rule-validation regressions exposed during this pass
were corrected without changing their assertions.

Final logs, TRX files, exact failure-name comparisons, renders, DOCX outputs
and a complete picture manifest are local evidence under
`out/explicit-label-breaks/final/` (latest), with earlier evidence under
`out/rulebook-completion/a34ebb9/`. Recognition's twelve explicit fixture
writers remain excluded from ordinary execution. **Acceptance is incomplete;
passing-count targets do not establish visual correctness or merge readiness.**

## 4. Work to fix forward

1. Complete the remaining rulebook implementation: collision-free label plans
   across connectors, automatic growth when no clear label run exists, crowded
   internal-return tracks and remaining placement, port and crossing rules.
   Local gaps, frame fitting and sequence spacing are implemented with coverage;
   this does not complete every rule in sections 1–8.
2. Resolve the recorded Layout/rule-validation failures and visual defects.
   The two stopped investigations need a fresh approach; existing thresholds
   and unrelated assertions remain intact. No merge is authorised.
3. Expand Enterprise syntax/configuration and verify provisional relationship
   rules. Review catalogue warnings individually; keep invalid-input diagnostics.
4. Continue native adapters for the remaining diagram kinds, retaining fallback
   until each adapter has recognition, accessibility and conversion evidence.

The policy decisions are complete. Rulebook acceptance and full native renderer
replacement remain unfinished. The owner directed forward progress followed by
fixes; these limitations are recorded rather than treated as accepted defects.

## 5. Documentation ownership

| File | Purpose |
| --- | --- |
| [README](../README.md) | Entry point and current rendering paths. |
| This file | Current solution state and remaining work. |
| [Grid layout review](grid-layout-review.md) | Dated evidence, commits and bounded investigations. |
| [Approved decisions](layout-engine/APPROVED-GRID-DECISIONS.md) | Owner-approved defaults and replacement frame rule. |
| [Recognition specification](../spec.md) | Normative recognition requirements at this branch's existing path. |
| [Parity roadmap](../parity-roadmap.md) | Historical recognition roadmap and native rendering programme. |
| [Flowchart catalogue](../flow.md) | Inputs and scenarios, not engine selectors. |

Original `docs/Instructions/`, `docs/briefs/`, supplied Markdown and editor
settings are left as supplied. The old root status file remains superseded.

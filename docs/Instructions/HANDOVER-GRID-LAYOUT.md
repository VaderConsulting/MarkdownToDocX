# Handover: grid layout and routing work on MarkdownToDocX

Written 9 October 2026. Use this to brief another coding agent.

## Goal

Replace the curved flowchart routing with a grid layout. Every node sits on one shared grid. Every line is made of horizontal and vertical straight runs joined by rounded corners. There are no curves and no diagonals. The longest path through a diagram is one straight centre column. Lines never cross needlessly, never cut through boxes or group frames, and never run along a frame.

## Where the work is

- Repository: VaderConsulting/MarkdownToDocX. Master is at `f09cb89` after the docs tidy; the grid work is not on master.
- Working branch: `cursor/many-to-many-routing-3834`, which is pull request #9, last pushed commit `a63aeb1` (8 October, about 4:37 AM WST). It holds all earlier grid work. Pull requests #8 and #12 to #18 are superseded and closed.
- Authoritative specification: `LAYOUT-RULEBOOK.md` (attached). It is 129 lines: terms, sections 1 to 10, an on-hold list, and resolved conflicts. Its top block "Decisions applied" overrides any conflicting text below it.
- Working practices for the agent: `CLOUD-AGENT-WORKING-PRACTICES.md` (attached). Commit messages follow `docs/COMMIT-MESSAGE-STYLE.md` in the repository.
- Dave's marked-up pictures and the longer brief are in `/workspace/briefs/` on my computer (`webshop-target-rules.md`, `md2docx-grid-layout-brief.md`, and the `*-markup.png` files). They can be sent on request.

## Where it got to

Checked at the last report (8 October, 4:28 AM WST):

- Layout tests: 50 failures on the branch at one point, against 33 on master. After the curve-test rewrites, 5 failures that master does not have remain, plus 3 extra rule-validation failures (14 against master's 11).
- Done: curve-expecting tests rewritten to assert rounded straight runs; the main-path column test updated to a centre spine; decision labels sit on the first horizontal run leaving the diamond; non-group nodes sit one grid step outside a group frame; loop-back lines are no longer dropped by the layout step.
- Rulebook sections 1 to 5 are partly done. Sections 6 (labels), 7 (groups and frames) and 8 (sequence diagrams) are not done.

## Open failures to fix next

1. `Catalogue_MatchesMermaidRankOrderClusterAndSides`: rank mismatches. Accept only the reordering that rulebook section 2 calls for.
2. `TryLayout_Sample11_StoreClusterFitsMembers`: a node outside a frame overlaps it. Nodes outside a frame must clear it by one grid step.
3. `LiveSamples_ClearTheListedPictureDefects`: a corner arrowhead. Every line ends with a straight run, at least the arrowhead length, square to the target side.
4. `RenderToSvg_Sample03_StrokeQualityMatchesSharedRules`: an edge label too far from its line. A label sits one "n" width from its straight run.
5. `TryResolve_Sample03_EdgesKeepGroupBorderClearance`: diagonal segments through group borders. Lines cross a frame at right angles.
6. The 3 extra rule-validation failures.

## Defaults I chose (Dave has not confirmed these)

- An offset child in the next rank: leave the downstream side and Z into the child's upstream side while that side has a free port.
- Minimum straight run is 50 px for diamond branches before their first corner. Base rank gap is 2 grid steps.
- Tracks inside a gap are 6 px apart. A gap grows by a whole grid step when it runs out of tracks.
- A node that rejoins the spine stays on the spine.
- Branches go left of the spine and return tracks go right. A diamond side branch uses the side vertex first, then the lower edge centre. Frame padding is one grid step.
- Default 1 may turn some lines in the approved web shop picture into Z shapes. Check this with Dave.

## Rules that are on hold (do not build)

- A group frame running on the midline of the gap between members and outside nodes.
- Reserving that midline for the frame. Two groups side by side would share a midline and their frames would touch. Dave will redefine this.

## Working rules from Dave

- Rules must be generic. Use rank, parents, ports and box or group edges, never test names, node labels or sample numbers.
- Run only the tests that cover the change while working. Run all suites once at the end and compare against master (Layout 33, Rule validation 11, Recognition 11, Naiad 0, Converter 7).
- Commit and push after every working fix. Give up on a failing test after three attempts and record why.
- Commit messages, pull request titles and descriptions: imperative subject with no full stop, plain full sentences, no rule codes or sample names, no mention of people or agents. Earlier commits on the branch use codes like A8 and F34; do not rewrite pushed history without Dave's confirmation.
- Never commit `extern/Naiad/.editorconfig` or `extern/Naiad/src/Shared.sln.DotSettings`. Leave untracked files alone.
- No helper agents, no side experiments. Merging into master needs Dave's approval.

## Repository layout after the docs tidy

`README.md`, `flow.md`, `EIP.md` and `enterprise.md` stay in the root. Specs, catalogues and the single status file `docs/currentstatus.md` are in `docs/`. Update `docs/currentstatus.md` when the work moves on.

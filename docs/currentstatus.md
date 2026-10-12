# MarkdownToDocX - current status

**Last updated:** 13 October 2026. This is the only status file; it describes what is true today. History stays in git.

## What the product does today

- `md2docx` converts Markdown with Mermaid diagrams to DOCX.
- Flowcharts and enterprise (ArchiMate) diagrams use the native path: Mermaid recognition, one shared grid layout, native routing and an SVG render. If a flowchart fails natively, the converter falls back to the frozen Naiad renderer.
- The grid layout draws straight runs with rounded corners and a straight centre spine, and it avoids needless crossings. The rules are in `layout-engine/APPROVED-GRID-DECISIONS.md`; the dated evidence is in `grid-layout-review.md`.
- Enterprise diagrams use a `mermaid` code fence only. The `enterprise` fence tag is not supported.
- Every diagram uses the grid. There is no layout setting yet.
- All other diagram kinds still render through Naiad.
- Recognition reaches level L4 for the nine started kinds (flowchart, sequence, class, state, ER, pie, gantt, mindmap, git graph) and L1 for the rest.

## Working rules for the agent

1. Follow `Instructions/CLOUD-AGENT-WORKING-PRACTICES.md`.
2. Work in the main `MarkdownToDocX` folder only. Do not create other local folders.
3. Use one branch from master and one pull request. Do not merge; Dave approves the merge.
4. Run the baseline test suites first, so that no new failure goes unnoticed.
5. Work in small steps. After each working step, commit and push, update this file in the same commit, and start the next step from that pushed commit.
6. Never change an existing test to make it pass, except where a task below says to rewrite it. List clashes for Dave to decide.
7. After three failed attempts at a task, stop work on that task. Record what failed and why in this file, and move on to the next task. Do not stop the whole list.
8. Regenerate `flow.docx`, `enterprise.docx` and `EIP.docx` in the main folder with the built converter only after the current task list is complete. Dave reviews only these three files. Do not ask him to review anything else, and do not ask for review while work is in progress.

## Tasks, in order

1. **Enterprise syntax gaps — complete.** Catalogue checks confirm that A100, A101 and A118 to A130 render or diagnose exactly as declared, and the 17 deliberately invalid scenarios keep their error counts. DOCX fallbacks show each A90 diagnostic on its own line.
2. **Layout mode — complete.** F144 to F150 and A143 to A149 render with their declared modes and orthogonal routing. Explicit grid matches the default, unknown values warn once and fall back to grid, and diagram frontmatter overrides the run-wide setting.
3. **Compact placement — complete with recorded limits.** Disconnected pieces and fans pack into measured blocks. Scores subtract the forced crossing minimum. A64 is 570 x 420 at score 0.7; A110 is valid at score 381.2 with 255 crossings, forced minimum 243 and 33.3% empty. F153, A114, A117 and A142 retain line-rule failures. A117 is rejected for four routes with more than two corners plus placement-side violations.

Failed attempt: A114 stopped after three route repairs: excluding group frames changed nothing; extending rank repair reduced crossings but left the layout rejected and worsened A117; flexible perimeter repair enlarged other layouts and failed on empty local routes.

Failed attempt: F153 stopped after three repairs: a capacity-correct fan grid removed its fan faults but left three rejected routes and raised empty area; moving the whole split/rejoin block raised crossings from 1 to 12; moving only its final leg and merge raised crossings to 7. All three changes were discarded.

Failed attempt: A142 stopped with the valid best at 51 crossings. A reproduced progressive port-side candidate reached 29 crossings but remained rejected on eight routes: `empowerExt->dmeEtms`, `corporate->dmeEtms`, `dmeEtms->etmsSq`, `samsCore->Email`, `dailyBurn->sharepoint`, `cadMf->dmeEtms`, `hrApiCon->empowerExt` and `gisMain->otherGis`. Exhaustive rescoring was impractical; a deterministic intersection-ranked repair left four faults at 36 crossings; direct aligned routes were blocked by nodes. The rejected candidates were discarded.
4. **Old-layout Layout tests — complete.** The five stale checks now test the rulebook rather than old coordinates: Sample03 accepts only straight runs, rounded corners and crossing humps; InspectContent and ValidDecision check rounded orthogonal branches; TypeDecision checks its straight centre spine and rounded side branches; MessageFilter checks rounded orthogonal fan-out routes. All five focused tests pass.
5. **Placement hints - complete.** Node, group and line hints are parsed into typed contract data. Missing peers, cross-group peers and circular constraints produce nonfatal diagnostics; group ids are valid relationship endpoints. Source-ordered relative and push hints move nodes or whole groups and keep dependent chains. Hinted line sides reserve ports before automatic routing; `travel` selects a legal middle run and hinted routes may use the corners they need. The layout score counts bends and subtracts 30 points for each honoured placement hint and 15 for each honoured routing hint. Notes render as measured dog-ear boxes and plain connections render as undirected markerless lines. Both carry hints and remain outside the ArchiMate model. A154 to A169 parse and render; the earlier A166, A168 and A169 failures were the unsupported note and plain-connection statements.
6. **Visual fixes — stopped after three failed placement attempts.** The regenerated documents confirm that F111 renders as aligned three-by-three partner ranks, A14 follows the application-layer stack, and retry routes are orthogonal. A142 and its Web Shop, Order Manager and Payment Hub regions remain sparse; the current routed result is 7750 x 5870 with 51 crossings and 45.8% empty area. The placement attempts added crossings or failed the line rules, so rule 7 requires moving on.
7. **Remaining test failures — reviewed.** Recognition is 1074/0, NaiadAdapter 47/0 and MDConverter 60/0. RuleValidation is 71/11. Layout is 819/80. Remaining classes include A117 line rules, terminal perpendicular attachment, crossing bridges, partner-rank and fan ports, retry returns, parallel labels, rounded-box ports, route hints, old curve expectations and the rendered-SVG quality gate. Existing tests were not changed.
8. **Housekeeping — complete.** The vendored Mermaid tree has no tracked differences, package version 11.14.0 matches the pin, and `flowchart-elk` remains deferred.
9. **Final check — complete.** All five suites ran on Windows. The release converter regenerated `flow.docx`, `enterprise.docx` and `EIP.docx`; all three are valid DOCX packages with document XML and 148, 161 and 342 native drawings respectively.
10. **Code slop cleanup — complete with unsafe steps recorded.** Steps 1, 3 to 6, 8 and 10 are complete. They removed the Sample03 oracle and 251 duplicated allocator lines, isolated render state, narrowed fallback behavior, named geometry policy, documented the 21 route passes, and found no IDE0051, IDE0052, IDE0060 or CA1822 findings. Steps 2, 7 and 9 were not forced because they still changed behavior or required wider state and call-contract rewrites. All 321 metrics and suite counts stayed at 1074/0, 805/57, 49/10, 47/0 and 60/0.
11. **Bend tests — reopened.** The checker has 22 focused unit tests and evaluates checks (a) to (j). It applies the current convergence order, rule 2.10 adjacent-rank blocks, occupied top-centre handling, side-column and next-rank fan rules, and line-hint precedence. Complete rectangular blocks use measured cells, declared order and nested tracks; their port order follows the measured visual cross-axis direction so descending LR/RL ranks also nest without local crossings. Real 2x3, 3x3 and 4x2 layouts pass the checker. Decision diamonds share one port inventory across all connections: four vertices plus at least three evenly spaced ports on every edge, with no fixed maximum. Active edge ports redistribute from halves through thirds, quarters, fifths and further equal divisions. They preserve destination order to avoid local crossings and use the minimum-cost match to each line's diamond-intersection ray; complete routes still pass through real crossing repair. Final repairs remove convergence S-jogs and replace every legal offset two-corner decision branch with the mandatory one-corner route of rule 3.8, without adding a crossing or shared segment. Unlabelled many-to-many blocks now optimise crossings between lines that share a hub as well as independent line pairs. Track ordering tries swaps and stable insertions until a full pass finds no strict improvement. Invisible connectors are excluded because they render no line or label. Wrapped chains derive both gaps from measured label size and round up to whole grid steps. Labelled self-loops try all four sides and keep the shortest clear route among those with the fewest crossings; shorter-route checks preserve label room. Nearly aligned endpoints share one straight track instead of keeping a sub-grid dogleg, including in open layout where routing rules remain unchanged. Ignoring deferred task-12 scenarios A170 to A179, the catalogue reports 165 faults: g 10, h 55, i 97 and j 3. The ratchet has no new violation ids. Port-side repair tries candidates in deterministic quality order and stops at the first strict score improvement, so fractional ports do not multiply whole-layout scoring work.

Failed attempt: packing paired fans removed F113's 40 fan-side faults, but changed two passing `WideFanPortTests` from their required top/bottom exits to the facing-side exits required by rule 5.8. The candidate was discarded because the test baseline may not grow; the test and rule require opposite sides for the same structure.

Failed attempt: assigning bottom/top ports to every detected vertical next-rank fan lowered the catalogue from 232 to 224 but introduced three new fault ids. Restricting it to fan-outs raised the total to 234; restricting it to dense adjacent-rank blocks made no change. All variants were discarded.

Failed attempt: extending convergence repair to every ordinary fan-in reduced the total sharply but exposed new convergence and avoidable-crossing ids in A31, A113, A116 and A151. Whole-pass repetition and stricter crossing guards did not preserve the ratchet, so the variants were discarded.

Failed attempt: treating every same-rank fan-in only as convergence removed 107 faults but added five avoidable-crossing ids and one label-room id. Convergence and fan ports must be reassigned together; the candidate was discarded.

Failed attempt: doubling measured rank space for labelled Z routes removed six label faults but introduced decision, fan and crossing faults. The gap change was discarded.

Failed attempt: extending the final two-corner repair to every diagram and to tracks beside other routed lines left the catalogue total at 251 and converted one fan fault into a new avoidable-crossing fault (i 134 to 133; j 4 to 5). The ratchet rejected the candidate, so it was discarded.

Failed attempt: repeating that repair until no full pass lowered the score reduced check j from 4 to 3, but changed an acceptable A116 route into a new convergence fault and raised check h from 77 to 78. The total stayed at 251, so the candidate was discarded.

Failed attempt: requiring each track change to lower its own normalized crossing or corner count still transferred faults to other lines, producing six check-j findings and a new convergence fault. This was the third failed generic repair for avoidable crossings; the remaining four baseline check-j faults stay recorded while work moves to the next cause.

12. **Optional diagram legend — complete.** `config.legend` and `--legend` support off, on, six outside positions, placement-hint positions and Word table mode. Diagram settings override the run default. Enterprise/EIP legends reuse the notation renderer and list the visible node and relationship types in source order, including groups, notes and plain connections. Flowcharts use the same post-layout placement and table pipeline. The legend does not change main diagram geometry. A170–A179 and focused flowchart and DOCX tests pass.

The pictured route repairs are committed and their focused checks pass for A54, A72, A100, A112, A150 and A151. They add a 10-point bend cost, 20 points per detached end, deterministic alternative-port search, same-side return corridors, aligned simplification and circular-junction port normalization. Two follow-up candidates were rejected: exhaustive all-side port search did not recover the wider fan/rank tests, and guarding all terminal simplification restored some older routes but regressed A72 and A100.

Placement-hint follow-up: node and group placement completes before automatic routes are rebuilt; explicit line hints are restored after every route repair. A150–A169 pass a zero-disconnection regression check. A164 is 4200x1350 with 95 crossings (0 forced), 62 bends, 63/64 hints, 53.8% empty and score 1068.8; its layout takes 86 seconds. A165 and A166 honour all hints. A167 is 2190x1200 with 18 crossings and 59.6% empty (31/32 hints); A168 is 2520x2220 with 37 crossings and 59.4% empty (39/39); A169 is 1990x450 with 19 crossings and 33.2% empty (24/24). Shape and empty-area quality remain under review. Failed attempt: a route candidate ranked by crossings and bends reduced A164 to 59 crossings, but the layout became score-rejected; discarded. The existing `Score_SubtractsHonouredHintsAndCountsHintedBends` assertion still assumes a 2-point bend cost; the current rule charges 10, so that stale assertion remains failing and was not changed.

## Stretch goals

These must be done, but not yet. Start them only when Dave says so.

1. **Rulebook gaps.** Finish the label, group-frame and sequence sections of `Instructions/LAYOUT-RULEBOOK.md`. Do not build the two rules on hold (frame on the midline, reserved midline). Ask Dave about the six unconfirmed defaults and wait for the answer.
2. **Native render for the other kinds** (sequence, class, state, ER, pie, gantt, mindmap, git graph). Do one kind at a time, in its own commit, and keep the Naiad fallback until a kind passes its scenarios.
## Test results

Windows baseline measured on 11 October 2026 with `dotnet test`, before task 1 implementation.

| Suite | Baseline pass | Baseline fail | Current pass | Current fail |
| --- | ---: | ---: | ---: | ---: |
| `MermaidRecognition.Tests` | 1069 | 0 | 1074 | 0 |
| `MermaidRecognition.Layout.Tests` | 724 | 31 | 819 | 80 |
| `MermaidRecognition.RuleValidation.Tests` | 49 | 10 | 71 | 11 |
| `MermaidRecognition.NaiadAdapter.Tests` | 47 | 0 | 47 | 0 |
| `MDConverter.Tests` | 59 | 0 | 60 | 0 |

## Compatibility pin

```text
Mermaid package version: 11.14.0
Vendored source commit: 1bb6ba52937c21999e9ac89d11b82b0c56fd2739
Vendored source state: clean
```

If the vendored Mermaid changes, update this pin, the recognition fixtures and `spec.md` together.

## Where things are

- `spec.md` is the normative recognition and rendering specification.
- `Mermaid_Specification.md` holds the flowchart geometry and routing rules.
- `Rule Specification/` (repository root) holds the rule validation rules and scripts.
- `HANDOVER-GRID-LAYOUT-v2.md` describes the grid layout that is required, including what "tight" means.
- `Instructions/CLOUD-AGENT-WORKING-PRACTICES.md` holds the working rules for agents.
- `Instructions/LAYOUT-RULEBOOK.md` is the layout rulebook.
- `BRIEF-ENTERPRISE-SYNTAX-GAPS.md` and `BRIEF-LAYOUT-MODE.md` are the briefs for the two next build tasks.
- `BRIEF-PLACEMENT-HINTS.md` is the brief for hint parsing, placement, routing and scoring (rule 2.21).
- `BRIEF-CODE-SLOP-CLEANUP.md` is the brief for removing code clutter without changing output.
- `BRIEF-BEND-TESTS.md` is the brief for the unnecessary-bend checker, its baseline and the routing fixes.
- `BRIEF-LEGEND.md` is the brief for the optional diagram legend (task 12).
- `flow.md`, `enterprise.md` and `EIP.md` (repository root) and `enterprise/samples/` in this folder are the stress-test inputs. `sequence.md`, `class.md`, `state.md` and `entityrelationship.md` in this folder are the other catalogues.

## Maintenance rule

Update this file at the end of every completed task. Keep it short, and delete anything that is no longer true instead of adding history.
 New scenarios to check with rules 2.17, 2.18, 2.19, 4.7 and 4.8: Order Suite with Web Shop and Order Manager (aggregation), and Place Order with Basket Submitted, Pricing, Stock Check and Order Service.

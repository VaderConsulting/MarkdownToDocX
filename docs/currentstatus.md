# MarkdownToDocX - current status

**Last updated:** 12 October 2026. This is the only status file; it describes what is true today. History stays in git.

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
8. After each completed task, regenerate `flow.docx`, `enterprise.docx` and `EIP.docx` in the main folder with the built converter. Dave reviews only these three files, periodically and at the end of each task list. Do not ask him to review anything else, and do not ask for review while work is in progress.

## Tasks, in order

1. **Enterprise syntax gaps — complete.** Catalogue checks confirm that A100, A101 and A118 to A130 render or diagnose exactly as declared, and the 17 deliberately invalid scenarios keep their error counts. DOCX fallbacks show each A90 diagnostic on its own line.
2. **Layout mode — complete.** F144 to F150 and A143 to A149 render with their declared modes and orthogonal routing. Explicit grid matches the default, unknown values warn once and fall back to grid, and diagram frontmatter overrides the run-wide setting.
3. **Compact placement — complete with recorded limits.** Disconnected pieces and fans pack into measured blocks. Fan rows and wrapped chains use guarded real routes. Multi-edge forward ranks use distinct bottom ports, top ports and gap tracks while supported complete partner blocks keep their nested routes. Scores subtract the forced crossing minimum derived from complete adjacent-rank blocks. A64 now packs its ten measured artifacts into a 570 x 420 block at score 0.7. A110 is valid at score 381.2 with 255 crossings, a forced minimum of 243 and an accepted 33.3% empty share. A117 is valid at score 1229.8 after deterministic port-side repair. F153, A114 and A142 retain the recorded line-rule limits below. Rule 2.13 now also scores bends (2 points per corner) and rule 2.19 tries each flow direction when none is declared. Rule 4.8 (fewest corners) is new; the Order handling application layer scenario in enterprise.md checks it. Rule 2.20 (clusters) is new; the cluster scenario in enterprise.md checks it. Rule 2.21 (hints) is new; the hint scenarios in enterprise.md check it once the converter parses hints.

Failed attempt: A114 stopped after three route repairs: excluding group frames changed nothing; extending rank repair reduced crossings but left the layout rejected and worsened A117; flexible perimeter repair enlarged other layouts and failed on empty local routes.

Failed attempt: F153 stopped after three repairs: a capacity-correct fan grid removed its fan faults but left three rejected routes and raised empty area; moving the whole split/rejoin block raised crossings from 1 to 12; moving only its final leg and merge raised crossings to 7. All three changes were discarded.

Failed attempt: A142 stopped with the valid best at 51 crossings. A reproduced progressive port-side candidate reached 29 crossings but remained rejected on eight routes: `empowerExt->dmeEtms`, `corporate->dmeEtms`, `dmeEtms->etmsSq`, `samsCore->Email`, `dailyBurn->sharepoint`, `cadMf->dmeEtms`, `hrApiCon->empowerExt` and `gisMain->otherGis`. Exhaustive rescoring was impractical; a deterministic intersection-ranked repair left four faults at 36 crossings; direct aligned routes were blocked by nodes. The rejected candidates were discarded.
4. **Old-layout Layout tests — complete.** The five stale checks now test the rulebook rather than old coordinates: Sample03 accepts only straight runs, rounded corners and crossing humps; InspectContent and ValidDecision check rounded orthogonal branches; TypeDecision checks its straight centre spine and rounded side branches; MessageFilter checks rounded orthogonal fan-out routes. All five focused tests pass.
5. **Placement hints - complete.** Node, group and line hints are parsed into typed contract data. Missing peers, cross-group peers and circular constraints produce nonfatal diagnostics; group ids are valid relationship endpoints. Source-ordered relative and push hints move nodes or whole groups and keep dependent chains. Hinted line sides reserve ports before automatic routing; `travel` selects a legal middle run and hinted routes may use the corners they need. The layout score counts bends and subtracts 30 points for each honoured placement hint and 15 for each honoured routing hint. Notes render as measured dog-ear boxes and plain connections render as undirected markerless lines. Both carry hints and remain outside the ArchiMate model. A154 to A169 parse and render; the earlier A166, A168 and A169 failures were the unsupported note and plain-connection statements.
6. **Visual fixes — stopped after three failed placement attempts.** The regenerated documents confirm that F111 renders as aligned three-by-three partner ranks, A14 follows the application-layer stack, and retry routes are orthogonal. A142 and its Web Shop, Order Manager and Payment Hub regions remain sparse; the current routed result is 7750 x 5870 with 51 crossings and 45.8% empty area. The placement attempts added crossings or failed the line rules, so rule 7 requires moving on.
7. **Remaining test failures — reviewed.** Recognition is 1074/0, NaiadAdapter 47/0 and MDConverter 60/0. RuleValidation remains at its 49/10 baseline. Layout is 805/57; its failures include old curved/default route expectations, old shoulder coordinates, quality-gate failures and the recorded perpendicular-attachment faults. Rule 6 does not permit rewriting those tests, and the recorded investigations did not produce a safe shared fix.
8. **Housekeeping — complete.** The vendored Mermaid tree has no tracked differences, package version 11.14.0 matches the pin, and `flowchart-elk` remains deferred.
9. **Final check — complete.** All five suites ran on Windows. The release converter regenerated `flow.docx`, `enterprise.docx` and `EIP.docx`; all three packages open as valid ZIP containers with document XML and 148, 151 and 342 embedded images respectively.
10. **Code slop cleanup — step 1 complete; step 2 unsafe.** Step 1 removed the 220-line Sample03 oracle and its 22-line gate. All 321 catalogue metrics stayed identical, the five suites stayed at 1074/0, 805/57, 49/10, 47/0 and 60/0, and none of the 18 Sample03 tests depended on the oracle. Step 2 was reverted: A142's external-node search still improved after its three-pass cap, while A164's overlap separation still moved after its 32-pass cap. Removing either cap therefore changed behaviour instead of being a pure refactor. The reverted tree again matches all 321 baseline metric rows exactly.
11. **Bend tests.** Build the unnecessary-bend checker and tests set out in docs/BRIEF-BEND-TESTS.md, record the baseline per scenario, then remove the causes generically. Report the count per scenario before and after.

## Stretch goals

These must be done, but not yet. Start them only when Dave says so.

1. **Rulebook gaps.** Finish the label, group-frame and sequence sections of `Instructions/LAYOUT-RULEBOOK.md`. Do not build the two rules on hold (frame on the midline, reserved midline). Ask Dave about the six unconfirmed defaults and wait for the answer.
2. **Native render for the other kinds** (sequence, class, state, ER, pie, gantt, mindmap, git graph). Do one kind at a time, in its own commit, and keep the Naiad fallback until a kind passes its scenarios.
## Test results

Windows baseline measured on 11 October 2026 with `dotnet test`, before task 1 implementation.

| Suite | Baseline pass | Baseline fail | Current pass | Current fail |
| --- | ---: | ---: | ---: | ---: |
| `MermaidRecognition.Tests` | 1069 | 0 | 1074 | 0 |
| `MermaidRecognition.Layout.Tests` | 724 | 31 | 805 | 57 |
| `MermaidRecognition.RuleValidation.Tests` | 49 | 10 | 49 | 10 |
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
- `flow.md`, `enterprise.md` and `EIP.md` (repository root) and `enterprise/samples/` in this folder are the stress-test inputs. `sequence.md`, `class.md`, `state.md` and `entityrelationship.md` in this folder are the other catalogues.

## Maintenance rule

Update this file at the end of every completed task. Keep it short, and delete anything that is no longer true instead of adding history.
 New scenarios to check with rules 2.17, 2.18, 2.19, 4.7 and 4.8: Order Suite with Web Shop and Order Manager (aggregation), and Place Order with Basket Submitted, Pricing, Stock Check and Order Service.

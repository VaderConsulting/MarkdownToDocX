# MarkdownToDocX - current status

This is the only status file. It describes what is true now and what is still to do. Old history is in `MarkdownToDocx-Backup\Old briefs\currentstatus-history-20261013.md` and in git.

## Recover complete candidates during incremental scoring — 18 October 2026

Large cell candidates reroute their endpoint/obstacle/title-band/slot dependency
set and reuse unaffected route and crossing-pair costs. Full completion confirms
the winner; already examined assignments can use exact reference recovery when
the scoped shortlist fails. Small coupled inventories retain their complete
joint search under the existing twelve-line limit. This avoids the seven lost
moves in earlier prototypes without admitting a seventeenth candidate. Hard
path/diagonal/slot findings must be zero before accepting a completed candidate;
F58 recovers the existing legal alternative instead of retaining its old fault.

Profiled audit 90.35 s / A142 11.5310 s; unprofiled 64.27 s / A142 9.50791 s.
Exclusive routing falls 94.6291 -> 45.5790 s; frame search 4.0497 s, scoring
2.0663 s, hints 0.8827 s. The 45-second audit target remains open. Cells off
39.74 s / A142 13.6054 s with all 673 original summary rows byte-identical.
Cells on emits all 673 summaries, every hard gate zero, retained quality totals
unchanged. Every original-81 bend count matches, total1332; the worse-fifty are
unchanged before joint fan/chain candidates. See cell-incremental-recovery.md.
Production cells and spans OFF, rules, weights, limits and hints unchanged.
No DOCX included; nothing pushed.

## Review cell-induced 4.2, 4.11 and 5.8 increases — 18 October 2026

The thirteen affected scenarios (ten 4.2, four 4.11, three 5.8, overlapping)
are listed exhaustively in cell-quality-increase-review.md. All 24 current 4.2
findings in those ten cases have a concrete rejected straight replacement:
sixteen cross nodes, eight violate allocated terminal normals. These explain
current legal detours, not global placement optimality. Restoring pixel nudges
or forced centring would hide findings rather than satisfy the cell rules.

The remaining side increases are F136, A63, EIP236 and A165. Built candidate
traces show new shared tracks, 4.2 or 4.9 findings, or incomplete routing, so the
current bounded search retains the prior option. A165's wider legal frame also
changes the expected facing direction without changing its bottom/top sides.
These remain open candidate-quality cases, not proven unavoidable conflicts.
No premature-scoring or remaining unconditional centring cause was identified.
A trial straightening inside side-candidate evaluation changed none of the
thirteen counts and was removed. No functional engine change is retained.

Fresh audit: cells on 120.35 s / A142 49.9605 s, all 673 rows identical to the
pre-review capture; cells off 44.98 s / A142 13.5332 s, all 673 rows byte-identical
to the original baseline. Every legality/grid/slot/hint-accounting gate remains
zero. Quality totals unchanged: 4.2=52, 4.11=5, 5.8=3, 5.9=1234, shared=346.
The new review regression asserts final cell projections and hints and exact
restoration of each test-only substituted route. Rules, weights, limits,
production cells OFF, independent spans OFF and parked F32/EIP261 unchanged.
No DOCX included; nothing pushed.

## Split hint costs from nested route searches; reject the second scoring trial — 18 October 2026

The original-hint correction is already committed separately as 531ab82. New
optional nested profiler scopes distinguish exclusive frame, hint, scoring,
routing and diagnostic work without changing the engine when profiling is off.
Full before profile: 127.27 s / A142 49.6515 s, all 673 summaries and every
legality/grid/slot/hint-accounting gate zero. Top three exclusive costs are
routing completion 94.6291 s, frame search 3.8677 s and scoring outside routing
2.0412 s. Hint checks cost 0.6980 s across 49170 calls; A142 only 0.0017 s.
The inclusive final-scoring timer is 91.06 s because it includes route work.

A dependency-keyed hint cache matched full checks across owner/peer/push changes,
hint edits, rejected states and rollback. It reduced validation to 0.0997 s while
retaining 49170 calls. A scoped routing trial retained the existing terminal,
facing-side, straightening and track families instead of the earlier simplified
builder, cached unchanged line/pair costs, and fully confirmed its proposed winner.

The first revised unprofiled run reached 43.08 s / A142 7.38143 s, but five bend
cases worsened and F58 had an off-slot port/group. A correction pass using segment
and title-band dependencies and a final slot seal still failed: profiled 54.08 s /
A142 10.6593 s; 74 original-81 cases equal, seven worse, none better, 1332 -> 1343
bends. Worse: F59, F61, A18, A107, A141, EIP215, EIP246. Its exclusive routing cost
was 26.7401 s, frame search 3.6147 s, scoring 1.7022 s and hints 0.0997 s.

F58's exact cause is legacy centre-ray projection in ProjectToOutline: requested
slot Y=680 becomes 680.9467455621302 and rounds to 681. Persist has no geometry
provider. Repeating the allocator cannot correct the along-side drift. A pinned
fixture reproduces it. No checker was weakened and no shape-specific router
exception was added. A correct dependency closure and legacy slot projection
remain unresolved; neither is hidden by a changed limit or baseline.

Stopped and reverted all functional trial code, including hint caching and its
experimental tests. Only general profiler instrumentation and evidence are kept.
Restored verification: 311 focused and 60 cell/source regressions pass; cells-on
117.80 s / A142 50.2319 s, 673 rows identical to the retained checkpoint, all
legality/grid/slot/hint-accounting gates zero. Cells-off 40.43 s / A142 11.7259 s,
673 rows byte-identical to the original baseline. Logical hint mismatches zero.
Retained original-81 results remain 31 no worse than cells off (13 fewer, 18 equal),
50 worse. No speed improvement is claimed for retained code.

See cell-hint-scoring-cost-review.md, its exclusive-cost CSV and 81-case trial
CSV for the full before/after breakdown and gates. Rules, weights, sixteen-candidate
limit, hints, production cells OFF and independent spans OFF are unchanged.
F32/EIP261 remains parked. No DOCX files included; nothing pushed.

## Reject scoped cell scoring after its speed and regression check — 18 October 2026

Frame-fit fixes already have separate checkpoints: A165 6d55987, A164 210d917,
and original group hint anchors 531ab82. Final-layout scoring is separately
implemented in 2db6901; no duplicate frame-fit commit or history rewrite was made.

The incremental prototype cached unchanged line/pair costs, reallocated affected
port inventories and rerouted affected corridors, keeping other paths exact.
Only its selected winner ran full completion for confirmation. Its reference
score tests and all 313 experimental focused tests passed, but acceptance failed:
the full cells-on audit took 43.55 s / A142 15.2505 s; eight original-81 cases
worsened (F59, A31, A38, A107, A141, A161, EIP215, EIP246), one improved and
72 matched. Total bends rose 1332 -> 1346. F58 also had one off-slot port/group
(Persist's left cylinder port, 0.338 instead of 1/3). Shared pairs rose 346 -> 348
and 5.9 1234 -> 1244. No legality failure was introduced, but slot gates failed.

The prototype and its two tests were entirely reverted. No incremental scoring
change was committed; only this rejection report and measured CSV are retained.
Do not repeat the same simplified affected-route builder as a complete fix.
Its provisional choices failed to preserve the old successful final-layout moves;
full confirmation alone did not recover the previous candidate selection.
The detailed F58 drift mechanism is unresolved and was not worked around.

Restored verification: 311 focused and 60 cell/source regressions pass. The full
cells-on audit emits all 673 rows identical to the prior checkpoint, with every
legality, grid and exact-slot gate zero; 118.50 s / A142 50.1762 s. Cells-off emits
673 byte-identical original baseline rows, every zero gate retained; 39.41 s /
A142 12.0582 s. The retained original-81 result remains 31 no worse than cells off
(13 fewer, 18 equal), with 50 still worse. The trial's eight increases are gone.

The existing profile attributes 79.60 s to 3921 private full route completions,
including 37.50 s in nine A142 candidates. Repeated side/terminal searches dominate;
snapshot capture/restore costs only 0.0436 s, and the final-diagnostics marker
includes nested searches. See cell-incremental-score-rejected.md and its 81-row CSV.
The budget's AcceptedMoves includes earlier frame/placement work; the prior report's
45/5 split cannot alone prove which final cell-route candidates were accepted.

Stopped after the failed keep condition; no further optimization attempted.
Rules, weights, sixteen-candidate limit, hints, production cells OFF, independent
spans OFF and F32/EIP261's parked status are unchanged. No DOCX files included;
nothing pushed.

## Verify rejected completed candidates and measure the original 81 — 18 October 2026

The first finished-scoring stack replay names A58 and A138; the full earlier
census also names EIP84. All hit the same empty-trial-path Length exception,
fixed in 2db6901. Finished-layout scoring is already present there. New tests
in 48f6d6a now force rejection on each original source and compare authoritative
regions, both gap maps, allocated ports, route points, mutable frame edges,
node/frame bounds, painted/cached guides and diagnostics exactly.

Two additional snapshot gaps were reproduced and fixed separately:
12c33c2 restores mutable painted guide points/kinds; 60b5fb1 restores the
renderer cache contract, geometry key, original family and mutable values.
311 focused tests and 60 cell/source regressions pass.

All original 81 scenarios were rerun in both modes. Against actual cells-off
output, 13 have fewer bends, 18 equal and 50 more: 31/81 are no worse.
Total bends for the fixed set: off 1143, on 1332. The old Before column was a
previous cells-on capture and must not be described as cells-off. All 50 still
worse spend sixteen candidates. Forty-five accept an improvement but remain
above off; five accept none. The remaining historical groups are 24 shared-fan,
12 main-chain, 10 fan-in, and one each chord/junction/nested-group/partner-rank.
These are not declared unavoidable. Every case and changed link is recorded
in cell-finished-score-81-bends.csv and cell-finished-score-link-differences.csv.

Both full audits emit 673 summaries; cells-off rows are ordinally identical
to the original baseline, cells-on rows to the preceding checkpoint and the
profiled rerun. Every legality, grid, slot, hint-accounting and cleanup gate
is zero. Totals remain off/on 4.2 44/52, 5.9 1694/1234, 3.1 4/2, 4.1 0/0,
4.11 3/5, 5.8 0/3, shared 2089/346. Existing quality findings remain.

Unprofiled off audit 37.75 s / A142 11.3095 s; on 115.54 s / A142 48.4578 s.
Profiled on 127.34 s / A142 52.9946 s. Reconstructed nested completion timings
show 3921 private completed candidates costing 79.60 s, including nine A142
candidates costing 37.50 s. Repeated facing-side/terminal repair dominates.
Snapshot capture/restore totals only 0.0436 s. The final-diagnostics marker
includes nested candidate completion and is not pure diagnostic time.

The requested 45 s threshold is exceeded: stopped at profiling/reporting,
without subsequent layout optimization. Full gates, per-case causes and
inclusive timings are in cell-finished-score-review.md and the profile CSV.
The sixteen-candidate limit, rules, weights, hints, production cells OFF and
independent spans OFF remain unchanged. F32/EIP261 remains parked. No DOCX
files included and nothing pushed.

## Audit authoritative cell regions — 18 October 2026

Title width and multiline band height are already included before frame fitting
in f8596f3, including nested and empty frames. The scored port-side retry is
only an alternative; late direct cell-frame enlargement remains blocked.

The cell grid audit now independently validates exclusive integer rectangles,
positive contiguous spans, configured gap minima and finite grown gaps, and
forward-derived node/frame bounds. The old lattice gate remains unchanged with
cells off and is removed from cell-bound fault attribution. Explicit old/new
comparison finds no newly flagged catalogue scenarios in 662 bound cases.

309 focused tests and 57 cell regressions pass. Both full audits emit 673 rows;
cells-off rows are ordinally identical to the original baseline, cells-on rows
to 42f7629. Every legality, grid, slot, hint-accounting and cleanup gate is zero.
Cells-on audit 103.39 s / A142 38.4686 s; cells-off 40.56 s / A142 11.7005 s.
Quality totals remain off/on: 4.2 44/52, 5.9 1694/1234, 3.1 4/2,
4.1 0/0, 4.11 3/5, 5.8 0/3, shared pairs 2089/346. Known quality findings
and five unmeasured cases remain. See `cell-grid-audit.md` for every gate.
Production cells and independent spans remain OFF. Rules, weights, limits,
hints and production geometry are unchanged; F32/EIP261's refinement stays parked.
No DOCX files included and nothing pushed.

## Produce legal A112 routes before final recovery — 18 October 2026

The old cells-on readiness audit's two illegal paths were both in A112:
`split1 -> checkPolicy` crossed `checkReceipts`, and `route -> managerOk`
crossed `financeOk`. Direct producer regressions and writer tracing reproduce
both at automatic route reconstruction, including the first final-model rebuild.
The existing later node-interior recovery cleared them, but was too late to
establish that the producer itself was correct. These are circle-normal/visual
box visibility faults, not late frame resizing or the parked F32/EIP261 pattern.

Automatic cell reconstruction now offers the legal allocated-normal fallback
before publishing/caching its corridor. It extends an outward normal to a safe
enclosing-box exit, validates actual bodies/title bands and orthogonal interior
runs in the engine, and preserves exact candidate coordinates across cold/warm
caches. The old final fallback shares that implementation as a defensive backstop.
The checker is unchanged. Both direct A112 constructor tests fail before this
change and pass after it, without running final recovery. Current F32/EIP261
legality and slots also pass; their 4.2 refinement stays parked.

309 focused and 44 cell/source regressions pass. Both full audits emit all 673
summaries with every legality, grid, exact-slot, hint-accounting and cleanup gate
zero. Cells-on summary rows are unchanged from f8596f3; cells-off rows are
byte-identical to the original baseline. Exact cells-off geometry and ordered
diagnostics: changed=0, missing=0 in all 673 cases (35.225 s).
Cells-on audit 109.11 s, A142 45.9994 s; cells-off 38.44 s, A142 12.3808 s.
Quality totals and known findings are unchanged: on 4.2=52, 5.9=1234, 3.1=2,
4.1=0, 4.11=5, 5.8=3, shared=346; off 44/1694/4/0/3/0/2089 respectively.
The prior separate frame/title fix is f8596f3. The sixteen-candidate limit,
rules.yaml, weights, hints, production cells OFF and independent spans OFF are
unchanged. Details and the full gate table are in `cell-route-production-legality.md`.
No DOCX files included; nothing pushed. Speed/quality readiness remains open.

## Reserve measured frame titles before fitting — 18 October 2026

The initial builder already included measured title width and multiline height.
The late enlargement was a routing fallback moving a reserved strip away from
a line, after fitting, rather than missing text space. Width/band reservation
is now shared by member-backed, empty/nested frames and title routing obstacles,
using the existing renderer text metric and clearance without new sizing values.
The existing cell bypass remains; the late growth writer itself now rejects
bound models before touching output frames or ancestors. No measured catalogue
case needs late physical growth. Any future spatial change must be a complete
scored region/gap assignment and frame rebuild, never a direct frame resize.

An isolated unsafe replay reaches the old writer in F45, A58, A138, A167,
A169 and EIP84. A167 retains three frame overlaps and A169 one. The earlier
pre-bypass capture also had six A168 overlaps; corrected hints prevent A168
from reaching that writer in the current replay. A168 stays a regression.
Replay counts include discarded private route candidates, not drawn enlargements.

The protected census passes all 673 cases in 95.778 s, zero writer catches,
662 completed bound diagrams / 3,806 nodes; the same six open-mode cases stay
unbound and five native skips/empty cases remain. Bound-case bends are unchanged
in every case (total 3,365). All 673 cells-on summary rows match the previous
checkpoint exactly. Audit 97.62 s, A142 36.9755 s; every legality, grid, exact-slot,
hint-accounting and cleanup gate zero. Quality: 4.2=52, 5.9=1234, 3.1=2, 4.1=0,
4.11=5, 5.8=3, shared=346. Quality and speed remain open.

Cells-off: all 673 ordered summaries are byte-identical to the original baseline,
all previous zero gates zero. Audit 40.88 s, A142 12.7527 s. Exact geometry and
ordered diagnostics: changed=0, missing=0 across all 673 cases (39.079 s).
Production cells, independent spans, rules.yaml, weights, hints and the unchanged
16-candidate limit remain untouched. F32/EIP261 stays parked. All 309 focused
tests and 40 cell/source regressions pass, including eleven new title cases. Dedicated long-title,
nested-frame, legacy-writer control and full pipeline checks are described in
`cell-frame-title-reservation.md`, with the on/off gate table and replay evidence.
No DOCX files are committed; nothing pushed.

## A164 candidate diagnosis and original-hint correction — 18 October 2026

Reproduced all 97 historical single-set frame candidates. Every one retained
at least one overlap among bwmGrp, dsmGrp and collabGrp; no identical conflict
was present in all 97. The closest two retained bwmGrp/collabGrp, bwmGrp/createWs
and collabGrp/configure. The bounded joint search combines independently
nominated whole sets, including their external dependents, and reaches a legal
complete old-layout assignment at candidate 12. A165's closure-only checkpoint
is now separate (`6d55987`), before the A164 chain checkpoint (`210d917`). The
previous combined history is retained on backup branch
`checkpoint/cell-frame-combined-a31638e`; nothing was pushed.

**Correction:** the old active-only census did not prove that every original
hint survived. Source comparison found that cell hint placement had already
deactivated A164's `retire right-of define` and eight A165 member hints. These
were compatible constraints rejected because a member move changed an earlier
group envelope's starting row/column. Checkpoint `531ab82` completes the A165 hint fix by translating the
whole group and its hint-dependent closure back to its declared anchor within
the same private ownership transaction. Every original placement/routing hint
value and active state now matches a fresh source contract in both regressions;
no hint is dropped. This is not a rule incompatibility.

With the original hints retained, A164 examines six candidates, first legal two
(288.361 ms frame search); A165 examines seven, first legal three (1,032.288 ms).
All 254 active catalogue frame searches examine 1,523 candidates in 3.679 s;
maximum count seven, unchanged limit sixteen. Census: all 673 pass, zero writer
catches, 662 bound scenarios / 3,806 nodes; six open-mode cases remain unbound,
five native skips/empty cases unchanged. Total census 102.714 s. Only A164,
A165 and A168 change bend totals against the reduced-hint run (127->125,
51->56, 37->38), due to preserving compatible group/member constraints.

306 focused tests and 32 source/cell regression tests pass. Both original-source
regressions also require HintsHonoured == HintsTotal in the final model. All 673
cells-off summaries remain byte-identical; audit 39.42 s, A142 12.2841 s, every
previous zero gate still zero. Full cells-on audit:
673 summaries, 114.61 s, A142 48.9862 s. All legality, grid, exact-slot and hint
accounting gates zero. Quality: 4.2=52, 5.9=1234, 3.1=2, 4.1=0, 4.11=5,
5.8=3, shared tracks=346. Speed and quality remain open; no readiness claim.
The 16-candidate limit, rules, weights, production cells OFF, independent spans
OFF and parked F32/EIP261 work remain unchanged. Details and all candidate
residuals are in `a164-frame-fit-diagnosis.md` and its linked CSV evidence.

## Earlier reduced-hint readiness measurement — 18 October 2026

A164 and A165 now complete frame fitting within the unchanged 16-candidate
limit. Checkpoint `6d55987` moves complete direct/transitive hint-dependent
sets for A165. Checkpoint `210d917` carries private partial assignments through the existing chain depth,
and publishes only a complete legal winner. A164 examines 16 candidates
(first legal 12), A165 examines 7 (first legal 4). Final measured frame-search
times are 64.205 ms and 138.514 ms; all 254 catalogue invocations total 1.896 s.
No separate or larger frame-fit limit is proposed; no rules/weights changed.

The cells-on census passes all 673 cases, with 662 completed bound scenarios,
3,806 bound nodes, zero writer catches and all 662 logical hint checks true.
Six open-mode cases remain unbound with exact parity; five known native
skips/empty cases remain. 305 focused and 30 cell regression tests pass.
The full Layout suite has the same 216 failing test names as untouched
49f9da9, with 1,082 passes versus 1,073 before the nine added tests. The broader
route selection retains its two known F153/large-fan shared-track failures
(64 passes of 66); it is not all green.

The completed cells-on audit emits 673 summaries with zero grid, exact slot,
hint-accounting, title-band, unrelated-frame, missing/no-legal/illegal-path and
cleanup gates. Totals: 4.2=51, 5.9=1244, 3.1=4, 4.1=0, 4.11=3, 5.8=3,
shared-track pairs=345. Audit 93.42 s; A142 36.3332 s. Final-model candidate
scoring adds experimental runtime; frame fitting is only 1.896 s of the census.
Cells-off remains byte-identical for all 673 ordered summaries and exact
geometry plus ordered diagnostics (changed=0, missing=0). Serial audit 48.82 s;
A142 15.9000 s. All previous zero gates remain zero, with quality totals
unchanged: 4.2=44, 5.9=1694, 3.1=4, 4.1=0, 4.11=3, 5.8=0, shared=2089.

Production cells and independent text spans remain OFF. Production readiness
is not established: the original 81 bend increases are not fully diagnosed
(78 remain above the old baseline; 14 other cases are now above), despite
common-case total bends improving from 3,211 to 3,183. Final-route scoring,
late legal fallback, and empty-frame projection fixes are in checkpoint 2db6901, described separately
in `cell-frame-readiness-review.md`, with all gate totals and measurements.
Historical attribution is not proof that extra bends are required for legality.
F32/EIP261 remains parked. No DOCX files are included; nothing pushed.

## Latest cell placement policy review — 18 October 2026

Reviewed every converted placement path after the terminal-child correction.
Centre-only paths now bypass bound nodes; mixed paths retain ordering, measured
capacity and clearance. Straightening competes through complete whole-cell
routed candidates using the existing score and named limits. Rule 2.10 source
representatives order nominations, never define fractional node positions.

The individual checkpoints run from 645607b through b2b85a9. Each bypass/trim
has its own commit and census evidence. Full path decisions, every commit hash,
per-step bend/frame counts and timings are in `cell-placement-policy-review.md`;
individual Superseded notes remain in `cell-census-first-catch-review.md`.

Final checks: 296 focused passes and 12 real-source frame/placement regression
passes. Cells-on census: all 673 cases, zero writer catches, 3,806 bound nodes,
660 completed bound scenarios and the same two frame failures, A164/A165.
The six open-mode scenarios remain unbound and five native skips/empty cases
remain unchanged. Completed-case bends are 3,270 -> 3,211 (70 improved, 81 rose,
509 unchanged); failed frame cases have no route total. The census takes
34.4947 seconds. No full cells-on legality acceptance is claimed.

Cells-off: all 673 summary rows are byte-identical in original order; all prior
zero legality/slot/grid/hint gates remain zero. Audit 46.46 seconds; A142
15.8487 seconds. Quality totals remain 4.2=44, 5.9=1694, 3.1=4, 4.1=0,
4.11=3, 5.8=0, shared tracks=2089. The existing 25 audit failures/skip cases
remain; no checker or baseline was changed. Exact page geometry and ordered
diagnostics match the pre-change capture in all 673 cases: changed=0,
missing=0, 38.937 seconds. No speed improvement is claimed for this review.

Production cells and independent text spans remain OFF. `rules.yaml`, weights,
hint behaviour and unrelated cells-off output remain unchanged. F32/EIP261
stays parked for routing work. A164/A165 and experimental layout quality
remain open. No DOCX files were included in these checkpoints; nothing pushed.

## The project

`md2docx` is a C#/.NET converter that turns Markdown with Mermaid diagrams into DOCX. Flowcharts, enterprise (ArchiMate 3.2) and EIP diagrams use the native path: Mermaid recognition, one shared grid layout, native routing and an SVG render, with the frozen Naiad renderer as fallback when a flowchart fails natively. All other diagram kinds still render through Naiad. Recognition reaches level L4 for the nine started kinds (flowchart, sequence, class, state, ER, pie, gantt, mindmap, git graph) and L1 for the rest. Enterprise diagrams use a `mermaid` code fence only. Layout modes, placement hints, notes, plain connections and the optional legend (`config.legend`, `--legend`) are supported.

`Instructions/LAYOUT-RULEBOOK.md` is the only layout and routing spec, and it wins over every other document, comment or test. After task 2 it is generated from `Instructions/rules.yaml`.

## Diamond route candidate repair

F17 offers straight connections at the diamond tips in the initial search. F30 offers extended edge-normal terminal candidates, and bend counting includes diagonal-to-axis transitions. Final painted guides now come from the final model routes for declared links. Single tip ports are recognized by the allocation audit. The six diamond regressions pass as part of the focused selection below. Score weights and baseline rows are unchanged.

## Cheapest legal layout repair — checkpoint, 16 October 2026

### Latest accepted speed checkpoint, 17 October 2026

`7b1eb66` restores the audit target without changing geometry. The final audit
emitted all **673 summaries in 40.57 seconds**, with **A142 at 12.0016 seconds**.
The whole command, including its explicit build and test discovery, took
**46.537 seconds**, also below the 48.78-second target. The audit now explicitly
builds and runs **Release**, matching the ARM64 DOCX converter; `-Configuration
Debug` remains available for investigation. These are Release measurements,
not a claim that the old Debug execution path now meets the time target.

The main regression was introduced by `6372bd4`. A142's Debug native completion
stage measured 11.123 s at 2b299a6, 12.872 s immediately before 6372bd4 and
28.124 s at the accepted pre-fix head. The added final facing-assignment and
settled-facing searches accounted for 6.762 s and 9.544 s respectively in the
latter trace. They validate complete assignments, but repeatedly searched
identical allocated terminals and remeasured restored baseline models. The
same added work affects other diagrams. The earlier 85.04-second audit used
Debug; the 12-second DOCX measurement used Release. Mixing those configurations
made the audit/conversion gap look like a separate routing path.

The engine now caches immutable built side-search paths against exact ordered
obstacle geometry, endpoints, sides, normals, shapes, direction and hint state.
Geometry changes clear that cache. It also carries forward the authoritative
baseline score after an accepted option or an exact rejected-option rollback.
Candidate order, complete-assignment checking, legality checking and the named
16-move, 3-operation and 12-line limits are retained. No new limit, weight or
scenario-specific engine condition was added. Most of the audit time reduction
comes from matching the converter's optimized build; route and score reuse
also removes engine work.

The Release engine matched **all 673 saved geometry snapshots exactly**, with
zero changed or missing cases (37.637 s). The focused routing regressions have
34 passes and the same two known failures, F153 and LargeFanOut; the original
23 passes and both illegal-path tests are retained. All 22 selected frame,
packing, label-index and guide-cache checks pass. Audit totals are unchanged:
**4.2 = 39, 5.9 = 1700, 3.1 = 4, 4.1 = 0, 4.11 = 3, 5.8 = 0 and shared-track
pairs = 2083**. Every hard legality gate remains zero. The five existing failed
native layouts remain explicitly unmeasured; none is silently counted as a
successful zero-finding layout. Baseline rows, rules.yaml, weights and hint
behaviour are unchanged. Audit exit status remains nonzero for the existing
quality/shared-track and failed-native cases.

Validation ran in the isolated accepted worktree `out/audit-speed-work`, so the
uncommitted coordinate prototype was not built or altered for this work. The
speed-only staged Git tree was checked against that validated tree before the
commit. The uncommitted migration diff also matched its saved pre-work diff
exactly after ignoring Git index hashes and shifted hunk line numbers. The
root working tree still contains the unfinished migration; do not
use it as the accepted audit checkout. Logs are under
`out/route-cost/speed-final-*`, with exact comparison evidence in
`speed-release-exact-geometry.log`. Nothing was pushed and the DOCX files were
not committed.

The migration is diagnosis only. [The migration diagnosis and proposed
plan](route-coordinate-migration-diagnosis-2026-10-17.md) lists all recorded
failures and measures the four surviving attempt-3 snapshots. Attempts 1 and
2 did not retain their coordinate arrays, so their displacement magnitudes
cannot honestly be recovered without replaying them. No migration was rerun.
The plan requires immutable stage/candidate traces and a full consumer/order/
rounding inventory before another coordinate edit. The prototype remains
uncommitted; A142's 10-second target, F153, the large fans and remaining shared
tracks are still open.

### Verified side-routing checkpoint, 17 October 2026

The side-routing fixes are committed in `6372bd4`, after the separate frame
invariant commit `afe393f` and shared-track checkpoint `06ede99`. Empty hinted
frames are also covered at repair boundaries and rollback in `ca1b9b9`.
The side-fix acceptance audit emitted all 673 summaries in 82.39 seconds. Totals
are 4.2 = 39, 5.9 = 1700, 3.1 = 4, 4.11 = 3, 5.8 = 0 and shared-track pairs =
2083. All hard zero gates remain zero; 4.1 is zero and 4.9 remains 6. No scenario
has a larger 4.11 or 5.8 count than c6605bd. A142 took 38.8553 seconds, so speed
is still open. The original 23 regression passes are retained; the current
focused run has 34 passes and two known failures, F153 and the synthetic large
fan. Both illegal-path cases pass. F148 and A147 now pass their named regressions.

The side search validates complete built assignments and every displaced port,
refreshes side choices after growth, and sizes channels for their prospective
facing assignment. The shared legality fallback is offered when the provisional
terminal family cannot build that assignment. Frame-outline regressions are
rejected. The original baseline rows, rules.yaml, score weights and hints are
unchanged. The latest rerun and subsequent work are recorded below.

### DOCX conversion profiling and exact-geometry speed fix

`03fe4ca` reuses the scoped guide-search context and invalidates it when endpoint
or obstacle bounds change. Canvas label queries index the same sampled neighbour
points instead of repeatedly scanning them. All 673 page-geometry snapshots
matched exactly, with zero changed or missing scenarios. Five new equivalence
and cache invalidation tests pass. `60fcd77` fixes apphost-based worker launching
in the profiling harness.

The complete whole-document profile measured flow.md at 30.513 seconds,
enterprise.md at 83.156 seconds and EIP.md at 46.848 seconds. Enterprise previously
took 454.900 seconds in the same profiling mode. The current ten slowest diagrams
are A142 12.008 s, A165 10.411 s, F153 6.300 s, A114 5.583 s, A115 3.926 s,
A167 3.923 s, A163 3.785 s, A164 3.636 s, F151 3.029 s and A166 2.959 s.
The raw timings are in out/route-cost/away-docx-diagram-times-after.jsonl.

The first speed attempt, indexing sampled neighbours alone, reduced point
checks but still timed out at 60 seconds. The profile exposed the other cost:
each per-edge lookup created a fresh context and reran the full guide search.
Reusing that context reduced the isolated A114 conversion to 7.496 seconds and
its canvas label measurement to 2.097 seconds. In the warmed document conversion,
A114 fell from 371.998 to 5.583 seconds. The existing
Render_FormatV1Diagram_BreaksStrokeAroundEdgeLabels test still fails for the
same stroke-gap assertion on the unchanged checkpoint and the new code; it is
not fixed by this performance work. Audit speed and the A142 layout search are
still open.

### Real empty-group packing hang and earlier accepted audit

`58822a5` fixes a real enterprise diagram with nested members, a right-of child
hint and an unrelated empty group. Empty-frame placement previously accepted
touching borders, while sibling packing required clearance and repeatedly moved
the unlocked members beside a locked hint. Both steps now use the same clearance
calculation. Explicit empty-frame hints retain their exact gap. Five new public
parser/layout tests pass, together with all 12 existing frame invariants.

The first unhinted reproduction settled normally. Adding a real child hint
reproduced the hang. The first clearance fix changed A167's hinted empty frames;
preserving explicit hint spacing corrected that. The final exact comparison
covered all 673 scenarios with zero changed or missing snapshots (76.284 s).
The two older SiblingFrameClearanceTests failures were reproduced with the
unchanged group-frame implementation; they remain open rather than being hidden.

Before the speed correction, the audit emitted all 673 summaries in **85.04 seconds**. A142
took **39.7853 seconds**. Totals are **4.2 = 39, 5.9 = 1700, 3.1 = 4, 4.11 = 3,
5.8 = 0 and shared-track pairs = 2083**. All hard legality gates, including
illegal paths, title bands, frame overlap, missing routes and off-slot ports,
remain zero. The focused side/cost regression selection retains 34 passes and
the same two known failures; the original 23 passes and both illegal-path cases
remain passing. Baseline rows, rules.yaml, weights and hints are unchanged.
The side-fix checkpoint satisfies the requested comparison against 06ede99.
F148 and A147 each now have zero shared-track pairs and one crossing; their
earlier named shared-track regression failures are resolved. F153, the large
fans and shared tracks elsewhere remain open. Engine changes were checked for
scenario-specific conditions; none were added.

### Page-down migration — uncommitted and not accepted

The accepted pre-migration code was 58822a5; the speed-only checkpoint is now
7b1eb66 (with separate documentation/tool commits).
The source working tree now contains an incomplete page-down prototype. It
builds, but it must not be used as an accepted converter or described as passing
the audit. The totals above belong to the accepted checkpoint, not this prototype.
No migration commit was made and nothing was pushed.

The exact pre-migration snapshot contains all 673 scenarios. Before the axis
edits, the conversion speed fix and packing fix each matched every snapshot
exactly. The migration has not met that requirement. Its three attempts were:

1. Remove the placement-to-routing flip and repair-time mirrored frame builder;
   make grid frame heights, box boundaries and the shared SVG mapper page-down.
   All five focused snapshots (F17, F30, F61, F153 and A167) changed.
2. Migrate initial port ordering and normals, diamond edge geometry, frame
   restoration and straight-route side selection. Four snapshots changed before
   A167 threw while measuring an empty side-search route.
3. Correct the straight-attachment helper, migrate convergence/branch and local
   route-repair compass definitions, move title strips to the northern edge and
   remove selected renderer reflections. Empty route measurement no longer
   throws there, but F17, F30, F61 and F153 still changed. The run was stopped
   while A167 had not finished, since exact acceptance was already disproved.

The three-attempt stop rule applies: do not repeat another isolated sign swap.
The remaining work must migrate each complete geometry consumer, including the
frame-title crossing exemption, compound/group bounds, route obstacle helpers,
all terminal and fallback builders, joint-channel comparisons, numeric candidate
ordering and canvas-extents propagation. The SVG renderer still passes its old
maximum-Y extent through several helpers even though its point mapping has been
changed; enterprise-specific mapping is not migrated. The optional Sugiyama
backend still needs its one conversion boundary, and frame-invariant test
expectations and the axis-sensitive checker consumers still need migration.
Dagre placement direction transforms and vendor Sugiyama internals were retained.

The focused mismatch is geometric, not just floating-point noise: F17 moves RN
from page X=293 to X=323 and moves the box-to-rounded-box route from Y=128 to
Y=248. It also exposes an exact-double difference at the circle endpoint
(127.99999999999999 versus 128). Neither difference is rounded away or accepted.
A complete post-migration 673-case match and audit have therefore not been
claimed. The uncommitted prototype is a starting point requiring further work.

The accepted ARM64 converter was published before the migration under
out/away-stable-converter. The WinForms Converter.exe entry point uses those
accepted Release dependencies for the requested DOCX regeneration, rather than
the experimental Debug engine. Its layout DLL SHA256 is
4E485D2708A67394D66F059730CD6FF70DB2E91E2ED781887557EF2C2785C4FC.

### Regenerated review documents

The three root DOCX files were regenerated with the accepted ARM64 WinForms
application at
C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX\out\away-stable-converter\Converter.exe.
The commands, run from the repository root, were:

```powershell
& .\out\away-stable-converter\Converter.exe -in flow.md -out flow.docx
& .\out\away-stable-converter\Converter.exe -in enterprise.md -out enterprise.docx
& .\out\away-stable-converter\Converter.exe -in EIP.md -out EIP.docx
```

All three commands exited successfully. Times were flow 29.026 s, enterprise
91.285 s and EIP 49.715 s. ZIP/XML inspection found no rendering-failure
placeholders in flow or EIP. Enterprise has the 18 deliberately invalid cases
A78-A84, A86-A94, A102 and A130, each retaining its validation diagnostic;
they are input-validation examples, not new layout crashes. The output checks
and command timings are under out/route-cost/away-docx-*. These are actual review
documents in the repository root, not test outputs. None is committed.

The following performance and quality paragraphs are older checkpoint history.

The full audit emits all 673 summaries in 54.14 seconds, down from 84.52 seconds at 404ce4c but above c6605bd's 48.78 seconds. A142 takes 24.7305 seconds in the final audit (23.90 seconds in the isolated prepared-geometry profile), still above its 10-second target. The five existing failed layouts remain F65, F70, F87, F88 and A01; the 668 successful layouts are measured, and failed layouts are not reported as successful zero-finding diagrams.

Current totals: 4.2 = 39, 5.9 = 1788, 3.1 = 4, 4.1 = 0 and shared-track pairs = 2271. Every hard legality gate remains zero. All 13 previously passing regressions and the F29/F119 illegal-path tests pass. F61 remains at zero bends and crossings. Eight new regression cases pin the recovered crossing ceilings. Baseline rows, rules.yaml, weights and hint constraints are unchanged.

The final track seal no longer accepts partial separation that adds crossings. Candidate searches reuse immutable prepared obstacle/title-band geometry, path costs and interactions. Port-distribution checks no longer run the full unrelated audit per candidate. Geometry tests reject distant obstacles before group traversal, and the audit runs once in a single host. Full checker comparisons and candidate-equivalence tests preserve the original geometry checks. No new arbitrary limit or scenario-specific engine condition was added. Port snapshots now preserve exact fractional slots, and lone projected ports retain their exact side centre. The audit summary includes off-slot groups, every checker rule, hint accounting, failed scenario names and precise case timings. All hard zero gates, including off-slot groups, are now zero; 3.3/5.2 remains at its old count of 5, while the newly visible 4.11 and 5.8 quality totals are 57 and 48 against 6 and 7 at c6605bd. These quality increases remain open.

The 5.9 definition is unchanged: rescoring c6605bd with the current checker reproduces 1794. The increase at 404ce4c was geometric, dominated by partial track separation. Fifteen scenarios still exceed their own old crossing counts, including cases that replace illegal shared tracks or node crossings. Their remaining excess is not declared unavoidable. The complete top-20 attribution, all remaining cases, performance explanation and validation are in [the crossing investigation](route-crossing-investigation-2026-10-16.md).

Open work: audit speed against c6605bd; A142 against 10 seconds; the remaining individual crossing increases; F153's bend/crossing regression; shared tracks in F148, A147 and the 20-way and 30-way fans. The original 17 named/structural regressions still have four known failures: F148, A147, F153 and the synthetic 20-way fan. Column-grid fan placement awaits agreement. The checkpoint is authorized with these open items. The code checkpoint was not pushed. The three DOCX files were regenerated afterwards and remain uncommitted. A follow-up checkpoint records the open shared-track and side-distribution increases before further engine changes.

## Where things are

- `Instructions/LAYOUT-RULEBOOK.md`: layout and routing rules. `Instructions/pictures/` and `briefs/`: the pictures it refers to.
- `spec.md`: recognition and rendering spec. `Mermaid_Specification.md`: Mermaid syntax and upstream rendering reference only.
- `Rule Specification/` (repository root): rule validation files and scripts.
- `Instructions/CLOUD-AGENT-WORKING-PRACTICES.md`, `Instructions/AGENT-ONBOARDING.md`: agent working rules and onboarding.
- `Instructions/code_rules.md`, `Instructions/test_rules.md`: audits of the rules the code and tests enforce.
- `flow.md`, `enterprise.md`, `EIP.md` (repository root) and `enterprise/samples/`: stress-test inputs. `sequence.md`, `class.md`, `state.md`, `entityrelationship.md`: other catalogues.

## Working rules for the agent

1. Follow `Instructions/CLOUD-AGENT-WORKING-PRACTICES.md`.
2. Work in the main `MarkdownToDocX` folder only. Do not create other local folders.
3. Use one branch from master and one pull request. Do not merge; Dave approves the merge.
4. Run the baseline test suites first, so that no new failure goes unnoticed.
4a. Any single check has a hard ceiling of 60 seconds. Treat every check over 10 seconds as a speed fault to record and fix; aim for a few seconds. Converting one document must stay within 2 minutes, with under 30 seconds as the target.
5. Work in small steps. After each working step, commit and push, update this file in the same commit, and start the next step from that pushed commit.
6. Never change an existing test to make it pass, except where a task below says to rewrite it. List clashes for Dave to decide.
7. After three failed attempts at a task, stop work on that task. Record the lesson under "Do not repeat" and move on to the next task. Do not stop the whole list.
8. Regenerate `flow.docx`, `enterprise.docx` and `EIP.docx` in the main folder with the built converter only after the task list is complete. Dave reviews only these three files. Do not ask him to review anything else, and do not ask for review while work is in progress.

## Open tasks, in execution order

1. **Many-to-many route ordering — revalidated.** The insertion-order search in `FlowchartManyToManyRoutes.ApplyHorizontal` uses endpoint-median seeding, alternating median sweeps and transpose, then incremental sifting until a full pass gives no strict crossing-score improvement. Historical results were 4x4 509 ms / 42 crossings / 0 overlap before, 393 ms / 36 / 0 after; 6x6 2,249 ms / 240 / 0 before, 1,242 ms / 225 / 0 after; 8x8 24,470 ms / 812 / 0 before, 3,642 ms / 784 / 0 after. The cache-off 12x12 baseline took 13m54s and failed the overlap assertion (`Expected overlap 0, got 3192`); its crossing count was not recorded. The 12x12 run completed in 68,071 ms with 4,356 crossings and 0 overlap. On 14 Oct 2026, a 4x4 stage trace showed route construction at 36 crossings, repair passes 0–28 retaining 36, the horizontal track search retaining 36, and repair pass 29, coupled repair and final validation also retaining 36. The search now rejects a candidate unless its affected-route crossing delta improves, except when it repairs a pre-existing endpoint attachment. Final runs measured 4x4 123 ms / 36 crossings, 6x6 558 ms / 225, and 8x8 659 ms / 784; each had zero overlap and finished under 1 second. Incremental score checks matched the full score. F136 retained three legal crossings and the shared-hub interaction test passed.

2. **Single rules source - open. Do this next.** All layout and routing rules live in one machine-readable file, `docs/Instructions/rules.yaml`. Each rule has an id, a short text, a category, its precedence position in the rule 9.6 order, its kind (legality, routing fault or quality), its score weight and its parameters, such as the 50 px minimum straight run, 10 points per bend and 20 points per crossing. `LAYOUT-RULEBOOK.md` is generated from the file and is not edited by hand after that. `Rule Specification/ruleset.json` and `evaluator-map.json` are regenerated from it too: they keep their syntax and recognition rules and drop their own geometry rules, which are saved to `MarkdownToDocx-Backup\Old rules`. `Build-Ruleset.ps1` is updated to match. The code reads every parameter and weight from the file, with no magic numbers, runs its stages in the precedence order the file defines, and each stage and check names the rule ids it implements. The tests are driven by the file: every rule id has at least one check, the checker runs every legality and routing-fault rule against every scenario in `flow.md`, `enterprise.md` and `EIP.md`, and a missing or unknown rule id fails the build. `FlowCatalogueRuleValidationTests` and `FlowMarkdownRuleValidationTests` change with the regenerated files. Placement and routing algorithms stay in code; the file holds the rule definitions, parameters, precedence and checks. rules.yaml, rules.schema.json and Build-Rulebook.ps1 exist (built from the rulebook on 13 Oct 2026). From now on, edit rules.yaml and regenerate the rulebook; do not hand-edit LAYOUT-RULEBOOK.md. Remaining in this task: wire the code and tests to read the file, and regenerate Rule Specification ruleset.json and evaluator-map.json.
    - **Step (not started): refactoring target 1: Incremental score in candidate loops.** Location: `FlowchartPortSideRouteRepair.Apply` (calls `FlowchartLayoutScore.Measure` before and inside each candidate, lines 22 to 109), also `FlowchartConvergenceRouteRepair`, `FlowchartFanPacking`, `FlowchartGroupMemberPacking`, `FlowchartDisconnectedPacking`. Problem: every trial of one route re-scores the whole diagram. Evidence: `Measure` runs `DiagramGridQuality.Collect`, `CountCrossings` (every route pair, O(E^2) pairs times segment pairs), bends, disconnections, hints and empty area; candidates per route are 4 exit sides x 4 entry sides x ports x track positions, so one pass is about O(E x C x E^2). Change: add a score context built once per pass that caches per-route crossings, bends and faults, and updates only the pairs that touch the changed route (O(E) per trial). Keep `Measure` as the reference and assert equality in a debug test. Gain: estimate 10x or more on diagrams with 60+ lines; this is the main cost in the repair stage. Risk: medium; the cached score must equal the full score exactly. In this task: the weights come from `rules.yaml`, so the cache must read them from there. Task 3: stage 5 (coupled repair) uses this context.
      Done for this step: one commit, suite counts and the metric table unchanged, the cached score equals `Measure` in a debug test, and timings recorded before and after.

3. **Pipeline order per rule 9.6 - open. Do it after task 2 and before any further layout tuning.** Today `DiagramGridLayout.Apply` runs automatic placement and compaction before placement hints, then a long chain of route builders and repairs. Routing hints are reapplied at the end as a patch, `MAX_SEPARATION_PASSES` caps separation at 32 passes, the renderer still draws curved fans (rule 4.1), and `EnterpriseFlowchartEdgeRouter` re-routes after the shared stage.
    - **First, fix the tests** listed as Conflicts in `Instructions/test_rules.md`: rewrite or delete tests that require hop arcs, curves or diagonal runs, F61 diagonal two-by-two lines, five-slot ports, F125 merge centring and labels beside lines, so they assert the rulebook instead. Close the gaps that let faults pass: a failed layout must fail the test, a missing baseline must fail rather than be rewritten, every scenario must be scored, the F136 bend allowance is removed, and hard-coded pixel checks are replaced by grid-relative or rule-based checks. Add tests for rulebook rules that have none (`test_rules.md` section 3). Update code comments that cite `Mermaid_Specification.md` layout sections to cite the rulebook rule; the known case is `src/MermaidRecognition.Layout/FlowchartDecisionShoulderLayoutPostProcessor.cs` line 13 (16.4.2).
    - **Then do refactoring targets 4 and 5 (below), then target 8 as the repair stage**, and rebuild the pipeline as explicit stages, listed once in one place and named after their rules: (1) grid and measured geometry, rules 1.1 to 1.7; (2) placement: hints (2.21), then 2.18, 2.7, 2.4 to 2.6 and 2.8 to 2.10, 2.17, 2.16, 2.20, 2.15, 2.11, 2.12, and 2.19 candidates compared by score; (3) joint port allocation (4.7); (4) routing: line hints, then 3.8, 4.11 and 5.8 (2.10 for its blocks), then 4.8, then 5.9, with back-edge rules 5.3 and 5.4 above generic choices; (5) coupled repair that keeps hints and higher-priority rules, built as refactoring target 8; (6) legality validation (section 10) last.
    - Honour hints from the start and never reapply them as a patch. Use no pass caps: each loop stops when a pass makes no improvement or repeats an earlier state. Remove curved fan rendering. Fold the enterprise repairs into the shared stages; enterprise may add notation only, never re-route. Delete or update stale comments and tests, including curved fans, F35 edge-midpoint ports, the edge-midpoint fallback and the removed rules 3.4, 3.5 and 5.5.
    - **Tests:** a recorded trace shows the stages run in the 9.6 order; a hinted node and line keep their hints through every later stage; no rendered path contains a curve; enterprise routing equals shared routing apart from notation; the full checker runs over `flow.md`, `enterprise.md` and `EIP.md`.
    - **Refactoring target 4: Replace static render state with a render context.** Location: `FlowchartSharedEdgeRouter._LastJustifications`, `FlowchartSvgLineJump._LastJumps`, `FlowchartEdgeRouteResolver._CachedRouteContract` and `_CachedRouteGuides`, `FlowchartSugiyamaSvgRenderer._ActiveRenderSession`. Problem: static state blocks parallel tests and parallel diagrams. Evidence: the Layout suite runs on one core for most of its time. Change: one `RenderContext` object passed through layout, routing and render. Gain: tests and documents can render diagrams in parallel (estimate 3x to 6x wall time on 8 cores); results stop leaking between renders. Risk: medium; many signatures change. In this task: the stage list takes this context from the start.
    - **Refactoring target 5: Split `DiagramGridLayout` by stage.** Location: `DiagramGridLayout.cs` (5,166 lines, 114 static methods, 249 LINQ calls), `DiagramGridLayout.Apply`. Problem: placement, ports, route building, track separation and repair all sit in one file and one long method. Change: one class per rule 9.6 stage, each naming its rule ids, with `Apply` reduced to the stage list. Replace LINQ chains in inner loops with indexed loops only where a profile shows a cost. Gain: supportability; each stage can be tested alone. Risk: medium, but low if done as a move without logic change. In this task: this is the shape task 3 needs, so do it as its first commit after its test fixes.
    - **Refactoring target 8: One table for repair passes.** Location: the repair chain in `DiagramGridLayout.Apply` (about 21 repair classes) and `EnterpriseFlowchartEdgeRouter.SeparateVisibleTracks`. Problem: repairs run in a fixed list, some twice, and enterprise runs them again. Change: one ordered table of repairs with rule ids, run until no pass improves the score or a state repeats. Gain: removes duplicate passes (estimate 20 to 40 percent of repair time) and makes the order visible. Risk: high; it changes when repairs run. This is stage 5 of this task; do not do it separately.
      Targets 4 and 5 are move-only commits with suite counts and the metric table unchanged and timings recorded; target 4 comes first, then target 5, then the stage rebuild.
    - **Target 4 checkpoint:** `FlowchartLayoutResult` now owns its `FlowchartRenderContext`; the layout engine accepts and preserves a supplied context, route-guide caching and routing diagnostics use it, and the renderer scopes legacy helpers to that context. The focused context-ownership test passed (1/1, 4.3 s). A wider quality-gate selection took 35 s and had 31 passes plus the two already-recorded known-bad picture/title-clearance failures; it is not acceptance evidence and must not be repeated as a single run.
    - **Target 5 complete:** `DiagramGridLayout.Apply` delegates to six named stages in `DiagramGridLayoutStages.cs` (grid preparation, node placement, group frames, route construction, route repair and final hint application). The stage bodies preserve the previous call order and algorithm. Group-frame helpers live in `DiagramGridLayoutGroupFrames.cs`, node placement and compaction in `DiagramGridLayoutNodePlacement.cs`, port allocation and endpoint attachment in `DiagramGridLayoutPortAllocation.cs`, route construction in `DiagramGridLayoutRouteConstruction.cs`, and track separation/obstacle repair in `DiagramGridLayoutRouteRepair.cs`. `DiagramGridLayout.cs` is 855 lines, down from about 5,179. The layout build passed in 2.3 s and `FlowchartSugiyamaLayoutEngineTests` passed 11/11 in 0.35 s. Pipeline reordering and rule-order trace remain part of the task 3 stage rebuild; these moves did not change behavior.
    - **Target 8 complete:** The route-repair operations run from one ordered table that records their rule ids. The table repeats full passes only while the YAML-weighted optimization score strictly improves, restores the best routes on a stalled pass and stops when a route state repeats. Incremental candidate scoring is checked against the full score, including the optimization score. The port-side search retains its interaction filter, scores every remaining legal candidate incrementally and chooses the lowest total rule 2.13 score with a deterministic tie-break. Final 4×4, 6×6 and 8×8 runs took 305 ms / 36 crossings, 396 ms / 225 crossings and 843 ms / 784 crossings; all had zero overlap, and incremental scores matched full scores exactly. The focused routing-hint suite passed 5/5. Selective gap-growth now leaves hinted lines and their endpoint nodes out of automatic moves, then reapplies each line hint after inserting space. Against d3d37e2, excluding A142, A163 and A164, the current catalogue had 213 passes and 115 failures versus 212 passes and 116 failures at baseline. A31 and A169 are new failures; A50 now passes; F136 and A178 pass with their newly added zero rows. A163 passed at baseline in 16.8 s but exceeded 30 s in the current tree. A142 and A164 remain excluded as the previously recorded timeouts. The 12×12 result remains 68,071 ms, 4,356 crossings and zero overlap.
    - **Done when** all of the above pass in one PR-sized change, shared engine first, with results reported here.

4. **Remaining refactoring - open. Do it after task 3.** Each target is one commit with suite counts and the metric table unchanged and timings recorded before and after.
    - **Refactoring target 6: Render once, do not re-parse the SVG.** Location: `FlowchartSvgLineJump.Apply(string Svg)` and `FlowchartRenderedSvgDocument` (27 regex uses). Problem: the renderer writes SVG, then parses it again with regex to find line crossings. Change: compute jumps from the route geometry before writing SVG; keep the regex parser for the quality gate tests only. Gain: small in time, but removes a fragile string round trip. Risk: low to medium. Hop arcs conflict with the rulebook and task 3 rewrites those tests first.
    - **Refactoring target 7: Split the other catch-all files.** Location: `FlowchartSharedEdgeRouter.cs` (119 methods), `FlowchartSharedPictureCleaner.cs` (100), `FlowchartRenderedSvgQualityGate.cs` (116), `FlowchartSugiyamaSvgRenderer.RenderToSvg`. Change: split by rule family once task 2 gives the rule ids. Gain: supportability. Risk: low if done as moves.

5. **Bend tests - open. Do it after task 4.** Goal: no unnecessary bend in any diagram of `flow.md`, `enterprise.md` and `EIP.md`; the target is zero.
    - **Current state:** the checker `UnnecessaryBendChecker` exists with 22 unit tests and evaluates checks (a) to (j). Ignoring A170 to A179, the catalogue reports 165 faults: g 10, h 55, i 97 and j 3. The ratchet has no new violation ids.
    - **Definition:** a bend is unnecessary when a legal route with the same end boxes or frames, end sides and ports is better. Every check is geometric, uses the measured layout, measures in grid steps and rule constants, and uses no fixed pixel size.
    - (a) More corners than the fewest for the chosen sides: 0 when the ports line up (rule 4.2), 1 for perpendicular sides with each port on the other's axis, 2 for same-direction sides or offset opposite sides. Extra corners pass only when a line hint needs them; test this by removing the hint and checking again.
    - (b) A zig-zag: two consecutive corners that cancel, where a straight or one-corner route is clear.
    - (c) A jog: a sideways step under one grid step where moving one end box by whole grid steps, without overlap, would align them. Report it as a placement cause too.
    - (d) A detour more than one grid step longer than the shortest clear orthogonal path between the same ports.
    - (e) A bend that only avoids a crossing within the forced minimum.
    - (f) A diamond branch that leaves a non-vertex, leaves the bottom vertex and turns sideways, is an outer branch that does not leave its side vertex, or reaches a lower offset target with more than one corner or without entering as rule 4.11 sets (rules 3.8 and 4.11).
    - (g) A corner or side step within the label's length plus both label gaps, or a label on a corner, that moving the label to a straight run or adding space would remove (rules 1.7, 6.1 and 6.2).
    - (h) A converging line that runs along the gap to the target's top, or enters the top with two corners, when a one-corner entry is legal and no line hint names the route (rule 4.11). Lines in a rule 2.10 block are checked against rule 2.10 instead.
    - (i) A fan line to targets wholly beyond one side of the source that leaves the far side, top or bottom, enters at a box corner, shares a track, or has tracks that are not nested, when no line hint names the side (rules 5.2 and 5.8). Next-rank fans are checked as bottom-to-top lines.
    - (j) A hop, or a crossing that swapping the pair's tracks, ports or sides would remove while keeping sections 3 to 5, rule 4.3 and every line hint (rule 5.9). Crossings that no swap removes are scored, not failed.
    - (k) A route whose first or last segment runs against its declared exit or entry side, when a legal route using those sides exists.
    - Not unnecessary: back-edge corners within rule 4.3 when no shorter legal route exists, corners a hint needs, corners forced by a box or frame in the way, and the corner that lets a group end meet its frame square-on (rule 4.9).
    - **Checker:** no scenario ids, labels or node names in code. For each routed line, read the end sides, ports, points and obstacles (all boxes, frames except those that contain an end, and all other lines). Search for a better legal route: remove corner pairs, try a straight route, try one- and two-corner routes, and run a shortest orthogonal path search on the grid of box edges, frame edges and ports. A route is legal when it keeps rules 4.1 to 4.4, meets its ports at right angles, crosses no box or frame, adds no crossing above the forced minimum and shares no port. A line fails when a candidate has fewer corners, has the same corners and is more than one grid step shorter, or removes a jog or zig-zag. No tolerance may let a real defect pass. Report the diagram, the line (source, target, label), the failed check, the corners and length, and the better route as points. Use no caps on lines or search steps and no time-out that passes; build one obstacle index per diagram, or reuse task 1's route index (target 3) and task 2's score context (target 1).
    - **Tests:** `UnnecessaryBendCheckerTests` uses small hand-built cases: an aligned straight line, a perpendicular one-corner line, an offset two-corner line, a zig-zag, a jog, a route with a box in the way (accepted), hint corners accepted only with the hint, the returned better route accepted by the checker, decision branches, two lines merging into End, a column fan to three targets, and 2x3, 3x3 and 4x2 blocks (pictures `Instructions/pictures/markup-*.png`). `UnnecessaryBendCatalogueTests` runs the checker over all three catalogues with the converter's own layout. `UnnecessaryBendBaseline.txt` is data: one line per scenario with its id, count and line ids.
    - **Execution update:** the catalogue check is split into one independently selectable test per diagram. The first full pass ran 331 cases: 119 failed and two hit the 30-second watchdog. The 119 failures group as 103 unnecessary-bend baseline regressions, five failed layouts (F65, F70, F87, F88 and A01), and 11 missing baseline entries (F136 and A170–A179). Each non-timeout case finished within the per-case cap. F136 and A178 were individually scored with zero findings and now have new zero rows in the baseline; no existing baseline row was changed. A170–A177 and A179 scored 20 findings in total and remain unbaselined. The timed-out bend checks were A142 (166 elements, 180 relationships, 21 groups and 13 locations; 7,750 × 5,870 px in the prior measurement) and A164 (61 declared entities and 64 placement/routing hints; 63 of 64 hints were honoured in the prior measurement). Do not use either timeout as an accepted result.
    - **Ratchet:** the baseline may only shrink. More violations or a new violation id fails, a zero-baseline scenario must stay clean, and the test prints a table per scenario by check.
    - **Steps:** fix causes generically in order of count, one commit per cause. After each fix, re-run the scenario table, confirm that overlaps, crossings and scores did not get worse, and shrink the baseline in the same commit. Never loosen the checker; fix a false positive by making it more exact, with a unit test.
    - **Done when** the baseline is empty or each remaining item is listed with its reason.

6. **Recorded layout faults - open. Do it after task 5; most may clear on their own after tasks 3 and 5.** Re-measure each after task 5 and fix the rest generically, with no scenario-specific code.
    - **Document conversion speed fault (2026-10-14):** the initial `enterprise.md` conversion took about 20 minutes, exceeding the 2-minute hard limit by 10x. Time each Mermaid diagram in `enterprise.md`, `flow.md` and `EIP.md` independently with a 60-second watchdog, sum the times, and identify the slowest twenty and the stage with the greatest aggregate cost. Do not use a full-document conversion as a per-diagram timing substitute. Fix the dominant shared stage while retaining the crossing/overlap benchmark gate and same-or-better diagram scores; record timings, results and the new document total after every fix. Any diagram/check over 10 seconds is a speed fault; no individual check may exceed 60 seconds.
    - **Conversion profile and route-classification fix (2026-10-14):** the source audit found 179 `enterprise.md` diagrams, 152 `flow.md` diagrams and 342 `EIP.md` `:::mermaid` blocks (342 opening and 342 closing delimiters). Before the fix, isolated per-diagram conversions summed to 1,526.6 s: enterprise 808.6 s, flow 389.0 s, EIP 329.1 s, with 11 diagrams capped at 60 s. `BuildRouteClassNamesByEdgeKey` had called the full intent analyzer, which resolved raw route guides for every edge; label rendering needs only the route class. It now classifies topology directly using the same route-classification functions, and enterprise rendering reuses the map already built for its coordinate frame. The final 673-diagram sum is 789.0 s (13:09.0): enterprise 262.6 s (4:22.5), flow 204.0 s (3:24.0), EIP 322.4 s (5:22.4), a 48.3% reduction; F153 (#152, mixed 160-node wrap stress) is the only remaining per-diagram timeout at 60 s. The slowest twenty are F153 60.0 s (timeout), A142 20.0 s, A163 18.4 s, A164 7.1 s, A113 6.9 s, A165 6.7 s, A115 6.6 s, A114 5.9 s, A167 5.9 s, A110 5.2 s, F151 5.1 s, A117 4.4 s, A116 4.3 s, A168 4.2 s, F152 4.2 s, A166 4.1 s, F113 3.1 s, F58 2.9 s, A153 2.9 s and A105 2.3 s. The summed timings are isolated conversions, not whole-file wall time. Whole-file conversions then took enterprise 99.9 s (success; wrote `enterprise.docx`), flow >120 s (stopped at F153; no new `flow.docx` was written), and EIP 45.7 s (success; wrote `EIP.docx`). A142's layout test now takes 8.6 s (placement 0.25 s, grid layout 7.68 s, score 0.02 s); isolated converter time is 20.0 s. Its converter stages include 9.27 s in enterprise layout and 9.82 s in edge routing. Across the final isolated profile, inclusive totals were 706.95 s for docx writing, 327.06 s for Mermaid SVG rendering, 276.27 s for SVG embedding/raster handling (including 246.61 s in rasterization); these stages nest and must not be summed. The 4x4, 6x6 and 8x8 gates remain 36, 225 and 784 crossings, each with zero overlap; measured layout times were 0.12 s, 0.58 s and 0.62 s. Route-class output equivalence to full intent analysis has a focused regression test. Per-diagram timings are saved in `docs/diagram-conversion-profile-2026-10-14.csv`. F153 remains an open speed fault; no baseline rows were changed.
    - **SVG edge-paint profile and shared render data (2026-10-14):** the corrected 673-diagram run completed without a watchdog timeout in 574.36 s: `enterprise.md` 179 diagrams / 216.23 s / 761.3 ms median, `flow.md` 152 / 117.35 s / 654.2 ms median, and `EIP.md` 342 / 240.78 s / 715.0 ms median; global median was 714 ms. The earlier comparable run totalled 654.22 s (228.57 s, 172.62 s and 253.03 s respectively). The largest summed timer remains inclusive DOCX writing (557.95 s); nested render totals were 258.62 s Mermaid SVG, 221.98 s SVG embed/raster handling and 199.30 s rasterization, so these figures overlap. F153 formerly hit the 60 s cap inside painting: by then it had made 2,490 grid path searches, 14 guide-resolver calls, scanned 105 guide-list entries and made 42 paint guide lookups; the timeout prevented complete line-jump and label-test counts. Painting had triggered label clearance that resolved all guides and searched paths again. Render data and guides are now resolved once per diagram and reused; the painter is drawing-only. F153 now converts in 8.03 s (SVG render 2.73 s; edge paint 12.7 ms): 163 direct guide lookups, zero paint-time path searches, zero guide-resolver calls and zero guide-list scans. Line-jump detection had 14,196 possible stroke pairs; sweep-and-prune examined 1,030 broad-phase pairs, tested 6 segment pairs and found 5 crossings. Across all 673 diagrams these counts were 90,096 possible pairs, 45,296 broad-phase pairs, 1,396 segment-pair tests and 1,380 crossings. The shared spatial index also serves label collision queries. SVG SHA-256 hashes were identical to the legacy rendering path for 20 diagrams from each document; route labels and benchmark scores remained unchanged. F153's old path produced no SVG before its timeout, so there is no pre-fix F153 output hash to compare. Eighteen known invalid/unsupported Mermaid samples still use the converter's fallback path (negative SVG-render stage marker), with no watchdog timeout. The fast layout/render gate passed 4 tests: 4x4 157 ms / 36 crossings / zero overlap, 6x6 567 ms / 225 / zero, 8x8 918 ms / 784 / zero; incremental and full scores matched. No existing baseline rows were changed.
    - F153, A114 and A117 fail line rules. A117 has four routes with more than two corners plus placement-side violations.
    - A142 is sparse (7750 x 5870, 51 crossings, 45.8% empty) and its Web Shop, Order Manager and Payment Hub regions are loose.
    - A164 honours 63 of 64 hints and A167 honours 31 of 32.
    - Remaining test failures: Layout 80 and RuleValidation 11 (see the table below). Those that conflict with the rulebook are handled by task 3; the rest are defects to fix.
    - **Confirmed attach and rendering faults to re-measure and track:** F61 has source-attachment failures on `A->D` and `B->C` (the expected vertical stubs do not meet horizontal faces); F33 has four cross edges with invalid orthogonal attachment; F58 has a perpendicular-attachment failure on `Validate->Reject`; the live-samples quality gate reports title clearance for `lan->appNode` in sample 03; the known-bad enterprise picture expectation asks for `GroupBorderDiagonal` on `jvm->hosting`, but the checker emits no such finding. These are open faults, not accepted exceptions; re-measure them after task 5 and fix them generically where the rulebook requires it. F136's unoptimized two-crossing route set also exposed an endpoint attachment fault: `Web->Audit` started 21 px outside the Web box outline. The many-to-many rebuild now emits three crossings with all endpoints on outlines; its focused test checks orthogonality, distinct ports, no shared runs, and clearance from unrelated boxes.
    - **Bend-catalogue failure groups:** 103 scenarios exceed their existing bend baselines; F65, F70, F87, F88 and A01 fail layout. The initial catalogue run found 11 missing baseline entries (F136 and A170–A179); F136 and A178 were later measured at zero and added as new zero rows, without changing any existing row. The remaining nine missing entries (A170–A177 and A179) have 20 findings total. A142 exceeded the 60-second hard ceiling; A164 profiled at 13.99 seconds. Both are recorded speed faults and remain excluded from catalogue acceptance until layout time is reduced. Re-measure and resolve these after task 5 without adding known findings to the baseline.
    - **Crossing-check findings:** A169's `westernPower->collaborator:8` route now preserves its south/north side hints and avoids its crossing with `otherAgencies---collaborator:12`; the focused catalogue assertion covers both. The earlier check `j` findings on `dplhPlan->collabAdmin:6` and `water->collaborator:7` still need a fresh measurement. The `dplhAdmin->collaborator:5` route was shortened while routing around obstacles. Excluding check `j` from the hinted unnecessary-bend assertion does not waive remaining findings.
    - **A169 checker gap:** the catalogue's finding count only included returned checker violations; its separate route-length and crossing assertions did not count as findings. The checker also did not validate terminal segments against their declared sides or search alternate ports when looking for crossing reductions. It now reports wrong-side routes as check `k` and crossing-removing alternatives as check `j`; focused regressions use the captured `dplhAdmin->collaborator:5` and `westernPower->collaborator:8` geometry. The A169 run now exposes three `k` findings (`dplhPlan->planner:1`, `localGov->referral:4` and `dplhAdmin->collaborator:5`) against the unchanged zero baseline. The captured westernPower crossing is detected by its focused test; the current generated A169 geometry already routes around it.
    - **Check k audit:** five distinct diagrams were inspected in layout coordinates, using the first and last route segments against the declared sides and endpoint box bounds. Every sampled `k` finding was real; none was a false positive, so no checker change or baseline edit was needed. Samples: A32 `webShop->orderMgr:0` exits left despite declared bottom; A36 `choice->refund:1` approaches the target from left despite declared right; A48 `split->inspect:1` approaches from left despite declared right; A58 `shop->ledger:0` exits bottom despite declared right; A62 `paySvc->checkout:1` exits top despite declared bottom. The fast catalogue batches recorded these `k` counts by scenario (all against existing unchanged zero rows; new scenarios remain without baseline rows): F37=2, F39=1, F40=2, F41=2, F42=1, F60=3, F74=1, F127=2, F129=2, F138=1, F139=1, F153=10; A32=2, A36=2, A48=4, A58=1, A59=1, A62=2, A63=1, A99=1, A105=2, A107=1, A111=7, A114=9, A115=10, A116=5, A137=1, A138=1, A146=1, A148=1, A153=2, A156=2, A157=5, A162=1, A165=9, A166=1, A167=7, A168=2 and A169=3. Total: 113 `k` findings across 39 scenarios. No existing baseline row was changed.
    - **Performance:** A163 completed in 20.9 seconds (175 routes), with score 98.9, zero crossings, zero overlaps and zero unnecessary-bend findings; at the current 10-second target, this is a speed fault. A110 took 36.23 seconds (12 findings) and A114 took 13.17 seconds (28 findings); both are speed faults. Rule 4.7 now indexes node boxes and route segments in one grid, scores candidates from cached route-local deltas, and revisits only routes touching an accepted move. The route-repair pipeline skips a repeated port-side pass when the route state is identical; this removed the second 16-second search seen in the earlier profile. The port-side pass's candidate changes were rejected by its full-layout score guard, so the optimization preserves A163's final layout. A142 did not finish within the 60-second ceiling and was stopped. Its partial stage trace recorded 33 group-frame-fitting invocations (7.012 s summed, 6.993 s max) and 32 coupled-repair invocations (3.856 s summed); these timings are nested and partial, not full-run totals. A 10-second sample attributed 5.35% exclusive / 8.13% inclusive time to `FlowchartPaddedVisibilityRouter.TryGridPath`, while the remaining samples were mostly thread-pool waits. A164 completed in 13.99 s with 56 nodes, 47 edges, 5 groups, 47 routes and zero overlaps: group-frame fitting took 8.154 s, repair pass 12 (`4.4,5.8`) took 4.605 s across repeats, and pass 27 (`4.7`) took 691 ms; its total is a speed fault over 10 seconds. In the current experimental refit branch, two A164 samples were stopped before 60 seconds: the layout had not returned, and the detailed trace reached root-container packing with 39 units and 46 links, scoring 2,450 transpose candidates in the last sample. Eight nested fit calls were logged (one pass each); six ended stable and two ended on a repeated state after the same adjustment, `evict explorer from frame regRoles`. No pair of alternating adjustments has appeared. These incomplete timings have no usable layout score and are not acceptance results. The earlier 25-second A164 run and 11.74-second hinted-route check remain historical speed warnings.
    - **Latest open-fault measurements (2026-10-14):** The route-repair change for rule 4.11 is committed, and its incremental score assertion still matches the full scorer on A142. The second and third convergence runs made no changes beyond routes whose dependencies had changed, so the stage now runs once and revisits only affected routes. A142 now completes in 12.12 s with 70 crossings, zero overlaps and 184 bends; it remains a speed fault against the 10-second target and remains open for its layout faults. A164's latest sample completed in 9.15 s with 19 crossings, zero overlaps and 67 bends. That run is below the speed threshold, but A164 remains open: `define->bwmSvc` has no legal side-choice candidate and still needs rule 1.7 gap-growth; the earlier 13.99 s and 25 s runs remain recorded historical speed faults. A169 still fails its unchanged zero baseline with three confirmed check `k` wrong-side findings (`dplhPlan->planner:1`, `localGov->referral:4`, `dplhAdmin->collaborator:5`); do not add these to or relax the baseline. The catalogue remains 213 pass / 115 fail against 212 / 116 at `d3d37e2`, with A31 and A169 newly failing and A50 newly passing; A142 and A164 remain excluded from that comparison. The other open failures and faults above remain unresolved: the 103 bend-baseline regressions, five layout failures, nine unbaselined diagrams with 20 findings, 113 check `k` findings across 39 scenarios, the attach/rendering faults, and the known-bad enterprise picture expectation. No existing baseline row was changed.
    - **A142 speed fault (2026-10-16):** Stop performance work on A142 for now. The latest focused layout took about 26 s against the 10 s target (90 crossings, zero overlap, 424 bends); keep it recorded as an open speed fault. Do not spend further optimization effort on it while starting the 5.10 candidate comparison.
    - **Uncommitted 5.10 and fast-gate checkpoint (2026-10-16):** The separate 5.10 comparison has only begun: current uncommitted work records the unchanged “hump” and node-move options as independently scored candidates; reroute-this, reroute-other and chain candidates are not implemented yet. The 4×4, 6×6 and 8×8 crossing benchmarks fail (8×8 is 1,042 against the unchanged 784 limit), and the F136 legality test fails. These failures were reproduced on `c6605bd`; all four checks passed on `7390dc0` and `e1822ad`, so the regression is introduced after `7390dc0`. On resume, first isolate the `c6605bd` change that causes the failures before continuing 5.10.
    - Check rules 2.17, 2.18, 2.19, 4.7 and 4.8 with two new scenarios: Order Suite with Web Shop and Order Manager (aggregation), and Place Order with Basket Submitted, Pricing, Stock Check and Order Service.
    - **Done when** each item passes, or is listed with its rule number and the reason it remains.

## Do not repeat
- F153's remaining regression is a legal four-bend fallback replacing an illegal two-bend path through three nodes. Re-running port repair with touched neighbours, adding the legacy corridor family, and removing the fixed-interaction count guard each left 146 bends / 174 crossings unchanged. Do not repeat those attempts or restore the illegal baseline path.
- Restoring six-pixel track spacing, restoring the complete old track stage, and rejecting extra crossings in the port-side scorer did not resolve the new 5.9 increases. The complete old track stage also created off-slot ports. Those experiments were reverted; their logs remain under `out/route-cost/`.
- Applying a single 16-option side budget or final-terminal budget per diagram broke the previously passing grouped 3×3 regression. Both experiments were reverted. Rejecting illegal changed paths before the full joint-assignment check preserves the 13 passing regressions, but does not meet the 48.78-second audit target. Do not retain a speed change that spends the budget before a required legal assignment can be considered.

- Shared-track repair stopped after three different attempts: internal-run nudging, measured channel growth, and local side/incident-route scoring. F61 can have different middle tracks while its opposing terminal runs still overlap. Do not repeat these as a complete fix; sides, symmetric slots and tracks need joint evaluation. Crossing-first local side ordering also reintroduced shared tracks in the fan/many-to-many examples; retain the failing tests rather than weakening their gates.

- Do not apply placement or line hints after automatic placement and repair, or reapply them at the end as a patch. Hints come first.
- Do not draw curved fans, S-curves, diagonal diamond ends or hop arcs. Remove avoidable crossings by swapping tracks, ports or sides.
- Do not use edge-midpoint diamond ports or a fixed number of port slots per side (such as five). Diamond and side ports are shared and spaced k/(n+1) with no limit (rule 3.2).
- Do not cap passes or searches (the old 32-pass separation cap). Stop when a pass gives no strict improvement or repeats a state.
- Do not let a baseline absorb faults: no rewriting a missing baseline, no counting a failed layout as zero lines, no bend allowance, no raising a count to pass.
- Do not write scenario-specific code, such as the removed `FlowchartSample03OracleLayout`, or branch on scenario ids, labels or node names.
- Do not keep tests that lock in old behaviour, or hard-coded pixel checks.
- Do not keep more than one rule source. The rulebook (later `rules.yaml`) is the only one; old specs and briefs are in `MarkdownToDocx-Backup\Old rules` and `Old briefs`.
- Do not grow gaps for quality, and do not move a label off its line instead of making room. Space grows only for legality, by whole grid steps.
- Do not let enterprise re-route after the shared stage; it adds notation only.
- Failed approaches: per-route repairs for A114, F153 and A142 (moving whole blocks or single legs, frame exclusion, perimeter repair, intersection-ranked repair) traded faults between lines or enlarged layouts. Fan and convergence ports must be reassigned together; changing either alone moved faults elsewhere. Doubling rank space for labelled Z routes, extending the final two-corner repair to all diagrams, and per-change crossing guards each created new faults. Exhaustive all-side port search and global terminal simplification regressed other scenarios.
- Unsafe code cleanups recorded earlier (static render state, wide file splits, call-contract rewrites) changed behaviour when forced; they are now refactoring targets 4 and 5 (task 3) and 7 (task 4) and must be done as move-only steps with unchanged metrics.

## Stretch goals

These must be done, but not yet. Start them only when Dave says so.

1. **Rulebook gaps.** Finish the label, group-frame and sequence sections of `Instructions/LAYOUT-RULEBOOK.md`. Do not build the two rules on hold (frame on the midline, reserved midline). Ask Dave about the six unconfirmed defaults and wait for the answer.
2. **Native render for the other kinds** (sequence, class, state, ER, pie, gantt, mindmap, git graph). Do one kind at a time, in its own commit, and keep the Naiad fallback until a kind passes its scenarios.

## Test results

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

If the vendored Mermaid changes, update this pin, the recognition fixtures and `spec.md` together. `flowchart-elk` remains deferred.

## Maintenance rule

Update this file at the end of every completed task. Remove a finished task, keep only lessons worth keeping under "Do not repeat", and delete anything that is no longer true instead of adding history.

## Reviewed DOCX layout round in progress — 17 October 2026

The coordinate migration is parked in `out/route-cost/parked-coordinate-migration.patch`
against 7226be4. Do not reapply it before this layout round is committed and a
new geometry baseline is captured. The rule source now requests 20 points per
bend and 100 per off-table node; generated rulebook and JSON were refreshed.

The table-alignment integration reached the three-attempt stop rule. First,
compacting all rows/columns after frame fitting removed grouped channel space
and F73 exceeded the 59-second progress watchdog (only 73 summaries).
Second, excluding groups and retaining a fine-grid horizontal pitch completed
the focused run but raised crossing counts in A153 (69 against 24), A110
(269 against 258), and A113 (147 against 120). Third, expanding validated
bands without compacting existing space restored six of seven crossing
regressions, but A110 still had 260 against 258. The integration was removed;
the table model, independent measurement, score term and tests remain for the
next frame-aware complete-candidate implementation. No checker threshold or
baseline row was changed to accept these results.

The accepted baseline full suites were captured once in the separate
`out/audit-speed-work` worktree: layout 792 passed / 208 failed (1000 total),
rule validation 966 passed / 160 failed (1126 total), converter 62 passed /
1 failed (63 total). Logs are `out/route-cost/layout-round-baseline-*-tests.log`;
the converter failure is ConvertText_MultipleEnterpriseErrors_UseOneVisibleLineEach.
New focused table, capacity and score tests currently total 13 passing tests.
This is work in progress, not a completed layout round or an approved migration
baseline. No new layout-round commit or DOCX regeneration has been made yet.

## Revised logical-grid prerequisite and shape integration — 17 October 2026

This section supersedes the older inferred-table/off-grid-penalty plan above.
The current `Instructions/rules.yaml` is authoritative. Rule 1.3 permits each
node to grow independently by whole cells, based on measured text, allocated
port capacity and mandatory geometry. Neighbour count alone is not a growth
request. Representation bottom ports use the horizontal line through the wave
endpoints, with vertical terminal runs; rule 4.1 has not been extended.

The unfinished layout fault batch was parked in
`out/logical-grid-prerequisite/paused-layout/tracked-source.patch`, with owned
new files retained under that directory and a `new-files.txt` inventory. Its
base is 7226be4. Do not blindly restore its inferred variable-band grid or
count-based fan sizing: both conflict with the revised cell-region model.
The separate coordinate migration remains parked. No migration has resumed.

### Renderer measurements and defaults

`DiagramGridMetrics` defines a 30 px drawing step, 150 px standard rectangular
width and 60 px standard height. `FlowchartRenderOptions` defaults to 16 px
Trebuchet MS/Verdana/Arial text and 15 px label padding. The estimator currently
uses 30 px horizontal padding on each side and 15 px on each vertical side.
`FlowchartSugiyamaSvgRenderer.AppendNodeLabel` paints multiline text at 1.5 em,
so two default lines plus vertical padding need 78 px, rounded up to 90 px.
The existing minimum label box is 67.2 px; adding its existing 60 px total
horizontal padding needs 127.2 px, below the existing 150 px standard width.

Only the three requested parameter defaults were set for this prerequisite:
`base_cell_width = 150`, `base_cell_height = 90`, `node_text_padding = 15`.
The rulebook and JSON twin were regenerated with `Build-Rulebook.ps1`.
Other rule edits already in the working tree belong to the revised user
specification, not to an engine implementation claim.

Real integration incompatibilities remain. Production grid preparation still
derives one shared size from all labels, placement and repair helpers still
move or resize pixel boxes, and their 30 px moves are not logical-cell moves.
Text sizing uses width heuristics, a different 1.25 em/8 px measurement path,
and can ignore the resolved per-node font used to paint the SVG. The shared
layout project has no actual-font measurement service. SkiaSharp 3.119.4 is
already used by DOCX rasterisation and its installed native assets include
win-arm64; no additional native dependency has been added to the layout
project during this increment.

### Shape integration completed, grid engine integration still pending

The renderer-owned `NodeGeometry` contract separates visual bounds, eligible
connection boundary and occupied silhouette. Contract and representation
providers now participate in shared allocation, routing clearance, terminal
checking, legal hints and the independent slot audit. The router has no new
contract/representation shape-name branches. Local geometry is page-down;
the existing routing adapter converts its points and normals to routing Y-up.
The enterprise drawings retain their existing outlines and numeric constants.

The contract's 6 px top decoration remains an obstacle; body ports are inset
and its north approach is ineligible because it crosses that decoration.
The representation endpoint line is 8 px above its visual box bottom. The
unchanged wave protrudes below that line on its right half, so the provider
rejects positions whose vertical approach would cross the wave. The centre
bottom port and vertical terminal remain available. Five painted arrowhead
variants touch the wave at the tip without crossing it. Impossible geometry
hints use the existing reported hint-rejection path; legal hints retain their
requested sides and connection-body ports.

The restored return/wrap regressions pass. EIP166 exposed another allocation
mismatch: a legal straight tip was restored after allocating two slanted-edge
ports, leaving its sibling at a two-port fraction. Allocation now reserves the
straight tip first and redistributes remaining slanted-edge ports at source.
An ineligible route candidate can clear provisional points, so allocating a
declared relationship now recreates its two anchors before writing endpoints.

The logical-grid foundation is implemented and tested, but is NOT enabled as
the production placement engine. `FlowchartLogicalGrid` owns immutable base
configuration, explicit integer cell regions, atomic occupancy changes and
sparse grown gaps. Its dimensions include every internal grown gap.
`FlowchartNodeSpanCandidates` measures wrapping before deriving finite,
undominated width/height candidates, preserves explicit blank lines and long
words, and accepts measured mandatory geometry minima. `FlowchartPortCapacity`
measures separation between allocated points and never infers growth from a
neighbour count. The 13 logical-grid tests do not prove the old renderer now
implements logical regions. Next, wire renderer font measurement and retained
wrapping into sizing, make regions authoritative across placement and repair,
then compare complete span/layout candidates under the unchanged score. Keep
open mode outside that path and preserve hint precedence. Finish this before
restoring the paused fault list or per-diagram switches.

### Verification and unresolved checker dependency

The final focused prerequisite selection passes 54 tests: shape providers,
painted guides and arrowheads, valid/impossible hints, geometry contracts,
return/wrap behavior, the decision return-slot regression and logical cells.
The established routing regression selection remains 34 passing / 2 known
failures (F153 and LargeFanOut), including the existing illegal-path tests.
The broader Enterprise selection was 201 passing / 28 failing. Of those,
24 were already in the recorded full-suite baseline; four opposing-pair tests
expect straight reverse links and conflict with the restored rule 5.3 return
behavior. Their expectations were not changed to conceal the difference.

Measured increments, in Release on this ARM machine:

| Increment | Summaries | Seconds | A142 seconds | 4.2 | 5.9 | 3.1 | 4.1 | 4.11 | 5.8 | Shared pairs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Numeric defaults only | 673 | 37.03 | 12.4163 | 39 | 1700 | 4 | 0 | 3 | 0 | 2083 |
| Shape integration and return slots | 673 | 34.76 | 10.4240 | 44 | 1694 | 4 | 0 | 3 | 0 | 2089 |
| Final shape guards and tested grid foundation | 673 | 38.98 | 11.6791 | 44 | 1694 | 4 | 0 | 3 | 0 | 2089 |

Final slot, node/frame grid, title-band, unrelated frame overlap, missing-route,
NoLegalRoute, illegal-path, cleanup and hint-accounting gates are zero. The
five existing native layout failures remain unmeasured; the audit therefore
still exits nonzero for known failures and shared-track findings. This is not
full acceptance. Baseline rows were not edited. Logs are under
`out/logical-grid-prerequisite/`, especially `increment3-final-audit.log`.

The legacy 4.2 checker still counts aligned terminals whose required directions
disagree. The revised rule explicitly excludes those illegal straight routes.
Three integration attempts at correcting that checker exposed a repair
dependency: F32 and EIP261 retained illegal paths; synchronising early bounds
removed them but changed other side/crossing outcomes. The checker refinement
was parked rather than leaving nonzero legality gates or changing unrelated
geometry. Its helper and two direct tests are saved in
`out/logical-grid-prerequisite/parked-direction-agreement-helper.txt` and
`FlowchartAlignedPortDirectionTests.cs`. Fix candidate/rollback geometry and
late writer legality before reinstating it. Thus the current 44 is the legacy
reported count, not a claim that all 44 represent legal straight alternatives.

No prerequisite commit, DOCX regeneration, push or coordinate migration has
been performed in this increment. The three previously dirty DOCX files were
left alone. Resume the paused layout fault list after production cell-region
integration passes its audits.

### Shape and cell foundation checkpoint

The finished shape integration, restored return/wrap behavior and tested cell-region foundation are checkpointed before production repair activation. Cell views, atomic ownership transactions, guarded pixel writes and their unit tests are included. The partially integrated repair searches and engine fixtures remain uncommitted. Independent spans remain disabled, and F32/EIP261 remains parked for routing work. The checkpoint is verified separately from those unfinished repair branches.

Isolated checkpoint verification passed 74 focused tests, including the original 54. Its full Release audit emitted all 673 summaries in 37.30 s, A142 11.8293 s, with every previously zero gate still zero. Existing totals are unchanged: 4.2=44, 5.9=1694, 3.1=4, 4.1=0, 4.11=3, 5.8=0 and shared pairs=2089. Five native failures remain unmeasured. The audit still exits nonzero for known findings. Logs: out/logical-grid-prerequisite/checkpoint-focused-tests.log and checkpoint-audit.log.

### Authoritative-cell repair increment (2026-10-17, incomplete)

The cell-backed engine now has atomic whole-region translations, joint region
assignments and swaps, explicit span changes, measured shared-gap growth, and
exact cell/gap snapshots. Regions are integer contiguous rectangles; a complete
assignment is checked for shared cells before any member changes. Bound node
pixels are read-only projections of those regions. Legacy X/Y/width/height,
frame-fit and coordinate-setter writes throw before mutating a bound node.
Pinned nodes reject span changes as well as translations: a span change at an
unchanged start cell would still move the derived centre.

Cell branches now cover move-before-detour, unrouted-group moves, joint
node-order searches, overlapping-frame member-group moves, channel sealing,
hint-aware gap growth and hinted-terminal obstacle clearance. Candidate search
uses the existing candidate/chain/line limits and cannot replenish the diagram
budget on another call. Accepted candidates and forced rejection/exception
rollback retain exact cells, gaps, frame caches, route points, port slots,
normals, hint activation and hinted-route caches. The routing-axis projection
remains at its existing boundary; this is not the parked coordinate migration.

This is **not production cell authority yet**. The catalogue's initial placement
producer still creates unbound pixel nodes. These nodes still take the legacy
repair branches. The new integration fixtures bind explicit declared regions
and run real repair/materialisation entry points; they deliberately do not
infer occupancy from the legacy output. Remaining work includes initial
rank/column/wrap/group region production, frame-fit/repacking member moves,
remaining compaction and packing writers, capacity-driven growth replacing
crowded/parallel-side direct resizes, and candidate snapshots in those paths.
Do not enable independent text/capacity spans or claim every repair converted
until these paths are migrated and the entire catalogue is exercised bound.
The existing integer GridNode width/height compatibility views also require
review for fractional configured pixel dimensions and very large spans.

Verification of this increment:

- 82 focused tests pass, including 41 cell tests and the 41 existing shape,
  geometry-contract, return/wrap and decision-return-slot tests. Cell coverage
  includes shared-cell rejection, atomic swaps/group moves, mandatory diamond
  reservations, pinned centres, gap ownership, exact rollback on exceptions,
  final materialisation and shared named search-budget exhaustion.
- The established routing selection remains 34 passing / 2 known failures
  (F153 and LargeFanOut), including the existing illegal-path tests.
- Exact before/after rendered geometry comparison: 673 scenarios, zero changed,
  zero missing. Baseline: `out/logical-grid-prerequisite/authoritative-cells-before.jsonl`;
  comparison: `authoritative-cells-compare.log` (31.653 s).
- Full Release audit: all 673 summaries, 36.50 s, A142 11.0010 s, five existing
  native failures unmeasured. All previously zero gates stay zero: off-slot
  ports/groups, node/frame grid, title bands, unrelated frame overlaps, missing
  routes, NoLegalRoute, illegal paths, cleanup and hint accounting.
- Totals remain 4.2=44, 5.9=1694, 3.1=4, 4.1=0, 4.11=3, 5.8=0,
  shared-track pairs=2089, 3.2/4.7=0, 3.3/5.2=5 and 4.9=6.
  The audit exits nonzero for the unchanged known failures; this is preservation,
  not full acceptance. Log: `authoritative-cells-audit.log` in the same directory.

No snapshot changes need explaining in this increment. No baseline rows,
rules.yaml, weights, DOCX outputs or open-mode behavior were changed by it.
Independent spans remain disabled. The F32/EIP261 4.2 checker/candidate fault
remains parked as described above. No commit or push was made in that increment.

### Checkpoint committed; cell frame fitting continuation

Checkpoint `c4895ffc80af4e10e1a6a7899307dc04195113f4` has the plain imperative
subject "Add the shape geometry contract and cell-region foundation". Only
the finished shape/return/wrap integration and tested cell foundation were
selected. It was independently built and tested in
`out/cell-foundation-checkpoint` without the unfinished repair-search branches.
All 74 focused tests passed, including the original 54; its audit emitted
673 summaries in 37.30 s with all previously zero gates still zero. The commit
excludes DOCX files, unfinished engine repair integration and its fixtures.
No push was made.

After that checkpoint, cell-backed `FitFrames` now bypasses the old cloned
pixel trial nodes and pixel copyback entirely. It searches atomic whole-cell
member translations, derives frames from the candidate regions, rejects new
frame/foreign-node conflicts, and restores exact cells/gaps and cached frame
objects after every rejected or exceptional candidate. Accepted frame work
shares the existing named candidate, chain and line limits with its explicit
diagram context. No new limits or score weights were added. Direct foreign-node
eviction and moving a frame away from a locked outsider use the same search,
and must clear the specifically requested conflict. External hint dependents
prevent an unsafe move; fixed hints are never silently displaced.

Sixteen new frame tests cover nested and empty groups, all four flow directions
in both existing coordinate views, direct eviction, pinned outsiders, external
hint dependencies, forced failure rollback, budget exhaustion, an explicit
budget independent of an unrelated ambient render, and mixed-axis rejection.
The combined selection passes 105 tests: the previous 82, these 16, and seven
existing frame/gap helper tests. Log: `out/logical-grid-prerequisite/cell-frame-combined-tests.log`.

Full Release audit after the continuation: all 673 summaries in 39.67 s; A142
13.7935 s. Every previously zero legality/slot/grid/title-band/frame-overlap/
missing-route/NoLegalRoute/illegal-path/cleanup/hint-accounting gate stays zero.
Other totals stay 4.2=44, 5.9=1694, 3.1=4, 4.1=0, 4.11=3, 5.8=0,
shared pairs=2089, 3.2/4.7=0, 3.3/5.2=5 and 4.9=6. Five existing native
failures remain unmeasured and the audit still exits nonzero for known findings.
Exact rendered geometry comparison completes all 673 scenarios with zero
changes and zero missing (37.362 s). Logs: `cell-frame-audit.log` and
`cell-frame-geometry-compare.log` in the same output directory.

The frame continuation is uncommitted. Production cell activation is still
pending the initial region producer and remaining packing, compaction,
hint-placement and resize writers. The catalogue still exercises its existing
unbound initial placement, so preservation is verified but global cell authority
is not yet established. Independent spans remain disabled. F32/EIP261 remains
parked for the routing work. No baseline rows, rules, weights or DOCX files
were changed during this continuation.

### Atomic region assignments and complete rollback

This increment replaces the remaining per-member writes inside the logical
grid itself. Its current state is now one immutable snapshot containing every
node region and both gap maps. Preparation copies and validates the entire
target set, including fixed nodes; commit publishes that state by one reference
replacement, and rollback restores the complete original state the same way.
The integer rectangle type requires positive contiguous spans, so fractional,
L-shaped or disconnected reservations cannot be submitted. Final pairwise
occupancy checks reject any shared cells before publication. Invalid later
gap increases cannot leave an earlier increase or member target applied.

Cell repair options no longer carry callbacks that construct targets while
mutating geometry. They carry frozen validated assignments. The prepared
assignment checks its exact source state before application, rejecting stale
plans, and also rejects a participant pinned since planning. Mutating a caller's
target dictionary after preparation cannot alter the assignment.

Prepared assignment now covers cell-backed endpoint/group move-before-detour,
unrouted-group moves, frame-overlap group moves, joint node-order swaps,
frame fitting, foreign-node eviction, moving a frame away from locked outsiders,
channel gap growth and hinted-terminal clearance. A neighbour-displacement
planner gathers the entire collision chain before validation, using the
existing named chain-depth and changed-line limits. Its full participant set
is checked for fixed hints and external hint dependents. Every accepted option
still passes the existing complete-layout legality/score checks where routing
is already present.

Hinted-terminal clearance previously could accept one obstacle move and then
stop at a locked later obstacle, leaving a partial repair. It now accumulates
the complete unpublished target set for all leave/enter terminals, validates
every final ray again, and commits all obstacle moves or none. Candidate pixels
are projected directly from proposed regions, not copied back into occupancy.
Frame fitting also restores its entry cells, gaps and frame objects if a late
failure follows an internally accepted assignment. Search effort remains spent
against the named budget after rejection; geometry and hint state are restored.

Fifteen new tests cover the three specifically requested cases, frozen targets,
stale plans, late pinning, recursive neighbour displacement and a locked tail,
invalid gap batches, a real scored gap-growth candidate failing, all-or-none
hinted obstacle clearance, and late frame rollback after an accepted move.
The focused combined selection passes 120 tests, including the original 54
and all 105 from the preceding increment. Production layout builds with zero
warnings/errors. Test log: `out/logical-grid-prerequisite/atomic-combined-tests.log`.

The full Release audit emits all 673 summaries in 40.68 s, A142 13.7707 s.
Every previously zero gate stays zero: off-slot ports/groups, 3.2/4.7, node/frame
grid, title bands, unrelated frame overlaps, missing routes, NoLegalRoute,
illegal paths, cleanup, hint accounting, 4.1 and 5.8. Other counts remain
4.2=44, 5.9=1694, 3.1=4, 4.11=3, shared pairs=2089, 3.3/5.2=5 and 4.9=6.
Five existing native failures remain unmeasured; the audit still exits nonzero
for known findings. Exact rendered comparison completes all 673 scenarios
with zero changed snapshots and zero missing in 39.088 s. Logs:
`atomic-regions-final-audit.log` and `atomic-regions-geometry-compare.log` in
the same output directory. There are no snapshot changes to explain.

The obsolete cell-side operation callbacks, member-by-member grid publication
and separate gap-map rollback have been removed. The legacy pixel trial/copyback
code still has live callers in unconverted initial placement and packing paths;
it cannot yet be deleted while preserving those layouts. It is bypassed by the
converted cell-backed repairs. Production cell activation and the remaining
placement/packing/resize conversion are still pending, so the catalogue's zero
snapshot difference is preservation evidence, not proof of global cell authority.
Independent spans remain disabled. F32/EIP261 remains parked. Baseline rows,
rules.yaml, weights and DOCX files were not changed. This atomic repair increment
was subsequently checkpointed as 50f1357, as described below. No push was made.

### Repair checkpoint, real guard coverage, and initial placement start

Checkpoint `50f135769a194de4af7b4d3cd3fb63e8a9fb05dd` has the imperative subject
"Apply cell-region repairs atomically with exact rollback". It includes only the
finished repair conversion, its 120 passing focused tests, and the recorded
zero-geometry-change verification across 673 scenarios. Its body explicitly
states that production nodes are not cell-bound and therefore that catalogue
run never exercised the bound-node guard. DOCX files were excluded; no push.

The uncommitted follow-up now reports primary nodes, cell-bound nodes, all
pixel-write checks, checks on bound nodes, and rejections for each scenario.
Detached legacy trial-node writes and nested group reflow checks are included;
primary node counts are not inflated by reflows. Six open-mode scenarios are
explicitly exempt from experimental binding. Native failures report no created
or bound nodes instead of claiming coverage.

Debug builds and all test hosts retain hard failures. Release production policy
rejects the write before touching backing fields, cells or gaps, and retains a
diagnostic with the caller chain, current repair step, coordinate/method and
node id. Diagnostics survive candidate rollback and SVG BeginRender. Release
behavior is tested with the strict test switch temporarily disabled only in
those dedicated policy tests. Normal tests enable the switch through a shared
module initializer; Debug always throws regardless of the switch.

The separate explicit cells-on fixture is test-only. It builds declared regions
from logical ranks/columns and premeasured common spans, binds all reachable grid
nodes before placement, and executes real pipeline stages until the first
illegal write. It does not infer occupancy from final pixel positions, enable
independent text spans, alter the production default, or pretend a guard catch
is a completed legal layout.

| Run | Scenarios | Primary nodes | Bound nodes | All write checks | Bound-node checks | Rejections | Time |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Cells off, full production audit | 673 | 3833 | 0 | 411937 | 0 | 0 | 45.56 s |
| Cells on, explicit guard census | 673 | 3833 | 3806 | 25358 | 662 | 662 | 3.925 s |

The cells-on time includes the test host and stops early at guard failures; it
is not comparable to a completed route audit. All 662 reachable grid scenarios
were bound and caught a write. Six open scenarios contain 27 unbound nodes; five
existing native failures never reached binding. First blocked paths:

| Repair path | Scenarios |
| --- | ---: |
| NodePlacementStage/PlaceOnGrid -> SetMain | 648 |
| PlacementHintStage -> PlaceRelative -> Translate | 13 |
| PlacementHintStage -> PushToBoundary -> Translate | 1 |

These are first blockers, not an exhaustive clearance of the later pipeline.
No broad placement rewrite or automatic pixel-to-cell snapping was used to
hide them. Per-scenario reports and caller chains are in
`out/logical-grid-prerequisite/cell-guard-coverage-off.csv`,
`cell-guard-coverage-on.csv` and `cell-guard-paths-on.csv`. Full census log:
`cell-guard-catalogue-on.log` (all 673 explicit diagnostic tests pass).

All 142 focused Release tests pass: the original 120, 15 guard/coverage tests,
and seven initial producer tests. The targeted Debug guard selection passes
25 tests. Logs: `guard-combined-release-tests.log`, `guard-debug-tests.log`.
The cells-off full audit still emits all 673 summaries. Every previously zero
gate stays zero. Other totals remain 4.2=44, 5.9=1694, 3.1=4, 4.1=0,
4.11=3, 5.8=0, shared pairs=2089, 3.2/4.7=0, 3.3/5.2=5 and 4.9=6.
A142 takes 14.8603 s. Five native failures and the known findings remain;
the audit exits nonzero for those unchanged findings. Cells-on legality gates
cannot be measured past the first failing write and are not reported as zero.
Exact cells-off rendered comparison: 673 scenarios, zero changed, zero missing,
40.103 s. Logs: `cell-guard-catalogue-off-audit.log` and
`cell-guard-cells-off-geometry.log`. No snapshot changes need explaining.

Profiling found a reporting gap: the older AUDIT_TIME line is below assertions,
so it omits failing scenarios including A142. New CELL_AUDIT_PROFILE output is
written in finally without changing checker logic. Inclusive stage totals are
17.159 s model completion/checking and 14.626 s coupled route repair; final
diagnostics is 12.577 s within completion, not an additional independent cost.
A142 spends 8.124 s in completion (including 4.845 s final diagnostics),
2.998 s fitting frames and 2.895 s in coupled repair. Profile CSV:
`cell-guard-audit-stage-profile.csv` in the same directory. A142 remains above
the 10-second target; this work makes no speed-improvement claim.

Initial placement has started with FlowchartCellInitialPlacement, a shared pure
rank/column-to-region producer. It receives already measured integer spans,
builds the whole deterministic ownership plan, resolves duplicate proposals by
whole cells and exposes no partially built grid. It neither reads pixel centres
nor changes fixed base cells. Its tests cover all four directions, stable input
ordering and invalid spans; the explicit census now uses this producer. It is
not called by production placement. Next work is to replace PlaceOnGrid's pixel
cursor and the hint translations with logical region assignment, then continue
past those first blockers. Group packing, frame-aware rank gaps and remaining
writers still need integration. Independent text spans remain disabled;
F32/EIP261 remains parked for routing work. Baselines, rules.yaml, weights and
DOCX files were not changed. Guard coverage/Release policy and initial-producer
work remain uncommitted after the requested 50f1357 checkpoint.

### Initial-placement conversion checkpoint (latest replacement queue)

The latest request replaces all earlier queued directions. The cell-bound
PlaceOnGrid branch now plans all logical rank/column reservations and publishes
one atomic assignment; it never derives cells from pixels. Locked hint regions
remain fixed. Production cells remain off and independent text spans disabled.
The focused selection passes 144 tests (including the previous 120). The full
cells-off audit emits 673 summaries in 42.14 s; A142 takes 12.5521 s. Every
previous zero gate stays zero. Quality totals: 4.2=44, 5.9=1694, 3.1=4,
4.1=0, 4.11=3, 5.8=0 and shared-track pairs=2089. The existing 25 failing
scenarios and five native failures are unchanged. The exact cells-off geometry
comparison finds zero changed scenarios out of 673 (35.136 s).

The post-conversion cells-on diagnostic census visits all 673 scenarios in
2.888 s test-host time. It stops at the first blocked writer, not at a completed
layout: 281 spine-column adjustments, 249 GridCompaction, 105 first terminal
child alignments, 13 relative hints, five first terminal decision alignments,
four first terminal fan centring, two unbranched decision input alignments,
one partner-rank preparation, one crowded-side growth and one boundary push.
It does not establish cells-on gate totals or full-audit time. Placement hints
are next, followed by one caught path at a time with tests and checkpoints.
F32/EIP261 stays parked. No DOCX files are included in this checkpoint.

### Whole-cell node/member hint conversion

The cell-bound placement hint branch plans source-ordered node and nested-group
member reservations together. +n is n whole cells, relative alignment uses
starting rows/columns, and pushes use logical sibling boundaries. Accepted
hints protect earlier relationships; cyclic later hints are rejected with a
reason. Explicit hints can move locked owners; automatic repair locks do not
veto them. Locked-node reapplication uses the same complete plan. The unchanged
empty-group frame placement remains responsible for groups without members;
there is no node occupancy to translate in that case.

Eleven new hint tests pass, taking the focused selection to 155. The census
passes 673 diagnostic tests in 3.5940 s; all 662 reachable grid layouts now get
past the initial hint stage. First catches are spine-column adjustment (295),
GridCompaction (249), terminal child alignment (105), decision-target alignment
(5), terminal fan centring (4), unbranched decision inputs (2), partner-rank
preparation (1), and crowded-side growth (1). It still does not certify cells-on
legality gates. Production cells remain off. The latest measured cells-off audit
is 42.14 s/A142 12.5521 s with all previous zero gates zero and exact geometry
unchanged across 673. No weights, baseline rows or rules changed; independent
text spans stay off and F32/EIP261 remains parked.

### Spine-column cell conversion

Spine and side-branch lanes now use complete logical reservations in the
cells-on path. Graph and label selection is shared with the existing processor;
collector rows use region boundaries. Locked regions stay fixed. A collision
rejects the whole proposed spine plan without changing cells or gaps. Three
new tests pass, and the focused selection is 158. The post-conversion census
runs all 673 diagnostic cases in 3.5153 s. Its next first catches include
GridCompaction (388) and AlignSingleTerminalChildren1 (208). No cells-on legality
audit is claimed before all writers have been converted. Cells off retains the
legacy path. Production activation, independent text spans and the parked
F32/EIP261 routing work remain unchanged.

### Whole-cell empty-strip compaction

GridCompaction now plans removal of wholly unoccupied logical cross-axis strips.
All region translations and measured gap growth are validated and published
in one transaction; hints reject the complete plan and rollback restores the
same snapshot. Three new tests pass, focused count 161. The rerun visits 673
census cases in 4.7996 s. The most common next catch is SnapGridNodeCentres (370),
followed by terminal child alignment (208). Fine-grid pixel snapping must not
write over authoritative region centres. Other pending writers remain visible
in census-after-cell-compaction.log. Production remains cells off; zero-gate
and exact-geometry evidence for cells off is unchanged from the last full run.

### Preserve cell-derived centres at legacy snapping boundaries

Cell-bound nodes now retain their exact region-derived centres at legacy fine-grid
snapping boundaries, both before and after the routing adapter. Unbound snapping
is unchanged. Two tests pass, focused count 163. The subsequent census completes
673 cases in 16.117 s but is NOT clean: eight real frame-fit failures are exposed
(F42, F141, A162, A167, A169, EIP78, EIP266 and EIP271). These remain hard failures;
no gate or checker was weakened. Remaining writer catches and first-failure
paths are in census-after-cell-centres.log. The next common writer is terminal
child alignment. Full cells-on legality and geometry comparison remain pending.

### Whole-cell terminal-child alignment

Terminal-child alignment builds the complete cross-axis reservation plan first.
A blocked plan leaves both ownership and ordering metadata unchanged; hinted
children remain fixed. Four new tests pass, focused selection 167. The census
runs all 673 cases in 16.2115 s, with 664 diagnostic passes and nine hard frame-fit
failures (see census-after-cell-terminal-child.log). The next most common writer
is DisconnectedPacking, 43 cases. This is still an incomplete cells-on pipeline,
not a passing cells-on audit. Cells-off production is unchanged.

### Whole-cell disconnected-component packing

Disconnected packing reuses graph/root-group membership, enumerates complete
whole-cell row packings, scores their forward-derived geometry and publishes
only the best legal improvement. A hinted member anchors its whole component.
Two tests pass, focused count 169. The census runs 673 cases in 14.9393 s with
nine hard frame-fit failures and remaining writer catches. DisconnectedPacking
is no longer caught. The largest combined stage category is GroupFrameStage
(27 writers); the next individual paths are listed in the census log.

The requested enterprise.docx regeneration completed successfully in 116.162 s
using the current Release ARM64 WinForms build:
.\src\Converter\bin\Release\net10.0-windows\win-arm64\Converter.exe -in enterprise.md -out enterprise.docx
The converter reported no rendering errors. The generated DOCX stays uncommitted.

### Whole-cell decision input alignment

Unbranched decision input chains now build one complete cross-axis cell plan;
return edges do not drive the forward chain. Locked inputs stop automatic
alignment, and any occupancy failure leaves the complete chain unchanged.
Three tests pass, focused count 172. The census-after-cell-decision-inputs.log
records the next writers and unresolved frame fits; no cells-on gate pass is
claimed. Production and independent text spans remain off.

### Parent-grid root container reflow

The cell-bound root reflow no longer runs an unbound local layout or copies
its pixel coordinates back. It compacts private logical member regions,
enumerates complete parent-grid container plans under repair_candidate_moves
and repair_chain_depth, checks ownership and derived frame conflicts and then
publishes one improving assignment. Every rejected trial restores the original
regions and gaps. Two tests pass, focused count 174. The census visits 673 cases
in 17.9431 s with eleven hard frame-fit failures; A165 and A168 are newly exposed
after their earlier root write is removed. Remaining writer paths are recorded
in census-after-cell-root-packing.log. No full cells-on gate pass is claimed.

### Whole-cell terminal fan centring

Odd terminal fans now plan parent and unbranched input-chain reservations on
the middle child's logical cross-axis start. Ordering metadata changes only
after acceptance. The input-chain planner is shared with decision input
alignment. Two additional tests pass, focused count 176. The exact cells-off
geometry rerun after root packing remains unchanged for all 673 scenarios
(42.015 s); no pixel results were used to infer cell regions.

### Whole-cell terminal side branches

Terminal branch placement now proposes integer logical side lanes, measures
forward-derived clearance and prepares the complete occupancy before applying
any node. Explicit hints remain fixed and exact rollback restores cells and gaps.
Three new tests pass, focused count 179. The post-conversion census log is
census-after-cell-terminal-branches.log. Cells-on audit acceptance is still
pending remaining guard catches and frame-fit failures.

### Whole-cell terminal decision outcomes

Two terminal decision outcomes now receive opposite side reservations in a
single plan, retaining spans and main-axis positions. A blocked second target
rejects both moves and ordering changes. Three tests pass, focused count 182.
The census-after-cell-decision-targets.log records the subsequent catches and
frame failures. The latest full cells-off audit emits all 673 summaries in
44.76 s, A142 13.6358 s, with all prior zero gates zero and unchanged totals:
4.2=44, 5.9=1694, 3.1=4, 4.1=0, 4.11=3, 5.8=0, shared pairs=2089.

### Whole-cell chain wrapping

Chain wrapping now proposes complete whole-span rows/columns and measured
label-clearance gaps in one transaction. The established wrap selector sees
only forward-derived reservation dimensions. A worse aspect ratio restores
the exact original snapshot, including gaps grown during the trial; a locked
tail rejects the complete plan before mutation. Four tests pass, focused count
186. The census-after-cell-chain-wrapping.log records remaining catches and
frame failures. The census is still not clean, so the full cells-on gate audit,
layout-difference grouping and final speed work remain pending.

### Whole-cell independent input fans

Independent fan-in compaction plans every parent's reservation before applying
any move. Anchored inputs determine an integer logical pitch, and incompatible
anchors or occupied targets prevent a partial fan. Three tests pass, focused
count 189. The next writer census is in census-after-cell-fan-in.log. Full
cells-on legality acceptance and final profiling are still pending the clean
census; production cells and independent text spans remain off.

### Whole-cell circular junction alignment

Circular fan-out and collector chains now plan complete cross-axis cell
reservations, retain locks, and reject all moves on collision. Half-cell
medians resolve to a deterministic whole logical start without fractional
occupancy. Three tests pass, focused count 192. The next census is recorded
in census-after-cell-circular-fans.log; acceptance remains pending the remaining
writers and eleven frame-fit failures. The legacy resize paths also need care:
the revised specification permits growth for measured allocated-port capacity,
not neighbour count alone. Do not activate independent text spans during this
conversion.

### Measured port-capacity cell growth

The crowded-side cell branch now measures allocated boundary ports using the
existing renderer-derived pitch policy. It offers whole-cell span increments
only for an actual measured separation shortfall; a sufficient boundary never
grows just because of neighbour count. Ineligible sides need reassignment,
and occupied growth rejects the complete plan. Three tests pass, focused count
195. The census runs 673 cases in 14.6221 s with eleven frame-fit failures;
GrowCrowdedSides is no longer caught. The remaining legacy parallel-label resize
is distinct and still pending conversion. No independent text-span selection
or production cell activation has been enabled.

### Whole-cell lone nested-group centring

Nested-group centring builds one complete translated member plan from logical
region envelopes. It keeps hinted groups fixed and rejects every translated
member when a target is occupied. Three tests pass, focused count 198. The
673-case census runs in 15.1441 s, with 15 hard frame-fit failures now exposed
past their former writer catches. It remains an incomplete experimental run;
production cells and independent text spans remain off, and the next pending
writers include parallel relationship sizing and junction branch compaction.

### Whole-cell parallel-label extent conversion

The parallel relationship path retains its existing measured label-track extent
calculation, but collects all affected minimum extents before planning any
whole-cell spans. A collision or hint rejects the entire resize plan. This is
a conversion of the existing label-clearance policy, not a claim that label
track demand equals minimum port separation. Three tests pass, focused count
201. The census-after-cell-parallel-extents.log records subsequent writers and
frame-fit failures. Production cells and independent text spans remain off.

### Whole-cell junction branch compaction

Aligned junction branches now propose nearest whole-cell side tracks, preserving
occupied-rank order and measuring forward-derived body clearance. Collector
alignment shares the circular-junction planner and joins the same complete
reservation plan. Three tests pass, focused count 204. The subsequent census is
recorded in census-after-cell-junction-branches.log; full cells-on acceptance
is still pending remaining writers and frame-fit failures.

### Preserve authoritative overlap validation

The legacy pixel rank-band overlap pass no longer moves cell-bound nodes whose
regions already enforce exclusive occupancy and mandatory body containment.
This preserves legal same-row relative hints even when original native ranks
differ. Two tests pass, focused count 206. The 673-case census runs in 19.3786 s
with sixteen frame-fit failures still exposed; remaining writer paths are in
census-after-cell-overlap-boundary.log. Full cells-on audit acceptance is pending.

### Whole-cell partner-rank preparation

Specialised partner preparation plans required track-port extents and both
rank lane assignments together. It shares the measured extent planner with
parallel sizing, never resizes and then aligns nodes separately. A rejected
specialised candidate leaves ownership untouched and ordinary joint allocation
handles the declared links. Three tests pass, focused count 209. The next
census is census-after-cell-partner-preparation.log. Production remains cells
off and no independent text-span selector has been enabled.

### Authoritative partner channel gaps

Partner channel repair now grows measured shared external gaps with all affected
nodes in one plan. It never translates pixels or edits frame edges by hand;
frames are rebuilt from accepted regions. Internal gaps are not used as routing
corridors. Hinted participants reject all gap growth unchanged. Three tests
pass, focused count 212. The census-after-cell-partner-gaps.log records the
next pending writers and frame-fit failures. Full cells-on acceptance remains
pending the clean census.

### Whole-cell sparse-rank compaction

Sparse rank bands reuse complete logical lanes across disjoint main-axis
strips. Rank-skipping connections keep separate lanes and their measured
corridor clearance grows as shared external gaps. Hinted gap participants
reject complete candidates unchanged. Three tests pass, focused count 215.
The next census is census-after-cell-sparse-compaction.log; full cells-on
acceptance and final profiling remain pending the clean census.

### Whole-cell independent terminal inputs

Independent terminal inputs now plan complete beside-collector reservations,
check measured clearance and shared occupancy, and update logical rank only
after the full assignment is accepted. Hinted inputs keep their region and
rank. Three tests pass, focused count 218. The next census is recorded in
census-after-cell-terminal-inputs.log; full cells-on acceptance remains pending
remaining writer paths and hard frame-fit failures.

### Whole-cell large-fan packing and pure candidate projection

Large fans now keep leaf spans fixed, measure hub port capacity, and evaluate
every finite whole-cell column-count plan before publishing the cheapest legal
complete assignment. Track room grows only a shared external channel gap;
leaf track/label spacing uses whole-cell strides. Prepared cell states project
geometry without temporary mutation, and foreign-grid snapshots are rejected.
The hub never grows merely to fill a block or lower empty-space score.

Fifteen new tests pass, focused count 233, covering 20/30-way grids, all four
physical directions for fan-in and fan-out, hinted complete rejection and pure
projection matching exact applied geometry. The census runs all 673 cases in
19.9981 s, with 657 diagnostic passes and sixteen hard frame-fit failures.
Only twelve first-writer catches remain: horizontal retries, decision detours,
vertical loops, simple decision merges, shared fan/tails, grouped channels,
group member packing, and two later grouped-placement paths. The census is
not clean; full cells-on gates, layout-difference analysis and final profiling
remain pending. Production cells and independent text spans stay off.

### Whole-cell decision detours

Decision detours, merges and optional early terminal inputs now form one
complete reservation plan; rank metadata changes only after acceptance.
Four tests pass, focused count 237. The next census is recorded in
census-after-cell-decision-detours.log. Remaining writers and frame-fit
failures still prevent full cells-on acceptance.

A real coordinate consequence is recorded for later mandatory-span/routing
work: with base height 90 and row gap 60, a centred base diamond reserves two
rows (height 240, centre 120), while a one-row rectangular node centres at
45 plus whole 150-pixel row pitches. Whole-cell moves alone cannot align those
centres. A compatible mandatory span candidate may be needed when independent
span selection is enabled; do not introduce forbidden per-node pixel offsets.
Production cells remain off; F32/EIP261 stays parked for routing work.

### Whole-cell horizontal retries

Horizontal retry placement now plans successful outcomes, retry regions,
measured departure gaps and accepted-only rank changes together. The prepared
geometry is rechecked before publication; unresolved clearance never leaks
an accepted region. Three tests pass, focused count 240. The post-conversion
census is census-after-cell-horizontal-retries.log. The remaining writers and
hard frame-fit failures still prevent a complete cells-on acceptance audit.

### Whole-cell vertical loop branches

Vertical loops and their complete continuing tails now use opposite logical
cell lanes in one ownership plan. An occupied or hinted late tail member
rejects both the tail and return branch. Three tests pass, focused count 243.
The next census is census-after-cell-vertical-loops.log; full cells-on acceptance
remains pending the remaining writers and hard frame-fit failures.

### Whole-cell simple decision merges

Both independent branches and their terminal merge now form a single complete
side-lane assignment. Hinted or occupied merge targets retain all selected
reservations unchanged. Three tests pass, focused count 246. The next census
is census-after-cell-simple-merges.log. Full cells-on acceptance is still
pending remaining shared/grouped writers and hard frame-fit failures.

### Whole-cell shared fan roots and tails

Shared roots and all selected unbranched tails now use one complete reservation
plan. A late tail collision or hinted root rejects the whole assignment.
The output-chain collector is shared with vertical loop placement. Three tests
pass, focused count 249. The next census is census-after-cell-shared-fans.log;
remaining grouped writers and frame-fit failures still prevent full acceptance.

### Whole-cell standalone group member packing

Standalone artifact groups now compare every finite declared-order column
count through pure prepared cell states, preserve whole-region centring and
publish only the best complete legal improvement. A fixed end member rejects
all improving candidates without live trial mutation. Two tests pass, focused
count 251. The subsequent census is census-after-cell-group-members.log;
remaining grouped-placement writers and hard frame-fit failures still prevent
full cells-on acceptance. Production cells and independent text spans stay off.

### Whole-cell grouped many-to-many channels

Complete unhinted group pairs now measure source/target side capacity separately,
plan both complete memberships and compare external-gap/whole-column channel
alternatives before publication. Unrelated nodes keep their spans. Hinted gap
participants stay exact through a legal whole-column alternative. The partner
rank detector delegates these same complete group pairs to channel placement,
preventing obsolete axis and port-size assumptions from being retained.

Candidate score projection is now shared by fan, standalone-member and channel
planners with each caller's existing empty-space policy unchanged. Four tests
pass, focused count 255. The next census is census-after-cell-grouped-channels.log;
remaining grouped/external leaf writers and hard frame-fit failures still prevent
full cells-on acceptance. Production cells and independent text spans stay off.

### Atomic grouped leaf placement and shared frame projection

Grouped leaf placement now uses a shared complete region/gap planner rather
than pixel assignments. Both sides are offered before farther candidates;
search uses the existing repair_candidate_moves limit across leaf placement.
Prepared frames and body bounds are checked before publication, including
new frame conflicts, title bands, geometry-provider side eligibility and body
collisions. Rounded gap growth is retained exactly across private prefix plans.
Projected nested/empty frames match accepted geometry in both coordinate views.

Nine focused tests for grouped leaves, frame projection and the tested shared
external mode pass, focused count 264. Only the grouped caller is connected in
this checkpoint; the external caller is the next conversion. The 673-case census
runs in 21.0747 s with seventeen hard frame-fit failures (A142 is newly exposed)
and four first-writer catches: external leaves in F59/A56 and same-rank labelled
gap growth in A58/A138. This is not a passing cells-on audit. Production cells
and independent text spans stay off; F32/EIP261 remains parked.

### Connect outside leaves to the shared cell planner

The external-leaf caller now uses the same tested complete region/gap planner
as grouped leaves. Its regression test calls the real dispatcher with the hard
guard active. All 264 focused tests pass. The next census is recorded in
census-after-cell-external-leaves.log; same-rank labelled-gap growth is the next
newly exposed writer. Frame-fit failures still prevent full cells-on acceptance.

### Complete same-rank labelled gap candidates

Same-rank terminal label clearance now plans the complete regions and cumulative,
rounded external gaps before publishing once. Four new tests cover both flow
axes, hinted rejection with exact restore, and cumulative rounded gap growth.
All 268 focused tests pass. The next 673-case census completes in 21.6492 s:
656 pass, seventeen frame-fit failures remain, and no direct pixel writer is
caught. This is not a passing cells-on legality audit. Production cells and
independent text spans remain off; F32/EIP261 stays parked. The next producer
blocker is fitting derived frames under logical hints and the named budgets.

### Separate audit timing from diagnostic work (2026-10-18)

Stage profiling is now opt-in with ROUTE_AUDIT_PROFILE=1; ordinary Release
catalogue runs retain all checks, snapshots, findings and summaries. The old
completion/final-diagnostics interval also contained eleven final route-search
calls. A new enclosing final-route-searches mark separates those from actual
final diagnostic collection. No search or legality check was removed.

Hint cleanup now skips the full route/obstacle/crossing checker when there is
no active unresolved hint whose diagnostic could be cleared. Completion also
avoids creating/restarting its profiling stopwatch when no timing observer is
installed. On the latest profiled run, actual final diagnostics total 25.7221 ms.
Subtracting the nested searches from the pre-change same-run interval gives
83.1609 ms; the estimated diagnostics saving is 57.4388 ms, not twelve seconds.

Before optimization: profiling on 37.20 s / A142 10.6066 s; profiling off
35.59 s / A142 10.6092 s. Final sequential runs: profiling on 43.05 s / A142
13.0169 s; profiling off 41.63 s / A142 12.2676 s. The historical profiled run
was 45.56 s / A142 14.8603 s; c4895ff's reference is 37.30 s. Timing variation
is larger than the small diagnostics saving; no full-audit speedup is claimed.

All 673 summary rows compare identically byte for byte. Every gate total stays
unchanged: 4.2=44, 5.9=1694, 3.1=4, 4.1=0, 4.11=3, 5.8=0, shared pairs=2089;
every previously zero gate remains zero, with the same 25 failing scenarios and
five native layout failures. A diagnostic-aware geometry harness compares the
exact ordered serialized final findings and page geometry: 673 scenarios,
zero differences. All 268 focused tests pass. The cells-on census still has
seventeen frame-fit failures, no caught direct writer, and production remains
cells-off. Diagnostic/timing logs use the diagnostics-* prefix under
out/logical-grid-prerequisite. F32/EIP261 remains parked.

The final strict comparison uses serialized ordinal equality (including finding
order, messages and escaping), not only JSON structural equality: 673 scenarios,
zero changes, zero missing, 34.301 s. The original cells-off geometry capture
also matches all 673 with zero changes/missing, 42.139 s. Five routing-hint tests
pass in addition to the 268 focused tests. No DOCX file is committed.

### Reconcile the historical 662 first catches (2026-10-18)

The requested grouping already existed in the original CSV: 648 initial
PlaceOnGrid -> SetMain writes, 13 PlaceRelative -> Translate writes and one
PushToBoundary -> Translate write. Initial placement was converted first in
9eddf0f (144 focused passes), clearing its 648 original blockers. Placement
hints followed in 0a65ada (155 passes), clearing the remaining 14 before later
repairs. docs/cell-census-first-catch-review.md records all 35 finished
conversion checkpoints, tests, before/after catch counts, and how many scenarios
get past the immediately preceding first writer. No conversion was repeated.

Two fresh full censuses confirm zero caught writers. The native-reason run
completes all 673 cases in 21.5003 s: 645 completed bound grid scenarios, 17 hard
frame-fit failures, six completed unbound open-mode scenarios, and five known
pre-binding exits. Primary nodes: 3,833 total, 3,806 bound, 27 open/unbound.
The focused selection passes all 268 tests again.

The five exits are not new Dagre failures: F65/F70/F87/F88 are CatalogueRemarked
and NativeRenderCapabilities excludes them before Dagre is attempted; A01 is
the valid empty/header-only diagram and has no nodes to bind. The probe now
reports the precise skip predicate and original recognition diagnostics without
changing its outcomes or the production path. F151/F152/F153 bind all 160 nodes
and complete the current probe. Native skips have not been repaired or removed
from the audit. The seventeen frame-fit failures remain open, not accepted as
zero legality findings. Production cells and independent spans stay off, the
six open-mode cases stay unbound, and F32/EIP261 remains parked. No engine,
rulebook, baseline, score weight, geometry or DOCX change is part of this review.

### Remove the terminal-child centre policy from cells (2026-10-18)

Spine conversion is already checkpointed in 777df62. Whole-empty-strip
compaction is already checkpointed separately in 593fcb4. That planner removes
only globally empty cross-axis rows/columns, preserves region spans, retains
existing grown gaps, and grows any required external boundary to at least the
caller's minimum. It does not shrink external gaps. One complete region/gap
assignment is accepted or rejected; a hinted region vetoes an incompatible
plan. Its three tests cover both axes, the minimum gap, hint rejection and
exact region/gap rollback. No second compaction rewrite was needed.

Inspection confirms that legacy AlignSingleTerminalChildren only aligns a sole
terminal child's centre with its parent's cross axis. The previous experimental
conversion e3fee8f had translated that policy into starting-cell alignment.
Per the clarified instruction, that cell helper is now deleted entirely, and
both pre-placement Column rewriting and post-placement alignment are skipped
for cell-bound nodes. The unbound legacy branch is unchanged to preserve
production geometry. Six regression cases exercise the real dispatcher across
both axes and both phases, unequal spans, ordering preservation and hinted
ownership. They replace the four tests for the withdrawn alignment policy;
the focused selection increases from 268 to 270 passes, retaining the original
158 tests.

Historical census after spine: 388 compaction first catches and 208 terminal
child first catches. After compaction, the next dominant writer was centre
snapping (370), already disabled for bound nodes in f046ae7. Once that boundary
was cleared, all 208 terminal-child catches were passed by e3fee8f; the next
largest group was disconnected packing (43), subsequently converted in 39508b6.
These are historical early-exit census results, not current outstanding writers.

Fresh before-removal census: all 673 cases, 21.3973 s, no writer catches,
645 completed bound grid cases, the same 17 hard frame-fit failures, six open
cases completing unbound, and five known pre-binding skips. After removal:
all 673 cases, 20.6686 s, identical status for every case and no writer catches.
No next writer group is exposed. The seventeen frame-fit failures remain open;
this is not a passing cells-on legality audit. Logs use census-compaction-
confirmed-before-terminal-removal and census-after-terminal-centre-policy-removal.

Fresh cells-off audit: all 673 byte-identical summaries in 35.41 s, A142
10.6803 s. Every previously zero gate remains zero; unchanged totals include
4.2=44, 5.9=1694, 3.1=4, 4.1=0, 4.11=3, 5.8=0 and shared pairs=2089. The same
25 known failed scenarios and five pre-binding skips remain. Exact comparison
against authoritative-cells-before.jsonl reports 673 scenarios, zero changed,
zero missing (33.246 s). Logs: terminal-policy-removal-off-audit.log,
terminal-policy-removal-off-geometry.log and terminal-removal-focused-tests.log.
Production cells and independent spans remain off; F32/EIP261 stays parked.
No DOCX files, baseline rows, rules or weights are changed in this checkpoint.

### Repair the eight reviewed cell-frame failures by score (2026-10-18)

Snapping was already committed in f046ae7. The detailed cause, measured
shortfall and accepted assignment for each of F42, F141, A162, A167, A169,
EIP78, EIP266 and EIP271 is in docs/cell-frame-fit-eight-review.md. All eight
now complete the full experimental cell-bound pipeline. No rule conflict was
found in the requested eight; no checker condition, rule, baseline or weight
was changed. Node spans remain fixed.

Frame fitting now compares complete external-gap, whole-cell unit and combined
assignments using the existing placement score including derived frames. Gap
and move alternatives compete; no fixed repair order is imposed. The old
translation-distance tie-break and chain-depth-as-distance cap are gone.
Empty-frame collisions also offer whole connected-component moves. Actual
hint relationships are validated in cells, allowing external gaps without
pixel pins. Only legality repairs may change a hinted region while preserving
its declared start alignment and direction; ordinary optimization locks and
manual absolute pins remain strict. Transactions remain atomic with exact
region/gap and frame-cache rollback. Existing named search limits remain.

All 277 focused layout tests pass (the original 163 included), plus eight new
non-explicit catalogue regressions that require completion rather than merely
passing a first-catch diagnostic. The full census improves from seventeen hard
frame-fit failures to two (A164/A165); no direct writer is caught. Completed
bound cases rise from 645 to 660; the six open scenarios stay unbound and the
five known native skips remain. Census time is 26.4261 s. These two unresolved
cases are search limitations, not proven rule conflicts; full cells-on legality
acceptance remains pending. F32/EIP261 stays parked and production cells and
independent spans remain off.

Fresh cells-off audit: all 673 identical summaries, 35.05 s, A142 10.6321 s.
Every previous zero gate remains zero and all quality totals remain unchanged.
Exact page geometry and ordered serialized diagnostics compare identically for
all 673 (33.859 s). The detailed report names every evidence log. No DOCX files
are part of this checkpoint.

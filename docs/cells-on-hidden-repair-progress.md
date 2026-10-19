# Repair the cells-on hidden-failure groups

## Group 1 — duplicate joint-search identities

The recognition database retains a node-shaped alias when an edge names a group. Dagre excludes that alias from placed leaves and represents the identity as a cluster. The joint route candidate producer incorrectly concatenated all recognition node IDs with all group IDs, producing the same movement-unit key twice. No physical node was placed twice.

Leaf movement units now come from actual placed cell owners. Group units still come from declared groups and include their complete member regions. Recognition metadata is preserved. The dictionary remains strict: a repeated physical owner throws, and different nodes cannot occupy shared cells. Two focused alias/single-owner regressions pass.

All 23 original hidden-group cases were run with cells on and unchanged assertions: **23 pass, zero fail**. Every case reached its original assertions; no downstream failure needs deferring. The test-wide compatibility flag was changed only for this selected validation run and restored exactly afterward; no existing fixture or assertion change is committed.

Acceptance: all 673 catalogue comparisons pass against 60d1fb1, including per-scenario bend/crossing nonincrease. Catalogue **2999 bends / 2095 crossings**; original 81 **1118 / 1100**. A104 **38 / 0**, A105 **28 / 0**, same-fan crossings zero. All hard gates zero, no per-scenario finding increases; quality totals unchanged (4.2 52, 5.9 1190, 3.1 2, 4.1 0, 4.11 5, 5.8 3, shared pairs 337). Five existing native-placement cases remain explicitly unmeasured.

Unprofiled Release audit **49.87 s**, A142 **9.620626 s**. Normal layout suite: **1114 pass / the same 216 failing identities / 1330 total**. No added or removed failure identities. No rule value, hint, independent-text setting or candidate limit changed. No DOCX regenerated. The audit and normal suite retain their existing nonzero exit statuses for known quality/test failures.

Evidence under out/markup-review: hidden-group1-cells-on.log and its 23-case TRX; hidden-group1-identity-tests.log; hidden-group1-catalogue.log and JSONL; hidden-group1-audit.log/stdout/timings; hidden-group1-finding-increases.txt; hidden-group1-layout-suite.log and failure list.

Held for approval in group 1: none. Later groups have not been changed.

## Group 2 — rejected trial; stop here

Group 1 is committed as `962fe1f` (Build joint route units from actual cell owners). Group 2 is not committed. All three trial source changes have been removed; their patch is archived at `out/markup-review/hidden-group2-rejected/tracked-changes.patch`. Groups 3–6 were not started.

The trial preserved fractional terminal geometry, extended joint fan-arm moves through their input prefix, and promoted joint straightening candidates ahead of the existing candidate budget. It temporarily fixed the two original crowded rounded-box terminal cases and F58's collector-side case: **3 of 18 original cases passed, 15 failed**. The selected run included six additional rounded-box controls: **9 pass / 15 fail / 24 total**. These fixes were reverted together because the group failed acceptance; all 18 original group-2 cases remain open in the retained state.

Remaining original cases in the trial:

- Retry-input alignment: TB, BT, LR and RL (four cases).
- ResolveGuidePoints_LayeredView_FarChildUsesGridRoute.
- TryLayout_LayeredView_FarChildLeavesFromFacingSide.
- TryLayout_MessageFilter_BranchesEnterLeftSideCentre.
- TryLayout_MessageFilter_UsesDownstreamExitAndUpstreamEntry.
- TryLayout_HorizontalRetry_DoneSharesDecisionRowAndRetrySitsBelow.
- TryLayout_PipelineWithEarlyExit_PreservesSpineAndSeparatesFinalBranches.
- TryResolveLayoutGuidePoints_F32_FanIn_SelectsTopFaceSourcePortsAndBottomCentreTarget.
- TryResolveLayoutGuidePoints_F59_NoBranch_UsesSidePortFacingWaitAndWaitCentreTarget.
- TryResolveLayoutGuidePoints_F58_ColumnAlignedEdges_AreStraight.
- RowWrapLeavesBottomAndEntersTop.
- LayeredViewStaysCompact_AndConnectorsClearEveryBodyIncludingTheirEnds.

The layered view still measured 6826 against its 3500 width limit; row wrap still used four bends rather than two. Retry straightening candidates were offered but rejected because their completed routes introduced crossings. A joint track solution is still needed; the crossing guard was not weakened.

### Acceptance failure

The catalogue comparison found **24 scenarios gaining bends or crossings**, including eight protected scenarios:

| Scenario | Before bends/crossings | Trial bends/crossings |
|---|---:|---:|
| A28 | 0/0 | 2/0 |
| A98 | 0/0 | 4/0 |
| A110 | 180/463 | 180/464 |
| A117 | 138/86 | 138/88 |
| A134 | 0/0 | 2/0 |
| A136 | 0/0 | 2/0 |
| EIP36 | 0/0 | 2/0 |
| EIP342 | 2/0 | 3/0 |

All 24 are recorded in `out/markup-review/hidden-group2-regressions.json`. Three per-scenario diagnostic increases were observed: A110 shared pairs 60→61, A117 shared pairs 16→18, and A54 rule 5.9 crossings 0→1.

A115 threw ArgumentOutOfRangeException in ModelRouteCompletionStage.RepairFinalFacingSides during reference candidate recovery. Consequently only **672 of 673** catalogue summaries were captured. The partial sum 2840/1944 is **not a complete catalogue total**. Observed hard-gate counters were zero, but complete hard-gate acceptance cannot be certified with A115 missing. A104 remained 38/0, A105 28/0 and A52 6/0. The protected 81 aggregate was 1083/1092, but its eight individual regressions block acceptance regardless of aggregate improvement.

The incomplete unprofiled audit took **49.49 s**, A142 **8.304426 s**. A full normal-suite replay was not performed after the catalogue rejection; no unchanged failure-set claim is made for this rejected trial. The retained group-1 state has the measured **2999 bends / 2095 crossings**, audit **49.87 s**, A142 **9.620626 s**, zero hard gates and the same 216 known normal-suite failures.

Evidence: `hidden-group2-ordering-trial.log`, `group2-results/hidden-group2-ordering-trial.trx`, `hidden-group2-catalogue-trial.log/jsonl`, `hidden-group2-trial-audit.log/stdout.log/timings.csv`, `hidden-group2-finding-increases.txt`, under out/markup-review.

### Test held for approval

`TryResolveLayoutGuidePoints_F32_FanIn_SelectsTopFaceSourcePortsAndBottomCentreTarget` expects Corridor, whereas the engine returns Straight. For aligned allocated ports, rules 4.2 and 4.8 favour the zero-corner straight route. The assertion is unchanged and is held for approval. The far-child route-kind expectations are not yet classified as obsolete because their placement and facing requirements remain unresolved. Group 5's pixel-size expectations were not reviewed because work stopped at group 2.

No rules, candidate limits, hints or independent-span settings changed. No DOCX was regenerated. This rejected-trial report remains uncommitted; no group-2 acceptance commit was made.

## Follow-up: isolated terminal geometry, F32 and A115

The archived terminal-publication change was tried alone, without fan-arm prefix moves or candidate promotion. All eight crowded rounded-box variants pass, but the 673-case catalogue comparison fails A110 crossings 463→464 and A117 86→88. Full production catalogue: **2998 bends / 2097 crossings**. The additional manual-census audit emits all 673 summaries, with rule 5.9 1231 and shared pairs 346. It took **52.51 s**, A142 **9.657660 s**, with build/test contention; its manual mode differs from the accepted production audit and is not used for a before/after findings or timing claim. The independent production crossing regression already rejects this change. The change was archived as `out/markup-review/terminal-isolated-rejected.patch` and reverted. The precise-terminal requirement remains open; retaining an entire fractional polyline also changes interior tracks, so it cannot be retained solely on the strength of the terminal tests.

The approved F32 route-kind-only change was applied to both Corridor expectations. With cells on the test proceeds to a separate failure: the worker leaves from Left where Top is required. With the historical test defaults the same edit introduces a new failure: Corridor is still produced where Straight is now expected. No other expectation or fixture was changed. The approved patch is archived as `out/markup-review/f32-approved-expectation.patch`; it is not retained because the unchanged known failure set would gain an entry. Approval of Straight remains valid, but this single-expectation edit cannot yet satisfy the acceptance contract on both test paths.

A115 completes on the retained engine and on the isolated-terminal trial. The old failure required the rejected joint-candidate trial. Its unsafe boundary is nevertheless reproducible in current code: inject an empty route alongside a genuine 4.11 side-repair opportunity, then invoke RepairFinalFacingSides. The routine snapshots every route and unconditionally indexes both endpoints while comparing reallocated slots. The new regression first demonstrated the same ArgumentOutOfRangeException. The retained narrow fix compares path inventory before dereferencing terminals and requires two endpoints before rerouting an affected line. It leaves the enclosing candidate responsible for recovery/rejection. A broader early-return guard was discarded during diagnosis. Empty and one-point A115 trial inputs are tested; the assertions require real A115 completion first, a nonempty side search, no exception and an exact serialized-layout match. No checker or legality assertion is weakened.

## Deferred group-2 placement work

The observations below come from the original cells-on failures and rejected-trial traces. Candidate-generation diagnosis is distinguished from a completed-layout rejection: where there is no completed candidate trace, a specific losing score is not claimed. No further placement fix is included in this work.

### Retry input alignment (TB, BT, LR, RL)

Observed: TB/BT input cross coordinate 735 versus decision 405 (330 px apart); LR/RL 45 versus 300 (255 px apart). The current priority estimate requires every changed interface to become/stay straight and rejects occupied facing sides before final side allocation. Promoting the joint prefix/arm options made the straightening trial run, but its finished Check→Retry and Retry→Input paths cross; the crossing guard correctly rejects it (`hidden-group2-probe-tb3.log`). Needed: an atomic input/decision/continuation alignment plus a coordinated outside return track and port assignment, scored together. Moving the input alone does not solve that track conflict.

### Layered-view far child

Observed: H→T exits Top rather than Right, and the published guide has two points where the old grid-route test expects at least four. These are separate assertions: a straight two-point path alone does not prove the facing-side requirement is satisfied. Current joint movement families nominate bent interfaces; an already-straight but incorrectly allocated far-child side does not establish a straightening gain. Needed: a complete H child-block placement/side assignment that preserves all its sibling lines and checks the facing side after routing. No measured losing score exists for that missing complete candidate.

### Layered-view width

Observed: enterprise layered-compaction fixture width 6826 against <3500. The sparse-rank pixel compactor is not authoritative over bound regions; removing empty columns alone cannot reclaim columns reserved by overlapping rank envelopes. Needed: atomic reuse/packing of disjoint rank columns with skipping-edge side tracks included in the completed candidate. The current translation/alignment families do not construct that packing assignment; this is a missing option, not a demonstrated numeric tie.

### Message-filter branches

Observed: Drop target terminal X=525 instead of the target's left edge X=360; departure Bottom instead of downstream Right. Allocating each line independently preserves a legal but nonfacing assignment. A single side change must compete with the other branch's slot/track usage; the rejected global priority experiment did not resolve either assertion. Needed: joint downstream departures, left-side arrivals and distinct branch tracks, with the target block moved as one region assignment if necessary. No completed winning option was found; no checker relaxation is proposed.

### Horizontal retry

Observed: Done Y=555 versus decision Y=300; Yes does not continue along the decision row. The missing complete prefix/fork candidate and return-track conflict are the same structural issue as retry input alignment. Needed: jointly align input/decision/Done, put Retry below, and allocate the return channel before the finished score is compared. The rejected promotion experiment still failed this assertion.

### Pipeline spine

Observed: Parse X=1455 versus required spine X=885. A chain terminates at a fork in the current candidate producer; extending the fan arm through its input prefix was tried and rejected globally because other scenarios regressed. Needed: one complete spine/fork/collector assignment with early-exit tracks protected. There is no retained candidate proving that the required spine can be changed without displacing an already-straight interface or adding a crossing.

### F58 shared column and collector

Observed: Parse→HasMermaid route X=1620 instead of source column X=1455. Persist→Done enters Top instead of Right. The rejected promotion trial fixed the collector assertion but changed F58 overall from 11 bends/0 crossings to 10/2. Thus its finished layout loses the explicit no-new-crossings condition. Needed: jointly preserve the whole spine column, collector side slots and two noncrossing branch tracks; accepting the isolated side improvement is insufficient.

### F59 Wait

Observed: Wait's side displacement from its decision is zero; it remains on the spine rather than beside the decision. A side placement changes the branch/return interfaces as well as the desired decision→Wait line, so the present straight-interface priority is not evidence of a completed improvement. Needed: atomic decision-side-branch placement and its return/join track assignment, preserving all hints and existing straight interfaces. The rejected trial still failed; no measured completed winner is available.

### Row wrap

Observed: N35→N36 uses four corners instead of two, while the test also requires Bottom departure and Top arrival. The route-only candidate is constrained by the allocated terminal tracks; node translations alone do not reassign the complete fold channel. Needed: joint old-row/new-row terminal allocation and a clear two-corner outside wrap track, with both row blocks and any gap growth included atomically. No extra bend or special scenario exception is approved.

## Group 3 — reserve measured flow-axis caption room

The same-rank terminal gap planner was only called across the flow axis. Cell-owned terminal lines along the flow axis could therefore be published with less room than their measured text plus two visible stroke fragments. The group-fit stage now calls that existing atomic planner along the other axis as well, before fitting frames. It uses measured text, existing rounded gap increments, the existing candidate limit, frame-conflict checks and hint locks; no new score or parameter is introduced.

Ten original hidden cases now pass unchanged: all six originally failing straight-label axis/extent cases, both short influence captions, F26 long-label centring and F94 multiline centring. The expanded focused run changes from 2 pass/29 fail to **12 pass/19 fail** (31 total); the ten improvements are original hidden cases. Thus **10 of 22 original cases fixed, 12 still open**. The latter comprise six TB/BT fan-rounding cases, IndependentFan_RendersRoundedBends, the F32 curved-worker expectation, repeated LR captions, F125 branch-label placement, F93 square-size expectation and message-filter rendering.

Every bent path in the inspected drawing builders already passes through BuildRoundedCornerPathData. The fan and F32 assertions instead demand some curves even when the selected routes are all straight; adding a bend solely for these assertions would violate the no-more-bends requirement. These unchanged assertions are held for approval. F93's text centring assertions pass before its equal-width/height assertion fails (150×240); that shape-size assumption is held for the sizing review, not weakened here. Repeated-caption clearance, F125 and message-filter geometry remain live engine defects.

Acceptance: **673/673** production catalogue comparisons pass, no scenario gains bends or crossings; **2999 bends / 2095 crossings**, A104 **38/0**, A105 **28/0**, same-fan zero. All hard-gate counters remain zero; no per-scenario finding increases. Quality counts are unchanged: 4.2 52, 5.9 1190, 3.1 2, 4.1 0, 4.11 5, 5.8 3, shared pairs 337. Production audit **45.04 s**, A142 **6.885709 s**. The normal suite has **1114 pass / the same 216 failing identities / 1330 total**. Five existing native-ineligible cases still have explicit summary rows rather than invented zero measurements. No DOCX regenerated or rule value changed.

Evidence: followup-group3-before.log, followup-group3-gap-trial.log and their TRX files; followup-group3-catalogue.jsonl/log; followup-group3-audit.log/stdout/timings; empty followup-group3-finding-increases.txt; followup-group3-normal.log/TRX, under out/markup-review.

A115 guard validation before this increment: the narrow guard passed both reproducing tests, all 673 production comparisons and the unchanged normal-suite failure set, but audit runs were 51.94 s (A142 10.177406 s) and 54.05 s (A142 8.061575 s). It was archived and reverted at that point because timing acceptance was not established. Manual-census audit numbers are not comparable with these production runs. The gap increment's newer accepted timing allows the guard to be rechecked separately against this state.

## A115 guard checkpoint after the caption increment

The narrow endpoint-comparison fix was re-applied on the caption checkpoint. Both forced A115 inputs pass, all 673 production catalogue comparisons pass, no per-scenario finding increases occur, and the normal suite remains 1114 pass / exactly the same 216 failures. The audit produces all 673 summaries with hard-gate counters zero and unchanged quality totals, **2999 bends / 2095 crossings**, A104 **38/0**, A105 **28/0**, same-fan zero. Audit **51.32 s** (approximately the requested 51 s), A142 **9.6506139 s**. Earlier timing attempts are retained above rather than presented as source-level speed improvements. No existing assertion or fixture was altered for this safety fix.

Evidence: a115-guard-reproducer.log (the empty-input crash before the fix); a115-after-caption-tests.log (2 pass); a115-after-caption-catalogue.log/jsonl (673 pass); a115-after-caption-audit.log/stdout/timings; empty a115-after-caption-increases.txt; a115-caption-normal.log/TRX, under out/markup-review. The previous early-return guard was not restored; only the narrow comparison/reroute precondition is retained.

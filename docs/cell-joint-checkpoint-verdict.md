# Retain joint candidates against checkpoint 3d73b88

The implementation already exists in `edc9ba3`. Checkpoint `3d73b88` stays in
history unchanged. This review adds an explicit frozen checkpoint assertion to
the original 81-case replay; it does not change engine behaviour or routing.

## Retention conditions

The saved checkpoint replay and final replay contain the same 81 identities.
None has more bends after the joint candidates; 47 improve and 34 are unchanged.
Their total bends decrease from 1332 to 1118. The complete per-scenario comparison
is [cell-joint-checkpoint-retention.csv](cell-joint-checkpoint-retention.csv).
The new test uses the checkpoint counts as ceilings, independently of the
cells-off comparison. It also asserts zero path, diagonal and slot faults and
the unchanged candidate limit on every case.

The existing full audits establish these retention conditions:

| Measurement | Checkpoint | Retained joint candidates |
| --- | ---: | ---: |
| Unprofiled cells-on audit | 64.27 s | 49.71 s |
| A142 | 9.50791 s | 9.56872 s |
| Summaries | 673 | 673 |
| Illegal paths / missing routes / no legal route | 0 / 0 / 0 | 0 / 0 / 0 |
| Title-band violations / unrelated frame overlaps | 0 / 0 | 0 / 0 |
| Grid / off-grid nodes / off-grid frame edges | 0 / 0 / 0 | 0 / 0 / 0 |
| Off-slot ports / groups | 0 / 0 | 0 / 0 |
| Hint-accounting faults / cleanup corrections | 0 / 0 | 0 / 0 |

Cells off took 36.63 s (A142 11.2999 s), with all 673 original ordered summary
rows byte-identical. Quality findings are not zero: cells on has 4.2=52,
5.9=1231, 3.1=2, 4.1=0, 4.11=5, 5.8=3 and 343 shared-track pairs. Five native
cases remain unmeasured. Neither the audit's known nonzero exit nor those
quality counts are described as a zero-all-rules result.

## Remaining speed cost and proposed next step

Final profiled wall time is 67.07 s; the separate unprofiled measurement is
49.71 s. The following are exclusive instrumented costs from the profiled run,
not an allocation of the unprofiled 49.71 seconds:

| Category | Seconds | Calls |
| --- | ---: | ---: |
| Routing completion | 29.4736 | 8462 |
| Scoring outside routing, including proposal planning | 3.4496 | 1082 |
| Frame search | 3.1797 | 254 |
| Hint validation | 0.6286 | 61514 |
| Final diagnostics | 0.0562 | 4380 |

The remaining 30.2823 seconds of profiled wall time is outside these categories:
native placement, other pipeline stages, audit comparisons/output and startup,
plus profiling overhead. It has not been individually attributed. Inclusive
timers overlap these categories and cannot be added to them.

Routing remains the largest measured cost. The next proposed step is to split
its cost between initial completion, affected-route trials, full winner
confirmation and reference recovery. Then reuse prepared immutable obstacle,
title-band and topology data across completions with exactly the same geometry
and hints, retaining complete invalidation/restoration at candidate boundaries.
Measure hit rates and cost first; keep it only with identical route geometry,
all summaries/findings and the 81-case retention assertion. Do not remove full
confirmation, reduce candidate coverage, weaken dependencies or change a limit.
This is a proposal, not another implemented cache. The 45-second target remains
open by 4.71 seconds; A142 already meets fifteen seconds.

## What the two requested candidate kinds actually do

**Complete root and fan.** Build the root plus every outgoing branch and its
unbranched tail, include hint-dependent followers, and translate the complete
set in whole cells. Keep relative member positions and all spans. It can
improve external connections or obstacle clearance; translating an isolated
unobstructed component cannot straighten its internal links and is not promoted.
EIP215 is the measured winner of this family: 12 to 4 bends. Eligibility elsewhere is not evidence of an improvement.

Concretely, root/fan proposals are available in shared-fan A21, A40, EIP162,
EIP215, EIP246, EIP36, EIP46, EIP63, EIP83, EIP99, EIP342, F30 and F120.
They are also available in main-chain A112, A136, A16, A30, A98 and F61.
Only EIP215 selects this family; other gains predominantly select the separate
combined incoming/outgoing `fan-sides` assignment. The historical cohort names
do not imply every source is a simple output fan. A103 has no eligible rigid
whole-fan translation; its improvement is from `fan-sides`.

**Atomic whole-chain alignment.** Extend a nominated middle node through its
unique predecessor and successor, including a previously straight prefix.
Plan starting-row/column assignments or a rigid whole-chain move before applying
anything. Respect occupied cells, gaps, frame membership and hints. A compatible
fully hinted simple chain also gets its explicit directed layout as one
assignment. A161 selects this extension: 26 to 0 bends with seven unchanged
leave-North hints. It is in the historical shared-fan cohort, not main-chain.

Plain chain alignment is eligible for A141 but is not scored within the retained
ordering; whole-chain translation proposals exist for A112, A136, A16, A174,
A29, A30, A98, EIP211 and EIP241 in the main-chain cohort. Some reach scoring;
none is the final winner there. Those cases' measured gains come from
`fan-sides`, not a demonstrated win from plain chain alignment. A110's dense
block has no eligible joint proposal under the existing incident-line bound.
F61 is many-to-many and A141 is cyclic, so a simple-chain family alone is not
a claim to solve them. Per-case availability and actual examined/winning family
are in [the fifty-case CSV](cell-joint-candidate-bends.csv).

There is one global budget of sixteen distinct complete assignments, not
sixteen per family. Canonical target identities deduplicate existing and new
options. Existing options retain their relative order; high-confidence joint
options are interleaved only when the complete changed interface can straighten
without bending another currently straight interface. Other options are
deferred. Final route scoring and full confirmation decide acceptance.
Interleaving necessarily can displace a later distinct option; there is no
general proof it cannot displace a winner on an unseen source. The concrete
protection requested here is the unchanged-or-better result on all 81 and the
source regressions. Earlier interleaving trials that lost wins were discarded.

## Ten fan-in cases: necessity versus missing search

Counts are cells off / checkpoint / retained. None of the unresolved cases
below has a proof that its extra bends are the minimum legal cell layout.

| Scenario | Bends | Verdict and next missing alternative |
| --- | --- | --- |
| A132 | 2 / 7 / 0 | Resolved by joint incoming/outgoing hub-side assignment. |
| A134 | 2 / 4 / 0 | Resolved by joint hub-side assignment. |
| A22 | 2 / 7 / 0 | Resolved by joint hub-side assignment. |
| A41 | 0 / 2 / 0 | Resolved by joint hub-side assignment. |
| A82 | 0 / 2 / 0 | Resolved by joint hub-side assignment. |
| A173 | 0 / 6 / 6 | orders->order, billing->order and orders->warehouse each retain two bends. Need coordinated convergence and outgoing-arm placement with the legend reservation. Three new side options were scored; none replaces the retained winner. No hint or legality contradiction is established. |
| A42 | 0 / 4 / 4 | cloudFirst->onPrem and buyNotBuild->saas each gain two bends. Shared targets couple the two influence fans (audit also feeds onPrem). Need joint overlapping-fan assignment, rather than treating each root's output tail independently. Three side options and one rigid fan move reach scoring. Necessity is unproved. |
| A46 | 15 / 16 / 16 | shop/suite/checkout/ordering/place form intersecting relationship families, not a simple median fan. Some links improve while shop->checkout grows from one to four bends. Joint side/slot and arm ordering is not supplied as one improving complete option in the examined sixteen. Available new proposals are deferred. No exact hint conflict or minimum-bend proof exists. |
| A72 | 8 / 9 / 9 | pick->pack changes from straight to two bends while picker->pick improves two to one. Need coupled placement/side assignment for picker, pick, packer and pack, preserving both assignment and derived triggering tracks. Current new proposals are deferred. Extra bend is not proven necessary. |
| EIP261 | 2 / 4 / 4 | C->B grows two to four bends; the three parallel C->DB exchanges compete for sides/tracks. Needs joint side/slot/track alternative including the repeated exchanges, not another median move. One side option reaches scoring. F32/EIP261's parked routing refinement remains untouched. Necessity is unproved. |

## Four individual cases

| Scenario | Bends: off / checkpoint / retained | Verdict |
| --- | --- | --- |
| A47 | 6 / 8 / 4 | Resolved: complete hub-side assignment improves both comparisons. |
| A114 | 108 / 143 / 143 | Cross-group order-handling graph. For example orderApi->ordering and payApi->payment increase two to four bends; payment->mobile zero to four. Need frame/channel-aware coupled group-and-arm alternatives. Eligible fan/chain plans exist but are deferred; the examined shortlist has no accepted improvement. More than twelve incident lines can exclude large joint units. No unavoidable-bend or conflicting-hint conclusion follows. |
| A117 | 99 / 138 / 138 | Roadmap plus gap and work-package associations. baseline->t1 and t1->t2 grow two to four bends; t2->target grows zero to four. Aligning the roadmap alone ignores its g1/g2/g3 and deliverable/implementation interfaces. Need coupled rank/channel assignments, not centre alignment. Existing proposals do not supply an accepted improvement. No minimum proof exists. |
| F136 | 8 / 14 / 14 | Partner alignment was removed intentionally. Web->Index, Api->Index and Api->Cache each gain two bends. Need a joint partner-position and side/track assignment. The observed four-line facing option removes both side findings but creates a shared-track finding; the six-line option also leaves a shared track. These are concrete rejected alternatives, not a proof all legal layouts need fourteen bends. |

## Four open side-search cases

| Scenario | Exact observed competing result | Verdict |
| --- | --- | --- |
| F136 | Four-line facing assignment clears 4.11/5.8 but adds one 5.1 finding; six-line assignment still shares a track. | Missing coupled position/side/track alternative; no proven rule conflict. |
| A63 | shop is a containing frame with checkout/search members and parent-child links. Two/three-line side options remove one/both side findings but add one/two 4.2 findings or a 4.9 finding (some also add a crossing). | Missing complete frame-endpoint/slot/route alternative. Current legal result is retained; extra bends are not proved necessary. |
| EIP236 | Producer/Blob store/Bus/Consumer has return and retrieval links. A three-line assignment clears both side findings but introduces 4.2. | Missing joint fan/convergence track alternative. No source hint contradiction established. |
| A165 | Seven-line assignment clears both convergence findings but adds two 4.9 findings and three 5.9 findings; other options do not complete routes. prog centre (4050,1380), reqs (1245,2400), goals (5565,2400): legal widened frame changes dominant-axis facing expectations, although bottom/top port choices remain as before. | Need hint-preserving group/frame-side/slot alternatives. Wider frame is legal, but the side findings are not proved unavoidable. Do not undo frame fitting or any original hint. |

These are exact conflicts in the observed rejected alternatives. They are not
certificates that no legal improving arrangement exists. The bounded search
has not found one, and this report does not invent an exhaustive search or a
required larger candidate count. No limit change is proposed or made.

## Validation follow-up

The original implementation validation remains 326 focused tests, 90 source
regressions, the fifty-case review and the 81-case replay. The explicit replay
is rerun for this report with the new checkpoint ceiling assertions; the
thirteen-case side/straightness review is also rerun. Both pass: **81/81** with
the new checkpoint assertions and **13/13** source review cases. These are
validation runs, not new benchmark timings. No engine change, DOCX file or
unrelated work is included.

## Every changed original-81 scenario

| Scenario | Bends at 3d73b88 | Retained bends |
| --- | ---: | ---: |
| A103 | 8 | 0 |
| A11 | 2 | 0 |
| A125 | 4 | 0 |
| A132 | 7 | 0 |
| A134 | 4 | 0 |
| A136 | 5 | 0 |
| A150 | 2 | 0 |
| A151 | 2 | 0 |
| A16 | 2 | 0 |
| A161 | 26 | 0 |
| A17 | 2 | 0 |
| A170 | 2 | 0 |
| A171 | 4 | 2 |
| A172 | 2 | 0 |
| A21 | 4 | 0 |
| A22 | 7 | 0 |
| A28 | 5 | 0 |
| A30 | 2 | 0 |
| A37 | 6 | 4 |
| A41 | 2 | 0 |
| A47 | 8 | 4 |
| A50 | 2 | 0 |
| A79 | 2 | 0 |
| A82 | 2 | 0 |
| A98 | 4 | 0 |
| EIP141 | 4 | 0 |
| EIP146 | 2 | 1 |
| EIP162 | 8 | 0 |
| EIP211 | 2 | 0 |
| EIP215 | 12 | 4 |
| EIP225 | 8 | 0 |
| EIP231 | 2 | 0 |
| EIP240 | 8 | 0 |
| EIP241 | 2 | 0 |
| EIP245 | 8 | 0 |
| EIP246 | 12 | 0 |
| EIP280 | 8 | 0 |
| EIP285 | 2 | 1 |
| EIP295 | 2 | 1 |
| EIP315 | 8 | 0 |
| EIP36 | 5 | 0 |
| EIP46 | 8 | 0 |
| EIP63 | 8 | 0 |
| EIP73 | 2 | 0 |
| EIP77 | 2 | 1 |
| EIP83 | 2 | 1 |
| EIP99 | 2 | 0 |

Fresh side-review total bends (cells off -> retained cells on): F136 8 -> 14,
A63 10 -> 6, EIP236 10 -> 6, A165 56 -> 56. Thus side findings in the latter
three cases do not imply a total-bend regression. Their detailed rejected
alternatives above still leave the side search unresolved. Fresh source traces
confirm the cited 4.2/4.9/shared-track blockers; A165's complete seven-line
alternative also adds three crossings. None is accepted by dropping a check.

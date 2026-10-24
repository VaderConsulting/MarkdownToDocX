# Stress watchdog follow-up — 20 October 2026

A142 is recorded as a **stress timeout at 182.1 s process wall time**, including
startup, under the unchanged 180-second case deadline. No completed result or
hard-gate pass can be inferred from that timeout. The normal watchdog stays
59 seconds. All 27 named stress cases remain excluded from quality totals.

## Other 26 cases

All 26 completed within their 180-second case deadlines. There were 17 NUnit
passes and nine failures, with no additional timeouts. These are individual
process wall times including test-host startup; they are not warm engine-only
timings. Every row emitted its audit summary.

| Case | Process wall s | Test result | Nonzero gate fields |
| --- | ---: | --- | --- |
| F26 | 2.743 | PASS | none reported |
| F58 | 3.407 | PASS | none reported |
| F59 | 2.917 | FAIL | offSlotPorts=1 |
| F60 | 3.06 | PASS | none reported |
| F151 | 12.304 | PASS | none reported |
| F152 | 10.899 | FAIL | offSlotPorts=10 |
| F153 | 18.415 | FAIL | offSlotPorts=1; 5.1=6 |
| A104 | 9.995 | FAIL | offSlotPorts=2 |
| A105 | 6.124 | PASS | none reported |
| A106 | 4.082 | PASS | none reported |
| A107 | 4.87 | PASS | port=3 |
| A108 | 3.495 | PASS | none reported |
| A109 | 2.964 | PASS | none reported |
| A110 | 16.149 | FAIL | 5.1=215 |
| A111 | 9.412 | FAIL | offSlotPorts=1 |
| A112 | 4.073 | PASS | none reported |
| A154 | 2.741 | PASS | none reported |
| A155 | 2.893 | PASS | none reported |
| A156 | 2.827 | PASS | none reported |
| A157 | 2.787 | PASS | none reported |
| A158 | 2.959 | PASS | none reported |
| A159 | 3.331 | FAIL | offSlotPorts=1 |
| A160 | 4.398 | FAIL | 5.1=9 |
| A161 | 3.031 | PASS | none reported |
| A162 | 3.032 | PASS | none reported |
| A163 | 24.532 | FAIL | port=8; offSlotPorts=7 |

Missing routes, no-legal-route failures, title bands, unrelated frame overlaps,
off-grid nodes/frame edges and region-grid fields are zero in all 26 summaries.
This is **not an all-gates-zero result**: the listed port, slot and shared-track
fields remain. A107's NUnit test passes while its measured port field is three;
the measured result is retained rather than hidden by the test outcome.
No checker or assertion has been changed.

Raw evidence: `out/markup-review/stress-watchdog/other-cases/results.jsonl`,
`gate-results.csv`, and each case's stdout and TRX. The original A142 timeout is
in `A142Compact.watchdog.json` and `a142-run.log`. The separately labelled
unsupported-thread-timeout TRX is a framework rejection, not this gate result.

## One A142 profile outside the gate

The one diagnostic finished: **581.697 s** from layout start through compact
metrics (NUnit case duration 581.949 s). Production layout itself took
581.556 s. The post-layout compact-metric calculation took only **0.139 s**.
This is an instrumented, outside-gate measurement, not a new gate result; A142's
recorded gate outcome remains the 182.1 s timeout. The diagnostic's successful
fixture outcome means it completed, not that A142's hard gates passed.

There are **11 complete core-layout executions** in the unchanged fully routed
wrap-selection path. The first 4.12 pass begins at elapsed 19.963 s, lasts
**37.309 s**, and ends at 57.272 s. Across all 11 executions, 4.12 takes
**423.082 s (72.7% of the diagnostic elapsed time)**. The candidate layouts repeat
allocation, routing, recovery, final repair and 4.12; this explains why a single
180-second gate deadline is exceeded without implying that one routing call
hangs. No wrap candidates were removed or changed.

| Pass | Calls | Total seconds | First call seconds |
| --- | ---: | ---: | ---: |
| Grid and measured geometry | 11 | 15.131 | 2.730 |
| Placement hints | 11 | 0.088 | 0.011 |
| Automatic node placement | 11 | 0.797 | 0.098 |
| Group frame fitting | 11 | 16.053 | 1.594 |
| Route coordinate preparation | 11 | 0.546 | 0.082 |
| Joint port allocation | 11 | 0.154 | 0.047 |
| Routing hints | 11 | 0.015 | 0.009 |
| Initial rule-specific route construction | 11 | 5.312 | 1.498 |
| Coupled route repair | 11 | 23.714 | 3.905 |
| Model route completion and checks | 11 | 38.198 | 4.402 |
| Final legality validation | 11 | 0.178 | 0.025 |
| Final complete-layout repair | 11 | 57.264 | 5.018 |
| Bend-removing port reassignment, 4.12 | 11 | 423.082 | 37.309 |
| Finished route interior/facing pass | 1 | 0.009 | 0.009 |
| Post-layout compact metrics | 1 | 0.139 | 0.139 |

The top-level stage totals are inclusive of their internal work. Nested
completion timers must not be added to them. The existing completion hooks report
33 completion invocations across these layouts and reference/confirmation work:
final route searches 32.617 s; final diagnostics 24.933 s; hint restoration and
unrouted-side search 3.913 s; title clearance/final move 3.864 s; frame overlap and
route seal 3.065 s; guide materialisation 2.356 s. The first initial model
completion is 4.402 s; coupled repair/recovery is 3.905 s on that layout.

Read-only timing callbacks write 48,941 numbered events, so these times include
instrumentation overhead and are not a clean performance benchmark. No expensive
per-stage geometry serialization was enabled. The diagnostic ends at 314 bends
and 127 score crossings; stress results stay outside quality totals.

Evidence: `out/markup-review/stress-watchdog/A142-pass-profile.jsonl`,
`A142-all-stage-times.csv`, `A142-profile-outside-gate.log` and its TRX. Only this
one outside-gate profiling invocation was run.

### Speed proposal for Dave — not implemented

Start inside 4.12, not by reducing the wrap search:

1. Reuse the baseline score, finding inventory and route-pair scoring context
   across successive node trials until an accepted route change invalidates
   them. Today `FlowchartBendRemovingPortRepair.Apply` constructs these again for
   each visited node with candidates. Keep identical candidate order, tie breaks
   and rejection conditions. Refresh after every acceptance.
2. Prepare immutable node/title obstacles once for fixed bounds. `BuildRoute`
   currently rebuilds routing obstacles for each changed terminal trial. Reuse
   only geometry that cannot change in that pass, including frame title bands.
3. Memoize identical terminal-route searches by complete bounds/geometry,
   endpoint, side, normal, direction and hint inputs, with independent returned
   route copies. Score each against the current other routes; do not cache a
   quality decision across changed surrounding routes.
4. If the same full candidate is rebuilt for final ownership confirmation,
   consider replaying its already confirmed route geometry only after proving
   equality of all inputs and restoring ownership, hints and caches exactly.
   Do not transplant a mutable grid or skip the confirmation checks.

First instrument these internal costs separately if Dave approves further work.
The current profile identifies the dominant pass but does not separate its
route-building time from per-node score/check reconstruction. No numerical saving
is promised without that measurement. Any implementation must preserve exact
geometry and all existing checks, candidate counts and limits. **None of these
optimisations has been built.**

## A115 and shared columns

The new production regression for the two drift-introduced shared pairs passes.
All 28 focused row/band tests also pass. See
`a115-drift-shared-track-follow-up.md` for the exact 15 px terminal overlaps and
why they no longer occur in the current partner-placement layout. A115 still has
two other shared-track findings; they are not waived.

The shared-column archive is unchanged and was not rerun. Proposal-only design
requirements covering A104's fan crossings, the slot rise and A52's painted
crossing failure are in `enterprise-shared-columns-next-design.md`.

No engine geometry changed during this follow-up. Current views remain:

| View | Bends | Crossings | Aspect |
| --- | ---: | ---: | ---: |
| A113 | 164 | 142 | 0.616822 |
| A115 | 82 | 66 | 5.647059 |
| A116 | 101 | 104 | 5.387097 |
| A117 | 107 | 67 | 2.684426 |

The existing `out/markup-review/band-views/<Id>-after-v3.png` renders remain
current. No DOCX, commits or push. HEAD remains ce6f1ce. The band engine remains
uncommitted, and the existing nonzero gates prevent an acceptance claim.

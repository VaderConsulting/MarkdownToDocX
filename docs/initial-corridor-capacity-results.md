Superseded by [the corridor allocation follow-up](corridor-allocation-follow-up.md), which records the retained routing changes and current metrics.

# Initial corridor capacity results

2026-10-20. HEAD remains ce6f1ce. The band engine and this work remain uncommitted. No DOCX generation or push.

The implementation reads `corridor_capacity_buffer` through `FlowchartLayoutRules.CorridorCapacityBuffer`. The authoritative value is 0.25. For forecast n > 0, reserved tracks = max(n + 1, ceil(n * (1 + buffer))); required width = reserved tracks * the existing 30 px track spacing. Gap growth rounds up through the existing whole-grid operation. Unused gaps remain unchanged. No rules.yaml edit was made after the latest instruction; its SHA256 is A9EDCF038C2925C3CBE522F9BC43F82D080A790D044345B6BC35224E69129DC1.

The forecast excludes every column gap covered by any multi-column node, even one in another row, and the transposed row case. It redirects reservations to the nearest globally open logical gap, preferring an interior gap on a distance tie. No grown gap is covered by a node in the 67 captures. Exterior reservations are diagnosed separately. This is a capacity forecast: it does not yet bind fallback route arms to those reservations. That missing routing handoff remains important; the requested distinct-track result is not complete.

The existing fully routed wrap choice and complete selected cell regions are retained. Initial gap sizes are published atomically with those regions before final routing. No new wrap candidate, node move, repair-budget exception or limit is introduced. All 67 captures retain exactly the same starting rows, columns and spans. This preserves validated placement rather than allowing gap growth to change a wrap or trigger a different node placement.

## Results and scope

42/42 focused band, row, clearance and capacity tests pass. The A115 regression and A116/A117 production singleton tests pass (3/3). Both native PNG render tests pass and verify exact geometry hashes against the production captures.

All 67 potentially affected multi-layer Enterprise cases except the previously timed-out A142 completed: 29 geometry changes with measured new gap growth, 38 byte-identical geometry hashes. Every case has zero strict slot and off-slot faults and zero final rules 10, 3.1 and 4.1 findings. Existing shared-track and port faults remain; this is not a claim that all gates are zero. The A107 ceiling test passes at 2 port findings. The unchanged A110 ceiling test fails: 126 shared pairs versus the required 58 (before this change: 215). A110 changes from 200 bends / 854 physical crossings to 184 / 791.

The 67-case measurement process took 97.78 s; this is not a full 673-summary audit time. A142 was not rerun and retains its recorded 182.1 s stress timeout. The full normal suite and full audit have not been certified for this change. Flow, EIP and single-layer Enterprise bypass the new initial band capacity path; this run does not substitute for their full catalogue validation.

Crossings increase in A115 (67 to 75) and A46 (0 to 1), despite unchanged cell ownership. Gap geometry changes which existing routing choices win. No checker or expectation was altered to conceal these changes. A115's specific overlap regression still passes. These routing regressions and A110's ceiling are unresolved, so the work remains uncommitted.

## Growth for every changed case

Dimensions are the complete scored layout bounds, including route extents. This explains A38 becoming 24 px narrower despite some gaps growing. Every changed case has zero cell-region differences.

| Case | Width before → after (delta px) | Height before → after (delta px) | Bends | Physical crossings |
|---|---:|---:|---:|---:|
| A100 | 900 → 990 (90) | 540 → 600 (60) | 18 → 15 | 0 → 0 |
| A104 | 5730 → 5730 (0) | 840 → 930 (90) | 20 → 20 | 0 → 0 |
| A110 | 1104 → 2274 (1170) | 3120 → 5340 (2220) | 200 → 184 | 854 → 791 |
| A111 | 3750 → 3810 (60) | 1080 → 1230 (150) | 19 → 19 | 2 → 1 |
| A113 | 1980 → 2640 (660) | 3210 → 4500 (1290) | 187 → 156 | 147 → 110 |
| A115 | 8640 → 8880 (240) | 1530 → 1740 (210) | 85 → 83 | 67 → 75 |
| A116 | 5010 → 5580 (570) | 930 → 1470 (540) | 103 → 98 | 104 → 73 |
| A117 | 3930 → 4590 (660) | 1464 → 1674 (210) | 129 → 114 | 67 → 61 |
| A133 | 610 → 670 (60) | 450 → 510 (60) | 15 → 12 | 4 → 3 |
| A135 | 384 → 474 (90) | 720 → 810 (90) | 17 → 14 | 5 → 1 |
| A140 | 370 → 460 (90) | 780 → 870 (90) | 14 → 12 | 1 → 1 |
| A165 | 18720 → 18960 (240) | 1830 → 1860 (30) | 43 → 39 | 9 → 9 |
| A167 | 5664 → 5730 (66) | 3630 → 3810 (180) | 56 → 50 | 77 → 67 |
| A26 | 534 → 564 (30) | 610 → 670 (60) | 14 → 13 | 5 → 2 |
| A38 | 204 → 180 (-24) | 540 → 570 (30) | 11 → 11 | 2 → 2 |
| A46 | 510 → 540 (30) | 1140 → 1230 (90) | 12 → 12 | 0 → 1 |
| A47 | 550 → 580 (30) | 720 → 780 (60) | 5 → 5 | 0 → 0 |
| A52 | 414 → 414 (0) | 690 → 720 (30) | 11 → 10 | 0 → 0 |
| A57 | 720 → 720 (0) | 390 → 420 (30) | 7 → 6 | 0 → 0 |
| A66 | 370 → 460 (90) | 780 → 870 (90) | 14 → 12 | 1 → 1 |
| A67 | 1450 → 1540 (90) | 660 → 750 (90) | 14 → 14 | 4 → 3 |
| A68 | 1410 → 1410 (0) | 720 → 780 (60) | 5 → 5 | 2 → 2 |
| A69 | 360 → 420 (60) | 1110 → 1110 (0) | 13 → 13 | 1 → 1 |
| A70 | 354 → 354 (0) | 960 → 990 (30) | 15 → 15 | 1 → 1 |
| A71 | 360 → 390 (30) | 570 → 600 (30) | 7 → 7 | 0 → 0 |
| A83 | 204 → 204 (0) | 600 → 630 (30) | 9 → 9 | 0 → 0 |
| A88 | 1050 → 1080 (30) | 420 → 420 (0) | 4 → 4 | 0 → 0 |
| A94 | 390 → 450 (60) | 610 → 670 (60) | 12 → 12 | 2 → 2 |
| A98 | 360 → 360 (0) | 690 → 720 (30) | 8 → 7 | 0 → 0 |

## A110 gap sizes

Indices identify the whole-cell boundary after the named row or column. Existing minimum gaps are 30 px between columns and 60 px between rows. Forecast counts use simultaneously overlapping declared-link reservations.

| Axis | After cell index | Forecast lines | Reserved tracks | Gap px before → after |
|---|---:|---:|---:|---:|
| Column | 0 | 31 | 39 | 30 → 1170 |
| Column | 1 | 2 | 3 | 30 → 90 |
| Row | 0 | 6 | 8 | 60 → 240 |
| Row | 6 | 32 | 40 | 60 → 1200 |
| Row | 1 | 6 | 8 | 60 → 240 |
| Row | 2 | 6 | 8 | 60 → 240 |
| Row | 3 | 6 | 8 | 60 → 240 |
| Row | 4 | 6 | 8 | 60 → 240 |
| Row | 8 | 3 | 4 | 60 → 120 |
| Row | 9 | 3 | 4 | 60 → 120 |
| Row | 10 | 3 | 4 | 60 → 120 |

## Four band views

| Case | Bends before → after | Crossings before → after | Aspect before → after |
|---|---:|---:|---:|
| A113 | 187 → 156 | 147 → 110 | 0.616822 → 0.586667 |
| A115 | 85 → 83 | 67 → 75 | 5.647059 → 5.103448 |
| A116 | 103 → 98 | 104 → 73 | 5.387097 → 3.795918 |
| A117 | 129 → 114 | 67 → 61 | 2.684426 → 2.741935 |

## Decisions and remaining work

- Kept the selected complete cell ownership and wrap fixed, so this remains a gap-sizing change.
- Reserved no artificial traffic for unused gaps. Every used gap gets at least one spare track.
- Never used a node's internal gap as a corridor or grew it for traffic. Redirected the forecast instead.
- Did not alter the 12-line or 16-candidate limit, any checker, hint, band order or watchdog.
- Fixed a measurement harness progress-file sharing race with FileShare.ReadWrite; watchdog durations are unchanged.
- Left routing allocation incomplete rather than claim that free capacity alone separates fallback fan arms. The next engine work must consume the reservation order inside route construction, not add a cleanup pass. It must also explain and remove the A115/A46 crossing increases.

The earlier percentage parameter remains in the supplied YAML alongside the authoritative fractional buffer; it is not read by this implementation. It was left untouched under the latest explicit instruction not to edit rules.yaml.

Evidence: `out/markup-review/initial-corridor-capacity/canonical-buffer/case-comparison.csv` and `changed-cases.csv`; per-case JSONs hold exact geometry, regions, gaps and diagnostics. Native converter renders: `canonical-buffer/A107-after.png` and `canonical-buffer/A110-after.png`. A110-preview.png is a reduced inspection copy; the full PNG is retained.

## Completed stress comparison

All 26 non-timeout stress captures completed under the unchanged watchdogs in 48.76 s process wall time. A104, A110 and A111 changed geometry; the other 23 match the retained pre-capacity geometry hashes exactly. All 26 have zero final strict slot, off-slot, and rules 10/3.1/4.1 findings. Known faults remain: A110 shared126, F153 shared6, A160 shared9, A107 port2, A163 port8. Accumulated intermediate diagnostics are not all zero and are retained in JSON, rather than hidden. A142 retains its recorded timeout and is not counted as a pass. Full stress comparison: out/markup-review/initial-corridor-capacity/canonical-stress/stress-comparison.csv.

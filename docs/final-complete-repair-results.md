# Confirm final repairs against the whole routed layout

19 October 2026. Reference checkpoint: `b96e67e` (2998 bends, 2065 geometric crossings, 330 shared-track pairs, 1195 rule 5.9 findings, 1596 measured findings; 205 known normal failure identities).

Approved rule commit: `942b4a4`.

## Scope and rule

No rule conflict was found. The approved final-only sentence was added verbatim to rule 5.10, and the rulebook and JSON twin were regenerated from `docs/Instructions`. No parameter value or other rule text changed. All earlier `NoNewFindings` admission calls remain unchanged.

The final stage is the last operation in `DiagramGridLayout.ApplyCore`, after model completion, star nesting and legality validation. `FlowchartSugiyamaLayoutEngine.TryLayout` returns that result directly. Internal group reflow and candidate evaluation cannot run the new pass. Each candidate uses the existing route-assignment or atomic region/gap factories, with at most 16 completed proposals, the existing chain-depth limit, 12 changed lines and 61 px of added Manhattan route length.

Every formed candidate goes through whole-diagram reference routing. Proposed track/slot/side choices are replayed on the freshly routed model as part of the candidate, then the complete result is validated and scored. Every route, frame, declared hint, port and label is checked. Admission recounts all bends, geometric crossings, shared pairs, same-fan crossing events and identities, and all findings. It requires lower repair score with the existing length/churn charges, no increase in any of those counts, no newly appearing same-fan crossing, and exact original hint status and placement relationships. There is no later routing or placement pass; publication refreshes guides and diagnostics only.

The previous cell/route/frame/guide/hint snapshot provides exact rollback. The production A115 test verifies rejected candidates restore cell regions, grown gaps, ports, routes, frames, guides and diagnostic state exactly. A separate geometric test makes the touched pair better while adding crossings elsewhere and requires rejection.

## Production result at the canonical cap

All 673 summaries were produced, including the same five native-ineligible sources recorded as unmeasured. Every reported hard gate is zero: legality, missing/no-legal routes, node/grid/frame/slot checks, title bands, hint accounting and cleanup. No scenario gains bends, geometric crossings, shared pairs or measured total findings. A104 remains 38/0 and A105 remains 28/0; both retain zero same-fan crossings.

The final pass accepted **77 candidates across 58 scenarios**, with a largest accepted added length of **60 px**. The following table includes every accepted scenario, even where the requested metric counts are unchanged and the score improves through other layout terms.

| Scenario | Accepted | Bends | Crossings | Shared pairs | 5.9 | Total findings |
|---|---:|---:|---:|---:|---:|---:|
| A100 | 3 | 12 → 4 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A112 | 3 | 37 → 37 | 5 → 5 | 1 → 1 | 3 → 3 | 6 → 6 |
| A115 | 1 | 100 → 96 | 139 → 131 | 16 → 13 | 98 → 97 | 117 → 113 |
| A116 | 1 | 78 → 78 | 18 → 17 | 1 → 1 | 15 → 14 | 16 → 15 |
| A133 | 1 | 1 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A135 | 1 | 4 → 4 | 2 → 0 | 0 → 0 | 1 → 0 | 1 → 0 |
| A137 | 1 | 8 → 4 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A160 | 1 | 46 → 46 | 34 → 27 | 13 → 9 | 8 → 9 | 21 → 18 |
| A163 | 1 | 194 → 189 | 24 → 23 | 0 → 0 | 16 → 15 | 24 → 23 |
| A171 | 1 | 2 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A173 | 2 | 6 → 0 | 2 → 0 | 1 → 0 | 0 → 0 | 1 → 0 |
| A179 | 1 | 3 → 1 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A18 | 1 | 20 → 18 | 1 → 1 | 0 → 0 | 1 → 1 | 1 → 1 |
| A20 | 1 | 3 → 3 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A23 | 1 | 2 → 2 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A26 | 1 | 9 → 7 | 2 → 2 | 0 → 0 | 1 → 1 | 1 → 1 |
| A29 | 2 | 10 → 6 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A31 | 1 | 8 → 6 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A37 | 1 | 4 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A38 | 1 | 5 → 3 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A40 | 1 | 4 → 2 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A42 | 1 | 4 → 0 | 2 → 0 | 1 → 0 | 0 → 0 | 1 → 0 |
| A46 | 2 | 16 → 11 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A47 | 2 | 4 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A48 | 2 | 12 → 8 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A54 | 1 | 9 → 9 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A67 | 1 | 11 → 8 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A70 | 1 | 8 → 6 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| A72 | 3 | 9 → 4 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP126 | 1 | 2 → 1 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP146 | 1 | 1 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP152 | 1 | 3 → 2 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP194 | 1 | 3 → 1 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP200 | 1 | 4 → 2 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP276 | 1 | 6 → 6 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP285 | 1 | 1 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP295 | 1 | 1 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP31 | 1 | 4 → 4 | 2 → 1 | 1 → 0 | 0 → 1 | 1 → 1 |
| EIP311 | 1 | 6 → 6 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP327 | 1 | 4 → 2 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP77 | 1 | 1 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP83 | 1 | 1 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| EIP84 | 1 | 11 → 7 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F113 | 1 | 98 → 98 | 107 → 106 | 9 → 9 | 79 → 78 | 88 → 87 |
| F124 | 1 | 4 → 2 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F127 | 3 | 2 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F135 | 2 | 3 → 1 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F139 | 1 | 3 → 3 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F153 | 1 | 105 → 103 | 87 → 86 | 7 → 7 | 68 → 67 | 75 → 74 |
| F34 | 1 | 4 → 2 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F37 | 3 | 2 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F40 | 2 | 2 → 2 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F45 | 1 | 1 → 1 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F53 | 1 | 4 → 1 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F58 | 2 | 11 → 8 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F60 | 1 | 10 → 6 | 3 → 3 | 0 → 0 | 3 → 3 | 3 → 3 |
| F62 | 2 | 4 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| F84 | 1 | 3 → 0 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| Catalogue | 77 | 2998 → 2875 | 2065 → 2039 | 330 → 320 | 1195 → 1191 | 1596 → 1582 |

Measured total findings exclude the five native-ineligible sources, consistently with the baseline and the audit summary. The optional capture also records their four missing-route observations separately; those are not silently included in the measured total of 1582.

## The eleven specified pairs

| Scenario | Exact pair | Final production result |
|---|---|---|
| A110 | containerHosting→orderManager:49 × messageQueue→webShop:54 | Remains |
| A110 | containerHosting→paymentHub:50 × messageQueue→orderManager:55 | Remains |
| A110 | orderManager→stockLookupSvc:38 × orderManager→shippingQuoteSvc:44 | Remains |
| A110 | priceLookupSvc→shipOrder:15 × shippingQuoteSvc→cancelOrder:26 | Remains |
| A110 | stockLookupSvc→cancelOrder:8 × priceLookupSvc→shipOrder:15 | Remains |
| A113 | customers→convert:0 × placeOrder→orderSvc:11 | Remains |
| A115 | drSvc→shop:23 × perthData→backup:30 | Remains |
| A115 | perthLan→perthApp:2 × shopImg1→shop:24 | Removed; the completed perthLan region move and whole-layout routing also improve total A115 metrics from 100/139/16/98/117 to 96/131/13/97/113 |
| A117 | g2→t2:6 × dDecom→t2:37 | Remains |
| A142 | cadMf→cadT1:24 × hrApiMf→hrApiCon:169 | Remains |
| A142 | empowerExt→dmeEtms:36 × cadMf→dmeEtms:165 | Remains |

These are production outcomes under the bounded final pass, not proofs that the ten remaining pairs have no legal alternative. No scenario filter or catalogue identity is used by production repair code. A110 stays 180/461/58/301/359; A113 117/74/5/61/66; A117 138/87/16/42/58; A142 322/158/6/124/130 (bends/crossings/shared/5.9/total findings).

## A117 joint diagnostic

The archived g3-north/wpSelect-west region assignment was offered explicitly through a generic test-only final-pass hook. Production has no A117-specific code. Whole-diagram confirmation gives 135 bends, 77 crossings, 11 shared pairs, 42 rule 5.9 findings and 53 total findings, versus 138/87/16/42/58. It adds 65 px, so the canonical 61 px cap rejects it.

At measurement caps of 65 and 80, the same candidate is still rejected by the same-fan guard: the `baseline→t1:0 × baseline→legacyPayroll:38` interaction appears, raising same-fan crossing events from 25 to 26. Its appearance is forbidden independently of the lower global crossing and shared-pair totals. The candidate is restored exactly and A117's published model remains unchanged. Raising the length cap alone does not make this joint option acceptable.

## Verification and timing

The eight policy tests pass (the seven retained guard cases plus remote-crossing geometry). The A115 completed-layout/rollback test and F60 envelope regression pass, as do the four committed crossing-repair regressions. Normal suite: **1148 passing / the same 205 failing identities / 1353 total**, with no new or removed failure identity.

The canonical unprofiled production audit took **359.37 s**, with **A142 227.600705 s**. Timing is not used to reject or hold back this work. The complete preliminary run took 345.45 s / A142 231.430988 s, but exposed the F60 frame issue described below and was not a passing retention result.

A ten-second A142 CPU sample put 99.8% of sampled active time under complete reference confirmation. Inclusive sampled costs included facing-side repair (26.2%), port-side repair (25.5%), the route checker (22.6%) and distinct-track repair (19.1%); these overlap and must not be added together. Compact stage profiling supplies the final non-overlapping cost breakdown below.

## Issues found while implementing the boundary

The first preliminary run showed that internal group reflow also returns through the grid pipeline. That internal invocation is now excluded, so the exception cannot affect placement. The old audit watchdog also called the .NET-only Kill(bool) overload from Windows PowerShell; that harness was left unchanged. Slow complete measurements use PowerShell 7 and the existing supported `BatchTimeoutSeconds` argument, with its maximum value. No engine search limit changed.

The first complete run exposed F60 frame overlap after a candidate rebuilt public envelopes. Node-overlap checks alone did not cover it. The engine now checks the completed public frame envelopes through the existing frame-overlap logic before admission. The reproducing `move|I1|3,0` and `move|P1|-3,0` options are rejected; the unchanged audit checker reports zero frame overlaps. A dedicated regression exercises both the rejection and the legal published result.

## Accepted assignments

| Scenario | Accepted assignment | Added length (px) |
|---|---|---:|
| F34 | move\|A\|1,0 | -80 |
| F37 | move\|S1\|-1,0 | -30 |
| F37 | move\|Start\|0,-1 | -180 |
| F37 | move\|S1\|0,1 | -330 |
| F40 | move\|b0\|0,-1 | 0 |
| F40 | move\|b0\|b1\|0,-1 | -360 |
| F45 | move\|A\|-2,0 | -390 |
| F53 | move\|Error\|2,0 | 60 |
| F58 | joint\|chain-move\|Ingest:7,4;Start:3,4;Validate:11,4 | -885 |
| F58 | move\|Parse\|0,2 | -15 |
| F60 | move\|P1\|1,0 | -80 |
| F62 | move\|a\|-1,0 | 60 |
| F62 | joint\|fan-sides\|b:1,1;c:0,2 | -390 |
| F84 | move\|Shared\|2,0 | -240 |
| F113 | L-B1-End-1 x L-B2-End-3\|slots | 0 |
| F124 | move\|A\|1,0 | -80 |
| F127 | move\|S1\|-1,0 | -30 |
| F127 | move\|Start\|0,-1 | -180 |
| F127 | move\|S1\|0,1 | -330 |
| F135 | move\|End\|-1,0 | -300 |
| F135 | move\|Ship\|1,0 | 0 |
| F139 | move\|Orders\|0,-1 | 0 |
| F153 | joint\|fan-arm\|n100:68,33;n101:69,33;n102:70,33;n103:71,33;n104:72,33;n105:73,33;n96:64,33;n97:65,33;n98:66,33;n99:67,33 | -5220 |
| A18 | move\|releaseReady\|0,2 | -270 |
| A20 | move\|parcel\|0,-1 | -330 |
| A23 | move\|agreement\|employment\|-1,0 | -150 |
| A26 | joint\|chain-move\|salesCap:1,1;salesFn:0,1 | -270 |
| A29 | joint\|chain-move\|autoFile:1,4;automate:0,4 | -270 |
| A29 | move\|lateFilings\|1,0 | 0 |
| A31 | move\|gap2\|0,-2 | -840 |
| A37 | joint\|fan-sides\|delivery:1,2;terms:-1,2 | -609 |
| A38 | move\|priceSvc\|0,-1 | 60 |
| A40 | move\|archive\|1,0 | -150 |
| A42 | joint\|fan-sides\|buyNotBuild:0,0;cloudFirst:1,2 | -840 |
| A46 | move\|legacyShop\|0,-1 | -240 |
| A46 | move\|order\|-1,0 | -300 |
| A47 | move\|inHouse\|0,1 | -300 |
| A47 | joint\|fan-sides\|gateway:0,2;inHouse:1,1 | -150 |
| A48 | move\|refund\|0,1 | -570 |
| A48 | move\|repair\|0,-1 | 0 |
| A54 | move\|shop\|1,0 | -420 |
| A67 | move\|hosting\|0,4 | -720 |
| A70 | move\|server\|1,0 | -450 |
| A72 | move\|packer\|0,1 | -570 |
| A72 | move\|pick\|0,-1 | 0 |
| A72 | move\|erp\|-1,0 | 0 |
| A100 | joint\|fan-sides\|directionSvc:4,2;versionStore:5,1 | -420 |
| A100 | move\|inbox\|0,-1 | -270 |
| A100 | move\|order-api\|0,-1 | 60 |
| A112 | move\|junction\|split1\|checkBudget\|6 | -1320 |
| A112 | move\|junction\|route\|managerOk\|1 | -660.000000000002 |
| A112 | move\|junction\|route\|autoOk\|4 | -1980 |
| A115 | move\|perthLan\|0,8 | -660 |
| A116 | ceo->cost:0 x manualWork->cost:8\|slots | 0 |
| A133 | joint\|fan-sides\|hosting:0,2;image:1,1 | -630 |
| A135 | move\|orderToCash\|1,0 | -300 |
| A137 | move\|ship\|0,1 | -570 |
| A160 | hub->t3:2 x hub->t11:10\|track | 0 |
| A163 | m12->m13:11 x m15->m16:14\|side | -88 |
| A171 | move\|portal\|-1,0 | -300 |
| A173 | move\|terminal-hole\|warehouse\|0\|2 | -150 |
| A173 | joint\|fan-sides\|billing:1,1;orders:0,0 | -300 |
| A179 | move\|basket\|-2,0 | -300 |
| EIP31 | move\|IN\|0,-1 | -180 |
| EIP77 | move\|A\|1,0 | -135 |
| EIP83 | move\|A\|1,0 | -135 |
| EIP84 | move\|C\|Q\|0,1 | -18 |
| EIP126 | move\|ReplyChannel\|-1,0 | -420 |
| EIP146 | move\|A\|1,0 | -135 |
| EIP152 | move\|I\|-1,0 | -150 |
| EIP194 | move\|D\|-1,0 | 0 |
| EIP200 | move\|C\|-1,0 | 0 |
| EIP276 | move\|DL\|-1,0 | -450 |
| EIP285 | move\|A\|1,0 | -135 |
| EIP295 | move\|A\|1,0 | -135 |
| EIP311 | move\|R\|-1,0 | -300 |
| EIP327 | move\|S\|-1,0 | -150 |

## Compact profile costs

The profiling run retained identical geometry hashes for all 668 measurable sources and identical metrics for all 673 summaries. It took **383.68 s**; A142 took **
244.139512
 s**. The whole final pass accounted for **308.625 s**, including the reference-routing category below; do not add that parent duration to its children.

| Exclusive category | Catalogue seconds | A142 seconds |
|---|---:|---:|
| routing/final-confirmation | 304.763 | 228.720 |
| routing/candidate-trials | 22.758 | 3.231 |
| routing/initial-completion | 7.317 | 2.725 |
| routing/recovery | 6.931 | 0.000 |
| final-layout-scoring | 4.507 | 1.476 |
| frame-search | 4.004 | 2.136 |
| final-complete-repair | 3.365 | 1.315 |
| routing/confirmation | 1.247 | 0.000 |
| hint-checks | 0.851 | 0.024 |
| final-diagnostics | 0.509 | 0.391 |

Every completed candidate reruns the whole reference route-completion pipeline. Repeated port-side repairs, facing-side repairs, distinct-track searches and whole-layout checks dominate the new confirmation time. No check was removed to improve the timings.

Length-cap experiments are recorded separately in `docs/final-repair-length-cap-measurements.md`; the retained configuration stays at 61 px.

# Retain four bounded joint crossing repairs

19 October 2026. Reference: `c2f94a7`. This checkpoint precedes the subsequently approved 5.10 finding-identity exception.

The search interleaves internal-track translations, shared-side slot exchanges and joint side reallocations. It scores the finished affected-line closure incrementally and restores the existing cell/route/frame/guide snapshot exactly on rejection. It uses the existing 16-candidate and 12-line limits, the 61 px added-length cap, unchanged repair charges, existing label-room and shape-legality checks, and the original strict identity guard. No checker, hint, span setting or rule value changes in this checkpoint.

## New baseline

All 673 production summaries are present, with the same five native-ineligible/unmeasured cases. All reported hard gates remain zero. No scenario gains bends, crossings or findings. Normal suite: **1140 pass / 205 fail / 1345 total**, with no new failure identities. Four production regressions pass. Eleven individual pair trials pass their bend/crossing and exact rollback checks, including regions, gaps, ports, routes, frames, guides and hint state.

| Scenario | Before bends / crossings / shared / 5.9 / total findings | New baseline |
|---|---:|---:|
| A14 | 19 / 2 / 0 / 2 / 2 | 19 / 1 / 0 / 1 / 1 |
| A152 | 20 / 1 / 0 / 1 / 1 | 20 / 0 / 0 / 0 / 0 |
| A167 | 62 / 41 / 13 / 9 / measured in audit | 62 / 40 / 13 / 8 / before minus one |
| EIP271 | 8 / 2 / 0 / 1 / measured in audit | 8 / 1 / 0 / 0 / before minus one |
| Catalogue | 2998 / 2069 / 330 / 1199 / 1600 | **2998 / 2065 / 330 / 1195 / 1596** |

These four are the only changed geometry signatures. Each accepted assignment removes one crossing; no route length changes. A104 stays 38/0 and A105 stays 28/0, with zero same-fan crossings. Other rule totals stay 4.2=52, 3.1=2, 4.1=0, 3.3/5.2=5, 4.9=4, 4.11=5, 5.8=3.

## Eleven individually retried pairs, still open under the original guard

Each diagnostic replay receives the existing 16-candidate limit, interleaves track/slot/side proposals, and leaves its selected pair unresolved. All rejected states restore exactly. These results are bounded-search observations, not proofs that no legal assignment exists.

| Scenario and pair | Observed blockers |
|---|---|
| A110 containerHosting→orderManager × messageQueue→webShop | Alternatives add crossings (461→464/466), add 290 px, or introduce a new objectStorage→webShop crossing and shared track; other options remain crossing or fail geometry. |
| A110 containerHosting→paymentHub × messageQueue→orderManager | Eleven options still cross; five fail track/body/terminal geometry. |
| A110 orderManager→stockLookupSvc × orderManager→shippingQuoteSvc | Crossing-removing alternatives add bends (180→181/182) or crossings (461→463/466); other options fail geometry or still cross. |
| A110 priceLookupSvc→shipOrder × shippingQuoteSvc→cancelOrder | Twelve options still cross; four fail geometry. |
| A110 stockLookupSvc→cancelOrder × priceLookupSvc→shipOrder | Ten options still cross; six fail geometry. |
| A113 customers→convert × placeOrder→orderSvc | Alternatives add bends (117→118/119) or crossings (74→75); other options fail geometry or still cross. |
| A115 drSvc→shop × perthData→backup | Alternatives add a bend or crossings (139→147/155); other options fail geometry or still cross. |
| A115 perthLan→perthApp × shopImg1→shop | Alternatives add crossings (139→147/159), add 290/370 px, or introduce a new shared track with containerSvc→shop. |
| A117 g2→t2 × dDecom→t2 | Alternatives add crossings (87→88/89/90/94); remaining options fail geometry or still cross. |
| A142 cadMf→cadT1 × hrApiMf→hrApiCon | Alternatives add crossings (158→159/161/163) or 290/410 px; remaining options fail geometry or still cross. |
| A142 empowerExt→dmeEtms × cadMf→dmeEtms | Alternatives add crossings (158→162) or 375 px; remaining options fail geometry or still cross. |

No limit was raised. No one of these measured closures was rejected by the 12-line limit; each exhausted its 16 complete proposals. The archived g3/wpSelect move was not restored by this checkpoint.

## Timing

Unprofiled audits: **51.68 s / 58.67 s**. A142: **8.071565 s / 10.841566 s**. Timing does not block this quality-passing checkpoint under the user's approval.

A separate profiled audit takes **108.50 s** and emits about 45 MB of profile output; it is not an unprofiled speed comparison. Measured layout time sums to **60.58 s**; the remaining 47.92 s is outside measured layout, including audit/harness/profiling/output work not further attributed. Exclusive attributed costs: trial routing **22.71 s**, recovery routing **7.55 s**, initial routing **6.82 s**, final-layout scoring **3.68 s**, frame search **3.32 s**, confirmation **1.20 s**, hint checks **0.98 s**. The new pair-search call costs **0.24 s inclusive** across the catalogue; it overlaps the routing totals and must not be added again.

Evidence: `out/markup-review/joint-crossing-pair-*`, including two audits, normal-suite TRX, production regressions, eleven isolated trials, geometry comparisons and profile output. No DOCX files are staged or regenerated. No push is performed.

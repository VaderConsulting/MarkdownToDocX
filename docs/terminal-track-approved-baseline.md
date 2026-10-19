# Retain exact terminal tracks under the approved exception

19 October 2026. Reference before this checkpoint: `06a7a48`. This checkpoint restores only `docs/exact-port-runs-rejected-2026-10-19/terminal-track-trial/`; it includes neither the joint-move nor gap-limit experiments.

The user approved the measured base-trial differences because exact terminal runs implement the existing rule 3.2 text in `e52b049`. The exception permits A117's 138 bends / 87 crossings and precisely the eleven added 5.9 identities listed below. It permits no other finding increase or bend/crossing regression. Rule values, guards, ordinary 12-line chain bound, 16-candidate limit and independent text spans are unchanged.

## New production baseline

All 673 summaries are present. The same five native-ineligible scenarios remain unmeasured. All reported legality, grid, slot, hint-accounting, missing-route, title-band, frame-overlap and cleanup gates remain zero. A104 is 38 bends / 0 crossings and A105 is 28 / 0, both with zero same-fan crossings.

| Metric | Before | Approved baseline |
|---|---:|---:|
| Bends | 2999 | 2998 |
| Geometric crossings | 2095 | 2069 |
| Shared-track pairs | 337 | 330 |
| Rule 5.9 | 1190 | 1199 |
| Rule 4.2 | 52 | 52 |
| Rule 3.1 | 2 | 2 |
| Rule 4.1 | 0 | 0 |
| Rule 4.11 | 5 | 5 |
| Rule 5.8 | 3 | 3 |

The only per-scenario finding increases are A110 5.9 296→301, A113 60→61, A115 96→98, A117 41→42 and A142 123→124. A18 falls 2→1; A142 removes one old finding while adding two new identities. Other rule counts do not rise.

| Scenario | Before bends / crossings / shared / 5.9 | Approved baseline |
|---|---:|---:|
| A18 | 20 / 3 / 0 / 2 | 20 / 1 / 0 / 1 |
| A110 | 180 / 463 / 60 / 296 | 180 / 461 / 58 / 301 |
| A113 | 117 / 75 / 6 / 60 | 117 / 74 / 5 / 61 |
| A114 | 143 / 353 / 108 / 115 | 142 / 352 / 108 / 115 |
| A115 | 100 / 143 / 19 / 96 | 100 / 139 / 16 / 98 |
| A117 | 138 / 86 / 16 / 41 | 138 / 87 / 16 / 42 |
| A142 | 322 / 159 / 7 / 123 | 322 / 158 / 6 / 124 |
| A163 | 194 / 40 / 0 / 16 | 194 / 24 / 0 / 16 |

## Exact approved finding identities

- A110: `containerHosting->orderManager:49 x messageQueue->webShop:54`.
- A110: `containerHosting->paymentHub:50 x messageQueue->orderManager:55`.
- A110: `orderManager->stockLookupSvc:38 x orderManager->shippingQuoteSvc:44`.
- A110: `priceLookupSvc->shipOrder:15 x shippingQuoteSvc->cancelOrder:26`.
- A110: `stockLookupSvc->cancelOrder:8 x priceLookupSvc->shipOrder:15`.
- A113: `customers->convert:0 x placeOrder->orderSvc:11`.
- A115: `drSvc->shop:23 x perthData->backup:30`.
- A115: `perthLan->perthApp:2 x shopImg1->shop:24`.
- A117: `g2->t2:6 x dDecom->t2:37`.
- A142: `cadMf->cadT1:24 x hrApiMf->hrApiCon:169`.
- A142: `empowerExt->dmeEtms:36 x cadMf->dmeEtms:165`.

The two `lan->webNode` findings from the joint experiment are absent; they are not part of this exception.

## Verification and timing

The two unprofiled production audits take **54.27 s / 53.06 s**, with A142 **7.779177 s / 7.927771 s**. Both emit all 673 summaries and reproduce the approved counters. Geometry signatures are captured in `out/markup-review/approved-terminal-run{1,2}.stdout.log`. The normal Release layout suite is **1133 pass / 205 fail / 1338 total**, exactly the same failure identities as the preceding base-trial verification, with no new failures against the old 211.

Because both audits exceed about 51 s, a separate profiled run was measured. It takes **77.26 s**; its timings include profiling overhead and are not the unprofiled audit times. Exclusive attributed costs are candidate-trial routing **21.89 s**, recovery routing **7.21 s**, initial-completion routing **6.58 s**, final-layout scoring **4.26 s**, frame search **3.71 s**, confirmation routing **1.17 s**, and hint checks **0.81 s**. These are exclusive categories. Inclusive stage timings overlap: final-layout scoring 41.97 s, model completion 41.22 s and final diagnostics 35.22 s must not be added together. Routing inside candidate trials is the largest exclusive cost. No search, guard or diagnostic was removed for speed in this checkpoint.

Evidence: `out/markup-review/approved-terminal-*` includes both audits and timings, the normal-suite TRX, the full catalogue comparison and the profiled-stage JSON. No DOCX was regenerated or staged, and no push was made.

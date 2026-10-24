# A107 rule 2.13 score breakdown

2026-10-20. Measurement only; no weights, rules, limits or retained engine changes.

Both layouts were measured with the existing scoring implementation after the final finished-route-interior-and-facing repair. The candidate moves Review Account to row 0, column -1, immediately left of Draft Order. It preserves all other selected regions and gaps and rebuilds its own ports and routes.

| Score term | Current published layout | Review left of Draft | Candidate minus current |
|---|---:|---:|---:|
| Aspect/shape | 33.125000 | 29.375000 | -3.750000 |
| Empty area | 133.636364 | 294.509804 | +160.873440 |
| Bends (20 each) | 200.000000 (10) | 160.000000 (8) | -40.000000 |
| Crossings, after forced minimum | 0 | 0 | 0 |
| Shared-endpoint crossings | 0 | 0 | 0 |
| Disconnected ends | 0 | 0 | 0 |
| Honoured placement/line hint credit | 0 | 0 | 0 |
| Primary subtotal | 366.761364 | 483.884804 | +117.123440 |
| Bounded route length | 15.890411 | 15.890411 | 0 |
| Displayed total | **382.651775** | **499.775215** | **+117.123440** |

Current full scored bounds are 330 x 1440; candidate bounds are 510 x 1440. Aspect is 0.229166667 versus 0.354166667. The empty-area share rises from 39.204545% to 59.313725%, an increase of 20.109180 percentage points. Both exceed the 30% threshold, so that increase is charged at 40 points per 5 percentage points (8 points per percentage point), giving 160.873440 points. Empty area is the scorer's raster occupancy measurement of nodes, frames and routes, not simply white pixels in a PNG. Both complete layouts have 2320 px of route length, or 77.333333 routing-grid units; the approved bounded term is 77.333333 / (1 + 77.333333/20) = 15.890411.

Existing shape weight is 2 points per 5% departure from the target 4:3 ratio. The candidate's wider bounds improve that term by 3.75. It saves two bends (40 points); there are no crossings, shared-endpoint crossings, disconnected ends or hints in either measured layout. Neither completed measurement has scoring violations.

ComparisonCost only includes the bounded length term when comparing layouts with tied bend counts and crossing costs. Here the bend counts differ, so the actual comparison uses the primary subtotals; the candidate still loses by exactly 117.123440 because its length term happens to be equal anyway. No extra generic findings-count penalty is added to this 2.13 score; validity is checked separately.

## Correction to the earlier trial report

The archived 472.174797 versus 499.775215 comparison was captured before the last finished-route-interior-and-facing repair. At that earlier boundary the incumbent had 11 bends, 360 x 1440 bounds, 47.916667% empty area and 2680 px of route length. The final existing repair changes it to the published 10-bend layout above. This is a stage mismatch in the earlier report, not two different corner counters. Applying the same final repair to the candidate leaves its 8-bend score unchanged. The temporary archived prototype remains unretained; no production code was changed by this measurement.

Evidence: out/markup-review/a107-score-breakdown/score-terms.json (original comparison boundary), finished-score-terms.json (candidate after final repair), finished-score-terms.json.published.json (current published layout), A107.json (production geometry). Both measurement tests passed. Temporary source hooks were removed and the three touched pre-existing source files restored byte-for-byte from snapshots captured immediately before this experiment.
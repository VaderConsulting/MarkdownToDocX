# Reserve frame title space before fitting

Production cells and independent spans remain OFF. The sixteen-candidate limit, rules.yaml, score weights and hints are unchanged. F32/EIP261 remains parked.

## Cause and correction

The initial frame builder already included the widest explicit title line and the renderer's multiline title band before frame fitting. The unsafe enlargement was a routing fallback: when a line could not avoid an already reserved band, it moved the band upward by resizing its frame and ancestors in drawing coordinates. This could engulf a neighbouring frame after the scored fit had accepted the layout. Reserving more arbitrary title space would not address that fault.

`FlowchartFrameTitleReservation` now owns the shared width/band calculation for member-backed frames, nested/empty frames and routing title obstacles. It uses the existing renderer text advances (`FlowchartRichLabelSvgWriter.EstimateLineWidthPx`), existing font/line-height settings and `RequiredTitleBandPx` clearance, rounded outward using the existing grid. This is the renderer's established width estimate, not a newly introduced font-shaping library or wrapping policy. Explicit lines are measured; frame text is not newly rewrapped. No arbitrary sizing constants were added.

The pre-existing cell bypass stays in place. The late growth writer itself now also refuses a cell-backed model before touching either the bounds index, output clusters or ancestors. Only the legacy unbound path retains the old resize behaviour. Cell routing repairs paths against the reserved band and complete allocated sides; none of the catalogue cases requires a late physical frame expansion. Any future spatial enlargement must go through a complete scored region/gap assignment and frame rebuild, not this drawing-cache writer.

## Which scenarios reached late enlargement

An isolated checkout replay removed only the two cell bypasses and instrumented the old writer. The main workspace retained its protections. The complete replay ran all 673 census cases.

| Scenario | Frames reached by the old writer | Final unrelated frame overlaps in the replay |
| --- | --- | ---: |
| F45 | L2 | 0 |
| A58 | finance | 0 |
| A138 | finance | 0 |
| A167 | landgateOp, mgmtRoles, regRoles, trusted, vendorOp | 3 |
| A169 | regRoles | 1 |
| EIP84 | loop-0 | 0 |

The 308 writer calls include private route candidates and repeated repair, not 308 final drawn enlargements. Per-frame counts are in [the replay census](title-band-legacy-growth-census.csv).

A168 was also affected in the earlier pre-bypass capture: A167/A168/A169 had 3/6/1 final frame overlaps respectively. With the subsequently corrected original hints, A168 no longer reaches the old writer in the current replay. It remains an explicit regression. The absence of overlaps in the other four replay cases does not make resizing their derived frames legitimate.

## Regression coverage

Three new Layout tests cover wide four-line titles before scored fitting, including a nested frame, and a long-title control whose legacy writer crosses a previously legal gap into its neighbour. The corresponding cell-bound writer calls return false and preserve exact region/gap ownership and frame cache objects.

Eight new complete-pipeline regressions cover a long title plus neighbouring frame and every replay caller plus the historical A168 source. The long-title fixture retains exactly the dimensions fitted before routing; all final frames must match forward projection from actual regions. Independent path, title-band, frame-overlap, missing-route and exact-slot gates must pass. A stale enlarged drawing frame therefore cannot pass by hiding behind a legal region snapshot.

All eleven new tests pass: three Layout cases and eight complete-pipeline cases. The full focused selection passes 309 tests, and all 40 cell/source regressions pass, including the four newly identified callers.
## Catalogue and audit results

The protected census passes all 673 cases in 95.778 seconds, with zero writer catches: 662 completed bound scenarios / 3,806 nodes, six unchanged open-mode scenarios kept unbound, and five known native skips/empty cases. Completed-case bend totals remain 3,365 with zero scenario changes against the pre-checkpoint census. Candidate limits, rules, hints and independent spans are unchanged.

Both audits emit all 673 summaries. The cells-on summary rows are byte-identical to the preceding original-hint checkpoint. Cells-off is checked against the original ordered baseline. No audit checker or baseline is modified.

| Gate/counter | Cells off | Cells on |
| --- | ---: | ---: |
| Illegal paths | 0 | 0 |
| Missing routes | 0 | 0 |
| No-legal-route failures | 0 | 0 |
| Title-band intersections | 0 | 0 |
| Unrelated frame overlaps | 0 | 0 |
| Grid findings / off-grid nodes / off-grid frame edges | 0 / 0 / 0 | 0 / 0 / 0 |
| Exact off-slot node/group ports | 0 / 0 | 0 / 0 |
| Hint accounting failures | 0 | 0 |
| Cleanup corrections | 0 | 0 |
| 4.1 | 0 | 0 |
| 3.2/4.7 | 0 | 0 |
| 4.2 | 44 | 52 |
| 5.9 | 1,694 | 1,234 |
| 3.1 | 4 | 2 |
| 3.3/5.2 | 5 | 5 |
| 4.9 | 6 | 4 |
| 4.11 | 3 | 5 |
| 5.8 | 0 | 3 |
| Shared-track pairs | 2,089 | 346 |
| Port findings | 56 | 67 |
| Aligned / nonaligned port jogs | 15 / 1,323 | 17 / 1,254 |
| Unmeasured native skips/empty cases | 5 | 5 |
| Failed/skip audit scenarios | 25 | 29 |

Cells-on audit: 97.62 seconds; A142 36.9755 seconds. Cells-off audit: 40.88 seconds; A142 12.7527 seconds. Quality failures and the experimental audit slowdown remain open. Audit exit status is nonzero for those existing findings; this checkpoint is not described as a wholly green quality audit or production activation.
Cells-off verification is complete: all 673 summary rows are byte-identical in original order to the baseline. Exact route/node/frame geometry and ordered diagnostics also match in all 673 cases (changed=0, missing=0, 39.079 seconds). Cells-on summaries match the preceding checkpoint byte-for-byte too. No layout/snapshot change is intended or observed in these comparisons. The census verifies unchanged bend totals in all 662 completed bound cases; it is not represented as an exact cells-on route-geometry capture.

DOCX files and unrelated working-tree changes are excluded from this checkpoint. Nothing is pushed.

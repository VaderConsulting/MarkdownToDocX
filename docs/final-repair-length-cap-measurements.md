# Measure the final repair length cap

19 October 2026. Measurement reference: engine commit `01dfee1`, approved rule commit `942b4a4`. The retained canonical cap is **61 px**. The 65/80 px switches apply only to the final repair pass in the test host; they do not change `rules.yaml`, an earlier repair or the converter's production configuration.

The reference production result is 2875 bends, 2039 crossings, 320 shared-track pairs, 1191 rule 5.9 findings and 1582 measured total findings. It accepted 77 final candidates across 58 scenarios, with a largest accepted added length of 60 px. All 673 audit summaries exist; the same five native-ineligible sources remain explicitly unmeasured. Reference unprofiled timing: 359.37 s, A142 227.600705 s.

Commands use the existing production audit, its unchanged checkers, and the supported watchdog argument so a slow fully confirmed candidate is measured rather than timed out:

```powershell
$env:ROUTE_AUDIT_CELLS_ON = 'production'
$env:ROUTE_AUDIT_GEOMETRY = '1'
$env:FINAL_REPAIR_CAP = '65' # then repeat with '80'
$env:FINAL_REPAIR_CAPTURE = Join-Path (Get-Location) 'out/markup-review/final-complete-cap65.jsonl'
[IO.File]::WriteAllText($env:FINAL_REPAIR_CAPTURE, '')
pwsh -NoProfile -File tests/MermaidRecognition.RuleValidation.Tests/Run-RouteRuleCatalogueAudit.ps1 -BatchTimeoutSeconds ([int]::MaxValue)
```

The helper `out/markup-review/Run-FinalRepair-CapAudit.ps1` runs those commands, resets profiling switches, starts an empty capture and copies the stdout and timings to separately named files. `FinalRepairMeasurementSetup` scopes the override to the test process and restores it afterwards. No engine source changes are made for the measurements.

## Whole-catalogue measurements

| Final-pass cap | Bends | Crossings | Shared pairs | Rule 5.9 | Measured findings | Accepted candidates | Largest added length accepted | Audit seconds | A142 seconds |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 61 px, retained | 2875 | 2039 | 320 | 1191 | 1582 | 77 | 60 px | 359.37 | 227.600705 |
| 65 px, measurement | 2875 | 2039 | 320 | 1191 | 1582 | 77 | 60 px | 369.18 | 246.084827 |
| 80 px, measurement | 2875 | 2039 | 320 | 1191 | 1582 | 77 | 60 px | 324.55 | 197.306590 |

Both experiments emitted all 673 summaries. All reported hard gates remain zero. Both have **zero geometry-hash differences among all 668 measurable layouts**, zero changes in the five requested metrics for any scenario, and the same 77 accepted candidates across 58 scenarios. The five native-ineligible statuses are unchanged. The timings varied despite identical accepted candidates and output; no speed change was implemented.

## Scenarios changed

**None at either cap.** A117 is explicitly unchanged:

| Cap | A117 bends | Crossings | Shared pairs | Rule 5.9 | Total findings |
|---|---:|---:|---:|---:|---:|
| 61 px | 138 | 87 | 16 | 42 | 58 |
| 65 px | 138 | 87 | 16 | 42 | 58 |
| 80 px | 138 | 87 | 16 | 42 | 58 |

A104 remains 38/0 and A105 remains 28/0 bends/crossings, both with zero same-fan crossings.

## Candidates advanced by the higher cap

These proposals reached a later rejection check at 80 px. `finding-or-fault` is the final policy's aggregate rejection label: it includes the complete repair-score comparison, total findings and protected fault categories. It does not by itself establish that a new fault was introduced. EIP195's 64 px proposal also advanced at 65 px and was rejected for bends. No proposal with added length above 61 px was accepted.
| Scenario | Assignment | Added length (px) | Decision at 80 px |
|---|---|---:|---|
| F141 | move\|Client\|-3,0 | 80 | finding-or-fault |
| A14 | move\|webShop\|0,2 | 70 | bends |
| A94 | move\|order\|-1,0 | 80 | finding-or-fault |
| A94 | move\|order\|ordering\|-1,0 | 80 | finding-or-fault |
| A94 | move\|shop\|1,0 | 80 | finding-or-fault |
| A107 | move\|approve\|draft\|submit\|0,-1 | 70 | bends |
| A112 | route->reject:11 x autoOk->approved:14\|side | 69.814 | bends |
| A152 | move\|checkout\|0,-2 | 75 | bends |
| EIP152 | move\|I\|0,-1 | 75 | bends |
| EIP152 | move\|T1\|0,1 | 75 | bends |
| EIP195 | move\|CMP\|1,0 | 64 | bends |

## A117 explicit joint diagnostic

A separate measurement offered precisely g3 north by one cell plus wpSelect west by one cell, through the generic diagnostic assignment hook. Whole-layout confirmation produced 135 bends, 77 crossings, 11 shared pairs, 42 rule 5.9 findings and 53 total findings, adding 65 px. At 61 px it fails the length cap. At 65 and 80 px it fails the same-fan check: the `baseline→t1:0 × baseline→legacyPayroll:38` interaction appears and same-fan crossing events rise 25→26. The candidate is rejected and restored in every case. The normal catalogue runs above are unfiltered production runs, not forced retention of that diagnostic assignment.

## Retained state and evidence

The cap stays **61 px**. No rule value, production option, earlier guard, weight, hint or checker was changed by these measurements. There is no measured layout benefit from 65 or 80 px. Work stops here for the user's requested approval point.

The canonical normal suite has 1148 passes and the exact same 205 failure identities as `b96e67e`; the cap experiments are catalogue measurements, not separately retained normal-suite configurations.

Local evidence:
- `out/markup-review/final-complete-baseline.{log,stdout.log,timings.csv,jsonl}`
- `out/markup-review/final-complete-profile.{log,stdout.log,timings.csv,jsonl}`
- `out/markup-review/final-complete-cap65.{log,stdout.log,timings.csv,jsonl}`
- `out/markup-review/final-complete-cap80.{log,stdout.log,timings.csv,jsonl}`
- `out/markup-review/final-complete-normal-final.trx`
- `out/markup-review/final-complete-a117-final.log`

The complete retained per-scenario comparison and accepted-assignment list are in `docs/final-complete-repair-results.md`. No DOCX was generated and nothing was pushed.

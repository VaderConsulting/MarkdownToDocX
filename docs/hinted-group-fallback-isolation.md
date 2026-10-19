# Isolated hinted-group fallback validation

19 October 2026. The engine change was applied alone to the detached `0977e24` worktree at `C:/Users/windo/AppData/Local/Temp/md2docx-band-png-before-0977e24`. No Enterprise band-placement implementation was applied there. The main working tree's band work remains separate and uncommitted.

## Full audit, bands off

The production-mode Release audit completed all **673 summaries** under the unmodified **59-second progress watchdog**, with five already-known native-ineligible cases. Wall time was **115.03 s** and A142 took **33.8836 s**. No invocation override was used.

All summary rows and catalogue metrics match the `0977e24` reference:

| Metric | Before | After |
|---|---:|---:|
| Bends | 2875 | 2875 |
| Geometric crossings | 2039 | 2039 |
| Shared-track pairs | 320 | 320 |
| Rule 5.9 findings | 1191 | 1191 |
| Total findings | 1582 | 1582 |
| Accepted final-repair candidates | 77 in 58 scenarios | 77 in 58 scenarios |

The measured hard gates remain zero: missing/no-legal routes, illegal paths, title-band intrusions, frame overlaps, off-grid nodes/frames, off-slot ports/groups, grid findings, hint-accounting failures and cleanup corrections. The audit still exits with the same **23 existing scenario assertion failures**, including known shared-track failures and the five native-ineligible cases. This is not a claim that those existing quality failures have been resolved.

Evidence is in `out/markup-review/hinted-frame-only.log`, `.stdout.log`, `.jsonl` and `.timings.csv`. All 668 eligible scenario snapshot hashes match except A167. The five native-ineligible placeholders have unchanged measurements and summaries.

## Every drawn-geometry change

| Scenario | Bends before → after | Crossings before → after | Changed geometry |
|---|---:|---:|---|
| A167 | 62 → 62 | 40 → 40 | Only hinted `vendorOp → devCustodian:12`; its source-side horizontal track moves from Y 820 to Y 817.5 in the existing Y-up model coordinates |

Both intermediate vertices `(6420,820)` and `(7620,820)` become `(6420,817.5)` and `(7620,817.5)`. The allocated endpoints, terminal sides and normals remain identical. The resolved drawn guide follows the same change. Node bounds, frame bounds, every other connector, hint states and findings are identical. One fallback diagnostic disappears and one whole-pixel diagnostic appears; neither changes a hard gate or audit summary.

The post-rebuild A167 snapshot has SHA-256 `A85E086A13837A315F4801697655438BB890FBEA56FB4C74480BC68A5ED11F17`, exactly matching the full audit capture. This also verifies that the audit used the fallback assembly, despite a stale incremental build encountered later during the original-engine regression comparison. That comparison was followed by an explicit rebuild and repeat checks.

## Focused regressions

`HintedGroupConnectorFallbackTests.CapturedTitleClearanceKeepsExactHintedPortsAndNormalRuns` uses captured bounds and strict hint replay directly, without running band placement. It fails with an empty route against the original engine and passes with the fallback. It checks exact allocated terminals, normal-preserving obstacle/title legality, the active honoured hint and the unchanged sibling route.

`A167_HintedVendorAssignmentRemainsDrawnAndLegal` also passes on the bands-off build. Together the two focused tests pass; the complete A167 layout is checked independently of the captured fixture.

The rebuilt normal suite reports **1148 passes and the same 205 failing identities out of 1353 tests**, with zero added or removed failure identities (2 minutes 15 seconds). Evidence: `out/markup-review/hinted-frame-confirmed-normal.trx` and `.log`, compared case-sensitively with the reference failure list. Both focused fallback regressions pass separately. No rule text, parameter, weight, limit, checker, watchdog, DOCX or band-placement source is included in this isolated change.

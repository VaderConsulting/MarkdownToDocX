# A110 and A107 source-group replay

20 October 2026. **No source fix is retained.** The cause predates all five named
later groups: it is the original semantic band-placement integration. The only
measured variant meeting both committed count ceilings bypasses that placement,
which removes its required behaviour. That reversal was not applied to the main
worktree. The two introduced faults remain open, with new production regressions.

## Verified cumulative replay

Started from detached ce6f1ce in
`C:/Users/windo/AppData/Local/Temp/md2docx-stress-bisect-ce6-verified-20261020`.
ce6f1ce has no band implementation, so the original band foundation must be
measured before the later row, partner and cache changes can be attributed.
These are reconstructed source groups, not fabricated historical commits.

| Cumulative source state | A110 shared-track pairs | A107 port findings |
| --- | ---: | ---: |
| ce6f1ce, explicit production cells | 58 | 2 |
| Original band foundation, before row levelling | 215 | 3 |
| Add band-row levelling | 215 | 3 |
| Add singleton and partner completion | 215 | 3 |
| Add mixed-band proximity and frame compaction | 215 | 3 |
| Add 4.12 allocated-slot exchange | 215 | 3 |
| Add publication cache invalidation | 215 | 3 |

The full geometry hashes, not just these totals, are identical for both scenarios
from the pre-row foundation through every later group. A110 has the same 195
introduced shared identities throughout, with 20 retained old identities;
38 of the original 58 disappear. A107 adds the same rule 4.2 identity,
`approve->draft:6`. Its other two port findings already exist at ce6f1ce.

Every replay capture of these two scenarios has **zero slot and off-slot findings**. Both engines use
explicit production cell ownership, the same sources and checkers, Release
builds and the canonical 180-second stress supervisor. No checker threshold,
watchdog, rule, parameter or engine limit was changed. Stress classification and
quality exclusions are unchanged.

The authoritative data is
`out/markup-review/stress-source-bisect/verified/stage-results.jsonl` and its CSV.
An initial replay was repeated because archived copies retain old source
timestamps. The verified replay uses a second fresh worktree, touches all replay
inputs before each build and recompiles every source group. The first replay is
preserved as provisional evidence; conclusions above use the verified builds.

## Source groups and reproducibility

The scripts in `out/markup-review/stress-source-bisect/` preserve the exact
reconstruction. `Run-SourceGroups-Verified.ps1` applies them cumulatively:

- Foundation: current uncommitted band dependencies, with the captured pre-row
  `EnterpriseBandPlacement.cs` and `EnterpriseBandPartners.cs`, the pre-partner
  plan, original alignment candidate and no new publication invalidations.
- Rows: captured row-levelling placement and partner files. The row-membership
  plan support is inactive until the row convention is attached.
- Singleton completion: final completion code restricted to singleton bands;
  frame compaction and mixed-band moves are not invoked yet.
- Mixed proximity: enable the final mixed-band completion and frame compaction.
- Slot exchange: replace the old arbitrary alignment projection with the tested
  exchange of existing allocated slots, keeping its candidate-budget position.
- Cache: restore the two added SVG-guide and label-index invalidations in final
  publication. Earlier committed cache/rollback fixes are already in ce6f1ce.

The manifest and all raw captures/build logs are retained. Independent no-polish
controls also exclude the new route-alternative paths and their private-copy
state as the origin of these two finding increases.

## Cause within the foundation

`DiagramGridLayoutStages.cs:132` invokes semantic band placement immediately
after cell binding. Its band branch at line 182 takes ownership of placement and
returns before the original general placement pipeline. `EnterpriseBandPlan.Build`
replaces the original coordinates with band-local ranks and partner/wrap packing;
`DiagramGridLayoutBandWraps.cs` scores the complete resulting candidates.

For A110, the selected narrow wrap puts the six Business targets in a column.
Several independently routed fan arms then choose the same outside corridor.
This exists before row levelling. For example:

- `customerLookupSvc->takeOrder:0` goes through `(-54,1050)` to `(-54,3000)`.
- `customerLookupSvc->amendOrder:1` goes through `(-54,1050)` to `(-54,2850)`.

They share the vertical run X=-54 over Y=1050..2850 and part of the horizontal
approach at Y=1050. This is drawn geometry, not a stronger checker counting it
differently. The band candidate eligibility function currently checks section
10, 4.1 and 3.2/4.7, but not shared tracks; therefore its `Legal` flag does not
establish the zero-shared-track requirement. Simply adding that guard would not
supply a legal candidate: all measured existing wraps already share tracks.

For A107, band-local placement puts draft, submit and approve in one column.
Approve and draft have equal right-edge X=150; submit occupies the direct
vertical corridor between them. `approve->draft:6` consequently detours. At
ce6f1ce those endpoints do not have equal X, so that particular 4.2 identity is
absent. Changing the checker or moving a port off its allocated slot is not a fix.

The later groups are not responsible: these two views have no singleton or
mixed-frame bands for those completion changes to move, and the row/slot/cache
replays preserve their full route geometry.

## Source-only causal and fix probes

All probes below were confined to the detached worktree, built afresh, and
restored afterwards. No additional final cleanup pass was added.

| Source variant | A110 shared pairs | A110 port findings | A107 port findings | Verdict |
| --- | ---: | ---: | ---: | --- |
| Bands, without final bend polish | 215 | 0 | 3 | Polish is not the cause |
| Bypass initial band placement | 58 | 0 | 2 | Meets counts but undoes band behaviour; rejected |
| Keep bands; allow original placement work under their constraints | 158 | 1 | 3 | Still fails; adds an A110 port finding |
| Measure/grow capacity before initial band packing | 215 | 0 | 3 | No change |
| Reserve wrapped-row approach capacity before first routing | 148 | 0 | 3 | Improvement, but still fails 58/2 ceilings |
| Combine original placement work and up-front approach capacity | 201 | 0 | 3 | Still fails |

The approach-capacity probe reserves gaps inside a band, where the existing
reservation considers band boundaries. It uses the existing cell-gap growth and
derived grid/track spacing, with no limit change. The improvement shows an
up-front capacity omission contributes, but it does not solve the remaining
joint allocation. It is not retained as a partial fix.

## Trade-off and stop

The only tested source reversal reaching both requested ceilings removes initial
semantic band placement. Retaining it would undo the required band membership
and order, the semantic timeline and the validated partner-placement behaviour.
Per step 3, that reversal is not retained and no later repair is added instead.

This is **not proof that the rules are incompatible**. A purpose-preserving fix
still needs a band candidate whose initial placement, measured approach capacity,
port allocation and track order work together. The tested local changes do not
provide one. No claim is made that the two introduced faults are fixed.

All 28 replayed source/project files in the verified worktree were restored to
match the main worktree; `verified/final-restored-source-hashes.csv` records exact
hash equality. The main engine was never modified during this investigation.

## Production regressions and protected tests

Added `IntroducedStressFaultRegressionTests`:

- `ProductionA110_SharedTracksDoNotExceedCe6f1ce`: maximum 58.
- `ProductionA107_PortFindingsDoNotExceedCe6f1ce`: maximum 2.

They use explicit production cells and the full layout path, including band
wraps, routing and final passes, and also assert zero slot/off-slot findings.
Both reproduce the current failures (215 and 3); they remain red rather than
being weakened or marked expected failures.

The A115 overlap regression and both production singleton tests pass (3/3).
All focused band/row tests pass (28/28), including the three-row Implementation
and Migration timeline and hint/band-convention cases. TRX files are in
`protected-tests/` and `new-regressions-before-fix/` under the comparison output.

No main-engine geometry moved, so A113, A115, A116 and A117 were not rerendered or
remeasured. No fresh four-view bend/crossing/aspect figures are claimed, and the
older v3 images are not presented as new slot-fixed renders. No commit, DOCX,
push, rule change or limit change. HEAD remains ce6f1ce; the band engine stays
uncommitted.

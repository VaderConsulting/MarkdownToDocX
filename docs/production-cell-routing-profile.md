# Profile routing purposes and reuse fixed trial geometry

Production activation is checkpoint `597bde3`. The strengthened bend assertions
remain in `4e0f05e`. This follow-up retains production cells on and independent
text spans off; rules, weights, hints and the sixteen-candidate limit do not
change.

## Separate the routing costs

Optional profiler scopes now distinguish initial model completion, candidate
trials, confirmation and reference recovery. Nested timers still subtract their
children, so these are exclusive costs rather than overlapping inclusive totals.
Disabled profiling creates neither a timer nor a purpose scope. Earlier named
placement, initial route-construction/repair stages and audit/output costs are
outside this completion category; this table is not the whole audit wall time.

The first complete split profile takes 101.19 s including trace output, with
A142 at 13.826324 s. Exclusive costs before the reuse experiment are:

| Category | Seconds | Calls |
| --- | ---: | ---: |
| Candidate routing trials | 26.4011 | 7462 |
| Reference recovery | 7.7497 | 322 |
| Initial model completion | 7.2999 | 668 |
| Winner confirmation | 1.3139 | 10 |
| Scoring outside routing | 5.0806 | 1082 |
| Frame search | 4.4689 | 254 |
| Hint checks | 0.9406 | 60852 |
| Final diagnostics | 0.0838 | 4380 |

Profiling and full geometry verification add overhead; those wall times must
not be compared directly with the normal, unprofiled audit target.

## Reject the broad cache

A first trial cached prepared title bands across calls, validating their entire
ordered bounds inventory, contract, titles and font on every lookup. It kept all
668 completed-scenario signatures and all 673 summaries identical, including
guide paths, normals, diagnostics, hints and bends. However, the validation
turned previously cheap cache hits into repeated inventory scans and renderer
option parsing. Its profiled audit was 102.67 s and A142 rose to 26.7541 s.
That broad cache and its trial-specific tests were reverted. No such cache is
retained, and the original global title-band cache behaviour is unchanged.

## Reuse geometry within one affected-route trial

The retained narrower change prepares the fixed node-body inventory and title
bands once after port redistribution has settled, then shares them among the
affected routes' legality checks and fallback searches. It retains the exact
obstacle insertion order and existing endpoint-specific filtering. It uses the
same title-band construction helper, with an optional already-prepared band
list, rather than duplicating title geometry.

The prepared object belongs only to `CompleteAffectedCellRoutes`. Bounds,
membership and titles do not change inside that loop. Applying another region
assignment, accepting/rejecting the candidate or restoring rollback ends this
lifetime; nothing is installed in a render cache or captured in the candidate
snapshot. Unchanged routes and the existing full winner confirmation and
reference recovery remain intact.

The stronger verification run takes 58.70 s, A142 10.7004 s. All **668** completed
scenario signatures match the fresh reference exactly; the other five native
empty/ineligible cases retain their unchanged status. Every one of the **673**
ordered summary rows also matches. The signature includes node/frame geometry,
route and guide coordinates, port sides/normals, ordered checker findings,
published diagnostics and hint states. Thus no scenario adds bends or changes
rendered connector geometry.

Normal unprofiled measurements, with signature capture disabled:

| Run | Audit | A142 |
| --- | ---: | ---: |
| Production activation earlier in this session | 51.07 s | 9.803666 s |
| Scoped preparation enabled | 55.61 s | 9.040527 s |
| Fresh per-route reference, measured later in the same session | 60.82 s | 12.560525 s |

The reference measurement temporarily disabled preparation at its sole call
site, without dropping a route or check, then restored the exact verified
source. This is evidence of a benefit in that comparison, not a claimed stable
5.21-second improvement: timings varied during the session. Cells-off likewise
rose from 39.34 s to 42.86 s. The retained run remains above the about-45-second
target. A142 remains below about fifteen seconds.

## Gates and compatibility

Every legality, grid, slot, hint-accounting and cleanup gate is zero in all
completed audit runs. Quality totals remain 4.2=52, 5.9=1231, 3.1=2, 4.1=0,
4.11=5, 5.8=3 and 343 shared-track pairs. Known quality findings and five native
skips still explain the nonzero audit exit; they are not hidden as gate passes.

The full explicitly disabled audit emits all 673 original cells-off summaries
byte for byte, including its unchanged quality totals and zero hard gates.
The seven new geometry tests compare fresh/prepared routes and legality for
rectangular/diamond endpoints, legal/illegal title-band tracks, replaced frames
and rollback, changed titles/contracts and incomplete frames. Existing rejected
candidate guide/cache restoration tests remain required.

The regenerated DOCX files remain those from production activation: enterprise
151.26 s and flow 51.30 s, in the normal repository root. They are excluded from
commits. No routing geometry changed after their regeneration.

## Final retained profile and tests

Final profiled audit: **80.32 s**, A142 **13.3924 s**. It emits all 673
unchanged cells-on summaries with zero hard gates. The normal unprofiled
measurement of the same retained source is **55.61 s / A142 9.040527 s**.
Signature verification and profiling are disabled for that normal timing.

| Exclusive category | Final seconds | Calls |
| --- | ---: | ---: |
| Initial model completion | 6.7407 | 668 |
| Candidate routing trials | 24.7120 | 7462 |
| Winner confirmation | 1.2259 | 10 |
| Reference recovery | 6.9997 | 322 |
| Scoring outside routing | 4.5735 | 1082 |
| Frame search | 4.5076 | 254 |
| Hint checks | 0.9155 | 60852 |
| Final diagnostics | 0.0737 | 4380 |

Routing phase counts are unchanged from the first split profile. The detailed
machine-readable results are in production-cell-routing-profile.csv. These
measurements do not establish that the entire wall-time difference is caused
by geometry preparation: trace/verification overhead and machine activity vary.
Candidate trials remain the largest measured routing cost; the about-45-second
normal audit target remains open by 10.61 seconds in the retained measurement.

Final validation: **335 focused tests** (including all seven new prepared-route
cases) and **90 retained source regressions** pass. The A58/A138/EIP84 rejected
candidate tests still restore regions, gaps, frames, ports, paths, guide contents
and renderer cache exactly. No old test is removed or weakened. All completed
scenario signatures and all catalogue summaries remain unchanged; native skips
remain the same five. Production cells stay ON, independent text spans OFF,
with rules, weights, limits and hints unchanged. Nothing is pushed.

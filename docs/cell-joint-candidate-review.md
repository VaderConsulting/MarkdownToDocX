# Review joint fan and chain candidates after incremental scoring

## Scope and result

Incremental scoring is checkpoint **3d73b88**. It preserves every retained
original-81 bend count and reduces the unprofiled audit to 64.27 s / A142
9.50791 s. This follow-up adds missing complete cell assignments before any
change to search limits. The sixteen distinct-candidate limit, twelve coupled
lines, chain-depth limit, rules and score weights are unchanged.

The original worse-fifty are frozen independently of current results. All
sources are measured in both modes with their original hints. The old cohort
names identify which producer removal first changed a case; they are not a
claim that every case is topologically a fan or a simple chain. For example,
A161 is an explicitly hinted chain in the historical shared-fan cohort, A141
contains cycles and a self-loop, and A110 is a large many-to-many layout.

| Historical cohort | Cases | Same/fewer bends than cells off | Improved versus previous cells on | Cells-off bends | Previous cells-on bends | New cells-on bends |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Shared fan | 24 | 18 | 19 | 69 | 159 | 23 |
| Main chain | 12 | 6 | 6 | 203 | 262 | 245 |
| Fan-in median | 10 | 5 | 5 | 31 | 61 | 39 |
| Other | 4 | 1 | 1 | 221 | 303 | 299 |
| Total | 50 | **30** | **31** | **524** | **785** | **606** |

No case in the fifty gets worse than its retained cells-on result. Nineteen
remain unchanged; A37 improves but still exceeds cells off. Twenty cases still
have more bends than cells off. This is not a claim that those extra bends are
all unavoidable or that the bounded search has found the global optimum.

Every case, its full before/after counts, eligible initial proposal families,
actual examined families and selected family is recorded in
[cell-joint-candidate-bends.csv](cell-joint-candidate-bends.csv).

## Missing kinds and their implementation

* **Combined fan-in/fan-out side placement (`fan-sides`).** Previously incoming
  and outgoing arms could be moved independently, retaining multiple slots on
  the same side. The new assignment places distinct neighbours on free sides
  as one option, moving each complete feeder prefix or output tail with it.
  Each target uses starting rows/columns and whole spans. Ports are allocated
  afterward by the existing geometry contract and routing engine. EIP246
  improves 12 -> 0 bends; A103 8 -> 0 and A136 5 -> 0.
* **Root and whole output fan translation (`fan`).** The root, all branches and
  their unbranched tails can move together. EIP215 selects this family and
  improves 12 -> 4 bends, beating its cells-off total of seven. A translation
  with no relative routing benefit is not allowed to crowd out straightening
  proposals merely because it changes an origin.
* **Complete chain and root/arm assignments (`chain`, `chain-move`, `fan-arm`).**
  A nominated middle endpoint can include its previously straight prefix.
  Rigid whole-chain translations and complete cross-axis start-cell alignment
  are offered, as are a fork root with one entire arm. Parallel topology links
  are treated as neighbours for unit traversal; routes still count separately
  against the unchanged coupled-line limit. These are unpublished scored
  options, not restored centre-alignment passes.
* **Fully hinted chain layout (`hint-chain`).** A whole simple chain with
  compatible explicit terminal directions can be placed in those directions,
  anchored on an existing member's starting cell. A161's seven original
  leave-North hints remain active and honoured, while its bends fall 26 -> 0
  (cells off: 24). No hint is edited or discarded. Conflicting explicit entry
  directions or travel constraints do not get rewritten into a straight chain.

All complete targets are built first, closed over placement-hint dependents,
then checked and applied through the existing atomic region assignment. Span
counts and gap configuration are preserved. Frames and pixels are derived
from those regions; no pixel position is copied back and no neighbour grows
just to make a centre line. Rejection restores the existing complete snapshot,
including guide contents and the renderer route cache.

## Candidate ordering, budget and cost

Equivalent target regions/spans/gaps receive one identity regardless of the
family that proposed them. Duplicate proposals cannot spend the budget twice.
The final-layout scorer remains authoritative: ports, complete paths, route
interactions, legality, hints and existing repair costs are measured before
acceptance. Its weights are unchanged.

Naively interleaving pure translations caused lost existing wins. Promoting
one attractive straight connector while making another straight connector bend
also caused regressions. Those trials were rejected. The retained ordering
promotes a complete changed interface only when its projected renderer ports
can offer straight lines, including newly freed single ports on different
sides, while respecting every explicit terminal hint. Other joint proposals
remain later in the candidate list. The projection is only ordering evidence;
it does not assign regions from pixels or accept a provisional score.

This conservative ordering is deliberately not a proof that a deferred option
cannot improve a bent route. It remains a limitation for cyclic, mixed-span and
group-contained arrangements. The full route scorer, not the projection,
accepts every candidate that actually reaches evaluation.

The initial joint-generator audit was 78.19 s, including A165 10.1759 s and
F152 9.8486 s. It was preparing large or hinted-frame assignments which the
existing route search could never examine. The same hint-lock and twelve-line
eligibility checks now run before hint closure and grid validation, then again
after closure. The pruned audit was 51.04 s and all 673 summary rows matched
that unpruned run exactly. No candidate that could have reached the scorer was
removed. The additional hinted-chain fix then produces the final numbers below.

## Profile and full audits

| Exclusive instrumented category | Before seconds | Final seconds |
| --- | ---: | ---: |
| Routing completion | 94.6291 | 29.4736 |
| Final-layout scoring outside routing | 2.0412 | 3.4496 |
| Frame search | 3.8677 | 3.1797 |
| Hint validation | 0.6980 | 0.6286 |
| Final diagnostics | 0.2574 | 0.0562 |

The new proposal construction and ordering are included in scoring's exclusive
cost. Hint checks are retained; this change does not install the previously
rejected hint cache. Native placement, other stages, audit comparisons, output
and startup are outside these five categories. Inclusive nested timings must
not be summed as if they were exclusive. The category CSV is
[cell-joint-candidate-profile.csv](cell-joint-candidate-profile.csv).

Profiled full audit: **67.07 s**, A142 **10.2250 s**, compared with the retained
profile 127.27 s / 49.6515 s. Final unprofiled cells-on audit: **49.71 s**, A142
**9.56872 s**. F151 is 1.769871 s, F152 1.558331 s and A165 1.202464 s.
The full audit is still 4.71 s above the 45-second target; A142 is below fifteen
seconds. No check was dropped to obtain these times. Small coupled diagrams and
failed scoped shortlists retain the conservative full-routing reference path.

Final cells-off audit: **36.63 s**, A142 **11.2999 s**. All 673 ordered summary
rows are byte-identical to the original cells-off baseline. Profiled and
unprofiled final cells-on summaries also match ordinally.

| Gate/counter | Cells off | Cells on |
| --- | ---: | ---: |
| Summaries | 673 | 673 |
| Illegal paths | 0 | 0 |
| Missing routes / no legal route | 0 / 0 | 0 / 0 |
| Title bands / unrelated frame overlaps | 0 / 0 | 0 / 0 |
| Grid / off-grid nodes / off-grid frame edges | 0 / 0 / 0 | 0 / 0 / 0 |
| Off-slot ports / off-slot groups | 0 / 0 | 0 / 0 |
| Hint accounting / cleanup corrections | 0 / 0 | 0 / 0 |
| 4.2 | 44 | 52 |
| 5.9 | 1694 | 1231 |
| 3.1 / 4.1 | 4 / 0 | 2 / 0 |
| 4.11 / 5.8 | 3 / 0 | 5 / 3 |
| Shared-track pairs | 2089 | 343 |

Five native skips/empty cases remain unmeasured. The audit still exits nonzero
for unchanged known quality/shared-track failures; that exit is not reported
as a clean all-rule pass. All legality, grid, slot and hint-accounting gates
remain zero. Production cells and independent text spans remain disabled.
The parked F32/EIP261 routing fault remains recorded and untouched.

## Remaining cases and what is established

* **Shared-fan cohort:** F30, F120, A37, A40, EIP131 and EIP342. The diamond cases
  have different body/reservation spans: for example F30's cell-derived diamond
  is 300x360 while neighbouring rectangles are 330x240. Equal starting rows do
  not make their lateral port centres coincide. A rejected spread fixed one
  line but bent another, so it is not retained. This explains why a pixel nudge
  is not a permissible fix; it does not prove that three bends are minimal.
  A37 improves 6 -> 4 but remains above two; A40 retains its shared-target
  constraints. EIP131's root/arm winner retains two bends. These are still
  routing/placement search gaps, not approved rule conflicts.
* **Main-chain cohort:** F61, A29, A110, A112, A141 and A174. F61 is a paired
  many-to-many block, A110 a dense multi-rank block, A112 a series of junction
  splits/joins, A141 contains cycles/self-loops, and A174 includes a legend-below
  arrangement. These are not fixed by moving one simple output tail. Joint
  proposals exist, but the current complete-score/preservation search retains
  their previous bend totals. Large units can exceed the unchanged twelve-line
  bound; cyclic/group-aware proposal ordering remains open. No larger
  sixteen-candidate limit is proposed on this evidence.
* **Other remaining cohorts:** A173, A42, A46, A72 and EIP261 (fan-in); A114,
  A117 and F136 (other). Nested group/frame placement and paired-rank/channel
  geometry remain work for the latter cases. The CSV distinguishes proposed,
  actually examined and winning families so budget exhaustion is not mistaken
  for proof that a better legal layout cannot exist.

The known F148/A147/large-fan shared-track cases and F153 remain open. This
checkpoint neither starts a separate large-fan packing migration nor changes
hint semantics, score weights or any rule parameter. No DOCX files are included
and nothing is pushed.

## All shared-fan and main-chain cases
| Historical group | Scenario | Cells off | Previous on | New on | Winning family |
| --- | --- | ---: | ---: | ---: | --- |
| main-chain | A110 | 150 | 180 | 180 | none |
| main-chain | A112 | 31 | 37 | 37 | existing |
| main-chain | A136 | 4 | 5 | 0 | fan-sides |
| main-chain | A141 | 11 | 12 | 12 | existing |
| main-chain | A16 | 0 | 2 | 0 | fan-sides |
| main-chain | A174 | 0 | 2 | 2 | fan-arm |
| main-chain | A29 | 5 | 10 | 10 | existing |
| main-chain | A30 | 0 | 2 | 0 | fan-sides |
| main-chain | A98 | 2 | 4 | 0 | fan-sides |
| main-chain | EIP211 | 0 | 2 | 0 | fan-sides |
| main-chain | EIP241 | 0 | 2 | 0 | fan-sides |
| main-chain | F61 | 0 | 4 | 4 | existing |
| shared-fan | A103 | 0 | 8 | 0 | fan-sides |
| shared-fan | A161 | 24 | 26 | 0 | hint-chain |
| shared-fan | A172 | 0 | 2 | 0 | fan-sides |
| shared-fan | A21 | 2 | 4 | 0 | fan-sides |
| shared-fan | A37 | 2 | 6 | 4 | fan-sides |
| shared-fan | A40 | 2 | 4 | 4 | existing |
| shared-fan | EIP131 | 0 | 2 | 2 | fan-arm |
| shared-fan | EIP162 | 4 | 8 | 0 | fan-sides |
| shared-fan | EIP215 | 7 | 12 | 4 | fan |
| shared-fan | EIP225 | 0 | 8 | 0 | fan-sides |
| shared-fan | EIP231 | 0 | 2 | 0 | fan-sides |
| shared-fan | EIP240 | 0 | 8 | 0 | fan-sides |
| shared-fan | EIP245 | 0 | 8 | 0 | fan-sides |
| shared-fan | EIP246 | 7 | 12 | 0 | fan-sides |
| shared-fan | EIP280 | 4 | 8 | 0 | fan-sides |
| shared-fan | EIP315 | 0 | 8 | 0 | fan-sides |
| shared-fan | EIP342 | 0 | 2 | 2 | existing |
| shared-fan | EIP36 | 4 | 5 | 0 | fan-sides |
| shared-fan | EIP46 | 4 | 8 | 0 | fan-sides |
| shared-fan | EIP63 | 4 | 8 | 0 | fan-sides |
| shared-fan | EIP83 | 1 | 2 | 1 | fan-sides |
| shared-fan | EIP99 | 0 | 2 | 0 | fan-sides |
| shared-fan | F120 | 2 | 3 | 3 | existing |
| shared-fan | F30 | 2 | 3 | 3 | existing |

## Regression validation

326 focused layout tests and 90 cell/source regressions pass. The explicit
fifty-case review passes, including its cells-off bend-baseline assertions and
exact hint/cell-projection checks. The original 81-case replay also passes:
no case worsens against the retained cells-on result, 61/81 now match or beat
cells off, and total bends fall **1332 -> 1118** (cells off: 1143).

The recovery tests retain exact rejected-candidate regions, both gap maps,
ports/normals, route points, frame bounds, guide contents and renderer caches
for A58, A138 and EIP84. The strengthened regular source selection pins the
new EIP246/A103/A136/A161 gains and F30/F120/EIP342 preservation. Unit coverage
includes complete fans, unnamed straight chain prefixes, occupancy rejection,
hint-dependent closure, all four hinted chain directions and conflicting
terminal hints. No pre-existing passing test was removed or weakened.

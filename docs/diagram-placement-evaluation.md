# Bounded retained placement evaluation

`DiagramPlacementEvaluator` owns a detached accepted cell scene and its document/source/layout revision identity. Shared callers can use it without enabling an editor feature. It never publishes a candidate, changes the caller's accepted state, refits groups or invokes initial placement.

`EvaluateMove` accepts complete whole-cell targets and a local route limit. Row and column changes are independent; routes retain the existing orthogonal policy. Spans remain fixed. Explicit locks, mandatory placement/route hints, cell ownership and semantic conventions are authoritative. A no-op on an explicitly locked position is permitted. Occupied cells, broken hint gaps and route-budget failures return an unpublishable trial with a reason and actual affected/rebuilt route identities.

`Explore` takes one stable visible relationship identity. It first submits the existing scoped port allocation/path refinement with mandatory routing hints intact. It then nominates source, target and node bodies obstructing the allocated-port chord. Existing cell candidate machinery interleaves single-node peer-boundary and compass alternatives, excluding output-chain/group moves. Explicitly locked and hinted nodes are reported as skipped. Trials use the same accepted baseline rather than accumulating speculative movement. Duplicate proposals are visited once; the candidate bound includes the first routing trial. Budget exhaustion states a search limit, not that no legal arrangement exists. Coordinate overflow terminates generation with an explicit reason.

The default maximum reads canonical `repair_candidate_moves` (currently 48), and retained routing uses canonical `repair_chain_lines` (currently 24). A caller may request smaller bounds. The values remain unchanged and provisional under the rulebook's release/performance requirement. Crossing/final-repair diagnostic messages now report those actual configured limits rather than historical literals 16/12.

Contained-member trials may use vacant cells inside their existing group frames. Every ancestor remains fixed; canonical member inset and measured title strips are protected. Membership, frame position/size, cell spans and unrelated geometry remain unchanged. Leaving a frame or entering its title rejects the candidate. This is a distinct engine opt-in; source/GUI grouped movement and group refitting retain their existing boundaries until their own source publication work is complete.

Trial results own their candidate state privately. Each `CopyCandidate` returns fresh model, route and grid objects, so rendering or scoring a copy cannot corrupt another trial. Origin revisions describe the accepted scene; they do not authorize publishing the result into a newer scene. A later selected suggestion must pass the ordinary source/revision transaction again.

QUALITY-2 provides candidate generation, local routing and rejection evidence. Evaluating a legal candidate alone makes no quality or speed claim.

## Complete candidate comparison (QUALITY-3)

`DiagramPlacementScorer` captures the accepted origin, geometry, options and padding. It compares a trial against that exact baseline. Topology, node geometry requests, mandatory hints and options must remain the same. Item/region inventories, exact frame bounds, non-target node bounds and routes outside the declared closure are checked independently of the candidate's own success result. A different document/revision or failed trial cannot qualify.

Every comparison measures the complete routed scene with `FlowchartLayoutScore.Measure`, the independent route checker and stable route diagnostics. It rejects new independent failures and new quality-failure identities/counts, and requires the complete hint inventory. Crossing-pair totals are counted once; the canonical scorer retains its existing event counts, shared-endpoint handling, forced-crossing allowance and weights. Per-route partner lists are never summed as a whole-diagram crossing score.

Comparisons expose immutable before/after canonical costs/faults, route diagnostics, model width/height including captured padding, complete stored route length, moved-node count and displacement in cells/pixels. Changed relationships receive the existing moved-line repair charge; extra total length receives the existing repair-length charge. These charges are shown separately from the canonical totals. A selected-route benefit that cancels out elsewhere is not a verified improvement.

Routing-fault/hint precedence remains authoritative. With AH off, comparison reuses the existing complete repair-cost policy. With AH on, it shares the width experiment's comparator: overflow of the captured target, weighted bend/crossing cost, height, then complete cost. A narrower legal candidate may therefore qualify at a visible bend cost only under AH. The real two-node regression exercises this trade-off with AF/AG off. Defaults and canonical weights do not change.

An improvement can retain documented baseline findings; it does not imply that every pre-existing defect is fixed. Existing and remaining findings stay in the measurements. Width/height here are complete **model** bounds and padding, not a claim of final glyph/title/marker SVG extents; WIDTH-3 remains responsible for that integration. All scoring is full-scene and currently copies state. Latency/memory claims require the pending benchmarks.

## Deterministic suggestion search (QUALITY-5)

`DiagramPlacementSuggestionSearch.Find` evaluates the bounded candidates and selects only strict whole-scene improvements. Both the challenger and incumbent retain their own repair charges. Remaining ties use displacement in cells/pixels, moved-node count and a stable serialized target key. Repeating the same request chooses the same state.

Complete state keys combine deterministic geometry and authoritative cell/gap digests. The current baseline and already examined complete candidates are skipped. An optional immutable accepted-state journal prevents returning to an earlier accepted arrangement during a sequence of moves. Keep `HistoryAfterAcceptance` only after ordinary publication succeeds; cancelling a preview or rejecting a transaction must not advance the journal. Previously visited states are search evidence, not a new legality rule.

History is scoped to the document, source revision, captured options, explicit locks, padding and geometry-request signature. The signature shares the source service's typed constraint/font/metadata serializers, including derived metadata values; it excludes source positions and paint. Changes to semantic metadata, hints, fonts, topology or other geometry requests start a fresh scope. Layout revisions alone may advance within the same automatic sequence. The signature also strengthens independent trial verification against accidental changes to these inputs.

Results retain each trial's reason, skipped locked nodes, unique/duplicate/revisited counts and budget exhaustion. A no-result message says that no verified improvement was found among generated candidates or within the budget; it never claims a global optimum. Cancellation is checked before work and between candidate operations. Cancelled work has no publishable result and leaves accepted geometry/history unchanged.

Repeated-acceptance tests compare predicted and ordinarily committed states, require strict improvement and reject repeated scenes. Visual preview and undoable source publication remain QUALITY-4 work. Search does not commit a candidate and makes no incremental speed claim.

# Add the approved bounded route-length comparison

19 October 2026. Reference: terminal-track checkpoint `d3fb2db`.

Shared full and incremental scoring now measure total final Euclidean route length, including exact terminal runs and diamond normals, divide by the existing routing grid unit, and apply the configured weight. They add `wL/(1+wL/B)` once for the complete layout, with B the smaller canonical bend/crossing weight. The separate rule 5.10 repair-length charges, 61-pixel added-length cap, 12-line bound and 16-candidate limit are unchanged.

Candidate comparisons carry the original primary score, weighted whole-layout length, bend count and crossing cost together. Length participates only when bend and crossing costs tie. When the primary score also ties, raw weighted lengths are compared exactly: neither the old fixed score epsilon nor floating-point saturation of the bounded term can suppress a genuine length improvement. Numeric observers still receive the complete score. Incremental single-route scoring caches unchanged lengths rather than remeasuring unchanged route points.

Seven added tests cover zero, ordinary and very large finite lengths; a term strictly below B; no length influence across different bend/crossing costs; exact improvements below the old epsilon; and one normalization for the whole layout. The focused score/cell-move run has 28 passes and only the same two pre-existing canonical-bend-weight failures. The hint-score arithmetic test now adds the approved length term, calculated independently from measured route points, to its expected total. Its hint, bend and reward assertions are unchanged; no port-side, label-clearance or aspect assertion was changed.

## Catalogue results

- Full capture: 673/673 pass against `approved-terminal-catalogue.jsonl`, with identical per-scenario bends, crossings and lengths.
- Published geometry/finding signatures: **zero changes** across all 668 completed layouts; the same five native-ineligible scenarios remain unmeasured.
- No scenario's findings increase. Totals remain **2998 bends / 2069 crossings / 330 shared-track pairs / 1199 rule 5.9 findings**. A104 remains 38/0 and A105 28/0, both with no same-fan crossing.
- All reported hard gates remain zero; 4.2=52, 3.1=2, 4.1=0, 4.11=5 and 5.8=3.
- Production audit: **58.23 s**, A142 **10.864721 s**. This is an unprofiled run, with the same geometry-signature capture used for the new baseline audits.
- Confirmed normal Release layout suite: **1140 pass / 205 fail / 1345 total**, with precisely the same 205 failure identities as the new baseline and no new failures. Results are recorded in `out/markup-review/bounded-length-normal-confirm.trx`.

The list of layouts changed by this scoring addition is empty. It supplies the previously missing canonical term for future tied alternatives without retaining any catalogue geometry change in this checkpoint. Evidence is under `out/markup-review/bounded-length-*`. Rule values and text, hints, guards, candidate limits and independent text spans are unchanged. No DOCX generation or push was performed.

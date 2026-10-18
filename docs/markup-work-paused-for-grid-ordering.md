# Pause markup repair work before reusing Dagre ordering

The routing-time split and scoped geometry reuse are already committed in
`4f921c1`. Production activation is `597bde3`. No additional timing changes or
fan-track engine changes were started during the markup investigation.

The finished new measurement fixture ran all **673** unchanged catalogue sources
successfully with production cells on. Its capture is
`out/markup-review/baseline.jsonl` (ignored measurement output). It checks
published legality, actual cell regions and rejected-write diagnostics and
records bends, all geometric crossings (including shared-endpoint routes) and
length. A supplied baseline makes it reject any per-scenario bend or crossing
increase. The two marked fans start at:

| Markup | Source | Bends | Crossings |
| --- | --- | ---: | ---: |
| 15 | A104, Customer Account Service, sixteen targets | 54 | 46 |
| 16 | A105, General Ledger, sixteen sources | 48 | 34 |

Unfinished markup work is paused: cause J nesting/labels and fan placement;
D/E/G/I route, allocation and placement fixes; the complete A–J diagnosis;
the five unapproved rule proposals with catalogue measurements; and the markup
acceptance audit. No rule draft was applied. `docs/markup-issues.md` remains the
user's original uncommitted list. No DOCX file is included or regenerated.

One genuine geometry conflict was found: a line joining two vertical sides at
different heights needs two corners, not one. A104's requested side-to-side
one-corner fan therefore cannot be implemented literally while rule 5.8 keeps
both facing-side requirements. This must remain explicit in the later diagnosis.

The replacement task is to share Dagre's existing barycentre ordering through a
thin adapter, measure complete constrained grid candidates, and retain it only
under the user's full gates, per-scenario bend/crossing and runtime conditions.
No engine code has changed at this pause point. Unrelated dirty files and DOCX
files remain untouched.

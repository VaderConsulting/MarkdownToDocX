Correction: the scores below compare an earlier stage before the last finished-route repair. The current published layout actually scores382.651775 versus candidate499.775215; see [the term-by-term measurement](a107-score-breakdown.md). The candidate remains rejected. This corrects the earlier stage-mismatched explanation.

# A107 loop-back placement trial

2026-10-20; no engine change retained, no commit, no DOCX or push.

The trial detected a real chain-closing node only when all its visible outgoing edges were back edges to the same earlier target and a forward path from that target proved membership in the same chain. Self-loops were excluded. It offered the free whole cell next to that target, on the flank away from the chain's first forward step. It excluded hint-controlled and grouped nodes, checked atomic occupancy and the existing band convention, and evaluated a complete private initial placement with all ports and routes rebuilt. Discarded contexts were isolated; exact hint statuses were restored.

A107 offered Review Account one column left of Draft Order on its starting row. Its complete candidate had 8 bends and 0 physical crossings, compared with 10 and 0 retained. The normal rule 2.13 score rose from 472.17479674796743 to 499.77521488047273. The candidate was rejected. Forcing the requested visual result would contradict the instruction to keep the lower normal score; no rule, weight, hint or checker was changed. The engine still retains Review Account below the chain, and the expected straight next-order result is not achieved.

The general candidate prototype was left out while the corridor work continued, rather than retain unvalidated changes to other loop diagrams. Thus no case changed from this trial. It is archived as a restorable patch and complete before/trial source snapshots, with the measured A107 JSON in `out/markup-review/loop-back-placement/trial/A107.json`. The patch passes `git apply --unidiff-zero --check` against the current working tree. It is not a DOCX artifact.

A fresh native production PNG, whose geometry hash is checked against the production layout, is in `out/markup-review/initial-corridor-capacity/selected/A107-after.png` once the final render check completes. The image remains unchanged because the candidate lost the score comparison. The retained port count is 2.

Decision made without asking Dave: respect the explicit normal-score requirement, archive the losing trial, and carry on with the corridor queue. The loop-back item remains open; do not treat the requested A107 arrangement as fixed.
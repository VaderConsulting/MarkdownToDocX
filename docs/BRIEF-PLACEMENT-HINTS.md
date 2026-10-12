# Brief: placement hints

## Purpose
A diagram can keep the structure of an original. A node, a group or a line may carry a hint on its own declaration line, and the hint is preferred to the calculated layout and routing. Rule 2.21 of `Instructions/LAYOUT-RULEBOOK.md` is the source of truth. Read it first, then rules 2.13, 2.19, 4.7, 4.8 and 9.6 and the preamble.

## Syntax to support
Hints are enterprise-diagram syntax first. Flowchart hint syntax is out of scope until Dave says so.

- A node or group hint is words at the end of its declaration line: `above`, `below`, `left-of` or `right-of` a named peer, optionally followed by an offset `+n`; or `push N`, `push S`, `push E` or `push W`.
- A line hint is in square brackets after the relationship: `leave`, `enter` and `travel`, each with N, S, E or W, separated by commas.
- N, S, E and W are screen directions. Never rotate them by rule 1.1.
- Group ids are valid relationship endpoints in the parser (rule 4.9 of the rulebook); scenario A164 needs it.

Examples:
```
application-component a2 "Part 2" right-of a1
application-component k2 "Kid 2" in box below k1
group box "Box" right-of out
hub -flow-> t1 [leave S, enter N]
d1 -flow-> d2 [leave N]
```

## Work in this order
1. Parse hints. Record the peer id, the offset and the directions. Report unknown peers, peers in another group and conflicts as diagnostics. Never fail.
2. Apply node and group hints in placement, before compaction. Take hints in source order. Use the same-cell rule and `push D` as rule 2.21 sets. Keep every honoured hint through compaction, wrapping, unconnected pieces and clusters.
3. Apply line hints in routing: `leave`, `enter` and `travel`. A hinted line is exempt from the corner limit of rule 4.3. It never breaks a legality rule; insert space as rule 1.7 sets.
4. Score hints as rule 2.21(g) says: -30 for each honoured node or group hint and -15 for each honoured line hint. Report the honoured and unhonoured counts for each scenario, with a reason for each unhonoured hint.
5. Check scenarios A154 to A164, then A150 to A153, in `enterprise.md`.

## Rules for the work
- Follow `Instructions/CLOUD-AGENT-WORKING-PRACTICES.md`.
- Every rule is generic. Nothing may depend on a scenario number, id or label.
- Parsing hints must not change the output of any scenario without hints. Measure the size of every diagram in `flow.md`, `enterprise.md` and `EIP.md` before and after, and list any change.
- Do not state a fixed size or count limit anywhere.
- Do not commit generated images.

## Done when
- No scenario A154 to A164 fails, including A163 (160 nodes, 40 hints) and A164 (group relationship endpoints).
- Every unhonoured hint is reported with its reason.
- Diagrams without hints render exactly as before.
- The five test suites show no new failures against the baseline.
- The report is only this table, one row for each of A150 to A164: size, ratio, overlaps, crossings, bends, hints honoured/total, empty share and score.

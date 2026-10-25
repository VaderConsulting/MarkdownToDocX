# Enterprise diagram syntax: consolidated design (revision 7)

Prepared for Dave Robinson on 20 October 2026. Revision 1 was written at 6:55 PM WST. Revision 4 (7:30 PM WST) was approved in principle, Revision 5 (7:45 PM WST) applied the corrections in decisions 11–17. Revision 6 (8:00 PM WST) restored the existing push meaning (decision 13) and recorded how scoring works today (section 5.2). Revision 7 (8:15 PM WST) makes only these corrections: the source-ordered push reference set, the rule 2.13 and term wording, final-geometry re-validation before scoring, and test 46 for note connections.

This is a design document. It makes no changes to `rules.yaml`, `enterprise.md` or the code. Every rule and grammar text below is a **proposal for Dave to approve**. Implementation starts only after the grammar text and rule wording for the workstream concerned have been approved.

**Sources** (as of 7:40 PM WST, in `C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX`):

| Short form | File |
| --- | --- |
| P: | `src\MermaidRecognition\Kinds\Enterprise\EnterpriseParser.cs` |
| C: | `src\MermaidRecognition.Layout\FlowchartPlacementHints.Cells.cs` (grid-mode hints) |
| SC: | `src\MermaidRecognition.Layout\FlowchartLayoutScore.cs` |
| LR: | `src\MermaidRecognition.Layout\FlowchartLayoutRules.cs` |
| F: | `src\MermaidRecognition.Layout\FlowchartPlacementHints.cs` (pixel-mode hints) |
| M: | `src\MermaidRecognition.Layout\Enterprise\EnterpriseFlowchartContractMapper.cs` |
| S: | `src\MermaidRecognition\Kinds\Enterprise\EnterpriseStatements.cs` |
| E: | `enterprise.md` |
| R: | `docs\Instructions\rules.yaml` |

## Decision log (final, binding)

| # | Decision | Workstream |
| --- | --- | --- |
| 1 | Mermaid familiarity is the governing principle. Elements stay typed, and relationships are still validated against `enterprise-rules.json`. | All |
| 2 | A declaration has at most one effective horizontal and one effective vertical placement hint, and each axis is validated on its own. A hint defines a relationship to the peer's complete final occupied region (positions per decision 11), not a stored cell number, so it is recalculated whenever that region changes. Occupancy conflicts are resolved by legal layout changes, including moving dependency chains. A neighbour may move only if every higher-priority constraint still holds. An honoured node never leaves either relationship unless that hint is explicitly dropped and reported. Only when the legal-repair process has used up its permitted candidates (decision 15) is the later axis in source order dropped. Cycles are detected per axis. Hints may ask for overlapping cells, but the final layout never contains an overlap. | B |
| 3 | A dedicated trailing-clause parser handles `in` and placement hints in either order. It gives precise diagnostics for duplicate clauses, malformed offsets, missing peers and misplaced `:::`. **There is no transitional "first hint only" behaviour.** | B (clause parser), A (diagnostics for today's grammar) |
| 4 | When a hint is only partly valid, the valid parts are kept, each rejected part is dropped and reported, and conflicts are never reinterpreted silently. | B |
| 5 | `subgraph id … end` is an alternative way to write a group. It requires an explicit id, takes the label as `id["Label"]`, `id ["Label"]` or `id "Label"`, and keeps nesting, local `direction` and membership. `group … { }` stays. Bare-title and anonymous subgraphs are **deferred** (see section 7). | C |
| 6 | `<type> id["Label"]` is a neutral alias for `<type> id "Label"`. Mermaid shape syntax never overrides element geometry, and each unsupported shape gets a specific diagnostic. **Unquoted `id[Label]` is not accepted** (see section 8). | C |
| 7 | Genuine syntax and semantic errors still fail the diagram. Recoverable hint faults produce a warning and the diagram renders normally. **Partial rendering is out of scope** and noted only as a possible future feature. | A (policy rule), B (hint cases) |
| 8 | Keywords are case-insensitive and identifiers are case-sensitive. Keywords are recognised from context. Diagnostics are specific. `:::` never styles a hint peer. | A |
| 9 | The test matrix separates these outcomes: honoured, accepted but dropped, invalid syntax, unsolvable, displaced successfully, and recalculated after a peer grows. | A, B, C |
| 10 | There are three workstreams, with no transitional grammar. Grammar and rule wording are approved before implementation, and nothing in A anticipates or contradicts B. | – |
| 11 | **Multi-cell positions** use zero-based logical cells. right-of P +n gives StartColumn = P.StartColumn + P.ColumnSpan + n. left-of P +n gives StartColumn = P.StartColumn − Node.ColumnSpan − n. below P +n gives StartRow = P.StartRow + P.RowSpan + n. above P +n gives StartRow = P.StartRow − Node.RowSpan − n. The standard physical gap stays mandatory. The position is recalculated whenever either node's relevant span changes or the peer moves. | B |
| 12 | **Scoring:** −30 for each placement axis honoured on its own (−60 for two axes, −30 for one, 0 when dropped). There is no new alignment score. −15 once for each honoured line hint (per connection, not per part). Implementing this is a prerequisite for B, checked through the real scoring path (section 5.2, tests 45–47). | B |
| 13 | **push** keeps its existing outward meaning, with a **deterministic, source-ordered reference set**. A push places its node just beyond the occupied envelope of its ordinary (unpushed) siblings plus the siblings pushed **earlier** in source order, in direction D. A later pushed sibling never invalidates an earlier honoured push. Re-evaluation keeps that order. When the applicable reference regions grow or move, the position is recalculated. Outward stacking is kept. With no applicable reference regions, the hint is dropped and reported as `no sibling reference region`. | B |
| 14 | **Placement peers** are elements, groups and notes, under the existing nesting and parent rules. | A (wording), B |
| 15 | **Repair exhaustion:** a hint is dropped only after the applicable deterministic legal-repair process has used up its permitted candidates. Impossibility is claimed only when it is established. The diagnostics distinguish "conflicting constraints" from "no legal candidate found within the permitted search". | B |
| 16 | **Redeclaration:** a warning is given only when the effective placement hint changes materially, not when it is identical. | A |
| 17 | The test matrix is extended to match decisions 11–16. | A, B |
| 18 | Before scoring, the existing final validation re-validates each retained placement constraint against the **final** occupied regions and sets its honoured status from that. The score uses those final statuses. This is not a new stage. | B |
| 19 | Rule 2.13's hint term and the terms Peer and Honoured are reworded to match 2.21(g): per independently honoured axis (notes included), and once per honoured connection-level line hint. | B (wording for Dave) |
| 20 | A valid, honoured line hint on a plain note connection scores −15, the same as on a relationship (rule 4.10). | B |

---

## 1. Governing principle: Mermaid familiarity

**Objectives**

1. **Familiarity:** existing Mermaid syntax behaves as expected wherever that is practical.
2. **Consistency:** Enterprise extensions follow Mermaid conventions.
3. **Compatibility:** Mermaid syntax is accepted only where its meaning fits Enterprise.

Familiarity does not mean supporting every flowchart feature.

**Clauses** (proposed for the opening of the Syntax decisions in `enterprise.md`)

1. Preserve Mermaid-compatible syntax unless the Enterprise model forbids it.
2. Accept `subgraph … end` alongside `group` and braces.
3. Keep Mermaid's direction keywords and styling statements.
4. Keep relationship meaning explicit, and never reinterpret `-->` as a modelling relationship.
5. Allow one horizontal and one vertical placement hint, validated independently, with deterministic conflict resolution.
6. Hints are optional. An invalid hint is diagnosed and ignored whenever the statement itself stays valid.
7. Never reject a valid diagram because of a recoverable hint fault.
8. Recognise keywords from context.
9. Make every diagnostic specific.
10. Keep one documented grammar, verified by executable tests.

Familiarity must not compromise element types, relationships, modelling rules or mandatory geometry.

## 2. Authority model

| Source | Owns |
| --- | --- |
| `enterprise.md` | The grammar, syntax decisions and examples. It must not promise syntax that the parser rejects. |
| `rules.yaml` | Meaning, constraints, precedence, layout and the error policy. It does not hold the grammar. |
| `EnterpriseParser.cs` | The implementation of the grammar. It must not accept syntax that the grammar leaves out. |
| `enterprise-rules.json` | Element and relationship validity for each language version. |
| The test catalogue | Executable evidence that all of the above agree. Its counts are generated, not typed. |

## 3. Error policy

| Condition | Severity | Diagram |
| --- | --- | --- |
| Valid source, all hints honoured | none | Renders normally |
| Recoverable placement-hint fault: missing peer, peer in another group, cycle on that axis, second hint on the same axis, or band restriction | Warning naming the hint and the reason | Renders normally without that hint (axis) |
| Placement conflict, established: the constraints provably exclude each other, for example two higher-priority honoured hints fix the same cells | Warning: `conflicting constraints`, naming both hints | Later axis dropped; renders normally |
| Placement conflict, search exhausted: repair used up its permitted candidates without finding a legal arrangement | Warning: `no legal candidate found within the permitted search`, never phrased as impossible | Later axis dropped; renders normally |
| Redeclaration that materially changes the effective placement hint | Warning naming both lines | Later declaration used; renders normally |
| Recoverable line-hint part: unknown part, repeated part, contradiction, port conflict | Warning naming the part | Renders normally with the remaining parts |
| Unknown element type | Error | Fails (error placeholder) |
| Invalid relationship type or qualifier, or a relationship that `enterprise-rules.json` does not allow | Error | Fails |
| Unresolved endpoint, or `in` naming no container | Error | Fails |
| Unsupported Mermaid arrow (`-->`, `-.->`, `==>`, `&`) or Mermaid shape | Error with the Enterprise alternative | Fails |
| Unclosed `subgraph` or `group { }` | Error naming the opening line | Fails |
| Malformed hint syntax (`+abc`, `above` with no peer), or a duplicate `in` clause | Error, because the statement cannot be read | Fails |

The last row follows decision 7. A hint that cannot be *read* is a syntax error. A hint that is read correctly but cannot be *honoured* is recoverable.

**Partial preview** (rendering the valid remainder of a failing diagram) is a separate possible future feature. It is **outside the scope** of this design.

## 4. Grammar (target)

### 4.1 Statements

```text
[--- YAML frontmatter (title, config) ---]          only at the very start
[%%{init: …}%%]                                      anywhere
enterprise | enterprise-beta [TB|TD|BT|LR|RL]
  title <text>
  accTitle: <text>      accDescr: <text>  |  accDescr { … }
  version 3.1 | 3.2
  direction TB | TD | BT | LR | RL                   in a block: that group's direction
  group <id> [<label>] <clauses> [{ … } | { … end]
  subgraph <id> [<label>] <clauses>
    …statements…
  end
  <element-type> <id> [<label>] <clauses>
  note <id> <quoted> <clauses>
  <src> -<relationship>[(qualifier)]-+> <tgt> [<quoted>] [<line-hint>]
  <a> ---+ <b> [<line-hint>]                        one end must be a note; at least three dashes
  classDef … | class … | style … | linkStyle …
  %% comment

<label>      ::= <quoted> | "[" <quoted> "]"      optional whitespace before "["; bracket form
                                                   allowed after element types and subgraph ids
<quoted>     ::= "…" | '…' | "`…`"
<clauses>    ::= any order of: [in <container>] [<h-hint>] [<v-hint>]   each at most once
<h-hint>     ::= left-of <peer> [+n] | right-of <peer> [+n] | push E | push W
<v-hint>     ::= above <peer> [+n] | below <peer> [+n] | push N | push S
<line-hint>  ::= "[" <part> {"," <part>} "]"
<part>       ::= leave D | enter D | travel D      D ::= N | S | E | W
:::class     only directly after a declared id, after its label, or after an id in a relationship
;            separates statements on one line (kept, unchanged: baseline bullet E:52 and the parser)
"`…`"        a markdown string may continue over several lines (kept, unchanged: E:52 and the parser)
```

The last two lines of the box record forms that are kept unchanged: `;` as a statement separator, and markdown strings that continue over several lines. Both are already accepted under baseline bullet E:52 and by the current parser (`;` splitting at S:38–46), and revision 7 neither replaces nor deprecates them.

Keywords are case-insensitive in every position: the header, statement words, `in`, the hint words, the direction letters and `end`. Identifiers are case-sensitive. Keywords are recognised from context. `below` and `push` can be ids, and the word `end` closes a block only when it appears on its own as a statement while a block is open.

### 4.2 Element types and relationships (unchanged)

There are 61 element types. Each can be written in kebab-case, without hyphens, or in CamelCase, in any letter case (E:55). The relationships are composition `c`, aggregation `g`, assignment `i`, realization `r`, serving `v`, access `a(read|write|readwrite|access)` (write by default), influence `n(+|-|++|--|±n)`, triggering `t`, flow `f`, specialization `s` and association `o(directed)`. Every relationship is checked against `enterprise-rules.json`.

The dashes immediately before `>` set the minimum rank span, using the same rule as a Mermaid flowchart arrow (`docs/Mermaid_Specification.md` §6.3). Count those dashes, subtract 1, and keep the result between 1 and 10. `->` and `-->` are length 1. `--->` is length 2. `---->` is length 3. A qualifier sits before that dash run: `-access(write)--->`. A plain note line uses the same idea on the whole dash run, so `---` stays length 1 and `----` is length 2. Bare `A --> B` is still not a modelling relationship.

### 4.3 Syntax examples

```mermaid
enterprise BT
  subgraph roles["Registered Roles"]
    direction LR
    business-role admin["SDT Administrator"]
    Business-Role custodian "SDT Security Custodian" right-of admin
  end
  group delivery "Delivery" {
    business-process publish "Publish" below roles
  }
  business-service pubSvc "Content Publishing Service" right-of delivery below roles
  note n1 "Reviewed yearly" in delivery
  pubSvc -serving-> admin [leave N, enter S]
  publish -realization-> pubSvc
  n1 --- publish
```

`publish below roles` would be dropped and reported (a recoverable fault), because its peer is not in the same group. The diagram still renders.

## 5. Placement hints: meaning and conflict resolution

1. **Axes.** Each axis is independent, and the second hint never overwrites the first. A further hint on an axis that already has one is dropped with a warning. Hints on one axis are never combined into "between". For example, `right-of a left-of b` and `right-of a push E` both keep `right-of a`.
2. **Positions.** These use zero-based logical cells; span means the number of cells occupied.

   | Hint | Hinted coordinate |
   | --- | --- |
   | `right-of P +n` | StartColumn = P.StartColumn + P.ColumnSpan + n |
   | `left-of P +n` | StartColumn = P.StartColumn − Node.ColumnSpan − n |
   | `below P +n` | StartRow = P.StartRow + P.RowSpan + n |
   | `above P +n` | StartRow = P.StartRow − Node.RowSpan − n |

   The standard physical column or rank gap between the two regions is always kept. The cells give the logical position, and the gap is added when the cells are converted to geometry. The hint is a relationship, not a stored cell. It is recalculated whenever the peer moves or the relevant span of either node changes. That means the peer's span for `right-of` and `below`, and the hinted node's own span for `left-of` and `above`.
3. **Region.** The hinted node's whole region must be free, not just its start cell. The final layout never contains an overlap, although hints may request overlapping occupancy.
4. **Resolving occupancy.** The steps run in this order:
   1. Keep every honoured relationship.
   2. Run the deterministic legal-repair process: move or expand other regions legally, including whole dependency chains.
   3. A neighbour moves only if every higher-priority constraint still holds: legality, group containment (7.8), bands (2.23) and its own honoured hints.
   4. Drop the later axis in source order only after that process has used up its permitted candidates. Report it as `conflicting constraints` when the conflict is established, or as `no legal candidate found within the permitted search` otherwise. Never claim impossibility that hasn't been established.
   5. Lay out the node by its remaining axis.
5. **Single-axis hints** keep 2.21(b) as it stands. The other start coordinate is the peer's. When the region is occupied, the node moves further in the hinted direction.
6. **Cycles** are detected per axis. `A right-of B` together with `B right-of A` is a cycle, so the later hint is dropped. `A right-of B` together with `B below A` is not a cycle.
7. **Bands.** If one axis would take the node out of its band, reorder the bands or reorder the plateaus, only that axis is dropped.
8. **Scoring (2.13 via 2.21(g)).** Each placement axis honoured on its own scores −30, so two honoured axes score −60, one scores −30, and a dropped axis scores 0. There is no separate alignment score. A line hint scores −15 once for each honoured connection hint, however many of its leave, enter and travel parts it has.
9. **Push** (existing outward meaning, with a source-ordered reference set).
   - `push D` places the node just beyond the occupied envelope, in direction D, of its **reference set**. That set is every sibling (a member with the same parent) that has no push hint, plus every sibling whose push hint comes **earlier in source order**.
   - The node itself and any later-pushed siblings are excluded. On the other axis, the node keeps its current coordinate.
   - Pushes may enlarge the group or diagram, and successive pushes stack outward in source order.
   - A later push never invalidates an earlier honoured one. The honour check for an earlier push looks only at that push's own reference set.
   - Re-evaluating with the same reference regions gives the same position, so there is no drift. When reference regions grow or move, the position is recalculated in source order.
   - With an empty reference set, the hint is dropped with the warning `no sibling reference region`.
10. **Peers** are elements, groups and notes, all subject to the same nesting and parent rules.
11. **Redeclaration.** If an id is declared again with the same type, the later declaration's hint is used. A warning is given only when the effective hint changes materially (a different kind, peer, offset, direction or axis set). Repeating the identical hint gives no warning.

### 5.1 Finding: how `push` works today (checked on the laptop at 7:40 PM WST)

There are two implementations, one per layout mode.

- **Grid mode, C:61–74.**
  - The boundary is `CellEnvelope` of the **siblings**, meaning the other nodes with the same parent, excluding the moving unit (C:63, C:65).
  - The unit is then translated so that it starts **just past that envelope**. For push E, `Columns = Boundary.EndColumn − OwnerRegion.StartColumn` (C:71), where `EndColumn` is exclusive (C:192–198). N, S and W work the same way (C:68–70).
  - If the target overlaps anything, the loop at C:103–115 steps one more cell in the push direction. Its comment says "the finite set of occupied regions bounds this monotone search" (C:112–113).
  - The accepted state is checked by `CellHintMatches`, which for push only tests `Owner.StartColumn >= Peer.EndColumn` and the equivalent for the other directions (C:170–176).
- **Pixel mode, F:68–71 and F:219–245.** The same idea in pixels: the unit is moved to `Other.Right + NODE_GAP_PX`, and likewise for the other directions (F:237–240).
- **"Farthest free cell" today** means the first position beyond the bounding box of the siblings, which is the meaning decision 13 keeps. A push may therefore enlarge the group's or diagram's extent.
- **Drift?** None. The envelope excludes the moving unit, so applying the same push again lands in the same place, and the stepping loop ends after finitely many occupied regions. Successive pushes in the same direction stack outward, and a push follows its siblings' envelope when that grows. Both are intended under decision 13.
- **Silent skip.** When the unit has no siblings, the push is skipped with no report (C:64 `continue`, F:228 `return false`). This is the one remaining push fault. Decision 13 requires the warning `no sibling reference region`.
- **Other axis.** On the other axis the push keeps the unit's current coordinate, which matches decision 13.

**Multiple pushes today (checked at 8:10 PM WST). They don't match the source-ordered reference set.**

- **Evaluation order.** Hints are applied once per call, in source order (C:45; F:37).
- **The envelope includes all other siblings.** Grid mode takes every node outside the moving unit in the same parent (C:58, C:63). Pixel mode does the same (F:226–227). The envelope therefore contains earlier pushed siblings at their new positions, because `Targets` is updated as hints are accepted (C:117). It also contains **later** pushed siblings at their not-yet-pushed positions, which the reviewer's set excludes.
- **The honour check also uses all siblings.**
  - `CellHintMatches` for push (C:165–176) requires the owner to lie beyond every other sibling.
  - When c is pushed past b, b's earlier honoured push no longer matches. C:124–129 then rejects **c** with `placement conflicts with an earlier explicit hint`.
  - The group-anchor retry can't rescue it, because it skips push hints (`FlowchartPlacementHints.CellGroupAnchors.cs:29`).
  - So in grid mode, a second push in the same direction and group is dropped instead of stacking.
- **It is run again.**
  - `Apply` runs at `DiagramGridLayoutStages.cs:153`, and again after the groups are fitted, as "group-fit/refresh-hints" (`DiagramGridLayoutStages.cs:322`).
  - `CellHintsMatch`, which uses the same all-siblings test, is used as a gate in later repair (`DiagramGridLayoutStages.cs:1578`) and in final repair (`DiagramGridLayoutFinalRepair.cs:141`).
  - In pixel mode there is no honour check. Each re-run of the all-siblings envelope moves an earlier push beyond a later one, and then the later one beyond that (example 6.6). So positions drift by one step on every re-run.

- **Relative hints already follow decision 11.** `right-of` uses `Anchor.EndColumn + Offset`, which is P.StartColumn + P.ColumnSpan + n (C:97–98). `left-of` uses `Anchor.StartColumn − OwnerRegion.EndColumn − Offset`, which gives StartColumn = P.StartColumn − Node.ColumnSpan − n (C:94–95). `below` and `above` are the same (C:88–93). The hint's position is recomputed from the current regions each time hints are applied (C:57, C:83), so stale coordinates aren't stored there.

### 5.2 Finding: how hints are scored today (checked on the laptop at 7:55 PM WST)

- **Where the values come from.** `score_hint_node_group` is −30 and `score_hint_line` is −15. Both are defined in `rules.yaml` (R:443–452, source "rule 2.21(g)") and listed as 2.21 parameters (R:1098). The engine reads them from the embedded `rules.yaml` through `FlowchartLayoutRules.NodeGroupHintScore` and `LineHintScore` (LR:38–39). No values are hard-coded.
- **Where the score is calculated.** `MeasureHints` (SC:641–680) is added to the layout score at SC:45–53. It is also included in the cached static score at SC:132–133.
- **Placement hints are counted per hint, which today is per node.**
  - SC:672–675 counts every node and group whose `PlacementHint` is active and has no `UnhonouredReason`, and multiplies that count by −30 (SC:678).
  - Notes are contract nodes (M:62–64), so an honoured note hint also scores −30.
  - A contract node holds a single `PlacementHint`, so there is no axis concept.
  - The count reads the hint's **status flags**. It doesn't re-check the geometry.
- **Line hints are counted per connection.**
  - Each edge with a `RoutingHint` counts at most once (SC:654–670).
  - The hint is honoured only if it is active, has no `UnhonouredReason`, has a route, and **every part present matches**: the exit side for `leave` (SC:662–663), the entry side for `enter` (SC:664–665) and the travel direction for `travel` (SC:666–668).
  - It scores −15 once (SC:676–679). This already matches decision 12.
- **Dropped hints score 0.** A placement hint with `IsActive` false or an `UnhonouredReason` isn't counted. A line hint whose parts don't all match scores 0.
  - Because of P:605–616, a single invalid line-hint part sets `IsActive` false. The whole connection then scores 0, even when the valid parts are honoured.
  - **Plain connections:** I found no route from a note's `---` line hint into `Contract.Edges` scoring. Whether those hints score at all is unverified, and should be checked in test 46.

**What must change (a prerequisite for implementing B, with unrelated scoring unchanged):**

1. **Placement, per axis.** Replace the single-hint count with a count of the **independently honoured axes**: honoured axes × `score_hint_node_group`. That needs a horizontal and a vertical hint, each with its own `IsActive` and `UnhonouredReason`, in `PlacementHintContract` or alongside it on nodes, notes and groups. A node with two honoured axes then scores −60, one honoured axis −30, and dropped axes 0. Push counts as one axis. The parameter value stays at −30. Only the definition text of `score_hint_node_group` changes from "per node or group hint" to "per honoured placement axis", which is a `rules.yaml` wording change for Dave.
1a. **Placement honour from the final geometry (decision 18).** Set each axis's honoured status in the existing final validation by re-checking it against the final occupied regions. Don't read the status flags left over from placement.
2. **Lines, per connection with partial recovery.** Keep counting once per connection. Under decision 4, mark only the rejected parts as dropped and keep the connection hint active. Then match only the **kept** parts, so the connection scores −15 when all of its kept parts are honoured and 0 when any kept part isn't. A connection with no kept parts scores 0.
3. **Bending, crossing, aspect, empty-area and disconnection terms** in SC:45–53 stay unchanged.
4. **Tests 45–47 must use the real path.** Run the full layout on a test diagram, read the total from `FlowchartLayoutScore` (the `MeasureHints` contribution), and assert the exact hint term. A hand-built score or a mocked hint count doesn't satisfy them.

### 5.3 Final re-validation before scoring

- As part of the **existing** final validation and scoring, every retained placement constraint, axis by axis, is checked again against the **final** occupied regions.
  - Relative hints are checked with the decision 11 formulas.
  - Push hints are checked against their source-ordered reference set.
- The honoured status is set from that check. The score (section 5.2) is calculated from those final statuses.
- A constraint that a later repair broke is reported and scores 0. It is never counted as honoured because of an earlier status flag.
- This is not a new stage. It replaces the flag-only reading at SC:672–675 inside `MeasureHints`.

## 6. Worked examples of conflict resolution

Coordinates are (row, column), zero-based, with the top-left cell at (0,0). Spans are written rows×columns.

**6.1 Recalculation after a peer grows**

```mermaid
enterprise
  business-role a "A"
  business-role b "B" below a
  business-role c "C" right-of a below b
```

- **First pass:**
  - a is at (0,0), 1×1.
  - b: StartRow = 0 + 1 = 1, and the column is a's start column, 0, so b is at (1,0).
  - c: StartColumn = 0 + 1 = 1 and StartRow = 1 + 1 = 2, so **c is at (2,1)**.
- **a grows to 1×3** after its label is fitted:
  - b is unchanged, because a's RowSpan is still 1.
  - c: StartColumn = 0 + 3 = **3** and StartRow = **2**, so **c is now at (2,3)**.
- A stale (2,1) would put c under a, not to its right. Score: b −30, c −60.

**6.2 left-of and above with multi-cell hinted nodes**

```mermaid
enterprise
  business-role p "P"
  business-role w "A very long role name that needs three columns" left-of p
  business-role q "Two-row role" above p
```

- p is at (3,5), 1×1, and w is 1×3.
  - w: StartColumn = 5 − 3 − 0 = **2**, and its row is p's start row, 3. So w occupies (3,2)–(3,4) and ends just before p. With `left-of p +1`, StartColumn = 1.
- q is 2×1.
  - q: StartRow = 3 − 2 − 0 = **1**, and its column is p's, 5. So q occupies (1,5)–(2,5) and ends just above p.
- If w later grows to four columns, StartColumn = 5 − 4 = 1, and it is recalculated without any stored value.
- The physical column gap and rank gap are added between the regions as usual.

**6.3 Chain displacement**

```mermaid
enterprise
  business-role a "A"
  business-role b "B" below a
  business-role u "U"
  business-role v "V" right-of u
  business-role c "C" right-of a below b
```

- Automatic placement puts u at (2,1), and v's hint puts it at (2,2). c needs (2,1).
- The repair moves the u→v chain one column: u to (2,2), v to (2,3). v's `right-of u` still holds.
- Result: all hints honoured. c scores −60 and v −30.

**6.4 Established conflict: the later axis is dropped**

```mermaid
enterprise
  business-role a "A"
  business-role b "B" below a
  business-role h "H" right-of a below b
  business-role c "C" right-of a below b
```

- h, declared earlier, takes (2,1) with both axes honoured. c's relationships fix exactly the same cell.
- No candidate can keep both of c's relationships without overlapping h or moving h off its own higher-priority hints. This is established from the constraints, not from a search.
- The warning is: `line 5: placement hint "below b" dropped: conflicting constraints with h (line 4)`.
- c keeps `right-of a`, so it takes a's start row: StartColumn = 0 + 1 = 1, giving (0,1). Score: h −60, c −30.

**6.5 Search exhausted (shown for the diagnostic)**

- Suppose a two-axis node's target region is held by a long chain of hinted neighbours inside a group frame that can't widen, and the permitted repair candidates (chain moves, row and column insertion within the region) run out without a legal arrangement.
- The later axis is dropped with: `placement hint "below b" dropped: no legal candidate found within the permitted search`.
- The message doesn't claim that no arrangement exists.

**6.6 Successive pushes: the source-ordered reference set against the naive version**

```mermaid
enterprise
  business-role a "A"
  business-role b "B" push E
  business-role c "C" push E
```

- a is at column 0. All three are top-level, 1×1.
- **Source-ordered (decision 13):**
  - b's reference set is {a}, so b starts at 0 + 1 = **1**.
  - c's reference set is {a, b}, an earlier push, so c starts at 1 + 1 = **2**.
  - On re-evaluation, b's set is still {a}, giving 1, and c's is {a, b}, giving 2. **The layout is stable.** b's honour check ignores c, so c never invalidates b.
- **Naive (all siblings, as the engine does today):** suppose re-evaluation starts from b at 1 and c at 2.
  - b's set is {a, c}. The envelope ends at column 2, so b moves to **3**.
  - c's set is {a, b}. The envelope ends at 3, so c moves to **4**.
  - The next pass gives b 5 and c 6, and so on.
  - Each pass moves both further east, and nothing converges. In grid mode today, the all-siblings honour check instead rejects c on the first pass (section 5.1).

## 7. `subgraph` (Mermaid compatibility)

| Form | Meaning |
| --- | --- |
| `subgraph roles` | Grouping `roles`, labelled with its id |
| `subgraph roles["Registered Roles"]` | Grouping `roles`, labelled "Registered Roles" |
| `subgraph roles ["Registered Roles"]` | The same (whitespace before `[` is optional) |
| `subgraph roles "Registered Roles"` | The same, matching how `group` is written |
| `subgraph roles[Registered Roles]` | Error: `quote the label: subgraph roles["Registered Roles"]` |
| `subgraph Registered Roles` | Error: `subgraph needs an id, for example subgraph regRoles["Registered Roles"]` |

The block closes with `end`. Inside it, `direction` sets the group's direction, and declarations become members (the same as a `{ }` block, P:219–262). Nesting works as it does for `group`. A `}` that tries to close a `subgraph`, an `end` with nothing open, and a block left open at the end of the source are all errors.

**Deferred: bare-title and anonymous subgraphs.** Groups are Enterprise modelling elements. They can be the source or target of relationships and the peer of a hint, so they need stable ids that the author chose. A generated id such as `subgraph-1` would change whenever subgraphs were added or reordered.

**Today's group and the equivalent subgraph.** Both produce identical parse results:

```mermaid
enterprise
  group roles "Registered Roles" {
    business-role admin "Admin"
  }
```

```mermaid
enterprise
  subgraph roles["Registered Roles"]
    business-role admin "Admin"
  end
```

## 8. Labels and Mermaid shapes

- **Accepted:** `<type> id["Label"]` and `<type> id ["Label"]`, as aliases of `<type> id "Label"`. The quoted text inside the brackets is the ordinary quoted-label token, so `['Label']` and markdown strings work too.
- **Unquoted `id[Label]`: not accepted (my choice).** The reviewer approved only the quoted form. An unquoted label can't carry `]`, escapes, `<br>` or markdown, and accepting it for elements but not subgraphs (decision 5) would be inconsistent. The diagnostic is `quote the label: business-role a["Label"]`.
- **Rejected with a shape diagnostic:** `(…)`, `([…])`, `[[…]]`, `[(…)]`, `((…))`, `>…]`, `{…}`, `{{…}}`, `[/…/]`, `[\…\]`, `@{ shape: … }`. Example message: `"((…))" is a Mermaid shape; enterprise geometry comes from the element type. Write: business-role a "Label"`.
- **A bracket label with no type** (`a["X"]`) gives the error `an element needs a type, for example business-role a["X"]`.
- **`-->` and other flowchart arrows** give the error `"-->" is a flowchart arrow; name the relationship, for example a -serving-> b or a -flow-> b`.
- **`---` between two elements** gives an error suggesting `-association->`.

## 9. Proposed `rules.yaml` wording (for Dave's approval)

**2.21 Hints** (replaces the current (a), (b), (c) and (d), and adds to (f) and (g); (e) and (h) are unchanged)

> - (a) Written: a node, note or group hint is written after its declaration's label, before or after its `in` clause. Horizontal hints are left-of or right-of a named peer, optionally followed by +n, or push E or W. Vertical hints are above or below a named peer, optionally followed by +n, or push N or S. A declaration carries at most one effective hint on each axis. A further hint on an axis that already has one is dropped and reported, and hints on one axis are never combined. A line hint is written in square brackets after the relationship: leave, enter or travel, each followed by N, S, E or W, separated by commas. Every direction word is relative to the reader's view of the finished page, whatever the diagram direction (rule 1.1).
>
> - (b) Meaning: a relative hint is a relationship to its peer's complete final occupied region, not a stored cell. In zero-based logical cells: right-of P +n gives StartColumn = P.StartColumn + P.ColumnSpan + n; left-of P +n gives StartColumn = P.StartColumn − Node.ColumnSpan − n; below P +n gives StartRow = P.StartRow + P.RowSpan + n; above P +n gives StartRow = P.StartRow − Node.RowSpan − n. The standard column gap or rank gap between the two regions is always kept. Recalculate the position whenever the peer moves or the relevant span of either node changes. The hinted node's whole region must be free, and the final layout never contains an overlap. A node with a hint on one axis only takes its peer's start row (or column) on the other axis. If its region is occupied, it moves further in the hinted direction by whole cells and never changes that start row or column. A node with hints on both axes has both start coordinates fixed by its relationships. "push D" places the node just beyond, in direction D, the occupied envelope of its reference set: every sibling (same parent) without a push hint, plus every sibling whose push hint comes earlier in source order. The node itself and later-pushed siblings are excluded. The node keeps its coordinate on the other axis. A push may enlarge the group or diagram, and successive pushes stack outward in source order. A later push never invalidates an earlier honoured push. Re-evaluating with unchanged reference regions must not move the node. When reference regions grow or move, recalculate in source order. With an empty reference set, drop the hint and report "no sibling reference region". "leave D" and "enter D" name the side of the source box and of the target box that the line uses. "travel D" is the direction of the line's middle run, so a straight line, which has no middle run, cannot honour travel.
>
> - (c) Peers: a peer must be an element, note or group at the same nesting level in the same group. A missing or cross-group peer causes the hint to be dropped and reported. Peer circles are detected separately on each axis. A valid peer may belong to another automatic cluster: honour the hint first, then move, split or recompute clusters without breaking group membership or geometry.
>
> - (d) Order and conflicts: take hints in source order. A hint that closes a circle of peers on its axis is dropped and reported, and the earlier hints stay. For line hints, two lines hinted to the same port on one box side is a conflict, and the later hint is dropped and reported. Within one line hint, each invalid or repeated part is dropped and reported, the first of a repeated part is kept, and the valid parts stay. Hints may request overlapping occupancy, but the final layout must never contain an overlap. Resolve occupancy with the deterministic legal-repair process, which includes moving whole dependency chains. A neighbour may move only if every higher-priority constraint still holds, including its own honoured hints. An honoured node never leaves either of its hint relationships unless that hint is explicitly dropped and reported. Drop the later axis of a node in source order only after the repair process has used up its permitted candidates, subject to the same higher-priority constraints. Report it as "conflicting constraints" when the conflict is established, and otherwise as "no legal candidate found within the permitted search". Never report a conflict as impossible unless that has been established.
>
> - (f) Legality and routing faults: … (current text unchanged), followed by: "A valid line hint that forces a detour is honoured, scored under 2.13 and reported with the detour it causes. No cost threshold drops it."
>
> - (g) Score, replacing the first sentence: "Each placement axis honoured on its own scores −30 in rule 2.13, so a node, note or group with two honoured axes scores −60 and one with a single honoured axis −30. Each honoured line hint scores −15 once per connection, however many parts it has. There is no separate alignment score." The rest of (g) is unchanged.

**Rule 2.13, hint term (R:963).** It currently says "Hints: -30 per honoured node/group hint and -15 per honoured line hint (2.21)." Proposed:

> Hints: −30 per independently honoured placement axis of a node, note or group, and −15 once per honoured connection-level line hint, on a relationship or a plain note connection (2.21(g), 4.10). Honoured status is taken from the final validation against the final occupied regions.

**Term definitions.**

- **Peer** (`term.peer`, R:251–258). It currently reads "the named node or group of a node or group hint; see rule 2.21." Proposed:

  > the named element, group or note of a placement hint, at the same nesting level in the same group; see rule 2.21(c). For push, the reference set of rule 2.21(b) takes the place of a named peer.

- **Honoured** (`term.honoured`, R:259–266). It currently reads "a hint is honoured when the final layout satisfies it; see rule 2.21." Proposed:

  > a placement hint is honoured axis by axis: each horizontal or vertical placement constraint is honoured independently when the final occupied regions satisfy it. A line hint is honoured once per connection when the final route satisfies every kept part. Honoured status is set by the final validation; see rules 2.13 and 2.21.

**Parameter definitions (R:443–452):** keep `score_hint_node_group` at −30, but define it as applying "per independently honoured placement axis of a node, note or group". Keep `score_hint_line` at −15, applying "once per honoured connection-level line hint". The values don't change.

**2.23, last paragraph** (cross-reference only):

> … Such a hint is dropped and reported under 2.21(c), with no score under 2.21(g). When a declaration has hints on both axes and only one breaks a band rule, only that hint is dropped.

**New 2.24 Diagnostics and error policy** (the id is a proposal; no such rule exists today, and neither "placeholder" nor an error policy appears in `rules.yaml`):

> Diagnostics and error policy. A source that cannot be read as valid statements, or that breaks the model (unknown element type, an invalid relationship type, qualifier or rules-table cell, an unresolved endpoint or container, an unsupported Mermaid arrow or shape, an unclosed block, malformed hint syntax or a duplicate clause), is an error, and the diagram is not drawn. A hint that is read correctly but cannot be honoured, and an invalid or conflicting part of a line hint, is a warning: drop only that hint, axis or part, report it with its reason, and draw the diagram normally. A placement axis dropped by the repair process is reported as "conflicting constraints" only when the conflict is established, and otherwise as "no legal candidate found within the permitted search". A redeclaration that materially changes the effective placement hint is a warning, and an identical one is not. Every diagnostic names the line, the construct and, where one exists, the accepted form.

## 10. Proposed `enterprise.md` text (for Dave's approval)

- **Syntax baseline:** replace the grammar box at E:30–47 with section 4.1, and change the identifier bullet at E:57 so that keywords are listed as case-insensitive in every position.
- **New decisions 31–36:**
  - **31 Placement hints:** section 5 and the 2.21 wording.
  - **32 Line hints:** partial recovery.
  - **33 Notes:** `note id "Text"`, with the label required.
  - **34 Plain connections:** `---` is used only with a note.
  - **35 Group blocks:** `{ }` or `end`, with `direction` inside.
  - **`:::` position (part of the section 4.1 baseline):** `:::class` is accepted only directly after a declared id, after its label, or after an id in a relationship. The current parser rewrites `:::` before it recognises the statement, so it accepted `in g:::cls`. **In revision 7, `in g:::cls` is an error.** No case in the enterprise.md corpus uses it (checked on 20 Oct 2026 at 8:15 PM WST: the only `:::` uses are four lines in A124, all in allowed positions). Diagnostic: `":::cls" is not allowed after the container in "in g"; write it directly after the declared id or its label, for example: business-role a:::cls "Label" in g`.
  - **36 Mermaid compatibility:** `subgraph id[label] … end` and `<type> id["Label"]`. Shapes, `-->`, unquoted bracket labels and subgraphs without an id are rejected with specific diagnostics. Subgraphs without an id are deferred.
- **Remaining differences (E:5323–5334):**
  - `---` is accepted only for notes.
  - `subgraph` is accepted with an id.
  - Bracket labels are accepted only in quoted form after a type.
  - Shapes and flowchart arrows are rejected with specific diagnostics.
  - Bare-title subgraphs are deferred.
- **Counts (E:10, E:108):** generate them from the catalogue's `Expected` lines.

## 11. Acceptance tests

Outcome codes:

| Code | Meaning |
| --- | --- |
| H | Valid and honoured |
| D | Accepted, hint dropped and warned |
| X | Invalid syntax or semantic error (fails) |
| U | Unsolvable: the later axis is dropped and reported |
| M | Succeeds with displacement |
| R | Recalculated after a peer grows |
| S | Scoring checked |

| # | Case | Outcome |
| --- | --- | --- |
| 1 | One horizontal hint | H |
| 2 | One vertical hint | H |
| 3 | `right-of a below b` | H; the start is (b's last row + 1, a's last column + 1) |
| 4 | `below b right-of a` (reversed) | H, same as 3 |
| 5 | `in g` before the hints | H |
| 6 | `in g` after the hints | H |
| 7 | `right-of a left-of b` | D on `left-of b` |
| 8 | `below a above b` | D on `above b` |
| 9 | `right-of a push E` | D on `push E` |
| 10 | Missing horizontal peer, valid vertical hint | D horizontal, H vertical |
| 11 | `right-of a +abc` | X: `offset "+abc" must be a whole number` |
| 12 | `above` with no peer | X: `"above" needs a peer id` |
| 13 | `in g in h` | X: duplicate `in` clause |
| 14 | Horizontal cycle | D on the later hint |
| 15 | `A right-of B` with `B below A` | H for both (per-axis cycle check) |
| 16 | Multi-cell peer (2×3) | H, measured from the far edge |
| 17 | Multi-cell hinted node | H, whole region free; no overlap |
| 18 | Example 6.1: a grows to 3 columns | R, c at (2,3), never (2,1) |
| 19 | Stale-coordinate check: a peer is displaced after the first pass | R, hinted node follows |
| 20 | Example 6.3 chain displacement | M, all hints honoured |
| 21 | Displacement that would take a neighbour out of its group | M, an alternative is used; the hinted node never moves |
| 22 | Example 6.4 | U, `below b` dropped with `conflicting constraints`, c at (0,1) |
| 23 | Peer in another group | D, both groups named |
| 24 | One axis breaks a band rule | D on that axis, H on the other |
| 25 | Hinted node in a group frame | H, containment (7.8) kept |
| 26 | `[leave N, enter Q]` | D on `enter Q`, `leave N` honoured |
| 27 | `[leave N, leave S]` | D on `leave S` |
| 28 | `enter W` that forces a detour | H, with a detour diagnostic |
| 29 | `:::hot` attached to a hint peer | X, message names the position; the peer gets no class |
| 29a | `business-role a "A" in g:::cls` (accepted by the current parser) | X with the `:::` position diagnostic (section 10); g gets no class |
| 30 | Mixed-case keywords (`Group`, `NOTE`, `IN`, `Right-Of`, `SUBGRAPH`, `End`) | H |
| 31 | Ids `below`, `push`, `end` and `note` | Accepted where they are unambiguous |
| 32 | `subgraph roles["R"] … end` and `group roles "R" { }` | Identical parse results |
| 33 | `subgraph roles ["R"]`, `subgraph roles "R"`, `subgraph roles` | Accepted |
| 34 | `subgraph Registered Roles`, `subgraph roles[R]` | X, specific messages |
| 35 | Nested subgraph with `direction` | Parent and direction set |
| 36 | Stray `end`, `}` closing a subgraph, unclosed block | X |
| 37 | `business-role a["X"]` | Same as `business-role a "X"` |
| 38 | `business-role a[X]`, `a((X))`, `a{X}`, `a[(X)]` | X, specific messages |
| 39 | `a --> b`, `a ==> b`, `a --- b` with no note | X, each suggests the Enterprise form |
| 40 | Relationship forbidden by `enterprise-rules.json` | X, unchanged |
| 41 | Note line hinted into a group frame | H (rule 4.9 frame end) or D |
| 42 | `w left-of p` with w 1×3, p at (3,5) (example 6.2) | H: w starts at (3,2); with `+1`, at (3,1) |
| 43 | `q above p` with q 2×1 | H: q starts at (1,5) |
| 44 | w (left-of) grows from 3 to 4 columns | R: StartColumn 1 |
| 45 | Scores: two honoured axes, one honoured axis, one dropped axis (real scoring path, section 5.2) | S: hint term −60, −30, −30 |
| 46 | Line hint `[leave N, enter S, travel E]` honoured on a relationship, and a valid honoured line hint on a plain note connection (`n1 --- a [leave E]`), real scoring path | S: −15 once for each connection; the note connection scores the same as the relationship (rule 4.10) |
| 47 | Line hint with one part dropped and the rest honoured (real scoring path) | S: −15 once, warning on the dropped part |
| 48 | `push E` beyond the siblings' envelope | H. Starts in the first column after the envelope; the group or diagram may widen (enlargement allowed) |
| 48a | Example 6.6: a; b push E; c push E | H for both: b at column 1, c at column 2; the later push doesn't invalidate b |
| 49 | Example 6.6 re-evaluated, through the real refresh path (a second `Apply` and the final repair gate) | b stays at 1 and c at 2 on every pass (no drift) |
| 49a | a widens to 3 columns after the pushes | R: b at 3, c at 4, recalculated in source order and stable afterwards |
| 50 | `push E` with no siblings | D with the warning `no sibling reference region`; never skipped silently |
| 57 | A node whose hint was honoured is moved by a later repair so that the relationship no longer holds | Final validation marks that axis unhonoured and reports it; the real score has no −30 for it |
| 57a | The same node is moved, but the relationship still holds (for example the peer moved with it) | Still honoured; −30 kept |
| 51 | `n1 right-of a` (note hinted to an element), `a below n1` (element hinted to a note) | H for both |
| 52 | Note peer in another group | D, both groups named |
| 53 | Example 6.4 | U with `conflicting constraints` |
| 54 | Example 6.5 (bounded search exhausted) | U with `no legal candidate found within the permitted search`; never claims impossibility; same result on every run |
| 55 | Identical redeclaration, `x right-of a` twice | H, no warning |
| 56 | Changed redeclaration, `x right-of a` then `x below a` | Warning naming both lines; `below a` used |

**Generated tests.** These generate peer spans from 1×1 to 3×3, hinted-node spans and offsets from +0 to +3, followed by random growth. For **honoured axes only**, they assert the decision 11 formulas: right-of and below start at peer start + peer span + n, and left-of and above at peer start − node span − n. In every case they assert that nothing overlaps and that no honoured node is off either of its relationships. Dropped axes must each carry a warning.

## 12. Workstreams and sequencing

There is no transitional grammar: the same source must never change meaning between releases. Each workstream begins only after its grammar text and rule wording are approved.

**A: documentation and diagnostics corrections (the grammar and meaning of today's syntax are unchanged).**

- **Documentation:** update the `enterprise.md` baseline so it documents what the parser already accepts: hints (one per line, as they are today), notes, `---`, `{ }` blocks with `end`, and local `direction`. Also fix "Remaining differences" for `---`, generate the counts, add a direction cheat sheet, document the contextual keywords and the hint replacement on re-declaration, and fix the 2.23 cross-reference.
- **Fixes:**
  - Make all keywords case-insensitive (the documentation already promises this).
  - Stop `:::` styling a hint peer: recognise the statement structure before rewriting `:::`.
  - Report an error for an unclosed block.
- **Redeclaration warning** only for a material change (decision 16; tests 55 and 56).
- **Peer wording:** 2.21(c) names notes (decision 14).
- **Specific diagnostics** for errors that already exist: a note without a label, `-->`, `---` without a note, a malformed offset, a hint with no peer. A *second hint stays an error, with the message "only one placement hint per declaration"*. A does not change that behaviour; B redefines it.
- **Error-policy rule (2.24):** today's behaviour already matches it (P:605–627 and P:639–709 warn; other faults are errors), so A only writes it down.

**B: the placement-hint extension.**

- The trailing-clause parser.
- Two axes, clauses in either order, and duplicate-clause diagnostics.
- Relational recalculation, occupancy, chain displacement and dropping the later axis.
- Per-axis cycles.
- Partial recovery of line hints.
- The detour diagnostic.
- Tests 1–29, 41–54 and the generated tests.
- **Push (decision 13):** keep the engine's sibling-envelope meaning (section 5.1). Replace only the silent skip with the warning `no sibling reference region`. Tests 48–50.
- **Scoring prerequisite (decision 12):** make the section 5.2 changes before or alongside the two-axis hints, and prove them with tests 45–47 through `FlowchartLayoutScore`.
- **Repair-exhaustion diagnostics (decision 15):** tests 53 and 54.

**C: Mermaid compatibility.** `subgraph … end`, `<type> id["Label"]`, and the shape, unquoted-label and missing-id diagnostics. This covers tests 30–40. C is independent of B. It reuses B's clause parser for the trailing clauses on `subgraph` lines, so if C goes first it should build that parser once, in a form that B then extends.

## 13. Remaining contradictions after revision 7 (checked on the laptop at 7:40–8:10 PM WST)

The relative-hint position formulas (decision 11) don't appear here. The engine already uses them (C:88–99), and it recomputes them from the current regions (C:57, C:83).

**Parser**

1. **Single hint.** Only one placement hint is allowed, and a second is a syntax error (P:41, P:60, P:265). This is resolved by B.
2. **Cross-axis cycles.** One peer map covers both axes, so `A right-of B` with `B below A` is rejected (P:660–690). Resolved by B.
3. **Line hints.** One bad or repeated part switches off the whole line hint (P:605–616). Resolved by B.
4. **Keyword case.** `group`, `note`, `in` and the hint words are case-sensitive (P:45, P:57, P:61). Resolved by A.
5. **`:::`.** It is rewritten before the statement is recognised, so it can style a hint peer (P:199–205). Resolved by A.
6. **Unclosed blocks.** An unclosed `{` block gives no error (P:268–281). Resolved by A.
7. **Generic diagnostics.** `-->`, bracket labels and shapes give the generic `cannot parse` (P:265), and a note without a label gives `unknown element type "note"` (P:336). Resolved by A and C.
8. **Redeclaration.** It replaces the hint silently, whether or not the hint changed (P:358). Decision 16 needs a warning for a material change only. Resolved by A.

**Layout engine**

9. **Push.**
   - The push reference envelope (C:58, C:63; F:226–227) and the push honour check (C:165–176) use **all** siblings, not the source-ordered reference set.
   - In grid mode, a second push in the same direction is rejected (C:124–129). In pixel mode, re-runs drift (`DiagramGridLayoutStages.cs:153`, 322).
   - A push with no siblings is skipped silently (C:64, F:228).
   - Resolved by B (decision 13).
10. **Rejections.** Rejected hints get a single message, `placement conflicts with an earlier explicit hint` (C:128), for the whole hint rather than per axis. It doesn't distinguish an established conflict from an exhausted search (decision 15). Resolved by B.

**rules.yaml**

11. **2.21 text.**
    - (a), (b) and (d) still describe a single hint, "keep moving" for every hint, and "two nodes hinted to the same cell are not a conflict".
    - (b) has no span formulas, and its push text ("extreme free cell … inside its own group") doesn't describe the sibling-envelope meaning that decision 13 keeps.
    - (g) scores −30 per node or group hint, not per axis. The parameter definition for `score_hint_node_group` (R:443) says the same.
    - **(c) names only "a node or group" as a peer, but notes are peers today.** The parser collects note hints and accepts notes as peers (P:654–658, P:667, P:698). Notes become layout nodes (M:62–64, M:186–197), so the engine treats them as peers too. Rule 2.22 (R:1124) already assumes that notes can carry hints ("unless an honoured hint moves it").
    - All of this is resolved by the section 9 wording, once Dave approves it.
12. **2.21(g) cross-reference.** Rule 2.23 cites 2.21(g) for reporting (R:1171), and so does the engine's message (C:121, C:134). The (g) subpart is about scoring. This is resolved by the 2.23 cross-reference wording, and the engine message should follow.
13. **No error-policy rule.** Proposed as 2.24.

**enterprise.md**

14. **Counts.** They say 130/110/20 (E:10, E:108), against 179 scenarios.
15. **Baseline.** It omits hints, notes, `---` and group blocks (E:30–47).
16. **Remaining differences.** It contradicts `---` (E:5325), and it says `subgraph` isn't accepted (E:5327), which C changes.

**Scoring engine**

17. **Placement hints score per hint, not per axis.** SC:672–678 counts −30 for each active, honoured node or group hint, with no axis concept. Resolved by B (section 5.2, change 1).
18. **Partial line hints.** One invalid part makes the whole connection score 0 (SC:661 with P:605–616), which contradicts decision 4 once partial recovery exists. Resolved by B (section 5.2, change 2).
19. **Status flags, not geometry.** Placement honour is read from the status flags (SC:672–675), not re-checked against the final regions, which contradicts decision 18. Resolved by B (section 5.3, test 57).

**rules.yaml (2.13 and terms)**

20. **Rule 2.13 hint term** (R:963) and the terms **Peer** (R:251–258) and **Honoured** (R:259–266) still describe one hint per node or group, peers that exclude notes, and per-hint honour. Resolved by the section 9 wording, once Dave approves it.

**Not verified**

21. Whether today's scoring path includes line hints on plain note connections. I found no use of plain connections in `MeasureHints`, which reads only `Contract.Edges` (SC:654), so they may be missing. Test 46 now requires them to score −15.
---

## 14. rules.yaml consistency (verified read-only on the laptop at 8:30 PM WST)

This section responds to an external audit of `docs\Instructions\rules.yaml`. Line numbers are from the current file, which is identical to the copy used above.

### 14.1 Verdicts

| # | Audit claim | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | 2.23 declares stage P5 but is missing from P5's list | **Confirmed** | 2.23 has `stage: "P5"` (R:1142–1147). P5's list is `["2.3","2.15","2.16","2.17","2.20","2.22","7.7"]` (R:539–542), and 2.23 isn't in any stage list. |
| 2 | 4.12 declares R4 but is missing from R4's list | **Confirmed** | 4.12 has `stage: "R4"` (R:1550–1555). R4's list (R:555–558) omits it, and so does every other stage list. These are the only two mismatches among the 80 rules. |
| 3 | 4.12 lacks `outranks` and `yields` | **Confirmed** | R:1550–1563. The other 79 rules all declare both fields. The schema doesn't require them (rules.schema.json:29), so this is a completeness gap, not a schema error. |
| 4 | `score_bend.used_by` omits 5.10 | **Confirmed** | `used_by: ["2.13","2.21"]` (R:433–437). 5.10 lists `score_bend` in its `params` and counts "each bend (2.13)" in its repair score (R:1692ff). |
| 5 | `score_route_length_per_grid_step.used_by` lists 4.8, 5.9 and 5.10, though they don't declare it | **Confirmed** | `used_by: ["2.13","4.8","5.9","5.10"]` (R:458–462). Only 2.13 declares it. 4.8 has no params, 5.9 has `score_crossing`, and 5.10 has `score_repair_length_per_grid_step`, a different parameter. |
| 6 | `legend_clearance.used_by` names `pre.legend.1` | **Partly right** | R:378–382. `pre.legend.1` is a valid entry id (R:2090), and the generator accepts entry ids in `used_by` (Build-Rulebook.ps1:90, 106). But the file's own header says "used_by = rule ids" (R:17). Either the header or the value is out of line; nothing breaks. |
| 7 | 2.23 is classed as quality although bands are binding | **Partly right** | `kind: "quality"` (R:1146). R:1171 says band order and membership "are conventions, not preferences". The kind list (R:24; schema enum) has no "convention" kind, and the binding treatment isn't section-10 legality. The engine already enforces it as a hard veto on hints (FlowchartPlacementHints.Cells.cs:118–122). So the metadata understates the rule, but the behaviour is right. |
| 8 | 5.7 against the proposed 2.24: is the order defined? | **Confirmed gap** | 5.7 begins "Every input produces a diagram and diagnostics" (R:1691) and ends "never silently drop an edge or the diagram". rules.yaml says nothing about syntax or semantic errors, the error placeholder, or which of the two applies first (there are no hits for "placeholder" or "syntax error"). Read literally, 5.7 conflicts with fail-on-error. |

### 14.2 Other checks

- **Counts:** 80 rules (77 active, 3 removed), 42 parameters, 25 terms, 17 stages (`precedence`) and 31 other entries. The audit's figures are correct.
- **Duplicate ids:** none, across rules, parameters, terms, stages, sections and entries.
- **Dangling references:** none in `refs`, `outranks`, `yields`, `params`, `pointer`, stage lists or `used_by`. All of them resolve.
- **Mismatches between `used_by` and `params`:** only the three in items 4–6.
- **Schema validation** (run on the box against a copy, with JSON Schema 2020-12): **1 error.** Rule 5.10 has `kind: "repair"` (R:1696), which isn't in the schema's kind enum (`legality`, `routing-fault`, `quality`, `structure/pointer`, `removed`), nor in the header's list (R:24). The audit didn't report this.
- **Generator:** `Build-Rulebook.ps1 -OutFile %TEMP%\rb-check\LAYOUT-RULEBOOK.rendered.md` printed "Rules 80 (active 77, removed 3), terms 25, parameters 42, stages 17, other entries 31; rulebook rule numbers 80" and "Validation: OK". Its output was **byte-identical** to the repo's `LAYOUT-RULEBOOK.md`. I didn't use `-JsonTwin`, because that would write `rules.json` next to the source file. No repo file was written.

### 14.3 How these fields are consumed

- **Build-Rulebook.ps1:**
  - It **renders** only sections, terms, rule text and entries, by `order` (Build-Rulebook.ps1:69–83). Stage lists, `kind`, `outranks`, `yields` and `used_by` don't appear in the rulebook.
  - It **validates** that:
    - `refs`, `outranks` and `yields` resolve (line 100);
    - `params` exist (101);
    - each rule's `stage` exists (102);
    - `used_by` and stage-list members resolve (106–107);
    - ids and orders are unique (88–89, 108–109).
  - It does **not** check that a rule appears in its declared stage's list, that `used_by` matches `params`, or that `kind` is in the enum. That is why items 1–5 and the 5.10 kind pass validation.
- **rules.schema.json** only checks types and required fields. `outranks` and `yields` are optional (lines 28–41), and `kind` is an enum, which is the one error found.
- **The engine** embeds `rules.yaml` (MermaidRecognition.Layout.csproj:72–74). `FlowchartLayoutRules.LoadParameters` reads only `- id:` / `value:` pairs (FlowchartLayoutRules.cs:68–101). Stage lists, `used_by`, `outranks`, `yields`, `kind` and `stage` have **no effect on execution**. They are documentation metadata, as the file header says (R:28–30: "metadata only … review before relying on them").
- **Tests:** I found no test that loads `rules.yaml` or cross-checks stage membership against each rule's declared stage. A search of `tests\` for `rules.yaml`, `precedence` and `Build-Rulebook` found nothing outside build output.

### 14.4 Recommended consistency fixes (a proposal for Dave; nothing applied)

1. Add 2.23 to P5's `rules` list, and 4.12 to R4's.
2. Give 4.12 `outranks` and `yields`. If it has none, use `[]`. Then make both fields required in the schema.
3. Fix `used_by`:
   - `score_bend` → `["2.13","2.21","5.10"]`;
   - `score_route_length_per_grid_step` → `["2.13"]`, or add it to the `params` of 4.8, 5.9 and 5.10 if they really use it;
   - confirm that `score_repair_length_per_grid_step` lists 5.10.
4. Either change the header (R:17) to "used_by = rule or entry ids", or move the legend clearance onto a rule.
5. Resolve 5.10's `kind: "repair"`: add `repair` to the schema enum and the header (R:24), or reclassify 5.10.
6. 2.23: either add a `convention` kind (to the schema, header and generator), or keep `quality` and note in the text that the band conventions are binding. Rewording is for Dave.
7. Add a sentence to 5.7, or to the proposed 2.24, that sets the order: "Rule 5.7 applies to input that has been read and validated without errors. A syntax or semantic error under 2.24 fails the diagram before layout." This is wording for Dave.
8. Extend `Build-Rulebook.ps1` validation, or add a test, to check four things:
   - each rule appears in exactly its declared stage's list;
   - `used_by` equals the set of rules that declare the parameter (with an explicit allowance for entries);
   - `kind` is in the enum;
   - `outranks` and `yields` are present.
   This catches items 1–5 automatically.

---

## 15. Deferred changes tracker

This file was copied from revision 7 on 20 Oct 2026 at 8:05 PM WST. **Rule: `enterprise.md` must not promise syntax before the parser accepts it.** Each `enterprise.md` change lands only together with, or after, the workstream named in its tag.

### 15.1 `enterprise.md` text changes (section 10)

- [ ] **[A]** Baseline grammar: document the forms the parser accepts today, which are one placement hint, line hints, `note`, `---` with a note, `group … {` closed by `}` or `end`, and local `direction`.
- [ ] **[B]** Baseline grammar: two-axis hints and `in` and the hints in either order (the section 4.1 `<clauses>` line).
- [ ] **[C]** Baseline grammar: `subgraph <id> … end`, and `<type> id["Label"]`.
- [ ] **[A]** Identifier and keyword bullet (E:57): keywords are case-insensitive in every position. This lands with the A parser fix.
- [ ] **[A]** Decision 31, placement hints: document the current single-hint behaviour.
- [ ] **[B]** Decision 31: update it to per-axis hints, recalculation, repair exhaustion and source-ordered push.
- [ ] **[B]** Decision 32, line hints: partial recovery.
- [ ] **[A]** Decision 33, notes: `note id "Text"` with a required label and a specific diagnostic.
- [ ] **[A]** Decision 34, plain connections: `---` is used only with a note.
- [ ] **[A]** Decision 35, group blocks: `{ }`, `end` closing a brace block, and `direction` inside.
- [ ] **[C]** Decision 36, Mermaid compatibility: `subgraph id[label] … end`, `<type> id["Label"]`, and the rejections of shapes, unquoted bracket labels and subgraphs without an id. Subgraphs without an id are deferred.
- [ ] **[A]** Remaining differences (E:5325): `---` is accepted only with notes.
- [ ] **[A]** Remaining differences: flowchart arrows are rejected with the specific `-->` diagnostic.
- [ ] **[C]** Remaining differences (E:5327): `subgraph` is accepted with an id. Bracket labels are accepted only quoted after a type. Shapes are rejected with specific diagnostics. Bare-title subgraphs are deferred.
- [ ] **[A]** Baseline grammar: state that `;` as a statement separator and markdown strings that continue over several lines are kept unchanged (section 4.1, E:52).
- [ ] **[A]** `:::` position: `in g:::cls`, accepted today, becomes an error with the specific diagnostic (section 10, test 29a). No corpus case uses it.
- [ ] **[A]** Counts (E:10, E:108): generate them from the catalogue's `Expected` lines.

### 15.2 `rules.yaml` items applied tonight (20 Oct 2026 WST)

Consistency fixes, applied at 7:50 PM WST (backup `Development\Merge\20261020-1950-rules-consistency`):

- [x] 2.23 added to stage P5, and 4.12 added to stage R4.
- [x] 4.12 given `outranks: []` and `yields: []`.
- [x] `score_bend.used_by` set to `["2.13","2.21","5.10"]`, and `score_route_length_per_grid_step.used_by` set to `["2.13"]`.
- [x] Header comment: `used_by` holds rule or entry ids.
- [x] 5.10 kind changed from `repair` to `quality`.
- [x] 2.23: sentence added saying that band membership and band order are binding.
- [x] 5.7: sentence added saying that the rule applies only to validated input.

Revision 7 wording, applied at 7:56 PM WST (backup `Development\Merge\20261020-1956-rules-enterprise-r7`):

- [x] 2.21 (a)–(d) replaced, (f) extended and the first sentence of (g) replaced.
- [x] 2.13 hint sentence reworded.
- [x] Definitions of `score_hint_node_group` and `score_hint_line` reworded. The values stay −30 and −15.
- [x] Terms Peer and Honoured reworded.
- [x] 2.23 cross-reference changed to 2.21(c), with only the breaking axis dropped.
- [x] New rule 2.24, Diagnostics and error policy, added (stage V1, legality), cross-referenced with 5.7.

### 15.3 Unresolved verification items

- [ ] **Plain note-connection scoring.** `MeasureHints` reads only `Contract.Edges` (SC:654). Confirm, or add, scoring for line hints on `---` connections, so that a fully honoured one scores −15 (test 46).
- [ ] **Final-axis geometry validation.** Honoured status must be re-checked for each axis against the final occupied regions inside the existing final validation, not read from the status flags (SC:672–675) (section 5.3; tests 57 and 57a).
- [ ] **Bounded legal-repair candidate enumeration.** Define and verify the permitted candidate set, so that the outcome is deterministic and a dropped axis can be reported correctly as "conflicting constraints" or as "no legal candidate found within the permitted search" (tests 53 and 54).
- [ ] **Source-ordered push reference sets.** Today the push envelope and its honour check use all siblings (C:58, 63, 165–176; F:226–227). **Tests 48a and 49 are mandatory.** Test 49 must run through the real refresh path (`DiagramGridLayoutStages.cs:153`, 322) and the final repair gate.

### 15.4 Rule 2.24 open point

- [ ] **Stage placement of 2.24.** `rules.yaml` has no parse or validation stage. 2.24 has been placed in V1 ("Legality"), next to 5.7, but it actually applies before layout, at the "Parse and validate hints" step of 9.6. Dave should decide whether to keep V1 or add a dedicated parse/validation stage to the precedence list. Adding 2.24 also moved the `order` of every later item up by one, which is metadata only.

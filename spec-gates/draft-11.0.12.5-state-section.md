#### 11.1.14.4 State diagram line-type catalogue (L2)

Dedicated recogniser: `src/MermaidRecognition/Kinds/State/StateKindRecognizer.cs`. Source: `mermaid/packages/mermaid/src/diagrams/state/parser/stateDiagram.jison` (shared by `stateDiagram` and `stateDiagram-v2` detectors).

**Render policy (Naiad replacement programme):** L2 classifies every source line into `SourceStatementContract.LineTypeId` only. Populating state entities (`NodeContract`, `EdgeContract`, composite regions, fork/join/choice pseudo-states, notes) in `DiagramContract` is **L3 (G4)** work in `MermaidRecognition`, then a **new render adapter** (or official Mermaid fallback per §11.E). **Do not** extend Naiad `StateParser` / `StateRenderer` for recognition gaps — Naiad is a temporary render baseline only (§12). Evidence scenarios: `extern/Naiad/src/test-renders/State.md` (Simple, MultipleStates, TransitionLabels, Description, ForkJoinState, ChoiceState, StateWithNote, StateDiagramV1, Complex).

Status values match §11.1.14.1: **parsed**, **partial**, **diagnostic**, **not-implemented**.

| Line type id | Example | Produces (contract) | Gate status | Fixture / notes |
| --- | --- | --- | --- | --- |
| `stateDiagram.header` | `stateDiagram` / `stateDiagram-v2` | `Kind`, `Header` | **parsed** | `simple`, `composite`, `note`; Naiad `StateDiagramV1` |
| `stateDiagram.init.leading` | `%%{init:...}%%` before header | `Configuration.RawDirectiveValues`, `EffectiveValues` | **parsed** | init strip in recogniser (`ApplyInit` / `StripLeadingInitBlocks`); no dedicated fixture yet |
| `stateDiagram.comment` | `%% note`, `# comment` | (none) | **parsed** | jison `#` and `%%` (not `%%{init}%%`) |
| `stateDiagram.transition` | `[*] --> Still`, `Still --> Moving`, `Active --> Inactive : timeout` | `SourceStatements[]` (`LineTypeId`, `RawLine`) | **parsed** | `simple`, `composite`, `note`; Naiad TransitionLabels, MultipleStates |
| `stateDiagram.state` | `state "Description" as s1`, `state fork_state <<fork>>`, `state join_state <<join>>`, `state choice_state <<choice>>`, `state First {` | `SourceStatements[]` | **parsed** | fork/join/choice via `ForkJoinRegex` on same id; Naiad Description, ForkJoinState, ChoiceState, Complex; composite opening brace on same line |
| `stateDiagram.structOpen` | `{` on its own line (nested body) | `SourceStatements[]` | **parsed** | jison `STRUCT_START`; rare vs inline `state Id {` |
| `stateDiagram.structClose` | `}` closing composite body | `SourceStatements[]` | **parsed** | `composite` |
| `stateDiagram.hideEmpty` | `hide empty description` | `SourceStatements[]` | **parsed** | jison `HIDE_EMPTY`; no fixture yet |
| `stateDiagram.note` | `note right of Active : text`, `note left of Error : handling` | `SourceStatements[]` | **parsed** | `note`; Naiad StateWithNote, Complex |
| `stateDiagram.classDef` | `classDef highlight fill:#f9f,stroke:#333` | `SourceStatements[]` | **parsed** | jison `classDefStatement`; no L2 fixture yet |
| `stateDiagram.style` | `style State2 fill:#f9f` | `SourceStatements[]` | **parsed** | — |
| `stateDiagram.cssClass` | `class State1 highlight` | `SourceStatements[]` | **parsed** | jison `cssClassStatement` (`applyClass`); distinct from `.classDef` |
| `stateDiagram.click` | `click State1 href "url" "tooltip"` | `SourceStatements[]` | **parsed** | callback forms → diagnostic at L3 render policy (mirror class) |
| `stateDiagram.direction` | `direction TB` / `LR` / `RL` / `BT` | `SourceStatements[]` (direction fact) | **parsed** | jison `direction_*` tokens |
| `stateDiagram.accTitle` | `accTitle: Title` | `Accessibility.Title` (when wired) | **parsed** | L3 G9 populates `AccessibilityContract` |
| `stateDiagram.accDescr` | `accDescr: text` / `accDescr { ... }` | `Accessibility.Description` (when wired) | **parsed** | L3 G9; inline and multiline lexer states |
| `stateDiagram.body` | (fallback) | `SourceStatements[]` + optional info diagnostic | **partial** | must not appear in official fixtures; `StateRecognitionFixtureTests` |

**Transition token coverage (G3 sub-rule):** `-->` only (jison `relation` / `idStatement '-->' idStatement`). Endpoints include implicit state ids, `[*]` (`EDGE_STATE`), and optional transition label after `:` (`DESCR` on relation). No dotted/open arrow variants (contrast class `classDiagram.relation`). Concurrent region divider `--` inside `struct` is **not** a separate L2 line type today (see gaps).

**State L2 exit:** table published (this section); tests in `StateRecognition/` (**3** fixtures: `simple`, `composite`, `note`); manifest `Gates/manifests/state.gate-manifest.json` aligned; no silent `stateDiagram.body` on fixture samples; every row **parsed**, **partial**, or **diagnostic** (no **not-implemented**).

**State L3 target (G4–G9, not started):** map classified lines to state entities in `DiagramContract` (states, transitions, composite `doc`, notes, styles); add fixtures per G4–G9 axis and Naiad `State.md` scenarios not yet in `StateRecognition/`; wire render outside Naiad.

| Gate | Applies to state? | L3 evidence (planned) |
| --- | --- | --- |
| G4 Entity model | Yes | `NodeContract` / `EdgeContract` (or state-specific DTOs) from statements; `[*]`, fork, join, choice types |
| G5 Containment | Yes | composite `state Id { ... }` membership (`struct` / `document` in jison) |
| G6 Styles | Yes | `classDef`, `style`, `cssClass`; inline `:::` class suffix on id (jison `STYLE_SEPARATOR`) |
| G7 Metadata | Partial | `hide empty description`; `scale` (jison); fork/join/choice; no `@{ }` in state grammar |
| G8 Interactions | Yes | `click` / `href`; callback diagnostic |
| G9 Accessibility | Yes | `accTitle`, `accDescr` fixtures |

**State L2 → L3 sequencing (no Naiad changes):**

```text
L2 (now)     StateKindRecognizer → SourceStatements + line-type ids
L3 (next)    StateContractMapper → DiagramContract entities + verified JSON
L3 render    MermaidRecognition.StateRenderAdapter OR §11.E fallback (not Naiad)
```

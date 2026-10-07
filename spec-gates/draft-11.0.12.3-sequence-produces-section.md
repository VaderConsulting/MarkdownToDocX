#### 11.1.14.2 Sequence line-type catalogue (L2)

Dedicated recogniser: `src/MermaidRecognition/Kinds/Sequence/SequenceKindRecognizer.cs`. Source: `mermaid/packages/mermaid/src/diagrams/sequence/parser/sequenceDiagram.jison` (detector: `sequenceDetector.ts`).

**Render policy (Naiad replacement programme):** L2 classifies every source line into `SourceStatementContract.LineTypeId` only. Populating sequence entities (participants, messages, activations, notes, control blocks, boxes) in `DiagramContract` is **L3 (G4)** work in `MermaidRecognition`, then a **new render adapter** (or official Mermaid fallback per §11.E). **Do not** extend Naiad sequence parsers / renderers for recognition gaps — Naiad is a temporary render baseline only (§12). Evidence scenarios: `extern/Naiad/src/test-renders/Sequence.md` (Simple, Participants, Actors, Activation, DifferentArrows, Notes, Title, AutoNumber, AltElseBlocks, Complex); regression catalogue [`sequence.md`](sequence.md) (S01–S32).

Status values match §11.1.14.1: **parsed**, **partial**, **diagnostic**, **not-implemented**.

| Line type id | Example | Produces (contract) | Gate status | Fixture / notes |
| --- | --- | --- | --- | --- |
| `sequence.header` | `sequenceDiagram` | `Kind`, `Header` | **parsed** | all fixtures; S01; Naiad Simple |
| `sequence.init.leading` | `%%{init: {'theme': 'base'}}%%` before header | `Configuration.RawDirectiveValues`, `EffectiveValues` | **parsed** | init strip in recogniser (`StripLeadingInitBlocks`); S24; no dedicated `SequenceRecognition/` fixture yet |
| `sequence.comment` | `%% This is a comment` | (none) | **parsed** | S22; not `%%{init}%%` |
| `sequence.participant` | `participant A as Alice`, `actor User` | `SourceStatements[]` (`LineTypeId`, `RawLine`) | **parsed** | `participants`; S04–S06; Naiad Participants, Actors |
| `sequence.message` | `A->>B: Hi`, `A-->>B: Reply`, `A->>+B: Start`, `A->>A: Self` | `SourceStatements[]` | **parsed** | `simple`, `activation`; S01–S03, S07–S09, S29–S31; Naiad Simple, DifferentArrows, Activation; inline `+`/`-` activation markers on arrow |
| `sequence.activate` | `activate A`, `deactivate B` | `SourceStatements[]` | **parsed** | `activate-deactivate`; S10 |
| `sequence.note` | `note right of A: text`, `Note over A,B: span` | `SourceStatements[]` | **parsed** | `notes`; S11; Naiad Notes |
| `sequence.title` | `title Authentication Flow` | `SourceStatements[]` (title fact; L3 → diagram title DTO) | **parsed** | `title`; S12; Naiad Title |
| `sequence.autonumber` | `autonumber` | `SourceStatements[]` (autonumber fact) | **parsed** | `autonumber`; S13; Naiad AutoNumber |
| `sequence.block` | `loop`, `alt`, `else`, `opt`, `par`, `and`, `critical`, `option`, `break`, `rect`, `par_over` | `SourceStatements[]` | **parsed** | `alt-else-blocks`, `loop-control`, `opt-block`, `par-block`, `critical-block`; S14–S18, S25–S27, S32; Naiad AltElseBlocks; `else`/`and`/`option` are branch keywords, not standalone blocks |
| `sequence.end` | `end` | `SourceStatements[]` | **parsed** | all block/box fixtures; closes `alt`/`loop`/`par`/`opt`/`critical`/`break`/`rect` and participant `box` |
| `sequence.box` | `box rgb(200,200,200) Internal` | `SourceStatements[]` | **parsed** | `box-rgb`; S19; closed by `sequence.end` |
| `sequence.lifecycle` | `create participant B`, `destroy B` | `SourceStatements[]` | **parsed** | `lifecycle-create`; S20 |
| `sequence.accTitle` | `accTitle: Auth` | `Accessibility.Title` (when wired) | **parsed** | `sequence-accessibility`; S21; L3 G9 populates `AccessibilityContract` |
| `sequence.accDescr` | `accDescr: text` / `accDescr { ... }` | `Accessibility.Description` (when wired) | **parsed** | `sequence-accessibility`; S21, S28; L3 G9; inline and multiline block forms |
| `sequence.body` | (fallback) | `SourceStatements[]` + optional info diagnostic | **partial** | must not appear in official fixtures or [`sequence.md`](sequence.md) active scenarios; `SequenceRecognitionFixtureTests` |

**Message arrow token coverage (G3 sub-rule):** recogniser `MessageRegex` matches any line containing an arrow token before `:`. Solid closed `->>`, dotted closed `-->>`, solid open `->`, dotted open `-->`, cross endings `-x` / `--x`, bidirectional `<<->>` / `<<-->>`, and additional jison-aligned tokens (`-x>>`, `x->`, `-)`, `--)`, `-\|`, `--\\`, `--\/`, `\/\/-`, `\\\\-`). Catalogue scenarios exercising primary variants:

| Arrow style | Example | Scenario | Naiad sample |
| --- | --- | --- | --- |
| Solid closed | `A->>B: msg` | S01, S07 | Simple |
| Dotted closed | `A-->>B: msg` | S01, S07 | Simple |
| Solid open | `A->B: msg` | S08 | DifferentArrows |
| Dotted open | `A-->B: msg` | S08 | DifferentArrows |
| Solid cross | `A-xB: msg` | S08 | DifferentArrows |
| Dotted cross | `A--xB: msg` | S08 | DifferentArrows |
| Inline activate | `A->>+B: Start`, `B-->>-A: Done` | S09 | Activation |
| Self-message | `A->>A: Reflect` | S29 | — |

Implicit participants (first appearance in a message, no prior `participant`/`actor` line) are a sub-axis of `sequence.message` and `sequence.participant` at L3 (S02, S03). Long and quoted message labels (S30, S31) remain `sequence.message`.

**Control-block keyword coverage:** `loop` (S15), `alt`/`else` (S14, S32), `opt` (S16), `par`/`and` (S17), `critical`/`option` (S18), `break` (S25), `rect rgb(...)` (S26), nested blocks (S27). `par_over` is recognised at L2 but has no catalogue scenario yet.

**Sequence L2 exit:** table published (this section); tests in `SequenceRecognition/` (**15** fixtures: `simple`, `participants`, `activation`, `activate-deactivate`, `notes`, `title`, `autonumber`, `alt-else-blocks`, `loop-control`, `opt-block`, `par-block`, `critical-block`, `box-rgb`, `lifecycle-create`, `sequence-accessibility`); no silent `sequence.body` on fixture samples; every row **parsed**, **partial**, or **diagnostic** (no **not-implemented**); [`sequence.md`](sequence.md) S01–S32 map to rows above.

**Sequence L3 target (G4–G9, not started):** map classified lines to sequence entities in `DiagramContract` (participants, messages, activations, notes, control frames, boxes, lifecycle); add fixtures per G4–G9 axis and Naiad `Sequence.md` scenarios not yet in `SequenceRecognition/`; wire render outside Naiad.

| Gate | Applies to sequence? | L3 evidence (planned) |
| --- | --- | --- |
| G4 Entity model | Yes | `NodeContract` per participant/actor; `EdgeContract` (or message DTO) per message; activation bars from inline `+`/`-` and explicit activate/deactivate |
| G5 Containment | Yes | `box` participant grouping; nested `alt`/`loop`/`par`/`opt`/`critical`/`break`/`rect` frames (`sequence.block` + `sequence.end`) |
| G6 Styles | Partial | `box rgb(...)`, `rect rgb(...)` background colours; no `classDef` / `style` in sequence grammar |
| G7 Metadata | Yes | `title`, `autonumber`, `create`/`destroy` lifecycle; implicit vs declared participants |
| G8 Interactions | No | sequence grammar has no `click` / href |
| G9 Accessibility | Yes | `accTitle`, `accDescr` fixtures (`sequence-accessibility`; S21, S28) |

**Sequence L2 → L3 sequencing (no Naiad changes):**

```text
L2 (now)     SequenceKindRecognizer → SourceStatements + line-type ids
L3 (next)    SequenceContractMapper → DiagramContract entities + verified JSON
L3 render    MermaidRecognition.SequenceRenderAdapter OR §11.E fallback (not Naiad)
```

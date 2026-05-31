#### 11.1.14.5 ER diagram line-type catalogue (L2)

Dedicated recogniser: `new_src/MermaidRecognition/Kinds/Er/ErKindRecognizer.cs`. Source: `mermaid/packages/mermaid/src/diagrams/er/parser/erDiagram.jison`.

**Render policy (Naiad replacement programme):** L2 classifies every source line into `SourceStatementContract.LineTypeId` only. Populating ER entities (`NodeContract` for entities and attributes, `EdgeContract` for relationships and cardinality) in `DiagramContract` is **L3 (G4)** work in `MermaidRecognition`, then a **new render adapter** (or official Mermaid fallback per §11.E). **Do not** extend Naiad `ErParser` / `ErRenderer` for recognition gaps — Naiad is a temporary render baseline only (§12). Naiad interim samples: `extern/Naiad/src/test-renders/EntityRelationship.md` (Simple, OneToOne, ZeroOrOne, NonIdentifying, MultipleRelationships, Attributes, KeyTypes, Comments, Complex).

Status values match §11.1.14.1: **parsed**, **partial**, **diagnostic**, **not-implemented**.

| Line type id | Example | Produces (contract) | Gate status | Fixture / notes |
| --- | --- | --- | --- | --- |
| `erDiagram.header` | `erDiagram` | `Kind`, `Header` | **parsed** | all fixtures; E01–E35; jison `ER_DIAGRAM` |
| `erDiagram.init.leading` | `%%{init: {'theme': 'base'}}%%` before header | `Configuration.RawDirectiveValues`, `EffectiveValues` | **parsed** | init strip in recogniser; E17; no dedicated `ErRecognition/` fixture yet |
| `erDiagram.comment` | `%% Domain: sales` | (none) | **parsed** | E15 |
| `erDiagram.accTitle` | `accTitle: Sales schema` | `Accessibility.Title` (when wired) | **parsed** | E16, E31; L3 G9 populates `AccessibilityContract` |
| `erDiagram.accDescr` | `accDescr: text` / `accDescr { ... }` | `Accessibility.Description` (when wired) | **parsed** | E16 (inline), E31 (block); L3 G9 |
| `erDiagram.direction` | `direction TB` / `LR` / `RL` / `BT` | `SourceStatements[]` (direction fact) | **parsed** | E11 |
| `erDiagram.classDef` | `classDef highlight fill:#f9f,stroke:#333` | `SourceStatements[]` | **parsed** | E13; jison `classDefStatement` |
| `erDiagram.class` | `class CUSTOMER highlight` | `SourceStatements[]` | **parsed** | E13 (with `classDef`), E33 (apply without prior `classDef`); jison `classStatement` |
| `erDiagram.style` | `style CUSTOMER fill:#bbf` | `SourceStatements[]` | **parsed** | E14; jison `styleStatement` |
| `erDiagram.relationship` | `CUSTOMER \|\|--o{ ORDER : places` | `SourceStatements[]` | **parsed** | E01, E02, E03, E05, E06, E18, E28, E29, E34, E35; fixture `relationship`; Naiad: Simple, NonIdentifying, MultipleRelationships, Complex |
| `erDiagram.entityBlockOpen` | `CUSTOMER {` | `SourceStatements[]` | **parsed** | E07, E08, E09, E12, E18, E19, E30, E32; fixture `entity-attributes`; Naiad: Attributes, KeyTypes, Comments |
| `erDiagram.entityBlockClose` | `}` closing entity block | `SourceStatements[]` | **parsed** | E32 (explicit multi-entity); fixture `entity-attributes` |
| `erDiagram.attribute` | `string name`, `int id PK`, `string email UK`, `int customer_id FK` | `SourceStatements[]` | **parsed** | E07, E08, E09, E12, E18, E19; fixture `entity-attributes`; PK / FK / UK per jison `ATTRIBUTE_KEY` |
| `erDiagram.entityAlias` | `CUSTOMER [Customer Account] {` | `SourceStatements[]` | **parsed** | E10 (**remarked** — §9.1; does not render on mermaid.live; recognition axis tracked separately); jison `entityName SQS entityName SQE BLOCK_START` |
| `erDiagram.body` | (fallback) | `SourceStatements[]` + optional info diagnostic | **partial** | must not appear in official fixtures or [`entityrelationship.md`](entityrelationship.md) active scenarios; `ErRecognitionFixtureTests` |

**Relationship cardinality token coverage (G3 sub-rule):** left marker + link type + right marker compose Mermaid ER cardinality (identifying `--` vs non-identifying `..`). Lexer tokens from `erDiagram.jison`: `ONLY_ONE` (`||`, `one`, `only one`, `1` before link), `ZERO_OR_ONE` (`|o`, `o|`, `one or zero`, `zero or one`), `ZERO_OR_MORE` (`}o`, `o{`, `zero or more`, `0+`, `many`), `ONE_OR_MORE` (`}|`, `|{`, `one or more`, `1+`, `many(1)`). Catalogue scenarios exercising each active combination:

| Left | Link | Right | Example scenario | Naiad sample |
| --- | --- | --- | --- | --- |
| `\|\|` | `--` | `o{` | E01 | Simple |
| `\|\|` | `--` | `\|\|` | E03 | OneToOne |
| `\|o` | `--` | `o\|` | E04 | ZeroOrOne |
| `\|\|` | `..` | `o{` | E05 | NonIdentifying |
| `\|\|` | `--` | `\|{` | E06, E21 | MultipleRelationships |
| `}o` | `--` | `\|\|` | E20 | — |
| `}o` | `--` | `o{` | E23 | — |
| `\|o` | `..` | `o{` | E22 | — |
| `}\|` | `..` | `o{` | E24 | — |
| `}o` | `..` | `o{` | E25 | — |
| `o\|` | `--` | `o{` | E26 | — |
| `}o` | `..` | `\|\|` | E27 | — |

Quoted relationship labels (`: "logs in"`) and hyphenated entity names (`LINE-ITEM`, `PARKING-SPACE`) are sub-axes of `erDiagram.relationship` and entity naming (E02, E34). Recursive and duplicate-role edges (E28, E29) remain `erDiagram.relationship`. Stand-alone entity blocks without relationships (E30) use `entityBlockOpen` / `attribute` / `entityBlockClose` only.

**Remarked catalogue axes (§9.1):** E10 (`erDiagram.entityAlias`) is **considered but rejected** for the render gate — every source line in the catalogue fence is prefixed with `%%`. SvgHarness and DOCX visual review skip remarked blocks. Recognition may still classify the axis via `ErKindRecognizer` and future fixtures; remarking is not a signal that L2 tracking was abandoned.

**ER L2 exit:** table published (this section); tests in `ErRecognition/` (**2** fixtures: `relationship`, `entity-attributes`); no silent `erDiagram.body` on fixture samples; every row **parsed**, **partial**, or **diagnostic** (no **not-implemented**); [`entityrelationship.md`](entityrelationship.md) E01–E35 map to rows above (E10 remarked).

**ER L3 target (G4–G9, not started):** map classified lines to ER entities in `DiagramContract` (entities, attributes, relationships, cardinality, styles); add fixtures per G4–G9 axis; wire render outside Naiad.

| Gate | Applies to ER? | L3 evidence (planned) |
| --- | --- | --- |
| G4 Entity model | Yes | `NodeContract` per entity + attribute facts; `EdgeContract` per relationship with cardinality + role label |
| G5 Containment | Partial | attribute membership inside entity blocks; no nested groups in ER grammar |
| G6 Styles | Yes | `classDef`, `class`, `style`; jison `STYLE_SEPARATOR` / `:::` on entity names |
| G7 Metadata | Partial | quoted attribute comments (`"Primary key"`); no `@{ }` in ER grammar |
| G8 Interactions | No | ER grammar has no `click` / href |
| G9 Accessibility | Yes | `accTitle`, `accDescr` fixtures (E16, E31) |

**ER L2 → L3 sequencing (no Naiad changes):**

```text
L2 (now)     ErKindRecognizer → SourceStatements + line-type ids
L3 (next)    ErContractMapper → DiagramContract entities + verified JSON
L3 render    MermaidRecognition.ErRenderAdapter OR §11.E fallback (not Naiad)
```

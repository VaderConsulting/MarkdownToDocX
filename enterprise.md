# Enterprise regression catalogue

Progressive Mermaid **enterprise** diagram scenarios for Markdown → DOCX conversion. The `enterprise` diagram kind draws enterprise architecture models in the native Enterprise notation. Each level increases complexity; scenarios within a level are ordered from simplest to hardest.


## How to use

This file (`enterprise.md`) is the **complete** enterprise regression catalogue: **179 scenarios** (`A01`–`A179`), mostly one recognition axis per scenario. Levels 1–9 introduce the header, directives and every element type, one layer per level. Level 10 covers every relationship type and qualifier, Levels 11–14 cover labels, nesting, cross-layer and derived relationships, styling and notation. **Level 15** holds scenarios that are **expected to fail validation**. Level 16 covers recognition edge cases, Level 17 layout stress and Level 18 large realistic viewpoints. Level 19 covers the general Mermaid conventions that the enterprise kind accepts, such as frontmatter, directives, accessibility statements and the `:::` shorthand.

Of the 130 scenarios, **110 active valid** diagrams must validate with no errors and no warnings, and **20 expected-diagnostic** diagrams must produce exactly the errors or warnings named on their `Expected` line: 16 in Level 15, the nesting warning in `A65`, the quoted identifiers in `A102`, and two in Level 19 (`A129` and `A130`). There are **0 remarked** diagrams: every syntax question has been decided (see [Syntax decisions](#syntax-decisions)). The guiding rule is that anything that is normal Mermaid syntax is accepted.

The scenario prefix is **`A`**. It is not used by any other catalogue or gate in `spec.md` (the existing catalogues use `F`, `S`, `C`, `E` and `ST`, and the programme gates use `G`).

### Scenario format

Every scenario has a heading with its id, a short description of what it tests, the axes it exercises, the expected result and exactly one fenced `mermaid` block.

| Field | Meaning |
| --- | --- |
| **Recognition** | Parser and validator axes: header and directives, element types, relationship types and qualifiers, nesting, comments, identifiers and diagnostics. |
| **Rendering** | Drawing and layout axes: box outlines, corner icons, line styles and end marks, containers, layer ranking, routing and label placement. |
| **Expected** | `valid` (no errors, no warnings), `N errors (lines …)` or `N warning (line …)`, or `remarked`. |

Line numbers are relative to the diagram block: line 1 is the first line inside the fence, and comment and blank lines are counted. The axis names are proposed names for the enterprise kind. Following `spec.md` §9.1, each axis needs a line-type row in a new §11.1.14 section for this kind (and, where it is recognition-critical, a fixture under an `EnterpriseRecognition/` folder) before the catalogue becomes a gate.

### Syntax baseline

All active diagrams use the `enterprise` grammar of the design study (`docs/enterprise/DESIGN-STUDY.md` §5) as implemented by the reference parser on the `enterprise-diagram` branch, with the changes recorded under [Syntax decisions](#syntax-decisions): the `enterprise` keyword, Enterprise **3.2** rules by default and **3.1** with `version 3.1`. **Anything that is normal Mermaid syntax is accepted.** Only the Enterprise-specific parts (element types, relationship forms, qualifiers and version rules) have their own grammar.

```text
[--- YAML frontmatter (title, config) ---]       (only at the very start of the block)
[%%{init: …}%%]                                   (anywhere; may span lines)
enterprise | enterprise-beta [TB|TD|BT|LR|RL]     (any letter case)
  title <text>                                    (rest of the line, up to a %% comment)
  accTitle: <text>
  accDescr: <text>  |  accDescr { <lines> }
  version 3.1 | 3.2                               (default 3.2)
  direction TB | TD | BT | LR | RL                (default TB)
  group <id>[:::class] ["Label"][:::class] [in <container-id>]
  <element-type> <id>[:::class] ["Label"][:::class] [in <container-id>]
  <src>[:::class] -<relationship>[(qualifier)]-> <tgt>[:::class] ["label"]
  classDef <class>[,<class>…] <property>:<value>[,<property>:<value>…]
  class <id>[,<id>…] <class>
  style <id> <property>:<value>[,<property>:<value>…]
  linkStyle <n>[,<n>…] | default [interpolate <curve>] [<property>:<value>[,…]]
  %% comment                                      (whole line or after a statement)
```

- **Statements** end at the end of the line or at a `;`, so several statements may share a line. `title`, `accTitle` and `accDescr` take the rest of the line, including any `;`.
- **Comments:** `%%` starts a comment that runs to the end of the line, on its own line or after a statement, except inside a quoted label, where `%%` is text. `%%{ … }%%` is a Mermaid directive, not a comment; it is read as configuration and does not affect validation.
- **Relationships** by name or letter, in any letter case: `composition|c`, `aggregation|g`, `assignment|i`, `realization|r`, `serving|v`, `access|a`, `influence|n`, `triggering|t`, `flow|f`, `specialization|s`, `association|o`.
- **Qualifiers:** `access(read|write|readwrite|access)` (default `write`), `influence(+|-|++|--|<signed whole number>)`, `association(directed)`. Any other qualifier is an error.
- **Element types:** the 61 kebab-case names of the reference implementation (Application, Technology, Physical, Business, Strategy, Motivation, Implementation and Migration, `grouping`, `location` and junctions). Hyphen-free spellings and the specification's CamelCase names are accepted, in any letter case. Junctions are written `and-junction` or `or-junction`; a bare `junction` is an and-junction.
- **Labels** use double or single quotes. `""` means no text. `<br>`, `<br/>`, `<br />` or `\n` breaks a line. `\"`, `\'` and `\\` are escapes, Mermaid entity codes such as `#quot;`, `#amp;` or `#35;` are decoded, and any other backslash stays as typed. A markdown string (`` "`…`" ``) supports bold and italic and may continue over several lines.
- **Identifiers** may use any letters, digits, `_`, `.` and `-`, must not start with `.` or `-`, and are case-sensitive. These words are reserved and cannot be ids (in any letter case): `title`, `version`, `direction`, `group`, `in`, `classDef`, `class`, `style`, `linkStyle`, `accTitle`, `accDescr`.
- **Order and repetition:** statements may appear in any order and references are resolved after the whole block is read. A repeated directive takes its last value. Declaring an id again with the same element type updates its label and container; with a different type it is an error.
- **Styling** follows Mermaid flowcharts. Relationships are numbered from 0 in the order their lines appear, for `linkStyle`. An undefined class is ignored; `class` or `style` naming an undeclared element is ignored with a warning; a `linkStyle` number with no relationship is an error. Styling never changes an outline shape, an icon or an end mark.
- **Rules:** every relationship is checked against `docs/enterprise/enterprise-rules.json` for the selected version. The 3.1 Application and Technology cells were checked against the specification. All other cells, including every 3.2 change, are provisional.

### Remarked scenarios (`%%`)

A diagram whose syntax is not yet defined, or that is otherwise **excluded from the render gate**, stays in this catalogue with **every source line prefixed by `%%`** inside the fenced block, and its `Expected` line reads `remarked`.

| Intent | Meaning |
| --- | --- |
| **Considered but rejected** | The axis was evaluated; **do not re-add** the same scenario because the file looks incomplete. |
| **Syntax pending** | Add the candidate syntax to [Syntax decisions](#syntax-decisions) as an open row. Un-remark the scenario (and update the counts above) once it is decided. |
| **Excluded from render gates** | DOCX visual review and the validation gate apply only to **active** (non-remarked) diagrams. |
| **Recognition may still apply** | Recognition fixtures may cover the axis separately once the syntax exists. |

**Do not confuse with:**

- A `%%` comment inside an otherwise **active** diagram, on its own line or after a statement (valid comment syntax, exercised in `A94`, `A95` and `A96`).

**Currently remarked:** none. The eight scenarios that were remarked while their syntax was open (`A56`, `A57`, `A75`, `A76`, `A77`, `A101`, `A102` and `A103`) are now active. All are valid diagrams except `A102`, which tests quoted identifiers and expects errors.

Remarked fences contain **no active source** (every non-blank line is `%%`). They are skipped, not failed.

### Expected-diagnostic scenarios

These diagrams are **meant to be rejected** (or, for `A65`, to render with a warning). A gate passes when the validator reports exactly the listed diagnostics, on the listed lines, and the DOCX shows the repository's normal error placeholder instead of silently dropping the block.

| Scenario | Check | Expected |
| --- | --- | --- |
| **A65** | Nesting without an explicit relationship | 1 warning |
| **A78** | Data object serving a component | 1 error |
| **A79** | Composition, assignment and realisation in the wrong direction | 4 errors |
| **A80** | Access to active structure | 2 errors |
| **A81** | Influence between core elements | 2 errors |
| **A82** | Motivation and passive elements triggering or assigned | 3 errors |
| **A83** | Derivation onto a passive element | 2 errors |
| **A84** | 3.2 change: Device specialising Node (provisional) | 1 error |
| **A86** | 3.2 change: Artifact realising Node or Device (provisional) | 2 errors |
| **A87** | 3.1 rule: Artifact realising Artifact | 1 error |
| **A88** | 3.2 changes across Technology, Physical and Business | 4 errors |
| **A89** | Unknown element type and relationship type | 2 errors |
| **A90** | Id redeclared with another type, undeclared source and target, reserved word as id | 4 errors |
| **A91** | Invalid qualifiers | 4 errors |
| **A92** | `direction` and `version` values outside the defined set | 3 errors |
| **A93** | `in` names an unknown container | 1 error |
| **A94** | Block-relative line numbers with comments and blank lines | 3 errors |
| **A102** | Quoted identifiers | 3 errors |
| **A129** | `class` and `style` naming undeclared elements | 2 warnings |
| **A130** | `linkStyle` index out of range | 1 error |

`A85` sits in Level 15 as the valid 3.1 counterpart of `A84`.

**Overall pass** means the 110 active valid diagrams produce no errors and no warnings, and each of the 20 expected-diagnostic diagrams produces exactly the diagnostics on its `Expected` line.

### Validation and DOCX visual review

There is no mermaid.ink or Mermaid Live reference for `enterprise` blocks, so the reference comparison used by the flowchart catalogue does not apply. The gates are the native validator and a visual review of the DOCX output against the notation tables in the design study (§6–§7).

```powershell
dotnet run --project src/md2docx/md2docx.csproj -- enterprise.md enterprise.docx
```

A structural self-check of this file (`check_enterprise_catalogue.py`) extracts every block and confirms that the header line is present, quotes and brackets balance, every statement parses with the decided grammar, every relationship, `class` and `style` statement references a declared element, every relationship is allowed by the rules table for the selected version (or is listed as an expected error), each block produces exactly the diagnostics on its `Expected` line, scenario ids are unique with no gaps, and the counts in this section match the headings.

**Checklist per scenario**

- Elements and relationships present (nothing missing or duplicated); relationships hidden by nesting are not drawn
- Box outline, corner icon and layer colour match the element type
- Line style and both end marks match the relationship type and qualifier, and are drawn as plain paths (no SVG `<marker>`)
- Labels readable (wrapping, explicit `<br>` breaks, entity codes decoded, punctuation, Unicode, relationship labels clear of end marks)
- Styling from `classDef`, `class`, `:::`, `style` and `linkStyle` applied without changing outline shapes, icons or end marks
- Frontmatter and `accTitle` titles shown or embedded as described; `accDescr` used as the image's alternative text
- Containers enclose their members without overlapping borders or titles
- Layer ranking in TB layout (as ranked by the reference implementation): Motivation and Strategy at the top, then Business, Application and Technology, with Implementation and Migration at the bottom
- Level 15: an error placeholder or message, never a silent drop

---

## Level 1 — Header, version and direction

### A01 — Empty diagram (header only)

The smallest legal block: the `enterprise` header with no statements. The recogniser must accept it without errors, and the renderer draws a small blank canvas rather than an error placeholder (see Syntax decisions).

- **Recognition:** `Header.Enterprise`, `Diagram.Empty`
- **Rendering:** `Render.Canvas.Empty`
- **Expected:** valid

```mermaid
enterprise
```

### A02 — Single element without a label

One element whose id doubles as its label, as allowed by `<element-type> <id> ["Label"]`.

- **Recognition:** `Header.Enterprise`, `Element.Declaration`, `Element.Label.FromId`
- **Rendering:** `Render.Element.Box`, `Render.Icon.Corner`
- **Expected:** valid

```mermaid
enterprise
  application-component webShop
```

### A03 — Single element with a quoted label

One labelled element and nothing else, so box sizing, icon placement and padding can be checked in isolation.

- **Recognition:** `Header.Enterprise`, `Element.Declaration`, `Element.Label.Quoted`
- **Rendering:** `Render.Element.Box`, `Render.Icon.Corner`, `Render.Label.Centre`
- **Expected:** valid

```mermaid
enterprise
  application-component webShop "Web Shop"
```

### A04 — Title directive

The `title` statement takes the rest of the line as plain text.

- **Recognition:** `Header.Enterprise`, `Directive.Title`
- **Rendering:** `Render.Title`
- **Expected:** valid

```mermaid
enterprise
  title Web shop - single component
  application-component webShop "Web Shop"
```

### A05 — Explicit version 3.2 (the default)

Declares the default rule set explicitly. The result must be identical to the same diagram without a `version` line.

- **Recognition:** `Header.Enterprise`, `Directive.Version.3_2`
- **Rendering:** `Render.Element.Box`, `Render.Relationship.Serving`
- **Expected:** valid

```mermaid
enterprise
  version 3.2
  application-service ordering "Online Ordering"
  application-component webShop "Web Shop"
  ordering -serving-> webShop
```

### A06 — Explicit version 3.1

Selects the version 3.1 relationship rule set. The diagram is valid under both versions, so only the selected rule table changes.

- **Recognition:** `Header.Enterprise`, `Directive.Version.3_1`
- **Rendering:** `Render.Element.Box`, `Render.Relationship.Serving`
- **Expected:** valid

```mermaid
enterprise
  version 3.1
  application-service ordering "Online Ordering"
  application-component webShop "Web Shop"
  ordering -serving-> webShop
```

### A07 — Direction TB (explicit)

Top-to-bottom is the default. With the layout-only edge reversal, the served component sits above the service that serves it.

- **Recognition:** `Header.Enterprise`, `Direction.TB`
- **Rendering:** `Layout.Direction.TB`, `Layout.EdgeReversal.Serving`
- **Expected:** valid

```mermaid
enterprise
  direction TB
  application-component webShop "Web Shop"
  application-service ordering "Online Ordering"
  technology-service hosting "Hosting"
  ordering -serving-> webShop
  hosting -serving-> webShop
```

### A08 — Direction LR

The same content as the previous scenario laid out left to right.

- **Recognition:** `Header.Enterprise`, `Direction.LR`
- **Rendering:** `Layout.Direction.LR`, `Layout.EdgeReversal.Serving`
- **Expected:** valid

```mermaid
enterprise
  direction LR
  application-component webShop "Web Shop"
  application-service ordering "Online Ordering"
  technology-service hosting "Hosting"
  ordering -serving-> webShop
  hosting -serving-> webShop
```

### A09 — Directives in an unusual order and after elements

`direction`, `version` and `title` appear in reverse order, the `title` comes after an element and `direction` is given twice. Directives are statements, not a fixed preamble, so the result must match the conventional order. When a directive is repeated, the last one wins and applies to the whole diagram, so the layout is left to right.

- **Recognition:** `Header.Enterprise`, `Directive.Title`, `Directive.Version.3_1`, `Direction.LR`, `Directive.Repeated.LastWins`, `Recognition.Statement.Order`
- **Rendering:** `Render.Title`, `Layout.Direction.LR`
- **Expected:** valid

```mermaid
enterprise
  direction TB
  version 3.1
  business-actor customer "Customer"
  title Directives after an element
  direction LR
  business-role buyer "Buyer"
  customer -assignment-> buyer
```

### A10 — Two elements, one relationship

The simplest connected diagram: one assignment from a component to the function it performs.

- **Recognition:** `Header.Enterprise`, `Element.Declaration`, `Relationship.Assignment`
- **Rendering:** `Render.Relationship.Assignment`, `Render.End.Dot`, `Render.End.ArrowFilled`
- **Expected:** valid

```mermaid
enterprise
  application-component webShop "Web Shop"
  application-function checkout "Checkout"
  webShop -assignment-> checkout
```

---

## Level 2 — Application layer

### A11 — Application active structure

Component, collaboration and interface: square-cornered boxes with the component, collaboration and lollipop icons.

- **Recognition:** `Element.Application.Component`, `Element.Application.Collaboration`, `Element.Application.Interface`, `Relationship.Aggregation`, `Relationship.Composition`
- **Rendering:** `Render.Box.Square`, `Render.Icon.Component`, `Render.Icon.Collaboration`, `Render.Icon.Interface`
- **Expected:** valid

```mermaid
enterprise
  application-collaboration orderSuite "Order Suite"
  application-component webShop "Web Shop"
  application-component orderMgr "Order Manager"
  application-interface shopApi "Shop API"
  orderSuite -aggregation-> webShop
  orderSuite -aggregation-> orderMgr
  webShop -composition-> shopApi
```

### A12 — Application behaviour

Function, interaction, process, event and service: rounded boxes with the chevron, split-circle, block-arrow, event and stadium icons.

- **Recognition:** `Element.Application.Function`, `Element.Application.Interaction`, `Element.Application.Process`, `Element.Application.Event`, `Element.Application.Service`
- **Rendering:** `Render.Box.Rounded`, `Render.Icon.Function`, `Render.Icon.Interaction`, `Render.Icon.Process`, `Render.Icon.Event`, `Render.Icon.Service`
- **Expected:** valid

```mermaid
enterprise
  application-event basketSubmitted "Basket Submitted"
  application-process placeOrder "Place Order"
  application-function pricing "Pricing"
  application-interaction stockCheck "Stock Check"
  application-service orderSvc "Order Service"
  basketSubmitted -triggering-> placeOrder
  pricing -flow-> placeOrder
  stockCheck -flow-> placeOrder
  placeOrder -realization-> orderSvc
```

### A13 — Data object (passive structure)

A data object is drawn with a header band and no corner icon. It is read and written by behaviour elements.

- **Recognition:** `Element.Application.DataObject`, `Relationship.Access.Write`, `Relationship.Access.Read`
- **Rendering:** `Render.Box.Object`, `Render.Relationship.Access`
- **Expected:** valid

```mermaid
enterprise
  application-process placeOrder "Place Order"
  application-function invoicing "Invoicing"
  data-object order "Order"
  placeOrder -access(write)-> order
  invoicing -access(read)-> order
```

### A14 — Complete application layer (all nine types)

Every application element type in one connected diagram for order handling.

- **Recognition:** `Element.Application.*`, `Relationship.Aggregation`, `Relationship.Composition`, `Relationship.Assignment`, `Relationship.Realization`, `Relationship.Serving`, `Relationship.Triggering`, `Relationship.Flow`, `Relationship.Access.ReadWrite`
- **Rendering:** `Render.Layer.Application`, `Render.Icon.*`, `Layout.Layer.Single`
- **Expected:** valid

```mermaid
enterprise
  title Order handling - application layer
  application-collaboration orderSuite "Order Suite"
  application-component webShop "Web Shop"
  application-component orderMgr "Order Manager"
  application-interface shopApi "Shop API"
  application-function checkout "Checkout"
  application-interaction orderSync "Order Synchronisation"
  application-process placeOrder "Place Order"
  application-event orderPlaced "Order Placed"
  application-service orderSvc "Order Service"
  data-object order "Order"
  orderSuite -aggregation-> webShop
  orderSuite -aggregation-> orderMgr
  orderSuite -assignment-> orderSync
  webShop -composition-> shopApi
  webShop -assignment-> checkout
  checkout -realization-> orderSvc
  shopApi -assignment-> orderSvc
  orderSvc -serving-> placeOrder
  placeOrder -triggering-> orderPlaced
  orderPlaced -triggering-> orderSync
  checkout -flow-> orderSync "order details"
  placeOrder -access(write)-> order
  orderSync -access(readwrite)-> order
```

---

## Level 3 — Technology layer

### A15 — Technology active structure

Node, device, system software, technology collaboration and technology interface. The device is the only 3D-bevelled box.

- **Recognition:** `Element.Technology.Node`, `Element.Technology.Device`, `Element.Technology.SystemSoftware`, `Element.Technology.Collaboration`, `Element.Technology.Interface`
- **Rendering:** `Render.Box.Square`, `Render.Box.Device3D`, `Render.Icon.Node`, `Render.Icon.Device`, `Render.Icon.SystemSoftware`
- **Expected:** valid

```mermaid
enterprise
  technology-collaboration webCluster "Web Cluster"
  node appServer "Application Server"
  device lbAppliance "Load Balancer"
  system-software jvm "Java Runtime"
  technology-interface httpsPort "HTTPS Endpoint"
  webCluster -aggregation-> appServer
  webCluster -aggregation-> lbAppliance
  appServer -assignment-> jvm
  appServer -composition-> httpsPort
```

### A16 — Path and communication network

The two network elements, with the communication network realising a path between two nodes.

- **Recognition:** `Element.Technology.Path`, `Element.Technology.CommunicationNetwork`, `Relationship.Realization`, `Relationship.Association`
- **Rendering:** `Render.Icon.Path`, `Render.Icon.Network`
- **Expected:** valid

```mermaid
enterprise
  node primary "Primary Site Server"
  node standby "Standby Site Server"
  path siteLink "Site-to-Site Link"
  communication-network wan "Corporate WAN"
  wan -realization-> siteLink
  siteLink -association-> primary
  siteLink -association-> standby
```

### A17 — Technology behaviour

Technology function, process, interaction, event and service, with a technology service realised by a function.

- **Recognition:** `Element.Technology.Function`, `Element.Technology.Process`, `Element.Technology.Interaction`, `Element.Technology.Event`, `Element.Technology.Service`
- **Rendering:** `Render.Box.Rounded`, `Render.Layer.Technology`
- **Expected:** valid

```mermaid
enterprise
  technology-event releaseReady "Release Ready"
  technology-process deploy "Deploy Release"
  technology-function hosting "Application Hosting"
  technology-interaction replicate "Session Replication"
  technology-service hostingSvc "Hosting Service"
  releaseReady -triggering-> deploy
  deploy -flow-> hosting "release"
  hosting -realization-> hostingSvc
  replicate -flow-> hosting
```

### A18 — Artifact and the complete technology layer (all thirteen types)

Adds the artifact (page with folded corner) and connects every technology element type.

- **Recognition:** `Element.Technology.*`, `Element.Technology.Artifact`, `Relationship.Access.Write`
- **Rendering:** `Render.Layer.Technology`, `Render.Icon.Artifact`
- **Expected:** valid

```mermaid
enterprise
  title Hosting platform - technology layer
  technology-collaboration webCluster "Web Cluster"
  node appServer "Application Server"
  device lbAppliance "Load Balancer"
  system-software jvm "Java Runtime"
  technology-interface httpsPort "HTTPS Endpoint"
  path dcLink "DC-to-DR Link"
  communication-network lan "Data Centre LAN"
  technology-function hosting "Application Hosting"
  technology-process deploy "Deploy Release"
  technology-interaction replicate "Session Replication"
  technology-event releaseReady "Release Ready"
  technology-service hostingSvc "Hosting Service"
  artifact shopWar "shop.war"
  webCluster -aggregation-> appServer
  webCluster -aggregation-> lbAppliance
  webCluster -assignment-> replicate
  appServer -assignment-> jvm
  appServer -composition-> httpsPort
  jvm -assignment-> hosting
  hosting -realization-> hostingSvc
  httpsPort -assignment-> hostingSvc
  releaseReady -triggering-> deploy
  deploy -access(write)-> shopWar
  deploy -flow-> hosting
  lan -association-> appServer
  lan -realization-> dcLink
  appServer -assignment-> shopWar
```

---

## Level 4 — Physical elements

### A19 — Equipment, facility, distribution network and material

The four physical element types for a warehouse.

- **Recognition:** `Element.Physical.Equipment`, `Element.Physical.Facility`, `Element.Physical.DistributionNetwork`, `Element.Physical.Material`
- **Rendering:** `Render.Box.Device3D`, `Render.Icon.Facility`, `Render.Icon.DistributionNetwork`, `Render.Box.Object`
- **Expected:** valid

```mermaid
enterprise
  facility warehouse "Central Warehouse"
  equipment packer "Packing Machine"
  distribution-network fleet "Delivery Fleet"
  material carton "Shipping Carton"
  warehouse -composition-> packer
  fleet -association-> warehouse
  packer -access(write)-> carton
```

### A20 — Physical elements combined with technology

Equipment hosts system software and is connected to a node; a facility houses both. This checks the physical-to-technology cells.

- **Recognition:** `Element.Physical.*`, `Element.Technology.SystemSoftware`, `Relationship.Assignment`, `Relationship.Composition`, `Relationship.Serving`
- **Rendering:** `Render.Layer.Technology`, `Render.Box.Device3D`
- **Expected:** valid

```mermaid
enterprise
  facility warehouse "Central Warehouse"
  equipment sorter "Parcel Sorter"
  system-software sorterFw "Sorter Firmware"
  node wmsServer "Warehouse Server"
  material parcel "Parcel"
  distribution-network fleet "Delivery Fleet"
  warehouse -composition-> sorter
  warehouse -composition-> wmsServer
  sorter -assignment-> sorterFw
  wmsServer -serving-> sorter
  sorter -access(readwrite)-> parcel
  fleet -serving-> warehouse
```

---

## Level 5 — Business layer

### A21 — Business active structure

Actor, role, collaboration and interface.

- **Recognition:** `Element.Business.Actor`, `Element.Business.Role`, `Element.Business.Collaboration`, `Element.Business.Interface`
- **Rendering:** `Render.Layer.Business`, `Render.Icon.Actor`, `Render.Icon.Role`
- **Expected:** valid

```mermaid
enterprise
  business-actor clerk "Payroll Clerk"
  business-role payrollAdmin "Payroll Administrator"
  business-role approver "Pay Approver"
  business-collaboration payTeam "Pay Run Team"
  business-interface selfService "Staff Self-Service Desk"
  clerk -assignment-> payrollAdmin
  payTeam -aggregation-> payrollAdmin
  payTeam -aggregation-> approver
  payrollAdmin -composition-> selfService
```

### A22 — Business behaviour

Function, interaction, process, event and service for a monthly pay run.

- **Recognition:** `Element.Business.Function`, `Element.Business.Interaction`, `Element.Business.Process`, `Element.Business.Event`, `Element.Business.Service`
- **Rendering:** `Render.Layer.Business`, `Render.Box.Rounded`
- **Expected:** valid

```mermaid
enterprise
  business-event periodEnd "Pay Period Ended"
  business-process payRun "Run Payroll"
  business-function payrollFn "Payroll"
  business-interaction signOff "Pay Run Sign-off"
  business-service paySvc "Salary Payment"
  periodEnd -triggering-> payRun
  payRun -triggering-> signOff
  payrollFn -composition-> payRun
  payRun -realization-> paySvc
```

### A23 — Business passive structure and product

Business object, contract, representation and product. The contract and representation have their own outlines.

- **Recognition:** `Element.Business.Object`, `Element.Business.Contract`, `Element.Business.Representation`, `Element.Business.Product`, `Relationship.Specialization`
- **Rendering:** `Render.Box.Object`, `Render.Box.Contract`, `Render.Box.Representation`, `Render.Icon.Product`
- **Expected:** valid

```mermaid
enterprise
  business-object payslipObj "Payslip"
  contract employment "Employment Contract"
  business-object agreement "Agreement"
  representation payslipPdf "Payslip PDF"
  product payrollPkg "Managed Payroll"
  business-service paySvc "Salary Payment"
  employment -specialization-> agreement
  payslipPdf -realization-> payslipObj
  payrollPkg -aggregation-> paySvc
  payrollPkg -aggregation-> employment
```

### A24 — Complete business layer (all thirteen types)

Every business element type in a single payroll diagram.

- **Recognition:** `Element.Business.*`, `Relationship.Assignment`, `Relationship.Serving`, `Relationship.Access.Write`, `Relationship.Access.Read`
- **Rendering:** `Render.Layer.Business`, `Render.Icon.*`
- **Expected:** valid

```mermaid
enterprise
  title Payroll - business layer
  business-actor clerk "Payroll Clerk"
  business-role payrollAdmin "Payroll Administrator"
  business-collaboration payTeam "Pay Run Team"
  business-interface selfService "Staff Self-Service Desk"
  business-function payrollFn "Payroll"
  business-interaction signOff "Pay Run Sign-off"
  business-process payRun "Run Payroll"
  business-event periodEnd "Pay Period Ended"
  business-service paySvc "Salary Payment"
  business-object payslipObj "Payslip"
  contract employment "Employment Contract"
  representation payslipPdf "Payslip PDF"
  product payrollPkg "Managed Payroll"
  clerk -assignment-> payrollAdmin
  payTeam -aggregation-> payrollAdmin
  payTeam -assignment-> signOff
  payrollAdmin -assignment-> payRun
  payrollFn -composition-> payRun
  periodEnd -triggering-> payRun
  payRun -triggering-> signOff
  payRun -realization-> paySvc
  selfService -assignment-> paySvc
  payRun -access(write)-> payslipObj
  payRun -access(read)-> employment
  payslipPdf -realization-> payslipObj
  payrollPkg -aggregation-> paySvc
```

---

## Level 6 — Strategy

### A25 — Resource, capability, value stream and course of action

The four strategy element types for a web shop growth plan.

- **Recognition:** `Element.Strategy.Resource`, `Element.Strategy.Capability`, `Element.Strategy.ValueStream`, `Element.Strategy.CourseOfAction`
- **Rendering:** `Render.Layer.Strategy`, `Render.Icon.Resource`, `Render.Icon.Capability`, `Render.Icon.ValueStream`, `Render.Icon.CourseOfAction`
- **Expected:** valid

```mermaid
enterprise
  resource staff "Fulfilment Staff"
  capability fulfilCap "Order Fulfilment"
  value-stream deliverOrder "Deliver Order"
  course-of-action sameDay "Offer Same-Day Delivery"
  staff -assignment-> fulfilCap
  fulfilCap -serving-> deliverOrder
  fulfilCap -realization-> sameDay
```

### A26 — Strategy realised by the business layer

Capabilities are realised by business functions and processes; a value stream is broken into stages by composition.

- **Recognition:** `Element.Strategy.*`, `Element.Business.Function`, `Element.Business.Process`, `Relationship.Realization`, `Relationship.Composition`, `Relationship.Flow`
- **Rendering:** `Layout.Layer.Rank.StrategyAboveBusiness`
- **Expected:** valid

```mermaid
enterprise
  value-stream orderToCash "Order to Cash"
  value-stream takeStage "Take Order"
  value-stream shipStage "Ship Order"
  capability salesCap "Sales"
  capability logisticsCap "Logistics"
  business-function salesFn "Sales Function"
  business-process pickPack "Pick and Pack"
  resource stock "Stock"
  orderToCash -composition-> takeStage
  orderToCash -composition-> shipStage
  takeStage -flow-> shipStage
  salesCap -serving-> takeStage
  logisticsCap -serving-> shipStage
  salesFn -realization-> salesCap
  pickPack -realization-> logisticsCap
  stock -assignment-> logisticsCap
```

---

## Level 7 — Motivation

### A27 — Stakeholder, driver and assessment

The motivation elements that describe why change is needed.

- **Recognition:** `Element.Motivation.Stakeholder`, `Element.Motivation.Driver`, `Element.Motivation.Assessment`, `Relationship.Association`, `Relationship.Influence`
- **Rendering:** `Render.Layer.Motivation`, `Render.Icon.Stakeholder`, `Render.Icon.Driver`, `Render.Icon.Assessment`
- **Expected:** valid

```mermaid
enterprise
  stakeholder cfo "Chief Financial Officer"
  driver compliance "Payroll Compliance"
  assessment lateFilings "Late Tax Filings"
  cfo -association-> compliance
  lateFilings -influence-> compliance
  compliance -association-> lateFilings
```

### A28 — Goal, outcome, principle, requirement and constraint

The intention elements, with requirements and a principle realising a goal and a constraint specialising a requirement.

- **Recognition:** `Element.Motivation.Goal`, `Element.Motivation.Outcome`, `Element.Motivation.Principle`, `Element.Motivation.Requirement`, `Element.Motivation.Constraint`, `Relationship.Realization`, `Relationship.Specialization`
- **Rendering:** `Render.Layer.Motivation`, `Render.Icon.Goal`, `Render.Icon.Outcome`, `Render.Icon.Principle`, `Render.Icon.Requirement`, `Render.Icon.Constraint`
- **Expected:** valid

```mermaid
enterprise
  goal onTimePay "Pay Staff on Time"
  outcome zeroLate "No Late Payments"
  principle automate "Automate by Default"
  requirement autoFile "File Tax Returns Automatically"
  constraint deadline "Lodge by the 21st"
  requirement fileOnTime "File Returns on Time"
  zeroLate -realization-> onTimePay
  automate -realization-> onTimePay
  autoFile -realization-> zeroLate
  deadline -specialization-> fileOnTime
  fileOnTime -realization-> zeroLate
```

### A29 — Meaning, value and the complete motivation set (all ten types)

Adds meaning and value (the only ellipse) and connects every motivation element type, including signed influence.

- **Recognition:** `Element.Motivation.*`, `Element.Motivation.Meaning`, `Element.Motivation.Value`, `Relationship.Influence.Positive`, `Relationship.Influence.Negative`
- **Rendering:** `Render.Box.Ellipse`, `Render.Icon.Meaning`, `Render.Relationship.Influence.Label`
- **Expected:** valid

```mermaid
enterprise
  title Payroll compliance - motivation
  stakeholder cfo "Chief Financial Officer"
  driver compliance "Payroll Compliance"
  assessment lateFilings "Late Tax Filings"
  goal onTimePay "Pay Staff on Time"
  outcome zeroLate "No Late Payments"
  principle automate "Automate by Default"
  requirement autoFile "File Tax Returns Automatically"
  constraint deadline "Lodge by the 21st"
  meaning payslipMeaning "A payslip is a legal record"
  value staffTrust "Staff Trust"
  cfo -association-> compliance
  compliance -association-> lateFilings
  lateFilings -influence(-)-> onTimePay
  zeroLate -realization-> onTimePay
  automate -influence(+)-> autoFile
  autoFile -realization-> zeroLate
  deadline -influence(+)-> zeroLate
  onTimePay -association-> staffTrust
  payslipMeaning -association-> staffTrust
```

---

## Level 8 — Implementation and migration

### A30 — Work package, deliverable and implementation event

A cutover work package started by an event and producing a deliverable.

- **Recognition:** `Element.Implementation.WorkPackage`, `Element.Implementation.Deliverable`, `Element.Implementation.Event`, `Relationship.Triggering`, `Relationship.Realization`
- **Rendering:** `Render.Layer.Implementation`, `Render.Icon.WorkPackage`, `Render.Icon.Deliverable`
- **Expected:** valid

```mermaid
enterprise
  implementation-event approved "Business Case Approved"
  work-package build "Build New Payroll"
  work-package cutover "Cutover"
  deliverable payrollApp "Configured Payroll System"
  implementation-event live "Go-Live"
  approved -triggering-> build
  build -triggering-> cutover
  build -realization-> payrollApp
  cutover -triggering-> live
```

### A31 — Plateau and gap (completing the five types)

Baseline, transition and target plateaus with gaps between them, completing the implementation and migration set.

- **Recognition:** `Element.Implementation.Plateau`, `Element.Implementation.Gap`, `Relationship.Triggering`, `Relationship.Association`
- **Rendering:** `Render.Icon.Plateau`, `Render.Icon.Gap`
- **Expected:** valid

```mermaid
enterprise
  plateau baseline "Baseline: Legacy Payroll"
  plateau transition "Transition: Parallel Run"
  plateau target "Target: Cloud Payroll"
  gap gap1 "Gap: Data Migration"
  gap gap2 "Gap: Decommissioning"
  work-package migrate "Migrate Employee Records"
  deliverable migrated "Migrated Records"
  baseline -triggering-> transition
  transition -triggering-> target
  gap1 -association-> baseline
  gap1 -association-> transition
  gap2 -association-> transition
  gap2 -association-> target
  migrate -realization-> migrated
  migrated -realization-> transition
```

---

## Level 9 — Composite elements: grouping, location and junction

### A32 — `group` statement with members

The `group` statement draws the dashed grouping box with a name tab. Members are placed with `in`.

- **Recognition:** `Group.Declaration`, `Nesting.In.Group`
- **Rendering:** `Render.Group.DashedTab`, `Layout.Compound.Single`
- **Expected:** valid

```mermaid
enterprise
  group shopFront "Customer-facing systems"
  application-component webShop "Web Shop" in shopFront
  application-component mobileApp "Mobile App" in shopFront
  application-component orderMgr "Order Manager"
  webShop -flow-> orderMgr
  mobileApp -flow-> orderMgr
```

### A33 — `grouping` element type with relationships

A grouping declared with the element form takes part in relationships (here, aggregation of its members and a serving relationship from it).

- **Recognition:** `Element.Composite.Grouping`, `Nesting.In.Group`, `Relationship.Aggregation`
- **Rendering:** `Render.Group.DashedTab`, `Render.Relationship.HiddenByNesting`
- **Expected:** valid

```mermaid
enterprise
  grouping payrollSuite "Payroll Suite"
  application-component payCalc "Pay Calculator" in payrollSuite
  application-component taxEngine "Tax Engine" in payrollSuite
  business-process payRun "Run Payroll"
  payrollSuite -aggregation-> payCalc
  payrollSuite -aggregation-> taxEngine
  payrollSuite -serving-> payRun
```

### A34 — Location as a container

Two locations aggregate the facilities, nodes and actors placed in them.

- **Recognition:** `Element.Composite.Location`, `Nesting.In.Element`, `Relationship.Aggregation`
- **Rendering:** `Render.Icon.Location`, `Layout.Compound.Siblings`
- **Expected:** valid

```mermaid
enterprise
  location perth "Perth"
  location sydney "Sydney"
  facility perthDc "Perth Data Centre" in perth
  node shopNode "Shop Server" in perthDc
  business-actor supportTeam "Support Team" in sydney
  perth -aggregation-> perthDc
  perthDc -composition-> shopNode
  sydney -aggregation-> supportTeam
  shopNode -serving-> supportTeam
```

### A35 — And-junction

An and-junction (filled circle) joins two triggers: shipping waits for both payment and picking.

- **Recognition:** `Element.Junction.And`, `Relationship.Triggering`
- **Rendering:** `Render.Junction.Filled`, `Layout.Junction.Small`
- **Expected:** valid

```mermaid
enterprise
  business-process takePayment "Take Payment"
  business-process pickItems "Pick Items"
  and-junction both
  business-process ship "Ship Order"
  takePayment -triggering-> both
  pickItems -triggering-> both
  both -triggering-> ship
```

### A36 — Or-junction and junction spellings

An or-junction (hollow circle) splits a flow into alternatives. Its optional label is drawn in small text beside the circle. The other accepted spellings (`junction`, `junction-and`, `junction-or`) are declared alongside; a bare `junction` is an and-junction.

- **Recognition:** `Element.Junction.Or`, `Element.Junction.And`, `Element.Junction.Aliases`, `Element.Junction.Label`, `Relationship.Flow`
- **Rendering:** `Render.Junction.Hollow`, `Render.Junction.Filled`, `Render.Junction.Label`
- **Expected:** valid

```mermaid
enterprise
  business-process assess "Assess Return"
  or-junction choice "Refund or replace?"
  business-process refund "Issue Refund"
  business-process replace "Send Replacement"
  junction-or choice2
  junction plainJoin
  junction-and andJoin
  business-process close "Close Case"
  assess -flow-> choice
  choice -flow-> refund
  choice -flow-> replace
  refund -flow-> choice2
  replace -flow-> choice2
  choice2 -flow-> plainJoin
  plainJoin -flow-> andJoin
  andJoin -flow-> close
```


---

## Level 10 — Relationships

### A37 — Structural relationships: composition, aggregation, assignment and realisation

The four structural relationships by full name: filled diamond, hollow diamond, dot plus filled arrow, and dotted line with hollow triangle.

- **Recognition:** `Relationship.Composition`, `Relationship.Aggregation`, `Relationship.Assignment`, `Relationship.Realization`, `Relationship.Name.Full`
- **Rendering:** `Render.End.DiamondFilled`, `Render.End.DiamondHollow`, `Render.End.Dot`, `Render.End.TriangleHollow`, `Render.Line.Dotted`
- **Expected:** valid

```mermaid
enterprise
  product shopOffer "Web Shop Membership"
  business-service delivery "Home Delivery"
  contract terms "Membership Terms"
  application-component webShop "Web Shop"
  application-interface shopUi "Shop Web UI"
  application-function checkout "Checkout"
  application-service ordering "Online Ordering"
  shopOffer -aggregation-> delivery
  shopOffer -aggregation-> terms
  webShop -composition-> shopUi
  webShop -assignment-> checkout
  checkout -realization-> ordering
```

### A38 — Serving

Serving within and across layers: a solid line with an open arrowhead pointing at the element being served.

- **Recognition:** `Relationship.Serving`
- **Rendering:** `Render.End.ArrowOpen`, `Render.Line.Solid`, `Layout.EdgeReversal.Serving`
- **Expected:** valid

```mermaid
enterprise
  application-service stockSvc "Stock Lookup"
  application-service priceSvc "Price Lookup"
  application-process placeOrder "Place Order"
  business-process takeOrder "Take Phone Order"
  stockSvc -serving-> placeOrder
  priceSvc -serving-> placeOrder
  stockSvc -serving-> takeOrder
  priceSvc -serving-> stockSvc
```

### A39 — Access with the default qualifier

An unqualified `-access->` defaults to write, so both relationships below must render identically (arrowhead at the data object).

- **Recognition:** `Relationship.Access.Default`, `Relationship.Access.Write`
- **Rendering:** `Render.Line.Dotted`, `Render.End.ArrowOpenSmall.Target`
- **Expected:** valid

```mermaid
enterprise
  application-process placeOrder "Place Order"
  application-process amendOrder "Amend Order"
  data-object order "Order"
  placeOrder -access-> order
  amendOrder -access(write)-> order
```

### A40 — Access read, readwrite and plain access

Read draws the arrowhead at the accessor, readwrite at both ends and `access` at neither end.

- **Recognition:** `Relationship.Access.Read`, `Relationship.Access.ReadWrite`, `Relationship.Access.Plain`
- **Rendering:** `Render.End.ArrowOpenSmall.Source`, `Render.End.ArrowOpenSmall.Both`, `Render.End.None`
- **Expected:** valid

```mermaid
enterprise
  business-process payRun "Run Payroll"
  business-process audit "Audit Pay Run"
  business-process archive "Archive Records"
  business-object timesheet "Timesheet"
  business-object payslipObj "Payslip"
  payRun -access(read)-> timesheet
  payRun -access(readwrite)-> payslipObj
  audit -access(read)-> payslipObj
  archive -access(access)-> payslipObj
```

### A41 — Influence without a qualifier, positive and negative

A dashed line with an open arrowhead; the `+` and `-` qualifiers are drawn as a small label near the target.

- **Recognition:** `Relationship.Influence`, `Relationship.Influence.Positive`, `Relationship.Influence.Negative`
- **Rendering:** `Render.Line.Dashed`, `Render.End.ArrowOpen`, `Render.Relationship.Influence.Label`
- **Expected:** valid

```mermaid
enterprise
  driver costPressure "Rising Costs"
  driver growth "Online Growth"
  goal cutCost "Reduce Fulfilment Cost"
  goal expand "Expand Online Sales"
  assessment slowSite "Slow Checkout Pages"
  costPressure -influence-> cutCost
  growth -influence(+)-> expand
  slowSite -influence(-)-> expand
```

### A42 — Influence strength (double signs and numbers)

The `++`, `--` and numeric forms (`3`, `-2`, `+1`) of the influence qualifier.

- **Recognition:** `Relationship.Influence.StrongPositive`, `Relationship.Influence.StrongNegative`, `Relationship.Influence.Numeric`
- **Rendering:** `Render.Relationship.Influence.Label`
- **Expected:** valid

```mermaid
enterprise
  principle cloudFirst "Cloud First"
  principle buyNotBuild "Buy Before Build"
  requirement saas "Use a SaaS Payroll Product"
  requirement onPrem "Keep Payroll On Premises"
  requirement audit "Keep Seven Years of Records"
  goal lowerCost "Lower Running Cost"
  cloudFirst -influence(++)-> saas
  cloudFirst -influence(--)-> onPrem
  buyNotBuild -influence(3)-> saas
  onPrem -influence(-2)-> lowerCost
  audit -influence(+1)-> onPrem
```

### A43 — Triggering and flow with labels

Triggering (solid, filled arrowhead) and flow (dashed, filled arrowhead), each with a relationship label.

- **Recognition:** `Relationship.Triggering`, `Relationship.Flow`, `Relationship.Label`
- **Rendering:** `Render.Line.Solid`, `Render.Line.Dashed`, `Render.End.ArrowFilled`, `Render.Relationship.Label`
- **Expected:** valid

```mermaid
enterprise
  business-event orderReceived "Order Received"
  business-process checkStock "Check Stock"
  business-process pick "Pick Items"
  business-process invoice "Send Invoice"
  orderReceived -triggering-> checkStock "new order"
  checkStock -triggering-> pick "in stock"
  checkStock -flow-> invoice "order lines"
  pick -flow-> invoice "packed quantities"
```

### A44 — Specialisation in several layers

Specialisation is only allowed between elements of the same type. A solid line with a hollow triangle at the general element.

- **Recognition:** `Relationship.Specialization`
- **Rendering:** `Render.End.TriangleHollow`, `Render.Line.Solid`, `Layout.Hierarchy`
- **Expected:** valid

```mermaid
enterprise
  business-actor customer "Customer"
  business-actor retailCustomer "Retail Customer"
  business-actor tradeCustomer "Trade Customer"
  application-component shopApp "Shop Application"
  application-component mobileShop "Mobile Shop"
  node server "Server"
  node dbServer "Database Server"
  goal improve "Improve Service"
  goal fasterDelivery "Faster Delivery"
  retailCustomer -specialization-> customer
  tradeCustomer -specialization-> customer
  mobileShop -specialization-> shopApp
  dbServer -specialization-> server
  fasterDelivery -specialization-> improve
```

### A45 — Association, undirected and directed

Plain association has no arrowhead; `association(directed)` draws a half arrowhead at the target.

- **Recognition:** `Relationship.Association`, `Relationship.Association.Directed`
- **Rendering:** `Render.End.None`, `Render.End.HalfArrow`
- **Expected:** valid

```mermaid
enterprise
  business-actor customer "Customer"
  business-object complaint "Complaint"
  business-role officer "Support Officer"
  stakeholder ombudsman "Consumer Ombudsman"
  customer -association-> complaint
  complaint -association(directed)-> officer
  ombudsman -association(directed)-> customer
  officer -association-> customer
```

### A46 — All eleven single-letter relationship codes

Each relationship written with its specification letter (`c g i r v a n t f s o`). The drawing must be identical to the full-name form.

- **Recognition:** `Relationship.Name.Letter`, `Relationship.*`
- **Rendering:** `Render.Relationship.*`
- **Expected:** valid

```mermaid
enterprise
  application-collaboration suite "Order Suite"
  application-component shop "Web Shop"
  application-component legacyShop "Legacy Shop"
  application-interface api "Shop API"
  application-function checkout "Checkout"
  application-service ordering "Online Ordering"
  application-process place "Place Order"
  application-event placed "Order Placed"
  data-object order "Order"
  requirement fastCheckout "Checkout under Two Seconds"
  goal conversion "Raise Conversion"
  shop -c-> api
  suite -g-> shop
  shop -i-> checkout
  checkout -r-> ordering
  ordering -v-> place
  place -a-> order
  fastCheckout -n-> conversion
  place -t-> placed
  checkout -f-> place
  legacyShop -s-> shop
  shop -o-> order
```

### A47 — Junctions with serving and realisation

Junctions are not only for triggering and flow: one or-junction collects two realisations and an and-junction fans one serving relationship out to three processes.

- **Recognition:** `Element.Junction.Or`, `Element.Junction.And`, `Relationship.Realization`, `Relationship.Serving`
- **Rendering:** `Render.Junction.Hollow`, `Render.Junction.Filled`, `Layout.Junction.FanOut`
- **Expected:** valid

```mermaid
enterprise
  application-component inHouse "In-house Payment Module"
  application-component gateway "Payment Gateway Adapter"
  or-junction either
  application-service paySvc "Card Payment"
  and-junction all
  business-process checkout "Checkout"
  business-process phoneOrder "Phone Order"
  business-process refund "Refund"
  inHouse -realization-> either
  gateway -realization-> either
  either -realization-> paySvc
  paySvc -serving-> all
  all -serving-> checkout
  all -serving-> phoneOrder
  all -serving-> refund
```

### A48 — Junction chains and mixed junction types

Junctions connected directly to each other, mixing and and or, with triggering in and out.

- **Recognition:** `Element.Junction.And`, `Element.Junction.Or`, `Relationship.Triggering`, `Relationship.Junction.Chain`
- **Rendering:** `Render.Junction.Filled`, `Render.Junction.Hollow`, `Layout.Junction.Chain`
- **Expected:** valid

```mermaid
enterprise
  business-event returnLogged "Return Logged"
  and-junction split
  business-process inspect "Inspect Item"
  business-process checkReceipt "Check Receipt"
  and-junction join
  or-junction decide
  business-process refund "Refund"
  business-process repair "Repair"
  or-junction merge
  business-event closed "Return Closed"
  returnLogged -triggering-> split
  split -triggering-> inspect
  split -triggering-> checkReceipt
  inspect -triggering-> join
  checkReceipt -triggering-> join
  join -triggering-> decide
  decide -triggering-> refund
  decide -triggering-> repair
  refund -triggering-> merge
  repair -triggering-> merge
  merge -triggering-> closed
```

---

## Level 11 — Labels and text

### A49 — Long names that wrap over several lines

Labels of 40 to 120 characters. The renderer wraps at word boundaries and grows the box height, so long names become multi-line names without any explicit break syntax.

- **Recognition:** `Element.Label.Quoted`, `Element.Label.Long`
- **Rendering:** `Render.Label.Wrap`, `Render.Box.AutoHeight`, `Layout.NodeSize.Variable`
- **Expected:** valid

```mermaid
enterprise
  business-process returns "Receive, inspect and restock returned items, then notify the customer and the finance team"
  application-service refundSvc "Refund calculation and payment instruction service for online and in-store returns"
  application-component returnsPortal "Returns Portal"
  business-object rma "Return Merchandise Authorisation with photographs, reason codes and courier reference"
  refundSvc -serving-> returns
  returnsPortal -realization-> refundSvc
  returns -access(write)-> rma
```

### A50 — Very long words with no break opportunity

Single tokens longer than the wrap width (identifiers, URLs, file names). The box must widen rather than let text overflow.

- **Recognition:** `Element.Label.Long`
- **Rendering:** `Render.Label.NoBreak`, `Render.Box.AutoWidth`
- **Expected:** valid

```mermaid
enterprise
  artifact jar "customer-order-fulfilment-reconciliation-service-2.14.3-SNAPSHOT.jar"
  technology-interface endpoint "https://orders.example.internal/api/v2/fulfilment/reconciliation"
  system-software runtime "JavaRuntimeEnvironmentWithExtendedCryptographyPolicyBundle"
  runtime -assignment-> jar
  runtime -composition-> endpoint
```

### A51 — Special characters in labels

Punctuation and symbols inside quoted labels: ampersand, angle brackets, apostrophe, slashes, all bracket types, pipe, colon, semicolon, hash, at sign and others. None of these may break parsing or SVG escaping.

- **Recognition:** `Element.Label.SpecialCharacters`, `Relationship.Label.SpecialCharacters`
- **Rendering:** `Render.Label.XmlEscape`
- **Expected:** valid

```mermaid
enterprise
  application-component crm "CRM & Billing <legacy>"
  application-service lookup "Customer's look-up (v2) [beta] {internal}"
  data-object record "Record: id/name | email; phone"
  application-interface api "REST /customers?id=42&expand=orders"
  application-function calc "Totals: 50% off + GST = $9.99 @ till #3"
  crm -assignment-> calc
  calc -realization-> lookup
  api -assignment-> lookup
  calc -access(read)-> record "reads <id> & name"
```

### A52 — Unicode labels

Accented Latin, German sharp s, Greek, Cyrillic, Chinese, Japanese, Arabic (right to left), currency symbols and emoji. Font fallback and width measurement must cope with all of them.

- **Recognition:** `Element.Label.Unicode`, `Relationship.Label.Unicode`
- **Rendering:** `Render.Label.Unicode`, `Render.Font.Fallback`, `Render.Label.RightToLeft`
- **Expected:** valid

```mermaid
enterprise
  business-actor kunde "Kunde (Großhandel)"
  business-role acheteur "Acheteur délégué"
  business-service delivery "Доставка заказов"
  business-process order "下单流程"
  business-object receipt "領収書"
  business-event paid "تم الدفع"
  product bundle "Bundle € £ ¥ ✓ 📦"
  application-service lambda "Σ totals (αβγ)"
  kunde -assignment-> acheteur
  acheteur -assignment-> order
  order -realization-> delivery "réalise ✓"
  order -access(write)-> receipt
  paid -triggering-> order "→ 開始"
  bundle -aggregation-> delivery
  lambda -serving-> order
```

### A53 — Empty labels

An empty quoted label on elements and on a relationship. The element must still render as a correctly sized box with its icon and no text.

- **Recognition:** `Element.Label.Empty`, `Relationship.Label.Empty`
- **Rendering:** `Render.Label.Empty`, `Render.Box.MinimumSize`
- **Expected:** valid

```mermaid
enterprise
  application-component blank ""
  application-service named "Named Service"
  data-object anon ""
  blank -realization-> named ""
  blank -access(read)-> anon
```

### A54 — Relationship labels: short, long and mixed

Labels on many relationships at once, including a long one that must wrap and labels on short edges where there is little room.

- **Recognition:** `Relationship.Label`, `Relationship.Label.Long`
- **Rendering:** `Render.Relationship.Label`, `Render.Relationship.Label.Wrap`, `Layout.Label.Collision`
- **Expected:** valid

```mermaid
enterprise
  application-component shop "Web Shop"
  application-component erp "ERP"
  application-component wms "Warehouse System"
  application-component crm "CRM"
  shop -flow-> erp "orders"
  erp -flow-> wms "pick lists, delivery notes and courier labels, sent every five minutes in batches"
  wms -flow-> erp "shipped"
  shop -flow-> crm "1"
  crm -flow-> shop "loyalty points balance"
  erp -triggering-> crm "invoice"
```

### A55 — Title with special characters and Unicode

The title is the rest of the line, so it may contain quotes, symbols and non-Latin text.

- **Recognition:** `Directive.Title`, `Directive.Title.SpecialCharacters`
- **Rendering:** `Render.Title`, `Render.Label.XmlEscape`
- **Expected:** valid

```mermaid
enterprise
  title Payroll "AS-IS" view: payslips & tax (FY 2026/27) – Ünïcödé ✓
  business-process payRun "Run Payroll"
  business-object payslipObj "Payslip"
  payRun -access(write)-> payslipObj
```

### A56 — Explicit line breaks in labels

Element, group and relationship labels break where they contain `<br>`, `<br/>` or `<br />` (in any letter case) or the two characters `\n`, as in Mermaid. Automatic word wrapping still applies to each resulting line. A literal backslash followed by `n` is written `\\n` (see `A125`).

- **Recognition:** `Element.Label.LineBreak`, `Element.Label.LineBreak.BackslashN`, `Group.Label.LineBreak`, `Relationship.Label.LineBreak`
- **Rendering:** `Render.Label.ExplicitBreak`, `Render.Box.AutoHeight`
- **Expected:** valid

```mermaid
enterprise
  group hr "Human<br>Resources"
  business-process payRun "Run Payroll<br/>(monthly)" in hr
  business-object payslipObj "Payslip<BR />PDF and paper" in hr
  business-object rule "Payroll rules\nversion 2"
  payRun -access(write)-> payslipObj "writes<br/>one per employee"
  payRun -access(read)-> rule "reads\nat the start"
```

### A57 — Embedded double quotes, entity codes and `%%` inside labels

A double quote inside a label can be written either as `\"` or as the Mermaid entity code `#quot;`. Other characters can be written as `#name;` (an HTML entity name) or `#123;` (a decimal character number). A backslash that does not start `\"`, `\'`, `\\` or `\n` stays as typed, so a Windows path keeps its backslashes; a label that ends in a backslash writes it as `\\`. `%%` inside a quoted label is text. On the title line, as in Mermaid, `%%` starts a trailing comment.

- **Recognition:** `Element.Label.EscapedQuote`, `Element.Label.EntityCode`, `Element.Label.Backslash.Literal`, `Recognition.Comment.InsideString`, `Directive.Title.TrailingComment`
- **Rendering:** `Render.Label.XmlEscape`, `Render.Label.EntityDecode`
- **Expected:** valid

```mermaid
enterprise
  title Discounts this week %% everything after the two percent signs is a comment
  application-component shop "The \"New\" Web Shop"
  application-service discount "Discount 20%% off"
  data-object quote "Quote #quot;draft#quot; #35;12 #lt;v2#gt; #9829;"
  artifact export "Exports in C:\data\exports\\"
  shop -realization-> discount "keeps 20%% visible"
  shop -access(write)-> quote
  shop -access(write)-> export "#quot;nightly#quot;"
```

---

## Level 12 — Nesting and containment

### A58 — Nested groups

A group inside a group, with members at both levels and a relationship between levels.

- **Recognition:** `Group.Declaration`, `Group.In.Group`, `Nesting.In.Group`
- **Rendering:** `Render.Group.DashedTab`, `Layout.Compound.Nested`
- **Expected:** valid

```mermaid
enterprise
  group estate "Application estate"
  group finance "Finance systems" in estate
  application-component shop "Web Shop" in estate
  application-component ledger "General Ledger" in finance
  application-component payroll "Payroll" in finance
  shop -flow-> ledger "sales journal"
  payroll -flow-> ledger "pay journal"
```

### A59 — Elements nested in a node

System software and artifacts nested in a node, each with the explicit relationship the nesting stands for. Those relationships are hidden in the drawing; the serving relationship to the outside is drawn.

- **Recognition:** `Nesting.In.Element`, `Nesting.ExplicitRelationship`, `Relationship.Composition`, `Relationship.Assignment`
- **Rendering:** `Render.Relationship.HiddenByNesting`, `Layout.Compound.Element`
- **Expected:** valid

```mermaid
enterprise
  node appNode "Application Server"
  system-software runtime "Java Runtime" in appNode
  system-software monitor "Monitoring Daemon" in appNode
  artifact shopWar "shop.war" in appNode
  application-component shop "Web Shop"
  appNode -composition-> runtime
  appNode -composition-> monitor
  appNode -assignment-> shopWar
  shopWar -realization-> shop
```

### A60 — Location, facility and equipment

Physical containment: a location aggregates a facility that composes equipment.

- **Recognition:** `Nesting.In.Element`, `Element.Composite.Location`, `Element.Physical.Facility`, `Element.Physical.Equipment`
- **Rendering:** `Layout.Compound.Nested`, `Render.Box.Device3D`
- **Expected:** valid

```mermaid
enterprise
  location kewdale "Kewdale Industrial Estate"
  facility dc "Distribution Centre" in kewdale
  equipment conveyor "Conveyor Line" in dc
  equipment scanner "Label Scanner" in dc
  material tote "Picking Tote"
  kewdale -aggregation-> dc
  dc -composition-> conveyor
  dc -composition-> scanner
  conveyor -access(readwrite)-> tote
```

### A61 — Five levels of nesting

group → location → node → node → system software → artifact. Each element-in-element level has its explicit relationship.

- **Recognition:** `Nesting.Depth.5`, `Group.Declaration`, `Nesting.In.Element`
- **Rendering:** `Layout.Compound.Deep`, `Render.Group.DashedTab`
- **Expected:** valid

```mermaid
enterprise
  group region "Asia-Pacific region"
  location perth "Perth" in region
  node cluster "Shop Cluster" in perth
  node server1 "Server 1" in cluster
  system-software runtime "Java Runtime" in server1
  artifact shopWar "shop.war" in runtime
  application-component shop "Web Shop"
  perth -aggregation-> cluster
  cluster -composition-> server1
  server1 -composition-> runtime
  runtime -assignment-> shopWar
  shopWar -realization-> shop
```

### A62 — Relationships that cross container borders

Edges from a nested child to an outside element, between children of different containers, and from a container to an element inside another container.

- **Recognition:** `Nesting.In.Group`, `Nesting.In.Element`, `Relationship.CrossContainer`
- **Rendering:** `Routing.CrossContainer`, `Routing.Obstacle.ContainerTitle`
- **Expected:** valid

```mermaid
enterprise
  group front "Front office"
  group back "Back office"
  application-component shop "Web Shop" in front
  application-function checkout "Checkout" in shop
  application-component erp "ERP" in back
  application-function invoicing "Invoicing" in erp
  application-service paySvc "Payment Service"
  data-object invoice "Invoice"
  shop -assignment-> checkout
  erp -assignment-> invoicing
  checkout -flow-> invoicing "order"
  paySvc -serving-> checkout
  invoicing -access(write)-> invoice
  erp -serving-> shop
```

### A63 — Parent-child relationships that stay visible

The parent assigns the nested function (hidden by nesting), but also has a flow to it and the child has a relationship back to the parent; those two are not expressed by nesting and must be drawn inside the container.

- **Recognition:** `Nesting.In.Element`, `Relationship.HiddenByNesting`, `Relationship.ParentChild.Visible`
- **Rendering:** `Routing.InsideContainer`, `Render.Relationship.HiddenByNesting`
- **Expected:** valid

```mermaid
enterprise
  application-component shop "Web Shop"
  application-function checkout "Checkout" in shop
  application-function search "Product Search" in shop
  shop -assignment-> checkout
  shop -assignment-> search
  shop -flow-> checkout "session"
  search -flow-> shop "results"
  search -triggering-> checkout
```

### A64 — One container with many children

A node holding ten artifacts. The container must grow and pack its children without overlap.

- **Recognition:** `Nesting.In.Element`, `Nesting.Children.Many`
- **Rendering:** `Layout.Compound.Packing`
- **Expected:** valid

```mermaid
enterprise
  node web "Web Server"
  artifact a1 "home.html" in web
  artifact a2 "catalogue.html" in web
  artifact a3 "basket.html" in web
  artifact a4 "checkout.html" in web
  artifact a5 "account.html" in web
  artifact a6 "styles.css" in web
  artifact a7 "app.js" in web
  artifact a8 "logo.svg" in web
  artifact a9 "robots.txt" in web
  artifact a10 "sitemap.xml" in web
  web -assignment-> a1
  web -assignment-> a2
  web -assignment-> a3
  web -assignment-> a4
  web -assignment-> a5
  web -assignment-> a6
  web -assignment-> a7
  web -assignment-> a8
  web -assignment-> a9
  web -assignment-> a10
```

### A65 — Nesting without an explicit relationship (warning)

The function is nested in the component but no composition, aggregation, assignment, realisation, access or specialisation from the component to it is declared, so the nesting is ambiguous. Expected: one warning on the nested element's line; the diagram still renders.

- **Recognition:** `Nesting.In.Element`, `Validation.Nesting.Ambiguous`
- **Rendering:** `Layout.Compound.Element`, `Diagnostics.Warning`
- **Expected:** 1 warning (line 3)

```mermaid
enterprise
  application-component shop "Web Shop"
  application-function checkout "Checkout" in shop
  application-service ordering "Online Ordering"
  checkout -realization-> ordering
```

---

## Level 13 — Cross-layer and derived relationships

### A66 — Application supports the business layer

Application services serve business processes and roles, an application process realises a business process, and a data object realises a business object.

- **Recognition:** `Relationship.CrossLayer.ApplicationToBusiness`, `Relationship.Serving`, `Relationship.Realization`
- **Rendering:** `Layout.Layer.Rank.BusinessAboveApplication`
- **Expected:** valid

```mermaid
enterprise
  business-role customer "Customer"
  business-process orderGoods "Order Goods"
  business-object orderBo "Order"
  application-service ordering "Online Ordering"
  application-process autoOrder "Automated Re-order"
  application-component shop "Web Shop"
  data-object orderDo "Order Record"
  ordering -serving-> orderGoods
  ordering -serving-> customer
  autoOrder -realization-> orderGoods
  orderDo -realization-> orderBo
  shop -realization-> ordering
  shop -assignment-> autoOrder
```

### A67 — Technology supports the application layer

Technology services serve application components, artifacts realise a component and a data object, and a node serves an application directly.

- **Recognition:** `Relationship.CrossLayer.TechnologyToApplication`, `Relationship.Serving`, `Relationship.Realization`
- **Rendering:** `Layout.Layer.Rank.ApplicationAboveTechnology`
- **Expected:** valid

```mermaid
enterprise
  application-component shop "Web Shop"
  data-object orderDo "Order Record"
  technology-service hosting "Container Hosting"
  technology-service dbSvc "Managed Database"
  node k8s "Container Platform"
  system-software db "Relational Database"
  artifact image "shop-image:2.4"
  artifact orderTable "orders table"
  hosting -serving-> shop
  dbSvc -serving-> shop
  k8s -realization-> hosting
  db -realization-> dbSvc
  image -realization-> shop
  orderTable -realization-> orderDo
  k8s -assignment-> image
  db -assignment-> orderTable
  k8s -serving-> shop
```

### A68 — Core elements realise motivation elements

Requirements are realised by application and business elements; a stakeholder and a driver are associated with the goal the requirements serve.

- **Recognition:** `Relationship.CrossLayer.CoreToMotivation`, `Relationship.Realization`, `Relationship.Influence`
- **Rendering:** `Layout.Layer.Rank.MotivationOnTop`
- **Expected:** valid

```mermaid
enterprise
  stakeholder hrDirector "HR Director"
  driver awardChange "Award Rate Changes"
  goal accuratePay "Accurate Pay"
  requirement autoRates "Apply Award Rates Automatically"
  requirement auditTrail "Keep an Audit Trail"
  application-service rateSvc "Rate Calculation"
  business-process payRun "Run Payroll"
  application-component payroll "Payroll System"
  hrDirector -association-> accuratePay
  awardChange -influence(+)-> accuratePay
  autoRates -realization-> accuratePay
  auditTrail -realization-> accuratePay
  rateSvc -realization-> autoRates
  payRun -realization-> auditTrail
  payroll -realization-> rateSvc
```

### A69 — Strategy and implementation linked to the core

Capabilities realised by business and application elements, and a work package whose deliverable realises an application component and a plateau.

- **Recognition:** `Relationship.CrossLayer.StrategyToCore`, `Relationship.CrossLayer.ImplementationToCore`
- **Rendering:** `Layout.Layer.Rank.ImplementationAtBottom`
- **Expected:** valid

```mermaid
enterprise
  capability payrollCap "Payroll Management"
  resource payData "Employee Pay Data"
  business-function payrollFn "Payroll"
  application-component payroll "Cloud Payroll"
  data-object employee "Employee Record"
  work-package implement "Implement Cloud Payroll"
  deliverable configured "Configured Tenant"
  plateau target "Target State"
  payrollFn -realization-> payrollCap
  payroll -realization-> payrollCap
  payData -assignment-> payrollCap
  employee -realization-> payData
  implement -realization-> configured
  configured -realization-> payroll
  target -aggregation-> payroll
  configured -realization-> target
```

### A70 — Derived serving shortcuts beside the full chain

The full chain node → technology service → component → function → application service → business process, plus three derived shortcuts the rules allow (node serves the component, the technology service serves the business process, the component serves the business process).

- **Recognition:** `Relationship.Derived.Serving`, `Relationship.CrossLayer.*`
- **Rendering:** `Routing.Parallel.LongAndShort`, `Layout.Layer.Rank.*`
- **Expected:** valid

```mermaid
enterprise
  node server "Shop Server"
  technology-service hosting "Hosting"
  application-component shop "Web Shop"
  application-function checkout "Checkout"
  application-service ordering "Online Ordering"
  business-process buy "Buy Goods"
  server -realization-> hosting
  hosting -serving-> shop
  shop -assignment-> checkout
  checkout -realization-> ordering
  ordering -serving-> buy
  server -serving-> shop "derived"
  hosting -serving-> buy "derived"
  shop -serving-> buy "derived"
```

### A71 — Derived realisation

An artifact realises a component, the component is assigned to a function that realises a service; the derived realisations (artifact realises the service, component realises the service) are drawn as well.

- **Recognition:** `Relationship.Derived.Realization`
- **Rendering:** `Routing.Parallel.LongAndShort`
- **Expected:** valid

```mermaid
enterprise
  artifact jar "payroll.jar"
  application-component payroll "Payroll Engine"
  application-function calc "Calculate Pay"
  application-service paySvc "Pay Calculation"
  jar -realization-> payroll
  payroll -assignment-> calc
  calc -realization-> paySvc
  jar -realization-> paySvc "derived"
  payroll -realization-> paySvc "derived"
```

### A72 — Derived flow and triggering between active structure

Flows and triggers between functions and processes, with the derived flow and triggering between the components and roles that perform them.

- **Recognition:** `Relationship.Derived.Flow`, `Relationship.Derived.Triggering`
- **Rendering:** `Routing.Parallel.LongAndShort`
- **Expected:** valid

```mermaid
enterprise
  application-component shop "Web Shop"
  application-component erp "ERP"
  application-function checkout "Checkout"
  application-function invoicing "Invoicing"
  business-role picker "Picker"
  business-role packer "Packer"
  business-process pick "Pick"
  business-process pack "Pack"
  shop -assignment-> checkout
  erp -assignment-> invoicing
  checkout -flow-> invoicing
  shop -flow-> erp "derived"
  picker -assignment-> pick
  packer -assignment-> pack
  pick -triggering-> pack
  picker -triggering-> packer "derived"
```


---

## Level 14 — Styling and notation

### A73 — Every outline kind in one diagram

One element of each distinct outline: square box, rounded behaviour box, object with header band, device 3D box, equipment 3D box, contract, representation, value ellipse, location, grouping and junction. This is the notation smoke test for the shape geometry.

- **Recognition:** `Element.*`, `Group.Declaration`
- **Rendering:** `Render.Box.Square`, `Render.Box.Rounded`, `Render.Box.Object`, `Render.Box.Device3D`, `Render.Box.Contract`, `Render.Box.Representation`, `Render.Box.Ellipse`, `Render.Group.DashedTab`, `Render.Junction.Filled`
- **Expected:** valid

```mermaid
enterprise
  direction LR
  group legend "Outline kinds"
  application-component square "Square box" in legend
  application-service rounded "Rounded box" in legend
  data-object object "Object box" in legend
  device box3d "Device box" in legend
  equipment equip "Equipment box" in legend
  contract contractBox "Contract box" in legend
  representation wave "Representation box" in legend
  value ellipse "Value ellipse" in legend
  location place "Location box" in legend
  and-junction dot in legend
```

### A74 — Every relationship line and end in one diagram

Each relationship type once, plus the four access variants, directed association and a signed influence, between pairs of elements laid out side by side. This is the notation smoke test for line styles and end marks drawn as plain paths.

- **Recognition:** `Relationship.*`, `Relationship.Access.*`, `Relationship.Association.Directed`, `Relationship.Influence.Positive`
- **Rendering:** `Render.Line.Solid`, `Render.Line.Dotted`, `Render.Line.Dashed`, `Render.End.*`, `Render.NoSvgMarker`
- **Expected:** valid

```mermaid
enterprise
  direction LR
  application-component c1 "Whole"
  application-component c2 "Part"
  application-component g1 "Collection"
  application-component g2 "Member"
  application-component i1 "Performer"
  application-function i2 "Behaviour"
  application-function r1 "Realiser"
  application-service r2 "Realised"
  application-service v1 "Server"
  application-process v2 "Served"
  application-process a1 "Writer"
  data-object a2 "Written"
  application-process a3 "Reader"
  data-object a4 "Read"
  application-process a5 "Updater"
  data-object a6 "Updated"
  application-process a7 "Accessor"
  data-object a8 "Accessed"
  driver n1 "Driver"
  goal n2 "Goal"
  application-event t1 "Trigger"
  application-process t2 "Triggered"
  application-function f1 "Sender"
  application-function f2 "Receiver"
  application-component s1 "Specific"
  application-component s2 "General"
  application-component o1 "Associated"
  data-object o2 "Associate"
  application-component o3 "Directed from"
  data-object o4 "Directed to"
  c1 -composition-> c2
  g1 -aggregation-> g2
  i1 -assignment-> i2
  r1 -realization-> r2
  v1 -serving-> v2
  a1 -access(write)-> a2
  a3 -access(read)-> a4
  a5 -access(readwrite)-> a6
  a7 -access(access)-> a8
  n1 -influence(+)-> n2
  t1 -triggering-> t2
  f1 -flow-> f2
  s1 -specialization-> s2
  o1 -association-> o2
  o3 -association(directed)-> o4
```

### A75 — `classDef` and `class` styling hooks

`classDef` defines a named set of style properties (several class names may share one definition) and `class` applies a class to one or more elements, as in Mermaid flowcharts. An element may receive several classes; later properties override earlier ones. Styling changes colours, line width and the dash pattern of the outline, never the outline shape or icon. The `:::` shorthand is covered in `A124`.

- **Recognition:** `Style.ClassDef`, `Style.ClassDef.List`, `Style.ClassApplication`, `Style.ClassApplication.List`
- **Rendering:** `Render.Style.Fill`, `Render.Style.Stroke`, `Render.Style.StrokeDash`
- **Expected:** valid

```mermaid
enterprise
  application-component shop "Web Shop"
  application-component legacy "Legacy Shop"
  application-component outlet "Outlet Shop"
  legacy -specialization-> shop
  outlet -specialization-> shop
  classDef retiring fill:#eeeeee,stroke:#999999,stroke-dasharray:4 2
  classDef highlight,focus stroke:#c00000,stroke-width:2px
  class legacy,outlet retiring
  class shop highlight
  class outlet focus
```

### A76 — `style` on a single element and `linkStyle` on relationships

`style` sets properties on one element. Relationships have no ids, so `linkStyle` addresses them by position, counting relationship lines from 0 in the order they appear, as Mermaid flowcharts do. `linkStyle` accepts the same properties as Mermaid links, including a dash pattern, which then replaces the relationship's own line pattern; the end marks always follow the relationship type. More `linkStyle` forms are in `A128`.

- **Recognition:** `Style.Element`, `Style.Relationship.Index`, `Style.Relationship.IndexList`
- **Rendering:** `Render.Style.Fill`, `Render.Style.Stroke`, `Render.Relationship.Colour`, `Render.Relationship.Dash`
- **Expected:** valid

```mermaid
enterprise
  application-component shop "Web Shop"
  application-service ordering "Online Ordering"
  business-process take "Take Order"
  business-role clerk "Order Clerk"
  shop -realization-> ordering
  ordering -serving-> take
  clerk -assignment-> take
  style shop fill:#ffd7d7,stroke:#c00000,stroke-width:2px
  linkStyle 0 stroke:#c00000
  linkStyle 1,2 stroke:#0070c0,stroke-width:2px,color:#0070c0,stroke-dasharray:6 2
```

### A77 — Icon (large-symbol) notation selected through Mermaid configuration

The specification allows each element to be drawn either as a box with a small corner icon or as the large symbol on its own. Like any rendering option in Mermaid, the choice is made in the configuration, here in the frontmatter (`config.enterprise.notation`, `box` by default or `icon`), so no new statement is needed. Until the renderer can draw the large symbols it may ignore the option and draw boxes; the diagram is valid either way.

- **Recognition:** `Config.Frontmatter`, `Config.Enterprise.Notation`
- **Rendering:** `Render.Notation.Icon`, `Render.Notation.Box.Fallback`
- **Expected:** valid

```mermaid
---
config:
  enterprise:
    notation: icon
---
enterprise
  application-interface api "Shop API"
  application-service ordering "Online Ordering"
  node server "Shop Server"
  api -assignment-> ordering
```

---

## Level 15 — Invalid relationships and validation errors

Every scenario in this level except `A85` (the valid 3.1 counterpart of the scenario before it) is **expected to fail validation**. The `Expected` line gives the number of errors and the block-relative line numbers (line 1 is the `enterprise` line; comment and blank lines count). The renderer must follow the repository's behaviour for diagrams that fail to parse (an error placeholder or message in the DOCX), never a silent drop.

### A78 — Passive structure cannot serve

A data object serving a component: only association is allowed from a data object.

- **Recognition:** `Validation.Rule.Disallowed`, `Relationship.Serving`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 1 error (line 4)

```mermaid
enterprise
  data-object order "Order"
  application-component shop "Web Shop"
  order -serving-> shop
```

### A79 — Structural relationships in the wrong direction

A service composed of a component, a role assigned to an actor (the actor is assigned to the role, not the reverse), an artifact assigned to a component, and a component realising a node.

- **Recognition:** `Validation.Rule.Disallowed`, `Relationship.Composition`, `Relationship.Assignment`, `Relationship.Realization`
- **Rendering:** `Diagnostics.Error.Placeholder`, `Diagnostics.Error.Multiple`
- **Expected:** 4 errors (lines 8, 9, 10, 11)

```mermaid
enterprise
  application-service ordering "Online Ordering"
  application-component shop "Web Shop"
  business-role buyer "Buyer"
  business-actor customer "Customer"
  artifact war "shop.war"
  node server "Shop Server"
  ordering -composition-> shop
  buyer -assignment-> customer
  war -assignment-> shop
  shop -realization-> server
```

### A80 — Access to something that is not passive structure

Access may only target a passive element (data object, business object, artifact and similar). Accessing a node or an actor is an error.

- **Recognition:** `Validation.Rule.Disallowed`, `Relationship.Access.Read`, `Relationship.Access.Write`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 2 errors (lines 6, 7)

```mermaid
enterprise
  technology-service hosting "Hosting"
  node server "Shop Server"
  business-process onboard "Onboard Customer"
  business-actor customer "Customer"
  hosting -access(read)-> server
  onboard -access(write)-> customer
```

### A81 — Influence between core elements

Influence is a motivation relationship. Between two application or technology elements the rule table offers no influence cell, so it is always rejected.

- **Recognition:** `Validation.Rule.Disallowed`, `Relationship.Influence`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 2 errors (lines 6, 7)

```mermaid
enterprise
  application-component shop "Web Shop"
  application-component erp "ERP"
  node server "Shop Server"
  technology-service hosting "Hosting"
  shop -influence(+)-> erp
  server -influence-> hosting
```

### A82 — Motivation and passive elements cannot trigger or be assigned

A goal triggering a process, a requirement assigned to an actor and a business object triggering a process: each pair only allows association.

- **Recognition:** `Validation.Rule.Disallowed`, `Relationship.Triggering`, `Relationship.Assignment`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 3 errors (lines 7, 8, 9)

```mermaid
enterprise
  goal growth "Grow Sales"
  requirement mustLog "Log Every Order"
  business-process takeOrder "Take Order"
  business-actor clerk "Order Clerk"
  business-object orderForm "Order Form"
  growth -triggering-> takeOrder
  mustLog -assignment-> clerk
  orderForm -triggering-> takeOrder
```

### A83 — A derivation that the rules do not permit

The chain node → technology service → component is valid, but deriving serving onto a passive data object is not: from a node or a technology service to a data object only access and association are allowed.

- **Recognition:** `Validation.Rule.Disallowed`, `Relationship.Derived.Invalid`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 2 errors (lines 9, 10)

```mermaid
enterprise
  node server "Shop Server"
  technology-service hosting "Hosting"
  application-component shop "Web Shop"
  data-object order "Order"
  server -realization-> hosting
  hosting -serving-> shop
  shop -association-> order
  server -serving-> order "derived?"
  hosting -serving-> order "derived?"
```

### A84 — 3.2: a device is no longer a specialisation of a node

Under the default 3.2 rules, specialisation from Device to Node is not allowed (the cell is one of the provisional 3.2 cells, so the message carries the provisional note).

- **Recognition:** `Validation.Rule.Disallowed`, `Validation.Version.3_2.Change`, `Validation.Provisional`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 1 error (line 4)

```mermaid
enterprise
  device rackServer "Rack Server"
  node webNode "Web Node"
  rackServer -specialization-> webNode
```

### A85 — 3.1: the same specialisation is valid

The previous diagram with `version 3.1`. It must validate cleanly, which proves the version switch selects a different rule table.

- **Recognition:** `Directive.Version.3_1`, `Validation.Version.3_1`
- **Rendering:** `Render.End.TriangleHollow`
- **Expected:** valid

```mermaid
enterprise
  version 3.1
  device rackServer "Rack Server"
  node webNode "Web Node"
  rackServer -specialization-> webNode
```

### A86 — 3.2: an artifact can no longer realise a node or a device

Two provisional 3.2 cells: artifact to node and artifact to device lose realisation (both are allowed under 3.1).

- **Recognition:** `Validation.Rule.Disallowed`, `Validation.Version.3_2.Change`, `Validation.Provisional`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 2 errors (lines 6, 7)

```mermaid
enterprise
  version 3.2
  artifact image "vm-image.qcow2"
  node vm "Virtual Machine"
  device kiosk "Store Kiosk"
  image -realization-> vm
  image -realization-> kiosk
```

### A87 — 3.1: an artifact cannot realise another artifact

The rule change in the other direction: artifact to artifact realisation is new in 3.2, so under 3.1 it is an error.

- **Recognition:** `Validation.Rule.Disallowed`, `Validation.Version.3_1`, `Directive.Version.3_1`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 1 error (line 5)

```mermaid
enterprise
  version 3.1
  artifact source "payroll-src.zip"
  artifact build "payroll.jar"
  build -realization-> source
```

### A88 — 3.2: several relationships removed from 3.1 at once

System software assigned to a device, a communication network aggregating a node, equipment assigned to an actor and a device realising a role are all valid under 3.1 but errors under 3.2. The first two are provisional Application/Technology cells; the last two involve other layers, whose cells are all provisional.

- **Recognition:** `Validation.Rule.Disallowed`, `Validation.Version.3_2.Change`, `Diagnostics.Error.Multiple`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 4 errors (lines 9, 10, 11, 12)

```mermaid
enterprise
  system-software os "Embedded OS"
  device till "Point-of-Sale Till"
  communication-network storeLan "Store LAN"
  node storeServer "Store Server"
  equipment forklift "Forklift"
  business-actor driverStaff "Warehouse Driver"
  business-role cashier "Cashier"
  os -assignment-> till
  storeLan -aggregation-> storeServer
  forklift -assignment-> driverStaff
  till -realization-> cashier
```

### A89 — Unknown element type and unknown relationship type

A misspelt element type and an invented relationship name. Both are parse errors with line numbers.

- **Recognition:** `Diagnostics.UnknownElementType`, `Diagnostics.UnknownRelationship`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 2 errors (lines 3, 5)

```mermaid
enterprise
  application-component shop "Web Shop"
  application-servise ordering "Online Ordering"
  application-service orderSvc "Order Service"
  orderSvc -supports-> shop
```

### A90 — Conflicting redeclaration, reserved word as id and undeclared references

An id declared again with a different element type, a relationship from an undeclared id, one to an undeclared id, and an element whose id is the reserved word `class`. Declaring an id again with the same type is allowed, as in Mermaid (see `A127`), but an element cannot change its type. Relationships must name declared elements, because every element needs an element type.

- **Recognition:** `Diagnostics.Redeclaration.TypeConflict`, `Diagnostics.UndeclaredSource`, `Diagnostics.UndeclaredTarget`, `Diagnostics.ReservedId`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 4 errors (lines 3, 5, 6, 7)

```mermaid
enterprise
  application-component shop "Web Shop"
  application-service shop "Second Web Shop"
  application-service ordering "Online Ordering"
  basket -serving-> shop
  ordering -serving-> checkout
  data-object class "Class list"
```

### A91 — Invalid qualifiers

A sign on access, a word on influence, a direction word that association does not accept, and a qualifier on a relationship that takes none.

- **Recognition:** `Diagnostics.Qualifier.Invalid`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 4 errors (lines 6, 7, 8, 9)

```mermaid
enterprise
  application-process place "Place Order"
  data-object order "Order"
  driver growth "Growth"
  goal sales "More Sales"
  place -access(+)-> order
  growth -influence(strong)-> sales
  place -association(both)-> order
  place -triggering(fast)-> place
```

### A92 — Directive values outside the defined set

`direction` accepts the Mermaid values `TB`, `TD`, `BT`, `LR` and `RL`; `version` accepts only `3.1` and `3.2`. `direction XY`, `version 4.0` and `version 3` are errors.

- **Recognition:** `Diagnostics.Direction.Invalid`, `Diagnostics.Version.Invalid`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 3 errors (lines 2, 3, 4)

```mermaid
enterprise
  direction XY
  version 4.0
  version 3
  application-component shop "Web Shop"
```

### A93 — `in` naming a container that does not exist

The nested element points at an id that is never declared.

- **Recognition:** `Diagnostics.Nesting.UnknownParent`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 1 error (line 3)

```mermaid
enterprise
  group dc "Primary data centre"
  node server "Shop Server" in dataCentre
  system-software runtime "Java Runtime" in server
  server -composition-> runtime
```

### A94 — Line numbers count comments and blank lines

Valid and invalid statements mixed with comments and blank lines. The reported line numbers must be relative to the block, counting every line.

- **Recognition:** `Diagnostics.LineNumber.BlockRelative`, `Recognition.Comment.FullLine`, `Recognition.BlankLine`
- **Rendering:** `Diagnostics.Error.Placeholder`, `Diagnostics.Error.Multiple`
- **Expected:** 3 errors (lines 12, 15, 18)

```mermaid
enterprise
  %% elements
  application-component shop "Web Shop"
  application-service ordering "Online Ordering"
  data-object order "Order"

  node server "Shop Server"
  %% valid
  shop -realization-> ordering

  %% invalid: data object serving
  order -serving-> shop
  %% valid
  server -serving-> shop
  ordering -composition-> order

  %% invalid: component realising a node
  shop -realization-> server
```

---

## Level 16 — Recognition edge cases

### A95 — Comments and blank lines everywhere

Full-line comments before the header, between statements and at the end; comments with and without a space after `%%`; leading, repeated and trailing blank lines (some containing only spaces).

- **Recognition:** `Recognition.Comment.FullLine`, `Recognition.Comment.BeforeHeader`, `Recognition.BlankLine`, `Recognition.BlankLine.Whitespace`
- **Rendering:** `Render.Element.Box`
- **Expected:** valid

```mermaid
%% catalogue scenario: comments before the header

enterprise
%%no space after the comment marker
  %% indented comment

  application-component shop "Web Shop"
    
  %% a comment between two statements


  application-service ordering "Online Ordering"
  shop -realization-> ordering
%% final comment

```

### A96 — Trailing comments after statements

A `%%` comment may follow any statement, including the header, as in Mermaid. Everything from `%%` to the end of the line is ignored, except inside a quoted label, where `%%` is text.

- **Recognition:** `Recognition.Comment.Trailing`, `Recognition.Comment.InsideString`
- **Rendering:** `Render.Element.Box`, `Render.Relationship.Serving`
- **Expected:** valid

```mermaid
enterprise %% header
  direction LR %% left to right
  application-component shop "Web Shop" %% the main shop
  application-service ordering "Online Ordering"%%no space before the comment
  application-service sale "Sale 50%% off" %% the first %% is in the label, this one is a comment
  ordering -serving-> shop %% serving
  shop -realization-> sale "%%" %% a label that is only two percent signs
```

### A97 — Whitespace variants

Tabs instead of spaces, no indentation, deep indentation, several spaces between tokens, column alignment and trailing spaces. All tokens are separated by at least one whitespace character, as the grammar requires.

- **Recognition:** `Recognition.Whitespace.Tabs`, `Recognition.Whitespace.Multiple`, `Recognition.Whitespace.Trailing`, `Recognition.Indentation.None`
- **Rendering:** `Render.Element.Box`
- **Expected:** valid

```mermaid
enterprise
application-component	shop	"Web Shop"
        application-service     ordering        "Online Ordering"   
	data-object order "Order"
  application-function   checkout   "Checkout"  
shop     -assignment->     checkout
	checkout	-realization->	ordering
  checkout -access(write)->   order    "creates"   
```

### A98 — Specification CamelCase element type names

The design accepts the specification's CamelCase names alongside kebab-case. Both spellings in one diagram must give the same element types.

- **Recognition:** `Element.Type.CamelCase`, `Element.Type.KebabCase`
- **Rendering:** `Render.Icon.*`
- **Expected:** valid

```mermaid
enterprise
  ApplicationComponent shop "Web Shop"
  application-service ordering "Online Ordering"
  BusinessActor customer "Customer"
  BusinessRole buyer "Buyer"
  TechnologyService hosting "Hosting"
  DataObject order "Order"
  shop -realization-> ordering
  customer -assignment-> buyer
  ordering -serving-> buyer
  hosting -serving-> shop
  shop -access(read)-> order
```

### A99 — Relationships and `in` references before declarations

Every relationship comes before the elements it connects, and a nested element names its container before the container is declared. References are resolved after the whole block is read.

- **Recognition:** `Recognition.ForwardReference`, `Nesting.ForwardReference`
- **Rendering:** `Layout.TopologyNotDeclarationOrder`
- **Expected:** valid

```mermaid
enterprise
  shop -realization-> ordering
  ordering -serving-> takeOrder
  server -assignment-> war
  war -realization-> shop
  artifact war "shop.war" in server
  node server "Shop Server"
  business-process takeOrder "Take Order"
  application-service ordering "Online Ordering"
  application-component shop "Web Shop"
```

### A100 — Identifier forms

Identifiers with underscores, digits, dots, hyphens, a leading underscore, a leading digit and non-Latin letters; identifiers that start with a reserved word (`titleBar`, `versionStore`, `directionSvc`, `groupware`, `inbox`); and an element whose id is the same word as its type. As in Mermaid flowcharts, an id may use any letters, digits, `_`, `.` and `-`, but must not start with `.` or `-`. A whole reserved word (see Syntax decisions) is an error, tested in `A90`.

- **Recognition:** `Element.Id.Underscore`, `Element.Id.Digits`, `Element.Id.LeadingDigit`, `Element.Id.Dot`, `Element.Id.Hyphen`, `Element.Id.Unicode`, `Element.Id.KeywordPrefix`, `Element.Id.SameAsType`
- **Rendering:** `Render.Element.Box`
- **Expected:** valid

```mermaid
enterprise
  application-component order_service_v2 "Order Service v2"
  application-component _internal "Internal Adapter"
  data-object db.orders.2026 "Orders 2026"
  application-component titleBar "Title Bar Widget"
  data-object versionStore "Version Store"
  application-service directionSvc "Directions"
  application-collaboration groupware "Groupware Suite"
  data-object inbox "Inbox"
  node node "Node"
  order_service_v2 -access(write)-> db.orders.2026
  _internal -flow-> order_service_v2
  titleBar -access(read)-> versionStore
  titleBar -realization-> directionSvc
  groupware -aggregation-> titleBar
  _internal -access(readwrite)-> inbox
  node -serving-> order_service_v2
  application-component order-api "Order API"
  data-object 2026-orders "Orders 2026"
  application-component 注文 "Order Desk"
  order-api -access(write)-> 2026-orders
  order-api -flow-> 注文
```

### A101 — Semicolons as statement separators

As in Mermaid flowcharts, `;` may end a statement or separate several statements on one line. A `;` inside a quoted label is ordinary text. Statements that share a line share its line number in diagnostics.

- **Recognition:** `Recognition.Statement.SemicolonBoundary`, `Recognition.Statement.SemicolonTrailing`, `Element.Label.SpecialCharacters`
- **Rendering:** `Render.Element.Box`, `Render.Relationship.Realization`
- **Expected:** valid

```mermaid
enterprise
  application-component shop "Web Shop"; application-service ordering "Online Ordering";
  shop -realization-> ordering;
  application-component notes "Semicolons; inside labels; are text"
  classDef muted fill:#eeeeee;
  class notes muted;
```

### A102 — Quoted identifiers are errors

Identifiers are bare words; spaces belong in the label. A quoted identifier is an error wherever it appears; Mermaid flowcharts do not accept quoted node ids either. The last line shows the accepted form.

- **Recognition:** `Diagnostics.Id.Quoted`, `Diagnostics.CannotParse`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 3 errors (lines 2, 3, 4)

```mermaid
enterprise
  application-component "web shop" "Web Shop"
  application-service "online ordering"
  "web shop" -realization-> "online ordering"
  application-component webShop "web shop"
```

### A103 — Header alias and letter case

The header may be `enterprise` or `enterprise-beta` (the Mermaid convention for a new diagram type), in any letter case. Directive keywords, element type names and relationship names are also case-insensitive, and the specification's CamelCase type names are accepted. Identifiers stay case-sensitive. No other diagram header is accepted.

- **Recognition:** `Header.Enterprise.Alias`, `Header.Enterprise.Case`, `Directive.Case`, `Element.Type.Case`, `Relationship.Case`
- **Rendering:** `Render.Element.Box`, `Layout.Direction.LR`
- **Expected:** valid

```mermaid
Enterprise-Beta
  TITLE Mixed-case keywords
  Direction lr
  Version 3.2
  Application-Component shop "Web Shop"
  ApplicationService ordering "Online Ordering"
  application-service Ordering "Second service with a capitalised id"
  shop -Realization-> ordering
  shop -R-> Ordering
```

---

## Level 17 — Layout stress

### A104 — High fan-out: one service serving sixteen processes

A single application service serves sixteen business processes. The source port allocation, edge spacing and the width of the business row are under test.

- **Recognition:** `Relationship.Serving`, `Element.Business.Process`
- **Rendering:** `Layout.FanOut.High`, `Routing.Ports.Distribution`
- **Expected:** valid

```mermaid
enterprise
  application-service customerSvc "Customer Account Service"
  business-process registerAccount "Register Account"
  business-process signIn "Sign In"
  business-process resetPassword "Reset Password"
  business-process updateAddress "Update Address"
  business-process browseCatalogue "Browse Catalogue"
  business-process saveWishList "Save Wish List"
  business-process placeOrder "Place Order"
  business-process trackDelivery "Track Delivery"
  business-process requestReturn "Request Return"
  business-process redeemVoucher "Redeem Voucher"
  business-process joinLoyaltyScheme "Join Loyalty Scheme"
  business-process leaveReview "Leave Review"
  business-process manageSubscriptions "Manage Subscriptions"
  business-process downloadInvoice "Download Invoice"
  business-process closeAccount "Close Account"
  business-process contactSupport "Contact Support"
  customerSvc -serving-> registerAccount
  customerSvc -serving-> signIn
  customerSvc -serving-> resetPassword
  customerSvc -serving-> updateAddress
  customerSvc -serving-> browseCatalogue
  customerSvc -serving-> saveWishList
  customerSvc -serving-> placeOrder
  customerSvc -serving-> trackDelivery
  customerSvc -serving-> requestReturn
  customerSvc -serving-> redeemVoucher
  customerSvc -serving-> joinLoyaltyScheme
  customerSvc -serving-> leaveReview
  customerSvc -serving-> manageSubscriptions
  customerSvc -serving-> downloadInvoice
  customerSvc -serving-> closeAccount
  customerSvc -serving-> contactSupport
```

### A105 — High fan-in: sixteen feeds into one ledger

Sixteen application components each send a labelled flow to the general ledger. Target port allocation and the placement of sixteen identical labels are under test.

- **Recognition:** `Relationship.Flow`, `Relationship.Label`
- **Rendering:** `Layout.FanIn.High`, `Routing.Ports.Distribution`, `Layout.Label.Collision`
- **Expected:** valid

```mermaid
enterprise
  application-component ledger "General Ledger"
  application-component webShop "Web Shop"
  application-component mobileApp "Mobile App"
  application-component pointOfSale "Point of Sale"
  application-component marketplaceConnector "Marketplace Connector"
  application-component payroll "Payroll"
  application-component expenses "Expenses"
  application-component accountsPayable "Accounts Payable"
  application-component accountsReceivable "Accounts Receivable"
  application-component fixedAssets "Fixed Assets"
  application-component inventory "Inventory"
  application-component purchasing "Purchasing"
  application-component treasury "Treasury"
  application-component taxEngine "Tax Engine"
  application-component bankFeed "Bank Feed"
  application-component cardSettlement "Card Settlement"
  application-component giftCardPlatform "Gift Card Platform"
  webShop -flow-> ledger "journal"
  mobileApp -flow-> ledger "journal"
  pointOfSale -flow-> ledger "journal"
  marketplaceConnector -flow-> ledger "journal"
  payroll -flow-> ledger "journal"
  expenses -flow-> ledger "journal"
  accountsPayable -flow-> ledger "journal"
  accountsReceivable -flow-> ledger "journal"
  fixedAssets -flow-> ledger "journal"
  inventory -flow-> ledger "journal"
  purchasing -flow-> ledger "journal"
  treasury -flow-> ledger "journal"
  taxEngine -flow-> ledger "journal"
  bankFeed -flow-> ledger "journal"
  cardSettlement -flow-> ledger "journal"
  giftCardPlatform -flow-> ledger "journal"
```

### A106 — Long chain: thirty triggering steps

An order-to-delivery chain of thirty business processes and events, laid out left to right. Rank count, page width and wrapping of a very wide drawing are under test.

- **Recognition:** `Relationship.Triggering`, `Direction.LR`
- **Rendering:** `Layout.Chain.Long`, `Layout.Direction.LR`, `Render.Scale.FitPage`
- **Expected:** valid

```mermaid
enterprise
  direction LR
  business-event orderReceived "Order Received"
  business-process validateOrder "Validate Order"
  business-process checkCredit "Check Credit"
  business-process reserveStock "Reserve Stock"
  business-process takePayment "Take Payment"
  business-event paymentConfirmed "Payment Confirmed"
  business-process createPickList "Create Pick List"
  business-process pickItems "Pick Items"
  business-process qualityCheck "Quality Check"
  business-process packParcel "Pack Parcel"
  business-process weighParcel "Weigh Parcel"
  business-process printLabel "Print Label"
  business-event parcelPacked "Parcel Packed"
  business-process bookCourier "Book Courier"
  business-process handToCourier "Hand to Courier"
  business-event parcelDispatched "Parcel Dispatched"
  business-process sendDispatchEmail "Send Dispatch Email"
  business-process lineHaul "Line Haul"
  business-process arriveAtDepot "Arrive at Depot"
  business-process sortParcel "Sort Parcel"
  business-process loadVan "Load Van"
  business-process outForDelivery "Out for Delivery"
  business-process attemptDelivery "Attempt Delivery"
  business-process captureSignature "Capture Signature"
  business-event parcelDelivered "Parcel Delivered"
  business-process sendDeliveryEmail "Send Delivery Email"
  business-process requestFeedback "Request Feedback"
  business-process recordFeedback "Record Feedback"
  business-process closeOrder "Close Order"
  business-event orderClosed "Order Closed"
  orderReceived -triggering-> validateOrder
  validateOrder -triggering-> checkCredit
  checkCredit -triggering-> reserveStock
  reserveStock -triggering-> takePayment
  takePayment -triggering-> paymentConfirmed
  paymentConfirmed -triggering-> createPickList
  createPickList -triggering-> pickItems
  pickItems -triggering-> qualityCheck
  qualityCheck -triggering-> packParcel
  packParcel -triggering-> weighParcel
  weighParcel -triggering-> printLabel
  printLabel -triggering-> parcelPacked
  parcelPacked -triggering-> bookCourier
  bookCourier -triggering-> handToCourier
  handToCourier -triggering-> parcelDispatched
  parcelDispatched -triggering-> sendDispatchEmail
  sendDispatchEmail -triggering-> lineHaul
  lineHaul -triggering-> arriveAtDepot
  arriveAtDepot -triggering-> sortParcel
  sortParcel -triggering-> loadVan
  loadVan -triggering-> outForDelivery
  outForDelivery -triggering-> attemptDelivery
  attemptDelivery -triggering-> captureSignature
  captureSignature -triggering-> parcelDelivered
  parcelDelivered -triggering-> sendDeliveryEmail
  sendDeliveryEmail -triggering-> requestFeedback
  requestFeedback -triggering-> recordFeedback
  recordFeedback -triggering-> closeOrder
  closeOrder -triggering-> orderClosed
```

### A107 — Cycles: a six-step ring, a back edge, a two-cycle, a self-loop and a cycle through a junction

Feedback structures that a layered layout must break: a six-process triggering ring with an extra back edge, a pair of flows in opposite directions, a relationship from an element to itself and a loop through an or-junction.

- **Recognition:** `Relationship.Triggering`, `Relationship.Flow`, `Relationship.SelfLoop`, `Element.Junction.Or`
- **Rendering:** `Layout.Cycle.Break`, `Routing.BackEdge`, `Routing.SelfLoop`
- **Expected:** valid

```mermaid
enterprise
  business-process draft "Draft Order"
  business-process submit "Submit Order"
  business-process approve "Approve Order"
  business-process fulfil "Fulfil Order"
  business-process invoice "Invoice Order"
  business-process review "Review Account"
  application-component shop "Web Shop"
  application-component erp "ERP"
  application-component retryQueue "Retry Queue"
  or-junction retry
  draft -triggering-> submit
  submit -triggering-> approve
  approve -triggering-> fulfil
  fulfil -triggering-> invoice
  invoice -triggering-> review
  review -triggering-> draft "next order"
  approve -triggering-> draft "rejected"
  shop -flow-> erp "order"
  erp -flow-> shop "status"
  retryQueue -flow-> retryQueue "retry"
  erp -flow-> retry
  retry -flow-> retryQueue
  retry -flow-> erp
```

### A108 — Many parallel relationships between the same pair

Nine relationships between the same two components, in both directions and of four types, most with labels. Edges must be fanned apart so none overlaps and every label stays readable.

- **Recognition:** `Relationship.Parallel`, `Relationship.Flow`, `Relationship.Triggering`, `Relationship.Serving`, `Relationship.Association`
- **Rendering:** `Routing.Parallel.Separation`, `Layout.Label.Collision`
- **Expected:** valid

```mermaid
enterprise
  application-component shop "Web Shop"
  application-component erp "ERP"
  shop -flow-> erp "orders"
  shop -flow-> erp "returns"
  shop -flow-> erp "customer changes"
  shop -triggering-> erp "end of day"
  shop -serving-> erp
  shop -association-> erp
  erp -flow-> shop "prices"
  erp -serving-> shop
  erp -flow-> shop "stock levels"
```

### A109 — Wide layer: thirty-six unconnected components

Thirty-six application components with no relationships. With nothing to rank them, the layout must still wrap or pack them into a readable block rather than one row that is far wider than the page.

- **Recognition:** `Element.Application.Component`, `Diagram.NoRelationships`
- **Rendering:** `Layout.Layer.Wide`, `Layout.Packing.Disconnected`, `Render.Scale.FitPage`
- **Expected:** valid

```mermaid
enterprise
  application-component webShop "Web Shop"
  application-component mobileApp "Mobile App"
  application-component kioskApp "Kiosk App"
  application-component callCentreDesktop "Call Centre Desktop"
  application-component crm "CRM"
  application-component loyalty "Loyalty"
  application-component vouchers "Vouchers"
  application-component search "Search"
  application-component recommendations "Recommendations"
  application-component productCatalogue "Product Catalogue"
  application-component pricing "Pricing"
  application-component promotions "Promotions"
  application-component basket "Basket"
  application-component checkout "Checkout"
  application-component payments "Payments"
  application-component fraudScreening "Fraud Screening"
  application-component orderManager "Order Manager"
  application-component inventory "Inventory"
  application-component warehouseSystem "Warehouse System"
  application-component transportPlanner "Transport Planner"
  application-component courierGateway "Courier Gateway"
  application-component returns "Returns"
  application-component invoicing "Invoicing"
  application-component generalLedger "General Ledger"
  application-component payroll "Payroll"
  application-component rostering "Rostering"
  application-component hr "HR"
  application-component identity "Identity"
  application-component notifications "Notifications"
  application-component reporting "Reporting"
  application-component dataWarehouse "Data Warehouse"
  application-component documentStore "Document Store"
  application-component contentManagement "Content Management"
  application-component chat "Chat"
  application-component reviews "Reviews"
  application-component supplierPortal "Supplier Portal"
```

### A110 — Dense cross-layer mesh

Six application services each serve all six business processes (a complete bipartite block of 36 edges); three components each realise four services; four technology services serve every component. Nineteen elements carry 60 relationships across three layers, so crossing reduction is under heavy load.

- **Recognition:** `Relationship.Serving`, `Relationship.Realization`, `Relationship.CrossLayer.*`
- **Rendering:** `Layout.CrossingReduction`, `Layout.Mesh.Dense`, `Layout.Layer.Rank.*`
- **Expected:** valid

```mermaid
enterprise
  business-process takeOrder "Take Order"
  business-process amendOrder "Amend Order"
  business-process cancelOrder "Cancel Order"
  business-process shipOrder "Ship Order"
  business-process returnOrder "Return Order"
  business-process refundOrder "Refund Order"
  application-service customerLookupSvc "Customer Lookup"
  application-service stockLookupSvc "Stock Lookup"
  application-service priceLookupSvc "Price Lookup"
  application-service paymentSvc "Payment"
  application-service shippingQuoteSvc "Shipping Quote"
  application-service notificationSvc "Notification"
  application-component webShop "Web Shop"
  application-component orderManager "Order Manager"
  application-component paymentHub "Payment Hub"
  technology-service containerHosting "Container Hosting"
  technology-service managedDatabase "Managed Database"
  technology-service messageQueue "Message Queue"
  technology-service objectStorage "Object Storage"
  customerLookupSvc -serving-> takeOrder
  customerLookupSvc -serving-> amendOrder
  customerLookupSvc -serving-> cancelOrder
  customerLookupSvc -serving-> shipOrder
  customerLookupSvc -serving-> returnOrder
  customerLookupSvc -serving-> refundOrder
  stockLookupSvc -serving-> takeOrder
  stockLookupSvc -serving-> amendOrder
  stockLookupSvc -serving-> cancelOrder
  stockLookupSvc -serving-> shipOrder
  stockLookupSvc -serving-> returnOrder
  stockLookupSvc -serving-> refundOrder
  priceLookupSvc -serving-> takeOrder
  priceLookupSvc -serving-> amendOrder
  priceLookupSvc -serving-> cancelOrder
  priceLookupSvc -serving-> shipOrder
  priceLookupSvc -serving-> returnOrder
  priceLookupSvc -serving-> refundOrder
  paymentSvc -serving-> takeOrder
  paymentSvc -serving-> amendOrder
  paymentSvc -serving-> cancelOrder
  paymentSvc -serving-> shipOrder
  paymentSvc -serving-> returnOrder
  paymentSvc -serving-> refundOrder
  shippingQuoteSvc -serving-> takeOrder
  shippingQuoteSvc -serving-> amendOrder
  shippingQuoteSvc -serving-> cancelOrder
  shippingQuoteSvc -serving-> shipOrder
  shippingQuoteSvc -serving-> returnOrder
  shippingQuoteSvc -serving-> refundOrder
  notificationSvc -serving-> takeOrder
  notificationSvc -serving-> amendOrder
  notificationSvc -serving-> cancelOrder
  notificationSvc -serving-> shipOrder
  notificationSvc -serving-> returnOrder
  notificationSvc -serving-> refundOrder
  webShop -realization-> customerLookupSvc
  orderManager -realization-> customerLookupSvc
  orderManager -realization-> stockLookupSvc
  paymentHub -realization-> stockLookupSvc
  paymentHub -realization-> priceLookupSvc
  webShop -realization-> priceLookupSvc
  webShop -realization-> paymentSvc
  orderManager -realization-> paymentSvc
  orderManager -realization-> shippingQuoteSvc
  paymentHub -realization-> shippingQuoteSvc
  paymentHub -realization-> notificationSvc
  webShop -realization-> notificationSvc
  containerHosting -serving-> webShop
  containerHosting -serving-> orderManager
  containerHosting -serving-> paymentHub
  managedDatabase -serving-> webShop
  managedDatabase -serving-> orderManager
  managedDatabase -serving-> paymentHub
  messageQueue -serving-> webShop
  messageQueue -serving-> orderManager
  messageQueue -serving-> paymentHub
  objectStorage -serving-> webShop
  objectStorage -serving-> orderManager
  objectStorage -serving-> paymentHub
```

### A111 — Deep nesting with crossing relationships

Two mirrored five-level containment trees (group, location, node, node, system software, artifact). Relationships cross between the deepest levels of the two trees and out to top-level elements, so edges must cross several container borders and titles.

- **Recognition:** `Nesting.Depth.5`, `Relationship.CrossContainer`, `Group.Declaration`
- **Rendering:** `Routing.CrossContainer.Deep`, `Routing.Obstacle.ContainerTitle`, `Layout.Compound.Deep`
- **Expected:** valid

```mermaid
enterprise
  group east "Data centre east"
  group west "Data centre west"
  location eastSite "East Site" in east
  location westSite "West Site" in west
  node eastCluster "East Cluster" in eastSite
  node westCluster "West Cluster" in westSite
  node eastHost "East Host" in eastCluster
  node westHost "West Host" in westCluster
  system-software eastDb "Database Primary" in eastHost
  system-software westDb "Database Replica" in westHost
  artifact eastData "orders.db" in eastDb
  artifact westData "orders-replica.db" in westDb
  system-software eastWeb "Web Runtime" in eastHost
  system-software westWeb "Web Runtime (standby)" in westHost
  communication-network wan "Inter-site WAN"
  technology-service dbSvc "Database Service"
  application-component shop "Web Shop"
  eastSite -aggregation-> eastCluster
  westSite -aggregation-> westCluster
  eastCluster -composition-> eastHost
  westCluster -composition-> westHost
  eastHost -composition-> eastDb
  westHost -composition-> westDb
  eastHost -composition-> eastWeb
  westHost -composition-> westWeb
  eastDb -assignment-> eastData
  westDb -assignment-> westData
  eastDb -flow-> westDb "replication"
  westWeb -serving-> eastDb
  eastWeb -serving-> westDb
  eastData -association-> westData
  wan -association-> eastCluster
  wan -association-> westCluster
  eastDb -realization-> dbSvc
  westDb -realization-> dbSvc
  dbSvc -serving-> shop
  eastWeb -realization-> shop
```

### A112 — Junction mesh: an expense-claim workflow

Six junctions of both kinds split and join parallel and alternative paths, with labelled branches out of an or-junction. Small junction nodes must not be swallowed by the edge bundle around them.

- **Recognition:** `Element.Junction.And`, `Element.Junction.Or`, `Relationship.Triggering`, `Relationship.Label`
- **Rendering:** `Layout.Junction.Mesh`, `Routing.Junction.Attach`
- **Expected:** valid

```mermaid
enterprise
  business-event claimIn "Expense Claim Submitted"
  and-junction split1
  business-process checkPolicy "Check Policy"
  business-process checkReceipts "Check Receipts"
  business-process checkBudget "Check Budget"
  and-junction join1
  or-junction route
  business-process managerOk "Manager Approval"
  business-process financeOk "Finance Approval"
  business-process autoOk "Automatic Approval"
  or-junction approved
  and-junction split2
  business-process pay "Pay Claimant"
  business-process post "Post to Ledger"
  and-junction join2
  business-event claimDone "Claim Settled"
  business-process reject "Reject Claim"
  claimIn -triggering-> split1
  split1 -triggering-> checkPolicy
  split1 -triggering-> checkReceipts
  split1 -triggering-> checkBudget
  checkPolicy -triggering-> join1
  checkReceipts -triggering-> join1
  checkBudget -triggering-> join1
  join1 -triggering-> route
  route -triggering-> managerOk "over limit"
  route -triggering-> financeOk "foreign currency"
  route -triggering-> autoOk "under limit"
  route -triggering-> reject "policy breach"
  managerOk -triggering-> approved
  financeOk -triggering-> approved
  autoOk -triggering-> approved
  approved -triggering-> split2
  split2 -triggering-> pay
  split2 -triggering-> post
  pay -triggering-> join2
  post -triggering-> join2
  join2 -triggering-> claimDone
  reject -triggering-> claimDone
```


---

## Level 18 — Large realistic viewpoints

Each scenario is a complete view of 30 to 60 elements, written the way a modeller would write it. They combine most of the axes above and are the main visual review targets in the DOCX output.

### A113 — Layered view: web shop from motivation to technology

Motivation, strategy, business, application and technology elements for an online shop, with the usual serving and realisation relationships between the layers. The layer ranking (motivation and strategy at the top, technology at the bottom) is the main layout check.

- **Recognition:** `Element.*`, `Relationship.*`, `Relationship.CrossLayer.*`, `Recognition.Comment.FullLine`
- **Rendering:** `Layout.Layer.Rank.*`, `Layout.CrossingReduction`, `Render.Layer.*`, `Render.Scale.FitPage`
- **Expected:** valid

```mermaid
enterprise
  title Web shop - layered view
  %% motivation and strategy
  stakeholder customers "Online Customers"
  goal convert "Raise Online Conversion"
  requirement fastCheckout "Checkout in Under a Minute"
  capability onlineSales "Online Sales"
  capability fulfilment "Order Fulfilment"
  %% business layer
  business-actor customer "Customer"
  business-role buyer "Buyer"
  business-actor warehouseTeam "Warehouse Team"
  business-role picker "Picker"
  business-interface website "Shop Website"
  business-service browseSvc "Product Browsing"
  business-service orderSvc "Online Ordering"
  business-service deliverySvc "Home Delivery"
  business-process placeOrder "Place Order"
  business-process fulfilOrder "Fulfil Order"
  business-event orderPlaced "Order Placed"
  business-object orderBo "Order"
  business-object invoiceBo "Invoice"
  %% application layer
  application-component shop "Web Shop"
  application-component orderMgr "Order Manager"
  application-component wms "Warehouse System"
  application-component payHub "Payment Hub"
  application-interface shopUi "Shop Web UI"
  application-interface orderApi "Order API"
  application-service catalogueSvc "Catalogue Service"
  application-service checkoutSvc "Checkout Service"
  application-service paymentSvc "Payment Service"
  application-service pickSvc "Pick List Service"
  application-function checkout "Checkout"
  data-object orderDo "Order Record"
  data-object invoiceDo "Invoice Record"
  %% technology layer
  technology-service webHosting "Web Hosting"
  technology-service dbSvc "Database Service"
  technology-service queueSvc "Message Queue"
  node webNode "Web Server Cluster"
  node appNode "Application Server"
  node dbNode "Database Server"
  system-software webRuntime "Web Runtime"
  system-software dbms "Relational Database"
  system-software broker "Message Broker"
  artifact shopBundle "shop-frontend.zip"
  artifact orderJar "order-manager.jar"
  artifact orderSchema "orders schema"
  communication-network lan "Data Centre LAN"
  device scanner "Handheld Scanner"
  %% motivation and strategy relationships
  customers -association-> convert
  fastCheckout -realization-> convert
  checkoutSvc -realization-> fastCheckout
  placeOrder -realization-> onlineSales
  fulfilOrder -realization-> fulfilment
  %% business relationships
  customer -assignment-> buyer
  warehouseTeam -assignment-> picker
  buyer -assignment-> placeOrder
  picker -assignment-> fulfilOrder
  website -assignment-> orderSvc
  website -serving-> buyer
  placeOrder -realization-> orderSvc
  fulfilOrder -realization-> deliverySvc
  browseSvc -serving-> buyer
  orderSvc -serving-> buyer
  deliverySvc -serving-> buyer
  placeOrder -triggering-> orderPlaced
  orderPlaced -triggering-> fulfilOrder
  placeOrder -access(write)-> orderBo
  fulfilOrder -access(read)-> orderBo
  fulfilOrder -access(write)-> invoiceBo
  %% application to business
  catalogueSvc -serving-> placeOrder
  checkoutSvc -serving-> placeOrder
  paymentSvc -serving-> placeOrder
  pickSvc -serving-> fulfilOrder
  shopUi -serving-> buyer
  wms -serving-> picker
  orderDo -realization-> orderBo
  invoiceDo -realization-> invoiceBo
  %% application relationships
  shop -composition-> shopUi
  orderMgr -composition-> orderApi
  shop -assignment-> checkout
  checkout -realization-> checkoutSvc
  shop -realization-> catalogueSvc
  payHub -realization-> paymentSvc
  wms -realization-> pickSvc
  orderApi -serving-> shop
  paymentSvc -serving-> checkout
  shop -flow-> orderMgr "order"
  orderMgr -flow-> wms "pick list"
  orderMgr -access(write)-> orderDo
  orderMgr -access(write)-> invoiceDo
  %% technology relationships
  webNode -composition-> webRuntime
  appNode -composition-> broker
  dbNode -composition-> dbms
  webNode -assignment-> shopBundle
  appNode -assignment-> orderJar
  dbms -assignment-> orderSchema
  webRuntime -realization-> webHosting
  dbms -realization-> dbSvc
  broker -realization-> queueSvc
  webHosting -serving-> shop
  dbSvc -serving-> orderMgr
  queueSvc -serving-> orderMgr
  queueSvc -serving-> wms
  shopBundle -realization-> shop
  orderJar -realization-> orderMgr
  orderSchema -realization-> orderDo
  lan -association-> webNode
  lan -association-> appNode
  lan -association-> dbNode
  scanner -serving-> picker
```

### A114 — Application cooperation view: order handling

Sixteen application components in four groups exchange flows, offer services through interfaces nested in their owners and access shared data objects. Left-to-right layout, many labelled flows and interfaces nested in components are the main checks.

- **Recognition:** `Element.Application.*`, `Group.Declaration`, `Nesting.In.Group`, `Nesting.In.Element`, `Relationship.Flow`, `Relationship.Label`
- **Rendering:** `Layout.Direction.LR`, `Layout.Compound.Siblings`, `Routing.CrossContainer`, `Layout.Label.Collision`
- **Expected:** valid

```mermaid
enterprise
  title Order handling - application cooperation
  direction LR
  group channels "Sales channels"
  group core "Order core"
  group finance "Finance"
  group external "External services"
  application-component shop "Web Shop" in channels
  application-component mobile "Mobile App" in channels
  application-component pos "Point of Sale" in channels
  application-component marketplace "Marketplace Connector" in channels
  application-component orderMgr "Order Manager" in core
  application-component inventory "Inventory" in core
  application-component pricing "Pricing Engine" in core
  application-component promotions "Promotions" in core
  application-component wms "Warehouse System" in core
  application-component returns "Returns" in core
  application-component invoicing "Invoicing" in finance
  application-component ledger "General Ledger" in finance
  application-component payHub "Payment Hub" in finance
  application-component fraud "Fraud Screening" in external
  application-component courier "Courier Gateway" in external
  application-component bank "Bank Connector" in external
  application-interface orderApi "Order API" in orderMgr
  application-interface stockApi "Stock API" in inventory
  application-interface priceApi "Price API" in pricing
  application-interface payApi "Payment API" in payHub
  application-interface shipApi "Shipping API" in courier
  application-interface ledgerApi "Journal API" in ledger
  application-service ordering "Order Capture"
  application-service stockCheck "Stock Availability"
  application-service quote "Price Quote"
  application-service payment "Card Payment"
  application-collaboration fulfilment "Fulfilment Collaboration"
  application-interaction allocate "Allocate Stock"
  data-object orderDo "Order"
  data-object stockDo "Stock Level"
  data-object priceDo "Price List"
  data-object invoiceDo "Invoice"
  data-object shipmentDo "Shipment"
  data-object journalDo "Journal Entry"
  orderMgr -composition-> orderApi
  inventory -composition-> stockApi
  pricing -composition-> priceApi
  payHub -composition-> payApi
  courier -composition-> shipApi
  ledger -composition-> ledgerApi
  orderApi -assignment-> ordering
  stockApi -assignment-> stockCheck
  priceApi -assignment-> quote
  payApi -assignment-> payment
  orderMgr -realization-> ordering
  inventory -realization-> stockCheck
  pricing -realization-> quote
  payHub -realization-> payment
  ordering -serving-> shop
  ordering -serving-> mobile
  ordering -serving-> pos
  ordering -serving-> marketplace
  quote -serving-> shop
  quote -serving-> pos
  stockCheck -serving-> shop
  stockCheck -serving-> orderMgr
  payment -serving-> shop
  payment -serving-> mobile
  promotions -flow-> pricing "discount rules"
  pricing -access(read)-> priceDo
  inventory -access(readwrite)-> stockDo
  orderMgr -access(write)-> orderDo
  orderMgr -flow-> wms "pick request"
  wms -flow-> inventory "stock movements"
  wms -flow-> courier "consignment"
  shipApi -serving-> wms
  wms -access(write)-> shipmentDo
  orderMgr -flow-> invoicing "billable order"
  invoicing -access(write)-> invoiceDo
  invoicing -flow-> ledger "receivable"
  ledgerApi -serving-> invoicing
  ledger -access(write)-> journalDo
  payHub -flow-> fraud "risk check"
  fraud -flow-> payHub "risk score"
  payHub -flow-> bank "settlement"
  bank -flow-> ledger "bank journal"
  returns -flow-> orderMgr "return"
  returns -flow-> payHub "refund"
  fulfilment -aggregation-> orderMgr
  fulfilment -aggregation-> inventory
  fulfilment -aggregation-> wms
  fulfilment -assignment-> allocate
  allocate -access(readwrite)-> stockDo
```

### A115 — Technology deployment view: web shop hosting in two data centres

Two locations, each with a data centre facility containing clusters, hosts, runtimes and deployed artifacts, joined by a wide-area network and a replication path. Deep element-in-element nesting with the matching explicit relationships, plus relationships across sites, are the main checks.

- **Recognition:** `Element.Technology.*`, `Element.Physical.Facility`, `Element.Composite.Location`, `Nesting.Depth.5`, `Nesting.ExplicitRelationship`
- **Rendering:** `Layout.Compound.Deep`, `Render.Relationship.HiddenByNesting`, `Routing.CrossContainer.Deep`, `Render.Box.Device3D`
- **Expected:** valid

```mermaid
enterprise
  title Web shop hosting - technology deployment
  location perth "Perth"
  location sydney "Sydney"
  facility perthDc "Perth Data Centre" in perth
  facility sydneyDc "Sydney Data Centre" in sydney
  node perthEdge "Edge Cluster" in perthDc
  device perthLb1 "Load Balancer A" in perthEdge
  device perthLb2 "Load Balancer B" in perthEdge
  technology-interface httpsIn "HTTPS 443" in perthEdge
  node perthApp "Application Cluster" in perthDc
  node perthApp1 "App Host 1" in perthApp
  node perthApp2 "App Host 2" in perthApp
  system-software perthRt1 "Container Runtime" in perthApp1
  system-software perthRt2 "Container Runtime" in perthApp2
  artifact shopImg1 "shop-image:2.4" in perthRt1
  artifact shopImg2 "shop-image:2.4" in perthRt2
  node perthData "Data Cluster" in perthDc
  system-software perthDb "Database Primary" in perthData
  artifact perthOrders "orders.db" in perthDb
  system-software perthMq "Message Broker" in perthData
  node sydneyApp "Standby Application Cluster" in sydneyDc
  system-software sydneyRt "Container Runtime" in sydneyApp
  artifact shopImg3 "shop-image:2.4" in sydneyRt
  node sydneyData "Standby Data Cluster" in sydneyDc
  system-software sydneyDb "Database Replica" in sydneyData
  artifact sydneyOrders "orders-replica.db" in sydneyDb
  communication-network perthLan "Perth LAN"
  communication-network sydneyLan "Sydney LAN"
  communication-network internet "Internet"
  communication-network wan "Inter-site WAN"
  path replication "Replication Link"
  technology-service lbSvc "Load Balancing"
  technology-service containerSvc "Container Hosting"
  technology-service dbSvc "Database Service"
  technology-service mqSvc "Messaging"
  technology-service drSvc "Disaster Recovery"
  technology-function failover "Failover"
  technology-process backup "Nightly Backup"
  technology-event backupDue "Backup Window Opens"
  artifact backupSet "Backup Set"
  device adminLaptop "Operator Laptop"
  application-component shop "Web Shop"
  application-component orderMgr "Order Manager"
  perth -aggregation-> perthDc
  sydney -aggregation-> sydneyDc
  perthDc -composition-> perthEdge
  perthDc -composition-> perthApp
  perthDc -composition-> perthData
  sydneyDc -composition-> sydneyApp
  sydneyDc -composition-> sydneyData
  perthEdge -composition-> perthLb1
  perthEdge -composition-> perthLb2
  perthEdge -composition-> httpsIn
  perthApp -composition-> perthApp1
  perthApp -composition-> perthApp2
  perthApp1 -composition-> perthRt1
  perthApp2 -composition-> perthRt2
  perthRt1 -assignment-> shopImg1
  perthRt2 -assignment-> shopImg2
  perthData -composition-> perthDb
  perthData -composition-> perthMq
  perthDb -assignment-> perthOrders
  sydneyApp -composition-> sydneyRt
  sydneyRt -assignment-> shopImg3
  sydneyData -composition-> sydneyDb
  sydneyDb -assignment-> sydneyOrders
  internet -association-> perthEdge
  perthLan -association-> perthEdge
  perthLan -association-> perthApp
  perthLan -association-> perthData
  sydneyLan -association-> sydneyApp
  sydneyLan -association-> sydneyData
  wan -realization-> replication
  replication -association-> perthData
  replication -association-> sydneyData
  perthDb -flow-> sydneyDb "log shipping"
  perthLb1 -realization-> lbSvc
  perthLb2 -realization-> lbSvc
  httpsIn -assignment-> lbSvc
  perthRt1 -realization-> containerSvc
  perthRt2 -realization-> containerSvc
  sydneyRt -realization-> drSvc
  perthDb -realization-> dbSvc
  perthMq -realization-> mqSvc
  lbSvc -serving-> shop
  containerSvc -serving-> shop
  containerSvc -serving-> orderMgr
  dbSvc -serving-> orderMgr
  mqSvc -serving-> orderMgr
  drSvc -serving-> shop
  shopImg1 -realization-> shop
  shopImg2 -realization-> shop
  shopImg3 -realization-> shop
  sydneyData -assignment-> failover
  failover -realization-> drSvc
  backupDue -triggering-> backup
  perthData -assignment-> backup
  backup -access(write)-> backupSet
  adminLaptop -association-> internet
```

### A116 — Motivation view: payroll modernisation

Stakeholders, drivers, assessments, goals, outcomes, principles, requirements, constraints, meaning and value for a payroll replacement, with signed influences and the core elements that realise the requirements.

- **Recognition:** `Element.Motivation.*`, `Relationship.Influence.*`, `Relationship.Realization`, `Relationship.Association`
- **Rendering:** `Render.Layer.Motivation`, `Render.Relationship.Influence.Label`, `Layout.Hierarchy`, `Render.Box.Ellipse`
- **Expected:** valid

```mermaid
enterprise
  title Payroll modernisation - motivation view
  stakeholder ceo "Chief Executive"
  stakeholder cfo "Chief Financial Officer"
  stakeholder hr "HR Director"
  stakeholder staff "Employees"
  stakeholder regulator "Tax Office"
  driver compliance "Payroll Compliance"
  driver cost "Operating Cost"
  driver experience "Employee Experience"
  driver awardChanges "Frequent Award Changes"
  assessment lateFilings "Late Tax Filings Last Year"
  assessment manualWork "Forty Hours of Manual Work per Pay Run"
  assessment complaints "Rising Payslip Complaints"
  assessment legacyRisk "Legacy System Out of Support"
  goal compliant "Fully Compliant Payroll"
  goal cheaper "Halve Payroll Running Cost"
  goal happyStaff "Accurate and Timely Pay"
  outcome noLate "No Late Filings"
  outcome automated "Ninety Percent of Pay Runs Fully Automated"
  outcome fewerComplaints "Payslip Complaints Down by Half"
  principle cloudFirst "Cloud First"
  principle singleSource "Single Source of Employee Data"
  principle selfService "Self-Service by Default"
  requirement autoFiling "Lodge Tax Data Electronically Each Pay Run"
  requirement awardRules "Maintain Award Rules as Configuration"
  requirement mobilePayslip "Payslips on Mobile"
  requirement auditTrail "Seven-Year Audit Trail"
  constraint residency "Data Stays in Australia"
  constraint budget "Capital Budget Cap"
  meaning payslipMeaning "A Payslip is a Legal Record"
  value trust "Staff Trust"
  value savings "Cost Savings"
  application-service payrollSvc "Cloud Payroll Service"
  business-process payRun "Run Payroll"
  ceo -association-> cost
  cfo -association-> cost
  cfo -association-> compliance
  hr -association-> experience
  hr -association-> awardChanges
  staff -association-> experience
  regulator -association-> compliance
  lateFilings -association-> compliance
  manualWork -association-> cost
  complaints -association-> experience
  legacyRisk -association-> compliance
  legacyRisk -association-> cost
  compliance -influence(+)-> compliant
  cost -influence(+)-> cheaper
  experience -influence(+)-> happyStaff
  awardChanges -influence(-)-> compliant
  lateFilings -influence(--)-> compliant
  manualWork -influence(-)-> cheaper
  complaints -influence(-)-> happyStaff
  noLate -realization-> compliant
  automated -realization-> cheaper
  fewerComplaints -realization-> happyStaff
  autoFiling -realization-> noLate
  awardRules -realization-> automated
  mobilePayslip -realization-> fewerComplaints
  auditTrail -realization-> compliant
  cloudFirst -realization-> cheaper
  singleSource -realization-> automated
  selfService -influence(+)-> mobilePayslip
  residency -influence(-)-> cloudFirst
  budget -influence(-2)-> cheaper
  payrollSvc -realization-> autoFiling
  payrollSvc -realization-> awardRules
  payRun -realization-> auditTrail
  payslipMeaning -association-> mobilePayslip
  trust -association-> happyStaff
  savings -association-> cheaper
```

### A117 — Implementation and migration view: payroll replacement roadmap

Four plateaus with gaps between them, a programme of work packages with deliverables and implementation events, and the application components that each plateau contains. Left-to-right layout of a timeline-like structure is the main check.

- **Recognition:** `Element.Implementation.*`, `Relationship.Triggering`, `Relationship.Realization`, `Relationship.Aggregation`, `Relationship.Composition`
- **Rendering:** `Layout.Direction.LR`, `Render.Layer.Implementation`, `Layout.Chain.Long`
- **Expected:** valid

```mermaid
enterprise
  title Payroll replacement - implementation and migration
  direction LR
  plateau baseline "Baseline: On-Premises Payroll"
  plateau t1 "Transition 1: Parallel Run"
  plateau t2 "Transition 2: Cloud Payroll Live"
  plateau target "Target: Integrated HR and Payroll"
  gap g1 "Gap: Cloud Tenant and Integrations"
  gap g2 "Gap: Legacy Decommissioning"
  gap g3 "Gap: Rostering Integration"
  work-package programme "Payroll Replacement Programme"
  work-package wpSelect "Select Vendor"
  work-package wpBuild "Configure Cloud Payroll"
  work-package wpMigrate "Migrate Employee Data"
  work-package wpParallel "Run Parallel Pay Runs"
  work-package wpCutover "Cutover"
  work-package wpDecom "Decommission Legacy"
  work-package wpRoster "Integrate Rostering"
  deliverable dContract "Signed SaaS Contract"
  deliverable dTenant "Configured Tenant"
  deliverable dData "Migrated Employee Records"
  deliverable dSignoff "Parallel Run Sign-off"
  deliverable dDecom "Decommissioning Certificate"
  deliverable dRoster "Rostering Interface"
  implementation-event start "Programme Approved"
  implementation-event goLive "Go-Live"
  implementation-event finish "Programme Closed"
  business-role sponsor "Programme Sponsor"
  application-component legacyPayroll "Legacy Payroll"
  application-component cloudPayroll "Cloud Payroll"
  application-component rostering "Rostering"
  application-interface rosterApi "Roster Feed"
  requirement awardRules "Maintain Award Rules as Configuration"
  goal cheaper "Halve Payroll Running Cost"
  baseline -triggering-> t1
  t1 -triggering-> t2
  t2 -triggering-> target
  g1 -association-> baseline
  g1 -association-> t1
  g2 -association-> t1
  g2 -association-> t2
  g3 -association-> t2
  g3 -association-> target
  programme -composition-> wpSelect
  programme -composition-> wpBuild
  programme -composition-> wpMigrate
  programme -composition-> wpParallel
  programme -composition-> wpCutover
  programme -composition-> wpDecom
  programme -composition-> wpRoster
  sponsor -assignment-> programme
  start -triggering-> wpSelect
  wpSelect -triggering-> wpBuild
  wpBuild -triggering-> wpMigrate
  wpMigrate -triggering-> wpParallel
  wpParallel -triggering-> wpCutover
  wpCutover -triggering-> goLive
  goLive -triggering-> wpDecom
  goLive -triggering-> wpRoster
  wpDecom -triggering-> finish
  wpRoster -triggering-> finish
  wpSelect -realization-> dContract
  wpBuild -realization-> dTenant
  wpMigrate -realization-> dData
  wpParallel -realization-> dSignoff
  wpDecom -realization-> dDecom
  wpRoster -realization-> dRoster
  dTenant -realization-> cloudPayroll
  dRoster -realization-> rosterApi
  dData -realization-> t1
  dSignoff -realization-> t1
  dDecom -realization-> t2
  baseline -aggregation-> legacyPayroll
  t1 -aggregation-> legacyPayroll
  t1 -aggregation-> cloudPayroll
  t2 -aggregation-> cloudPayroll
  target -aggregation-> cloudPayroll
  target -aggregation-> rostering
  rostering -composition-> rosterApi
  g2 -association-> legacyPayroll
  g3 -association-> rosterApi
  cloudPayroll -realization-> awardRules
  awardRules -realization-> cheaper
  target -realization-> cheaper
```



---

## Level 19 — Mermaid conventions

Everything that is normal Mermaid syntax is accepted by the enterprise kind. This level covers the Mermaid forms that are not tied to Enterprise: frontmatter, `%%{init}%%` directives, accessibility statements, the five direction values, one-line diagrams, the `:::` shorthand, quoting and escape forms, markdown strings, redeclaration and the full `linkStyle` statement. The last two scenarios expect diagnostics.

### A118 — Frontmatter with a title and configuration

A YAML frontmatter block between two `---` lines at the very start of the block sets the title and configuration, as in Mermaid. Unknown configuration keys are ignored. Block-relative line numbers still count the frontmatter lines, so the `enterprise` header here is line 8. A `title` statement, if present, would replace the frontmatter title.

- **Recognition:** `Config.Frontmatter`, `Config.Frontmatter.Title`, `Config.Unknown.Ignored`
- **Rendering:** `Render.Title`
- **Expected:** valid

```mermaid
---
title: Order handling (frontmatter title)
config:
  theme: neutral
  enterprise:
    notation: box
---
enterprise
  application-component shop "Web Shop"
  application-service ordering "Online Ordering"
  shop -realization-> ordering
```

### A119 — `%%{init}%%` directives

Mermaid initialisation directives before the header and inside the diagram, one of them spread over several lines. Directives are configuration, not statements, and do not affect validation.

- **Recognition:** `Config.Directive.Init`, `Config.Directive.MultiLine`
- **Rendering:** `Render.Element.Box`
- **Expected:** valid

```mermaid
%%{init: {"theme": "forest"}}%%
enterprise
  %%{init: {
    "themeVariables": {"fontSize": "14px"}
  }}%%
  application-component shop "Web Shop"
  application-service ordering "Online Ordering"
  shop -realization-> ordering
```

### A120 — Accessibility title and description

`accTitle:` and `accDescr:` take the rest of the line; `accDescr { … }` spans several lines. They are written to the SVG as the accessible title and description (and to the DOCX image's alternative text) but are not drawn. A `;` inside them is text.

- **Recognition:** `Directive.AccTitle`, `Directive.AccDescr`, `Directive.AccDescr.Block`
- **Rendering:** `Render.Accessibility.Title`, `Render.Accessibility.Description`
- **Expected:** valid

```mermaid
enterprise
  accTitle: Web shop ordering; application view
  accDescr: The web shop realises the online ordering service.
  accDescr {
    The online ordering service serves the take-order process,
    which the order clerk performs.
  }
  application-component shop "Web Shop"
  application-service ordering "Online Ordering"
  business-process take "Take Order"
  business-role clerk "Order Clerk"
  shop -realization-> ordering
  ordering -serving-> take
  clerk -assignment-> take
```

### A121 — Direction on the header line

As with `flowchart LR`, the direction may follow the header keyword instead of being a separate `direction` statement.

- **Recognition:** `Header.Enterprise.Direction`, `Direction.LR`
- **Rendering:** `Layout.Direction.LR`
- **Expected:** valid

```mermaid
enterprise LR
  business-actor customer "Customer"
  business-role buyer "Buyer"
  business-process order "Place Order"
  customer -assignment-> buyer
  buyer -assignment-> order
```

### A122 — All five direction values

`TD` is the same as `TB`; `BT` and `RL` reverse the layout. The direction is given five times, so the last value (`RL`) applies.

- **Recognition:** `Direction.TB`, `Direction.TD`, `Direction.BT`, `Direction.LR`, `Direction.RL`, `Directive.Repeated.LastWins`
- **Rendering:** `Layout.Direction.RL`
- **Expected:** valid

```mermaid
enterprise
  direction TD
  direction BT
  direction LR
  direction TB
  direction RL
  application-component shop "Web Shop"
  application-service ordering "Online Ordering"
  business-process take "Take Order"
  shop -realization-> ordering
  ordering -serving-> take
```

### A123 — A whole diagram on one line

The Mermaid habit of writing `graph TD; A-->B` on a single line: header, direction and every statement separated by `;`. Every diagnostic would be reported on line 1.

- **Recognition:** `Recognition.Statement.SemicolonBoundary`, `Header.Enterprise.Direction`, `Direction.BT`
- **Rendering:** `Layout.Direction.BT`
- **Expected:** valid

```mermaid
enterprise BT; application-component shop "Web Shop"; application-service ordering "Online Ordering"; shop -realization-> ordering; ordering -serving-> shop
```

### A124 — The `:::` class shorthand

`:::className` attaches a class to an element directly after its id or after its label, and to an element where it is named in a relationship line, as Mermaid allows on nodes in link statements. `classDef default` applies to every element. A class that is never defined is quietly ignored, as in Mermaid.

- **Recognition:** `Style.ClassShorthand.AfterId`, `Style.ClassShorthand.AfterLabel`, `Style.ClassShorthand.InRelationship`, `Style.ClassDef.Default`, `Style.Class.Undefined.Ignored`
- **Rendering:** `Render.Style.Fill`, `Render.Style.Stroke`, `Render.Style.StrokeDash`
- **Expected:** valid

```mermaid
enterprise
  application-component shop:::core "Web Shop"
  application-component legacy "Legacy Shop":::retiring
  application-component outlet "Outlet Shop"
  application-service ordering "Online Ordering"
  legacy -specialization-> shop
  outlet:::retiring -specialization-> shop:::core
  shop -realization-> ordering:::neverDefined
  classDef default stroke-width:1.5px
  classDef core fill:#d7ffd7,stroke:#008000
  classDef retiring fill:#eeeeee,stroke:#999999,stroke-dasharray:4 2
```

### A125 — Single-quoted labels and backslash escapes

Labels may use single or double quotes, as Mermaid's newer grammars allow. Inside a label, `\"` and `\'` give a quote, `\\` gives one backslash and `\n` a line break; any other backslash stays as typed.

- **Recognition:** `Element.Label.SingleQuoted`, `Relationship.Label.SingleQuoted`, `Element.Label.EscapedQuote`, `Element.Label.Backslash.Escape`, `Element.Label.LineBreak.BackslashN`
- **Rendering:** `Render.Label.XmlEscape`, `Render.Label.ExplicitBreak`
- **Expected:** valid

```mermaid
enterprise
  application-component shop 'Web Shop'
  application-service ordering 'The "Online" Ordering Service'
  data-object order "Order \"draft\" copy"
  data-object path "Literal \\n stays as typed in C:\\new"
  data-object note 'It\'s the customer\'s note'
  shop -realization-> ordering 'realises'
  shop -access(write)-> order "line one\nline two"
  shop -access(read)-> path
  shop -access(read)-> note
```

### A126 — Markdown strings

A label written as a Mermaid markdown string (backticks inside the quotes) supports `**bold**` and `*italic*`, wraps automatically, and may continue over several source lines; a line break in the source is a line break in the label. Diagnostics for such a statement use the line where it starts.

- **Recognition:** `Element.Label.Markdown`, `Element.Label.Markdown.MultiLine`, `Relationship.Label.Markdown`
- **Rendering:** `Render.Label.Bold`, `Render.Label.Italic`, `Render.Label.Wrap`
- **Expected:** valid

```mermaid
enterprise
  application-component shop "`The **new** web shop`"
  application-service ordering "`Online *ordering*,
  open every day`"
  business-process take "Take Order"
  shop -realization-> ordering "`**realises**`"
  ordering -serving-> take
```

### A127 — Declaring an element again with the same type

As in Mermaid, naming an id again does not create a second element: a later declaration with the same element type may add or replace the label and the container. Here `shop` is declared without a label, then given a label and a group, then renamed. One box is drawn, labelled "Web Shop (final)", inside the group.

- **Recognition:** `Element.Redeclaration.SameType`, `Element.Redeclaration.LabelUpdate`, `Element.Redeclaration.ContainerUpdate`
- **Rendering:** `Render.Element.Box`, `Render.Group.DashedTab`
- **Expected:** valid

```mermaid
enterprise
  application-component shop
  application-service ordering "Online Ordering"
  group web "Web tier"
  shop -realization-> ordering
  application-component shop "Web Shop" in web
  application-component shop "Web Shop (final)"
```

### A128 — `linkStyle` with the full Mermaid property set

`linkStyle default` styles every relationship; numbered `linkStyle` lines override it. Any property Mermaid accepts for links may be used, a comma inside a value is written `\,` as in Mermaid, and `interpolate <curve>` sets the curve of the routed line (a renderer that has only one routing style may ignore it).

- **Recognition:** `Style.Relationship.Default`, `Style.Relationship.Index`, `Style.Relationship.Interpolate`, `Style.Value.EscapedComma`
- **Rendering:** `Render.Relationship.Colour`, `Render.Relationship.Dash`, `Render.Relationship.Opacity`
- **Expected:** valid

```mermaid
enterprise
  application-component shop "Web Shop"
  application-service ordering "Online Ordering"
  business-process take "Take Order"
  business-role clerk "Order Clerk"
  shop -realization-> ordering
  ordering -serving-> take
  clerk -assignment-> take
  linkStyle default stroke:#666666,color:#666666
  linkStyle 0 stroke:#c00000,stroke-width:3px,stroke-dasharray:5\,5
  linkStyle 1 interpolate basis stroke:#0070c0,opacity:0.6
  linkStyle 2 interpolate linear
```

### A129 — `class` and `style` naming undeclared elements (warnings)

Mermaid would create a plain node for an unknown id named in `class` or `style`. An enterprise element needs an element type, so the statement is ignored with a warning instead. A `class` naming a class that is never defined is ignored without any diagnostic, as in Mermaid.

- **Recognition:** `Diagnostics.Style.UndeclaredElement`, `Style.Class.Undefined.Ignored`
- **Rendering:** `Render.Element.Box`
- **Expected:** 2 warnings (lines 4, 5)

```mermaid
enterprise
  application-component shop "Web Shop"
  classDef retiring fill:#eeeeee
  class shop,ghost retiring
  style phantom fill:#ffd7d7
  class shop neverDefined
```

### A130 — `linkStyle` index out of range

Mermaid rejects a `linkStyle` number that has no matching link, and so does the enterprise kind. Relationships are numbered from 0, so with one relationship only `linkStyle 0` is valid.

- **Recognition:** `Diagnostics.LinkStyle.OutOfRange`
- **Rendering:** `Diagnostics.Error.Placeholder`
- **Expected:** 1 error (line 6)

```mermaid
enterprise
  application-component shop "Web Shop"
  application-service ordering "Online Ordering"
  shop -realization-> ordering
  linkStyle 0 stroke:#c00000
  linkStyle 1 stroke:#0070c0
```


## Level 20 — Elements first, relationships later

Each scenario declares **every element first** (some with a quoted name, some as a bare id) and writes the **relationships afterwards**. They mirror earlier scenarios of the same subject and size and check that the layout and drawing follow the relationships, not the order of declaration.

### A131 — Elements first: business event and process chain

Mirrors A43. The two processes without a quoted name take their label from the id.

- **Recognition:** `Element.Business.Event`, `Element.Business.Process`, `Relationship.Triggering`, `Relationship.Flow`, `Relationship.Label`
- **Rendering:** `Layout.Direction.LR`, `Layout.Chain.Short`
- **Expected:** valid

```mermaid
enterprise
  business-event orderReceived "Order Received"
  business-process checkStock
  business-process pick "Pick Items"
  business-process invoice
  orderReceived -triggering-> checkStock "new order"
  checkStock -triggering-> pick "in stock"
  pick -flow-> invoice "packed quantities"
```

### A132 — Elements first: application components with fan-in and fan-out

Mirrors A32. Two front ends flow into one manager, which flows on to two back-end systems.

- **Recognition:** `Element.Application.Component`, `Relationship.Flow`, `Relationship.Label`
- **Rendering:** `Layout.Layer.Application`, `Layout.Junction.FanIn`, `Layout.Junction.FanOut`
- **Expected:** valid

```mermaid
enterprise
  application-component webShop "Web Shop"
  application-component mobileApp
  application-component orderMgr "Order Manager"
  application-component erp
  application-component wms "Warehouse System"
  webShop -flow-> orderMgr
  mobileApp -flow-> orderMgr
  orderMgr -flow-> erp "invoice data"
  orderMgr -flow-> wms "pick list"
```

### A133 — Elements first: technology hosting an application

Mirrors A67. A node assigned an artifact that realises an application component, and a technology service that serves it.

- **Recognition:** `Element.Technology.Node`, `Element.Technology.Artifact`, `Element.Technology.Service`, `Relationship.Realization`, `Relationship.Assignment`, `Relationship.Serving`
- **Rendering:** `Layout.Layer.Rank.ApplicationAboveTechnology`
- **Expected:** valid

```mermaid
enterprise
  application-component shop "Web Shop"
  technology-service hosting "Container Hosting"
  node k8s
  artifact image "shop-image:2.4"
  system-software db
  technology-service dbSvc "Managed Database"
  hosting -serving-> shop
  dbSvc -serving-> shop
  k8s -realization-> hosting
  db -realization-> dbSvc
  image -realization-> shop
  k8s -assignment-> image
```

### A134 — Elements first: principles and requirements with influence

Mirrors A42. Influence relationships declared after all of the motivation elements.

- **Recognition:** `Element.Motivation.Principle`, `Element.Motivation.Requirement`, `Element.Motivation.Goal`, `Relationship.Influence.StrongPositive`, `Relationship.Influence.StrongNegative`, `Relationship.Influence.Numeric`
- **Rendering:** `Render.Layer.Motivation`, `Render.Relationship.Influence.Label`
- **Expected:** valid

```mermaid
enterprise
  principle cloudFirst "Cloud First"
  principle buyNotBuild
  requirement saas "Use a SaaS Payroll Product"
  requirement onPrem
  goal lowerCost "Lower Running Cost"
  cloudFirst -influence(++)-> saas
  cloudFirst -influence(--)-> onPrem
  buyNotBuild -influence(3)-> saas
  onPrem -influence(-2)-> lowerCost
```

### A135 — Elements first: strategy elements and business realisation

Mirrors A26. A value stream broken into stages, capabilities that serve the stages and business elements that realise the capabilities.

- **Recognition:** `Element.Strategy.*`, `Element.Business.Function`, `Element.Business.Process`, `Relationship.Composition`, `Relationship.Flow`, `Relationship.Serving`, `Relationship.Realization`
- **Rendering:** `Layout.Layer.Rank.StrategyAboveBusiness`
- **Expected:** valid

```mermaid
enterprise
  value-stream orderToCash "Order to Cash"
  value-stream takeStage "Take Order"
  value-stream shipStage
  capability salesCap "Sales"
  capability logisticsCap
  business-function salesFn "Sales Function"
  business-process pickPack
  orderToCash -composition-> takeStage
  orderToCash -composition-> shipStage
  takeStage -flow-> shipStage
  salesCap -serving-> takeStage
  logisticsCap -serving-> shipStage
  salesFn -realization-> salesCap
  pickPack -realization-> logisticsCap
```

### A136 — Elements first: physical elements with technology

Mirrors A20. A facility composed of equipment and a server, firmware assigned to the equipment and a material that it accesses.

- **Recognition:** `Element.Physical.*`, `Element.Technology.SystemSoftware`, `Element.Technology.Node`, `Relationship.Composition`, `Relationship.Assignment`, `Relationship.Access`
- **Rendering:** `Render.Layer.Technology`, `Render.Box.Device3D`
- **Expected:** valid

```mermaid
enterprise
  facility warehouse "Central Warehouse"
  equipment sorter
  system-software sorterFw "Sorter Firmware"
  node wmsServer
  material parcel "Parcel"
  distribution-network fleet "Delivery Fleet"
  warehouse -composition-> sorter
  warehouse -composition-> wmsServer
  sorter -assignment-> sorterFw
  sorter -access(readwrite)-> parcel
  fleet -serving-> warehouse
```

### A137 — Elements first: and-junction and or-junction

Mirrors A35 and A48. An and-junction joins two triggers and an or-junction then splits the flow into two alternatives that merge again.

- **Recognition:** `Element.Junction.And`, `Element.Junction.Or`, `Relationship.Triggering`, `Relationship.Junction.Chain`
- **Rendering:** `Render.Junction.Filled`, `Render.Junction.Hollow`, `Layout.Junction.Chain`
- **Expected:** valid

```mermaid
enterprise
  business-process takePayment "Take Payment"
  business-process pickItems
  and-junction both
  or-junction decide
  business-process ship "Ship Order"
  business-process collect
  or-junction merge
  business-event done "Order Complete"
  takePayment -triggering-> both
  pickItems -triggering-> both
  both -triggering-> decide
  decide -triggering-> ship
  decide -triggering-> collect
  ship -triggering-> merge
  collect -triggering-> merge
  merge -triggering-> done
```

### A138 — Elements first: nested groups with members

Mirrors A58. Both groups and every member are declared before the flows between levels.

- **Recognition:** `Group.Declaration`, `Group.In.Group`, `Nesting.In.Group`, `Relationship.Flow`
- **Rendering:** `Render.Group.DashedTab`, `Layout.Compound.Nested`
- **Expected:** valid

```mermaid
enterprise
  group estate "Application estate"
  group finance "Finance systems" in estate
  application-component shop "Web Shop" in estate
  application-component ledger "General Ledger" in finance
  application-component payroll in finance
  shop -flow-> ledger "sales journal"
  payroll -flow-> ledger "pay journal"
```

### A139 — Elements first: locations, a facility and a node

Mirrors A34. Two locations hold a facility, a node and an actor, and the node serves the actor in the other location.

- **Recognition:** `Element.Composite.Location`, `Nesting.In.Element`, `Relationship.Aggregation`, `Relationship.Composition`, `Relationship.Serving`
- **Rendering:** `Render.Icon.Location`, `Layout.Compound.Siblings`
- **Expected:** valid

```mermaid
enterprise
  location perth "Perth"
  location sydney
  facility perthDc "Perth Data Centre" in perth
  node shopNode in perthDc
  business-actor supportTeam "Support Team" in sydney
  perth -aggregation-> perthDc
  perthDc -composition-> shopNode
  sydney -aggregation-> supportTeam
  shopNode -serving-> supportTeam
```

### A140 — Elements first: application supports the business layer

Mirrors A66. Business, application and data elements are declared together, then connected across the layers.

- **Recognition:** `Relationship.CrossLayer.ApplicationToBusiness`, `Relationship.Serving`, `Relationship.Realization`, `Relationship.Assignment`
- **Rendering:** `Layout.Layer.Rank.BusinessAboveApplication`
- **Expected:** valid

```mermaid
enterprise
  business-role customer "Customer"
  business-process orderGoods
  business-object orderBo "Order"
  application-service ordering "Online Ordering"
  application-process autoOrder
  application-component shop "Web Shop"
  data-object orderDo "Order Record"
  ordering -serving-> orderGoods
  ordering -serving-> customer
  autoOrder -realization-> orderGoods
  orderDo -realization-> orderBo
  shop -realization-> ordering
  shop -assignment-> autoOrder
```

### A141 — Elements first: cycles, a two-cycle, a self-loop and a loop through a junction

Mirrors A107. A four-step triggering ring with a back edge, two flows in opposite directions, a self-loop and a retry loop through an or-junction.

- **Recognition:** `Relationship.Triggering`, `Relationship.Flow`, `Relationship.SelfLoop`, `Element.Junction.Or`
- **Rendering:** `Layout.Cycle.Break`, `Routing.BackEdge`, `Routing.SelfLoop`
- **Expected:** valid

```mermaid
enterprise
  business-process draft "Draft Order"
  business-process submit
  business-process approve "Approve Order"
  business-process fulfil
  application-component shop "Web Shop"
  application-component erp
  application-component retryQueue "Retry Queue"
  or-junction retry
  draft -triggering-> submit
  submit -triggering-> approve
  approve -triggering-> fulfil
  fulfil -triggering-> draft "next order"
  approve -triggering-> draft "rejected"
  shop -flow-> erp "order"
  erp -flow-> shop "status"
  retryQueue -flow-> retryQueue "retry"
  erp -flow-> retry
  retry -flow-> retryQueue
  retry -flow-> erp
```
### A142 — Grid stress test: application landscape (nodes first, relationships later)

This is a **stress test of the grid system**. It redraws a large Enterprise application landscape: one shared grid has to hold 21 groups and 13 locations (several nested three deep) with 166 elements, and 180 relationships, most of them crossing between groups. All groups, locations and elements are declared first (most with a quoted name, one as a bare id), and every relationship is written afterwards. Only elements that could be read reliably from the source picture are included; some names are approximations.

- **Recognition:** `Group.Declaration`, `Group.In.Group`, `Element.Composite.Location`, `Nesting.In.Group`, `Nesting.In.Element`, `Element.Application.Component`, `Element.Application.Service`, `Element.Application.DataObject`, `Element.Technology.Device`, `Relationship.Flow`, `Relationship.Triggering`, `Relationship.Serving`, `Relationship.Label`
- **Rendering:** `Layout.Grid.Shared`, `Layout.Compound.Nested`, `Layout.Stress.CrossGroup`, `Routing.Orthogonal`, `Render.Group.DashedTab`
- **Expected:** valid

```mermaid
enterprise
  group comms "Comms"
  group dbms "DBMS and FAS"
  group myfin "MyFinance"
  group hr "HR"
  group etms "ETMS"
  group sams "SAMS"
  group sdt "SDT"
  group avl "AVL"
  group brms "BRMS and EAD"
  group uac "User account creation"
  group vhub "Volunteer Hub"
  group other "Other"
  group appsvc "Application Services (Detailed)"
  group cadInt "CAD Integration Components" in appsvc
  group naPolice "WA Police" in cadInt
  group omsInt "OMS Integration Components" in appsvc
  group gisInt "GIS Integration Components" in appsvc
  group conInt "ConnData Integration Components" in appsvc
  group webeoc "WebEOC"
  group ewa "Emergency WA"
  group pegsap "PEGSaP"
  location telstra "Telstra" in comms
  location fasLoc "FAMS" in dbms
  location t1Loc "TechnologyOne" in myfin
  location bfLoc "BFinch" in myfin
  location empLoc "Empower" in hr
  location bomLoc "Bureau of Meteorology" in sdt
  location nvlLoc "Netstar" in avl
  location ammLoc "Ammistar" in brms
  location uipLoc "UiPath Cloud" in uac
  location peopleLoc "PeopleSmart" in vhub
  location dmcaLoc "DMCA Cloud" in other
  location weVendor "Vendor" in webeoc
  location ewaVendor "Vendor" in ewa
  application-service lightSvc "Light Service" in comms
  application-service pbxSvc "Phonebook Service" in comms
  device deskPhone "Desk phone" in comms
  application-component c1Receiver "C1 Receiver" in comms
  application-component c1Viewer "C1 Viewer" in comms
  data-object c1Db "C1 database" in comms
  application-component activeDir "Active Directory" in comms
  data-object commsDirDb "Comms Directory database" in comms
  application-component searchDial "Search and Dial" in comms
  application-service hourContact "24 Hour Contact Service" in comms
  application-service telstraMsg "Telstra Messaging Service" in telstra
  application-component rapidSub "Rapid Alert Incident Subscriber" in comms
  data-object rapidDb "Rapid Alert database" in comms
  application-component cadComms "CAD" in comms
  application-component dmeComms "DME" in comms
  data-object dbmsTopDb "DBMS database" in dbms
  application-component dbmsApi "DBMS API" in dbms
  application-component dbmsSub "DBMS Incident Subscriber" in dbms
  application-component dbmsTop "DBMS" in dbms
  application-service cbmApi "CBM Service API" in dbms
  application-service fireAlarm "Solar Fire Alarm Incoming System" in dbms
  application-service alarmApi "Data Alarm Service API" in dbms
  application-component incRepApi "Incident Report API" in dbms
  data-object fasDb "FAS database" in fasLoc
  application-component cadDbms "CAD" in dbms
  application-component dmeDbms "DME" in dbms
  application-component t1Fin "T1 Finance Subscriber" in myfin
  application-component finApi "Finance API" in myfin
  application-component t1Fpas "T1 FPAS Subscriber" in myfin
  application-component sftp "SFTP Folder" in myfin
  application-service t2Emp "T2 Employee Service" in myfin
  application-component cadT1 "CAD T1 Subscriber" in myfin
  application-component hrApiMf "HR API" in myfin
  application-component cadMf "CAD" in myfin
  application-component t1Cix "TechnologyOne CIX" in t1Loc
  application-component bfinchApi "BFinch API" in bfLoc
  application-component bfinchSoap "BFinch SOAP API" in bfLoc
  application-component empower "Empower" in empLoc
  data-object empowerDb "Empower database" in empLoc
  application-component empowerExt "Empower extract" in empLoc
  application-component corporate "Corporate" in hr
  application-component dataFactory "Azure Data Factory" in etms
  application-component dmeEtms "DME" in etms
  application-component etmsLs "ETMS LS" in etms
  application-component etmsSq "ETMS SQ" in etms
  application-component eAcademy "eAcademy" in etms
  application-component createPeople "Creation of People Function" in etms
  application-component etmsApi "ETMS Service API" in etms
  application-component etmsApp "ETMS Application" in etms
  application-component corpEtms "Corporate" in etms
  application-component samsAdf1 "Azure Data Factory" in sams
  application-component samsAdf2 "Azure Data Factory" in sams
  application-component samsAdf3 "Azure Data Factory" in sams
  application-component Email in sams
  application-component samsCore "SAMS" in sams
  application-component samsMain "SAMS" in sams
  data-object samsRepDb "SAMS Reporting Database" in sams
  application-service samsSsrs "SQL Server Reporting Services" in sams
  device samsGis "GIS" in sams
  data-object samsNoteDb "DM Net Note database" in sams
  application-component dmeSams "DME" in sams
  application-service weatherApi "Weather Service API" in sdt
  application-component bomApi "BOM Weather API" in bomLoc
  data-object weatherDb "Weather database" in sdt
  device sdtDevice "SDT" in sdt
  application-component sdtApi "SDT API" in sdt
  application-service sdtSub "SDT Subscriber Service" in sdt
  data-object sdtDb "SDT database" in sdt
  application-component dmeSdt "DME" in sdt
  data-object loggingDb "Logging database" in avl
  application-component vehAvlSub "Vehicle AVL Subscriber" in avl
  application-service nvls "Netstar Vehicle Location Service" in nvlLoc
  application-component avlClock "AVL Clock" in avl
  application-service kpVls "KP Vehicle Location Service" in avl
  device avlGis "GIS" in avl
  application-component avlSub2 "AVL Subscriber" in avl
  application-component rmcAvl "RMC Publisher" in avl
  application-component dmeAvl "DME" in avl
  application-component ammBrms "Ammistar BRMS" in ammLoc
  application-component ammApi "Ammistar BRMS API" in ammLoc
  application-component ammMap "Ammistar Mapuncts" in ammLoc
  application-component brmsDf "Azure Data Factory" in brms
  application-component brmsFn "BRMS Function" in brms
  data-object brmsDb "BRMS database" in brms
  application-service sqlAnalysis "SQL Server Analysis Services" in brms
  application-component brmsReports "BRMS Reports" in brms
  application-component sais "SAIS" in brms
  application-component eadInt "EAD Integration" in brms
  application-component eadSub "EAD Incident Subscriber" in brms
  data-object saisDb "SAIS database" in brms
  application-component cadBrms "CAD" in brms
  application-service serviceNow "Service Now" in uac
  application-service pdPop "PD Population Service" in uac
  application-component uipRobot "UiPath Robot" in uac
  application-component uipOrch "UiPath Orchestrator" in uipLoc
  application-component activeDir2 "Active Directory" in uac
  application-component ms365 "Microsoft 365 Admin Centre" in uac
  application-component volEnrol "Volunteer Enrolment" in vhub
  application-component volMgmtApi "User Management API" in vhub
  application-component vhSub "Volunteer Hub Subscriber" in vhub
  application-component rmcVh "RMC Publisher" in vhub
  application-service vhStatus "Volunteer Hub Status Region" in vhub
  application-service emailSvc "Email Service" in vhub
  application-component vhHub "Volunteer Hub" in peopleLoc
  application-component staffHub "Staffhub" in peopleLoc
  application-component vhApi "Volunteer Hub API" in vhub
  application-component personnelApi "PersonnelAPI" in vhub
  application-component dscVh "DSC" in vhub
  application-component dmeVh "DME" in vhub
  application-component dbca "DBCA" in dmcaLoc
  application-component dailyBurn "Daily Burns Summary" in other
  application-component sharepoint "SharePoint Drive" in other
  application-service pwsVls "PWS Vehicle Location Service" in other
  device otherGis "GIS" in other
  data-object vehicleData "Vehicle Data" in other
  application-component dmeOther "DME" in other
  application-component dmsSub "DMS Incident Subscriber" in other
  application-component gisSub "GIS Incident Subscriber" in other
  application-component t1Sub "T1 Subscriber" in other
  application-component wfamm "WFAMM" in other
  data-object wfammDb "WFAMM Database" in other
  application-component cadPerson "CAD Person Subscriber" in naPolice
  application-component cadVehicle "CAD Vehicle Subscriber" in naPolice
  application-component cadEvent "CAD Event Publisher" in naPolice
  application-component cadNotif "CAD Notification Publisher" in naPolice
  application-component cadUseApi "P1 USE API" in naPolice
  application-component cadPubApi "CAD Publisher API" in cadInt
  application-component cadWork "CAD Work Publisher" in cadInt
  application-component dmeCadInt "DME" in appsvc
  application-component omsPms "PMS" in omsInt
  data-object omsDb "OMK database" in omsInt
  application-component omsRs "RS" in omsInt
  application-component omsApi "OMS Publisher API" in omsInt
  application-component dmeOms "DME" in appsvc
  application-component gisArc "ArcGIS Subscriber" in gisInt
  application-component gisApi "GIS API" in gisInt
  application-component gisMain "GIS" in gisInt
  application-component cadGis "CAD" in gisInt
  application-component dmeGis "DME" in appsvc
  application-component empSvc "Empower Service" in conInt
  data-object conDb "ConnData database" in conInt
  application-component hrApiCon "HR API" in conInt
  data-object spatialDb "Spatial reporting database" in webeoc
  application-component spatialSub "Spatial BA Unit Subscriber" in webeoc
  application-component webeocApi "WebEOC API" in webeoc
  application-component webeocSub "WebEOC Incident Subscriber" in webeoc
  application-component cadWeb "CAD" in webeoc
  application-component webeocApp "WebEOC" in weVendor
  application-component webeocPlat "WebEOC platform" in weVendor
  application-service webeocSvc "WebEOC Service" in webeoc
  application-component dmeWeb "DME" in webeoc
  application-component ewaApp "Emergency WA" in ewaVendor
  application-component ewaPlat "Emergency WA platform" in ewaVendor
  application-component ewaSub "EWA Incident Subscriber" in ewa
  application-component ewaAdaptor "EWA WebAdaptor" in ewa
  application-service ewaSvc "EWA Service" in ewa
  application-component ewaMaps "FEDMaps"
  application-component powerBi "PowerBI" in pegsap
  application-component pegApp "PEGSaP" in pegsap
  application-component orgApi "Organisation API" in pegsap
  data-object pegDb "PEGSAP database" in pegsap
  application-service pegSsrs "SQL Server Reporting Services" in pegsap
  application-component extApi "External Party API" in pegsap
  application-component cadPeg "CAD ETL PEGSAP" in pegsap
  device ircDevice "IRC device" in pegsap
  application-component dmePeg "DME" in pegsap
  deskPhone -flow-> c1Receiver
  c1Receiver -flow-> c1Viewer
  c1Receiver -access-> c1Db
  c1Viewer -flow-> pbxSvc
  pbxSvc -serving-> lightSvc
  pbxSvc -access-> commsDirDb
  activeDir -access-> commsDirDb
  searchDial -access-> commsDirDb
  hourContact -access-> commsDirDb
  telstraMsg -flow-> hourContact
  telstraMsg -access-> rapidDb
  rapidSub -access-> rapidDb
  cadComms -flow-> rapidSub
  dmeComms -flow-> telstraMsg
  dbmsApi -access-> dbmsTopDb
  dbmsTop -access-> dbmsTopDb
  dbmsSub -flow-> dbmsApi
  cadDbms -flow-> dbmsSub
  dbmsTop -flow-> cbmApi
  cbmApi -flow-> alarmApi
  alarmApi -flow-> fireAlarm
  incRepApi -flow-> alarmApi
  dmeDbms -flow-> incRepApi
  alarmApi -access-> fasDb
  cadMf -flow-> cadT1
  cadT1 -flow-> t2Emp
  hrApiMf -flow-> t2Emp
  t2Emp -flow-> sftp
  sftp -flow-> t1Cix
  t1Fpas -flow-> finApi
  t1Fin -flow-> finApi
  finApi -flow-> t1Cix
  t1Cix -flow-> bfinchApi
  bfinchApi -flow-> bfinchSoap
  empower -access-> empowerDb
  empower -flow-> empowerExt
  empowerExt -flow-> dmeEtms "Staff"
  empowerExt -flow-> corporate
  corporate -flow-> dmeEtms "Staff"
  dataFactory -flow-> dmeEtms
  dmeEtms -flow-> etmsLs
  dmeEtms -flow-> etmsSq
  createPeople -flow-> dmeEtms
  dmeEtms -flow-> etmsApi
  etmsApi -flow-> etmsApp
  etmsApp -flow-> eAcademy
  eAcademy -flow-> samsAdf1 "Competencies"
  corpEtms -flow-> dmeEtms "Staff"
  samsAdf1 -flow-> samsCore
  samsAdf2 -flow-> samsCore
  samsCore -flow-> Email
  samsCore -flow-> samsMain
  samsMain -flow-> samsAdf3
  samsMain -access-> samsRepDb
  samsSsrs -access-> samsRepDb
  samsSsrs -flow-> samsGis
  samsMain -access-> samsNoteDb
  dmeSams -flow-> samsAdf2
  bomApi -flow-> weatherApi
  weatherApi -access-> weatherDb
  weatherApi -flow-> sdtDevice
  sdtApi -flow-> sdtDevice
  sdtSub -flow-> sdtApi
  sdtSub -access-> sdtDb
  dmeSdt -flow-> sdtSub
  vehAvlSub -access-> loggingDb
  nvls -flow-> vehAvlSub
  nvls -flow-> avlClock
  kpVls -flow-> avlGis
  avlSub2 -flow-> kpVls
  rmcAvl -flow-> avlSub2
  dmeAvl -flow-> avlSub2
  ammBrms -flow-> ammApi
  ammBrms -flow-> ammMap
  ammApi -flow-> brmsDf
  brmsDf -flow-> brmsFn
  brmsFn -access-> brmsDb
  sqlAnalysis -access-> brmsDb
  sqlAnalysis -flow-> brmsReports
  ammMap -flow-> sais
  sais -flow-> brmsFn
  sais -access-> saisDb
  eadSub -access-> saisDb
  eadInt -flow-> sais
  cadBrms -flow-> eadSub
  serviceNow -flow-> pdPop
  pdPop -flow-> uipRobot
  uipOrch -flow-> uipRobot
  uipRobot -flow-> activeDir2
  activeDir2 -flow-> ms365
  ms365 -flow-> uipRobot
  volEnrol -flow-> volMgmtApi
  volMgmtApi -flow-> vhSub
  rmcVh -flow-> vhSub
  vhSub -flow-> vhStatus
  vhStatus -flow-> emailSvc
  vhStatus -flow-> vhHub
  vhHub -triggering-> staffHub
  vhHub -flow-> vhApi
  vhApi -flow-> personnelApi
  dscVh -flow-> vhStatus "Logs"
  dmeVh -flow-> vhSub
  dbca -flow-> dailyBurn
  dailyBurn -flow-> sharepoint
  pwsVls -flow-> otherGis
  pwsVls -flow-> dmeOther
  dmeOther -access-> vehicleData
  dmsSub -flow-> dmeOther
  gisSub -flow-> dmeOther
  t1Sub -flow-> dmeOther
  dmeOther -flow-> wfamm
  wfamm -access-> wfammDb
  cadPerson -flow-> cadUseApi
  cadVehicle -flow-> cadUseApi
  cadEvent -flow-> cadPubApi
  cadNotif -flow-> cadPubApi
  cadUseApi -flow-> cadPubApi
  cadPubApi -flow-> cadWork
  cadPubApi -flow-> dmeCadInt
  omsPms -flow-> omsApi
  omsRs -access-> omsDb
  omsRs -flow-> omsPms
  omsApi -flow-> dmeOms
  gisArc -flow-> gisApi
  cadGis -flow-> gisArc
  gisApi -flow-> gisMain
  gisMain -flow-> dmeGis
  empSvc -access-> conDb
  hrApiCon -access-> conDb
  spatialSub -access-> spatialDb
  spatialSub -flow-> webeocApi
  webeocApi -flow-> webeocApp
  webeocSub -flow-> webeocApi
  cadWeb -flow-> webeocSub
  webeocApp -flow-> webeocPlat
  webeocPlat -flow-> webeocSvc
  webeocSvc -flow-> dmeWeb
  ewaSub -flow-> ewaApp
  ewaApp -flow-> ewaPlat
  ewaPlat -flow-> ewaSvc
  ewaAdaptor -flow-> ewaSvc
  ewaMaps -flow-> ewaSvc
  powerBi -flow-> pegApp
  pegApp -flow-> orgApi
  pegApp -access-> pegDb
  pegSsrs -access-> pegDb
  pegSsrs -flow-> extApi
  cadPeg -access-> pegDb
  extApi -flow-> ircDevice
  orgApi -flow-> dmePeg
  dmeEtms -flow-> dmeOther
  dmeSams -flow-> dmeEtms
  empowerExt -flow-> dmeSams
  dmeDbms -flow-> dmeWeb
  dmeWeb -flow-> dmeOther
  webeocSvc -flow-> ewaSvc
  dmePeg -flow-> dmeOther
  dmeAvl -flow-> dmeOther
  dmeVh -flow-> dmeOther
  dmeSdt -flow-> dmeOther
  dmeComms -flow-> dmeOther
  dmeCadInt -flow-> dmeOms
  dmeOms -flow-> dmeGis
  cadComms -flow-> dmeComms
  cadDbms -flow-> dmeDbms
  cadMf -flow-> dmeEtms
  cadBrms -flow-> eadInt
  cadWeb -flow-> dmeWeb
  hrApiCon -flow-> empowerExt
  hrApiMf -flow-> hrApiCon
  cadPeg -flow-> dmePeg
  telstraMsg -flow-> dmeComms
  vhApi -flow-> ewaApp
  samsGis -flow-> otherGis
  avlGis -flow-> otherGis
  ewaSvc -flow-> dmeWeb
  cadT1 -flow-> t1Sub
  gisMain -flow-> otherGis
  uipRobot -flow-> volMgmtApi
  activeDir2 -flow-> activeDir
```

### A143 - Layout mode: explicit grid on a small chain

For the layout mode feature. Frontmatter `config: layout: grid` selects the shared grid explicitly. Elements are declared first, relationships later.

- **Recognition:** `Config.Frontmatter`, `Config.Layout.Grid`, `Relationship.Flow`
- **Rendering:** `Layout.Grid`
- **Expected:** valid. Once the feature exists, elements sit on one shared grid, with straight runs and rounded corners, the same as A145.

```mermaid
---
config:
  layout: grid
---
enterprise
  application-component shop "Web Shop"
  application-component orders "Order Manager"
  application-component erp "ERP"
  application-component ledger "General Ledger"
  shop -flow-> orders "order"
  orders -flow-> erp "booking"
  erp -flow-> ledger "journal"
```

### A144 - Layout mode: explicit open on the same chain

For the layout mode feature. Same diagram as A143 with `layout: open`.

- **Recognition:** `Config.Frontmatter`, `Config.Layout.Open`, `Relationship.Flow`
- **Rendering:** `Layout.Open`
- **Expected:** valid. Once the feature exists, elements and groups are placed freely with no shared grid. Line routing follows the same rules (straight runs, rounded corners, no overlaps). Only placement may differ from A143.

```mermaid
---
config:
  layout: open
---
enterprise
  application-component shop "Web Shop"
  application-component orders "Order Manager"
  application-component erp "ERP"
  application-component ledger "General Ledger"
  shop -flow-> orders "order"
  orders -flow-> erp "booking"
  erp -flow-> ledger "journal"
```

### A145 - Layout mode: default with no setting

For the layout mode feature. Same diagram as A143 with no `layout` setting.

- **Recognition:** `Config.Layout.Default`, `Relationship.Flow`
- **Rendering:** `Layout.Grid`
- **Expected:** valid. Once the feature exists, `grid` is the default and the result is the same as A143.

```mermaid
enterprise
  application-component shop "Web Shop"
  application-component orders "Order Manager"
  application-component erp "ERP"
  application-component ledger "General Ledger"
  shop -flow-> orders "order"
  orders -flow-> erp "booking"
  erp -flow-> ledger "journal"
```

### A146 - Layout mode: open with nested groups

For the layout mode feature. `layout: open` with nested groups. Groups and members are declared first, relationships later.

- **Recognition:** `Config.Layout.Open`, `Group.Declaration`, `Group.In.Group`, `Relationship.Flow`
- **Rendering:** `Layout.Open`, `Layout.Compound.Nested`
- **Expected:** valid. Once the feature exists, groups and elements are placed freely, group boxes never overlap, and lines follow the usual routing rules.

```mermaid
---
config:
  layout: open
---
enterprise
  group estate "Application estate"
  group finance "Finance systems" in estate
  application-component shop "Web Shop" in estate
  application-component ledger "General Ledger" in finance
  application-component payroll in finance
  shop -flow-> ledger "sales journal"
  payroll -flow-> ledger "pay journal"
```

### A147 - Layout mode: grid with fan-out and many-to-many

For the layout mode feature. `layout: grid` with one source feeding three consumers and a many-to-many block between applications and services. Elements are declared first.

- **Recognition:** `Config.Layout.Grid`, `Relationship.Flow`, `Relationship.Serving`
- **Rendering:** `Layout.Grid`, `Routing.FanOut`
- **Expected:** valid. Once the feature exists, all elements align to one shared grid with straight runs, rounded corners and no overlapping lines.

```mermaid
---
config:
  layout: grid
---
enterprise
  application-component hub "Integration Hub"
  application-component crm "CRM"
  application-component erp "ERP"
  application-component dwh
  application-service customers "Customer Data"
  application-service invoices "Invoice Data"
  application-service reports
  hub -flow-> crm
  hub -flow-> erp
  hub -flow-> dwh
  crm -serving-> customers
  crm -serving-> invoices
  erp -serving-> invoices
  erp -serving-> reports
  dwh -serving-> customers
  dwh -serving-> reports
```

### A148 - Layout mode: open with cross-boundary links

For the layout mode feature. `layout: open` with two groups and links that cross the group boundaries. Groups and elements are declared first.

- **Recognition:** `Config.Layout.Open`, `Group.Declaration`, `Relationship.Flow`, `Relationship.Serving`
- **Rendering:** `Layout.Open`, `Routing.CrossBoundary`
- **Expected:** valid. Once the feature exists, groups and elements are placed freely, and links crossing group boundaries follow the usual routing rules.

```mermaid
---
config:
  layout: open
---
enterprise
  group frontEnd "Front end"
  group backEnd "Back end"
  application-component shop "Web Shop" in frontEnd
  application-component portal in frontEnd
  application-component orders "Order Manager" in backEnd
  application-component erp "ERP" in backEnd
  node server "App Server"
  business-process fulfil "Fulfil Order"
  shop -flow-> orders "order"
  portal -flow-> orders "request"
  orders -flow-> erp "booking"
  server -serving-> orders
  orders -serving-> fulfil
  erp -serving-> fulfil
```

### A149 - Layout mode: unknown value falls back to grid

For the layout mode feature. `layout: freeform` is not a known value.

- **Recognition:** `Config.Frontmatter`, `Config.Layout.Unknown`, `Relationship.Flow`
- **Rendering:** `Layout.Grid`
- **Expected:** valid, with exactly one warning about the unknown layout value. Once the feature exists, the diagram is laid out as `grid`, the same as A145.

```mermaid
---
config:
  layout: freeform
---
enterprise
  application-component shop "Web Shop"
  application-component orders "Order Manager"
  application-component erp "ERP"
  application-component ledger "General Ledger"
  shop -flow-> orders "order"
  orders -flow-> erp "booking"
  erp -flow-> ledger "journal"
```

### A150 - Port placement: aggregation to centred and side targets

For the port placement and under-the-parent rules. One collaboration aggregates two components, and one of them is composed of an interface. Elements are declared first.

- **Recognition:** `Application.Collaboration`, `Application.Component`, `Application.Interface`, `Relationship.Aggregation`, `Relationship.Composition`
- **Rendering:** `Layout.PortPlacement`, `Layout.UnderParent`
- **Expected:** valid. Web Shop sits directly under Order Suite and its line is straight from the centre ports. Order Manager is beside Web Shop; its line leaves a side port, runs across once and drops straight into the top of Order Manager. Shop API sits directly under Web Shop (rules 4.7 and 2.17).

```mermaid
enterprise
  application-collaboration orderSuite "Order Suite"
  application-component webShop "Web Shop"
  application-component orderMgr "Order Manager"
  application-interface shopApi "Shop API"
  orderSuite -aggregation-> webShop
  orderSuite -aggregation-> orderMgr
  webShop -composition-> shopApi
```

### A151 - Main path and converging flows

For the main path, under-the-parent and port placement rules. Three inputs converge on one process, and the process realizes a service. Stock Check has two lines into Place Order, one triggering and one flow. Elements are declared first.

- **Recognition:** `Business.Event`, `Business.Function`, `Business.Interaction`, `Business.Process`, `Business.Service`, `Relationship.Flow`, `Relationship.Triggering`, `Relationship.Realization`
- **Rendering:** `Layout.MainPath`, `Layout.UnderParent`, `Layout.PortPlacement`
- **Expected:** valid. Place Order is centred under its three inputs, with Pricing directly above it on the centre line. The lines enter its top on separate ports, in target order. Order Service sits directly under Place Order with a straight line (rules 2.17, 2.18 and 4.7).

```mermaid
enterprise
  business-event basketSubmitted "Basket Submitted"
  business-function pricing "Pricing"
  business-interaction stockCheck "Stock Check"
  business-process placeOrder "Place Order"
  business-service orderSvc "Order Service"
  basketSubmitted -flow-> placeOrder
  pricing -flow-> placeOrder
  stockCheck -triggering-> placeOrder
  stockCheck -flow-> placeOrder
  placeOrder -realization-> orderSvc
```

### A152 - Fewest corners: Order handling application layer

For the fewest-corners, port placement and under-the-parent rules. An application collaboration aggregates a web shop and an order manager; the web shop and the checkout feed an order service and an interaction. Elements are declared first, relationships later.

- **Recognition:** `Application.Collaboration`, `Application.Component`, `Application.Interface`, `Application.Function`, `Application.Service`, `Application.Process`, `Application.Event`, `Application.Interaction`, `Application.DataObject`, `Relationship.Aggregation`, `Relationship.Composition`, `Relationship.Serving`, `Relationship.Realization`, `Relationship.Triggering`, `Relationship.Flow`, `Relationship.Access`
- **Rendering:** `Layout.FewestCorners`, `Layout.PortPlacement`
- **Expected:** valid. Order Suite to Web Shop and Checkout to Order Service are straight centre drops (rule 4.7). Web Shop to Checkout and Shop API to Order Service take one corner each, leaving the side that faces the target (rule 4.8). No line crosses a box.

```mermaid
enterprise
  application-collaboration orderSuite "Order Suite"
  application-component webShop "Web Shop"
  application-component orderMgr "Order Manager"
  application-interface shopApi "Shop API"
  application-function checkout "Checkout"
  application-service orderSvc "Order Service"
  application-process placeOrder "Place Order"
  application-event orderPlaced "Order Placed"
  application-interaction orderSync "Order Synchronisation"
  data-object order "Order"
  orderSuite -aggregation-> webShop
  orderSuite -aggregation-> orderMgr
  webShop -composition-> shopApi
  webShop -serving-> checkout
  shopApi -serving-> orderSvc
  checkout -realization-> orderSvc
  orderSvc -triggering-> placeOrder
  placeOrder -triggering-> orderPlaced
  orderPlaced -triggering-> orderSync
  orderSuite -flow-> orderSync
  checkout -flow-> orderSync "order details"
  placeOrder -access(write)-> order
  orderSync -access(read)-> order
```

### A153 - Clusters: two linked 5-node clusters

For the cluster rule. Two groups of five application components: every component is linked to every other in its own group by an association, and every component of the first group is linked to every component of the second by a flow. There are no group frames. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Relationship.Association`, `Relationship.Flow`
- **Rendering:** `Layout.Clusters`, `Layout.ManyToMany`
- **Expected:** valid. Each cluster is placed whole, and the two clusters face each other, rank against rank. The lines between them follow the many-to-many rule, each with its own port and track. The diagram is near 4:3 because the clusters move, not individual nodes (rules 2.20 and 2.13).

```mermaid
enterprise
  application-component a1 "Cluster A1"
  application-component a2 "Cluster A2"
  application-component a3 "Cluster A3"
  application-component a4 "Cluster A4"
  application-component a5 "Cluster A5"
  application-component b1 "Cluster B1"
  application-component b2 "Cluster B2"
  application-component b3 "Cluster B3"
  application-component b4 "Cluster B4"
  application-component b5 "Cluster B5"
  a1 -association-> a2
  a1 -association-> a3
  a1 -association-> a4
  a1 -association-> a5
  a2 -association-> a3
  a2 -association-> a4
  a2 -association-> a5
  a3 -association-> a4
  a3 -association-> a5
  a4 -association-> a5
  b1 -association-> b2
  b1 -association-> b3
  b1 -association-> b4
  b1 -association-> b5
  b2 -association-> b3
  b2 -association-> b4
  b2 -association-> b5
  b3 -association-> b4
  b3 -association-> b5
  b4 -association-> b5
  a1 -flow-> b1
  a1 -flow-> b2
  a1 -flow-> b3
  a1 -flow-> b4
  a1 -flow-> b5
  a2 -flow-> b1
  a2 -flow-> b2
  a2 -flow-> b3
  a2 -flow-> b4
  a2 -flow-> b5
  a3 -flow-> b1
  a3 -flow-> b2
  a3 -flow-> b3
  a3 -flow-> b4
  a3 -flow-> b5
  a4 -flow-> b1
  a4 -flow-> b2
  a4 -flow-> b3
  a4 -flow-> b4
  a4 -flow-> b5
  a5 -flow-> b1
  a5 -flow-> b2
  a5 -flow-> b3
  a5 -flow-> b4
  a5 -flow-> b5
```

### A154 - Hints: a row of five

Five components, each hinted right-of the previous one. Stress test of rule 2.21. Requires hint syntax support: the converter does not parse hints yet, so conversion may report errors. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Relationship.Flow`
- **Rendering:** `Layout.Hints`, `Layout.PortPlacement`
- **Expected:** valid once hint syntax is supported. No failure: every hint is honoured, or dropped and reported, or ignored and reported, as rule 2.21 says. All four hints are honoured (each scores -30). The five components form one row in order, with straight lines between them.

```mermaid
enterprise
  application-component a1 "Part 1"
  application-component a2 "Part 2" right-of a1
  application-component a3 "Part 3" right-of a2
  application-component a4 "Part 4" right-of a3
  application-component a5 "Part 5" right-of a4
  a1 -flow-> a2
  a2 -flow-> a3
  a3 -flow-> a4
  a4 -flow-> a5
```

### A155 - Hints: conflict

Two components hinted right-of each other. Stress test of rule 2.21. Requires hint syntax support: the converter does not parse hints yet, so conversion may report errors. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Relationship.Flow`
- **Rendering:** `Layout.Hints`, `Layout.PortPlacement`
- **Expected:** valid once hint syntax is supported. No failure: every hint is honoured, or dropped and reported, or ignored and reported, as rule 2.21 says. The first hint (A right-of B) is honoured. The later hint (B right-of A) would make the set impossible, so it is dropped and reported; B is laid out as if unhinted.

```mermaid
enterprise
  application-component a "A" right-of b
  application-component b "B" right-of a
  a -flow-> b
```

### A156 - Hints: circular chain

Three components hinted below each other in a circle. Stress test of rule 2.21. Requires hint syntax support: the converter does not parse hints yet, so conversion may report errors. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Relationship.Flow`
- **Rendering:** `Layout.Hints`, `Layout.PortPlacement`
- **Expected:** valid once hint syntax is supported. No failure: every hint is honoured, or dropped and reported, or ignored and reported, as rule 2.21 says. The first two hints (A below B, B below C) are honoured. The third hint (C below A) closes the circle, so it is dropped and reported.

```mermaid
enterprise
  application-component a "A" below b
  application-component b "B" below c
  application-component c "C" below a
  a -flow-> b
  b -flow-> c
```

### A157 - Hints: against the flow

A top-to-bottom chain of six, where every node is hinted above its parent. Stress test of rule 2.21. Requires hint syntax support: the converter does not parse hints yet, so conversion may report errors. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Relationship.Flow`
- **Rendering:** `Layout.Hints`, `Layout.PortPlacement`
- **Expected:** valid once hint syntax is supported. No failure: every hint is honoured, or dropped and reported, or ignored and reported, as rule 2.21 says. All five node hints are honoured: each node is above its parent, so the chain runs upward against the declared direction, which is kept. Lines are routed legally by rules 4.7 and 4.8 with the fewest corners, and space is inserted where needed (rule 1.7).

```mermaid
enterprise
  application-component c1 "Step 1"
  application-component c2 "Step 2" above c1
  application-component c3 "Step 3" above c2
  application-component c4 "Step 4" above c3
  application-component c5 "Step 5" above c4
  application-component c6 "Step 6" above c5
  c1 -flow-> c2
  c2 -flow-> c3
  c3 -flow-> c4
  c4 -flow-> c5
  c5 -flow-> c6
```

### A158 - Hints: missing and cross-group peers

One hint names a peer that does not exist, and one names a node in another group frame. Stress test of rule 2.21. Requires hint syntax support: the converter does not parse hints yet, so conversion may report errors. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Relationship.Flow`, `Group`
- **Rendering:** `Layout.Hints`, `Layout.PortPlacement`
- **Expected:** valid once hint syntax is supported. No failure: every hint is honoured, or dropped and reported, or ignored and reported, as rule 2.21 says. Both hints are ignored and reported (P right-of ghost: the peer is missing; Q left-of R: the peer is in another group). The nodes are laid out as if unhinted.

```mermaid
enterprise
  group g1 "Group 1"
  group g2 "Group 2"
  application-component p "P" in g1 right-of ghost
  application-component q "Q" in g1 left-of r
  application-component r "R" in g2
  application-component s "S" in g1
  p -flow-> q
  q -flow-> r
  s -flow-> p
```

### A159 - Hints: same cell

Two components both hinted right-of the same peer. Stress test of rule 2.21. Requires hint syntax support: the converter does not parse hints yet, so conversion may report errors. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Relationship.Flow`
- **Rendering:** `Layout.Hints`, `Layout.PortPlacement`
- **Expected:** valid once hint syntax is supported. No failure: every hint is honoured, or dropped and reported, or ignored and reported, as rule 2.21 says. X is placed in the cell right of Hub. Y wants the same cell, so it takes the next free cell on that side, and both hints are honoured.

```mermaid
enterprise
  application-component hub "Hub"
  application-component x "X" right-of hub
  application-component y "Y" right-of hub
  hub -flow-> x
  hub -flow-> y
```

### A160 - Hints: line hints on a fan

One hub with 16 targets, and every line hinted leave S, enter N. There are more lines than port slots. Stress test of rule 2.21. Requires hint syntax support: the converter does not parse hints yet, so conversion may report errors. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Relationship.Flow`
- **Rendering:** `Layout.Hints`, `Layout.PortPlacement`
- **Expected:** valid once hint syntax is supported. No failure: every hint is honoured, or dropped and reported, or ignored and reported, as rule 2.21 says. All 16 line hints are honoured (each scores -15). The 16 lines on one side exceed the 5 slots, so the node grows along that side by whole grid steps (rule 3.2), and every line has its own port and track.

```mermaid
enterprise
  application-component hub "Hub"
  application-component t1 "Target 1"
  application-component t2 "Target 2"
  application-component t3 "Target 3"
  application-component t4 "Target 4"
  application-component t5 "Target 5"
  application-component t6 "Target 6"
  application-component t7 "Target 7"
  application-component t8 "Target 8"
  application-component t9 "Target 9"
  application-component t10 "Target 10"
  application-component t11 "Target 11"
  application-component t12 "Target 12"
  application-component t13 "Target 13"
  application-component t14 "Target 14"
  application-component t15 "Target 15"
  application-component t16 "Target 16"
  hub -flow-> t1 [leave S, enter N]
  hub -flow-> t2 [leave S, enter N]
  hub -flow-> t3 [leave S, enter N]
  hub -flow-> t4 [leave S, enter N]
  hub -flow-> t5 [leave S, enter N]
  hub -flow-> t6 [leave S, enter N]
  hub -flow-> t7 [leave S, enter N]
  hub -flow-> t8 [leave S, enter N]
  hub -flow-> t9 [leave S, enter N]
  hub -flow-> t10 [leave S, enter N]
  hub -flow-> t11 [leave S, enter N]
  hub -flow-> t12 [leave S, enter N]
  hub -flow-> t13 [leave S, enter N]
  hub -flow-> t14 [leave S, enter N]
  hub -flow-> t15 [leave S, enter N]
  hub -flow-> t16 [leave S, enter N]
```

### A161 - Hints: all lines against the flow

A top-to-bottom chain of eight, where every line is hinted leave N. Stress test of rule 2.21. Requires hint syntax support: the converter does not parse hints yet, so conversion may report errors. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Relationship.Flow`
- **Rendering:** `Layout.Hints`, `Layout.PortPlacement`
- **Expected:** valid once hint syntax is supported. No failure: every hint is honoured, or dropped and reported, or ignored and reported, as rule 2.21 says. All seven line hints are honoured: each line leaves the top of its source and uses the corners its hint needs, without the corner limit of rule 4.3. Those corners are scored (rule 2.13) and space is inserted where needed (rule 1.7).

```mermaid
enterprise
  application-component d1 "Node 1"
  application-component d2 "Node 2"
  application-component d3 "Node 3"
  application-component d4 "Node 4"
  application-component d5 "Node 5"
  application-component d6 "Node 6"
  application-component d7 "Node 7"
  application-component d8 "Node 8"
  d1 -flow-> d2 [leave N]
  d2 -flow-> d3 [leave N]
  d3 -flow-> d4 [leave N]
  d4 -flow-> d5 [leave N]
  d5 -flow-> d6 [leave N]
  d6 -flow-> d7 [leave N]
  d7 -flow-> d8 [leave N]
```

### A162 - Hints: group and children

A group hinted right-of an outside node, with children hinted inside it. Stress test of rule 2.21. Requires hint syntax support: the converter does not parse hints yet, so conversion may report errors. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Relationship.Flow`, `Group`
- **Rendering:** `Layout.Hints`, `Layout.PortPlacement`
- **Expected:** valid once hint syntax is supported. No failure: every hint is honoured, or dropped and reported, or ignored and reported, as rule 2.21 says. All hints are honoured: the group frame is right of Outside, Kid 2 is below Kid 1 and Kid 3 is right of Kid 1, all inside the frame.

```mermaid
enterprise
  application-component out "Outside"
  group box "Box" right-of out
  application-component k1 "Kid 1" in box
  application-component k2 "Kid 2" in box below k1
  application-component k3 "Kid 3" in box right-of k1
  out -flow-> k1
  k1 -flow-> k2
  k1 -flow-> k3
```

### A163 - Hints: scale

160 components labelled 1 to 160 in a chain with branches, where every fourth is hinted (the first push E, the others right-of the previous hinted node), 40 hints in all. Stress test of rule 2.21. Requires hint syntax support: the converter does not parse hints yet, so conversion may report errors. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Relationship.Flow`
- **Rendering:** `Layout.Hints`, `Layout.PortPlacement`
- **Expected:** valid once hint syntax is supported. No failure: every hint is honoured, or dropped and reported, or ignored and reported, as rule 2.21 says. All 40 hints are honoured. The first, push E, puts node 4 in the extreme east cell of the diagram, and each later hinted node is right of the previous hinted node. The hinted nodes keep their relation to their peers; the other 120 are laid out around them; no failure at any size.

```mermaid
enterprise
  application-component m1 "1"
  application-component m2 "2"
  application-component m3 "3"
  application-component m4 "4" push E
  application-component m5 "5"
  application-component m6 "6"
  application-component m7 "7"
  application-component m8 "8" right-of m4
  application-component m9 "9"
  application-component m10 "10"
  application-component m11 "11"
  application-component m12 "12" right-of m8
  application-component m13 "13"
  application-component m14 "14"
  application-component m15 "15"
  application-component m16 "16" right-of m12
  application-component m17 "17"
  application-component m18 "18"
  application-component m19 "19"
  application-component m20 "20" right-of m16
  application-component m21 "21"
  application-component m22 "22"
  application-component m23 "23"
  application-component m24 "24" right-of m20
  application-component m25 "25"
  application-component m26 "26"
  application-component m27 "27"
  application-component m28 "28" right-of m24
  application-component m29 "29"
  application-component m30 "30"
  application-component m31 "31"
  application-component m32 "32" right-of m28
  application-component m33 "33"
  application-component m34 "34"
  application-component m35 "35"
  application-component m36 "36" right-of m32
  application-component m37 "37"
  application-component m38 "38"
  application-component m39 "39"
  application-component m40 "40" right-of m36
  application-component m41 "41"
  application-component m42 "42"
  application-component m43 "43"
  application-component m44 "44" right-of m40
  application-component m45 "45"
  application-component m46 "46"
  application-component m47 "47"
  application-component m48 "48" right-of m44
  application-component m49 "49"
  application-component m50 "50"
  application-component m51 "51"
  application-component m52 "52" right-of m48
  application-component m53 "53"
  application-component m54 "54"
  application-component m55 "55"
  application-component m56 "56" right-of m52
  application-component m57 "57"
  application-component m58 "58"
  application-component m59 "59"
  application-component m60 "60" right-of m56
  application-component m61 "61"
  application-component m62 "62"
  application-component m63 "63"
  application-component m64 "64" right-of m60
  application-component m65 "65"
  application-component m66 "66"
  application-component m67 "67"
  application-component m68 "68" right-of m64
  application-component m69 "69"
  application-component m70 "70"
  application-component m71 "71"
  application-component m72 "72" right-of m68
  application-component m73 "73"
  application-component m74 "74"
  application-component m75 "75"
  application-component m76 "76" right-of m72
  application-component m77 "77"
  application-component m78 "78"
  application-component m79 "79"
  application-component m80 "80" right-of m76
  application-component m81 "81"
  application-component m82 "82"
  application-component m83 "83"
  application-component m84 "84" right-of m80
  application-component m85 "85"
  application-component m86 "86"
  application-component m87 "87"
  application-component m88 "88" right-of m84
  application-component m89 "89"
  application-component m90 "90"
  application-component m91 "91"
  application-component m92 "92" right-of m88
  application-component m93 "93"
  application-component m94 "94"
  application-component m95 "95"
  application-component m96 "96" right-of m92
  application-component m97 "97"
  application-component m98 "98"
  application-component m99 "99"
  application-component m100 "100" right-of m96
  application-component m101 "101"
  application-component m102 "102"
  application-component m103 "103"
  application-component m104 "104" right-of m100
  application-component m105 "105"
  application-component m106 "106"
  application-component m107 "107"
  application-component m108 "108" right-of m104
  application-component m109 "109"
  application-component m110 "110"
  application-component m111 "111"
  application-component m112 "112" right-of m108
  application-component m113 "113"
  application-component m114 "114"
  application-component m115 "115"
  application-component m116 "116" right-of m112
  application-component m117 "117"
  application-component m118 "118"
  application-component m119 "119"
  application-component m120 "120" right-of m116
  application-component m121 "121"
  application-component m122 "122"
  application-component m123 "123"
  application-component m124 "124" right-of m120
  application-component m125 "125"
  application-component m126 "126"
  application-component m127 "127"
  application-component m128 "128" right-of m124
  application-component m129 "129"
  application-component m130 "130"
  application-component m131 "131"
  application-component m132 "132" right-of m128
  application-component m133 "133"
  application-component m134 "134"
  application-component m135 "135"
  application-component m136 "136" right-of m132
  application-component m137 "137"
  application-component m138 "138"
  application-component m139 "139"
  application-component m140 "140" right-of m136
  application-component m141 "141"
  application-component m142 "142"
  application-component m143 "143"
  application-component m144 "144" right-of m140
  application-component m145 "145"
  application-component m146 "146"
  application-component m147 "147"
  application-component m148 "148" right-of m144
  application-component m149 "149"
  application-component m150 "150"
  application-component m151 "151"
  application-component m152 "152" right-of m148
  application-component m153 "153"
  application-component m154 "154"
  application-component m155 "155"
  application-component m156 "156" right-of m152
  application-component m157 "157"
  application-component m158 "158"
  application-component m159 "159"
  application-component m160 "160" right-of m156
  m1 -flow-> m2
  m2 -flow-> m3
  m3 -flow-> m4
  m4 -flow-> m5
  m5 -flow-> m6
  m6 -flow-> m7
  m7 -flow-> m8
  m8 -flow-> m9
  m9 -flow-> m10
  m10 -flow-> m11
  m11 -flow-> m12
  m12 -flow-> m13
  m13 -flow-> m14
  m14 -flow-> m15
  m15 -flow-> m16
  m16 -flow-> m17
  m17 -flow-> m18
  m18 -flow-> m19
  m19 -flow-> m20
  m20 -flow-> m21
  m21 -flow-> m22
  m22 -flow-> m23
  m23 -flow-> m24
  m24 -flow-> m25
  m25 -flow-> m26
  m26 -flow-> m27
  m27 -flow-> m28
  m28 -flow-> m29
  m29 -flow-> m30
  m30 -flow-> m31
  m31 -flow-> m32
  m32 -flow-> m33
  m33 -flow-> m34
  m34 -flow-> m35
  m35 -flow-> m36
  m36 -flow-> m37
  m37 -flow-> m38
  m38 -flow-> m39
  m39 -flow-> m40
  m40 -flow-> m41
  m41 -flow-> m42
  m42 -flow-> m43
  m43 -flow-> m44
  m44 -flow-> m45
  m45 -flow-> m46
  m46 -flow-> m47
  m47 -flow-> m48
  m48 -flow-> m49
  m49 -flow-> m50
  m50 -flow-> m51
  m51 -flow-> m52
  m52 -flow-> m53
  m53 -flow-> m54
  m54 -flow-> m55
  m55 -flow-> m56
  m56 -flow-> m57
  m57 -flow-> m58
  m58 -flow-> m59
  m59 -flow-> m60
  m60 -flow-> m61
  m61 -flow-> m62
  m62 -flow-> m63
  m63 -flow-> m64
  m64 -flow-> m65
  m65 -flow-> m66
  m66 -flow-> m67
  m67 -flow-> m68
  m68 -flow-> m69
  m69 -flow-> m70
  m70 -flow-> m71
  m71 -flow-> m72
  m72 -flow-> m73
  m73 -flow-> m74
  m74 -flow-> m75
  m75 -flow-> m76
  m76 -flow-> m77
  m77 -flow-> m78
  m78 -flow-> m79
  m79 -flow-> m80
  m80 -flow-> m81
  m81 -flow-> m82
  m82 -flow-> m83
  m83 -flow-> m84
  m84 -flow-> m85
  m85 -flow-> m86
  m86 -flow-> m87
  m87 -flow-> m88
  m88 -flow-> m89
  m89 -flow-> m90
  m90 -flow-> m91
  m91 -flow-> m92
  m92 -flow-> m93
  m93 -flow-> m94
  m94 -flow-> m95
  m95 -flow-> m96
  m96 -flow-> m97
  m97 -flow-> m98
  m98 -flow-> m99
  m99 -flow-> m100
  m100 -flow-> m101
  m101 -flow-> m102
  m102 -flow-> m103
  m103 -flow-> m104
  m104 -flow-> m105
  m105 -flow-> m106
  m106 -flow-> m107
  m107 -flow-> m108
  m108 -flow-> m109
  m109 -flow-> m110
  m110 -flow-> m111
  m111 -flow-> m112
  m112 -flow-> m113
  m113 -flow-> m114
  m114 -flow-> m115
  m115 -flow-> m116
  m116 -flow-> m117
  m117 -flow-> m118
  m118 -flow-> m119
  m119 -flow-> m120
  m120 -flow-> m121
  m121 -flow-> m122
  m122 -flow-> m123
  m123 -flow-> m124
  m124 -flow-> m125
  m125 -flow-> m126
  m126 -flow-> m127
  m127 -flow-> m128
  m128 -flow-> m129
  m129 -flow-> m130
  m130 -flow-> m131
  m131 -flow-> m132
  m132 -flow-> m133
  m133 -flow-> m134
  m134 -flow-> m135
  m135 -flow-> m136
  m136 -flow-> m137
  m137 -flow-> m138
  m138 -flow-> m139
  m139 -flow-> m140
  m140 -flow-> m141
  m141 -flow-> m142
  m142 -flow-> m143
  m143 -flow-> m144
  m144 -flow-> m145
  m145 -flow-> m146
  m146 -flow-> m147
  m147 -flow-> m148
  m148 -flow-> m149
  m149 -flow-> m150
  m150 -flow-> m151
  m151 -flow-> m152
  m152 -flow-> m153
  m153 -flow-> m154
  m154 -flow-> m155
  m155 -flow-> m156
  m156 -flow-> m157
  m157 -flow-> m158
  m158 -flow-> m159
  m159 -flow-> m160
  m1 -flow-> m3
  m11 -flow-> m13
  m21 -flow-> m23
  m31 -flow-> m33
  m41 -flow-> m43
  m51 -flow-> m53
  m61 -flow-> m63
  m71 -flow-> m73
  m81 -flow-> m83
  m91 -flow-> m93
  m101 -flow-> m103
  m111 -flow-> m113
  m121 -flow-> m123
  m131 -flow-> m133
  m141 -flow-> m143
  m151 -flow-> m153
```
---

## Scenario index

| ID | Level | Topic |
| --- | --- | --- |
| A01–A10 | 1 | Header, version, direction, empty diagram, single element |
| A11–A14 | 2 | Application layer (9 element types) |
| A15–A18 | 3 | Technology layer (13 element types) |
| A19–A20 | 4 | Physical elements (4 element types) |
| A21–A24 | 5 | Business layer (13 element types) |
| A25–A26 | 6 | Strategy (4 element types) |
| A27–A29 | 7 | Motivation (10 element types) |
| A30–A31 | 8 | Implementation and migration (5 element types) |
| A32–A36 | 9 | Grouping, location and junctions |
| A37–A48 | 10 | Every relationship type and qualifier, junctions |
| A49–A57 | 11 | Labels and text, including explicit line breaks and entity codes |
| A58–A65 | 12 | Nesting and containment (A65 expects a warning) |
| A66–A72 | 13 | Cross-layer and derived relationships |
| A73–A77 | 14 | Notation and styling, including icon notation through configuration |
| A78–A94 | 15 | Invalid relationships and parse errors (**expected errors**, except A85) |
| A95–A103 | 16 | Recognition edge cases (A102 **expects errors**) |
| A104–A112 | 17 | Layout stress |
| A113–A117 | 18 | Large viewpoints (31–45 elements each) |
| A118–A130 | 19 | Mermaid conventions (A129 and A130 **expect diagnostics**) |
| A131–A142 | 20 | Elements declared first, relationships later |

---

## Source differences

The design study and the reference implementation on the `enterprise-diagram` branch disagree in places, and both are silent on some points. The last column shows what the catalogue follows; the numbers refer to the [Syntax decisions](#syntax-decisions) table. Where a topic is ordinary Mermaid syntax, the catalogue follows Mermaid.

| Topic | Design study | Reference implementation | Catalogue follows |
| --- | --- | --- | --- |
| Keyword | Use the `enterprise` diagram keyword and its `enterprise-beta` alias | `enterprise`, also `enterprise-beta`, in any letter case | Implementation, plus a Mermaid-style direction after the keyword (14, 24) |
| Default version | Original text: 3.1; implementation note: 3.2 | 3.2 | Both: 3.2, with 3.1 selectable (22) |
| Element types | 22 Application and Technology types | 61 types: adds Business, Strategy, Motivation, Physical, Implementation and Migration, `grouping`, `location` and `junction`, plus the aliases `group`, `and-junction`, `or-junction`, `junction-and`, `junction-or` and hyphen-free spellings | Implementation (1) |
| Junctions | Not supported (open question) | Element types; may carry a label, drawn beside the junction | Implementation (2) |
| `group` | Visual only, no relationships; `in` names a group | Registered as a Grouping element that takes part in relationships; `in` may name any container | Implementation (3) |
| Influence | Always rejected (no in-scope pair allows it); qualifier `n` not defined further | Allowed between motivation elements; numeric strength is a signed integer | Implementation (4) |
| 3.1 / 3.2 differences | 15 Application and Technology cells | 211 cells differ across all 61 types (including junction cells); only the 15 carry the per-relationship provisional note, the rest are covered by one general note | Implementation rules table, all 3.2 changes provisional (22) |
| Direction values | `TB`, `LR` | `TB`, `LR` | Mermaid: `TB`, `TD`, `BT`, `LR`, `RL` (23) |
| Comments | `%%` lines | Everything from `%%` to the end of the line is ignored, including inside a quoted label | Implementation and Mermaid for trailing comments; Mermaid for labels, where `%%` is text (15) |
| Label escapes | Not defined | Backslash escapes inside quotes; a backslash before any other character is dropped | Implementation's `\"`, `\'` and `\\`, plus Mermaid's `\n` and entity codes; other backslashes kept (6, 7) |
| Line breaks in labels | Not defined | Not defined (`<br>` is drawn as text) | Mermaid: `<br>` forms and `\n` (6) |
| Single-quoted and markdown labels | Not defined | Not accepted | Mermaid (28, 29) |
| Empty label `""` | Not defined | No text | Implementation (8) |
| Styling | Not defined | Not defined | Mermaid: `classDef`, `class`, `:::`, `style` and `linkStyle` (9) |
| Icon notation | Open question | Not supported | Mermaid-style configuration key, `box` by default (10) |
| Semicolons | Not defined | Not accepted (the line fails to parse) | Mermaid: statement separator (11) |
| Identifiers | Not defined | `[A-Za-z_][\w.]*` | Mermaid flowchart ids: letters, digits, `_`, `.`, `-` (12) |
| Reserved words | Not defined | None; a relationship whose source is `title` is read as a `title` statement | Statement keywords reserved, as Mermaid reserves its own (13) |
| Letter case | Not defined | Header, directives, element types and relationship names case-insensitive; ids case-sensitive | Implementation (14) |
| Accessibility, frontmatter, `%%{init}%%` | Not defined | Detection skips frontmatter and directives, but the parser rejects them | Mermaid (25, 26, 27) |
| Forward references | Not defined | Accepted | Implementation (16) |
| Repeated directives | Not defined | Last one wins | Implementation (17) |
| Redeclared ids | Not defined | Error ("duplicate element id") | Mermaid for the same type, error for a different type (30) |
| Duplicate and self relationships | Not defined | Accepted and drawn | Implementation (20) |

---

## Syntax decisions

The catalogue was written with 22 open syntax questions. They were first decided with these principles, in order: (a) do not contradict what the reference parser already does unless it is clearly wrong; (b) follow an existing Mermaid convention where there is one; (c) keep the grammar small and unambiguous; (d) relationship rules version 3.2 by default, 3.1 selectable. Dave then ruled that **anything that is normal Mermaid syntax must be accepted**, which overrides (a) and (c) wherever they would reject a Mermaid form. Every decision was re-checked against that rule; rows 23 to 30 cover Mermaid forms that the original questions did not mention. The Enterprise-specific grammar (element types, relationship forms, qualifiers and version rules) keeps its own decisions. When a decision changes, update the scenarios in its last column and the counts at the top.

| # | Question | Status | Decision | Reason | Scenarios affected |
| --- | --- | --- | --- | --- | --- |
| 1 | Element type names outside Application and Technology; hyphen-free and CamelCase spellings | Unchanged | The 61 kebab-case names of the reference implementation are the canonical names. Hyphen-free spellings and the specification's CamelCase names are accepted too, in any letter case. | This is what the parser does, and the design study already accepts CamelCase. Enterprise-specific. | A11–A36, A98, A103 |
| 2 | Junction spellings, meaning of a bare `junction`, junction labels | Unchanged | Write `and-junction` or `or-junction`. A bare `junction` is an and-junction, and `junction-and`, `junction-or` and the hyphen-free forms stay as accepted aliases. A junction may have a label, drawn in small text beside the circle. | Parser and renderer behaviour; the specification's default junction is an and-junction. | A35, A36, A47, A48, A73, A107, A112 |
| 3 | Is `group` visual only or a Grouping element? May a group sit in an element? | Unchanged | `group` is shorthand for a Grouping element and can take part in relationships. `in` may name a group or any element; a group placed in an element follows the normal nesting-warning rule. | Parser behaviour; `in` for containment also matches Mermaid's architecture diagrams. | A32, A33, A56, A58, A61, A62, A73, A93, A111, A114, A127 |
| 4 | Influence strength values and drawing | Unchanged | The qualifier is `+`, `-`, `++`, `--` or a signed whole number such as `3`, `-2` or `+1`. Decimals and words are errors. The qualifier is drawn as text beside the line. | Parser behaviour. Enterprise-specific. | A27, A29, A41, A42, A46, A68, A74, A81, A91, A116 |
| 5 | Other undirected or two-way relationship forms | Unchanged | None. Only association has an undirected form (the default) and a directed form, `association(directed)`. | The specification has no other undirected relationship. Enterprise-specific. | A45, A74, A91 |
| 6 | Explicit line breaks in labels | Revised | `<br>`, `<br/>` and `<br />` (any letter case) and `\n` break element, group and relationship labels. A literal backslash and `n` is written `\\n`. The title stays on one line. | Mermaid uses both: `<br>` in labels and `\n` in several diagram types. | A56, A125 |
| 7 | Double quotes and `%%` inside labels | Revised | `\"` (and `\'`, `\\`) work as in the parser, alongside Mermaid entity codes such as `#quot;`, `#name;` and `#123;`. A backslash before any other character stays as typed, so `C:\data` keeps its backslash. `%%` inside a quoted label is text. | Keeps the parser's escapes and adds Mermaid's. Keeping other backslashes fixes the parser silently dropping them from paths, and `%%` in a label must not be cut off. | A57, A125, A51 |
| 8 | Does `""` mean no text or the id? | Unchanged | No text; the box is still drawn at its minimum size. Leaving the label out uses the id. | Parser behaviour. | A02, A53 |
| 9 | Styling hooks | Revised | `classDef` (including `classDef default` and several names at once), `class`, `:::className`, `style` and `linkStyle`, as in Mermaid flowcharts. `:::` may follow an id or a label, and may be attached to an element named in a relationship line. `linkStyle` takes a list of numbers or `default`, an optional `interpolate <curve>`, and any property Mermaid accepts for links, with `\,` for a comma in a value. A class that is never defined is quietly ignored. `class` or `style` naming an undeclared element is ignored with a warning, because an element needs a type and cannot be created by styling. A `linkStyle` number with no relationship is an error, as in Mermaid. Styling never changes an outline shape, an icon or an end mark. | Mermaid convention throughout. | A75, A76, A101, A124, A128, A129, A130 |
| 10 | Icon (large-symbol) notation switch | Revised | No statement. The notation is a rendering option set through Mermaid configuration, `config.enterprise.notation: box` (default) or `icon`, in the frontmatter or a `%%{init}%%` directive. A renderer without the large symbols may ignore it. | Mermaid sets rendering options through configuration, so no new grammar is needed. | A77, A118 |
| 11 | Semicolons as statement separators | Revised | Accepted. `;` ends a statement and may separate several statements on one line, including the header. A `;` inside a label, and in `title`, `accTitle` or `accDescr` text, is text. | Mermaid flowchart convention (`graph TD; A-->B`). | A101, A123, A51, A120 |
| 12 | Quoted identifiers and the identifier character set | Revised | Ids may use any letters (including non-Latin), digits, `_`, `.` and `-`, may start with a digit, must not start with `.` or `-`, and are case-sensitive. Quoted identifiers are still errors. | Matches Mermaid flowchart node ids, which also cannot be quoted. | A100, A102 |
| 13 | Ids that equal a keyword | Revised | `title`, `version`, `direction`, `group`, `in`, `classDef`, `class`, `style`, `linkStyle`, `accTitle` and `accDescr` are reserved in any letter case and cannot be ids. Words that only start with one, such as `titleBar` or `inbox`, are fine, and element type names are not reserved. `notation` is no longer reserved. | Mermaid also reserves its statement keywords (for example `class` and `style` in flowcharts); in the parser a relationship starting with `title` is silently read as a title. | A90, A100 |
| 14 | Header aliases and letter case | Unchanged | The header is `enterprise` or `enterprise-beta`, in any letter case, optionally followed by a direction (row 24). Directive keywords, element types and relationship names are case-insensitive; ids are case-sensitive. Other diagram headers are not accepted. | Parser behaviour; `-beta` is the Mermaid convention for a new diagram type. | A103, A121, A123 |
| 15 | Trailing comments | Revised | Accepted. `%%` starts a comment on its own line or after any statement, including the header, and the comment runs to the end of the line. Inside a quoted label `%%` is text. In `title`, `accTitle` and `accDescr` text, `%%` starts a comment, as in Mermaid. | Mermaid and the parser both accept trailing comments; the parser's one fault, cutting labels at `%%`, is fixed. | A96, A57, A94, A95 |
| 16 | Forward references | Unchanged | Allowed. Relationship, `in`, `class`, `style` and `:::` references are resolved after the whole block is read. | Parser behaviour; declaration order does not matter in Mermaid flowcharts either. | A99 |
| 17 | Repeated `title`, `version` or `direction` | Unchanged | The last one wins and applies to the whole diagram, with no diagnostic. A `title` statement replaces a frontmatter title. | Parser behaviour; Mermaid also keeps the last value. | A09, A118, A120, A122 |
| 18 | Relationships on relationships | Unchanged | Not supported; relationships have no ids. A junction covers the common case of joining relationships. | Enterprise-specific and rarely needed in document diagrams. Mermaid's edge ids (`e1@-->`) belong to flowchart arrows, which this kind does not use. | none |
| 19 | Marking derived relationships | Unchanged | No special syntax. A derived relationship is an ordinary relationship; use its label, a `linkStyle` or a class if it needs to stand out. | The notation has no separate symbol for derivation. | A70, A71, A72 |
| 20 | Identical parallel relationships and self-relationships | Unchanged | Both are kept and drawn: parallel relationships separately, and a self-relationship as a loop when the rules table allows that type from the element type to itself. | Parser behaviour; Mermaid also draws repeated links. | A107, A108 |
| 21 | Rendering an empty diagram | Unchanged | Valid. It renders a small blank canvas, with the title if one is given, not an error placeholder. | Parser and renderer behaviour. | A01 |
| 22 | `version` values | Unchanged | Only `3.1` and `3.2`, default `3.2`. Any other value, such as `3` or `4.0`, is an error. Enterprise 4 is out of scope for this catalogue. | Principle (d) and parser behaviour. Enterprise-specific. | A05, A06, A09, A84–A88, A92 |
| 23 | `direction` values | New | `TB`, `TD`, `BT`, `LR` and `RL`, in any letter case; `TD` is the same as `TB`. Anything else is an error. | Mermaid's direction values. | A07, A08, A92, A122, A123 |
| 24 | Direction on the header line | New | `enterprise LR` (or any other direction value) is the same as `enterprise` followed by `direction LR`. | Mermaid convention (`flowchart LR`). | A121, A123 |
| 25 | Accessibility statements | New | `accTitle: text`, `accDescr: text` and `accDescr { … }` over several lines are accepted. They become the SVG's accessible title and description and the DOCX image's alternative text, and are not drawn. | Mermaid convention, available in every diagram type. | A120 |
| 26 | Frontmatter | New | A YAML block between `---` lines at the very start of the block may set `title` and `config`; unknown keys are ignored. Frontmatter lines count in block-relative line numbers. | Mermaid convention. | A77, A118 |
| 27 | `%%{init}%%` directives | New | Accepted anywhere in the block, on one line or spread over several, and read as configuration. They never cause a diagnostic. | Mermaid convention. | A119 |
| 28 | Single-quoted labels | New | A label may use single quotes; inside them `"` is ordinary text and `\'` gives an apostrophe. | Mermaid's newer grammars accept both quote styles. | A125 |
| 29 | Markdown strings | New | A label written `` "`…`" `` is a Mermaid markdown string: `**bold**` and `*italic*` are drawn, text wraps automatically, and the string may continue over several source lines, where a source line break is a label line break. Diagnostics use the line where the statement starts. | Mermaid convention for node, edge and subgraph labels. | A126 |
| 30 | Declaring an id twice | New | A second declaration with the same element type is the same element: a given label or container replaces the earlier one. A second declaration with a different element type is an error. | Mermaid treats a repeated node id as the same node. An Enterprise element can only have one type. | A90, A127 |

### Remaining differences from Mermaid

These are the places where the enterprise kind still does not accept something that Mermaid accepts somewhere. Each one follows from the Enterprise-specific grammar or from elements needing a type.

- Elements and relationships use the Enterprise statement forms, not flowchart node shapes (`A[label]`, `A(label)`, `A@{ shape: … }`) or flowchart arrows (`-->`, `-.->`, `==>`, `A & B --> C`). The relationship arrow needs a space on each side.
- Flowchart edge ids (`A e1@--> B`) and `@{ … }` metadata are not accepted, so a relationship cannot be named in `class` or `style`; use `linkStyle`.
- `subgraph … end` blocks are not accepted; containment uses `group` and `in`, as in Mermaid's architecture diagrams.
- `click`, `callback` and link (`href`) statements are not accepted.
- A relationship or `in` naming an undeclared id is an error, and `class` or `style` naming one is a warning. Mermaid would create a plain node, but an enterprise element needs an element type.
- Declaring an id again with a different element type is an error, where Mermaid would let the later shape win.
- Quoted identifiers are errors. Flowcharts do not accept them either, but some other Mermaid diagrams (for example entity relationship diagrams) do.
- Statement keywords are reserved and cannot be ids (row 13).
- The undocumented flowchart direction symbols `>`, `<`, `^` and `v` are not accepted; only the five documented direction values are.
- Block-relative line numbers count frontmatter and directive lines, whereas Mermaid's own error messages count from after the frontmatter. This changes only how errors are reported.

### A164 - Hints: recreation of a real diagram

A placement-hints recreation test of a real business capability diagram (roles, services and processes), redrawn from a picture with the original arrangement kept by hints: roles in a row along the top, services in a row beneath them, processes under their services, and dashed groups wrapped around their member processes. Only our own line and node formatting is used; the picture's colours are not copied. Source labels are kept as written, including "Annoucements". Elements are declared first, relationships later.

- **Recognition:** `Business.Role`, `Business.Service`, `Business.Process`, `Group`, `Relationship.Serving`, `Relationship.Realization`, `Relationship.Specialization`
- **Rendering:** `Layout.Hints`, `Layout.PortPlacement`, `Layout.Direction`
- **Expected:** valid. Every hint is honoured, dropped and reported, or ignored and reported, as rule 2.21 says. There are no overlaps, and lines are straight or rounded orthogonal. Notes: SDT Explorer is hinted right of the Registered Roles group, because its peer must be in the same group (rule 2.21(c)). Three serving lines and two realization lines end on a group frame, as in the picture. The picture shows the lines from View Annoucements and View Knowledge Material meeting at one point on Content Consumption and Feedback, so only the first carries a line hint (one port per line, rule 2.21(d)). The two lines from Track Task Progress and Update Task Details run to different services. All labels were readable; the title of the dashed group "Collaboration Management Service" wraps onto two lines in the picture.

```mermaid
enterprise BT
  group allRoles "All Roles"
  group regRoles "Registered Roles" in allRoles
  business-role admin "SDT Administrator" in regRoles
  business-role custodian "SDT Security Custodian" in regRoles right-of admin
  business-role dataMgr "SDT Data Manager" in regRoles right-of custodian
  business-role collabAdmin "Collaboration Workspace Administrator" in regRoles right-of dataMgr
  business-role collaborator "SDT Collaborator" in regRoles right-of collabAdmin
  business-role visualiser "SDT Visualiser" in regRoles right-of collaborator
  business-role analyst "SDT Analyst" in regRoles right-of visualiser
  business-role explorer "SDT Explorer" in allRoles right-of regRoles
  business-service pubSvc "Content Publishing Service"
  business-service consumeSvc "Content Consumption and Feedback" right-of pubSvc
  business-service feedbackSvc "Feedback Management" right-of consumeSvc
  business-service userRegSvc "User Registration Service" right-of feedbackSvc
  business-service platformSvc "Platform Administration Service" right-of userRegSvc
  business-service bwmSvc "Business Workflow Management" right-of platformSvc
  business-service dsApprSvc "Data Science Tool Approval and Configuration Service" right-of bwmSvc
  business-service dsmSvc "Data Source Management" right-of dsApprSvc
  business-service collabMgmtSvc "Collaboration Management Service" right-of dsmSvc
  business-service collabSvc "Collaboration Service" right-of collabMgmtSvc
  business-service editSvc "Data Editing and Analysis Service" right-of collabSvc
  business-service dsSubmitSvc "Data Science Tool Submission Service" right-of editSvc
  business-service geoEditSvc "Geospatial Data Editing and Analysis Service" right-of dsSubmitSvc
  business-service catalogueSvc "Data Catalogue Service" right-of geoEditSvc
  business-service geoVisSvc "Geospatial Visualisation and Query Service" right-of catalogueSvc
  group bwmGrp "Business Workflow Management" below bwmSvc
  group dsmGrp "Data Source Management" below dsmSvc
  group collabGrp "Collaboration Management Service" below collabMgmtSvc
  business-process maintain "Maintain Knowledge Material" below pubSvc
  business-process publish "Publish Annoucements" below maintain
  business-process viewKnow "View Knowledge Material" below consumeSvc
  business-process viewAnn "View Annoucements" below viewKnow
  business-process participate "Participate in Forum Discussion" right-of viewAnn
  business-process submitFb "Submit Feedback" right-of viewKnow
  business-process review "Review and Address Feedback" right-of participate
  business-process accept "Accept Terms and Conditions" below userRegSvc
  business-process register "Register as SDT Platform User" below accept
  business-process establish "Establish Federation" right-of register
  business-process manageTerms "Manage Terms and Conditions" below platformSvc
  business-process manageRes "Manage System Resource Usage" right-of manageTerms
  business-process onboard "Onboard Partner Agencies" right-of establish
  business-process define "Define Business Process" in bwmGrp
  business-process retire "Retire Business Process" in bwmGrp right-of define
  business-process configure "Configure and Update Business Process" in bwmGrp below define
  business-process configDs "Configure Data Science Tool" below dsApprSvc
  business-process defMeta "Define Dataset Metadata" in dsmGrp
  business-process manageMeta "Manage Dataset Metadata" in dsmGrp below defMeta
  business-process manageDs "Manage Dataset" in dsmGrp below manageMeta
  business-process advertise "Advertise Dataset" in dsmGrp below manageDs
  business-process privileges "Manage Dataset Privileges" in dsmGrp right-of defMeta
  business-process provide "Provide Dataset (Content)" in dsmGrp below privileges
  business-process retireDs "Retire Dataset" in dsmGrp below provide
  business-process sendNotif "Send Notification about Dataset" right-of dsmGrp
  business-process createWs "Create Collaboration Workspace" in collabGrp
  business-process manageWs "Manage Collaboration Workspace" in collabGrp below createWs
  business-process closeWs "Close and Archive Collaboration Workspace" in collabGrp below manageWs
  business-process sendCollab "Send Collaboration Notification" right-of collabGrp
  business-process collaborate "Collaborate" right-of sendCollab
  business-process track "Track Task Progress and Update Task Details" below collaborate
  business-process submitDs "Submit Data Science Tool" below dsSubmitSvc
  business-process consume "Consume and Use Data" right-of submitDs
  pubSvc -serving-> admin [leave N, enter W]
  consumeSvc -serving-> allRoles
  feedbackSvc -serving-> admin
  userRegSvc -serving-> admin
  platformSvc -serving-> admin
  bwmSvc -serving-> admin
  dsApprSvc -serving-> custodian [leave N, enter S]
  dsmSvc -serving-> dataMgr [leave N, enter S]
  collabMgmtSvc -serving-> collabAdmin [leave N, enter S]
  collabSvc -serving-> collaborator [leave N, enter S]
  editSvc -serving-> collaborator
  editSvc -serving-> analyst
  dsSubmitSvc -serving-> analyst
  geoEditSvc -serving-> analyst [leave N, enter S]
  catalogueSvc -serving-> allRoles
  geoVisSvc -serving-> allRoles
  collabAdmin -specialization-> collaborator [leave E, enter W]
  publish -realization-> pubSvc
  maintain -realization-> pubSvc
  viewAnn -realization-> consumeSvc [leave N, enter W]
  viewKnow -realization-> consumeSvc
  participate -realization-> consumeSvc
  submitFb -realization-> consumeSvc
  review -realization-> feedbackSvc
  accept -realization-> userRegSvc
  register -realization-> userRegSvc
  establish -realization-> userRegSvc
  establish -realization-> platformSvc [leave N, enter W]
  manageTerms -realization-> platformSvc
  onboard -realization-> platformSvc
  manageRes -realization-> platformSvc
  define -realization-> bwmSvc
  configure -realization-> bwmSvc
  retire -realization-> bwmSvc
  configDs -realization-> dsApprSvc
  track -realization-> dsApprSvc [leave N, enter S, travel W]
  dsmGrp -realization-> dsmSvc
  sendNotif -realization-> dsmSvc
  collabGrp -realization-> collabMgmtSvc
  sendCollab -realization-> collabSvc
  track -realization-> collabSvc
  collaborate -realization-> collabSvc
  consume -realization-> editSvc
  submitDs -realization-> dsSubmitSvc
  consume -realization-> geoEditSvc
  consume -realization-> catalogueSvc
  consume -realization-> geoVisSvc
```

### A165 - Hints: recreation of a business context diagram with motivation elements

A placement-hints recreation test of a real programme business context diagram, redrawn from a picture: motivation groups (requirements, values, drivers, goals) at the top, a programme group holding four use-case groups (roles in a row, processes beneath, then drivers, goals and outcomes), a group of outcomes, and a group of business services. Only our own line and node formatting is used; the picture's colours are not copied. Source labels are kept as written, including "decisons", "Informatinn" and "identifcation". Elements are declared first, relationships later.

- **Recognition:** `Business.Role`, `Business.Process`, `Business.Service`, `Motivation.Driver`, `Motivation.Goal`, `Motivation.Outcome`, `Motivation.Value`, `Motivation.Requirement`, `Group`, `Relationship.Triggering`, `Relationship.Serving`, `Relationship.Influence`, `Relationship.Realization`
- **Rendering:** `Layout.Hints`, `Layout.Nesting`, `Layout.GroupEndpoints`
- **Expected:** valid. Every hint is honoured, dropped and reported, or ignored and reported, as rule 2.21 says. There are no overlaps, and lines are straight or rounded orthogonal. Notes: the picture's dashed lines between groups are read as influence and the dotted line with a hollow head as realization. Open-headed solid lines are serving and filled-headed solid lines triggering. The services group serves the IP&A group in the picture (a line to a group frame, rule 4.9).

```mermaid
enterprise TB
  group reqs "Common Requirements"
  group values "Common Values" right-of reqs
  group drivers "Common Drivers/Motivations" right-of values
  group goals "Common Goals" right-of drivers
  group prog "Spatial WA Programme Business Context" below reqs
  group outcomes "Common Outcomes" below prog
  group platform "SDT Platform Business Context - Common Business Services" right-of outcomes
  group daw "DAW Use Case Business Context" in prog
  group ulpi "ULPI Use Case Business Context" in prog right-of daw
  group ipa "IP&A Use Case Business Context" in prog right-of ulpi
  group emem "EMEM Use Case Business Context" in prog right-of ipa
  requirement control "Control and track access to sensitive shared information" in reqs
  value dataDriven "Data driven decisons" in values
  value infoSec "Informatinn Security" in values right-of dataDriven
  driver ineffData "Inefficiencies due to siloed data" in drivers
  driver ineffProc "Inefficiencies and limitations of siloed government processes" in drivers right-of ineffData
  driver needData "Need to find and use data sources of varying source, type and quality" in drivers right-of ineffProc
  goal betterData "Better and more consistent data" in goals
  goal consult "Facilitate community consultation" in goals right-of betterData
  goal sharing "Better data sharing" in goals right-of consult
  goal interParty "Better inter-party collaboration using 2/3/4D spatial information" in goals right-of sharing
  business-role proponent "Proponent" in daw
  business-role planner "Planner" in daw right-of proponent
  business-role referral "Referral Agency" in daw right-of planner
  business-process subdivision "Subdivision" in daw below proponent
  business-process sigDev "Significant Development Proposal" in daw right-of subdivision
  business-process referralProc "Referral Process" in daw below subdivision
  business-role keyStake "Key Stakeholders" in ulpi
  business-role workGroup "Working Group Members" in ulpi right-of keyStake
  business-role otherProj "Other Project Stakeholders" in ulpi right-of workGroup
  business-process idInv "Identification - Investigation" in ulpi below keyStake
  business-process idDecl "Identification - Declaration" in ulpi right-of idInv
  business-process establish "Establishment" in ulpi right-of idDecl
  business-process implement "Implementation" in ulpi right-of establish
  driver lack "Lack of Understanding of Supporting Infrastructure and Constraints" in ulpi below idInv
  goal growth "Support Economic Growth" in ulpi right-of lack
  goal diversify "Diversify WA Economy" in ulpi right-of growth
  business-role projDel "Project Delivery Authorities" in ipa
  business-role transport "Transport Asset Operators & Maintainers" in ipa right-of projDel
  business-role otherProj2 "Other Project Stakeholders" in ipa right-of transport
  business-process strategic "Strategic Planning" in ipa below projDel
  business-process design "Design and Approval" in ipa right-of strategic
  business-process deliveryInc "Delivery Incremental Data Supply" in ipa right-of design
  business-process sieC "Stakeholder Identification, Engagement and Collaboration" in ipa below design
  goal easier "Easier identifcation of stakeholders, other initiatives (nearby)" in ipa below strategic
  outcome costCert "Greater cost certainty" in ipa right-of easier
  outcome safer "Safer outcomes on-site" in ipa right-of costCert
  business-role emergency "Emergency Management Community" in emem
  business-role planners "Planners and Asset Managers" in emem right-of emergency
  business-role utilities "Public utilities" in emem right-of planners
  business-role community "Community groups" in emem right-of utilities
  business-role industry "Primary industry stakeholders" in emem right-of community
  business-process risk "Risk management and preparedness planning" in emem below emergency
  business-process asset "Asset Management" in emem right-of risk
  business-process coordinate "Coordinate / support recovery planning and operations" in emem right-of asset
  business-process simulation "Simulation and Training" in emem below risk
  business-process hazard "Hazard modelling and scenario analysis" in emem right-of simulation
  business-process stakeEng "Stakeholder engagement and collaboration" in emem right-of hazard
  goal informed "Informed interested stakeholders" in emem below simulation
  outcome prepared "Improved preparedness" in emem right-of informed
  outcome costing "More accurate costing of recovery support" in emem right-of prepared
  outcome integrate "Integrate local and expert knowledge" in emem right-of costing
  outcome informedPlan "Better informed planning outcomes" in outcomes
  outcome reduced "Reduced duplication of effort" in outcomes right-of informedPlan
  outcome speed "Improved speed and efficiency" in outcomes right-of reduced
  outcome landUse "Better land use planning" in outcomes right-of speed
  outcome engagement "Better stakeholder engagement" in outcomes right-of landUse
  business-service userReg "User Registration Service" in platform
  business-service bwm "Business Workflow Management" in platform right-of userReg
  business-service dsm "Data Source Management" in platform right-of bwm
  business-service collabMgmt "Collaboration Management Service" in platform right-of dsm
  business-service collabSvc "Collaboration Service" in platform right-of collabMgmt
  business-service catalogue "Data Catalogue Service" in platform right-of collabSvc
  business-service geoVis "Geospatial Visualisation and Query Service" in platform right-of catalogue
  business-service geoEdit "Geospatial Data Editing and Analysis Service" in platform right-of geoVis
  business-service editSvc "Data Editing and Analysis Service" in platform right-of geoEdit
  drivers -influence-> reqs
  drivers -influence-> goals
  values -influence-> reqs
  values -influence-> goals
  reqs -influence-> prog
  goals -influence-> prog
  prog -realization-> outcomes
  platform -serving-> ipa
  referralProc -serving-> subdivision
  referralProc -serving-> sigDev
  idInv -triggering-> idDecl
  idDecl -triggering-> establish
  establish -triggering-> implement
  strategic -triggering-> design
  design -triggering-> deliveryInc
  strategic -triggering-> sieC
  design -triggering-> sieC
  sieC -triggering-> deliveryInc
  risk -triggering-> asset
  asset -triggering-> coordinate
  simulation -serving-> risk
  hazard -serving-> risk
  stakeEng -serving-> risk
  stakeEng -serving-> coordinate
```

### A166 - Hints: recreation of a systems scope diagram with a key

A placement-hints recreation test of a real systems scope picture: a key, a group of systems with a nested sub-group, a second group of other systems, and two callout notes pointing at one system each. The picture's green, red and yellow fills are not copied (no per-node colours); their meaning is written into each label instead. The key boxes and the two callouts are notes, and each callout is joined to its system by a plain connection. Elements are declared first, relationships later.

- **Recognition:** `Application.Component`, `Business.Process`, `Note`, `Group`, `Connection.Plain`
- **Rendering:** `Layout.Hints`, `Layout.Nesting`, `Layout.LineHints`, `Layout.Notes`
- **Expected:** valid. Every hint is honoured, dropped and reported, or ignored and reported, as rule 2.21 says. There are no overlaps, and lines are straight or rounded orthogonal. Notes: the picture is not drawn in Enterprise, so the element types are the closest reading (systems as application components, creation processes as business processes). Label line breaks in the picture are not copied where they fall after a slash. The key colour meaning is added to each label as a suffix. The key boxes and callouts are notes; the plain connections are not relationships.

```mermaid
enterprise TB
  group keyGrp "KEY"
  group slip "SLIP" below keyGrp
  group other "Other Systems that aren't part of SLIP" right-of slip
  group landgate "Landgate Specific" in slip
  note keyBox "KEY" in keyGrp
  note keyIn "In Scope SDT to replace" in keyGrp right-of keyBox
  note keyOut "Not in Scope of SDT" in keyGrp right-of keyIn
  note keyImpact "May have impact from SDT" in keyGrp right-of keyOut
  application-component download "SLIP Data Download<br>[in scope of SDT]" in slip
  application-component publishing "SLIP Publishing Tools<br>[in scope of SDT]" in slip right-of download
  application-component dut "Data Upload Tool (DUT)<br>[in scope of SDT]" in slip right-of publishing
  application-component gis "GIS Services (Esri / OGC Services, Imagery / Basemap)<br>[in scope of SDT]" in slip right-of dut
  application-component ping "Ping/Sailpoint Authentication/Authorisation<br>[in scope of SDT]" in slip right-of gis
  application-component webApps "SLIP Web Apps (Locate/Landinfo/Bushfire Prone/Marine Map)<br>[not in scope of SDT]" in slip below download
  application-component lswa "LSWA Integration to SLIP<br>[not in scope of SDT]" in landgate
  business-process imagery "Imagery Mosaic Creation Process (in SLIP)<br>[in scope of SDT]" in landgate right-of lswa
  business-process basemap "Basemap Creation Process (in SLIP)<br>[in scope of SDT]" in slip right-of landgate
  application-component payment "Landgate Payment Portal<br>[not in scope of SDT]" in other
  application-component serviceNow "Service Now<br>[not in scope of SDT]" in other right-of payment
  application-component dataWa "Data.wa.gov.au<br>[may have impact from SDT]" in other below payment
  application-component customers "Customer Systems that use SLIP<br>[not in scope of SDT]" in other right-of dataWa
  note locateNote "Locate not directly replaced, however expect that there will be public viewer use cases in the future for SDT<br><br>Could fail in future SDT use case (generic viewer)" below slip
  note dataWaNote "Changes may be required within Data WA to integrate with SDT, including the retrieval and ingestion of data catalogues from SDT" below other
  locateNote --- webApps [leave N, enter S]
  dataWaNote --- dataWa [leave N, enter S]
```

### A167 - Hints: recreation of a locations, stakeholders and roles diagram

A placement-hints recreation test of a real diagram of locations, stakeholder groups, registered roles, platform management roles, processes, an application component and the hosting nodes. Locations aggregate the stakeholder groups, the groups are assigned to roles, and the nodes are composed from locations as drawn. The stakeholder groups are empty groups in the picture and stay empty here. Only our own line and node formatting is used. Elements are declared first, relationships later.

- **Recognition:** `Business.Actor`, `Business.Role`, `Business.Process`, `Application.Component`, `Technology.Node`, `Location`, `Group`, `Relationship.Aggregation`, `Relationship.Assignment`, `Relationship.Specialization`, `Relationship.Serving`, `Relationship.Composition`
- **Rendering:** `Layout.Hints`, `Layout.GroupEndpoints`, `Layout.LineHints`
- **Expected:** valid. Every hint is honoured, dropped and reported, or ignored and reported, as rule 2.21 says. There are no overlaps, and lines are straight or rounded orthogonal. Notes: the aggregation lines from Australia to the group frames, and the assignment lines from the groups to roles, use group endpoints (rule 4.9). The two location-to-node composition lines are drawn with the diamond on the location end, so the location is the source.

```mermaid
enterprise TB
  location australia "Australia"
  location india "India"
  location nz "New Zealand"
  business-actor publicUser "Public Unauthenticated User"
  group trusted "Trusted Stakeholders" right-of publicUser
  group landgateOp "Landgate Spatial WA Operation and Governance" right-of trusted
  group vendorOp "SDT Vendor Operation" right-of landgateOp
  business-role explorer "SDT Explorer" below publicUser
  group regRoles "SDT Registered Roles" right-of explorer
  group mgmtRoles "SDT Platform Management and Support Roles" right-of regRoles
  business-role visualiser "SDT Visualiser" in regRoles
  business-role analyst "SDT Analyst" in regRoles right-of visualiser
  business-role collaborator "SDT Collaborator" in regRoles right-of analyst
  business-role dataMgr "SDT Data Manager" in regRoles right-of collaborator
  business-role admin "SDT Administrator" in regRoles right-of dataMgr
  business-role owner "SDT Platform Owner" in mgmtRoles
  business-role secCustodian "SDT Security Custodian" in mgmtRoles right-of owner
  business-role platCustodian "SDT Platform Custodian" in mgmtRoles right-of secCustodian
  business-role devCustodian "SDT Platform Development and Support Custodian" in mgmtRoles right-of platCustodian
  application-component sdt "SDT" below regRoles
  business-process cyber "Cybersecurity Process" right-of sdt
  business-process support "Support and Maintain Platform" right-of cyber
  node azure "Azure Platform" below sdt
  node primary "Azure Platform (Primary) Australia East" below azure
  node secondary "Azure Platform (Secondary) Australia Southeast" right-of primary
  location locNsw "Azure (Primary) New South Wales Australia" below primary
  location locVic "Azure (Secondary) Victoria Australia" below secondary
  australia -aggregation-> publicUser
  australia -aggregation-> trusted
  australia -aggregation-> landgateOp
  australia -aggregation-> vendorOp
  india -aggregation-> vendorOp
  nz -aggregation-> vendorOp
  publicUser -assignment-> explorer [leave S, enter N]
  trusted -assignment-> regRoles [leave S, enter N]
  landgateOp -assignment-> regRoles [leave S, enter N]
  landgateOp -assignment-> owner
  landgateOp -assignment-> secCustodian
  landgateOp -assignment-> platCustodian
  vendorOp -assignment-> devCustodian [leave S, enter N]
  owner -specialization-> admin [leave W, enter E]
  secCustodian -specialization-> admin
  secCustodian -assignment-> cyber [leave S, enter N]
  platCustodian -assignment-> support [leave S, enter N]
  devCustodian -assignment-> support [leave S, enter N]
  cyber -serving-> sdt [leave W, enter E]
  support -serving-> sdt
  sdt -serving-> explorer
  sdt -serving-> regRoles [leave N, enter S]
  azure -serving-> sdt [leave N, enter S]
  azure -composition-> primary
  azure -composition-> secondary
  locNsw -composition-> primary
  locVic -composition-> secondary
```

### A168 - Hints: recreation of a custodians, consumers and role assignment diagram

A placement-hints recreation test of a real diagram of data custodians and consumers, stakeholder groups, registered roles and two operation groups, with assignments from the actors and groups to roles, three specializations, and two labelled lines. The picture's notes are declared as notes and joined by plain connections; the note that sits on a line in the picture is the label of that line, not a note. Only our own line and node formatting is used. Elements are declared first, relationships later.

- **Recognition:** `Business.Actor`, `Business.Role`, `Note`, `Group`, `Relationship.Assignment`, `Relationship.Specialization`, `Relationship.Serving`, `Connection.Plain`
- **Rendering:** `Layout.Hints`, `Layout.GroupEndpoints`, `Layout.LineHints`, `Layout.RelationshipLabels`, `Layout.Notes`
- **Expected:** valid. Every hint is honoured, dropped and reported, or ignored and reported, as rule 2.21 says. There are no overlaps, and lines are straight or rounded orthogonal. Notes: the red border of the first note is not copied. The "Subject to privilege" note of the picture sits on a line, so it is the label of the Analyst to Collaborator line and is not declared as a note. Both "And others. ..." notes are members of their groups. The labelled "Reports to" and "Subject to privilege" lines are read as serving with open heads. The "Reports to" line runs from the platform support manager to the Landgate manager as drawn. The platform support manager has two assignment lines, one to the security custodian and one to the administrator.

```mermaid
enterprise TB
  group custodians "Existing SLIP Data Custodians"
  group consumers "Existing SLIP Data Consumers" right-of custodians
  group trusted "Trusted Stakeholders" below custodians
  group newCust "New Data Custodians" right-of trusted
  group regRoles "SDT Registered Roles" below trusted
  group landgateOp "Landgate Spatial WA Operation and Governance" below regRoles
  group vendorOp "SDT Vendor Operation" right-of landgateOp
  note redNote "Some SDT Capabilities, in terms of the defined platform roles, are subject to granular permissions assignment based on the domain of access (organisation, collaboration workspace, workflow management, etc).<br>Refer to full Function Requirements." left-of trusted
  business-actor landgateCust "Landgate Data Custodian" in custodians
  business-actor dmpe "DMPE Data Custodian" in custodians right-of landgateCust
  business-actor dwer "DWER Data Custodian" in custodians right-of dmpe
  business-actor mrwa "MRWA Data Custodian" in custodians right-of dwer
  note othersCust "And others.<br>..." in custodians right-of mrwa
  business-actor govCons "Government Agency Consumer" in consumers
  business-actor privOrg "Private Organisation Consumer" in consumers right-of govCons
  business-actor privInd "Private Individual Consumer" in consumers right-of privOrg
  note othersCons "And others.<br>..." in consumers right-of privInd
  business-actor trustedAdmin "Trusted Stakeholder Administrator" in trusted
  business-role owner "SDT Platform Owner" in regRoles
  business-role admin "SDT Administrator" in regRoles right-of owner
  business-role secCustodian "SDT Security Custodian" in regRoles right-of admin
  business-role dataMgr "SDT Data Manager" in regRoles right-of secCustodian
  business-role collabAdmin "Collaboration Workspace Administrator" in regRoles right-of dataMgr
  business-role collaborator "SDT Collaborator" in regRoles right-of collabAdmin
  business-role visualiser "SDT Visualiser" in regRoles right-of collaborator
  business-role analyst "SDT Analyst" in regRoles right-of visualiser
  business-actor landgateOwner "Landgate SDT Owner" in landgateOp
  business-actor landgateMgr "Landgate SDT Manager" in landgateOp right-of landgateOwner
  business-actor cyberRep "Landgate Cybersecurity Representative" in landgateOp right-of landgateMgr
  business-actor platMgr "SDT Platform Support Manager" in vendorOp
  business-actor l2 "SDT Platform L2 Support Officer" in vendorOp right-of platMgr
  redNote --- regRoles [leave S, enter N]
  trustedAdmin -assignment-> admin [leave S, enter N]
  custodians -assignment-> dataMgr [leave S, enter N]
  newCust -assignment-> dataMgr [leave S, enter N]
  consumers -assignment-> visualiser [leave S, enter N]
  consumers -assignment-> analyst [leave S, enter N]
  owner -specialization-> admin [leave E, enter W]
  secCustodian -specialization-> admin [leave W, enter E]
  collabAdmin -specialization-> collaborator [leave E, enter W]
  landgateOwner -assignment-> owner [leave N, enter S]
  landgateMgr -assignment-> admin [leave N, enter S]
  cyberRep -assignment-> secCustodian [leave N, enter S]
  platMgr -assignment-> secCustodian [leave N, enter S]
  platMgr -assignment-> admin
  l2 -assignment-> dataMgr [leave N, enter S]
  l2 -assignment-> collabAdmin [leave N, enter S]
  platMgr -serving-> landgateMgr "Reports to"
  analyst -serving-> collaborator "Subject to privilege"
```

### A169 - Hints: recreation of a referral agency actors and roles diagram

A placement-hints recreation test of a real diagram of use-case roles in one group, referral agency officers who are assigned to those roles and to registered roles in another group, and a note. One officer's line ends on the group frame itself. Only our own line and node formatting is used. Elements are declared first, relationships later.

- **Recognition:** `Business.Actor`, `Business.Role`, `Note`, `Group`, `Relationship.Assignment`, `Relationship.Specialization`, `Relationship.Serving`, `Connection.Plain`
- **Rendering:** `Layout.Hints`, `Layout.GroupEndpoints`, `Layout.LineHints`, `Layout.Notes`
- **Expected:** valid. Every hint is honoured, dropped and reported, or ignored and reported, as rule 2.21 says. There are no overlaps, and lines are straight or rounded orthogonal. Notes: the DPLH Admin Officer line ends on the DAW group frame (rule 4.9). The line from the Western Power Representative to the Referral Agency role has an arrowhead at the role only. The note is joined to SDT Collaborator by a plain connection, which is not a relationship. "Subject to privilege" is the label of a serving line, not a note. The group title "DAW Use Case Business Context" is cut off in the picture and is written in full.

```mermaid
enterprise TB
  group daw "DAW Use Case Business Context"
  business-actor dplhAdmin "DPLH Admin Officer" below daw
  business-actor dplhPlan "DPLH Planning Officer" right-of dplhAdmin
  business-actor water "Water Corporation Representative" right-of dplhPlan
  business-actor westernPower "Western Power Representative" right-of water
  business-actor localGov "Local Government Officer" right-of westernPower
  group regRoles "SDT Registered Roles" below dplhAdmin
  note otherAgencies "And Other Referral Agencies" below localGov
  business-role proponent "Proponent" in daw
  business-role planner "Planner" in daw right-of proponent
  business-role referral "Referral Agency" in daw right-of planner
  business-role admin "SDT Administrator" in regRoles
  business-role secCustodian "SDT Security Custodian" in regRoles right-of admin
  business-role dataMgr "SDT Data Manager" in regRoles right-of secCustodian
  business-role collabAdmin "Collaboration Workspace Administrator" in regRoles right-of dataMgr
  business-role collaborator "SDT Collaborator" in regRoles right-of collabAdmin
  business-role visualiser "SDT Visualiser" in regRoles right-of collaborator
  business-role analyst "SDT Analyst" in regRoles right-of visualiser
  dplhAdmin -assignment-> daw [leave N, enter S]
  dplhPlan -assignment-> planner [leave N, enter S]
  water -assignment-> referral [leave N, enter S]
  westernPower -assignment-> referral [leave N, enter S]
  localGov -assignment-> referral [leave N, enter S]
  dplhAdmin -assignment-> collaborator
  dplhPlan -assignment-> collabAdmin [leave S, enter N]
  water -assignment-> collaborator [leave S, enter N]
  westernPower -assignment-> collaborator [leave S, enter N]
  localGov -assignment-> collaborator
  collabAdmin -specialization-> collaborator [leave E, enter W]
  otherAgencies --- collaborator
  analyst -serving-> collaborator "Subject to privilege"
```

### A170 - Legend: on, default bottom-right

For the legend feature. A small business process with the legend turned on and no position.

- **Recognition:** `Config.Frontmatter`, `Config.Legend.On`, `Business.Actor`, `Business.Process`, `Business.Service`, `Relationship.Assignment`, `Relationship.Triggering`, `Relationship.Realization`
- **Rendering:** `Layout.Legend`
- **Expected:** valid. The main layout is identical to the same diagram with no legend. A framed group titled Legend sits outside the bottom-right corner of the main bounds, one grid step clear. It lists Business actor, Business process, Business service, then Assignment, Triggering and Realization.

```mermaid
---
config:
  legend: on
---
enterprise
  business-actor clerk "Claims Clerk"
  business-process receive "Receive Claim"
  business-process assess "Assess Claim"
  business-process pay "Pay Claim"
  business-service claims "Claims Handling"
  clerk -assignment-> assess
  receive -triggering-> assess
  assess -triggering-> pay
  pay -realization-> claims
```

### A171 - Legend: top-left

For the legend feature. Application services realised by components, with the legend top-left.

- **Recognition:** `Config.Legend.Position`, `Application.Component`, `Application.Service`, `Application.Interface`, `Relationship.Realization`, `Relationship.Serving`, `Relationship.Composition`
- **Rendering:** `Layout.Legend`
- **Expected:** valid. The legend sits outside the top-left corner of the main bounds, one grid step clear; the main layout is unchanged. It lists Application component, Application service, Application interface, then Realization, Composition and Serving.

```mermaid
---
config:
  legend: top-left
---
enterprise
  application-component crm "CRM"
  application-service custSvc "Customer Lookup"
  application-service caseSvc "Case Management"
  application-interface crmApi "CRM API"
  application-component portal "Self-Service Portal"
  crm -realization-> custSvc
  crm -realization-> caseSvc
  crm -composition-> crmApi
  custSvc -serving-> portal
  caseSvc -serving-> portal
```

### A172 - Legend: top-right

For the legend feature. A small technology stack with the legend top-right.

- **Recognition:** `Config.Legend.Position`, `Technology.Node`, `Technology.Device`, `Technology.SystemSoftware`, `Technology.Service`, `Relationship.Composition`, `Relationship.Assignment`, `Relationship.Realization`
- **Rendering:** `Layout.Legend`
- **Expected:** valid. The legend sits outside the top-right corner of the main bounds, one grid step clear; the main layout is unchanged. It lists Node, Device, System software, Technology service, then Composition, Assignment and Realization.

```mermaid
---
config:
  legend: top-right
---
enterprise
  node web "Web Server"
  device rack "Rack Server"
  system-software os "Linux"
  system-software nginx "Nginx"
  technology-service hosting "Web Hosting"
  web -composition-> rack
  web -composition-> os
  os -assignment-> nginx
  nginx -realization-> hosting
```

### A173 - Legend: bottom-left

For the legend feature. Data access by two applications, with the legend bottom-left.

- **Recognition:** `Config.Legend.Position`, `Application.Component`, `Data.Object`, `Relationship.Access`, `Relationship.Flow`
- **Rendering:** `Layout.Legend`
- **Expected:** valid. The legend sits outside the bottom-left corner of the main bounds, one grid step clear; the main layout is unchanged. It lists Application component, Data object, then Access and Flow. Read and write access draw different arrowheads, so each is a separate Access entry (Access (write), Access (read)) in order of first use.

```mermaid
---
config:
  legend: bottom-left
---
enterprise
  application-component orders "Order Manager"
  application-component billing "Billing"
  application-component warehouse "Warehouse System"
  data-object order "Order"
  data-object invoice "Invoice"
  orders -access(write)-> order
  billing -access(read)-> order
  billing -access(write)-> invoice
  orders -flow-> warehouse "pick list"
```

### A174 - Legend: below

For the legend feature. Motivation elements with the legend centred below the diagram.

- **Recognition:** `Config.Legend.Position`, `Motivation.Stakeholder`, `Motivation.Driver`, `Motivation.Goal`, `Relationship.Association`, `Relationship.Influence`
- **Rendering:** `Layout.Legend`
- **Expected:** valid. The legend is centred under the main bounds, one grid step clear; the main layout is unchanged. It lists Stakeholder, Driver, Goal, then Association and Influence.

```mermaid
---
config:
  legend: below
---
enterprise
  stakeholder ceo "Chief Executive"
  driver churn "Customer Churn"
  driver cost "Rising Costs"
  goal retain "Retain Customers"
  goal efficient "Lower Cost to Serve"
  ceo -association-> churn
  ceo -association-> cost
  churn -influence(+)-> retain
  cost -influence(+)-> efficient
```

### A175 - Legend: right

For the legend feature. Product composition and aggregation with the legend centred to the right of the diagram.

- **Recognition:** `Config.Legend.Position`, `Business.Product`, `Business.Service`, `Business.Object`, `Relationship.Aggregation`, `Relationship.Composition`
- **Rendering:** `Layout.Legend`
- **Expected:** valid. The legend is centred beside the right edge of the main bounds, one grid step clear; the main layout is unchanged. It lists Product, Business service, Business object, then Aggregation and Composition.

```mermaid
---
config:
  legend: right
---
enterprise
  product broadband "Home Broadband"
  business-service install "Installation"
  business-service support "Support"
  business-object contract "Service Contract"
  broadband -aggregation-> install
  broadband -aggregation-> support
  broadband -composition-> contract
```

### A176 - Legend: table mode

For the legend feature. Event-driven ordering with the legend as a Word table.

- **Recognition:** `Config.Legend.Table`, `Business.Event`, `Business.Process`, `Application.Service`, `Relationship.Triggering`, `Relationship.Serving`, `Relationship.Flow`
- **Rendering:** `Layout.Legend`
- **Expected:** valid. No legend is drawn in the image, and the image is identical to the same diagram with no legend. A two-column table (Symbol, Type) follows the diagram with rows Business event, Business process, Application service, Triggering, Flow and Serving.

```mermaid
---
config:
  legend: table
---
enterprise
  business-event received "Order Received"
  business-process check "Check Stock"
  business-process ship "Ship Order"
  application-service stock "Stock Query"
  received -triggering-> check
  check -flow-> ship "picked items"
  stock -serving-> check
```

### A177 - Legend: hint position right-of a node

For the legend feature. A two-layer chain with the legend placed by a hint beside one node.

- **Recognition:** `Config.Legend.Hint`, `Business.Role`, `Business.Process`, `Application.Component`, `Relationship.Assignment`, `Relationship.Serving`
- **Rendering:** `Layout.Legend`
- **Expected:** valid. The legend sits to the right of node api, outside the right edge of the main bounds, one grid step clear; the main layout is unchanged. It lists Business role, Business process, Application component, then Assignment and Serving.

```mermaid
---
config:
  legend: right-of api
---
enterprise
  business-role agent "Service Agent"
  business-process resolve "Resolve Ticket"
  application-component desk "Service Desk"
  application-component api "Ticket API"
  agent -assignment-> resolve
  desk -serving-> resolve
  api -serving-> desk
```

### A178 - Legend: explicitly off

For the legend feature. The legend is set off explicitly.

- **Recognition:** `Config.Legend.Off`, `Application.Component`, `Application.Service`, `Technology.Node`, `Technology.Service`, `Relationship.Realization`, `Relationship.Serving`
- **Rendering:** `Layout.Legend`
- **Expected:** valid. No legend and no table are produced. The output is identical to the same diagram with no legend setting.

```mermaid
---
config:
  legend: off
---
enterprise
  application-component hr "HR System"
  application-service payroll "Payroll"
  node vm "Virtual Machine"
  technology-service compute "Compute"
  hr -realization-> payroll
  vm -realization-> compute
  compute -serving-> hr
```

### A179 - Legend: notes, plain connections and a group

For the legend feature. A group of application components with a note, a plain connection and several relationship types, with the legend on.

- **Recognition:** `Config.Legend.On`, `Group`, `Note`, `Connection.Plain`, `Application.Component`, `Application.Service`, `Data.Object`, `Relationship.Composition`, `Relationship.Realization`, `Relationship.Serving`, `Relationship.Access`, `Relationship.Flow`
- **Rendering:** `Layout.Legend`
- **Expected:** valid. The legend sits outside the bottom-right corner, one grid step clear; the main layout is unchanged. It lists Group, Application component, Application service, Data object, Note, then Composition, Realization, Access (readwrite), Flow, Serving and Plain connection.

```mermaid
---
config:
  legend: on
---
enterprise
  group shop "Online Shop"
  application-component front "Storefront" in shop
  application-component cart "Basket" in shop right-of front
  application-service checkout "Checkout"
  data-object basket "Basket Contents"
  note n1 "Basket is kept for 30 days"
  front -composition-> cart
  cart -realization-> checkout
  cart -access(readwrite)-> basket
  front -flow-> cart "items"
  checkout -serving-> front
  n1 --- basket
```

# ArchiMate® notation for Mermaid — design study and PoC

*Prepared for Dave Robinson, 5 Oct 2026 (Perth time). Status: design proposal + working proof of concept. Nothing has been pushed or published.*

> ArchiMate is a registered trademark of The Open Group in the United States and other countries.
> This document describes an independent, unofficial notation renderer. It is not endorsed by The Open Group.

> **Implementation note (5 Oct 2026):** Dave chose the neutral diagram keyword **`enterprise`** instead of `archimate`, and the default rule set is **ArchiMate 3.2** (3.1 selectable with `version 3.1`; the 15 cells that differ from 3.1 stay provisional † until checked against a licensed 3.2 spec). Wherever this study says `archimate` as a keyword or package name, read `enterprise`. Paths below refer to the original PoC workspace, which is not part of this repo; the relevant files are copied into `docs/enterprise/`.

## 1. Summary

* **Mermaid version used by markdown2docx: 11.14.0.** It is vendored under `mermaid/` in
  `VaderConsulting/MarkdownToDocX` (README: "built on a vendored Mermaid 11.14.0 parser";
  `mermaid/packages/mermaid/package.json` → `"version": "11.14.0"`; `status.md` compatibility pin →
  11.14.0, vendored source commit `1bb6ba52937c21999e9ac89d11b82b0c56fd2739`, flagged "dirty").
  That commit is **not in upstream mermaid-js** (the upstream tag `mermaid@11.14.0` is
  `2b9d054d622101a727b03c5d47a15f3bd98125fa`, 1 Apr 2026, 17:16 Perth time), so the vendored tree has local changes.
* **Important architectural fact:** markdown2docx does **not execute Mermaid JS**. It is a .NET 10 app.
  Flowcharts go through a native C# recogniser → Sugiyama/MSAGL layout → SVG; every other diagram kind is
  handed to the vendored C# "Naiad" (MermaidSharp) renderer; SVG is rasterised with Svg.Skia. A JS Mermaid
  plugin alone will therefore never show up in markdown2docx output.
* **Recommendation: option (b), a dedicated `archimate` diagram type**, delivered twice from one spec:
  1. a **Mermaid external-diagram plugin** (`mermaid.registerExternalDiagrams`, works unmodified on 11.14.0) for
     browsers, Mermaid CLI, docs sites; and
  2. a **native C# diagram kind in markdown2docx** (same grammar, same rules JSON, same notation geometry).
  In the interim, a **transpiler to plain flowchart** gives a degraded but readable output that
  markdown2docx can already render.
* Option (a) (a convention on top of `flowchart`) cannot reproduce the spec notation: no diamonds, hollow
  triangles, assignment dot or corner icons are available in 11.14 flowcharts or in markdown2docx's renderer.
* **PoC works end-to-end:** parser + rule validator + spec-notation SVG renderer (ELK layout) + Mermaid
  11.14.0 plugin (tested in headless Chrome) + flowchart fallback transpiler, with 5 samples (incl. two
  deliberately invalid ones the validator rejects).
* **Constraint tables:** all 484 in-scope ArchiMate **3.1** cells (22 Application + Technology elements,
  source × target) were checked by eye against the 3.1 specification's Appendix B tables — all match.
  The 15 cells that change in **3.2** are **provisional** (not verified against the 3.2 text).
* **Licensing/trademark:** shapes and icons were drawn fresh as SVG paths; no spec figure was copied. Don't
  use "ArchiMate" in a product/package name; commercial use of the spec needs an Open Group Commercial
  Licence. See §9.

## 2. Step 1 — markdown2docx findings

| Item | Finding | Source |
|---|---|---|
| Repo | `VaderConsulting/MarkdownToDocX` (private, C#, branch `master`, last push 4 Oct 2026 23:05 Perth time) | GitHub |
| Mermaid version | **11.14.0** | README; `mermaid/packages/mermaid/package.json`; `status.md` "Compatibility pin" |
| Vendored commit | `1bb6ba52937c…` — not fetchable from upstream ("dirty") | `status.md`, upstream fetch attempt |
| Runtime | .NET (net10); Mermaid JS is **not executed** | `src/MDConverter/Pipeline/DocxWriter.cs` |
| Flowchart path | `FlowchartRecognitionRenderBridge`: native recogniser (`new_src/MermaidRecognition`) → Sugiyama/MSAGL → SVG; Naiad fallback | `DocxWriter.cs` |
| Other kinds | Vendored Naiad (`extern/Naiad`, frozen, "not a forward option") | `status.md` |
| Rasteriser | Svg.Skia 5.0.0 / SkiaSharp 3.119.4; flowchart markers are pre-expanded (`ExpandSvgFlowchartMarkersForRasterization`) | `DocxWriter.cs` |
| Flowchart shapes | rect, rounded, stadium, subroutine, cylinder, circle, doublecircle, diamond, hexagon, asymmetric, lean L/R, trapezoid (+inv); unknown `@{shape}` → rect | recogniser source |
| Edge ends | none / point / circle / cross only (circle/cross/bidirectional listed as not finished) | `status.md` |
| Icons | none | — |
| Recognition levels | L4: flowchart, sequence, class, state, ER, gantt, pie, mindmap, gitGraph. L1 (Naiad only): architecture, block, C4, requirement | `status.md` |

Consequences for the design:
* Whatever we add must exist as a **native C# kind** to appear in DOCX output.
* Arrowheads must be **explicit paths, not SVG `<marker>`** (Svg.Skia marker handling is the reason the
  existing marker-expansion hack exists). The PoC renderer already follows this rule, and uses `<text>`
  only (no `<foreignObject>`), so its SVG is Skia-friendly.

## 3. Step 2 — how Mermaid 11.14.0 builds diagram types

Studied in a clone of tag `mermaid@11.14.0` (`/workspace/mermaid-archimate/mermaid`).

* **Registration:** `packages/mermaid/src/diagram-api/diagram-orchestration.ts` registers lazily loaded
  diagrams via ordered **detectors** (regex/function on the text). Architecture, mindmap and flowchart-elk are
  only registered with `includeLargeFeatures`.
* **Diagram contract:** `DiagramDefinition = { parser, db, renderer, styles }` plus a detector and loader
  (e.g. `architectureDetector.ts`, `architectureDiagram.ts`).
* **Parsing:** two generations — **Jison** (flowchart `flow.jison`, block, C4, requirement, …) and **Langium**
  grammars in `@mermaid-js/parser` 1.1.0 (info, packet, pie, architecture, gitGraph, radar, treemap,
  treeView, wardley; `parse('architecture', text)`). New diagrams upstream are Langium.
* **Lifecycle** (`Diagram.fromText`): text → `encodeEntities` (`#name;` → `ﬂ°name¶ß`) → `parser.parser.yy = db`
  (Jison only) → `db.clear()` → `await parser.parse(text)` → `await renderer.draw(text, id, version, diag)`.
* **External diagrams:** `mermaid.registerExternalDiagrams(defs, {lazyLoad})` is public in 11.14.0;
  `packages/mermaid-zenuml` is the reference plugin. **No fork is needed.**
* **Existing features worth reusing:** unified flowchart renderer shapes (`rendering-util/rendering-elements/shapes/`),
  `architecture-beta` (icons + groups + ports, but its own iconography and no ArchiMate arrowheads), ELK layout
  (`flowchart-elk`, layout loaders).

Why not reuse an existing type:

| Need (spec notation) | flowchart 11.14 | class | architecture-beta | C4 / block / requirement |
|---|---|---|---|---|
| Layer colours | ✔ via classDef | partial | ✘ | ✘ |
| Square vs rounded corners | ✔ | ✘ | ✘ | ✘ |
| Corner icons (component, cube, lollipop …) | ✘ (icon shapes are full-size icons) | ✘ | own icon set | ✘ |
| Filled/hollow diamond at source | ✘ | ✔ | ✘ | ✘ |
| Hollow triangle (realization, specialization) | ✘ | ✔ | ✘ | ✘ |
| Assignment dot at source + arrow | ✘ | ✘ | ✘ | ✘ |
| Dotted vs dashed semantics with correct heads | partial | partial | ✘ | ✘ |
| Rule validation | ✘ | ✘ | ✘ | ✘ |
| markdown2docx native support | L4, but no diamonds/triangles | L4 via Naiad only for drawing | L1 | L1 |

## 4. Options and recommendation

### (a) Convention on flowchart + external validator (fallback only)
Write ArchiMate as flowchart with `classDef`s (`app`/`tech` fills), stereotype text («Application Service»),
dotted/thick edges and edge labels naming the relationship; a linter checks the rules.
* ✔ Renders today in Mermaid and markdown2docx.
* ✘ Cannot show the standard arrowheads, the assignment dot, corner icons, device 3D box or data-object header.
  The relationship type has to be spelled out as edge text. Rules are not enforced at render time.
* The PoC implements this as `to-flowchart` (see `*.flowchart-fallback.png`).

### (b) Dedicated `archimate` diagram type (**recommended**)
* ✔ Spec-faithful notation, compact syntax, rule violations become Mermaid parse errors (Mermaid shows its
  error diagram), layer-aware layout.
* ✔ Mermaid side needs no fork: an external plugin works on stock 11.14.0 (demonstrated).
* ✘ Needs a native C# implementation in markdown2docx (the plugin can't run there). Estimated pieces:
  1. **Parser** — port of the grammar (small; ~10 statement forms). If the vendored Mermaid parser
     infrastructure is used for detection, add `archimate` to the detector list in the recogniser.
  2. **Rules** — load `archimate-rules.json` (embedded resource); identical to the JS validator.
  3. **Notation** — port `notation.js` (box outlines, 20×14 icon paths, end-marker paths): pure geometry,
     emitted as SVG paths so Svg.Skia rasterises it without `<marker>`.
  4. **Layout** — reuse the existing Sugiyama/MSAGL pipeline (compound nodes needed for nesting/groups) with
     the same layout-only edge reversal the PoC uses (consumers above providers, Application above Technology).
  5. **Level targets** — follow the repo's recognition levels (L1 parse → L4 parity) with golden SVG tests
     generated from the JS PoC.
* Production JS grammar should be Langium (in `@mermaid-js/parser` style) so it can be upstreamed or shared;
  the PoC uses a hand-written line parser.

**Plan:** (1) agree syntax and version target; (2) ship the JS plugin; (3) build the C# kind in
markdown2docx; (4) until then, markdown2docx users can pre-transpile with `cli.mjs to-flowchart`.

## 5. Syntax

```
enterprise                                  %% header keyword
  title <text>
  version 3.1 | 3.2                         %% rule set; default 3.2 (15 cells provisional †)
  direction TB | LR
  group <id> ["Label"] [in <group-id>]      %% drawn as the spec Grouping notation (dashed box with tab)
  <element-type> <id> ["Label"] [in <group-or-element-id>]
  <src> -<relationship>[(qualifier)]-> <tgt> ["label"]
  %% comment
```

* **Element types** (kebab-case; the spec's CamelCase names are also accepted):
  `application-component, application-collaboration, application-interface, application-function,
  application-interaction, application-process, application-event, application-service, data-object,
  node, device, system-software, technology-collaboration, technology-interface, path,
  communication-network, technology-function, technology-process, technology-interaction,
  technology-event, technology-service, artifact`.
* **Relationships**: full name or the spec letter — `composition|c, aggregation|g, assignment|i,
  realization|r, serving|v, access|a, influence|n, triggering|t, flow|f, specialization|s, association|o`.
  Direction is always source → target as in the spec tables (e.g. composition: whole → part).
* **Qualifiers**: `access(read|write|readwrite|access)` (default `write`; arrowhead drawn per spec:
  read = towards the accessor, write = towards the object); `influence(+|-|++|--|n)`;
  `association(directed)`.
* **Nesting** (`in <element>`): drawn as containment. The spec allows nesting to express composition,
  aggregation, assignment, realization, access or specialization, but nesting is ambiguous, so the model must
  also contain an explicit parent → child relationship of one of those types; otherwise a warning is raised.
  Relationships expressed by nesting are hidden in the drawing.
* **Validation**: every relationship is checked against the rule table for the selected version. A violation
  is an error naming the line, the element types and the allowed alternatives, e.g.
  `line 10: serving from DataObject "Order" to ApplicationComponent "Web Shop" is not allowed in ArchiMate 3.1. Allowed: association.`
  (Influence is parsed but no in-scope element pair allows it, so it is always rejected in this scope.)

Example (`samples/03-cross-layer-webshop.mmd`, abridged):

```enterprise
enterprise
  title Web shop - application and technology view
  group dc "Primary data centre"
  application-component webShop  "Web Shop"
  application-function  checkout "Checkout" in webShop
  application-service   ordering "Online Ordering"
  data-object           order    "Order"
  technology-service    hosting  "Java Application Hosting"
  node                  appNode  "App Server Node" in dc
  system-software       jvm      "Java VM" in appNode
  artifact              war      "shop.war" in appNode
  webShop  -assignment->    checkout
  checkout -realization->   ordering
  checkout -access(write)-> order
  appNode  -composition->   jvm
  appNode  -assignment->    war
  war      -realization->   webShop
  jvm      -realization->   hosting
  hosting  -serving->       webShop
```

## 6. Element notation (source of truth: ArchiMate 3.1 spec, Appendix A and §3.9)

General conventions from the spec: rectangles with **square corners for active/passive structure**,
**rounded corners for behaviour**; a small icon in the top-right corner identifies the type. Colours carry no
formal semantics in the spec, but its figures use blue for Application and green for Technology; the PoC uses
the fills measured from the spec's Appendix A figure: **Application `#AFFFFF`, Technology `#AFFFAF`**
(Business, out of scope, `#FFFFAF`). Black 1 px outline.

| Element | Box | Corner icon (drawn fresh) |
|---|---|---|
| Application component | square | component: rectangle with two small tabs on the left |
| Application / Technology collaboration | square | two overlapping circles |
| Application / Technology interface | square | lollipop (short line + circle) |
| Application / Technology function | rounded | upward chevron (bookmark-like arrow) |
| Application / Technology interaction | rounded | circle split into two half-circles |
| Application / Technology process | rounded | right-pointing block arrow |
| Application / Technology event | rounded | event shape: notched (pointed-in) left end, rounded right end |
| Application / Technology service | rounded | stadium (fully rounded rectangle) |
| Data object | square, with header band | none (the header band identifies it) |
| Node | square | 3D cube |
| Device | 3D-bevelled box | monitor on a stand |
| System software | square | circle with an offset arc behind it (disc) |
| Path | square | thin angle brackets with a dotted line between them ("<··>") |
| Communication network | square | four filled black dots on a parallelogram, joined by lines |
| Artifact | square | page with folded top-right corner |

Alternative "large icon" shapes from Appendix A (e.g. node as a 3D box, interface as a lollipop symbol,
service as a stadium shape) and junctions (filled = and, hollow = or) are not in the PoC; see open questions.

## 7. Relationship notation (spec §5 / Appendix A)

| Relationship | Letter | Line | Source end | Target end |
|---|---|---|---|---|
| Composition | c | solid | filled diamond | — |
| Aggregation | g | solid | hollow diamond | — |
| Assignment | i | solid | filled dot | filled arrowhead |
| Realization | r | dotted | — | hollow triangle |
| Serving | v | solid | — | open arrowhead (line style "→") |
| Access | a | dotted | — | small open arrowhead (direction by qualifier: read points to the accessor; readwrite = both ends; `access` = none) |
| Influence | n | dashed | — | open arrowhead, optional +/- label |
| Triggering | t | solid | — | filled arrowhead |
| Flow | f | dashed | — | filled arrowhead |
| Specialization | s | solid | — | hollow triangle |
| Association | o | solid | — | none (directed: half/open arrowhead) |

All ends are emitted as explicit `<path>`/`<circle>` elements (no SVG `<marker>`), so they rasterise
correctly in Svg.Skia. See `samples/rendered/00-notation-legend.png`.

## 8. Constraints (allowed relationships)

**Provenance.** The 22 × 22 Application + Technology **3.1** cells were checked by eye against the ArchiMate 3.1
Appendix B.5 tables — **all 484 match**. Every other cell (Business, Motivation, Strategy, Physical,
Implementation & Migration, Grouping, Location, Junction, and the 15 Application/Technology **3.2** deltas)
is taken from the MIT-licensed Archi project's `relationships.xml` (3.1: commit `9ba0559c`, release_4.6.0;
3.2: commit `0469def6`, release_5.0.0). Archi is the **source** of those cells, not a transcription aid.
They stay **provisional** — Archi is its maintainers' reading of The Open Group tables, not an official
Open Group publication, and those cells have not been verified against specification text. Machine-readable:
`docs/enterprise/enterprise-rules.json`; pinned Archi files: `docs/enterprise/archi/`. See
`THIRD-PARTY-NOTICES.md`.

Note that the 3.1 Appendix B tables already include the **permitted derived relationships**, which is why many
cross-layer cells are permissive (e.g. `fotv`).

Letters: a=access, c=composition, f=flow, g=aggregation, i=assignment, n=influence, o=association,
r=realization, s=specialization, t=triggering, v=serving. Row = source, column = target. **ArchiMate 3.1**;
† = cell differs in 3.2 (provisional).

| src \ tgt | AC | ACo | AI | AF | AIn | AP | AE | AS | DO | N | Dev | SS | TC | TI | Pa | CN | TF | TP | TIn | TE | TS | Art |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **AC** | cfgorstv | fotv | cfgortv | fiortv | fiortv | fiortv | fiortv | fiortv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | ao |
| **ACo** | fgortv | cfgostv | cfgortv | fiortv | fiortv | fiortv | fiortv | fiortv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | ao |
| **AI** | fotv | fotv | cfgostv | fotv | fotv | fotv | fotv | fiotv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | ao |
| **AF** | fotv | fotv | fotv | cfgostv | cfgotv | cfgotv | fotv | fortv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | ao |
| **AIn** | fotv | fotv | fotv | cfgotv | cfgostv | cfgotv | fotv | fortv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | ao |
| **AP** | fotv | fotv | fotv | cfgotv | cfgotv | cfgostv | fotv | fortv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | ao |
| **AE** | fotv | fotv | fotv | fotv | fotv | fotv | cfgostv | fotv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | ao |
| **AS** | fotv | fotv | fotv | fotv | fotv | fotv | fotv | cfgostv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | ao |
| **DO** | o | o | o | o | o | o | o | o | cgos | o | o | o | o | o | o | o | o | o | o | o | o | o |
| **N** | fortv | fortv | fortv | fortv | fortv | fortv | fortv | fortv | ao | cfgiorstv† | cfgiorstv† | cfgiorstv† | fotv | cfgiortv | fotv | fotv | fiortv | fiortv | fiortv | fiortv | fiortv | aio |
| **Dev** | fortv | fortv | fortv | fortv | fortv | fortv | fortv | fortv | ao | cfgiorstv† | cfgiorstv† | cfgiorstv† | fotv | cfgiortv | fotv | fotv | fiortv | fiortv | fiortv | fiortv | fiortv | aio |
| **SS** | fortv | fortv | fortv | fortv | fortv | fortv | fortv | fortv | ao | cfgiorstv† | cfgiorstv† | cfgiorstv | fotv | cfgiortv | fotv | fotv | fiortv | fiortv | fiortv | fiortv | fiortv | aio |
| **TC** | fortv | fortv | fortv | fortv | fortv | fortv | fortv | fortv | ao | fgiortv† | fgiortv | fgiortv | cfgostv | cfgiortv | fotv | fotv | fiortv | fiortv | fiortv | fiortv | fiortv | aio |
| **TI** | fotv | fotv | fortv | fotv | fotv | fotv | fotv | fortv | ao | fotv | fotv | fotv | fotv | cfgostv | fotv | fotv | fotv | fotv | fotv | fotv | fiotv | ao |
| **Pa** | fortv | fortv | fortv | fortv | fortv | fortv | fortv | fortv | ao | fgiortv† | fgiortv | fgiortv | fgotv | fgiortv | cfgostv | fotv | fiortv | fiortv | fiortv | fiortv | fiortv | aio |
| **CN** | fortv | fortv | fortv | fortv | fortv | fortv | fortv | fortv | ao | fgiortv† | fgiortv† | fgiortv | fortv | fgiortv | fortv | cfgostv | fiortv | fiortv | fiortv | fiortv | fiortv | aio |
| **TF** | fotv | fotv | fotv | fortv | fortv | fortv | fotv | fortv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | cfgostv | cfgotv | cfgotv | fotv | fortv | ao |
| **TP** | fotv | fotv | fotv | fortv | fortv | fortv | fotv | fortv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | cfgotv | cfgostv | cfgotv | fotv | fortv | ao |
| **TIn** | fotv | fotv | fotv | fortv | fortv | fortv | fotv | fortv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | cfgotv | cfgotv | cfgostv | fotv | fortv | ao |
| **TE** | fotv | fotv | fotv | fotv | fotv | fotv | fortv | fotv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | cfgostv | fotv | ao |
| **TS** | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fortv | ao | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | fotv | cfgostv | ao |
| **Art** | or | or | or | or | or | or | or | or | or | or† | or† | or | o | or | o | o | or | or | or | or | or | cgos† |

Key: AC = ApplicationComponent, ACo = ApplicationCollaboration, AI = ApplicationInterface, AF = ApplicationFunction, AIn = ApplicationInteraction, AP = ApplicationProcess, AE = ApplicationEvent, AS = ApplicationService, DO = DataObject, N = Node, Dev = Device, SS = SystemSoftware, TC = TechnologyCollaboration, TI = TechnologyInterface, Pa = Path, CN = CommunicationNetwork, TF = TechnologyFunction, TP = TechnologyProcess, TIn = TechnologyInteraction, TE = TechnologyEvent, TS = TechnologyService, Art = Artifact

The 15 provisional 3.2 changes (3.1 → 3.2):

```
15 in-scope cells differ between Archi 3.1 and 3.2 tables
Node                       -> Node                       3.1=cfgiorstv    3.2=cfgiostv
Node                       -> Device                     3.1=cfgiorstv    3.2=cfgiortv
Node                       -> SystemSoftware             3.1=cfgiorstv    3.2=cfgiortv
Device                     -> Node                       3.1=cfgiorstv    3.2=fotv
Device                     -> Device                     3.1=cfgiorstv    3.2=cfgostv
Device                     -> SystemSoftware             3.1=cfgiorstv    3.2=cfgiortv
SystemSoftware             -> Node                       3.1=cfgiorstv    3.2=fotv
SystemSoftware             -> Device                     3.1=cfgiorstv    3.2=fotv
TechnologyCollaboration    -> Node                       3.1=fgiortv      3.2=fgiotv
Path                       -> Node                       3.1=fgiortv      3.2=fgiotv
CommunicationNetwork       -> Node                       3.1=fgiortv      3.2=fortv
CommunicationNetwork       -> Device                     3.1=fgiortv      3.2=fgortv
Artifact                   -> Node                       3.1=or           3.2=o
Artifact                   -> Device                     3.1=or           3.2=o
Artifact                   -> Artifact                   3.1=cgos         3.2=cgors
```

**Cross-layer highlights (3.1):**
* Application → Technology: almost everything is `f o t v` (flow, association, triggering, serving — mostly via
  derivation); Application elements → Artifact is `a o` only (access/association).
* Technology active structure (Node, Device, System Software, Technology Collaboration, Path, Communication
  Network) → any Application element: `f o r t v` — e.g. System Software *realizes* an Application Component.
  Technology Interface → Application: `f o t v` (+ `r` to Application Interface/Service).
* Technology function/process/interaction → Application function/process/interaction/service: `f o r t v`
  (to Application Event and structure: `f o t v`). Technology Service → Application elements: `f o t v`
  (serving), plus `r` to Application Service.
* **Artifact realizes Application Component and Data Object** (the table allows `o r` from Artifact to every
  Application element, the others via derivation). Towards Technology Collaboration, Path and Communication
  Network an Artifact may only be associated (`o`).
* **Access** targets only passive structure: Data Object and Artifact (`a` never appears towards any other column).
* Data Object as source: association only (plus `c g s` to itself; Artifact to Artifact `c g s` in 3.1).
* Grouping (B.6, not a typed element in the PoC): a grouping may aggregate or compose anything, and may have any
  relationship that would be valid for its contents. In the PoC `group` is visual only (no relationships).

## 9. Copyright, licensing and trademark

**Sources consulted:** The Open Group trademarks page (https://www.opengroup.org/trademarks); the ArchiMate 4
Commercial Licence (https://www.opengroup.org/sites/default/files/contentimages/Licenses/ArchiMate4_CLv.1_ff.pdf)
and Non-Commercial Licence (…/ArchiMate4_NCLv.1_ff.pdf); the ArchiMate 3.1 specification front matter and page
footer (Copyright © 2012-2019 The Open Group; documentation downloadable under licence, free for internal use).

Key points:
1. **Trademark.** ArchiMate® is a registered trademark of The Open Group. Use it as an adjective
   ("ArchiMate modeling language", "ArchiMate notation"), not as a noun or a product name. Don't use it to name or
   brand a product, service, tool, package, domain or company without permission. Include the attribution:
   *"ArchiMate is a registered trademark of The Open Group in the United States and other countries."*
   (The PoC source files carry it.)
2. **Spec licence.** The specification is copyrighted and licensed. Internal, non-commercial use is free under
   the Non-Commercial Licence. Building **tools or products for commercial gain**, or commercial training, needs
   the annual **Commercial Licence** (free for ArchiMate Forum members). Consultancy and writing books are listed
   as exempt.
3. **Notation.** The spec presents Appendix A as the *default* iconography (modellers may use other
   iconography; conforming tools must at least support it). Implementing the notation is the expected use of the
   standard; copying the **figures** themselves is a copyright matter.

**What the PoC does to stay clean:**
* Every shape, icon and arrowhead is **drawn fresh as SVG path geometry** from the spec's verbal description and
  general appearance. No image, path data or figure was copied from the spec, from Archi or from any shape library.
* The spec pages and figures downloaded for verification (`sources/`) are copyrighted Open Group material kept
  **for local checking only**, marked in `sources/README-DO-NOT-REDISTRIBUTE.md`. Don't commit or publish them.
* Rule data: the 22 × 22 Application + Technology 3.1 cells were checked against the 3.1 Appendix B tables.
  Archi's MIT `relationships.xml` is the source of every new-layer cell and of the 15 Application/Technology
  3.2 deltas; those cells stay provisional. The shipped JSON and `THIRD-PARTY-NOTICES.md` record that source
  and the MIT licence. No Archi notation, figures, icons or code are used.

**Risk flags:**
* ⚠ **Product/package naming:** don't publish anything called "mermaid-archimate", "ArchiMate for Mermaid" and so
  on. The PoC's npm package is named neutrally (`ea-notation-mermaid-poc`, private). The `archimate` keyword
  inside a diagram is a descriptive use and is fine internally; for a public release, consider permission or a
  neutral alias (e.g. `ea-model`) alongside it, plus the attribution line.
* ⚠ **Commercial use:** if the renderer is sold, bundled into a paid product or used to deliver paid training,
  get the Commercial Licence (or Forum membership).
* ⚠ **Don't copy spec figures** (Appendix A/B images, example views) into docs, tests or icon sets; draw your own
  examples, as the samples here do.
* ⚠ **ArchiMate 4** (April 2026) publications carry a notice prohibiting use for LLM training/data synthesis.
  This work used only the 3.1 material and did not use ArchiMate 4 content.
* This is not legal advice; Dave should confirm with The Open Group or counsel before any public or commercial release.

## 10. Where Archi differs from the spec (in-scope elements)

Archi is a popular modelling tool, but its default icons and colours differ from the spec's Appendix A notation
in places, so it was **not** used as a notation reference. Differences seen in Archi's palette icons and default
colours (in-diagram figures not examined):

| Element | Spec (3.1 Appendix A) | Archi default |
|---|---|---|
| Technology layer colour | `#AFFFAF` (bright green) | `#C9E7B7` (muted, greyer green) |
| Application layer colour | `#AFFFFF` | `#B5FFFF` (near-identical) |
| Event (App/Tech) | notched, pointed-in left end, rounded right end | concave/crescent left end |
| Communication network | four **filled black** dots on a parallelogram | hollow, layer-coloured circles |
| Path | thin angle brackets with a dotted line ("<··>") | heavy chevrons with a dashed line |
| System software | circle with an offset arc behind it | two overlapping filled circles |

Other in-scope icons (component, collaboration, interface, function, interaction, process, service, artifact,
data object, node, device) are broadly similar. Diagrams compared side-by-side with Archi output will therefore
look slightly different by design.

## 11. ArchiMate 4 (released April 2026)

ArchiMate 4 (C260, April 2026) changes the in-scope area significantly: Application/Technology *Interaction* is
removed; collaborations and behaviour elements (process, function, event, service) become common across domains;
Path moves to a Common Domain (realization replaces aggregation); relationship multiplicity is added. The syntax
has a `version` directive so a `4` rule set and the merged element types can be added later. The PoC targets 3.1
(verified) and 3.2 (provisional), as asked.

## 12. Proof of concept

Location: `/workspace/mermaid-archimate/poc` (Node 20; private package `ea-notation-mermaid-poc`; mermaid pinned to
11.14.0 via npm overrides; elkjs 0.9.3; esbuild; @mermaid-js/mermaid-cli 11.17.0 for the fallback renders).

| File | Role |
|---|---|
| `extract_rules.py` | builds `archimate-rules.json`, `rules-diff-3.1-vs-3.2.txt`, `src/rules-data.js` (with provenance comments) |
| `src/notation.js` | element catalogue, layer colours, relationship notation, fresh SVG icon/box/arrowhead geometry |
| `src/parser.js`, `src/validate.js` | line parser; rule validation, nesting warnings |
| `src/render.js` | ELK layered layout (compound nodes, orthogonal routing, layer-aware edge reversal) → Skia-friendly SVG |
| `src/legend.js` | notation legend (all element and relationship types) |
| `src/to-flowchart.js` | fallback transpiler to plain `flowchart` |
| `src/mermaid-plugin.js` | Mermaid external diagram (`id: 'archimate'`, detector, db, parser, renderer) |
| `dist/mermaid-archimate.iife.js`, `dist/mermaid-archimate.esm.js` | bundled plugin (includes elkjs) |
| `cli.mjs` | `validate | render | to-flowchart | legend` |
| `test-plugin.mjs` | loads the real Mermaid 11.14.0 `mermaid.min.js` + plugin in headless Chrome and renders all samples |
| `svg2png.mjs` | PNG previews |

Using the plugin in a page:

```html
<script src="mermaid.min.js"></script>               <!-- 11.14.0 -->
<script src="mermaid-archimate.iife.js"></script>
<script>
  mermaid.registerExternalDiagrams([archimatePlugin.default]).then(() => mermaid.run());
</script>
```

Samples (`/workspace/mermaid-archimate/samples`) and results:

| Sample | Result |
|---|---|
| `01-application-layer.mmd` — all 9 Application elements | valid; rendered |
| `02-technology-layer.mmd` — all 13 Technology elements | valid; rendered (the validator caught a mistake while writing it: Path → Communication Network realization is not allowed) |
| `03-cross-layer-webshop.mmd` — groups, nesting, cross-layer | valid; rendered |
| `04-invalid-relationships.mmd` | 5 rule errors, as intended; Mermaid shows an error |
| `05-version-3.2-delta.mmd` — Device realizes Node | rejected under `version 3.2` (provisional rule), allowed under 3.1 |

Rendered output (`samples/rendered/`): `00-notation-legend.{svg,png}`; for 01–03, `*.native.{svg,png}` (CLI),
`*.mermaid-plugin.{svg,png}` (through Mermaid 11.14.0 `mermaid.render`) and `*.flowchart-fallback.{mmd,png}`
(degraded, markdown2docx-compatible); `mermaid-plugin-test-results.json`.

Known PoC limitations: hand-written parser (not Langium); no junctions or alternative large-icon shapes; label
placement is basic; with nesting, an edge from a nested child can cross its parent's title; ELK is bundled in
(about 1.5 MB minified), so a production build should lazy-load it or share Mermaid's ELK loader.

## 13. Open questions for Dave

1. Target **3.1, 3.2 or ArchiMate 4**? (3.1 fully verified; 3.2 deltas provisional.)
2. Can you check the 15 provisional 3.2 cells against your licensed copy of the 3.2 spec?
3. Naming: will anything be **published**? If so, choose a neutral package name and decide whether to keep
   the `archimate` keyword or add an alias.
4. Will it be used **commercially** (product, paid training)? If so, Commercial Licence / Forum membership.
5. Go ahead with the **native C# kind in markdown2docx**? Is the flowchart fallback acceptable in the meantime?
6. Layout preferences: layer bands (Application above Technology, as the PoC does by edge direction), TB vs LR.
7. Are alternative "large icon" shapes, junctions or Business layer elements needed?
8. The vendored Mermaid commit `1bb6ba5` isn't in upstream ("dirty"): is there a record of the local changes?

---
ArchiMate is a registered trademark of The Open Group in the United States and other countries.

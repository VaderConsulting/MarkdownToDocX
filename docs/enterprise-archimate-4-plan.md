# Enterprise diagram — ArchiMate 4.0 version plan

**Feature name:** `enterprise` (never ArchiMate as a product, package, class, namespace, or feature name).

**Status:** Planning only. The current implementation targets ArchiMate 3.2 full coverage. This document records the intended 4.0 follow-on.

**Licence reminder:** Do not ingest, quote, or train on ArchiMate 4 Specification text. The ArchiMate 4 licence forbids using the specification to train or develop large language models. This plan is based only on **public summaries** of the 4.0 changes (announcements, vendor blogs, and quoted official change lists). Points that are not corroborated by those public summaries are marked **unverified**.

ArchiMate® is a registered trademark of The Open Group.

---

## Full-coverage requirement (mandatory)

**Version 4.0 of the `enterprise` diagram will support ALL ArchiMate 4 domains. Coverage is complete, not a subset.**

That means every ArchiMate 4 domain listed below is in scope for parsing, validation, notation, layout, samples, and tests. There is no “core only”, “Application + Technology only”, or other reduced 4.0 palette. Composite constructs (Grouping, Location) and Junctions remain in scope; their exact 4.0 domain placement is listed where public summaries are silent or disagree, and those placements are marked **unverified**.

The 3.2 implementation already covers every 3.2 layer and aspect (Business, Application, Technology including Physical, Motivation, Strategy, Implementation & Migration, Grouping, Location, Junction). The 4.0 version must keep that same full-language stance after the 3.2 → 4.0 metamodel remap.

---

## Sources used (public summaries only)

| Source | Date | What it is | Used for |
|---|---|---|---|
| The Open Group announcement, “The Open Group Announces ArchiMate® 4 Specification” | 27 April 2026 | Official product announcement | Release date, continuity intent. Does not list per-domain element changes. |
| Quoted official change list (reproduced on r/EnterpriseArchitect from the announcement materials) | April 2026 | Public copy of the “main changes between Version 3.2 and Version 4” list | Removed elements, merged behaviour, Role, Collaboration, Path, layer→domain, multiplicity, hexagon depiction. |
| Marc Lankhorst, “ArchiMate® 4 Specification: What Changed from Version 3.2?” (Bizzdesign blog) | 20 August 2026 | Public commentary by the language’s long-standing managing consultant | Concept count (~60 → 42), Common Domain, deprecated-concept replacements, domains vs layers, hexagonion name, multiplicity rationale. Not specification text. |
| Visual Paradigm, “ArchiMate 4 Support” and “What’s new in ArchiMate 4” pages | 2026 | Vendor tooling pages | Domain list (Common, Business, Application, Technology, Motivation, Strategy, Implementation & Migration); Path in Common; Technology includes OT/physical. Extra claims (Motivation at centre, Composition removed) are **unverified** against the official change list. |
| The Open Group white paper W262, “The Motivation for Changes in the ArchiMate® 4 Specification” (Lankhorst & Sarrodie) | 27 April 2026 | Cited by public blogs; **the white paper text was not read** | Existence and authorship only. Any detail attributed only to W262 is **unverified**. |

The ArchiMate 4 Specification itself (C260 and any evaluation PDF) was **not** used as a source.

---

## Cross-cutting changes (all domains)

These apply to the whole language, not to a single domain.

| Change | Public summary | Confidence |
|---|---|---|
| “Layer” renamed to **domain** | Official change list; Bizzdesign; Visual Paradigm | Publicly summarised |
| Framework depiction is no longer a layer × aspect matrix; it is a concentric hexagonal diagram (Bizzdesign: “hexagonion”) | Official change list; Bizzdesign | Publicly summarised |
| Motivation drawn at the centre of that hexagon | Visual Paradigm only | **Unverified** |
| Concept count reduced by about 30%, from about 60 to 42 | Bizzdesign | Publicly summarised (vendor commentary) |
| Behaviour concepts that were split by layer are merged into one set: **Service, Process, Function, Event** | Official change list; Bizzdesign | Publicly summarised |
| Business / Application / Technology **Collaboration** merge into one **Collaboration** | Official change list | Publicly summarised |
| **Business Role** replaced by a generic **Role** that any internal active structure element can be assigned to | Official change list | Publicly summarised |
| **Path** moves to the Common Domain; aggregation from Path to a technology internal active structure element is replaced by **realization** from the active structure element to Path | Official change list; Visual Paradigm | Publicly summarised |
| Relationships may carry **multiplicity** (lower/upper bounds on each end) | Official change list; Bizzdesign | Publicly summarised |
| **Composition** relationship removed; use Aggregation or Grouping instead | Visual Paradigm only; absent from the official change list and the Bizzdesign summary | **Unverified** |
| Aspects (active / behaviour / passive) remain in the language but are no longer drawn on the new framework figure | Bizzdesign | Publicly summarised |

Removed / deprecated concepts named in public summaries (replacements are Bizzdesign’s public wording, not specification text):

| 3.2 concept | 4.0 treatment in public summaries |
|---|---|
| Business Interaction, Application Interaction, Technology Interaction | Removed. Model as Process plus the participants. |
| Constraint | Removed. Model as a Requirement that states what cannot be done. |
| Contract | Removed. Model as a Business Object. |
| Representation | Removed. Paper: Material. Digital: Data Object or Artifact. |
| Gap | Removed. Model as an Assessment of the difference between two Plateaus. |
| Implementation Event | Replaced by the generic Event. |

---

## Domain-by-domain coverage (3.2 → 4.0)

Every row below is in the 4.0 product. “What changes from 3.2” is limited to public summaries.

### 1. Common Domain (new in 4.0)

**3.2:** No Common layer. Shared behaviour lived as parallel Business / Application / Technology types (Process, Function, Service, Event, Interaction, Collaboration). Path lived in Technology. Role lived only as Business Role.

**4.0 (public summaries):**

* New domain for generic elements that combine with other domains.
* Holds the merged **Service, Process, Function, Event**.
* Holds the merged **Collaboration**.
* Holds the generic **Role** (replaces Business Role).
* Holds **Path** (moved from Technology).
* Grouping and Location are placed here by some third-party primers (Linked.Archi). **Unverified** — the official change list does not say where Grouping or Location sit.

**Enterprise 4.0 work:** Accept the generic tokens (`service`, `process`, `function`, `event`, `collaboration`, `role`, `path`) as Common Domain types; keep 3.2 prefixed aliases as a migration mode. Path relationship check: realization from active structure to Path, not aggregation from Path. **Unverified:** exact remaining Common types (Interface? Grouping? Location?) beyond those named above.

### 2. Business Domain

**3.2 layer:** Business (yellow `#FFFFAF`): Business Actor, Business Role, Business Collaboration, Business Interface, Business Function, Business Interaction, Business Process, Business Event, Business Service, Business Object, Contract, Representation, Product.

**4.0 (public summaries):**

* Still a first-class domain (not dropped). The word is **domain**, not layer.
* Layer-specific behaviour types (Business Process / Function / Service / Event / Interaction) move to Common as Process / Function / Service / Event; Interaction is removed.
* Business Collaboration folds into generic Collaboration.
* Business Role becomes generic Role.
* Contract and Representation are removed (see replacements above).
* Business Actor, Business Interface, Business Object, and Product remain in public vendor lists (Visual Paradigm / Linked.Archi). **Unverified** as an official residual list — those pages are not the specification.

**Enterprise 4.0 work:** Keep Business Actor, Business Interface, Business Object, Product (and any other Business types confirmed later). Stop accepting Contract, Representation, Business Interaction, and the Business-prefixed behaviour/collaboration/role tokens except as deprecated aliases that rewrite to Common types.

### 3. Application Domain

**3.2 layer:** Application (cyan `#AFFFFF`): Application Component, Application Collaboration, Application Interface, Application Function, Application Interaction, Application Process, Application Event, Application Service, Data Object.

**4.0 (public summaries):**

* Still a first-class domain.
* Application Process / Function / Service / Event merge into Common.
* Application Interaction removed.
* Application Collaboration folds into generic Collaboration.
* Application Component, Application Interface, and Data Object remain in public vendor lists. **Unverified** as an official residual list.

**Enterprise 4.0 work:** Keep Application Component, Application Interface, Data Object (and any other Application types confirmed later). Same deprecated-alias treatment for prefixed behaviour, interaction, and collaboration.

### 4. Technology Domain (includes physical / operational technology)

**3.2 layers:** Technology and Physical, both green `#AFFFAF`: Node, Device, System Software, Technology Collaboration, Technology Interface, Path, Communication Network, Technology Function, Technology Process, Technology Interaction, Technology Event, Technology Service, Artifact, plus Equipment, Facility, Distribution Network, Material.

**4.0 (public summaries):**

* Still a first-class domain. Visual Paradigm states it comprises **both information technology and operational (physical) technology**.
* Technology Process / Function / Service / Event merge into Common.
* Technology Interaction removed.
* Technology Collaboration folds into generic Collaboration.
* Path leaves Technology and joins Common; Path’s former aggregation-to-node-like-elements becomes realization the other way.
* Physical elements are not listed as a separate domain in the public summaries; they are treated as part of Technology. **Unverified:** official confirmation that Equipment, Facility, Distribution Network, and Material keep those names unchanged.

**Enterprise 4.0 work:** Keep Node, Device, System Software, Technology Interface, Communication Network, Artifact, Equipment, Facility, Distribution Network, Material (pending confirmation). Move Path to Common. Drop Technology-prefixed behaviour, interaction, and collaboration except as aliases.

### 5. Motivation Domain

**3.2 aspect:** Motivation (purple `#CCCCFF`): Stakeholder, Driver, Assessment, Goal, Outcome, Principle, Requirement, Constraint, Meaning, Value.

**4.0 (public summaries):**

* Still a first-class domain (“stakeholders, goals, and other related contexts and motivations”).
* Constraint is removed; public replacement is Requirement.
* Visual Paradigm places Motivation at the centre of the hexagonion. **Unverified** as a language rule (it may be depiction only).
* Residual Motivation set in third-party lists: Stakeholder, Driver, Assessment, Goal, Outcome, Principle, Requirement, Meaning, Value. **Unverified** as an official residual list.

**Enterprise 4.0 work:** Keep the Motivation types except Constraint (accept `constraint` only as a deprecated alias for Requirement, if a migration mode is offered). Do not treat “Motivation at the centre” as a layout algorithm until it is confirmed; current TB rank (Motivation above Strategy above Business …) stays unless a documented 4.0 layout rule says otherwise.

### 6. Strategy Domain

**3.2 aspect:** Strategy (salmon `#F8D0C8`): Resource, Capability, Value Stream, Course of Action.

**4.0 (public summaries):**

* Still a first-class domain (“strategic direction and choices, based on the motivation of the enterprise”).
* No official change-list item names a Strategy element as added or removed.
* Third-party lists keep Resource, Capability, Value Stream, Course of Action. **Unverified** as an official residual list.

**Enterprise 4.0 work:** Keep all four Strategy types unless a later public summary says otherwise. Mark any relationship-table deltas **unverified** until a public 4.0 matrix exists (do not read the specification tables).

### 7. Implementation and Migration Domain

**3.2 aspect:** Implementation & Migration (pink `#FFE0E0`): Work Package, Deliverable, Implementation Event, Plateau, Gap.

**4.0 (public summaries):**

* Still a first-class domain (“evolution and implementation of architectures”).
* Implementation Event is replaced by generic Event (Common).
* Gap is removed; public replacement is an Assessment between Plateaus.
* Work Package, Deliverable, and Plateau remain in third-party lists. **Unverified** as an official residual list.

**Enterprise 4.0 work:** Keep Work Package, Deliverable, Plateau. Drop Gap and Implementation Event except as deprecated aliases (Gap → Assessment, Implementation Event → Event).

### Composite constructs and Junctions (domain placement unverified)

| 3.2 construct | 4.0 public summaries | Enterprise 4.0 stance |
|---|---|---|
| Grouping | Not in the official change list. Some primers put it in Common. | **Keep. Domain placement unverified.** Still a typed element and a visual dashed box. |
| Location | Not in the official change list. Some primers put it in Common. | **Keep. Domain placement unverified.** |
| Junction (and / or) | Not mentioned in the official change list or the Bizzdesign summary. | **Keep unless a later public summary removes it. Presence in 4.0 is unverified.** |

---

## Product rules for the 4.0 version

1. **Full domain coverage** — implement every domain in the table above. Do not ship a subset palette.
2. **Public summaries only** — do not paste, embed, or quote ArchiMate 4 Specification text, Appendix tables, or figures. If a rule cannot be sourced from a public summary, implement it as **provisional** and say so in diagnostics, the same way 3.2 new-layer cells are provisional today.
3. **Migration aliases** — 3.2 prefixed tokens (`business-process`, `application-service`, `technology-function`, `business-role`, `implementation-event`, `constraint`, `contract`, `gap`, …) should parse in a compatibility mode and rewrite to the 4.0 generic or replacement type, with an info diagnostic.
4. **Relationship table** — ship a 4.0 matrix only from a public, redistributable typing aid (for example a future MIT Archi `relationships.xml` labelled 4.0) or from cells that can be justified without specification text. Every 4.0 cell starts **provisional** until verified by a human against a licence-safe source.
5. **Multiplicity** — extend the relationship qualifier grammar (for example `-serving(1..*)->`) once the public syntax we choose is documented. Until then, parse multiplicity as an optional qualifier and treat unknown forms as errors.
6. **Composition** — keep Composition in the 3.2 tables. For 4.0, do **not** remove Composition until the official change list or another non-spec public Open Group source says it is gone. Visual Paradigm’s removal claim stays **unverified**.
7. **Notation** — draw 4.0 icons fresh from verbal public descriptions. Do not copy Archi, draw.io, specification figures, or ArchiMate 4 artwork.
8. **Tests and samples** — one sample and one render/validation test per 4.0 domain, plus a cross-domain view (Motivation → Strategy → Business → Application → Technology → Implementation), plus Common-only behaviour assigned to Business and Technology participants.
9. **Version switch** — `version 3.1` and `version 3.2` remain. Add `version 4.0`. Default stays 3.2 until 4.0 validation is complete.
10. **Trademark** — continue to use “ArchiMate” only as an adjective (“ArchiMate modeling language”, “ArchiMate notation”), never as the feature name.

---

## Suggested implementation order (after 3.2 is merged)

1. Extend `version` to accept `4.0` and add a 4.0 rules document that starts empty/provisional.
2. Add Common Domain types and the generic Role / Collaboration / Path remap.
3. Remove or alias the seven publicly named removals (three Interactions, Constraint, Contract, Representation, Gap) and Implementation Event → Event.
4. Add multiplicity qualifiers.
5. Retarget Path relationship direction.
6. Samples and tests for **every** 4.0 domain (not a subset).
7. README / `status.md` / this plan: mark each domain Done or Deferred; keep **unverified** labels until a licence-safe source confirms them.

---

## Explicitly deferred / unverified (do not pretend these are closed)

* Exact official residual element list per domain (vendor lists only).
* Official home of Grouping, Location, and Junction in 4.0.
* Whether Composition is removed.
* Whether Motivation-at-centre is a depiction or a layout rule.
* Full 4.0 allowed-relationship matrix.
* Notation deltas beyond “depiction updated to a hexagon”.
* Anything stated only in W262 or in the ArchiMate 4 Specification text.

Those items stay marked **unverified** until a public, licence-safe source confirms them. They do **not** reduce the coverage requirement: the 4.0 version still implements every domain, using provisional rules where the public summaries stop.

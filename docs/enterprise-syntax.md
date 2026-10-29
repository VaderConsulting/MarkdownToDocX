# Enterprise diagram syntax

This guide documents the Enterprise diagram syntax parsed by MarkdownToDocX. It is a dedicated grammar, not ordinary Mermaid flowchart syntax: declare typed Enterprise elements and use the relationship forms below. Mermaid-style shapes and arrows such as `A --> B` are not accepted in an Enterprise diagram.

The parser implementation is [`EnterpriseParser`](../src/MermaidRecognition/Kinds/Enterprise/EnterpriseParser.cs). The supported element/relationship pairings for each language version are in [`enterprise-rules.json`](enterprise/enterprise-rules.json). The repository's [`enterprise.md`](../enterprise.md) is the progressive recognition and rendering catalogue, with examples and expected diagnostics.

## A first diagram

Put Enterprise source in a `mermaid` fence:

````markdown
```mermaid
enterprise LR
title Online ordering
version 3.2
application-service ordering "Online Ordering"
application-component webShop "Web Shop"
ordering -serving-> webShop
```
````

`enterprise` and `enterprise-beta` are accepted headers. A header may specify `TB`, `TD`, `BT`, `LR`, or `RL`; the default direction is `TB`. A `direction` statement can set or override the direction, including inside a group to set that group's direction. Keywords and type names are case-insensitive; identifiers are case-sensitive.

The default language version is `3.2`. Use `version 3.1` or `version 3.2` to select the relationship-validity matrix. A relationship must be permitted for its source and target element types in that version.

## Source statements

Statements normally end at a newline. A semicolon can separate multiple statements on one line. `title`, `accTitle`, and `accDescr` consume the rest of their line, so semicolons in those statements are text. A `%%` starts a comment through the end of the line; it may follow a statement. Comments do not start inside quoted labels. Leading Mermaid YAML frontmatter and `%%{init: ...}%%` directives are read as configuration. References can point to declarations that appear later in the block.

### Identifiers and labels

An identifier starts with a letter, digit, or underscore and may also contain combining marks, dots, and hyphens. Identifiers are case-sensitive. These statement keywords are reserved in any case and cannot be identifiers: `title`, `version`, `direction`, `group`, `in`, `classDef`, `class`, `style`, `linkStyle`, `accTitle`, and `accDescr`.

Labels are optional and can use single or double quotes. Without a label, the identifier is displayed. The empty string `""` is a valid empty label. Quoted labels support escaped quotes, backslashes, and `\n`; `<br>`, `<br/>`, and `<br />` also create line breaks. Mermaid entity references are decoded. Markdown labels can be written as quoted backtick strings, for example ``"`**Important**`"``.

Redeclaring an identifier with the same element type updates the label, parent, or placement hint when supplied. Reusing an identifier for a different element type, or duplicating a drawing-item identifier, is an error.

### Element types

Write an element declaration as:

```text
<type> <id> ["label"] [in <container>] [<placement-hint>]
```

The canonical kebab-case type tokens are:

| Enterprise aspect | Element types |
| --- | --- |
| Application | `application-component`, `application-collaboration`, `application-interface`, `application-function`, `application-interaction`, `application-process`, `application-event`, `application-service`, `data-object` |
| Technology and physical | `node`, `device`, `system-software`, `technology-collaboration`, `technology-interface`, `path`, `communication-network`, `technology-function`, `technology-process`, `technology-interaction`, `technology-event`, `technology-service`, `artifact`, `equipment`, `facility`, `distribution-network`, `material` |
| Motivation | `stakeholder`, `driver`, `assessment`, `goal`, `outcome`, `principle`, `requirement`, `constraint`, `meaning`, `value` |
| Strategy | `resource`, `capability`, `value-stream`, `course-of-action` |
| Business | `business-actor`, `business-role`, `business-collaboration`, `business-interface`, `business-function`, `business-interaction`, `business-process`, `business-event`, `business-service`, `business-object`, `contract`, `representation`, `product` |
| Implementation and migration | `work-package`, `deliverable`, `implementation-event`, `plateau`, `gap` |
| Composite and junction | `grouping`, `location`, `junction`, `and-junction`, `or-junction` |

Hyphen-free spellings and the specification's CamelCase type names are also accepted, in any case. `group` is an alias for `grouping`; `junction` defaults to AND. Use `and-junction` or `or-junction` to choose a junction explicitly.

### Groups and containment

Declare a named group with an optional label, parent, placement hint, and brace-delimited members:

```text
group <id> ["label"] [in <parent>] [<placement-hint>] {
  ...
}
```

Close a group with `}` or `end`. Groups can be nested and can have their own `direction`. Alternatively, put `in <container-id>` on an element, group, or note declaration to place it in an existing container. Group and element IDs share the same namespace. The special `at <row> <column>` absolute pin is not supported on groups; group containment must determine their placement.

### Notes and plain connections

Notes are declared separately from typed elements:

```text
note <id> "<text>" [in <container>] [<placement-hint>]
```

A plain connection uses at least three dashes and must connect a note at one or both ends:

```text
<drawing-item> --- <drawing-item> [<routing-hints>]
```

It is a visual connection, not a typed Enterprise relationship. Extra dashes can request a longer connection. Notes and plain connections can use placement and routing hints.

## Relationships

Write a typed relationship with a source and target element:

```text
<source-id> -<kind>[(qualifier)]-> <target-id> ["label"] [<routing-hints>]
```

Extra dashes before `>` can request a longer connection. The relationship kind can be written by its name or its single-letter alias:

| Relationship | Alias |
| --- | --- |
| `composition` | `c` |
| `aggregation` | `g` |
| `assignment` | `i` |
| `realization` | `r` |
| `serving` | `v` |
| `access` | `a` |
| `influence` | `n` |
| `triggering` | `t` |
| `flow` | `f` |
| `specialization` | `s` |
| `association` | `o` |

Names and aliases are case-insensitive. The parser checks each source/target/type combination against the selected version's rules. Unknown kinds, unsupported qualifiers, missing endpoints, and disallowed combinations are errors.

Three relationship kinds accept qualifiers:

| Kind | Qualifiers | Default |
| --- | --- | --- |
| `access` | `read`, `write`, `readwrite`, `access` | `write` |
| `influence` | `+`, `-`, `++`, `--`, or a signed integer | none |
| `association` | `directed` | undirected |

For example: `api -access(read)-> order "looks up"` or `goal -influence(++)-> outcome`.

## Placement and routing hints

An element, group, or note declaration accepts one placement hint:

| Hint | Effect |
| --- | --- |
| `above <peer> [+n]` | Place above a peer. |
| `below <peer> [+n]` | Place below a peer. |
| `left-of <peer> [+n]` | Place to the left of a peer. |
| `right-of <peer> [+n]` | Place to the right of a peer. |
| `NW`, `NE`, `SE`, or `SW` `<peer> [+rows [+columns]]` | Place in a quadrant relative to a peer; row and column gaps are independent. |
| `at <row> <column>` | Pin to signed logical-cell coordinates; supported for elements and notes. |
| `push <N|S|E|W>` | Push outward in a compass direction. |

Relative hints require a peer in the same group. The optional `+n` adds whole logical cells beyond the normal gap. Placement requests are layout constraints, not permission for overlap. A recoverable invalid or unhonoured hint produces a warning; the diagram can still render without that hint.

Append an optional routing-hint list to a relationship or plain connection:

```text
[leave E@0, enter W@-2, travel E]
```

`leave`, `enter`, and `travel` each take `N`, `S`, `E`, or `W`. `leave` and `enter` may add an optional signed connection-point step with `@n`; `travel` does not accept a point. Duplicate or malformed hint parts are reported and deactivated rather than silently reinterpreted.

## Styling

Enterprise supports Mermaid-style element class and style statements:

```text
application-component api "API":::highlight
classDef highlight fill:#e8f2ff,stroke:#246
class anotherApi highlight
style api color:#123,stroke-width:2px
```

`classDef` defines comma-separated property/value pairs and can define multiple class names. Escape a comma inside a property value as `\,`. `class` applies a defined class; `style` adds properties directly to declared elements. The inline `:::className` shorthand attaches a class to a declaration. Unknown classes are ignored; applying `class` or `style` to an undeclared element produces a warning. Styling changes appearance, not an element's semantic type, outline shape, or relationship end mark.

`linkStyle` styles relationships by their zero-based declaration index, or all relationships with `default`:

```text
linkStyle 0 stroke:#c00,stroke-width:2px
linkStyle default interpolate basis stroke:#666
```

An index without a corresponding relationship is an error. `interpolate <curve>` is accepted as a Mermaid-style link option.

## Title, accessibility, and rendering configuration

The `title <text>` statement sets the diagram title. YAML frontmatter may also provide a `title`.

Accessibility text is written as:

```text
accTitle: Online ordering
accDescr: Shows how an order is fulfilled.
accDescr {
  A customer starts an order.
  The application exposes the ordering service.
}
```

The accessible title and description are metadata, not visible diagram nodes.

Enterprise diagrams support `config.layout: grid` (default) or `config.layout: open`. The command-line `--layout grid|open` sets a default; explicit diagram configuration takes precedence. Grid mode uses shared-grid placement; open mode skips that placement step. Both retain native routing and overlap checks.

`config.legend` can be `off` (default), `on` (bottom-right), `bottom-right`, `bottom-left`, `top-right`, `top-left`, `below`, `right`, or `table`; a placement hint such as `right-of api` is also accepted. The CLI `--legend` sets a default that diagram configuration can override. Image legends are placed outside the main diagram; table mode emits a Symbol/Type legend table.

## Rendering and scope

Enterprise diagrams use the native shared-grid layout, routing, and SVG rendering path for DOCX output. The Enterprise parser also underlies the native EIP path. Mermaid's `architecture-beta` is a separate diagram kind. Other Mermaid diagram kinds may be recognized without having a native Enterprise renderer.

See the top-level [README](../README.md) for the current renderer table and [`currentstatus.md`](currentstatus.md) for project status and recognition gates.

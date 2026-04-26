# MarkdownToDocx Converter – Functional & Technical Specification (v0.1)

## 1. Purpose & Scope

This specification defines a standalone Markdown → DOCX conversion engine and its primary hosting patterns.

The **primary use-case** is:

> **Markdown text in → DOCX as bytes/stream out**

Typical examples:

* A web/API endpoint that accepts Markdown in the request body and returns a `.docx` download.
* A Blazor or web utility where the user pastes or uploads Markdown and receives a Word document.

Secondary use-cases (provided as thin wrappers over the same core engine):

* CLI/console utility (`md2docx input.md output.docx`).
* Batch conversion or background services that process Markdown from files, queues or databases.

The converter must:

* Respect Markdown standards (CommonMark, with optional GitHub Flavoured extensions).
* Produce standards-compliant DOCX (Office Open XML).
* Use Word templates/styles for visual formatting, keeping the core focused on document structure.

---

## 2. Design Overview

### 2.1 High-Level Flow

1. Caller obtains Markdown as a `string`.
2. Caller invokes the core converter:

   * `ConvertText(string Markdown, string BasePath)` → `byte[]` (DOCX bytes), or
   * `ConvertTextToStream(string Markdown, string BasePath, Stream OutputStream)`.
3. Converter:

   * Parses Markdown into an AST (using a CommonMark-compatible parser such as Markdig with configured extensions).
   * Maps the AST to an internal document model (blocks and inlines).
   * Creates a new DOCX in a target `Stream` using Open XML SDK and a configured template/styles.
4. Caller decides what to do with the resulting bytes/stream (return from API, save to disk, upload to storage, etc.).

### 2.2 Primary vs Secondary Use-Cases

* **Primary:**

  * Input: Markdown `string` (plus optional `BasePath` for images).
  * Output: DOCX `byte[]` or `Stream`.

* **Secondary (wrappers only):**

  * File-based CLI: reads `.md` files and writes `.docx` files, internally calling the core `ConvertText` method.
  * Batch/service: same as primary, but integrated into background workers and pipeline jobs.

The core engine is **string/stream-first**. File paths are optional conveniences implemented in wrapper layers.

---

## 3. Standards Support

### 3.1 Markdown Support

The converter must support:

* **Baseline:** CommonMark syntax (headings, paragraphs, emphasis, lists, links, images, code, blockquotes, horizontal rules).
* **Optional extensions:**

  * GitHub-style pipe tables.
  * Fenced code blocks (with language hints).
  * Task lists.
  * Automatic links (URLs, emails).
  * Footnotes (optional).

The exact feature set is configurable via `MarkdownOptions` and/or a named profile.

### 3.2 DOCX / Office Open XML

The converter must:

* Produce valid `.docx` files compliant with the Office Open XML packaging model (ECMA/ISO 29500).
* Use standard WordprocessingML structures for:

  * Paragraphs, runs, tables, images, hyperlinks, bookmarks.
* Avoid non-standard or macro-enabled formats (no `.docm` or add-ins).

### 3.3 Styling & Templates

Visual styling is controlled by Word templates and styles, not hard-coded in the converter.

* Use an existing `.dotx` or `.docx` template as the base document.
* Define a mapping from semantic Markdown elements to Word style names (e.g., `MdHeading1`, `MdCodeBlock`, `MdTable`).
* Optionally support use of built-in Word styles (e.g., `Heading 1`, `Normal`) via configuration.

---

## 4. Public API Specification

### 4.1 Options Classes

#### 4.1.1 MarkdownOptions

```csharp
public sealed class MarkdownOptions
{
    public string Profile { get; set; }

    public bool EnableTables { get; set; }

    public bool EnableTaskLists { get; set; }

    public bool EnableAutoLinks { get; set; }

    public bool EnableFootnotes { get; set; }

    public bool EnableInlineHtml { get; set; }
}
```

* `Profile` is a logical name, e.g. `"CommonMark"`, `"GitHub"`, `"Extended"`.
* Flags select which extensions to enable.

#### 4.1.2 StyleMapping

```csharp
public sealed class StyleMapping
{
    public string Heading1Style { get; set; }

    public string Heading2Style { get; set; }

    public string Heading3Style { get; set; }

    public string Heading4Style { get; set; }

    public string Heading5Style { get; set; }

    public string Heading6Style { get; set; }

    public string ParagraphStyle { get; set; }

    public string CodeBlockStyle { get; set; }

    public string InlineCodeStyle { get; set; }

    public string QuoteStyle { get; set; }

    public string TableStyle { get; set; }

    public string MissingImageStyle { get; set; }
}
```

#### 4.1.3 DocxOptions

```csharp
public sealed class DocxOptions
{
    public bool UseBuiltinTemplate { get; set; }

    public string TemplatePath { get; set; }

    public StyleMapping Styles { get; set; }
}
```

* `UseBuiltinTemplate`: when `true`, use an embedded template shipped with the converter.
* `TemplatePath`: when provided, load template from this path instead.

#### 4.1.4 ImageOptions

```csharp
public sealed class ImageOptions
{
    public double MaxWidthCm { get; set; }

    public bool ScaleDownOnly { get; set; }

    public string MissingImagePlaceholderFormat { get; set; }
}
```

* `MaxWidthCm`: maximum width of images on the page; images wider than this are scaled down, preserving aspect ratio.
* `ScaleDownOnly`: if `true`, images smaller than `MaxWidthCm` are not upscaled.
* `MissingImagePlaceholderFormat`: string format used when an image cannot be resolved, e.g. `"[Missing image: {AltText}]"`.

#### 4.1.5 LinkOptions

```csharp
public sealed class LinkOptions
{
    public bool EnableInternalLinks { get; set; }

    public bool EnableExternalLinks { get; set; }
}
```

* `EnableInternalLinks`: when `true`, `[text](#anchor)` is mapped to internal bookmarks/hyperlinks.
* `EnableExternalLinks`: when `true`, `[text](https://example.com)` is mapped to external hyperlinks.

#### 4.1.6 TableOptions

```csharp
public sealed class TableOptions
{
    public bool EnableAlignment { get; set; }
}
```

* `EnableAlignment`: when `true`, Markdown alignment markers (`:---`, `:---:`, `---:`) are mapped to cell alignment.

#### 4.1.7 MarkdownToDocxOptions

```csharp
public sealed class MarkdownToDocxOptions
{
    public string Profile { get; set; }

    public MarkdownOptions Markdown { get; set; }

    public DocxOptions Docx { get; set; }

    public ImageOptions Images { get; set; }

    public LinkOptions Links { get; set; }

    public TableOptions Tables { get; set; }
}
```

* `Profile`: high-level profile name (`"default"`, `"strict-commonmark"`, `"docsite"`, etc.) which sets baseline values for the other option groups.

#### 4.1.8 Options Resolution

* Sub-option objects (`Markdown`, `Docx`, `Images`, `Links`, `Tables`) may be null. Before conversion, the engine resolves effective options as follows:
  1. If `Profile` is set (non-null, non-empty), apply that profile’s built-in defaults to all option groups.
  2. Overlay any non-null sub-option object and its non-default properties onto those defaults.
* When `Profile` is null or empty, treat as `"default"` for the purpose of step 1.
* This allows callers to pass a minimal options bag (e.g. only `Profile = "strict-commonmark"`) and rely on consistent defaults.
* **Value types and “set” vs “default”:** For reference types, `null` means “not set” and is straightforward. For value types (`bool`, `int`, etc.), the overlay step must distinguish “caller explicitly set this property” from “left at C# default” (e.g. `false` or `0`). Otherwise a profile default of `true` can be incorrectly overwritten when the caller passes a sub-option object with no assignment (default `false`). Implementations should use **nullable wrappers** (e.g. `bool?`, `int?`) or **explicit “HasX” flags** on option types so that only properties that were explicitly set are overlaid onto the profile defaults.

### 4.2 Converter Class

#### 4.2.1 Type Definition

```csharp
public sealed class MarkdownToDocxConverter
{
    private readonly MarkdownToDocxOptions _Options;

    public MarkdownToDocxConverter(MarkdownToDocxOptions Options)
    {
        this._Options = Options;
    }

    public byte[] ConvertText(string Markdown, string BasePath)
    {
        using (MemoryStream OutputStream = new MemoryStream())
        {
            this.ConvertTextToStream(Markdown, BasePath, OutputStream);
            return OutputStream.ToArray();
        }
    }

    public void ConvertTextToStream(string Markdown, string BasePath, Stream OutputStream)
    {
        // Implementation responsibilities (see 5. Internal Behaviour).
    }

    public void ConvertFile(string MarkdownPath, string OutputDocxPath)
    {
        string Markdown = File.ReadAllText(MarkdownPath);
        string BasePath = Path.GetDirectoryName(MarkdownPath) ?? Environment.CurrentDirectory;

        byte[] DocxBytes = this.ConvertText(Markdown, BasePath);
        File.WriteAllBytes(OutputDocxPath, DocxBytes);
    }
}
```

#### 4.2.2 Method Responsibilities

* **ConvertText**

  * Accepts Markdown content as in-memory text and a `BasePath` for resolving relative image paths.
  * When `BasePath` is null or empty, the converter treats it as the current directory (`Environment.CurrentDirectory`) for the purpose of resolving relative image paths only.
  * Writes a DOCX document into a `MemoryStream` by delegating to `ConvertTextToStream`.
  * Returns the resulting `.docx` as a byte array.

* **ConvertTextToStream**

  * Primary implementation method.
  * Resolves effective options (profile + overlay) before conversion.
  * Uses `MarkdownOptions` to configure the Markdown parser.
  * Walks the parse tree into an internal document model.
  * Uses `DocxOptions`, `ImageOptions`, `LinkOptions`, and `TableOptions` to render the document into a DOCX in `OutputStream`.
  * The converter writes the complete DOCX into `OutputStream`; it may buffer the document in memory before writing. True streaming write (incremental flush of DOCX parts) is not required for v0.1.
  * The caller is responsible for opening, positioning, and closing `OutputStream`; the converter only writes to it.

* **ConvertFile**

  * Secondary convenience method for CLI/batch wrappers.
  * Reads Markdown from the file at `MarkdownPath` using UTF-8 encoding (no BOM). Encoding behaviour may be made configurable in a future version.
  * Sets `BasePath` to the directory of `MarkdownPath` (or current directory if not determinable), then calls `ConvertText` and writes the result to `OutputDocxPath`.
  * File names and download behaviour are the responsibility of the host (e.g. API controller); the core API is byte/stream-only and does not define or return file names.

---

## 5. Internal Behaviour (Informative)

This section describes expected behaviours; exact implementation details may vary.

### 5.1 Markdown Parsing

* Create a Markdown parser pipeline based on `MarkdownOptions` and/or `Profile`.
* Supported elements include:

  * Headings `#` to `######`.
  * Paragraphs and line breaks.
  * Emphasis and strong emphasis.
  * Inline code.
  * Blockquotes.
  * Fenced code blocks with language hints.
  * Ordered and unordered lists (including nesting).
  * GitHub-style pipe tables (if enabled).
  * Links and images.

### 5.2 Internal Document Model

The converter maintains an internal, library-agnostic representation of the document. The Markdown AST is mapped into this model; DOCX is generated by walking this model only. This decouples the parser from the Open XML writer and simplifies testing and future format support.

#### 5.2.1 Concrete C# Types (Reference)

**Root**

* **DocModel** (or **DocumentNode**): root container; exposes a single ordered collection of block-level nodes, e.g. `IReadOnlyList<BlockNode> Blocks`.

**Block nodes (structural)**

* **BlockNode**: base type or interface for all block-level elements.
* **ParagraphBlock**: plain paragraph; contains a sequence of `InlineNode`.
* **HeadingBlock**: heading with level 1–6; contains inlines (e.g. title text).
* **CodeBlock**: preformatted block; has raw content string and optional language identifier.
* **QuoteBlock**: blockquote; contains a sequence of `BlockNode` (nested blocks).
* **ListBlock**: ordered or unordered list; contains list items, each item being a sequence of `BlockNode`.
* **TableBlock**: table; has header row flag and a grid of cells, each cell containing block content.
* **HorizontalRuleBlock**: horizontal rule; no inner content.

**Inline nodes (within paragraphs/headings)**

* **InlineNode**: base type or interface for all inline elements.
* **TextRun**: plain text.
* **EmphasisInline**: emphasised text (e.g. italic); contains nested inlines.
* **StrongInline**: strong text (e.g. bold); contains nested inlines.
* **CodeInline**: inline code span.
* **LinkInline**: hyperlink; has URI and a sequence of `InlineNode` for the link text.
* **ImageInline**: image; has source URI, alt text, and optional title; may be replaced by a placeholder block when the image cannot be resolved.

Implementations may use different naming (e.g. `DocxDocument`, `DocxParagraph`) provided the roles above are covered. The important point is a single, explicit model between parsing and DOCX generation.

### 5.3 DOCX Generation

* Use Open XML SDK to open or create a `WordprocessingDocument` on `OutputStream`.
* If a template is configured:

  * Load the template into memory or clone it into the target stream.
  * Append content to the main document part.
* For headings:

  * Insert paragraphs with appropriate style from `StyleMapping` (e.g., `Heading1Style` for level 1 headings).
  * Optionally generate bookmarks/IDs for internal links.
* For normal paragraphs:

  * Use `ParagraphStyle`.
* For code blocks:

  * Use `CodeBlockStyle`.
  * Preserve indentation and line breaks.
  * Optionally render language label above the block.
* For inline code:

  * Use `InlineCodeStyle`.
* For blockquotes:

  * Use `QuoteStyle`.
* For tables:

  * Create a table using `TableStyle` if configured.
  * Map Markdown alignment markers to cell paragraph alignment when `EnableAlignment` is true.
  * Set the first row as a header row where appropriate.
* For images:

  * Resolve relative paths against `BasePath`.
  * If an image is missing or cannot be loaded, insert placeholder text using `MissingImagePlaceholderFormat`, styled with `MissingImageStyle`.
  * Scale images to respect `MaxWidthCm` and `ScaleDownOnly`.
* For links:

  * External links: insert hyperlinks with the URI and link text.
  * Internal links: where `EnableInternalLinks` is true, resolve `#anchor` references to bookmarks associated with headings or other targets.

### 5.4 Internal Document Model Validation Against Edge Markdown Constructs

The internal document model (§5.2) must represent all supported Markdown constructs, including edge cases, in a single canonical form. The following table validates the model against edge constructs and specifies required behaviour or model extensions.

| Edge construct | Example / description | Internal model handling | Notes |
|----------------|------------------------|-------------------------|--------|
| **Empty document** | Input is `""` or only whitespace | `DocModel` with `Blocks` = empty list or single `ParagraphBlock` with empty `Inlines` | Implementations may choose empty list or one empty paragraph; must be deterministic per implementation. |
| **Blank lines only** | `"\n\n\n"` | One or more `ParagraphBlock` nodes with empty `Inlines` | CommonMark: blank lines separate blocks; consecutive blanks can collapse to one empty paragraph or be preserved as multiple. Spec: preserve at least one paragraph break. |
| **Setext heading (level 1)** | Line of text then `===` on next line | `HeadingBlock` with `Level = 1`, `Inlines` from the text line | No trailing `===` in content. |
| **Setext heading (level 2)** | Line of text then `---` on next line | `HeadingBlock` with `Level = 2` | Must not be confused with thematic break (thematic break is a line of only `-` or `*` or `_`, possibly with spaces). |
| **ATX heading with trailing #** | `## Heading ###` | `HeadingBlock` with `Level = 2`, `Inlines` = "Heading" (trailing `#` and inner space stripped) | Strip closing `#` and one space before them. |
| **Indented code block (4 spaces)** | Four spaces + code lines | `CodeBlock` with `Content` = raw lines, `Language` = null or empty | No language hint in CommonMark indented code. |
| **Fenced code block** | ` ```lang\n...\n``` ` | `CodeBlock` with `Content` = raw content, `Language` = "lang" (or empty if no hint) | Language is optional; empty string if not present. |
| **Nested list (unordered in ordered)** | `1. One\n   - Sub` | `ListBlock` (Ordered=true) with one item; item’s blocks contain a `ListBlock` (Ordered=false) with one item | `ListBlock.Items` = list of block sequences; nesting is recursive. |
| **List item with multiple blocks** | `- Para\n\n  Code block` | One list item = `IReadOnlyList<BlockNode>` containing `ParagraphBlock` then `CodeBlock` | Items are sequences of blocks, not single blocks. |
| **Tight vs loose list** | No blank between items vs blank between items | Same `ListBlock`; no extra block node. Spacing is a rendering concern (DOCX paragraph spacing). | Model does not distinguish tight/loose; writer may use paragraph spacing. |
| **Link in heading** | `# [Link](url)` | `HeadingBlock` with `Inlines` containing `LinkInline` (Uri, Inlines) | No special node; link is inline content. |
| **Image in table cell** | `\| ![](img.png) \|` | `TableBlock.Rows[row][col]` = list of blocks; one block is `ParagraphBlock` with single `ImageInline` | Cells are sequences of blocks; image is inline inside a paragraph. |
| **Task list item (GFM)** | `- [ ] Task` / `- [x] Done` | `ListBlock`; item’s first block is `ParagraphBlock` whose first inline is `TaskCheckboxInline` (see below) with `Checked` true/false, followed by remaining inlines | **Model extension:** add `TaskCheckboxInline { bool Checked }` so mapping is deterministic. If extension disabled, treat as plain paragraph. |
| **Footnote (extension)** | `Text[^1]\n\n[^1]: note` | Body: `ParagraphBlock` with `FootnoteReferenceInline { string ReferenceId }`. Footnotes: separate collection (e.g. `DocModel.Footnotes`) of `FootnoteBlock { string Id, IReadOnlyList<BlockNode> Blocks }` | **Model extension:** `DocModel` gets optional `IReadOnlyList<FootnoteBlock> Footnotes`; inlines get `FootnoteReferenceInline`. If extension disabled, treat ref as plain text. |
| **Thematic break** | `---` or `***` or `___` (line of only 3+ of these, optional spaces) | `HorizontalRuleBlock` | Single block; no content. |
| **Escaped character** | `\*` not emphasis | `TextRun` with `Text = "*"` | Backslash removed; following character is literal. |
| **Hard line break (two spaces + newline)** | `A  \nB` | `ParagraphBlock` with `Inlines`: TextRun "A", `BreakInline`, TextRun "B" | **Model extension:** add `BreakInline` (no properties) for explicit line break. |
| **Hard line break (backslash)** | `A\nB` with `\` at end of line (GFM) | Same as above: `BreakInline` between runs | Same as two-space break. |
| **Empty table cell** | `\|   \|` | `TableBlock` cell = single `ParagraphBlock` with empty `Inlines` | Cell is never null; empty = paragraph with no inlines. |
| **Table with header only** | `\| A \| B \|\n\| --- \| --- \|` | `TableBlock` with `HasHeaderRow = true`, `Rows.Count = 1` (header row only) | Body rows may be empty list; writer still emits table with one row. |
| **Table alignment row** | `\| --- \| :--- \| :---: \| ---: \|` | Stored in `TableBlock` as column alignment metadata (see §5.5). | **Model extension:** `TableBlock` has `IReadOnlyList<ColumnAlignment?> ColumnAlignments` (Left, Center, Right, null = default). |
| **Empty link text** | `[](url)` | `LinkInline` with `Uri` set, `Inlines` = empty list | DOCX: hyperlink with empty display text or use URI as display. |
| **Reference-style link** | `[text][ref]` with `[ref]: url` | Same as direct link: `LinkInline(Uri = resolved URL, Inlines = content of "text")` | Resolution is parse-time; model sees only resolved URI. |
| **Autolink (extension)** | `<https://example.com>` | `LinkInline` with `Uri = "https://example.com"`, `Inlines` = single `TextRun` with same text (or URI as display) | Deterministic: one LinkInline, URI and display from autolink. |
| **Strikethrough (GFM, optional)** | `~~text~~` | **Model extension:** `StrikethroughInline { IReadOnlyList<InlineNode> Inlines }` or fallback to `TextRun` if not supported | If extension disabled, map to TextRun. |
| **Image missing / unloadable** | `![alt](missing.png)` | In document model: keep `ImageInline` with `SourceUri` etc. At **write** time: replace with placeholder `ParagraphBlock` (or inline) per §5.3 / A.6. | Model is unchanged; placeholder insertion is a DOCX-writing step. |
| **Inline HTML (if enabled)** | `<span>x</span>` | Treat as raw pass-through or map to equivalent inlines; if not supported, fall back to `TextRun` with literal text. | v0.1 may treat as literal text (single TextRun) for determinism. |

**Summary of model extensions for full edge coverage:**

* **BreakInline** – explicit line break within a paragraph.
* **TaskCheckboxInline** – `bool Checked`; used as first inline of a task list item paragraph when task lists are enabled.
* **FootnoteReferenceInline** – `string ReferenceId`; links to `DocModel.Footnotes`.
* **FootnoteBlock** – `string Id`, `IReadOnlyList<BlockNode> Blocks`; `DocModel` has optional `IReadOnlyList<FootnoteBlock> Footnotes`.
* **TableBlock.ColumnAlignments** – `IReadOnlyList<ColumnAlignment?>` (Left, Center, Right, null).
* **StrikethroughInline** (optional) – `IReadOnlyList<InlineNode> Inlines` for GFM strikethrough.

Implementations that do not support extensions (e.g. no footnotes) must still map the unsupported construct to a deterministic fallback (e.g. plain text) so that the mapping table remains single-valued.

### 5.5 Deterministic Markdown → Internal Model Mapping Table

The following table defines the **canonical** mapping from Markdown (parser output / AST) to the internal document model. For any given Markdown input and options, the converter must produce exactly one equivalent `DocModel` (and optional `Footnotes`) under this mapping. Order of blocks and inlines is document order (top-to-bottom, left-to-right).

**Block-level mapping**

| Markdown construct | Condition / example | Internal model output |
|--------------------|---------------------|------------------------|
| Thematic break | `---` / `***` / `___` (3+ chars, optional spaces only) | One `HorizontalRuleBlock` in `DocModel.Blocks`. |
| ATX heading | `#` to `######` plus optional trailing `#`, then text | One `HeadingBlock`: `Level` = count of leading `#`, `Inlines` = parsed inlines from rest of line (trailing `#` and adjacent space removed). |
| Setext heading (L1) | Non-blank line followed by line of only `=` (3+) | One `HeadingBlock`: `Level = 1`, `Inlines` from first line. |
| Setext heading (L2) | Non-blank line followed by line of only `-` (3+) | One `HeadingBlock`: `Level = 2`, `Inlines` from first line. |
| Indented code block | Lines starting with 4 spaces (or 1 tab) that are not list/blockquote continuation | One `CodeBlock`: `Content` = concatenation of block lines (with leading 4 spaces/tab stripped per line), `Language` = null or empty. |
| Fenced code block | ` ``` ` or `~~~` plus optional info string, then lines until closing fence | One `CodeBlock`: `Content` = raw content between fences, `Language` = info string trimmed or empty. |
| Blockquote | `>` at start of line(s), optional space after `>` | One `QuoteBlock`: `Blocks` = sequence of blocks parsed from quoted lines (nested blockquotes become nested `QuoteBlock`). |
| Ordered list | `1. ` or similar (digit(s) + `.` or `)`) + space | One `ListBlock`: `Ordered = true`, `Items` = list of item block sequences; each item = blocks until next list item or end of list. Start number may be stored (see below). |
| Unordered list | `- ` / `* ` / `+ ` + space | One `ListBlock`: `Ordered = false`, `Items` = same structure. |
| Task list (GFM) | Same as unordered/ordered but first line of item is `[ ]` or `[x]` | Same `ListBlock`; first block of each item is `ParagraphBlock` whose first inline is `TaskCheckboxInline` (Checked = true iff `[x]`). |
| Pipe table | When extension enabled: header row, separator row, body rows | One `TableBlock`: `HasHeaderRow = true`, `ColumnAlignments` from separator (`:---` = Left, `:---:` = Center, `---:` = Right), `Rows` = list of rows; each row = list of cells; each cell = list of blocks (usually one `ParagraphBlock`). |
| Paragraph | Any non-empty line sequence not matching above | One `ParagraphBlock`: `Inlines` = parsed inlines from the line(s). Consecutive non-blank lines that do not start block syntax form one paragraph (joined with space or soft break per spec). |
| Blank line | Line with no content (or only whitespace) | No direct block; separates blocks. Consecutive blanks: implementation may emit zero or one empty `ParagraphBlock` (must be deterministic). |

**Inline-level mapping (within paragraph/heading content)**

| Markdown construct | Example | Internal model output |
|--------------------|---------|------------------------|
| Plain text | `hello` | One or more `TextRun` nodes (may be split by other inlines). |
| Emphasis | `*text*` or `_text_` | One `EmphasisInline` with `Inlines` = parsed content. |
| Strong | `**text**` or `__text__` | One `StrongInline` with `Inlines` = parsed content. |
| Strong+emphasis | `***text***` | One `StrongInline` containing one `EmphasisInline` (or equivalent nesting per parser). Order is deterministic per implementation. |
| Inline code | `` `code` `` | One `CodeInline` with `Content` = literal code (no nested inlines). |
| Link (direct) | `[text](url)` or `[text](url "title")` | One `LinkInline`: `Uri` = url, `Inlines` = parsed "text". Title may be stored on LinkInline if model extends. |
| Link (reference) | `[text][ref]` | One `LinkInline`: `Uri` = resolved reference URL, `Inlines` = parsed "text". |
| Autolink | `<url>` (if enabled) | One `LinkInline`: `Uri` = url, `Inlines` = e.g. single `TextRun` with url. |
| Image | `![alt](src)` | One `ImageInline`: `SourceUri` = src, `AltText` = alt, `Title` = optional title. |
| Image (reference) | `![alt][ref]` | One `ImageInline` with resolved `SourceUri`. |
| Hard line break | Two spaces + newline, or `\` at EOL | One `BreakInline`. |
| Escaped char | `\*` | One `TextRun` with `Text` = the character after backslash. |
| Footnote ref | `[^id]` (if enabled) | One `FootnoteReferenceInline` with `ReferenceId` = id. |
| Task checkbox | `[ ]` / `[x]` at start of list item line | One `TaskCheckboxInline` with `Checked` = true for `[x]`. |
| Strikethrough | `~~text~~` (if enabled) | One `StrikethroughInline` with `Inlines` = parsed content. |

**Determinism rules**

* **Document order:** `DocModel.Blocks` order = order of blocks in the source. Within each block, inlines and nested blocks follow source order.
* **One construct → one canonical form:** Each row above defines the only allowed representation for that construct.
* **References resolved at parse time:** Reference-style links/images and footnote definitions are resolved when building the model; the model stores only resolved URIs and footnote bodies.
* **List start number:** For ordered lists, the start number (e.g. `3.` for "3. Item") may be stored on `ListBlock` as optional `int? StartNumber` for DOCX numbering; if not stored, writer assumes 1.

### 5.6 DOCX Writing Algorithm (Open XML Granularity)

This section defines how to write a single DOCX to an output stream from the internal document model, at the level of Open XML parts, relationships, and key elements.

#### 5.6.1 Package and Parts Overview

A DOCX is an Office Open XML package (ZIP). The converter produces at least:

| Part | Content type | Purpose |
|------|----------------|--------|
| `[Content_Types].xml` | Part of package | Declares content types for all parts. |
| `_rels/.rels` | Relationship part | Package-level relationships: one to `word/document.xml` (officeDocument), optionally to `word/styles.xml`, `word/numbering.xml`, `word/settings.xml`. |
| `word/document.xml` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml` | Main document part (body, content). |
| `word/_rels/document.xml.rels` | Relationship part | Document part relationships: styles, numbering, media (images), hyperlinks, etc. |
| `word/styles.xml` | `application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml` | Style definitions (from template or minimal set). |
| `word/numbering.xml` | `application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml` | Numbering definitions for ordered/unordered lists (when lists exist). |
| `word/media/imageN.png` (etc.) | `image/png` (or jpeg, etc.) | Image parts; N = deterministic index (1-based). |

Template handling: if a template (`.dotx`/`.docx`) is used, the package is created by copying the template package and then modifying `word/document.xml` body and relationships (and optionally `word/styles.xml`, `word/numbering.xml`). If using built-in template, the converter generates a minimal package with the same part set.

#### 5.6.2 Algorithm Steps (High Level)

1. **Resolve options** (profile + overlay).
2. **Obtain or create package:** Create `WordprocessingDocument` on the output stream (or open template stream and clone to output). Ensure package has main document part and styles part; add numbering part if the model contains any `ListBlock`.
3. **Resolve effective styles** from `DocxOptions.Styles` (StyleMapping).
4. **Allocate relationship IDs:** Maintain deterministic IDs for:
   * Hyperlinks: `rId1`, `rId2`, … (in document.xml.rels, type `http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink`).
   * Images: `rId1`, `rId2`, … (in document.xml.rels, type `http://schemas.openxmlformats.org/officeDocument/2006/relationships/image`; target `media/image1.png` etc.).
   * Bookmarks: internal anchors for headings/IDs; see §5.6.7 for the required bookmark ID generation algorithm.
5. **Build bookmark map:** First pass over `DocModel.Blocks`: for each `HeadingBlock`, assign a bookmark ID per §5.6.7 and store in a map (heading index or slug → bookmark ID). Used when writing internal links.
6. **Write body:** Traverse `DocModel.Blocks` in order; for each block, emit the corresponding WordprocessingML (see 5.6.3–5.6.5).
7. **Write footnotes (if any):** If model has `DocModel.Footnotes`, create `word/footnotes.xml` and add a relationship from document to footnotes part; write footnote content per ECMA-376.
8. **Flush and close** the package.

IDs and relationship IDs must be deterministic for the same input (e.g. first hyperlink = rId1, first image = rId1 in document.xml.rels; if both exist, use distinct rIds per type as required by the schema).

#### 5.6.3 Main Document Part (word/document.xml)

* **Root:** `w:document` → `w:body` (and optionally `w:footnotes` reference if footnotes part exists).
* **Body content order:** For each `BlockNode` in `DocModel.Blocks`, append one of:
  * **Paragraph:** `w:p` containing `w:pPr` (optional; style, numbering if list item), then run(s) `w:r` for inlines.
  * **Table:** `w:tbl` with `w:tblPr`, then `w:tr` (rows), each `w:tr` has `w:tc` (cells), each cell contains block content (e.g. `w:p`).
  * **Heading:** Same as paragraph but `w:pPr` references heading style (e.g. `w:pStyle val="Heading1"`). Bookmark `w:bookmarkStart` / `w:bookmarkEnd` around the paragraph if internal links are enabled.
  * **Code block:** Paragraph(s) with `w:pPr` (code style); use `w:r` with `w:t` (preserve space) for content, or single paragraph with line breaks as `w:br`.
  * **Blockquote:** One or more paragraphs with quote style; no extra wrapper element (quote is style on each paragraph).
  * **Horizontal rule:** Empty paragraph with bottom border, or a single `w:p` with `w:pPr` border/rule styling.
  * **List:** Each list item is one or more paragraphs with `w:pPr` containing `w:numPr` (`w:ilvl`, `w:numId`) so that numbering part controls bullet/number. Nested lists use deeper `w:ilvl` and possibly different `w:numId`. Paragraph spacing for list items MUST be applied consistently (the writer must not introduce inconsistent double-spacing between tight and loose lists).

**Runs (w:r):** Each run can contain: `w:rPr` (properties: bold, italic, style), `w:t` (text), `w:br` (line break), `w:drawing` (image), `w:hyperlink` (link). Inlines map as follows:

* `TextRun` → `w:t` (escape XML; use `xml:space="preserve"` if leading/trailing space significant).
* `EmphasisInline` → `w:r` with italic `w:rPr`.
* `StrongInline` → `w:r` with bold `w:rPr`.
* `StrongInline` + `EmphasisInline` → bold + italic.
* `CodeInline` → `w:r` with inline code style and `w:t`.
* `LinkInline` → `w:hyperlink` with `r:id` pointing to relationship (external or internal); child `w:r` for display inlines.
* `ImageInline` → `w:drawing` with `a:blip` `r:embed` to image part; extent from `ImageOptions` (MaxWidthCm, aspect ratio). If image missing, write placeholder paragraph with `MissingImageStyle` and `MissingImagePlaceholderFormat` text.
* `BreakInline` → `w:br`.
* `TaskCheckboxInline` → implementation-defined (e.g. content control, symbol, or plain text `[ ]` / `[x]`).
* `FootnoteReferenceInline` → `w:footnoteReference` with `w:id` matching footnote part.

#### 5.6.4 Numbering Part (word/numbering.xml)

* **When present:** Create this part only if `DocModel.Blocks` (or nested blocks) contain at least one `ListBlock`.
* **Structure:**
  * `w:numbering` contains `w:abstractNum` and `w:num` elements.
  * **Abstract numbering:** Define one `w:abstractNum` for unordered (bullet) and one for ordered (decimal). Use deterministic `w:abstractNumId` (e.g. 0 and 1). For each level (0–8), set `w:lvl` with `w:ilvl`, `w:numFmt` (bullet or decimal), `w:lvlText` (e.g. `•` or `%1.`), `w:start` (1 for ordered).
  * **Num instances:** For each distinct list in the document (or per list style), add a `w:num` with `w:abstractNumId` and `w:numId`. Use consistent `w:numId` per list (e.g. 1, 2, …).
* **Linking paragraphs to numbering:** In body, each list item paragraph has `w:numPr`: `w:ilvl` = nesting level (0-based), `w:numId` = instance id. Restart numbering per top-level list (each new `ListBlock` gets a new `w:num` instance with `w:start` if ordered).

#### 5.6.5 Relationships (word/_rels/document.xml.rels)

* **Styles:** Relationship to `styles.xml` (type styles).
* **Numbering:** Relationship to `numbering.xml` when the part exists (type numbering).
* **Hyperlinks:** One relationship per external link; TargetMode="External", Target = URI. Internal link: relationship type can be same; Target = `#BookmarkId` (fragment to bookmark in same document).
* **Images:** One relationship per image part; type image, Target = `media/image1.png` (or .jpeg etc. by format). Part name must match content type (png → image/png).
* **Footnotes:** When `word/footnotes.xml` exists, add relationship from document to footnotes (type footnotes).

Relationship IDs (`rId1`, `rId2`, …) must be unique within `document.xml.rels`. Generation order **MUST** be: first all hyperlinks in document order, then all images in document order, then styles, numbering, footnotes. This requirement ensures deterministic output for the same model.

#### 5.6.6 Content Types ([Content_Types].xml)

* Default for `xml` extension: `application/xml`.
* Override for `word/document.xml`: main content type.
* Override for `word/styles.xml`: styles content type.
* Override for `word/numbering.xml`: numbering content type (if part exists).
* Override for `word/footnotes.xml`: footnotes content type (if part exists).
* Override for `word/media/*`: each image extension (png, jpeg, gif, etc.) with corresponding `image/*` content type.

Implementations must ensure the package validates against the Office Open XML schema (ECMA-376 Part 2).

#### 5.6.7 Bookmark ID Generation

Implementations **MUST** use one of the following strategies for generating bookmark IDs for headings (and any other bookmark targets). The chosen strategy MUST be applied consistently so that the same document always yields the same bookmark IDs.

**Strategy A – Deterministic numeric IDs only**

* For each `HeadingBlock` in document order, assign bookmark ID `_Toc1`, `_Toc2`, `_Toc3`, … (or equivalent with a fixed prefix and incrementing integer).
* No use of heading text. Collisions are impossible; internal links resolve by position.

**Strategy B – Slug derived from heading text, then deduplicate**

* For each heading, derive a slug from the heading’s plain text (e.g. lowercase, replace non-alphanumeric with `-`, trim/collapse hyphens). Slugification rules (e.g. Unicode normalisation, character set) MUST be fixed and documented.
* If the slug has already been used, append `-1`, `-2`, … until unique.
* Use the resulting string as the bookmark ID (optionally with a prefix such as `_Toc` for validity).

Implementations MUST document which strategy is used. Two implementations that both choose Strategy A will produce the same bookmark IDs for the same document; two that both choose Strategy B will match only if slugification and deduplication rules are identical.

---

## 6. Hosting Patterns

### 6.1 Web/API Hosting (Primary Scenario)

Typical usage in ASP.NET:

* Configure `MarkdownToDocxOptions` (e.g. via `appsettings.json`).
* Register a singleton `MarkdownToDocxConverter` using dependency injection.
* Expose an endpoint that:

  * Accepts Markdown text and optional `BasePath`.
  * Calls `ConvertText` on the converter.
  * Returns the resulting DOCX bytes as a file download.

The core API does not define or return file names; the host is responsible for the HTTP response filename (e.g. via `Content-Disposition`) and any safe naming rules.

Example controller pattern:

```csharp
public sealed class ConvertRequest
{
    public string Markdown { get; set; }

    public string BasePath { get; set; }

    public string FileName { get; set; }
}

[ApiController]
[Route("api/markdown")]
public sealed class MarkdownController : ControllerBase
{
    private readonly MarkdownToDocxConverter _Converter;

    public MarkdownController(MarkdownToDocxConverter Converter)
    {
        this._Converter = Converter;
    }

    [HttpPost("convert-to-docx")]
    public IActionResult ConvertToDocx([FromBody] ConvertRequest Request)
    {
        byte[] DocxBytes = this._Converter.ConvertText(
            Request.Markdown,
            string.IsNullOrWhiteSpace(Request.BasePath)
                ? Environment.CurrentDirectory
                : Request.BasePath);

        string FileName = string.IsNullOrWhiteSpace(Request.FileName)
            ? "Document.docx"
            : Request.FileName;

        return this.File(
            DocxBytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileName);
    }
}
```

### 6.2 CLI Wrapper (Secondary Scenario)

A CLI front-end is implemented as a separate project referencing the core library.

Responsibilities of the CLI:

* Parse command-line arguments.
* Construct or load `MarkdownToDocxOptions` (using profiles and optional config files).
* Instantiate `MarkdownToDocxConverter`.
* Call `ConvertFile` (or `ConvertText` with stdin) and handle exit codes.

Example usage:

* `md2docx input.md output.docx`
* `md2docx input.md output.docx --profile strict-commonmark`

### 6.3 Batch / Background Services

Background workers or scheduled jobs can:

* Retrieve Markdown from files, queues or a database.
* Call `ConvertText` to get DOCX bytes.
* Save the DOCX to storage or send it downstream.

---

## 7. Configuration & Profiles

### 7.1 Profile Resolution

* When `Profile` on `MarkdownToDocxOptions` is set to a known name, it supplies **built-in default values** for all option groups (`Markdown`, `Docx`, `Images`, `Links`, `Tables`).
* Explicit properties on `MarkdownToDocxOptions` and its sub-option objects **override** profile defaults (overlay applied after profile defaults).
* When `Profile` is null or empty, it is treated as `"default"`.

### 7.2 Named Profile Defaults

The following table defines the built-in defaults for each named profile. Implementations must use these (or equivalent) values when applying a profile; overlay rules in §7.1 still apply.

| Option | `default` | `strict-commonmark` | `docsite` |
|--------|-----------|---------------------|-----------|
| **Markdown.Profile** | `"Extended"` | `"CommonMark"` | `"Extended"` |
| **Markdown.EnableTables** | `true` | `false` | `true` |
| **Markdown.EnableTaskLists** | `true` | `false` | `true` |
| **Markdown.EnableAutoLinks** | `true` | `false` | `true` |
| **Markdown.EnableFootnotes** | `false` | `false` | `true` |
| **Markdown.EnableInlineHtml** | `false` | `false` | `false` |
| **Docx.UseBuiltinTemplate** | `true` | `true` | `true` |
| **Docx.TemplatePath** | null | null | null |
| **Docx.Styles** | Built-in `Md*` style names (e.g. `MdHeading1`, `MdCodeBlock`, `MdTable`) | Optional built-in Word names (e.g. `Heading 1`, `Normal`) | Same as default; full internal linking and table styling |
| **Images.MaxWidthCm** | e.g. `16.0` | e.g. `16.0` | e.g. `16.0` |
| **Images.ScaleDownOnly** | `true` | `true` | `true` |
| **Images.MissingImagePlaceholderFormat** | `"[Missing image: {AltText}]"` | Same | Same |
| **Links.EnableInternalLinks** | `true` | `true` | `true` |
| **Links.EnableExternalLinks** | `true` | `true` | `true` |
| **Tables.EnableAlignment** | `true` | N/A (tables off) | `true` |

**Profile intent (summary):**

* **default**: Balanced, extended Markdown (tables, task lists, auto links), built-in template and `Md*` style mapping, image scaling and link support.
* **strict-commonmark**: CommonMark-only, no extensions; optionally map to built-in Word styles for minimal, standards-strict output.
* **docsite**: Extended Markdown suited to documentation sites (tables, task lists, footnotes); same DOCX behaviour as default with full internal linking and table styling.

### 7.3 Configuration Source

In a web application, `MarkdownToDocxOptions` can be bound from `appsettings.json` and optionally overridden by environment variables or other configuration providers.

For the CLI, an external JSON configuration file (e.g. `md2docx.config.json`) can be supported, with precedence rules:

1. CLI arguments (highest precedence).
2. Config file values.
3. Built-in profile defaults (lowest).

---

## 8. Non-Goals (v0.1)

The following are explicitly out of scope for the initial version:

* **Asynchronous API:** Asynchronous overloads (e.g. `ConvertTextAsync`) are not required for v0.1; they may be considered in a future version.
* Syntax-highlighted code blocks with language-specific colouring.
* Complex Word features such as:

  * Fields, cross-references, index generation.
  * Track changes.
  * Macro-enabled documents (`.docm`).
* Full round-tripping (DOCX → Markdown).

These features may be considered for future versions once the core conversion engine is stable.

---

## 9. Quality & Correctness Considerations

* **Correctness focus:**

  * The converter should prioritise structural fidelity (headings, lists, tables, code blocks, links, images) over pixel-perfect visual layout.
  * Styling differences should be controlled by the chosen Word template.

* **Error handling – API contract:**

  * **Critical failures** (converter must throw, no partial DOCX returned): template file not found or unreadable when a custom template is configured; output stream not writable; invalid or missing options after resolution that make conversion impossible. Prefer a dedicated exception type or `InvalidOperationException` with a clear message.
  * **Non-critical failures** (converter must not throw; degrade gracefully): missing image → insert placeholder per `MissingImagePlaceholderFormat`, optionally log; invalid or unsupported Markdown construct → fall back to plain text where reasonable. The document should be produced in full with placeholders or fallback content.

* **Extensibility:**

  * The internal document model and options classes should be designed to accommodate additional features without breaking existing consumers.

This specification provides the baseline required to implement a robust Markdown → DOCX converter that treats **text in, file/stream out** as the primary use-case and supports CLI/batch scenarios as thin wrappers over the same core engine.

---

## Addendum: Clarifications, Defaults & C# Examples

This addendum fills specification gaps, defines default values and resolution behaviour, and provides C# examples that follow the project coding standards (PascalCase for types, methods, properties, and variables; `_PascalCase` for fields; `UPPER_CASE` for constants; braces always; block-body properties where appropriate; avoid `var` except for LINQ/anonymous types).

### A.1 Default Values for Options

Before profile resolution, the following defaults apply when a sub-option object or property is null or unspecified.

**MarkdownOptions**

| Property | Default |
|----------|---------|
| `Profile` | `null` (engine treats as `"default"` when resolving) |
| `EnableTables` | `true` |
| `EnableTaskLists` | `true` |
| `EnableAutoLinks` | `true` |
| `EnableFootnotes` | `false` |
| `EnableInlineHtml` | `false` |

**DocxOptions**

| Property | Default |
|----------|---------|
| `UseBuiltinTemplate` | `true` |
| `TemplatePath` | `null` |
| `Styles` | A `StyleMapping` with built-in `Md*` style names (see below) |

**StyleMapping (built-in defaults)**

| Property | Default |
|----------|---------|
| `Heading1Style` … `Heading6Style` | `"MdHeading1"` … `"MdHeading6"` |
| `ParagraphStyle` | `"MdParagraph"` |
| `CodeBlockStyle` | `"MdCodeBlock"` |
| `InlineCodeStyle` | `"MdInlineCode"` |
| `QuoteStyle` | `"MdQuote"` |
| `TableStyle` | `"MdTable"` |
| `MissingImageStyle` | `"MdMissingImage"` |

**ImageOptions**

| Property | Default |
|----------|---------|
| `MaxWidthCm` | `16.0` |
| `ScaleDownOnly` | `true` |
| `MissingImagePlaceholderFormat` | `"[Missing image: {AltText}]"` |

**LinkOptions**

| Property | Default |
|----------|---------|
| `EnableInternalLinks` | `true` |
| `EnableExternalLinks` | `true` |

**TableOptions**

| Property | Default |
|----------|---------|
| `EnableAlignment` | `true` |

### A.2 Options Resolution (C# Example)

Effective options are computed by applying the named profile first, then overlaying non-null sub-options. The following example illustrates the pattern; the actual implementation may live in a dedicated `OptionsResolver` or inside the converter.

```csharp
public sealed class EffectiveOptions
{
    public MarkdownOptions Markdown { get; set; }

    public DocxOptions Docx { get; set; }

    public ImageOptions Images { get; set; }

    public LinkOptions Links { get; set; }

    public TableOptions Tables { get; set; }
}

public static class OptionsResolver
{
    private const string DEFAULT_PROFILE = "default";

    public static EffectiveOptions Resolve(MarkdownToDocxOptions Options)
    {
        string Profile = string.IsNullOrWhiteSpace(Options?.Profile)
            ? DEFAULT_PROFILE
            : Options.Profile.Trim();

        EffectiveOptions Effective = ApplyProfileDefaults(Profile);

        if (Options?.Markdown != null)
        {
            OverlayMarkdown(Effective.Markdown, Options.Markdown);
        }

        if (Options?.Docx != null)
        {
            OverlayDocx(Effective.Docx, Options.Docx);
        }

        if (Options?.Images != null)
        {
            OverlayImages(Effective.Images, Options.Images);
        }

        if (Options?.Links != null)
        {
            OverlayLinks(Effective.Links, Options.Links);
        }

        if (Options?.Tables != null)
        {
            OverlayTables(Effective.Tables, Options.Tables);
        }

        return Effective;
    }

    private static EffectiveOptions ApplyProfileDefaults(string Profile)
    {
        EffectiveOptions Effective = new EffectiveOptions
        {
            Markdown = new MarkdownOptions(),
            Docx = new DocxOptions(),
            Images = new ImageOptions(),
            Links = new LinkOptions(),
            Tables = new TableOptions()
        };

        switch (Profile.ToLowerInvariant())
        {
            case "strict-commonmark":
                Effective.Markdown.Profile = "CommonMark";
                Effective.Markdown.EnableTables = false;
                Effective.Markdown.EnableTaskLists = false;
                Effective.Markdown.EnableAutoLinks = false;
                break;
            case "docsite":
                Effective.Markdown.Profile = "Extended";
                Effective.Markdown.EnableFootnotes = true;
                break;
            default:
                Effective.Markdown.Profile = "Extended";
                Effective.Markdown.EnableTables = true;
                Effective.Markdown.EnableTaskLists = true;
                Effective.Markdown.EnableAutoLinks = true;
                break;
        }

        return Effective;
    }

    private static void OverlayMarkdown(MarkdownOptions Target, MarkdownOptions Source)
    {
        if (Source.Profile != null)
        {
            Target.Profile = Source.Profile;
        }

        Target.EnableTables = Source.EnableTables;
        Target.EnableTaskLists = Source.EnableTaskLists;
        Target.EnableAutoLinks = Source.EnableAutoLinks;
        Target.EnableFootnotes = Source.EnableFootnotes;
        Target.EnableInlineHtml = Source.EnableInlineHtml;
    }

    private static void OverlayDocx(DocxOptions Target, DocxOptions Source)
    {
        Target.UseBuiltinTemplate = Source.UseBuiltinTemplate;
        Target.TemplatePath = Source.TemplatePath ?? Target.TemplatePath;
        Target.Styles = Source.Styles ?? Target.Styles;
    }

    private static void OverlayImages(ImageOptions Target, ImageOptions Source)
    {
        Target.MaxWidthCm = Source.MaxWidthCm;
        Target.ScaleDownOnly = Source.ScaleDownOnly;
        Target.MissingImagePlaceholderFormat = Source.MissingImagePlaceholderFormat ?? Target.MissingImagePlaceholderFormat;
    }

    private static void OverlayLinks(LinkOptions Target, LinkOptions Source)
    {
        Target.EnableInternalLinks = Source.EnableInternalLinks;
        Target.EnableExternalLinks = Source.EnableExternalLinks;
    }

    private static void OverlayTables(TableOptions Target, TableOptions Source)
    {
        Target.EnableAlignment = Source.EnableAlignment;
    }
}
```

### A.3 Converter Construction and Usage

**Minimal options (profile only):**

```csharp
MarkdownToDocxOptions Options = new MarkdownToDocxOptions
{
    Profile = "strict-commonmark"
};

MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(Options);

string Markdown = "# Hello\n\nThis is **bold**.";
string BasePath = Environment.CurrentDirectory;

byte[] DocxBytes = Converter.ConvertText(Markdown, BasePath);
```

**Full options with custom template and styles:**

```csharp
StyleMapping Styles = new StyleMapping
{
    Heading1Style = "Heading 1",
    Heading2Style = "Heading 2",
    ParagraphStyle = "Normal",
    CodeBlockStyle = "MdCodeBlock",
    InlineCodeStyle = "MdInlineCode",
    QuoteStyle = "MdQuote",
    TableStyle = "MdTable",
    MissingImageStyle = "MdMissingImage"
};

MarkdownToDocxOptions Options = new MarkdownToDocxOptions
{
    Profile = "default",
    Docx = new DocxOptions
    {
        UseBuiltinTemplate = false,
        TemplatePath = @"C:\Templates\Corporate.dotx",
        Styles = Styles
    },
    Images = new ImageOptions
    {
        MaxWidthCm = 14.0,
        ScaleDownOnly = true,
        MissingImagePlaceholderFormat = "[Image not found: {AltText}]"
    }
};

MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(Options);
```

**Stream-based conversion (e.g. for large output or API):**

```csharp
using (FileStream OutputStream = new FileStream(
    "Output.docx",
    FileMode.Create,
    FileAccess.Write,
    FileShare.None))
{
    Converter.ConvertTextToStream(Markdown, BasePath, OutputStream);
}
```

### A.4 ConvertFile and Encoding

`ConvertFile` must read the Markdown file using UTF-8 without a BOM. Example implementation detail:

```csharp
private static readonly Encoding UTF8_NO_BOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

public void ConvertFile(string MarkdownPath, string OutputDocxPath)
{
    string Markdown = File.ReadAllText(MarkdownPath, UTF8_NO_BOM);
    string BasePath = Path.GetDirectoryName(MarkdownPath) ?? Environment.CurrentDirectory;

    byte[] DocxBytes = this.ConvertText(Markdown, BasePath);
    File.WriteAllBytes(OutputDocxPath, DocxBytes);
}
```

Encoding may be made configurable in a future version; for v0.1, UTF-8 without BOM is fixed.

### A.5 BasePath and Image Path Resolution

`BasePath` is used only to resolve **relative** image URLs in Markdown. When `BasePath` is null or empty, the converter treats it as `Environment.CurrentDirectory`.

* Example: Markdown contains `![Logo](images/logo.png)`. If `BasePath` is `C:\Docs\Project`, the resolved path is `C:\Docs\Project\images\logo.png`.
* Absolute paths (e.g. `file:///C:/Data/logo.png` or `https://example.com/logo.png`) are not combined with `BasePath`.

Example resolution helper (conceptual; actual implementation may differ):

```csharp
public static string ResolveImagePath(string ImageUri, string BasePath)
{
    if (string.IsNullOrWhiteSpace(ImageUri))
    {
        return ImageUri;
    }

    if (Uri.TryCreate(ImageUri, UriKind.Absolute, out Uri AbsoluteUri) && AbsoluteUri.IsAbsoluteUri)
    {
        if (AbsoluteUri.Scheme == "file")
        {
            return AbsoluteUri.LocalPath;
        }

        return ImageUri;
    }

    string Base = string.IsNullOrWhiteSpace(BasePath)
        ? Environment.CurrentDirectory
        : BasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    return Path.GetFullPath(Path.Combine(Base, ImageUri));
}
```

### A.6 Error Handling

**Critical failures** (converter must throw; no partial DOCX):

* Template file not found or not readable when a custom template is configured.
* Output stream not writable.
* Invalid or impossible options after resolution.

Use a dedicated exception type or `InvalidOperationException` with a clear message. Example:

```csharp
public sealed class MarkdownToDocxException : InvalidOperationException
{
    public MarkdownToDocxException(string Message)
        : base(Message)
    {
    }

    public MarkdownToDocxException(string Message, Exception InnerException)
        : base(Message, InnerException)
    {
    }
}

public void ConvertTextToStream(string Markdown, string BasePath, Stream OutputStream)
{
    if (OutputStream == null || !OutputStream.CanWrite)
    {
        throw new MarkdownToDocxException("Output stream must be non-null and writable.");
    }

    EffectiveOptions Effective = OptionsResolver.Resolve(this._Options);

    if (Effective.Docx.UseBuiltinTemplate == false
        && string.IsNullOrWhiteSpace(Effective.Docx.TemplatePath))
    {
        throw new MarkdownToDocxException(
            "A template path is required when UseBuiltinTemplate is false.");
    }

    if (Effective.Docx.UseBuiltinTemplate == false)
    {
        if (!File.Exists(Effective.Docx.TemplatePath))
        {
            throw new MarkdownToDocxException(
                $"Template file not found: {Effective.Docx.TemplatePath}");
        }
    }

    // ... parse, build model, write DOCX ...
}
```

**Non-critical failures** (converter must not throw; degrade gracefully):

* Missing image: insert placeholder text using `MissingImagePlaceholderFormat`, styled with `MissingImageStyle`; optionally log.
* Invalid or unsupported Markdown construct: fall back to plain text where reasonable.

Example placeholder insertion (conceptual):

```csharp
private string FormatMissingImagePlaceholder(string AltText, ImageOptions ImageOptions)
{
    string Format = ImageOptions?.MissingImagePlaceholderFormat ?? "[Missing image: {AltText}]";
    return Format.Replace("{AltText}", AltText ?? "unknown");
}
```

### A.7 Internal Document Model (Reference C# Types)

The following C# types illustrate the internal document model referenced in §5.2. Implementations may use different names or inheritance; the roles (root, block nodes, inline nodes) must be preserved. For edge constructs and extensions (e.g. `BreakInline`, `TaskCheckboxInline`, `FootnoteReferenceInline`, `FootnoteBlock`, `TableBlock.ColumnAlignments`), see §5.4 and §5.5.

```csharp
public sealed class DocModel
{
    public IReadOnlyList<BlockNode> Blocks { get; set; }
}

public abstract class BlockNode
{
}

public sealed class ParagraphBlock : BlockNode
{
    public IReadOnlyList<InlineNode> Inlines { get; set; }
}

public sealed class HeadingBlock : BlockNode
{
    public int Level { get; set; }

    public IReadOnlyList<InlineNode> Inlines { get; set; }
}

public sealed class CodeBlock : BlockNode
{
    public string Content { get; set; }

    public string Language { get; set; }
}

public sealed class QuoteBlock : BlockNode
{
    public IReadOnlyList<BlockNode> Blocks { get; set; }
}

public sealed class ListBlock : BlockNode
{
    public bool Ordered { get; set; }

    public IReadOnlyList<IReadOnlyList<BlockNode>> Items { get; set; }
}

public sealed class TableBlock : BlockNode
{
    public bool HasHeaderRow { get; set; }

    public IReadOnlyList<IReadOnlyList<BlockNode>> Rows { get; set; }
}

public sealed class HorizontalRuleBlock : BlockNode
{
}

public abstract class InlineNode
{
}

public sealed class TextRun : InlineNode
{
    public string Text { get; set; }
}

public sealed class EmphasisInline : InlineNode
{
    public IReadOnlyList<InlineNode> Inlines { get; set; }
}

public sealed class StrongInline : InlineNode
{
    public IReadOnlyList<InlineNode> Inlines { get; set; }
}

public sealed class CodeInline : InlineNode
{
    public string Content { get; set; }
}

public sealed class LinkInline : InlineNode
{
    public string Uri { get; set; }

    public IReadOnlyList<InlineNode> Inlines { get; set; }
}

public sealed class ImageInline : InlineNode
{
    public string SourceUri { get; set; }

    public string AltText { get; set; }

    public string Title { get; set; }
}
```

### A.8 Dependency Injection and Configuration (ASP.NET Core)

**appsettings.json** (example):

```json
{
  "MarkdownToDocx": {
    "Profile": "default",
    "Markdown": {
      "EnableTables": true,
      "EnableTaskLists": true,
      "EnableFootnotes": false
    },
    "Docx": {
      "UseBuiltinTemplate": true,
      "TemplatePath": null
    },
    "Images": {
      "MaxWidthCm": 16.0,
      "ScaleDownOnly": true,
      "MissingImagePlaceholderFormat": "[Missing image: {AltText}]"
    },
    "Links": {
      "EnableInternalLinks": true,
      "EnableExternalLinks": true
    },
    "Tables": {
      "EnableAlignment": true
    }
  }
}
```

**Program.cs (or Startup) registration:**

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// In Program.cs or equivalent:

IConfiguration Configuration = builder.Configuration;

builder.Services.Configure<MarkdownToDocxOptions>(
    Configuration.GetSection("MarkdownToDocx"));

builder.Services.AddSingleton<MarkdownToDocxConverter>(ServiceProvider =>
{
    IOptions<MarkdownToDocxOptions> OptionsAccessor = ServiceProvider
        .GetRequiredService<IOptions<MarkdownToDocxOptions>>();
    return new MarkdownToDocxConverter(OptionsAccessor.Value);
});
```

**Controller with injected converter** (reiterating §6.1 with consistent naming):

```csharp
[ApiController]
[Route("api/markdown")]
public sealed class MarkdownController : ControllerBase
{
    private readonly MarkdownToDocxConverter _Converter;

    public MarkdownController(MarkdownToDocxConverter Converter)
    {
        this._Converter = Converter;
    }

    [HttpPost("convert-to-docx")]
    public IActionResult ConvertToDocx([FromBody] ConvertRequest Request)
    {
        string BasePath = string.IsNullOrWhiteSpace(Request.BasePath)
            ? Environment.CurrentDirectory
            : Request.BasePath;

        byte[] DocxBytes = this._Converter.ConvertText(Request.Markdown ?? string.Empty, BasePath);

        string FileName = string.IsNullOrWhiteSpace(Request.FileName)
            ? "Document.docx"
            : Request.FileName;

        return this.File(
            DocxBytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileName);
    }
}
```

### A.9 CLI Wrapper Example

A minimal CLI that parses arguments and calls the core converter:

```csharp
public static class Program
{
    private const string DEFAULT_PROFILE = "default";

    public static int Main(string[] Args)
    {
        if (Args.Length < 2)
        {
            Console.Error.WriteLine("Usage: md2docx <input.md> <output.docx> [--profile <name>]");
            return 1;
        }

        string MarkdownPath = Args[0];
        string OutputDocxPath = Args[1];
        string Profile = DEFAULT_PROFILE;

        for (int I = 2; I < Args.Length; I++)
        {
            if (Args[I] == "--profile" && I + 1 < Args.Length)
            {
                Profile = Args[I + 1];
                I++;
            }
        }

        if (!File.Exists(MarkdownPath))
        {
            Console.Error.WriteLine($"Input file not found: {MarkdownPath}");
            return 1;
        }

        MarkdownToDocxOptions Options = new MarkdownToDocxOptions
        {
            Profile = Profile
        };

        MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(Options);

        try
        {
            Converter.ConvertFile(MarkdownPath, OutputDocxPath);
            return 0;
        }
        catch (MarkdownToDocxException Ex)
        {
            Console.Error.WriteLine(Ex.Message);
            return 1;
        }
    }
}
```

Usage: `md2docx input.md output.docx --profile strict-commonmark`

### A.10 Batch / Background Service Example

A background worker that reads Markdown from a queue, converts to DOCX, and saves to storage:

```csharp
public sealed class MarkdownToDocxBackgroundService : BackgroundService
{
    private readonly MarkdownToDocxConverter _Converter;
    private readonly ILogger<MarkdownToDocxBackgroundService> _Logger;

    public MarkdownToDocxBackgroundService(
        MarkdownToDocxConverter Converter,
        ILogger<MarkdownToDocxBackgroundService> Logger)
    {
        this._Converter = Converter;
        this._Logger = Logger;
    }

    protected override async Task ExecuteAsync(CancellationToken StoppingToken)
    {
        while (!StoppingToken.IsCancellationRequested)
        {
            ConversionJob Job = await GetNextJobAsync(StoppingToken);
            if (Job == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), StoppingToken);
                continue;
            }

            try
            {
                byte[] DocxBytes = this._Converter.ConvertText(Job.Markdown, Job.BasePath ?? string.Empty);
                await SaveDocxAsync(Job.OutputKey, DocxBytes, StoppingToken);
                this._Logger.LogInformation("Converted job {JobId} to DOCX.", Job.JobId);
            }
            catch (MarkdownToDocxException Ex)
            {
                this._Logger.LogError(Ex, "Conversion failed for job {JobId}.", Job.JobId);
            }
        }
    }

    private static Task<ConversionJob> GetNextJobAsync(CancellationToken CancellationToken)
    {
        return Task.FromResult<ConversionJob>(null);
    }

    private static Task SaveDocxAsync(string OutputKey, byte[] DocxBytes, CancellationToken CancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class ConversionJob
{
    public string JobId { get; set; }

    public string Markdown { get; set; }

    public string BasePath { get; set; }

    public string OutputKey { get; set; }
}
```

### A.11 MissingImagePlaceholderFormat Format Keys

The placeholder format string may use the following keys (implementation-dependent; at least `{AltText}` is required by the spec):

| Key | Description |
|-----|-------------|
| `{AltText}` | Alt text of the image in Markdown. |
| `{Uri}` | Original image URI/source. |

Example: `MissingImagePlaceholderFormat = "[Image missing: {AltText} ({Uri})]"` may produce `[Image missing: Company logo (images/logo.png)]`.

---

## Appendix B: Specification Maturity and Revision Note

This specification has evolved from a design sketch to a **production-grade technical specification**. It is:

* **Deterministic** – Single canonical mapping from Markdown to internal model and from model to DOCX; bookmark and relationship ID rules are explicit.
* **Layered** – Clear separation: parser → internal model → DOCX writer; options resolution; hosting patterns as thin wrappers.
* **Explicit** – Edge Markdown constructs (§5.4), mapping tables (§5.5), and Open XML algorithm (§5.6) are spelled out.
* **Testable** – Mapping tables and model extensions support unit tests and conformance tests (input → expected DocModel or DOCX).
* **Extensible** – Internal model and options accommodate additional features without breaking existing consumers.
* **Standards-aligned** – CommonMark/GFM and Office Open XML (ECMA/ISO 29500) are respected.
* **Implementation-ready** – An implementer can build the converter from this document without ambiguity on determinism, parts, or IDs.
* **Implementation-verifiable** – Appendix C defines a formal conformance test matrix (Input Markdown → Expected DocModel JSON → Expected DOCX structural assertions) derived from §5.4 and §5.5.

**Suggested next steps (beyond v0.1):**

* Define a strict Bookmark ID generation algorithm in code (or adopt Strategy A/B as the single normative strategy).
* Define unit test categories derived from §5.4 (edge constructs) and §5.5 (mapping table).
* Extend or refine the conformance test matrix in Appendix C (e.g. more edge cases, profile-specific rows).
* Define memory/performance boundaries (max document size, streaming threshold).
* Define thread-safety and instance lifetime rules for `MarkdownToDocxConverter`.

---

## Appendix C: Formal Conformance Test Matrix

This appendix defines a **formal conformance test matrix** derived directly from §5.4 (edge Markdown constructs) and §5.5 (deterministic mapping). Each test case specifies: **Input Markdown** → **Expected canonical DocModel JSON** → **Expected structural DOCX assertions**. Implementations that pass the matrix are **implementation-verifiable** against this specification.

### C.1 Canonical DocModel JSON Schema

The canonical representation of the internal document model for conformance testing uses the following JSON structure. All type names and property names are normative for the test matrix.

**Canonical JSON property names (single name per concept):** In serialised DocModel JSON, **only one property name** is used per concept. Aliases or in-memory-only names must not appear in the canonical JSON. This keeps conformance expectations unambiguous. In particular:
* **ImageInline:** Use **`SourceUri`** only in JSON (not `Url` or other aliases).
* **ListBlock:** Use **`Ordered`** for ordered vs unordered (not `IsOrdered`); use optional **`StartNumber`** (number, omit when 1) for non-1 list starts. Ordered is explicit; StartNumber is optional as in §5.5.
* No other aliases: the table below defines the only names that appear in expected DocModel JSON.

**Root**

* `DocModel`: `{ "Blocks": [ BlockNode, ... ] }`  
  Optional: `"Footnotes": [ FootnoteBlock, ... ]` when footnotes extension is used.

**Block nodes** (each has `"Type"` and type-specific properties)

| Type | Required properties | Optional / notes |
|------|----------------------|------------------|
| `ParagraphBlock` | `"Inlines": [ InlineNode, ... ]` | |
| `HeadingBlock` | `"Level": 1..6`, `"Inlines": [ InlineNode, ... ]` | |
| `CodeBlock` | `"Content": string` | `"Language": string` (empty or null if none) |
| `QuoteBlock` | `"Blocks": [ BlockNode, ... ]` | |
| `ListBlock` | `"Ordered": boolean`, `"Items": [ [ BlockNode, ... ], ... ]` | `"StartNumber": number` |
| `TableBlock` | `"HasHeaderRow": boolean`, `"Rows": [ [ [ BlockNode, ... ], ... ], ... ]` | `"ColumnAlignments": [ "Left" \| "Center" \| "Right" \| null, ... ]` |
| `HorizontalRuleBlock` | (no properties) | |

**Inline nodes** (each has `"Type"` and type-specific properties)

| Type | Required properties | Optional |
|------|----------------------|----------|
| `TextRun` | `"Text": string` | |
| `EmphasisInline` | `"Inlines": [ InlineNode, ... ]` | |
| `StrongInline` | `"Inlines": [ InlineNode, ... ]` | |
| `CodeInline` | `"Content": string` | |
| `LinkInline` | `"Uri": string`, `"Inlines": [ InlineNode, ... ]` | |
| `ImageInline` | `"SourceUri": string`, `"AltText": string` | `"Title": string` |
| `BreakInline` | (no properties) | |
| `TaskCheckboxInline` | `"Checked": boolean` | |
| `FootnoteReferenceInline` | `"ReferenceId": string` | |
| `StrikethroughInline` | `"Inlines": [ InlineNode, ... ]` | |

**Footnote block** (when extension enabled)

* `FootnoteBlock`: `{ "Type": "FootnoteBlock", "Id": string, "Blocks": [ BlockNode, ... ] }`

**Determinism for tests:** Document order is preserved in all arrays. References (links, images, footnotes) appear in the model with **resolved** URIs/IDs; reference definitions are not represented as separate nodes.

**Encoding and format for DocModel JSON (belt-and-braces):** Expected DocModel JSON in conformance tests uses **UTF-8** encoding; **normalised line endings** (e.g. LF, `\n`, for consistency across platforms); and **standard JSON** quote and escape rules (double-quote for strings, `\` for `"`, `\`, `/`, `\b`, `\f`, `\n`, `\r`, `\t`, `\uXXXX`). Implementers will infer this from context; stating it explicitly avoids ambiguity when comparing or serialising expected output.

**Optional normative JSON Schema:** The schema above is defined textually; that is sufficient for an experienced implementer. If extra formality is desired, a **JSON Schema** (e.g. draft-07 or later) fragment could be added in an appendix, or a standalone **DocModel.schema.json** could be provided out-of-band. Not required for conformance.

### C.2 Conformance Test Matrix

Each row is one conformance test. **Profile** is the `MarkdownToDocxOptions.Profile` (and implied options) used; **default** means profile `"default"` with tables, task lists, and autolinks enabled as per §7.2. **DocModel JSON** is the expected canonical model (abbreviated where obvious). **DOCX assertions** are structural conditions that the emitted DOCX must satisfy; they can be checked via XPath over the package or by parsing `word/document.xml`, `word/_rels/document.xml.rels`, etc.

| Id | Category | Input Markdown | Profile | Expected DocModel JSON (canonical) | Expected DOCX structural assertions |
|----|----------|----------------|--------|-----------------------------------|-------------------------------------|
| **CT-EMPTY** | §5.4 Empty | `""` | default | `{"Blocks":[]}` or `{"Blocks":[{"Type":"ParagraphBlock","Inlines":[]}]}` | Body has 0 or 1 `w:p`; no `w:tbl`. Package has `word/document.xml`, `word/styles.xml`. |
| **CT-BLANK** | §5.4 Blank | `"\n\n"` | default | `{"Blocks":[{"Type":"ParagraphBlock","Inlines":[]},{"Type":"ParagraphBlock","Inlines":[]}]}` or equivalent (at least one empty paragraph) | At least one `w:p` in body; paragraph(s) have no `w:r` or empty run. |
| **CT-THEMATIC** | §5.5 Block | `---` | default | `{"Blocks":[{"Type":"HorizontalRuleBlock"}]}` | Exactly one block in body that represents a horizontal rule (e.g. `w:p` with border/rule styling or equivalent). |
| **CT-ATX** | §5.5 Block | `# H1` | default | `{"Blocks":[{"Type":"HeadingBlock","Level":1,"Inlines":[{"Type":"TextRun","Text":"H1"}]}]}` | Exactly one `w:p` with `w:pPr/w:pStyle` @val = configured Heading1Style (e.g. `MdHeading1`). |
| **CT-ATX-TRAIL** | §5.4 ATX trailing # | `## Heading ###` | default | `{"Blocks":[{"Type":"HeadingBlock","Level":2,"Inlines":[{"Type":"TextRun","Text":"Heading"}]}]}` | One heading paragraph; run text is "Heading" (no trailing #). |
| **CT-SETEXT-H1** | §5.4 Setext L1 | `Title\n===` | default | `{"Blocks":[{"Type":"HeadingBlock","Level":1,"Inlines":[{"Type":"TextRun","Text":"Title"}]}]}` | One `w:p` with heading style for level 1. |
| **CT-SETEXT-H2** | §5.4 Setext L2 | `Sub\n---` | default | `{"Blocks":[{"Type":"HeadingBlock","Level":2,"Inlines":[{"Type":"TextRun","Text":"Sub"}]}]}` | One `w:p` with heading style for level 2. |
| **CT-INDENTED-CODE** | §5.4 Indented code | `    code` (4 spaces) | default | `{"Blocks":[{"Type":"CodeBlock","Content":"code","Language":null}]}` or `"Language":""` | At least one paragraph with CodeBlockStyle; text "code" present; no language run required. |
| **CT-FENCED-CODE** | §5.5 Block | `` ```c\nint x;\n``` `` | default | `{"Blocks":[{"Type":"CodeBlock","Content":"int x;\n","Language":"c"}]}` | Paragraph(s) with CodeBlockStyle; content "int x;" and line break preserved. |
| **CT-PARAGRAPH** | §5.5 Block | `Hello world.` | default | `{"Blocks":[{"Type":"ParagraphBlock","Inlines":[{"Type":"TextRun","Text":"Hello world."}]}]}` | Exactly one `w:p` with single run containing "Hello world.". |
| **CT-ESCAPE** | §5.4 Escape | `\* not emphasis` | default | `{"Blocks":[{"Type":"ParagraphBlock","Inlines":[{"Type":"TextRun","Text":"*"},{"Type":"TextRun","Text":" not emphasis"}]}]}` | One paragraph; first run is "*", no italic. |
| **CT-HARD-BREAK** | §5.4 Hard break | `A  \nB` | default | `{"Blocks":[{"Type":"ParagraphBlock","Inlines":[{"Type":"TextRun","Text":"A"},{"Type":"BreakInline"},{ "Type":"TextRun","Text":"B"}]}]}` | One `w:p` containing `w:r` with "A", then `w:br`, then `w:r` with "B". |
| **CT-EMPHASIS** | §5.5 Inline | `*em*` | default | `{"Blocks":[{"Type":"ParagraphBlock","Inlines":[{"Type":"EmphasisInline","Inlines":[{"Type":"TextRun","Text":"em"}]}]}]}` | One paragraph; one run with italic property. |
| **CT-STRONG** | §5.5 Inline | `**bold**` | default | `{"Blocks":[{"Type":"ParagraphBlock","Inlines":[{"Type":"StrongInline","Inlines":[{"Type":"TextRun","Text":"bold"}]}]}]}` | One paragraph; one run with bold property. |
| **CT-INLINE-CODE** | §5.5 Inline | `` `code` `` | default | `{"Blocks":[{"Type":"ParagraphBlock","Inlines":[{"Type":"CodeInline","Content":"code"}]}]}` | One paragraph; run with inline code style, text "code". |
| **CT-LINK** | §5.5 Inline | `[text](https://example.com)` | default | `{"Blocks":[{"Type":"ParagraphBlock","Inlines":[{"Type":"LinkInline","Uri":"https://example.com","Inlines":[{"Type":"TextRun","Text":"text"}]}]}]}` | One `w:hyperlink` with r:id; relationship Target = "https://example.com"; display "text". |
| **CT-EMPTY-LINK** | §5.4 Empty link | `[](https://u)` | default | `{"Blocks":[{"Type":"ParagraphBlock","Inlines":[{"Type":"LinkInline","Uri":"https://u","Inlines":[]}]}]}` | One hyperlink; empty display or URI as display. |
| **CT-LINK-IN-HEADING** | §5.4 Link in heading | `# [Link](url)` | default | `{"Blocks":[{"Type":"HeadingBlock","Level":1,"Inlines":[{"Type":"LinkInline","Uri":"url","Inlines":[{"Type":"TextRun","Text":"Link"}]}]}]}` | One heading paragraph containing `w:hyperlink` with Target "url". |
| **CT-BLOCKQUOTE** | §5.5 Block | `> Quote` | default | `{"Blocks":[{"Type":"QuoteBlock","Blocks":[{"Type":"ParagraphBlock","Inlines":[{"Type":"TextRun","Text":"Quote"}]}]}]}` | At least one `w:p` with QuoteStyle; text "Quote". |
| **CT-UNORDERED-LIST** | §5.5 Block | `- One` | default | `{"Blocks":[{"Type":"ListBlock","Ordered":false,"Items":[[{"Type":"ParagraphBlock","Inlines":[{"Type":"TextRun","Text":"One"}]}]]}]}` | Body has paragraph with `w:numPr` (ilvl, numId); package has `word/numbering.xml`. |
| **CT-ORDERED-LIST** | §5.5 Block | `1. First` | default | `{"Blocks":[{"Type":"ListBlock","Ordered":true,"Items":[[{"Type":"ParagraphBlock","Inlines":[{"Type":"TextRun","Text":"First"}]}]]}]}` | Same numbering part; `w:numPr` present; number format decimal. |
| **CT-NESTED-LIST** | §5.4 Nested | `1. One\n   - Sub` | default | `{"Blocks":[{"Type":"ListBlock","Ordered":true,"Items":[[{"Type":"ParagraphBlock","Inlines":[{"Type":"TextRun","Text":"One"}]},{"Type":"ListBlock","Ordered":false,"Items":[[{"Type":"ParagraphBlock","Inlines":[{"Type":"TextRun","Text":"Sub"}]}]]}]]}]}` | Two list item paragraphs; second has deeper `w:ilvl`. |
| **CT-LIST-MULTI** | §5.4 Multi-block item | `- P` then blank line then indented `` ``` `` + `c` + `` ``` `` | default | ListBlock, one item; item Blocks = [ ParagraphBlock "P", CodeBlock "c" ] | One numPr paragraph with "P"; following paragraph(s) with code content "c", same list context (ilvl). |
| **CT-TASK-LIST** | §5.4 Task list | `- [ ] Todo` | default | `{"Blocks":[{"Type":"ListBlock","Ordered":false,"Items":[[{"Type":"ParagraphBlock","Inlines":[{"Type":"TaskCheckboxInline","Checked":false},{"Type":"TextRun","Text":" Todo"}]}]]}]}` | List paragraph; first run or content represents unchecked task (e.g. "[ ]" or symbol). |
| **CT-TASK-LIST-X** | §5.4 Task list | `- [x] Done` | default | Same with `TaskCheckboxInline` `"Checked":true` | List paragraph; checked task representation. |
| **CT-TABLE-HEADER-ONLY** | §5.4 Table header only | `\| A \| B \|\n\| --- \| --- \|` | default | `{"Blocks":[{"Type":"TableBlock","HasHeaderRow":true,"Rows":[[...],[...]],"ColumnAlignments":[null,null]}]}` — one row of cells | Exactly one `w:tbl` with one `w:tr`; two `w:tc`; cell content "A", "B". |
| **CT-TABLE-EMPTY-CELL** | §5.4 Empty cell | `\| X \| \|\n\| --- \| --- \|` | default | TableBlock; second cell = ParagraphBlock with Inlines = [] | One table; second cell has empty paragraph. |
| **CT-TABLE-ALIGN** | §5.4 Alignment | `\| L \| C \| R \|\n\| :--- \| :---: \| ---: \|` | default | TableBlock with ColumnAlignments = ["Left","Center","Right"] | Table cells have paragraph alignment left, center, right respectively (when EnableAlignment). |
| **CT-REF-LINK** | §5.5 Inline | `[t][r]\n\n[r]: https://u` | default | `LinkInline` with Uri = "https://u", Inlines = [ TextRun "t" ] | One hyperlink; Target "https://u"; display "t". |
| **CT-AUTOLINK** | §5.4 Autolink | `<https://example.com>` | default | `LinkInline` Uri "https://example.com", Inlines e.g. [ TextRun "https://example.com" ] | One hyperlink; Target and display as URL. (Skip in strict-commonmark.) |
| **CT-IMAGE** | §5.5 Inline | `![alt](img.png)` | default | `{"Blocks":[{"Type":"ParagraphBlock","Inlines":[{"Type":"ImageInline","SourceUri":"img.png","AltText":"alt"}]}]}` | One drawing or image part; relationship to `word/media/image1.png` (or placeholder if missing). |
| **CT-IMAGE-MISSING** | §5.4 Missing image | `![alt](missing.png)` | default | Model unchanged (ImageInline); at write time placeholder | DOCX: placeholder text per MissingImagePlaceholderFormat; MissingImageStyle. No image part for missing file. |
| **CT-BOOKMARK** | §5.6 + internal link | `# H\n[go](#h)` | default, internal links on | HeadingBlock "H"; ParagraphBlock with LinkInline Uri "#_Toc1" (or slug) | `w:bookmarkStart`/`w:bookmarkEnd` around heading; hyperlink Target "#_Toc1" (or slug per §5.6.7). |
| **CT-REL-ID-ORDER** | §5.6.5 | Any doc with link + image | default | (any) | In `document.xml.rels`: first relationship(s) for hyperlinks, then images, then styles, numbering, footnotes (MUST order). |

### C.3 DOCX Structural Assertion Format

Assertions can be implemented as:

1. **XPath over package:** e.g. `//w:body/w:p` count, `//w:tbl`, `//w:hyperlink/@r:id`, `//w:bookmarkStart/@w:name`.
2. **Relationship order:** Parse `word/_rels/document.xml.rels`; verify order of relationship types (hyperlinks first, then images, then styles, numbering, footnotes).
3. **Part presence:** When `DocModel.Blocks` (or nested) contains a `ListBlock`, package MUST contain `word/numbering.xml`. When `DocModel.Footnotes` is non-empty, package MUST contain `word/footnotes.xml`.
4. **Content match:** Text in `w:t` nodes matches expected from DocModel; styles match StyleMapping.

Implementations MAY add more tests (e.g. footnote body, strikethrough run properties) using the same schema and assertion style. A conformant implementation MUST pass all matrix rows whose **Profile** and extensions are supported; rows that depend on a disabled extension (e.g. CT-AUTOLINK with `strict-commonmark`) MAY be skipped or asserted as fallback (e.g. plain text).

### C.4 Test Categories Summary

| Category | Source | Test IDs |
|----------|--------|----------|
| Empty / blank | §5.4 | CT-EMPTY, CT-BLANK |
| Thematic break | §5.4, §5.5 | CT-THEMATIC |
| Headings (ATX, setext, trailing #) | §5.4, §5.5 | CT-ATX, CT-ATX-TRAIL, CT-SETEXT-H1, CT-SETEXT-H2 |
| Code (indented, fenced) | §5.4, §5.5 | CT-INDENTED-CODE, CT-FENCED-CODE |
| Paragraph and inlines | §5.5 | CT-PARAGRAPH, CT-ESCAPE, CT-HARD-BREAK, CT-EMPHASIS, CT-STRONG, CT-INLINE-CODE |
| Links (direct, empty, ref, autolink, in heading) | §5.4, §5.5 | CT-LINK, CT-EMPTY-LINK, CT-REF-LINK, CT-AUTOLINK, CT-LINK-IN-HEADING |
| Blockquote | §5.5 | CT-BLOCKQUOTE |
| Lists (unordered, ordered, nested, multi-block, task) | §5.4, §5.5 | CT-UNORDERED-LIST, CT-ORDERED-LIST, CT-NESTED-LIST, CT-LIST-MULTI, CT-TASK-LIST, CT-TASK-LIST-X |
| Tables (header-only, empty cell, alignment) | §5.4 | CT-TABLE-HEADER-ONLY, CT-TABLE-EMPTY-CELL, CT-TABLE-ALIGN |
| Images (present, missing) | §5.4, §5.5 | CT-IMAGE, CT-IMAGE-MISSING |
| Bookmarks and relationship order | §5.6 | CT-BOOKMARK, CT-REL-ID-ORDER |

This matrix elevates the specification from **implementation-ready** to **implementation-verifiable**: an implementation can be tested automatically against the canonical DocModel JSON and the DOCX structural assertions for each row.

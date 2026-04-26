namespace MDConverter.Options;

/// <summary>
/// Mapping from semantic Markdown elements to Word style names.
/// </summary>
public sealed class StyleMapping
{
    /// <summary>Style name for level 1 headings (Word built-in: Heading1).</summary>
    public string Heading1Style { get; set; } = "Heading1";

    /// <summary>Style name for level 2 headings (Word built-in: Heading2).</summary>
    public string Heading2Style { get; set; } = "Heading2";

    /// <summary>Style name for level 3 headings (Word built-in: Heading3).</summary>
    public string Heading3Style { get; set; } = "Heading3";

    /// <summary>Style name for level 4 headings (Word built-in: Heading4).</summary>
    public string Heading4Style { get; set; } = "Heading4";

    /// <summary>Style name for level 5 headings (Word built-in: Heading5).</summary>
    public string Heading5Style { get; set; } = "Heading5";

    /// <summary>Style name for level 6 headings (Word built-in: Heading6).</summary>
    public string Heading6Style { get; set; } = "Heading6";

    /// <summary>Style name for normal paragraphs (Word built-in: Normal).</summary>
    public string ParagraphStyle { get; set; } = "Normal";

    /// <summary>Style name for code blocks (custom; Word has no built-in).</summary>
    public string CodeBlockStyle { get; set; } = "MdCodeBlock";

    /// <summary>Style name for inline code (custom).</summary>
    public string InlineCodeStyle { get; set; } = "MdInlineCode";

    /// <summary>Style name for blockquotes (Word built-in: IntenseQuote).</summary>
    public string QuoteStyle { get; set; } = "IntenseQuote";

    /// <summary>Style name for tables.</summary>
    public string TableStyle { get; set; } = "MdTable";

    /// <summary>Style name for missing-image placeholder text.</summary>
    public string MissingImageStyle { get; set; } = "MdMissingImage";
}

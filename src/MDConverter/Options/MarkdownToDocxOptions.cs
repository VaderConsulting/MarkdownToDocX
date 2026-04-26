namespace MDConverter.Options;

/// <summary>
/// Top-level options for Markdown to DOCX conversion.
/// Profile supplies built-in defaults; explicit sub-options override.
/// </summary>
public sealed class MarkdownToDocxOptions
{
    /// <summary>
    /// High-level profile name ("default", "strict-commonmark", "docsite", etc.) which sets baseline values.
    /// Null or empty is treated as "default".
    /// </summary>
    public string? Profile { get; set; }

    /// <summary>Markdown parsing options.</summary>
    public MarkdownOptions? Markdown { get; set; }

    /// <summary>DOCX output options.</summary>
    public DocxOptions? Docx { get; set; }

    /// <summary>Image handling options.</summary>
    public ImageOptions? Images { get; set; }

    /// <summary>Link handling options.</summary>
    public LinkOptions? Links { get; set; }

    /// <summary>Table rendering options.</summary>
    public TableOptions? Tables { get; set; }
}

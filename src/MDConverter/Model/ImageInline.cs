namespace MDConverter.Model;

/// <summary>Image; has source URI, alt text, and optional title.</summary>
public sealed class ImageInline(string SourceUri, string AltText = "", string Title = "") : InlineNode
{
    /// <summary>Source URI of the image (spec name: SourceUri).</summary>
    public string SourceUri { get; } = SourceUri ?? string.Empty;

    /// <summary>Alt text from Markdown.</summary>
    public string AltText { get; } = AltText ?? string.Empty;

    /// <summary>Optional title.</summary>
    public string Title { get; } = Title ?? string.Empty;

    /// <summary>Alias for SourceUri for backward compatibility.</summary>
    public string Url => SourceUri;
}

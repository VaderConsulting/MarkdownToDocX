namespace MDConverter.Model;

/// <summary>Image; has source URI, alt text, and optional title.</summary>
public sealed class ImageInline : InlineNode
{
    /// <summary>Source URI of the image (spec name: SourceUri).</summary>
    public string SourceUri { get; }

    /// <summary>Alt text from Markdown.</summary>
    public string AltText { get; }

    /// <summary>Optional title.</summary>
    public string Title { get; }

    /// <summary>Alias for SourceUri for backward compatibility.</summary>
    public string Url => SourceUri;

    public ImageInline(string SourceUri, string AltText = "", string Title = "")
    {
        this.SourceUri = SourceUri ?? string.Empty;
        this.AltText = AltText ?? string.Empty;
        this.Title = Title ?? string.Empty;
    }
}

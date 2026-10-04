namespace MDConverter.Options;

/// <summary>
/// Options for link handling (internal and external).
/// </summary>
public sealed class LinkOptions
{
    private bool _EnableInternalLinks = true;
    private bool _EnableExternalLinks = true;

    internal bool HasEnableInternalLinks { get; private set; }
    internal bool HasEnableExternalLinks { get; private set; }

    /// <summary>
    /// When true, [text](#anchor) is mapped to internal bookmarks/hyperlinks.
    /// Heading anchors use lowercase text, hyphens for whitespace, and no punctuation
    /// other than hyphens and underscores. Repeated headings get -1, -2, etc.
    /// </summary>
    public bool EnableInternalLinks
    {
        get => _EnableInternalLinks;
        set { _EnableInternalLinks = value; HasEnableInternalLinks = true; }
    }

    /// <summary>
    /// When true, [text](https://example.com) is mapped to external hyperlinks.
    /// </summary>
    public bool EnableExternalLinks
    {
        get => _EnableExternalLinks;
        set { _EnableExternalLinks = value; HasEnableExternalLinks = true; }
    }

    internal void SetResolvedValues(bool EnableInternalLinks, bool EnableExternalLinks)
    {
        _EnableInternalLinks = EnableInternalLinks;
        _EnableExternalLinks = EnableExternalLinks;
    }
}

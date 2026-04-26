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
    /// </summary>
    public bool EnableInternalLinks
    {
        get { return _EnableInternalLinks; }
        set { _EnableInternalLinks = value; HasEnableInternalLinks = true; }
    }

    /// <summary>
    /// When true, [text](https://example.com) is mapped to external hyperlinks.
    /// </summary>
    public bool EnableExternalLinks
    {
        get { return _EnableExternalLinks; }
        set { _EnableExternalLinks = value; HasEnableExternalLinks = true; }
    }

    internal void SetResolvedValues(bool EnableInternalLinks, bool EnableExternalLinks)
    {
        _EnableInternalLinks = EnableInternalLinks;
        _EnableExternalLinks = EnableExternalLinks;
    }
}

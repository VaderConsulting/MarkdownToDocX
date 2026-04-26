namespace MDConverter.Options;

/// <summary>
/// Options for Markdown parsing (profile and extension flags).
/// </summary>
public sealed class MarkdownOptions
{
    private bool _EnableTables;
    private bool _EnableTaskLists;
    private bool _EnableAutoLinks;
    private bool _EnableFootnotes;
    private bool _EnableInlineHtml;

    internal bool HasEnableTables { get; private set; }
    internal bool HasEnableTaskLists { get; private set; }
    internal bool HasEnableAutoLinks { get; private set; }
    internal bool HasEnableFootnotes { get; private set; }
    internal bool HasEnableInlineHtml { get; private set; }

    /// <summary>
    /// Logical profile name, e.g. "CommonMark", "GitHub", "Extended".
    /// </summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>
    /// When true, GitHub-style pipe tables are enabled.
    /// </summary>
    public bool EnableTables
    {
        get { return _EnableTables; }
        set { _EnableTables = value; HasEnableTables = true; }
    }

    /// <summary>
    /// When true, task lists are enabled.
    /// </summary>
    public bool EnableTaskLists
    {
        get { return _EnableTaskLists; }
        set { _EnableTaskLists = value; HasEnableTaskLists = true; }
    }

    /// <summary>
    /// When true, automatic links (URLs, emails) are enabled.
    /// </summary>
    public bool EnableAutoLinks
    {
        get { return _EnableAutoLinks; }
        set { _EnableAutoLinks = value; HasEnableAutoLinks = true; }
    }

    /// <summary>
    /// When true, footnotes are enabled.
    /// </summary>
    public bool EnableFootnotes
    {
        get { return _EnableFootnotes; }
        set { _EnableFootnotes = value; HasEnableFootnotes = true; }
    }

    /// <summary>
    /// When true, inline HTML is enabled.
    /// </summary>
    public bool EnableInlineHtml
    {
        get { return _EnableInlineHtml; }
        set { _EnableInlineHtml = value; HasEnableInlineHtml = true; }
    }

    internal void SetResolvedValues(bool EnableTables, bool EnableTaskLists, bool EnableAutoLinks, bool EnableFootnotes, bool EnableInlineHtml)
    {
        _EnableTables = EnableTables;
        _EnableTaskLists = EnableTaskLists;
        _EnableAutoLinks = EnableAutoLinks;
        _EnableFootnotes = EnableFootnotes;
        _EnableInlineHtml = EnableInlineHtml;
    }
}

namespace MDConverter.Options;

/// <summary>
/// Options for table rendering (alignment).
/// </summary>
public sealed class TableOptions
{
    private bool _EnableAlignment = true;

    internal bool HasEnableAlignment { get; private set; }

    /// <summary>
    /// When true, Markdown alignment markers (:---, :---:, ---:) are mapped to cell alignment.
    /// </summary>
    public bool EnableAlignment
    {
        get { return _EnableAlignment; }
        set { _EnableAlignment = value; HasEnableAlignment = true; }
    }

    internal void SetResolvedValues(bool EnableAlignment)
    {
        _EnableAlignment = EnableAlignment;
    }
}

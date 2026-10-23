namespace LiveEditor.Completion;

/// <summary>
/// One IntelliSense suggestion row.
/// </summary>
internal sealed class CompletionItem
{
    private readonly string _InsertText;

    private readonly string _DisplayText;

    private readonly string _Detail;

    /// <summary>
    /// Creates a suggestion item.
    /// </summary>
    /// <param name="InsertText">Text inserted at the caret token.</param>
    /// <param name="DisplayText">Primary label in the list.</param>
    /// <param name="Detail">Secondary detail shown beside the label.</param>
    public CompletionItem(string InsertText, string DisplayText, string Detail)
    {
        _InsertText = InsertText ?? string.Empty;
        _DisplayText = DisplayText ?? string.Empty;
        _Detail = Detail ?? string.Empty;
    }

    /// <summary>Gets the text to insert.</summary>
    public string InsertText
    {
        get
        {
            return _InsertText;
        }
    }

    /// <summary>Gets the primary list label.</summary>
    public string DisplayText
    {
        get
        {
            return _DisplayText;
        }
    }

    /// <summary>Gets the secondary detail text.</summary>
    public string Detail
    {
        get
        {
            return _Detail;
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(_Detail))
        {
            return _DisplayText;
        }

        return _DisplayText + "  " + _Detail;
    }
}

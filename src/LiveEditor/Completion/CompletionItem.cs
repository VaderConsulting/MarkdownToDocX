namespace LiveEditor.Completion;

/// <summary>
/// One IntelliSense suggestion row.
/// </summary>
internal sealed class CompletionItem
{
    private readonly string _InsertText;

    private readonly string _DisplayText;

    private readonly string _Detail;

    private readonly string _Documentation;

    /// <summary>
    /// Creates a suggestion item.
    /// </summary>
    /// <param name="InsertText">Text inserted at the caret token.</param>
    /// <param name="DisplayText">Primary label in the list.</param>
    /// <param name="Detail">Short secondary text. Kept for callers that still pass a hint.</param>
    /// <param name="Documentation">Explanation shown beside the list.</param>
    public CompletionItem(string InsertText, string DisplayText, string Detail, string Documentation = "")
    {
        _InsertText = InsertText ?? string.Empty;
        _DisplayText = DisplayText ?? string.Empty;
        _Detail = Detail ?? string.Empty;
        _Documentation = Documentation ?? string.Empty;
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

    /// <summary>Gets the explanation shown beside the completion list.</summary>
    public string Documentation
    {
        get
        {
            if (_Documentation.Length > 0)
            {
                return _Documentation;
            }

            return _Detail;
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _DisplayText;
    }
}

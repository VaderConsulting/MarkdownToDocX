using System;

namespace LiveEditor;

/// <summary>
/// Label and identifier chosen for a new element line.
/// </summary>
internal sealed class ElementInsertRequest : EventArgs
{
    private readonly string _KebabName;

    private readonly string _Identifier;

    private readonly string _Label;

    /// <summary>
    /// Creates an insert request.
    /// </summary>
    /// <param name="KebabName">Element type token.</param>
    /// <param name="Identifier">Short unique identifier.</param>
    /// <param name="Label">Display label.</param>
    public ElementInsertRequest(string KebabName, string Identifier, string Label)
    {
        _KebabName = KebabName ?? string.Empty;
        _Identifier = Identifier ?? string.Empty;
        _Label = Label ?? string.Empty;
    }

    /// <summary>Gets the element type token.</summary>
    public string KebabName
    {
        get
        {
            return _KebabName;
        }
    }

    /// <summary>Gets the short identifier.</summary>
    public string Identifier
    {
        get
        {
            return _Identifier;
        }
    }

    /// <summary>Gets the display label.</summary>
    public string Label
    {
        get
        {
            return _Label;
        }
    }
}

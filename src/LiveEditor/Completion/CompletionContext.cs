using System.Collections.Generic;

namespace LiveEditor.Completion;

/// <summary>
/// Kind of token being completed on the current line.
/// </summary>
internal enum CompletionTokenKind
{
    None,
    RelationshipKind,
    RelationshipTarget
}

/// <summary>
/// Caret completion context for one editor position.
/// </summary>
internal sealed class CompletionContext
{
    private readonly List<CompletionItem> _Items = new List<CompletionItem>();

    /// <summary>Gets or sets the token kind under the caret.</summary>
    public CompletionTokenKind TokenKind { get; set; }

    /// <summary>Gets or sets the absolute start index of the token being replaced.</summary>
    public int ReplaceStart { get; set; }

    /// <summary>Gets or sets the absolute end index of the token being replaced.</summary>
    public int ReplaceEnd { get; set; }

    /// <summary>Gets or sets the typed prefix used to filter suggestions.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Gets the suggestion items.</summary>
    public List<CompletionItem> Items
    {
        get
        {
            return _Items;
        }
    }

    /// <summary>Gets or sets a short status note when there are no items.</summary>
    public string StatusMessage { get; set; } = string.Empty;
}

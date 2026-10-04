using System.Collections.Generic;

namespace MDConverter.Model;

/// <summary>Hyperlink; has URI and a sequence of InlineNode for the link text.</summary>
public sealed class LinkInline(string Url) : InlineNode
{
    private readonly List<InlineNode> _Inlines = [];

    public string Url { get; } = Url ?? string.Empty;

    public IReadOnlyList<InlineNode> Inlines => _Inlines;

    public void AddInline(InlineNode Inline)
    {
        _Inlines.Add(Inline);
    }
}

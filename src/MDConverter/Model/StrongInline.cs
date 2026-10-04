using System.Collections.Generic;

namespace MDConverter.Model;

/// <summary>Strong text (e.g. bold); contains nested inlines.</summary>
public sealed class StrongInline : InlineNode
{
    private readonly List<InlineNode> _Inlines = [];

    public IReadOnlyList<InlineNode> Inlines => _Inlines;

    public void AddInline(InlineNode Inline)
    {
        _Inlines.Add(Inline);
    }
}

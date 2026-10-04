using System.Collections.Generic;

namespace MDConverter.Model;

public sealed class StrikethroughInline : InlineNode
{
    private readonly List<InlineNode> _Inlines = [];

    public IReadOnlyList<InlineNode> Inlines => _Inlines;

    public void AddInline(InlineNode Inline)
    {
        _Inlines.Add(Inline);
    }
}

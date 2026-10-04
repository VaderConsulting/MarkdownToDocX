using System.Collections.Generic;

namespace MDConverter.Model;

/// <summary>Plain paragraph; contains a sequence of inline nodes.</summary>
public sealed class ParagraphBlock : BlockNode
{
    private readonly List<InlineNode> _Inlines = [];

    public IReadOnlyList<InlineNode> Inlines => _Inlines;

    public void AddInline(InlineNode Inline)
    {
        _Inlines.Add(Inline);
    }
}

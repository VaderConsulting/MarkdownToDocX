using System.Collections.Generic;

namespace MDConverter.Model;

/// <summary>Emphasised text (e.g. italic); contains nested inlines.</summary>
public sealed class EmphasisInline : InlineNode
{
    private readonly List<InlineNode> _Inlines = [];

    public IReadOnlyList<InlineNode> Inlines => _Inlines;

    public void AddInline(InlineNode Inline)
    {
        _Inlines.Add(Inline);
    }
}

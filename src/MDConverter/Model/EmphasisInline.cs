using System.Collections.Generic;

namespace MDConverter.Model;

/// <summary>Emphasised text (e.g. italic); contains nested inlines.</summary>
public sealed class EmphasisInline : InlineNode
{
    private readonly List<InlineNode> _Inlines = new List<InlineNode>();

    public IReadOnlyList<InlineNode> Inlines
    {
        get { return _Inlines; }
    }

    public void AddInline(InlineNode Inline)
    {
        _Inlines.Add(Inline);
    }
}

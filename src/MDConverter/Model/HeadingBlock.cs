using System.Collections.Generic;

namespace MDConverter.Model;

/// <summary>Heading with level 1-6; contains inlines.</summary>
public sealed class HeadingBlock(int Level) : BlockNode
{
    private readonly List<InlineNode> _Inlines = [];

    public int Level { get; } = Level;

    public IReadOnlyList<InlineNode> Inlines => _Inlines;

    public void AddInline(InlineNode Inline)
    {
        _Inlines.Add(Inline);
    }
}

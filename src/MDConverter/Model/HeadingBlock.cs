using System.Collections.Generic;

namespace MDConverter.Model;

/// <summary>Heading with level 1-6; contains inlines.</summary>
public sealed class HeadingBlock : BlockNode
{
    private readonly List<InlineNode> _Inlines = [];

    public int Level { get; }

    public IReadOnlyList<InlineNode> Inlines => _Inlines;

    public HeadingBlock(int Level) => this.Level = Level;

    public void AddInline(InlineNode Inline)
    {
        _Inlines.Add(Inline);
    }
}

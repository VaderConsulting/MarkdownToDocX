using System.Collections.Generic;

namespace MDConverter.Model;

/// <summary>Hyperlink; has URI and a sequence of InlineNode for the link text.</summary>
public sealed class LinkInline : InlineNode
{
    private readonly List<InlineNode> _Inlines = new List<InlineNode>();

    public string Url { get; }

    public IReadOnlyList<InlineNode> Inlines
    {
        get { return _Inlines; }
    }

    public LinkInline(string Url) => this.Url = Url ?? string.Empty;

    public void AddInline(InlineNode Inline)
    {
        _Inlines.Add(Inline);
    }
}

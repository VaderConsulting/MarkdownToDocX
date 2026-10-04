namespace MDConverter.Model;

/// <summary>Inline code span.</summary>
public sealed class CodeInline(string Content) : InlineNode
{
    public string Content { get; } = Content ?? string.Empty;
}

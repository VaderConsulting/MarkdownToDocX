namespace MDConverter.Model;

/// <summary>Inline code span.</summary>
public sealed class CodeInline : InlineNode
{
    public string Content { get; }

    public CodeInline(string Content) => this.Content = Content ?? string.Empty;
}

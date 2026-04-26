namespace MDConverter.Model;

/// <summary>Preformatted block; raw content and optional language.</summary>
public sealed class CodeBlock : BlockNode
{
    public string Content { get; }

    public string Language { get; }

    public CodeBlock(string Content, string Language = "")
    {
        this.Content = Content ?? string.Empty;
        this.Language = Language ?? string.Empty;
    }
}

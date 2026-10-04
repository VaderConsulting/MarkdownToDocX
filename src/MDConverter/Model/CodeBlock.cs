namespace MDConverter.Model;

/// <summary>Preformatted block; raw content and optional language.</summary>
public sealed class CodeBlock(string Content, string Language = "") : BlockNode
{
    public string Content { get; } = Content ?? string.Empty;

    public string Language { get; } = Language ?? string.Empty;
}

namespace MDConverter.Model;

public sealed class MermaidBlock(string Source) : BlockNode
{
    public string Source { get; } = Source ?? string.Empty;
}

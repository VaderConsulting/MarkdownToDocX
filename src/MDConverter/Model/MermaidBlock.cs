namespace MDConverter.Model;

public sealed class MermaidBlock : BlockNode
{
    public string Source { get; }

    public MermaidBlock(string Source) => this.Source = Source ?? string.Empty;
}

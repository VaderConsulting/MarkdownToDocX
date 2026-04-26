namespace MermaidSharp.Diagrams.Block;

/// <summary>
/// Directed or undirected link between block elements (see Mermaid block diagram edge syntax).
/// </summary>
public sealed class BlockEdge
{
    public string FromId { get; set; } = string.Empty;

    public string ToId { get; set; } = string.Empty;

    /// <summary>
    /// True for <c>--&gt;</c>; false for <c>---</c>.
    /// </summary>
    public bool Directed { get; set; }
}

namespace MermaidSharp.Diagrams.Flowchart;

public class FlowchartModel : GraphDiagramBase
{
    /// <summary>
    /// Mermaid <c>classDef</c> declarations keyed by class name.
    /// </summary>
    public Dictionary<string, Style> ClassDefinitions { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Mermaid <c>class</c> assignments keyed by node id.
    /// </summary>
    public Dictionary<string, List<string>> ClassesByNodeId { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Mermaid <c>style</c> directives keyed by element id (node id or subgraph id).
    /// </summary>
    public Dictionary<string, Style> StyleByElementId { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Mermaid <c>linkStyle</c> directives keyed by 0-based edge index in parse order.
    /// </summary>
    public Dictionary<int, Style> StyleByLinkIndex { get; } = new();
}

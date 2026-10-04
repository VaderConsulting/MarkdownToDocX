using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Result of native Sugiyama layout for a flowchart contract.
/// </summary>
public sealed class FlowchartLayoutResult
{
    private readonly List<FlowchartLayoutNodeBounds> _NodeBounds = [];

    public IReadOnlyList<FlowchartLayoutNodeBounds> NodeBounds => _NodeBounds;

    internal List<FlowchartLayoutNodeBounds> NodeBoundsMutable => _NodeBounds;

    public double GraphWidth { get; init; }

    public double GraphHeight { get; init; }
}

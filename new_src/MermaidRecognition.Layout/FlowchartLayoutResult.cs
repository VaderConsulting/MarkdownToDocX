using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Result of native Sugiyama layout for a flowchart contract.
/// </summary>
public sealed class FlowchartLayoutResult
{
    private readonly List<FlowchartLayoutNodeBounds> _NodeBounds = new List<FlowchartLayoutNodeBounds>();

    public IReadOnlyList<FlowchartLayoutNodeBounds> NodeBounds
    {
        get
        {
            return _NodeBounds;
        }
    }

    internal List<FlowchartLayoutNodeBounds> NodeBoundsMutable
    {
        get
        {
            return _NodeBounds;
        }
    }

    public double GraphWidth { get; init; }

    public double GraphHeight { get; init; }
}

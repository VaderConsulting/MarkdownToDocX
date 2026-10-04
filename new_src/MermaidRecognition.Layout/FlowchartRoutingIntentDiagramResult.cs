using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Routing-intent diagnostic result for one flowchart diagram.
/// </summary>
public sealed class FlowchartRoutingIntentDiagramResult
{
    private string _DiagramId = string.Empty;

    private string _Direction = string.Empty;

    private List<FlowchartEdgeRoutingIntentRecord> _Edges = [];

    /// <summary>
    /// Diagram identifier from the torture-test fixture or generated sequence.
    /// </summary>
    public string DiagramId
    {
        get => _DiagramId; set => _DiagramId = value;
    }

    /// <summary>
    /// Normalised diagram direction such as <c>TD</c>, <c>LR</c>, <c>BT</c>, or <c>RL</c>.
    /// </summary>
    public string Direction
    {
        get => _Direction; set => _Direction = value;
    }

    /// <summary>
    /// Per-edge routing intent records in creation order.
    /// </summary>
    public List<FlowchartEdgeRoutingIntentRecord> Edges
    {
        get => _Edges; set => _Edges = value ?? [];
    }
}

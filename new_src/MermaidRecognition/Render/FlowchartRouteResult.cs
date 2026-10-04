namespace MermaidRecognition.Render;

/// <summary>
/// Full structural routing output for a flowchart contract.
/// </summary>
public sealed class FlowchartRouteResult
{
    private readonly List<FlowchartEdgeRoute> _Routes = [];

    public IReadOnlyList<FlowchartEdgeRoute> Routes => _Routes;

    public List<FlowchartEdgeRoute> RoutesMutable => _Routes;
}

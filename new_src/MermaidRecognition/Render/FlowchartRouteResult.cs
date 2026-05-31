namespace MermaidRecognition.Render;

/// <summary>
/// Full structural routing output for a flowchart contract.
/// </summary>
public sealed class FlowchartRouteResult
{
    private readonly List<FlowchartEdgeRoute> _Routes = new List<FlowchartEdgeRoute>();

    public IReadOnlyList<FlowchartEdgeRoute> Routes
    {
        get
        {
            return _Routes;
        }
    }

    public List<FlowchartEdgeRoute> RoutesMutable
    {
        get
        {
            return _Routes;
        }
    }
}

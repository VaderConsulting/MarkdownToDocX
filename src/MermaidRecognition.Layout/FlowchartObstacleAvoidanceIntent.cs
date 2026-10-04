namespace MermaidRecognition.Layout;

/// <summary>
/// Obstacle-avoidance requirement for a route.
/// </summary>
public enum FlowchartObstacleAvoidanceIntent
{
    NotRequired,

    AvoidDecisionNodeAndSiblingBranch,

    AvoidSpineCorridor,

    AvoidUnrelatedNodes,

    AvoidNodeBodiesAndDecisionDiamonds
}

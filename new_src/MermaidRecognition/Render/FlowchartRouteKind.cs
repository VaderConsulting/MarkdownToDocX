namespace MermaidRecognition.Render;

/// <summary>
/// Structural route classification for flowchart edges (spec §11.8). Must not depend on edge label text.
/// </summary>
public enum FlowchartRouteKind
{
    SimpleLink = 0,
    DecisionFanOut = 1,
    DecisionFanIn = 2,
    InvisibleLink = 3,
    BackEdgeLoop = 4,
    CrossGroupBoundary = 5
}

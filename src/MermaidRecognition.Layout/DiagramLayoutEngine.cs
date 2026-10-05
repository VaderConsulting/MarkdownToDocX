using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Factory and façade for the shared diagram layout engine.
/// </summary>
public static class DiagramLayoutEngine
{
    /// <summary>
    /// Creates the implementation selected by <paramref name="Profile"/>.
    /// </summary>
    /// <param name="Profile">Options bag. The engine kind chooses the implementation.</param>
    /// <returns>The engine instance.</returns>
    public static IDiagramLayoutEngine Create(DiagramLayoutProfile Profile)
    {
        if (Profile == null)
        {
            return new CustomDiagramLayoutEngine();
        }

        switch (Profile.EngineKind)
        {
            case DiagramLayoutEngineKind.MsaglRectilinear:
            case DiagramLayoutEngineKind.MsaglSpline:
                return new MsaglDiagramLayoutEngine();
            default:
                return new CustomDiagramLayoutEngine();
        }
    }

    /// <summary>
    /// Lays out <paramref name="Contract"/> with <paramref name="Profile"/>.
    /// </summary>
    /// <param name="Contract">Recognised diagram.</param>
    /// <param name="Profile">Options bag.</param>
    /// <param name="Solution">SVG-space geometry when this method returns true.</param>
    /// <returns>True when layout produced at least one node.</returns>
    public static bool TryLayout(
        DiagramContract Contract,
        DiagramLayoutProfile Profile,
        out DiagramLayoutSolution Solution)
    {
        return Create(Profile).TryLayout(Contract, Profile, out Solution);
    }

    /// <summary>
    /// Renders <paramref name="Contract"/> to SVG with the engine selected by <paramref name="Profile"/>.
    /// </summary>
    /// <param name="Contract">Recognised diagram.</param>
    /// <param name="Profile">Options bag.</param>
    /// <returns>SVG markup, or empty when layout failed.</returns>
    public static string RenderToSvg(DiagramContract Contract, DiagramLayoutProfile Profile)
    {
        if (Profile == null || Profile.EngineKind == DiagramLayoutEngineKind.Custom)
        {
            return FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        }

        if (!TryLayout(Contract, Profile, out DiagramLayoutSolution Solution) || Solution.NodeBounds.Count == 0)
        {
            return string.Empty;
        }

        return DiagramLayoutSvgRenderer.RenderToSvg(Contract, Solution, Profile);
    }
}

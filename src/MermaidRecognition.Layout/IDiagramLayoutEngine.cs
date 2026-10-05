using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// One layout and routing engine used by every node-and-edge diagram type.
/// </summary>
public interface IDiagramLayoutEngine
{
    /// <summary>
    /// Places nodes and routes edges for <paramref name="Contract"/> using <paramref name="Profile"/> options.
    /// </summary>
    /// <param name="Contract">Recognised diagram.</param>
    /// <param name="Profile">Options only: engine kind, spacing, arrowheads, and label offset.</param>
    /// <param name="Solution">SVG-space geometry when this method returns true.</param>
    /// <returns>True when layout produced at least one node.</returns>
    bool TryLayout(DiagramContract Contract, DiagramLayoutProfile Profile, out DiagramLayoutSolution Solution);
}

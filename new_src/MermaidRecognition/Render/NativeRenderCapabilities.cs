using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Diagnostics;

namespace MermaidRecognition.Render;

/// <summary>
/// Contract-based render eligibility (Stage 11.E pilot).
/// </summary>
public static class NativeRenderCapabilities
{
    /// <summary>
    /// Returns whether the native render path can attempt this diagram.
    /// </summary>
    public static bool CanRender(DiagramContract Contract)
    {
        if (Contract == null)
        {
            return false;
        }

        if (Contract.RemarkStatus == MermaidDiagramRemarkStatus.CatalogueRemarked)
        {
            return false;
        }

        if (HasBlockingDiagnostic(Contract))
        {
            return false;
        }

        string Kind = Contract.Kind ?? string.Empty;
        if (Kind.Equals("flowchart", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("graph", StringComparison.OrdinalIgnoreCase))
        {
            return Contract.Nodes.Count > 0 || Contract.Edges.Count > 0;
        }

        if (Kind.Equals("sequence", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("sequenceDiagram", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("classDiagram", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("stateDiagram", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("erDiagram", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("pie", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("gantt", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("mindmap", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("gitGraph", StringComparison.OrdinalIgnoreCase))
        {
            return Contract.Nodes.Count > 0
                || Contract.Edges.Count > 0
                || Contract.SourceStatements.Count > 0;
        }

        return false;
    }

    private static bool HasBlockingDiagnostic(DiagramContract Contract)
    {
        foreach (MermaidDiagnostic Diagnostic in Contract.Diagnostics)
        {
            if (Diagnostic.Severity == MermaidDiagnosticSeverity.Error)
            {
                return true;
            }
        }

        return false;
    }
}

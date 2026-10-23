using System;
using System.Collections.Generic;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Rule 3.11 Yes/No branch label matching (trim whitespace, case-insensitive Y/Yes and N/No).
/// </summary>
internal static class FlowchartDecisionBranchLabelHelper
{
    public static bool IsYesBranchLabel(string? Label)
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            return false;
        }

        string Normalised = Label.Trim();
        return string.Equals(Normalised, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "y", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNoBranchLabel(string? Label)
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            return false;
        }

        string Normalised = Label.Trim();
        return string.Equals(Normalised, "no", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "n", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsYesLabeledEdge(EdgeContract? Edge) =>
        Edge is not null && IsYesBranchLabel(Edge.Label);

    public static bool IsNoLabeledEdge(EdgeContract? Edge) =>
        Edge is not null && IsNoBranchLabel(Edge.Label);

    public static EdgeContract? FindYesLabeledEdge(IReadOnlyList<EdgeContract> OutgoingEdges)
    {
        foreach (EdgeContract Edge in OutgoingEdges)
        {
            if (IsYesBranchLabel(Edge.Label))
            {
                return Edge;
            }
        }

        return null;
    }

    public static EdgeContract? FindNoLabeledEdge(IReadOnlyList<EdgeContract> OutgoingEdges)
    {
        foreach (EdgeContract Edge in OutgoingEdges)
        {
            if (IsNoBranchLabel(Edge.Label))
            {
                return Edge;
            }
        }

        return null;
    }
}

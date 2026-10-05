using System;
using System.Collections.Generic;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Checks that enterprise edge geometry does not pass through non-endpoint nodes,
/// compound titles, or group tabs.
/// </summary>
public static class EnterpriseEdgeClearance
{
    private const double NODE_INSET = 1.5;

    private const double ENDPOINT_IGNORE_PX = 8;

    /// <summary>
    /// Returns one diagnostic string per illegal crossing or shared endpoint.
    /// </summary>
    /// <param name="Source">Enterprise diagram source.</param>
    /// <returns>Violation messages; empty when the diagram is clear.</returns>
    public static IReadOnlyList<string> FindViolations(string Source)
    {
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
        if (Analysis.HasErrors)
        {
            throw new InvalidOperationException("Enterprise diagram has validation errors.");
        }

        return FindViolations(Analysis.Document);
    }

    /// <summary>
    /// Returns one diagnostic string per illegal crossing or shared endpoint.
    /// </summary>
    /// <param name="Document">Validated document.</param>
    /// <returns>Violation messages; empty when the diagram is clear.</returns>
    public static IReadOnlyList<string> FindViolations(EnterpriseDocument Document)
    {
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        List<EnterpriseRoutedEdge> Routed = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        List<string> Violations = new List<string>();
        CollectNodeCrossings(Routed, Layout.Nodes, Violations);
        CollectSharedEndpoints(Routed, Violations);
        return Violations;
    }

    internal static bool PathHitsObstacle(
        IReadOnlyList<(double X, double Y)> Samples,
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        return FirstHit(Samples, Source, Target, Nodes) != null;
    }

    internal static bool PathDataHitsObstacle(
        string PathData,
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        if (!FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out List<(double X, double Y)> Samples))
        {
            return true;
        }

        return PathHitsObstacle(Samples, Source, Target, Nodes);
    }

    private static void CollectNodeCrossings(
        List<EnterpriseRoutedEdge> Routed,
        Dictionary<string, EnterpriseLayoutNode> Nodes,
        List<string> Violations)
    {
        foreach (EnterpriseRoutedEdge Edge in Routed)
        {
            if (!Nodes.TryGetValue(Edge.Relationship.SourceId, out EnterpriseLayoutNode? Source)
                || !Nodes.TryGetValue(Edge.Relationship.TargetId, out EnterpriseLayoutNode? Target))
            {
                continue;
            }

            if (!FlowchartSvgPathGeometryHelper.TryFlattenPathData(Edge.PathData, out List<(double X, double Y)> Samples))
            {
                Samples = Edge.SvgPoints;
            }

            string? Hit = FirstHit(Samples, Source, Target, Nodes);
            if (Hit != null)
            {
                Violations.Add(
                    Edge.Relationship.SourceId + " -> " + Edge.Relationship.TargetId
                    + " crosses " + Hit);
            }
        }
    }

    private static void CollectSharedEndpoints(List<EnterpriseRoutedEdge> Routed, List<string> Violations)
    {
        Dictionary<string, string> Occupied = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (EnterpriseRoutedEdge Edge in Routed)
        {
            if (Edge.SvgPoints.Count < 2)
            {
                continue;
            }

            string EdgeName = Edge.Relationship.SourceId + " -> " + Edge.Relationship.TargetId;
            RegisterEndpoint(Occupied, Edge.SvgPoints[0], EdgeName, "start", Violations);
            RegisterEndpoint(Occupied, Edge.SvgPoints[Edge.SvgPoints.Count - 1], EdgeName, "end", Violations);
        }
    }

    private static void RegisterEndpoint(
        Dictionary<string, string> Occupied,
        (double X, double Y) Point,
        string EdgeName,
        string End,
        List<string> Violations)
    {
        string Key = Math.Round(Point.X).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ","
            + Math.Round(Point.Y).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (Occupied.TryGetValue(Key, out string? Existing))
        {
            Violations.Add(EdgeName + " shares " + End + " marker with " + Existing + " at " + Key);
            return;
        }

        Occupied[Key] = EdgeName + " " + End;
    }

    private static string? FirstHit(
        IReadOnlyList<(double X, double Y)> Samples,
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        if (Samples.Count < 2)
        {
            return null;
        }

        double Total = 0;
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            Total += Distance(Samples[Index - 1], Samples[Index]);
        }

        double Traversed = 0;
        for (int Index = 0; Index < Samples.Count - 1; Index++)
        {
            (double X1, double Y1) = Samples[Index];
            (double X2, double Y2) = Samples[Index + 1];
            double Segment = Distance(Samples[Index], Samples[Index + 1]);
            bool NearEnd = Traversed + Segment <= ENDPOINT_IGNORE_PX || Traversed >= Total - ENDPOINT_IGNORE_PX;
            Traversed += Segment;
            if (NearEnd)
            {
                continue;
            }

            foreach (EnterpriseLayoutNode Node in Nodes.Values)
            {
                if (string.Equals(Node.Id, Source.Id, StringComparison.Ordinal)
                    || string.Equals(Node.Id, Target.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                bool Ancestor = EnterpriseObstacleCatalog.IsAncestor(Node, Source, Nodes)
                    || EnterpriseObstacleCatalog.IsAncestor(Node, Target, Nodes);
                EnterpriseRect? Title = EnterpriseObstacleCatalog.TitleBandOf(Node);
                if (Title.HasValue && Title.Value.IntersectsSegment(X1, Y1, X2, Y2, 0.5))
                {
                    return Node.Id + (Node.IsGroup ? " tab" : " title");
                }

                if (Ancestor)
                {
                    continue;
                }

                if (!Node.IsGroup
                    && (Node.Element != null || Node.IsCompound)
                    && EnterpriseObstacleCatalog.BoundsOf(Node).IntersectsSegment(X1, Y1, X2, Y2, NODE_INSET))
                {
                    return Node.Id;
                }
            }
        }

        return null;
    }

    private static double Distance((double X, double Y) A, (double X, double Y) B)
    {
        double Dx = B.X - A.X;
        double Dy = B.Y - A.Y;
        return Math.Sqrt((Dx * Dx) + (Dy * Dy));
    }
}

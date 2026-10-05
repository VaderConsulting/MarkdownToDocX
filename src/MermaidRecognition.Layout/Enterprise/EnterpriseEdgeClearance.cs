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
        CollectOwnEndpointHits(Routed, Layout.Nodes, Violations);
        CollectSharedEndpoints(Routed, Violations);
        CollectParallelOverlaps(Routed, Violations);
        CollectBorderHugs(Routed, Layout.Nodes, Violations);
        CollectBendBudget(Routed, Violations);
        CollectEndpointAttach(Routed, Layout.Nodes, Violations);
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

    private static void CollectOwnEndpointHits(
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

            if (!TrySamples(Edge, out List<(double X, double Y)> Samples))
            {
                continue;
            }

            string? Hit = FirstOwnLabelOrTitle(Samples, Source, Target);
            if (Hit != null)
            {
                Violations.Add(
                    Edge.Relationship.SourceId + " -> " + Edge.Relationship.TargetId
                    + " crosses own " + Hit);
            }
        }
    }

    private static void CollectParallelOverlaps(List<EnterpriseRoutedEdge> Routed, List<string> Violations)
    {
        for (int Left = 0; Left < Routed.Count; Left++)
        {
            List<(double X, double Y)> LeftPoints = EnterpriseOrthogonalRouter.Simplify(Routed[Left].SvgPoints);
            for (int Right = Left + 1; Right < Routed.Count; Right++)
            {
                List<(double X, double Y)> RightPoints = EnterpriseOrthogonalRouter.Simplify(Routed[Right].SvgPoints);
                if (HasCloseParallel(LeftPoints, RightPoints))
                {
                    Violations.Add(
                        Routed[Left].Relationship.SourceId + " -> " + Routed[Left].Relationship.TargetId
                        + " overlaps "
                        + Routed[Right].Relationship.SourceId + " -> " + Routed[Right].Relationship.TargetId
                        + " under " + EnterpriseOrthogonalRouter.CHANNEL_GAP.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "px");
                }
            }
        }
    }

    private static bool HasCloseParallel(
        List<(double X, double Y)> Left,
        List<(double X, double Y)> Right)
    {
        for (int Li = 1; Li < Left.Count - 2; Li++)
        {
            for (int Ri = 1; Ri < Right.Count - 2; Ri++)
            {
                (double Ax, double Ay) = Left[Li];
                (double Bx, double By) = Left[Li + 1];
                (double Cx, double Cy) = Right[Ri];
                (double Dx, double Dy) = Right[Ri + 1];
                bool LeftVertical = Math.Abs(Ax - Bx) < 0.6;
                bool RightVertical = Math.Abs(Cx - Dx) < 0.6;
                bool LeftHorizontal = Math.Abs(Ay - By) < 0.6;
                bool RightHorizontal = Math.Abs(Cy - Dy) < 0.6;
                if (LeftVertical && RightVertical && RangesOverlap(Ay, By, Cy, Dy, 8))
                {
                    double Gap = Math.Abs(Ax - Cx);
                    if (Gap < EnterpriseOrthogonalRouter.CHANNEL_GAP)
                    {
                        return true;
                    }
                }

                if (LeftHorizontal && RightHorizontal && RangesOverlap(Ax, Bx, Cx, Dx, 8))
                {
                    double Gap = Math.Abs(Ay - Cy);
                    if (Gap < EnterpriseOrthogonalRouter.CHANNEL_GAP)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool RangesOverlap(double A1, double A2, double B1, double B2, double MinOverlap)
    {
        double Low = Math.Max(Math.Min(A1, A2), Math.Min(B1, B2));
        double High = Math.Min(Math.Max(A1, A2), Math.Max(B1, B2));
        return High - Low >= MinOverlap;
    }

    private static void CollectBorderHugs(
        List<EnterpriseRoutedEdge> Routed,
        Dictionary<string, EnterpriseLayoutNode> Nodes,
        List<string> Violations)
    {
        foreach (EnterpriseRoutedEdge Edge in Routed)
        {
            List<(double X, double Y)> Points = EnterpriseOrthogonalRouter.Simplify(Edge.SvgPoints);
            for (int Index = 1; Index < Points.Count - 2; Index++)
            {
                (double X1, double Y1) = Points[Index];
                (double X2, double Y2) = Points[Index + 1];
                foreach (EnterpriseLayoutNode Node in Nodes.Values)
                {
                    if (EnterpriseObstacleCatalog.BoundsOf(Node).IsAlongBorder(
                            X1,
                            Y1,
                            X2,
                            Y2,
                            EnterpriseOrthogonalRouter.BORDER_GAP,
                            16))
                    {
                        Violations.Add(
                            Edge.Relationship.SourceId + " -> " + Edge.Relationship.TargetId
                            + " runs along " + Node.Id + " border");
                        Index = Points.Count;
                        break;
                    }
                }
            }
        }
    }

    private static void CollectBendBudget(List<EnterpriseRoutedEdge> Routed, List<string> Violations)
    {
        foreach (EnterpriseRoutedEdge Edge in Routed)
        {
            List<(double X, double Y)> Points = EnterpriseOrthogonalRouter.Simplify(Edge.SvgPoints);
            if (!IsOrthogonal(Points))
            {
                continue;
            }

            int Bends = EnterpriseOrthogonalRouter.CountBends(Points);
            if (Bends > EnterpriseOrthogonalRouter.MAX_BENDS)
            {
                Violations.Add(
                    Edge.Relationship.SourceId + " -> " + Edge.Relationship.TargetId
                    + " has " + Bends.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " bends (budget "
                    + EnterpriseOrthogonalRouter.MAX_BENDS.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ")");
            }
        }
    }

    private static void CollectEndpointAttach(
        List<EnterpriseRoutedEdge> Routed,
        Dictionary<string, EnterpriseLayoutNode> Nodes,
        List<string> Violations)
    {
        foreach (EnterpriseRoutedEdge Edge in Routed)
        {
            if (Edge.SvgPoints.Count < 2)
            {
                continue;
            }

            if (!Nodes.TryGetValue(Edge.Relationship.SourceId, out EnterpriseLayoutNode? Source)
                || !Nodes.TryGetValue(Edge.Relationship.TargetId, out EnterpriseLayoutNode? Target))
            {
                continue;
            }

            (double X0, double Y0) = Edge.SvgPoints[0];
            (double Xn, double Yn) = Edge.SvgPoints[Edge.SvgPoints.Count - 1];
            if (!EnterpriseObstacleCatalog.BoundsOf(Source).ContainsOnBorder(X0, Y0, 2.5))
            {
                Violations.Add(
                    Edge.Relationship.SourceId + " -> " + Edge.Relationship.TargetId
                    + " does not start on " + Source.Id + " border");
            }

            if (!EnterpriseObstacleCatalog.BoundsOf(Target).ContainsOnBorder(Xn, Yn, 2.5))
            {
                Violations.Add(
                    Edge.Relationship.SourceId + " -> " + Edge.Relationship.TargetId
                    + " does not end on " + Target.Id + " border");
            }
        }
    }

    private static bool IsOrthogonal(List<(double X, double Y)> Points)
    {
        for (int Index = 0; Index < Points.Count - 1; Index++)
        {
            bool Axis = Math.Abs(Points[Index].X - Points[Index + 1].X) < 1.2
                || Math.Abs(Points[Index].Y - Points[Index + 1].Y) < 1.2;
            if (!Axis)
            {
                return false;
            }
        }

        return Points.Count >= 2;
    }

    private static bool TrySamples(EnterpriseRoutedEdge Edge, out List<(double X, double Y)> Samples)
    {
        if (FlowchartSvgPathGeometryHelper.TryFlattenPathData(Edge.PathData, out Samples))
        {
            return Samples.Count >= 2;
        }

        Samples = Edge.SvgPoints;
        return Samples.Count >= 2;
    }

    private static string? FirstOwnLabelOrTitle(
        IReadOnlyList<(double X, double Y)> Samples,
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target)
    {
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

            EnterpriseRect? SourceLabel = EnterpriseObstacleCatalog.LabelBandOf(Source);
            if (SourceLabel.HasValue && SourceLabel.Value.IntersectsSegment(X1, Y1, X2, Y2, 0.5))
            {
                return Source.Id + " label";
            }

            EnterpriseRect? TargetLabel = EnterpriseObstacleCatalog.LabelBandOf(Target);
            if (TargetLabel.HasValue && TargetLabel.Value.IntersectsSegment(X1, Y1, X2, Y2, 0.5))
            {
                return Target.Id + " label";
            }
        }

        return null;
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

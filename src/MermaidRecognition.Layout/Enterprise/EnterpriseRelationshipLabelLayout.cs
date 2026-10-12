using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Render;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Wraps relationship text and staggers competing labels along straight runs.</summary>
internal static class EnterpriseRelationshipLabelLayout
{
    private const double FontSize = 12;
    private const double Clearance = 6;

    internal static void Place(List<EnterpriseRoutedEdge> Edges,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        foreach (var Edge in Edges)
        {
            Edge.LabelLines = Wrap(Edge.Label);
            Edge.LabelWidth = Edge.LabelLines.Count == 0 ? 0 : Edge.LabelLines.Max(Line =>
                Line.Length * FontSize * FlowchartEdgeLabelGapHelper.LABEL_CHAR_WIDTH_EM);
            Edge.LabelHeight = string.IsNullOrWhiteSpace(Edge.Label) ? 0 : FontSize + 15 * (Edge.LabelLines.Count - 1);
        }
        List<EnterpriseRoutedEdge> Placed = new();
        // Reserve the most constrained text first. Stable ordering resolves equal sizes.
        foreach (var Edge in Edges.Where(Edge => !string.IsNullOrWhiteSpace(Edge.Label))
            .OrderByDescending(Edge => Edge.LabelWidth * Edge.LabelHeight))
        {
            FlowchartEdgeLabelGapHelper.LabelSegmentBreak? Preferred = null;
            bool Found = false;
            foreach (int WrapWidth in new[] { 32, 24, 16, 8 })
            {
                Edge.LabelLines = Wrap(Edge.Label, WrapWidth);
                Edge.LabelWidth = Edge.LabelLines.Max(Line => Line.Length * FontSize * FlowchartEdgeLabelGapHelper.LABEL_CHAR_WIDTH_EM);
                Edge.LabelHeight = FontSize + 15 * (Edge.LabelLines.Count - 1);
                var Dimensions = new FlowchartLabelDimensions { DisplayText = string.Join("\n", Edge.LabelLines),
                    Width = Edge.LabelWidth, Height = Edge.LabelHeight };
                FlowchartEdgeLabelGapHelper.TryResolveLabelSegmentBreak(Edge.SvgPoints, Dimensions, FontSize, out Preferred);
                var Segments = Enumerable.Range(0, Edge.SvgPoints.Count - 1)
                    .OrderByDescending(Index => Distance(Edge.SvgPoints[Index], Edge.SvgPoints[Index + 1]));
                foreach (int Index in Segments)
                {
                    var A = Edge.SvgPoints[Index];
                    var B = Edge.SvgPoints[Index + 1];
                    bool Horizontal = Math.Abs(A.Y - B.Y) < 0.5;
                    if (!Horizontal && Math.Abs(A.X - B.X) >= 0.5) continue;
                    double Length = Distance(A, B);
                    double HalfGap = (Horizontal ? Edge.LabelWidth : Edge.LabelHeight) / 2 + Clearance;
                    double StartClearance = Index == 0 ? 10 : Math.Min(DiagramGridRouteBuilder.CORNER_RADIUS_PX,
                        Distance(Edge.SvgPoints[Index - 1], A) / 2);
                    double EndClearance = Index + 2 == Edge.SvgPoints.Count ? 10 : Math.Min(DiagramGridRouteBuilder.CORNER_RADIUS_PX,
                        Distance(B, Edge.SvgPoints[Index + 2]) / 2);
                    double Margin = HalfGap + Math.Max(StartClearance, EndClearance);
                    if (Length < 2 * Margin) continue;
                    for (int Step = 0; Step <= (int)(Length / Clearance) && !Found; Step++)
                    {
                        double Offset = Step == 0 ? 0 : ((Step + 1) / 2) * Clearance * (Step % 2 == 1 ? -1 : 1);
                        double Along = Length / 2 + Offset;
                        if (Along < Margin || Along > Length - Margin) continue;
                        double X = A.X + (B.X - A.X) * Along / Length;
                        double Y = A.Y + (B.Y - A.Y) * Along / Length;
                        if (!IsClear(X, Y, Edge.LabelWidth, Edge.LabelHeight, Nodes, Placed)) continue;
                        // No other connector may strike through the text.
                        bool Crosses = Edges.Where(Other => Other != Edge).Any(Other =>
                            Enumerable.Range(0, Other.SvgPoints.Count - 1).Any(Segment =>
                            {
                                var P = Other.SvgPoints[Segment];
                                var Q = Other.SvgPoints[Segment + 1];
                                return Math.Max(P.X, Q.X) > X - Edge.LabelWidth / 2 - Clearance
                                    && Math.Min(P.X, Q.X) < X + Edge.LabelWidth / 2 + Clearance
                                    && Math.Max(P.Y, Q.Y) > Y - Edge.LabelHeight / 2 - Clearance
                                    && Math.Min(P.Y, Q.Y) < Y + Edge.LabelHeight / 2 + Clearance;
                            }));
                        if (Crosses) continue;
                        Edge.LabelX = X;
                        Edge.LabelY = Y;
                        Edge.LabelBreak = new(Index, Index + 1, X, Y, HalfGap, Horizontal);
                        Found = true;
                    }
                    if (Found) break;
                }
                if (Found) break;
            }
            if (!Found && Preferred != null)
            {
                // Preserve visible text and its break when a local corridor is exhausted.
                Edge.LabelX = Preferred.CentreX;
                Edge.LabelY = Preferred.CentreY;
                Edge.LabelBreak = Preferred;
            }
            Placed.Add(Edge);
        }
        ResolveRouteLabelConflicts(Edges, Nodes);
    }

    private static void ResolveRouteLabelConflicts(List<EnterpriseRoutedEdge> Edges,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        List<EnterpriseRoutedEdge> Labels = Edges.Where(Edge => !string.IsNullOrWhiteSpace(Edge.Label)).ToList();
        foreach (EnterpriseRoutedEdge Label in Labels)
        foreach (EnterpriseRoutedEdge Route in Edges.Where(Edge => Edge != Label && string.IsNullOrWhiteSpace(Edge.Label)))
        {
            if (!IntersectsLabel(Route.SvgPoints, Label)) continue;
            if (!TryShiftEndpoint(Route, AtSource: false, Labels, Edges, Nodes))
                TryShiftEndpoint(Route, AtSource: true, Labels, Edges, Nodes);
        }
    }

    private static bool TryShiftEndpoint(EnterpriseRoutedEdge Route, bool AtSource,
        List<EnterpriseRoutedEdge> Labels,
        List<EnterpriseRoutedEdge> Edges, Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        if (Route.SvgPoints.Count < 2) return false;
        string Id = AtSource ? Route.Relationship.SourceId : Route.Relationship.TargetId;
        if (!Nodes.TryGetValue(Id, out EnterpriseLayoutNode? Node)) return false;
        int End = AtSource ? 0 : Route.SvgPoints.Count - 1;
        int Previous = AtSource ? 1 : End - 1;
        var Port = Route.SvgPoints[End];
        var Inner = Route.SvgPoints[Previous];
        bool VerticalRun = Math.Abs(Port.X - Inner.X) < 0.5;
        bool HorizontalRun = Math.Abs(Port.Y - Inner.Y) < 0.5;
        if (VerticalRun == HorizontalRun) return false;
        double Left = Node.AbsoluteX, Right = Left + Node.Width;
        double Top = Node.AbsoluteY, Bottom = Top + Node.Height;
        double Radius = Node.IsGroup || Node.IsCompound ? 0 : DiagramGridRouteBuilder.CORNER_RADIUS_PX;
        double Low = VerticalRun ? Left + Radius : Top + Radius;
        double High = VerticalRun ? Right - Radius : Bottom - Radius;
        double Current = VerticalRun ? Port.X : Port.Y;
        if (Current < Low - 0.5 || Current > High + 0.5) return false;
        double PortSpacing = FlowchartGeometryPolicy.CompoundPortSpacingPx;
        int SlotCount = Math.Max(1, (int)Math.Floor((High - Low) / PortSpacing));
        int[] Slots = DiagramGridMetrics.EqualCentres(Math.Max(1, (int)Math.Floor(High - Low)), SlotCount);
        List<double> Candidates = Slots.Select(Slot => Low + Slot)
            .Where(Value => Math.Abs(Value - Current) >= 0.5)
            .OrderBy(Value => Math.Abs(Value - Current)).ThenBy(Value => Value).ToList();
        List<(double X, double Y)> Original = Route.SvgPoints.ToList();
        int BaselineCrossings = Edges.Where(Edge => Edge != Route).Sum(Edge =>
            FlowchartBranchRouteOrder.Intersections(Original, Edge.SvgPoints).Crossings);
        double BaselineOverlap = Edges.Where(Edge => Edge != Route).Sum(Edge =>
            FlowchartBranchRouteOrder.Intersections(Original, Edge.SvgPoints).Overlap);

        foreach (double Candidate in Candidates)
        {
            List<(double X, double Y)> Moved = Original.ToList();
            int Index = End;
            while (Index >= 0 && Index < Moved.Count
                && Math.Abs((VerticalRun ? Moved[Index].X : Moved[Index].Y) - Current) < 0.5)
            {
                Moved[Index] = VerticalRun ? (Candidate, Moved[Index].Y) : (Moved[Index].X, Candidate);
                Index += AtSource ? 1 : -1;
            }
            if (!DistinctFromSiblingPorts(Route, AtSource, Candidate, VerticalRun, Edges)) continue;
            if (!ClearOfNodes(Moved, Route, Nodes) || Labels.Any(Label => IntersectsLabel(Moved, Label))) continue;
            int Crossings = 0;
            double Overlap = 0;
            foreach (EnterpriseRoutedEdge Other in Edges.Where(Edge => Edge != Route))
            {
                var Intersections = FlowchartBranchRouteOrder.Intersections(Moved, Other.SvgPoints);
                Crossings += Intersections.Crossings;
                Overlap += Intersections.Overlap;
            }
            if (Crossings > BaselineCrossings || Overlap > BaselineOverlap + 0.01) continue;
            Route.SvgPoints.Clear();
            Route.SvgPoints.AddRange(Moved);
            Route.PathData = FlowchartCurvePathBuilder.BuildRoundedCornerPathData(
                Route.SvgPoints, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
            return true;
        }
        return false;
    }

    private static bool DistinctFromSiblingPorts(EnterpriseRoutedEdge Route, bool AtSource,
        double Candidate, bool VerticalRun, List<EnterpriseRoutedEdge> Edges)
    {
        string Id = AtSource ? Route.Relationship.SourceId : Route.Relationship.TargetId;
        int End = AtSource ? 0 : Route.SvgPoints.Count - 1;
        foreach (EnterpriseRoutedEdge Other in Edges.Where(Edge => Edge != Route))
        {
            string OtherId = AtSource ? Other.Relationship.SourceId : Other.Relationship.TargetId;
            int OtherEnd = AtSource ? 0 : Other.SvgPoints.Count - 1;
            if (OtherId != Id || Other.SvgPoints.Count == 0) continue;
            var Port = Other.SvgPoints[OtherEnd];
            double OtherSlot = VerticalRun ? Port.X : Port.Y;
            double SameFace = VerticalRun
                ? Math.Abs(Port.Y - Route.SvgPoints[End].Y)
                : Math.Abs(Port.X - Route.SvgPoints[End].X);
            if (SameFace < 0.5 && Math.Abs(Candidate - OtherSlot) < FlowchartGeometryPolicy.CompoundPortSpacingPx)
                return false;
        }
        return true;
    }

    private static bool ClearOfNodes(List<(double X, double Y)> Points, EnterpriseRoutedEdge Route,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        foreach (EnterpriseLayoutNode Node in Nodes.Values.Where(Node =>
            Node.Id != Route.Relationship.SourceId && Node.Id != Route.Relationship.TargetId))
        {
            if (Node.IsGroup && (Contains(Node.Id, Route.Relationship.SourceId, Nodes)
                || Contains(Node.Id, Route.Relationship.TargetId, Nodes)
                || Encloses(Node, Route.Relationship.SourceId, Nodes)
                || Encloses(Node, Route.Relationship.TargetId, Nodes))) continue;
            double Height = Node.IsGroup ? 18 + 15 * (Node.LabelLines.Count - 1)
                : Node.IsCompound ? 36 + 15 * (Node.LabelLines.Count - 1) : Node.Height;
            double Left = Node.AbsoluteX, Right = Left + Node.Width;
            double Top = Node.AbsoluteY, Bottom = Top + Height;
            for (int Index = 1; Index < Points.Count; Index++)
            {
                var A = Points[Index - 1]; var B = Points[Index];
                bool HitVertical = Math.Abs(A.X - B.X) < 0.5 && A.X > Left && A.X < Right
                    && Math.Max(Math.Min(A.Y, B.Y), Top) < Math.Min(Math.Max(A.Y, B.Y), Bottom);
                bool HitHorizontal = Math.Abs(A.Y - B.Y) < 0.5 && A.Y > Top && A.Y < Bottom
                    && Math.Max(Math.Min(A.X, B.X), Left) < Math.Min(Math.Max(A.X, B.X), Right);
                if (HitVertical || HitHorizontal) return false;
            }
        }
        return true;
    }

    private static bool Contains(string ContainerId, string ElementId,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        string? ParentId = Nodes.TryGetValue(ElementId, out EnterpriseLayoutNode? Element)
            ? Element.ParentId : null;
        while (!string.IsNullOrEmpty(ParentId) && Nodes.TryGetValue(ParentId, out EnterpriseLayoutNode? Parent))
        {
            if (Parent.Id == ContainerId) return true;
            ParentId = Parent.ParentId;
        }
        return false;
    }

    private static bool Encloses(EnterpriseLayoutNode Container, string ElementId,
        Dictionary<string, EnterpriseLayoutNode> Nodes) => Nodes.TryGetValue(ElementId, out EnterpriseLayoutNode? Element)
        && Element.AbsoluteX >= Container.AbsoluteX
        && Element.AbsoluteY >= Container.AbsoluteY
        && Element.AbsoluteX + Element.Width <= Container.AbsoluteX + Container.Width
        && Element.AbsoluteY + Element.Height <= Container.AbsoluteY + Container.Height;

    private static bool IntersectsLabel(IReadOnlyList<(double X, double Y)> Points, EnterpriseRoutedEdge Label)
    {
        for (int Index = 1; Index < Points.Count; Index++)
        {
            var A = Points[Index - 1]; var B = Points[Index];
            if (Math.Max(A.X, B.X) > Label.LabelX - Label.LabelWidth / 2 - Clearance
                && Math.Min(A.X, B.X) < Label.LabelX + Label.LabelWidth / 2 + Clearance
                && Math.Max(A.Y, B.Y) > Label.LabelY - Label.LabelHeight / 2 - Clearance
                && Math.Min(A.Y, B.Y) < Label.LabelY + Label.LabelHeight / 2 + Clearance) return true;
        }
        return false;
    }

    private static bool IsClear(double X, double Y, double Width, double Height,
        Dictionary<string, EnterpriseLayoutNode> Nodes, List<EnterpriseRoutedEdge> Placed)
    {
        if (X - Width / 2 < Clearance || Y - Height / 2 < Clearance) return false;
        bool Overlaps(double Left, double Top, double OtherWidth, double OtherHeight) =>
            X - Width / 2 < Left + OtherWidth + Clearance && X + Width / 2 > Left - Clearance
            && Y - Height / 2 < Top + OtherHeight + Clearance && Y + Height / 2 > Top - Clearance;
        foreach (var Node in Nodes.Values)
        {
            if (Overlaps(Node.AbsoluteX, Node.AbsoluteY, Node.Width, Node.IsGroup ? 18 + 15 * (Node.LabelLines.Count - 1)
                : Node.IsCompound ? 36 + 15 * (Node.LabelLines.Count - 1) : Node.Height)) return false;
            if ((Node.IsGroup || Node.IsCompound) && (Overlaps(Node.AbsoluteX, Node.AbsoluteY, 0, Node.Height)
                || Overlaps(Node.AbsoluteX + Node.Width, Node.AbsoluteY, 0, Node.Height)
                || Overlaps(Node.AbsoluteX, Node.AbsoluteY + Node.Height, Node.Width, 0))) return false;
        }
        foreach (var Other in Placed)
            if (Overlaps(Other.LabelX - Other.LabelWidth / 2, Other.LabelY - Other.LabelHeight / 2,
                Other.LabelWidth, Other.LabelHeight)) return false;
        return true;
    }

    private static double Distance((double X, double Y) A, (double X, double Y) B) =>
        Math.Abs(B.X - A.X) + Math.Abs(B.Y - A.Y);

    private static List<string> Wrap(string Text, int Width = 32) => EnterpriseLabelText.Wrap(Text, Width);
}

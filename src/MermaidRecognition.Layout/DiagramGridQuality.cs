using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Grid, size, port, track, and group-frame checks for a rendered diagram.
/// </summary>
public static class DiagramGridQuality
{
    /// <summary>
    /// Adds a failure for each grid rule the layout breaks.
    /// </summary>
    /// <param name="Contract">The diagram contract.</param>
    /// <param name="Layout">The gridded layout.</param>
    /// <param name="Violations">Failures are appended here.</param>
    public static void Collect(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        if (Contract == null || Layout == null || Layout.NodeBounds.Count == 0)
        {
            return;
        }

        CheckWholePixels(Layout, Violations);
        CheckSizes(Contract, Layout, Violations);
        CheckLineShape(Layout, Violations);
        CheckOverlaps(Layout, Violations);
        CheckGroupFrames(Contract, Layout, Violations);
        CheckArrangement(Contract, Layout, Violations);
    }

    private static void CheckWholePixels(FlowchartLayoutResult Layout, List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            if (!IsWhole(Bounds.CenterX) || !IsWhole(Bounds.CenterY) || !IsWhole(Bounds.Width) || !IsWhole(Bounds.Height))
            {
                Violations.Add(Failure(
                    "WholePixel",
                    Bounds.NodeId + " has a fractional coordinate",
                    Bounds.NodeId));
            }
        }

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                if (!IsWhole(Point.X) || !IsWhole(Point.Y))
                {
                    Violations.Add(Failure(
                        "WholePixel",
                        Route.SourceId + "->" + Route.TargetId + " has a fractional point",
                        Route.SourceId,
                        Route.TargetId));
                    break;
                }
            }
        }
    }

    private static void CheckSizes(DiagramContract Contract, FlowchartLayoutResult Layout, List<FlowchartLayoutQualityFailure> Violations)
    {
        (int WidthPx, int HeightPx) = DiagramGridLayout.ResolveStandardBox(Contract);
        int DiamondHeight = HeightPx * FlowchartDagrePresentation.DIAMOND_HEIGHT_FACTOR;
        Dictionary<string, NodeContract> Nodes = IndexNodes(Contract);
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            bool Diamond = Nodes.TryGetValue(Bounds.NodeId, out NodeContract? Node)
                && FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
            if (Diamond)
            {
                if (Math.Abs(Bounds.Width - WidthPx) > 0.5 && !IsGridGrowth(Bounds.Width, WidthPx))
                {
                    Violations.Add(Failure(
                        "NodeSize",
                        Bounds.NodeId + " diamond width is " + Bounds.Width.ToString("0") + " instead of " + WidthPx.ToString(),
                        Bounds.NodeId));
                }

                if (Math.Abs(Bounds.Height - DiamondHeight) > 0.5 && !IsGridGrowth(Bounds.Height, DiamondHeight))
                {
                    Violations.Add(Failure(
                        "NodeSize",
                        Bounds.NodeId + " diamond height is " + Bounds.Height.ToString("0") + " instead of " + DiamondHeight.ToString(),
                        Bounds.NodeId));
                }

                continue;
            }

            if (Math.Abs(Bounds.Width - WidthPx) > 0.5 && !IsGridGrowth(Bounds.Width, WidthPx))
            {
                Violations.Add(Failure(
                    "NodeSize",
                    Bounds.NodeId + " width is " + Bounds.Width.ToString("0") + " instead of " + WidthPx.ToString(),
                    Bounds.NodeId));
            }

            if (Math.Abs(Bounds.Height - HeightPx) > 0.5 && !IsGridGrowth(Bounds.Height, HeightPx))
            {
                Violations.Add(Failure(
                    "NodeSize",
                    Bounds.NodeId + " height is " + Bounds.Height.ToString("0") + " instead of " + HeightPx.ToString(),
                    Bounds.NodeId));
            }
        }
    }

    private static void CheckLineShape(FlowchartLayoutResult Layout, List<FlowchartLayoutQualityFailure> Violations)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            FlowchartLayoutPoint Start = Route.Points[0];
            FlowchartLayoutPoint End = Route.Points[Route.Points.Count - 1];
            bool Aligned = Math.Abs(Start.X - End.X) <= 0.5 || Math.Abs(Start.Y - End.Y) <= 0.5;
            if (Aligned && Route.Points.Count != 2)
            {
                Violations.Add(Failure(
                    "LineShape",
                    Route.SourceId + "->" + Route.TargetId + " lines up but is not a single straight segment",
                    Route.SourceId,
                    Route.TargetId));
                continue;
            }

            if (!Aligned && Route.Points.Count == 2)
            {
                bool Diagonal = Math.Abs(Start.X - End.X) > 0.5 && Math.Abs(Start.Y - End.Y) > 0.5;
                if (Diagonal)
                {
                    Violations.Add(Failure(
                        "LineShape",
                        Route.SourceId + "->" + Route.TargetId + " is a diagonal instead of an orthogonal route",
                        Route.SourceId,
                        Route.TargetId));
                }

                continue;
            }

            if (!Aligned)
            {
                if (!IsOrthogonalPolyline(Route.Points))
                {
                    Violations.Add(Failure(
                        "LineShape",
                        Route.SourceId + "->" + Route.TargetId + " is not an orthogonal polyline",
                        Route.SourceId,
                        Route.TargetId));
                    continue;
                }

                int Corners = CountOrthogonalCorners(Route.Points);
                if (Corners > 2)
                {
                    Violations.Add(Failure(
                        "LineShape",
                        Route.SourceId + "->" + Route.TargetId + " has more than two corners",
                        Route.SourceId,
                        Route.TargetId));
                }

                FlowchartLayoutPoint Second = Route.Points[1];
                FlowchartLayoutPoint Penultimate = Route.Points[Route.Points.Count - 2];
                bool LeavesStraight = Math.Abs(Second.X - Start.X) <= 0.5 || Math.Abs(Second.Y - Start.Y) <= 0.5;
                bool EntersStraight = Math.Abs(Penultimate.X - End.X) <= 0.5 || Math.Abs(Penultimate.Y - End.Y) <= 0.5;
                if (!LeavesStraight || !EntersStraight)
                {
                    Violations.Add(Failure(
                        "LineShape",
                        Route.SourceId + "->" + Route.TargetId + " does not leave and enter perpendicular to the node side",
                        Route.SourceId,
                        Route.TargetId));
                }
            }
        }
    }

    private static void CheckOverlaps(FlowchartLayoutResult Layout, List<FlowchartLayoutQualityFailure> Violations)
    {
        for (int Left = 0; Left < Layout.NodeBounds.Count; Left++)
        {
            for (int Right = Left + 1; Right < Layout.NodeBounds.Count; Right++)
            {
                FlowchartLayoutNodeBounds A = Layout.NodeBounds[Left];
                FlowchartLayoutNodeBounds B = Layout.NodeBounds[Right];
                if (BoxesOverlap(A, B))
                {
                    Violations.Add(Failure(
                        "NodeOverlap",
                        A.NodeId + " overlaps " + B.NodeId,
                        A.NodeId,
                        B.NodeId));
                }
            }
        }
    }

    private static void CheckGroupFrames(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        if (Contract.Groups.Count == 0)
        {
            return;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Node in Layout.NodeBounds)
        {
            Bounds[Node.NodeId] = Node;
        }

        foreach (FlowchartLayoutNodeBounds Cluster in Layout.ClusterBounds)
        {
            Bounds[Cluster.NodeId] = Cluster;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (GroupContract Group in Contract.Groups)
        {
            if (!Bounds.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds? Frame))
            {
                Violations.Add(Failure("GroupFrame", "Group " + Group.Id + " has no frame", Group.Id));
                continue;
            }

            double FrameLeft = Frame.CenterX - (Frame.Width / 2.0);
            double FrameRight = Frame.CenterX + (Frame.Width / 2.0);
            double FrameBottom = Frame.CenterY - (Frame.Height / 2.0);
            double FrameTop = Frame.CenterY + (Frame.Height / 2.0);
            bool Any = false;
            double ContentLeft = 0;
            double ContentRight = 0;
            double ContentBottom = 0;
            double ContentTop = 0;
            foreach (string ChildId in Group.ChildNodeIds)
            {
                if (Bounds.TryGetValue(ChildId, out FlowchartLayoutNodeBounds? Child) && !IsGroup(Contract, ChildId))
                {
                    Include(Child, ref Any, ref ContentLeft, ref ContentRight, ref ContentBottom, ref ContentTop);
                }
            }

            foreach (string ChildGroupId in Group.ChildGroupIds)
            {
                if (Bounds.TryGetValue(ChildGroupId, out FlowchartLayoutNodeBounds? Child))
                {
                    Include(Child, ref Any, ref ContentLeft, ref ContentRight, ref ContentBottom, ref ContentTop);
                }
            }

            if (!Any)
            {
                continue;
            }

            double LeftPad = ContentLeft - FrameLeft;
            double RightPad = FrameRight - ContentRight;
            double BottomPad = ContentBottom - FrameBottom;
            double TopPad = FrameTop - ContentTop;
            if (LeftPad < DiagramGridMetrics.NODE_GAP_PX - 0.5
                || RightPad < DiagramGridMetrics.NODE_GAP_PX - 0.5
                || BottomPad < DiagramGridMetrics.NODE_GAP_PX - 0.5
                || Math.Abs(LeftPad - RightPad) > 1.5
                || Math.Abs(BottomPad - DiagramGridMetrics.NODE_GAP_PX) > 1.5)
            {
                Violations.Add(Failure(
                    "GroupFrame",
                    Group.Id + " padding is uneven or smaller than one grid unit",
                    Group.Id));
            }

            string Title = string.IsNullOrWhiteSpace(Group.Title) ? Group.Id : Group.Title;
            double TitleWidth = FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Title, Options.FontSizePx);
            if (TopPad + 0.5 < BottomPad + DiagramGridMetrics.GRID_UNIT_PX)
            {
                Violations.Add(Failure(
                    "GroupFrame",
                    Group.Id + " title strip is covered by the boxes inside it",
                    Group.Id));
            }

            if (Frame.Width + 0.5 < TitleWidth)
            {
                Violations.Add(Failure(
                    "GroupFrame",
                    Group.Id + " frame is narrower than its title",
                    Group.Id));
            }
        }
    }

    private static void CheckArrangement(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        List<FlowchartLayoutQualityFailure> Violations)
    {
        if (!FlowchartDagrePlacement.TryPlace(Contract, out FlowchartLayoutResult Reference))
        {
            return;
        }

        bool Horizontal = IsHorizontal(Contract.Direction);
        Dictionary<string, (double X, double Y)> ReferenceCentres = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Reference.NodeBounds)
        {
            ReferenceCentres[Bounds.NodeId] = (Bounds.CenterX, Bounds.CenterY);
        }

        Dictionary<string, (double X, double Y)> OurCentres = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            OurCentres[Bounds.NodeId] = (Bounds.CenterX, Bounds.CenterY);
        }

        List<List<string>> ReferenceRanks = BuildRanks(ReferenceCentres, Horizontal);
        List<List<string>> OurRanks = BuildRanks(OurCentres, Horizontal);
        if (!Horizontal)
        {
            OurRanks.Reverse();
        }

        // Rulebook 2.1 and 9.4: Mermaid ranks are the starting point, but section 2 may
        // reorder nodes within a rank and move them between columns. Only rank membership
        // is checked here; order, column and port sides are governed by placement rules.
        if (!SamePartitions(ReferenceRanks, OurRanks))
        {
            Violations.Add(Failure("ArrangementKept", "Ranks do not match the Mermaid placement"));
        }
    }

    private static FlowchartLayoutEdgeRoute? FindRoute(FlowchartLayoutResult Layout, FlowchartLayoutEdgeRoute Route)
    {
        foreach (FlowchartLayoutEdgeRoute Candidate in Layout.EdgeRoutes)
        {
            if (!string.IsNullOrEmpty(Route.EdgeId)
                && string.Equals(Candidate.EdgeId, Route.EdgeId, StringComparison.Ordinal))
            {
                return Candidate;
            }
        }

        foreach (FlowchartLayoutEdgeRoute Candidate in Layout.EdgeRoutes)
        {
            if (string.Equals(Candidate.SourceId, Route.SourceId, StringComparison.Ordinal)
                && string.Equals(Candidate.TargetId, Route.TargetId, StringComparison.Ordinal))
            {
                return Candidate;
            }
        }

        return null;
    }

    private static bool IsHorizontal(string? Direction)
    {
        FlowchartDirection Normalised = FlowchartDirectionNormalizer.Normalize(Direction);
        return Normalised == FlowchartDirection.LeftToRight || Normalised == FlowchartDirection.RightToLeft;
    }

    private static List<List<string>> BuildRanks(Dictionary<string, (double X, double Y)> Centres, bool Horizontal)
    {
        List<string> Ids = new List<string>(Centres.Keys);
        Ids.Sort(delegate(string Left, string Right)
        {
            double LeftMain = Horizontal ? Centres[Left].X : Centres[Left].Y;
            double RightMain = Horizontal ? Centres[Right].X : Centres[Right].Y;
            int Compare = LeftMain.CompareTo(RightMain);
            if (Compare != 0)
            {
                return Compare;
            }

            double LeftCross = Horizontal ? Centres[Left].Y : Centres[Left].X;
            double RightCross = Horizontal ? Centres[Right].Y : Centres[Right].X;
            return LeftCross.CompareTo(RightCross);
        });

        List<List<string>> Ranks = new List<List<string>>();
        List<string> Current = new List<string>();
        foreach (string Id in Ids)
        {
            if (Current.Count == 0)
            {
                Current.Add(Id);
                continue;
            }

            double CurrentMain = Horizontal ? Centres[Current[0]].X : Centres[Current[0]].Y;
            double Main = Horizontal ? Centres[Id].X : Centres[Id].Y;
            if (Math.Abs(Main - CurrentMain) <= DiagramGridMetrics.RANK_BAND_TOLERANCE_PX)
            {
                Current.Add(Id);
            }
            else
            {
                Ranks.Add(SortedCopy(Current, Centres, Horizontal));
                Current = new List<string> { Id };
            }
        }

        if (Current.Count > 0)
        {
            Ranks.Add(SortedCopy(Current, Centres, Horizontal));
        }

        return Ranks;
    }

    private static List<string> SortedCopy(
        List<string> Ids,
        Dictionary<string, (double X, double Y)> Centres,
        bool Horizontal)
    {
        List<string> Copy = new List<string>(Ids);
        Copy.Sort(delegate(string Left, string Right)
        {
            double LeftCross = Horizontal ? Centres[Left].Y : Centres[Left].X;
            double RightCross = Horizontal ? Centres[Right].Y : Centres[Right].X;
            return LeftCross.CompareTo(RightCross);
        });
        return Copy;
    }

    private static bool SamePartitions(List<List<string>> Left, List<List<string>> Right)
    {
        if (Left.Count != Right.Count)
        {
            return false;
        }

        for (int Index = 0; Index < Left.Count; Index++)
        {
            if (Left[Index].Count != Right[Index].Count)
            {
                return false;
            }

            HashSet<string> LeftIds = new HashSet<string>(Left[Index], StringComparer.Ordinal);
            foreach (string Id in Right[Index])
            {
                if (!LeftIds.Contains(Id))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool SameOrder(
        Dictionary<string, (double X, double Y)> Reference,
        Dictionary<string, (double X, double Y)> Ours,
        List<List<string>> Ranks,
        bool Horizontal)
    {
        foreach (List<string> Rank in Ranks)
        {
            List<string> ReferenceOrder = SortedCopy(Rank, Reference, Horizontal);
            List<string> OurOrder = SortedCopy(Rank, Ours, Horizontal);
            for (int Index = 0; Index < ReferenceOrder.Count; Index++)
            {
                if (!string.Equals(ReferenceOrder[Index], OurOrder[Index], StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void Include(
        FlowchartLayoutNodeBounds Child,
        ref bool Any,
        ref double Left,
        ref double Right,
        ref double Bottom,
        ref double Top)
    {
        double ChildLeft = Child.CenterX - (Child.Width / 2.0);
        double ChildRight = Child.CenterX + (Child.Width / 2.0);
        double ChildBottom = Child.CenterY - (Child.Height / 2.0);
        double ChildTop = Child.CenterY + (Child.Height / 2.0);
        if (!Any)
        {
            Any = true;
            Left = ChildLeft;
            Right = ChildRight;
            Bottom = ChildBottom;
            Top = ChildTop;
            return;
        }

        Left = Math.Min(Left, ChildLeft);
        Right = Math.Max(Right, ChildRight);
        Bottom = Math.Min(Bottom, ChildBottom);
        Top = Math.Max(Top, ChildTop);
    }

    private static bool IsGroup(DiagramContract Contract, string Id)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.Equals(Group.Id, Id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BoxesOverlap(FlowchartLayoutNodeBounds A, FlowchartLayoutNodeBounds B)
    {
        double Gap = 0.5;
        return Math.Abs(A.CenterX - B.CenterX) < ((A.Width + B.Width) / 2.0) - Gap
            && Math.Abs(A.CenterY - B.CenterY) < ((A.Height + B.Height) / 2.0) - Gap;
    }

    private static bool IsWhole(double Value)
    {
        return Math.Abs(Value - Math.Round(Value)) < 0.01;
    }

    private static bool IsGridGrowth(double Actual, int Standard)
    {
        double Extra = Actual - Standard;
        if (Extra < DiagramGridMetrics.GRID_UNIT_PX - 0.5)
        {
            return false;
        }

        double Units = Extra / DiagramGridMetrics.GRID_UNIT_PX;
        return Math.Abs(Units - Math.Round(Units)) < 0.01;
    }

    private static Dictionary<string, NodeContract> IndexNodes(DiagramContract Contract)
    {
        Dictionary<string, NodeContract> Nodes = new Dictionary<string, NodeContract>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            Nodes[Node.Id] = Node;
        }

        return Nodes;
    }

    private static FlowchartLayoutQualityFailure Failure(string Kind, string Description, params string[] Ids)
    {
        return new FlowchartLayoutQualityFailure(Kind, Description, Ids, null);
    }

    private static bool IsOrthogonalPolyline(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        if (Points.Count < 2)
        {
            return true;
        }

        for (int Index = 1; Index < Points.Count; Index++)
        {
            double DeltaX = Math.Abs(Points[Index].X - Points[Index - 1].X);
            double DeltaY = Math.Abs(Points[Index].Y - Points[Index - 1].Y);
            if (DeltaX <= 0.75 && DeltaY <= 0.75)
            {
                return false;
            }

            if (DeltaX > 0.75 && DeltaY > 0.75)
            {
                return false;
            }
        }

        return true;
    }

    private static int CountOrthogonalCorners(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        if (Points.Count < 3)
        {
            return 0;
        }

        int Corners = 0;
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            bool IncomingHorizontal = Math.Abs(Points[Index].X - Points[Index - 1].X) > 0.75;
            bool OutgoingHorizontal = Math.Abs(Points[Index + 1].X - Points[Index].X) > 0.75;
            if (IncomingHorizontal != OutgoingHorizontal)
            {
                Corners++;
            }
        }

        return Corners;
    }
}

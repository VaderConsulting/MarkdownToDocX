using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Adjusts a dagre placement for the document renderer without changing rank order.
/// Node centres stay put unless boxes overlap, and then a whole rank or column moves apart.
/// </summary>
public static class FlowchartDagrePresentation
{
    private const double RANK_TOLERANCE_PX = 1.5;

    private const double MIN_GAP_PX = 1.0;

    private const double CLUSTER_PAD_PX = 8.0;

    private const double CORNER_FACE_MIN_PX = 56.0;

    private const double CORNER_CLEARANCE_PX = 12.0;

    private const double WIDE_FACE_PX = 100.0;

    private const double WIDE_CORNER_CLEARANCE_PX = 24.0;

    /// <summary>
    /// Returns a Y-up copy of <paramref name="LaidOut"/> with one shared rectangle size,
    /// whole-pixel boxes, and edge ends on the drawn outline.
    /// </summary>
    public static FlowchartLayoutResult Apply(DiagramContract Contract, FlowchartLayoutResult LaidOut)
    {
        FlowchartLayoutResult Copy = CopyResult(LaidOut);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        bool Horizontal = Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.RightToLeft;
        Dictionary<string, NodeContract> Nodes = IndexNodes(Contract);
        List<Box> Boxes = new List<Box>();
        foreach (FlowchartLayoutNodeBounds Bounds in Copy.NodeBounds)
        {
            Boxes.Add(Box.From(Bounds));
        }

        ApplySharedSize(Boxes, Nodes);
        SeparateOverlaps(Boxes, Horizontal);
        List<Box> Clusters = new List<Box>();
        foreach (FlowchartLayoutNodeBounds Bounds in Copy.ClusterBounds)
        {
            Clusters.Add(Box.From(Bounds));
        }

        ExpandClusters(Contract, Boxes, Clusters);
        RoundBoxes(Boxes);
        RoundBoxes(Clusters);
        ExpandClusters(Contract, Boxes, Clusters);
        Dictionary<string, EdgeContract> Edges = IndexEdges(Contract);
        RewriteEdgeEnds(Copy, Boxes, Clusters, Nodes, Edges);
        PullRectangleCorners(Copy, Boxes, Clusters, Nodes);
        SeparateStackedEnds(Copy, Boxes, Clusters);
        double Flip = Math.Max(Copy.GraphHeight, Extent(Boxes, Clusters, Copy));
        FlipToYUp(Boxes, Clusters, Copy, Flip);
        WriteBoxes(Copy.NodeBoundsMutable, Boxes);
        WriteBoxes(Copy.ClusterBoundsMutable, Clusters);
        return Copy;
    }

    private static void ApplySharedSize(List<Box> Boxes, Dictionary<string, NodeContract> Nodes)
    {
        double StandardWidth = 0;
        double StandardHeight = 0;
        bool Found = false;
        foreach (Box Bounds in Boxes)
        {
            if (!Nodes.TryGetValue(Bounds.Id, out NodeContract? Node) || !IsRectangle(Node.Shape))
            {
                continue;
            }

            Found = true;
            StandardWidth = Math.Max(StandardWidth, Bounds.Width);
            StandardHeight = Math.Max(StandardHeight, Bounds.Height);
        }

        if (!Found || StandardWidth < 1.0 || StandardHeight < 1.0)
        {
            return;
        }

        foreach (Box Bounds in Boxes)
        {
            Bounds.Width = StandardWidth;
            Bounds.Height = StandardHeight;
        }
    }

    private static bool IsRectangle(string? Shape)
    {
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return true;
        }

        return string.Equals(Shape, "rect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "rectangle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "square", StringComparison.OrdinalIgnoreCase);
    }

    private static void SeparateOverlaps(List<Box> Boxes, bool Horizontal)
    {
        List<List<Box>> Ranks = BuildRanks(Boxes, Horizontal);
        foreach (List<Box> Rank in Ranks)
        {
            Rank.Sort((Left, Right) => Cross(Left, Horizontal).CompareTo(Cross(Right, Horizontal)));
            for (int Index = 1; Index < Rank.Count; Index++)
            {
                Box Previous = Rank[Index - 1];
                Box Current = Rank[Index];
                double Needed = (CrossSize(Previous, Horizontal) / 2.0)
                    + (CrossSize(Current, Horizontal) / 2.0)
                    + MIN_GAP_PX;
                double Have = Cross(Current, Horizontal) - Cross(Previous, Horizontal);
                if (Have < Needed)
                {
                    ShiftCross(Rank, Index, Needed - Have, Horizontal);
                }
            }
        }

        Ranks.Sort((Left, Right) => Main(Left[0], Horizontal).CompareTo(Main(Right[0], Horizontal)));
        for (int RankIndex = 1; RankIndex < Ranks.Count; RankIndex++)
        {
            double Deficit = 0;
            foreach (Box Earlier in Ranks[RankIndex - 1])
            {
                foreach (Box Later in Ranks[RankIndex])
                {
                    double Needed = (MainSize(Earlier, Horizontal) / 2.0)
                        + (MainSize(Later, Horizontal) / 2.0)
                        + BorderGapFloor(Earlier, Later, Horizontal, CrossAxis: false);
                    double Have = Main(Later, Horizontal) - Main(Earlier, Horizontal);
                    if (Have < Needed)
                    {
                        Deficit = Math.Max(Deficit, Needed - Have);
                    }
                }
            }

            if (Deficit > 0)
            {
                for (int LaterIndex = RankIndex; LaterIndex < Ranks.Count; LaterIndex++)
                {
                    foreach (Box Bounds in Ranks[LaterIndex])
                    {
                        SetMain(Bounds, Main(Bounds, Horizontal) + Deficit, Horizontal);
                    }
                }
            }
        }
    }

    private static List<List<Box>> BuildRanks(List<Box> Boxes, bool Horizontal)
    {
        List<Box> Ordered = new List<Box>(Boxes);
        Ordered.Sort((Left, Right) => Main(Left, Horizontal).CompareTo(Main(Right, Horizontal)));
        List<List<Box>> Ranks = new List<List<Box>>();
        foreach (Box Bounds in Ordered)
        {
            if (Ranks.Count == 0 || Math.Abs(Main(Bounds, Horizontal) - Main(Ranks[Ranks.Count - 1][0], Horizontal)) > RANK_TOLERANCE_PX)
            {
                Ranks.Add(new List<Box>());
            }

            Ranks[Ranks.Count - 1].Add(Bounds);
        }

        return Ranks;
    }

    private static void ShiftCross(List<Box> Rank, int From, double Delta, bool Horizontal)
    {
        for (int Index = From; Index < Rank.Count; Index++)
        {
            SetCross(Rank[Index], Cross(Rank[Index], Horizontal) + Delta, Horizontal);
        }
    }

    private static void ExpandClusters(DiagramContract Contract, List<Box> Boxes, List<Box> Clusters)
    {
        Dictionary<string, Box> Nodes = new Dictionary<string, Box>(StringComparer.Ordinal);
        foreach (Box Bounds in Boxes)
        {
            Nodes[Bounds.Id] = Bounds;
        }

        Dictionary<string, Box> ClusterById = new Dictionary<string, Box>(StringComparer.Ordinal);
        foreach (Box Bounds in Clusters)
        {
            ClusterById[Bounds.Id] = Bounds;
        }

        List<GroupContract> Ordered = InnermostFirst(Contract);
        foreach (GroupContract Group in Ordered)
        {
            if (!ClusterById.TryGetValue(Group.Id, out Box? Cluster))
            {
                continue;
            }

            double Left = double.PositiveInfinity;
            double Right = double.NegativeInfinity;
            double Top = double.PositiveInfinity;
            double Bottom = double.NegativeInfinity;
            bool Included = false;
            foreach (string ChildId in Group.ChildNodeIds)
            {
                if (Nodes.TryGetValue(ChildId, out Box? Child))
                {
                    Include(ref Left, ref Right, ref Top, ref Bottom, Child, CLUSTER_PAD_PX);
                    Included = true;
                }
            }

            foreach (string ChildGroupId in Group.ChildGroupIds)
            {
                if (ClusterById.TryGetValue(ChildGroupId, out Box? Child))
                {
                    Include(ref Left, ref Right, ref Top, ref Bottom, Child, CLUSTER_PAD_PX);
                    Included = true;
                }
            }

            if (!Included)
            {
                continue;
            }

            Cluster.CenterX = (Left + Right) / 2.0;
            Cluster.CenterY = (Top + Bottom) / 2.0;
            Cluster.Width = Math.Max(1.0, Right - Left);
            Cluster.Height = Math.Max(1.0, Bottom - Top);
        }
    }

    private static void Include(ref double Left, ref double Right, ref double Top, ref double Bottom, Box Child, double Pad)
    {
        Left = Math.Min(Left, Child.CenterX - (Child.Width / 2.0) - Pad);
        Right = Math.Max(Right, Child.CenterX + (Child.Width / 2.0) + Pad);
        Top = Math.Min(Top, Child.CenterY - (Child.Height / 2.0) - Pad);
        Bottom = Math.Max(Bottom, Child.CenterY + (Child.Height / 2.0) + Pad);
    }

    private static List<GroupContract> InnermostFirst(DiagramContract Contract)
    {
        Dictionary<string, int> Depth = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            Depth[Group.Id] = DepthOf(Contract, Group.Id);
        }

        List<GroupContract> Ordered = new List<GroupContract>(Contract.Groups);
        Ordered.Sort((Left, Right) => Depth[Right.Id].CompareTo(Depth[Left.Id]));
        return Ordered;
    }

    private static int DepthOf(DiagramContract Contract, string GroupId)
    {
        int Depth = 0;
        string? Parent = ParentOf(Contract, GroupId);
        while (Parent != null)
        {
            Depth++;
            Parent = ParentOf(Contract, Parent);
        }

        return Depth;
    }

    private static string? ParentOf(DiagramContract Contract, string GroupId)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (Group.Id == GroupId)
            {
                return Group.ParentGroupId;
            }
        }

        return null;
    }

    private static void RewriteEdgeEnds(
        FlowchartLayoutResult Copy,
        List<Box> Boxes,
        List<Box> Clusters,
        Dictionary<string, NodeContract> Nodes,
        Dictionary<string, EdgeContract> Edges)
    {
        Dictionary<string, Box> ById = IndexBoxes(Boxes, Clusters);
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count < 2 || !ById.TryGetValue(Route.SourceId, out Box? Source) || !ById.TryGetValue(Route.TargetId, out Box? Target))
            {
                continue;
            }

            List<FlowchartLayoutPoint> Kept = new List<FlowchartLayoutPoint>();
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                Kept.Add(new FlowchartLayoutPoint
                {
                    X = Point.X,
                    Y = Point.Y
                });
            }

            EdgeContract? Edge = Edges.TryGetValue(Route.EdgeId, out EdgeContract? Found) ? Found : null;
            string? SourceShape = ShapeOf(Nodes, Route.SourceId, Clusters);
            string? TargetShape = ShapeOf(Nodes, Route.TargetId, Clusters);
            EdgeEndpointMarker StartMarker = Edge?.MarkerStart ?? EdgeEndpointMarker.None;
            EdgeEndpointMarker EndMarker = Edge?.MarkerEnd ?? EdgeEndpointMarker.Point;
            for (int Index = 1; Index < Kept.Count - 1; Index++)
            {
                if (StrictlyInside(Source, Kept[Index].X, Kept[Index].Y))
                {
                    (double OutsideX, double OutsideY) = PushOutside(Source, SourceShape, Kept[Index].X, Kept[Index].Y);
                    Kept[Index].X = OutsideX;
                    Kept[Index].Y = OutsideY;
                }
                else if (StrictlyInside(Target, Kept[Index].X, Kept[Index].Y))
                {
                    (double OutsideX, double OutsideY) = PushOutside(Target, TargetShape, Kept[Index].X, Kept[Index].Y);
                    Kept[Index].X = OutsideX;
                    Kept[Index].Y = OutsideY;
                }
                else
                {
                    Kept[Index].X = Math.Round(Kept[Index].X);
                    Kept[Index].Y = Math.Round(Kept[Index].Y);
                }
            }

            (double StartX, double StartY) = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
                Source.ToBounds(),
                SourceShape,
                Kept[1].X,
                Kept[1].Y,
                StartMarker);
            (double EndX, double EndY) = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
                Target.ToBounds(),
                TargetShape,
                Kept[Kept.Count - 2].X,
                Kept[Kept.Count - 2].Y,
                EndMarker);
            Kept[0] = new FlowchartLayoutPoint { X = StartX, Y = StartY };
            Kept[Kept.Count - 1] = new FlowchartLayoutPoint { X = EndX, Y = EndY };
            Route.Points.Clear();
            Route.Points.AddRange(Kept);
        }
    }

    private static void SeparateStackedEnds(FlowchartLayoutResult Copy, List<Box> Boxes, List<Box> Clusters)
    {
        Dictionary<string, Box> ById = IndexBoxes(Boxes, Clusters);
        Dictionary<string, List<FlowchartLayoutEdgeRoute>> ByTarget = new Dictionary<string, List<FlowchartLayoutEdgeRoute>>(StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            if (!ByTarget.TryGetValue(Route.TargetId, out List<FlowchartLayoutEdgeRoute>? Group))
            {
                Group = new List<FlowchartLayoutEdgeRoute>();
                ByTarget[Route.TargetId] = Group;
            }

            Group.Add(Route);
        }

        foreach (KeyValuePair<string, List<FlowchartLayoutEdgeRoute>> Pair in ByTarget)
        {
            if (Pair.Value.Count < 2 || !ById.TryGetValue(Pair.Key, out Box? Target))
            {
                continue;
            }

            for (int Index = 1; Index < Pair.Value.Count; Index++)
            {
                FlowchartLayoutPoint Current = Pair.Value[Index].Points[Pair.Value[Index].Points.Count - 1];
                bool Stacked = false;
                for (int Earlier = 0; Earlier < Index; Earlier++)
                {
                    FlowchartLayoutPoint Other = Pair.Value[Earlier].Points[Pair.Value[Earlier].Points.Count - 1];
                    double Dx = Current.X - Other.X;
                    double Dy = Current.Y - Other.Y;
                    if ((Dx * Dx) + (Dy * Dy) < 100.0)
                    {
                        Stacked = true;
                        break;
                    }
                }

                if (!Stacked || IsAxisAligned(Pair.Value[Index]))
                {
                    continue;
                }

                SlideAlongFace(Target, Current, Index);
            }
        }

        Dictionary<string, List<FlowchartLayoutEdgeRoute>> BySource = new Dictionary<string, List<FlowchartLayoutEdgeRoute>>(StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            if (!BySource.TryGetValue(Route.SourceId, out List<FlowchartLayoutEdgeRoute>? Group))
            {
                Group = new List<FlowchartLayoutEdgeRoute>();
                BySource[Route.SourceId] = Group;
            }

            Group.Add(Route);
        }

        foreach (KeyValuePair<string, List<FlowchartLayoutEdgeRoute>> Pair in BySource)
        {
            if (Pair.Value.Count < 2 || !ById.TryGetValue(Pair.Key, out Box? Source))
            {
                continue;
            }

            for (int Index = 1; Index < Pair.Value.Count; Index++)
            {
                FlowchartLayoutPoint Current = Pair.Value[Index].Points[0];
                bool Stacked = false;
                for (int Earlier = 0; Earlier < Index; Earlier++)
                {
                    FlowchartLayoutPoint Other = Pair.Value[Earlier].Points[0];
                    double Dx = Current.X - Other.X;
                    double Dy = Current.Y - Other.Y;
                    if ((Dx * Dx) + (Dy * Dy) < 100.0)
                    {
                        Stacked = true;
                        break;
                    }
                }

                if (!Stacked || IsAxisAligned(Pair.Value[Index]))
                {
                    continue;
                }

                SlideAlongFace(Source, Current, Index);
            }
        }
    }

    private static bool IsAxisAligned(FlowchartLayoutEdgeRoute Route)
    {
        if (Route.Points.Count < 2)
        {
            return false;
        }

        FlowchartLayoutPoint Start = Route.Points[0];
        FlowchartLayoutPoint End = Route.Points[Route.Points.Count - 1];
        return Math.Abs(Start.X - End.X) <= 1.0 || Math.Abs(Start.Y - End.Y) <= 1.0;
    }

    private static void NudgeOffSharedRuns(FlowchartLayoutResult Copy, List<Box> Boxes, List<Box> Clusters)
    {
        Dictionary<string, Box> ById = IndexBoxes(Boxes, Clusters);
        List<FlowchartLayoutEdgeRoute> Routes = new List<FlowchartLayoutEdgeRoute>();
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count >= 2)
            {
                Routes.Add(Route);
            }
        }

        for (int Left = 0; Left < Routes.Count; Left++)
        {
            for (int Right = Left + 1; Right < Routes.Count; Right++)
            {
                bool SameTarget = string.Equals(Routes[Left].TargetId, Routes[Right].TargetId, StringComparison.Ordinal);
                if (!SameTarget && !SharesARun(Routes[Right], Routes[Left]))
                {
                    continue;
                }

                FlowchartLayoutEdgeRoute Mover = Routes[Right];
                FlowchartLayoutEdgeRoute Stay = Routes[Left];
                if (IsAxisAligned(Mover) && !IsAxisAligned(Stay))
                {
                    Mover = Stay;
                    Stay = Routes[Right];
                }

                OffsetRun(Mover, Stay, ById);
            }
        }
    }

    private static bool SharesARun(FlowchartLayoutEdgeRoute Left, FlowchartLayoutEdgeRoute Right)
    {
        for (int Index = 1; Index < Left.Points.Count; Index++)
        {
            double MidX = (Left.Points[Index - 1].X + Left.Points[Index].X) / 2.0;
            double MidY = (Left.Points[Index - 1].Y + Left.Points[Index].Y) / 2.0;
            if (ClosestPoint(Right, MidX, MidY, out _, out _, out double Distance) && Distance < 8.0)
            {
                double Run = Math.Abs(Left.Points[Index].X - Left.Points[Index - 1].X)
                    + Math.Abs(Left.Points[Index].Y - Left.Points[Index - 1].Y);
                if (Run >= 20.0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void OffsetRun(
        FlowchartLayoutEdgeRoute Mover,
        FlowchartLayoutEdgeRoute Other,
        Dictionary<string, Box> ById)
    {
        FlowchartLayoutPoint Start = Mover.Points[0];
        FlowchartLayoutPoint End = Mover.Points[Mover.Points.Count - 1];
        double TravelX = End.X - Start.X;
        double TravelY = End.Y - Start.Y;
        double Travel = Math.Sqrt((TravelX * TravelX) + (TravelY * TravelY));
        if (Travel < 1.0)
        {
            return;
        }

        double SideX = -TravelY / Travel;
        double SideY = TravelX / Travel;
        double MidX = (Start.X + End.X) / 2.0;
        double MidY = (Start.Y + End.Y) / 2.0;
        if (ClosestPoint(Other, MidX, MidY, out double NearX, out double NearY, out _))
        {
            double AwayX = MidX - NearX;
            double AwayY = MidY - NearY;
            if ((AwayX * SideX) + (AwayY * SideY) < 0.0)
            {
                SideX = -SideX;
                SideY = -SideY;
            }
        }

        const double OffsetPx = 20.0;
        if (Mover.Points.Count == 2)
        {
            Mover.Points.Insert(1, new FlowchartLayoutPoint
            {
                X = MidX + (SideX * OffsetPx),
                Y = MidY + (SideY * OffsetPx)
            });
        }

        for (int Index = 1; Index < Mover.Points.Count; Index++)
        {
            Mover.Points[Index].X += SideX * OffsetPx;
            Mover.Points[Index].Y += SideY * OffsetPx;
        }

        if (ById.TryGetValue(Mover.TargetId, out Box? Target))
        {
            FlowchartLayoutPoint Tip = Mover.Points[Mover.Points.Count - 1];
            (double X, double Y) = ProjectOntoOutline(Target, Tip.X, Tip.Y);
            Tip.X = X;
            Tip.Y = Y;
        }
    }

    private static (double X, double Y) ProjectOntoOutline(Box Bounds, double X, double Y)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        double ClampX = Math.Clamp(X, Left, Right);
        double ClampY = Math.Clamp(Y, Bottom, Top);
        double DistLeft = Math.Abs(ClampX - Left);
        double DistRight = Math.Abs(ClampX - Right);
        double DistBottom = Math.Abs(ClampY - Bottom);
        double DistTop = Math.Abs(ClampY - Top);
        double Nearest = Math.Min(Math.Min(DistLeft, DistRight), Math.Min(DistBottom, DistTop));
        if (Nearest == DistLeft)
        {
            ClampX = Left;
        }
        else if (Nearest == DistRight)
        {
            ClampX = Right;
        }
        else if (Nearest == DistBottom)
        {
            ClampY = Bottom;
        }
        else
        {
            ClampY = Top;
        }

        return (ClampX, ClampY);
    }

    private static void NudgeInteriorAway(FlowchartLayoutEdgeRoute Mover, FlowchartLayoutEdgeRoute Other, double ClearancePx)
    {
        bool Close = false;
        for (int Index = 0; Index < Mover.Points.Count; Index++)
        {
            FlowchartLayoutPoint Point = Mover.Points[Index];
            if (ClosestPoint(Other, Point.X, Point.Y, out _, out _, out double Distance) && Distance < 8.0)
            {
                Close = true;
                break;
            }

            if (Index == 0)
            {
                continue;
            }

            double MidX = (Mover.Points[Index - 1].X + Point.X) / 2.0;
            double MidY = (Mover.Points[Index - 1].Y + Point.Y) / 2.0;
            if (ClosestPoint(Other, MidX, MidY, out _, out _, out Distance) && Distance < 8.0)
            {
                Close = true;
                break;
            }
        }

        if (!Close)
        {
            return;
        }

        if (Mover.Points.Count == 2)
        {
            FlowchartLayoutPoint Start = Mover.Points[0];
            FlowchartLayoutPoint End = Mover.Points[1];
            Mover.Points.Insert(1, new FlowchartLayoutPoint
            {
                X = (Start.X + End.X) / 2.0,
                Y = (Start.Y + End.Y) / 2.0
            });
        }

        for (int Index = 1; Index < Mover.Points.Count - 1; Index++)
        {
            FlowchartLayoutPoint Point = Mover.Points[Index];
            if (!ClosestPoint(Other, Point.X, Point.Y, out double NearX, out double NearY, out double Distance) || Distance >= 8.0)
            {
                continue;
            }

            double DirX = Point.X - NearX;
            double DirY = Point.Y - NearY;
            double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
            if (Length < 1e-3)
            {
                DirX = -(Point.Y - Mover.Points[Index - 1].Y);
                DirY = Point.X - Mover.Points[Index - 1].X;
                Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
                if (Length < 1e-3)
                {
                    continue;
                }
            }

            double Push = ClearancePx - Distance;
            Point.X += DirX / Length * Push;
            Point.Y += DirY / Length * Push;
        }
    }

    private static bool ClosestPoint(
        FlowchartLayoutEdgeRoute Route,
        double X,
        double Y,
        out double NearX,
        out double NearY,
        out double Distance)
    {
        NearX = 0.0;
        NearY = 0.0;
        Distance = double.MaxValue;
        bool Found = false;
        for (int Index = 1; Index < Route.Points.Count; Index++)
        {
            FlowchartLayoutPoint Start = Route.Points[Index - 1];
            FlowchartLayoutPoint End = Route.Points[Index];
            double SegX = End.X - Start.X;
            double SegY = End.Y - Start.Y;
            double LengthSquared = (SegX * SegX) + (SegY * SegY);
            double T = 0.0;
            if (LengthSquared > 1e-6)
            {
                T = (((X - Start.X) * SegX) + ((Y - Start.Y) * SegY)) / LengthSquared;
                T = Math.Clamp(T, 0.0, 1.0);
            }

            double CandidateX = Start.X + (SegX * T);
            double CandidateY = Start.Y + (SegY * T);
            double Dx = X - CandidateX;
            double Dy = Y - CandidateY;
            double CandidateDistance = Math.Sqrt((Dx * Dx) + (Dy * Dy));
            if (CandidateDistance < Distance)
            {
                Distance = CandidateDistance;
                NearX = CandidateX;
                NearY = CandidateY;
                Found = true;
            }
        }

        return Found;
    }

    private static void SlideAlongFace(Box Bounds, FlowchartLayoutPoint Point, int Index)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        double DistLeft = Math.Abs(Point.X - Left);
        double DistRight = Math.Abs(Point.X - Right);
        double DistBottom = Math.Abs(Point.Y - Bottom);
        double DistTop = Math.Abs(Point.Y - Top);
        double Shift = Index * 12.0;
        bool HorizontalFace = DistTop <= DistLeft && DistTop <= DistRight && DistTop <= DistBottom
            || DistBottom < DistTop && DistBottom <= DistLeft && DistBottom <= DistRight;
        if (HorizontalFace)
        {
            double Direction = Point.X < Bounds.CenterX ? 1.0 : -1.0;
            Point.X = Math.Clamp(Point.X + (Direction * Shift), Left + 1.0, Right - 1.0);
            return;
        }

        double VerticalDirection = Point.Y < Bounds.CenterY ? 1.0 : -1.0;
        Point.Y = Math.Clamp(Point.Y + (VerticalDirection * Shift), Bottom + 1.0, Top - 1.0);
    }

    private static void PullRectangleCorners(
        FlowchartLayoutResult Copy,
        List<Box> Boxes,
        List<Box> Clusters,
        Dictionary<string, NodeContract> Nodes)
    {
        Dictionary<string, Box> ById = IndexBoxes(Boxes, Clusters);
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            if (IsAxisAligned(Route))
            {
                continue;
            }

            if (ById.TryGetValue(Route.SourceId, out Box? Source))
            {
                (double X, double Y) = PullInFromRectangleCorner(
                    Source,
                    ShapeOf(Nodes, Route.SourceId, Clusters),
                    Route.Points[0].X,
                    Route.Points[0].Y);
                Route.Points[0].X = X;
                Route.Points[0].Y = Y;
            }

            if (ById.TryGetValue(Route.TargetId, out Box? Target))
            {
                int Last = Route.Points.Count - 1;
                (double X, double Y) = PullInFromRectangleCorner(
                    Target,
                    ShapeOf(Nodes, Route.TargetId, Clusters),
                    Route.Points[Last].X,
                    Route.Points[Last].Y);
                Route.Points[Last].X = X;
                Route.Points[Last].Y = Y;
            }
        }
    }

    private static (double X, double Y) PullInFromRectangleCorner(Box Bounds, string? Shape, double X, double Y)
    {
        if (!IsRectangle(Shape))
        {
            return (X, Y);
        }

        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        double DistLeft = Math.Abs(X - Left);
        double DistRight = Math.Abs(X - Right);
        double DistBottom = Math.Abs(Y - Bottom);
        double DistTop = Math.Abs(Y - Top);
        double NearestEdge = Math.Min(Math.Min(DistLeft, DistRight), Math.Min(DistBottom, DistTop));
        if (NearestEdge > 1.5)
        {
            return (X, Y);
        }

        bool VerticalFace = DistLeft <= DistBottom && DistLeft <= DistTop && DistLeft <= DistRight
            || DistRight < DistLeft && DistRight <= DistBottom && DistRight <= DistTop;
        double FaceLength = VerticalFace ? Bounds.Height : Bounds.Width;
        if (FaceLength < CORNER_FACE_MIN_PX)
        {
            return (X, Y);
        }

        double Limit = FaceLength >= WIDE_FACE_PX ? WIDE_CORNER_CLEARANCE_PX : CORNER_CLEARANCE_PX;
        if (VerticalFace)
        {
            double Low = Bottom + Limit;
            double High = Top - Limit;
            if (Low <= High)
            {
                Y = Math.Clamp(Y, Low, High);
            }
        }
        else
        {
            double Low = Left + Limit;
            double High = Right - Limit;
            if (Low <= High)
            {
                X = Math.Clamp(X, Low, High);
            }
        }

        return (X, Y);
    }

    private static string? ShapeOf(Dictionary<string, NodeContract> Nodes, string Id, List<Box> Clusters)
    {
        if (Nodes.TryGetValue(Id, out NodeContract? Node))
        {
            return Node.Shape;
        }

        foreach (Box Cluster in Clusters)
        {
            if (Cluster.Id == Id)
            {
                return "rect";
            }
        }

        return null;
    }

    private static (double X, double Y) PushOutside(Box Bounds, string? Shape, double X, double Y)
    {
        double DirX = X - Bounds.CenterX;
        double DirY = Y - Bounds.CenterY;
        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length < 1e-6)
        {
            DirX = 0;
            DirY = 1;
            Length = 1;
        }

        double TowardX = Bounds.CenterX + ((DirX / Length) * 1000.0);
        double TowardY = Bounds.CenterY + ((DirY / Length) * 1000.0);
        (double EdgeX, double EdgeY) = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
            Bounds.ToBounds(),
            Shape,
            TowardX,
            TowardY,
            EdgeEndpointMarker.None);
        double OutX = EdgeX - Bounds.CenterX;
        double OutY = EdgeY - Bounds.CenterY;
        double OutLength = Math.Sqrt((OutX * OutX) + (OutY * OutY));
        if (OutLength < 1e-6)
        {
            return (EdgeX, EdgeY);
        }

        return (EdgeX + (OutX / OutLength), EdgeY + (OutY / OutLength));
    }

    private static bool StrictlyInside(Box Bounds, double X, double Y)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Top = Bounds.CenterY - (Bounds.Height / 2.0);
        double Bottom = Bounds.CenterY + (Bounds.Height / 2.0);
        return X > Left && X < Right && Y > Top && Y < Bottom;
    }

    private static void RoundBoxes(List<Box> Boxes)
    {
        foreach (Box Bounds in Boxes)
        {
            Bounds.CenterX = Math.Round(Bounds.CenterX);
            Bounds.CenterY = Math.Round(Bounds.CenterY);
            Bounds.Width = Math.Max(1.0, Math.Round(Bounds.Width));
            Bounds.Height = Math.Max(1.0, Math.Round(Bounds.Height));
        }
    }

    private static double Extent(List<Box> Boxes, List<Box> Clusters, FlowchartLayoutResult Copy)
    {
        double Max = 0;
        foreach (Box Bounds in Boxes)
        {
            Max = Math.Max(Max, Bounds.CenterY + (Bounds.Height / 2.0));
        }

        foreach (Box Bounds in Clusters)
        {
            Max = Math.Max(Max, Bounds.CenterY + (Bounds.Height / 2.0));
        }

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                Max = Math.Max(Max, Point.Y);
            }
        }

        return Max;
    }

    private static void FlipToYUp(List<Box> Boxes, List<Box> Clusters, FlowchartLayoutResult Copy, double Flip)
    {
        foreach (Box Bounds in Boxes)
        {
            Bounds.CenterY = Flip - Bounds.CenterY;
        }

        foreach (Box Bounds in Clusters)
        {
            Bounds.CenterY = Flip - Bounds.CenterY;
        }

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                Point.Y = Flip - Point.Y;
            }
        }
    }

    private static FlowchartLayoutResult CopyResult(FlowchartLayoutResult Source)
    {
        FlowchartLayoutResult Copy = new FlowchartLayoutResult
        {
            GraphWidth = Source.GraphWidth,
            GraphHeight = Source.GraphHeight
        };
        foreach (FlowchartLayoutNodeBounds Bounds in Source.NodeBounds)
        {
            Copy.NodeBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX,
                CenterY = Bounds.CenterY,
                Width = Bounds.Width,
                Height = Bounds.Height
            });
        }

        foreach (FlowchartLayoutNodeBounds Bounds in Source.ClusterBounds)
        {
            Copy.ClusterBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX,
                CenterY = Bounds.CenterY,
                Width = Bounds.Width,
                Height = Bounds.Height
            });
        }

        foreach (FlowchartLayoutEdgeRoute Route in Source.EdgeRoutes)
        {
            FlowchartLayoutEdgeRoute Clone = new FlowchartLayoutEdgeRoute
            {
                EdgeId = Route.EdgeId,
                SourceId = Route.SourceId,
                TargetId = Route.TargetId,
                ExitSide = Route.ExitSide,
                EntrySide = Route.EntrySide
            };
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                Clone.Points.Add(new FlowchartLayoutPoint
                {
                    X = Point.X,
                    Y = Point.Y
                });
            }

            Copy.EdgeRoutesMutable.Add(Clone);
        }

        return Copy;
    }

    private static void WriteBoxes(List<FlowchartLayoutNodeBounds> Target, List<Box> Boxes)
    {
        Target.Clear();
        foreach (Box Bounds in Boxes)
        {
            Target.Add(Bounds.ToBounds());
        }
    }

    private static Dictionary<string, Box> IndexBoxes(List<Box> Boxes, List<Box> Clusters)
    {
        Dictionary<string, Box> ById = new Dictionary<string, Box>(StringComparer.Ordinal);
        foreach (Box Bounds in Boxes)
        {
            ById[Bounds.Id] = Bounds;
        }

        foreach (Box Bounds in Clusters)
        {
            ById[Bounds.Id] = Bounds;
        }

        return ById;
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

    private static Dictionary<string, EdgeContract> IndexEdges(DiagramContract Contract)
    {
        Dictionary<string, EdgeContract> Edges = new Dictionary<string, EdgeContract>(StringComparer.Ordinal);
        int Index = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            string Name = string.IsNullOrEmpty(Edge.Id) ? "e" + Index.ToString() : Edge.Id;
            Edges[Name] = Edge;
            Index++;
        }

        return Edges;
    }

    private static double Main(Box Bounds, bool Horizontal)
    {
        if (Horizontal)
        {
            return Bounds.CenterX;
        }

        return Bounds.CenterY;
    }

    private static double Cross(Box Bounds, bool Horizontal)
    {
        if (Horizontal)
        {
            return Bounds.CenterY;
        }

        return Bounds.CenterX;
    }

    private static double MainSize(Box Bounds, bool Horizontal)
    {
        if (Horizontal)
        {
            return Bounds.Width;
        }

        return Bounds.Height;
    }

    private static double CrossSize(Box Bounds, bool Horizontal)
    {
        if (Horizontal)
        {
            return Bounds.Height;
        }

        return Bounds.Width;
    }

    private static void SetMain(Box Bounds, double Value, bool Horizontal)
    {
        if (Horizontal)
        {
            Bounds.CenterX = Value;
        }
        else
        {
            Bounds.CenterY = Value;
        }
    }

    private static void SetCross(Box Bounds, double Value, bool Horizontal)
    {
        if (Horizontal)
        {
            Bounds.CenterY = Value;
        }
        else
        {
            Bounds.CenterX = Value;
        }
    }

    /// <summary>
    /// Keeps the border gap that dagre left before boxes grew to a shared size.
    /// </summary>
    private static double BorderGapFloor(Box Earlier, Box Later, bool Horizontal, bool CrossAxis)
    {
        double EarlierCentre = CrossAxis ? OriginalCross(Earlier, Horizontal) : OriginalMain(Earlier, Horizontal);
        double LaterCentre = CrossAxis ? OriginalCross(Later, Horizontal) : OriginalMain(Later, Horizontal);
        double EarlierSize = CrossAxis ? OriginalCrossSize(Earlier, Horizontal) : OriginalMainSize(Earlier, Horizontal);
        double LaterSize = CrossAxis ? OriginalCrossSize(Later, Horizontal) : OriginalMainSize(Later, Horizontal);
        double OriginalGap = (LaterCentre - (LaterSize / 2.0)) - (EarlierCentre + (EarlierSize / 2.0));
        return Math.Max(MIN_GAP_PX, OriginalGap);
    }

    private static double OriginalMain(Box Bounds, bool Horizontal)
    {
        if (Horizontal)
        {
            return Bounds.OriginalCenterX;
        }

        return Bounds.OriginalCenterY;
    }

    private static double OriginalCross(Box Bounds, bool Horizontal)
    {
        if (Horizontal)
        {
            return Bounds.OriginalCenterY;
        }

        return Bounds.OriginalCenterX;
    }

    private static double OriginalMainSize(Box Bounds, bool Horizontal)
    {
        if (Horizontal)
        {
            return Bounds.OriginalWidth;
        }

        return Bounds.OriginalHeight;
    }

    private static double OriginalCrossSize(Box Bounds, bool Horizontal)
    {
        if (Horizontal)
        {
            return Bounds.OriginalHeight;
        }

        return Bounds.OriginalWidth;
    }

    private sealed class Box
    {
        public string Id { get; set; } = string.Empty;

        public double CenterX { get; set; }

        public double CenterY { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double OriginalCenterX { get; set; }

        public double OriginalCenterY { get; set; }

        public double OriginalWidth { get; set; }

        public double OriginalHeight { get; set; }

        public static Box From(FlowchartLayoutNodeBounds Bounds)
        {
            return new Box
            {
                Id = Bounds.NodeId,
                CenterX = Bounds.CenterX,
                CenterY = Bounds.CenterY,
                Width = Bounds.Width,
                Height = Bounds.Height,
                OriginalCenterX = Bounds.CenterX,
                OriginalCenterY = Bounds.CenterY,
                OriginalWidth = Bounds.Width,
                OriginalHeight = Bounds.Height
            };
        }

        public FlowchartLayoutNodeBounds ToBounds()
        {
            return new FlowchartLayoutNodeBounds
            {
                NodeId = Id,
                CenterX = CenterX,
                CenterY = CenterY,
                Width = Width,
                Height = Height
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Track separation and obstacle-clearing helpers for route repair.</summary>
public static partial class DiagramGridLayout
{    internal static void SeparateTracks(FlowchartLayoutResult Copy, List<GridNode> Placed, DiagramContract? Contract = null)
    {
        List<FlowchartLayoutEdgeRoute> Routes = new List<FlowchartLayoutEdgeRoute>();
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count >= 2)
            {
                Routes.Add(Route);
            }
        }

        Routes.Sort(delegate(FlowchartLayoutEdgeRoute Left, FlowchartLayoutEdgeRoute Right)
        {
            int Compare = Left.Points[0].X.CompareTo(Right.Points[0].X);
            if (Compare != 0)
            {
                return Compare;
            }

            return Left.Points[0].Y.CompareTo(Right.Points[0].Y);
        });

        for (int LeftIndex = 0; LeftIndex < Routes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Routes.Count; RightIndex++)
            {
                NudgeOffSharedRun(Routes[LeftIndex], Routes[RightIndex], Placed, Contract);
            }
        }
    }

    private static void NudgeOffSharedRun(FlowchartLayoutEdgeRoute Stay, FlowchartLayoutEdgeRoute Mover, List<GridNode> Placed, DiagramContract? Contract)
    {
        if (Contract != null && FindContractEdge(Contract, Mover)?.RoutingHint?.IsActive == true) return;
        if (Mover.Points.Count == 2)
        {
            FlowchartLayoutPoint Start = Mover.Points[0];
            FlowchartLayoutPoint End = Mover.Points[1];
            bool Vertical = Math.Abs(Start.X - End.X) < 0.5;
            bool Horizontal = Math.Abs(Start.Y - End.Y) < 0.5;
            if (!Vertical && !Horizontal) return;
            double TrackSeparation = Math.Max(DiagramGridMetrics.GRID_UNIT_PX, FlowchartLayoutRules.LineSpacing);
            FlowchartLayoutPoint OtherStart = Stay.Points[0];
            FlowchartLayoutPoint OtherEnd = Stay.Points[^1];
            bool Parallel = Vertical
                ? Math.Abs(OtherStart.X - OtherEnd.X) < 0.5
                : Math.Abs(OtherStart.Y - OtherEnd.Y) < 0.5;
            double Gap = Vertical ? Start.X - OtherStart.X : Start.Y - OtherStart.Y;
            double Overlap = FlowchartBranchRouteOrder.Intersections(
                new[] { (Start.X, Start.Y), (End.X, End.Y) },
                Stay.Points.Select(Point => (Point.X, Point.Y)).ToArray()).Overlap;
            if (!Parallel || Overlap <= 0 || Math.Abs(Gap) >= TrackSeparation) return;
            double Offset = (Gap < 0 ? -TrackSeparation : TrackSeparation) - Gap;
            var Obstacles = Contract == null ? Placed : Placed.Where(Node =>
                !FlowchartCompoundEndpointHelper.Contains(Contract, Node.Id, Mover.SourceId)
                && !FlowchartCompoundEndpointHelper.Contains(Contract, Node.Id, Mover.TargetId)).ToList();
            if (!TryTranslateWholeOrthogonalRoute(Mover, Vertical, Offset, Obstacles))
                TryTranslateWholeOrthogonalRoute(Mover, Vertical, -Offset, Obstacles);
            return;
        }

        int FirstIndex = Mover.Points.Count == 3 ? 0 : 1;
        for (int Index = FirstIndex; Index < Mover.Points.Count - 1; Index++)
        {
            FlowchartLayoutPoint Start = Mover.Points[Index];
            FlowchartLayoutPoint End = Mover.Points[Index + 1];
            bool Vertical = Math.Abs(Start.X - End.X) < 0.5;
            bool Horizontal = Math.Abs(Start.Y - End.Y) < 0.5;
            if (!Vertical && !Horizontal)
            {
                continue;
            }
            double TrackSeparation = Math.Max(DiagramGridMetrics.GRID_UNIT_PX, FlowchartLayoutRules.LineSpacing);
            if (Contract != null)
            {
                var Options = FlowchartRenderOptions.FromContract(Contract);
                double CaptionHalf(FlowchartLayoutEdgeRoute Route)
                {
                    var Edge = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId);
                    if (string.IsNullOrWhiteSpace(Edge?.Label)) return 0;
                    var Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label, Options, Options.EdgeLabelFontSizePx);
                    return (Vertical ? Size.Width : Size.Height) / 2;
                }
                TrackSeparation = DiagramGridMetrics.CeilToGrid((int)Math.Ceiling(Math.Max(
                    FlowchartLayoutRules.LineSpacing, CaptionHalf(Stay) + CaptionHalf(Mover) + 6)));
            }

            for (int OtherIndex = 1; OtherIndex < Stay.Points.Count; OtherIndex++)
            {
                FlowchartLayoutPoint OtherStart = Stay.Points[OtherIndex - 1];
                FlowchartLayoutPoint OtherEnd = Stay.Points[OtherIndex];
                bool Parallel = Vertical
                    ? Math.Abs(OtherStart.X - OtherEnd.X) < 0.5
                    : Math.Abs(OtherStart.Y - OtherEnd.Y) < 0.5;
                double Gap = Vertical ? Start.X - OtherStart.X : Start.Y - OtherStart.Y;
                double Overlap = Vertical
                    ? Math.Min(Math.Max(Start.Y, End.Y), Math.Max(OtherStart.Y, OtherEnd.Y))
                        - Math.Max(Math.Min(Start.Y, End.Y), Math.Min(OtherStart.Y, OtherEnd.Y))
                    : Math.Min(Math.Max(Start.X, End.X), Math.Max(OtherStart.X, OtherEnd.X))
                        - Math.Max(Math.Min(Start.X, End.X), Math.Min(OtherStart.X, OtherEnd.X));
                if (!Parallel || Overlap <= 0 || Math.Abs(Gap) >= TrackSeparation)
                {
                    continue;
                }

                double Offset = (Gap < 0 ? -TrackSeparation : TrackSeparation) - Gap;
                var Obstacles = Contract == null ? Placed : Placed.Where(Node =>
                    !FlowchartCompoundEndpointHelper.Contains(Contract, Node.Id, Mover.SourceId)
                    && !FlowchartCompoundEndpointHelper.Contains(Contract, Node.Id, Mover.TargetId)).ToList();
                if (!TryTranslateInternalRun(Mover, Index, Vertical, Offset, Obstacles))
                {
                    TryTranslateInternalRun(Mover, Index, Vertical, -Offset, Obstacles);
                }
            }
        }
    }

    // A bend belongs to both adjoining runs. Moving just one point creates a
    // diagonal, so only translate a complete internal run between two elbows.
    internal static bool TryTranslateInternalRun(
        FlowchartLayoutEdgeRoute Route, int Index, bool Vertical, double Offset, List<GridNode> Placed)
    {
        if (Route.Points.Count == 3 && Index == 0)
            return TryTranslateTerminalRunPair(Route, Vertical, Offset, Placed);
        if (Index < 1 || Index + 2 >= Route.Points.Count)
        {
            return false;
        }

        FlowchartLayoutPoint Before = Route.Points[Index - 1];
        FlowchartLayoutPoint Start = Route.Points[Index];
        FlowchartLayoutPoint End = Route.Points[Index + 1];
        FlowchartLayoutPoint After = Route.Points[Index + 2];
        if (Vertical
            ? Math.Abs(Start.X - End.X) > 0.5 || Math.Abs(Before.Y - Start.Y) > 0.5 || Math.Abs(End.Y - After.Y) > 0.5
            : Math.Abs(Start.Y - End.Y) > 0.5 || Math.Abs(Before.X - Start.X) > 0.5 || Math.Abs(End.X - After.X) > 0.5)
        {
            return false;
        }

        double OldStart = Vertical ? Start.X : Start.Y;
        double OldEnd = Vertical ? End.X : End.Y;
        if (Vertical)
        {
            Start.X += Offset;
            End.X += Offset;
        }
        else
        {
            Start.Y += Offset;
            End.Y += Offset;
        }

        // Translating a track must not reverse or erase either terminal run.
        // The endpoint itself stays fixed, so crossing it would create a hook.
        double BeforeRun = Vertical ? Start.X - Before.X : Start.Y - Before.Y;
        double AfterRun = Vertical ? End.X - After.X : End.Y - After.Y;
        double OldBeforeRun = OldStart - (Vertical ? Before.X : Before.Y);
        double OldAfterRun = OldEnd - (Vertical ? After.X : After.Y);
        bool Clear = BeforeRun * OldBeforeRun > 0 && AfterRun * OldAfterRun > 0
            && Math.Abs(BeforeRun) >= DiagramGridRouteBuilder.CORNER_RADIUS_PX
            && Math.Abs(AfterRun) >= DiagramGridRouteBuilder.CORNER_RADIUS_PX;
        for (int Segment = Index; Segment <= Index + 2 && Clear; Segment++)
        {
            foreach (GridNode Node in Placed)
            {
                if (Node.Id == Route.SourceId || Node.Id == Route.TargetId)
                {
                    continue;
                }

                FlowchartLayoutNodeBounds Bounds = new()
                {
                    CenterX = Node.X, CenterY = Node.Y, Width = Node.Width, Height = Node.Height
                };
                FlowchartLayoutPoint A = Route.Points[Segment - 1];
                FlowchartLayoutPoint B = Route.Points[Segment];
                if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Bounds))
                {
                    Clear = false;
                    break;
                }
            }
        }

        if (!Clear)
        {
            if (Vertical) { Start.X = OldStart; End.X = OldEnd; }
            else { Start.Y = OldStart; End.Y = OldEnd; }
        }

        return Clear;
    }

    /// <summary>Translates every point on a straight two-point route perpendicular to its run.</summary>
    internal static bool TryTranslateWholeOrthogonalRoute(FlowchartLayoutEdgeRoute Route, bool Vertical, double Offset,
        List<GridNode> Placed)
    {
        if (Route.Points.Count != 2) return false;
        var Saved = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
        foreach (var Point in Route.Points)
        {
            if (Vertical) Point.X += Offset;
            else Point.Y += Offset;
        }

        bool Clear = true;
        for (int Segment = 1; Segment <= Route.Points.Count && Clear; Segment++)
        {
            foreach (GridNode Node in Placed)
            {
                if (Node.Id == Route.SourceId || Node.Id == Route.TargetId) continue;
                FlowchartLayoutNodeBounds Bounds = new()
                {
                    CenterX = Node.X, CenterY = Node.Y, Width = Node.Width, Height = Node.Height
                };
                FlowchartLayoutPoint A = Route.Points[Segment - 1];
                FlowchartLayoutPoint B = Route.Points[Segment];
                if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Bounds))
                {
                    Clear = false;
                    break;
                }
            }
        }

        if (!Clear)
        {
            Route.Points.Clear();
            foreach ((double X, double Y) in Saved) Route.Points.Add(new() { X = X, Y = Y });
        }

        return Clear;
    }

    /// <summary>Moves the first interior leg of a three-point route without breaking the fixed target port.</summary>
    private static bool TryTranslateTerminalRunPair(FlowchartLayoutEdgeRoute Route, bool Vertical, double Offset,
        List<GridNode> Placed)
    {
        if (Route.Points.Count != 3) return false;
        FlowchartLayoutPoint Start = Route.Points[0];
        FlowchartLayoutPoint Elbow = Route.Points[1];
        FlowchartLayoutPoint Target = Route.Points[2];
        if (Vertical
            ? Math.Abs(Start.X - Elbow.X) > 0.5 || Math.Abs(Elbow.Y - Target.Y) > 0.5
            : Math.Abs(Start.Y - Elbow.Y) > 0.5 || Math.Abs(Elbow.X - Target.X) > 0.5)
            return false;
        double OldStart = Vertical ? Start.X : Start.Y;
        double OldElbow = Vertical ? Elbow.X : Elbow.Y;
        if (Vertical) { Start.X += Offset; Elbow.X += Offset; }
        else { Start.Y += Offset; Elbow.Y += Offset; }
        double BeforeRun = Vertical ? Elbow.X - Start.X : Elbow.Y - Start.Y;
        double AfterRun = Vertical ? Target.X - Elbow.X : Target.Y - Elbow.Y;
        bool Clear = Math.Abs(BeforeRun) >= DiagramGridRouteBuilder.CORNER_RADIUS_PX
            && Math.Abs(AfterRun) >= DiagramGridRouteBuilder.CORNER_RADIUS_PX;
        for (int Segment = 0; Segment <= 2 && Clear; Segment++)
        {
            foreach (GridNode Node in Placed)
            {
                if (Node.Id == Route.SourceId || Node.Id == Route.TargetId) continue;
                FlowchartLayoutNodeBounds Bounds = new()
                {
                    CenterX = Node.X, CenterY = Node.Y, Width = Node.Width, Height = Node.Height
                };
                FlowchartLayoutPoint A = Route.Points[Segment];
                FlowchartLayoutPoint B = Route.Points[Segment + 1];
                if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Bounds))
                {
                    Clear = false;
                    break;
                }
            }
        }

        if (!Clear)
        {
            if (Vertical) { Start.X = OldStart; Elbow.X = OldElbow; }
            else { Start.Y = OldStart; Elbow.Y = OldElbow; }
        }

        return Clear;
    }

    private static bool InsideAny(List<GridNode> Placed, double X, double Y)
    {
        foreach (GridNode Node in Placed)
        {
            if (X > Node.X - (Node.Width / 2.0) && X < Node.X + (Node.Width / 2.0)
                && Y > Node.Y - (Node.Height / 2.0) && Y < Node.Y + (Node.Height / 2.0))
            {
                return true;
            }
        }

        return false;
    }

    private static double DistanceToRoute(double X, double Y, FlowchartLayoutEdgeRoute Route)
    {
        double Best = double.MaxValue;
        for (int Index = 1; Index < Route.Points.Count; Index++)
        {
            Best = Math.Min(Best, DistanceToSegment(X, Y, Route.Points[Index - 1], Route.Points[Index]));
        }

        return Best;
    }

    private static double DistanceToSegment(double X, double Y, FlowchartLayoutPoint Start, FlowchartLayoutPoint End)
    {
        double Dx = End.X - Start.X;
        double Dy = End.Y - Start.Y;
        double LengthSquared = (Dx * Dx) + (Dy * Dy);
        if (LengthSquared < 1e-6)
        {
            double EndDx = X - Start.X;
            double EndDy = Y - Start.Y;
            return Math.Sqrt((EndDx * EndDx) + (EndDy * EndDy));
        }

        double T = (((X - Start.X) * Dx) + ((Y - Start.Y) * Dy)) / LengthSquared;
        T = Math.Clamp(T, 0.0, 1.0);
        double NearX = Start.X + (T * Dx);
        double NearY = Start.Y + (T * Dy);
        double GapX = X - NearX;
        double GapY = Y - NearY;
        return Math.Sqrt((GapX * GapX) + (GapY * GapY));
    }

    private static void PushPointsOutOfNodes(FlowchartLayoutResult Copy, List<GridNode> Placed)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            for (int Index = 1; Index < Route.Points.Count - 1; Index++)
            {
                FlowchartLayoutPoint Point = Route.Points[Index];
                foreach (GridNode Node in Placed)
                {
                    if (string.Equals(Node.Id, Route.SourceId, StringComparison.Ordinal)
                        || string.Equals(Node.Id, Route.TargetId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    double Left = Node.X - (Node.Width / 2.0);
                    double Right = Node.X + (Node.Width / 2.0);
                    double Bottom = Node.Y - (Node.Height / 2.0);
                    double Top = Node.Y + (Node.Height / 2.0);
                    if (Point.X <= Left || Point.X >= Right || Point.Y <= Bottom || Point.Y >= Top)
                    {
                        continue;
                    }

                    for (int Run = Math.Max(1, Index - 1); Run <= Index && Run < Route.Points.Count - 2; Run++)
                    {
                        bool Vertical = Math.Abs(Route.Points[Run].X - Route.Points[Run + 1].X) < 0.5;
                        double Near = Vertical ? Left - STUB_PX - Point.X : Bottom - STUB_PX - Point.Y;
                        double Far = Vertical ? Right + STUB_PX - Point.X : Top + STUB_PX - Point.Y;
                        if (Math.Abs(Far) < Math.Abs(Near)) { (Near, Far) = (Far, Near); }
                        if (TryTranslateInternalRun(Route, Run, Vertical, Near, Placed)
                            || TryTranslateInternalRun(Route, Run, Vertical, Far, Placed))
                        {
                            break;
                        }
                    }
                }
            }
        }
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> BuildObstacleBounds(
        List<GridNode> Placed,
        List<GridFrame> Frames)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (GridNode Node in Placed)
        {
            BoundsById[Node.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height,
                Geometry = Node.GeometryProvider?.Create(Node.Width, Node.Height)
            };
        }

        foreach (GridFrame Frame in Frames)
        {
            BoundsById[Frame.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Frame.Id,
                CenterX = Frame.CenterX(),
                CenterY = Frame.CenterY(),
                Width = Frame.Right - Frame.Left,
                Height = Frame.Top - Frame.Bottom
            };
        }

        return BoundsById;
    }

    private static bool StraightChordIsClear(
        DiagramContract Contract,
        BoxRef Source,
        BoxRef Target,
        string ExitSide,
        string EntrySide,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> ObstacleBounds,
        string SourceId,
        string TargetId)
    {
        (double StartX, double StartY) = StraightAttachPoint(Source, ExitSide);
        (double EndX, double EndY) = StraightAttachPoint(Target, EntrySide);
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in ObstacleBounds)
        {
            if (string.Equals(Entry.Key, SourceId, StringComparison.Ordinal)
                || string.Equals(Entry.Key, TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            bool IsGroupFrame = false;
            foreach (GroupContract Group in Contract.Groups)
            {
                if (string.Equals(Group.Id, Entry.Key, StringComparison.Ordinal))
                {
                    IsGroupFrame = true;
                    break;
                }
            }

            if (IsGroupFrame
                && (IsInsideGroup(Contract, SourceId, Entry.Key) || IsInsideGroup(Contract, TargetId, Entry.Key)))
            {
                continue;
            }

            if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    StartX,
                    StartY,
                    EndX,
                    EndY,
                    Entry.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static (double X, double Y) StraightAttachPoint(BoxRef Box, string Side)
    {
        return Side switch
        {
            "top" => (Box.CenterX, Box.Top),
            "bottom" => (Box.CenterX, Box.Bottom),
            "left" => (Box.Left, Box.CenterY),
            "right" => (Box.Right, Box.CenterY),
            _ => (Box.CenterX, Box.CenterY)
        };
    }

    private static bool TryAssignStraightPortSides(
        BoxRef Source,
        BoxRef Target,
        FlowchartDirection Direction,
        out string ExitSide,
        out string EntrySide)
    {
        ExitSide = string.Empty;
        EntrySide = string.Empty;
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (Horizontal)
        {
            if (Math.Abs(Source.CenterY - Target.CenterY) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                return false;
            }

            if (Source.CenterX > Target.CenterX)
            {
                ExitSide = "left";
                EntrySide = "right";
            }
            else if (Source.CenterX < Target.CenterX)
            {
                ExitSide = "right";
                EntrySide = "left";
            }
            else
            {
                return false;
            }

            return true;
        }

        if (Math.Abs(Source.CenterY - Target.CenterY) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            if (Source.CenterX < Target.CenterX)
            {
                ExitSide = "right";
                EntrySide = "left";
                return true;
            }

            if (Source.CenterX > Target.CenterX)
            {
                ExitSide = "left";
                EntrySide = "right";
                return true;
            }

            return false;
        }

        if (Math.Abs(Source.CenterX - Target.CenterX) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            if (Target.IsFrame)
            {
                if (Source.CenterX < Target.Left - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX
                    || Source.CenterX > Target.Right + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        if (Source.CenterY < Target.CenterY)
        {
            // Y grows toward the layout's top. Support the reverse aligned pair
            // as well: a lower source exits its top and an upper target enters
            // its bottom.
            ExitSide = "top";
            EntrySide = "bottom";
            return true;
        }

        if (Source.CenterY > Target.CenterY)
        {
            ExitSide = "bottom";
            EntrySide = "top";
            return true;
        }

        return false;
    }

    private static string ChooseElbowExitSide(
        DiagramContract Contract,
        BoxRef Source,
        BoxRef Target,
        FlowchartDirection Direction,
        HashSet<string> ReservedCenterPorts,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> ObstacleBounds,
        string SourceId,
        string TargetId)
    {
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (Horizontal)
        {
            return FacingSide(Source, Target);
        }

        bool BottomReserved = ReservedCenterPorts.Contains(Source.Id + "|bottom");
        bool BottomBusy = BottomReserved
            || BottomSideBusyWithNextRankChild(Contract, Source, ObstacleBounds);
        if (!BottomBusy
            && Target.CenterY < Source.CenterY
            && StraightChordIsClear(Contract, Source, Target, "bottom", "top", ObstacleBounds, SourceId, TargetId))
        {
            return "bottom";
        }

        if (BottomBusy && Target.CenterY < Source.CenterY - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            if (Target.CenterX > Source.CenterX + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                return "right";
            }

            if (Target.CenterX < Source.CenterX - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                return "left";
            }
        }

        if (Target.CenterX < Source.CenterX - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            return "left";
        }

        if (Target.CenterX > Source.CenterX + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            return "right";
        }

        return "left";
    }

    private static bool BottomSideBusyWithNextRankChild(
        DiagramContract Contract,
        BoxRef Source,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> ObstacleBounds)
    {
        double BottomBand = (Source.Width / 2.0) + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!string.Equals(Edge.SourceId, Source.Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (!ObstacleBounds.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            if (TargetBounds.CenterY >= Source.CenterY - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                continue;
            }

            if (Math.Abs(TargetBounds.CenterX - Source.CenterX) <= BottomBand)
            {
                return true;
            }
        }

        return false;
    }

}

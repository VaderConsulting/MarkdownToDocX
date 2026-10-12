using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Moves shared orthogonal routes off borders, corners, titles, and duplicated corridors.
/// </summary>
internal static class FlowchartSharedPictureCleaner
{
    private const double BORDER_CLEAR_PX = 8.0;

    private const double HUG_PX = 6.0;

    private const double HUG_MIN_PX = 28.0;

    private const double CORNER_INSET_PX = 12.0;

    private const double ARRIVAL_CLEAR_PX = 18.0;

    private const double GROUP_CLEAR_PX = 20.0;

    private const double TITLE_CLEAR_PX = 10.0;

    private const double SHARED_MIN_PX = 36.0;

    private const double TITLE_BAND_PX = 26.0;

    /// <summary>
    /// Cleans every searchable route in place.
    /// </summary>
    public static void Clean(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        List<FlowchartResolvedGuide> Guides)
    {
        foreach (FlowchartResolvedGuide Guide in Guides)
        {
            if (FlowchartSharedEdgeRouter.IsDesignedRoute(Guide.RouteKind) || Guide.Points.Count < 2)
            {
                continue;
            }

            ShortenStackedPair(Contract, BoundsById, Guides, Guide);
            BypassTouchingPair(Contract, BoundsById, Guides, Guide);
            NudgeOffGroupBorder(Contract, BoundsById, Guide);
            RemoveSourceDogleg(BoundsById, Guide);
            LeaveHuggedSource(Contract, BoundsById, Guides, Guide);
            LiftBorderHugs(Contract, BoundsById, Guides, Guide);
            StepAsideTitle(Contract, BoundsById, Guide);
            SquareFinalPort(Contract, BoundsById, Guides, Guide);
            PullInOuterLane(Contract, BoundsById, Guides, Guide);
            FlowchartPaddedVisibilityRouter.CollapseReversalSpurs(Guide.Points);
            ClipRouteToTargetBorder(Contract, BoundsById, Guide);
            PullArrivalOffCorner(BoundsById, Guide);
            PreferDirectOrthogonal(Contract, BoundsById, Guides, Guide);
            FlowchartPaddedVisibilityRouter.CollapseReversalSpurs(Guide.Points);
        }

        SeparateSharedSourceChannels(Contract, BoundsById, Guides);
        foreach (FlowchartResolvedGuide Guide in Guides)
        {
            if (FlowchartSharedEdgeRouter.IsDesignedRoute(Guide.RouteKind) || Guide.Points.Count < 2)
            {
                continue;
            }

            PullEndsOffCorners(Contract, BoundsById, Guide);
            SnapRoundedOutline(BoundsById, NodeById, Guide);
            RestorePerpendicularStubs(BoundsById, NodeById, Guide);
            DropTinyAxisSteps(Guide.Points);
            LiftShortGroupBorderRun(Contract, BoundsById, Guide);
            PullSourceOffCapJunction(BoundsById, NodeById, Guides, Guide);
        }

        SpreadBunchedFacePorts(BoundsById, NodeById, Guides);
        PreferRightAngleToSharedRun(Contract, BoundsById, Guides);
    }

    private static void PushOffBorders(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        List<(double X, double Y)> Points)
    {
        for (int Index = 1; Index < Points.Count; Index++)
        {
            (double X1, double Y1) = Points[Index - 1];
            (double X2, double Y2) = Points[Index];
            double Length = Math.Sqrt(Math.Pow(X2 - X1, 2.0) + Math.Pow(Y2 - Y1, 2.0));
            if (Length < HUG_MIN_PX)
            {
                continue;
            }

            bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5;
            bool Vertical = Math.Abs(X2 - X1) <= 1.5;
            if (!Horizontal && !Vertical)
            {
                continue;
            }

            foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
            {
                FlowchartLayoutNodeBounds Bounds = Pair.Value;
                double Left = Bounds.CenterX - (Bounds.Width / 2.0);
                double Right = Bounds.CenterX + (Bounds.Width / 2.0);
                double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
                double Top = Bounds.CenterY + (Bounds.Height / 2.0);
                if (Horizontal)
                {
                    double Overlap = AxisOverlap(X1, X2, Left, Right);
                    if (Overlap < HUG_MIN_PX)
                    {
                        continue;
                    }

                    double Side = Math.Abs(Y1 - Top) <= Math.Abs(Y1 - Bottom) ? Top : Bottom;
                    if (Math.Abs(Y1 - Side) > HUG_PX)
                    {
                        continue;
                    }

                    double Preferred = Y1 >= Side ? Side + BORDER_CLEAR_PX : Side - BORDER_CLEAR_PX;
                    if (!TryAcceptHorizontal(Contract, BoundsById, X1, X2, Preferred, Pair.Key))
                    {
                        Preferred = Y1 >= Side ? Side - BORDER_CLEAR_PX : Side + BORDER_CLEAR_PX;
                    }

                    if (TryAcceptHorizontal(Contract, BoundsById, X1, X2, Preferred, Pair.Key))
                    {
                        ShiftY(Points, Y1, Preferred);
                    }
                }
                else
                {
                    double Overlap = AxisOverlap(Y1, Y2, Bottom, Top);
                    if (Overlap < HUG_MIN_PX)
                    {
                        continue;
                    }

                    double Side = Math.Abs(X1 - Right) <= Math.Abs(X1 - Left) ? Right : Left;
                    if (Math.Abs(X1 - Side) > HUG_PX)
                    {
                        continue;
                    }

                    double Outside = Math.Abs(X1 - Right) <= Math.Abs(X1 - Left)
                        ? (X1 >= Right ? Right + BORDER_CLEAR_PX : Right - BORDER_CLEAR_PX)
                        : (X1 <= Left ? Left - BORDER_CLEAR_PX : Left + BORDER_CLEAR_PX);
                    if (!TryAcceptVertical(Contract, BoundsById, Y1, Y2, Outside, Pair.Key))
                    {
                        Outside = Math.Abs(X1 - Right) <= Math.Abs(X1 - Left)
                            ? Right - BORDER_CLEAR_PX
                            : Left + BORDER_CLEAR_PX;
                    }

                    if (TryAcceptVertical(Contract, BoundsById, Y1, Y2, Outside, Pair.Key))
                    {
                        ShiftX(Points, X1, Outside);
                    }
                }
            }
        }
    }

    private static void BypassTitles(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        List<(double X, double Y)> Points)
    {
        if (Contract.Groups.Count == 0)
        {
            return;
        }

        for (int Index = Points.Count - 1; Index >= 1; Index--)
        {
            (double X1, double Y1) = Points[Index - 1];
            (double X2, double Y2) = Points[Index];
            if (Math.Abs(X2 - X1) > 1.5 || Math.Abs(Y2 - Y1) < TITLE_BAND_PX)
            {
                continue;
            }

            foreach (GroupContract Group in Contract.Groups)
            {
                if (string.IsNullOrWhiteSpace(Group.Title)
                    || !BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds? Bounds)
                    || Bounds == null)
                {
                    continue;
                }

                double Top = Bounds.CenterY + (Bounds.Height / 2.0);
                double BandBottom = Top - TITLE_BAND_PX;
                double Low = Math.Min(Y1, Y2);
                double High = Math.Max(Y1, Y2);
                if (High < BandBottom || Low > Top)
                {
                    continue;
                }

                double TitleWidth = Math.Max(48.0, Group.Title.Length * 7.2);
                double TitleLeft = Bounds.CenterX - (TitleWidth / 2.0);
                double TitleRight = Bounds.CenterX + (TitleWidth / 2.0);
                if (X1 <= TitleLeft || X1 >= TitleRight)
                {
                    continue;
                }

                double ClearX = X1 >= Bounds.CenterX ? TitleRight + BORDER_CLEAR_PX : TitleLeft - BORDER_CLEAR_PX;
                if (VerticalHitsNode(Contract, BoundsById, Math.Min(Y1, Y2), Math.Max(Y1, Y2), ClearX, Group.Id))
                {
                    ClearX = X1 >= Bounds.CenterX ? TitleLeft - BORDER_CLEAR_PX : TitleRight + BORDER_CLEAR_PX;
                }

                if (VerticalHitsNode(Contract, BoundsById, Math.Min(Y1, Y2), Math.Max(Y1, Y2), ClearX, Group.Id))
                {
                    continue;
                }

                double Above = Top + 4.0;
                double Below = BandBottom - 4.0;
                if (High < Above)
                {
                    Above = High;
                }

                if (Low > Below)
                {
                    Below = Low;
                }

                Points.RemoveAt(Index);
                Points.RemoveAt(Index - 1);
                double StartY = Y1;
                double EndY = Y2;
                Points.Insert(Index - 1, (X1, StartY));
                if (StartY > EndY)
                {
                    Points.Insert(Index, (X1, Above));
                    Points.Insert(Index + 1, (ClearX, Above));
                    Points.Insert(Index + 2, (ClearX, Below));
                    Points.Insert(Index + 3, (X1, Below));
                }
                else
                {
                    Points.Insert(Index, (X1, Below));
                    Points.Insert(Index + 1, (ClearX, Below));
                    Points.Insert(Index + 2, (ClearX, Above));
                    Points.Insert(Index + 3, (X1, Above));
                }

                Points.Insert(Index + 4, (X1, EndY));
                break;
            }
        }
    }

    private static void SeparateSharedRuns(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        List<FlowchartResolvedGuide> Guides)
    {
        for (int LeftIndex = 0; LeftIndex < Guides.Count; LeftIndex++)
        {
            FlowchartResolvedGuide Left = Guides[LeftIndex];
            if (FlowchartSharedEdgeRouter.IsDesignedRoute(Left.RouteKind))
            {
                continue;
            }

            for (int RightIndex = LeftIndex + 1; RightIndex < Guides.Count; RightIndex++)
            {
                FlowchartResolvedGuide Right = Guides[RightIndex];
                if (FlowchartSharedEdgeRouter.IsDesignedRoute(Right.RouteKind))
                {
                    continue;
                }

                bool SharesNode = string.Equals(Left.Edge.SourceId, Right.Edge.SourceId, StringComparison.Ordinal)
                    || string.Equals(Left.Edge.SourceId, Right.Edge.TargetId, StringComparison.Ordinal)
                    || string.Equals(Left.Edge.TargetId, Right.Edge.SourceId, StringComparison.Ordinal)
                    || string.Equals(Left.Edge.TargetId, Right.Edge.TargetId, StringComparison.Ordinal);
                if (!SharesNode)
                {
                    continue;
                }

                ShiftShared(Contract, BoundsById, Left.Points, Right.Points);
            }
        }
    }

    private static void SeparateNearParallel(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        List<FlowchartResolvedGuide> Guides)
    {
        const double CLEAR_PX = 12.0;
        for (int LeftIndex = 0; LeftIndex < Guides.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Guides.Count; RightIndex++)
            {
                FlowchartResolvedGuide Left = Guides[LeftIndex];
                FlowchartResolvedGuide Right = Guides[RightIndex];
                if (FlowchartSharedEdgeRouter.IsDesignedRoute(Left.RouteKind)
                    || FlowchartSharedEdgeRouter.IsDesignedRoute(Right.RouteKind))
                {
                    continue;
                }

                for (int LeftSegment = 1; LeftSegment < Left.Points.Count; LeftSegment++)
                {
                    (double Lx1, double Ly1) = Left.Points[LeftSegment - 1];
                    (double Lx2, double Ly2) = Left.Points[LeftSegment];
                    for (int RightSegment = 1; RightSegment < Right.Points.Count; RightSegment++)
                    {
                        (double Rx1, double Ry1) = Right.Points[RightSegment - 1];
                        (double Rx2, double Ry2) = Right.Points[RightSegment];
                        bool Horizontal = Math.Abs(Ly2 - Ly1) <= 1.5
                            && Math.Abs(Ry2 - Ry1) <= 1.5
                            && Math.Abs(Lx2 - Lx1) >= 24.0
                            && Math.Abs(Rx2 - Rx1) >= 24.0;
                        bool Vertical = Math.Abs(Lx2 - Lx1) <= 1.5
                            && Math.Abs(Rx2 - Rx1) <= 1.5
                            && Math.Abs(Ly2 - Ly1) >= 24.0
                            && Math.Abs(Ry2 - Ry1) >= 24.0;
                        if (Horizontal && AxisOverlap(Lx1, Lx2, Rx1, Rx2) >= 24.0)
                        {
                            double Gap = Math.Abs(Ly1 - Ry1);
                            if (Gap >= 2.5 && Gap < CLEAR_PX)
                            {
                                double Target = Ry1 + (CLEAR_PX - Gap);
                                if (!TryAcceptHorizontal(Contract, BoundsById, Rx1, Rx2, Target, string.Empty))
                                {
                                    Target = Ry1 - (CLEAR_PX - Gap);
                                }

                                if (TryAcceptHorizontal(Contract, BoundsById, Rx1, Rx2, Target, string.Empty))
                                {
                                    ShiftY(Right.Points, Ry1, Target);
                                }
                            }
                        }
                        else if (Vertical && AxisOverlap(Ly1, Ly2, Ry1, Ry2) >= 24.0)
                        {
                            double Gap = Math.Abs(Lx1 - Rx1);
                            if (Gap >= 2.5 && Gap < CLEAR_PX)
                            {
                                double Target = Rx1 + (CLEAR_PX - Gap);
                                if (!TryAcceptVertical(Contract, BoundsById, Ry1, Ry2, Target, string.Empty))
                                {
                                    Target = Rx1 - (CLEAR_PX - Gap);
                                }

                                if (TryAcceptVertical(Contract, BoundsById, Ry1, Ry2, Target, string.Empty))
                                {
                                    ShiftX(Right.Points, Rx1, Target);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static void ShiftShared(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        List<(double X, double Y)> Left,
        List<(double X, double Y)> Right)
    {
        for (int LeftSegment = 1; LeftSegment < Left.Count; LeftSegment++)
        {
            (double Lx1, double Ly1) = Left[LeftSegment - 1];
            (double Lx2, double Ly2) = Left[LeftSegment];
            bool LeftHorizontal = Math.Abs(Ly2 - Ly1) <= 1.5 && Math.Abs(Lx2 - Lx1) >= SHARED_MIN_PX;
            bool LeftVertical = Math.Abs(Lx2 - Lx1) <= 1.5 && Math.Abs(Ly2 - Ly1) >= SHARED_MIN_PX;
            if (!LeftHorizontal && !LeftVertical)
            {
                continue;
            }

            for (int RightSegment = 1; RightSegment < Right.Count; RightSegment++)
            {
                (double Rx1, double Ry1) = Right[RightSegment - 1];
                (double Rx2, double Ry2) = Right[RightSegment];
                if (LeftHorizontal && Math.Abs(Ry2 - Ry1) <= 1.5 && Math.Abs(Ly1 - Ry1) <= 2.5)
                {
                    double Overlap = AxisOverlap(Lx1, Lx2, Rx1, Rx2);
                    if (Overlap < SHARED_MIN_PX)
                    {
                        continue;
                    }

                    double Raised = Ry1 + 12.0;
                    double Lowered = Ry1 - 12.0;
                    double Chosen = TryAcceptHorizontal(Contract, BoundsById, Rx1, Rx2, Raised, string.Empty)
                        ? Raised
                        : Lowered;
                    if (TryAcceptHorizontal(Contract, BoundsById, Rx1, Rx2, Chosen, string.Empty))
                    {
                        ShiftY(Right, Ry1, Chosen);
                    }
                }
                else if (LeftVertical && Math.Abs(Rx2 - Rx1) <= 1.5 && Math.Abs(Lx1 - Rx1) <= 2.5)
                {
                    double Overlap = AxisOverlap(Ly1, Ly2, Ry1, Ry2);
                    if (Overlap < SHARED_MIN_PX)
                    {
                        continue;
                    }

                    double Raised = Rx1 + 12.0;
                    double Lowered = Rx1 - 12.0;
                    double Chosen = TryAcceptVertical(Contract, BoundsById, Ry1, Ry2, Raised, string.Empty)
                        ? Raised
                        : Lowered;
                    if (TryAcceptVertical(Contract, BoundsById, Ry1, Ry2, Chosen, string.Empty))
                    {
                        ShiftX(Right, Rx1, Chosen);
                    }
                }
            }
        }
    }

    private static void RetargetEndpointHugs(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide)
    {
        if (!BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source) || Source == null)
        {
            return;
        }

        if (!BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target) || Target == null)
        {
            return;
        }

        for (int Index = 1; Index < Guide.Points.Count; Index++)
        {
            (double X1, double Y1) = Guide.Points[Index - 1];
            (double X2, double Y2) = Guide.Points[Index];
            bool Vertical = Math.Abs(X2 - X1) <= 1.5 && Math.Abs(Y2 - Y1) >= HUG_MIN_PX;
            bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5 && Math.Abs(X2 - X1) >= HUG_MIN_PX;
            if (!Vertical && !Horizontal)
            {
                continue;
            }

            foreach (FlowchartLayoutNodeBounds Bounds in new[] { Source, Target })
            {
                double Left = Bounds.CenterX - (Bounds.Width / 2.0);
                double Right = Bounds.CenterX + (Bounds.Width / 2.0);
                double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
                double Top = Bounds.CenterY + (Bounds.Height / 2.0);
                if (Vertical)
                {
                    double Side = Math.Abs(X1 - Right) <= Math.Abs(X1 - Left) ? Right : Left;
                    if (Math.Abs(X1 - Side) > HUG_PX || AxisOverlap(Y1, Y2, Bottom, Top) < HUG_MIN_PX)
                    {
                        continue;
                    }

                    double ClearX = Side == Right ? Right + BORDER_CLEAR_PX : Left - BORDER_CLEAR_PX;
                    for (int PointIndex = 0; PointIndex < Guide.Points.Count; PointIndex++)
                    {
                        if (Math.Abs(Guide.Points[PointIndex].X - X1) <= 1.5)
                        {
                            Guide.Points[PointIndex] = (ClearX, Guide.Points[PointIndex].Y);
                        }
                    }

                    Guide.Points[0] = InsetPort(Source, Guide.Points[0].X, Guide.Points[0].Y);
                    Guide.Points[Guide.Points.Count - 1] = InsetPort(Target, Guide.Points[Guide.Points.Count - 1].X, Guide.Points[Guide.Points.Count - 1].Y);
                }
                else
                {
                    double Side = Math.Abs(Y1 - Top) <= Math.Abs(Y1 - Bottom) ? Top : Bottom;
                    if (Math.Abs(Y1 - Side) > HUG_PX || AxisOverlap(X1, X2, Left, Right) < HUG_MIN_PX)
                    {
                        continue;
                    }

                    double ClearY = Side == Top ? Top + BORDER_CLEAR_PX : Bottom - BORDER_CLEAR_PX;
                    for (int PointIndex = 0; PointIndex < Guide.Points.Count; PointIndex++)
                    {
                        if (Math.Abs(Guide.Points[PointIndex].Y - Y1) <= 1.5)
                        {
                            Guide.Points[PointIndex] = (Guide.Points[PointIndex].X, ClearY);
                        }
                    }

                    Guide.Points[0] = InsetPort(Source, Guide.Points[0].X, Guide.Points[0].Y);
                    Guide.Points[Guide.Points.Count - 1] = InsetPort(Target, Guide.Points[Guide.Points.Count - 1].X, Guide.Points[Guide.Points.Count - 1].Y);
                }
            }
        }
    }

    private static (double X, double Y) InsetPort(FlowchartLayoutNodeBounds Bounds, double X, double Y)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        double LeftGap = Math.Abs(X - Left);
        double RightGap = Math.Abs(X - Right);
        double TopGap = Math.Abs(Y - Top);
        double BottomGap = Math.Abs(Y - Bottom);
        double Nearest = Math.Min(Math.Min(LeftGap, RightGap), Math.Min(TopGap, BottomGap));
        if (Nearest == TopGap || Nearest == BottomGap)
        {
            double FaceY = Nearest == TopGap ? Top : Bottom;
            double Slot = Math.Clamp(X, Left + CORNER_INSET_PX, Right - CORNER_INSET_PX);
            return (Slot, FaceY);
        }

        double FaceX = Nearest == LeftGap ? Left : Right;
        double SlotY = Math.Clamp(Y, Bottom + CORNER_INSET_PX, Top - CORNER_INSET_PX);
        return (FaceX, SlotY);
    }

    private static double PathLength(IReadOnlyList<(double X, double Y)> Points)
    {
        double Length = 0.0;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            Length += Math.Sqrt(Math.Pow(Points[Index].X - Points[Index - 1].X, 2.0) + Math.Pow(Points[Index].Y - Points[Index - 1].Y, 2.0));
        }

        return Length;
    }

    private static void LiftBorderHugs(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide)
    {
        for (int Guard = 0; Guard < 4; Guard++)
        {
            bool Changed = false;
            for (int Index = 1; Index < Guide.Points.Count; Index++)
            {
                (double X1, double Y1) = Guide.Points[Index - 1];
                (double X2, double Y2) = Guide.Points[Index];
                bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5 && Math.Abs(X2 - X1) >= HUG_MIN_PX;
                bool Vertical = Math.Abs(X2 - X1) <= 1.5 && Math.Abs(Y2 - Y1) >= HUG_MIN_PX;
                if (!Horizontal && !Vertical)
                {
                    continue;
                }

                foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
                {
                    FlowchartLayoutNodeBounds Bounds = Pair.Value;
                    double Left = Bounds.CenterX - (Bounds.Width / 2.0);
                    double Right = Bounds.CenterX + (Bounds.Width / 2.0);
                    double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
                    double Top = Bounds.CenterY + (Bounds.Height / 2.0);
                    double Clear;
                    if (Horizontal)
                    {
                        if (AxisOverlap(X1, X2, Left, Right) < HUG_MIN_PX)
                        {
                            continue;
                        }

                        if (Math.Abs(Y1 - Top) <= HUG_PX)
                        {
                            Clear = Top + BORDER_CLEAR_PX;
                        }
                        else if (Math.Abs(Y1 - Bottom) <= HUG_PX)
                        {
                            Clear = Bottom - BORDER_CLEAR_PX;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (AxisOverlap(Y1, Y2, Bottom, Top) < HUG_MIN_PX)
                        {
                            continue;
                        }

                        if (Math.Abs(X1 - Left) <= HUG_PX)
                        {
                            Clear = Left - BORDER_CLEAR_PX;
                        }
                        else if (Math.Abs(X1 - Right) <= HUG_PX)
                        {
                            Clear = Right + BORDER_CLEAR_PX;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    List<(double X, double Y)> Candidate = new List<(double X, double Y)>(Guide.Points);
                    OffsetSegment(Candidate, Index, Horizontal, Clear);
                    if (RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId)
                        || CrossesAnotherGuide(Guides, Guide, Candidate))
                    {
                        continue;
                    }

                    Guide.Points.Clear();
                    Guide.Points.AddRange(Candidate);
                    Changed = true;
                    break;
                }

                if (Changed)
                {
                    break;
                }
            }

            if (!Changed)
            {
                return;
            }
        }
    }

    private static void OffsetSegment(
        List<(double X, double Y)> Points,
        int Index,
        bool Horizontal,
        double Clear)
    {
        (double X1, double Y1) = Points[Index - 1];
        (double X2, double Y2) = Points[Index];
        if (Horizontal)
        {
            if (Index - 1 == 0)
            {
                Points.Insert(1, (X1, Clear));
                Index++;
            }
            else
            {
                Points[Index - 1] = (X1, Clear);
            }

            if (Index >= Points.Count - 1)
            {
                Points.Insert(Points.Count - 1, (X2, Clear));
            }
            else
            {
                Points[Index] = (X2, Clear);
            }
        }
        else
        {
            if (Index - 1 == 0)
            {
                Points.Insert(1, (Clear, Y1));
                Index++;
            }
            else
            {
                Points[Index - 1] = (Clear, Y1);
            }

            if (Index >= Points.Count - 1)
            {
                Points.Insert(Points.Count - 1, (Clear, Y2));
            }
            else
            {
                Points[Index] = (Clear, Y2);
            }
        }
    }

    private static void SquareFinalPort(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide)
    {
        if (Guide.Points.Count < 2
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target)
            || Target == null
            || !IsRouteableBox(Contract, Guide.Edge.TargetId))
        {
            return;
        }

        (double PrevX, double PrevY) = Guide.Points[Guide.Points.Count - 2];
        (double TipX, double TipY) = Guide.Points[Guide.Points.Count - 1];
        double Run = Math.Sqrt(Math.Pow(TipX - PrevX, 2.0) + Math.Pow(TipY - PrevY, 2.0));
        bool Vertical = Math.Abs(TipX - PrevX) <= 1.5 && Run > 8.0;
        bool Horizontal = Math.Abs(TipY - PrevY) <= 1.5 && Run > 8.0;
        if (!Vertical && !Horizontal)
        {
            return;
        }

        double Left = Target.CenterX - (Target.Width / 2.0);
        double Right = Target.CenterX + (Target.Width / 2.0);
        double Bottom = Target.CenterY - (Target.Height / 2.0);
        double Top = Target.CenterY + (Target.Height / 2.0);
        double Cap = RoundedCap(Contract, Guide.Edge.TargetId, Target);
        double Corner = DistanceToCorner(TipX, TipY, Left + Cap, Right - Cap, Bottom, Top);
        bool OnVerticalSide = Math.Abs(TipX - Left) <= HUG_PX || Math.Abs(TipX - Right) <= HUG_PX;
        bool OnHorizontalSide = Math.Abs(TipY - Top) <= HUG_PX || Math.Abs(TipY - Bottom) <= HUG_PX;
        bool InCap = Cap > 0.0 && (TipX < Left + Cap + CORNER_INSET_PX || TipX > Right - Cap - CORNER_INSET_PX);
        bool Tangential = (Vertical && OnVerticalSide) || (Horizontal && OnHorizontalSide);
        if (Corner > CORNER_INSET_PX && !Tangential && !InCap)
        {
            return;
        }

        string Face;
        if (OnHorizontalSide)
        {
            Face = Math.Abs(TipY - Top) <= Math.Abs(TipY - Bottom) ? "top" : "bottom";
        }
        else if (OnVerticalSide && Vertical)
        {
            Face = PrevY >= Top - 1.0 ? "top" : "bottom";
        }
        else if (Vertical)
        {
            Face = Math.Abs(TipY - Top) <= Math.Abs(TipY - Bottom) ? "top" : "bottom";
        }
        else
        {
            Face = Math.Abs(TipX - Left) <= Math.Abs(TipX - Right) ? "left" : "right";
        }

        double Inset = CORNER_INSET_PX + 2.0 + Cap;
        double PortX;
        double PortY;
        if (string.Equals(Face, "top", StringComparison.Ordinal) || string.Equals(Face, "bottom", StringComparison.Ordinal))
        {
            PortY = string.Equals(Face, "top", StringComparison.Ordinal) ? Top : Bottom;
            double WantX = PrevX;
            if (Tangential && Vertical)
            {
                WantX = Math.Abs(TipX - Left) <= Math.Abs(TipX - Right) ? Left + Inset : Right - Inset;
            }

            double InnerLeft = Left + Inset;
            double InnerRight = Right - Inset;
            PortX = InnerLeft >= InnerRight ? (Left + Right) / 2.0 : Math.Clamp(WantX, InnerLeft, InnerRight);
        }
        else
        {
            PortX = string.Equals(Face, "left", StringComparison.Ordinal) ? Left : Right;
            double WantY = PrevY;
            if (Tangential && Horizontal)
            {
                WantY = Math.Abs(TipY - Top) <= Math.Abs(TipY - Bottom) ? Top - Inset : Bottom + Inset;
            }

            double InnerBottom = Bottom + Inset;
            double InnerTop = Top - Inset;
            PortY = InnerBottom >= InnerTop ? (Bottom + Top) / 2.0 : Math.Clamp(WantY, InnerBottom, InnerTop);
        }

        List<(double X, double Y)> Candidate = BuildFinalApproach(Guide.Points, PrevX, PrevY, PortX, PortY, Face, Target.CenterX, Target.CenterY, Tangential, false);
        if (AnotherTipIsClose(Guides, Guide, PortX, PortY))
        {
            double[] Offsets = [14.0, -14.0, 28.0, -28.0];
            foreach (double Offset in Offsets)
            {
                double TryX = PortX;
                double TryY = PortY;
                if (string.Equals(Face, "top", StringComparison.Ordinal) || string.Equals(Face, "bottom", StringComparison.Ordinal))
                {
                    TryX = Math.Clamp(PortX + Offset, Left + 18.0, Right - 18.0);
                }
                else
                {
                    TryY = Math.Clamp(PortY + Offset, Bottom + 18.0, Top - 18.0);
                }

                if (AnotherTipIsClose(Guides, Guide, TryX, TryY))
                {
                    continue;
                }

                List<(double X, double Y)> Shifted = BuildFinalApproach(Guide.Points, PrevX, PrevY, TryX, TryY, Face, Target.CenterX, Target.CenterY, Tangential, true);
                if (TailIntroducesObstacle(Contract, BoundsById, Guide.Points, Shifted, Guide.Edge.SourceId, Guide.Edge.TargetId)
                    || CrossesAnotherGuide(Guides, Guide, Shifted))
                {
                    continue;
                }

                Candidate = Shifted;
                break;
            }
        }

        if (TailIntroducesObstacle(Contract, BoundsById, Guide.Points, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId)
            || CrossesAnotherGuide(Guides, Guide, Candidate))
        {
            return;
        }

        Guide.Points.Clear();
        Guide.Points.AddRange(Candidate);
    }

    private static List<(double X, double Y)> BuildFinalApproach(
        IReadOnlyList<(double X, double Y)> Points,
        double PrevX,
        double PrevY,
        double PortX,
        double PortY,
        string Face,
        double CentreX,
        double CentreY,
        bool Tangential,
        bool OppositeApproach)
    {
        List<(double X, double Y)> Candidate = new List<(double X, double Y)>(Points);
        Candidate.RemoveAt(Candidate.Count - 1);
        bool EnterVertically = string.Equals(Face, "top", StringComparison.Ordinal) || string.Equals(Face, "bottom", StringComparison.Ordinal);
        if (EnterVertically)
        {
            double ApproachY = PrevY;
            if (Math.Abs(ApproachY - PortY) <= 20.0)
            {
                int ApproachSign = string.Equals(Face, "top", StringComparison.Ordinal) ? 1 : -1;
                if (OppositeApproach)
                {
                    ApproachSign = -ApproachSign;
                }

                ApproachY = PortY + (ApproachSign * BORDER_CLEAR_PX);
                if (Candidate.Count > 0)
                {
                    int Last = Candidate.Count - 1;
                    (double LastX, double LastY) = Candidate[Last];
                    bool PastChannel = string.Equals(Face, "top", StringComparison.Ordinal)
                        ? LastY < ApproachY - 0.5
                        : LastY > ApproachY + 0.5;
                    if (PastChannel)
                    {
                        Candidate[Last] = (LastX, ApproachY);
                    }
                    else if (Math.Abs(LastY - ApproachY) > 1.5)
                    {
                        Candidate.Add((LastX, ApproachY));
                    }
                }
            }

            if (Candidate.Count >= 2
                && Math.Abs(Candidate[Candidate.Count - 1].Y - ApproachY) <= 1.5
                && Math.Abs(Candidate[Candidate.Count - 2].Y - ApproachY) <= 1.5)
            {
                Candidate[Candidate.Count - 1] = (PortX, ApproachY);
            }
            else if (Candidate.Count > 0 && Math.Abs(Candidate[Candidate.Count - 1].X - PortX) > 1.5)
            {
                Candidate.Add((PortX, ApproachY));
            }
            else if (Candidate.Count > 0 && Math.Abs(ApproachY - PrevY) > 1.5)
            {
                Candidate[Candidate.Count - 1] = (PortX, ApproachY);
            }

            Candidate.Add((PortX, PortY));
        }
        else
        {
            double ApproachX = PrevX;
            if (Tangential && Math.Abs(ApproachX - PortX) <= HUG_PX)
            {
                ApproachX = PortX >= CentreX ? PortX + BORDER_CLEAR_PX : PortX - BORDER_CLEAR_PX;
            }

            if (Candidate.Count > 0 && Math.Abs(Candidate[Candidate.Count - 1].Y - PortY) > 1.5)
            {
                Candidate.Add((ApproachX, PortY));
            }
            else if (Candidate.Count > 0 && Math.Abs(ApproachX - PrevX) > 1.5)
            {
                Candidate[Candidate.Count - 1] = (ApproachX, PortY);
            }

            Candidate.Add((PortX, PortY));
        }

        return Candidate;
    }

    private static void LeaveHuggedSource(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide)
    {
        if (Guide.Points.Count < 2
            || !BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source)
            || Source == null
            || !IsPlainBox(Contract, Guide.Edge.SourceId))
        {
            return;
        }

        (double X1, double Y1) = Guide.Points[0];
        (double X2, double Y2) = Guide.Points[1];
        if (Math.Abs(Y2 - Y1) > 1.5 || Math.Abs(X2 - X1) < HUG_MIN_PX)
        {
            return;
        }

        double Left = Source.CenterX - (Source.Width / 2.0);
        double Right = Source.CenterX + (Source.Width / 2.0);
        double Bottom = Source.CenterY - (Source.Height / 2.0);
        double Top = Source.CenterY + (Source.Height / 2.0);
        if (AxisOverlap(X1, X2, Left, Right) < HUG_MIN_PX)
        {
            return;
        }

        if (Math.Abs(Y1 - Top) > HUG_PX && Math.Abs(Y1 - Bottom) > HUG_PX)
        {
            return;
        }

        bool LeaveLeft = X2 < X1;
        double PortX = LeaveLeft ? Left : Right;
        double PortY = Math.Clamp(Y1, Bottom + CORNER_INSET_PX, Top - CORNER_INSET_PX);
        if (Top - Bottom < (CORNER_INSET_PX * 2.0))
        {
            PortY = Source.CenterY;
        }

        List<(double X, double Y)> Candidate = new List<(double X, double Y)> { (PortX, PortY) };
        Candidate.AddRange(Guide.Points.Skip(1));
        if (RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId)
            || CrossesAnotherGuide(Guides, Guide, Candidate))
        {
            return;
        }

        Guide.Points.Clear();
        Guide.Points.AddRange(Candidate);
    }

    private static bool AnotherTipIsClose(
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        double PortX,
        double PortY)
    {
        foreach (FlowchartResolvedGuide Other in Guides)
        {
            if (ReferenceEquals(Other, Guide) || Other.Points.Count == 0)
            {
                continue;
            }

            if (!string.Equals(Other.Edge.TargetId, Guide.Edge.TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            (double OtherX, double OtherY) = Other.Points[Other.Points.Count - 1];
            if (Math.Abs(OtherX - PortX) < 12.0 && Math.Abs(OtherY - PortY) < 12.0)
            {
                return true;
            }
        }

        return false;
    }

    private static void PullInOuterLane(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide)
    {
        if (Guide.Points.Count < 3)
        {
            return;
        }

        (double StartX, double StartY) = Guide.Points[0];
        (double EndX, double EndY) = Guide.Points[Guide.Points.Count - 1];
        double Manhattan = Math.Abs(EndX - StartX) + Math.Abs(EndY - StartY);
        if (PathLength(Guide.Points) <= (2.8 * Math.Max(Manhattan, 40.0)))
        {
            return;
        }

        double GoalX = (StartX + EndX) / 2.0;
        for (int Index = 1; Index < Guide.Points.Count; Index++)
        {
            (double X1, double Y1) = Guide.Points[Index - 1];
            (double X2, double Y2) = Guide.Points[Index];
            if (Math.Abs(X2 - X1) > 1.5 || Math.Abs(Y2 - Y1) < 80.0 || Math.Abs(GoalX - X1) < 24.0)
            {
                continue;
            }

            double Direction = GoalX < X1 ? -8.0 : 8.0;
            double Tried = X1;
            while (Math.Abs(GoalX - Tried) > 8.0)
            {
                double Next = Tried + Direction;
                if (HugsSomeGroup(BoundsById, Contract, Next, Math.Min(Y1, Y2), Math.Max(Y1, Y2)))
                {
                    Tried = Next;
                    continue;
                }

                List<(double X, double Y)> Slid = new List<(double X, double Y)>(Guide.Points);
                for (int PointIndex = 1; PointIndex < Slid.Count - 1; PointIndex++)
                {
                    if (Math.Abs(Slid[PointIndex].X - X1) <= 1.5)
                    {
                        Slid[PointIndex] = (Next, Slid[PointIndex].Y);
                    }
                }

                if (RouteStillBlocked(Contract, BoundsById, Slid, Guide.Edge.SourceId, Guide.Edge.TargetId)
                    || CrossesAnotherGuide(Guides, Guide, Slid))
                {
                    break;
                }

                Guide.Points.Clear();
                Guide.Points.AddRange(Slid);
                Tried = Next;
                X1 = Next;
                (StartX, StartY) = Guide.Points[0];
                (EndX, EndY) = Guide.Points[Guide.Points.Count - 1];
                Manhattan = Math.Abs(EndX - StartX) + Math.Abs(EndY - StartY);
                if (PathLength(Guide.Points) <= (2.8 * Math.Max(Manhattan, 40.0)))
                {
                    return;
                }
            }

            if (TryWaistAroundBlocker(Contract, BoundsById, Guides, Guide, Index, GoalX))
            {
                return;
            }
        }
    }

    private static bool HugsSomeGroup(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        DiagramContract Contract,
        double X,
        double Low,
        double High)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (!BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds? Bounds) || Bounds == null)
            {
                continue;
            }

            double Left = Bounds.CenterX - (Bounds.Width / 2.0);
            double Right = Bounds.CenterX + (Bounds.Width / 2.0);
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            if (AxisOverlap(Low, High, Bottom, Top) < HUG_MIN_PX)
            {
                continue;
            }

            if (Math.Abs(X - Left) <= HUG_PX || Math.Abs(X - Right) <= HUG_PX)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryWaistAroundBlocker(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        int Index,
        double GoalX)
    {
        (double X1, double Y1) = Guide.Points[Index - 1];
        (double X2, double Y2) = Guide.Points[Index];
        double Low = Math.Min(Y1, Y2);
        double High = Math.Max(Y1, Y2);
        double BlockerRight = double.NegativeInfinity;
        double BlockerTop = High;
        double BlockerBottom = Low;
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
        {
            if (!IsNode(Contract, Pair.Key))
            {
                continue;
            }

            FlowchartLayoutNodeBounds Bounds = Pair.Value;
            double Left = Bounds.CenterX - (Bounds.Width / 2.0);
            double Right = Bounds.CenterX + (Bounds.Width / 2.0);
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            if (Right < Math.Min(GoalX, X1) || Left > Math.Max(GoalX, X1))
            {
                continue;
            }

            if (AxisOverlap(Low, High, Bottom, Top) < 8.0 || Right <= BlockerRight)
            {
                continue;
            }

            BlockerRight = Right;
            BlockerTop = Top;
            BlockerBottom = Bottom;
        }

        if (double.IsNegativeInfinity(BlockerRight))
        {
            return false;
        }

        double OpenLane = GoalX;
        foreach (FlowchartResolvedGuide Other in Guides)
        {
            if (ReferenceEquals(Other, Guide))
            {
                continue;
            }

            for (int OtherIndex = 1; OtherIndex < Other.Points.Count; OtherIndex++)
            {
                (double Ox1, double Oy1) = Other.Points[OtherIndex - 1];
                (double Ox2, double Oy2) = Other.Points[OtherIndex];
                if (Math.Abs(Oy2 - Oy1) > 1.5 || Oy1 <= Low || Oy1 >= High)
                {
                    continue;
                }

                if (AxisOverlap(Ox1, Ox2, Math.Min(GoalX, X1), Math.Max(GoalX, X1)) < 8.0)
                {
                    continue;
                }

                double End = X1 > GoalX ? Math.Max(Ox1, Ox2) : Math.Min(Ox1, Ox2);
                if (X1 > GoalX)
                {
                    OpenLane = Math.Max(OpenLane, End + 12.0);
                }
                else
                {
                    OpenLane = Math.Min(OpenLane, End - 12.0);
                }
            }
        }

        double OuterLane = X1 > GoalX ? BlockerRight + 10.0 : BlockerRight - 10.0;
        if (X1 > GoalX && OuterLane > X1 - 8.0)
        {
            return false;
        }

        double Above = Math.Min(High, BlockerTop + 12.0);
        double Below = Math.Max(Low, BlockerBottom - 12.0);
        if (Above - Below < 20.0 || Above >= High - 8.0 || Below <= Low + 8.0)
        {
            return false;
        }

        List<(double X, double Y)> Candidate = new List<(double X, double Y)>();
        for (int PointIndex = 0; PointIndex < Index; PointIndex++)
        {
            Candidate.Add(Guide.Points[PointIndex]);
        }

        if (Y1 >= Y2)
        {
            Candidate.Add((OpenLane, Y1));
            Candidate.Add((OpenLane, Above));
            Candidate.Add((OuterLane, Above));
            Candidate.Add((OuterLane, Below));
            Candidate.Add((OpenLane, Below));
            Candidate.Add((OpenLane, Y2));
        }
        else
        {
            Candidate.Add((OpenLane, Y1));
            Candidate.Add((OpenLane, Below));
            Candidate.Add((OuterLane, Below));
            Candidate.Add((OuterLane, Above));
            Candidate.Add((OpenLane, Above));
            Candidate.Add((OpenLane, Y2));
        }

        for (int PointIndex = Index; PointIndex < Guide.Points.Count; PointIndex++)
        {
            Candidate.Add(Guide.Points[PointIndex]);
        }

        if (RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId)
            || CrossesAnotherGuide(Guides, Guide, Candidate)
            || PathLength(Candidate) >= PathLength(Guide.Points))
        {
            return false;
        }

        Guide.Points.Clear();
        Guide.Points.AddRange(Candidate);
        return true;
    }

    private static void ShortenStackedPair(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide)
    {
        if (!BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source)
            || Source == null
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target)
            || Target == null
            || !IsPlainBox(Contract, Guide.Edge.SourceId)
            || !IsPlainBox(Contract, Guide.Edge.TargetId))
        {
            return;
        }

        double Length = PathLength(Guide.Points);
        (double StartX, double StartY) = Guide.Points[0];
        (double EndX, double EndY) = Guide.Points[Guide.Points.Count - 1];
        double Manhattan = Math.Abs(EndX - StartX) + Math.Abs(EndY - StartY);
        if (Length - Manhattan < FlowchartGeometryPolicy.ExcessDetourMinPx)
        {
            return;
        }

        double SourceLeft = Source.CenterX - (Source.Width / 2.0);
        double SourceRight = Source.CenterX + (Source.Width / 2.0);
        double SourceBottom = Source.CenterY - (Source.Height / 2.0);
        double SourceTop = Source.CenterY + (Source.Height / 2.0);
        double TargetLeft = Target.CenterX - (Target.Width / 2.0);
        double TargetRight = Target.CenterX + (Target.Width / 2.0);
        double TargetBottom = Target.CenterY - (Target.Height / 2.0);
        double TargetTop = Target.CenterY + (Target.Height / 2.0);
        double SourceCap = RoundedCap(Contract, Guide.Edge.SourceId, Source);
        double TargetCap = RoundedCap(Contract, Guide.Edge.TargetId, Target);
        bool SourceAbove = Source.CenterY >= Target.CenterY;
        double OverlapLeft = Math.Max(SourceLeft + SourceCap, TargetLeft + TargetCap);
        double OverlapRight = Math.Min(SourceRight - SourceCap, TargetRight - TargetCap);
        if (OverlapRight - OverlapLeft < 28.0)
        {
            return;
        }

        double SourceFace = SourceAbove ? SourceBottom : SourceTop;
        double TargetFace = SourceAbove ? TargetTop : TargetBottom;
        double Slot = OverlapLeft + 8.0;
        bool Accepted = false;
        List<(double X, double Y)> Candidate = [];
        while (Slot <= OverlapRight - 8.0)
        {
            Candidate = [(Slot, SourceFace), (Slot, TargetFace)];
            if (!RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId)
                && !CrossesAnotherGuide(Guides, Guide, Candidate))
            {
                Accepted = true;
                break;
            }

            Slot += 16.0;
        }

        if (!Accepted)
        {
            return;
        }

        Guide.Points.Clear();
        Guide.Points.AddRange(Candidate);
    }

    private static bool TailIntroducesObstacle(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<(double X, double Y)> Original,
        IReadOnlyList<(double X, double Y)> Candidate,
        string SourceId,
        string TargetId)
    {
        int FirstNew = Math.Max(1, Original.Count - 2);
        for (int Index = FirstNew; Index < Candidate.Count; Index++)
        {
            if (SegmentEntersNode(
                Contract,
                BoundsById,
                Candidate[Index - 1].X,
                Candidate[Index - 1].Y,
                Candidate[Index].X,
                Candidate[Index].Y,
                SourceId,
                TargetId))
            {
                return true;
            }
        }

        return false;
    }

    private static void BypassTouchingPair(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide)
    {
        if (!BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source) || Source == null
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target) || Target == null
            || !IsRouteableBox(Contract, Guide.Edge.SourceId)
            || !IsRouteableBox(Contract, Guide.Edge.TargetId))
        {
            return;
        }

        if (TryGapRoute(Contract, BoundsById, Guides, Guide, Source, Target))
        {
            return;
        }

        for (int Index = 1; Index < Guide.Points.Count; Index++)
        {
            (double X1, double Y1) = Guide.Points[Index - 1];
            (double X2, double Y2) = Guide.Points[Index];
            if (Math.Abs(X2 - X1) > 1.5 || Math.Abs(Y2 - Y1) < HUG_MIN_PX)
            {
                continue;
            }

            double SourceLeft = Source.CenterX - (Source.Width / 2.0);
            double SourceRight = Source.CenterX + (Source.Width / 2.0);
            double TargetLeft = Target.CenterX - (Target.Width / 2.0);
            double TargetRight = Target.CenterX + (Target.Width / 2.0);
            double SourceSide = Math.Abs(X1 - SourceRight) <= Math.Abs(X1 - SourceLeft) ? SourceRight : SourceLeft;
            double TargetSide = Math.Abs(X1 - TargetRight) <= Math.Abs(X1 - TargetLeft) ? TargetRight : TargetLeft;
            if (Math.Abs(X1 - SourceSide) > HUG_PX || Math.Abs(X1 - TargetSide) > HUG_PX || Math.Abs(SourceSide - TargetSide) > 12.0)
            {
                continue;
            }

            double SourceBottom = Source.CenterY - (Source.Height / 2.0);
            double SourceTop = Source.CenterY + (Source.Height / 2.0);
            double TargetBottom = Target.CenterY - (Target.Height / 2.0);
            double TargetTop = Target.CenterY + (Target.Height / 2.0);
            bool SourceAbove = SourceBottom >= TargetTop - 2.0;
            double UpperBottom = SourceAbove ? SourceBottom : TargetBottom;
            double LowerTop = SourceAbove ? TargetTop : SourceTop;
            double ChannelY = (UpperBottom + LowerTop) / 2.0;
            double UpperLeft = SourceAbove ? SourceLeft : TargetLeft;
            double UpperRight = SourceAbove ? SourceRight : TargetRight;
            double LowerLeft = SourceAbove ? TargetLeft : SourceLeft;
            double LowerRight = SourceAbove ? TargetRight : SourceRight;
            double UpperSlot = Math.Clamp(Math.Min(UpperRight, LowerRight) - CORNER_INSET_PX, UpperLeft + CORNER_INSET_PX, UpperRight - CORNER_INSET_PX);
            double LowerSlot = Math.Clamp(Math.Max(UpperLeft, LowerLeft) + CORNER_INSET_PX, LowerLeft + CORNER_INSET_PX, LowerRight - CORNER_INSET_PX);
            double SourceSlotX = SourceAbove ? UpperSlot : LowerSlot;
            double TargetSlotX = SourceAbove ? LowerSlot : UpperSlot;
            double SourceFaceY = SourceAbove ? SourceBottom : SourceTop;
            double TargetFaceY = SourceAbove ? TargetTop : TargetBottom;
            List<(double X, double Y)> Candidate =
            [
                (SourceSlotX, SourceFaceY),
                (SourceSlotX, ChannelY),
                (TargetSlotX, ChannelY),
                (TargetSlotX, TargetFaceY)
            ];
            if (RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId))
            {
                continue;
            }

            Guide.Points.Clear();
            Guide.Points.AddRange(Candidate);
            return;
        }
    }

    private static bool TryGapRoute(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target)
    {
        if (!RouteHugsABox(Contract, BoundsById, Guide))
        {
            return false;
        }

        double SourceLeft = Source.CenterX - (Source.Width / 2.0);
        double SourceRight = Source.CenterX + (Source.Width / 2.0);
        double SourceBottom = Source.CenterY - (Source.Height / 2.0);
        double SourceTop = Source.CenterY + (Source.Height / 2.0);
        double TargetLeft = Target.CenterX - (Target.Width / 2.0);
        double TargetRight = Target.CenterX + (Target.Width / 2.0);
        double TargetBottom = Target.CenterY - (Target.Height / 2.0);
        double TargetTop = Target.CenterY + (Target.Height / 2.0);
        bool SourceAbove = Source.CenterY >= Target.CenterY;
        double UpperBottom = SourceAbove ? SourceBottom : TargetBottom;
        double LowerTop = SourceAbove ? TargetTop : SourceTop;
        double UpperLeft = SourceAbove ? SourceLeft : TargetLeft;
        double UpperRight = SourceAbove ? SourceRight : TargetRight;
        double LowerLeft = SourceAbove ? TargetLeft : SourceLeft;
        double LowerRight = SourceAbove ? TargetRight : SourceRight;
        if (UpperRight - UpperLeft < (CORNER_INSET_PX * 2.0) || LowerRight - LowerLeft < (CORNER_INSET_PX * 2.0))
        {
            return false;
        }

        double UpperSlot = Math.Clamp((UpperLeft + UpperRight) / 2.0, UpperLeft + CORNER_INSET_PX, UpperRight - CORNER_INSET_PX);
        double LowerSlot = Math.Clamp(UpperSlot, LowerLeft + CORNER_INSET_PX, LowerRight - CORNER_INSET_PX);
        UpperSlot = Math.Clamp(LowerSlot, UpperLeft + CORNER_INSET_PX, UpperRight - CORNER_INSET_PX);
        double SourceSlotX = SourceAbove ? UpperSlot : LowerSlot;
        double TargetSlotX = SourceAbove ? LowerSlot : UpperSlot;
        double SourceFaceY = SourceAbove ? SourceBottom : SourceTop;
        double TargetFaceY = SourceAbove ? TargetTop : TargetBottom;
        double[] Channels =
        [
            (UpperBottom + LowerTop) / 2.0,
            UpperBottom - 10.0,
            LowerTop + 10.0
        ];
        foreach (double ChannelY in Channels)
        {
            if (ChannelY >= UpperBottom - 1.0 || ChannelY <= LowerTop + 1.0)
            {
                continue;
            }

            List<(double X, double Y)> Candidate =
            [
                (SourceSlotX, SourceFaceY),
                (SourceSlotX, ChannelY),
                (TargetSlotX, ChannelY),
                (TargetSlotX, TargetFaceY)
            ];
            if (RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId)
                || CrossesAnotherGuide(Guides, Guide, Candidate))
            {
                continue;
            }

            Guide.Points.Clear();
            Guide.Points.AddRange(Candidate);
            return true;
        }

        return false;
    }

    private static void NudgeOffGroupBorder(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide)
    {
        for (int Index = 1; Index < Guide.Points.Count; Index++)
        {
            (double X1, double Y1) = Guide.Points[Index - 1];
            (double X2, double Y2) = Guide.Points[Index];
            bool Vertical = Math.Abs(X2 - X1) <= 1.5 && Math.Abs(Y2 - Y1) >= HUG_MIN_PX;
            bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5 && Math.Abs(X2 - X1) >= HUG_MIN_PX;
            if (!Vertical && !Horizontal)
            {
                continue;
            }

            foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
            {
                if (IsNode(Contract, Pair.Key))
                {
                    continue;
                }

                FlowchartLayoutNodeBounds Bounds = Pair.Value;
                double Left = Bounds.CenterX - (Bounds.Width / 2.0);
                double Right = Bounds.CenterX + (Bounds.Width / 2.0);
                double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
                double Top = Bounds.CenterY + (Bounds.Height / 2.0);
                if (Vertical && AxisOverlap(Y1, Y2, Bottom, Top) >= HUG_MIN_PX)
                {
                    bool NearRight = Math.Abs(X1 - Right) <= Math.Abs(X1 - Left);
                    bool Outside = NearRight ? X1 >= Right - 1.0 : X1 <= Left + 1.0;
                    double Clearance = Outside ? GROUP_CLEAR_PX : 12.0;
                    double Side = NearRight ? Right : Left;
                    if (Math.Abs(X1 - Side) > Clearance)
                    {
                        continue;
                    }

                    double ClearX = NearRight
                        ? (X1 >= Right ? Right + Clearance : Right - Clearance)
                        : (X1 <= Left ? Left - Clearance : Left + Clearance);
                    if (Math.Abs(ClearX - X1) > Clearance)
                    {
                        continue;
                    }

                    ShiftX(Guide.Points, X1, ClearX);
                }
                else if (Horizontal && AxisOverlap(X1, X2, Left, Right) >= HUG_MIN_PX)
                {
                    bool NearTop = Math.Abs(Y1 - Top) <= Math.Abs(Y1 - Bottom);
                    bool Outside = NearTop ? Y1 >= Top - 1.0 : Y1 <= Bottom + 1.0;
                    double Clearance = Outside ? GROUP_CLEAR_PX : 12.0;
                    double Side = NearTop ? Top : Bottom;
                    if (Math.Abs(Y1 - Side) > Clearance)
                    {
                        continue;
                    }

                    double ClearY = Y1 >= Side ? Side + Clearance : Side - Clearance;
                    if (Math.Abs(ClearY - Y1) > Clearance)
                    {
                        continue;
                    }

                    ShiftY(Guide.Points, Y1, ClearY);
                }
            }
        }
    }

    private static void StepAsideTitle(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide)
    {
        for (int Index = Guide.Points.Count - 1; Index >= 1; Index--)
        {
            (double X1, double Y1) = Guide.Points[Index - 1];
            (double X2, double Y2) = Guide.Points[Index];
            if (Math.Abs(X2 - X1) > 1.5)
            {
                continue;
            }

            foreach (GroupContract Group in Contract.Groups)
            {
                if (string.IsNullOrWhiteSpace(Group.Title)
                    || !BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds? Bounds)
                    || Bounds == null)
                {
                    continue;
                }

                double Top = Bounds.CenterY + (Bounds.Height / 2.0);
                double BandBottom = Top - TITLE_BAND_PX;
                if (AxisOverlap(Y1, Y2, BandBottom, Top) < 8.0)
                {
                    continue;
                }

                double TitleWidth = Math.Max(48.0, Group.Title.Length * 16.0 * 0.58);
                double TitleLeft = Bounds.CenterX - (TitleWidth / 2.0);
                double TitleRight = Bounds.CenterX + (TitleWidth / 2.0);
                if (X1 < TitleLeft - TITLE_CLEAR_PX || X1 > TitleRight + TITLE_CLEAR_PX)
                {
                    continue;
                }

                double Low = Math.Min(Y1, Y2);
                double High = Math.Max(Y1, Y2);
                double LeftClear = TitleLeft - TITLE_CLEAR_PX;
                double RightClear = TitleRight + TITLE_CLEAR_PX;
                double TowardX = Index >= 2 ? Guide.Points[Index - 2].X : X1;
                double ClearX = Math.Abs(TowardX - LeftClear) <= Math.Abs(TowardX - RightClear) ? LeftClear : RightClear;
                if (VerticalHitsNode(Contract, BoundsById, Low, High, ClearX, Group.Id))
                {
                    ClearX = Math.Abs(ClearX - LeftClear) <= 0.1 ? RightClear : LeftClear;
                }

                if (VerticalHitsNode(Contract, BoundsById, Low, High, ClearX, Group.Id))
                {
                    continue;
                }

                Guide.Points[Index - 1] = (ClearX, Y1);
                Guide.Points[Index] = (ClearX, Y2);
                return;
            }
        }
    }

    private static bool RouteHugsABox(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide)
    {
        _ = Contract;
        for (int Index = 1; Index < Guide.Points.Count; Index++)
        {
            (double X1, double Y1) = Guide.Points[Index - 1];
            (double X2, double Y2) = Guide.Points[Index];
            bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5 && Math.Abs(X2 - X1) >= HUG_MIN_PX;
            bool Vertical = Math.Abs(X2 - X1) <= 1.5 && Math.Abs(Y2 - Y1) >= HUG_MIN_PX;
            if (!Horizontal && !Vertical)
            {
                continue;
            }

            if (!BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? SourceBounds) || SourceBounds == null)
            {
                return false;
            }

            if (!BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? TargetBounds) || TargetBounds == null)
            {
                return false;
            }

            FlowchartLayoutNodeBounds[] Ends = [SourceBounds, TargetBounds];
            foreach (FlowchartLayoutNodeBounds Bounds in Ends)
            {
                double Left = Bounds.CenterX - (Bounds.Width / 2.0);
                double Right = Bounds.CenterX + (Bounds.Width / 2.0);
                double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
                double Top = Bounds.CenterY + (Bounds.Height / 2.0);
                if (Horizontal && AxisOverlap(X1, X2, Left, Right) >= HUG_MIN_PX
                    && (Math.Abs(Y1 - Top) <= HUG_PX || Math.Abs(Y1 - Bottom) <= HUG_PX))
                {
                    return true;
                }

                if (Vertical && AxisOverlap(Y1, Y2, Bottom, Top) >= HUG_MIN_PX
                    && (Math.Abs(X1 - Left) <= HUG_PX || Math.Abs(X1 - Right) <= HUG_PX))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool RouteCrossesTitle(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.IsNullOrWhiteSpace(Group.Title)
                || !BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds? Bounds)
                || Bounds == null)
            {
                continue;
            }

            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            double BandBottom = Top - TITLE_BAND_PX;
            double TitleWidth = Math.Max(48.0, Group.Title.Length * 7.2);
            double TitleLeft = Bounds.CenterX - (TitleWidth / 2.0);
            double TitleRight = Bounds.CenterX + (TitleWidth / 2.0);
            for (int Index = 1; Index < Guide.Points.Count; Index++)
            {
                (double X1, double Y1) = Guide.Points[Index - 1];
                (double X2, double Y2) = Guide.Points[Index];
                if (Math.Abs(X2 - X1) > 1.5)
                {
                    continue;
                }

                if (X1 > TitleLeft && X1 < TitleRight && AxisOverlap(Y1, Y2, BandBottom, Top) > 8.0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryRebuildFacingRoute(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide,
        out List<(double X, double Y)> Points)
    {
        Points = [];
        if (!BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source)
            || Source == null
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target)
            || Target == null)
        {
            return false;
        }

        double SourceLeft = Source.CenterX - (Source.Width / 2.0);
        double SourceRight = Source.CenterX + (Source.Width / 2.0);
        double SourceBottom = Source.CenterY - (Source.Height / 2.0);
        double SourceTop = Source.CenterY + (Source.Height / 2.0);
        double TargetLeft = Target.CenterX - (Target.Width / 2.0);
        double TargetRight = Target.CenterX + (Target.Width / 2.0);
        double TargetBottom = Target.CenterY - (Target.Height / 2.0);
        double TargetTop = Target.CenterY + (Target.Height / 2.0);
        double DeltaX = Target.CenterX - Source.CenterX;
        double DeltaY = Target.CenterY - Source.CenterY;
        double SourceX;
        double SourceY;
        double TargetX;
        double TargetY;
        if (Math.Abs(DeltaY) >= Math.Abs(DeltaX))
        {
            SourceY = DeltaY >= 0.0 ? SourceTop : SourceBottom;
            TargetY = DeltaY >= 0.0 ? TargetBottom : TargetTop;
            double OverlapLeft = Math.Max(SourceLeft, TargetLeft) + CORNER_INSET_PX;
            double OverlapRight = Math.Min(SourceRight, TargetRight) - CORNER_INSET_PX;
            double Slot = OverlapRight > OverlapLeft
                ? (OverlapLeft + OverlapRight) / 2.0
                : Math.Clamp(Target.CenterX, SourceLeft + CORNER_INSET_PX, SourceRight - CORNER_INSET_PX);
            SourceX = Slot;
            TargetX = Math.Clamp(Slot, TargetLeft + CORNER_INSET_PX, TargetRight - CORNER_INSET_PX);
        }
        else
        {
            SourceX = DeltaX >= 0.0 ? SourceRight : SourceLeft;
            TargetX = DeltaX >= 0.0 ? TargetLeft : TargetRight;
            double OverlapBottom = Math.Max(SourceBottom, TargetBottom) + CORNER_INSET_PX;
            double OverlapTop = Math.Min(SourceTop, TargetTop) - CORNER_INSET_PX;
            double Slot = OverlapTop > OverlapBottom
                ? (OverlapBottom + OverlapTop) / 2.0
                : Math.Clamp(Target.CenterY, SourceBottom + CORNER_INSET_PX, SourceTop - CORNER_INSET_PX);
            SourceY = Slot;
            TargetY = Math.Clamp(Slot, TargetBottom + CORNER_INSET_PX, TargetTop - CORNER_INSET_PX);
        }

        List<(double X, double Y)> Candidate = [(SourceX, SourceY), (TargetX, TargetY)];
        if (Math.Abs(SourceX - TargetX) > 1.5 && Math.Abs(SourceY - TargetY) > 1.5)
        {
            Candidate = [(SourceX, SourceY), (SourceX, TargetY), (TargetX, TargetY)];
            if (SegmentEntersNode(Contract, BoundsById, SourceX, SourceY, SourceX, TargetY, Guide.Edge.SourceId, Guide.Edge.TargetId)
                || SegmentEntersNode(Contract, BoundsById, SourceX, TargetY, TargetX, TargetY, Guide.Edge.SourceId, Guide.Edge.TargetId))
            {
                Candidate = [(SourceX, SourceY), (TargetX, SourceY), (TargetX, TargetY)];
            }
        }

        if (RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId))
        {
            return false;
        }

        Points = Candidate;
        return true;
    }

    private static bool CrossesAnotherGuide(
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        IReadOnlyList<(double X, double Y)> Candidate)
    {
        foreach (FlowchartResolvedGuide Other in Guides)
        {
            if (ReferenceEquals(Other, Guide) || Other.Points.Count < 2)
            {
                continue;
            }

            for (int Left = 1; Left < Candidate.Count; Left++)
            {
                for (int Right = 1; Right < Other.Points.Count; Right++)
                {
                    if (SegmentsCross(
                        Candidate[Left - 1],
                        Candidate[Left],
                        Other.Points[Right - 1],
                        Other.Points[Right]))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool SegmentsCross(
        (double X, double Y) A,
        (double X, double Y) B,
        (double X, double Y) C,
        (double X, double Y) D)
    {
        double Denominator = ((B.X - A.X) * (D.Y - C.Y)) - ((B.Y - A.Y) * (D.X - C.X));
        if (Math.Abs(Denominator) < 1e-6)
        {
            return false;
        }

        double Ua = (((C.X - A.X) * (D.Y - C.Y)) - ((C.Y - A.Y) * (D.X - C.X))) / Denominator;
        double Ub = (((C.X - A.X) * (B.Y - A.Y)) - ((C.Y - A.Y) * (B.X - A.X))) / Denominator;
        return Ua > 0.02 && Ua < 0.98 && Ub > 0.02 && Ub < 0.98;
    }

    private static bool RouteStillBlocked(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<(double X, double Y)> Points,
        string SourceId,
        string TargetId)
    {
        for (int Index = 1; Index < Points.Count; Index++)
        {
            if (SegmentEntersNode(
                Contract,
                BoundsById,
                Points[Index - 1].X,
                Points[Index - 1].Y,
                Points[Index].X,
                Points[Index].Y,
                SourceId,
                TargetId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SegmentEntersNode(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double X1,
        double Y1,
        double X2,
        double Y2,
        string SourceId,
        string TargetId)
    {
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
        {
            if (!IsNode(Contract, Pair.Key)
                || string.Equals(Pair.Key, SourceId, StringComparison.Ordinal)
                || string.Equals(Pair.Key, TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            FlowchartLayoutNodeBounds Bounds = Pair.Value;
            double Left = Bounds.CenterX - (Bounds.Width / 2.0);
            double Right = Bounds.CenterX + (Bounds.Width / 2.0);
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            if (Math.Abs(Y2 - Y1) <= 1.5 && Y1 > Bottom + 1.0 && Y1 < Top - 1.0 && AxisOverlap(X1, X2, Left, Right) > 4.0)
            {
                return true;
            }

            if (Math.Abs(X2 - X1) <= 1.5 && X1 > Left + 1.0 && X1 < Right - 1.0 && AxisOverlap(Y1, Y2, Bottom, Top) > 4.0)
            {
                return true;
            }

            if (Math.Abs(Y2 - Y1) <= 1.5
                && AxisOverlap(X1, X2, Left, Right) > 20.0
                && (Math.Abs(Y1 - Top) <= HUG_PX || Math.Abs(Y1 - Bottom) <= HUG_PX))
            {
                return true;
            }

            if (Math.Abs(X2 - X1) <= 1.5
                && AxisOverlap(Y1, Y2, Bottom, Top) > 20.0
                && (Math.Abs(X1 - Left) <= HUG_PX || Math.Abs(X1 - Right) <= HUG_PX))
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveSourceDogleg(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide)
    {
        if (Guide.Points.Count < 3 || !BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source) || Source == null)
        {
            return;
        }

        double Left = Source.CenterX - (Source.Width / 2.0);
        double Right = Source.CenterX + (Source.Width / 2.0);
        double Bottom = Source.CenterY - (Source.Height / 2.0);
        double Top = Source.CenterY + (Source.Height / 2.0);
        if (Guide.Points.Count >= 4)
        {
            (double A0X, double A0Y) = Guide.Points[0];
            (double A1X, double A1Y) = Guide.Points[1];
            (double A2X, double A2Y) = Guide.Points[2];
            (double A3X, double A3Y) = Guide.Points[3];
            double FirstLength = Math.Sqrt(Math.Pow(A1X - A0X, 2.0) + Math.Pow(A1Y - A0Y, 2.0));
            double SecondLength = Math.Sqrt(Math.Pow(A2X - A1X, 2.0) + Math.Pow(A2Y - A1Y, 2.0));
            bool FirstVertical = Math.Abs(A1X - A0X) <= 1.5;
            bool ThirdVertical = Math.Abs(A3X - A2X) <= 1.5;
            bool FirstHorizontal = Math.Abs(A1Y - A0Y) <= 1.5;
            bool ThirdHorizontal = Math.Abs(A3Y - A2Y) <= 1.5;
            if (FirstLength <= 18.0
                && SecondLength <= 18.0
                && FirstLength > 1.0
                && ((FirstVertical && ThirdVertical) || (FirstHorizontal && ThirdHorizontal)))
            {
                if ((FirstVertical && Math.Abs(A0Y - Top) <= 3.0) || (FirstVertical && Math.Abs(A0Y - Bottom) <= 3.0))
                {
                    double FaceY = Math.Abs(A0Y - Top) <= Math.Abs(A0Y - Bottom) ? Top : Bottom;
                    double Slot = Math.Clamp(A2X, Left + CORNER_INSET_PX, Right - CORNER_INSET_PX);
                    Guide.Points[0] = (Slot, FaceY);
                    Guide.Points.RemoveAt(1);
                    Guide.Points.RemoveAt(1);
                    return;
                }

                if (FirstHorizontal && (Math.Abs(A0X - Left) <= 3.0 || Math.Abs(A0X - Right) <= 3.0))
                {
                    double FaceX = Math.Abs(A0X - Left) <= Math.Abs(A0X - Right) ? Left : Right;
                    double Slot = Math.Clamp(A2Y, Bottom + CORNER_INSET_PX, Top - CORNER_INSET_PX);
                    Guide.Points[0] = (FaceX, Slot);
                    Guide.Points.RemoveAt(1);
                    Guide.Points.RemoveAt(1);
                    return;
                }
            }
        }

        (double X0, double Y0) = Guide.Points[0];
        (double X1, double Y1) = Guide.Points[1];
        double Length = Math.Sqrt(Math.Pow(X1 - X0, 2.0) + Math.Pow(Y1 - Y0, 2.0));
        if (Length > FlowchartGeometryPolicy.ShortInitialStubMaxPx
            || Length < FlowchartGeometryPolicy.ShortInitialStubMinPx)
        {
            return;
        }

        bool HorizontalFace = Math.Abs(Y0 - Top) <= 3.0 || Math.Abs(Y0 - Bottom) <= 3.0;
        bool Slides = (HorizontalFace && Math.Abs(Y1 - Y0) <= 1.5) || (!HorizontalFace && Math.Abs(X1 - X0) <= 1.5);
        if (!Slides)
        {
            return;
        }

        if (HorizontalFace)
        {
            double Slot = Math.Clamp(X1, Left + CORNER_INSET_PX, Right - CORNER_INSET_PX);
            double FaceY = Math.Abs(Y0 - Top) <= Math.Abs(Y0 - Bottom) ? Top : Bottom;
            Guide.Points[0] = (Slot, FaceY);
            if (Math.Abs(Guide.Points[1].X - Slot) <= 1.5 && Math.Abs(Guide.Points[1].Y - FaceY) <= 1.5)
            {
                Guide.Points.RemoveAt(1);
            }
        }
        else
        {
            double Slot = Math.Clamp(Y1, Bottom + CORNER_INSET_PX, Top - CORNER_INSET_PX);
            double FaceX = Math.Abs(X0 - Left) <= Math.Abs(X0 - Right) ? Left : Right;
            Guide.Points[0] = (FaceX, Slot);
            if (Math.Abs(Guide.Points[1].Y - Slot) <= 1.5 && Math.Abs(Guide.Points[1].X - FaceX) <= 1.5)
            {
                Guide.Points.RemoveAt(1);
            }
        }
    }

    private static void InsetEndpoint(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartResolvedGuide Guide,
        bool AtSource)
    {
        string NodeId = AtSource ? Guide.Edge.SourceId : Guide.Edge.TargetId;
        if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds) || Bounds == null || Guide.Points.Count < 2)
        {
            return;
        }

        int EndIndex = AtSource ? 0 : Guide.Points.Count - 1;
        int OtherIndex = AtSource ? 1 : Guide.Points.Count - 2;
        (double EndX, double EndY) = Guide.Points[EndIndex];
        (double OtherX, double OtherY) = Guide.Points[OtherIndex];
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        double Corner = DistanceToCorner(EndX, EndY, Left, Right, Bottom, Top);
        bool VerticalRun = Math.Abs(EndX - OtherX) <= 1.5;
        bool HorizontalRun = Math.Abs(EndY - OtherY) <= 1.5;
        bool AlongSide = (VerticalRun && (Math.Abs(EndX - Left) <= HUG_PX || Math.Abs(EndX - Right) <= HUG_PX))
            || (HorizontalRun && (Math.Abs(EndY - Top) <= HUG_PX || Math.Abs(EndY - Bottom) <= HUG_PX));
        if (Corner > CORNER_INSET_PX && !AlongSide)
        {
            return;
        }

        bool ApproachVertical = Math.Abs(EndX - OtherX) <= Math.Abs(EndY - OtherY);
        double PortX = EndX;
        double PortY = EndY;
        if (ApproachVertical || AlongSide && VerticalRun)
        {
            PortY = Math.Abs(EndY - Top) <= Math.Abs(EndY - Bottom) ? Top : Bottom;
            if (AlongSide && VerticalRun && Corner > CORNER_INSET_PX)
            {
                PortX = Math.Abs(EndX - Right) <= Math.Abs(EndX - Left) ? Right : Left;
                PortY = Math.Clamp(EndY, Bottom + CORNER_INSET_PX, Top - CORNER_INSET_PX);
                ApproachVertical = false;
            }
            else
            {
                PortX = Math.Clamp(EndX, Left + CORNER_INSET_PX, Right - CORNER_INSET_PX);
            }
        }
        else
        {
            PortX = Math.Abs(EndX - Right) <= Math.Abs(EndX - Left) ? Right : Left;
            PortY = Math.Clamp(EndY, Bottom + CORNER_INSET_PX, Top - CORNER_INSET_PX);
        }

        if (!ApproachVertical && AlongSide && VerticalRun)
        {
            double ChannelX = OtherX;
            Guide.Points[EndIndex] = (PortX, PortY);
            if (AtSource)
            {
                Guide.Points.Insert(1, (ChannelX, PortY));
            }
            else
            {
                Guide.Points.Insert(EndIndex, (ChannelX, PortY));
            }
        }
        else if (ApproachVertical)
        {
            Guide.Points[EndIndex] = (PortX, PortY);
            Guide.Points[OtherIndex] = (PortX, OtherY);
        }
        else
        {
            Guide.Points[EndIndex] = (PortX, PortY);
            Guide.Points[OtherIndex] = (OtherX, PortY);
        }

        _ = NodeById;
    }

    private static double DistanceToCorner(double X, double Y, double Left, double Right, double Bottom, double Top)
    {
        double Best = double.PositiveInfinity;
        double[] Xs = [Left, Right];
        double[] Ys = [Bottom, Top];
        foreach (double CornerX in Xs)
        {
            foreach (double CornerY in Ys)
            {
                Best = Math.Min(Best, Math.Sqrt(Math.Pow(X - CornerX, 2.0) + Math.Pow(Y - CornerY, 2.0)));
            }
        }

        return Best;
    }

    private static void ShiftY(List<(double X, double Y)> Points, double OldY, double NewY)
    {
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            if (Math.Abs(Points[Index].Y - OldY) <= 1.5)
            {
                Points[Index] = (Points[Index].X, NewY);
            }
        }
    }

    private static void ShiftX(List<(double X, double Y)> Points, double OldX, double NewX)
    {
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            if (Math.Abs(Points[Index].X - OldX) <= 1.5)
            {
                Points[Index] = (NewX, Points[Index].Y);
            }
        }
    }

    private static bool TryAcceptHorizontal(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double X1,
        double X2,
        double Y,
        string IgnoredId)
    {
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
        {
            if (string.Equals(Pair.Key, IgnoredId, StringComparison.Ordinal) || !IsNode(Contract, Pair.Key))
            {
                continue;
            }

            FlowchartLayoutNodeBounds Bounds = Pair.Value;
            double Left = Bounds.CenterX - (Bounds.Width / 2.0);
            double Right = Bounds.CenterX + (Bounds.Width / 2.0);
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            if (Y > Bottom + 1.0 && Y < Top - 1.0 && AxisOverlap(X1, X2, Left, Right) > 8.0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryAcceptVertical(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double Y1,
        double Y2,
        double X,
        string IgnoredId)
    {
        return !VerticalHitsNode(Contract, BoundsById, Math.Min(Y1, Y2), Math.Max(Y1, Y2), X, IgnoredId);
    }

    private static bool VerticalHitsNode(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double Low,
        double High,
        double X,
        string IgnoredId)
    {
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
        {
            if (string.Equals(Pair.Key, IgnoredId, StringComparison.Ordinal) || !IsNode(Contract, Pair.Key))
            {
                continue;
            }

            FlowchartLayoutNodeBounds Bounds = Pair.Value;
            double Left = Bounds.CenterX - (Bounds.Width / 2.0);
            double Right = Bounds.CenterX + (Bounds.Width / 2.0);
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            if (X > Left + 1.0 && X < Right - 1.0 && AxisOverlap(Low, High, Bottom, Top) > 8.0)
            {
                return true;
            }
        }

        return false;
    }

    private static double RoundedCap(
        DiagramContract Contract,
        string NodeId,
        FlowchartLayoutNodeBounds Bounds)
    {
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!string.Equals(Node.Id, NodeId, StringComparison.Ordinal))
            {
                continue;
            }

            string Shape = Node.Shape ?? string.Empty;
            if (string.Equals(Shape, "stadium", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Shape, "round", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Shape, "circle", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Min(Bounds.Width, Bounds.Height) / 2.0;
            }
        }

        return 0.0;
    }

    private static bool IsRouteableBox(DiagramContract Contract, string NodeId)
    {
        if (IsPlainBox(Contract, NodeId))
        {
            return true;
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.Equals(Group.Id, NodeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPlainBox(DiagramContract Contract, string NodeId)
    {
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!string.Equals(Node.Id, NodeId, StringComparison.Ordinal))
            {
                continue;
            }

            string Shape = Node.Shape ?? "rect";
            return string.Equals(Shape, "rect", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Shape, "square", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Shape, "round", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Shape, "rounded", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Shape, "stadium", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Shape, "subroutine", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Shape, "process", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Shape, "event", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsNode(DiagramContract Contract, string NodeId)
    {
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (string.Equals(Node.Id, NodeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGroup(DiagramContract Contract, string NodeId)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.Equals(Group.Id, NodeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static double AxisOverlap(double A1, double A2, double B1, double B2)
    {
        return Math.Min(Math.Max(A1, A2), Math.Max(B1, B2)) - Math.Max(Math.Min(A1, A2), Math.Min(B1, B2));
    }

    private static void ClipRouteToTargetBorder(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide)
    {
        if (Guide.Points.Count < 2
            || !IsNode(Contract, Guide.Edge.TargetId)
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target)
            || Target == null)
        {
            return;
        }

        double Left = Target.CenterX - (Target.Width / 2.0);
        double Right = Target.CenterX + (Target.Width / 2.0);
        double Bottom = Target.CenterY - (Target.Height / 2.0);
        double Top = Target.CenterY + (Target.Height / 2.0);
        for (int Index = 1; Index < Guide.Points.Count; Index++)
        {
            (double X1, double Y1) = Guide.Points[Index - 1];
            (double X2, double Y2) = Guide.Points[Index];
            if (!IsStrictlyInside(X2, Y2, Left, Right, Bottom, Top))
            {
                continue;
            }

            if (!TryBorderHit(X1, Y1, X2, Y2, Left, Right, Bottom, Top, out double HitX, out double HitY))
            {
                continue;
            }

            Guide.Points[Index] = (HitX, HitY);
            if (Index < Guide.Points.Count - 1)
            {
                Guide.Points.RemoveRange(Index + 1, Guide.Points.Count - (Index + 1));
            }

            break;
        }
    }

    private static bool IsStrictlyInside(
        double X,
        double Y,
        double Left,
        double Right,
        double Bottom,
        double Top)
    {
        return X > Left + 1.0 && X < Right - 1.0 && Y > Bottom + 1.0 && Y < Top - 1.0;
    }

    private static bool TryBorderHit(
        double X1,
        double Y1,
        double X2,
        double Y2,
        double Left,
        double Right,
        double Bottom,
        double Top,
        out double HitX,
        out double HitY)
    {
        HitX = X2;
        HitY = Y2;
        double BestT = double.PositiveInfinity;
        double DeltaX = X2 - X1;
        double DeltaY = Y2 - Y1;
        if (Math.Abs(DeltaX) > 0.001)
        {
            ConsiderBorderT(X1, Y1, DeltaX, DeltaY, (Left - X1) / DeltaX, Bottom, Top, true, Left, ref BestT, ref HitX, ref HitY);
            ConsiderBorderT(X1, Y1, DeltaX, DeltaY, (Right - X1) / DeltaX, Bottom, Top, true, Right, ref BestT, ref HitX, ref HitY);
        }

        if (Math.Abs(DeltaY) > 0.001)
        {
            ConsiderBorderT(X1, Y1, DeltaX, DeltaY, (Bottom - Y1) / DeltaY, Left, Right, false, Bottom, ref BestT, ref HitX, ref HitY);
            ConsiderBorderT(X1, Y1, DeltaX, DeltaY, (Top - Y1) / DeltaY, Left, Right, false, Top, ref BestT, ref HitX, ref HitY);
        }

        return !double.IsPositiveInfinity(BestT);
    }

    private static void ConsiderBorderT(
        double X1,
        double Y1,
        double DeltaX,
        double DeltaY,
        double T,
        double RangeMin,
        double RangeMax,
        bool VerticalEdge,
        double EdgeCoordinate,
        ref double BestT,
        ref double HitX,
        ref double HitY)
    {
        if (T <= 0.02 || T >= 0.98 || T >= BestT)
        {
            return;
        }

        double X = X1 + (DeltaX * T);
        double Y = Y1 + (DeltaY * T);
        double Across = VerticalEdge ? Y : X;
        if (Across < RangeMin - 0.5 || Across > RangeMax + 0.5)
        {
            return;
        }

        BestT = T;
        HitX = VerticalEdge ? EdgeCoordinate : X;
        HitY = VerticalEdge ? Y : EdgeCoordinate;
    }

    private static void PreferDirectOrthogonal(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide)
    {
        if (Guide.Points.Count < 3
            || CountBends(Guide.Points) <= 1
            || !BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source)
            || Source == null
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target)
            || Target == null)
        {
            return;
        }

        double SourceLeft = Source.CenterX - (Source.Width / 2.0);
        double SourceRight = Source.CenterX + (Source.Width / 2.0);
        double SourceBottom = Source.CenterY - (Source.Height / 2.0);
        double SourceTop = Source.CenterY + (Source.Height / 2.0);
        double TargetLeft = Target.CenterX - (Target.Width / 2.0);
        double TargetRight = Target.CenterX + (Target.Width / 2.0);
        double TargetBottom = Target.CenterY - (Target.Height / 2.0);
        double TargetTop = Target.CenterY + (Target.Height / 2.0);
        bool TargetBelow = Target.CenterY < Source.CenterY - 8.0;
        bool TargetAbove = Target.CenterY > Source.CenterY + 8.0;
        if (TargetBelow || TargetAbove)
        {
            double BandLeft = Math.Max(SourceLeft + CORNER_INSET_PX, TargetLeft + CORNER_INSET_PX);
            double BandRight = Math.Min(SourceRight - CORNER_INSET_PX, TargetRight - CORNER_INSET_PX);
            if (BandRight - BandLeft >= 8.0)
            {
                double SharedX = Math.Clamp(Target.CenterX, BandLeft, BandRight);
                double SourceY = TargetBelow ? SourceBottom : SourceTop;
                double TargetY = TargetBelow ? TargetTop : TargetBottom;
                List<(double X, double Y)> Straight = [(SharedX, SourceY), (SharedX, TargetY)];
                if (AcceptsDirect(Contract, BoundsById, Guides, Guide, Straight))
                {
                    return;
                }
            }
        }

        (double StartX, double StartY) = Guide.Points[0];
        (double EndX, double EndY) = Guide.Points[Guide.Points.Count - 1];
        List<(double X, double Y)> ElbowA = [(StartX, StartY), (EndX, StartY), (EndX, EndY)];
        List<(double X, double Y)> ElbowB = [(StartX, StartY), (StartX, EndY), (EndX, EndY)];
        bool FirstLeavesVertically = Math.Abs(Guide.Points[1].X - StartX) <= 1.5;
        List<(double X, double Y)> Preferred = FirstLeavesVertically ? ElbowB : ElbowA;
        List<(double X, double Y)> Alternate = FirstLeavesVertically ? ElbowA : ElbowB;
        if (CountBends(Preferred) < CountBends(Guide.Points) && AcceptsDirect(Contract, BoundsById, Guides, Guide, Preferred))
        {
            return;
        }

        if (CountBends(Alternate) < CountBends(Guide.Points) && AcceptsDirect(Contract, BoundsById, Guides, Guide, Alternate))
        {
            return;
        }

        TrySlideSourceToSingleBend(Contract, BoundsById, Guides, Guide, Source, Target, TargetBelow, TargetAbove);
    }

    private static bool RouteHugsBox(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<(double X, double Y)> Points)
    {
        for (int Index = 1; Index < Points.Count; Index++)
        {
            (double X1, double Y1) = Points[Index - 1];
            (double X2, double Y2) = Points[Index];
            bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5 && Math.Abs(X2 - X1) >= HUG_MIN_PX;
            bool Vertical = Math.Abs(X2 - X1) <= 1.5 && Math.Abs(Y2 - Y1) >= HUG_MIN_PX;
            if (!Horizontal && !Vertical)
            {
                continue;
            }

            foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
            {
                FlowchartLayoutNodeBounds Bounds = Pair.Value;
                double Left = Bounds.CenterX - (Bounds.Width / 2.0);
                double Right = Bounds.CenterX + (Bounds.Width / 2.0);
                double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
                double Top = Bounds.CenterY + (Bounds.Height / 2.0);
                if (Horizontal && AxisOverlap(X1, X2, Left, Right) >= HUG_MIN_PX
                    && (Math.Abs(Y1 - Top) <= HUG_PX || Math.Abs(Y1 - Bottom) <= HUG_PX))
                {
                    return true;
                }

                if (Vertical && AxisOverlap(Y1, Y2, Bottom, Top) >= HUG_MIN_PX
                    && (Math.Abs(X1 - Left) <= HUG_PX || Math.Abs(X1 - Right) <= HUG_PX))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void PullArrivalOffCorner(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide)
    {
        if (Guide.Points.Count < 2
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target)
            || Target == null)
        {
            return;
        }

        int Last = Guide.Points.Count - 1;
        (double TipX, double TipY) = Guide.Points[Last];
        (double PrevX, double PrevY) = Guide.Points[Last - 1];
        double Left = Target.CenterX - (Target.Width / 2.0);
        double Right = Target.CenterX + (Target.Width / 2.0);
        double Bottom = Target.CenterY - (Target.Height / 2.0);
        double Top = Target.CenterY + (Target.Height / 2.0);
        bool Horizontal = Math.Abs(TipY - PrevY) <= 1.5 && Math.Abs(TipX - PrevX) >= 8.0;
        bool Vertical = Math.Abs(TipX - PrevX) <= 1.5 && Math.Abs(TipY - PrevY) >= 8.0;
        if (!Horizontal && !Vertical)
        {
            return;
        }

        double Corner = DistanceToCorner(TipX, TipY, Left, Right, Bottom, Top);
        if (Corner >= ARRIVAL_CLEAR_PX)
        {
            return;
        }

        if (Horizontal)
        {
            if (Top - Bottom < (ARRIVAL_CLEAR_PX * 2.0))
            {
                return;
            }

            double PortY = Math.Clamp(TipY, Bottom + ARRIVAL_CLEAR_PX, Top - ARRIVAL_CLEAR_PX);
            List<(double X, double Y)> Candidate = new List<(double X, double Y)>(Guide.Points);
            Candidate[Last] = (TipX, PortY);
            Candidate[Last - 1] = (PrevX, PortY);
            if (RouteHugsBox(BoundsById, Candidate))
            {
                return;
            }

            Guide.Points[Last] = (TipX, PortY);
            Guide.Points[Last - 1] = (PrevX, PortY);
            return;
        }

        if (Right - Left < (ARRIVAL_CLEAR_PX * 2.0))
        {
            return;
        }

        double PortX = Math.Clamp(TipX, Left + ARRIVAL_CLEAR_PX, Right - ARRIVAL_CLEAR_PX);
        List<(double X, double Y)> Shifted = new List<(double X, double Y)>(Guide.Points);
        Shifted[Last] = (PortX, TipY);
        Shifted[Last - 1] = (PortX, PrevY);
        if (RouteHugsBox(BoundsById, Shifted))
        {
            return;
        }

        Guide.Points[Last] = (PortX, TipY);
        Guide.Points[Last - 1] = (PortX, PrevY);
    }

    private static bool RunsAlongEndpoint(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string NodeId,
        IReadOnlyList<(double X, double Y)> Points,
        bool AtStart)
    {
        if (Points.Count < 2
            || !BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds)
            || Bounds == null)
        {
            return false;
        }

        int First = AtStart ? 0 : Points.Count - 2;
        int Second = AtStart ? 1 : Points.Count - 1;
        (double X1, double Y1) = Points[First];
        (double X2, double Y2) = Points[Second];
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5 && Math.Abs(X2 - X1) > 8.0;
        bool Vertical = Math.Abs(X2 - X1) <= 1.5 && Math.Abs(Y2 - Y1) > 8.0;
        if (Horizontal
            && AxisOverlap(X1, X2, Left, Right) > 8.0
            && (Math.Abs(Y1 - Top) <= HUG_PX || Math.Abs(Y1 - Bottom) <= HUG_PX))
        {
            return true;
        }

        if (Vertical
            && AxisOverlap(Y1, Y2, Bottom, Top) > 8.0
            && (Math.Abs(X1 - Left) <= HUG_PX || Math.Abs(X1 - Right) <= HUG_PX))
        {
            return true;
        }

        return false;
    }

    private static bool ClosesOnAnotherGuide(
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        IReadOnlyList<(double X, double Y)> Candidate)
    {
        foreach (FlowchartResolvedGuide Other in Guides)
        {
            if (ReferenceEquals(Other, Guide) || Other.Points.Count < 2)
            {
                continue;
            }

            for (int LeftIndex = 1; LeftIndex < Candidate.Count; LeftIndex++)
            {
                (double LeftX1, double LeftY1) = Candidate[LeftIndex - 1];
                (double LeftX2, double LeftY2) = Candidate[LeftIndex];
                for (int RightIndex = 1; RightIndex < Other.Points.Count; RightIndex++)
                {
                    (double RightX1, double RightY1) = Other.Points[RightIndex - 1];
                    (double RightX2, double RightY2) = Other.Points[RightIndex];
                    bool Horizontal = Math.Abs(LeftY2 - LeftY1) <= 1.5
                        && Math.Abs(RightY2 - RightY1) <= 1.5
                        && Math.Abs(LeftX2 - LeftX1) >= 24.0
                        && Math.Abs(RightX2 - RightX1) >= 24.0
                        && AxisOverlap(LeftX1, LeftX2, RightX1, RightX2) >= 24.0;
                    bool Vertical = Math.Abs(LeftX2 - LeftX1) <= 1.5
                        && Math.Abs(RightX2 - RightX1) <= 1.5
                        && Math.Abs(LeftY2 - LeftY1) >= 24.0
                        && Math.Abs(RightY2 - RightY1) >= 24.0
                        && AxisOverlap(LeftY1, LeftY2, RightY1, RightY2) >= 24.0;
                    if (Horizontal && Math.Abs(LeftY1 - RightY1) < 8.0)
                    {
                        return true;
                    }

                    if (Vertical && Math.Abs(LeftX1 - RightX1) < 8.0)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static void TrySlideSourceToSingleBend(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target,
        bool TargetBelow,
        bool TargetAbove)
    {
        if ((!TargetBelow && !TargetAbove) || CountBends(Guide.Points) <= 1)
        {
            return;
        }

        (double EndX, double EndY) = Guide.Points[Guide.Points.Count - 1];
        double SourceLeft = Source.CenterX - (Source.Width / 2.0);
        double SourceRight = Source.CenterX + (Source.Width / 2.0);
        double FaceY = TargetBelow ? Source.CenterY - (Source.Height / 2.0) : Source.CenterY + (Source.Height / 2.0);
        bool TowardRight = Target.CenterX >= Source.CenterX;
        double[] Margins = [12.0, 16.0, 20.0, 28.0];
        foreach (double Margin in Margins)
        {
            if ((SourceRight - SourceLeft) < (Margin * 2.0))
            {
                continue;
            }

            double PortX = TowardRight ? SourceRight - Margin : SourceLeft + Margin;
            List<(double X, double Y)> Elbow =
            [
                (PortX, FaceY),
                (PortX, EndY),
                (EndX, EndY)
            ];
            if (CountBends(Elbow) < CountBends(Guide.Points)
                && AcceptsDirect(Contract, BoundsById, Guides, Guide, Elbow))
            {
                return;
            }
        }
    }

    private static bool AcceptsDirect(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        List<(double X, double Y)> Candidate)
    {
        FlowchartPaddedVisibilityRouter.CollapseReversalSpurs(Candidate);
        if (Candidate.Count < 2
            || RouteHugsBox(BoundsById, Candidate)
            || RunsAlongEndpoint(BoundsById, Guide.Edge.SourceId, Candidate, true)
            || RunsAlongEndpoint(BoundsById, Guide.Edge.TargetId, Candidate, false)
            || RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId)
            || CrossesAnotherGuide(Guides, Guide, Candidate)
            || ClosesOnAnotherGuide(Guides, Guide, Candidate)
            || !TerminalsLeaveOnTheNormal(BoundsById, Guide, Candidate))
        {
            return false;
        }

        Guide.Points.Clear();
        Guide.Points.AddRange(Candidate);
        Guide.RouteKind = Candidate.Count <= 2
            ? FlowchartEdgeRouteKind.Straight
            : FlowchartEdgeRouteKind.Corridor;
        return true;
    }

    private static int CountBends(IReadOnlyList<(double X, double Y)> Points)
    {
        int Bends = 0;
        for (int Index = 2; Index < Points.Count; Index++)
        {
            double InX = Points[Index - 1].X - Points[Index - 2].X;
            double InY = Points[Index - 1].Y - Points[Index - 2].Y;
            double OutX = Points[Index].X - Points[Index - 1].X;
            double OutY = Points[Index].Y - Points[Index - 1].Y;
            if ((Math.Abs(InX) > 1.5 || Math.Abs(InY) > 1.5)
                && (Math.Abs(OutX) > 1.5 || Math.Abs(OutY) > 1.5)
                && Math.Abs((InX * OutY) - (InY * OutX)) > 1.0)
            {
                Bends++;
            }
        }

        return Bends;
    }

    private static void SeparateSharedSourceChannels(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        List<FlowchartResolvedGuide> Guides)
    {
        for (int LeftIndex = 0; LeftIndex < Guides.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Guides.Count; RightIndex++)
            {
                FlowchartResolvedGuide Left = Guides[LeftIndex];
                FlowchartResolvedGuide Right = Guides[RightIndex];
                if (!SharesEdgeEndpoint(Left.Edge, Right.Edge))
                {
                    continue;
                }

                if (LongestSharedRun(Left.Points, Right.Points) < 36.0)
                {
                    continue;
                }

                if (TryShiftOffSharedRun(Contract, BoundsById, Guides, Left, Right)
                    || TryShiftOffSharedRun(Contract, BoundsById, Guides, Right, Left)
                    || TryAlternateSourcePort(Contract, BoundsById, Guides, Left, Right)
                    || TryAlternateSourcePort(Contract, BoundsById, Guides, Right, Left)
                    || TryShoulderToTargetSide(Contract, BoundsById, Guides, Left, Right)
                    || TryShoulderToTargetSide(Contract, BoundsById, Guides, Right, Left)
                    || TryUnderRowToTarget(Contract, BoundsById, Guides, Left, Right)
                    || TryUnderRowToTarget(Contract, BoundsById, Guides, Right, Left))
                {
                    continue;
                }
            }
        }
    }

    private static bool SharesEdgeEndpoint(EdgeContract Left, EdgeContract Right)
    {
        return string.Equals(Left.SourceId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.SourceId, Right.TargetId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.SourceId, StringComparison.Ordinal)
            || string.Equals(Left.TargetId, Right.TargetId, StringComparison.Ordinal);
    }

    private static bool TryShiftOffSharedRun(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        FlowchartResolvedGuide Other)
    {
        if (Guide.Points.Count < 3)
        {
            return false;
        }

        double[] Offsets = [16.0, -16.0, 24.0, -24.0, 36.0, -36.0];
        foreach (double Offset in Offsets)
        {
            List<(double X, double Y)> Candidate = new List<(double X, double Y)>(Guide.Points);
            bool Horizontal = SharedAxisIsHorizontal(Guide.Points, Other.Points);
            bool Moved = false;
            for (int Index = 1; Index < Candidate.Count - 1; Index++)
            {
                bool OnShared = false;
                for (int OtherIndex = 1; OtherIndex < Other.Points.Count; OtherIndex++)
                {
                    if (Horizontal
                        && Math.Abs(Candidate[Index].Y - Other.Points[OtherIndex].Y) <= 2.5
                        && Math.Abs(Other.Points[OtherIndex].Y - Other.Points[OtherIndex - 1].Y) <= 1.5)
                    {
                        OnShared = true;
                        break;
                    }

                    if (!Horizontal
                        && Math.Abs(Candidate[Index].X - Other.Points[OtherIndex].X) <= 2.5
                        && Math.Abs(Other.Points[OtherIndex].X - Other.Points[OtherIndex - 1].X) <= 1.5)
                    {
                        OnShared = true;
                        break;
                    }
                }

                if (!OnShared)
                {
                    continue;
                }

                if (Horizontal)
                {
                    Candidate[Index] = (Candidate[Index].X, Candidate[Index].Y + Offset);
                }
                else
                {
                    Candidate[Index] = (Candidate[Index].X + Offset, Candidate[Index].Y);
                }

                Moved = true;
            }

            if (!Moved)
            {
                continue;
            }

            Candidate = FlowchartPaddedVisibilityRouter.EnsureOrthogonal(Candidate);
            bool Blocked = RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId);
            bool Crosses = CrossesAnotherGuide(Guides, Guide, Candidate);
            double SharedAfter = LongestSharedRun(Candidate, Other.Points);
            FlowchartPaddedVisibilityRouter.CollapseReversalSpurs(Candidate);
            if (RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId)
                || CrossesAnotherGuide(Guides, Guide, Candidate)
                || LongestSharedRun(Candidate, Other.Points) >= 36.0)
            {
                continue;
            }

            Guide.Points.Clear();
            Guide.Points.AddRange(Candidate);
            Guide.RouteKind = Candidate.Count <= 2
                ? FlowchartEdgeRouteKind.Straight
                : FlowchartEdgeRouteKind.Corridor;
            return true;
        }

        return false;
    }

    private static bool SharedAxisIsHorizontal(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right)
    {
        double Horizontal = 0.0;
        double Vertical = 0.0;
        for (int LeftIndex = 1; LeftIndex < Left.Count; LeftIndex++)
        {
            bool LeftHorizontal = Math.Abs(Left[LeftIndex].Y - Left[LeftIndex - 1].Y) <= 1.5;
            bool LeftVertical = Math.Abs(Left[LeftIndex].X - Left[LeftIndex - 1].X) <= 1.5;
            for (int RightIndex = 1; RightIndex < Right.Count; RightIndex++)
            {
                double Overlap = 0.0;
                if (LeftHorizontal && Math.Abs(Right[RightIndex].Y - Right[RightIndex - 1].Y) <= 1.5
                    && Math.Abs(Left[LeftIndex].Y - Right[RightIndex].Y) <= 2.5)
                {
                    Overlap = AxisOverlap(Left[LeftIndex - 1].X, Left[LeftIndex].X, Right[RightIndex - 1].X, Right[RightIndex].X);
                    Horizontal = Math.Max(Horizontal, Overlap);
                }
                else if (LeftVertical && Math.Abs(Right[RightIndex].X - Right[RightIndex - 1].X) <= 1.5
                    && Math.Abs(Left[LeftIndex].X - Right[RightIndex].X) <= 2.5)
                {
                    Overlap = AxisOverlap(Left[LeftIndex - 1].Y, Left[LeftIndex].Y, Right[RightIndex - 1].Y, Right[RightIndex].Y);
                    Vertical = Math.Max(Vertical, Overlap);
                }
            }
        }

        return Horizontal >= Vertical;
    }

    private static bool TryAlternateSourcePort(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        FlowchartResolvedGuide Other)
    {
        if (!string.Equals(Guide.Edge.SourceId, Other.Edge.SourceId, StringComparison.Ordinal)
            || !BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source)
            || Source == null
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target)
            || Target == null
            || Guide.Points.Count < 2)
        {
            return false;
        }

        List<FlowchartLayoutNodeBounds> Obstacles = FlowchartPaddedVisibilityRouter.CollectObstacles(
            Contract,
            BoundsById,
            Guide.Edge);
        (double EndX, double EndY) = Guide.Points[Guide.Points.Count - 1];
        double[] Slots = [0.28, 0.5, 0.72];
        foreach (double Slot in Slots)
        {
            (double BottomX, double BottomY) = SlotOnFace(Source, "bottom", Slot);
            (double LeftX, double LeftY) = SlotOnFace(Source, "left", Slot);
            (double RightX, double RightY) = SlotOnFace(Source, "right", Slot);
            if (TryAcceptReroute(Contract, BoundsById, Guides, Guide, Other, Obstacles, Source, Target, BottomX, BottomY, EndX, EndY)
                || TryAcceptReroute(Contract, BoundsById, Guides, Guide, Other, Obstacles, Source, Target, LeftX, LeftY, EndX, EndY)
                || TryAcceptReroute(Contract, BoundsById, Guides, Guide, Other, Obstacles, Source, Target, RightX, RightY, EndX, EndY))
            {
                return true;
            }
        }

        return false;
    }

    private static (double X, double Y) SlotOnFace(FlowchartLayoutNodeBounds Bounds, string Face, double Slot)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        double SpanX = Right - Left;
        double SpanY = Top - Bottom;
        double X = Left + (SpanX * Slot);
        double Y = Bottom + (SpanY * Slot);
        double InnerLeft = Left + CORNER_INSET_PX;
        double InnerRight = Right - CORNER_INSET_PX;
        double InnerBottom = Bottom + CORNER_INSET_PX;
        double InnerTop = Top - CORNER_INSET_PX;
        if (InnerLeft >= InnerRight)
        {
            X = Bounds.CenterX;
            InnerLeft = X;
            InnerRight = X;
        }

        if (InnerBottom >= InnerTop)
        {
            Y = Bounds.CenterY;
            InnerBottom = Y;
            InnerTop = Y;
        }

        if (string.Equals(Face, "bottom", StringComparison.Ordinal))
        {
            return (Math.Clamp(X, InnerLeft, InnerRight), Bottom);
        }

        if (string.Equals(Face, "top", StringComparison.Ordinal))
        {
            return (Math.Clamp(X, InnerLeft, InnerRight), Top);
        }

        if (string.Equals(Face, "left", StringComparison.Ordinal))
        {
            return (Left, Math.Clamp(Y, InnerBottom, InnerTop));
        }

        return (Right, Math.Clamp(Y, InnerBottom, InnerTop));
    }

    private static bool TryAcceptReroute(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        FlowchartResolvedGuide Other,
        List<FlowchartLayoutNodeBounds> Obstacles,
        FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target,
        double StartX,
        double StartY,
        double EndX,
        double EndY)
    {
        if (!FlowchartPaddedVisibilityRouter.TryRoute(
            StartX,
            StartY,
            EndX,
            EndY,
            Obstacles,
            Source,
            Target,
            out List<(double X, double Y)> Routed))
        {
            return false;
        }

        if (RouteStillBlocked(Contract, BoundsById, Routed, Guide.Edge.SourceId, Guide.Edge.TargetId)
            || CrossesAnotherGuide(Guides, Guide, Routed)
            || LongestSharedRun(Routed, Other.Points) >= 36.0)
        {
            return false;
        }

        Guide.Points.Clear();
        Guide.Points.AddRange(Routed);
        Guide.RouteKind = Routed.Count <= 2
            ? FlowchartEdgeRouteKind.Straight
            : FlowchartEdgeRouteKind.Corridor;
        return true;
    }

    private static double LongestSharedRun(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right)
    {
        double Best = 0.0;
        for (int LeftIndex = 1; LeftIndex < Left.Count; LeftIndex++)
        {
            bool LeftHorizontal = Math.Abs(Left[LeftIndex].Y - Left[LeftIndex - 1].Y) <= 1.5;
            bool LeftVertical = Math.Abs(Left[LeftIndex].X - Left[LeftIndex - 1].X) <= 1.5;
            for (int RightIndex = 1; RightIndex < Right.Count; RightIndex++)
            {
                double Overlap = 0.0;
                if (LeftHorizontal
                    && Math.Abs(Right[RightIndex].Y - Right[RightIndex - 1].Y) <= 1.5
                    && Math.Abs(Left[LeftIndex].Y - Right[RightIndex].Y) <= 2.5)
                {
                    Overlap = AxisOverlap(
                        Left[LeftIndex - 1].X,
                        Left[LeftIndex].X,
                        Right[RightIndex - 1].X,
                        Right[RightIndex].X);
                }
                else if (LeftVertical
                    && Math.Abs(Right[RightIndex].X - Right[RightIndex - 1].X) <= 1.5
                    && Math.Abs(Left[LeftIndex].X - Right[RightIndex].X) <= 2.5)
                {
                    Overlap = AxisOverlap(
                        Left[LeftIndex - 1].Y,
                        Left[LeftIndex].Y,
                        Right[RightIndex - 1].Y,
                        Right[RightIndex].Y);
                }

                if (Overlap > Best)
                {
                    Best = Overlap;
                }
            }
        }

        return Best;
    }

    /// <summary>
    /// Slides an end that sits on a corner inward along its face, further on a long face.
    /// </summary>
    private static void PullEndsOffCorners(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide)
    {
        PullOneEndOffCorner(Contract, BoundsById, Guide, AtSource: true);
        PullOneEndOffCorner(Contract, BoundsById, Guide, AtSource: false);
    }

    private static void PullOneEndOffCorner(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide,
        bool AtSource)
    {
        string NodeId = AtSource ? Guide.Edge.SourceId : Guide.Edge.TargetId;
        if (Guide.Points.Count < 2
            || !BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds)
            || Bounds == null)
        {
            return;
        }

        int TipIndex = AtSource ? 0 : Guide.Points.Count - 1;
        int PrevIndex = AtSource ? 1 : Guide.Points.Count - 2;
        (double TipX, double TipY) = Guide.Points[TipIndex];
        (double PrevX, double PrevY) = Guide.Points[PrevIndex];
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        bool Horizontal = Math.Abs(TipY - PrevY) <= 1.5 && Math.Abs(TipX - PrevX) >= 8.0;
        bool Vertical = Math.Abs(TipX - PrevX) <= 1.5 && Math.Abs(TipY - PrevY) >= 8.0;
        if (!Horizontal && !Vertical)
        {
            return;
        }

        double FaceLength = Vertical ? Right - Left : Top - Bottom;
        double Clear = FaceLength >= FlowchartGeometryPolicy.WideArrivalFaceMinPx
            ? FlowchartGeometryPolicy.WideArrivalCornerClearancePx
            : ARRIVAL_CLEAR_PX;
        if (DistanceToCorner(TipX, TipY, Left, Right, Bottom, Top) >= Clear)
        {
            return;
        }

        List<(double X, double Y)> Candidate = new List<(double X, double Y)>(Guide.Points);
        if (Vertical)
        {
            if (Right - Left < (Clear * 2.0))
            {
                return;
            }

            double PortX = Math.Clamp(TipX, Left + Clear, Right - Clear);
            Candidate[TipIndex] = (PortX, TipY);
            Candidate[PrevIndex] = (PortX, PrevY);
        }
        else
        {
            if (Top - Bottom < (Clear * 2.0))
            {
                return;
            }

            double PortY = Math.Clamp(TipY, Bottom + Clear, Top - Clear);
            Candidate[TipIndex] = (TipX, PortY);
            Candidate[PrevIndex] = (PrevX, PortY);
        }

        if (!TailIntroducesObstacle(Contract, BoundsById, Guide.Points, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId))
        {
            Guide.Points.Clear();
            Guide.Points.AddRange(Candidate);
            return;
        }

        if (!AtSource)
        {
            TrySideEntry(Contract, BoundsById, Guide, Bounds, Clear);
        }
    }

    private static void TrySideEntry(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide,
        FlowchartLayoutNodeBounds Bounds,
        double Clear)
    {
        if (Guide.Points.Count < 3)
        {
            return;
        }

        int Last = Guide.Points.Count - 1;
        (double TipX, double TipY) = Guide.Points[Last];
        (double PrevX, double PrevY) = Guide.Points[Last - 1];
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        bool Vertical = Math.Abs(TipX - PrevX) <= 1.5;
        if (!Vertical || Top - Bottom < (Clear * 2.0))
        {
            return;
        }

        bool FromLeft = PrevX <= Bounds.CenterX;
        double SideX = FromLeft ? Left : Right;
        double SideY = Math.Clamp(TipY + (TipY >= Bounds.CenterY ? -Clear : Clear), Bottom + Clear, Top - Clear);
        double ApproachX = FromLeft ? SideX - 14.0 : SideX + 14.0;
        List<(double X, double Y)> Candidate = new List<(double X, double Y)>(Guide.Points);
        Candidate[Last - 1] = (ApproachX, PrevY);
        Candidate.Insert(Last, (ApproachX, SideY));
        Candidate[Candidate.Count - 1] = (SideX, SideY);
        FlowchartPaddedVisibilityRouter.CollapseReversalSpurs(Candidate);
        if (TailIntroducesObstacle(Contract, BoundsById, Guide.Points, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId))
        {
            return;
        }

        Guide.Points.Clear();
        Guide.Points.AddRange(Candidate);
        Guide.RouteKind = FlowchartEdgeRouteKind.Corridor;
    }

    /// <summary>
    /// Moves a rounded or stadium end along its terminal segment until it sits on the outline.
    /// </summary>
    private static void SnapRoundedOutline(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartResolvedGuide Guide)
    {
        SnapOneRoundedEnd(BoundsById, NodeById, Guide, AtSource: true);
        SnapOneRoundedEnd(BoundsById, NodeById, Guide, AtSource: false);
    }

    private static void SnapOneRoundedEnd(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartResolvedGuide Guide,
        bool AtSource)
    {
        string NodeId = AtSource ? Guide.Edge.SourceId : Guide.Edge.TargetId;
        if (Guide.Points.Count < 2
            || !NodeById.TryGetValue(NodeId, out NodeContract? Node)
            || Node == null
            || !BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds)
            || Bounds == null
            || !TryRoundedRadius(Node.Shape, Bounds.Height, out double Radius))
        {
            return;
        }

        int TipIndex = AtSource ? 0 : Guide.Points.Count - 1;
        int OtherIndex = AtSource ? 1 : Guide.Points.Count - 2;
        (double TipX, double TipY) = Guide.Points[TipIndex];
        (double OtherX, double OtherY) = Guide.Points[OtherIndex];
        bool Vertical = Math.Abs(TipX - OtherX) <= 1.5;
        bool Horizontal = Math.Abs(TipY - OtherY) <= 1.5;
        if (!Vertical && !Horizontal)
        {
            PullLooseRoundedEnd(Guide, TipIndex, OtherIndex, Bounds, Radius, TipX, TipY, OtherX, OtherY);
            return;
        }

        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        double FlatLeft = Left + Radius;
        double FlatRight = Right - Radius;
        double FlatBottom = Bottom + Radius;
        double FlatTop = Top - Radius;
        if (Vertical)
        {
            double NewX = TipX;
            double OutlineY = TipY;
            if (TipX >= FlatLeft && TipX <= FlatRight)
            {
                if (TipY <= Top + 0.5 && TipY >= Bottom - 0.5)
                {
                    return;
                }

                OutlineY = TipY > Top ? Top : Bottom;
            }
            else
            {
                double CapX = TipX < Bounds.CenterX ? FlatLeft : FlatRight;
                double DeltaX = TipX - CapX;
                if (Math.Abs(DeltaX) >= Radius - 0.5)
                {
                    NewX = TipX < Bounds.CenterX ? FlatLeft + 2.0 : FlatRight - 2.0;
                    OutlineY = TipY >= Bounds.CenterY ? Top : Bottom;
                }
                else
                {
                    double Distance = Math.Sqrt((DeltaX * DeltaX) + Math.Pow(TipY - Bounds.CenterY, 2.0));
                    if (Distance <= Radius + 1.5)
                    {
                        return;
                    }

                    double DeltaY = Math.Sqrt((Radius * Radius) - (DeltaX * DeltaX));
                    OutlineY = TipY >= Bounds.CenterY ? Bounds.CenterY + DeltaY : Bounds.CenterY - DeltaY;
                }
            }

            if (Math.Abs(OutlineY - TipY) <= 1.5 && Math.Abs(NewX - TipX) <= 1.5)
            {
                return;
            }

            Guide.Points[TipIndex] = (NewX, OutlineY);
            if (Math.Abs(NewX - TipX) > 1.5 && Math.Abs(OtherX - TipX) <= 1.5)
            {
                Guide.Points[OtherIndex] = (NewX, OtherY);
            }

            return;
        }

        double OutlineX = TipX;
        if (TipY >= FlatBottom && TipY <= FlatTop)
        {
            OutlineX = TipX >= Bounds.CenterX ? Right : Left;
        }
        else if (Radius < (Bounds.Height / 2.0) - 0.5)
        {
            double CapY = TipY < Bounds.CenterY ? FlatBottom : FlatTop;
            double DeltaY = TipY - CapY;
            if (Math.Abs(DeltaY) >= Radius)
            {
                return;
            }

            double DeltaX = Math.Sqrt((Radius * Radius) - (DeltaY * DeltaY));
            OutlineX = TipX >= Bounds.CenterX ? Bounds.CenterX + (Bounds.Width / 2.0) - Radius + DeltaX : Bounds.CenterX - (Bounds.Width / 2.0) + Radius - DeltaX;
        }
        else
        {
            double DeltaY = TipY - Bounds.CenterY;
            if (Math.Abs(DeltaY) >= Radius - 0.5)
            {
                return;
            }

            double CapX = TipX < Bounds.CenterX ? FlatLeft : FlatRight;
            double DeltaX = Math.Sqrt((Radius * Radius) - (DeltaY * DeltaY));
            OutlineX = TipX < Bounds.CenterX ? CapX - DeltaX : CapX + DeltaX;
        }

        if (Math.Abs(OutlineX - TipX) <= 1.5)
        {
            return;
        }

        Guide.Points[TipIndex] = (OutlineX, TipY);
    }

    /// <summary>
    /// Pulls a diagonal stadium or rounded end onto the arc and leaves on a horizontal stub.
    /// </summary>
    private static void PullLooseRoundedEnd(
        FlowchartResolvedGuide Guide,
        int TipIndex,
        int OtherIndex,
        FlowchartLayoutNodeBounds Bounds,
        double Radius,
        double TipX,
        double TipY,
        double OtherX,
        double OtherY)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double CentreY = Bounds.CenterY;
        double FlatLeft = Left + Radius;
        double FlatRight = Right - Radius;
        if (TipX >= FlatLeft && TipX <= FlatRight)
        {
            return;
        }

        double DeltaY = TipY - CentreY;
        if (Math.Abs(DeltaY) >= Radius - 0.5)
        {
            return;
        }

        double CapX = TipX < Bounds.CenterX ? FlatLeft : FlatRight;
        double Reach = Math.Sqrt((Radius * Radius) - (DeltaY * DeltaY));
        double OutlineX = TipX < Bounds.CenterX ? CapX - Reach : CapX + Reach;
        bool Outside = TipX < Bounds.CenterX ? TipX < OutlineX - 1.5 : TipX > OutlineX + 1.5;
        if (!Outside)
        {
            return;
        }

        Guide.Points[TipIndex] = (OutlineX, TipY);
        if (Math.Abs(OtherY - TipY) <= 1.5)
        {
            return;
        }

        Guide.Points.Insert(Math.Min(TipIndex, OtherIndex) + 1, (OtherX, TipY));
    }

    private static bool TryRoundedRadius(string? Shape, double Height, out double Radius)
    {
        Radius = 0.0;
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim().ToLowerInvariant();
        if (string.Equals(Normalised, "stadium", StringComparison.Ordinal))
        {
            Radius = Height / 2.0;
            return Radius > 1.0;
        }

        if (string.Equals(Normalised, "rounded", StringComparison.Ordinal))
        {
            Radius = Math.Min(5.0, Height / 2.0);
            return Radius > 1.0;
        }

        return false;
    }

    /// <summary>
    /// Inserts a straight stub when a source or target segment no longer leaves the face on its normal.
    /// </summary>
    private static void RestorePerpendicularStubs(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartResolvedGuide Guide)
    {
        if (!BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source)
            || Source == null
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target)
            || Target == null)
        {
            return;
        }

        string? SourceShape = NodeById.TryGetValue(Guide.Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
        string? TargetShape = NodeById.TryGetValue(Guide.Edge.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;
        if (FlowchartPerpendicularAttachValidator.TryValidateGuidePoints(
            Guide.Points,
            Source,
            SourceShape,
            Target,
            TargetShape,
            AllowDiagonalCrossEdge: false,
            out string Failure)
            || string.IsNullOrEmpty(Failure)
            || Failure.Contains("diamond", StringComparison.Ordinal))
        {
            return;
        }

        List<(double X, double Y)> Repaired = FlowchartEdgeTerminalStubExpander.Apply(
            Guide.Points,
            Source,
            SourceShape,
            Target,
            TargetShape);
        if (Repaired.Count == Guide.Points.Count
            && Failure.Contains("Source attach", StringComparison.Ordinal)
            && Guide.Points.Count < 3)
        {
            (double BoundaryX, double BoundaryY) = Guide.Points[0];
            (double NextX, double NextY) = Guide.Points[1];
            (double StubX, double StubY) = FlowchartEdgeTerminalStubExpander.ResolveExteriorStubPoint(
                Source,
                SourceShape,
                BoundaryX,
                BoundaryY,
                NextX,
                NextY);
            if (Math.Abs(StubX - BoundaryX) > 0.5 || Math.Abs(StubY - BoundaryY) > 0.5)
            {
                Repaired = [(BoundaryX, BoundaryY), (StubX, StubY), (NextX, NextY)];
            }
        }

        DropReturnOntoSourceFace(Repaired, Source);
        if (!FlowchartPerpendicularAttachValidator.TryValidateGuidePoints(
            Repaired,
            Source,
            SourceShape,
            Target,
            TargetShape,
            AllowDiagonalCrossEdge: false,
            out _))
        {
            return;
        }

        Guide.Points.Clear();
        Guide.Points.AddRange(Repaired);
        Guide.RouteKind = Repaired.Count <= 2
            ? FlowchartEdgeRouteKind.Straight
            : FlowchartEdgeRouteKind.Corridor;
    }

    /// <summary>
    /// Drops a waypoint that sits back on the source face after a perpendicular stub, which otherwise
    /// renders as a hook onto the outline.
    /// </summary>
    private static void DropReturnOntoSourceFace(
        List<(double X, double Y)> Points,
        FlowchartLayoutNodeBounds Source)
    {
        double Left = Source.CenterX - (Source.Width / 2.0);
        double Right = Source.CenterX + (Source.Width / 2.0);
        double Bottom = Source.CenterY - (Source.Height / 2.0);
        double Top = Source.CenterY + (Source.Height / 2.0);
        while (Points.Count >= 4 && LiesOnBoxEdge(Points[2].X, Points[2].Y, Left, Right, Bottom, Top))
        {
            Points.RemoveAt(2);
        }
    }

    private static bool LiesOnBoxEdge(
        double X,
        double Y,
        double Left,
        double Right,
        double Bottom,
        double Top)
    {
        bool OnHorizontal = (Math.Abs(Y - Top) <= 2.0 || Math.Abs(Y - Bottom) <= 2.0)
            && X >= Left - 2.0
            && X <= Right + 2.0;
        bool OnVertical = (Math.Abs(X - Left) <= 2.0 || Math.Abs(X - Right) <= 2.0)
            && Y >= Bottom - 2.0
            && Y <= Top + 2.0;
        return OnHorizontal || OnVertical;
    }

    private static bool TerminalsLeaveOnTheNormal(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide,
        IReadOnlyList<(double X, double Y)> Points)
    {
        if (!BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source)
            || Source == null
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target)
            || Target == null)
        {
            return true;
        }

        return FlowchartPerpendicularAttachValidator.TryValidateGuidePoints(
            Points,
            Source,
            null,
            Target,
            null,
            AllowDiagonalCrossEdge: false,
            out _);
    }

    /// <summary>
    /// Drops a shared run onto a clear lane and brings it into the target's facing side.
    /// </summary>
    private static bool TryShoulderToTargetSide(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        FlowchartResolvedGuide Other)
    {
        if (LongestSharedRun(Guide.Points, Other.Points) < SHARED_MIN_PX
            || !BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source)
            || Source == null
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target)
            || Target == null
            || Target.CenterY > Source.CenterY - 8.0)
        {
            return false;
        }

        double SourceLeft = Source.CenterX - (Source.Width / 2.0);
        double SourceRight = Source.CenterX + (Source.Width / 2.0);
        double SourceBottom = Source.CenterY - (Source.Height / 2.0);
        double TargetLeft = Target.CenterX - (Target.Width / 2.0);
        double TargetRight = Target.CenterX + (Target.Width / 2.0);
        double TargetBottom = Target.CenterY - (Target.Height / 2.0);
        double TargetTop = Target.CenterY + (Target.Height / 2.0);
        bool FromRight = Source.CenterX >= Target.CenterX;
        double SideX = FromRight ? TargetRight : TargetLeft;
        double SideY = Math.Clamp(Target.CenterY, TargetBottom + CORNER_INSET_PX, TargetTop - CORNER_INSET_PX);
        double ApproachX = FromRight ? SideX + 14.0 : SideX - 14.0;
        double[] PortSlots = [0.2, 0.35, 0.5, 0.65, 0.8];
        double[] Drops = [16.0, 24.0, 32.0, 40.0, 46.0, 50.0, 54.0, 64.0, 72.0];
        foreach (double Slot in PortSlots)
        {
            double PortX = SourceLeft + ((SourceRight - SourceLeft) * Slot);
            PortX = Math.Clamp(PortX, SourceLeft + CORNER_INSET_PX, SourceRight - CORNER_INSET_PX);
            foreach (double Drop in Drops)
            {
                double LaneY = SourceBottom - Drop;
                if (LaneY <= TargetTop + 8.0)
                {
                    continue;
                }

                List<(double X, double Y)> Candidate =
                [
                    (PortX, SourceBottom),
                    (PortX, LaneY),
                    (ApproachX, LaneY),
                    (ApproachX, SideY),
                    (SideX, SideY)
                ];
                FlowchartPaddedVisibilityRouter.CollapseReversalSpurs(Candidate);
                if (Candidate.Count < 2
                    || RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId)
                    || CrossesAnotherGuide(Guides, Guide, Candidate)
                    || ClosesOnAnotherGuide(Guides, Guide, Candidate)
                    || LongestSharedRun(Candidate, Other.Points) >= SHARED_MIN_PX
                    || SharesALongRunWithAny(Guides, Guide, Candidate))
                {
                    continue;
                }

                Guide.Points.Clear();
                Guide.Points.AddRange(Candidate);
                Guide.RouteKind = FlowchartEdgeRouteKind.Corridor;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sends one of a shared pair out beside its source and back underneath the target row.
    /// </summary>
    private static bool TryUnderRowToTarget(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        FlowchartResolvedGuide Other)
    {
        if (LongestSharedRun(Guide.Points, Other.Points) < SHARED_MIN_PX
            || !BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source)
            || Source == null
            || !BoundsById.TryGetValue(Guide.Edge.TargetId, out FlowchartLayoutNodeBounds? Target)
            || Target == null
            || Target.CenterY > Source.CenterY - 8.0)
        {
            return false;
        }

        double SourceLeft = Source.CenterX - (Source.Width / 2.0);
        double SourceRight = Source.CenterX + (Source.Width / 2.0);
        double SourceBottom = Source.CenterY - (Source.Height / 2.0);
        double SourceTop = Source.CenterY + (Source.Height / 2.0);
        double TargetLeft = Target.CenterX - (Target.Width / 2.0);
        double TargetRight = Target.CenterX + (Target.Width / 2.0);
        double TargetBottom = Target.CenterY - (Target.Height / 2.0);
        if (SourceTop - SourceBottom < (CORNER_INSET_PX * 2.0) || TargetRight - TargetLeft < (CORNER_INSET_PX * 2.0))
        {
            return false;
        }

        double ExitY = Math.Clamp(Source.CenterY, SourceBottom + CORNER_INSET_PX, SourceTop - CORNER_INSET_PX);
        double PortX = Math.Clamp(Target.CenterX, TargetLeft + CORNER_INSET_PX, TargetRight - CORNER_INSET_PX);
        bool SourceIsRight = Source.CenterX >= Target.CenterX;
        bool[] Outward = [true, false];
        double[] Gutters = [20.0, 32.0, 44.0];
        double[] Drops = [20.0, 32.0, 48.0, 72.0, 110.0, 150.0];
        foreach (bool Away in Outward)
        {
            bool UseRight = Away ? SourceIsRight : !SourceIsRight;
            double FaceX = UseRight ? SourceRight : SourceLeft;
            foreach (double Gutter in Gutters)
            {
                double DropX = UseRight ? FaceX + Gutter : FaceX - Gutter;
                foreach (double Drop in Drops)
                {
                    double LaneY = TargetBottom - Drop;
                    if (LaneY >= SourceBottom - 8.0)
                    {
                        continue;
                    }

                    List<(double X, double Y)> Candidate =
                    [
                        (FaceX, ExitY),
                        (DropX, ExitY),
                        (DropX, LaneY),
                        (PortX, LaneY),
                        (PortX, TargetBottom)
                    ];
                    List<(double X, double Y)> Saved = new List<(double X, double Y)>(Guide.Points);
                    FlowchartEdgeRouteKind SavedKind = Guide.RouteKind;
                    if (!AcceptsDirect(Contract, BoundsById, Guides, Guide, Candidate)
                        || LongestSharedRun(Guide.Points, Other.Points) >= SHARED_MIN_PX
                        || SharesALongRunWithAny(Guides, Guide, Guide.Points))
                    {
                        Guide.Points.Clear();
                        Guide.Points.AddRange(Saved);
                        Guide.RouteKind = SavedKind;
                        continue;
                    }

                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Removes a bend that runs on for less than two pixels, which reads as a dangling stub.
    /// </summary>
    private static void DropTinyAxisSteps(List<(double X, double Y)> Points)
    {
        for (int Index = 1; Index < Points.Count; Index++)
        {
            double DeltaX = Points[Index].X - Points[Index - 1].X;
            double DeltaY = Points[Index].Y - Points[Index - 1].Y;
            if (Math.Abs(DeltaX) < 2.0 && Math.Abs(DeltaX) > 0.05 && Math.Abs(DeltaY) > 8.0)
            {
                Points[Index - 1] = (Points[Index].X, Points[Index - 1].Y);
            }
            else if (Math.Abs(DeltaY) < 2.0 && Math.Abs(DeltaY) > 0.05 && Math.Abs(DeltaX) > 8.0)
            {
                Points[Index - 1] = (Points[Index - 1].X, Points[Index].Y);
            }
        }

        bool Changed = true;
        while (Changed && Points.Count > 2)
        {
            Changed = false;
            for (int Index = 1; Index < Points.Count - 1; Index++)
            {
                double Step = Math.Sqrt(
                    Math.Pow(Points[Index].X - Points[Index - 1].X, 2.0)
                    + Math.Pow(Points[Index].Y - Points[Index - 1].Y, 2.0));
                if (Step >= 2.0 || Step < 0.2)
                {
                    continue;
                }

                bool SameX = Math.Abs(Points[Index].X - Points[Index + 1].X) <= 1.5;
                bool SameY = Math.Abs(Points[Index].Y - Points[Index + 1].Y) <= 1.5;
                if (!SameX && !SameY)
                {
                    continue;
                }

                double KeptX = SameX ? Points[Index].X : Points[Index - 1].X;
                double KeptY = SameY ? Points[Index].Y : Points[Index - 1].Y;
                Points[Index - 1] = (KeptX, KeptY);
                Points.RemoveAt(Index);
                Changed = true;
                break;
            }
        }
    }

    private static bool SharesALongRunWithAny(
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        IReadOnlyList<(double X, double Y)> Candidate)
    {
        foreach (FlowchartResolvedGuide Other in Guides)
        {
            if (ReferenceEquals(Other, Guide))
            {
                continue;
            }

            if (LongestSharedRun(Candidate, Other.Points) >= SHARED_MIN_PX)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Moves a short run that sits on a group side just outside that side, so the edge crosses the border once.
    /// </summary>
    private static void LiftShortGroupBorderRun(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide)
    {
        if (Guide.Points.Count < 2)
        {
            return;
        }

        for (int Index = 1; Index < Guide.Points.Count; Index++)
        {
            (double X1, double Y1) = Guide.Points[Index - 1];
            (double X2, double Y2) = Guide.Points[Index];
            double Length = Math.Sqrt(Math.Pow(X2 - X1, 2.0) + Math.Pow(Y2 - Y1, 2.0));
            bool Vertical = Math.Abs(X2 - X1) <= 1.5 && Length >= 4.0 && Length < HUG_MIN_PX;
            bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5 && Length >= 4.0 && Length < HUG_MIN_PX;
            if (!Vertical && !Horizontal)
            {
                continue;
            }

            foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in BoundsById)
            {
                if (string.Equals(Pair.Key, Guide.Edge.SourceId, StringComparison.Ordinal)
                    || string.Equals(Pair.Key, Guide.Edge.TargetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsNode(Contract, Pair.Key) && !IsGroup(Contract, Pair.Key))
                {
                    continue;
                }

                FlowchartLayoutNodeBounds Bounds = Pair.Value;
                double Left = Bounds.CenterX - (Bounds.Width / 2.0);
                double Right = Bounds.CenterX + (Bounds.Width / 2.0);
                double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
                double Top = Bounds.CenterY + (Bounds.Height / 2.0);
                if (Vertical && AxisOverlap(Y1, Y2, Bottom, Top) >= 4.0)
                {
                    bool NearRight = Math.Abs(X1 - Right) <= Math.Abs(X1 - Left);
                    double Side = NearRight ? Right : Left;
                    if (Math.Abs(X1 - Side) > 1.5)
                    {
                        continue;
                    }

                    double ClearX = NearRight ? Right + BORDER_CLEAR_PX : Left - BORDER_CLEAR_PX;
                    ShiftRunX(Guide.Points, X1, ClearX);
                }
                else if (Horizontal && AxisOverlap(X1, X2, Left, Right) >= 4.0)
                {
                    bool NearTop = Math.Abs(Y1 - Top) <= Math.Abs(Y1 - Bottom);
                    double Side = NearTop ? Top : Bottom;
                    if (Math.Abs(Y1 - Side) > 1.5)
                    {
                        continue;
                    }

                    double ClearY = NearTop ? Top + BORDER_CLEAR_PX : Bottom - BORDER_CLEAR_PX;
                    ShiftRunY(Guide.Points, Y1, ClearY);
                }
            }
        }
    }

    private static void ShiftRunX(List<(double X, double Y)> Points, double OldX, double NewX)
    {
        for (int Index = 0; Index < Points.Count; Index++)
        {
            if (Math.Abs(Points[Index].X - OldX) <= 1.5)
            {
                Points[Index] = (NewX, Points[Index].Y);
            }
        }
    }

    private static void ShiftRunY(List<(double X, double Y)> Points, double OldY, double NewY)
    {
        for (int Index = 0; Index < Points.Count; Index++)
        {
            if (Math.Abs(Points[Index].Y - OldY) <= 1.5)
            {
                Points[Index] = (Points[Index].X, NewY);
            }
        }
    }

    /// <summary>
    /// Slides a stadium exit off the flat-to-cap junction so the nearest face is the one the stub leaves.
    /// </summary>
    private static void PullSourceOffCapJunction(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide)
    {
        if (Guide.Points.Count < 2
            || !BoundsById.TryGetValue(Guide.Edge.SourceId, out FlowchartLayoutNodeBounds? Source)
            || Source == null
            || !NodeById.TryGetValue(Guide.Edge.SourceId, out NodeContract? Node)
            || Node == null
            || !TryRoundedRadius(Node.Shape, Source.Height, out double Radius))
        {
            return;
        }

        (double X0, double Y0) = Guide.Points[0];
        (double X1, double Y1) = Guide.Points[1];
        if (Math.Abs(X1 - X0) > 1.5)
        {
            return;
        }

        double Left = Source.CenterX - (Source.Width / 2.0);
        double Right = Source.CenterX + (Source.Width / 2.0);
        double Bottom = Source.CenterY - (Source.Height / 2.0);
        double Top = Source.CenterY + (Source.Height / 2.0);
        if (Math.Abs(Y0 - Bottom) > 3.0 && Math.Abs(Y0 - Top) > 3.0)
        {
            return;
        }

        double FlatLeft = Left + Radius;
        double FlatRight = Right - Radius;
        double Inset = Math.Min(CORNER_INSET_PX, Math.Max(4.0, (FlatRight - FlatLeft) / 4.0));
        if (FlatRight - FlatLeft < Inset * 2.0)
        {
            return;
        }

        double PortX = X0;
        if (Math.Abs(X0 - FlatRight) <= 4.0)
        {
            PortX = FlatRight - Inset;
        }
        else if (Math.Abs(X0 - FlatLeft) <= 4.0)
        {
            PortX = FlatLeft + Inset;
        }
        else
        {
            return;
        }

        double FaceY = Math.Abs(Y0 - Bottom) <= Math.Abs(Y0 - Top) ? Bottom : Top;
        if (PortRunWouldHug(BoundsById, Guide, AtSource: true, HorizontalFace: true, PortX))
        {
            return;
        }

        foreach (FlowchartResolvedGuide Other in Guides)
        {
            if (ReferenceEquals(Other, Guide)
                || !string.Equals(Other.Edge.SourceId, Guide.Edge.SourceId, StringComparison.Ordinal)
                || Other.Points.Count < 2)
            {
                continue;
            }

            if (Math.Abs(Other.Points[0].X - PortX) <= 10.0 && Math.Abs(Other.Points[0].Y - FaceY) <= 3.0)
            {
                return;
            }
        }

        Guide.Points[0] = (PortX, FaceY);
        if (Math.Abs(Guide.Points[1].X - X0) <= 1.5)
        {
            Guide.Points[1] = (PortX, Guide.Points[1].Y);
        }
    }

    /// <summary>
    /// Spreads exits and arrivals that share a face when the flat side has room for a normal gap.
    /// </summary>
    private static void SpreadBunchedFacePorts(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        List<FlowchartResolvedGuide> Guides)
    {
        SpreadPortsOnFace(BoundsById, NodeById, Guides, AtSource: true);
        SpreadPortsOnFace(BoundsById, NodeById, Guides, AtSource: false);
    }

    private static void SpreadPortsOnFace(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        List<FlowchartResolvedGuide> Guides,
        bool AtSource)
    {
        for (int Index = 0; Index < Guides.Count; Index++)
        {
            FlowchartResolvedGuide Guide = Guides[Index];
            if (FlowchartSharedEdgeRouter.IsDesignedRoute(Guide.RouteKind) || Guide.Points.Count < 2)
            {
                continue;
            }

            string NodeId = AtSource ? Guide.Edge.SourceId : Guide.Edge.TargetId;
            if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds) || Bounds == null)
            {
                continue;
            }

            if (!TryFacePort(Guide, Bounds, AtSource, out bool HorizontalFace, out double Axis))
            {
                continue;
            }

            List<FlowchartResolvedGuide> Bunch = [];
            foreach (FlowchartResolvedGuide Other in Guides)
            {
                if (FlowchartSharedEdgeRouter.IsDesignedRoute(Other.RouteKind) || Other.Points.Count < 2)
                {
                    continue;
                }

                string OtherId = AtSource ? Other.Edge.SourceId : Other.Edge.TargetId;
                if (!string.Equals(OtherId, NodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!TryFacePort(Other, Bounds, AtSource, out bool OtherHorizontal, out double OtherAxis))
                {
                    continue;
                }

                if (OtherHorizontal == HorizontalFace && Math.Abs(OtherAxis - Axis) <= 3.0)
                {
                    Bunch.Add(Other);
                }
            }

            if (Bunch.Count < 2)
            {
                continue;
            }

            string? Shape = NodeById.TryGetValue(NodeId, out NodeContract? Node) ? Node.Shape : null;
            double Radius = 0.0;
            bool Rounded = TryRoundedRadius(Shape, Bounds.Height, out Radius);
            double Left = Bounds.CenterX - (Bounds.Width / 2.0);
            double Right = Bounds.CenterX + (Bounds.Width / 2.0);
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            double SpanStart = HorizontalFace
                ? (Rounded ? Left + Radius : Left)
                : (Rounded ? Bottom + Radius : Bottom);
            double SpanEnd = HorizontalFace
                ? (Rounded ? Right - Radius : Right)
                : (Rounded ? Top - Radius : Top);
            double Inset = Math.Min(CORNER_INSET_PX, Math.Max(4.0, (SpanEnd - SpanStart) / 5.0));
            double UsableStart = SpanStart + Inset;
            double UsableEnd = SpanEnd - Inset;
            if (UsableEnd - UsableStart < (Bunch.Count - 1) * 14.0)
            {
                continue;
            }

            Bunch.Sort((LeftGuide, RightGuide) => PortSlot(LeftGuide, AtSource, HorizontalFace).CompareTo(PortSlot(RightGuide, AtSource, HorizontalFace)));
            double Closest = double.PositiveInfinity;
            for (int Slot = 1; Slot < Bunch.Count; Slot++)
            {
                Closest = Math.Min(
                    Closest,
                    Math.Abs(PortSlot(Bunch[Slot], AtSource, HorizontalFace) - PortSlot(Bunch[Slot - 1], AtSource, HorizontalFace)));
            }

            if (Closest >= 16.0)
            {
                continue;
            }

            double Step = (UsableEnd - UsableStart) / (Bunch.Count - 1);
            for (int Slot = 0; Slot < Bunch.Count; Slot++)
            {
                double Position = UsableStart + (Step * Slot);
                if (PortRunWouldHug(BoundsById, Bunch[Slot], AtSource, HorizontalFace, Position))
                {
                    continue;
                }

                List<(double X, double Y)> Before = new List<(double X, double Y)>(Bunch[Slot].Points);
                SetPortSlot(Bunch[Slot], AtSource, HorizontalFace, Position);
                if (CrossesAnotherGuide(Guides, Bunch[Slot], Bunch[Slot].Points))
                {
                    Bunch[Slot].Points.Clear();
                    Bunch[Slot].Points.AddRange(Before);
                }
            }
        }
    }

    private static bool TryFacePort(
        FlowchartResolvedGuide Guide,
        FlowchartLayoutNodeBounds Bounds,
        bool AtSource,
        out bool HorizontalFace,
        out double Axis)
    {
        HorizontalFace = false;
        Axis = 0.0;
        int EndIndex = AtSource ? 0 : Guide.Points.Count - 1;
        int OtherIndex = AtSource ? 1 : Guide.Points.Count - 2;
        (double EndX, double EndY) = Guide.Points[EndIndex];
        (double OtherX, double OtherY) = Guide.Points[OtherIndex];
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        bool VerticalRun = Math.Abs(EndX - OtherX) <= 1.5;
        bool HorizontalRun = Math.Abs(EndY - OtherY) <= 1.5;
        if (VerticalRun && (Math.Abs(EndY - Bottom) <= 3.0 || Math.Abs(EndY - Top) <= 3.0))
        {
            HorizontalFace = true;
            Axis = Math.Abs(EndY - Bottom) <= Math.Abs(EndY - Top) ? Bottom : Top;
            return EndX >= Left - 2.0 && EndX <= Right + 2.0;
        }

        if (HorizontalRun && (Math.Abs(EndX - Left) <= 3.0 || Math.Abs(EndX - Right) <= 3.0))
        {
            HorizontalFace = false;
            Axis = Math.Abs(EndX - Left) <= Math.Abs(EndX - Right) ? Left : Right;
            return EndY >= Bottom - 2.0 && EndY <= Top + 2.0;
        }

        return false;
    }

    private static double PortSlot(FlowchartResolvedGuide Guide, bool AtSource, bool HorizontalFace)
    {
        int EndIndex = AtSource ? 0 : Guide.Points.Count - 1;
        return HorizontalFace ? Guide.Points[EndIndex].X : Guide.Points[EndIndex].Y;
    }

    private static void SetPortSlot(FlowchartResolvedGuide Guide, bool AtSource, bool HorizontalFace, double Position)
    {
        int EndIndex = AtSource ? 0 : Guide.Points.Count - 1;
        int OtherIndex = AtSource ? 1 : Guide.Points.Count - 2;
        (double EndX, double EndY) = Guide.Points[EndIndex];
        (double OtherX, double OtherY) = Guide.Points[OtherIndex];
        if (HorizontalFace)
        {
            Guide.Points[EndIndex] = (Position, EndY);
            if (Math.Abs(OtherX - EndX) <= 1.5)
            {
                Guide.Points[OtherIndex] = (Position, OtherY);
            }
        }
        else
        {
            Guide.Points[EndIndex] = (EndX, Position);
            if (Math.Abs(OtherY - EndY) <= 1.5)
            {
                Guide.Points[OtherIndex] = (OtherX, Position);
            }
        }
    }

    /// <summary>
    /// Replaces a shared or overlaid run with one right-angle crossing when every clear separation failed.
    /// </summary>
    private static void PreferRightAngleToSharedRun(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        List<FlowchartResolvedGuide> Guides)
    {
        for (int LeftIndex = 0; LeftIndex < Guides.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Guides.Count; RightIndex++)
            {
                FlowchartResolvedGuide Left = Guides[LeftIndex];
                FlowchartResolvedGuide Right = Guides[RightIndex];
                if (FlowchartSharedEdgeRouter.IsDesignedRoute(Left.RouteKind)
                    || FlowchartSharedEdgeRouter.IsDesignedRoute(Right.RouteKind))
                {
                    continue;
                }

                double Shared = LongestSharedRun(Left.Points, Right.Points);
                double Gap = ClosestParallelGap(Left.Points, Right.Points);
                bool Overlaid = Gap < 8.0 && SharesEdgeEndpoint(Left.Edge, Right.Edge);
                if (Shared < SHARED_MIN_PX && !Overlaid)
                {
                    continue;
                }

                if (TryShiftOntoOneCrossing(Contract, BoundsById, Guides, Left, Right)
                    || TryShiftOntoOneCrossing(Contract, BoundsById, Guides, Right, Left))
                {
                    continue;
                }
            }
        }
    }

    private static bool TryShiftOntoOneCrossing(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        FlowchartResolvedGuide Other)
    {
        if (Guide.Points.Count < 3)
        {
            return false;
        }

        double[] Offsets = [12.0, -12.0, 16.0, -16.0, 24.0, -24.0, 36.0, -36.0, 48.0, -48.0];
        bool Horizontal = SharedAxisIsHorizontal(Guide.Points, Other.Points);
        if (ClosestParallelGap(Guide.Points, Other.Points) < 8.0)
        {
            Horizontal = ParallelRunIsHorizontal(Guide.Points, Other.Points);
        }

        foreach (double Offset in Offsets)
        {
            List<(double X, double Y)> Candidate = new List<(double X, double Y)>(Guide.Points);
            bool Moved = false;
            for (int Index = 1; Index < Candidate.Count - 1; Index++)
            {
                if (!PointSitsOnOtherRun(Candidate[Index], Other.Points, Horizontal))
                {
                    continue;
                }

                if (Horizontal)
                {
                    Candidate[Index] = (Candidate[Index].X, Candidate[Index].Y + Offset);
                }
                else
                {
                    Candidate[Index] = (Candidate[Index].X + Offset, Candidate[Index].Y);
                }

                Moved = true;
            }

            if (!Moved)
            {
                continue;
            }

            Candidate = FlowchartPaddedVisibilityRouter.EnsureOrthogonal(Candidate);
            FlowchartPaddedVisibilityRouter.CollapseReversalSpurs(Candidate);
            bool Blocked = RouteStillBlocked(Contract, BoundsById, Candidate, Guide.Edge.SourceId, Guide.Edge.TargetId);
            bool StillShared = LongestSharedRun(Candidate, Other.Points) >= SHARED_MIN_PX;
            bool Closes = ClosesOnAnotherGuide(Guides, Guide, Candidate);
            bool SharesAny = SharesALongRunWithAny(Guides, Guide, Candidate);
            bool Terminals = TerminalsLeaveOnTheNormal(BoundsById, Guide, Candidate);
            bool Inside = RouteEntersEndpointInterior(BoundsById, Guide, Candidate);
            bool NearGroup = RunsNearGroupBorder(Contract, BoundsById, Candidate);
            if (Candidate.Count < 2 || Blocked || StillShared || Closes || SharesAny || !Terminals || Inside || NearGroup)
            {
                continue;
            }

            int CrossingCount = CountPerpendicularCrossings(Guides, Guide, Candidate, out FlowchartResolvedGuide? Crossed);
            if (CrossingCount < 0 || CrossingCount > 1)
            {
                continue;
            }

            Guide.Points.Clear();
            Guide.Points.AddRange(Candidate);
            Guide.RouteKind = Candidate.Count <= 2
                ? FlowchartEdgeRouteKind.Straight
                : FlowchartEdgeRouteKind.Corridor;
            if (CrossingCount == 0 || Crossed == null)
            {
                return true;
            }

            string Reason = "right-angle fallback: no clear channel remained for "
                + Guide.Edge.SourceId
                + "->"
                + Guide.Edge.TargetId
                + " beside "
                + Other.Edge.SourceId
                + "->"
                + Other.Edge.TargetId
                + "; other exit ports, channel spacing, and same-rank order were tried and every separated run still shares the corridor or hits a neighbouring box, so the edge crosses "
                + Crossed.Edge.SourceId
                + "->"
                + Crossed.Edge.TargetId
                + " once at a right angle";
            FlowchartSharedEdgeRouter.RecordRightAngleFallback(
                Guide.Edge.SourceId,
                Guide.Edge.TargetId,
                Crossed.Edge.SourceId,
                Crossed.Edge.TargetId,
                Reason);
            return true;
        }

        return false;
    }

    private static bool PointSitsOnOtherRun(
        (double X, double Y) Point,
        IReadOnlyList<(double X, double Y)> Other,
        bool Horizontal)
    {
        for (int Index = 1; Index < Other.Count; Index++)
        {
            if (Horizontal
                && Math.Abs(Other[Index].Y - Other[Index - 1].Y) <= 1.5
                && Math.Abs(Point.Y - Other[Index].Y) <= 8.0
                && Point.X > Math.Min(Other[Index - 1].X, Other[Index].X) - 1.0
                && Point.X < Math.Max(Other[Index - 1].X, Other[Index].X) + 1.0)
            {
                return true;
            }

            if (!Horizontal
                && Math.Abs(Other[Index].X - Other[Index - 1].X) <= 1.5
                && Math.Abs(Point.X - Other[Index].X) <= 8.0
                && Point.Y > Math.Min(Other[Index - 1].Y, Other[Index].Y) - 1.0
                && Point.Y < Math.Max(Other[Index - 1].Y, Other[Index].Y) + 1.0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ParallelRunIsHorizontal(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right)
    {
        double Best = double.PositiveInfinity;
        bool Horizontal = true;
        for (int LeftIndex = 1; LeftIndex < Left.Count; LeftIndex++)
        {
            bool LeftHorizontal = Math.Abs(Left[LeftIndex].Y - Left[LeftIndex - 1].Y) <= 1.5
                && Math.Abs(Left[LeftIndex].X - Left[LeftIndex - 1].X) >= 24.0;
            bool LeftVertical = Math.Abs(Left[LeftIndex].X - Left[LeftIndex - 1].X) <= 1.5
                && Math.Abs(Left[LeftIndex].Y - Left[LeftIndex - 1].Y) >= 24.0;
            for (int RightIndex = 1; RightIndex < Right.Count; RightIndex++)
            {
                if (LeftHorizontal
                    && Math.Abs(Right[RightIndex].Y - Right[RightIndex - 1].Y) <= 1.5
                    && AxisOverlap(Left[LeftIndex - 1].X, Left[LeftIndex].X, Right[RightIndex - 1].X, Right[RightIndex].X) >= 24.0)
                {
                    double Gap = Math.Abs(Left[LeftIndex].Y - Right[RightIndex].Y);
                    if (Gap < Best)
                    {
                        Best = Gap;
                        Horizontal = true;
                    }
                }
                else if (LeftVertical
                    && Math.Abs(Right[RightIndex].X - Right[RightIndex - 1].X) <= 1.5
                    && AxisOverlap(Left[LeftIndex - 1].Y, Left[LeftIndex].Y, Right[RightIndex - 1].Y, Right[RightIndex].Y) >= 24.0)
                {
                    double Gap = Math.Abs(Left[LeftIndex].X - Right[RightIndex].X);
                    if (Gap < Best)
                    {
                        Best = Gap;
                        Horizontal = false;
                    }
                }
            }
        }

        return Horizontal;
    }

    private static double ClosestParallelGap(
        IReadOnlyList<(double X, double Y)> Left,
        IReadOnlyList<(double X, double Y)> Right)
    {
        double Best = double.PositiveInfinity;
        for (int LeftIndex = 1; LeftIndex < Left.Count; LeftIndex++)
        {
            bool LeftHorizontal = Math.Abs(Left[LeftIndex].Y - Left[LeftIndex - 1].Y) <= 1.5
                && Math.Abs(Left[LeftIndex].X - Left[LeftIndex - 1].X) >= 24.0;
            bool LeftVertical = Math.Abs(Left[LeftIndex].X - Left[LeftIndex - 1].X) <= 1.5
                && Math.Abs(Left[LeftIndex].Y - Left[LeftIndex - 1].Y) >= 24.0;
            for (int RightIndex = 1; RightIndex < Right.Count; RightIndex++)
            {
                if (LeftHorizontal
                    && Math.Abs(Right[RightIndex].Y - Right[RightIndex - 1].Y) <= 1.5
                    && AxisOverlap(Left[LeftIndex - 1].X, Left[LeftIndex].X, Right[RightIndex - 1].X, Right[RightIndex].X) >= 24.0)
                {
                    Best = Math.Min(Best, Math.Abs(Left[LeftIndex].Y - Right[RightIndex].Y));
                }
                else if (LeftVertical
                    && Math.Abs(Right[RightIndex].X - Right[RightIndex - 1].X) <= 1.5
                    && AxisOverlap(Left[LeftIndex - 1].Y, Left[LeftIndex].Y, Right[RightIndex - 1].Y, Right[RightIndex].Y) >= 24.0)
                {
                    Best = Math.Min(Best, Math.Abs(Left[LeftIndex].X - Right[RightIndex].X));
                }
            }
        }

        return Best;
    }

    private static int CountPerpendicularCrossings(
        IReadOnlyList<FlowchartResolvedGuide> Guides,
        FlowchartResolvedGuide Guide,
        IReadOnlyList<(double X, double Y)> Candidate,
        out FlowchartResolvedGuide? Crossed)
    {
        Crossed = null;
        int Count = 0;
        foreach (FlowchartResolvedGuide Other in Guides)
        {
            if (ReferenceEquals(Other, Guide) || Other.Points.Count < 2)
            {
                continue;
            }

            for (int Left = 1; Left < Candidate.Count; Left++)
            {
                (double AX, double AY) = Candidate[Left - 1];
                (double BX, double BY) = Candidate[Left];
                bool LeftHorizontal = Math.Abs(BY - AY) <= 1.5 && Math.Abs(BX - AX) >= 16.0;
                bool LeftVertical = Math.Abs(BX - AX) <= 1.5 && Math.Abs(BY - AY) >= 16.0;
                if (!LeftHorizontal && !LeftVertical)
                {
                    continue;
                }

                for (int Right = 1; Right < Other.Points.Count; Right++)
                {
                    (double CX, double CY) = Other.Points[Right - 1];
                    (double DX, double DY) = Other.Points[Right];
                    bool RightHorizontal = Math.Abs(DY - CY) <= 1.5 && Math.Abs(DX - CX) >= 16.0;
                    bool RightVertical = Math.Abs(DX - CX) <= 1.5 && Math.Abs(DY - CY) >= 16.0;
                    if (LeftHorizontal == RightHorizontal || LeftVertical == RightVertical)
                    {
                        continue;
                    }

                    if (!SegmentsCross((AX, AY), (BX, BY), (CX, CY), (DX, DY)))
                    {
                        continue;
                    }

                    double Denominator = ((BX - AX) * (DY - CY)) - ((BY - AY) * (DX - CX));
                    double Ua = (((CX - AX) * (DY - CY)) - ((CY - AY) * (DX - CX))) / Denominator;
                    double Ub = (((CX - AX) * (BY - AY)) - ((CY - AY) * (BX - AX))) / Denominator;
                    double CrossX = AX + (Ua * (BX - AX));
                    double CrossY = AY + (Ua * (BY - AY));
                    if (DistanceToPoint(CrossX, CrossY, AX, AY) < 8.0
                        || DistanceToPoint(CrossX, CrossY, BX, BY) < 8.0
                        || DistanceToPoint(CrossX, CrossY, CX, CY) < 8.0
                        || DistanceToPoint(CrossX, CrossY, DX, DY) < 8.0
                        || Ua <= 0.08
                        || Ua >= 0.92
                        || Ub <= 0.08
                        || Ub >= 0.92)
                    {
                        return -1;
                    }

                    Count++;
                    Crossed = Other;
                    if (Count > 1)
                    {
                        Crossed = null;
                        return Count;
                    }
                }
            }
        }

        return Count;
    }

    private static double DistanceToPoint(double X1, double Y1, double X2, double Y2)
    {
        return Math.Sqrt(Math.Pow(X1 - X2, 2.0) + Math.Pow(Y1 - Y2, 2.0));
    }

    private static bool RouteEntersEndpointInterior(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide,
        IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count < 3)
        {
            return false;
        }

        if (PointInside(BoundsById, Guide.Edge.SourceId, Points[1].X, Points[1].Y))
        {
            return true;
        }

        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            if (PointInside(BoundsById, Guide.Edge.TargetId, Points[Index].X, Points[Index].Y))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PortRunWouldHug(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartResolvedGuide Guide,
        bool AtSource,
        bool HorizontalFace,
        double Position)
    {
        int EndIndex = AtSource ? 0 : Guide.Points.Count - 1;
        int OtherIndex = AtSource ? 1 : Guide.Points.Count - 2;
        (double EndX, double EndY) = Guide.Points[EndIndex];
        (double OtherX, double OtherY) = Guide.Points[OtherIndex];
        double RunEndX = HorizontalFace ? Position : OtherX;
        double RunEndY = HorizontalFace ? OtherY : Position;
        double RunStartX = HorizontalFace ? Position : EndX;
        double RunStartY = HorizontalFace ? EndY : Position;
        if (!HorizontalFace && Math.Abs(OtherX - EndX) <= 1.5)
        {
            RunEndX = EndX;
        }

        if (HorizontalFace && Math.Abs(OtherY - EndY) <= 1.5)
        {
            RunEndY = EndY;
        }

        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            if (string.Equals(Bounds.NodeId, AtSource ? Guide.Edge.SourceId : Guide.Edge.TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            double Left = Bounds.CenterX - (Bounds.Width / 2.0);
            double Right = Bounds.CenterX + (Bounds.Width / 2.0);
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);
            if (HorizontalFace
                && Math.Abs(RunStartX - RunEndX) <= 1.5
                && AxisOverlap(RunStartY, RunEndY, Bottom, Top) > 20.0
                && (Math.Abs(Position - Left) <= HUG_PX || Math.Abs(Position - Right) <= HUG_PX))
            {
                return true;
            }

            if (!HorizontalFace
                && Math.Abs(RunStartY - RunEndY) <= 1.5
                && AxisOverlap(RunStartX, RunEndX, Left, Right) > 20.0
                && (Math.Abs(Position - Bottom) <= HUG_PX || Math.Abs(Position - Top) <= HUG_PX))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RunsNearGroupBorder(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyList<(double X, double Y)> Points)
    {
        for (int Index = 1; Index < Points.Count; Index++)
        {
            (double X1, double Y1) = Points[Index - 1];
            (double X2, double Y2) = Points[Index];
            double Length = Math.Sqrt(Math.Pow(X2 - X1, 2.0) + Math.Pow(Y2 - Y1, 2.0));
            bool Vertical = Math.Abs(X2 - X1) <= 1.5 && Length >= HUG_MIN_PX;
            bool Horizontal = Math.Abs(Y2 - Y1) <= 1.5 && Length >= HUG_MIN_PX;
            if (!Vertical && !Horizontal)
            {
                continue;
            }

            foreach (GroupContract Group in Contract.Groups)
            {
                if (!BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds? Bounds) || Bounds == null)
                {
                    continue;
                }

                double Left = Bounds.CenterX - (Bounds.Width / 2.0);
                double Right = Bounds.CenterX + (Bounds.Width / 2.0);
                double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
                double Top = Bounds.CenterY + (Bounds.Height / 2.0);
                double Overlap = Vertical
                    ? AxisOverlap(Y1, Y2, Bottom, Top)
                    : AxisOverlap(X1, X2, Left, Right);
                double Separation = Vertical
                    ? Math.Min(Math.Abs(X1 - Left), Math.Abs(X1 - Right))
                    : Math.Min(Math.Abs(Y1 - Bottom), Math.Abs(Y1 - Top));
                if (Overlap >= HUG_MIN_PX && Separation < 16.0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool PointInside(
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string NodeId,
        double X,
        double Y)
    {
        if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Bounds) || Bounds == null)
        {
            return false;
        }

        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        return X > Left + 2.0 && X < Right - 2.0 && Y > Bottom + 2.0 && Y < Top - 2.0;
    }
}

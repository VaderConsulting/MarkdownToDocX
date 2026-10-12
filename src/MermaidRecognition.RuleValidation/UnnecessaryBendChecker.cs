using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Layout;

namespace MermaidRecognition.RuleValidation;

public readonly record struct BendPoint(double X, double Y);

public readonly record struct BendObstacle(string Id, double Left, double Bottom, double Right, double Top);

public sealed record BendRoute(
    string LineId,
    string SourceId,
    string TargetId,
    string ExitSide,
    string EntrySide,
    IReadOnlyList<BendPoint> Points,
    bool HintNeedsExtraCorners = false);

public sealed record UnnecessaryBendViolation(
    string DiagramId,
    string LineId,
    char Check,
    int RoutedCorners,
    double RoutedLength,
    IReadOnlyList<BendPoint> BetterRoute);

/// <summary>Compares routed orthogonal lines with legal routes using the same ports and endpoint sides.</summary>
public static class UnnecessaryBendChecker
{
    private const double Epsilon = 0.001;

    public static IReadOnlyList<UnnecessaryBendViolation> Check(
        string DiagramId,
        DiagramContract Contract,
        FlowchartLayoutResult Layout)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds
            .Concat(Layout.ClusterBounds).GroupBy(Box => Box.NodeId, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
        Dictionary<string, EdgeContract> Edges = Contract.Edges
            .Where(Edge => !string.IsNullOrEmpty(Edge.Id))
            .GroupBy(Edge => Edge.Id!, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
        List<UnnecessaryBendViolation> Result = [];
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2) continue;
            List<BendObstacle> Obstacles = Bounds.Values
                .Where(Box => Box.NodeId != Route.SourceId && Box.NodeId != Route.TargetId)
                .Select(Box => new BendObstacle(Box.NodeId, Box.CenterX - Box.Width / 2.0,
                    Box.CenterY - Box.Height / 2.0, Box.CenterX + Box.Width / 2.0, Box.CenterY + Box.Height / 2.0))
                .ToList();
            bool HintNeeded = Edges.TryGetValue(Route.EdgeId, out EdgeContract? Edge)
                && Edge.RoutingHint?.IsActive == true;
            BendRoute Input = new(Route.EdgeId, Route.SourceId, Route.TargetId,
                Route.ExitSide, Route.EntrySide,
                Route.Points.Select(Point => new BendPoint(Point.X, Point.Y)).ToList(), HintNeeded);
            UnnecessaryBendViolation? Finding = CheckRoute(DiagramId, Input, Obstacles);
            if (Finding != null) Result.Add(Finding);
        }
        return Result;
    }

    public static UnnecessaryBendViolation? CheckRoute(
        string DiagramId,
        BendRoute Route,
        IReadOnlyList<BendObstacle> Obstacles)
    {
        List<BendPoint> Routed = Normalize(Route.Points);
        if (Routed.Count < 2 || !IsOrthogonal(Routed)) return null;
        int RoutedCorners = Routed.Count - 2;
        double RoutedLength = Length(Routed);
        List<BendPoint>? Best = FindBest(Route with { Points = Routed }, Obstacles);
        if (Best == null) return null;
        int BetterCorners = Best.Count - 2;
        double BetterLength = Length(Best);
        bool FewerCorners = BetterCorners < RoutedCorners;
        bool MeaningfullyShorter = BetterCorners == RoutedCorners
            && RoutedLength - BetterLength > DiagramGridMetrics.GRID_UNIT_PX + Epsilon;
        if (!FewerCorners && !MeaningfullyShorter) return null;
        if (Route.HintNeedsExtraCorners && HintDirectionsSatisfied(Route, Routed)) return null;
        char Check = FewerCorners
            ? HasShortSideStep(Routed) ? 'c' : HasCancellingPair(Routed) ? 'b' : 'a'
            : 'd';
        return new UnnecessaryBendViolation(DiagramId, Route.LineId, Check,
            RoutedCorners, RoutedLength, Best);
    }

    private static List<BendPoint>? FindBest(BendRoute Route, IReadOnlyList<BendObstacle> Obstacles)
    {
        BendPoint Start = Route.Points[0];
        BendPoint End = Route.Points[^1];
        List<List<BendPoint>> Candidates = [];
        AddCandidate(Candidates, [Start, End]);
        AddCandidate(Candidates, [Start, new BendPoint(End.X, Start.Y), End]);
        AddCandidate(Candidates, [Start, new BendPoint(Start.X, End.Y), End]);

        HashSet<double> XTracks = [Start.X, End.X];
        HashSet<double> YTracks = [Start.Y, End.Y];
        foreach (BendPoint Point in Route.Points) { XTracks.Add(Point.X); YTracks.Add(Point.Y); }
        foreach (BendObstacle Box in Obstacles)
        {
            XTracks.Add(Box.Left - DiagramGridMetrics.GRID_UNIT_PX);
            XTracks.Add(Box.Right + DiagramGridMetrics.GRID_UNIT_PX);
            YTracks.Add(Box.Bottom - DiagramGridMetrics.GRID_UNIT_PX);
            YTracks.Add(Box.Top + DiagramGridMetrics.GRID_UNIT_PX);
        }
        foreach (double X in XTracks)
            AddCandidate(Candidates, [Start, new BendPoint(X, Start.Y), new BendPoint(X, End.Y), End]);
        foreach (double Y in YTracks)
            AddCandidate(Candidates, [Start, new BendPoint(Start.X, Y), new BendPoint(End.X, Y), End]);

        return Candidates.Select(Normalize)
            .Where(Candidate => Candidate.Count >= 2 && IsOrthogonal(Candidate)
                && MatchesEndpointSides(Candidate, Route.ExitSide, Route.EntrySide)
                && HasMinimumRuns(Candidate) && IsClear(Candidate, Obstacles))
            .OrderBy(Candidate => Candidate.Count - 2)
            .ThenBy(Length)
            .FirstOrDefault();
    }

    private static void AddCandidate(List<List<BendPoint>> Candidates, List<BendPoint> Candidate)
    {
        if (Candidate.Count >= 2) Candidates.Add(Candidate);
    }

    private static List<BendPoint> Normalize(IReadOnlyList<BendPoint> Points)
    {
        List<BendPoint> Result = [];
        foreach (BendPoint Point in Points)
        {
            if (Result.Count == 0 || !Same(Result[^1], Point)) Result.Add(Point);
            while (Result.Count >= 3 && Collinear(Result[^3], Result[^2], Result[^1])) Result.RemoveAt(Result.Count - 2);
        }
        return Result;
    }

    private static bool HasMinimumRuns(IReadOnlyList<BendPoint> Points)
    {
        if (Points.Count == 2) return true;
        double Minimum = DiagramGridRouteBuilder.CORNER_RADIUS_PX;
        for (int Index = 1; Index < Points.Count; Index++)
            if (Distance(Points[Index - 1], Points[Index]) + Epsilon < Minimum) return false;
        return true;
    }

    private static bool MatchesEndpointSides(IReadOnlyList<BendPoint> Points, string Exit, string Entry) =>
        Points.Count >= 2 && Leaves(Points[0], Points[1], Exit) && Enters(Points[^2], Points[^1], Entry);

    private static bool Leaves(BendPoint A, BendPoint B, string Side) => Side switch
    {
        "left" => B.X < A.X - Epsilon && Math.Abs(B.Y - A.Y) <= Epsilon,
        "right" => B.X > A.X + Epsilon && Math.Abs(B.Y - A.Y) <= Epsilon,
        "bottom" => B.Y < A.Y - Epsilon && Math.Abs(B.X - A.X) <= Epsilon,
        "top" => B.Y > A.Y + Epsilon && Math.Abs(B.X - A.X) <= Epsilon,
        _ => true
    };

    private static bool Enters(BendPoint A, BendPoint B, string Side) => Side switch
    {
        "left" => B.X > A.X + Epsilon && Math.Abs(B.Y - A.Y) <= Epsilon,
        "right" => B.X < A.X - Epsilon && Math.Abs(B.Y - A.Y) <= Epsilon,
        "bottom" => B.Y > A.Y + Epsilon && Math.Abs(B.X - A.X) <= Epsilon,
        "top" => B.Y < A.Y - Epsilon && Math.Abs(B.X - A.X) <= Epsilon,
        _ => true
    };

    private static bool IsClear(IReadOnlyList<BendPoint> Points, IReadOnlyList<BendObstacle> Obstacles)
    {
        for (int Index = 1; Index < Points.Count; Index++)
            foreach (BendObstacle Box in Obstacles)
                if (SegmentCrossesInterior(Points[Index - 1], Points[Index], Box)) return false;
        return true;
    }

    private static bool SegmentCrossesInterior(BendPoint A, BendPoint B, BendObstacle Box)
    {
        if (Math.Abs(A.X - B.X) <= Epsilon)
            return A.X > Box.Left + Epsilon && A.X < Box.Right - Epsilon
                && Math.Max(Math.Min(A.Y, B.Y), Box.Bottom) < Math.Min(Math.Max(A.Y, B.Y), Box.Top) - Epsilon;
        return A.Y > Box.Bottom + Epsilon && A.Y < Box.Top - Epsilon
            && Math.Max(Math.Min(A.X, B.X), Box.Left) < Math.Min(Math.Max(A.X, B.X), Box.Right) - Epsilon;
    }

    private static bool HintDirectionsSatisfied(BendRoute Route, IReadOnlyList<BendPoint> Points) =>
        MatchesEndpointSides(Points, Route.ExitSide, Route.EntrySide);

    private static bool HasCancellingPair(IReadOnlyList<BendPoint> Points)
    {
        for (int Index = 0; Index + 3 < Points.Count; Index++)
        {
            bool FirstHorizontal = Math.Abs(Points[Index].Y - Points[Index + 1].Y) <= Epsilon;
            bool ThirdHorizontal = Math.Abs(Points[Index + 2].Y - Points[Index + 3].Y) <= Epsilon;
            if (FirstHorizontal == ThirdHorizontal) return true;
        }
        return false;
    }

    private static bool HasShortSideStep(IReadOnlyList<BendPoint> Points)
    {
        for (int Index = 1; Index + 1 < Points.Count; Index++)
            if (Distance(Points[Index - 1], Points[Index]) < DiagramGridMetrics.GRID_UNIT_PX - Epsilon)
                return true;
        return false;
    }

    private static bool IsOrthogonal(IReadOnlyList<BendPoint> Points)
    {
        for (int Index = 1; Index < Points.Count; Index++)
            if (Math.Abs(Points[Index - 1].X - Points[Index].X) > Epsilon
                && Math.Abs(Points[Index - 1].Y - Points[Index].Y) > Epsilon) return false;
        return true;
    }

    private static bool Same(BendPoint A, BendPoint B) =>
        Math.Abs(A.X - B.X) <= Epsilon && Math.Abs(A.Y - B.Y) <= Epsilon;
    private static bool Collinear(BendPoint A, BendPoint B, BendPoint C) =>
        Math.Abs(A.X - B.X) <= Epsilon && Math.Abs(B.X - C.X) <= Epsilon
        || Math.Abs(A.Y - B.Y) <= Epsilon && Math.Abs(B.Y - C.Y) <= Epsilon;
    private static double Distance(BendPoint A, BendPoint B) => Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);
    private static double Length(IReadOnlyList<BendPoint> Points) =>
        Enumerable.Range(1, Points.Count - 1).Sum(Index => Distance(Points[Index - 1], Points[Index]));
}

using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.RuleValidation;

public readonly record struct BendPoint(double X, double Y);

public readonly record struct BendObstacle(string Id, double Left, double Bottom, double Right, double Top);

public readonly record struct BendBox(double CenterX, double CenterY, double Width, double Height);

public readonly record struct BendSegment(
    string LineId,
    BendPoint Start,
    BendPoint End,
    string SourceId = "",
    string TargetId = "");

public sealed record BendRoute(
    string LineId,
    string SourceId,
    string TargetId,
    string ExitSide,
    string EntrySide,
    IReadOnlyList<BendPoint> Points,
    bool HintNeedsExtraCorners = false,
    int RoutedCrossings = 0,
    int AllowedCrossings = 0,
    BendBox? SourceBox = null,
    BendBox? TargetBox = null,
    FlowchartDirection Direction = FlowchartDirection.TopToBottom,
    bool SourceIsDiamond = false,
    int OutgoingCount = 0,
    int IncomingCount = 0,
    bool FanBetweenColumns = false,
    string? Label = null,
    double LabelFontSizePx = 16.0,
    double LabelRequiredHorizontalPx = 0.0,
    double LabelRequiredVerticalPx = 0.0);

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
        Dictionary<string, NodeContract> Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        (int TotalCrossings, int ForcedMinimum) = FlowchartCrossingBudget.Measure(Contract, Layout);
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
            List<BendSegment> OtherLines = Layout.EdgeRoutes.Where(Other => Other.EdgeId != Route.EdgeId)
                .SelectMany(Other => Enumerable.Range(1, Other.Points.Count - 1).Select(Index =>
                    new BendSegment(Other.EdgeId,
                        new BendPoint(Other.Points[Index - 1].X, Other.Points[Index - 1].Y),
                        new BendPoint(Other.Points[Index].X, Other.Points[Index].Y),
                        Other.SourceId, Other.TargetId)))
                .ToList();
            List<BendPoint> RoutedPoints = Route.Points.Select(Point => new BendPoint(Point.X, Point.Y)).ToList();
            int RoutedCrossings = LineInteraction(RoutedPoints, OtherLines).Crossings;
            int AllowedCrossings = RoutedCrossings + Math.Max(0, ForcedMinimum - TotalCrossings);
            Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? SourceBounds);
            Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? TargetBounds);
            int OutgoingCount = Contract.Edges.Count(Candidate => IsVisible(Candidate)
                && Candidate.SourceId == Route.SourceId);
            int IncomingCount = Contract.Edges.Count(Candidate => IsVisible(Candidate)
                && Candidate.TargetId == Route.TargetId);
            bool SourceIsDiamond = Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode)
                && FlowchartDagrePresentation.IsDiamondShape(SourceNode.Shape);
            bool FanBetweenColumns = IsFanBetweenColumns(Contract, Bounds, Route, Direction);
            double LabelRequiredHorizontal = 0.0;
            double LabelRequiredVertical = 0.0;
            if (!string.IsNullOrWhiteSpace(Edge?.Label))
            {
                FlowchartLabelDimensions Dimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
                    Edge.Label, Options, Options.EdgeLabelFontSizePx);
                LabelRequiredHorizontal = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                    Dimensions, Options.EdgeLabelFontSizePx, true);
                LabelRequiredVertical = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                    Dimensions, Options.EdgeLabelFontSizePx, false);
            }
            BendRoute Input = new(Route.EdgeId, Route.SourceId, Route.TargetId,
                Route.ExitSide, Route.EntrySide, RoutedPoints, HintNeeded, RoutedCrossings, AllowedCrossings,
                ToBox(SourceBounds), ToBox(TargetBounds), Direction, SourceIsDiamond,
                OutgoingCount, IncomingCount, FanBetweenColumns, Edge?.Label, Options.EdgeLabelFontSizePx,
                LabelRequiredHorizontal, LabelRequiredVertical);
            UnnecessaryBendViolation? Finding = CheckRoute(DiagramId, Input, Obstacles, OtherLines);
            if (Finding != null) Result.Add(Finding);
        }
        return Result;
    }

    public static UnnecessaryBendViolation? CheckRoute(
        string DiagramId,
        BendRoute Route,
        IReadOnlyList<BendObstacle> Obstacles,
        IReadOnlyList<BendSegment>? OtherLines = null)
    {
        List<BendPoint> Routed = Normalize(Route.Points);
        if (Routed.Count < 2 || !IsOrthogonal(Routed)) return null;
        int RoutedCorners = Routed.Count - 2;
        double RoutedLength = Length(Routed);
        (char Check, List<BendPoint>? Better)? Structural = FindStructuralViolation(Route with { Points = Routed }, Obstacles, OtherLines ?? []);
        if (Structural.HasValue)
            return new UnnecessaryBendViolation(DiagramId, Route.LineId, Structural.Value.Check,
                RoutedCorners, RoutedLength, Structural.Value.Better ?? Routed);
        List<BendPoint>? Best = FindBest(Route with { Points = Routed }, Obstacles, OtherLines ?? []);
        if (Best == null) return null;
        int BetterCorners = Best.Count - 2;
        double BetterLength = Length(Best);
        bool FewerCorners = BetterCorners < RoutedCorners;
        bool MeaningfullyShorter = BetterCorners == RoutedCorners
            && RoutedLength - BetterLength > DiagramGridMetrics.GRID_UNIT_PX + Epsilon;
        if (!FewerCorners && !MeaningfullyShorter) return null;
        if (Route.HintNeedsExtraCorners && HintDirectionsSatisfied(Route, Routed)) return null;
        char Check = LineInteraction(Best, OtherLines ?? []).Crossings > Route.RoutedCrossings ? 'e' : FewerCorners
            ? HasShortSideStep(Routed) ? 'c' : HasCancellingPair(Routed) ? 'b' : 'a'
            : 'd';
        return new UnnecessaryBendViolation(DiagramId, Route.LineId, Check,
            RoutedCorners, RoutedLength, Best);
    }

    private static (char Check, List<BendPoint>? Better)? FindStructuralViolation(
        BendRoute Route, IReadOnlyList<BendObstacle> Obstacles, IReadOnlyList<BendSegment> OtherLines)
    {
        if (Route.SourceIsDiamond && Route.SourceBox.HasValue && Route.TargetBox.HasValue
            && DiamondBranchViolation(Route, out List<BendPoint>? DiamondRoute))
            return ('f', DiamondRoute);

        if (!string.IsNullOrWhiteSpace(Route.Label) && LabelHasNoLegalRun(Route))
        {
            List<BendPoint>? Better = FindBest(Route, Obstacles, OtherLines);
            return ('g', Better);
        }

        if (Route.IncomingCount > 1 && Route.SourceBox.HasValue && Route.TargetBox.HasValue
            && ConvergenceViolation(Route, Obstacles, OtherLines, out List<BendPoint>? ConvergingRoute))
            return ('h', ConvergingRoute);

        if (Route.FanBetweenColumns && Route.SourceBox.HasValue && Route.TargetBox.HasValue
            && FanViolation(Route, OtherLines, out List<BendPoint>? FanRoute))
            return ('i', FanRoute);

        if (Route.RoutedCrossings > 0)
        {
            List<BendPoint>? Better = FindCrossingReduction(Route, Obstacles, OtherLines);
            if (Better != null) return ('j', Better);
        }
        return null;
    }

    private static bool DiamondBranchViolation(BendRoute Route, out List<BendPoint>? Better)
    {
        Better = null;
        BendBox Source = Route.SourceBox!.Value;
        BendBox Target = Route.TargetBox!.Value;
        BendPoint Start = Route.Points[0];
        (double StartCross, double StartFlow) = Axes(Start, Route.Direction);
        (double SourceCross, double SourceFlow, double SourceHalfCross, double SourceHalfFlow) = Axes(Source, Route.Direction);
        (double TargetCross, _, _, double TargetHalfFlow) = Axes(Target, Route.Direction);
        bool AtLeftVertex = Near(StartCross, SourceCross - SourceHalfCross) && Near(StartFlow, SourceFlow);
        bool AtRightVertex = Near(StartCross, SourceCross + SourceHalfCross) && Near(StartFlow, SourceFlow);
        bool AtDownstreamVertex = Near(StartCross, SourceCross) && Near(StartFlow, SourceFlow + SourceHalfFlow);
        bool AtUpstreamVertex = Near(StartCross, SourceCross) && Near(StartFlow, SourceFlow - SourceHalfFlow);
        bool AtVertex = AtLeftVertex || AtRightVertex || AtDownstreamVertex || AtUpstreamVertex;
        bool TurnsSidewaysFromBottom = AtDownstreamVertex && Route.Points.Count > 2
            && Math.Abs(Axes(Route.Points[1], Route.Direction).Cross - StartCross) > Epsilon;
        bool OffsetDownstream = TargetFlow(Target, Route.Direction) > SourceFlow + Epsilon
            && Math.Abs(TargetCross - SourceCross) > Epsilon;
        if (!AtVertex || TurnsSidewaysFromBottom)
        {
            Better = OffsetDownstream ? BuildDiamondElbow(Route, Source, Target) : null;
            return true;
        }
        if (Route.OutgoingCount is 2 or 3 && OffsetDownstream)
        {
            bool FacingVertex = TargetCross < SourceCross ? AtLeftVertex : AtRightVertex;
            BendPoint End = Route.Points[^1];
            (double EndCross, double EndFlow) = Axes(End, Route.Direction);
            double TargetFlow = Axes(Target, Route.Direction).Flow;
            bool TopCentre = Near(EndCross, TargetCross) && Near(EndFlow, TargetFlow - TargetHalfFlow);
            if (!FacingVertex || Route.Points.Count - 2 != 1 || !TopCentre)
            {
                Better = BuildDiamondElbow(Route, Source, Target);
                return true;
            }
        }
        return false;
    }

    private static List<BendPoint> BuildDiamondElbow(BendRoute Route, BendBox Source, BendBox Target)
    {
        (double SourceCross, double SourceFlow, double SourceHalfCross, _) = Axes(Source, Route.Direction);
        (double TargetCross, double TargetFlow, _, double TargetHalfFlow) = Axes(Target, Route.Direction);
        double VertexCross = TargetCross < SourceCross ? SourceCross - SourceHalfCross : SourceCross + SourceHalfCross;
        BendPoint Start = PointFromAxes(VertexCross, SourceFlow, Route.Direction);
        BendPoint Corner = PointFromAxes(TargetCross, SourceFlow, Route.Direction);
        BendPoint End = PointFromAxes(TargetCross, TargetFlow - TargetHalfFlow, Route.Direction);
        return Normalize([Start, Corner, End]);
    }

    private static bool LabelHasNoLegalRun(BendRoute Route)
    {
        for (int Index = 1; Index < Route.Points.Count; Index++)
        {
            bool Horizontal = Near(Route.Points[Index - 1].Y, Route.Points[Index].Y);
            double Required = Horizontal ? Route.LabelRequiredHorizontalPx : Route.LabelRequiredVerticalPx;
            if (Required <= 0)
                Required = Math.Max(Route.Label!.Length * Route.LabelFontSizePx * FlowchartEdgeLabelGapHelper.LABEL_CHAR_WIDTH_EM,
                    DiagramGridMetrics.GRID_UNIT_PX);
            double Length = Distance(Route.Points[Index - 1], Route.Points[Index]);
            double CornerRoom = Index > 1 || Index < Route.Points.Count - 1
                ? 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX : DiagramGridRouteBuilder.CORNER_RADIUS_PX;
            if (Length + Epsilon >= Required + CornerRoom) return false;
        }
        return true;
    }

    private static bool ConvergenceViolation(BendRoute Route,
        IReadOnlyList<BendObstacle> Obstacles,
        IReadOnlyList<BendSegment> OtherLines,
        out List<BendPoint>? Better)
    {
        Better = null;
        BendBox Source = Route.SourceBox!.Value;
        BendBox Target = Route.TargetBox!.Value;
        (double SourceCross, double SourceFlow, _, double SourceHalfFlow) = Axes(Source, Route.Direction);
        (double TargetCross, double TargetFlow, double TargetHalfCross, double TargetHalfFlow) = Axes(Target, Route.Direction);
        if (Route.Points.Count == 2) return false;
        bool Aligned = Near(SourceCross, TargetCross);
        if (Aligned) return false;
        if (Route.EntrySide == UpstreamSide(Route.Direction) && Route.Points.Count - 2 >= 2)
        {
            double SourceEndFlow = SourceFlow + SourceHalfFlow;
            double TargetSideCross = TargetCross + (SourceCross < TargetCross ? -TargetHalfCross : TargetHalfCross);
            BendPoint Start = PointFromAxes(SourceCross, SourceEndFlow, Route.Direction);
            BendPoint Corner = PointFromAxes(SourceCross, TargetFlow, Route.Direction);
            BendPoint End = PointFromAxes(TargetSideCross, TargetFlow, Route.Direction);
            List<BendPoint> Candidate = Normalize([Start, Corner, End]);
            if (IsLegalStructuralCandidate(Candidate, Obstacles, OtherLines, Route.AllowedCrossings))
            {
                Better = Candidate;
                return true;
            }
        }
        return false;
    }

    private static bool IsLegalStructuralCandidate(
        IReadOnlyList<BendPoint> Candidate,
        IReadOnlyList<BendObstacle> Obstacles,
        IReadOnlyList<BendSegment> OtherLines,
        int AllowedCrossings)
    {
        if (Candidate.Count < 2 || !IsOrthogonal(Candidate) || !HasMinimumRuns(Candidate)
            || !IsClear(Candidate, Obstacles)) return false;
        (int Crossings, bool Overlap) = LineInteraction(Candidate, OtherLines);
        return !Overlap && Crossings <= AllowedCrossings;
    }

    private static bool FanViolation(BendRoute Route, IReadOnlyList<BendSegment> OtherLines,
        out List<BendPoint>? Better)
    {
        Better = null;
        BendBox Source = Route.SourceBox!.Value;
        BendBox Target = Route.TargetBox!.Value;
        string ExpectedExit = FacingSide(Source, Target);
        string ExpectedEntry = FacingSide(Target, Source);
        List<BendSegment> FanLines = OtherLines.Where(Line =>
            Line.SourceId == Route.SourceId || Line.TargetId == Route.TargetId).ToList();
        bool SharedRail = LineInteraction(Route.Points, FanLines).Overlap;
        if (Route.ExitSide == ExpectedExit && Route.EntrySide == ExpectedEntry && !SharedRail) return false;
        Better = BuildFacingElbow(Route.Points[0], Route.Points[^1], ExpectedExit, ExpectedEntry);
        return true;
    }

    private static List<BendPoint>? FindCrossingReduction(BendRoute Route,
        IReadOnlyList<BendObstacle> Obstacles, IReadOnlyList<BendSegment> OtherLines)
    {
        BendPoint Start = Route.Points[0];
        BendPoint End = Route.Points[^1];
        List<List<BendPoint>> Candidates = [];
        AddCandidate(Candidates, [Start, End]);
        AddCandidate(Candidates, [Start, new BendPoint(End.X, Start.Y), End]);
        AddCandidate(Candidates, [Start, new BendPoint(Start.X, End.Y), End]);
        foreach (BendSegment Segment in OtherLines)
        {
            if (Near(Segment.Start.X, Segment.End.X))
                AddCandidate(Candidates, [Start, new BendPoint(Segment.Start.X - DiagramGridMetrics.GRID_UNIT_PX, Start.Y),
                    new BendPoint(Segment.Start.X - DiagramGridMetrics.GRID_UNIT_PX, End.Y), End]);
            if (Near(Segment.Start.Y, Segment.End.Y))
            {
                AddCandidate(Candidates, [Start, new BendPoint(Start.X, Segment.Start.Y - DiagramGridMetrics.GRID_UNIT_PX),
                    new BendPoint(End.X, Segment.Start.Y - DiagramGridMetrics.GRID_UNIT_PX), End]);
                foreach (double X in new[] { Segment.Start.X, Segment.End.X })
                {
                    AddCandidate(Candidates, [Start, new BendPoint(X - DiagramGridMetrics.GRID_UNIT_PX, Start.Y),
                        new BendPoint(X - DiagramGridMetrics.GRID_UNIT_PX, End.Y), End]);
                    AddCandidate(Candidates, [Start, new BendPoint(X + DiagramGridMetrics.GRID_UNIT_PX, Start.Y),
                        new BendPoint(X + DiagramGridMetrics.GRID_UNIT_PX, End.Y), End]);
                }
            }
            if (Near(Segment.Start.X, Segment.End.X))
            foreach (double Y in new[] { Segment.Start.Y, Segment.End.Y })
            {
                AddCandidate(Candidates, [Start, new BendPoint(Start.X, Y - DiagramGridMetrics.GRID_UNIT_PX),
                    new BendPoint(End.X, Y - DiagramGridMetrics.GRID_UNIT_PX), End]);
                AddCandidate(Candidates, [Start, new BendPoint(Start.X, Y + DiagramGridMetrics.GRID_UNIT_PX),
                    new BendPoint(End.X, Y + DiagramGridMetrics.GRID_UNIT_PX), End]);
            }
        }
        int RoutedCorners = Route.Points.Count - 2;
        return Candidates.Select(Normalize).Where(Candidate => Candidate.Count >= 2
                && Candidate.Count - 2 <= RoutedCorners && IsOrthogonal(Candidate)
                && MatchesEndpointSides(Candidate, Route.ExitSide, Route.EntrySide)
                && HasMinimumRuns(Candidate) && IsClear(Candidate, Obstacles)
                && !LineInteraction(Candidate, OtherLines).Overlap
                && LineInteraction(Candidate, OtherLines).Crossings < Route.RoutedCrossings)
            .OrderBy(Candidate => LineInteraction(Candidate, OtherLines).Crossings)
            .ThenBy(Candidate => Candidate.Count).ThenBy(Length).FirstOrDefault();
    }

    private static List<BendPoint>? BuildFacingElbow(BendPoint Start, BendPoint End, string Exit, string Entry)
    {
        List<BendPoint> A = Normalize([Start, new BendPoint(End.X, Start.Y), End]);
        if (MatchesEndpointSides(A, Exit, Entry)) return A;
        List<BendPoint> B = Normalize([Start, new BendPoint(Start.X, End.Y), End]);
        return MatchesEndpointSides(B, Exit, Entry) ? B : null;
    }

    private static List<BendPoint>? FindBest(BendRoute Route, IReadOnlyList<BendObstacle> Obstacles,
        IReadOnlyList<BendSegment> OtherLines)
    {
        BendPoint Start = Route.Points[0];
        BendPoint End = Route.Points[^1];
        List<List<BendPoint>> Candidates = [];
        for (int Index = 1; Index + 2 < Route.Points.Count; Index++)
        {
            BendPoint Before = Route.Points[Index - 1];
            BendPoint After = Route.Points[Index + 2];
            List<BendPoint> Prefix = Route.Points.Take(Index).ToList();
            List<BendPoint> Suffix = Route.Points.Skip(Index + 2).ToList();
            AddCandidate(Candidates, Prefix.Concat([new BendPoint(After.X, Before.Y)]).Concat(Suffix).ToList());
            AddCandidate(Candidates, Prefix.Concat([new BendPoint(Before.X, After.Y)]).Concat(Suffix).ToList());
        }
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
                && HasMinimumRuns(Candidate) && IsClear(Candidate, Obstacles)
                && LegalLineInteraction(Candidate, OtherLines, Route.RoutedCrossings, Route.AllowedCrossings))
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
        {
            foreach (BendObstacle Box in Obstacles)
                if (SegmentCrossesInterior(Points[Index - 1], Points[Index], Box)) return false;
        }
        return true;
    }

    private static bool LegalLineInteraction(IReadOnlyList<BendPoint> Points,
        IReadOnlyList<BendSegment> OtherLines, int RoutedCrossings, int AllowedCrossings)
    {
        (int Crossings, bool Overlap) = LineInteraction(Points, OtherLines);
        return !Overlap && (Crossings == 0 || Crossings > RoutedCrossings && Crossings <= AllowedCrossings);
    }

    private static (int Crossings, bool Overlap) LineInteraction(IReadOnlyList<BendPoint> Points,
        IReadOnlyList<BendSegment> OtherLines)
    {
        HashSet<(long X, long Y, string Line)> Crossings = [];
        for (int Index = 1; Index < Points.Count; Index++)
        foreach (BendSegment Other in OtherLines)
        {
            BendPoint A = Points[Index - 1], B = Points[Index], C = Other.Start, D = Other.End;
            bool ABHorizontal = Math.Abs(A.Y - B.Y) <= Epsilon;
            bool CDHorizontal = Math.Abs(C.Y - D.Y) <= Epsilon;
            if (ABHorizontal == CDHorizontal)
            {
                if (ABHorizontal && Math.Abs(A.Y - C.Y) > Epsilon
                    || !ABHorizontal && Math.Abs(A.X - C.X) > Epsilon) continue;
                double A0 = ABHorizontal ? Math.Min(A.X, B.X) : Math.Min(A.Y, B.Y);
                double A1 = ABHorizontal ? Math.Max(A.X, B.X) : Math.Max(A.Y, B.Y);
                double C0 = ABHorizontal ? Math.Min(C.X, D.X) : Math.Min(C.Y, D.Y);
                double C1 = ABHorizontal ? Math.Max(C.X, D.X) : Math.Max(C.Y, D.Y);
                if (Math.Max(A0, C0) < Math.Min(A1, C1) - Epsilon) return (Crossings.Count, true);
                continue;
            }
            BendPoint Crossing = ABHorizontal ? new BendPoint(C.X, A.Y) : new BendPoint(A.X, C.Y);
            if (Between(Crossing.X, A.X, B.X) && Between(Crossing.Y, A.Y, B.Y)
                && Between(Crossing.X, C.X, D.X) && Between(Crossing.Y, C.Y, D.Y)
                && !Same(Crossing, Points[0]) && !Same(Crossing, Points[^1]))
                Crossings.Add(((long)Math.Round(Crossing.X * 1000), (long)Math.Round(Crossing.Y * 1000), Other.LineId));
        }
        return (Crossings.Count, false);
    }

    private static bool Between(double Value, double A, double B) =>
        Value >= Math.Min(A, B) - Epsilon && Value <= Math.Max(A, B) + Epsilon;

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

    private static bool IsFanBetweenColumns(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        FlowchartLayoutEdgeRoute Route,
        FlowchartDirection Direction)
    {
        if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
            || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)) return false;
        List<FlowchartLayoutNodeBounds> OutTargets = Contract.Edges.Where(Edge => IsVisible(Edge)
                && Edge.SourceId == Route.SourceId && Bounds.ContainsKey(Edge.TargetId))
            .Select(Edge => Bounds[Edge.TargetId]).ToList();
        if (OutTargets.Count >= 2 && PeersFormOtherColumn(Source, OutTargets, Direction)) return true;
        List<FlowchartLayoutNodeBounds> InSources = Contract.Edges.Where(Edge => IsVisible(Edge)
                && Edge.TargetId == Route.TargetId && Bounds.ContainsKey(Edge.SourceId))
            .Select(Edge => Bounds[Edge.SourceId]).ToList();
        return InSources.Count >= 2 && PeersFormOtherColumn(Target, InSources, Direction);
    }

    private static bool PeersFormOtherColumn(FlowchartLayoutNodeBounds Hub,
        IReadOnlyList<FlowchartLayoutNodeBounds> Peers, FlowchartDirection Direction)
    {
        (double HubCross, _) = Axes(new BendPoint(Hub.CenterX, Hub.CenterY), Direction);
        List<double> Crosses = Peers.Select(Peer => Axes(new BendPoint(Peer.CenterX, Peer.CenterY), Direction).Cross).ToList();
        double Column = Crosses.Average();
        return Math.Abs(Column - HubCross) >= DiagramGridMetrics.GRID_UNIT_PX - Epsilon
            && Crosses.All(Cross => Math.Abs(Cross - Column) <= DiagramGridMetrics.GRID_UNIT_PX / 2.0);
    }

    private static bool IsVisible(EdgeContract Edge) =>
        !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase);

    private static BendBox? ToBox(FlowchartLayoutNodeBounds? Bounds) => Bounds == null ? null
        : new BendBox(Bounds.CenterX, Bounds.CenterY, Bounds.Width, Bounds.Height);

    private static (double Cross, double Flow) Axes(BendPoint Point, FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => (Point.X, -Point.Y),
        FlowchartDirection.BottomToTop => (Point.X, Point.Y),
        FlowchartDirection.LeftToRight => (Point.Y, Point.X),
        FlowchartDirection.RightToLeft => (Point.Y, -Point.X),
        _ => (Point.X, -Point.Y)
    };

    private static (double Cross, double Flow, double HalfCross, double HalfFlow) Axes(
        BendBox Box, FlowchartDirection Direction)
    {
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        (double Cross, double Flow) = Axes(new BendPoint(Box.CenterX, Box.CenterY), Direction);
        return (Cross, Flow, Horizontal ? Box.Height / 2.0 : Box.Width / 2.0,
            Horizontal ? Box.Width / 2.0 : Box.Height / 2.0);
    }

    private static BendPoint PointFromAxes(double Cross, double Flow, FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => new BendPoint(Cross, -Flow),
        FlowchartDirection.BottomToTop => new BendPoint(Cross, Flow),
        FlowchartDirection.LeftToRight => new BendPoint(Flow, Cross),
        FlowchartDirection.RightToLeft => new BendPoint(-Flow, Cross),
        _ => new BendPoint(Cross, -Flow)
    };

    private static double TargetFlow(BendBox Box, FlowchartDirection Direction) => Axes(Box, Direction).Flow;

    private static string UpstreamSide(FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => "top",
        FlowchartDirection.BottomToTop => "bottom",
        FlowchartDirection.LeftToRight => "left",
        FlowchartDirection.RightToLeft => "right",
        _ => "top"
    };

    private static string CrossSide(bool Negative, FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom or FlowchartDirection.BottomToTop => Negative ? "left" : "right",
        FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft => Negative ? "bottom" : "top",
        _ => Negative ? "left" : "right"
    };

    private static string FacingSide(BendBox From, BendBox To)
    {
        double Dx = To.CenterX - From.CenterX;
        double Dy = To.CenterY - From.CenterY;
        return Math.Abs(Dx) >= Math.Abs(Dy) ? Dx >= 0 ? "right" : "left" : Dy >= 0 ? "top" : "bottom";
    }

    private static bool Near(double A, double B) => Math.Abs(A - B) <= Epsilon;

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

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
    double LabelRequiredVerticalPx = 0.0,
    bool FanToNextRank = false,
    bool AdjacentRankBlockLine = false,
    bool HintOverridesRoutingFault = false);

public sealed record UnnecessaryBendViolation(
    string DiagramId,
    string LineId,
    char Check,
    int RoutedCorners,
    double RoutedLength,
    IReadOnlyList<BendPoint> BetterRoute,
    string? Detail = null);

/// <summary>Compares routed orthogonal lines with legal routes using the same ports and endpoint sides.</summary>
public static partial class UnnecessaryBendChecker
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
            Edges.TryGetValue(Route.EdgeId, out EdgeContract? Edge);
            if (string.Equals(Edge?.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)) continue;
            List<BendObstacle> Obstacles = Bounds.Values
                .Where(Box => Box.NodeId != Route.SourceId && Box.NodeId != Route.TargetId
                    && !Contract.Groups.Any(Group => Group.Id == Box.NodeId
                        && (IsWithinGroup(Contract, Group.Id, Route.SourceId)
                            || IsWithinGroup(Contract, Group.Id, Route.TargetId))))
                .Select(Box => new BendObstacle(Box.NodeId, Box.CenterX - Box.Width / 2.0,
                    Box.CenterY - Box.Height / 2.0, Box.CenterX + Box.Width / 2.0, Box.CenterY + Box.Height / 2.0))
                .ToList();
            bool HintNeeded = Edge?.RoutingHint?.IsActive == true;
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
            (bool FanBetweenColumns, bool FanToNextRank) = ClassifyFan(Contract, Bounds, Route, Direction);
            bool AdjacentRankBlockLine = IsAdjacentRankBlockLine(Contract, Bounds, Route, Direction);
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
                LabelRequiredHorizontal, LabelRequiredVertical, FanToNextRank, AdjacentRankBlockLine, HintNeeded);
            UnnecessaryBendViolation? Finding = CheckRoute(DiagramId, Input, Obstacles, OtherLines);
            if (Finding != null) Result.Add(Finding);
        }
        Result.AddRange(CheckLayoutAlternatives(DiagramId, Contract, Layout));
        return Result;
    }

    private static bool IsWithinGroup(DiagramContract Contract, string GroupId, string ChildId)
    {
        string? Parent = Contract.Nodes.FirstOrDefault(Node => Node.Id == ChildId)?.ParentGroupId
            ?? Contract.Groups.FirstOrDefault(Group => Group.Id == ChildId)?.ParentGroupId;
        HashSet<string> Seen = new(StringComparer.Ordinal);
        while (!string.IsNullOrEmpty(Parent) && Seen.Add(Parent))
        {
            if (Parent == GroupId) return true;
            Parent = Contract.Groups.FirstOrDefault(Group => Group.Id == Parent)?.ParentGroupId;
        }
        return false;
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
        if (LineInteraction(Best, OtherLines ?? []).Crossings < Route.RoutedCrossings)
            return new UnnecessaryBendViolation(DiagramId, Route.LineId, 'j',
                RoutedCorners, RoutedLength, Best);
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
        if (!MatchesEndpointSides(Route.Points, Route.ExitSide, Route.EntrySide))
            return ('k', FindBest(Route, Obstacles, OtherLines) ?? Route.Points.ToList());

        if (Route.SourceIsDiamond && Route.SourceBox.HasValue && Route.TargetBox.HasValue
            && DiamondBranchViolation(Route, OtherLines, out List<BendPoint>? DiamondRoute))
            return ('f', DiamondRoute);

        if (!string.IsNullOrWhiteSpace(Route.Label) && LabelHasNoLegalRun(Route))
        {
            List<BendPoint>? Better = FindBest(Route, Obstacles, OtherLines);
            return ('g', Better);
        }

        if (!Route.HintOverridesRoutingFault && !Route.AdjacentRankBlockLine
            && Route.IncomingCount > 1 && Route.SourceBox.HasValue && Route.TargetBox.HasValue
            && ConvergenceViolation(Route, Obstacles, OtherLines, out List<BendPoint>? ConvergingRoute))
            return ('h', ConvergingRoute);

        // Diamond branches use the vertices and one-corner routes of rules 3.8
        // and 3.9. They are not ordinary-box fans governed by rule 5.8.
        if (!Route.HintOverridesRoutingFault && !Route.SourceIsDiamond
            && (Route.FanBetweenColumns || Route.FanToNextRank)
            && Route.SourceBox.HasValue && Route.TargetBox.HasValue
            && FanViolation(Route, OtherLines, out List<BendPoint>? FanRoute))
            return ('i', FanRoute);

        if (Route.RoutedCrossings > 0)
        {
            List<BendPoint>? Better = FindCrossingReduction(Route, Obstacles, OtherLines);
            if (Better != null) return ('j', Better);
        }
        return null;
    }

    private static bool DiamondBranchViolation(BendRoute Route, IReadOnlyList<BendSegment> OtherLines,
        out List<BendPoint>? Better)
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
            Better = OffsetDownstream ? BuildDiamondElbow(Route, Source, Target, OtherLines) : null;
            return true;
        }
        if (Route.OutgoingCount is 2 or 3 && OffsetDownstream)
        {
            bool FacingVertex = TargetCross < SourceCross ? AtLeftVertex : AtRightVertex;
            BendPoint End = Route.Points[^1];
            (double EndCross, double EndFlow) = Axes(End, Route.Direction);
            double TargetFlow = Axes(Target, Route.Direction).Flow;
            bool TopCentre = Near(EndCross, TargetCross) && Near(EndFlow, TargetFlow - TargetHalfFlow);
            BendPoint TargetTopCentre = PointFromAxes(TargetCross, TargetFlow - TargetHalfFlow, Route.Direction);
            bool TopFree = !PortOccupied(TargetTopCentre, Route.TargetId, OtherLines);
            string FacingEntry = CrossSide(SourceCross < TargetCross, Route.Direction);
            bool CorrectEntry = TopFree ? TopCentre : Route.EntrySide == FacingEntry;
            if (!FacingVertex || Route.Points.Count - 2 != 1 || !CorrectEntry)
            {
                Better = BuildDiamondElbow(Route, Source, Target, OtherLines);
                return true;
            }
        }
        return false;
    }

    private static List<BendPoint> BuildDiamondElbow(BendRoute Route, BendBox Source, BendBox Target,
        IReadOnlyList<BendSegment> OtherLines)
    {
        (double SourceCross, double SourceFlow, double SourceHalfCross, _) = Axes(Source, Route.Direction);
        (double TargetCross, double TargetFlow, _, double TargetHalfFlow) = Axes(Target, Route.Direction);
        double VertexCross = TargetCross < SourceCross ? SourceCross - SourceHalfCross : SourceCross + SourceHalfCross;
        BendPoint Start = PointFromAxes(VertexCross, SourceFlow, Route.Direction);
        BendPoint TopCentre = PointFromAxes(TargetCross, TargetFlow - TargetHalfFlow, Route.Direction);
        bool TopFree = !PortOccupied(TopCentre, Route.TargetId, OtherLines);
        double EndCross = TopFree ? TargetCross
            : TargetCross + (SourceCross < TargetCross ? -Axes(Target, Route.Direction).HalfCross
                : Axes(Target, Route.Direction).HalfCross);
        double EndFlow = TopFree ? TargetFlow - TargetHalfFlow : TargetFlow;
        BendPoint Corner = PointFromAxes(TopFree ? TargetCross : VertexCross, SourceFlow, Route.Direction);
        BendPoint End = PointFromAxes(EndCross, EndFlow, Route.Direction);
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
        BendPoint TopCentre = PointFromAxes(TargetCross, TargetFlow - TargetHalfFlow, Route.Direction);
        bool TopFree = !PortOccupied(TopCentre, Route.TargetId, OtherLines);
        string FacingEntry = CrossSide(SourceCross < TargetCross, Route.Direction);
        bool CorrectOneCorner = Route.Points.Count - 2 == 1
            && (Route.EntrySide == FacingEntry || TopFree && Route.EntrySide == UpstreamSide(Route.Direction));
        if (!CorrectOneCorner)
        {
            double SourceSideCross = SourceCross + (SourceCross < TargetCross
                ? Axes(Source, Route.Direction).HalfCross : -Axes(Source, Route.Direction).HalfCross);
            BendPoint Start = TopFree
                ? PointFromAxes(SourceSideCross, SourceFlow, Route.Direction)
                : PointFromAxes(SourceCross, SourceFlow + SourceHalfFlow, Route.Direction);
            BendPoint Corner = TopFree
                ? PointFromAxes(TargetCross, SourceFlow, Route.Direction)
                : PointFromAxes(SourceCross, TargetFlow, Route.Direction);
            BendPoint End = TopFree ? TopCentre : PointFromAxes(
                TargetCross + (SourceCross < TargetCross ? -TargetHalfCross : TargetHalfCross),
                TargetFlow, Route.Direction);
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
        string ExpectedExit = Route.FanToNextRank ? DownstreamSide(Route.Direction) : FacingSide(Source, Target);
        string ExpectedEntry = Route.FanToNextRank ? UpstreamSide(Route.Direction) : FacingSide(Target, Source);
        List<BendSegment> FanLines = OtherLines.Where(Line =>
            Line.SourceId == Route.SourceId || Line.TargetId == Route.TargetId).ToList();
        (int FanCrossings, bool SharedRail) = LineInteraction(Route.Points, FanLines);
        bool CornerEntry = IsBoxCorner(Route.Points[^1], Target);
        if (Route.ExitSide == ExpectedExit && Route.EntrySide == ExpectedEntry
            && !SharedRail && FanCrossings == 0 && !CornerEntry) return false;
        Better = BuildFacingElbow(Route.Points[0], Route.Points[^1], ExpectedExit, ExpectedEntry);
        return true;
    }

    private static List<BendPoint>? FindCrossingReduction(BendRoute Route,
        IReadOnlyList<BendObstacle> Obstacles, IReadOnlyList<BendSegment> OtherLines)
    {
        BendPoint Start = Route.Points[0];
        BendPoint End = Route.Points[^1];
        List<List<BendPoint>> Candidates = [];
        HashSet<double> XTracks = [Start.X, End.X];
        HashSet<double> YTracks = [Start.Y, End.Y];
        foreach (BendSegment Segment in OtherLines)
        foreach (BendPoint Point in new[] { Segment.Start, Segment.End })
        {
            XTracks.Add(Point.X - DiagramGridMetrics.GRID_UNIT_PX);
            XTracks.Add(Point.X + DiagramGridMetrics.GRID_UNIT_PX);
            YTracks.Add(Point.Y - DiagramGridMetrics.GRID_UNIT_PX);
            YTracks.Add(Point.Y + DiagramGridMetrics.GRID_UNIT_PX);
        }
        foreach (BendObstacle Obstacle in Obstacles)
        {
            XTracks.Add(Obstacle.Left - DiagramGridMetrics.GRID_UNIT_PX);
            XTracks.Add(Obstacle.Right + DiagramGridMetrics.GRID_UNIT_PX);
            YTracks.Add(Obstacle.Bottom - DiagramGridMetrics.GRID_UNIT_PX);
            YTracks.Add(Obstacle.Top + DiagramGridMetrics.GRID_UNIT_PX);
        }
        foreach (BendPoint StartPort in CandidatePorts(Route.SourceBox, Route.ExitSide, Start))
        foreach (BendPoint EndPort in CandidatePorts(Route.TargetBox, Route.EntrySide, End))
        {
            foreach (double Y in YTracks)
                AddCandidate(Candidates, [StartPort, new BendPoint(StartPort.X, Y),
                    new BendPoint(EndPort.X, Y), EndPort]);
            foreach (double X in XTracks)
                AddCandidate(Candidates, [StartPort, new BendPoint(X, StartPort.Y),
                    new BendPoint(X, EndPort.Y), EndPort]);
            AddCandidate(Candidates, [StartPort, new BendPoint(EndPort.X, StartPort.Y), EndPort]);
            AddCandidate(Candidates, [StartPort, new BendPoint(StartPort.X, EndPort.Y), EndPort]);
        }
        foreach (double X in XTracks)
            AddCandidate(Candidates, [Start, new BendPoint(X, Start.Y), new BendPoint(X, End.Y), End]);
        foreach (double Y in YTracks)
            AddCandidate(Candidates, [Start, new BendPoint(Start.X, Y), new BendPoint(End.X, Y), End]);
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
                && HasDistinctEndpointPorts(Candidate[0], Candidate[^1], Route, OtherLines)
                && HasMinimumRuns(Candidate) && IsClear(Candidate, Obstacles)
                && !LineInteraction(Candidate, OtherLines).Overlap
                && LineInteraction(Candidate, OtherLines).Crossings < Route.RoutedCrossings)
            .OrderBy(Candidate => LineInteraction(Candidate, OtherLines).Crossings)
            .ThenBy(Candidate => Candidate.Count).ThenBy(Length).FirstOrDefault();
    }

    private static IEnumerable<BendPoint> CandidatePorts(BendBox? Box, string Side, BendPoint Current)
    {
        if (!Box.HasValue) return [Current];
        BendBox Value = Box.Value;
        bool HorizontalSide = Side is "top" or "bottom";
        double HalfSpan = (HorizontalSide ? Value.Width : Value.Height) / 2.0;
        double Center = HorizontalSide ? Value.CenterX : Value.CenterY;
        double Min = Center - HalfSpan + DiagramGridMetrics.GRID_UNIT_PX;
        double Max = Center + HalfSpan - DiagramGridMetrics.GRID_UNIT_PX;
        HashSet<BendPoint> Ports = [Current];
        double Fixed = Side switch
        {
            "left" => Value.CenterX - Value.Width / 2.0,
            "right" => Value.CenterX + Value.Width / 2.0,
            "bottom" => Value.CenterY - Value.Height / 2.0,
            "top" => Value.CenterY + Value.Height / 2.0,
            _ => HorizontalSide ? Current.Y : Current.X
        };
        for (double Position = Min; Position <= Max + Epsilon; Position += DiagramGridMetrics.GRID_UNIT_PX)
            Ports.Add(HorizontalSide ? new BendPoint(Position, Fixed) : new BendPoint(Fixed, Position));
        return Ports;
    }

    private static bool HasDistinctEndpointPorts(BendPoint Start, BendPoint End, BendRoute Route,
        IReadOnlyList<BendSegment> OtherLines) => OtherLines.All(Line =>
        !(Line.SourceId == Route.SourceId && Same(Line.Start, Start))
        && !(Line.TargetId == Route.SourceId && Same(Line.End, Start))
        && !(Line.SourceId == Route.TargetId && Same(Line.Start, End))
        && !(Line.TargetId == Route.TargetId && Same(Line.End, End)));

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
        foreach (BendSegment Segment in OtherLines)
        foreach (BendPoint Point in new[] { Segment.Start, Segment.End })
        {
            XTracks.Add(Point.X - DiagramGridMetrics.GRID_UNIT_PX);
            XTracks.Add(Point.X + DiagramGridMetrics.GRID_UNIT_PX);
            YTracks.Add(Point.Y - DiagramGridMetrics.GRID_UNIT_PX);
            YTracks.Add(Point.Y + DiagramGridMetrics.GRID_UNIT_PX);
        }
        XTracks.Add(Start.X - DiagramGridMetrics.GRID_UNIT_PX);
        XTracks.Add(Start.X + DiagramGridMetrics.GRID_UNIT_PX);
        XTracks.Add(End.X - DiagramGridMetrics.GRID_UNIT_PX);
        XTracks.Add(End.X + DiagramGridMetrics.GRID_UNIT_PX);
        YTracks.Add(Start.Y - DiagramGridMetrics.GRID_UNIT_PX);
        YTracks.Add(Start.Y + DiagramGridMetrics.GRID_UNIT_PX);
        YTracks.Add(End.Y - DiagramGridMetrics.GRID_UNIT_PX);
        YTracks.Add(End.Y + DiagramGridMetrics.GRID_UNIT_PX);
        foreach (double X in XTracks)
            AddCandidate(Candidates, [Start, new BendPoint(X, Start.Y), new BendPoint(X, End.Y), End]);
        foreach (double Y in YTracks)
            AddCandidate(Candidates, [Start, new BendPoint(Start.X, Y), new BendPoint(End.X, Y), End]);

        return Candidates.Select(Normalize)
            .Where(Candidate => Candidate.Count >= 2 && IsOrthogonal(Candidate)
                && MatchesEndpointSides(Candidate, Route.ExitSide, Route.EntrySide)
                && HasMinimumRuns(Candidate) && IsClear(Candidate, Obstacles)
                && (string.IsNullOrWhiteSpace(Route.Label)
                    || !LabelHasNoLegalRun(Route with { Points = Candidate }))
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

    private static (bool SideColumn, bool NextRank) ClassifyFan(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        FlowchartLayoutEdgeRoute Route,
        FlowchartDirection Direction)
    {
        if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
            || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)) return (false, false);
        List<FlowchartLayoutNodeBounds> OutTargets = Contract.Edges.Where(Edge => IsVisible(Edge)
                && Edge.SourceId == Route.SourceId && Bounds.ContainsKey(Edge.TargetId))
            .Select(Edge => Bounds[Edge.TargetId]).ToList();
        if (OutTargets.Count >= 2 && SameRank(OutTargets, Direction))
        {
            if (AllInNextRank(Source, OutTargets, Direction)) return (false, true);
            if (AllWhollyBeyondSide(Source, OutTargets)) return (true, false);
        }
        List<FlowchartLayoutNodeBounds> InSources = Contract.Edges.Where(Edge => IsVisible(Edge)
                && Edge.TargetId == Route.TargetId && Bounds.ContainsKey(Edge.SourceId))
            .Select(Edge => Bounds[Edge.SourceId]).ToList();
        if (InSources.Count >= 2 && SameRank(InSources, Direction))
        {
            if (AllInPreviousRank(Target, InSources, Direction)) return (false, true);
            if (AllWhollyBeyondSide(Target, InSources)) return (true, false);
        }
        return (false, false);
    }

    private static bool SameRank(IReadOnlyList<FlowchartLayoutNodeBounds> Peers, FlowchartDirection Direction) =>
        Peers.Select(Peer => Axes(new BendPoint(Peer.CenterX, Peer.CenterY), Direction).Flow)
            .DistinctBy(Value => Math.Round(Value, 3)).Count() == 1;

    private static bool AllWhollyBeyondSide(FlowchartLayoutNodeBounds Hub,
        IReadOnlyList<FlowchartLayoutNodeBounds> Peers) =>
        Peers.All(Peer => Peer.CenterX - Peer.Width / 2.0 >= Hub.CenterX + Hub.Width / 2.0 - Epsilon)
        || Peers.All(Peer => Peer.CenterX + Peer.Width / 2.0 <= Hub.CenterX - Hub.Width / 2.0 + Epsilon);

    private static bool AllInNextRank(FlowchartLayoutNodeBounds Hub,
        IReadOnlyList<FlowchartLayoutNodeBounds> Peers, FlowchartDirection Direction) =>
        Peers.All(Peer => Axes(new BendPoint(Peer.CenterX, Peer.CenterY), Direction).Flow
            > Axes(new BendPoint(Hub.CenterX, Hub.CenterY), Direction).Flow + Epsilon);

    private static bool AllInPreviousRank(FlowchartLayoutNodeBounds Hub,
        IReadOnlyList<FlowchartLayoutNodeBounds> Peers, FlowchartDirection Direction) =>
        Peers.All(Peer => Axes(new BendPoint(Peer.CenterX, Peer.CenterY), Direction).Flow
            < Axes(new BendPoint(Hub.CenterX, Hub.CenterY), Direction).Flow - Epsilon);

    private static bool IsAdjacentRankBlockLine(DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        FlowchartLayoutEdgeRoute Route,
        FlowchartDirection Direction)
    {
        if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
            || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target)) return false;
        double SourceFlow = Axes(new BendPoint(Source.CenterX, Source.CenterY), Direction).Flow;
        double TargetFlow = Axes(new BendPoint(Target.CenterX, Target.CenterY), Direction).Flow;
        List<(string SourceId, string TargetId)> RankEdges = Contract.Edges.Where(IsVisible)
            .Where(Edge => Bounds.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds? CandidateSource)
                && Bounds.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds? CandidateTarget)
                && Near(Axes(new BendPoint(CandidateSource.CenterX, CandidateSource.CenterY), Direction).Flow, SourceFlow)
                && Near(Axes(new BendPoint(CandidateTarget.CenterX, CandidateTarget.CenterY), Direction).Flow, TargetFlow))
            .Select(Edge => (Edge.SourceId, Edge.TargetId)).Distinct().ToList();
        if (!RankEdges.Contains((Route.SourceId, Route.TargetId))) return false;
        var Sources = new HashSet<string>(StringComparer.Ordinal) { Route.SourceId };
        var Targets = new HashSet<string>(StringComparer.Ordinal) { Route.TargetId };
        bool Changed;
        do
        {
            Changed = false;
            foreach ((string SourceId, string TargetId) in RankEdges)
            {
                if (Sources.Contains(SourceId) && Targets.Add(TargetId)) Changed = true;
                if (Targets.Contains(TargetId) && Sources.Add(SourceId)) Changed = true;
            }
        } while (Changed);
        return Sources.Count >= 2 && Targets.Count >= 2;
    }

    private static bool PortOccupied(BendPoint Port, string TargetId, IReadOnlyList<BendSegment> OtherLines) =>
        OtherLines.Any(Line => Line.TargetId == TargetId && (Same(Line.Start, Port) || Same(Line.End, Port)));

    private static bool IsBoxCorner(BendPoint Point, BendBox Box) =>
        Near(Math.Abs(Point.X - Box.CenterX), Box.Width / 2.0)
        && Near(Math.Abs(Point.Y - Box.CenterY), Box.Height / 2.0);

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

    private static string DownstreamSide(FlowchartDirection Direction) => Direction switch
    {
        FlowchartDirection.TopToBottom => "bottom",
        FlowchartDirection.BottomToTop => "top",
        FlowchartDirection.LeftToRight => "right",
        FlowchartDirection.RightToLeft => "left",
        _ => "bottom"
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

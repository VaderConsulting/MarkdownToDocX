using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Measures a routed layout using rules 2.13 and 2.14.</summary>
internal static class FlowchartLayoutScore
{
    internal sealed record Result(double Width, double Height, int Overlaps, int Crossings, int ForcedMinimum,
        int Bends, int HintsHonoured, int HintsTotal, double EmptyShare, double? Score,
        IReadOnlyList<FlowchartLayoutQualityFailure> Violations);

    internal static Result Measure(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        (double Width, double Height) = Bounds(Layout);
        var Violations = new List<FlowchartLayoutQualityFailure>();
        DiagramGridQuality.Collect(Contract, Layout, Violations);
        int Overlaps = Violations.Count(Failure => Failure.Kind == "NodeOverlap");
        int Crossings = CountCrossings(Layout);
        int ForcedMinimum = ForcedCrossingMinimum(Contract, Layout);
        int Bends = CountBends(Layout);
        (int HintsHonoured, int HintsTotal, double HintScore) = MeasureHints(Contract, Layout);
        double EmptyShare = EmptyAreaShare(Layout, Width, Height);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract)
            .Select(Edge => (Edge.SourceId, Edge.TargetId)).ToHashSet();
        bool Rejected = Overlaps > 0 || Violations.Any(Failure => Rejects(Failure, Contract, Layout, Back));
        double? Score = Rejected ? null
            : Calculate(Width, Height, Crossings, ForcedMinimum, EmptyShare) + Bends * 2.0 + HintScore;
        return new(Width, Height, Overlaps, Crossings, ForcedMinimum, Bends,
            HintsHonoured, HintsTotal, EmptyShare, Score, Violations);
    }

    private static bool Rejects(FlowchartLayoutQualityFailure Failure, DiagramContract Contract,
        FlowchartLayoutResult Layout,
        IReadOnlySet<(string SourceId, string TargetId)> Back)
    {
        if (Failure.Kind is "NodeOverlap" or "ArrangementKept") return false;
        if (Failure.Kind != "LineShape" || !Failure.Description.Contains("more than two corners", StringComparison.Ordinal)
            || Failure.InvolvedIds.Count < 2) return true;
        if (Contract.Edges.Any(Edge => Edge.SourceId == Failure.InvolvedIds[0]
            && Edge.TargetId == Failure.InvolvedIds[1] && Edge.RoutingHint?.IsActive == true)) return false;
        if (Contract.Groups.Any(Group => Group.Id == Failure.InvolvedIds[0]
            || Group.Id == Failure.InvolvedIds[1])) return false;
        if (!Back.Contains((Failure.InvolvedIds[0], Failure.InvolvedIds[1]))) return true;
        FlowchartLayoutEdgeRoute? Route = Layout.EdgeRoutes.FirstOrDefault(Edge =>
            Edge.SourceId == Failure.InvolvedIds[0] && Edge.TargetId == Failure.InvolvedIds[1]);
        if (Route == null) return true;
        int Corners = 0;
        for (int Index = 1; Index < Route.Points.Count - 1; Index++)
        {
            bool BeforeHorizontal = Math.Abs(Route.Points[Index].Y - Route.Points[Index - 1].Y) < 0.5;
            bool AfterHorizontal = Math.Abs(Route.Points[Index + 1].Y - Route.Points[Index].Y) < 0.5;
            if (BeforeHorizontal != AfterHorizontal) Corners++;
        }
        return Corners > 3;
    }

    internal static double Calculate(double Width, double Height, int Crossings, double EmptyShare)
        => Calculate(Width, Height, Crossings, 0, EmptyShare);

    internal static double Calculate(double Width, double Height, int Crossings, int ForcedMinimum, double EmptyShare)
    {
        const double TargetRatio = 4.0 / 3.0;
        double Shape = Math.Abs((Width / Math.Max(1.0, Height)) - TargetRatio) / TargetRatio * 100.0 * 0.4;
        double EmptyPercent = EmptyShare * 100.0;
        double Empty = EmptyPercent <= 30.0 ? EmptyPercent * 2.0 : 60.0 + (EmptyPercent - 30.0) * 8.0;
        return Shape + Empty + Math.Max(0, Crossings - ForcedMinimum) * 20.0;
    }

    private static int ForcedCrossingMinimum(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        var Bounds = Layout.NodeBounds.ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        bool Horizontal = FlowchartDirectionNormalizer.Normalize(Contract.Direction)
            is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        double Rank(FlowchartLayoutNodeBounds Box) => Math.Round(Horizontal ? Box.CenterX : Box.CenterY);
        List<double> Ranks = Bounds.Values.Select(Rank).Distinct().Order().ToList();
        var RankIndex = Ranks.Select((Value, Index) => (Value, Index)).ToDictionary(Item => Item.Value, Item => Item.Index);
        var Pairs = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                && Bounds.ContainsKey(Edge.SourceId) && Bounds.ContainsKey(Edge.TargetId)
                && Math.Abs(RankIndex[Rank(Bounds[Edge.SourceId])] - RankIndex[Rank(Bounds[Edge.TargetId])]) == 1)
            .Select(Edge => (Edge.SourceId, Edge.TargetId, SourceRank: Rank(Bounds[Edge.SourceId]),
                TargetRank: Rank(Bounds[Edge.TargetId])))
            .Distinct().GroupBy(Edge => (Edge.SourceRank, Edge.TargetRank));
        int Result = 0;
        foreach (var RankPair in Pairs)
        {
            var Edges = RankPair.Select(Edge => (Edge.SourceId, Edge.TargetId)).ToHashSet();
            var RemainingSources = Edges.Select(Edge => Edge.SourceId).ToHashSet(StringComparer.Ordinal);
            while (RemainingSources.Count > 0)
            {
                string Seed = RemainingSources.Order(StringComparer.Ordinal).First();
                var Sources = new HashSet<string>(StringComparer.Ordinal) { Seed };
                var Targets = new HashSet<string>(StringComparer.Ordinal);
                bool Changed;
                do
                {
                    Changed = false;
                    foreach ((string SourceId, string TargetId) in Edges)
                    {
                        if (Sources.Contains(SourceId) && Targets.Add(TargetId)) Changed = true;
                        if (Targets.Contains(TargetId) && Sources.Add(SourceId)) Changed = true;
                    }
                } while (Changed);
                RemainingSources.ExceptWith(Sources);
                if (Sources.Count < 2 || Targets.Count < 2) continue;
                int Present = Edges.Count(Edge => Sources.Contains(Edge.SourceId) && Targets.Contains(Edge.TargetId));
                if (Present != Sources.Count * Targets.Count) continue;
                Result += ChooseTwo(Sources.Count) * ChooseTwo(Targets.Count);
            }
        }
        return Result;
    }

    private static int ChooseTwo(int Count) => Count * (Count - 1) / 2;

    private static (double Width, double Height) Bounds(FlowchartLayoutResult Layout)
    {
        var X = new List<double>();
        var Y = new List<double>();
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds.Concat(Layout.ClusterBounds))
        {
            X.Add(Box.CenterX - Box.Width / 2.0); X.Add(Box.CenterX + Box.Width / 2.0);
            Y.Add(Box.CenterY - Box.Height / 2.0); Y.Add(Box.CenterY + Box.Height / 2.0);
        }
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        foreach (FlowchartLayoutPoint Point in Route.Points)
        {
            X.Add(Point.X); Y.Add(Point.Y);
        }
        return X.Count == 0 ? (0.0, 0.0) : (X.Max() - X.Min(), Y.Max() - Y.Min());
    }

    private static int CountCrossings(FlowchartLayoutResult Layout)
    {
        int Result = 0;
        for (int Left = 0; Left < Layout.EdgeRoutes.Count; Left++)
        for (int Right = Left + 1; Right < Layout.EdgeRoutes.Count; Right++)
        {
            FlowchartLayoutEdgeRoute A = Layout.EdgeRoutes[Left];
            FlowchartLayoutEdgeRoute B = Layout.EdgeRoutes[Right];
            if (A.SourceId == B.SourceId || A.SourceId == B.TargetId
                || A.TargetId == B.SourceId || A.TargetId == B.TargetId) continue;
            Result += FlowchartBranchRouteOrder.Intersections(
                A.Points.Select(Point => (Point.X, Point.Y)).ToList(),
                B.Points.Select(Point => (Point.X, Point.Y)).ToList()).Crossings;
        }
        return Result;
    }

    private static int CountBends(FlowchartLayoutResult Layout) =>
        Layout.EdgeRoutes.Sum(Route => CountCorners(Route.Points));

    private static int CountCorners(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        int Result = 0;
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            bool BeforeHorizontal = Math.Abs(Points[Index].Y - Points[Index - 1].Y) < 0.5;
            bool AfterHorizontal = Math.Abs(Points[Index + 1].Y - Points[Index].Y) < 0.5;
            if (BeforeHorizontal != AfterHorizontal) Result++;
        }
        return Result;
    }

    private static (int Honoured, int Total, double Score) MeasureHints(
        DiagramContract Contract,
        FlowchartLayoutResult Layout)
    {
        int Honoured = 0;
        int Total = 0;
        foreach (PlacementHintContract Hint in Contract.Nodes.Select(Node => Node.PlacementHint)
            .Concat(Contract.Groups.Select(Group => Group.PlacementHint)).OfType<PlacementHintContract>())
        {
            Total++;
            if (Hint.IsActive && Hint.UnhonouredReason == null) Honoured++;
        }

        foreach (EdgeContract Edge in Contract.Edges.Where(Edge => Edge.RoutingHint != null))
        {
            Total++;
            RoutingHintContract Hint = Edge.RoutingHint!;
            FlowchartLayoutEdgeRoute? Route = Layout.EdgeRoutes.FirstOrDefault(Item =>
                !string.IsNullOrEmpty(Edge.Id) && Item.EdgeId == Edge.Id
                || string.IsNullOrEmpty(Edge.Id) && Item.SourceId == Edge.SourceId && Item.TargetId == Edge.TargetId);
            bool Matches = Hint.IsActive && Hint.UnhonouredReason == null && Route != null;
            if (Matches && Hint.Leave.HasValue)
                Matches = Route!.ExitSide == DiagramGridLayout.HintSide(Hint.Leave.Value);
            if (Matches && Hint.Enter.HasValue)
                Matches = Route!.EntrySide == DiagramGridLayout.HintSide(Hint.Enter.Value);
            if (Matches && Hint.Travel.HasValue)
                Matches = DiagramGridRouteBuilder.HasTravelDirection(
                    Route!.Points.Select(Point => (Point.X, Point.Y)).ToList(), Hint.Travel.Value);
            if (Matches) Honoured++;
        }

        int PlacementHonoured = Contract.Nodes.Count(Node => Node.PlacementHint?.IsActive == true
                && Node.PlacementHint.UnhonouredReason == null)
            + Contract.Groups.Count(Group => Group.PlacementHint?.IsActive == true
                && Group.PlacementHint.UnhonouredReason == null);
        int LineHonoured = Honoured - PlacementHonoured;
        return (Honoured, Total, PlacementHonoured * -30.0 + LineHonoured * -15.0);
    }

    private static double EmptyAreaShare(FlowchartLayoutResult Layout, double Width, double Height)
    {
        if (Width <= 0.0 || Height <= 0.0) return 0.0;
        const double Cell = DiagramGridMetrics.GRID_UNIT_PX;
        double Left = Layout.NodeBounds.Concat(Layout.ClusterBounds)
            .Min(Box => Box.CenterX - Box.Width / 2.0);
        double Bottom = Layout.NodeBounds.Concat(Layout.ClusterBounds)
            .Min(Box => Box.CenterY - Box.Height / 2.0);
        int Columns = Math.Max(1, (int)Math.Ceiling(Width / Cell));
        int Rows = Math.Max(1, (int)Math.Ceiling(Height / Cell));
        var Used = new HashSet<(int Column, int Row)>();
        void Mark(double MinX, double MinY, double MaxX, double MaxY)
        {
            int FirstColumn = (int)Math.Floor((MinX - Left) / Cell);
            int LastColumn = (int)Math.Floor((MaxX - Left) / Cell);
            int FirstRow = (int)Math.Floor((MinY - Bottom) / Cell);
            int LastRow = (int)Math.Floor((MaxY - Bottom) / Cell);
            for (int Column = FirstColumn; Column <= LastColumn; Column++)
            for (int Row = FirstRow; Row <= LastRow; Row++)
                if (Column >= 0 && Column < Columns && Row >= 0 && Row < Rows)
                    Used.Add((Column, Row));
        }
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds.Concat(Layout.ClusterBounds))
            Mark(Box.CenterX - Box.Width / 2.0, Box.CenterY - Box.Height / 2.0,
                Box.CenterX + Box.Width / 2.0, Box.CenterY + Box.Height / 2.0);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        for (int Index = 1; Index < Route.Points.Count; Index++)
        {
            FlowchartLayoutPoint A = Route.Points[Index - 1], B = Route.Points[Index];
            Mark(Math.Min(A.X, B.X) - Cell / 2.0, Math.Min(A.Y, B.Y) - Cell / 2.0,
                Math.Max(A.X, B.X) + Cell / 2.0, Math.Max(A.Y, B.Y) + Cell / 2.0);
        }
        return 1.0 - Used.Count / (double)(Columns * Rows);
    }
}

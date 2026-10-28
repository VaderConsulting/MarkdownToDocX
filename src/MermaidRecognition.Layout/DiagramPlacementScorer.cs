using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

internal sealed record DiagramPlacementMetrics(FlowchartLayoutScore.Result Canonical, DiagramRouteReport Routes,
    double WidthPx, double HeightPx, double RouteLengthPx, int MovedNodes, long DisplacementCells, double DisplacementPx);

internal sealed record DiagramPlacementComparison(DiagramPlacementTrial Trial, DiagramPlacementMetrics Before,
    DiagramPlacementMetrics? After, bool IsImprovement, string Reason, int ChangedRoutes, double RepairCharge);

/// <summary>Scores complete isolated candidates; the nominated relationship is never a substitute for the whole scene.</summary>
internal sealed class DiagramPlacementScorer
{
    private readonly DiagramLayoutSession.Snapshot Baseline;
    private readonly DiagramEvaluationOrigin Origin;
    private readonly double Padding;
    internal DiagramPlacementMetrics Before { get; }
    internal double PaddingPx => Padding;
    internal string RequestSignature { get; }

    internal DiagramPlacementScorer(DiagramLayoutSession.Snapshot Snapshot, double? PaddingPx = null)
    {
        Origin = new(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.Revision);
        Baseline = new DiagramLayoutSession(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.RoutingModel, Snapshot.Layout).CopySnapshot();
        Padding = PaddingPx ?? FlowchartRenderOptions.FromContract(Baseline.RoutingModel).DiagramPaddingPx;
        if (!double.IsFinite(Padding) || Padding < 0) throw new ArgumentOutOfRangeException(nameof(PaddingPx));
        RequestSignature = DiagramSourceUpdateService.PlacementRequestSignature(Baseline.RoutingModel);
        Before = Measure(Baseline);
    }

    internal DiagramPlacementComparison Compare(DiagramPlacementTrial Trial)
    {
        if (Trial.Origin != Origin) return Reject("The trial belongs to a different document or revision.");
        if (Trial.FailureReason != null) return Reject(Trial.FailureReason);
        if (Trial.CopyCandidate() is not { } Candidate) return Reject("The trial has no validated candidate.");
        string? ScopeFailure = CheckScope(Trial, Candidate);
        if (ScopeFailure != null) return Reject(ScopeFailure);
        var After = Measure(Candidate);
        if (NewFindings(Before.Routes, After.Routes)) return Reject("The whole-diagram check found a new route or port failure.", After);
        var OldFaults = Faults(Before.Canonical); var NewFaults = Faults(After.Canonical);
        if (NewFaults.Any(Pair => Pair.Value > OldFaults.GetValueOrDefault(Pair.Key)))
            return Reject("The candidate introduces a new quality failure elsewhere in the diagram.", After);
        if (After.Canonical.HintsHonoured < Before.Canonical.HintsHonoured || After.Canonical.HintsTotal != Before.Canonical.HintsTotal)
            return Reject("The candidate does not retain the complete mandatory hint inventory.", After);
        var OldRoutes = Baseline.Layout.EdgeRoutes.ToDictionary(Route => Route.EdgeId, StringComparer.Ordinal);
        int Changed = Candidate.Layout.EdgeRoutes.Count(Route => !SameRoute(Route, OldRoutes[Route.EdgeId]));
        double Charge = Math.Max(0, After.RouteLengthPx - Before.RouteLengthPx) / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RepairLengthWeight
            + Changed * FlowchartLayoutRules.RepairMovedLineWeight;
        bool Better = Prefer(After, Before, Charge);
        return new(Trial, Before, After, Better, Better ? "Verified whole-diagram improvement under the captured policy."
            : "The complete candidate does not strictly improve the captured policy.", Changed, Charge);
        DiagramPlacementComparison Reject(string Reason, DiagramPlacementMetrics? After = null) => new(Trial, Before, After, false, Reason, 0, 0);
    }

    private bool Prefer(DiagramPlacementMetrics Candidate, DiagramPlacementMetrics Incumbent, double RepairCharge)
    {
        int BeforeFaults = FlowchartRepairCandidateComparison.RoutingFaultCount(Incumbent.Canonical.Violations);
        int AfterFaults = FlowchartRepairCandidateComparison.RoutingFaultCount(Candidate.Canonical.Violations);
        if (BeforeFaults != AfterFaults) return AfterFaults < BeforeFaults;
        if (Candidate.Canonical.HintsHonoured != Incumbent.Canonical.HintsHonoured)
            return Candidate.Canonical.HintsHonoured > Incumbent.Canonical.HintsHonoured;
        var Charged = Candidate.Canonical with { Cost = Candidate.Canonical.Cost + RepairCharge };
        return Baseline.Layout.RenderContext.LayoutOptions.EnableWidthConstrainedLayout
            ? DiagramGridLayout.PreferWidthCandidate(Charged, Incumbent.Canonical, Baseline.Layout.RenderContext.LayoutOptions.TargetWidthPx, Padding)
            : FlowchartRepairCandidateComparison.CandidateBeatsBaseline(BeforeFaults, Incumbent.Canonical.HintsHonoured, Incumbent.Canonical.Cost,
                AfterFaults, Candidate.Canonical.HintsHonoured, Charged.Cost);
    }

    // Both candidates pay their own repair charges relative to the same accepted
    // scene. A selected incumbent must not silently lose its churn/length charge.
    internal int CompareImprovements(DiagramPlacementComparison A, DiagramPlacementComparison B)
    {
        if (!A.IsImprovement || !B.IsImprovement || A.After == null || B.After == null || A.Trial.Origin != Origin || B.Trial.Origin != Origin)
            throw new ArgumentException("Ranking requires two verified improvements from the captured scene.");
        var Left = A.After.Canonical with { Cost = A.After.Canonical.Cost + A.RepairCharge };
        var Right = B.After.Canonical with { Cost = B.After.Canonical.Cost + B.RepairCharge };
        int Faults = FlowchartRepairCandidateComparison.RoutingFaultCount(Left.Violations).CompareTo(FlowchartRepairCandidateComparison.RoutingFaultCount(Right.Violations));
        if (Faults != 0) return Faults;
        int Hints = Right.HintsHonoured.CompareTo(Left.HintsHonoured);
        if (Hints != 0) return Hints;
        int Policy = Baseline.Layout.RenderContext.LayoutOptions.EnableWidthConstrainedLayout
            ? DiagramGridLayout.PreferWidthCandidate(Left, Right, Baseline.Layout.RenderContext.LayoutOptions.TargetWidthPx, Padding) ? -1
                : DiagramGridLayout.PreferWidthCandidate(Right, Left, Baseline.Layout.RenderContext.LayoutOptions.TargetWidthPx, Padding) ? 1 : 0
            : Left.Cost.CompareTo(Right.Cost);
        if (Policy != 0) return Policy;
        int Cells = A.After.DisplacementCells.CompareTo(B.After.DisplacementCells);
        if (Cells != 0) return Cells;
        int Pixels = A.After.DisplacementPx.CompareTo(B.After.DisplacementPx);
        if (Pixels != 0) return Pixels;
        int Moved = A.After.MovedNodes.CompareTo(B.After.MovedNodes);
        return Moved != 0 ? Moved : string.CompareOrdinal(TargetKey(A.Trial), TargetKey(B.Trial));
        static string TargetKey(DiagramPlacementTrial Trial) => JsonSerializer.Serialize(Trial.Targets.OrderBy(Item => Item.Key, StringComparer.Ordinal)
            .Select(Item => new { Id = Item.Key, Item.Value.StartRow, Item.Value.StartColumn, Item.Value.RowSpan, Item.Value.ColumnSpan }));
    }

    private DiagramPlacementMetrics Measure(DiagramLayoutSession.Snapshot Snapshot)
    {
        using var Scope = FlowchartRenderContext.Activate(Snapshot.Layout.RenderContext);
        var Score = FlowchartLayoutScore.Measure(Snapshot.RoutingModel, Snapshot.Layout, IncludeArrangementCheck: false);
        Score = Score with { Violations = Score.Violations.Select(Fault => new FlowchartLayoutQualityFailure(Fault.Kind, Fault.Description,
            Fault.InvolvedIds.ToImmutableArray(), Fault.Justification)).ToImmutableArray() };
        var Routes = DiagramRouteDiagnostics.Measure(Snapshot.RoutingModel, Snapshot.Layout);
        double Length = Snapshot.Layout.EdgeRoutes.Sum(Route => Route.Points.Zip(Route.Points.Skip(1)).Sum(Pair =>
            Math.Sqrt(Math.Pow(Pair.Second.X - Pair.First.X, 2) + Math.Pow(Pair.Second.Y - Pair.First.Y, 2))));
        var Regions = Baseline.Layout.CellNodes!.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Region, StringComparer.Ordinal);
        var Boxes = Baseline.Layout.NodeBounds.ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        int Moved = 0; long Cells = 0; double Displacement = 0;
        foreach (var Node in Snapshot.Layout.CellNodes!)
            if (Regions.TryGetValue(Node.Id, out var Old) && Old != Node.CellNode!.Region)
            {
                Moved++; var New = Node.CellNode!.Region;
                Cells += Math.Abs((long)New.StartRow - Old.StartRow) + Math.Abs((long)New.StartColumn - Old.StartColumn);
                var Box = Snapshot.Layout.NodeBounds.Single(Box => Box.NodeId == Node.Id); var Prior = Boxes[Node.Id];
                Displacement += Math.Sqrt(Math.Pow(Box.CenterX - Prior.CenterX, 2) + Math.Pow(Box.CenterY - Prior.CenterY, 2));
            }
        return new(Score, Routes, Score.Width + 2 * Padding, Score.Height + 2 * Padding, Length, Moved, Cells, Displacement);
    }

    private string? CheckScope(DiagramPlacementTrial Trial, DiagramLayoutSession.Snapshot Candidate)
    {
        var Layout = Candidate.Layout;
        var RequestNodes = Candidate.RoutingModel.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        if (RequestSignature != DiagramSourceUpdateService.PlacementRequestSignature(Candidate.RoutingModel)
            || Baseline.RoutingModel.Nodes.Any(Node => !RequestNodes.TryGetValue(Node.Id, out var Other) || Other.GeometryProvider != Node.GeometryProvider)
            || Layout.RenderContext.LayoutOptions != Baseline.Layout.RenderContext.LayoutOptions)
            return "The trial changes topology, geometry requests or the captured options.";
        var Nodes = Layout.NodeBounds.ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        var Frames = Layout.ClusterBounds.ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        var Routes = Layout.EdgeRoutes.ToDictionary(Route => Route.EdgeId, StringComparer.Ordinal);
        if (Nodes.Count != Baseline.Layout.NodeBounds.Count || Frames.Count != Baseline.Layout.ClusterBounds.Count || Routes.Count != Baseline.Layout.EdgeRoutes.Count
            || Baseline.Layout.NodeBounds.Any(Box => !Nodes.ContainsKey(Box.NodeId)) || Baseline.Layout.ClusterBounds.Any(Box => !Frames.ContainsKey(Box.NodeId))
            || Baseline.Layout.EdgeRoutes.Any(Route => !Routes.ContainsKey(Route.EdgeId))) return "The candidate changes the retained item inventory.";
        if (Baseline.Layout.NodeBounds.Any(Box => !Trial.Targets.ContainsKey(Box.NodeId) && !SameBox(Box, Nodes[Box.NodeId]))
            || Baseline.Layout.ClusterBounds.Any(Box => !SameBox(Box, Frames[Box.NodeId]))) return "The candidate changes geometry outside its declared node targets.";
        if (Baseline.Layout.EdgeRoutes.Any(Route => !Trial.AffectedRouteIds.Contains(Route.EdgeId) && !SameRoute(Route, Routes[Route.EdgeId])))
            return "The candidate changes a relationship outside its routing dependencies.";
        var Cells = Layout.CellNodes!.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Region, StringComparer.Ordinal);
        if (Cells.Count != Baseline.Layout.CellNodes!.Count || Baseline.Layout.CellNodes.Any(Node => !Cells.TryGetValue(Node.Id, out var Region)
            || Region != (Trial.Targets.TryGetValue(Node.Id, out var Target) ? Target : Node.CellNode!.Region)))
            return "The candidate does not match its declared authoritative cell targets.";
        return null;
    }

    private static bool SameBox(FlowchartLayoutNodeBounds A, FlowchartLayoutNodeBounds B) =>
        (A.CenterX, A.CenterY, A.Width, A.Height, A.Geometry) == (B.CenterX, B.CenterY, B.Width, B.Height, B.Geometry);
    private static bool SameRoute(FlowchartLayoutEdgeRoute A, FlowchartLayoutEdgeRoute B) =>
        (A.SourceId, A.TargetId, A.ExitSide, A.EntrySide, A.ExitNormalX, A.ExitNormalY, A.EntryNormalX, A.EntryNormalY)
        == (B.SourceId, B.TargetId, B.ExitSide, B.EntrySide, B.ExitNormalX, B.ExitNormalY, B.EntryNormalX, B.EntryNormalY)
        && A.Points.Select(Point => (Point.X, Point.Y)).SequenceEqual(B.Points.Select(Point => (Point.X, Point.Y)));
    private static Dictionary<(string Kind, string Ids), int> Faults(FlowchartLayoutScore.Result Score) => Score.Violations
        .Where(Fault => Fault.Kind != "ArrangementKept").GroupBy(Fault => (Fault.Kind,
            Ids: JsonSerializer.Serialize(Fault.InvolvedIds.Order(StringComparer.Ordinal).ToArray()))).ToDictionary(Group => Group.Key, Group => Group.Count());
    private static bool NewFindings(DiagramRouteReport Before, DiagramRouteReport After)
    {
        var Old = Before.Findings.Where(Item => Item.Rule is not ("5.1" or "5.9")).ToHashSet();
        if (After.Findings.Any(Item => Item.Rule is not ("5.1" or "5.9") && !Old.Contains(Item))) return true;
        var Pairs = PairKeys(Before).ToHashSet();
        return PairKeys(After).Any(Pair => !Pairs.Contains(Pair));
        static IEnumerable<(string Rule, string A, string B)> PairKeys(DiagramRouteReport Report)
        {
            foreach (var Route in Report.Routes)
            {
                foreach (string Other in Route.CrossingPartners) if (string.CompareOrdinal(Route.Identity, Other) < 0) yield return ("5.9", Route.Identity, Other);
                foreach (string Other in Route.SharedTrackPartners) if (string.CompareOrdinal(Route.Identity, Other) < 0) yield return ("5.1", Route.Identity, Other);
            }
        }
    }
}

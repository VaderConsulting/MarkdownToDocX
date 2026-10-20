using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Removes terminal bends by scoring complete affected-port assignments under rule 4.12.</summary>
internal static class FlowchartBendRemovingPortRepair
{
    /// <summary>Observes a completed swap decision after its exact restore or final acceptance, for focused regressions.</summary>
    internal static Action<string, string>? SwapDecisionObserver;
    /// <summary>A proven whole-assignment improvement, including its exact better allocated port pair.</summary>
    /// <param name="LineId">Declared line whose bends fell.</param>
    /// <param name="Before">Bends before the complete assignment.</param>
    /// <param name="After">Bends after the complete assignment.</param>
    /// <param name="Points">Independent better route, whose endpoints are the proposed port pair.</param>
    internal sealed record Improvement(string LineId, int Before, int After, (double X, double Y)[] Points);
    /// <summary>One end of a declared line at the node currently being visited.</summary>
    /// <param name="Route">Live route owned by this layout.</param>
    /// <param name="Source">Whether the end leaves the source.</param>
    private sealed record End(FlowchartLayoutEdgeRoute Route, bool Source)
    {
        /// <summary>Current allocated side.</summary>
        internal string Side => Source ? Route.ExitSide : Route.EntrySide;
        /// <summary>Current exact allocated point.</summary>
        internal FlowchartLayoutPoint Point => Source ? Route.Points[0] : Route.Points[^1];
    }

    /// <summary>Private exact route state; candidate generation never touches cells, frames, gaps, hints or caches.</summary>
    /// <param name="Route">Route to restore.</param>
    /// <param name="Points">Independent complete polyline.</param>
    /// <param name="Exit">Source side.</param>
    /// <param name="Entry">Target side.</param>
    /// <param name="ExitX">Source normal X.</param>
    /// <param name="ExitY">Source normal Y.</param>
    /// <param name="EntryX">Target normal X.</param>
    /// <param name="EntryY">Target normal Y.</param>
    private sealed record Saved(FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points,
        string Exit, string Entry, double ExitX, double ExitY, double EntryX, double EntryY);

    /// <summary>Visits each node once in a pass, repeating only after a strictly bend-reducing accepted assignment.</summary>
    /// <param name="Contract">Original links and immutable hint declarations.</param>
    /// <param name="Layout">Finished model; only accepted affected routes are changed.</param>
    /// <param name="Accepted">Optional diagnostic callback for each line whose corners were removed.</param>
    /// <returns>Total bends removed from this completed layout.</returns>
    internal static int Apply(DiagramContract Contract, FlowchartLayoutResult Layout, Action<Improvement>? Accepted = null)
    {
        var Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).GroupBy(Box => Box.NodeId)
            .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
        var Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        var Hinted = Layout.EdgeRoutes.Where(Route => DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint?.IsActive == true)
            .Select(Route => (string?)Route.EdgeId).ToHashSet();
        var Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        int Removed = 0;
        bool Progress;
        do
        {
            Progress = false;
            foreach (string Id in Bounds.Keys)
            {
                var Ends = Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2).SelectMany(Route =>
                    (Route.SourceId == Id ? new[] { new End(Route, true) } : Array.Empty<End>())
                    .Concat(Route.TargetId == Id ? new[] { new End(Route, false) } : Array.Empty<End>())).ToArray();
                var Routes = Ends.Select(End => End.Route).Distinct().ToArray();
                if (Routes.Length > FlowchartLayoutRules.RepairChainLines || !Ends.Any(End =>
                    !Hinted.Contains(End.Route.EdgeId) && FlowchartLayoutScore.CountCorners(End.Route.Points) > 0)) continue;
                var Original = Save(Routes);
                var Changes = Candidates(Id, Ends, Hinted, Contract, Layout, Bounds, Nodes).Take(FlowchartLayoutRules.RepairCandidateMoves).ToArray();
                if (Changes.Length == 0) continue;
                var Initial = FlowchartLayoutScore.Measure(Contract, Layout, false);
                var Scorer = new FlowchartLayoutScore.CellMovementContext(Contract, Layout);
                var InitialFindings = FlowchartRouteRuleChecker.Check(Contract, Layout);
                var Reference = Layout.EdgeRoutes.ToDictionary(Route => Route, Route => Route.Points.Select(Point => (Point.X, Point.Y)).ToArray());
                Saved[]? Best = null; var BestCost = Initial.Cost; int BestRemoved = 0; string BestKind = "";
                foreach (var Change in Changes)
                {
                    Restore(Original); Change.Apply();
                    var Affected = Routes.Where(Route => !SameTerminals(Original.Single(Saved => Saved.Route == Route))).ToHashSet();
                    if (Affected.Count == 0 || Affected.Any(Route => Hinted.Contains(Route.EdgeId))) continue;
                    bool Valid = true;
                    foreach (var Route in Affected)
                    {
                        var A = Route.Points[0]; var B = Route.Points[^1];
                        Nodes.TryGetValue(Route.SourceId, out var Source); Nodes.TryGetValue(Route.TargetId, out var Target);
                        var Path = DiagramGridRouteBuilder.BuildRoute(A.X, A.Y, Bounds[Route.SourceId], Source?.Shape, Route.ExitSide,
                            B.X, B.Y, Bounds[Route.TargetId], Target?.Shape, Route.EntrySide, Bounds, Route.SourceId, Route.TargetId,
                            Contract, Direction, Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft,
                            RequireLegal: true, ExitNormal: (Route.ExitNormalX, Route.ExitNormalY), EntryNormal: (Route.EntryNormalX, Route.EntryNormalY));
                        if (!FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Path)) { Valid = false; break; }
                        Route.Points.Clear(); Route.Points.AddRange(Path.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
                        if (FlowchartLayoutScore.CountCorners(Route.Points) > Corners(Reference[Route])) { Valid = false; break; }
                    }
                    if (!Valid || !Separated(Ends)) continue;
                    // Flow and EIP may change only on lines independently flagged
                    // for this repair: every changed line must lose a bend.
                    int Reduction = Affected.Sum(Route => Corners(Reference[Route]) - FlowchartLayoutScore.CountCorners(Route.Points));
                    if (Reduction <= 0) continue;
                    if (!InteractionsDoNotGrow(Layout, Affected, Reference))
                    {
                        Restore(Original);
                        if (Change.Kind == "swap") SwapDecisionObserver?.Invoke(Id, "crossing-rejected");
                        continue;
                    }
                    var Score = Scorer.Measure(Affected);
                    if (Score.Cost >= BestCost) continue;
                    var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
                    if (Findings.Any(Finding => !InitialFindings.Any(Old => Old.Rule == Finding.Rule && Old.Subject == Finding.Subject))) continue;
                    BestCost = Score.Cost; Best = Save(Routes); BestRemoved = Reduction; BestKind = Change.Kind;
                }
                Restore(Original);
                if (Best == null) continue;
                Restore(Best); Removed += BestRemoved; Progress = true;
                foreach (var Saved in Original.Where(Saved => Corners(Saved.Points) > FlowchartLayoutScore.CountCorners(Saved.Route.Points)))
                    Accepted?.Invoke(new(Saved.Route.EdgeId, Corners(Saved.Points), FlowchartLayoutScore.CountCorners(Saved.Route.Points),
                        Saved.Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()));
                if (BestKind == "swap") SwapDecisionObserver?.Invoke(Id, "accepted");
            }
        } while (Progress);
        return Removed;
    }

    /// <summary>Runs the same legal joint search on independent route state so a checker can identify allocation-caused bends.</summary>
    /// <param name="Contract">Unchanged source and honoured hints.</param>
    /// <param name="Layout">Read-only completed production layout.</param>
    /// <returns>Proven alternatives; the original layout and its caches are untouched.</returns>
    internal static IReadOnlyList<Improvement> Inspect(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        var Copy = FlowchartLayoutAlternatives.Copy(Layout);
        using var Active = FlowchartRenderContext.Activate(Copy.RenderContext);
        var Result = new List<Improvement>();
        Apply(Contract, Copy, Result.Add);
        return Result;
    }

    /// <summary>Offers existing-port swaps, exact aligned ports and adjacent-side joint allocations in deterministic order.</summary>
    /// <param name="Id">Current node identity.</param>
    /// <param name="Ends">Current node's endpoint inventory.</param>
    /// <param name="Hinted">Lines whose sides and ports are frozen.</param>
    /// <param name="Contract">Declared membership.</param>
    /// <param name="Layout">Finished live model.</param>
    /// <param name="Bounds">Unchanged geometry.</param>
    /// <param name="Nodes">Renderer geometry contracts.</param>
    /// <returns>Private assignment actions; each is applied only after exact restore.</returns>
    private static IEnumerable<(string Kind, Action Apply)> Candidates(string Id, End[] Ends, HashSet<string?> Hinted, DiagramContract Contract,
        FlowchartLayoutResult Layout, Dictionary<string, FlowchartLayoutNodeBounds> Bounds, Dictionary<string, NodeContract> Nodes)
    {
        string[] Sides = ["top", "right", "bottom", "left"];
        foreach (var A in Ends.Where(End => !Hinted.Contains(End.Route.EdgeId) && FlowchartLayoutScore.CountCorners(End.Route.Points) > 0))
        {
            foreach (var B in Ends.Where(End => End != A && End.Side == A.Side && !Hinted.Contains(End.Route.EdgeId)))
            {
                var AP = (A.Point.X, A.Point.Y); var BP = (B.Point.X, B.Point.Y);
                yield return ("swap", () => { Put(A, BP, A.Side, Bounds[Id], Nodes.GetValueOrDefault(Id)); Put(B, AP, B.Side, Bounds[Id], Nodes.GetValueOrDefault(Id)); });
            }
            var Other = A.Source ? A.Route.Points[^1] : A.Route.Points[0]; var Box = Bounds[Id];
            var Aligned = A.Side is "left" or "right" ? (A.Point.X, Other.Y) : (Other.X, A.Point.Y);
            string Side = A.Side;
            yield return ("align", () =>
            {
                // Alignment may exchange allocated slots, never manufacture an
                // off-slot terminal. Keep this candidate in its original budget
                // position even when no allocated slot matches the approach.
                var Peer = Ends.FirstOrDefault(End => End != A && End.Side == Side
                    && !Hinted.Contains(End.Route.EdgeId)
                    && Math.Abs(End.Point.X - Aligned.Item1) < 0.001
                    && Math.Abs(End.Point.Y - Aligned.Item2) < 0.001);
                if (Peer == null) return;
                var Original = (A.Point.X, A.Point.Y);
                Put(A, Aligned, Side, Box, Nodes.GetValueOrDefault(Id));
                Put(Peer, Original, Side, Box, Nodes.GetValueOrDefault(Id));
            });
            int Index = Array.IndexOf(Sides, Side);
            if (Index < 0) continue;
            foreach (string Next in new[] { Sides[(Index + 1) % Sides.Length], Sides[(Index + Sides.Length - 1) % Sides.Length] })
            {
                yield return ("side", () => { SetSide(A, Next); DiagramGridLayout.ReallocateBendRepairPorts(Contract, Layout, Bounds, Nodes, Id); });
                foreach (var B in Ends.Where(End => End.Side == Next && !Hinted.Contains(End.Route.EdgeId)))
                    yield return ("side-swap", () => { SetSide(A, Next); SetSide(B, Side); DiagramGridLayout.ReallocateBendRepairPorts(Contract, Layout, Bounds, Nodes, Id); });
            }
        }
    }

    /// <summary>Projects a proposed slot through the renderer's boundary and records its exact outward normal.</summary>
    /// <param name="End">Affected allocated end.</param>
    /// <param name="Point">Requested aligned or exchanged coordinate.</param>
    /// <param name="Side">Legal side to use.</param>
    /// <param name="Box">Unchanged renderer bounds.</param>
    /// <param name="Node">Optional renderer geometry provider; frames use their rectangular outline.</param>
    private static void Put(End End, (double X, double Y) Point, string Side, FlowchartLayoutNodeBounds Box, NodeContract? Node)
    {
        double Fraction = Side is "left" or "right" ? (Box.CenterY + Box.Height / 2 - Point.Y) / Box.Height
            : (Point.X - Box.CenterX + Box.Width / 2) / Box.Width;
        if (Fraction <= 0 || Fraction >= 1) return;
        double Nx = Side == "left" ? -1 : Side == "right" ? 1 : 0, Ny = Side == "bottom" ? -1 : Side == "top" ? 1 : 0;
        if (Node?.GeometryProvider is { } Provider)
        {
            if (!Provider.Create(Box.Width, Box.Height).ConnectionBoundary.TryPort(Side, Fraction, out var Port)) return;
            Point = (Box.CenterX + Port.X, Box.CenterY - Port.Y); Nx = Port.NormalX; Ny = -Port.NormalY;
        }
        End.Point.X = Point.X; End.Point.Y = Point.Y; SetSide(End, Side);
        if (End.Source) { End.Route.ExitNormalX = Nx; End.Route.ExitNormalY = Ny; }
        else { End.Route.EntryNormalX = Nx; End.Route.EntryNormalY = Ny; }
    }

    /// <summary>Sets one trial side; joint allocation supplies its new slots and normals.</summary>
    /// <param name="End">Endpoint changed inside this candidate only.</param>
    /// <param name="Side">Adjacent side to try.</param>
    private static void SetSide(End End, string Side) { if (End.Source) End.Route.ExitSide = Side; else End.Route.EntrySide = Side; }

    /// <summary>Checks minimum side-port separation without relaxing an existing checker.</summary>
    /// <param name="Ends">Current node's complete ports.</param>
    /// <returns>Whether distinct side ports have the configured minimum clearance.</returns>
    private static bool Separated(End[] Ends) => Ends.GroupBy(End => End.Side).All(Group => Group.All(A => Group.All(B => A == B
        || Math.Sqrt(Math.Pow(A.Point.X - B.Point.X, 2) + Math.Pow(A.Point.Y - B.Point.Y, 2)) >= FlowchartLayoutRules.TrackSpacing - 0.001)));

    /// <summary>Checks every affected/unaffected pair, so a local improvement cannot add a remote crossing or shared track.</summary>
    /// <param name="Layout">Complete candidate.</param>
    /// <param name="Affected">Only rerouted lines.</param>
    /// <param name="Reference">Exact unchanged base polylines.</param>
    /// <returns>Whether every pair preserves or reduces crossings and overlap.</returns>
    private static bool InteractionsDoNotGrow(FlowchartLayoutResult Layout, IReadOnlySet<FlowchartLayoutEdgeRoute> Affected,
        IReadOnlyDictionary<FlowchartLayoutEdgeRoute, (double X, double Y)[]> Reference)
    {
        foreach (var A in Affected) foreach (var B in Layout.EdgeRoutes.Where(Route => Route != A))
        {
            var Before = FlowchartBranchRouteOrder.Intersections(Reference[A], Reference[B]);
            var After = FlowchartBranchRouteOrder.Intersections(A.Points.Select(Point => (Point.X, Point.Y)).ToArray(), B.Points.Select(Point => (Point.X, Point.Y)).ToArray());
            if (After.Crossings > Before.Crossings || After.Overlap > Before.Overlap + 0.001) return false;
        }
        return true;
    }

    /// <summary>Counts geometric corners on a captured path using the shared scorer.</summary>
    /// <param name="Points">Captured exact polyline.</param>
    /// <returns>The shared bend count.</returns>
    private static int Corners((double X, double Y)[] Points) => FlowchartLayoutScore.CountCorners(Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }).ToArray());

    /// <summary>Captures independent route state before a trial.</summary>
    /// <param name="Routes">Complete affected-node inventory.</param>
    /// <returns>Exact rollback values.</returns>
    private static Saved[] Save(IEnumerable<FlowchartLayoutEdgeRoute> Routes) => Routes.Select(Route => new Saved(Route,
        Route.Points.Select(Point => (Point.X, Point.Y)).ToArray(), Route.ExitSide, Route.EntrySide,
        Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY)).ToArray();

    /// <summary>Detects whether joint allocation changed either terminal.</summary>
    /// <param name="Saved">Original complete route.</param>
    /// <returns>Whether sides, endpoint coordinates and normals remain identical.</returns>
    private static bool SameTerminals(Saved Saved) => Saved.Route.ExitSide == Saved.Exit && Saved.Route.EntrySide == Saved.Entry
        && (Saved.Route.Points[0].X, Saved.Route.Points[0].Y) == Saved.Points[0]
        && (Saved.Route.Points[^1].X, Saved.Route.Points[^1].Y) == Saved.Points[^1]
        && (Saved.Route.ExitNormalX, Saved.Route.ExitNormalY, Saved.Route.EntryNormalX, Saved.Route.EntryNormalY)
            == (Saved.ExitX, Saved.ExitY, Saved.EntryX, Saved.EntryY);

    /// <summary>Restores every route value exactly after a rejected or superseded candidate.</summary>
    /// <param name="Saved">Independent captured assignment.</param>
    private static void Restore(IEnumerable<Saved> Saved)
    {
        foreach (var Item in Saved)
        {
            var Route = Item.Route; Route.ExitSide = Item.Exit; Route.EntrySide = Item.Entry;
            Route.ExitNormalX = Item.ExitX; Route.ExitNormalY = Item.ExitY; Route.EntryNormalX = Item.EntryX; Route.EntryNormalY = Item.EntryY;
            Route.Points.Clear(); Route.Points.AddRange(Item.Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
        }
    }
}

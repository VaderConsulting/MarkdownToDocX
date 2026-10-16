using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Bounded searches that compare a channel's placement and connector assignments atomically.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>A complete connector assignment retained independently of mutable search geometry.</summary>
    /// <param name="Route">The declared connector being assigned.</param>
    /// <param name="Points">The complete unrounded polyline.</param>
    /// <param name="Exit">The source side.</param>
    /// <param name="Entry">The target side.</param>
    private sealed record JointChannelPath(FlowchartLayoutEdgeRoute Route,
        List<(double X, double Y)> Points, string Exit, string Entry);

    /// <summary>Compares complete side, slot and track assignments for a shared rank channel.</summary>
    /// <param name="State">Final geometry and declarations, including honoured hints.</param>
    /// <param name="Bounds">Authoritative endpoint and obstacle geometry.</param>
    private static void SearchJointChannels(GridLayoutState State,
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        var Layout = State.Copy;
        SearchJointNodeOrder(State, Bounds);
        int RemainingCandidates = FlowchartLayoutRules.RepairCandidateMoves;
        var BeforeFindings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
        var Eligible = Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2
            && Bounds.ContainsKey(Route.SourceId) && Bounds.ContainsKey(Route.TargetId)
            && State.Nodes.TryGetValue(Route.SourceId, out var Source) && IsRectangular(Source.Shape)
            && State.Nodes.TryGetValue(Route.TargetId, out var Target) && IsRectangular(Target.Shape)
            && FindContractEdge(State.Contract, Route)?.RoutingHint?.IsActive != true).ToArray();
        // Each orientation offers an entire channel at once. Neither an earlier
        // line nor its accidental track becomes a fixed obstacle for later lines.
        foreach (bool Horizontal in new[] { false, true })
        {
            var Groups = Eligible.GroupBy(Route => (
                Source: Horizontal ? Bounds[Route.SourceId].CenterX : Bounds[Route.SourceId].CenterY,
                Target: Horizontal ? Bounds[Route.TargetId].CenterX : Bounds[Route.TargetId].CenterY));
            foreach (var Group in Groups.Where(Group => Group.Count() > 1
                         && Group.Count() <= FlowchartLayoutRules.RepairChainLines))
            {
                if (RemainingCandidates <= 0) return;
                var Routes = Group.ToArray();
                if (!BeforeFindings.Any(Finding => Finding.Rule == "5.1"
                        && Routes.Any(Route => Finding.Subject.Split(" x ", StringSplitOptions.None).Contains(Route.EdgeId)))) continue;
                double Sign = Math.Sign(Group.Key.Target - Group.Key.Source);
                if (Sign == 0) continue;
                string Exit = Horizontal ? Sign > 0 ? "right" : "left" : Sign > 0 ? "top" : "bottom";
                string Entry = Horizontal ? Sign > 0 ? "left" : "right" : Sign > 0 ? "bottom" : "top";
                // A side used by an outside link needs a larger coupled cohort;
                // never silently displace a port whose route is outside this option.
                if (Layout.EdgeRoutes.Except(Routes).Any(Other =>
                        Routes.Any(Route => Other.SourceId == Route.SourceId && Other.ExitSide == Exit
                            || Other.TargetId == Route.SourceId && Other.EntrySide == Exit
                            || Other.SourceId == Route.TargetId && Other.ExitSide == Entry
                            || Other.TargetId == Route.TargetId && Other.EntrySide == Entry))) continue;
                var Saved = Routes.Select(Route => new JointChannelPath(Route,
                    Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Route.ExitSide, Route.EntrySide)).ToArray();
                var Normals = Routes.Select(Route => (Route, Route.ExitNormalX, Route.ExitNormalY,
                    Route.EntryNormalX, Route.EntryNormalY)).ToArray();
                double BestScore = double.PositiveInfinity;
                JointChannelPath[]? Best = null;
                int Examined = 0;
                foreach (bool ReverseSources in new[] { false, true })
                foreach (bool ReverseTargets in new[] { false, true })
                foreach (bool ReverseTracks in new[] { false, true })
                {
                    if (Examined++ >= FlowchartLayoutRules.RepairCandidateMoves || RemainingCandidates-- <= 0) break;
                    var Starts = Slots(true, ReverseSources);
                    var Ends = Slots(false, ReverseTargets);
                    var Bent = Routes.Where(Route => Math.Abs(Cross(Starts[Route]) - Cross(Ends[Route])) > 0.001)
                        .OrderBy(Route => Cross(Starts[Route]) + Cross(Ends[Route])).ThenBy(Route => Route.EdgeId, StringComparer.Ordinal).ToArray();
                    if (ReverseTracks) Array.Reverse(Bent);
                    double NearSource = Routes.Max(Route => Main(Starts[Route]) * Sign);
                    double NearTarget = Routes.Min(Route => Main(Ends[Route]) * Sign);
                    double Gap = NearTarget - NearSource;
                    if (Gap <= 0) continue;
                    var Candidate = Routes.Select(Route =>
                    {
                        var A = Starts[Route]; var B = Ends[Route];
                        int Track = Array.IndexOf(Bent, Route);
                        double Along = DiagramGridMetrics.RoundToGrid((NearSource + Gap * (Track + 1.0) / (Bent.Length + 1.0)) * Sign);
                        var Points = Track < 0 ? new List<(double X, double Y)> { A, B }
                            : Horizontal ? new() { A, (Along, A.Y), (Along, B.Y), B }
                            : new List<(double X, double Y)> { A, (A.X, Along), (B.X, Along), B };
                        return new JointChannelPath(Route, Points, Exit, Entry);
                    }).ToArray();
                    Install(Candidate);
                    // Reject an invalid changed path before checking every pair
                    // in a dense diagram. This uses the same exact outline and
                    // title-band checker; surviving options still receive the
                    // complete assignment check below.
                    var ChangedPaths = new FlowchartLayoutResult();
                    ChangedPaths.NodeBoundsMutable.AddRange(Layout.NodeBounds);
                    ChangedPaths.ClusterBoundsMutable.AddRange(Layout.ClusterBounds);
                    ChangedPaths.EdgeRoutesMutable.AddRange(Routes);
                    if (FlowchartRouteRuleChecker.Check(State.Contract, ChangedPaths)
                            .Any(Finding => Finding.Rule is "10" or "3.1" or "4.1")) continue;
                    var Findings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
                    bool Legal = !Findings.Any(Finding => Finding.Rule is "10" or "5.1"
                        && Routes.Any(Route => Finding.Subject == Route.EdgeId
                            || Finding.Subject.Split(" x ", StringSplitOptions.None).Contains(Route.EdgeId)))
                        && new[] { "3.1", "4.1", "4.2", "3.2/4.7", "3.3/5.2", "4.11", "5.9" }.All(Rule =>
                            Findings.Count(Finding => Finding.Rule == Rule) <= BeforeFindings.Count(Finding => Finding.Rule == Rule));
                    FlowchartRenderContext.ReportOperationCount("joint-channel-assignments", 1);
                    RouteRepairCandidateObserver?.Invoke("joint-channel",
                        $"lines={Routes.Length};horizontal={Horizontal};illegal={Findings.Count(Finding => Finding.Rule == "10")};shared={Findings.Count(Finding => Finding.Rule == "5.1")}", 0, Legal);
                    if (!Legal) continue;
                    var Score = FlowchartLayoutScore.Measure(State.Contract, Layout, IncludeArrangementCheck: false);
                    if (Score.OptimizationScore < BestScore)
                    {
                        BestScore = Score.OptimizationScore;
                        Best = Candidate;
                    }
                }
                Install(Best ?? Saved);
                if (Best != null) BeforeFindings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
                if (Best == null)
                    foreach (var Original in Normals)
                    {
                        Original.Route.ExitNormalX = Original.ExitNormalX; Original.Route.ExitNormalY = Original.ExitNormalY;
                        Original.Route.EntryNormalX = Original.EntryNormalX; Original.Route.EntryNormalY = Original.EntryNormalY;
                    }

                // <summary>Allocates every end on the selected side before any track is scored.</summary>
                // <param name="Source">Whether to allocate departures or arrivals.</param>
                // <param name="Reverse">Whether to offer the opposite slot order.</param>
                // <returns>One continuous k/(n+1) outline point per connector.</returns>
                Dictionary<FlowchartLayoutEdgeRoute, (double X, double Y)> Slots(bool Source, bool Reverse)
                {
                    var Result = new Dictionary<FlowchartLayoutEdgeRoute, (double X, double Y)>();
                    foreach (var NodeGroup in Routes.GroupBy(Route => Source ? Route.SourceId : Route.TargetId))
                    {
                        var Box = Bounds[NodeGroup.Key];
                        var Order = NodeGroup.OrderBy(Route => Horizontal
                            ? Bounds[Source ? Route.TargetId : Route.SourceId].CenterY
                            : Bounds[Source ? Route.TargetId : Route.SourceId].CenterX)
                            .ThenBy(Route => Route.EdgeId, StringComparer.Ordinal).ToArray();
                        if (Reverse) Array.Reverse(Order);
                        for (int Index = 0; Index < Order.Length; Index++)
                        {
                            double Fraction = (Index + 1.0) / (Order.Length + 1.0);
                            double Outward = Source ? Sign : -Sign;
                            Result[Order[Index]] = Horizontal
                                ? (Box.CenterX + Outward * Box.Width / 2, Box.CenterY - Box.Height / 2 + Fraction * Box.Height)
                                : (Box.CenterX - Box.Width / 2 + Fraction * Box.Width, Box.CenterY + Outward * Box.Height / 2);
                        }
                    }
                    return Result;
                }

                // <summary>Projects a point across the channel.</summary>
                // <param name="Point">A terminal in final model coordinates.</param>
                // <returns>The coordinate used to order slots and tracks.</returns>
                double Cross((double X, double Y) Point) => Horizontal ? Point.Y : Point.X;
                // <summary>Projects a point along the channel.</summary>
                // <param name="Point">A terminal in final model coordinates.</param>
                // <returns>The coordinate used to measure available track space.</returns>
                double Main((double X, double Y) Point) => Horizontal ? Point.X : Point.Y;
            }
        }
    }

    /// <summary>Offers bounded whole-grid permutations before fixing the sides of a small unhinted channel.</summary>
    /// <param name="State">The complete model whose node centres are the available grid positions.</param>
    /// <param name="Bounds">The obstacle index updated atomically with a winning assignment.</param>
    private static void SearchJointNodeOrder(GridLayoutState State, Dictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        var Layout = State.Copy;
        if (State.Contract.Groups.Count != 0 || Layout.NodeBounds.Count > FlowchartLayoutRules.RepairChainLines
            || Layout.EdgeRoutes.Count > FlowchartLayoutRules.RepairChainLines
            || State.Placed.Any(Node => Node.PlacementHintLocked || !IsRectangular(State.Nodes[Node.Id].Shape)
                || State.Nodes[Node.Id].PlacementHint?.IsActive == true)
            || State.Contract.Edges.Any(Edge => Edge.RoutingHint?.IsActive == true)) return;
        var Before = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
        if (!Before.Any(Finding => Finding.Rule == "5.1")) return;
        var SharedLinks = Before.Where(Finding => Finding.Rule == "5.1")
            .SelectMany(Finding => Finding.Subject.Split(" x ", StringSplitOptions.None)).ToHashSet(StringComparer.Ordinal);
        var InvolvedNodes = Layout.EdgeRoutes.Where(Route => SharedLinks.Contains(Route.EdgeId))
            .SelectMany(Route => new[] { Route.SourceId, Route.TargetId }).ToHashSet(StringComparer.Ordinal);
        var OriginalBounds = Layout.NodeBounds.OrderBy(Box => InvolvedNodes.Contains(Box.NodeId) ? 1 : 0).ToArray();
        var OriginalPaths = Layout.EdgeRoutes.Select(Route => new JointChannelPath(Route,
            Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Route.ExitSide, Route.EntrySide)).ToArray();
        var OriginalNormals = Layout.EdgeRoutes.Select(Route => (Route, Route.ExitNormalX, Route.ExitNormalY,
            Route.EntryNormalX, Route.EntryNormalY)).ToArray();
        var Positions = OriginalBounds.Select(Box => (Box.CenterX, Box.CenterY)).ToArray();
        int[] Order = Enumerable.Range(0, Positions.Length).ToArray();
        var BestBounds = OriginalBounds;
        var BestPaths = OriginalPaths;
        bool Improved = false;
        double BestScore = FlowchartLayoutScore.Measure(State.Contract, Layout, IncludeArrangementCheck: false).OptimizationScore;
        int Examined = 0;
        Visit(0);
        int Nudges = 0;
        foreach (int Position in Enumerable.Range(0, OriginalBounds.Length).Where(Position => InvolvedNodes.Contains(OriginalBounds[Position].NodeId)))
        foreach (var Direction in new[] { (X: 0, Y: 1), (X: 0, Y: -1), (X: 1, Y: 0), (X: -1, Y: 0) })
        {
            if (Nudges++ >= FlowchartLayoutRules.RepairCandidateMoves) break;
            var Candidate = Positions.ToArray();
            Candidate[Position] = (Candidate[Position].CenterX + Direction.X * DiagramGridMetrics.GRID_UNIT_PX,
                Candidate[Position].CenterY + Direction.Y * DiagramGridMetrics.GRID_UNIT_PX);
            Evaluate(Candidate);
        }
        Layout.NodeBoundsMutable.Clear(); Layout.NodeBoundsMutable.AddRange(BestBounds);
        foreach (var Box in BestBounds)
        {
            Bounds[Box.NodeId] = Box;
            State.Placed.Single(Node => Node.Id == Box.NodeId).SetPositionFromFrameFit(Box.CenterX, Box.CenterY);
        }
        Install(BestPaths);
        if (!Improved)
            foreach (var Saved in OriginalNormals)
            {
                Saved.Route.ExitNormalX = Saved.ExitNormalX; Saved.Route.ExitNormalY = Saved.ExitNormalY;
                Saved.Route.EntryNormalX = Saved.EntryNormalX; Saved.Route.EntryNormalY = Saved.EntryNormalY;
            }

        // A changed node order is an entire placement and routing option. The
        // existing chain depth bounds how many nodes may change position; the
        // candidate limit bounds complete options, rather than inner loop work.
        // <summary>Enumerates complete position permutations within the named move and chain limits.</summary>
        // <param name="Index">The next node whose existing grid position is assigned.</param>
        void Visit(int Index)
        {
            if (Examined >= FlowchartLayoutRules.RepairCandidateMoves) return;
            if (Index < Order.Length)
            {
                for (int Other = Index; Other < Order.Length; Other++)
                {
                    (Order[Index], Order[Other]) = (Order[Other], Order[Index]);
                    int Moved = Enumerable.Range(0, Index + 1).Count(Position => Order[Position] != Position);
                    if (Moved <= FlowchartLayoutRules.RepairChainDepth) Visit(Index + 1);
                    (Order[Index], Order[Other]) = (Order[Other], Order[Index]);
                    if (Examined >= FlowchartLayoutRules.RepairCandidateMoves) break;
                }
                return;
            }
            Examined++;
            Evaluate(Order.Select(Position => Positions[Position]).ToArray());
        }

        // <summary>Builds, validates and scores all connectors for one complete placement option.</summary>
        // <param name="Centres">One proposed whole-grid centre per node.</param>
        void Evaluate((double CenterX, double CenterY)[] Centres)
        {
            Layout.NodeBoundsMutable.Clear();
            for (int Position = 0; Position < Order.Length; Position++)
            {
                var Original = OriginalBounds[Position]; var Centre = Centres[Position];
                var Box = new FlowchartLayoutNodeBounds { NodeId = Original.NodeId, CenterX = Centre.CenterX,
                    CenterY = Centre.CenterY, Width = Original.Width, Height = Original.Height };
                Layout.NodeBoundsMutable.Add(Box); Bounds[Box.NodeId] = Box;
            }
            var Ends = new List<PortEnd>();
            foreach (var Route in Layout.EdgeRoutes)
            {
                var Source = AsBox(Bounds[Route.SourceId]); var Target = AsBox(Bounds[Route.TargetId]);
                string Exit = FacingSide(Source, Target);
                string Entry = FacingSide(Target, Source);
                Route.ExitSide = Exit; Route.EntrySide = Entry;
                Ends.Add(new() { Route = Route, IsSource = true, Side = Exit, Box = Source, Other = Target });
                Ends.Add(new() { Route = Route, IsSource = false, Side = Entry, Box = Target, Other = Source });
            }
            foreach (var Side in Ends.GroupBy(End => (End.Box.Id, End.Side)))
                ApplySlots(Side.OrderBy(End => End.Side is "left" or "right" ? -End.Other.CenterY : End.Other.CenterX).ToList(), State.Nodes);
            foreach (var Route in Layout.EdgeRoutes)
            {
                var Points = DiagramGridRouteBuilder.BuildRoute(Route.Points[0].X, Route.Points[0].Y,
                    Bounds[Route.SourceId], State.Nodes[Route.SourceId].Shape, Route.ExitSide,
                    Route.Points[^1].X, Route.Points[^1].Y, Bounds[Route.TargetId], State.Nodes[Route.TargetId].Shape,
                    Route.EntrySide, Bounds, Route.SourceId, Route.TargetId, State.Contract, State.Direction, State.Horizontal,
                    ExitNormal: (Route.ExitNormalX, Route.ExitNormalY), EntryNormal: (Route.EntryNormalX, Route.EntryNormalY));
                Route.Points.Clear();
                foreach (var Point in Points) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            }
            var Findings = FlowchartRouteRuleChecker.Check(State.Contract, Layout);
            bool Legal = !Findings.Any(Finding => Finding.Rule is "10" or "5.1")
                && new[] { "3.1", "4.1", "4.2", "3.2/4.7", "3.3/5.2", "4.11", "5.9" }.All(Rule =>
                    Findings.Count(Finding => Finding.Rule == Rule) <= Before.Count(Finding => Finding.Rule == Rule));
            FlowchartRenderContext.ReportOperationCount("joint-placement-assignments", 1);
            RouteRepairCandidateObserver?.Invoke("joint-placement",
                $"nodes={Centres.Length};illegal={Findings.Count(Finding => Finding.Rule == "10")};shared={Findings.Count(Finding => Finding.Rule == "5.1")}", 0, Legal);
            if (!Legal) return;
            var Score = FlowchartLayoutScore.Measure(State.Contract, Layout, IncludeArrangementCheck: false);
            if (Score.Overlaps != 0 || Score.OptimizationScore >= BestScore) return;
            BestScore = Score.OptimizationScore; Improved = true;
            BestBounds = Layout.NodeBounds.ToArray();
            BestPaths = Layout.EdgeRoutes.Select(Route => new JointChannelPath(Route,
                Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Route.ExitSide, Route.EntrySide)).ToArray();
        }

        // <summary>Exposes a final node's four sides to the shared port allocator.</summary>
        // <param name="Bounds">The proposed node rectangle.</param>
        // <returns>An upward-Y outline with unchanged node identity and dimensions.</returns>
        BoxRef AsBox(FlowchartLayoutNodeBounds Bounds) => new() { Id = Bounds.NodeId, CenterX = Bounds.CenterX,
            CenterY = Bounds.CenterY, Width = Bounds.Width, Height = Bounds.Height, Left = Bounds.CenterX - Bounds.Width / 2,
            Right = Bounds.CenterX + Bounds.Width / 2, Bottom = Bounds.CenterY - Bounds.Height / 2, Top = Bounds.CenterY + Bounds.Height / 2 };
    }

    /// <summary>Installs an entire channel option before either legality or cost is evaluated.</summary>
    /// <param name="Paths">All routes belonging to this atomic assignment.</param>
    private static void Install(IEnumerable<JointChannelPath> Paths)
    {
        foreach (var Path in Paths)
        {
            Path.Route.Points.Clear();
            foreach (var Point in Path.Points) Path.Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            Path.Route.ExitSide = Path.Exit; Path.Route.EntrySide = Path.Entry;
            Path.Route.ExitNormalX = Path.Exit == "right" ? 1 : Path.Exit == "left" ? -1 : 0;
            Path.Route.ExitNormalY = Path.Exit == "top" ? 1 : Path.Exit == "bottom" ? -1 : 0;
            Path.Route.EntryNormalX = Path.Entry == "right" ? 1 : Path.Entry == "left" ? -1 : 0;
            Path.Route.EntryNormalY = Path.Entry == "top" ? 1 : Path.Entry == "bottom" ? -1 : 0;
        }
    }
}

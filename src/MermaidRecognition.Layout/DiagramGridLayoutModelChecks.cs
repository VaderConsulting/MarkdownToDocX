using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Config;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    /// <summary>Final model-only route materialization and 1.2/4.2 diagnostics.</summary>
    private static class ModelRouteCompletionStage
    {
        private const double Tolerance = 0.5;

        public static void Apply(GridLayoutState State)
        {
            using var CostTiming = FlowchartCellCostTiming.BeginRouting();
            System.Diagnostics.Stopwatch? Profile = LayoutStageTimingObserver == null
                ? null : System.Diagnostics.Stopwatch.StartNew();
            void Mark(string Name)
            {
                if (Profile == null) return;
                Profile.Stop();
                LayoutStageTimingObserver?.Invoke("completion/" + Name, Profile.Elapsed.TotalMilliseconds);
                Profile.Restart();
            }
            void ProfileCall(string Name, Action Operation) =>
                MeasureLayoutStage("completion-call/" + Name, Operation);
            FlowchartLayoutResult Layout = State.Copy;
            // Earlier placement and frame stages can update the public bounds after
            // the mutable grid objects were created (notably during group refits).
            // Port allocation must use the exact geometry returned to the painter,
            // otherwise endpoints remain centred on the pre-refit node position.
            Dictionary<string, FlowchartLayoutNodeBounds> FinalNodeBounds = Layout.NodeBounds
                .GroupBy(Box => Box.NodeId, StringComparer.Ordinal)
                .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
            foreach (GridNode Node in State.Placed)
            {
                // Render bounds are a projection, never an input to a cell repair.
                if (Node.CellNode != null) continue;
                if (!FinalNodeBounds.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds NodeBound)) continue;
                Node.SetPositionFromFrameFit(NodeBound.CenterX, NodeBound.CenterY);
                Node.Width = (int)Math.Round(NodeBound.Width);
                Node.Height = (int)Math.Round(NodeBound.Height);
            }
            Dictionary<string, GridFrame> FinalFrames = State.Frames.ToDictionary(Frame => Frame.Id,
                StringComparer.Ordinal);
            foreach (FlowchartLayoutNodeBounds FrameBound in Layout.ClusterBounds)
            {
                if (!FinalFrames.TryGetValue(FrameBound.NodeId, out GridFrame? Frame)) continue;
                Frame.Left = (int)Math.Round(FrameBound.CenterX - FrameBound.Width / 2.0);
                Frame.Right = (int)Math.Round(FrameBound.CenterX + FrameBound.Width / 2.0);
                Frame.Bottom = (int)Math.Round(FrameBound.CenterY - FrameBound.Height / 2.0);
                Frame.Top = (int)Math.Round(FrameBound.CenterY + FrameBound.Height / 2.0);
            }
            // Repairs may choose a shorter side candidate. Restore the final symmetric
            // allocation as the source of truth, then rebuild paths from those exact ports.
            AssignPortSides(State.Contract, Layout, State.Placed, State.Frames,
                State.Nodes, State.Direction, State.SharedFanSources);
            DiagramGridRouteBuilder.RebuildAllRoutes(Layout, State.Placed, State.Nodes,
                State.Contract, State.Frames, State.Direction);
            DiagramGridRouteBuilder.RebuildDiagonalBackRoutes(Layout, State.Placed, State.Nodes,
                State.Contract, State.Frames, State.Direction);
            Dictionary<string, NodeContract> Nodes = State.Contract.Nodes
                .ToDictionary(Node => Node.Id, StringComparer.Ordinal);
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds
                .GroupBy(Box => Box.NodeId, StringComparer.Ordinal)
                .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
            HashSet<string> GroupIds = State.Contract.Groups.Select(Group => Group.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (FlowchartLayoutNodeBounds Cluster in Layout.ClusterBounds)
                if (GroupIds.Contains(Cluster.NodeId) && Cluster.Width > 0 && Cluster.Height > 0)
                    Bounds[Cluster.NodeId] = Cluster;
            FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(State.Contract);
            foreach (FlowchartSubgraphLayoutRect Rect in FlowchartSubgraphBoundsBuilder.BuildOrderedRects(
                         State.Contract, Bounds,
                         FlowchartSubgraphBoundsRequest.FromContract(State.Contract, RenderOptions)))
            {
                if (Bounds.TryGetValue(Rect.GroupId, out FlowchartLayoutNodeBounds Existing)
                    && Existing.Width > 0 && Existing.Height > 0) continue;
                FlowchartLayoutNodeBounds Fitted = GridAlignedFrame(Rect.GroupId,
                    Rect.Left, Rect.Bottom, Rect.Right, Rect.Top);
                Bounds[Rect.GroupId] = Fitted;
                int clusterIndex = Layout.ClusterBoundsMutable.FindIndex(Box => Box.NodeId == Rect.GroupId);
                if (clusterIndex >= 0) Layout.ClusterBoundsMutable[clusterIndex] = Fitted;
                else Layout.ClusterBoundsMutable.Add(Fitted);
            }
            // Back-edge placeholders are temporary anchors for port allocation. They are not
            // drawable routes; remove them before the final model route pass.
            Layout.EdgeRoutesMutable.RemoveAll(Route =>
                Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds Source)
                && Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds Target)
                && Route.Points.Count == 2
                && IsCentre(Route.Points[0], Source)
                && IsCentre(Route.Points[^1], Target));

            List<FlowchartResolvedGuide> Guides = FlowchartEdgeRouteResolver.ResolveAllVisibleGuidePoints(
                State.Contract, Layout, Bounds, Nodes, State.Direction, Layout.RenderContext);
            Dictionary<EdgeContract, FlowchartResolvedGuide> ByEdge = Guides.ToDictionary(Guide => Guide.Edge);
            foreach (EdgeContract Edge in State.Contract.Edges.Where(IsVisible))
            {
                if (!ByEdge.TryGetValue(Edge, out FlowchartResolvedGuide? Guide) || Guide.Points.Count < 2)
                {
                    State.Copy.RenderContext.LayoutStageViolations.Add(new FlowchartLayoutQualityFailure(
                        "MissingRoute", $"No model route could be built for '{Edge.SourceId}->{Edge.TargetId}'.",
                        [Edge.Id ?? Edge.SourceId, Edge.TargetId], null));
                    continue;
                }

                if (Layout.TryCopyDrawnRoutePoints(Edge, out _)) continue;
                if (!Bounds.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                    || !Bounds.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds)) continue;

                FlowchartLayoutEdgeRoute Route = new()
                {
                    EdgeId = Edge.Id ?? string.Empty,
                    SourceId = Edge.SourceId,
                    TargetId = Edge.TargetId,
                    ExitSide = ResolveSide(Guide.Points, SourceBounds, true),
                    EntrySide = ResolveSide(Guide.Points, TargetBounds, false)
                };
                foreach ((double X, double Y) in Guide.Points)
                    Route.Points.Add(new FlowchartLayoutPoint { X = X, Y = Y });
                Layout.EdgeRoutesMutable.Add(Route);
            }
            Mark("guide-resolution-and-materialization");

            // Any guide materialized above was absent during the first allocation pass.
            // Include those endpoints in the same per-side k/(n+1) allocation before the
            // final route set is validated and drawn.
            ProfileCall("port-allocation", () => AllocateFinalPortSlots(State.Contract, Layout, Bounds, State.Nodes));
            ProfileCall("route-rebuild", () =>
            {
                DiagramGridRouteBuilder.RebuildAllRoutes(Layout, State.Placed, State.Nodes,
                    State.Contract, State.Frames, State.Direction);
                DiagramGridRouteBuilder.RebuildDiagonalBackRoutes(Layout, State.Placed, State.Nodes,
                    State.Contract, State.Frames, State.Direction);
            });

            // Apply title-band constraints to the completed route set immediately before auditing it.
            ProfileCall("title-band-repair", () => RepairRoutesThroughFrameTitleBands(State, Bounds));
            // Title-band repair can grow a frame after the regular port stage. Keep
            // the route endpoints tied to the final rendered frame outline: frame
            // ports must be allocated on the same geometry the renderer exposes.
            ReallocatePortsAfterFrameGrowth(State, Bounds);
            // Port redistribution can change a hinted frame endpoint. Re-route
            // those affected paths under the same allocated sides so the final
            // frame slot remains symmetric without entering a title band.
            ProfileCall("title-band-repair", () => RepairRoutesThroughFrameTitleBands(State, Bounds, includeHinted: true));
            StraightenLegalAlignedRoutes(State, Bounds, Nodes);
            ProfileCall("title-band-repair", () => RepairRoutesThroughFrameTitleBands(State, Bounds, includeHinted: true));
            if (RouteRepairStage.ApplyFinalMoveBeforeDetour(State))
            {
                // The accepted placement change is authoritative. Reapply title
                // clearance and legal straightening before recording final checks.
                // A move can change both the per-side peer order and the symmetric
                // slot fraction. Reallocate from the final geometry before rebuilding
                // the routes, otherwise the saved endpoints remain on pre-move slots.
                AssignPortSides(State.Contract, Layout, State.Placed, State.Frames,
                    State.Nodes, State.Direction, State.SharedFanSources);
                DiagramGridRouteBuilder.RebuildAllRoutes(Layout, State.Placed, State.Nodes,
                    State.Contract, State.Frames, State.Direction);
                DiagramGridRouteBuilder.RebuildDiagonalBackRoutes(Layout, State.Placed,
                    State.Nodes, State.Contract, State.Frames, State.Direction);
                ProfileCall("title-band-repair", () => RepairRoutesThroughFrameTitleBands(State, Bounds, includeHinted: true));
                StraightenLegalAlignedRoutes(State, Bounds, Nodes);
            }
            Mark("initial-title-clearance-and-final-move");
            // Title-band growth and the final score-guarded placement move can
            // update node bounds after the last symmetric allocation. Seal the
            // title bands first, then allocate and route from the resulting final
            // bounds so the repair cannot leave stale endpoint slots behind.
            ProfileCall("title-band-repair", () => RepairRoutesThroughFrameTitleBands(State, Bounds, includeHinted: true));
            foreach (GridNode Node in State.Placed)
            {
                if (Node.CellNode != null) continue;
                if (!Bounds.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds NodeBound)) continue;
                Node.SetPositionFromFrameFit(NodeBound.CenterX, NodeBound.CenterY);
                Node.Width = (int)Math.Round(NodeBound.Width);
                Node.Height = (int)Math.Round(NodeBound.Height);
            }
            foreach (GridFrame Frame in State.Frames)
            {
                if (!Bounds.TryGetValue(Frame.Id, out FlowchartLayoutNodeBounds FrameBound)) continue;
                Frame.Left = (int)Math.Round(FrameBound.CenterX - FrameBound.Width / 2.0);
                Frame.Right = (int)Math.Round(FrameBound.CenterX + FrameBound.Width / 2.0);
                Frame.Bottom = (int)Math.Round(FrameBound.CenterY - FrameBound.Height / 2.0);
                Frame.Top = (int)Math.Round(FrameBound.CenterY + FrameBound.Height / 2.0);
            }
            ProfileCall("port-allocation", () => AllocateFinalPortSlots(State.Contract, Layout, Bounds, State.Nodes));
            ProfileCall("route-rebuild", () => RebuildRoutesFromFinalPorts(State));
            ProfileCall("title-band-repair", () => RepairRoutesThroughFrameTitleBands(State, Bounds, includeHinted: true));
            ProfileCall("diagonal-repair", () => RepairDiagonalBoxRoutes(State.Contract, Layout, Bounds, Nodes, State.Direction));
            // Final model completion can rebuild automatic back routes after the
            // coupled repair stage. Explicit routing hints are inputs to every
            // stage, so restore them as the last route writer against final ports.
            ProfileCall("hint-restoration", () => FlowchartRoutingHints.Apply(Layout, State.Contract, Bounds, Nodes, State.Direction,
                RepairAutomaticRoutesEnabled: false,
                ReportUnroutedRoutes: true,
                ClearUnroutedRoutes: false));
            ProfileCall("missing-route-side-search", () => TryBestPortSidesForUnroutedRoutes(State, Bounds, Nodes));
            ProfileCall("drop-impossible-hints", () => DropImpossibleRoutingHints(State, Bounds, Nodes));
            Mark("hint-restoration-and-unrouted-side-search");
            if (Layout.RenderContext.LayoutStageViolations.Any(failure => failure.Kind == "NoLegalRoute"))
            {
                // A failed rebuild is a 5.10 routing fault, not permission to keep
                // stale geometry. Give the existing score-guarded node/group move
                // repair one final opportunity against the newly allocated slots.
                if (RouteRepairStage.ApplyFinalMoveBeforeDetour(State))
                {
                    Bounds.Clear();
                    foreach (FlowchartLayoutNodeBounds box in Layout.NodeBounds.Concat(Layout.ClusterBounds)
                                 .GroupBy(box => box.NodeId, StringComparer.Ordinal).Select(group => group.First()))
                        Bounds[box.NodeId] = box;
                    AllocateFinalPortSlots(State.Contract, Layout, Bounds, Nodes);
                    RebuildRoutesFromFinalPorts(State);
                    FlowchartRoutingHints.Apply(Layout, State.Contract, Bounds, Nodes, State.Direction,
                        RepairAutomaticRoutesEnabled: false, ReportUnroutedRoutes: true,
                        ClearUnroutedRoutes: false);
                }

                foreach (FlowchartLayoutQualityFailure failure in Layout.RenderContext.LayoutStageViolations
                             .Where(failure => failure.Kind == "NoLegalRoute").ToArray())
                {
                    string edgeId = failure.InvolvedIds.FirstOrDefault() ?? string.Empty;
                    FlowchartLayoutEdgeRoute? route = Layout.EdgeRoutes.FirstOrDefault(candidate =>
                        string.Equals(candidate.EdgeId, edgeId, StringComparison.Ordinal));
                    route?.Points.Clear();
                    int index = Layout.RenderContext.LayoutStageViolations.IndexOf(failure);
                    if (index >= 0)
                        Layout.RenderContext.LayoutStageViolations[index] = new FlowchartLayoutQualityFailure(
                            failure.Kind,
                            failure.Description + " Rule 5.10 move/reroute repair found no score-improving legal candidate; route geometry was discarded.",
                            failure.InvolvedIds, failure.Justification);
                }
            }

            // 5.10 repairs and the final hint restoration may both alter a
            // terminal after its earlier joint allocation. Seal the model with
            // one last shared symmetric allocation and rebuild from those slots;
            // no later route writer may move an endpoint off its allocated slot.
            AllocateFinalPortSlots(State.Contract, Layout, Bounds, Nodes);
            RebuildRoutesFromFinalPorts(State);
            RepairRoutesThroughFrameTitleBands(State, Bounds, includeHinted: true);
            if (State.Placed.Any(Node => Node.CellNode != null))
                TryBestPortSidesForUnroutedRoutes(State, Bounds, Nodes);
            ReallocatePortsAfterFrameGrowth(State, Bounds);
            AllocateFinalPortSlots(State.Contract, Layout, Bounds, Nodes);
            HashSet<string> seenFrameStates = new(StringComparer.Ordinal);
            while (true)
            {
                bool geometryMoved = false;
                while (CountFrameOverlapPairs(State.Contract, Bounds).Length > 0)
                {
                    string state = string.Join("|", CountFrameOverlapPairs(State.Contract, Bounds)) + "|"
                        + string.Join(";", Bounds.Values.OrderBy(box => box.NodeId, StringComparer.Ordinal)
                            .Select(box => $"{box.NodeId}:{box.CenterX:0.##},{box.CenterY:0.##},{box.Width:0.##},{box.Height:0.##}"));
                    if (!seenFrameStates.Add(state) || !MoveOneOverlappingFrameGroup(State, Bounds))
                    {
                        string[] remaining = CountFrameOverlapPairs(State.Contract, Bounds).ToArray();
                        Layout.RenderContext.LayoutStageViolations.Add(new FlowchartLayoutQualityFailure(
                            "FrameOverlap", "Rule 7.8 could not clear unrelated group-frame overlap(s): "
                                + string.Join(", ", remaining), remaining, null));
                        break;
                    }
                    geometryMoved = true;
                }
                if (!geometryMoved || CountFrameOverlapPairs(State.Contract, Bounds).Length > 0) break;
                AllocateFinalPortSlots(State.Contract, Layout, Bounds, Nodes);
                RebuildRoutesFromFinalPorts(State);
                FlowchartRoutingHints.Apply(Layout, State.Contract, Bounds, Nodes, State.Direction,
                    RepairAutomaticRoutesEnabled: false, ReportUnroutedRoutes: true, ClearUnroutedRoutes: false);
                RepairRoutesThroughFrameTitleBands(State, Bounds, includeHinted: true);
                ReallocatePortsAfterFrameGrowth(State, Bounds);
                AllocateFinalPortSlots(State.Contract, Layout, Bounds, Nodes);
                if (CountFrameOverlapPairs(State.Contract, Bounds).Length == 0) break;
            }
            // Final completion and frame-overlap repair rebuild routes from the
            // symmetric slots, which can undo the earlier horizontal track order.
            // Re-run the crossing reducer only after those endpoint changes settle.
            if (State.Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft)
                ProfileCall("final-horizontal-track-search", () => FlowchartManyToManyRoutes.Apply(
                    Layout, State.Contract, Bounds, State.Direction));
            Mark("final-frame-overlap-and-route-seal");
            // Final allocation must not erase a legal lower-cost straight
            // candidate. Offer it against the final geometry and joint port
            // constraints; the unchanged score and legality checks decide.
            ProfileCall("final-port-candidates", () => FlowchartPortSideRouteRepair.Apply(Layout, State.Contract,
                IncludeTerminalCandidates: true));
            ProfileCall("node-interior-legality", () => RepairNodeInteriorRoutes(State, Bounds));
            ProfileCall("facing-side-assignment", () => RepairFinalFacingSides(State, Bounds));
            ProfileCall("aligned-straight-candidates", () => StraightenFinalAlignedRoutes(State));
            ProfileCall("joint-channel-search", () => SearchJointChannels(State, Bounds));
            ProfileCall("terminal-track-repair", () => RouteRepairStage.RepairTerminalPortTracks(State, Bounds));
            ProfileCall("distinct-track-seal", () => SealDistinctTracks(State, Bounds));
            ProfileCall("nonincreasing-track-search", () => FlowchartDistinctTrackRepair.Apply(State.Contract, State.Copy, State.Placed));
            ProfileCall("post-track-straight-candidates", () => StraightenFinalAlignedRoutes(State));
            ProfileCall("settled-facing-assignment", () => RepairFinalFacingSides(State, Bounds));
            ProfileCall("settled-side-placement", () => SearchJointNodeOrder(State, Bounds, IncludeSideFaults: true));
            ProfileCall("settled-crossing-placement", () => SearchJointNodeOrder(State, Bounds, IncludeCrossings: true));
            if (State.Placed.Any(Node => Node.CellNode != null))
                ProfileCall("cell-final-node-legality", () => RepairNodeInteriorRoutes(State, Bounds));
            // Keep route searches out of the diagnostics timer: ProfileCall
            // records nested timings without advancing the enclosing Mark.
            Mark("final-route-searches");
            if (State.Placed.Any(Node => Node.CellNode != null) && !Layout.RenderContext.IsEvaluatingCellRouteCandidate)
            {
                State.CellFinalRoutingReady = true;
                RouteRepairStage.RepairCellTerminalSpacing(State, Bounds);
            }
            if (State.Placed.Any(Node => Node.CellNode != null))
            {
                // A cell move competes with the completed retained fan, not
                // provisional crossings that nesting would remove for free.
                // Private move trials complete the same nesting before scoring.
                var FanResult = FlowchartStarFanNesting.Apply(State.Contract, Layout);
                FlowchartRenderContext.ReportOperationCount("star-nesting/kept", FanResult.Kept);
                FlowchartRenderContext.ReportOperationCount("star-nesting/rejected", FanResult.Rejected);
            }
            if (State.Placed.Any(Node => Node.CellNode != null) && !Layout.RenderContext.IsEvaluatingCellRouteCandidate)
            {
                if (RouteRepairStage.ApplyFinalMoveBeforeDetour(State))
                {
                    Bounds.Clear();
                    foreach (var Box in Layout.NodeBounds.Concat(Layout.ClusterBounds)) Bounds[Box.NodeId] = Box;
                }
            }
            if (State.Placed.Any(Node => Node.CellNode != null))
            {
                ProfileCall("terminal-track-repair", () => RouteRepairStage.RepairTerminalPortTracks(State, Bounds));
                if (!Layout.RenderContext.IsEvaluatingCellRouteCandidate)
                    ProfileCall("joint-crossing-pair-assignments", () => RouteRepairStage.RepairCrossingPairAssignments(State, Bounds));
            }
            // Painting consumes this cache directly. Guides resolved before
            // final allocation/repair would draw superseded hooks and terminals
            // even though the returned routing model contains the winning path.

            Layout.ResolvedEdgeGuides = State.Contract.Edges.Where(IsVisible).Select(Edge =>
            {
                // Every painted connector comes from one declared link and its
                // final model path. Retaining even an unchanged early guide can
                // resurrect a pre-repair hook or an obsolete obstacle crossing.
                if (!Layout.TryCopyDrawnRoutePoints(Edge, out var Points)) return null;
                return new FlowchartResolvedGuide(Edge, Points,
                    Points.Count == 2 ? FlowchartEdgeRouteKind.Straight : FlowchartEdgeRouteKind.Grid);
            }).OfType<FlowchartResolvedGuide>().ToList();
            using (FlowchartCellCostTiming.Begin("final-diagnostics"))
            {
                CollectFrameTitleBandDiagnostics(State.Contract, Layout, Bounds,
                    Layout.RenderContext.LayoutStageViolations);
                CollectGridAndAlignedPortDiagnostics(State.Contract, Layout, Bounds,
                    Layout.RenderContext.LayoutStageViolations);
                ClearResolvedHintDiagnostics(State.Contract, Layout, Bounds);
            }
            Mark("final-diagnostics");
        }

        /// <summary>Compares complete built side assignments against final geometry, including every displaced symmetric port.</summary>
        /// <param name="State">The authoritative final model and its declared hints.</param>
        /// <param name="Bounds">The settled node and frame obstacles used by both allocation and construction.</param>
        /// <param name="OnlyRoutes">Affected lines eligible for side repair, or null for the complete existing search.</param>
        private static void RepairFinalFacingSides(GridLayoutState State, Dictionary<string, FlowchartLayoutNodeBounds> Bounds,
            IReadOnlySet<FlowchartLayoutEdgeRoute>? OnlyRoutes = null)
        {
            if (OnlyRoutes is { Count: 0 }) return;
            int Examined = 0;
            var Before = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
            var Faulty = State.Copy.EdgeRoutes.Where(Route => (OnlyRoutes == null || OnlyRoutes.Contains(Route)) && Before.Any(Finding =>
                Finding.Subject == Route.EdgeId && Finding.Rule is "4.11" or "5.8")).ToArray();
            if (Faulty.Length == 0) return;
            // A route search depends on allocated terminals and obstacles, not
            // the other connectors being scored. Preserve obstacle order as
            // well as exact doubles so cached results retain search tie order.
            ulong GeometryHash = FlowchartLayoutGeometryHash.HashBounds(Bounds);
            if (State.FacingRouteGeometryHash != GeometryHash)
            {
                State.FacingRouteGeometryHash = GeometryHash;
                State.FacingRoutePaths.Clear();
            }
            var Plans = Faulty.SelectMany(Route => new[] { (Node: Route.SourceId, Source: true, Route),
                    (Node: Route.TargetId, Source: false, Route) })
                .GroupBy(End => (End.Node, End.Source))
                .Where(Group => Group.Count() > 1)
                .OrderByDescending(Group => Group.Count())
                .Select(Group => Group.Select(End => End.Route).Distinct().ToArray())
                .Concat(Faulty.Select(Route => new[] { Route })).ToList();
            if (Faulty.Length > 1 && Faulty.Length <= FlowchartLayoutRules.RepairChainLines) Plans.Insert(0, Faulty);
            // Rejected options restore the entire model. Accepted options
            // already have an authoritative score, so neither case requires
            // remeasuring the same baseline before the next bounded option.
            var BeforeScore = FlowchartLayoutScore.Measure(State.Contract, State.Copy, IncludeArrangementCheck: false);
            FlowchartRepairSearchPatience Patience = new FlowchartRepairSearchPatience(State.Copy.EdgeRoutes.Count, State.EnterpriseBands);
            // Changing one terminal may expose that the old opposite-side
            // fallback was only necessary for the superseded departure. Offer
            // the complete facing pair as well, under the same named budget.
            foreach (var Option in Plans.Select(Plan => (Plan, Both: false))
                         .Concat(Plans.Select(Plan => (Plan, Both: true))))
            {
                var Plan = Option.Plan;
                if (Examined >= FlowchartLayoutRules.RepairCandidateMoves) break;
                var Changing = Plan.Where(Route => Before.Any(Finding =>
                    Finding.Subject == Route.EdgeId && Finding.Rule is "4.11" or "5.8")
                    && FindContractEdge(State.Contract, Route)?.RoutingHint?.IsActive != true
                    && Bounds.ContainsKey(Route.SourceId) && Bounds.ContainsKey(Route.TargetId)).ToArray();
                if (Changing.Length == 0) continue;
                var Saved = State.Copy.EdgeRoutes.Select(Item => (Route: Item,
                    Points: Item.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                    Item.ExitSide, Item.EntrySide, Item.ExitNormalX, Item.ExitNormalY, Item.EntryNormalX, Item.EntryNormalY)).ToArray();
                Examined++;
                foreach (var Route in Changing)
                {
                    var Source = Bounds[Route.SourceId]; var Target = Bounds[Route.TargetId];
                    if (Option.Both || Before.Any(Finding => Finding.Subject == Route.EdgeId && Finding.Rule == "5.8")) Route.ExitSide = Facing(Source, Target);
                    if (Option.Both || Before.Any(Finding => Finding.Subject == Route.EdgeId && Finding.Rule == "4.11")) Route.EntrySide = Facing(Target, Source);
                }
                AllocateFinalPortSlots(State.Contract, State.Copy, Bounds, State.Nodes);
                // Recovery can supply an empty discarded path. Compare its
                // inventory before dereferencing terminals, and never attempt
                // an allocated-port reroute without both endpoints present.
                var Affected = Saved.Where(Item => Item.Route.ExitSide != Item.ExitSide || Item.Route.EntrySide != Item.EntrySide
                    || Item.Route.Points.Count != Item.Points.Length
                    || Item.Points.Length > 0 && ((Item.Route.Points[0].X, Item.Route.Points[0].Y) != Item.Points[0]
                        || (Item.Route.Points[^1].X, Item.Route.Points[^1].Y) != Item.Points[^1])).ToArray();
                bool Complete = Affected.Length <= FlowchartLayoutRules.RepairChainLines
                    && Affected.All(Item => Item.Route.Points.Count >= 2);
                if (Complete) foreach (var Item in Affected)
                {
                    var Changed = Item.Route;
                    if (!Bounds.TryGetValue(Changed.SourceId, out var From) || !Bounds.TryGetValue(Changed.TargetId, out var To)) { Complete = false; break; }
                    var Edge = FindContractEdge(State.Contract, Changed);
                    ulong PathKey = FlowchartLayoutGeometryHash.HashFacingPathKey(
                        Changed,
                        State.Nodes.GetValueOrDefault(Changed.SourceId)?.Shape,
                        State.Nodes.GetValueOrDefault(Changed.TargetId)?.Shape,
                        State.Direction,
                        State.Horizontal,
                        Edge?.RoutingHint?.Travel,
                        Edge?.RoutingHint?.IsActive);
                    if (!State.FacingRoutePaths.TryGetValue(PathKey, out List<(double X, double Y)>? Path))
                    {
                        FlowchartRenderContext.ReportOperationCount("facing-route-cache-misses", 1);
                        Path = DiagramGridRouteBuilder.BuildRoute(Changed.Points[0].X, Changed.Points[0].Y,
                            From, State.Nodes.GetValueOrDefault(Changed.SourceId)?.Shape, Changed.ExitSide,
                            Changed.Points[^1].X, Changed.Points[^1].Y, To,
                            State.Nodes.GetValueOrDefault(Changed.TargetId)?.Shape, Changed.EntrySide,
                            Bounds, Changed.SourceId, Changed.TargetId, State.Contract, State.Direction, State.Horizontal,
                            Travel: Edge?.RoutingHint?.Travel, RequireLegal: true,
                            ExitNormal: (Changed.ExitNormalX, Changed.ExitNormalY), EntryNormal: (Changed.EntryNormalX, Changed.EntryNormalY));
                        if (Path.Count < 2 && Edge?.RoutingHint is not { IsActive: true, Travel: not null })
                            Path = BuildAllocatedNormalFallback(State, Bounds, Changed);
                        State.FacingRoutePaths[PathKey] = Path;
                    }
                    else FlowchartRenderContext.ReportOperationCount("facing-route-cache-hits", 1);
                    if (Path.Count < 2) { Complete = false; break; }
                    Changed.Points.Clear();
                    foreach ((double X, double Y) Point in Path)
                    {
                        Changed.Points.Add(new FlowchartLayoutPoint { X = Point.X, Y = Point.Y });
                    }

                    State.Copy.BumpGeometryVersion();
                }
                var After = Complete ? FlowchartRouteRuleChecker.Check(State.Contract, State.Copy) : Before;
                if (Complete && !After.Any(Finding => Finding.Rule == "10")
                    && After.Count(Finding => Finding.Rule is "4.11" or "5.8") < Before.Count(Finding => Finding.Rule is "4.11" or "5.8")
                    && After.Count(Finding => Finding.Rule == "5.1") > Before.Count(Finding => Finding.Rule == "5.1"))
                {
                    // Independent construction can put displaced connectors on
                    // one track. Compare the completed side-and-track option
                    // before rejecting its side allocation. Node and frame
                    // geometry remain fixed throughout this transaction.
                    SearchJointChannels(State, Bounds, IncludeNodeOrder: false);
                    FlowchartDistinctTrackRepair.Apply(State.Contract, State.Copy, State.Placed);
                    Complete = Saved.Count(Item => Item.Route.ExitSide != Item.ExitSide || Item.Route.EntrySide != Item.EntrySide
                        || !Item.Route.Points.Select(Point => (Point.X, Point.Y)).SequenceEqual(Item.Points))
                        <= FlowchartLayoutRules.RepairChainLines;
                    After = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
                }
                var AfterScore = Complete ? FlowchartLayoutScore.Measure(State.Contract, State.Copy, IncludeArrangementCheck: false) : BeforeScore;
                // This compares the actual complete assignment, rather than a
                // cheap provisional family with different paths or neighbours.
                bool Keep = Complete
                    // The routing-fault precedence is part of legality before
                    // numeric crossing/bend cost; no score weight is changed.
                    && (After.Count(Finding => Finding.Rule is "4.11" or "5.8"), AfterScore.Cost)
                        .CompareTo((Before.Count(Finding => Finding.Rule is "4.11" or "5.8"), BeforeScore.Cost)) < 0
                    && new[] { "10", "3.1", "4.1", "3.2/4.7", "3.3/5.2", "4.2", "4.9", "4.11", "5.8", "5.1" }.All(Rule =>
                        After.Count(Finding => Finding.Rule == Rule) <= Before.Count(Finding => Finding.Rule == Rule));
                RouteRepairCandidateObserver?.Invoke("facing-assignment",
                    $"lines={Affected.Length};complete={Complete};sides={Before.Count(Finding => Finding.Rule is "4.11" or "5.8")}->{After.Count(Finding => Finding.Rule is "4.11" or "5.8")};new={string.Join(",", After.GroupBy(Finding => Finding.Rule).Where(Group => Group.Count() > Before.Count(Finding => Finding.Rule == Group.Key)).Select(Group => Group.Key + ":" + Group.Count()))}",
                    AfterScore.Cost, Keep);
                if (Keep)
                {
                    Patience.Reset();
                    Before = After;
                    BeforeScore = AfterScore;
                    continue;
                }

                if (Patience.RegisterNonImprovement())
                {
                    foreach (var Item in Saved)
                    {
                        Item.Route.ExitSide = Item.ExitSide; Item.Route.EntrySide = Item.EntrySide;
                        Item.Route.ExitNormalX = Item.ExitNormalX; Item.Route.ExitNormalY = Item.ExitNormalY;
                        Item.Route.EntryNormalX = Item.EntryNormalX; Item.Route.EntryNormalY = Item.EntryNormalY;
                        Item.Route.Points.Clear();
                        foreach (var Point in Item.Points) Item.Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                    }

                    break;
                }

                foreach (var Item in Saved)
                {
                    Item.Route.ExitSide = Item.ExitSide; Item.Route.EntrySide = Item.EntrySide;
                    Item.Route.ExitNormalX = Item.ExitNormalX; Item.Route.ExitNormalY = Item.ExitNormalY;
                    Item.Route.EntryNormalX = Item.EntryNormalX; Item.Route.EntryNormalY = Item.EntryNormalY;
                    Item.Route.Points.Clear();
                    foreach (var Point in Item.Points) Item.Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                }
            }
        }

        /// <summary>Returns the page-facing side in the current routing model.</summary>
        /// <param name="From">The endpoint owning the port.</param>
        /// <param name="To">Its peer endpoint.</param>
        /// <returns>The side facing the peer centre.</returns>
        internal static string Facing(FlowchartLayoutNodeBounds From, FlowchartLayoutNodeBounds To)
        {
            double Dx = To.CenterX - From.CenterX, Dy = To.CenterY - From.CenterY;
            double Tolerance = DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX;
            if (Math.Abs(Dx) <= Tolerance && Math.Abs(Dy) > Tolerance)
            {
                return Dy >= 0 ? "top" : "bottom";
            }

            if (Math.Abs(Dy) <= Tolerance && Math.Abs(Dx) > Tolerance)
            {
                return Dx >= 0 ? "right" : "left";
            }

            return Math.Abs(Dx) >= Math.Abs(Dy) ? Dx >= 0 ? "right" : "left" : Dy >= 0 ? "top" : "bottom";
        }

        /// <summary>Runs the existing settled route families only on the candidate dependency set.</summary>
        /// <param name="State">Applied cell candidate.</param>
        /// <param name="Bounds">Complete current obstacles.</param>
        /// <param name="Affected">Only routes allowed to change during this private trial.</param>
        internal static void RefineCellRouteCandidate(GridLayoutState State,
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds, IReadOnlySet<FlowchartLayoutEdgeRoute> Affected)
        {
            FlowchartPortSideRouteRepair.Apply(State.Copy, State.Contract, IncludeTerminalCandidates: true, OnlyRoutes: Affected);
            RepairFinalFacingSides(State, Bounds, Affected);
            StraightenFinalAlignedRoutes(State, Affected);
            FlowchartDistinctTrackRepair.Apply(State.Contract, State.Copy, State.Placed, Affected);
            StraightenFinalAlignedRoutes(State, Affected);
            RepairFinalFacingSides(State, Bounds, Affected);
            RouteRepairStage.RepairTerminalPortTracks(State, Bounds, Affected);
        }
        /// <summary>Preserves the legal zero-bend option after all writers have settled the allocated ports.</summary>
        /// <param name="State">The complete final model and declared links.</param>
        /// <param name="OnlyRoutes">Affected lines, or null to offer straightening to every aligned line.</param>
        private static void StraightenFinalAlignedRoutes(GridLayoutState State, IReadOnlySet<FlowchartLayoutEdgeRoute>? OnlyRoutes = null)
        {
            foreach (var Route in State.Copy.EdgeRoutes.Where(Route => Route.Points.Count > 2 && (OnlyRoutes == null || OnlyRoutes.Contains(Route))))
            {
                if (FindContractEdge(State.Contract, Route)?.RoutingHint?.IsActive == true) continue;
                var Start = Route.Points[0]; var End = Route.Points[^1];
                if (Math.Abs(Start.X - End.X) > 0.001 && Math.Abs(Start.Y - End.Y) > 0.001) continue;
                var Original = Route.Points.ToArray();
                var OriginalPath = Original.Select(Point => (Point.X, Point.Y)).ToArray();
                var OtherPaths = State.Copy.EdgeRoutes.Where(Other => Other != Route)
                    .Select(Other => Other.Points.Select(Point => (Point.X, Point.Y)).ToArray()).ToArray();
                int BeforeCrossings = OtherPaths.Sum(Other => FlowchartBranchRouteOrder.Intersections(OriginalPath, Other).Crossings);
                double BeforeOverlap = OtherPaths.Sum(Other => FlowchartBranchRouteOrder.Intersections(OriginalPath, Other).Overlap);
                Route.Points.Clear(); Route.Points.Add(Start); Route.Points.Add(End);
                var CandidateLayout = new FlowchartLayoutResult();
                CandidateLayout.NodeBoundsMutable.AddRange(State.Copy.NodeBounds);
                CandidateLayout.ClusterBoundsMutable.AddRange(State.Copy.ClusterBounds);
                CandidateLayout.EdgeRoutesMutable.Add(Route);
                var After = FlowchartRouteRuleChecker.Check(State.Contract, CandidateLayout);
                // Straightening is a single candidate, not a side/slot repair.
                // Keep every existing legality and crossing constraint intact.
                var Path = new[] { (Start.X, Start.Y), (End.X, End.Y) };
                bool Keep = !After.Any(Finding => Finding.Subject == Route.EdgeId && Finding.Rule is "10" or "3.1" or "4.1")
                    && OtherPaths.Sum(Other => FlowchartBranchRouteOrder.Intersections(Path, Other).Crossings) <= BeforeCrossings
                    && OtherPaths.Sum(Other => FlowchartBranchRouteOrder.Intersections(Path, Other).Overlap) <= BeforeOverlap;
                if (!Keep) { Route.Points.Clear(); Route.Points.AddRange(Original); }
            }
        }

        /// <summary>Offers an obstacle-avoiding fallback when a later route writer has introduced a node crossing.</summary>
        /// <param name="State">The final model, including authoritative allocated ports and hints.</param>
        /// <param name="Bounds">Node and frame bounds for endpoint and title-band checks.</param>
        private static void RepairNodeInteriorRoutes(GridLayoutState State, Dictionary<string, FlowchartLayoutNodeBounds> Bounds)
        {
            var Blocked = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy)
                .Where(Finding => Finding.Rule == "10" && Finding.Description.StartsWith("route passes through node", StringComparison.Ordinal)
                    || Finding.Rule == "3.1" && Finding.Description.StartsWith("terminal segment", StringComparison.Ordinal))
                .Select(Finding => Finding.Subject).ToHashSet();
            foreach (var Route in State.Copy.EdgeRoutes.Where(Route => Blocked.Contains(Route.EdgeId) && Route.Points.Count >= 2))
            {
                var Saved = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
                var Candidate = BuildAllocatedNormalFallback(State, Bounds, Route);
                if (Candidate.Count < 2) continue;
                Route.Points.Clear();
                foreach (var Point in Candidate) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                bool Legal = !FlowchartRouteRuleChecker.Check(State.Contract, State.Copy).Any(Finding =>
                    Finding.Subject == Route.EdgeId && Finding.Rule is "10" or "3.1" or "4.1");
                if (!Legal)
                {
                    Route.Points.Clear();
                    foreach (var Point in Saved) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                }
                else State.Copy.RenderContext.LayoutStageViolations.Add(new("RoutingFallback",
                    "An obstacle-avoiding final route replaced a connector that crossed a node.", [Route.EdgeId], null));
            }
        }

        /// <summary>Offers the same unpadded obstacle-avoiding candidate to legality repair and final side comparison.</summary>
        /// <param name="State">Declared groups and exact endpoint shape definitions.</param>
        /// <param name="Bounds">Final node bodies and title-band obstacles.</param>
        /// <param name="Route">The allocated terminals, sides and exact outline normals to preserve.</param>
        /// <returns>A complete candidate, or an empty path when no normal-preserving route exists; callers must check legality before accepting it.</returns>
        private static List<(double X, double Y)> BuildAllocatedNormalFallback(GridLayoutState State,
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds, FlowchartLayoutEdgeRoute Route) =>
            FlowchartAllocatedPortFallback.Build(State.Contract, Bounds, State.Nodes, Route,
                ExtendEnvelope: State.Placed.Any(Node => Node.CellNode != null),
                ExistingRoutes: FlowchartFallbackCorridorTracks.IsConstructionStage(State.Copy.RenderContext)
                    ? State.Copy.EdgeRoutes : null);

        /// <summary>Separates final shared runs and grows an undersized unhinted channel before retrying its tracks.</summary>
        /// <param name="State">The final placed model and immutable hint constraints.</param>
        /// <param name="Bounds">Endpoint and frame bounds, refreshed after accepted growth.</param>
        private static void SealDistinctTracks(GridLayoutState State, Dictionary<string, FlowchartLayoutNodeBounds> Bounds)
        {
            if (State.Placed.Any(Node => Node.CellNode != null))
            {
                RouteRepairStage.RepairCellChannelSpacing(State);
                foreach (var Box in State.Copy.NodeBounds.Concat(State.Copy.ClusterBounds)) Bounds[Box.NodeId] = Box;
                FlowchartTerminalTrackRepair.Apply(State.Contract, State.Copy, State.Placed, Bounds);
                for (int Pass = 0; Pass < 4 && FlowchartDistinctTrackRepair.Apply(State.Contract, State.Copy, State.Placed); Pass++)
                {
                }

                SeparateTracks(State.Copy, State.Placed, State.Contract);
                return;
            }
            while (true)
            {
                FlowchartTerminalTrackRepair.Apply(State.Contract, State.Copy, State.Placed, Bounds);
                for (int Pass = 0; Pass < 4 && FlowchartDistinctTrackRepair.Apply(State.Contract, State.Copy, State.Placed); Pass++)
                {
                }

                var Before = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
                int Shared = Before.Count(Finding => Finding.Rule == "5.1");
                int SideFaults = Before.Count(Finding => Finding.Rule is "4.11" or "5.8");
                if (Shared == 0 && SideFaults == 0) return;
                int OriginalFrameOverlaps = CountFrameOverlapPairs(State.Contract, Bounds).Length;
                var Positions = State.Placed.ToDictionary(Node => Node.Id, Node => (Node.X, Node.Y));
                var OriginalNodes = State.Copy.NodeBounds.ToArray();
                var OriginalClusters = State.Copy.ClusterBounds.ToArray();
                var OriginalFrames = State.Frames.Select(Frame => (Frame, Frame.Left, Frame.Right, Frame.Top, Frame.Bottom)).ToArray();
                var Routes = State.Copy.EdgeRoutes.Select(Route => (Route,
                    Points: Route.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                    Route.ExitSide, Route.EntrySide, Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY)).ToArray();
                var Violations = State.Copy.RenderContext.LayoutStageViolations.ToArray();
                bool Grew = false;
                foreach (var Route in State.Copy.EdgeRoutes.Where(Route => Route.Points.Count >= 2))
                {
                    bool WrongSide = Before.Any(Finding => Finding.Subject == Route.EdgeId && Finding.Rule is "4.11" or "5.8");
                    if (!WrongSide && !Before.Any(Finding => Finding.Rule == "5.1" && Finding.Subject
                            .Split(" x ", StringSplitOptions.None).Contains(Route.EdgeId))) continue;
                    if (!Bounds.TryGetValue(Route.SourceId, out var Source) || !Bounds.TryGetValue(Route.TargetId, out var Target)) continue;
                    string Exit = WrongSide ? Facing(Source, Target) : Route.ExitSide;
                    string Entry = WrongSide ? Facing(Target, Source) : Route.EntrySide;
                    bool Horizontal = Exit is "left" or "right";
                    if (Horizontal != (Entry is "left" or "right")) continue;
                    double A = Horizontal ? Source.CenterX : Source.CenterY;
                    double B = Horizontal ? Target.CenterX : Target.CenterY;
                    double HalfA = (Horizontal ? Source.Width : Source.Height) / 2;
                    double HalfB = (Horizontal ? Target.Width : Target.Height) / 2;
                    double Gap = Math.Abs(A - B) - HalfA - HalfB;
                    double Required = 4 * DiagramGridRouteBuilder.CORNER_RADIUS_PX + DiagramGridMetrics.GRID_UNIT_PX;
                    int ChannelLines = State.Copy.EdgeRoutes.Count(Other => Other.Points.Count >= 2
                        && Bounds.TryGetValue(Other.SourceId, out var OtherSource)
                        && Bounds.TryGetValue(Other.TargetId, out var OtherTarget)
                        && (WrongSide ? Facing(OtherSource, OtherTarget) == Exit && Facing(OtherTarget, OtherSource) == Entry
                            : Other.ExitSide == Exit && Other.EntrySide == Entry)
                        && Math.Abs((Horizontal ? OtherSource.CenterX : OtherSource.CenterY) - A) < 0.001
                        && Math.Abs((Horizontal ? OtherTarget.CenterX : OtherTarget.CenterY) - B) < 0.001);
                    if (ChannelLines > FlowchartLayoutRules.RepairChainLines) continue;
                    // Reserve both terminal clearances plus the complete track
                    // lattice. One grid step covers an unfavourable port phase.
                    double TrackPitch = Math.Max(DiagramGridMetrics.GRID_UNIT_PX, FlowchartLayoutRules.LineSpacing);
                    Required = Math.Max(Required, (ChannelLines - 1) * TrackPitch
                        + 4 * DiagramGridRouteBuilder.CORNER_RADIUS_PX + TrackPitch);
                    if (Gap <= 0 || Gap >= Required) continue;
                    double Boundary = A < B ? (A + HalfA + B - HalfB) / 2 : (B + HalfB + A - HalfA) / 2;
                    var Moving = State.Placed.Where(Node => (Horizontal ? Node.X : Node.Y) > Boundary).ToArray();
                    if (Moving.Any(Node => Node.PlacementHintLocked
                        || State.Nodes.GetValueOrDefault(Node.Id)?.PlacementHint?.IsActive == true
                        || RouteRepairStage.HasHintedContainingGroup(State.Contract, Node.Id))) continue;
                    int Growth = DiagramGridMetrics.CeilToGrid((int)Math.Ceiling(Required - Gap));
                    foreach (var Node in Moving) Node.SetPositionFromFrameFit(Node.X + (Horizontal ? Growth : 0), Node.Y + (Horizontal ? 0 : Growth));
                    State.Frames.Clear(); BuildFramesYUp(State.Contract, State.Placed, State.Frames);
                    WriteBack(State.Copy, State.Placed, State.Frames);
                    RefreshFinalBounds(State, Bounds);
                    AssignPortSides(State.Contract, State.Copy, State.Placed, State.Frames,
                        State.Nodes, State.Direction, State.SharedFanSources);
                    AllocateFinalPortSlots(State.Contract, State.Copy, Bounds, State.Nodes);
                    RebuildRoutesFromFinalPorts(State);
                    SearchJointChannels(State, Bounds);
                    Grew = true;
                    break;
                }
                SeparateTracks(State.Copy, State.Placed, State.Contract);
                var After = FlowchartRouteRuleChecker.Check(State.Contract, State.Copy);
                var OldFaults = Before.Where(Finding => Finding.Rule is "10" or "3.1" or "4.1")
                    .Select(Finding => (Finding.Rule, Finding.Subject)).ToHashSet();
                bool ClearedSharedTracks = Shared > 0 && !After.Any(Finding => Finding.Rule == "5.1");
                bool Improved = After.Count(Finding => Finding.Rule == "5.1") <= Shared
                    && (After.Count(Finding => Finding.Rule == "5.1") < Shared
                        || After.Count(Finding => Finding.Rule is "4.11" or "5.8") < SideFaults)
                    && After.Count(Finding => Finding.Rule == "4.2") <= Before.Count(Finding => Finding.Rule == "4.2")
                    && After.Count(Finding => Finding.Rule == "4.9") <= Before.Count(Finding => Finding.Rule == "4.9")
                    && After.Count(Finding => Finding.Rule == "4.11") <= Before.Count(Finding => Finding.Rule == "4.11")
                    && After.Count(Finding => Finding.Rule == "5.8") <= Before.Count(Finding => Finding.Rule == "5.8")
                    && CountFrameOverlapPairs(State.Contract, Bounds).Length <= OriginalFrameOverlaps
                    // A fully separated assignment repairs illegality and may
                    // cost more than its illegal baseline. Partial separation
                    // must not merely exchange shared tracks for new crossings.
                    && (ClearedSharedTracks
                        || After.All(Finding => Finding.Rule != "5.1")
                        || After.Count(Finding => Finding.Rule == "5.9") <= Before.Count(Finding => Finding.Rule == "5.9"))
                    && !After.Any(Finding => Finding.Rule is "10" or "3.1" or "4.1"
                        && !OldFaults.Contains((Finding.Rule, Finding.Subject)));
                RouteRepairCandidateObserver?.Invoke("track-separation",
                    $"grew={Grew};shared={Shared}->{After.Count(Finding => Finding.Rule == "5.1")};new={string.Join(";", After.Where(Finding => Finding.Rule is "10" or "3.1" or "4.1").Select(Finding => Finding.Description))};paths={string.Join("|", State.Copy.EdgeRoutes.Select(Route => Route.EdgeId + ":" + string.Join(";", Route.Points.Select(Point => $"{Point.X},{Point.Y}"))))}",
                    After.Count(Finding => Finding.Rule == "5.1"), Improved);
                if (Improved) continue;
                foreach (var Node in State.Placed)
                {
                    var Position = Positions[Node.Id]; Node.SetPositionFromFrameFit(Position.X, Position.Y);
                }
                // Rollback must restore the authoritative final model verbatim.
                // Re-fitting frames here crosses the placement/final-coordinate
                // boundary and can invert a container's height even when the
                // attempted repair did not move anything.
                State.Frames.Clear();
                foreach (var Saved in OriginalFrames)
                {
                    Saved.Frame.Left = Saved.Left; Saved.Frame.Right = Saved.Right;
                    Saved.Frame.Top = Saved.Top; Saved.Frame.Bottom = Saved.Bottom;
                    State.Frames.Add(Saved.Frame);
                }
                State.Copy.NodeBoundsMutable.Clear();
                State.Copy.NodeBoundsMutable.AddRange(OriginalNodes);
                State.Copy.ClusterBoundsMutable.Clear();
                State.Copy.ClusterBoundsMutable.AddRange(OriginalClusters);
                RefreshFinalBounds(State, Bounds);
                foreach (var Saved in Routes)
                {
                    Saved.Route.Points.Clear();
                    foreach (var Point in Saved.Points) Saved.Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                    Saved.Route.ExitNormalX = Saved.ExitNormalX; Saved.Route.ExitNormalY = Saved.ExitNormalY;
                    Saved.Route.EntryNormalX = Saved.EntryNormalX; Saved.Route.EntryNormalY = Saved.EntryNormalY;
                    Saved.Route.ExitSide = Saved.ExitSide; Saved.Route.EntrySide = Saved.EntrySide;
                }
                State.Copy.RenderContext.LayoutStageViolations.Clear();
                State.Copy.RenderContext.LayoutStageViolations.AddRange(Violations);
                return;
            }

        }

        /// <summary>Updates the shared obstacle index after moving a whole column or restoring it.</summary>
        /// <param name="State">The current node and frame geometry.</param>
        /// <param name="Bounds">The mutable index used by routing and title-band checks.</param>
        private static void RefreshFinalBounds(GridLayoutState State, Dictionary<string, FlowchartLayoutNodeBounds> Bounds)
        {
            Bounds.Clear();
            foreach (var Box in State.Copy.NodeBounds.Concat(State.Copy.ClusterBounds)) Bounds[Box.NodeId] = Box;
        }

        private static void ClearResolvedHintDiagnostics(DiagramContract contract,
            FlowchartLayoutResult layout, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds)
        {
            // This pass can only clear an existing rejection. Avoid the full
            // route, obstacle and pairwise crossing audit when none can change.
            EdgeContract[] unresolved = contract.Edges.Where(candidate =>
                candidate.RoutingHint is { IsActive: true, UnhonouredReason: not null }).ToArray();
            if (unresolved.Length == 0) return;
            HashSet<string> routeFaults = FlowchartRouteRuleChecker.Check(contract, layout)
                .Where(finding => finding.Rule is "3.1" or "3.2/4.7")
                .Select(finding => finding.Subject).ToHashSet(StringComparer.Ordinal);
            foreach (EdgeContract edge in unresolved)
            {
                string edgeId = edge.Id ?? edge.SourceId;
                if (routeFaults.Contains(edgeId)) continue;
                FlowchartLayoutEdgeRoute? route = layout.EdgeRoutes.FirstOrDefault(candidate =>
                    string.Equals(candidate.EdgeId, edgeId, StringComparison.Ordinal));
                if (route is not { Points.Count: >= 2 }
                    || (edge.RoutingHint!.Leave.HasValue
                        && route.ExitSide != HintSide(edge.RoutingHint.Leave.Value))
                    || (edge.RoutingHint.Enter.HasValue
                        && route.EntrySide != HintSide(edge.RoutingHint.Enter.Value))
                    || !FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(route.Points
                        .Select(point => (point.X, point.Y)).ToList())
                    || !DiagramGridRouteBuilder.PathIsValid(route.Points
                        .Select(point => (point.X, point.Y)).ToList(), bounds,
                        route.SourceId, route.TargetId, contract)) continue;
                edge.RoutingHint.UnhonouredReason = null;
            }
        }

        private static string[] CountFrameOverlapPairs(DiagramContract contract,
            IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds)
        {
            Dictionary<string, GroupContract> groups = contract.Groups.ToDictionary(group => group.Id,
                StringComparer.Ordinal);
            FlowchartLayoutNodeBounds[] frames = contract.Groups.Select(group => group.Id)
                .Where(bounds.ContainsKey).Select(id => bounds[id]).OrderBy(frame => frame.NodeId,
                    StringComparer.Ordinal).ToArray();
            bool Ancestor(string ancestor, string child)
            {
                string? parent = groups.TryGetValue(child, out GroupContract? group) ? group.ParentGroupId : null;
                while (parent != null)
                {
                    if (string.Equals(parent, ancestor, StringComparison.Ordinal)) return true;
                    parent = groups.TryGetValue(parent, out group) ? group.ParentGroupId : null;
                }
                return false;
            }

            List<string> overlaps = [];
            for (int first = 0; first < frames.Length; first++)
            for (int second = first + 1; second < frames.Length; second++)
            {
                FlowchartLayoutNodeBounds a = frames[first], b = frames[second];
                if (Ancestor(a.NodeId, b.NodeId) || Ancestor(b.NodeId, a.NodeId)) continue;
                double overlapX = Math.Min(a.CenterX + a.Width / 2, b.CenterX + b.Width / 2)
                    - Math.Max(a.CenterX - a.Width / 2, b.CenterX - b.Width / 2);
                double overlapY = Math.Min(a.CenterY + a.Height / 2, b.CenterY + b.Height / 2)
                    - Math.Max(a.CenterY - a.Height / 2, b.CenterY - b.Height / 2);
                if (overlapX > 0.5 && overlapY > 0.5) overlaps.Add($"{a.NodeId} x {b.NodeId}");
            }
            return overlaps.ToArray();
        }

        private static bool MoveOneOverlappingFrameGroup(GridLayoutState state,
            Dictionary<string, FlowchartLayoutNodeBounds> bounds)
        {
            if (state.Placed.Any(Node => Node.CellNode != null))
            {
                bool Changed = RouteRepairStage.RepairCellFrameOverlap(state);
                foreach (var Box in state.Copy.NodeBounds.Concat(state.Copy.ClusterBounds)) bounds[Box.NodeId] = Box;
                return Changed;
            }
            Dictionary<string, GroupContract> groups = state.Contract.Groups.ToDictionary(group => group.Id,
                StringComparer.Ordinal);
            bool Ancestor(string ancestor, string child)
            {
                string? parent = groups.TryGetValue(child, out GroupContract? group) ? group.ParentGroupId : null;
                while (parent != null)
                {
                    if (string.Equals(parent, ancestor, StringComparison.Ordinal)) return true;
                    parent = groups.TryGetValue(parent, out group) ? group.ParentGroupId : null;
                }
                return false;
            }

            string[] pairs = CountFrameOverlapPairs(state.Contract, bounds);
            if (pairs.Length == 0) return false;
            (int Priority, int Remaining, double Distance, string Group, double Dx, double Dy,
                Dictionary<string, (double X, double Y)> Positions,
                Dictionary<string, FlowchartLayoutNodeBounds> Bounds, double OverlapArea)? best = null;
            double baselineFrameOverlapArea = FrameOverlapArea(state.Contract, bounds);
            foreach (string pair in pairs)
            {
                string[] ids = pair.Split(" x ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (ids.Length != 2 || !bounds.TryGetValue(ids[0], out FlowchartLayoutNodeBounds? a)
                    || !bounds.TryGetValue(ids[1], out FlowchartLayoutNodeBounds? b)) continue;
                foreach ((string movingId, FlowchartLayoutNodeBounds moving,
                             string fixedId, FlowchartLayoutNodeBounds fixedFrame) in new[]
                         { (ids[0], a, ids[1], b), (ids[1], b, ids[0], a) })
                {
                    PlacementHintContract? hint = groups[movingId].PlacementHint is { IsActive: true } active
                        ? active : null;
                    bool directional = hint?.Kind is PlacementHintKind.RightOf or PlacementHintKind.LeftOf
                        or PlacementHintKind.Above or PlacementHintKind.Below;
                    (double Dx, double Dy)[] moves = directional
                        ? hint!.Kind switch
                        {
                            PlacementHintKind.RightOf => [(Math.Ceiling((fixedFrame.CenterX + fixedFrame.Width / 2
                                + DiagramGridMetrics.GRID_UNIT_PX - (moving.CenterX - moving.Width / 2))
                                / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX, 0)],
                            PlacementHintKind.LeftOf => [(-Math.Ceiling(((moving.CenterX + moving.Width / 2)
                                - (fixedFrame.CenterX - fixedFrame.Width / 2) + DiagramGridMetrics.GRID_UNIT_PX)
                                / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX, 0)],
                            PlacementHintKind.Above => [(0, Math.Ceiling((fixedFrame.CenterY + fixedFrame.Height / 2
                                + DiagramGridMetrics.GRID_UNIT_PX - (moving.CenterY - moving.Height / 2))
                                / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX)],
                            PlacementHintKind.Below => [(0, -Math.Ceiling(((moving.CenterY + moving.Height / 2)
                                - (fixedFrame.CenterY - fixedFrame.Height / 2) + DiagramGridMetrics.GRID_UNIT_PX)
                                / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX)],
                            _ => []
                        }
                        : [
                            (Math.Ceiling((fixedFrame.CenterX + fixedFrame.Width / 2 + DiagramGridMetrics.GRID_UNIT_PX
                                - (moving.CenterX - moving.Width / 2)) / DiagramGridMetrics.GRID_UNIT_PX)
                                * DiagramGridMetrics.GRID_UNIT_PX, 0),
                            (-Math.Ceiling((moving.CenterX + moving.Width / 2 - (fixedFrame.CenterX - fixedFrame.Width / 2)
                                + DiagramGridMetrics.GRID_UNIT_PX) / DiagramGridMetrics.GRID_UNIT_PX)
                                * DiagramGridMetrics.GRID_UNIT_PX, 0),
                            (0, Math.Ceiling((fixedFrame.CenterY + fixedFrame.Height / 2 + DiagramGridMetrics.GRID_UNIT_PX
                                - (moving.CenterY - moving.Height / 2)) / DiagramGridMetrics.GRID_UNIT_PX)
                                * DiagramGridMetrics.GRID_UNIT_PX),
                            (0, -Math.Ceiling((moving.CenterY + moving.Height / 2 - (fixedFrame.CenterY - fixedFrame.Height / 2)
                                + DiagramGridMetrics.GRID_UNIT_PX) / DiagramGridMetrics.GRID_UNIT_PX)
                                * DiagramGridMetrics.GRID_UNIT_PX)
                        ];
                    foreach ((double dx, double dy) in moves)
                    {
                        double trialDx = dx, trialDy = dy;
                        HashSet<string> movingGroups = groups.Keys.Where(id => id == movingId || Ancestor(movingId, id))
                            .ToHashSet(StringComparer.Ordinal);
                        HashSet<string> movingNodes = state.Placed.Where(node => movingGroups.Any(groupId =>
                                IsInsideGroup(state.Contract, node.Id, groupId)))
                            .Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
                        if (movingNodes.Count == 0) continue;
                        // Directional placement hints on the moving frame permit
                        // extra distance in the same direction. Every other active
                        // hint must remain internal to the rigidly moved unit.
                        PlacementHintContract[] externalHints = state.Contract.Nodes.Where(node => movingNodes.Contains(node.Id)
                                && node.PlacementHint?.IsActive == true
                                && !movingNodes.Contains(node.PlacementHint.PeerId ?? string.Empty)
                                && !movingGroups.Contains(node.PlacementHint.PeerId ?? string.Empty))
                            .Select(node => node.PlacementHint!).Concat(groups.Values.Where(group => movingGroups.Contains(group.Id)
                                && group.Id != movingId && group.PlacementHint?.IsActive == true
                                && !movingNodes.Contains(group.PlacementHint.PeerId ?? string.Empty)
                                && !movingGroups.Contains(group.PlacementHint.PeerId ?? string.Empty))
                            .Select(group => group.PlacementHint!)).ToArray();
                        if (externalHints.Any(externalHint => !MovesWithHint(trialDx, trialDy,
                                externalHint.Kind))) continue;
                        double required = trialDx != 0 ? trialDx : trialDy;
                        GridNode[] movingNodeList = state.Placed.Where(node => movingNodes.Contains(node.Id)).ToArray();
                        GridNode[] fixedNodeList = state.Placed.Where(node => !movingNodes.Contains(node.Id)).ToArray();
                        foreach (GridNode moved in movingNodeList)
                        foreach (GridNode other in fixedNodeList)
                        {
                            double movedX = moved.X + trialDx, movedY = moved.Y + trialDy;
                            bool overlapX = Math.Abs(movedX - other.X) * 2 < moved.Width + other.Width;
                            bool overlapY = Math.Abs(movedY - other.Y) * 2 < moved.Height + other.Height;
                            if (!overlapX || !overlapY) continue;
                            if (trialDx > 0)
                                required = Math.Max(required, other.X + other.Width / 2.0 + DiagramGridMetrics.GRID_UNIT_PX
                                    - (moved.X - moved.Width / 2.0));
                            else if (trialDx < 0)
                                required = Math.Min(required, other.X - other.Width / 2.0 - DiagramGridMetrics.GRID_UNIT_PX
                                    - (moved.X + moved.Width / 2.0));
                            else if (trialDy > 0)
                                required = Math.Max(required, other.Y + other.Height / 2.0 + DiagramGridMetrics.GRID_UNIT_PX
                                    - (moved.Y - moved.Height / 2.0));
                            else
                                required = Math.Min(required, other.Y - other.Height / 2.0 - DiagramGridMetrics.GRID_UNIT_PX
                                    - (moved.Y + moved.Height / 2.0));
                        }
                        double gridDelta = Math.Sign(required)
                            * DiagramGridMetrics.CeilToGrid(Math.Abs(required));
                        if (trialDx != 0) trialDx = gridDelta;
                        else if (trialDy != 0) trialDy = gridDelta;
                        bool nodeCollision = movingNodeList.Any(moved => fixedNodeList.Any(other =>
                            Math.Abs(moved.X + trialDx - other.X) * 2 < moved.Width + other.Width
                            && Math.Abs(moved.Y + trialDy - other.Y) * 2 < moved.Height + other.Height));
                        if (nodeCollision) continue;
                        Dictionary<string, FlowchartLayoutNodeBounds> trial = bounds.ToDictionary(pair => pair.Key,
                            pair => CopyBounds(pair.Value), StringComparer.Ordinal);
                        foreach (string groupId in movingGroups)
                            if (trial.TryGetValue(groupId, out FlowchartLayoutNodeBounds? frame))
                                trial[groupId] = new FlowchartLayoutNodeBounds
                                {
                                    NodeId = frame.NodeId, CenterX = frame.CenterX + trialDx, CenterY = frame.CenterY + trialDy,
                                    Width = frame.Width, Height = frame.Height
                                };
                        // A group move is rigid: descendants keep their width and height,
                        // so translated frame bounds exactly describe the candidate. Avoid
                        // cloning every node and rebuilding every frame for each trial.
                        int remaining = CountFrameOverlapPairs(state.Contract, trial).Length;
                        double overlapArea = FrameOverlapArea(state.Contract, trial);
                        if (overlapArea >= baselineFrameOverlapArea - 0.5) continue;
                        var candidate = (Priority: directional ? 0 : 1, Remaining: remaining,
                            Distance: Math.Abs(trialDx) + Math.Abs(trialDy), Group: movingId, Dx: trialDx, Dy: trialDy,
                            Positions: state.Placed.ToDictionary(node => node.Id,
                                node => movingNodes.Contains(node.Id) ? (node.X + trialDx, node.Y + trialDy)
                                    : (node.X, node.Y), StringComparer.Ordinal),
                            Bounds: trial, OverlapArea: overlapArea);
                        if (best == null || candidate.Priority < best.Value.Priority
                            || candidate.Priority == best.Value.Priority && (candidate.Remaining < best.Value.Remaining
                                || candidate.Remaining == best.Value.Remaining && (candidate.OverlapArea < best.Value.OverlapArea - 0.5
                                    || Math.Abs(candidate.OverlapArea - best.Value.OverlapArea) < 0.5
                                        && candidate.Distance < best.Value.Distance)))
                            best = candidate;
                    }
                }
            }
            if (best == null) return false;
            foreach (GridNode node in state.Placed)
                if (best.Value.Positions.TryGetValue(node.Id, out var position))
                    node.SetPositionFromFrameFit(position.X, position.Y);
            bounds.Clear();
            foreach (var item in best.Value.Bounds) bounds[item.Key] = item.Value;
            foreach (GridNode node in state.Placed)
                if (bounds.TryGetValue(node.Id, out FlowchartLayoutNodeBounds? nodeBound))
                    bounds[node.Id] = new FlowchartLayoutNodeBounds
                    {
                        NodeId = nodeBound.NodeId, CenterX = node.X, CenterY = node.Y,
                        Width = node.Width, Height = node.Height
                    };
            foreach (GridFrame frame in state.Frames)
                if (bounds.TryGetValue(frame.Id, out FlowchartLayoutNodeBounds? box))
                {
                    frame.Left = (int)Math.Round(box.CenterX - box.Width / 2);
                    frame.Right = (int)Math.Round(box.CenterX + box.Width / 2);
                    frame.Bottom = (int)Math.Round(box.CenterY - box.Height / 2);
                    frame.Top = (int)Math.Round(box.CenterY + box.Height / 2);
                }
            WriteBack(state.Copy, state.Placed, state.Frames);
            return true;

            static FlowchartLayoutNodeBounds CopyBounds(FlowchartLayoutNodeBounds box) => new()
            {
                NodeId = box.NodeId, CenterX = box.CenterX, CenterY = box.CenterY,
                Width = box.Width, Height = box.Height
            };

            static bool MovesWithHint(double dx, double dy, PlacementHintKind kind) => kind switch
            {
                PlacementHintKind.RightOf => dx > 0 && Math.Abs(dy) < 0.01,
                PlacementHintKind.LeftOf => dx < 0 && Math.Abs(dy) < 0.01,
                PlacementHintKind.Above => dy > 0 && Math.Abs(dx) < 0.01,
                PlacementHintKind.Below => dy < 0 && Math.Abs(dx) < 0.01,
                _ => false
            };
        }

        private static double FrameOverlapArea(DiagramContract contract,
            IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds)
        {
            FlowchartLayoutNodeBounds[] frames = contract.Groups.Select(group => group.Id)
                .Where(bounds.ContainsKey).Select(id => bounds[id]).ToArray();
            HashSet<string> groupIds = contract.Groups.Select(group => group.Id)
                .ToHashSet(StringComparer.Ordinal);
            Dictionary<string, string?> parents = contract.Groups.ToDictionary(group => group.Id,
                group => group.ParentGroupId, StringComparer.Ordinal);
            bool Ancestor(string ancestor, string child)
            {
                string? parent = parents.GetValueOrDefault(child);
                while (parent != null)
                {
                    if (parent == ancestor) return true;
                    parent = parents.GetValueOrDefault(parent);
                }
                return false;
            }
            double total = 0;
            for (int first = 0; first < frames.Length; first++)
            for (int second = first + 1; second < frames.Length; second++)
            {
                FlowchartLayoutNodeBounds a = frames[first], b = frames[second];
                if (!groupIds.Contains(a.NodeId) || !groupIds.Contains(b.NodeId)
                    || Ancestor(a.NodeId, b.NodeId) || Ancestor(b.NodeId, a.NodeId)) continue;
                double x = Math.Min(a.CenterX + a.Width / 2, b.CenterX + b.Width / 2)
                    - Math.Max(a.CenterX - a.Width / 2, b.CenterX - b.Width / 2);
                double y = Math.Min(a.CenterY + a.Height / 2, b.CenterY + b.Height / 2)
                    - Math.Max(a.CenterY - a.Height / 2, b.CenterY - b.Height / 2);
                if (x > 0.5 && y > 0.5) total += x * y;
            }
            return total;
        }

        private static int CountNodeOverlaps(IReadOnlyList<GridNode> nodes,
            IReadOnlyDictionary<string, (double X, double Y)>? positions = null)
        {
            int count = 0;
            for (int first = 0; first < nodes.Count; first++)
            for (int second = first + 1; second < nodes.Count; second++)
            {
                GridNode a = nodes[first], b = nodes[second];
                (double X, double Y) pa = positions?.GetValueOrDefault(a.Id, (a.X, a.Y)) ?? (a.X, a.Y);
                (double X, double Y) pb = positions?.GetValueOrDefault(b.Id, (b.X, b.Y)) ?? (b.X, b.Y);
                if (Math.Abs(pa.X - pb.X) * 2 < a.Width + b.Width
                    && Math.Abs(pa.Y - pb.Y) * 2 < a.Height + b.Height) count++;
            }
            return count;
        }

        private static void DropImpossibleRoutingHints(GridLayoutState State,
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds,
            Dictionary<string, NodeContract> Nodes)
        {
            FlowchartLayoutResult Layout = State.Copy;
            string[] failedEdges = Layout.RenderContext.LayoutStageViolations
                .Where(failure => failure.Kind == "NoLegalRoute" && failure.InvolvedIds.Count > 0)
                .Select(failure => failure.InvolvedIds[0]).Distinct(StringComparer.Ordinal).ToArray();
            foreach (string edgeId in failedEdges)
            {
                EdgeContract? edge = State.Contract.Edges.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, edgeId, StringComparison.Ordinal));
                RoutingHintContract? hint = edge?.RoutingHint;
                if (hint?.IsActive != true) continue;
                string failedHintDetail = Layout.RenderContext.LayoutStageViolations
                    .FirstOrDefault(failure => failure.Kind == "NoLegalRoute"
                        && failure.InvolvedIds.Contains(edgeId))?.Description
                    ?? "The hinted route could not be constructed from its allocated side slots.";

                var routeSnapshot = Layout.EdgeRoutes.Select(route => (Route: route,
                    Points: route.Points.Select(point => (point.X, point.Y)).ToArray(),
                    route.ExitSide, route.EntrySide, route.ExitNormalX, route.ExitNormalY,
                    route.EntryNormalX, route.EntryNormalY)).ToArray();
                var hintSnapshot = State.Contract.Edges.Where(candidate => candidate.RoutingHint != null)
                    .ToDictionary(candidate => candidate, candidate =>
                        (candidate.RoutingHint!.IsActive, candidate.RoutingHint.UnhonouredReason));
                FlowchartLayoutQualityFailure[] violationSnapshot = Layout.RenderContext.LayoutStageViolations.ToArray();
                hint.IsActive = false;
                hint.UnhonouredReason = "Rule 2.21: requested routing sides had no legal path; testing normal side allocation.";
                Layout.RenderContext.LayoutStageViolations.RemoveAll(failure =>
                    failure.Kind == "NoLegalRoute" && failure.InvolvedIds.Contains(edgeId));

                AssignPortSides(State.Contract, Layout, State.Placed, State.Frames,
                    Nodes, State.Direction, State.SharedFanSources);
                AllocateFinalPortSlots(State.Contract, Layout, Bounds, Nodes);
                RebuildRoutesFromFinalPorts(State);
                FlowchartRoutingHints.Apply(Layout, State.Contract, Bounds, Nodes, State.Direction,
                    RepairAutomaticRoutesEnabled: false, ReportUnroutedRoutes: true,
                    ClearUnroutedRoutes: false);
                // A routing hint may be dropped only if normal routing produces a
                // complete legal route. Re-run title-band repair as part of that
                // candidate, then reject the candidate if any reserved band remains.
                RepairRoutesThroughFrameTitleBands(State, Bounds, includeHinted: true);

                FlowchartLayoutEdgeRoute? route = Layout.EdgeRoutes.FirstOrDefault(candidate =>
                    string.Equals(candidate.EdgeId, edgeId, StringComparison.Ordinal));
                IReadOnlyList<FlowchartFrameTitleBandHelper.Band> bands =
                    FlowchartFrameTitleBandHelper.Get(State.Contract, Bounds);
                bool repaired = route?.Points.Count >= 2
                    && !Layout.RenderContext.LayoutStageViolations.Any(failure =>
                        failure.Kind == "NoLegalRoute" && failure.InvolvedIds.Contains(edgeId))
                    && !Layout.RenderContext.LayoutStageViolations.Any(failure =>
                        failure.Kind == "TitleBandRepairFailed" && failure.InvolvedIds.Contains(edgeId))
                    && FlowchartFrameTitleBandHelper.FindEnteredBandIds(
                        route.Points.Select(point => (point.X, point.Y)).ToArray(), bands,
                        route.SourceId, route.TargetId, State.Contract).Count == 0
                    && !FlowchartRouteRuleChecker.Check(State.Contract, Layout).Any(finding =>
                        finding.Rule == "3.1" && finding.Subject == edgeId);
                if (repaired)
                {
                hint.UnhonouredReason = "Rule 2.21: dropped the impossible side hint; normal side allocation succeeded. "
                    + failedHintDetail;
                    Layout.RenderContext.LayoutStageViolations.RemoveAll(failure =>
                        failure.Kind == "NoLegalRoute" && failure.InvolvedIds.Contains(edgeId));
                    Layout.RenderContext.LayoutStageViolations.Add(new FlowchartLayoutQualityFailure(
                        "RoutingHintDropped", $"Routing hint on '{edgeId}' was impossible and was dropped under rule 2.21; normal routing succeeded.",
                        [edgeId], hint.UnhonouredReason));
                    continue;
                }

                foreach ((EdgeContract candidate, (bool active, string? reason)) in hintSnapshot)
                {
                    candidate.RoutingHint!.IsActive = active;
                    candidate.RoutingHint.UnhonouredReason = reason;
                }
                foreach (var saved in routeSnapshot)
                {
                    saved.Route.ExitSide = saved.ExitSide;
                    saved.Route.EntrySide = saved.EntrySide;
                    saved.Route.ExitNormalX = saved.ExitNormalX;
                    saved.Route.ExitNormalY = saved.ExitNormalY;
                    saved.Route.EntryNormalX = saved.EntryNormalX;
                    saved.Route.EntryNormalY = saved.EntryNormalY;
                    saved.Route.Points.Clear();
                    foreach ((double x, double y) in saved.Points)
                        saved.Route.Points.Add(new FlowchartLayoutPoint { X = x, Y = y });
                }
                Layout.RenderContext.LayoutStageViolations.Clear();
                Layout.RenderContext.LayoutStageViolations.AddRange(violationSnapshot);
            }
        }

        private static void TryBestPortSidesForUnroutedRoutes(GridLayoutState State,
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds,
            Dictionary<string, NodeContract> Nodes)
        {
            string[] sides = ["top", "right", "bottom", "left"];
            foreach (string edgeId in State.Copy.RenderContext.LayoutStageViolations
                         .Where(item => item.Kind == "NoLegalRoute" && item.InvolvedIds.Count > 0)
                         .Select(item => item.InvolvedIds[0]).Distinct(StringComparer.Ordinal).ToArray())
            {
                using var SideSearchTiming = FlowchartCellCostTiming.Begin("unrouted-side-search/" + edgeId);
                FlowchartLayoutEdgeRoute? route = State.Copy.EdgeRoutes.FirstOrDefault(item => item.EdgeId == edgeId);
                EdgeContract? edge = State.Contract.Edges.FirstOrDefault(item => item.Id == edgeId);
                if (route == null) continue;
                FlowchartLayoutScore.Result initial = FlowchartLayoutScore.Measure(
                    State.Contract, State.Copy, IncludeArrangementCheck: false);
                bool HasActiveSideHint = edge?.RoutingHint?.IsActive == true;
                double OriginalTrackLength = State.Placed.Any(Node => Node.CellNode != null)
                    ? CellSharedTrackLength(State.Copy) : 0;
                List<FlowchartRouteRuleChecker.Finding> initialFindings =
                    FlowchartRouteRuleChecker.Check(State.Contract, State.Copy).ToList();
                initialFindings.Add(new FlowchartRouteRuleChecker.Finding("NoLegalRoute", edgeId, "unrouted"));
                var original = SaveRoutes(State.Copy);
                FlowchartLayoutQualityFailure[] originalViolations = State.Copy.RenderContext.LayoutStageViolations.ToArray();
                (FlowchartLayoutScore.ComparisonCost Score, object Routes, FlowchartLayoutQualityFailure[] Violations)? best = null;
                List<string> attempts = [];

                string[] exits = edge?.RoutingHint is { IsActive: true, Leave: not null } hint
                    ? [DiagramGridLayout.HintSide(hint.Leave.Value)] : sides;
                string[] entries = edge?.RoutingHint is { IsActive: true, Enter: not null } entryHint
                    ? [DiagramGridLayout.HintSide(entryHint.Enter.Value)] : sides;
                foreach (string exit in exits)
                foreach (string entry in entries)
                {
                    if (exit == route.ExitSide && entry == route.EntrySide) continue;
                    RestoreRoutes(State.Copy, original);
                    State.Copy.RenderContext.LayoutStageViolations.Clear();
                    State.Copy.RenderContext.LayoutStageViolations.AddRange(originalViolations);
                    route.ExitSide = exit;
                    route.EntrySide = entry;
                    route.ExitNormalX = route.ExitNormalY = route.EntryNormalX = route.EntryNormalY = 0;
                    AllocateFinalPortSlots(State.Contract, State.Copy, Bounds, Nodes);
                    RebuildRoutesFromFinalPorts(State);
                    FlowchartRoutingHints.Apply(State.Copy, State.Contract, Bounds, Nodes, State.Direction,
                        RepairAutomaticRoutesEnabled: false, ReportUnroutedRoutes: true,
                        ClearUnroutedRoutes: false);
                    if (State.Placed.Any(Node => Node.CellNode != null))
                    {
                        RepairNodeInteriorRoutes(State, Bounds);
                        FlowchartDistinctTrackRepair.Apply(State.Contract, State.Copy, State.Placed);
                    }
                    bool recovered = !State.Copy.RenderContext.LayoutStageViolations.Any(item =>
                        item.Kind == "NoLegalRoute" && item.InvolvedIds.Contains(edgeId))
                        && route.Points.Count >= 2
                        && !FlowchartRouteRuleChecker.Check(State.Contract, State.Copy).Any(item =>
                            item.Rule == "3.1" && item.Subject == edgeId)
                        && (State.Placed.Any(Node => Node.CellNode != null)
                            ? !FlowchartFrameTitleBandHelper.RouteEntersAny(route.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                                FlowchartFrameTitleBandHelper.Get(State.Contract, Bounds), route.SourceId, route.TargetId, State.Contract)
                            : !HasTitleBandRoute(State, Bounds))
                        && !HasIllegalRouteGeometry(State.Contract, State.Copy, edgeId);
                    FlowchartLayoutScore.Result score = FlowchartLayoutScore.Measure(
                        State.Contract, State.Copy, IncludeArrangementCheck: false);
                    attempts.Add($"{exit}/{entry}:recovered={recovered}:score={score.Cost:0.##}:newFindings={candidateFindingsSummary(State.Contract, State.Copy, initialFindings)}");
                    if (!recovered) continue;
                    List<FlowchartRouteRuleChecker.Finding> candidateFindings =
                        FlowchartRouteRuleChecker.Check(State.Contract, State.Copy).ToList();
                    bool CurrentRouteWasMissing = originalViolations.Any(item =>
                        item.Kind == "NoLegalRoute" && item.InvolvedIds.Contains(edgeId));
                    bool CellLegalityRepair = CurrentRouteWasMissing && State.Placed.Any(Node => Node.CellNode != null);
                    bool TracksNotWorse = !CellLegalityRepair || candidateFindings.Count(Finding => Finding.Rule == "5.1")
                        <= initialFindings.Count(Finding => Finding.Rule == "5.1")
                        && CellSharedTrackLength(State.Copy) <= OriginalTrackLength + 0.001;
                    bool NewDisallowedFinding = candidateFindings.Any(candidate =>
                        !initialFindings.Any(previous => previous.Rule == candidate.Rule
                            && previous.Subject == candidate.Subject)
                        && !(CellLegalityRepair && candidate.Rule is "5.9" or "5.8")
                        && !(CellLegalityRepair && TracksNotWorse && candidate.Rule == "5.1")
                        && (!HasActiveSideHint || candidate.Rule != "5.9"));
                    // An illegal option cannot beat a legal one on quality
                    // cost. Compare legal recoveries with one another while
                    // rejecting new legality/slot faults and any increase in
                    // shared-track pair count or shared length.
                    if (CellLegalityRepair)
                        RouteRepairCandidateObserver?.Invoke("cell-side-legality",
                            $"{edgeId}:{exit}/{entry};hints={initial.HintsHonoured}->{score.HintsHonoured};overlaps={initial.Overlaps}->{score.Overlaps};tracks={initialFindings.Count(Finding => Finding.Rule == "5.1")}->{candidateFindings.Count(Finding => Finding.Rule == "5.1")};newDisallowed={NewDisallowedFinding};newRules={string.Join(",", candidateFindings.Where(Finding => !initialFindings.Any(Previous => Previous.Rule == Finding.Rule && Previous.Subject == Finding.Subject)).Select(Finding => Finding.Rule + ":" + Finding.Subject))}",
                            score.Cost, !NewDisallowedFinding && score.HintsHonoured >= initial.HintsHonoured && score.Overlaps <= initial.Overlaps);
                    if (score.HintsHonoured < initial.HintsHonoured || score.Overlaps > initial.Overlaps
                        || NewDisallowedFinding
                        || !CellLegalityRepair && !HasActiveSideHint && score.Cost >= initial.Cost - 0.001
                        || !CellLegalityRepair && HasActiveSideHint && !CurrentRouteWasMissing
                            && score.Cost >= initial.Cost - 0.001) continue;
                    if (best == null || score.Cost < best.Value.Score)
                        best = (score.Cost, SaveRoutes(State.Copy),
                            State.Copy.RenderContext.LayoutStageViolations.ToArray());
                }

                RestoreRoutes(State.Copy, original);
                State.Copy.RenderContext.LayoutStageViolations.Clear();
                State.Copy.RenderContext.LayoutStageViolations.AddRange(originalViolations);
                if (best == null)
                {
                    int violationIndex = State.Copy.RenderContext.LayoutStageViolations.FindIndex(item =>
                        item.Kind == "NoLegalRoute" && item.InvolvedIds.Contains(edgeId));
                    if (violationIndex >= 0)
                    {
                        FlowchartLayoutQualityFailure old = State.Copy.RenderContext.LayoutStageViolations[violationIndex];
                        State.Copy.RenderContext.LayoutStageViolations[violationIndex] = new FlowchartLayoutQualityFailure(
                            old.Kind, old.Description + " Side candidates: " + string.Join(";", attempts),
                            old.InvolvedIds, old.Justification);
                    }
                    continue;
                }
                RestoreRoutes(State.Copy, best.Value.Routes);
                State.Copy.RenderContext.LayoutStageViolations.Clear();
                State.Copy.RenderContext.LayoutStageViolations.AddRange(best.Value.Violations);
                State.Copy.RenderContext.LayoutStageViolations.RemoveAll(item =>
                    item.Kind == "NoLegalRoute" && item.InvolvedIds.Contains(edgeId));
            }

            static object SaveRoutes(FlowchartLayoutResult layout) => layout.EdgeRoutes.Select(route =>
                (Route: route, Points: route.Points.Select(point => (point.X, point.Y)).ToArray(),
                    route.ExitSide, route.EntrySide, route.ExitNormalX, route.ExitNormalY,
                    route.EntryNormalX, route.EntryNormalY)).ToArray();

            static void RestoreRoutes(FlowchartLayoutResult layout, object snapshot)
            {
                var routes = ((FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points,
                    string ExitSide, string EntrySide, double ExitNormalX, double ExitNormalY,
                    double EntryNormalX, double EntryNormalY)[])snapshot;
                foreach (var saved in routes)
                {
                    saved.Route.ExitSide = saved.ExitSide; saved.Route.EntrySide = saved.EntrySide;
                    saved.Route.ExitNormalX = saved.ExitNormalX; saved.Route.ExitNormalY = saved.ExitNormalY;
                    saved.Route.EntryNormalX = saved.EntryNormalX; saved.Route.EntryNormalY = saved.EntryNormalY;
                    saved.Route.Points.Clear();
                    foreach ((double x, double y) in saved.Points)
                        saved.Route.Points.Add(new FlowchartLayoutPoint { X = x, Y = y });
                }
            }

            static bool HasTitleBandRoute(GridLayoutState state,
                IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds)
            {
                IReadOnlyList<FlowchartFrameTitleBandHelper.Band> bands =
                    FlowchartFrameTitleBandHelper.Get(state.Contract, bounds);
                return state.Copy.EdgeRoutes.Any(route => route.Points.Count >= 2
                    && FlowchartFrameTitleBandHelper.FindEnteredBandIds(
                        route.Points.Select(point => (point.X, point.Y)).ToArray(), bands,
                        route.SourceId, route.TargetId, state.Contract).Count > 0);
            }

            static bool HasIllegalRouteGeometry(DiagramContract contract, FlowchartLayoutResult layout, string edgeId) =>
                FlowchartRouteRuleChecker.Check(contract, layout).Any(item => item.Subject == edgeId
                    && item.Rule is "3.1" or "4.1");

            static string candidateFindingsSummary(DiagramContract contract, FlowchartLayoutResult layout,
                IReadOnlyCollection<FlowchartRouteRuleChecker.Finding> previous)
            {
                HashSet<string> prior = previous.Select(item => item.Rule + "|" + item.Subject).ToHashSet(StringComparer.Ordinal);
                return string.Join(",", FlowchartRouteRuleChecker.Check(contract, layout)
                    .Where(item => !prior.Contains(item.Rule + "|" + item.Subject))
                    .Select(item => item.Rule + ":" + item.Subject).Distinct().Take(8));
            }
        }

        /// <summary>Measures complete shared segment length so legality recovery cannot trade old shared pairs for more overlap.</summary>
        /// <param name="Layout">Complete candidate route assignment.</param>
        /// <returns>Sum of positive-length intersections between distinct declared routes.</returns>
        private static double CellSharedTrackLength(FlowchartLayoutResult Layout)
        {
            var Paths = Layout.EdgeRoutes.Select(Route => Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()).ToArray();
            double Length = 0;
            for (int First = 0; First < Paths.Length; First++)
            for (int Second = First + 1; Second < Paths.Length; Second++)
                Length += FlowchartBranchRouteOrder.Intersections(Paths[First], Paths[Second]).Overlap;
            return Length;
        }

        /// <summary>Allocates symmetric slots using the entire side inventory, optionally limiting writes to affected endpoint boxes.</summary>
        /// <param name="contract">Original memberships and shape definitions.</param>
        /// <param name="layout">Current stored route inventory.</param>
        /// <param name="bounds">Exact current node and frame projections.</param>
        /// <param name="nodes">Renderer geometry-provider definitions.</param>
        /// <param name="OnlyBoxes">Boxes whose port inventory can change, or null for all boxes.</param>
        internal static void AllocateFinalPortSlots(DiagramContract contract, FlowchartLayoutResult layout,
            IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds,
            Dictionary<string, NodeContract> nodes, IReadOnlySet<string>? OnlyBoxes = null)
        {
            HashSet<string> frameIds = contract.Groups.Select(group => group.Id)
                .ToHashSet(StringComparer.Ordinal);
            Dictionary<string, List<PortEnd>> groups = new(StringComparer.Ordinal);
            foreach (FlowchartLayoutEdgeRoute route in layout.EdgeRoutes)
            {
                if (route.Points.Count < 2 || string.IsNullOrEmpty(route.ExitSide)
                    || string.IsNullOrEmpty(route.EntrySide)
                    || !bounds.TryGetValue(route.SourceId, out FlowchartLayoutNodeBounds sourceBounds)
                    || !bounds.TryGetValue(route.TargetId, out FlowchartLayoutNodeBounds targetBounds)) continue;
                EdgeContract? edge = FindContractEdge(contract, route);
                if (edge != null && string.Equals(edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)) continue;
                bool hinted = edge?.RoutingHint?.IsActive == true;
                BoxRef source = FinalBox(route.SourceId, sourceBounds);
                BoxRef target = FinalBox(route.TargetId, targetBounds);
                Add(source, target, route, true, route.ExitSide, hinted);
                Add(target, source, route, false, route.EntrySide, hinted);
            }

            foreach (List<PortEnd> ends in groups.Values)
            {
                if (OnlyBoxes != null && !OnlyBoxes.Contains(ends[0].Box.Id)) continue;
                if (!ends[0].Box.IsFrame
                    && nodes.TryGetValue(ends[0].Box.Id, out NodeContract? node)
                    && FlowchartDagrePresentation.IsDiamondShape(node.Shape))
                {
                    ApplySlots(ends, nodes, contract);
                    continue;
                }
                bool CorridorNesting = NeedsCorridorPortNesting(ends, layout.RenderContext);
                ends.Sort((left, right) =>
                {
                    if (CorridorNesting)
                        return CompareCorridorPortEnds(left, right);
                    int hintOrder = right.IsHinted.CompareTo(left.IsHinted);
                    if (hintOrder != 0) return hintOrder;
                    bool vertical = left.Side is "left" or "right";
                    int PeerOrder = vertical ? right.Other.CenterY.CompareTo(left.Other.CenterY)
                        : left.Other.CenterX.CompareTo(right.Other.CenterX);
                    if (PeerOrder == 0 && CorridorSlotIsOnFlatSide(left) && CorridorSlotIsOnFlatSide(right)
                        && FlowchartFallbackCorridorTracks.HasReservedCapacity(layout.RenderContext))
                        return CompareExistingCorridorSlots(left, right);
                    return vertical ? right.Other.CenterY.CompareTo(left.Other.CenterY)
                        : left.Other.CenterX.CompareTo(right.Other.CenterX);
                });
                ApplySlots(ends, nodes, contract);
                CorridorPortOrderObserver?.Invoke("final|" + layout.RenderContext.ActiveLayoutStage + "|" + ends[0].Box.Id + "|" + ends[0].Side + "|nested=" + CorridorNesting + "|"
                    + string.Join(";", ends.Select(End => End.Route.EdgeId + ":" + (End.IsSource ? End.Route.Points[0].Y : End.Route.Points[^1].Y))));
            }

            void Add(BoxRef box, BoxRef other, FlowchartLayoutEdgeRoute route, bool source,
                string side, bool hinted)
            {
                bool diamond = !box.IsFrame && nodes.TryGetValue(box.Id, out NodeContract? node)
                    && FlowchartDagrePresentation.IsDiamondShape(node.Shape);
                string key = box.Id + (diamond ? "|diamond" : "|" + side);
                if (!groups.TryGetValue(key, out List<PortEnd>? ends)) groups[key] = ends = [];
                ends.Add(new PortEnd
                {
                    Route = route,
                    IsSource = source,
                    IsHinted = hinted,
                    Side = side,
                    Box = box,
                    Other = other
                });
            }

            BoxRef FinalBox(string id, FlowchartLayoutNodeBounds box) => new()
            {
                Id = id,
                CenterX = box.CenterX,
                CenterY = box.CenterY,
                Width = box.Width,
                Height = box.Height,
                Left = box.CenterX - box.Width / 2.0,
                Right = box.CenterX + box.Width / 2.0,
                Top = box.CenterY + box.Height / 2.0,
                Bottom = box.CenterY - box.Height / 2.0,
                IsFrame = frameIds.Contains(id)
            };
        }

        private static void RepairDiagonalBoxRoutes(DiagramContract contract, FlowchartLayoutResult layout,
            IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds,
            IReadOnlyDictionary<string, NodeContract> nodes, FlowchartDirection direction)
        {
            bool horizontal = direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
            foreach (FlowchartLayoutEdgeRoute route in layout.EdgeRoutes)
            {
                if (route.Points.Count < 2) continue;
                List<(double X, double Y)> points = route.Points.Select(point => (point.X, point.Y)).ToList();
                if (FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(points)) continue;
                EdgeContract? edge = FindContractEdge(contract, route);
                bool hasRoutingHint = edge?.RoutingHint?.IsActive == true;
                nodes.TryGetValue(route.SourceId, out NodeContract? sourceNode);
                nodes.TryGetValue(route.TargetId, out NodeContract? targetNode);
                bool sourceIsFrame = contract.Groups.Any(group => group.Id == route.SourceId);
                bool targetIsFrame = contract.Groups.Any(group => group.Id == route.TargetId);
                if ((!sourceIsFrame && !IsRectangular(sourceNode?.Shape))
                    || (!targetIsFrame && !IsRectangular(targetNode?.Shape))) continue;
                if (!bounds.TryGetValue(route.SourceId, out FlowchartLayoutNodeBounds source)
                    || !bounds.TryGetValue(route.TargetId, out FlowchartLayoutNodeBounds target)) continue;
                List<(double X, double Y)> candidate = DiagramGridRouteBuilder.BuildRoute(
                    points[0].X, points[0].Y, source, sourceNode?.Shape, route.ExitSide,
                    points[^1].X, points[^1].Y, target, targetNode?.Shape, route.EntrySide,
                    bounds, route.SourceId, route.TargetId, contract, direction, horizontal,
                    Travel: hasRoutingHint ? edge!.RoutingHint!.Travel : null,
                    ExitNormal: (route.ExitNormalX, route.ExitNormalY),
                    EntryNormal: (route.EntryNormalX, route.EntryNormalY));
                if (candidate.Count < 2 || !FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(candidate)
                    || Math.Abs(candidate[0].X - points[0].X) > Tolerance
                    || Math.Abs(candidate[0].Y - points[0].Y) > Tolerance
                    || Math.Abs(candidate[^1].X - points[^1].X) > Tolerance
                    || Math.Abs(candidate[^1].Y - points[^1].Y) > Tolerance
                    || FlowchartFrameTitleBandHelper.RouteEntersAny(candidate,
                        FlowchartFrameTitleBandHelper.Get(contract, bounds), route.SourceId,
                        route.TargetId, contract)) continue;
                StateCorrectionLog(layout, route.EdgeId);
                route.Points.Clear();
                foreach ((double X, double Y) in candidate)
                    route.Points.Add(new FlowchartLayoutPoint { X = X, Y = Y });
            }
        }

        private static void StateCorrectionLog(FlowchartLayoutResult layout, string edgeId)
        {
            OrthogonalRouteCleanupObserver?.Invoke(edgeId);
            layout.RenderContext.LayoutStageTrace.Add("orthogonal-route-cleanup:" + edgeId);
        }

        private static void RebuildRoutesFromFinalPorts(GridLayoutState state)
        {
            DiagramGridRouteBuilder.RebuildAllRoutes(state.Copy, state.Placed, state.Nodes,
                state.Contract, state.Frames, state.Direction);
            DiagramGridRouteBuilder.RebuildDiagonalBackRoutes(state.Copy, state.Placed,
                state.Nodes, state.Contract, state.Frames, state.Direction);
            RebuildOrdinaryFanOutRoutes(state.Contract, state.Copy, state.Placed, state.Frames,
                state.Nodes, state.Direction);
            RebuildHintedOrthogonalRoutes(state.Contract, state.Copy,
                BuildPlacedBounds(state.Placed, state.Frames), state.Nodes, state.Direction);
        }

        private static void RebuildHintedOrthogonalRoutes(DiagramContract contract,
            FlowchartLayoutResult layout, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds,
            Dictionary<string, NodeContract> nodes, FlowchartDirection direction)
        {
            bool horizontal = direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
            IReadOnlyList<FlowchartFrameTitleBandHelper.Band> titleBands =
                FlowchartFrameTitleBandHelper.Get(contract, bounds);
            foreach (FlowchartLayoutEdgeRoute route in layout.EdgeRoutes)
            {
                EdgeContract? edge = FindContractEdge(contract, route);
                if (edge?.RoutingHint?.IsActive != true || route.Points.Count < 2
                    || !bounds.TryGetValue(route.SourceId, out FlowchartLayoutNodeBounds source)
                    || !bounds.TryGetValue(route.TargetId, out FlowchartLayoutNodeBounds target)) continue;
                List<(double X, double Y)> existing = route.Points.Select(point => (point.X, point.Y)).ToList();
                if (FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(existing)) continue;
                nodes.TryGetValue(route.SourceId, out NodeContract? sourceNode);
                nodes.TryGetValue(route.TargetId, out NodeContract? targetNode);
                List<(double X, double Y)> candidate = DiagramGridRouteBuilder.BuildRoute(
                    existing[0].X, existing[0].Y, source, sourceNode?.Shape, route.ExitSide,
                    existing[^1].X, existing[^1].Y, target, targetNode?.Shape, route.EntrySide,
                    bounds, route.SourceId, route.TargetId, contract, direction, horizontal,
                    Travel: edge.RoutingHint.Travel,
                    ExitNormal: (route.ExitNormalX, route.ExitNormalY),
                    EntryNormal: (route.EntryNormalX, route.EntryNormalY));
                if (candidate.Count < 2 || !FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(candidate)
                    || Math.Abs(candidate[0].X - existing[0].X) > Tolerance
                    || Math.Abs(candidate[0].Y - existing[0].Y) > Tolerance
                    || Math.Abs(candidate[^1].X - existing[^1].X) > Tolerance
                    || Math.Abs(candidate[^1].Y - existing[^1].Y) > Tolerance
                    || FlowchartFrameTitleBandHelper.RouteEntersAny(candidate, titleBands,
                        route.SourceId, route.TargetId, contract)) continue;
                route.Points.Clear();
                foreach ((double x, double y) in candidate)
                    route.Points.Add(new FlowchartLayoutPoint { X = x, Y = y });
            }
        }

        private static void StraightenLegalAlignedRoutes(GridLayoutState state,
            IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds,
            IReadOnlyDictionary<string, NodeContract> nodes)
        {
            IReadOnlyList<FlowchartFrameTitleBandHelper.Band> titleBands =
                FlowchartFrameTitleBandHelper.Get(state.Contract, bounds);
            Dictionary<string, FlowchartLayoutNodeBounds> nodeBodies = state.Copy.NodeBounds
                .GroupBy(box => box.NodeId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (FlowchartLayoutEdgeRoute route in state.Copy.EdgeRoutes)
            {
                if (route.Points.Count < 3) continue;
                EdgeContract? edge = FindContractEdge(state.Contract, route);
                if (edge?.RoutingHint?.IsActive == true) continue;
                if (!bounds.TryGetValue(route.SourceId, out FlowchartLayoutNodeBounds source)
                    || !bounds.TryGetValue(route.TargetId, out FlowchartLayoutNodeBounds target)) continue;
                if (nodes.TryGetValue(route.SourceId, out NodeContract? sourceNode)
                    && !IsRectangular(sourceNode.Shape)) continue;
                if (nodes.TryGetValue(route.TargetId, out NodeContract? targetNode)
                    && !IsRectangular(targetNode.Shape)) continue;
                FlowchartLayoutPoint start = route.Points[0], end = route.Points[^1];
                bool horizontal = Math.Abs(start.Y - end.Y) <= Tolerance;
                bool vertical = Math.Abs(start.X - end.X) <= Tolerance;
                if (!horizontal && !vertical) continue;
                bool sidesMatch = horizontal
                    ? (end.X >= start.X && route.ExitSide == "right" && route.EntrySide == "left")
                        || (end.X < start.X && route.ExitSide == "left" && route.EntrySide == "right")
                    : (end.Y >= start.Y && route.ExitSide == "top" && route.EntrySide == "bottom")
                        || (end.Y < start.Y && route.ExitSide == "bottom" && route.EntrySide == "top");
                if (!sidesMatch) continue;
                if (FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                        start.X, start.Y, end.X, end.Y, nodeBodies,
                        route.SourceId, route.TargetId)
                    || FlowchartFrameTitleBandHelper.RouteEntersAny(
                        [(start.X, start.Y), (end.X, end.Y)], titleBands,
                        route.SourceId, route.TargetId, state.Contract)) continue;
                route.Points.Clear();
                route.Points.Add(new FlowchartLayoutPoint { X = start.X, Y = start.Y });
                route.Points.Add(new FlowchartLayoutPoint { X = end.X, Y = end.Y });
            }
        }

        private static void ReallocatePortsAfterFrameGrowth(GridLayoutState state,
            Dictionary<string, FlowchartLayoutNodeBounds> bounds)
        {
            Dictionary<string, GridFrame> frames = state.Frames.ToDictionary(frame => frame.Id,
                StringComparer.Ordinal);
            HashSet<string> updatedFrames = new(StringComparer.Ordinal);
            foreach (FlowchartLayoutNodeBounds cluster in state.Copy.ClusterBounds)
            {
                if (!frames.TryGetValue(cluster.NodeId, out GridFrame? frame)) continue;
                int left = (int)Math.Round(cluster.CenterX - cluster.Width / 2.0);
                int right = (int)Math.Round(cluster.CenterX + cluster.Width / 2.0);
                int bottom = (int)Math.Round(cluster.CenterY - cluster.Height / 2.0);
                int top = (int)Math.Round(cluster.CenterY + cluster.Height / 2.0);
                if (frame.Left == left && frame.Right == right && frame.Bottom == bottom && frame.Top == top)
                    continue;
                frame.Left = left;
                frame.Right = right;
                frame.Bottom = bottom;
                frame.Top = top;
                updatedFrames.Add(cluster.NodeId);
            }
            if (updatedFrames.Count == 0) return;

            Dictionary<string, GridNode> nodes = IndexPlaced(state.Placed);
            Dictionary<string, List<PortEnd>> byFrameSide = new(StringComparer.Ordinal);
            Dictionary<FlowchartLayoutEdgeRoute, (double X, double Y, double X2, double Y2)> oldPorts = [];
            foreach (FlowchartLayoutEdgeRoute route in state.Copy.EdgeRoutes)
            {
                if (route.Points.Count < 2
                    || !TryBox(route.SourceId, nodes, frames, out BoxRef source)
                    || !TryBox(route.TargetId, nodes, frames, out BoxRef target)) continue;
                AddFrameEndpoint(route, source, target, true, route.ExitSide);
                AddFrameEndpoint(route, target, source, false, route.EntrySide);
            }

            foreach (List<PortEnd> ends in byFrameSide.Values)
            {
                ends.Sort((left, right) =>
                {
                    int hintOrder = right.IsHinted.CompareTo(left.IsHinted);
                    if (hintOrder != 0) return hintOrder;
                    bool verticalSide = left.Side is "left" or "right";
                    return verticalSide
                        ? right.Other.CenterY.CompareTo(left.Other.CenterY)
                        : left.Other.CenterX.CompareTo(right.Other.CenterX);
                });
                ApplySlots(ends, state.Nodes);
            }

            HashSet<FlowchartLayoutEdgeRoute> changedRoutes = [];
            foreach (FlowchartLayoutEdgeRoute route in oldPorts.Keys)
            {
                (double X, double Y, double X2, double Y2) old = oldPorts[route];
                if (old.X != route.Points[0].X || old.Y != route.Points[0].Y
                    || old.X2 != route.Points[^1].X || old.Y2 != route.Points[^1].Y)
                    changedRoutes.Add(route);
            }
            if (changedRoutes.Count > 0)
                DiagramGridRouteBuilder.RebuildAllRoutes(state.Copy, state.Placed, state.Nodes,
                    state.Contract, state.Frames, state.Direction, changedRoutes);

            void AddFrameEndpoint(FlowchartLayoutEdgeRoute route, BoxRef box, BoxRef other,
                bool source, string? side)
            {
                if (!box.IsFrame || !updatedFrames.Contains(box.Id) || string.IsNullOrEmpty(side)) return;
                string key = box.Id + "|" + side;
                if (!byFrameSide.TryGetValue(key, out List<PortEnd>? ends))
                    byFrameSide[key] = ends = [];
                oldPorts.TryAdd(route, (route.Points[0].X, route.Points[0].Y,
                    route.Points[^1].X, route.Points[^1].Y));
                ends.Add(new PortEnd
                {
                    Route = route,
                    IsSource = source,
                    IsHinted = FindContractEdge(state.Contract, route)?.RoutingHint?.IsActive == true,
                    Side = side,
                    Box = box,
                    Other = other
                });
            }
        }

        /// <summary>Retains the legacy unbound title-clearance fallback; cell-backed frames can never be resized through this writer.</summary>
        /// <param name="state">Completed routing model and authoritative ownership.</param>
        /// <param name="bounds">Legacy mutable drawing index; untouched for bound nodes.</param>
        /// <param name="groupId">Requested frame and any enclosing ancestors.</param>
        /// <returns>Whether legacy geometry grew; always false for a cell-backed diagram.</returns>
        private static bool GrowFrameTitleClearance(GridLayoutState state,
            Dictionary<string, FlowchartLayoutNodeBounds> bounds, string groupId)
        {
            // Cell frames are derived caches. Late routing must repair its path;
            // any future spatial growth must be a complete scored cell assignment.
            if (state.Placed.Any(Node => Node.CellNode != null)) return false;
            if (!bounds.TryGetValue(groupId, out FlowchartLayoutNodeBounds frame))
                return false;
            double oldTop = frame.CenterY + frame.Height / 2.0;
            double bottom = frame.CenterY - frame.Height / 2.0;
            double newTop = DiagramGridMetrics.CeilToGrid(oldTop) + DiagramGridMetrics.GRID_UNIT_PX;
            FlowchartLayoutNodeBounds expanded = new()
            {
                NodeId = groupId,
                CenterX = frame.CenterX,
                CenterY = (bottom + newTop) / 2.0,
                Width = frame.Width,
                Height = newTop - bottom
            };
            bounds[groupId] = expanded;
            int clusterIndex = state.Copy.ClusterBoundsMutable.FindIndex(box => box.NodeId == groupId);
            if (clusterIndex >= 0) state.Copy.ClusterBoundsMutable[clusterIndex] = expanded;
            else state.Copy.ClusterBoundsMutable.Add(expanded);

            // Keep ancestors enclosing the expanded child. Each adjustment is
            // also a whole-grid increase and does not change any member position.
            GroupContract? group = state.Contract.Groups.FirstOrDefault(item => item.Id == groupId);
            while (!string.IsNullOrEmpty(group?.ParentGroupId))
            {
                string parentId = group.ParentGroupId;
                if (!bounds.TryGetValue(parentId, out FlowchartLayoutNodeBounds parent)) break;
                double parentBottom = parent.CenterY - parent.Height / 2.0;
                double parentTop = parent.CenterY + parent.Height / 2.0;
                double requiredTop = newTop + DiagramGridMetrics.GRID_UNIT_PX;
                if (parentTop < requiredTop)
                {
                    double grownTop = DiagramGridMetrics.CeilToGrid(requiredTop);
                    FlowchartLayoutNodeBounds expandedParent = new()
                    {
                        NodeId = parentId,
                        CenterX = parent.CenterX,
                        CenterY = (parentBottom + grownTop) / 2.0,
                        Width = parent.Width,
                        Height = grownTop - parentBottom
                    };
                    bounds[parentId] = expandedParent;
                    int parentIndex = state.Copy.ClusterBoundsMutable.FindIndex(box => box.NodeId == parentId);
                    if (parentIndex >= 0) state.Copy.ClusterBoundsMutable[parentIndex] = expandedParent;
                    else state.Copy.ClusterBoundsMutable.Add(expandedParent);
                    newTop = grownTop;
                }
                group = state.Contract.Groups.FirstOrDefault(item => item.Id == parentId);
            }
            return true;
        }

        /// <summary>Records a cell-frame routing failure for legal-side search instead of mutating derived frame pixels.</summary>
        /// <param name="State">Complete model and retained legal cell ownership.</param>
        /// <param name="Route">Declared link that still intersects a title band.</param>
        private static void ReportCellTitleRouteFailure(GridLayoutState State, FlowchartLayoutEdgeRoute Route)
        {
            if (!State.Copy.RenderContext.LayoutStageViolations.Any(Failure => Failure.Kind == "NoLegalRoute"
                && Failure.InvolvedIds.Contains(Route.EdgeId)))
                State.Copy.RenderContext.LayoutStageViolations.Add(new("NoLegalRoute",
                    "No legal path at the allocated sides avoids the reserved frame title band.", [Route.EdgeId], null));
        }

        private static void RepairRoutesThroughFrameTitleBands(GridLayoutState State,
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds, bool includeHinted = false)
        {
            IReadOnlyList<FlowchartFrameTitleBandHelper.Band> Bands =
                FlowchartFrameTitleBandHelper.Get(State.Contract, Bounds);
            if (Bands.Count == 0) return;
            bool horizontal = State.Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
            foreach (FlowchartLayoutEdgeRoute Route in State.Copy.EdgeRoutes)
            {
                if (Route.Points.Count < 2
                    || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds Source)
                    || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds Target)) continue;
                EdgeContract? Edge = State.Contract.Edges.FirstOrDefault(Candidate => Candidate.Id == Route.EdgeId)
                    ?? State.Contract.Edges.FirstOrDefault(Candidate => Candidate.SourceId == Route.SourceId
                        && Candidate.TargetId == Route.TargetId);
                if (Edge?.RoutingHint?.IsActive == true && !includeHinted) continue;
                List<(double X, double Y)> Existing = Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
                if (!FlowchartFrameTitleBandHelper.RouteEntersAny(Existing, Bands,
                    Route.SourceId, Route.TargetId, State.Contract)) continue;
                State.Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode);
                State.Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode);
                List<(double X, double Y)> Candidate = DiagramGridRouteBuilder.BuildRoute(
                    Existing[0].X, Existing[0].Y, Source, SourceNode?.Shape, Route.ExitSide,
                    Existing[^1].X, Existing[^1].Y, Target, TargetNode?.Shape, Route.EntrySide,
                    Bounds, Route.SourceId, Route.TargetId, State.Contract, State.Direction, horizontal,
                    ExitNormal: (Route.ExitNormalX, Route.ExitNormalY),
                    EntryNormal: (Route.EntryNormalX, Route.EntryNormalY));
                if (Candidate.Count < 2 || FlowchartFrameTitleBandHelper.RouteEntersAny(Candidate,
                    Bands, Route.SourceId, Route.TargetId, State.Contract))
                {
                    if (TryDetourAroundTitleBands(Existing, Bands, Bounds, Route, State.Contract,
                        out List<(double X, double Y)> Detoured))
                    {
                        Candidate = Detoured;
                    }
                    else
                    {
                    List<FlowchartLayoutNodeBounds> TitleOnlyObstacles = Bands
                        .Where(Band => !IsFrameBoundaryCrossingAllowed(Band, Route, State.Contract))
                        .Select(Band => new FlowchartLayoutNodeBounds
                        {
                            NodeId = "title-obstacle:" + Band.GroupId,
                            CenterX = (Band.Left + Band.Right) / 2,
                            CenterY = (Band.Bottom + Band.Top) / 2,
                            Width = Band.Right - Band.Left,
                            Height = Band.Top - Band.Bottom
                        }).ToList();
                    if (FlowchartPaddedVisibilityRouter.TryRoute(Existing[0].X, Existing[0].Y,
                        Existing[^1].X, Existing[^1].Y, TitleOnlyObstacles, Source, Target,
                        out List<(double X, double Y)> TitleOnlyPath, PortStubPx: 20, ObstaclePaddingPx: 0)
                        && !FlowchartFrameTitleBandHelper.RouteEntersAny(TitleOnlyPath, Bands,
                            Route.SourceId, Route.TargetId, State.Contract)
                        && TitleDetourIsLegal(TitleOnlyPath, State.Copy.NodeBounds,
                            Route.SourceId, Route.TargetId))
                    {
                        Candidate = TitleOnlyPath;
                    }
                    else
                    {
                    Dictionary<string, FlowchartLayoutNodeBounds> WithTitleObstacles =
                        FlowchartFrameTitleBandHelper.AddRoutingObstacles(State.Contract, Bounds);
                    List<FlowchartLayoutNodeBounds> Obstacles = FlowchartPaddedVisibilityRouter.CollectObstacles(
                        State.Contract, WithTitleObstacles, Edge);
                    Obstacles.RemoveAll(Box =>
                        FlowchartFrameTitleBandHelper.TryGetObstacleBand(Box, out FlowchartFrameTitleBandHelper.Band Band)
                        && IsFrameBoundaryCrossingAllowed(Band, Route, State.Contract));
                    if (!FlowchartPaddedVisibilityRouter.TryRoute(Existing[0].X, Existing[0].Y,
                        Existing[^1].X, Existing[^1].Y, Obstacles, null, null, out List<(double X, double Y)> VisibilityPath,
                        PortStubPx: 0, ObstaclePaddingPx: 0)
                        || FlowchartFrameTitleBandHelper.RouteEntersAny(VisibilityPath, Bands,
                            Route.SourceId, Route.TargetId, State.Contract))
                    {
                        // A crowded nested frame can leave no legal track around its
                        // title strip. Grow only the affected frame (and any parent
                        // needed to contain it) by whole grid steps, then retry against
                        // the resulting rendered frame geometry. The old path is kept
                        // only once its title-band intersection has been removed by
                        // that explicit geometry change.
                        if (State.Placed.Any(Node => Node.CellNode != null))
                        {
                            // A compound frame is not a solid node obstacle.
                            // Reuse the allocated-normal visibility search, which
                            // carries node bodies and title strips separately,
                            // before declaring that a fixed frame has no route.
                            var Allocated = Edge?.RoutingHint is { IsActive: true, Travel: not null }
                                ? [] : BuildAllocatedNormalFallback(State, Bounds, Route);
                            if (FlowchartAllocatedPortFallback.IsLegal(State.Contract, Bounds, Route, Allocated))
                            {
                                Route.Points.Clear();
                                foreach (var Point in Allocated) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
                                State.Copy.RenderContext.LayoutStageViolations.RemoveAll(Failure => Failure.Kind == "NoLegalRoute"
                                    && Failure.InvolvedIds.Contains(Route.EdgeId));
                                continue;
                            }
                            ReportCellTitleRouteFailure(State, Route);
                            continue;
                        }
                        while (FlowchartFrameTitleBandHelper.RouteEntersAny(Existing, Bands,
                            Route.SourceId, Route.TargetId, State.Contract))
                        {
                            IReadOnlyList<string> entered = FlowchartFrameTitleBandHelper.FindEnteredBandIds(
                                Existing, Bands, Route.SourceId, Route.TargetId, State.Contract);
                            bool changed = false;
                            foreach (string groupId in entered)
                                changed |= GrowFrameTitleClearance(State, Bounds, groupId);
                            if (!changed) break;
                            Bands = FlowchartFrameTitleBandHelper.Get(State.Contract, Bounds);
                        }
                        if (FlowchartFrameTitleBandHelper.RouteEntersAny(Existing, Bands,
                            Route.SourceId, Route.TargetId, State.Contract))
                        {
                            State.Copy.RenderContext.LayoutStageViolations.Add(new FlowchartLayoutQualityFailure(
                                "TitleBandRepairFailed",
                                $"No route around the reserved title band could be found for '{Route.SourceId}->{Route.TargetId}'.",
                                [Route.EdgeId], null));
                            continue;
                        }
                        Candidate = Existing;
                    }
                    else
                    {
                        Candidate = VisibilityPath;
                    }
                    }
                    }
                }
                if (Candidate.Count >= 2 && FlowchartFrameTitleBandHelper.RouteEntersAny(Candidate,
                    Bands, Route.SourceId, Route.TargetId, State.Contract))
                    State.Copy.RenderContext.LayoutStageViolations.Add(new FlowchartLayoutQualityFailure(
                        "TitleBandRepairFailed", $"repair retained {Candidate.Count} points: {string.Join(";", Candidate.Select(point => $"{point.X:0.##},{point.Y:0.##}"))}",
                        [Route.EdgeId], null));
                Route.Points.Clear();
                foreach ((double X, double Y) in Candidate)
                    Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(X), Y = Math.Round(Y) });
            }

            // Growing a nested frame can move its title strip into a route that was
            // processed earlier. Revisit the complete affected set until the frame
            // geometry and all unhinted routes are stable.
            while (true)
            {
                Bands = FlowchartFrameTitleBandHelper.Get(State.Contract, Bounds);
                List<(FlowchartLayoutEdgeRoute Route, string[] Groups)> conflicts = State.Copy.EdgeRoutes
                    .Where(Route => Route.Points.Count >= 2)
                    .Select(Route => (Route, FlowchartFrameTitleBandHelper.FindEnteredBandIds(
                        Route.Points.Select(Point => (Point.X, Point.Y)).ToArray(), Bands,
                        Route.SourceId, Route.TargetId, State.Contract).ToArray()))
                    .Where(Item => Item.Item2.Length > 0)
                    .ToList();
                if (conflicts.Count == 0) break;
                if (State.Placed.Any(Node => Node.CellNode != null))
                {
                    foreach (var Conflict in conflicts) ReportCellTitleRouteFailure(State, Conflict.Route);
                    break;
                }
                bool changed = false;
                foreach ((FlowchartLayoutEdgeRoute route, string[] groups) in conflicts)
                foreach (string groupId in groups)
                    changed |= GrowFrameTitleClearance(State, Bounds, groupId);
                if (changed) continue;
                foreach ((FlowchartLayoutEdgeRoute route, string[] _) in conflicts)
                    State.Copy.RenderContext.LayoutStageViolations.Add(new FlowchartLayoutQualityFailure(
                        "TitleBandRepairFailed",
                        $"No legal title-band clearance could be created for '{route.SourceId}->{route.TargetId}'.",
                        [route.EdgeId], null));
                break;
            }

            static bool IsFrameBoundaryCrossingAllowed(FlowchartFrameTitleBandHelper.Band Band,
                FlowchartLayoutEdgeRoute Route, DiagramContract Contract) =>
                DiagramGridLayout.IsInsideGroup(Contract, Route.SourceId, Band.GroupId)
                    != DiagramGridLayout.IsInsideGroup(Contract, Route.TargetId, Band.GroupId);

            static bool TitleDetourIsLegal(IReadOnlyList<(double X, double Y)> points,
                IReadOnlyList<FlowchartLayoutNodeBounds> nodeBounds, string sourceId, string targetId)
            {
                if (!FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(points)) return false;
                Dictionary<string, FlowchartLayoutNodeBounds> nodes = nodeBounds
                    .ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
                for (int index = 1; index < points.Count; index++)
                {
                    (double X, double Y) a = points[index - 1], b = points[index];
                    if (FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                        a.X, a.Y, b.X, b.Y, nodes, sourceId, targetId)) return false;
                }
                return true;
            }

            static bool TryDetourAroundTitleBands(List<(double X, double Y)> original,
                IReadOnlyList<FlowchartFrameTitleBandHelper.Band> bands,
                IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds,
                FlowchartLayoutEdgeRoute route, DiagramContract contract,
                out List<(double X, double Y)> best)
            {
                best = original;
                HashSet<string> seen = new(StringComparer.Ordinal);
                string Signature(List<(double X, double Y)> points) => string.Join(";", points.Select(point =>
                    $"{point.X:R},{point.Y:R}"));
                seen.Add(Signature(original));
                int beforeCount = FlowchartFrameTitleBandHelper.FindEnteredBandIds(original, bands,
                    route.SourceId, route.TargetId, contract).Count;
                bool improved = false;
                while (true)
                {
                    int bestRemaining = beforeCount;
                    double bestLength = double.MaxValue;
                    List<(double X, double Y)>? passBest = null;
                    for (int index = 1; index < best.Count; index++)
                    {
                        (double x1, double y1) = best[index - 1];
                        (double x2, double y2) = best[index];
                        foreach (FlowchartFrameTitleBandHelper.Band band in bands)
                        {
                            if (!FlowchartFrameTitleBandHelper.SegmentEnters(band, x1, y1, x2, y2)) continue;
                            IEnumerable<double> lanes = Math.Abs(y1 - y2) <= 0.5
                                ? [band.Top + DiagramGridMetrics.GRID_UNIT_PX, band.Bottom - DiagramGridMetrics.GRID_UNIT_PX]
                                : [band.Right + DiagramGridMetrics.GRID_UNIT_PX, band.Left - DiagramGridMetrics.GRID_UNIT_PX];
                            foreach (double lane in lanes)
                            {
                                List<(double X, double Y)> candidate = best.Take(index).ToList();
                                if (Math.Abs(y1 - y2) <= 0.5)
                                {
                                    candidate.Add((x1, lane)); candidate.Add((x2, lane));
                                }
                                else
                                {
                                    candidate.Add((lane, y1)); candidate.Add((lane, y2));
                                }
                                candidate.AddRange(best.Skip(index));
                                candidate = DiagramGridRouteBuilder.SimplifyCollinear(candidate);
                                string signature = Signature(candidate);
                                if (!seen.Add(signature)
                                    || !DiagramGridRouteBuilder.PathIsValid(candidate, bounds,
                                        route.SourceId, route.TargetId, contract, MaxCorners: 8)) continue;
                                int remaining = FlowchartFrameTitleBandHelper.FindEnteredBandIds(candidate, bands,
                                    route.SourceId, route.TargetId, contract).Count;
                                double length = candidate.Zip(candidate.Skip(1)).Sum(pair =>
                                    Math.Abs(pair.First.X - pair.Second.X) + Math.Abs(pair.First.Y - pair.Second.Y));
                                if (remaining < bestRemaining || (remaining == bestRemaining && remaining < beforeCount && length < bestLength))
                                {
                                    bestRemaining = remaining;
                                    bestLength = length;
                                    passBest = candidate;
                                }
                            }
                        }
                    }
                    if (passBest == null) break;
                    best = passBest;
                    improved = true;
                    beforeCount = bestRemaining;
                    if (beforeCount == 0) break;
                }
                return improved && FlowchartFrameTitleBandHelper.FindEnteredBandIds(best, bands,
                    route.SourceId, route.TargetId, contract).Count == 0;
            }
        }

        private static void CollectFrameTitleBandDiagnostics(DiagramContract Contract,
            FlowchartLayoutResult Layout, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
            List<FlowchartLayoutQualityFailure> Findings)
        {
            IReadOnlyList<FlowchartFrameTitleBandHelper.Band> Bands =
                FlowchartFrameTitleBandHelper.Get(Contract, Bounds);
            if (Bands.Count == 0) return;
            foreach (EdgeContract Edge in Contract.Edges.Where(IsVisible))
            {
                if (!Layout.TryCopyDrawnRoutePoints(Edge, out List<(double X, double Y)> Points) || Points.Count < 2) continue;
                IReadOnlyList<string> Entered = FlowchartFrameTitleBandHelper.FindEnteredBandIds(
                    Points, Bands, Edge.SourceId, Edge.TargetId, Contract);
                if (Entered.Count == 0) continue;
                Findings.Add(new FlowchartLayoutQualityFailure("TitleBandRoute",
                    $"Route '{Edge.SourceId}->{Edge.TargetId}' enters reserved title band(s): {string.Join(", ", Entered)}.",
                    [Edge.Id ?? Edge.SourceId, Edge.TargetId, .. Entered], null));
            }
        }

        private static bool IsVisible(EdgeContract Edge) =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase);

        private static bool IsCentre(FlowchartLayoutPoint Point, FlowchartLayoutNodeBounds Box) =>
            Math.Abs(Point.X - Box.CenterX) <= Tolerance && Math.Abs(Point.Y - Box.CenterY) <= Tolerance;

        private static FlowchartLayoutNodeBounds GridAlignedFrame(string groupId,
            double left, double bottom, double right, double top)
        {
            double step = DiagramGridMetrics.GRID_UNIT_PX;
            left = Math.Floor(left / step) * step;
            bottom = Math.Floor(bottom / step) * step;
            right = Math.Ceiling(right / step) * step;
            top = Math.Ceiling(top / step) * step;
            return new FlowchartLayoutNodeBounds
            {
                NodeId = groupId,
                CenterX = (left + right) / 2.0,
                CenterY = (bottom + top) / 2.0,
                Width = right - left,
                Height = top - bottom
            };
        }


        private static string ResolveSide(IReadOnlyList<(double X, double Y)> Points,
            FlowchartLayoutNodeBounds Box, bool Source)
        {
            (double X, double Y) Port = Source ? Points[0] : Points[^1];
            (double X, double Y) Adjacent = Source ? Points[1] : Points[^2];
            double dx = Adjacent.X - Port.X, dy = Adjacent.Y - Port.Y;
            if (Math.Abs(dx) > Math.Abs(dy)) return Port.X >= Box.CenterX ? "right" : "left";
            if (Math.Abs(dy) > Math.Abs(dx)) return Port.Y >= Box.CenterY ? "top" : "bottom";

            double left = Math.Abs(Port.X - (Box.CenterX - Box.Width / 2));
            double right = Math.Abs(Port.X - (Box.CenterX + Box.Width / 2));
            double bottom = Math.Abs(Port.Y - (Box.CenterY - Box.Height / 2));
            double top = Math.Abs(Port.Y - (Box.CenterY + Box.Height / 2));
            double minimum = Math.Min(Math.Min(left, right), Math.Min(bottom, top));
            if (minimum == left) return "left";
            if (minimum == right) return "right";
            if (minimum == top) return "top";
            return "bottom";
        }

        private static void CollectGridAndAlignedPortDiagnostics(DiagramContract Contract,
            FlowchartLayoutResult Layout, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
            List<FlowchartLayoutQualityFailure> Findings)
        {
            if (Contract.LayoutMode != DiagramLayoutMode.Open)
            {
                double Step = DiagramGridMetrics.GRID_UNIT_PX;
                FlowchartLayoutNodeBounds? GridOrigin = Layout.NodeBounds.FirstOrDefault();
                double OriginX = GridOrigin?.CenterX ?? 0;
                double OriginY = GridOrigin?.CenterY ?? 0;
                foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds)
                {
                    if (!OnGrid(Box.CenterX, OriginX, Step) || !OnGrid(Box.CenterY, OriginY, Step))
                        Findings.Add(new FlowchartLayoutQualityFailure("Rule1.2Grid",
                            $"Node '{Box.NodeId}' centre is not aligned to the {Step:0}-px grid.", [Box.NodeId], null));
                }
                foreach (FlowchartLayoutNodeBounds Frame in Layout.ClusterBounds)
                {
                    if (!OnGrid(Frame.CenterX - Frame.Width / 2, OriginX, Step)
                        || !OnGrid(Frame.CenterX + Frame.Width / 2, OriginX, Step)
                        || !OnGrid(Frame.CenterY - Frame.Height / 2, OriginY, Step)
                        || !OnGrid(Frame.CenterY + Frame.Height / 2, OriginY, Step))
                        Findings.Add(new FlowchartLayoutQualityFailure("Rule1.2Grid",
                            $"Frame '{Frame.NodeId}' outline is not aligned to the {Step:0}-px grid.", [Frame.NodeId], null));
                }
            }

            foreach (EdgeContract Edge in Contract.Edges.Where(IsVisible))
            {
                if (Edge.RoutingHint?.IsActive == true
                    || !Layout.TryCopyDrawnRoutePoints(Edge, out List<(double X, double Y)> Points)
                    || Points.Count <= 2) continue;
                (double X, double Y) Start = Points[0], End = Points[^1];
                bool EndpointsAligned = Math.Abs(Start.X - End.X) <= Tolerance
                    || Math.Abs(Start.Y - End.Y) <= Tolerance;
                if (EndpointsAligned)
                    Findings.Add(new FlowchartLayoutQualityFailure("Rule4.2AlignedPorts",
                        $"Aligned allocated ports for '{Edge.SourceId}->{Edge.TargetId}' are not joined by one straight run.",
                        [Edge.Id ?? Edge.SourceId, Edge.TargetId], null));
            }
        }

        private static bool OnGrid(double Value, double Origin, double Step) =>
            Math.Abs((Value - Origin) / Step - Math.Round((Value - Origin) / Step)) <= 0.01;
    }
}

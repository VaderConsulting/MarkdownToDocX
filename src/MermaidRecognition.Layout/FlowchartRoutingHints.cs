using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>Restores explicit endpoint and track directions after automatic route repair.</summary>
internal static class FlowchartRoutingHints
{
    /// <summary>Restores original active endpoint and travel hints against current bounds without changing frozen routes.</summary>
    /// <param name="Layout">Current route inventory and hint diagnostics.</param>
    /// <param name="Contract">Original declarations and active hints.</param>
    /// <param name="Bounds">Current node and frame obstacles.</param>
    /// <param name="Nodes">Original shape definitions.</param>
    /// <param name="Direction">Diagram flow direction.</param>
    /// <param name="RepairAutomaticRoutesEnabled">Whether to run the established automatic-route repairs afterward.</param>
    /// <param name="PreserveValidSideHintRoutes">Whether already legal side-hinted paths retain their exact geometry.</param>
    /// <param name="ReportUnroutedRoutes">Whether failed hinted routes produce diagnostics.</param>
    /// <param name="ClearUnroutedRoutes">Whether failed hinted routes discard stale path contents.</param>
    /// <param name="OnlyRoutes">Affected lines, or null to retain the complete original hint pass.</param>
    internal static void Apply(
        FlowchartLayoutResult Layout,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction,
        bool RepairAutomaticRoutesEnabled = true,
        bool PreserveValidSideHintRoutes = false,
        bool ReportUnroutedRoutes = false,
        bool ClearUnroutedRoutes = true, IReadOnlySet<FlowchartLayoutEdgeRoute>? OnlyRoutes = null)
    {
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        Dictionary<(FlowchartLayoutEdgeRoute Route, bool Source), (double X, double Y)> HintedPorts =
            SelectHintedPorts(Layout, Contract, Bounds);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (OnlyRoutes != null && !OnlyRoutes.Contains(Route))
            {
                continue;
            }

            if (Route.Points.Count < 2)
            {
                continue;
            }

            EdgeContract? Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            RoutingHintContract? Hint = Edge?.RoutingHint;
            bool HasHint = Hint?.IsActive == true;
            bool HasSelectedStart = HintedPorts.TryGetValue((Route, true), out (double X, double Y) StartPort);
            bool HasSelectedEnd = HintedPorts.TryGetValue((Route, false), out (double X, double Y) EndPort);
            if ((!HasHint && !HasSelectedStart && !HasSelectedEnd)
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
            {
                continue;
            }

            string Exit = HasHint && Hint!.Leave.HasValue
                ? DiagramGridLayout.HintSide(Hint.Leave.Value) : Route.ExitSide;
            string Entry = HasHint && Hint!.Enter.HasValue
                ? DiagramGridLayout.HintSide(Hint.Enter.Value) : Route.EntrySide;
            (double X, double Y) ExistingStart = (Route.Points[0].X, Route.Points[0].Y);
            (double X, double Y) ExistingEnd = (Route.Points[^1].X, Route.Points[^1].Y);
            (double X, double Y) DesiredStart = HasSelectedStart ? StartPort : ExistingStart;
            (double X, double Y) DesiredEnd = HasSelectedEnd ? EndPort : ExistingEnd;
            if (!HasHint && ExistingStart == DesiredStart && ExistingEnd == DesiredEnd)
            {
                continue;
            }

            if (HasHint && Hint!.Travel == null
                && (Hint.Leave == null || Route.ExitSide == Exit)
                && (Hint.Enter == null || Route.EntrySide == Entry)
                && OnOutline(Route.Points[0], Source) && OnOutline(Route.Points[^1], Target)
                && LeavesBy(Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Exit)
                && EntersBy(Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Entry)
                && SamePoint(ExistingStart, DesiredStart) && SamePoint(ExistingEnd, DesiredEnd)
                && FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(Route.Points
                    .Select(Point => (Point.X, Point.Y)).ToList())
                && DiagramGridRouteBuilder.PathIsValid(
                    [.. Route.Points.Select(Point => (Point.X, Point.Y))], Bounds,
                    Route.SourceId, Route.TargetId, Contract))
            {
                Hint.UnhonouredReason = null;
                continue;
            }
            (double StartX, double StartY) = HasSelectedStart ? StartPort
                : HasHint && Hint!.Leave.HasValue ? ResolveEndpoint(Route, Source, Exit, true) : ExistingStart;
            (double EndX, double EndY) = HasSelectedEnd ? EndPort
                : HasHint && Hint!.Enter.HasValue ? ResolveEndpoint(Route, Target, Entry, false) : ExistingEnd;
            Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode);
            Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode);
            List<(double X, double Y)> Path = DiagramGridRouteBuilder.BuildRoute(
                StartX, StartY, Source, SourceNode?.Shape, Exit,
                EndX, EndY, Target, TargetNode?.Shape, Entry,
                Bounds, Route.SourceId, Route.TargetId, Contract, Direction, Horizontal,
                HasHint ? Hint!.Travel : null,
                // A sibling's side-hint can select a fresh symmetric slot for an
                // automatic route. Keep the normal automatic fallback search for
                // that route; only an explicitly hinted line requires the strict
                // hint-legal candidate set here.
                RequireLegal: HasHint);
            if (Path.Count < 2)
            {
                // Port reallocation can move a terminal slot along the same side
                // after the initial route was built. If the general path search
                // cannot find a replacement, slide the existing terminal track
                // to the new symmetric slot and accept it only if it stays legal.
                List<(double X, double Y)> Slid = [.. Route.Points.Select(Point => (Point.X, Point.Y))];
                if (HasSelectedStart)
                {
                    SlideTerminalTrack(Slid, true, Exit, StartX, StartY);
                }

                if (HasSelectedEnd)
                {
                    SlideTerminalTrack(Slid, false, Entry, EndX, EndY);
                }

                Slid = DiagramGridRouteBuilder.SimplifyCollinear(Slid);
                bool SlideLegal = Slid.Count >= 2 && DiagramGridRouteBuilder.PathIsValid(
                    Slid, Bounds, Route.SourceId, Route.TargetId, Contract)
                    && FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(Slid);
                if (SlideLegal)
                {
                    Path = Slid;
                }
            }
            if (Path.Count < 2)
            {
                // Strict template/visibility searches use padded, extended stubs.
                // An allocated frame port can still have a legal path in the
                // actual free space. Offer the same exact-normal fallback used
                // by automatic frame routing before declaring the hint impossible.
                if (HasHint && Layout.RenderContext.UseCellRegions
                    && Contract.Groups.Any(Group => Group.Id == Route.SourceId || Group.Id == Route.TargetId))
                {
                    Path = BuildAllocatedHintFallback(Contract, Bounds, Nodes, Route, Exit, Entry,
                        StartX, StartY, EndX, EndY, Hint!);
                }
            }
            if (Path.Count < 2)
            {
                string reason = Hint?.Travel.HasValue == true
                    ? "no legal route has the requested travel direction"
                    : "no legal route has the allocated endpoint directions";
                if (HasHint)
                {
                    Hint!.UnhonouredReason = reason;
                }

                if (ReportUnroutedRoutes)
                {
                    string PortSummary = $"hint={(HasHint ? $"leave:{Hint!.Leave?.ToString() ?? "auto"},enter:{Hint.Enter?.ToString() ?? "auto"},travel:{Hint.Travel?.ToString() ?? "none"}" : "none")}; "
                        + $"sides={Exit}/{Entry}; allocatedStart=({StartX:0.##},{StartY:0.##}) selected={HasSelectedStart}; "
                        + $"allocatedEnd=({EndX:0.##},{EndY:0.##}) selected={HasSelectedEnd}; "
                        + $"sourceBounds=({Source.CenterX:0.##},{Source.CenterY:0.##},{Source.Width:0.##},{Source.Height:0.##}); "
                        + $"targetBounds=({Target.CenterX:0.##},{Target.CenterY:0.##},{Target.Width:0.##},{Target.Height:0.##}); "
                        + $"previous=({ExistingStart.X:0.##},{ExistingStart.Y:0.##})->({ExistingEnd.X:0.##},{ExistingEnd.Y:0.##})";
                    Layout.RenderContext.LayoutStageViolations.RemoveAll(failure =>
                        failure.Kind == "NoLegalRoute" && failure.InvolvedIds.Contains(Route.EdgeId));
                    Layout.RenderContext.LayoutStageViolations.Add(new FlowchartLayoutQualityFailure(
                        "NoLegalRoute",
                        $"Route '{Route.EdgeId}' could not reach its newly allocated port slots: {reason}. {PortSummary}",
                        [Route.EdgeId], null));
                    if (ClearUnroutedRoutes)
                    {
                        Route.Points.Clear();
                    }
                }
                continue;
            }

            Layout.RenderContext.LayoutStageViolations.RemoveAll(failure =>
                failure.Kind == "NoLegalRoute" && failure.InvolvedIds.Contains(Route.EdgeId));
            if (HasHint)
            {
                Hint!.UnhonouredReason = null;
            }

            Route.ExitSide = Exit;
            Route.EntrySide = Entry;
            Route.Points.Clear();
            foreach ((double X, double Y) in Path)
            {
                Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(X), Y = Math.Round(Y) });
            }
        }

        // A failed strict attempt can leave its diagnostic behind even when a
        // later pass has installed a legal route on the same hinted sides and
        // symmetric slots. Clear only that stale status; the side, exact port
        // slots, orthogonality and obstacle checks must all agree first.
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (OnlyRoutes != null && !OnlyRoutes.Contains(Route))
            {
                continue;
            }

            EdgeContract? Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            if (Edge?.RoutingHint is not { IsActive: true } Hint || Route.Points.Count < 2
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds Target))
            {
                continue;
            }

            if ((Hint.Leave.HasValue && Route.ExitSide != DiagramGridLayout.HintSide(Hint.Leave.Value))
                || (Hint.Enter.HasValue && Route.EntrySide != DiagramGridLayout.HintSide(Hint.Enter.Value))
                || (Hint.Travel.HasValue && !DiagramGridRouteBuilder.HasTravelDirection(
                    Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Hint.Travel.Value))
                || !HintedPortMatches((Route.Points[0].X, Route.Points[0].Y), (Route, true))
                || !HintedPortMatches((Route.Points[^1].X, Route.Points[^1].Y), (Route, false))
                || !OnOutline(Route.Points[0], Source) || !OnOutline(Route.Points[^1], Target)
                || !LeavesBy(Route.Points.Select(Point => (Point.X, Point.Y)).ToList(),
                    Hint.Leave.HasValue ? DiagramGridLayout.HintSide(Hint.Leave.Value) : Route.ExitSide)
                || !EntersBy(Route.Points.Select(Point => (Point.X, Point.Y)).ToList(),
                    Hint.Enter.HasValue ? DiagramGridLayout.HintSide(Hint.Enter.Value) : Route.EntrySide)
                || !FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(Route.Points
                    .Select(Point => (Point.X, Point.Y)).ToList())
                || !DiagramGridRouteBuilder.PathIsValid([.. Route.Points.Select(Point => (Point.X, Point.Y))], Bounds,
                    Route.SourceId, Route.TargetId, Contract))
            {
                continue;
            }

            Hint.UnhonouredReason = null;
        }

        if (RepairAutomaticRoutesEnabled
            && (Contract.Nodes.Any(Node => Node.PlacementHint?.IsActive == true)
                || Contract.Groups.Any(Group => Group.PlacementHint?.IsActive == true)))
        {
            RepairAutomaticRoutes(Layout, Contract, Bounds, Nodes, Direction, Horizontal);
        }

        static bool SamePoint((double X, double Y) first, (double X, double Y) second) =>
            Math.Abs(first.X - second.X) <= 0.5 && Math.Abs(first.Y - second.Y) <= 0.5;

        bool HintedPortMatches((double X, double Y) point, (FlowchartLayoutEdgeRoute Route, bool Source) key) =>
            !HintedPorts.TryGetValue(key, out (double X, double Y) expected) || SamePoint(point, expected);
    }

    /// <summary>Offers an exact-port, unpadded fallback for an otherwise unreachable hinted group connector.</summary>
    /// <param name="Contract">Declared groups and protected titles.</param>
    /// <param name="Bounds">Current node and frame geometry.</param>
    /// <param name="Nodes">Renderer-owned endpoint shapes.</param>
    /// <param name="Route">Original route identity, never changed by this trial.</param>
    /// <param name="Exit">Required source side.</param>
    /// <param name="Entry">Required target side.</param>
    /// <param name="StartX">Exact allocated source X coordinate.</param>
    /// <param name="StartY">Exact allocated source Y coordinate.</param>
    /// <param name="EndX">Exact allocated target X coordinate.</param>
    /// <param name="EndY">Exact allocated target Y coordinate.</param>
    /// <param name="Hint">Original active hint, including any required travel direction.</param>
    /// <returns>A fully validated normal-preserving path, or no candidate if the hint is genuinely blocked.</returns>
    private static List<(double X, double Y)> BuildAllocatedHintFallback(DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, IReadOnlyDictionary<string, NodeContract> Nodes,
        FlowchartLayoutEdgeRoute Route, string Exit, string Entry, double StartX, double StartY,
        double EndX, double EndY, RoutingHintContract Hint)
    {
        Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode);
        Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode);
        (double X, double Y) SourceStub = DiagramGridRouteBuilder.ResolvePortStub(StartX, StartY, Bounds[Route.SourceId], SourceNode?.Shape,
            Exit, DiagramGridRouteBuilder.CORNER_RADIUS_PX,
            Route.ExitSide == Exit ? Route.ExitNormalX : 0, Route.ExitSide == Exit ? Route.ExitNormalY : 0);
        (double X, double Y) TargetStub = DiagramGridRouteBuilder.ResolvePortStub(EndX, EndY, Bounds[Route.TargetId], TargetNode?.Shape,
            Entry, DiagramGridRouteBuilder.CORNER_RADIUS_PX,
            Route.EntrySide == Entry ? Route.EntryNormalX : 0, Route.EntrySide == Entry ? Route.EntryNormalY : 0);
        FlowchartLayoutEdgeRoute Candidate = new FlowchartLayoutEdgeRoute
        {
            EdgeId = Route.EdgeId, SourceId = Route.SourceId, TargetId = Route.TargetId,
            ExitSide = Exit, EntrySide = Entry,
            ExitNormalX = SourceStub.X - StartX, ExitNormalY = SourceStub.Y - StartY,
            EntryNormalX = TargetStub.X - EndX, EntryNormalY = TargetStub.Y - EndY
        };
        Candidate.Points.Add(new() { X = StartX, Y = StartY });
        Candidate.Points.Add(new() { X = EndX, Y = EndY });
        List<(double X, double Y)> Path = FlowchartAllocatedPortFallback.Build(Contract, Bounds, Nodes, Candidate, true);
        return FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Candidate, Path)
            && (!Hint.Travel.HasValue || DiagramGridRouteBuilder.HasTravelDirection(Path, Hint.Travel.Value)) ? Path : [];
    }

    private static Dictionary<(FlowchartLayoutEdgeRoute Route, bool Source), (double X, double Y)> SelectHintedPorts(
        FlowchartLayoutResult Layout,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        List<(FlowchartLayoutEdgeRoute Route, bool Source, FlowchartLayoutNodeBounds Box, FlowchartLayoutNodeBounds Other, string Side, bool IsHinted)> Ends = [];
        Dictionary<string, NodeContract> Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        HashSet<string> frameIds = Contract.Groups.Select(Group => Group.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            EdgeContract? Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            bool IsHinted = Edge?.RoutingHint?.IsActive == true;
            if (!Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
            {
                continue;
            }

            if (!IsDiamond(Route.SourceId))
            {
                Ends.Add((Route, true, Source, Target, IsHinted && Edge!.RoutingHint!.Leave.HasValue
                    ? DiagramGridLayout.HintSide(Edge.RoutingHint.Leave.Value) : Route.ExitSide, IsHinted));
            }

            if (!IsDiamond(Route.TargetId))
            {
                Ends.Add((Route, false, Target, Source, IsHinted && Edge!.RoutingHint!.Enter.HasValue
                    ? DiagramGridLayout.HintSide(Edge.RoutingHint.Enter.Value) : Route.EntrySide, IsHinted));
            }
        }

        Dictionary<(FlowchartLayoutEdgeRoute Route, bool Source), (double X, double Y)> Result = [];
        foreach (IGrouping<(string NodeId, string Side), (FlowchartLayoutEdgeRoute Route, bool Source, FlowchartLayoutNodeBounds Box, FlowchartLayoutNodeBounds Other, string Side, bool IsHinted)> Group in Ends.GroupBy(End => (End.Box.NodeId, End.Side)))
        {
            // Allocate hinted and automatic edges together. If automatic siblings
            // are omitted, the hinted subset can be centred on top of the full set,
            // leaving the visible ports asymmetric or coincident.
            if (!Group.Any(End => End.IsHinted))
            {
                continue;
            }

            (FlowchartLayoutEdgeRoute Route, bool Source, FlowchartLayoutNodeBounds Box, FlowchartLayoutNodeBounds Other, string Side, bool IsHinted)[] Ordered = Group.Key.Side is "left" or "right"
                ? [.. Group.OrderByDescending(End => End.Other.CenterY).ThenBy(End => End.Route.EdgeId, StringComparer.Ordinal)]
                : [.. Group.OrderBy(End => End.Other.CenterX).ThenBy(End => End.Route.EdgeId, StringComparer.Ordinal)];
            bool vertical = Group.Key.Side is "left" or "right";
            double Length = vertical ? Ordered[0].Box.Height : Ordered[0].Box.Width;
            Nodes.TryGetValue(Group.Key.NodeId, out NodeContract? node);
            bool circle = !frameIds.Contains(Group.Key.NodeId)
                && node != null
                && (string.Equals(node.Shape, "circle", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.Shape, "doublecircle", StringComparison.OrdinalIgnoreCase));
            INodeConnectionBoundary? Boundary = node?.GeometryProvider?.Create(Ordered[0].Box.Width, Ordered[0].Box.Height).ConnectionBoundary;
            double[] GridFractions = circle
                ? []
                : ConnectionPointGrid.FractionsOnGrid(Boundary, Group.Key.Side, Length, Ordered.Length);
            if (Ordered.Any(End => FlowchartConnectionPoints.Step(DiagramGridLayout.FindContractEdge(Contract, End.Route)?.RoutingHint, End.Source).HasValue))
            {
                var Points = ConnectionPointGrid.UsablePoints(Boundary, Group.Key.Side, Length, out double SegmentLength);
                int[] Fixed = [.. Ordered.Select(End => FlowchartConnectionPoints.Step(
                    DiagramGridLayout.FindContractEdge(Contract, End.Route)?.RoutingHint, End.Source) is { } Step
                    ? Points.FindIndex(Point => Math.Abs(Point.Offset - SegmentLength / 2 - Step * FlowchartLayoutRules.LineSpacing)
                        <= DiagramGridMetrics.PORT_GRID_TOLERANCE_PX) : -1)];
                var Plan = ConnectionPointGrid.Plan(Points.Select(Point => Point.Offset).ToArray(), SegmentLength, Fixed);
                if (Plan != null) GridFractions = [.. Plan.Select(Index => Points[Index].Fraction)];
            }
            for (int Index = 0; Index < Ordered.Length; Index++)
            {
                (FlowchartLayoutEdgeRoute Route, bool Source, FlowchartLayoutNodeBounds Box, FlowchartLayoutNodeBounds Other, string Side, bool IsHinted) End = Ordered[Index];
                double Fraction = GridFractions.Length == Ordered.Length
                    ? GridFractions[Index]
                    : (Index + 1.0) / (Ordered.Length + 1.0);
                if (Nodes.GetValueOrDefault(End.Box.NodeId)?.GeometryProvider is { } Provider)
                {
                    if (Provider.Create(End.Box.Width, End.Box.Height).ConnectionBoundary.TryPort(End.Side, Fraction, out NodeGeometryPort BodyPort))
                    {
                        Result[(End.Route, End.Source)] = (End.Box.CenterX + BodyPort.X, End.Box.CenterY - BodyPort.Y);
                    }

                    continue;
                }
                if (circle)
                {
                    double Radius = Math.Min(End.Box.Width, End.Box.Height) / 2.0;
                    double Angle = Group.Key.Side switch
                    {
                        "top" => Math.PI * (1.0 - Fraction),
                        "bottom" => Math.PI * (1.0 + Fraction),
                        "left" => (Math.PI / 2.0) + (Math.PI * Fraction),
                        _ => (Math.PI / 2.0) - (Math.PI * Fraction)
                    };
                    Result[(End.Route, End.Source)] = (End.Box.CenterX + (Radius * Math.Cos(Angle)),
                        End.Box.CenterY + (Radius * Math.Sin(Angle)));
                    continue;
                }
                // A routing hint selects the side. The point on that side is still a
                // centre-out grid point, the same point an unhinted line would use.
                double Along = Fraction * Length;
                (double X, double Y) Port = End.Side switch
                {
                    "left" => (End.Box.CenterX - (End.Box.Width / 2.0), End.Box.CenterY + (End.Box.Height / 2.0) - Along),
                    "right" => (End.Box.CenterX + (End.Box.Width / 2.0), End.Box.CenterY + (End.Box.Height / 2.0) - Along),
                    "top" => (End.Box.CenterX - (End.Box.Width / 2.0) + Along, End.Box.CenterY + (End.Box.Height / 2.0)),
                    _ => (End.Box.CenterX - (End.Box.Width / 2.0) + Along, End.Box.CenterY - (End.Box.Height / 2.0))
                };
                Result[(End.Route, End.Source)] = Port;
            }
        }

        ReserveAlignedTravelPorts(Layout, Contract, Bounds, Result);
        return Result;

        bool IsDiamond(string Id) => Nodes.TryGetValue(Id, out NodeContract? Node)
            && FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
    }

    /// <summary>
    /// A straight travel hint takes the grid points that share one coordinate before the other lines keep the centre-out points.
    /// </summary>
    private static void ReserveAlignedTravelPorts(
        FlowchartLayoutResult Layout,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        Dictionary<(FlowchartLayoutEdgeRoute Route, bool Source), (double X, double Y)> Ports)
    {
        const double SAME_POINT_PX = 0.5;
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            EdgeContract? Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            RoutingHintContract? Hint = Edge?.RoutingHint;
            if (Hint?.IsActive != true || !Hint.Leave.HasValue || !Hint.Enter.HasValue || !Hint.Travel.HasValue
                || Hint.LeavePoint.HasValue || Hint.EnterPoint.HasValue)
            {
                continue;
            }

            string Exit = DiagramGridLayout.HintSide(Hint.Leave.Value);
            string Entry = DiagramGridLayout.HintSide(Hint.Enter.Value);
            if (!TravelRunsStraight(Hint.Travel.Value, Exit, Entry)
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
            {
                continue;
            }

            List<(double X, double Y)> SourcePoints = SideGridPorts(Source, Exit);
            List<(double X, double Y)> TargetPoints = SideGridPorts(Target, Entry);
            bool Horizontal = Exit is "left" or "right";
            (double X, double Y)? BestSource = null;
            (double X, double Y)? BestTarget = null;
            double BestDistance = double.PositiveInfinity;
            foreach ((double X, double Y) SourcePoint in SourcePoints)
            {
                foreach ((double X, double Y) TargetPoint in TargetPoints)
                {
                    double SourceAxis = Horizontal ? SourcePoint.Y : SourcePoint.X;
                    double TargetAxis = Horizontal ? TargetPoint.Y : TargetPoint.X;
                    if (Math.Abs(SourceAxis - TargetAxis) > SAME_POINT_PX)
                    {
                        continue;
                    }

                    double Distance = Horizontal
                        ? Math.Abs(SourceAxis - Source.CenterY) + Math.Abs(TargetAxis - Target.CenterY)
                        : Math.Abs(SourceAxis - Source.CenterX) + Math.Abs(TargetAxis - Target.CenterX);
                    if (Distance >= BestDistance)
                    {
                        continue;
                    }

                    BestDistance = Distance;
                    BestSource = SourcePoint;
                    BestTarget = TargetPoint;
                }
            }

            if (BestSource == null || BestTarget == null)
            {
                continue;
            }

            MoveOccupiedPort(Ports, Route, true, Source, Exit, BestSource.Value);
            MoveOccupiedPort(Ports, Route, false, Target, Entry, BestTarget.Value);
            Ports[(Route, true)] = BestSource.Value;
            Ports[(Route, false)] = BestTarget.Value;
        }
    }

    /// <summary>Whether leave, travel and enter describe one straight run.</summary>
    private static bool TravelRunsStraight(LayoutHintDirection Travel, string Exit, string Entry)
    {
        if (Travel == LayoutHintDirection.East)
        {
            return Exit == "right" && Entry == "left";
        }

        if (Travel == LayoutHintDirection.West)
        {
            return Exit == "left" && Entry == "right";
        }

        if (Travel == LayoutHintDirection.North)
        {
            return Exit == "top" && Entry == "bottom";
        }

        return Travel == LayoutHintDirection.South && Exit == "bottom" && Entry == "top";
    }

    /// <summary>Grid points on a rectangular side, using the same corner the port allocator measures from.</summary>
    private static List<(double X, double Y)> SideGridPorts(FlowchartLayoutNodeBounds Box, string Side)
    {
        double Length = Side is "left" or "right" ? Box.Height : Box.Width;
        double[] Offsets = DiagramGridMetrics.SideGridOffsets(Length);
        List<(double X, double Y)> Points = [];
        foreach (double Offset in Offsets)
        {
            (double X, double Y) Point = Side switch
            {
                "left" => (Box.CenterX - (Box.Width / 2.0), Box.CenterY + (Box.Height / 2.0) - Offset),
                "right" => (Box.CenterX + (Box.Width / 2.0), Box.CenterY + (Box.Height / 2.0) - Offset),
                "top" => (Box.CenterX - (Box.Width / 2.0) + Offset, Box.CenterY + (Box.Height / 2.0)),
                _ => (Box.CenterX - (Box.Width / 2.0) + Offset, Box.CenterY - (Box.Height / 2.0))
            };
            Points.Add(Point);
        }

        return Points;
    }

    /// <summary>Slides a line that already occupies the reserved straight-run point onto the nearest free grid point.</summary>
    private static void MoveOccupiedPort(
        Dictionary<(FlowchartLayoutEdgeRoute Route, bool Source), (double X, double Y)> Ports,
        FlowchartLayoutEdgeRoute Owner,
        bool AtSource,
        FlowchartLayoutNodeBounds Box,
        string Side,
        (double X, double Y) Reserved)
    {
        const double SAME_POINT_PX = 0.5;
        List<(double X, double Y)> Points = SideGridPorts(Box, Side);
        List<KeyValuePair<(FlowchartLayoutEdgeRoute Route, bool Source), (double X, double Y)>> Occupants = [.. Ports.Where(Pair =>
            Pair.Key.Route != Owner
            && (AtSource ? Pair.Key.Route.SourceId : Pair.Key.Route.TargetId) == Box.NodeId
            && Math.Abs(Pair.Value.X - Reserved.X) <= SAME_POINT_PX
            && Math.Abs(Pair.Value.Y - Reserved.Y) <= SAME_POINT_PX)];
        foreach (KeyValuePair<(FlowchartLayoutEdgeRoute Route, bool Source), (double X, double Y)> Occupant in Occupants)
        {
            (double X, double Y) Replacement = default;
            double Best = double.PositiveInfinity;
            foreach ((double X, double Y) Point in Points)
            {
                bool Taken = Ports.Any(Pair =>
                    (Pair.Key.Route != Occupant.Key.Route || Pair.Key.Source != Occupant.Key.Source)
                    && (AtSource ? Pair.Key.Route.SourceId : Pair.Key.Route.TargetId) == Box.NodeId
                    && Math.Abs(Pair.Value.X - Point.X) <= SAME_POINT_PX
                    && Math.Abs(Pair.Value.Y - Point.Y) <= SAME_POINT_PX);
                if (Taken)
                {
                    continue;
                }

                double Distance = Math.Abs(Point.X - Occupant.Value.X) + Math.Abs(Point.Y - Occupant.Value.Y);
                if (Distance >= Best)
                {
                    continue;
                }

                Best = Distance;
                Replacement = Point;
            }

            if (Best == double.PositiveInfinity)
            {
                continue;
            }

            Ports[Occupant.Key] = Replacement;
        }
    }

    private static void SlideTerminalTrack(List<(double X, double Y)> Path, bool AtStart,
        string Side, double PortX, double PortY)
    {
        if (Path.Count < 2)
        {
            return;
        }

        bool HorizontalRun = Side is "left" or "right";
        int Endpoint = AtStart ? 0 : Path.Count - 1;
        int Adjacent = AtStart ? 1 : Path.Count - 2;
        (double X, double Y) Original = Path[Endpoint];
        bool IsRun((double X, double Y) Point) => HorizontalRun
            ? Math.Abs(Point.Y - Original.Y) < 0.01
            : Math.Abs(Point.X - Original.X) < 0.01;
        if (!IsRun(Path[Adjacent]))
        {
            return;
        }

        int Index = Adjacent;
        while (Index >= 0 && Index < Path.Count && IsRun(Path[Index]))
        {
            Path[Index] = HorizontalRun ? (Path[Index].X, PortY) : (PortX, Path[Index].Y);
            Index += AtStart ? 1 : -1;
        }
        Path[Endpoint] = (PortX, PortY);
    }

    private static bool OnOutline(FlowchartLayoutPoint Point, FlowchartLayoutNodeBounds Box)
    {
        if (Box.Geometry is { } Geometry)
        {
            return Geometry.ConnectionBoundary.Contains(Point.X - Box.CenterX, Box.CenterY - Point.Y, 0.01);
        }

        double Left = Box.CenterX - (Box.Width / 2.0), Right = Box.CenterX + (Box.Width / 2.0);
        double Top = Box.CenterY - (Box.Height / 2.0), Bottom = Box.CenterY + (Box.Height / 2.0);
        bool OnVertical = (Math.Abs(Point.X - Left) < 0.01 || Math.Abs(Point.X - Right) < 0.01)
            && Point.Y >= Top - 0.01 && Point.Y <= Bottom + 0.01;
        bool OnHorizontal = (Math.Abs(Point.Y - Top) < 0.01 || Math.Abs(Point.Y - Bottom) < 0.01)
            && Point.X >= Left - 0.01 && Point.X <= Right + 0.01;
        return OnVertical || OnHorizontal;
    }

    private static void RepairAutomaticRoutes(
        FlowchartLayoutResult Layout,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction,
        bool Horizontal)
    {
        string[] Sides = ["top", "bottom", "left", "right"];
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            EdgeContract? Edge = DiagramGridLayout.FindContractEdge(Contract, Route);
            List<(double X, double Y)> CurrentPath = [.. Route.Points.Select(Point => (Point.X, Point.Y))];
            bool BadTerminal = !LeavesBy(CurrentPath, Route.ExitSide) || !EntersBy(CurrentPath, Route.EntrySide);
            if (Edge?.RoutingHint?.IsActive == true || (!BadTerminal && CountCorners(Route.Points) <= 2)
                || !Bounds.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds? Source)
                || !Bounds.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds? Target))
            {
                continue;
            }

            Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode);
            Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode);
            List<(string Exit, string Entry, List<(double X, double Y)> Path, int Corners, double Length)> Candidates = [];
            foreach (string Exit in Sides)
            {
                foreach (string Entry in Sides)
                {
                    foreach ((double StartX, double StartY) in Ports(Source, Exit,
                Exit == Route.ExitSide ? Route.Points[0] : null))
                    {
                        foreach ((double EndX, double EndY) in Ports(Target, Entry,
                Entry == Route.EntrySide ? Route.Points[^1] : null))
            {
                if (EndpointTaken(Layout, Route, Route.SourceId, StartX, StartY)
                    || EndpointTaken(Layout, Route, Route.TargetId, EndX, EndY))
                            {
                                continue;
                            }

                            foreach (List<(double X, double Y)> RawPath in CandidatePaths(
                    StartX, StartY, Source, SourceNode, Exit,
                    EndX, EndY, Target, TargetNode, Entry,
                    Bounds, Route, Contract, Direction, Horizontal))
                {
                    List<(double X, double Y)> Path = DiagramGridRouteBuilder.SimplifyCollinear(RawPath);
                    int Corners = CountCorners(Path);
                    if (Path.Count < 2 || Corners > 2
                        || !LeavesBy(Path, Exit) || !EntersBy(Path, Entry)
                        || !DiagramGridRouteBuilder.PathIsValid(
                            Path, Bounds, Route.SourceId, Route.TargetId, Contract))
                                {
                                    continue;
                                }

                                double Length = Path.Zip(Path.Skip(1)).Sum(Pair =>
                        Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y));
                    Candidates.Add((Exit, Entry, Path, Corners, Length));
                }
            }
                    }
                }
            }

            (string Exit, string Entry, List<(double X, double Y)> Path, int Corners, double Length) Best = Candidates.OrderBy(Candidate => Candidate.Corners)
                .ThenBy(Candidate => Candidate.Length).FirstOrDefault();
            if (Best.Path == null)
            {
                continue;
            }

            Route.ExitSide = Best.Exit;
            Route.EntrySide = Best.Entry;
            Route.Points.Clear();
            foreach ((double X, double Y) in Best.Path)
            {
                Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(X), Y = Math.Round(Y) });
            }
        }
    }

    private static (double X, double Y) CentrePort(FlowchartLayoutNodeBounds Bounds, string Side) => Side switch
    {
        "left" => (Bounds.CenterX - (Bounds.Width / 2.0), Bounds.CenterY),
        "right" => (Bounds.CenterX + (Bounds.Width / 2.0), Bounds.CenterY),
        "top" => (Bounds.CenterX, Bounds.CenterY + (Bounds.Height / 2.0)),
        _ => (Bounds.CenterX, Bounds.CenterY - (Bounds.Height / 2.0))
    };

    private static IEnumerable<(double X, double Y)> Ports(
        FlowchartLayoutNodeBounds Bounds,
        string Side,
        FlowchartLayoutPoint? Existing)
    {
        if (Existing != null)
        {
            yield return (Existing.X, Existing.Y);
        }

        (double CentreX, double CentreY) = CentrePort(Bounds, Side);
        double Span = Side is "top" or "bottom" ? Bounds.Width : Bounds.Height;
        double Margin = Math.Min(DiagramGridRouteBuilder.CORNER_RADIUS_PX + 3.0, Span / 4.0);
        double UsableHalf = Math.Max(0.0, (Span / 2.0) - Margin);
        double Pitch = DiagramGridMetrics.GRID_UNIT_PX;
        IEnumerable<double> Offsets = Enumerable.Range(0,
                (int)Math.Floor(UsableHalf / Pitch) + 1)
            .SelectMany(Step => Step == 0
                ? new[] { 0.0 }
                : [-Step * Pitch, Step * Pitch])
            .Where(Offset => Math.Abs(Offset) <= UsableHalf + 0.5);
        foreach (double Offset in Offsets)
        {
            (double X, double Y) Port = Side is "top" or "bottom"
                ? (CentreX + Offset, CentreY)
                : (CentreX, CentreY + Offset);
            if (Existing == null || Math.Abs(Existing.X - Port.X) >= 1.0 || Math.Abs(Existing.Y - Port.Y) >= 1.0)
            {
                yield return Port;
            }
        }
    }

    private static IEnumerable<List<(double X, double Y)>> CandidatePaths(
        double StartX,
        double StartY,
        FlowchartLayoutNodeBounds Source,
        NodeContract? SourceNode,
        string Exit,
        double EndX,
        double EndY,
        FlowchartLayoutNodeBounds Target,
        NodeContract? TargetNode,
        string Entry,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        FlowchartLayoutEdgeRoute Route,
        DiagramContract Contract,
        FlowchartDirection Direction,
        bool Horizontal)
    {
        yield return [(StartX, StartY), (EndX, StartY), (EndX, EndY)];
        yield return [(StartX, StartY), (StartX, EndY), (EndX, EndY)];
        yield return DiagramGridRouteBuilder.BuildRoute(
            StartX, StartY, Source, SourceNode?.Shape, Exit,
            EndX, EndY, Target, TargetNode?.Shape, Entry,
            Bounds, Route.SourceId, Route.TargetId, Contract, Direction, Horizontal);
    }

    private static bool LeavesBy(IReadOnlyList<(double X, double Y)> Path, string Side)
    {
        if (Path.Count < 2)
        {
            return false;
        }

        double Dx = Path[1].X - Path[0].X;
        double Dy = Path[1].Y - Path[0].Y;
        return Side switch
        {
            "left" => Dx < -0.5 && Math.Abs(Dy) < 0.5,
            "right" => Dx > 0.5 && Math.Abs(Dy) < 0.5,
            "top" => Dy > 0.5 && Math.Abs(Dx) < 0.5,
            _ => Dy < -0.5 && Math.Abs(Dx) < 0.5
        };
    }

    private static bool EntersBy(IReadOnlyList<(double X, double Y)> Path, string Side)
    {
        if (Path.Count < 2)
        {
            return false;
        }

        double Dx = Path[^1].X - Path[^2].X;
        double Dy = Path[^1].Y - Path[^2].Y;
        return Side switch
        {
            "left" => Dx > 0.5 && Math.Abs(Dy) < 0.5,
            "right" => Dx < -0.5 && Math.Abs(Dy) < 0.5,
            "top" => Dy < -0.5 && Math.Abs(Dx) < 0.5,
            _ => Dy > 0.5 && Math.Abs(Dx) < 0.5
        };
    }

    private static bool EndpointTaken(
        FlowchartLayoutResult Layout,
        FlowchartLayoutEdgeRoute Route,
        string NodeId,
        double X,
        double Y) => Layout.EdgeRoutes.Any(Other => !ReferenceEquals(Other, Route) && Other.Points.Count > 0
            && ((Other.SourceId == NodeId && Near(Other.Points[0], X, Y))
                || (Other.TargetId == NodeId && Near(Other.Points[^1], X, Y))));

    private static bool Near(FlowchartLayoutPoint Point, double X, double Y) =>
        Math.Abs(Point.X - X) < 1.0 && Math.Abs(Point.Y - Y) < 1.0;

    private static int CountCorners(IReadOnlyList<FlowchartLayoutPoint> Points) =>
        CountCorners(Points.Select(Point => (Point.X, Point.Y)).ToList());

    private static int CountCorners(IReadOnlyList<(double X, double Y)> Points)
    {
        int Result = 0;
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            bool BeforeHorizontal = Math.Abs(Points[Index].Y - Points[Index - 1].Y) < 0.5;
            bool AfterHorizontal = Math.Abs(Points[Index + 1].Y - Points[Index].Y) < 0.5;
            if (BeforeHorizontal != AfterHorizontal)
            {
                Result++;
            }
        }
        return Result;
    }

    private static (double X, double Y) ResolveEndpoint(
        FlowchartLayoutEdgeRoute Route,
        FlowchartLayoutNodeBounds Bounds,
        string Side,
        bool Source)
    {
        FlowchartLayoutPoint? Existing = Route.Points.Count == 0
            ? null : Source ? Route.Points[0] : Route.Points[^1];
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        if (Existing != null)
        {
            bool OnSide = Side switch
            {
                "left" => Math.Abs(Existing.X - Left) < 1.0,
                "right" => Math.Abs(Existing.X - Right) < 1.0,
                "top" => Math.Abs(Existing.Y - Top) < 1.0,
                "bottom" => Math.Abs(Existing.Y - Bottom) < 1.0,
                _ => false
            };
            if (OnSide)
            {
                return (Existing.X, Existing.Y);
            }
        }

        return Side switch
        {
            "left" => (Left, Bounds.CenterY),
            "right" => (Right, Bounds.CenterY),
            "top" => (Bounds.CenterX, Top),
            "bottom" => (Bounds.CenterX, Bottom),
            _ => (Bounds.CenterX, Bounds.CenterY)
        };
    }
}

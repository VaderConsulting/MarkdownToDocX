using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Builds independently owned, fully routed alternatives shared by bend diagnosis and finished-layout repair.</summary>
internal static class FlowchartLayoutAlternatives
{
    /// <summary>Reports proven rejection causes for focused diagnostics without changing candidate order or acceptance.</summary>
    internal static Action<string>? RejectionObserver;
    /// <summary>A proven alternative and its optional atomic whole-cell move.</summary>
    /// <param name="LineId">Line whose placement or routing fault is removed.</param>
    /// <param name="Layout">Complete candidate model with every unaffected route preserved.</param>
    /// <param name="Move">Prepared immutable cell assignment, not applied during diagnosis.</param>
    /// <param name="Detail">Measured cell move or side reassignment.</param>
    internal sealed record Witness(string LineId, FlowchartLayoutResult Layout, FlowchartCellRepair.Assignment? Move, string Detail);

    /// <summary>Copies mutable routes and their render context; node/frame values remain immutable during all searches.</summary>
    /// <param name="Layout">Read-only completed source.</param>
    /// <returns>A private route model with no shared route or cache content.</returns>
    internal static FlowchartLayoutResult Copy(FlowchartLayoutResult Layout)
    {
        var Result = new FlowchartLayoutResult { UsesGridRoutes = Layout.UsesGridRoutes, RenderContext = new() { UseCellRegions = Layout.RenderContext.UseCellRegions } };
        Result.NodeBoundsMutable.AddRange(Layout.NodeBounds); Result.ClusterBoundsMutable.AddRange(Layout.ClusterBounds);
        foreach (var Route in Layout.EdgeRoutes)
        {
            var New = new FlowchartLayoutEdgeRoute { EdgeId = Route.EdgeId, SourceId = Route.SourceId, TargetId = Route.TargetId,
                ExitSide = Route.ExitSide, EntrySide = Route.EntrySide, ExitNormalX = Route.ExitNormalX, ExitNormalY = Route.ExitNormalY,
                EntryNormalX = Route.EntryNormalX, EntryNormalY = Route.EntryNormalY };
            New.Points.AddRange(Route.Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
            Result.EdgeRoutesMutable.Add(New);
        }
        return Result;
    }

    /// <summary>Tries unhinted whole-cell alignment of a jogged link, never inferring cell regions from pixel centres.</summary>
    /// <param name="Contract">Declared topology and original hints.</param>
    /// <param name="Layout">Completed cell-owned production layout.</param>
    /// <param name="Line">Jogged line to straighten.</param>
    /// <returns>The first fully checked straight alternative, or none.</returns>
    internal static Witness? Straighten(DiagramContract Contract, FlowchartLayoutResult Layout, FlowchartLayoutEdgeRoute Line)
    {
        if (Line.Points.Count < 3 || Layout.CellNodes == null) return null;
        var Nodes = Layout.CellNodes.ToDictionary(Node => Node.Id);
        if (!Nodes.TryGetValue(Line.SourceId, out var Source) || !Nodes.TryGetValue(Line.TargetId, out var Target)
            || Source.CellNode == null || Target.CellNode == null) return null;
        foreach (var Pair in new[] { (Node: Source, Peer: Target), (Node: Target, Peer: Source) })
        {
            if (Pair.Node.PlacementHintLocked) continue;
            var Region = Pair.Node.CellNode!.Region; var Peer = Pair.Peer.CellNode!.Region;
            foreach (var Delta in new[] { (Rows: 0, Columns: Peer.StartColumn - Region.StartColumn), (Rows: Peer.StartRow - Region.StartRow, Columns: 0) })
            {
                if (Delta.Rows == 0 && Delta.Columns == 0 || Math.Abs(Delta.Rows) + Math.Abs(Delta.Columns) > FlowchartLayoutRules.RepairChainDepth) continue;
                var Move = FlowchartCellRepair.PlanMove([Pair.Node], Delta.Rows, Delta.Columns);
                if (Move == null || Layout.CellNodes.Select(Node => Node.CellHintValidator).OfType<Func<IReadOnlyDictionary<string, FlowchartCellRegion>, bool>>()
                    .Distinct().Any(Validate => !Validate(Move.After.Regions))) continue;
                var Grid = Pair.Node.CellNode.Grid;
                var Candidate = Copy(Layout);
                int Index = Candidate.NodeBoundsMutable.FindIndex(Box => Box.NodeId == Pair.Node.Id);
                if (Index < 0) continue;
                var Old = Candidate.NodeBounds[Index];
                var Projected = Pair.Node.CellNode.Bounds(Move.After.Regions[Pair.Node.Id],
                    Contract.Nodes.Single(Node => Node.Id == Pair.Node.Id).GeometryProvider, Move.After);
                double Dx = Projected.CenterX - Old.CenterX, Dy = Projected.CenterY - Old.CenterY;
                Candidate.NodeBoundsMutable[Index] = Projected;
                if (!FitsFixedFrames(Contract, Candidate, Pair.Node.Id)) continue;
                foreach (var Route in Candidate.EdgeRoutes.Where(Route => Route.Points.Count >= 2))
                {
                    if (Route.SourceId == Pair.Node.Id) { Route.Points[0].X += Dx; Route.Points[0].Y += Dy; }
                    if (Route.TargetId == Pair.Node.Id) { Route.Points[^1].X += Dx; Route.Points[^1].Y += Dy; }
                }
                // Moving and choosing the facing ports are one complete option.
                // A preceding one-corner side repair must not prevent a legal
                // zero-corner cell move from being considered.
                var MovedLine = Candidate.EdgeRoutes.Single(Route => Route.EdgeId == Line.EdgeId);
                var SourceBox = Candidate.NodeBounds.Single(Box => Box.NodeId == Line.SourceId);
                var TargetBox = Candidate.NodeBounds.Single(Box => Box.NodeId == Line.TargetId);
                if (Math.Abs(SourceBox.CenterX - TargetBox.CenterX) < 0.001)
                {
                    MovedLine.ExitSide = TargetBox.CenterY > SourceBox.CenterY ? "top" : "bottom";
                    MovedLine.EntrySide = MovedLine.ExitSide == "top" ? "bottom" : "top";
                }
                else if (Math.Abs(SourceBox.CenterY - TargetBox.CenterY) < 0.001)
                {
                    MovedLine.ExitSide = TargetBox.CenterX > SourceBox.CenterX ? "right" : "left";
                    MovedLine.EntrySide = MovedLine.ExitSide == "right" ? "left" : "right";
                }
                var Affected = Candidate.EdgeRoutes.Where(Route => Route.SourceId == Pair.Node.Id || Route.TargetId == Pair.Node.Id).ToHashSet();
                if (!Complete(Contract, Layout, Candidate, Affected, new[] { Line.SourceId, Line.TargetId })) continue;
                var Better = Candidate.EdgeRoutes.Single(Route => Route.EdgeId == Line.EdgeId);
                if (Better.Points.Count != 2 || !NoWorse(Contract, Layout, Candidate, Affected)) continue;
                return new(Line.EdgeId, Candidate, Move, $"move {Pair.Node.Id} by rows={Delta.Rows}, columns={Delta.Columns}");
            }
        }
        return null;
    }

    /// <summary>Finds an internal route for an outside-margin line, including legal side reassignment where necessary.</summary>
    /// <param name="Contract">Unchanged declarations and hints.</param>
    /// <param name="Layout">Completed production model.</param>
    /// <param name="Line">Line with a point outside the node/frame envelope.</param>
    /// <returns>A complete legal interior alternative with no bend or crossing increase, or none.</returns>
    internal static Witness? Interior(DiagramContract Contract, FlowchartLayoutResult Layout, FlowchartLayoutEdgeRoute Line)
    {
        var Boxes = Layout.NodeBounds.Concat(Layout.ClusterBounds).ToArray();
        double Left = Boxes.Min(Box => Box.CenterX - Box.Width / 2), Right = Boxes.Max(Box => Box.CenterX + Box.Width / 2);
        double Low = Boxes.Min(Box => Box.CenterY - Box.Height / 2), High = Boxes.Max(Box => Box.CenterY + Box.Height / 2);
        if (Line.Points.All(Point => Point.X >= Left && Point.X <= Right && Point.Y >= Low && Point.Y <= High)) return null;
        foreach (var Sides in SidePairs(Line))
        {
            var Candidate = Copy(Layout); var Route = Candidate.EdgeRoutes.Single(Route => Route.EdgeId == Line.EdgeId);
            if (DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint?.IsActive == true
                && (Sides.Exit != Route.ExitSide || Sides.Entry != Route.EntrySide)) continue;
            Route.ExitSide = Sides.Exit; Route.EntrySide = Sides.Entry;
            var Affected = new HashSet<FlowchartLayoutEdgeRoute> { Route };
            double Width = Right - Left, Height = High - Low;
            FlowchartLayoutNodeBounds[] Walls = [new() { NodeId = "\0margin:left", CenterX = Left - Width / 2, CenterY = (Low + High) / 2, Width = Width, Height = Height * 3 },
                new() { NodeId = "\0margin:right", CenterX = Right + Width / 2, CenterY = (Low + High) / 2, Width = Width, Height = Height * 3 },
                new() { NodeId = "\0margin:top", CenterX = (Left + Right) / 2, CenterY = High + Height / 2, Width = Width * 3, Height = Height },
                new() { NodeId = "\0margin:bottom", CenterX = (Left + Right) / 2, CenterY = Low - Height / 2, Width = Width * 3, Height = Height }];
            if (!Complete(Contract, Layout, Candidate, Affected, new[] { Line.SourceId, Line.TargetId }, Walls)) continue;
            if (Route.Points.Any(Point => Point.X < Left || Point.X > Right || Point.Y < Low || Point.Y > High)
                || !NoWorse(Contract, Layout, Candidate, Affected)) continue;
            return new(Line.EdgeId, Candidate, null, "route inside the node/frame bounding box");
        }
        return null;
    }

    /// <summary>Identifies a hub arrival on the face opposite a wholly separated source.</summary>
    /// <param name="Layout">Fixed geometry.</param>
    /// <param name="Route">Arrival under review.</param>
    /// <returns>Whether the allocated target face points away from the source.</returns>
    internal static bool FarSide(FlowchartLayoutResult Layout, FlowchartLayoutEdgeRoute Route)
    {
        var Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).GroupBy(Box => Box.NodeId).ToDictionary(Group => Group.Key, Group => Group.First());
        if (!Bounds.TryGetValue(Route.SourceId, out var A) || !Bounds.TryGetValue(Route.TargetId, out var B)) return false;
        return Route.EntrySide switch { "left" => A.CenterX - A.Width / 2 >= B.CenterX + B.Width / 2,
            "right" => A.CenterX + A.Width / 2 <= B.CenterX - B.Width / 2, "top" => A.CenterY + A.Height / 2 <= B.CenterY - B.Height / 2,
            "bottom" => A.CenterY - A.Height / 2 >= B.CenterY + B.Height / 2, _ => false };
    }

    /// <summary>Attempts a facing hub entry under the existing side, slot, hint and global interaction guards.</summary>
    /// <param name="Contract">Original declarations.</param>
    /// <param name="Layout">Completed production model.</param>
    /// <param name="Line">Far-side arrival.</param>
    /// <returns>A complete legal facing alternative, or none.</returns>
    internal static Witness? FacingHub(DiagramContract Contract, FlowchartLayoutResult Layout, FlowchartLayoutEdgeRoute Line)
    {
        if (!FarSide(Layout, Line) || DiagramGridLayout.FindContractEdge(Contract, Line)?.RoutingHint?.IsActive == true) return null;
        var Candidate = Copy(Layout); var Route = Candidate.EdgeRoutes.Single(Route => Route.EdgeId == Line.EdgeId);
        Route.EntrySide = Line.EntrySide switch { "left" => "right", "right" => "left", "top" => "bottom", _ => "top" };
        var Affected = new HashSet<FlowchartLayoutEdgeRoute> { Route };
        // Exchange two misplaced entries together instead of crowding an
        // occupied face and adding a bend to the other line.
        foreach (var Other in Candidate.EdgeRoutes.Where(Other => Other != Route && Other.TargetId == Route.TargetId
            && Other.EntrySide == Route.EntrySide && FarSide(Layout, Other)
            && DiagramGridLayout.FindContractEdge(Contract, Other)?.RoutingHint?.IsActive != true))
        {
            Other.EntrySide = Line.EntrySide;
            if (FarSide(Candidate, Other)) return null;
            Affected.Add(Other);
        }
        return Complete(Contract, Layout, Candidate, Affected, [Line.TargetId]) && NoWorse(Contract, Layout, Candidate, Affected)
            ? new(Line.EdgeId, Candidate, null, "use facing target side " + Route.EntrySide) : null;
    }

    /// <summary>Allocates affected boxes and completes only changed or newly obstructed routes.</summary>
    /// <param name="Contract">Original source.</param>
    /// <param name="Original">Immutable base routes.</param>
    /// <param name="Candidate">Private complete model.</param>
    /// <param name="Affected">Receives every route whose terminal or obstacle changed.</param>
    /// <param name="Boxes">Only boxes eligible for new port assignments.</param>
    /// <param name="Walls">Optional search-only outer barriers.</param>
    /// <returns>Whether every affected line has complete legal geometry within the existing chain bound.</returns>
    private static bool Complete(DiagramContract Contract, FlowchartLayoutResult Original, FlowchartLayoutResult Candidate,
        HashSet<FlowchartLayoutEdgeRoute> Affected, IEnumerable<string> Boxes, FlowchartLayoutNodeBounds[]? Walls = null)
    {
        var Bounds = Candidate.NodeBounds.Concat(Candidate.ClusterBounds).GroupBy(Box => Box.NodeId).ToDictionary(Group => Group.Key, Group => Group.First());
        var Nodes = Contract.Nodes.ToDictionary(Node => Node.Id);
        using var Active = FlowchartRenderContext.Activate(Candidate.RenderContext);
        foreach (string Id in Boxes.Distinct()) DiagramGridLayout.ReallocateBendRepairPorts(Contract, Candidate, Bounds, Nodes, Id);
        var Before = Original.EdgeRoutes.ToDictionary(Route => Route.EdgeId);
        foreach (var Route in Candidate.EdgeRoutes.Where(Route => Route.Points.Count >= 2))
        {
            var Old = Before[Route.EdgeId];
            // A joint side retry must not reset an already straight, legal
            // allocation just to make the remaining slots symmetric (3.2).
            // It may be restored only while both bodies and sides are fixed.
            if (Old.Points.Count == 2 && Route.ExitSide == Old.ExitSide && Route.EntrySide == Old.EntrySide
                && FixedBody(Original, Bounds, Route.SourceId) && FixedBody(Original, Bounds, Route.TargetId))
            {
                Route.Points[0].X = Old.Points[0].X; Route.Points[0].Y = Old.Points[0].Y;
                Route.Points[^1].X = Old.Points[^1].X; Route.Points[^1].Y = Old.Points[^1].Y;
                Route.ExitNormalX = Old.ExitNormalX; Route.ExitNormalY = Old.ExitNormalY;
                Route.EntryNormalX = Old.EntryNormalX; Route.EntryNormalY = Old.EntryNormalY;
            }
            if (Route.ExitSide != Old.ExitSide || Route.EntrySide != Old.EntrySide || Route.Points[0].X != Old.Points[0].X
                || Route.Points[0].Y != Old.Points[0].Y || Route.Points[^1].X != Old.Points[^1].X || Route.Points[^1].Y != Old.Points[^1].Y
                || !FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Route.Points.Select(Point => (Point.X, Point.Y)).ToArray())) Affected.Add(Route);
        }
        if (Affected.Count > FlowchartLayoutRules.RepairChainLines) return false;
        foreach (string Id in Boxes.Distinct())
        {
            var Ports = Candidate.EdgeRoutes.Where(Route => Route.Points.Count >= 2).SelectMany(Route =>
                (Route.SourceId == Id ? new[] { (Side: Route.ExitSide, Point: Route.Points[0]) } : [])
                .Concat(Route.TargetId == Id ? new[] { (Side: Route.EntrySide, Point: Route.Points[^1]) } : [])).ToArray();
            foreach (var Group in Ports.GroupBy(Port => Port.Side))
            {
                var Ends = Group.ToArray();
                for (int A = 0; A < Ends.Length; A++) for (int B = A + 1; B < Ends.Length; B++)
                    if (Math.Sqrt(Math.Pow(Ends[A].Point.X - Ends[B].Point.X, 2) + Math.Pow(Ends[A].Point.Y - Ends[B].Point.Y, 2))
                        < FlowchartLayoutRules.LineSpacing - 0.001) return false;
            }
        }
        var SearchBounds = new Dictionary<string, FlowchartLayoutNodeBounds>(Bounds);
        foreach (var Wall in Walls ?? []) SearchBounds.Add(Wall.NodeId, Wall);
        var Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        foreach (var Route in Affected)
        {
            var Old = Before[Route.EdgeId]; var Hint = DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint;
            if (Hint?.IsActive == true && (Route.ExitSide != Old.ExitSide || Route.EntrySide != Old.EntrySide
                || Route.Points[0].X != Old.Points[0].X || Route.Points[0].Y != Old.Points[0].Y
                || Route.Points[^1].X != Old.Points[^1].X || Route.Points[^1].Y != Old.Points[^1].Y)) return false;
            var A = Route.Points[0]; var B = Route.Points[^1]; Nodes.TryGetValue(Route.SourceId, out var Source); Nodes.TryGetValue(Route.TargetId, out var Target);
            var Path = DiagramGridRouteBuilder.BuildRoute(A.X, A.Y, Bounds[Route.SourceId], Source?.Shape, Route.ExitSide,
                B.X, B.Y, Bounds[Route.TargetId], Target?.Shape, Route.EntrySide, SearchBounds, Route.SourceId, Route.TargetId, Contract,
                Direction, Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft,
                Travel: Hint?.IsActive == true ? Hint.Travel : null, RequireLegal: true,
                ExitNormal: (Route.ExitNormalX, Route.ExitNormalY), EntryNormal: (Route.EntryNormalX, Route.EntryNormalY));
            if (!FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Path)) return false;
            Route.Points.Clear(); Route.Points.AddRange(Path.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
        }
        return true;

    }

    /// <summary>Checks that a preserved straight line still owns the same unchanged connection body.</summary>
    /// <param name="Original">Immutable base layout.</param>
    /// <param name="Bounds">Candidate connection-body bounds.</param>
    /// <param name="Id">Declared endpoint identity.</param>
    /// <returns>Whether the candidate leaves its position and dimensions exact.</returns>
    private static bool FixedBody(FlowchartLayoutResult Original, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, string Id)
    {
        var Box = Bounds[Id];
        var OldBox = Original.NodeBounds.Concat(Original.ClusterBounds).First(Item => Item.NodeId == Id);
        return (Box.CenterX, Box.CenterY, Box.Width, Box.Height) == (OldBox.CenterX, OldBox.CenterY, OldBox.Width, OldBox.Height);
    }

    /// <summary>Confirms complete scenario quality, every affected bend count, global interactions and finding identities.</summary>
    /// <param name="Contract">Original source.</param>
    /// <param name="Original">Base model.</param>
    /// <param name="Candidate">Complete privately routed option.</param>
    /// <param name="Affected">Changed candidate routes.</param>
    /// <returns>Whether the score strictly improves without a geometric or audit regression.</returns>
    private static bool NoWorse(DiagramContract Contract, FlowchartLayoutResult Original, FlowchartLayoutResult Candidate, HashSet<FlowchartLayoutEdgeRoute> Affected)
    {
        var Old = Original.EdgeRoutes.ToDictionary(Route => Route.EdgeId);
        if (Affected.Any(Route => FlowchartLayoutScore.CountCorners(Route.Points) > FlowchartLayoutScore.CountCorners(Old[Route.EdgeId].Points)))
        {
            RejectionObserver?.Invoke("bend increase: " + string.Join(",", Affected.Select(Route => Route.EdgeId + ":"
                + FlowchartLayoutScore.CountCorners(Old[Route.EdgeId].Points) + "->" + FlowchartLayoutScore.CountCorners(Route.Points))));
            return false;
        }
        foreach (var A in Affected) foreach (var B in Candidate.EdgeRoutes.Where(Route => Route != A))
        {
            var Before = FlowchartBranchRouteOrder.Intersections(Old[A.EdgeId].Points.Select(Point => (Point.X, Point.Y)).ToArray(), Old[B.EdgeId].Points.Select(Point => (Point.X, Point.Y)).ToArray());
            var After = FlowchartBranchRouteOrder.Intersections(A.Points.Select(Point => (Point.X, Point.Y)).ToArray(), B.Points.Select(Point => (Point.X, Point.Y)).ToArray());
            if (After.Crossings > Before.Crossings || After.Overlap > Before.Overlap + 0.001)
            {
                RejectionObserver?.Invoke("interaction increase: " + A.EdgeId + " x " + B.EdgeId);
                return false;
            }
        }
        var Reference = FlowchartRouteRuleChecker.Check(Contract, Original);
        var Added = FlowchartRouteRuleChecker.Check(Contract, Candidate).Where(Finding => !Reference.Any(Old => Old.Rule == Finding.Rule && Old.Subject == Finding.Subject)).ToArray();
        if (Added.Length != 0)
        {
            RejectionObserver?.Invoke("finding increase: " + string.Join(",", Added.Select(Finding => Finding.Rule + ":" + Finding.Subject)));
            return false;
        }
        return FlowchartLayoutScore.Measure(Contract, Candidate, false).Cost < FlowchartLayoutScore.Measure(Contract, Original, false).Cost;
    }

    /// <summary>Checks containment against fixed frames before routing, so a checker never proposes unscored frame growth.</summary>
    /// <param name="Contract">Declared group membership.</param>
    /// <param name="Layout">Private moved-node bounds.</param>
    /// <param name="Id">Moved node identity.</param>
    /// <returns>Whether the node remains inside its existing frames below their title strips.</returns>
    private static bool FitsFixedFrames(DiagramContract Contract, FlowchartLayoutResult Layout, string Id)
    {
        var Box = Layout.NodeBounds.Single(Item => Item.NodeId == Id);
        var Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).GroupBy(Item => Item.NodeId).ToDictionary(Group => Group.Key, Group => Group.First());
        foreach (var Frame in Layout.ClusterBounds.Where(Frame => DiagramGridLayout.IsInsideGroup(Contract, Id, Frame.NodeId)))
        {
            var Title = FlowchartFrameTitleBandHelper.Build(Contract, Bounds).Single(Band => Band.GroupId == Frame.NodeId);
            if (Box.CenterX - Box.Width / 2 < Frame.CenterX - Frame.Width / 2 || Box.CenterX + Box.Width / 2 > Frame.CenterX + Frame.Width / 2
                || Box.CenterY - Box.Height / 2 < Frame.CenterY - Frame.Height / 2 || Box.CenterY + Box.Height / 2 > Title.Bottom) return false;
        }
        return true;
    }

    /// <summary>Visits the allocated pair first, then all legal side pairs under the existing candidate count.</summary>
    /// <param name="Route">Current allocated line.</param>
    /// <returns>Deterministic side pairs without duplicate options.</returns>
    private static IEnumerable<(string Exit, string Entry)> SidePairs(FlowchartLayoutEdgeRoute Route) =>
        new[] { (Route.ExitSide, Route.EntrySide) }.Concat(new[] { "top", "right", "bottom", "left" }
            .SelectMany(Exit => new[] { "top", "right", "bottom", "left" }.Select(Entry => (Exit, Entry)))).Distinct().Take(FlowchartLayoutRules.RepairCandidateMoves);
}

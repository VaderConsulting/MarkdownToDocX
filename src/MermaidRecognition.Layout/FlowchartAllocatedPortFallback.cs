using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Constructs an obstacle-avoiding route from exact allocated outline ports and their renderer-owned outward normals.</summary>
internal static class FlowchartAllocatedPortFallback
{
    /// <summary>Immutable obstacle inventory valid only while one candidate's bounds and declared membership stay fixed.</summary>
    /// <param name="Bodies">Ordered node bodies, excluding group frames.</param>
    /// <param name="Obstacles">Bodies plus title bands, before endpoint-specific filtering.</param>
    /// <param name="Bands">Reserved title strips from this exact candidate.</param>
    internal sealed record PreparedGeometry(IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bodies,
        IReadOnlyList<FlowchartLayoutNodeBounds> Obstacles, IReadOnlyList<FlowchartFrameTitleBandHelper.Band> Bands);

    /// <summary>Prepares fixed candidate geometry; callers discard it before another assignment or rollback.</summary>
    /// <param name="Contract">Current declared frames and titles.</param>
    /// <param name="Bounds">Immutable node/frame values in route-search order.</param>
    /// <returns>Private ordered obstacles and title bands; no live cache is changed.</returns>
    internal static PreparedGeometry Prepare(DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        var Bands = FlowchartFrameTitleBandHelper.Build(Contract, Bounds).ToArray();
        var Groups = Contract.Groups.Select(Group => Group.Id).ToHashSet(StringComparer.Ordinal);
        var Bodies = Bounds.Where(Pair => !Groups.Contains(Pair.Key))
            .ToDictionary(Pair => Pair.Key, Pair => Pair.Value, StringComparer.Ordinal);
        // Preserve AddRoutingObstacles order, including replacement of a band
        // already present in the input. No mutable inventory escapes this trial.
        var Obstacles = FlowchartFrameTitleBandHelper.AddRoutingObstacles(Contract, Bounds, Bands);
        foreach (string Group in Groups) Obstacles.Remove(Group);
        return new(Bodies, Obstacles.Values.ToArray(), Bands);
    }

    /// <summary>Searches between normal stubs; cell routes extend empty bounding-box corner stubs before visibility search.</summary>
    /// <param name="Contract">Declared membership and protected frame titles.</param>
    /// <param name="Bounds">Current complete endpoint and obstacle geometry.</param>
    /// <param name="Nodes">Declared endpoint shapes.</param>
    /// <param name="Route">Allocated endpoints, sides and normals; never modified here.</param>
    /// <param name="ExtendEnvelope">Whether authoritative cell routing needs an outward stub at the enclosing box boundary.</param>
    /// <param name="Prepared">Optional geometry scoped to this unchanged candidate.</param>
    /// <returns>An unpublished candidate, or an empty list when the fixed ports cannot be connected.</returns>
    internal static List<(double X, double Y)> Build(DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, IReadOnlyDictionary<string, NodeContract> Nodes,
        FlowchartLayoutEdgeRoute Route, bool ExtendEnvelope, PreparedGeometry? Prepared = null)
    {
        if (Route.Points.Count < 2 || !Bounds.TryGetValue(Route.SourceId, out var Source)
            || !Bounds.TryGetValue(Route.TargetId, out var Target)) return [];
        var Saved = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
        var WithBands = Prepared == null ? FlowchartFrameTitleBandHelper.AddRoutingObstacles(Contract, Bounds) : null;
        if (WithBands != null) foreach (var Group in Contract.Groups) WithBands.Remove(Group.Id);
        Nodes.TryGetValue(Route.SourceId, out var SourceNode);
        Nodes.TryGetValue(Route.TargetId, out var TargetNode);
        // Preserve the established diamond fallback policy. All candidates are
        // independently checked against their actual endpoint bodies before use.
        IEnumerable<FlowchartLayoutNodeBounds> Inventory = Prepared != null ? Prepared.Obstacles : WithBands!.Values;
        var Obstacles = Inventory.Where(Box =>
            !(Box.NodeId == Route.SourceId && FlowchartDiamondAttachHelper.IsDiamondShape(SourceNode?.Shape))
            && !(Box.NodeId == Route.TargetId && FlowchartDiamondAttachHelper.IsDiamondShape(TargetNode?.Shape))).ToArray();
        double Stub = DiagramGridRouteBuilder.CORNER_RADIUS_PX;
        var SourceStub = DiagramGridRouteBuilder.ResolvePortStub(Saved[0].X, Saved[0].Y,
            Source, SourceNode?.Shape, Route.ExitSide, Stub, Route.ExitNormalX, Route.ExitNormalY);
        var TargetStub = DiagramGridRouteBuilder.ResolvePortStub(Saved[^1].X, Saved[^1].Y,
            Target, TargetNode?.Shape, Route.EntrySide, Stub, Route.EntryNormalX, Route.EntryNormalY);
        if (ExtendEnvelope)
        {
            SourceStub = ExtendStub(Source, Saved[0], SourceStub);
            TargetStub = ExtendStub(Target, Saved[^1], TargetStub);
        }
        if (!FlowchartPaddedVisibilityRouter.TryRoute(SourceStub.X, SourceStub.Y,
            TargetStub.X, TargetStub.Y, Obstacles, null, null, out var Body,
            PortStubPx: 0, ObstaclePaddingPx: 0)) return [];
        return DiagramGridRouteBuilder.SimplifyCollinear(
            new List<(double X, double Y)> { Saved[0] }.Concat(Body).Append(Saved[^1]).ToList());
    }

    /// <summary>Checks complete candidate geometry before publication, without depending on the final audit checker.</summary>
    /// <param name="Contract">Group membership and title obstacles.</param>
    /// <param name="Bounds">Actual current renderer bodies.</param>
    /// <param name="Route">Allocated link supplying endpoint identities and normals.</param>
    /// <param name="Path">Complete unpublished candidate.</param>
    /// <param name="Prepared">Optional obstacle geometry scoped to this unchanged candidate and discarded before rollback.</param>
    /// <returns>Whether terminals retain their normals, interior runs are orthogonal, and no body or title band is entered illegally.</returns>
    internal static bool IsLegal(DiagramContract Contract, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        FlowchartLayoutEdgeRoute Route, IReadOnlyList<(double X, double Y)> Path, PreparedGeometry? Prepared = null)
    {
        if (Path.Count < 2 || !Bounds.TryGetValue(Route.SourceId, out var Source)
            || !Bounds.TryGetValue(Route.TargetId, out var Target)) return false;
        if (!AlongNormal(Path[0], Path[1], Route.ExitNormalX, Route.ExitNormalY)
            || !AlongNormal(Path[^1], Path[^2], Route.EntryNormalX, Route.EntryNormalY)) return false;
        var Bodies = Prepared?.Bodies ?? Bounds.Where(Pair => !Contract.Groups.Any(Group => Group.Id == Pair.Key))
            .ToDictionary(Pair => Pair.Key, Pair => Pair.Value, StringComparer.Ordinal);
        for (int Index = 1; Index < Path.Count; Index++)
        {
            var A = Path[Index - 1]; var B = Path[Index];
            if (!double.IsFinite(A.X) || !double.IsFinite(A.Y) || !double.IsFinite(B.X) || !double.IsFinite(B.Y)) return false;
            if (Index > 1 && Index < Path.Count - 1
                && !FlowchartPerpendicularRouteBuilder.IsOrthogonalPolyline(new[] { A, B })) return false;
            if (FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(A.X, A.Y, B.X, B.Y, Bodies, Route.SourceId, Route.TargetId)
                || Index > 1 && Bodies.ContainsKey(Route.SourceId) && FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Source)
                || Index < Path.Count - 1 && Bodies.ContainsKey(Route.TargetId) && FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Target)) return false;
        }
        return !FlowchartFrameTitleBandHelper.RouteEntersAny(Path, Prepared?.Bands ?? FlowchartFrameTitleBandHelper.Build(Contract, Bounds), Route.SourceId, Route.TargetId, Contract);
    }

    /// <summary>Tests the outward ray in model units; missing normal metadata retains the caller's legacy terminal policy.</summary>
    /// <param name="Port">Exact outline point.</param>
    /// <param name="Next">Adjacent candidate point outside the endpoint.</param>
    /// <param name="Nx">Allocated normal X component.</param>
    /// <param name="Ny">Allocated normal Y component.</param>
    /// <returns>Whether the segment follows the outward normal within the existing route tolerance.</returns>
    private static bool AlongNormal((double X, double Y) Port, (double X, double Y) Next, double Nx, double Ny)
    {
        double Length = Math.Sqrt(Nx * Nx + Ny * Ny);
        if (Length == 0) return true;
        double Dx = Next.X - Port.X, Dy = Next.Y - Port.Y;
        return Dx * Nx + Dy * Ny > 0 && Math.Abs(Dx * Ny - Dy * Nx) / Length <= DiagramGridRouteBuilder.COLLINEAR_EPSILON_PX;
    }

    /// <summary>Extends an outward ray through the empty corner of an enclosing box without crossing the actual endpoint body.</summary>
    /// <param name="Box">Renderer-owned endpoint geometry.</param>
    /// <param name="Port">Allocated outline point.</param>
    /// <param name="Stub">Existing normal stub.</param>
    /// <returns>A candidate at the enclosing box boundary, or the original stub when extension is unnecessary or unsafe.</returns>
    private static (double X, double Y) ExtendStub(FlowchartLayoutNodeBounds Box, (double X, double Y) Port, (double X, double Y) Stub)
    {
        double Left = Box.CenterX - Box.Width / 2, Right = Box.CenterX + Box.Width / 2;
        double Low = Box.CenterY - Box.Height / 2, High = Box.CenterY + Box.Height / 2;
        if (Stub.X <= Left || Stub.X >= Right || Stub.Y <= Low || Stub.Y >= High) return Stub;
        double Dx = Stub.X - Port.X, Dy = Stub.Y - Port.Y;
        double AlongX = Dx == 0 ? double.PositiveInfinity : ((Dx < 0 ? Left : Right) - Port.X) / Dx;
        double AlongY = Dy == 0 ? double.PositiveInfinity : ((Dy < 0 ? Low : High) - Port.Y) / Dy;
        double Scale = Math.Min(AlongX, AlongY);
        if (!double.IsFinite(Scale) || Scale <= 1) return Stub;
        var End = (X: Port.X + Scale * Dx, Y: Port.Y + Scale * Dy);
        return Box.Geometry?.ObstacleGeometry.IntersectsSegmentInterior(Port.X - Box.CenterX, Box.CenterY - Port.Y,
            End.X - Box.CenterX, Box.CenterY - End.Y) == true ? Stub : End;
    }
}

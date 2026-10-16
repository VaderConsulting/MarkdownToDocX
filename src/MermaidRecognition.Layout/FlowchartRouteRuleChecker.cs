using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Checks routing rules against the allocated ports and stored model routes.</summary>
public static class FlowchartRouteRuleChecker
{
    private const double Tolerance = 1.0;

    /// <summary>One rulebook finding for a port or route.</summary>
    public sealed record Finding(string Rule, string Subject, string Description);

    /// <summary>Checks model geometry after route construction, without inspecting rendered SVG.</summary>
    public static IReadOnlyList<Finding> Check(DiagramContract contract, FlowchartLayoutResult layout)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(layout);
        List<Finding> findings = [];
        Dictionary<string, FlowchartLayoutNodeBounds> boxes = layout.NodeBounds
            .Concat(layout.ClusterBounds).GroupBy(box => box.NodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, NodeContract> nodes = contract.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        List<EdgeContract> edges = contract.Edges
            .Where(edge => !string.Equals(edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();

        List<(EdgeContract Edge, FlowchartLayoutEdgeRoute Route)> matched = [];
        HashSet<FlowchartLayoutEdgeRoute> used = [];
        foreach (EdgeContract edge in edges)
        {
            FlowchartLayoutEdgeRoute? route = FindRoute(edge, layout.EdgeRoutes, used);
            if (route == null || route.Points.Count < 2)
            {
                findings.Add(new Finding("3.1", EdgeName(edge), "visible edge has no stored route with two endpoints"));
                continue;
            }
            used.Add(route);
            matched.Add((edge, route));
            CheckTerminal(edge, route, true, boxes, nodes, findings);
            CheckTerminal(edge, route, false, boxes, nodes, findings);
            CheckOrthogonal(edge, route, nodes, boxes, findings);
            CheckAlignedStraight(edge, route, findings);
            CheckSideChoice(edge, route, contract, boxes, findings);
            CheckFrameRun(edge, route, contract, boxes, findings);
            CheckObstacles(edge, route, contract, layout, boxes, nodes, findings);
        }

        foreach (var Route in layout.EdgeRoutes.Where(Route => !used.Contains(Route)))
            if (!contract.Edges.Any(Edge => string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    && Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId
                    && (string.IsNullOrEmpty(Edge.Id) || Edge.Id == Route.EdgeId)))
                findings.Add(new("10", Route.EdgeId, "stored path has no unmatched declared visible link"));

        CheckPortUniqueness(matched, findings);
        CheckPortDistribution(matched, boxes, nodes, findings);
        CheckPortOrder(matched, boxes, nodes, findings);
        CheckCrossings(matched, findings);
        for (int Left = 0; Left < matched.Count; Left++)
        for (int Right = Left + 1; Right < matched.Count; Right++)
        {
            var A = matched[Left]; var B = matched[Right];
            if (FlowchartBranchRouteOrder.Intersections(
                    A.Route.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                    B.Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()).Overlap > 0.001)
                findings.Add(new("5.1", EdgeName(A.Edge) + " x " + EdgeName(B.Edge), "routes share a positive-length track"));
        }
        return findings;
    }

    /// <summary>Checks every final segment against actual node interiors and protected title strips.</summary>
    /// <param name="Edge">The declared link represented by the route.</param>
    /// <param name="Route">The final stored polyline.</param>
    /// <param name="Contract">Group membership and link declarations.</param>
    /// <param name="Layout">Node outlines to test, excluding non-solid group frames.</param>
    /// <param name="Boxes">Node and frame bounds used to derive title strips.</param>
    /// <param name="Nodes">Shape definitions for exact diamond and circular outlines.</param>
    /// <param name="Findings">Receives hard legality failures, independently of hints.</param>
    private static void CheckObstacles(EdgeContract Edge, FlowchartLayoutEdgeRoute Route,
        DiagramContract Contract, FlowchartLayoutResult Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Boxes,
        IReadOnlyDictionary<string, NodeContract> Nodes, List<Finding> Findings)
    {
        foreach (var Box in Layout.NodeBounds)
        {
            Nodes.TryGetValue(Box.NodeId, out var Node);
            bool Blocked = false;
            for (int Index = 1; Index < Route.Points.Count && !Blocked; Index++)
            {
                var A = Route.Points[Index - 1]; var B = Route.Points[Index];
                if (FlowchartDiamondAttachHelper.IsDiamondShape(Node?.Shape))
                {
                    double Ratio = Box.Width / Box.Height;
                    double Ax = A.X - Box.CenterX, Ay = (A.Y - Box.CenterY) * Ratio;
                    double Bx = B.X - Box.CenterX, By = (B.Y - Box.CenterY) * Ratio;
                    // The intersection primitive includes its boundary. Inset
                    // by the same attachment tolerance as rectangular bodies so
                    // a terminal on the outline is not an interior collision.
                    double Half = Math.Max(0.001, Box.Width / 2 - 2 * Math.Max(1, Ratio));
                    Blocked = FlowchartRouteObstacleHelper.SegmentIntersectsRectangleInterior(
                        Ax + Ay, Ax - Ay, Bx + By, Bx - By,
                        -Half, Half, Half, -Half);
                }
                else if (Node?.Shape is "circle" or "doublecircle" or "ellipse")
                {
                    double Rx = Node.Shape == "ellipse" ? Box.Width / 2 : Math.Min(Box.Width, Box.Height) / 2;
                    double Ry = Node.Shape == "ellipse" ? Box.Height / 2 : Rx;
                    double Ax = (A.X - Box.CenterX) / Rx, Ay = (A.Y - Box.CenterY) / Ry;
                    double Dx = (B.X - A.X) / Rx, Dy = (B.Y - A.Y) / Ry;
                    double Denominator = Dx * Dx + Dy * Dy;
                    double T = Denominator == 0 ? 0 : Math.Clamp(-(Ax * Dx + Ay * Dy) / Denominator, 0, 1);
                    Blocked = Math.Pow(Ax + T * Dx, 2) + Math.Pow(Ay + T * Dy, 2) < 1 - 0.000001;
                }
                else Blocked = FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Box);
            }
            if (Blocked) Findings.Add(new("10", EdgeName(Edge), $"route passes through node '{Box.NodeId}'"));
        }
        if (FlowchartFrameTitleBandHelper.RouteEntersAny(
                Route.Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                FlowchartFrameTitleBandHelper.Get(Contract, Boxes), Route.SourceId, Route.TargetId, Contract))
            Findings.Add(new("10", EdgeName(Edge), "route passes through a group title band"));
    }

    private static FlowchartLayoutEdgeRoute? FindRoute(EdgeContract edge,
        IReadOnlyList<FlowchartLayoutEdgeRoute> routes, HashSet<FlowchartLayoutEdgeRoute> used)
    {
        if (!string.IsNullOrEmpty(edge.Id))
        {
            FlowchartLayoutEdgeRoute? byId = routes.FirstOrDefault(route => !used.Contains(route)
                && string.Equals(route.EdgeId, edge.Id, StringComparison.Ordinal)
                && route.SourceId == edge.SourceId && route.TargetId == edge.TargetId);
            if (byId != null) return byId;
        }
        return routes.FirstOrDefault(route => !used.Contains(route)
            && string.Equals(route.SourceId, edge.SourceId, StringComparison.Ordinal)
            && string.Equals(route.TargetId, edge.TargetId, StringComparison.Ordinal));
    }

    private static void CheckTerminal(EdgeContract edge, FlowchartLayoutEdgeRoute route, bool source,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> boxes,
        IReadOnlyDictionary<string, NodeContract> nodes, List<Finding> findings)
    {
        string id = source ? edge.SourceId : edge.TargetId;
        string side = source ? route.ExitSide : route.EntrySide;
        FlowchartLayoutPoint point = source ? route.Points[0] : route.Points[^1];
        if (!boxes.TryGetValue(id, out FlowchartLayoutNodeBounds? box))
        {
            findings.Add(new Finding("3.1", EdgeName(edge), $"endpoint box '{id}' has no layout bounds"));
            return;
        }
        nodes.TryGetValue(id, out NodeContract? node);
        bool onOutline = IsOnOutline(point, box, node?.Shape, side);
        if (!onOutline)
            findings.Add(new Finding("3.1", EdgeName(edge), $"{(source ? "source" : "target")} port does not end on the {side} outline of '{id}'"));
        if (route.Points.Count > 1)
        {
            FlowchartLayoutPoint neighbor = source ? route.Points[1] : route.Points[^2];
            bool diamond = FlowchartDiamondAttachHelper.IsDiamondShape(node?.Shape);
            bool circle = IsCircleShape(node?.Shape);
            (double NormalX, double NormalY) normal = diamond || circle
                ? StoredNormal(route, source, box, point)
                : default;
            double dx = neighbor.X - point.X, dy = neighbor.Y - point.Y;
            bool perpendicular = diamond || circle
                ? IsAlongDiamondNormal(dx, dy, normal.NormalX, normal.NormalY)
                : side switch
                {
                    "left" or "right" => Math.Abs(point.Y - neighbor.Y) <= Tolerance,
                    "top" or "bottom" => Math.Abs(point.X - neighbor.X) <= Tolerance,
                    _ => false
                };
            if (!perpendicular)
                findings.Add(new Finding("3.1", EdgeName(edge), $"terminal segment does not leave '{id}' square-on from its {side} port"));
        }
    }

    private static bool IsOnOutline(FlowchartLayoutPoint point, FlowchartLayoutNodeBounds box, string? shape, string side)
    {
        double left = box.CenterX - box.Width / 2, right = box.CenterX + box.Width / 2;
        double bottom = box.CenterY - box.Height / 2, top = box.CenterY + box.Height / 2;
        if (FlowchartDagrePresentation.IsDiamondShape(shape))
        {
            double halfWidth = box.Width / 2.0, halfHeight = box.Height / 2.0;
            if (halfWidth <= 0 || halfHeight <= 0) return false;
            double equation = Math.Abs(point.X - box.CenterX) / halfWidth
                + Math.Abs(point.Y - box.CenterY) / halfHeight;
            return Math.Abs(equation - 1.0) <= Tolerance / Math.Max(1.0, Math.Min(halfWidth, halfHeight));
        }
        if (IsCircleShape(shape))
        {
            double radius = Math.Min(box.Width, box.Height) / 2.0;
            double distance = Math.Sqrt(Math.Pow(point.X - box.CenterX, 2) + Math.Pow(point.Y - box.CenterY, 2));
            return Math.Abs(distance - radius) <= Tolerance;
        }
        return side switch
        {
            "left" => Math.Abs(point.X - left) <= Tolerance && point.Y >= bottom - Tolerance && point.Y <= top + Tolerance,
            "right" => Math.Abs(point.X - right) <= Tolerance && point.Y >= bottom - Tolerance && point.Y <= top + Tolerance,
            "top" => Math.Abs(point.Y - top) <= Tolerance && point.X >= left - Tolerance && point.X <= right + Tolerance,
            "bottom" => Math.Abs(point.Y - bottom) <= Tolerance && point.X >= left - Tolerance && point.X <= right + Tolerance,
            _ => false
        };
    }

    private static void CheckOrthogonal(EdgeContract edge, FlowchartLayoutEdgeRoute route,
        IReadOnlyDictionary<string, NodeContract> nodes,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> boxes, List<Finding> findings)
    {
        for (int i = 1; i < route.Points.Count; i++)
        {
            FlowchartLayoutPoint a = route.Points[i - 1], b = route.Points[i];
            if (Math.Abs(a.X - b.X) > Tolerance && Math.Abs(a.Y - b.Y) > Tolerance)
            {
                bool sourceCurved = i == 1 && IsCurvedOutline(edge.SourceId);
                bool targetCurved = i == route.Points.Count - 1 && IsCurvedOutline(edge.TargetId);
                bool validSource = sourceCurved && boxes.TryGetValue(edge.SourceId, out FlowchartLayoutNodeBounds? sourceBox)
                    && IsAlongDiamondNormal(b.X - a.X, b.Y - a.Y,
                        StoredNormal(route, true, sourceBox, a).NormalX, StoredNormal(route, true, sourceBox, a).NormalY);
                bool validTarget = targetCurved && boxes.TryGetValue(edge.TargetId, out FlowchartLayoutNodeBounds? targetBox)
                    && IsAlongDiamondNormal(a.X - b.X, a.Y - b.Y,
                        StoredNormal(route, false, targetBox, b).NormalX, StoredNormal(route, false, targetBox, b).NormalY);
                if ((sourceCurved || targetCurved) && (!sourceCurved || validSource) && (!targetCurved || validTarget))
                    continue;
                findings.Add(new Finding("4.1", EdgeName(edge), "route contains a diagonal segment"));
                return;
            }
        }

        bool IsCurvedOutline(string id) => nodes.TryGetValue(id, out NodeContract? node)
            && (FlowchartDiamondAttachHelper.IsDiamondShape(node.Shape) || IsCircleShape(node.Shape));
    }

    private static (double NormalX, double NormalY) StoredNormal(FlowchartLayoutEdgeRoute route, bool source,
        FlowchartLayoutNodeBounds box, FlowchartLayoutPoint point)
    {
        double nx = source ? route.ExitNormalX : route.EntryNormalX;
        double ny = source ? route.ExitNormalY : route.EntryNormalY;
        if (Math.Sqrt(nx * nx + ny * ny) > 1e-6) return (nx, ny);
        return FlowchartDiamondAttachHelper.TryResolveOutwardNormal(box, point.X, point.Y, out nx, out ny)
            ? (nx, ny) : (0, 0);
    }

    private static bool IsAlongDiamondNormal(double dx, double dy, double nx, double ny)
    {
        double length = Math.Sqrt(dx * dx + dy * dy), normalLength = Math.Sqrt(nx * nx + ny * ny);
        if (length <= Tolerance || normalLength <= 1e-6) return false;
        return ((dx * nx) + (dy * ny)) / (length * normalLength) >= 0.999;
    }

    private static bool IsCircleShape(string? shape) =>
        string.Equals(shape, "circle", StringComparison.OrdinalIgnoreCase)
        || string.Equals(shape, "doublecircle", StringComparison.OrdinalIgnoreCase);

    private static void CheckAlignedStraight(EdgeContract edge, FlowchartLayoutEdgeRoute route, List<Finding> findings)
    {
        FlowchartLayoutPoint a = route.Points[0], b = route.Points[^1];
        if ((Math.Abs(a.X - b.X) <= Tolerance || Math.Abs(a.Y - b.Y) <= Tolerance)
            && route.Points.Count != 2 && edge.RoutingHint == null)
            findings.Add(new Finding("4.2", EdgeName(edge), "aligned ports are not connected by one straight run"));
    }

    private static void CheckPortUniqueness(List<(EdgeContract Edge, FlowchartLayoutEdgeRoute Route)> routes, List<Finding> findings)
    {
        var ports = routes.SelectMany(pair => new[]
        {
            (Node: pair.Edge.SourceId, Side: pair.Route.ExitSide, Point: pair.Route.Points[0], Edge: pair.Edge),
            (Node: pair.Edge.TargetId, Side: pair.Route.EntrySide, Point: pair.Route.Points[^1], Edge: pair.Edge)
        });
        foreach (var group in ports.GroupBy(port => (port.Node, port.Side)))
        {
            var duplicate = group.GroupBy(port => (X: Math.Round(port.Point.X, 1), Y: Math.Round(port.Point.Y, 1)))
                .FirstOrDefault(same => same.Count() > 1);
            if (duplicate != null)
                findings.Add(new Finding("3.1", group.Key.Node, $"{duplicate.Count()} lines share the {group.Key.Side} port"));
        }
    }

    private static void CheckPortDistribution(List<(EdgeContract Edge, FlowchartLayoutEdgeRoute Route)> routes,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> boxes,
        IReadOnlyDictionary<string, NodeContract> nodes, List<Finding> findings)
    {
        var ports = routes.SelectMany(pair => new[]
        {
            (Node: pair.Edge.SourceId, Side: pair.Route.ExitSide, Point: pair.Route.Points[0], Route: pair.Route, Source: true),
            (Node: pair.Edge.TargetId, Side: pair.Route.EntrySide, Point: pair.Route.Points[^1], Route: pair.Route, Source: false)
        });
        foreach (var group in ports.GroupBy(port => (port.Node, PortGroup: PortGroupKey(port.Node, port.Side, port.Route, port.Source, nodes))))
        {
            if (group.Count() < 2 || !boxes.TryGetValue(group.Key.Node, out FlowchartLayoutNodeBounds? box)) continue;
            bool diamond = nodes.TryGetValue(group.Key.Node, out NodeContract? node)
                && FlowchartDiamondAttachHelper.IsDiamondShape(node.Shape);
            bool circle = nodes.TryGetValue(group.Key.Node, out node) && IsCircleShape(node.Shape);
            var ordered = group.OrderBy(port => diamond
                ? DiamondPortPosition(port.Point, box, port.Route, port.Source)
                : circle ? CirclePortPosition(port.Point, box, port.Side)
                    : PortPosition(port.Point, box, port.Side)).ToArray();
            for (int i = 0; i < ordered.Length; i++)
            {
                double expected = (i + 1.0) / (ordered.Length + 1.0);
                double actual = diamond
                    ? DiamondPortPosition(ordered[i].Point, box, ordered[i].Route, ordered[i].Source)
                    : circle ? CirclePortPosition(ordered[i].Point, box, ordered[i].Side)
                        : PortPosition(ordered[i].Point, box, ordered[i].Side);
                (double startX, double startY, double endX, double endY) = diamond
                    ? DiamondEdge(group.Key.PortGroup, box) : default;
                double distributionError = diamond
                    ? Math.Abs(actual - expected) * Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2))
                    : circle
                        ? Math.Abs(actual - expected) * Math.PI * Math.Min(box.Width, box.Height) / 2.0
                    : Math.Abs(actual - expected) * Math.Max(box.Width, box.Height);
                if (distributionError > Tolerance)
                {
                    findings.Add(new Finding("3.2/4.7", group.Key.Node, $"{group.Key.PortGroup} port {i + 1}/{ordered.Length} is at {actual:0.###}, expected {expected:0.###}"));
                    break;
                }
            }
        }
    }

    private static string PortGroupKey(string nodeId, string side, FlowchartLayoutEdgeRoute route, bool source,
        IReadOnlyDictionary<string, NodeContract> nodes)
    {
        if (!nodes.TryGetValue(nodeId, out NodeContract? node) || !FlowchartDiamondAttachHelper.IsDiamondShape(node.Shape))
            return side;
        double nx = source ? route.ExitNormalX : route.EntryNormalX;
        double ny = source ? route.ExitNormalY : route.EntryNormalY;
        if (Math.Abs(nx) < 0.001 || Math.Abs(ny) < 0.001) return "diamond-tip-" + side;
        return "diamond-" + (nx >= 0 ? "+" : "-") + (ny >= 0 ? "+" : "-");
    }

    private static double DiamondPortPosition(FlowchartLayoutPoint point, FlowchartLayoutNodeBounds box,
        FlowchartLayoutEdgeRoute route, bool source)
    {
        double nx = source ? route.ExitNormalX : route.EntryNormalX;
        double ny = source ? route.ExitNormalY : route.EntryNormalY;
        bool rightHalf = nx >= 0;
        bool topHalf = ny >= 0;
        double sx = rightHalf ? box.CenterX : box.CenterX;
        double sy = topHalf ? box.CenterY + box.Height / 2 : box.CenterY - box.Height / 2;
        double ex = rightHalf ? box.CenterX + box.Width / 2 : box.CenterX - box.Width / 2;
        double ey = topHalf ? box.CenterY : box.CenterY;
        double vx = ex - sx, vy = ey - sy;
        double lengthSquared = (vx * vx) + (vy * vy);
        if (lengthSquared <= 1e-9) return 0.5;
        return Math.Clamp((((point.X - sx) * vx) + ((point.Y - sy) * vy)) / lengthSquared, 0.0, 1.0);
    }

    private static double CirclePortPosition(FlowchartLayoutPoint point, FlowchartLayoutNodeBounds box, string side)
    {
        double angle = Math.Atan2(point.Y - box.CenterY, point.X - box.CenterX);
        return side switch
        {
            "top" => (Math.PI - angle) / Math.PI,
            "bottom" => ((angle < 0 ? angle + (2 * Math.PI) : angle) - Math.PI) / Math.PI,
            "left" => ((angle < Math.PI / 2 ? angle + (2 * Math.PI) : angle) - (Math.PI / 2)) / Math.PI,
            _ => ((Math.PI / 2) - angle) / Math.PI
        };
    }

    private static double PortPosition(FlowchartLayoutPoint point, FlowchartLayoutNodeBounds box, string side) => side switch
    {
        "left" or "right" => (point.Y - (box.CenterY - box.Height / 2)) / box.Height,
        "top" or "bottom" => (point.X - (box.CenterX - box.Width / 2)) / box.Width,
        _ => 0.5
    };

    private static void CheckPortOrder(List<(EdgeContract Edge, FlowchartLayoutEdgeRoute Route)> routes,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> boxes,
        IReadOnlyDictionary<string, NodeContract> nodes, List<Finding> findings)
    {
        var ports = routes.SelectMany(pair => new[]
        {
            (Node: pair.Edge.SourceId, Side: pair.Route.ExitSide, Peer: pair.Edge.TargetId, Point: pair.Route.Points[0], Route: pair.Route, Source: true),
            (Node: pair.Edge.TargetId, Side: pair.Route.EntrySide, Peer: pair.Edge.SourceId, Point: pair.Route.Points[^1], Route: pair.Route, Source: false)
        });
        foreach (var group in ports.GroupBy(port => (port.Node, PortGroup: PortGroupKey(port.Node, port.Side, port.Route, port.Source, nodes))))
        {
            if (!boxes.TryGetValue(group.Key.Node, out FlowchartLayoutNodeBounds? box)) continue;
            bool diamond = nodes.TryGetValue(group.Key.Node, out NodeContract? node)
                && FlowchartDiamondAttachHelper.IsDiamondShape(node.Shape);
            bool circle = nodes.TryGetValue(group.Key.Node, out node) && IsCircleShape(node.Shape);
            var ordered = group.OrderBy(port => diamond
                ? DiamondPortPosition(port.Point, box, port.Route, port.Source)
                : circle ? CirclePortPosition(port.Point, box, port.Side)
                    : PortPosition(port.Point, box, port.Side)).ToArray();
            for (int i = 1; i < ordered.Length; i++)
            {
                if (!boxes.TryGetValue(ordered[i - 1].Peer, out FlowchartLayoutNodeBounds? a)
                    || !boxes.TryGetValue(ordered[i].Peer, out FlowchartLayoutNodeBounds? b)) continue;
                double peerA, peerB;
                if (diamond)
                {
                    peerA = DiamondPeerPosition(a, box, group.Key.PortGroup);
                    peerB = DiamondPeerPosition(b, box, group.Key.PortGroup);
                }
                else
                {
                    peerA = group.Key.PortGroup is "left" or "right" ? a.CenterY : a.CenterX;
                    peerB = group.Key.PortGroup is "left" or "right" ? b.CenterY : b.CenterX;
                }
                if (peerA > peerB + Tolerance)
                {
                    findings.Add(new Finding("3.3/5.2", group.Key.Node, $"{group.Key.PortGroup} ports are out of order relative to their peer nodes"));
                    break;
                }
            }
        }
    }

    private static (double StartX, double StartY, double EndX, double EndY) DiamondEdge(string portGroup, FlowchartLayoutNodeBounds box)
    {
        double halfWidth = box.Width / 2, halfHeight = box.Height / 2;
        return portGroup switch
        {
            "diamond-++" => (box.CenterX, box.CenterY + halfHeight, box.CenterX + halfWidth, box.CenterY),
            "diamond-+-" => (box.CenterX + halfWidth, box.CenterY, box.CenterX, box.CenterY - halfHeight),
            "diamond---" => (box.CenterX, box.CenterY - halfHeight, box.CenterX - halfWidth, box.CenterY),
            "diamond--+" => (box.CenterX - halfWidth, box.CenterY, box.CenterX, box.CenterY + halfHeight),
            _ => (box.CenterX, box.CenterY, box.CenterX, box.CenterY)
        };
    }

    private static double DiamondPeerPosition(FlowchartLayoutNodeBounds peer, FlowchartLayoutNodeBounds diamond, string portGroup)
    {
        double horizontal = Math.Abs(peer.CenterX - diamond.CenterX) / Math.Max(1.0, diamond.Width / 2.0);
        double vertical = Math.Abs(peer.CenterY - diamond.CenterY) / Math.Max(1.0, diamond.Height / 2.0);
        double total = horizontal + vertical;
        if (total <= 1e-9) return 0.5;
        return portGroup is "diamond-++" or "diamond---" ? horizontal / total : vertical / total;
    }

    private static void CheckSideChoice(EdgeContract edge, FlowchartLayoutEdgeRoute route, DiagramContract contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> boxes, List<Finding> findings)
    {
        if (edge.RoutingHint != null) return;
        if (!boxes.TryGetValue(edge.SourceId, out FlowchartLayoutNodeBounds? source)
            || !boxes.TryGetValue(edge.TargetId, out FlowchartLayoutNodeBounds? target)) return;
        int fanOut = contract.Edges.Count(item => item.SourceId == edge.SourceId && item.Stroke != "invisible");
        int fanIn = contract.Edges.Count(item => item.TargetId == edge.TargetId && item.Stroke != "invisible");
        if (fanOut > 1)
        {
            string expected = FacingSide(source, target);
            if (route.ExitSide != expected)
                findings.Add(new Finding("5.8", EdgeName(edge), $"fan line leaves {route.ExitSide}, not the facing {expected} side"));
        }
        if (fanIn > 1)
        {
            string expected = FacingSide(target, source);
            if (route.EntrySide != expected)
                findings.Add(new Finding("4.11", EdgeName(edge), $"converging line enters {route.EntrySide}, not the facing {expected} side"));
        }
        if (contract.Groups.Any(group => group.Id == edge.SourceId || group.Id == edge.TargetId))
        {
            if (route.Points.Count >= 2)
            {
                FlowchartLayoutPoint first = route.Points[0], next = route.Points[1];
                FlowchartLayoutPoint last = route.Points[^1], before = route.Points[^2];
                if (edge.SourceId != edge.TargetId && (Math.Abs(first.X - next.X) <= Tolerance && Math.Abs(first.Y - next.Y) <= Tolerance
                    || Math.Abs(last.X - before.X) <= Tolerance && Math.Abs(last.Y - before.Y) <= Tolerance))
                    findings.Add(new Finding("4.9", EdgeName(edge), "frame connection has a zero-length or non-square terminal run"));
            }
        }
    }

    private static string FacingSide(FlowchartLayoutNodeBounds from, FlowchartLayoutNodeBounds to)
    {
        double dx = to.CenterX - from.CenterX, dy = to.CenterY - from.CenterY;
        return Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? "right" : "left") : (dy >= 0 ? "top" : "bottom");
    }

    private static void CheckFrameRun(EdgeContract edge, FlowchartLayoutEdgeRoute route, DiagramContract contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> boxes, List<Finding> findings)
    {
        foreach (string frameId in new[] { edge.SourceId, edge.TargetId }.Where(id => contract.Groups.Any(group => group.Id == id)))
        {
            if (!boxes.TryGetValue(frameId, out FlowchartLayoutNodeBounds? frame)) continue;
            double left = frame.CenterX - frame.Width / 2, right = frame.CenterX + frame.Width / 2;
            double bottom = frame.CenterY - frame.Height / 2, top = frame.CenterY + frame.Height / 2;
            for (int i = 1; i < route.Points.Count; i++)
            {
                FlowchartLayoutPoint a = route.Points[i - 1], b = route.Points[i];
                bool horizontalEdgeRun = Math.Abs(a.Y - b.Y) <= Tolerance
                    && (Math.Abs(a.Y - top) <= Tolerance || Math.Abs(a.Y - bottom) <= Tolerance)
                    && Math.Max(Math.Min(a.X, b.X), left) < Math.Min(Math.Max(a.X, b.X), right) - Tolerance;
                bool verticalEdgeRun = Math.Abs(a.X - b.X) <= Tolerance
                    && (Math.Abs(a.X - left) <= Tolerance || Math.Abs(a.X - right) <= Tolerance)
                    && Math.Max(Math.Min(a.Y, b.Y), bottom) < Math.Min(Math.Max(a.Y, b.Y), top) - Tolerance;
                if (horizontalEdgeRun || verticalEdgeRun)
                {
                    findings.Add(new Finding("4.9", EdgeName(edge), $"route runs along frame '{frameId}' outline"));
                    break;
                }
            }
        }
    }

    private static void CheckCrossings(List<(EdgeContract Edge, FlowchartLayoutEdgeRoute Route)> routes, List<Finding> findings)
    {
        for (int i = 0; i < routes.Count; i++)
        for (int j = i + 1; j < routes.Count; j++)
        {
            if (routes[i].Edge.SourceId == routes[j].Edge.SourceId && routes[i].Edge.TargetId == routes[j].Edge.TargetId) continue;
            if (HasProperCrossing(routes[i].Route, routes[j].Route))
                findings.Add(new Finding("5.9", EdgeName(routes[i].Edge) + " x " + EdgeName(routes[j].Edge), "routes cross away from a shared endpoint"));
        }
    }

    private static bool HasProperCrossing(FlowchartLayoutEdgeRoute a, FlowchartLayoutEdgeRoute b)
    {
        for (int i = 1; i < a.Points.Count; i++) for (int j = 1; j < b.Points.Count; j++)
        {
            FlowchartLayoutPoint a1 = a.Points[i - 1], a2 = a.Points[i], b1 = b.Points[j - 1], b2 = b.Points[j];
            bool av = Math.Abs(a1.X - a2.X) <= Tolerance, bv = Math.Abs(b1.X - b2.X) <= Tolerance;
            if (av == bv) continue;
            FlowchartLayoutPoint v1 = av ? a1 : b1, v2 = av ? a2 : b2, h1 = av ? b1 : a1, h2 = av ? b2 : a2;
            double x = v1.X, y = h1.Y;
            if (x > Math.Min(h1.X, h2.X) + Tolerance && x < Math.Max(h1.X, h2.X) - Tolerance
                && y > Math.Min(v1.Y, v2.Y) + Tolerance && y < Math.Max(v1.Y, v2.Y) - Tolerance) return true;
        }
        return false;
    }

    private static string EdgeName(EdgeContract edge) => string.IsNullOrEmpty(edge.Id) ? edge.SourceId + "->" + edge.TargetId : edge.Id;
}

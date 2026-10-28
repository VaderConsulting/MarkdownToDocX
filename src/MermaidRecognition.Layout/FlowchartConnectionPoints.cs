using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>One renderer-approved point on the centre-out grid, in layout coordinates (Y upwards).</summary>
internal readonly record struct FlowchartConnectionPoint(LayoutHintDirection Direction, int Step, double X, double Y);

/// <summary>Shared connection-point inventory for exact source hints and editor snapping.</summary>
internal static class FlowchartConnectionPoints
{
    internal static IReadOnlyList<FlowchartConnectionPoint> ForNode(NodeContract Node, FlowchartLayoutNodeBounds Box)
    {
        if (FlowchartDagrePresentation.IsDiamondShape(Node.Shape) || Node.Shape is "circle" or "doublecircle") return [];
        var Boundary = Node.GeometryProvider?.Create(Box.Width, Box.Height).ConnectionBoundary;
        List<FlowchartConnectionPoint> Result = [];
        foreach (LayoutHintDirection Direction in Enum.GetValues<LayoutHintDirection>())
        {
            string Side = DiagramGridLayout.HintSide(Direction);
            bool Vertical = Side is "left" or "right";
            var Points = ConnectionPointGrid.UsablePoints(Boundary, Side, Vertical ? Box.Height : Box.Width, out double Length);
            foreach (var Point in Points)
            {
                int Step = (int)Math.Round((Point.Offset - Length / 2) / FlowchartLayoutRules.LineSpacing);
                double X, Y;
                if (Boundary != null)
                {
                    if (!Boundary.TryPort(Side, Point.Fraction, out var Port)) continue;
                    X = Box.CenterX + Port.X; Y = Box.CenterY - Port.Y;
                }
                else
                {
                    X = Side switch { "left" => Box.CenterX - Box.Width / 2, "right" => Box.CenterX + Box.Width / 2,
                        _ => Box.CenterX - Box.Width / 2 + Point.Offset };
                    Y = Side switch { "top" => Box.CenterY + Box.Height / 2, "bottom" => Box.CenterY - Box.Height / 2,
                        _ => Box.CenterY + Box.Height / 2 - Point.Offset };
                }
                Result.Add(new(Direction, Step, X, Y));
            }
        }
        return Result;
    }

    internal static int? Step(RoutingHintContract? Hint, bool Source) => Hint?.IsActive == true
        ? Source ? Hint.LeavePoint : Hint.EnterPoint : null;

    internal static string? Validate(DiagramContract Contract, FlowchartLayoutResult Layout, bool CheckRoutes = false)
    {
        HashSet<(string Id, LayoutHintDirection Direction, int Step)> Taken = [];
        var Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        var Boxes = Layout.NodeBounds.ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        foreach (var Edge in Contract.Edges)
        {
            if (Edge.RoutingHint is not { IsActive: true } Hint) continue;
            foreach (bool Source in new[] { true, false })
            {
                if (Step(Hint, Source) is not { } Index) continue;
                string Id = Source ? Edge.SourceId : Edge.TargetId;
                var Direction = Source ? Hint.Leave : Hint.Enter;
                if (!Direction.HasValue || !Nodes.TryGetValue(Id, out var Node) || !Boxes.TryGetValue(Id, out var Box))
                    return "An exact connection point requires a placed node and endpoint side.";
                var Matches = ForNode(Node, Box).Where(Point => Point.Direction == Direction && Point.Step == Index).ToArray();
                if (Matches.Length != 1) return "The requested connection point is not available on node '" + Id + "'.";
                if (!Taken.Add((Id, Direction.Value, Index))) return "Two endpoints request the same exact connection point on node '" + Id + "'.";
                if (!CheckRoutes) continue;
                var Route = Layout.EdgeRoutes.SingleOrDefault(Route => Route.EdgeId == Edge.Id);
                if (Route == null || Route.Points.Count < 2) return "An exact endpoint has no completed route.";
                var End = Source ? Route.Points[0] : Route.Points[^1];
                if (Math.Abs(End.X - Matches[0].X) > 0.51 || Math.Abs(End.Y - Matches[0].Y) > 0.51)
                    return FormattableString.Invariant($"Routing could not retain the requested exact connection point on node '{Id}': expected ({Matches[0].X},{Matches[0].Y}), got ({End.X},{End.Y}).");
            }
        }
        return null;
    }
}

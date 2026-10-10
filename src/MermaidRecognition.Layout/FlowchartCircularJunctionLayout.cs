using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>Centres circular collectors and assigns distinct cardinal fan ports.</summary>
internal static class FlowchartCircularJunctionLayout
{
    internal static void AlignFans(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        var ById = Nodes.ToDictionary(Node => Node.Id);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Edges = Contract.Edges.Where(Edge => !Back.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        double Cross(DiagramGridLayout.GridNode Node) => Horizontal ? Node.Y : Node.X;
        foreach (var Junction in Nodes.Where(Node => Node.Shape == "circle"))
        {
            var Outgoing = Edges.Where(Edge => Edge.SourceId == Junction.Id).ToList();
            if (Outgoing.Count < 2 || Outgoing.Count > 3 || Outgoing.Any(Edge => !ById.ContainsKey(Edge.TargetId))) continue;
            var Children = Outgoing.Select(Edge => ById[Edge.TargetId]).ToList();
            if (Children.Select(Node => Node.Rank).Distinct().Count() != 1
                || Children.Any(Node => Node.GroupId != Junction.GroupId)) continue;
            double Centre = Children.Count == 3 ? Cross(Children.OrderBy(Cross).ElementAt(1))
                : (Children.Min(Cross) + Children.Max(Cross)) / 2;
            var Current = Junction;
            var Seen = new HashSet<string>();
            while (Seen.Add(Current.Id))
            {
                double Half = (Horizontal ? Current.Height : Current.Width) / 2.0;
                if (Nodes.Any(Node => Node != Current && Node.Rank == Current.Rank
                    && Math.Abs(Cross(Node) - Centre) < Half + (Horizontal ? Node.Height : Node.Width) / 2.0
                        + DiagramGridMetrics.NODE_GAP_PX)) break;
                if (Horizontal) Current.Y = Centre; else Current.X = Centre;
                var Incoming = Edges.Where(Edge => Edge.TargetId == Current.Id).ToList();
                if (Incoming.Count != 1 || !ById.TryGetValue(Incoming[0].SourceId, out var Previous)
                    || Previous.Rank >= Current.Rank || Previous.GroupId != Current.GroupId
                    || Edges.Count(Edge => Edge.SourceId == Previous.Id) != 1) break;
                Current = Previous;
            }
        }
        foreach (var Junction in Nodes.Where(Node => Node.Shape == "circle"))
        {
            var Incoming = Edges.Where(Edge => Edge.TargetId == Junction.Id).ToList();
            if (Incoming.Count < 2 || Incoming.Count > 3 || Incoming.Any(Edge => !ById.ContainsKey(Edge.SourceId))) continue;
            var Parents = Incoming.Select(Edge => ById[Edge.SourceId]).ToList();
            if (Parents.Select(Node => Node.Rank).Distinct().Count() != 1
                || Parents.Any(Node => Node.GroupId != Junction.GroupId)) continue;
            double Centre = Parents.Count == 3 ? Cross(Parents.OrderBy(Cross).ElementAt(1))
                : (Parents.Min(Cross) + Parents.Max(Cross)) / 2;
            var Current = Junction;
            var Seen = new HashSet<string>();
            while (Seen.Add(Current.Id))
            {
                double Half = (Horizontal ? Current.Height : Current.Width) / 2.0;
                if (Nodes.Any(Node => Node != Current && Node.Rank == Current.Rank
                    && Math.Abs(Cross(Node) - Centre) < Half + (Horizontal ? Node.Height : Node.Width) / 2.0
                        + DiagramGridMetrics.NODE_GAP_PX)) break;
                if (Horizontal) Current.Y = Centre; else Current.X = Centre;
                var Outgoing = Edges.Where(Edge => Edge.SourceId == Current.Id).ToList();
                if (Outgoing.Count != 1 || !ById.TryGetValue(Outgoing[0].TargetId, out var Next)
                    || Next.Rank <= Current.Rank || Next.GroupId != Current.GroupId
                    || Edges.Count(Edge => Edge.TargetId == Next.Id) != 1) break;
                Current = Next;
            }
        }
    }

    internal static void RebuildFans(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        bool Horizontal = Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.RightToLeft;
        bool Positive = Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.BottomToTop;
        string Down = Horizontal ? (Positive ? "right" : "left") : (Positive ? "top" : "bottom");
        string Up = Horizontal ? (Positive ? "left" : "right") : (Positive ? "bottom" : "top");
        double Main(FlowchartLayoutNodeBounds Box) => Horizontal ? Box.CenterX : Box.CenterY;
        double Cross(FlowchartLayoutNodeBounds Box) => Horizontal ? Box.CenterY : Box.CenterX;
        var Changes = new Dictionary<FlowchartLayoutEdgeRoute, (string Exit, string Entry)>();
        foreach (var Junction in Nodes.Values.Where(Node => Node.Shape == "circle" && Bounds.ContainsKey(Node.Id)))
        foreach (bool Outgoing in new[] { true, false })
        {
            var Box = Bounds[Junction.Id];
            var Fan = Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2
                && (Outgoing ? Route.SourceId : Route.TargetId) == Junction.Id).ToList();
            if (Fan.Count < 2 || Fan.Count > 3) continue;
            var Others = Fan.Select(Route => Outgoing ? Route.TargetId : Route.SourceId).ToList();
            if (Others.Distinct().Count() != Fan.Count || Others.Any(Id => !Bounds.ContainsKey(Id))
                || Others.Any(Id => (Main(Bounds[Id]) - Main(Box)) * (Positive ? 1 : -1) * (Outgoing ? 1 : -1) <= 0)) continue;
            var Sides = Others.Select(Id => Math.Abs(Cross(Bounds[Id]) - Cross(Box)) <= 0.5
                ? (Outgoing ? Down : Up)
                : Horizontal ? (Cross(Bounds[Id]) > Cross(Box) ? "top" : "bottom")
                    : (Cross(Bounds[Id]) > Cross(Box) ? "right" : "left")).ToList();
            if (Sides.Distinct().Count() != Fan.Count) continue;
            for (int Index = 0; Index < Fan.Count; Index++)
            {
                var Route = Fan[Index];
                var Previous = Changes.TryGetValue(Route, out var Existing) ? Existing : (Route.ExitSide, Route.EntrySide);
                Changes[Route] = Outgoing ? (Sides[Index], Previous.Item2) : (Previous.Item1, Sides[Index]);
            }
        }
        foreach (var Change in Changes)
        {
            var Route = Change.Key;
            var Source = Bounds[Route.SourceId]; var Target = Bounds[Route.TargetId];
            var Start = Change.Value.Exit == Route.ExitSide ? (Route.Points[0].X, Route.Points[0].Y)
                : Cardinal(Source, Change.Value.Exit);
            var End = Change.Value.Entry == Route.EntrySide ? (Route.Points[^1].X, Route.Points[^1].Y)
                : Cardinal(Target, Change.Value.Entry);
            var Path = DiagramGridRouteBuilder.BuildRoute(Start.Item1, Start.Item2, Source,
                Nodes.TryGetValue(Route.SourceId, out var SourceNode) ? SourceNode.Shape : "rect",
                Change.Value.Exit, End.Item1, End.Item2, Target,
                Nodes.TryGetValue(Route.TargetId, out var TargetNode) ? TargetNode.Shape : "rect", Change.Value.Entry,
                Bounds, Route.SourceId, Route.TargetId, Contract, Direction, Horizontal);
            Route.ExitSide = Change.Value.Exit; Route.EntrySide = Change.Value.Entry;
            Route.Points.Clear();
            foreach (var Point in Path) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
        }
    }

    private static (double X, double Y) Cardinal(FlowchartLayoutNodeBounds Box, string Side)
    {
        double Radius = Math.Min(Box.Width, Box.Height) / 2;
        return Side switch
        {
            "left" => (Box.CenterX - Radius, Box.CenterY),
            "right" => (Box.CenterX + Radius, Box.CenterY),
            "top" => (Box.CenterX, Box.CenterY + Radius),
            _ => (Box.CenterX, Box.CenterY - Radius)
        };
    }
}

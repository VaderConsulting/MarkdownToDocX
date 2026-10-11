using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans complete many-to-many fans as aligned ranks with nested tracks.</summary>
internal static class FlowchartPartnerRankLayout
{
    private const int TRACK_PITCH = 6;
    // Reserve enough track slots that a crossing stays on the straight part
    // of a connector rather than intersecting its rounded corner.
    private const int CORNER_TRACK_PITCH = ((DiagramGridRouteBuilder.CORNER_RADIUS_PX + TRACK_PITCH - 1) / TRACK_PITCH) * TRACK_PITCH;
    private const int TERMINAL_RESERVE = 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX;

    internal sealed record Partners(DiagramGridLayout.GridNode[] Parents,
        DiagramGridLayout.GridNode[] Children, int PortPitch);

    internal static List<Partners> Find(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        var Visible = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        var Outgoing = Visible.ToLookup(Edge => Edge.SourceId);
        var Incoming = Visible.ToLookup(Edge => Edge.TargetId);
        var ById = Nodes.ToDictionary(Node => Node.Id);
        var Used = new HashSet<string>();
        var Result = new List<Partners>();
        foreach (var Parent in Nodes.Where(IsBox))
        {
            if (Used.Contains(Parent.Id)) continue;
            var Targets = Outgoing[Parent.Id].Select(Edge => Edge.TargetId).ToArray();
            int Count = Targets.Length;
            if (Count < 2 || Targets.Distinct().Count() != Count || Targets.Any(Id => !ById.ContainsKey(Id))) continue;
            var Children = Targets.Select(Id => ById[Id]).OrderBy(Node => Cross(Node, Horizontal)).ToArray();
            var Sources = Incoming[Children[0].Id].Select(Edge => Edge.SourceId).ToArray();
            if (Sources.Length != Count || Sources.Distinct().Count() != Count || Sources.Any(Id => !ById.ContainsKey(Id))) continue;
            var Parents = Sources.Select(Id => ById[Id]).OrderBy(Node => Cross(Node, Horizontal)).ToArray();
            if (Parents.Any(Node => !IsBox(Node) || Node.Rank != Parent.Rank || Node.GroupId != Parent.GroupId)
                || Children.Any(Node => !IsBox(Node) || Node.Rank != Children[0].Rank || Node.GroupId != Children[0].GroupId)
                || Parent.Rank == Children[0].Rank
                || Parents.Any(Node => !Outgoing[Node.Id].Select(Edge => Edge.TargetId).Order().SequenceEqual(Targets.Order()))
                || Children.Any(Node => !Incoming[Node.Id].Select(Edge => Edge.SourceId).Order().SequenceEqual(Sources.Order()))) continue;
            int Pitch = Math.Max(CORNER_TRACK_PITCH, Parents.Concat(Children).Min(Node => CrossSize(Node, Horizontal)) / 6);
            Result.Add(new(Parents, Children, Pitch));
            foreach (var Node in Parents) Used.Add(Node.Id);
        }
        return Result;
    }

    internal static void Prepare(List<Partners> Blocks, bool Horizontal, bool Align = true)
    {
        for (int BlockIndex = 0; BlockIndex < Blocks.Count; BlockIndex++)
        {
            var Block = Blocks[BlockIndex];
            if (!Align)
            {
                double Displacement = Enumerable.Range(0, Block.Parents.Length)
                    .Max(Index => Math.Abs(Cross(Block.Parents[Index], Horizontal) - Cross(Block.Children[Index], Horizontal)));
                int Pitch = (int)Math.Ceiling((Block.PortPitch + Displacement) / TRACK_PITCH) * TRACK_PITCH;
                Blocks[BlockIndex] = Block = Block with { PortPitch = Pitch };
            }
            int Needed = DiagramGridMetrics.CeilToGrid(4 * (Block.Parents.Length - 1) * Block.PortPitch + DiagramGridMetrics.GRID_UNIT_PX);
            foreach (var Node in Block.Parents.Concat(Block.Children))
            {
                if (Horizontal) Node.Height = Math.Max(Node.Height, Needed);
                else Node.Width = Math.Max(Node.Width, Needed);
            }
            for (int Index = 0; Align && Index < Block.Children.Length; Index++)
            {
                if (Horizontal) Block.Children[Index].Y = Block.Parents[Index].Y;
                else Block.Children[Index].X = Block.Parents[Index].X;
            }
        }
    }

    internal static HashSet<FlowchartLayoutEdgeRoute> StraightRoutes(FlowchartLayoutResult Layout, List<Partners> Blocks)
    {
        var Pairs = Blocks.SelectMany(Block => Block.Parents.Select((Parent, Index) =>
            (Parent.Id, Block.Children[Index].Id))).ToHashSet();
        return Layout.EdgeRoutes.Where(Route => Pairs.Contains((Route.SourceId, Route.TargetId))).ToHashSet();
    }

    internal static bool GrowGaps(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        List<DiagramGridLayout.GridFrame> Frames, List<Partners> Blocks, bool Horizontal)
    {
        bool Changed = false;
        foreach (var Block in Blocks)
        {
            var (Start, End, Flow) = Limits(Contract, Frames, Block, Horizontal);
            int Tracks = Block.Parents.Length * (Block.Children.Length - 1);
            int Extra = DiagramGridMetrics.CeilToGrid(2 * TERMINAL_RESERVE + (Tracks - 1) * CORNER_TRACK_PITCH - (End - Start));
            if (Extra == 0) continue;
            int LaterRank = Math.Max(Block.Parents[0].Rank, Block.Children[0].Rank);
            double Cut = (Main(Block.Parents[0], Horizontal) + Main(Block.Children[0], Horizontal)) / 2;
            foreach (var Node in Nodes.Where(Node => Node.Rank >= LaterRank))
            {
                if (Horizontal) Node.X += Extra; else Node.Y += Extra;
            }
            foreach (var Frame in Frames)
            {
                if (Horizontal)
                {
                    if (Frame.Left > Cut) Frame.Left += Extra;
                    if (Frame.Right > Cut) Frame.Right += Extra;
                }
                else
                {
                    if (Frame.Top > Cut) Frame.Top += Extra;
                    if (Frame.Bottom > Cut) Frame.Bottom += Extra;
                }
            }
            Changed = true;
        }
        return Changed;
    }

    internal static void Route(DiagramContract Contract, FlowchartLayoutResult Layout,
        List<DiagramGridLayout.GridNode> Nodes, List<DiagramGridLayout.GridFrame> Frames, List<Partners> Blocks, bool Horizontal)
    {
        var Bounds = Nodes.ToDictionary(Node => Node.Id, Node => new FlowchartLayoutNodeBounds
            { NodeId = Node.Id, CenterX = Node.X, CenterY = Node.Y, Width = Node.Width, Height = Node.Height });
        var Routes = Layout.EdgeRoutes.ToLookup(Route => (Route.SourceId, Route.TargetId));
        foreach (var Block in Blocks)
        {
            var (Start, End, Flow) = Limits(Contract, Frames, Block, Horizontal);
            double CrossSign = Math.Sign(Cross(Block.Parents[^1], Horizontal) - Cross(Block.Parents[0], Horizontal));
            var Pairs = Enumerable.Range(0, Block.Parents.Length).SelectMany(Parent =>
                Enumerable.Range(0, Block.Children.Length).Select(Child => (Parent, Child))).ToList();
            // In each half-fan, outer branches turn first on departure and last
            // on arrival. The opposite half uses the mirrored order.
            var Crossed = Pairs.Where(Pair => Pair.Child > Pair.Parent).OrderByDescending(Pair => Pair.Parent).ThenByDescending(Pair => Pair.Child)
                .Concat(Pairs.Where(Pair => Pair.Child < Pair.Parent).OrderBy(Pair => Pair.Parent).ThenBy(Pair => Pair.Child)).ToList();
            var Tracks = Crossed.Select((Pair, Index) => (Pair, Lane: Flow * (Start + TERMINAL_RESERVE + Index * CORNER_TRACK_PITCH)))
                .ToDictionary(Item => Item.Pair, Item => Item.Lane);
            foreach (var Pair in Pairs)
            {
                var Source = Block.Parents[Pair.Parent]; var Target = Block.Children[Pair.Child];
                var Route = Routes[(Source.Id, Target.Id)].SingleOrDefault();
                if (Route == null) continue;
                double StartMain = Main(Source, Horizontal) + Flow * MainSize(Source, Horizontal) / 2;
                double EndMain = Main(Target, Horizontal) - Flow * MainSize(Target, Horizontal) / 2;
                int Difference = (int)CrossSign * (Pair.Child - Pair.Parent);
                double StartCross = Cross(Source, Horizontal) + Math.Sign(Difference) * (2 * Math.Abs(Difference) - 1) * Block.PortPitch;
                double EndCross = Cross(Target, Horizontal) - 2 * Difference * Block.PortPitch;
                if (Difference == 0) StartCross = Cross(Source, Horizontal);
                if (Difference == 0 && Math.Abs(StartCross - EndCross) > 0.01)
                {
                    double SharedCross = (StartCross + EndCross) / 2;
                    if (Math.Abs(SharedCross - StartCross) < CrossSize(Source, Horizontal) / 2
                        && Math.Abs(SharedCross - EndCross) < CrossSize(Target, Horizontal) / 2)
                        StartCross = EndCross = SharedCross;
                }
                (double X, double Y) Map(double MainValue, double CrossValue) => Horizontal ? (MainValue, CrossValue) : (CrossValue, MainValue);
                var Path = new List<(double X, double Y)> { Map(StartMain, StartCross) };
                if (Difference != 0) { Path.Add(Map(Tracks[Pair], StartCross)); Path.Add(Map(Tracks[Pair], EndCross)); }
                else if (Math.Abs(StartCross - EndCross) > 0.01)
                {
                    double Track = (StartMain + EndMain) / 2;
                    Path.Add(Map(Track, StartCross));
                    Path.Add(Map(Track, EndCross));
                }
                Path.Add(Map(EndMain, EndCross));
                if (Path.Zip(Path.Skip(1)).Any(Segment => FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                    Segment.First.X, Segment.First.Y, Segment.Second.X, Segment.Second.Y, Bounds, Source.Id, Target.Id))) continue;
                Route.ExitSide = Horizontal ? (Flow > 0 ? "right" : "left") : (Flow > 0 ? "top" : "bottom");
                Route.EntrySide = Horizontal ? (Flow > 0 ? "left" : "right") : (Flow > 0 ? "bottom" : "top");
                Route.Points.Clear();
                foreach (var Point in Path) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            }
        }
    }

    private static (double Start, double End, double Flow) Limits(DiagramContract Contract,
        List<DiagramGridLayout.GridFrame> Frames, Partners Block, bool Horizontal)
    {
        double Flow = Math.Sign(Main(Block.Children[0], Horizontal) - Main(Block.Parents[0], Horizontal));
        double Start = Block.Parents.Max(Node => Flow * Main(Node, Horizontal) + MainSize(Node, Horizontal) / 2);
        double End = Block.Children.Min(Node => Flow * Main(Node, Horizontal) - MainSize(Node, Horizontal) / 2);
        foreach (var Frame in Frames)
        {
            bool ParentsInside = Block.Parents.Any(Node => DiagramGridLayout.IsInsideGroup(Contract, Node.Id, Frame.Id));
            bool ChildrenInside = Block.Children.Any(Node => DiagramGridLayout.IsInsideGroup(Contract, Node.Id, Frame.Id));
            double First = Flow * (Horizontal ? Frame.Left : Frame.Bottom);
            double Second = Flow * (Horizontal ? Frame.Right : Frame.Top);
            if (ParentsInside && !ChildrenInside) Start = Math.Max(Start, Math.Max(First, Second));
            if (ChildrenInside && !ParentsInside) End = Math.Min(End, Math.Min(First, Second));
        }
        return (Start, End, Flow);
    }

    private static bool IsBox(DiagramGridLayout.GridNode Node) => !Node.IsDiamond
        && (string.IsNullOrEmpty(Node.Shape) || Node.Shape == "rect" || Node.Shape == "rounded" || Node.Shape == "round");
    private static double Main(DiagramGridLayout.GridNode Node, bool Horizontal) => Horizontal ? Node.X : Node.Y;
    private static double Cross(DiagramGridLayout.GridNode Node, bool Horizontal) => Horizontal ? Node.Y : Node.X;
    private static int MainSize(DiagramGridLayout.GridNode Node, bool Horizontal) => Horizontal ? Node.Width : Node.Height;
    private static int CrossSize(DiagramGridLayout.GridNode Node, bool Horizontal) => Horizontal ? Node.Height : Node.Width;
}

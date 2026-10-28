using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Offers complete nested header and gap channels for isolated stars, without changing node regions or gaps.</summary>
internal static partial class FlowchartStarFanNesting
{
    /// <summary>Counts accepted and rejected complete fan assignments.</summary>
    /// <param name="Kept">Fans whose finished routes strictly improved.</param>
    /// <param name="Rejected">Generated fans rejected without publishing any geometry.</param>
    internal sealed record Result(int Kept, int Rejected);

    /// <summary>Constructs each star as one assignment, validates its complete paths, then publishes all terminals and tracks atomically.</summary>
    /// <param name="Contract">Original links, hints and geometry providers.</param>
    /// <param name="Layout">Finished Y-up model; no placement or renderer cache is mutated.</param>
    /// <returns>Complete assignment outcomes.</returns>
    internal static Result Apply(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> Boxes = Layout.NodeBounds.Concat(Layout.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        Dictionary<string, NodeContract> Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        EdgeContract[] Visible = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        Dictionary<string, int> Degree = Visible.SelectMany(Edge => new[] { Edge.SourceId, Edge.TargetId }).GroupBy(Id => Id)
            .ToDictionary(Group => Group.Key, Group => Group.Count(), StringComparer.Ordinal);
        bool Horizontal = FlowchartDirectionNormalizer.Normalize(Contract.Direction)
            is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        FlowchartRenderOptions Text = FlowchartRenderOptions.FromContract(Contract);
        int Kept = 0, Rejected = 0;
        foreach (FlowchartLayoutNodeBounds Hub in Layout.NodeBounds)
        {
            EdgeContract[] Links = [.. Visible.Where(Edge => Edge.SourceId == Hub.NodeId || Edge.TargetId == Hub.NodeId)];
            if (Links.Length < 3 || Links.Any(Edge => Edge.RoutingHint?.IsActive == true)
                || (!Links.All(Edge => Edge.SourceId == Hub.NodeId) && !Links.All(Edge => Edge.TargetId == Hub.NodeId)))
            {
                continue;
            }

            bool FanOut = Links[0].SourceId == Hub.NodeId;
            (EdgeContract Edge, FlowchartLayoutNodeBounds? Leaf)[] Leaves = [.. Links.Select(Edge => (Edge, Leaf: Boxes.GetValueOrDefault(FanOut ? Edge.TargetId : Edge.SourceId)))];
            if (Leaves.Any(Item => Item.Leaf == null || Degree.GetValueOrDefault(Item.Leaf.NodeId) != 1))
            {
                continue;
            }

            double Main(FlowchartLayoutNodeBounds Box) => Horizontal ? Box.CenterX : Box.CenterY;
            double Cross(FlowchartLayoutNodeBounds Box) => Horizontal ? Box.CenterY : Box.CenterX;
            double CrossHalf(FlowchartLayoutNodeBounds Box) => (Horizontal ? Box.Height : Box.Width) / 2;
            int Along = Math.Sign(Main(Leaves[0].Leaf!) - Main(Hub));
            if (Along == 0 || Leaves.Any(Item => Math.Sign(Main(Item.Leaf!) - Main(Hub)) != Along))
            {
                continue;
            }

            Dictionary<FlowchartLayoutEdgeRoute, FlowchartLayoutEdgeRoute> Proposed = [];
            bool Valid = true;
            string HubSide = Horizontal ? Along < 0 ? "left" : "right" : Along < 0 ? "bottom" : "top";
            double MainHalf(FlowchartLayoutNodeBounds Box) => (Horizontal ? Box.Width : Box.Height) / 2;
            (double X, double Y) Point(double M, double C) => Horizontal ? (M, C) : (C, M);
            double HubEdge = Main(Hub) + (Along * MainHalf(Hub));
            double Gap = Leaves.Min(Item => Along * (Main(Item.Leaf!) - (Along * MainHalf(Item.Leaf!)) - HubEdge));
            double Clearance = DiagramGridRouteBuilder.CORNER_RADIUS_PX;
            if (Gap <= 2 * Clearance)
            {
                continue;
            }

            IGrouping<int, (EdgeContract Edge, FlowchartLayoutNodeBounds? Leaf)>[] Banks = [.. Leaves.GroupBy(Item => Cross(Item.Leaf!) < Cross(Hub) ? -1 : 1).OrderBy(Bank => Bank.Key)];
            double Low = Banks.Length == 2 ? Banks[0].Max(Item => Cross(Item.Leaf!) + CrossHalf(Item.Leaf!)) : double.PositiveInfinity;
            double High = Banks.Length == 2 ? Banks[1].Min(Item => Cross(Item.Leaf!) - CrossHalf(Item.Leaf!)) : double.NegativeInfinity;
            int Offset = 0;
            foreach (IGrouping<int, (EdgeContract Edge, FlowchartLayoutNodeBounds? Leaf)>? Bank in Banks)
            {
                double LabelRun = Bank.Max(Item => string.IsNullOrWhiteSpace(Item.Edge.Label) ? 0
                    : (Item.Edge.Label!.Length * Text.EdgeLabelFontSizePx * FlowchartEdgeLabelGapHelper.LABEL_CHAR_WIDTH_EM)
                        + (2 * (Text.LabelPaddingPx + DiagramGridRouteBuilder.CORNER_RADIUS_PX)));
                List<(double X, double Y, double Nx, double Ny)> Ports = [];
                for (int Index = 0; Index < Bank.Count(); Index++)
                {
                    double Parameter = (Offset + Index + 1.0) / (Links.Length + 1.0);
                    if (Horizontal)
                    {
                        Parameter = 1 - Parameter;
                    }

                    if (!Port(Hub, HubSide, Parameter, out (double X, double Y, double Nx, double Ny) Value)) { Valid = false; break; }
                    Ports.Add(Value);
                }
                Offset += Bank.Count();
                if (!Valid)
                {
                    break;
                }

                double PortCross((double X, double Y, double Nx, double Ny) Value) => Horizontal ? Value.Y : Value.X;
                (double X, double Y, double Nx, double Ny)[] Inner = [.. Ports.Where(Value => PortCross(Value) > Low + Clearance && PortCross(Value) < High - Clearance
                    && (Bank.Key < 0 ? PortCross(Value) - Low : High - PortCross(Value)) >= LabelRun)];
                (double X, double Y, double Nx, double Ny)[] Outer = [.. Ports.Except(Inner).OrderBy(Value => -Bank.Key * PortCross(Value))];
                (EdgeContract Edge, FlowchartLayoutNodeBounds? Leaf)[] Near = [.. Bank.OrderBy(Item => Math.Abs(Main(Item.Leaf!) - Main(Hub))).ThenBy(Item => Item.Leaf!.NodeId, StringComparer.Ordinal)];
                (EdgeContract Edge, FlowchartLayoutNodeBounds? Leaf)[] OuterLeaves = [.. Near.Take(Outer.Length).Reverse()];
                (EdgeContract Edge, FlowchartLayoutNodeBounds? Leaf)[] InnerLeaves = [.. Near.Skip(Outer.Length)];
                Inner = [.. Inner.OrderBy(Value => -Bank.Key * PortCross(Value))];
                var Header = DiagramGridMetrics.TrackOffsets(Outer.Length, (int)Math.Floor(Gap - (2 * Clearance)));
                double Grid = DiagramGridMetrics.GRID_UNIT_PX;
                double Envelope = Bank.Key < 0 ? Boxes.Values.Min(Box => Cross(Box) - CrossHalf(Box)) : Boxes.Values.Max(Box => Cross(Box) + CrossHalf(Box));
                Envelope = (Bank.Key < 0 ? Math.Floor(Envelope / Grid) : Math.Ceiling(Envelope / Grid)) * Grid;
                Envelope += Bank.Key * DiagramGridMetrics.CeilToGrid(LabelRun);
                // Outside ports turn in the measured header, farthest leaf
                // first. Interior ports already own a distinct clear track.
                for (int Index = 0; Index < Outer.Length; Index++)
                {
                    (EdgeContract Edge, FlowchartLayoutNodeBounds? Leaf) Item = OuterLeaves[Index]; (double X, double Y, double Nx, double Ny) HubPort = Outer[Index];
                    string LeafSide = Horizontal ? Bank.Key < 0 ? "bottom" : "top" : Bank.Key < 0 ? "left" : "right";
                    if (!Port(Item.Leaf!, LeafSide, 0.5, out (double X, double Y, double Nx, double Ny) LeafPort)) { Valid = false; break; }
                    double Track = Envelope + (Bank.Key * (Outer.Length - Index) * Grid);
                    double Turn = HubEdge + (Along * (Clearance + Header[Index]));
                    Publish(Item.Edge, HubPort, LeafPort, LeafSide, [(HubPort.X, HubPort.Y),
                        Point(Turn, PortCross(HubPort)), Point(Turn, Track), Point(Main(Item.Leaf!), Track), (LeafPort.X, LeafPort.Y)]);
                }
                for (int Index = 0; Index < Inner.Length && Valid; Index++)
                {
                    (EdgeContract Edge, FlowchartLayoutNodeBounds? Leaf) Item = InnerLeaves[Index]; (double X, double Y, double Nx, double Ny) HubPort = Inner[Index];
                    string LeafSide = Horizontal ? Bank.Key < 0 ? "top" : "bottom" : Bank.Key < 0 ? "right" : "left";
                    if (!Port(Item.Leaf!, LeafSide, 0.5, out (double X, double Y, double Nx, double Ny) LeafPort)) { Valid = false; break; }
                    Publish(Item.Edge, HubPort, LeafPort, LeafSide, [(HubPort.X, HubPort.Y),
                        Point(Main(Item.Leaf!), PortCross(HubPort)), (LeafPort.X, LeafPort.Y)]);
                }
                if (!Valid)
                {
                    break;
                }
            }
            if (!Valid || Proposed.Count != Links.Length) { Rejected++; continue; }
            (int Bends, int Crossings, double Shared) Before = Measure(Layout.EdgeRoutes);
            (int Bends, int Crossings, double Shared) After = Measure(Layout.EdgeRoutes.Select(Route => Proposed.GetValueOrDefault(Route, Route)));
            (int Bends, int Crossings, double Shared) Fan = Measure(Proposed.Values);
            if (After.Bends > Before.Bends || After.Crossings > Before.Crossings
                || After.Bends + After.Crossings >= Before.Bends + Before.Crossings || After.Shared > Before.Shared
                || Fan.Crossings != 0 || Fan.Shared != 0)
            { Rejected++; continue; }
            if (TryPublish(Contract, Layout, Proposed))
            {
                Kept++;
            }
            else
            {
                Rejected++;
            }

            void Publish(EdgeContract Edge, (double X, double Y, double Nx, double Ny) HubPort,
                (double X, double Y, double Nx, double Ny) LeafPort, string LeafSide, List<(double X, double Y)> Points)
            {
                FlowchartLayoutEdgeRoute? Route = Layout.EdgeRoutes.SingleOrDefault(Route => Route.EdgeId == Edge.Id);
                if (Route == null) { Valid = false; return; }
                if (!FanOut)
                {
                    Points.Reverse();
                }

                FlowchartLayoutEdgeRoute Candidate = new FlowchartLayoutEdgeRoute { EdgeId = Route.EdgeId, SourceId = Route.SourceId, TargetId = Route.TargetId,
                    ExitSide = FanOut ? HubSide : LeafSide, EntrySide = FanOut ? LeafSide : HubSide,
                    ExitNormalX = FanOut ? HubPort.Nx : LeafPort.Nx, ExitNormalY = FanOut ? HubPort.Ny : LeafPort.Ny,
                    EntryNormalX = FanOut ? LeafPort.Nx : HubPort.Nx, EntryNormalY = FanOut ? LeafPort.Ny : HubPort.Ny };
                Candidate.Points.AddRange(Points.Select(Value => new FlowchartLayoutPoint { X = Value.X, Y = Value.Y }));
                if (!FlowchartAllocatedPortFallback.IsLegal(Contract, Boxes, Candidate, Points)) { Valid = false; return; }
                Proposed.Add(Route, Candidate);
            }
        }
        return new(Kept, Rejected);

        // The provider owns the silhouette. Sloping normals are not coerced
        // into rectangular terminals merely to make this channel candidate fit.
        bool Port(FlowchartLayoutNodeBounds Box, string Side, double Parameter, out (double X, double Y, double Nx, double Ny) Point)
        {
            NodeGeometry Geometry = Nodes.GetValueOrDefault(Box.NodeId)?.GeometryProvider?.Create(Box.Width, Box.Height)
                ?? NodeGeometry.Rectangle(Box.Width, Box.Height);
            if (!Geometry.ConnectionBoundary.TryPort(Side, Parameter, out NodeGeometryPort Local)) { Point = default; return false; }
            Point = (Box.CenterX + Local.X, Box.CenterY - Local.Y, Local.NormalX, -Local.NormalY);
            return (Math.Abs(Point.Nx) < 0.001 && Math.Abs(Point.Ny) > 0.999)
                || (Math.Abs(Point.Ny) < 0.001 && Math.Abs(Point.Nx) > 0.999);
        }
    }

    /// <summary>Measures complete routed geometry, including intersections between links sharing a hub.</summary>
    /// <param name="Routes">Original or substituted complete inventory.</param>
    /// <returns>Corners, geometric crossings and shared segment length.</returns>
    internal static (int Bends, int Crossings, double Shared) Measure(IEnumerable<FlowchartLayoutEdgeRoute> Routes)
    {
        FlowchartLayoutEdgeRoute[] Inventory = [.. Routes];
        (double X, double Y)[][] Paths = [.. Inventory.Select(Route => Route.Points.Select(Point => (Point.X, Point.Y)).ToArray())];
        int Crossings = 0; double Shared = 0;
        for (int A = 0; A < Paths.Length; A++)
        {
            for (int B = A + 1; B < Paths.Length; B++)
        {
            (int Crossings, double Overlap) Pair = FlowchartBranchRouteOrder.Intersections(Paths[A], Paths[B]);
            Crossings += Pair.Crossings; Shared += Pair.Overlap;
        }
        }

        return (Inventory.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)), Crossings, Shared);
    }

    /// <summary>Publishes a fully validated terminal and path without retaining trial-owned mutable points.</summary>
    /// <param name="From">Validated immutable-in-practice candidate.</param>
    /// <param name="To">Existing declared route identity.</param>
    private static void Copy(FlowchartLayoutEdgeRoute From, FlowchartLayoutEdgeRoute To)
    {
        To.ExitSide = From.ExitSide; To.EntrySide = From.EntrySide;
        To.ExitNormalX = From.ExitNormalX; To.ExitNormalY = From.ExitNormalY;
        To.EntryNormalX = From.EntryNormalX; To.EntryNormalY = From.EntryNormalY;
        To.Points.Clear(); To.Points.AddRange(From.Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
    }
}

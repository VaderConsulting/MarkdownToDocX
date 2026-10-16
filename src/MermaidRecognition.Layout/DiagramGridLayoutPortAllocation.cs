using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Port allocation and endpoint-attachment helpers for the port stage.</summary>
public static partial class DiagramGridLayout
{    private static void AssignPortSides(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction,
        HashSet<string> SharedFanSources)
    {
        var BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        Dictionary<string, GridFrame> FrameById = new Dictionary<string, GridFrame>(StringComparer.Ordinal);
        foreach (GridFrame Frame in Frames)
        {
            FrameById[Frame.Id] = Frame;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> ObstacleBounds = BuildObstacleBounds(Placed, Frames);
        HashSet<string> ReservedCenterPorts = new HashSet<string>(StringComparer.Ordinal);
        List<PortEnd> Ends = new List<PortEnd>();
        List<(FlowchartLayoutEdgeRoute Route, BoxRef Source, BoxRef Target)> Pending = [];
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (!TryBox(Route.SourceId, ById, FrameById, out BoxRef Source) || !TryBox(Route.TargetId, ById, FrameById, out BoxRef Target))
            {
                continue;
            }

            EdgeContract? ContractEdge = FindContractEdge(Contract, Route);
            if (string.Equals(ContractEdge?.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                // Invisible links are not drawn and must not consume a visible
                // port slot on the same side as a real connection.
                continue;
            }
            RoutingHintContract? RoutingHint = ContractEdge?.RoutingHint?.IsActive == true
                ? ContractEdge.RoutingHint : null;
            bool CircularReturn = BackEdges.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)
                && (Nodes.TryGetValue(Route.SourceId, out var CircularSource) && CircularSource.Shape == "circle"
                    || Nodes.TryGetValue(Route.TargetId, out var CircularTarget) && CircularTarget.Shape == "circle");
            string StraightExit = string.Empty, StraightEntry = string.Empty;
            bool HasStraightCandidate = RoutingHint == null && !CircularReturn
                && TryAssignStraightPortSides(Source, Target, Direction,
                    out StraightExit, out StraightEntry);
            if (HasStraightCandidate && StraightChordIsClear(
                    Contract,
                    Source,
                    Target,
                    StraightExit,
                    StraightEntry,
                    ObstacleBounds,
                    Route.SourceId,
                    Route.TargetId))
            {
                Route.ExitSide = StraightExit;
                Route.EntrySide = StraightEntry;
                ReservedCenterPorts.Add(Source.Id + "|" + StraightExit);
                ReservedCenterPorts.Add(Target.Id + "|" + StraightEntry);
                Ends.Add(new PortEnd
                {
                    Route = Route,
                    IsSource = true,
                    KeepsCentre = true,
                    Side = StraightExit,
                    Box = Source,
                    Other = Target
                });
                Ends.Add(new PortEnd
                {
                    Route = Route,
                    IsSource = false,
                    KeepsCentre = true,
                    Side = StraightEntry,
                    Box = Target,
                    Other = Source
                });
                continue;
            }
            Pending.Add((Route, Source, Target));
        }

        Dictionary<string, FlowchartLayoutNodeBounds> FanOutBoundsById = BuildPlacedBounds(Placed, Frames);
        foreach ((FlowchartLayoutEdgeRoute Route, BoxRef Source, BoxRef Target) Item in Pending)
        {
            string Exit;
            string Entry;
            EdgeContract? ContractEdge = FindContractEdge(Contract, Item.Route);
            RoutingHintContract? RoutingHint = ContractEdge?.RoutingHint?.IsActive == true
                ? ContractEdge.RoutingHint : null;
            if (RoutingHint != null)
            {
                Exit = RoutingHint.Leave.HasValue
                    ? HintSide(RoutingHint.Leave.Value)
                    : FacingSide(Item.Source, Item.Target);
                Entry = RoutingHint.Enter.HasValue
                    ? HintSide(RoutingHint.Enter.Value)
                    : FacingSide(Item.Target, Item.Source);
            }
            else
            {
                // Geometry owns the first side choice for every ordinary edge,
                // including fans and junctions. Direction, old Dagre sides and
                // track occupancy affect the path between endpoints, never the
                // endpoint sides. This prevents 4.7 repair becoming a late
                // substitute for allocating sides from the placed node geometry.
                Exit = FacingSide(Item.Source, Item.Target);
                Entry = FacingSide(Item.Target, Item.Source);
            }
            Item.Route.ExitSide = Exit;
            Item.Route.EntrySide = Entry;
            Ends.Add(new PortEnd
            {
                Route = Item.Route,
                IsSource = true,
                IsHinted = RoutingHint != null,
                Side = Exit,
                Box = Item.Source,
                Other = Item.Target
            });
            Ends.Add(new PortEnd
            {
                Route = Item.Route,
                IsSource = false,
                IsHinted = RoutingHint != null,
                Side = Entry,
                Box = Item.Target,
                Other = Item.Source
            });
        }

        Dictionary<string, List<PortEnd>> Groups = new Dictionary<string, List<PortEnd>>(StringComparer.Ordinal);
        foreach (PortEnd End in Ends)
        {
            bool Diamond = !End.Box.IsFrame
                && Nodes.TryGetValue(End.Box.Id, out NodeContract? PortNode)
                && FlowchartDagrePresentation.IsDiamondShape(PortNode.Shape);
            // A diamond's four vertices and sloping-edge ports form one shared
            // inventory. Allocating each nominal rectangle side separately can
            // give two lines the same vertex.
            string Key = End.Box.Id + (Diamond ? "|diamond" : "|" + End.Side);
            if (!Groups.TryGetValue(Key, out List<PortEnd>? Group))
            {
                Group = new List<PortEnd>();
                Groups[Key] = Group;
            }

            Group.Add(End);
        }

        foreach (KeyValuePair<string, List<PortEnd>> Pair in Groups)
        {
            List<PortEnd> Group = Pair.Value;
            bool Diamond = Pair.Key.EndsWith("|diamond", StringComparison.Ordinal);
            if (!Diamond) Group.Sort(delegate(PortEnd Left, PortEnd Right)
            {
                int HintOrder = Right.IsHinted.CompareTo(Left.IsHinted);
                if (HintOrder != 0) return HintOrder;
                bool VerticalSide = Left.Side == "left" || Left.Side == "right";
                if (VerticalSide)
                {
                    return Right.Other.CenterY.CompareTo(Left.Other.CenterY);
                }

                return Left.Other.CenterX.CompareTo(Right.Other.CenterX);
            });

            ApplySlots(Group, Nodes);
        }

    }

    internal static EdgeContract? FindContractEdge(DiagramContract Contract, FlowchartLayoutEdgeRoute Route)
    {
        EdgeContract? ById = Contract.Edges.FirstOrDefault(Edge =>
            !string.IsNullOrEmpty(Edge.Id) && string.Equals(Edge.Id, Route.EdgeId, StringComparison.Ordinal));
        if (ById != null) return ById;
        return Contract.Edges.Where(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)
            .OrderBy(Edge => Edge.CreationIndex).FirstOrDefault();
    }

    private static HashSet<FlowchartLayoutEdgeRoute> RedistributeAllocatedPorts(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        Dictionary<string, GridFrame> FrameById = Frames.ToDictionary(Frame => Frame.Id, StringComparer.Ordinal);
        Dictionary<string, List<PortEnd>> Groups = new(StringComparer.Ordinal);
        Dictionary<FlowchartLayoutEdgeRoute, ((double X, double Y) Start, (double X, double Y) End)> OriginalEndpoints = [];
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 2
                || string.IsNullOrEmpty(Route.ExitSide)
                || string.IsNullOrEmpty(Route.EntrySide)
                || !TryBox(Route.SourceId, ById, FrameById, out BoxRef Source)
                || !TryBox(Route.TargetId, ById, FrameById, out BoxRef Target))
            {
                continue;
            }

            bool IsHinted = FindContractEdge(Contract, Route)?.RoutingHint?.IsActive == true;
            if (IsHinted) continue;
            // Hinted routes still occupy their symmetric slot. Excluding them
            // here must be compensated by the final hint allocator, which
            // allocates hinted and automatic endpoints on each affected side as
            // one group. Keep that allocator as the single source of truth.
            OriginalEndpoints[Route] = ((Route.Points[0].X, Route.Points[0].Y),
                (Route.Points[^1].X, Route.Points[^1].Y));
            Add(Source, Target, Route, true, Route.ExitSide);
            Add(Target, Source, Route, false, Route.EntrySide);
        }

        foreach (KeyValuePair<string, List<PortEnd>> Pair in Groups)
        {
            List<PortEnd> Ends = Pair.Value;
            bool Diamond = Pair.Key.EndsWith("|diamond", StringComparison.Ordinal);
            if (!Diamond)
            {
                Ends.Sort((Left, Right) =>
                {
                    int HintOrder = Right.IsHinted.CompareTo(Left.IsHinted);
                    if (HintOrder != 0) return HintOrder;
                    bool VerticalSide = Left.Side is "left" or "right";
                    return VerticalSide
                        ? Right.Other.CenterY.CompareTo(Left.Other.CenterY)
                        : Left.Other.CenterX.CompareTo(Right.Other.CenterX);
                });
            }
            if (!Diamond)
                ApplySlots(Ends, Nodes);
        }

        HashSet<FlowchartLayoutEdgeRoute> Changed = [];
        foreach (KeyValuePair<FlowchartLayoutEdgeRoute, ((double X, double Y) Start, (double X, double Y) End)> Pair in OriginalEndpoints)
        {
            FlowchartLayoutEdgeRoute Route = Pair.Key;
            if (Pair.Value.Start != (Route.Points[0].X, Route.Points[0].Y)
                || Pair.Value.End != (Route.Points[^1].X, Route.Points[^1].Y))
            {
                Changed.Add(Route);
            }
        }
        return Changed;

        void Add(BoxRef Box, BoxRef Other, FlowchartLayoutEdgeRoute Route,
            bool IsSource, string Side)
        {
            bool Diamond = !Box.IsFrame && Nodes.TryGetValue(Box.Id, out NodeContract? Node)
                && FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
            string Key = Box.Id + (Diamond ? "|diamond" : "|" + Side);
            if (!Groups.TryGetValue(Key, out List<PortEnd>? Ends))
                Groups[Key] = Ends = [];
            Ends.Add(new PortEnd
            {
                Route = Route,
                IsSource = IsSource,
                IsHinted = false,
                Side = Side,
                Box = Box,
                Other = Other
            });
        }
    }

    internal static string HintSide(LayoutHintDirection Direction) => Direction switch
    {
        LayoutHintDirection.North => "top",
        LayoutHintDirection.South => "bottom",
        LayoutHintDirection.East => "right",
        _ => "left"
    };

    private static void ApplySlots(List<PortEnd> Group, Dictionary<string, NodeContract> Nodes)
    {
        if (Group.Count == 0)
        {
            return;
        }

        BoxRef Box = Group[0].Box;
        string Side = Group[0].Side;
        bool Diamond = Nodes.TryGetValue(Box.Id, out NodeContract? Node) && FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
        if (Diamond && !Box.IsFrame)
        {
            List<(double X, double Y)> Destinations = new List<(double X, double Y)>();
            foreach (PortEnd End in Group)
            {
                Destinations.Add((End.Other.CenterX - Box.CenterX, End.Other.CenterY - Box.CenterY));
            }

            if (FlowchartDiamondPorts.TryAssign((int)Box.Width, (int)Box.Height, Destinations, out IReadOnlyList<FlowchartDiamondPort> Ports)
                && Ports.Count == Group.Count)
            {
                for (int Index = 0; Index < Group.Count; Index++)
                {
                    FlowchartDiamondPort Port = Ports[Index];
                    double X = Box.CenterX + Port.OffsetX;
                    double Y = Box.CenterY + Port.OffsetY;
                    SetEnd(Group[Index], X, Y, Port.DepartureX, Port.DepartureY);
                }

                return;
            }
        }

        bool VerticalSide = Side == "left" || Side == "right";
        int Length = (int)Math.Round(VerticalSide ? Box.Height : Box.Width);
        bool Circle = !Box.IsFrame && Nodes.TryGetValue(Box.Id, out NodeContract? CircleNode)
            && IsCircleShape(CircleNode.Shape);
        if (Circle)
        {
            double Radius = Math.Min(Box.Width, Box.Height) / 2.0;
            for (int Index = 0; Index < Group.Count; Index++)
            {
                double Fraction = (Index + 1.0) / (Group.Count + 1.0);
                double Angle = Side switch
                {
                    "top" => Math.PI * (1.0 - Fraction),
                    "bottom" => Math.PI * (1.0 + Fraction),
                    "left" => (Math.PI / 2.0) + (Math.PI * Fraction),
                    _ => (Math.PI / 2.0) - (Math.PI * Fraction)
                };
                double NormalX = Math.Cos(Angle), NormalY = Math.Sin(Angle);
                double X = Box.CenterX + (Radius * NormalX);
                double Y = Box.CenterY + (Radius * NormalY);
                SetEnd(Group[Index], X, Y, NormalX, NormalY);
            }

            return;
        }

        // Rounded rectangles still allocate over the complete declared edge.
        // Reserving a corner inset here moves ports away from k/(n+1) and makes
        // the slot pattern depend on the renderer's corner radius.
        double AvailableLength = Length;
        for (int Index = 0; Index < Group.Count; Index++)
        {
            double Along = ((Index + 1.0) / (Group.Count + 1.0)) * AvailableLength;
            double X;
            double Y;
            int OutX = 0;
            int OutY = 0;
            if (Side == "top")
            {
                X = Box.Left + Along;
                Y = Box.Top;
                OutY = 1;
            }
            else if (Side == "bottom")
            {
                X = Box.Left + Along;
                Y = Box.Bottom;
                OutY = -1;
            }
            else if (Side == "left")
            {
                X = Box.Left;
                Y = Box.Top - Along;
                OutX = -1;
            }
            else
            {
                X = Box.Right;
                Y = Box.Top - Along;
                OutX = 1;
            }

            bool HorizontalBracketEdge = Side is "top" or "bottom"
                && Nodes.TryGetValue(Box.Id, out NodeContract? BracketNode)
                && FlowchartTrapezoidBracketPathAnalyser.IsTrapezoidBracketShape(BracketNode.Shape);
            if (!Box.IsFrame && Nodes.TryGetValue(Box.Id, out NodeContract? ShapeNode)
                && !IsRectangular(ShapeNode.Shape) && !Diamond && !HorizontalBracketEdge)
            {
                (X, Y) = ProjectToOutline(Box, ShapeNode.Shape,
                    (int)Math.Round(X), (int)Math.Round(Y), OutX, OutY);
            }

            SetEnd(Group[Index], X, Y, OutX, OutY);
        }
    }

    private static (int X, int Y) ProjectToOutline(BoxRef Box, string? Shape, int SlotX, int SlotY, int OutX, int OutY)
    {
        if (string.Equals(Shape, "ellipse", StringComparison.OrdinalIgnoreCase))
        {
            double Rx = Box.Width / 2.0, Ry = Box.Height / 2.0;
            if (OutX != 0)
            {
                double Dy = (SlotY - Box.CenterY) / Ry;
                double Offset = Rx * Math.Sqrt(Math.Max(0, 1 - Dy * Dy));
                return ((int)Math.Round(Box.CenterX + OutX * Offset), SlotY);
            }
            double Dx = (SlotX - Box.CenterX) / Rx;
            double VerticalOffset = Ry * Math.Sqrt(Math.Max(0, 1 - Dx * Dx));
            return (SlotX, (int)Math.Round(Box.CenterY + OutY * VerticalOffset));
        }

        if (string.Equals(Shape, "circle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "doublecircle", StringComparison.OrdinalIgnoreCase))
        {
            double Radius = Math.Min(Box.Width, Box.Height) / 2.0;
            double Dx = SlotX - Box.CenterX;
            double Dy = SlotY - Box.CenterY;
            if (OutX != 0)
            {
                double Inside = (Radius * Radius) - (Dy * Dy);
                if (Inside > 0.0)
                {
                    double Offset = Math.Sqrt(Inside);
                    int X = (int)Math.Round(Box.CenterX + (OutX > 0 ? Offset : -Offset));
                    return (X, SlotY);
                }
            }

            if (OutY != 0)
            {
                double Inside = (Radius * Radius) - (Dx * Dx);
                if (Inside > 0.0)
                {
                    double Offset = Math.Sqrt(Inside);
                    int Y = (int)Math.Round(Box.CenterY + (OutY > 0 ? Offset : -Offset));
                    return (SlotX, Y);
                }
            }
        }

        FlowchartLayoutNodeBounds Bounds = new FlowchartLayoutNodeBounds
        {
            NodeId = Box.Id,
            CenterX = Box.CenterX,
            CenterY = Box.CenterY,
            Width = Box.Width,
            Height = Box.Height
        };
        (double AttachX, double AttachY) = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
            Bounds,
            Shape,
            SlotX + (OutX * 4.0),
            SlotY + (OutY * 4.0),
            EdgeEndpointMarker.None);
        return ((int)Math.Round(AttachX), (int)Math.Round(AttachY));
    }

    private static bool IsRectangular(string? Shape)
    {
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return true;
        }

        return string.Equals(Shape, "rect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "rectangle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "square", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "round", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "rounded", StringComparison.OrdinalIgnoreCase)
            // Sequence-diagram participants render as rectangular lifelines' heads.
            // Treating this non-flowchart shape name as a generic ray-clipped shape
            // pulled its edge ports toward the centre and broke k/(n+1) allocation.
            || string.Equals(Shape, "participant", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCircleShape(string? Shape) =>
        string.Equals(Shape, "circle", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Shape, "doublecircle", StringComparison.OrdinalIgnoreCase);

    private static void SetEnd(PortEnd End, double X, double Y, double OutX, double OutY)
    {
        End.OutX = OutX;
        End.OutY = OutY;
        if (End.IsSource)
        {
            End.Route.ExitNormalX = OutX;
            End.Route.ExitNormalY = OutY;
        }
        else
        {
            End.Route.EntryNormalX = OutX;
            End.Route.EntryNormalY = OutY;
        }
        FlowchartLayoutPoint Point = End.IsSource ? End.Route.Points[0] : End.Route.Points[End.Route.Points.Count - 1];
        Point.X = X;
        Point.Y = Y;
        End.Route.Points[End.IsSource ? 0 : End.Route.Points.Count - 1] = new FlowchartLayoutPoint
        {
            X = X,
            Y = Y
        };
    }

    private static void EnsureStub(FlowchartLayoutEdgeRoute Route, bool AtStart)
    {
        if (Route.Points.Count < 2)
        {
            return;
        }

        int EndIndex = AtStart ? 0 : Route.Points.Count - 1;
        int NextIndex = AtStart ? 1 : Route.Points.Count - 2;
        FlowchartLayoutPoint End = Route.Points[EndIndex];
        FlowchartLayoutPoint Next = Route.Points[NextIndex];
        double Dx = Next.X - End.X;
        double Dy = Next.Y - End.Y;
        bool Perpendicular = Math.Abs(Dx) <= 0.5 || Math.Abs(Dy) <= 0.5;
        if (Perpendicular && ((Dx * Dx) + (Dy * Dy)) >= (STUB_PX * STUB_PX))
        {
            return;
        }

        double Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
        double DirX = Length < 1e-6 ? 0.0 : Dx / Length;
        double DirY = Length < 1e-6 ? 1.0 : Dy / Length;
        if (Math.Abs(DirX) >= Math.Abs(DirY))
        {
            DirY = 0.0;
            DirX = DirX >= 0 ? 1.0 : -1.0;
        }
        else
        {
            DirX = 0.0;
            DirY = DirY >= 0 ? 1.0 : -1.0;
        }

        FlowchartLayoutPoint Stub = new FlowchartLayoutPoint
        {
            X = Math.Round(End.X + (DirX * STUB_PX)),
            Y = Math.Round(End.Y + (DirY * STUB_PX))
        };
        if (AtStart)
        {
            Route.Points.Insert(1, Stub);
        }
        else
        {
            Route.Points.Insert(Route.Points.Count - 1, Stub);
        }
    }

}

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
            RoutingHintContract? RoutingHint = ContractEdge?.RoutingHint?.IsActive == true
                ? ContractEdge.RoutingHint : null;
            bool CircularReturn = BackEdges.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)
                && (Nodes.TryGetValue(Route.SourceId, out var CircularSource) && CircularSource.Shape == "circle"
                    || Nodes.TryGetValue(Route.TargetId, out var CircularTarget) && CircularTarget.Shape == "circle");
            if (RoutingHint == null && !CircularReturn && TryAssignStraightPortSides(Source, Target, Direction, out string StraightExit, out string StraightEntry)
                && StraightChordIsClear(
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
                    : string.IsNullOrEmpty(Item.Route.ExitSide)
                        ? ChooseElbowExitSide(Contract, Item.Source, Item.Target, Direction,
                            ReservedCenterPorts, ObstacleBounds, Item.Route.SourceId, Item.Route.TargetId)
                        : Item.Route.ExitSide;
                Entry = RoutingHint.Enter.HasValue
                    ? HintSide(RoutingHint.Enter.Value)
                    : string.IsNullOrEmpty(Item.Route.EntrySide)
                        ? ChooseElbowEntrySide(Item.Target, Item.Source, Direction, Exit)
                        : Item.Route.EntrySide;
            }
            else if (FlowchartOrdinaryFanOutRouteBuilder.TryGetFlowSides(Direction, out string FanOutExit, out string FanOutEntry)
                && Nodes.TryGetValue(Item.Source.Id, out NodeContract? SourceNode)
                && (SharedFanSources.Contains(Item.Source.Id) || FlowchartOrdinaryFanOutRouteBuilder.TryIsOrdinaryFanOutHub(
                    Contract,
                    Item.Source.Id,
                    SourceNode.Shape,
                    Direction,
                    FanOutBoundsById)))
            {
                Exit = FanOutExit;
                Entry = FanOutEntry;
            }
            else
            {
                bool IsJunctionRoute = IsEnterpriseJunction(Item.Source.Id, Nodes)
                    || IsEnterpriseJunction(Item.Target.Id, Nodes);
                if (IsJunctionRoute || string.IsNullOrEmpty(Item.Route.ExitSide)
                    || string.IsNullOrEmpty(Item.Route.EntrySide))
                {
                    // A junction branch must enter on the side that faces its
                    // source. Dagre's sides belong to its old route and can
                    // leave a needless second corner or attach to the far
                    // side. Explicit line hints were handled above.
                    Exit = ChooseElbowExitSide(
                        Contract,
                        Item.Source,
                        Item.Target,
                        Direction,
                        ReservedCenterPorts,
                        ObstacleBounds,
                        Item.Route.SourceId,
                        Item.Route.TargetId);
                    Entry = ChooseElbowEntrySide(Item.Target, Item.Source, Direction, Exit);
                }
                else
                {
                    Exit = Item.Route.ExitSide;
                    Entry = Item.Route.EntrySide;
                }
            }
            double SourceCross = Horizontal ? Item.Source.CenterY : Item.Source.CenterX;
            double TargetCross = Horizontal ? Item.Target.CenterY : Item.Target.CenterX;
            if (!Horizontal && !Item.Source.IsFrame && !Item.Target.IsFrame
                && Math.Abs(SourceCross - TargetCross) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX
                && Nodes.TryGetValue(Item.Source.Id, out var ReturnSource)
                && Nodes.TryGetValue(Item.Target.Id, out var ReturnTarget)
                && !FlowchartDagrePresentation.IsDiamondShape(ReturnSource.Shape)
                && !FlowchartDagrePresentation.IsDiamondShape(ReturnTarget.Shape)
                && BackEdges.Any(Edge => Edge.SourceId == Item.Source.Id && Edge.TargetId == Item.Target.Id))
            {
                // An aligned return shares a clear side corridor, not inward-facing rank ports.
                double Span = Math.Abs(Item.Source.CenterY - Item.Target.CenterY);
                var OtherSpans = BackEdges.Where(Edge => Edge.TargetId == Item.Target.Id && Edge.SourceId != Item.Source.Id
                    && ById.ContainsKey(Edge.SourceId))
                    .Select(Edge => ById[Edge.SourceId])
                    .Where(Other => Math.Abs(Other.X - Item.Target.CenterX) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
                    .Select(Other => Math.Abs(Other.Y - Item.Target.CenterY)).ToList();
                // Keep the longest return outside the shorter nested returns.
                Exit = Entry = OtherSpans.Any(Other => Other < Span - 0.5)
                    && OtherSpans.All(Other => Other <= Span + 0.5) ? "left" : "right";
            }
            double CrossGap = Math.Abs(TargetCross - SourceCross)
                - (Horizontal ? Item.Source.Height + Item.Target.Height : Item.Source.Width + Item.Target.Width) / 2;
            if (CrossGap > 0)
            {
                if (!Item.Source.IsFrame && Nodes.TryGetValue(Item.Source.Id, out var HubNode)
                    && !FlowchartDagrePresentation.IsDiamondShape(HubNode.Shape)
                    && Contract.Edges.Count(Edge => Edge.SourceId == Item.Source.Id
                        && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)) > 3)
                    Exit = Horizontal ? (TargetCross > SourceCross ? "top" : "bottom")
                        : (TargetCross > SourceCross ? "right" : "left");
                if (!Item.Target.IsFrame && Nodes.TryGetValue(Item.Target.Id, out var CollectorNode)
                    && !FlowchartDagrePresentation.IsDiamondShape(CollectorNode.Shape)
                    && Contract.Edges.Count(Edge => Edge.TargetId == Item.Target.Id
                        && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)) > 3)
                    Entry = Horizontal ? (SourceCross > TargetCross ? "top" : "bottom")
                        : (SourceCross > TargetCross ? "right" : "left");
            }
            Item.Route.ExitSide = Exit;
            Item.Route.EntrySide = Entry;
            Ends.Add(new PortEnd
            {
                Route = Item.Route,
                IsSource = true,
                Side = Exit,
                Box = Item.Source,
                Other = Item.Target
            });
            Ends.Add(new PortEnd
            {
                Route = Item.Route,
                IsSource = false,
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
                bool VerticalSide = Left.Side == "left" || Left.Side == "right";
                if (VerticalSide)
                {
                    return Right.Other.CenterY.CompareTo(Left.Other.CenterY);
                }

                return Left.Other.CenterX.CompareTo(Right.Other.CenterX);
            });

            ApplySlots(Group, Nodes);
        }

        KeepFramePortsClearOfTitles(Contract, Ends);
        AlignFacingFramePorts(Contract, Ends, ObstacleBounds);

    }

    internal static EdgeContract? FindContractEdge(DiagramContract Contract, FlowchartLayoutEdgeRoute Route)
    {
        EdgeContract? ById = Contract.Edges.FirstOrDefault(Edge =>
            !string.IsNullOrEmpty(Edge.Id) && string.Equals(Edge.Id, Route.EdgeId, StringComparison.Ordinal));
        if (ById != null) return ById;
        return Contract.Edges.Where(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)
            .OrderBy(Edge => Edge.CreationIndex).FirstOrDefault();
    }

    private static bool IsEnterpriseJunction(string NodeId, IReadOnlyDictionary<string, NodeContract> Nodes)
    {
        return Nodes.TryGetValue(NodeId, out NodeContract? Node)
            && Node.Metadata.TryGetValue("enterpriseElementType", out MermaidMetadataValue? Metadata)
            && Metadata is RawMetadata Raw
            && string.Equals(Raw.RawValue, "Junction", StringComparison.Ordinal);
    }

    internal static string HintSide(LayoutHintDirection Direction) => Direction switch
    {
        LayoutHintDirection.North => "top",
        LayoutHintDirection.South => "bottom",
        LayoutHintDirection.East => "right",
        _ => "left"
    };

    private static void AlignFacingFramePorts(DiagramContract Contract, List<PortEnd> Ends,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Obstacles)
    {
        foreach (PortEnd Top in Ends.Where(End => End.Box.IsFrame && End.Side == "top"))
        {
            PortEnd? Bottom = Ends.FirstOrDefault(End => ReferenceEquals(End.Route, Top.Route)
                && End.IsSource != Top.IsSource && End.Box.IsFrame && End.Side == "bottom");
            if (Bottom is null || Top.Box.Top >= Bottom.Box.Bottom
                || Math.Abs(Top.Box.CenterX - Bottom.Box.CenterX) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
                continue;
            // A title can displace one end of an otherwise straight frame-to-frame
            // connection. Carry that track to the facing port if it is still free.
            double X = (Top.IsSource ? Top.Route.Points[0] : Top.Route.Points[^1]).X;
            if (X < Bottom.Box.Left + STUB_PX || X > Bottom.Box.Right - STUB_PX
                || Ends.Any(End => !ReferenceEquals(End.Route, Top.Route)
                    && End.Box.Id == Bottom.Box.Id && End.Side == "bottom"
                    && Math.Abs((End.IsSource ? End.Route.Points[0] : End.Route.Points[^1]).X - X) < STUB_PX))
                continue;
            bool Clear = Obstacles.All(Obstacle => Obstacle.Key == Top.Box.Id || Obstacle.Key == Bottom.Box.Id
                || (Contract.Groups.Any(Group => Group.Id == Obstacle.Key)
                    && (IsInsideGroup(Contract, Top.Box.Id, Obstacle.Key)
                        || IsInsideGroup(Contract, Bottom.Box.Id, Obstacle.Key)))
                || !FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    X, Top.Box.Top, X, Bottom.Box.Bottom, Obstacle.Value));
            if (Clear)
                SetEnd(Bottom, (int)Math.Round(X), (int)Math.Round(Bottom.Box.Bottom), 0, -1);
        }
    }

    private static void KeepFramePortsClearOfTitles(DiagramContract Contract, List<PortEnd> Ends)
    {
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (var FrameEnds in Ends.Where(End => End.Box.IsFrame && End.Side == "top")
            .GroupBy(End => End.Box.Id))
        {
            GroupContract? Frame = Contract.Groups.FirstOrDefault(Group => Group.Id == FrameEnds.Key);
            if (Frame is null) continue;
            string Title = string.IsNullOrWhiteSpace(Frame.Title) ? Frame.Id : Frame.Title;
            double HalfTitle = Math.Max(
                FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Title, Options.FontSizePx),
                Title.Length * Options.FontSizePx * 0.58) / 2;
            double Offset = HalfTitle + STUB_PX;
            List<double> Used = FrameEnds.Select(End =>
                (End.IsSource ? End.Route.Points[0] : End.Route.Points[^1]).X)
                .Where(X => Math.Abs(X - FrameEnds.First().Box.CenterX) >= Offset).ToList();
            foreach (PortEnd End in FrameEnds)
            {
                FlowchartLayoutPoint Point = End.IsSource ? End.Route.Points[0] : End.Route.Points[^1];
                if (Math.Abs(Point.X - End.Box.CenterX) >= Offset)
                {
                    continue;
                }
                // Reserve the title band before routing, while endpoints can
                // still be moved together with their normal terminal runs.
                List<double> Candidates = [];
                if (Math.Abs(End.Other.CenterX - End.Box.CenterX) >= Offset
                    && End.Other.CenterX >= End.Box.Left + STUB_PX
                    && End.Other.CenterX <= End.Box.Right - STUB_PX)
                {
                    Candidates.Add(End.Other.CenterX);
                }
                for (double Step = Offset; Step <= End.Box.Width / 2 - STUB_PX; Step += STUB_PX)
                {
                    Candidates.Add(End.Box.CenterX - Step);
                    Candidates.Add(End.Box.CenterX + Step);
                }
                double? X = Candidates.OrderBy(Value => Math.Abs(Value - End.Other.CenterX))
                    .Where(Value => Used.All(Existing => Math.Abs(Value - Existing) >= STUB_PX))
                    .Select(Value => (double?)Value).FirstOrDefault();
                if (X.HasValue)
                {
                    SetEnd(End, (int)Math.Round(X.Value), (int)Math.Round(End.Box.Top), 0, 1);
                    Used.Add(X.Value);
                }
            }
        }
    }

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
                    int X = (int)Math.Round(Box.CenterX + Port.OffsetX);
                    int Y = (int)Math.Round(Box.CenterY + Port.OffsetY);
                    SetEnd(Group[Index], X, Y, Port.DepartureX, Port.DepartureY);
                }

                return;
            }
        }

        bool VerticalSide = Side == "left" || Side == "right";
        int Length = (int)Math.Round(VerticalSide ? Box.Height : Box.Width);
        int CornerInset = !Box.IsFrame && Nodes.TryGetValue(Box.Id, out var RoundedNode)
            && (string.Equals(RoundedNode.Shape, "rounded", StringComparison.OrdinalIgnoreCase)
                || string.Equals(RoundedNode.Shape, "round", StringComparison.OrdinalIgnoreCase))
            ? Math.Min(STUB_PX, Length / 4) : 0;
        int SlotCount = Math.Max(DiagramGridMetrics.TRACKS_PER_GAP, Group.Count);
        if (Group.Count > 1 && Group.All(End => End.Other.Id == Group[0].Other.Id))
            foreach (var End in Group) End.KeepsCentre = false;
        int CentreEnd = Group.FindIndex(End => End.KeepsCentre);
        if (CentreEnd >= 0)
        {
            SlotCount = Math.Max(SlotCount, 2 * Math.Max(CentreEnd, Group.Count - CentreEnd - 1) + 1);
            if (SlotCount % 2 == 0) { SlotCount++; }
        }
        int[] Centres = DiagramGridMetrics.EqualCentres(Math.Max(1, Length - 2 * CornerInset), SlotCount);
        int[] Indexes = DiagramGridMetrics.SelectSlotIndexes(Group.Count, SlotCount);
        for (int Index = 0; Index < Group.Count; Index++)
        {
            int Slot = CentreEnd >= 0
                ? SlotCount / 2 + Index - CentreEnd : Indexes[Math.Min(Index, Indexes.Length - 1)];
            int Along = CornerInset + Centres[Slot];
            int X;
            int Y;
            int OutX = 0;
            int OutY = 0;
            if (Side == "top")
            {
                X = (int)Math.Round(Box.Left + Along);
                Y = (int)Math.Round(Box.Top);
                OutY = 1;
            }
            else if (Side == "bottom")
            {
                X = (int)Math.Round(Box.Left + Along);
                Y = (int)Math.Round(Box.Bottom);
                OutY = -1;
            }
            else if (Side == "left")
            {
                X = (int)Math.Round(Box.Left);
                Y = (int)Math.Round(Box.Top - Along);
                OutX = -1;
            }
            else
            {
                X = (int)Math.Round(Box.Right);
                Y = (int)Math.Round(Box.Top - Along);
                OutX = 1;
            }

            if (!Box.IsFrame && Nodes.TryGetValue(Box.Id, out NodeContract? ShapeNode) && !IsRectangular(ShapeNode.Shape) && !Diamond)
            {
                (X, Y) = ProjectToOutline(Box, ShapeNode.Shape, X, Y, OutX, OutY);
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
            || string.Equals(Shape, "rounded", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetEnd(PortEnd End, int X, int Y, int OutX, int OutY)
    {
        End.OutX = OutX;
        End.OutY = OutY;
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

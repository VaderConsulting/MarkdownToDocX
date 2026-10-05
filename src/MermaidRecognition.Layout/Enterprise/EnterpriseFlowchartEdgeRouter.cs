using System;
using System.Collections.Generic;
using System.Text;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Routes enterprise relationships through the existing flowchart edge-routing path
/// (<see cref="FlowchartEdgeRouteResolver"/>, <see cref="FlowchartCurvePathBuilder"/>,
/// <see cref="FlowchartEdgeLabelPlacementHelper"/>).
/// </summary>
internal static class EnterpriseFlowchartEdgeRouter
{
    /// <summary>
    /// Resolves every visible relationship using flowchart routing, keeping ArchiMate markers.
    /// </summary>
    /// <param name="Document">Validated document.</param>
    /// <param name="Nodes">Laid-out nodes.</param>
    /// <returns>Routed edges in SVG coordinates (Y down).</returns>
    internal static List<EnterpriseRoutedEdge> Route(
        EnterpriseDocument Document,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        DiagramContract Contract = BuildRoutingContract(Document, Nodes);
        Dictionary<string, FlowchartLayoutNodeBounds> LeafBounds = BuildLeafBounds(Nodes);
        Dictionary<string, NodeContract> NodeById = new Dictionary<string, NodeContract>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            NodeById[Node.Id] = Node;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(
            Document.Direction == EnterpriseDiagramDirection.Lr ? "LR" : "TB");
        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);

        Dictionary<(string NodeId, EnterprisePortSide Side), List<EnterpriseRelationship>> Slots =
            PlanPortSlots(Document, Nodes);
        List<EnterpriseVisibleHop> Hops = CollectVisibleHops(Document, Nodes, Slots);
        if (Hops.Count == 0)
        {
            return new List<EnterpriseRoutedEdge>();
        }

        bool NeedsOrthogonal = false;
        foreach (EnterpriseVisibleHop Hop in Hops)
        {
            if (EnterpriseOrthogonalRouter.CrossesContainer(Hop.Source, Hop.Target, Nodes))
            {
                NeedsOrthogonal = true;
                break;
            }
        }

        if (!NeedsOrthogonal)
        {
            List<EnterpriseRoutedEdge>? FlowchartRouted = TryRouteAllFlowchart(
                Hops,
                Contract,
                LeafBounds,
                NodeById,
                Direction,
                RenderOptions,
                Nodes);
            if (FlowchartRouted != null)
            {
                return FlowchartRouted;
            }
        }

        return RouteAllOrthogonal(Hops, Nodes);
    }

    internal static void Append(
        StringBuilder Builder,
        EnterpriseRoutedEdge Routed)
    {
        EnterpriseRelationshipMetadata Spec = EnterpriseElementCatalog.Relationships[Routed.Relationship.Kind];
        Builder.Append("<g class=\"relationship ")
            .Append(Spec.Name)
            .Append("\" data-source=\"")
            .Append(EnterpriseSvgFormat.Escape(Routed.Relationship.SourceId))
            .Append("\" data-target=\"")
            .Append(EnterpriseSvgFormat.Escape(Routed.Relationship.TargetId))
            .Append("\"><path d=\"")
            .Append(Routed.PathData)
            .Append("\" fill=\"none\" stroke=\"#000\" stroke-width=\"1\"");
        if (Routed.DashArray != null)
        {
            Builder.Append(" stroke-dasharray=\"").Append(Routed.DashArray).Append('"');
        }

        Builder.Append("/>");
        if (Routed.SvgPoints.Count >= 2)
        {
            (double X1, double Y1) = Routed.SvgPoints[0];
            (double X2, double Y2) = Routed.SvgPoints[1];
            (double Xn1, double Yn1) = Routed.SvgPoints[Routed.SvgPoints.Count - 2];
            (double Xn, double Yn) = Routed.SvgPoints[Routed.SvgPoints.Count - 1];
            (double Usx, double Usy) = Unit(X2, Y2, X1, Y1);
            (double Uex, double Uey) = Unit(Xn1, Yn1, Xn, Yn);
            Builder.Append(EnterpriseNotationSvg.EndSvg(Routed.StartMarker, X1, Y1, Usx, Usy).Svg);
            Builder.Append(EnterpriseNotationSvg.EndSvg(Routed.EndMarker, Xn, Yn, Uex, Uey).Svg);
        }

        if (Routed.Label.Length > 0)
        {
            double LabelWidth = (Routed.Label.Length * 7.0) + 10;
            double LabelHeight = 16;
            Builder.Append("<rect x=\"")
                .Append(EnterpriseSvgFormat.Number(Routed.LabelX - (LabelWidth / 2.0)))
                .Append("\" y=\"")
                .Append(EnterpriseSvgFormat.Number(Routed.LabelY - 8))
                .Append("\" width=\"")
                .Append(EnterpriseSvgFormat.Number(LabelWidth))
                .Append("\" height=\"")
                .Append(EnterpriseSvgFormat.Number(LabelHeight))
                .Append("\" rx=\"2\" fill=\"#ffffff\" stroke=\"#ffffff\"/>");
            Builder.Append("<text x=\"")
                .Append(EnterpriseSvgFormat.Number(Routed.LabelX))
                .Append("\" y=\"")
                .Append(EnterpriseSvgFormat.Number(Routed.LabelY + 4))
                .Append("\" text-anchor=\"middle\" font-size=\"12\" font-family=\"&quot;trebuchet ms&quot;, verdana, arial, sans-serif\" fill=\"#333\">")
                .Append(EnterpriseSvgFormat.Escape(Routed.Label))
                .Append("</text>");
        }

        Builder.Append("</g>");
    }

    private static List<EnterpriseVisibleHop> CollectVisibleHops(
        EnterpriseDocument Document,
        Dictionary<string, EnterpriseLayoutNode> Nodes,
        Dictionary<(string NodeId, EnterprisePortSide Side), List<EnterpriseRelationship>> Slots)
    {
        List<EnterpriseVisibleHop> Hops = new List<EnterpriseVisibleHop>();
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (Relationship.HiddenByNesting)
            {
                continue;
            }

            if (!Nodes.TryGetValue(Relationship.SourceId, out EnterpriseLayoutNode? Source)
                || !Nodes.TryGetValue(Relationship.TargetId, out EnterpriseLayoutNode? Target))
            {
                continue;
            }

            Hops.Add(new EnterpriseVisibleHop(
                Relationship,
                Source,
                Target,
                PortFor(Relationship, Source, Target, Slots),
                PortFor(Relationship, Target, Source, Slots)));
        }

        return Hops;
    }

    private static List<EnterpriseRoutedEdge>? TryRouteAllFlowchart(
        List<EnterpriseVisibleHop> Hops,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> LeafBounds,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        FlowchartRenderOptions RenderOptions,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        HashSet<string> UsedEdgeIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> UsedPorts = new HashSet<string>(StringComparer.Ordinal);
        List<EnterpriseRoutedEdge> Routed = new List<EnterpriseRoutedEdge>();
        foreach (EnterpriseVisibleHop Hop in Hops)
        {
            EdgeContract? Edge = FindUnusedEdge(Contract, Hop.Relationship, UsedEdgeIds);
            if (Edge == null)
            {
                return null;
            }

            UsedEdgeIds.Add(Edge.Id ?? string.Empty);
            Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
                BoundsForEdge(Hop.Relationship, Nodes, LeafBounds);
            if (!FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints,
                    out FlowchartEdgeRouteKind RouteKind)
                || LayoutPoints.Count < 2)
            {
                return null;
            }

            List<(double X, double Y)> SvgPoints = new List<(double X, double Y)>();
            foreach ((double LayoutX, double LayoutY) in LayoutPoints)
            {
                SvgPoints.Add((LayoutX, ToSvgY(LayoutY)));
            }

            string PathData = FlowchartCurvePathBuilder.BuildRenderedEdgePathData(SvgPoints, RouteKind, Direction);
            if (string.IsNullOrWhiteSpace(PathData))
            {
                PathData = FlowchartCurvePathBuilder.BuildPathDataFromGuidePoints(SvgPoints, RenderOptions.Curve);
            }

            if (EnterpriseEdgeClearance.PathDataHitsObstacle(PathData, Hop.Source, Hop.Target, Nodes)
                || SharesPort(UsedPorts, SvgPoints)
                || !NearPort(SvgPoints[0], Hop.SourcePort)
                || !NearPort(SvgPoints[SvgPoints.Count - 1], Hop.TargetPort)
                || !PointOnBorder(SvgPoints[0], Hop.Source)
                || !PointOnBorder(SvgPoints[SvgPoints.Count - 1], Hop.Target)
                || HitsOwnLabelOrTitle(PathData, Hop.Source, Hop.Target))
            {
                return null;
            }

            string Label = EdgeLabel(Hop.Relationship);
            double LabelX = 0;
            double LabelY = 0;
            if (Label.Length > 0)
            {
                (LabelX, LabelY) = EnterpriseOrthogonalRouter.LabelAnchor(SvgPoints);
            }

            RememberPorts(UsedPorts, SvgPoints);
            ResolveArchiMateMarkers(Hop.Relationship, out string? StartMarker, out string? EndMarker);
            Routed.Add(new EnterpriseRoutedEdge(
                Hop.Relationship,
                PathData,
                SvgPoints,
                StartMarker,
                EndMarker,
                DashArray(Hop.Relationship),
                Label,
                LabelX,
                LabelY));
        }

        return Routed;
    }

    private static List<EnterpriseRoutedEdge> RouteAllOrthogonal(
        List<EnterpriseVisibleHop> Hops,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        List<EnterpriseRouteWork> Work = new List<EnterpriseRouteWork>();
        foreach (EnterpriseVisibleHop Hop in Hops)
        {
            Work.Add(new EnterpriseRouteWork(Hop.Source, Hop.Target, Hop.SourcePort, Hop.TargetPort));
        }

        List<List<(double X, double Y)>> Paths = EnterpriseOrthogonalRouter.RouteAll(Work, Nodes);
        List<EnterpriseRoutedEdge> Routed = new List<EnterpriseRoutedEdge>();
        for (int Index = 0; Index < Hops.Count; Index++)
        {
            EnterpriseVisibleHop Hop = Hops[Index];
            List<(double X, double Y)> SvgPoints = Paths[Index];
            if (SvgPoints.Count < 2)
            {
                continue;
            }

            string Label = EdgeLabel(Hop.Relationship);
            double LabelX = 0;
            double LabelY = 0;
            if (Label.Length > 0)
            {
                (LabelX, LabelY) = EnterpriseOrthogonalRouter.LabelAnchor(SvgPoints);
            }

            ResolveArchiMateMarkers(Hop.Relationship, out string? StartMarker, out string? EndMarker);
            Routed.Add(new EnterpriseRoutedEdge(
                Hop.Relationship,
                EnterpriseOrthogonalRouter.BuildRoundedPathData(SvgPoints),
                SvgPoints,
                StartMarker,
                EndMarker,
                DashArray(Hop.Relationship),
                Label,
                LabelX,
                LabelY));
        }

        return Routed;
    }

    private static bool PointOnBorder((double X, double Y) Point, EnterpriseLayoutNode Node)
    {
        return EnterpriseObstacleCatalog.BoundsOf(Node).ContainsOnBorder(Point.X, Point.Y, 2.5);
    }

    private static bool HitsOwnLabelOrTitle(
        string PathData,
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target)
    {
        if (!FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathData, out List<(double X, double Y)> Samples)
            || Samples.Count < 2)
        {
            return true;
        }

        double Total = 0;
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            double Dx = Samples[Index].X - Samples[Index - 1].X;
            double Dy = Samples[Index].Y - Samples[Index - 1].Y;
            Total += Math.Sqrt((Dx * Dx) + (Dy * Dy));
        }

        double Traversed = 0;
        for (int Index = 0; Index < Samples.Count - 1; Index++)
        {
            (double X1, double Y1) = Samples[Index];
            (double X2, double Y2) = Samples[Index + 1];
            double SegDx = X2 - X1;
            double SegDy = Y2 - Y1;
            double Segment = Math.Sqrt((SegDx * SegDx) + (SegDy * SegDy));
            bool NearEnd = Traversed + Segment <= 8 || Traversed >= Total - 8;
            Traversed += Segment;
            if (NearEnd)
            {
                continue;
            }

            EnterpriseRect? SourceLabel = EnterpriseObstacleCatalog.LabelBandOf(Source);
            EnterpriseRect? TargetLabel = EnterpriseObstacleCatalog.LabelBandOf(Target);
            if (SourceLabel.HasValue && SourceLabel.Value.IntersectsSegment(X1, Y1, X2, Y2, 0.5))
            {
                return true;
            }

            if (TargetLabel.HasValue && TargetLabel.Value.IntersectsSegment(X1, Y1, X2, Y2, 0.5))
            {
                return true;
            }
        }

        return false;
    }

    private static DiagramContract BuildRoutingContract(
        EnterpriseDocument Document,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        DiagramContract Contract = new DiagramContract
        {
            Kind = "flowchart",
            Header = "flowchart",
            Direction = Document.Direction == EnterpriseDiagramDirection.Lr ? "LR" : "TB"
        };

        foreach (EnterpriseLayoutNode Node in Nodes.Values)
        {
            if (Node.IsGroup)
            {
                GroupContract Group = new GroupContract
                {
                    Id = Node.Id,
                    Title = Node.DisplayLabel,
                    ParentGroupId = Node.ParentId
                };
                foreach (EnterpriseLayoutNode Child in Node.Children)
                {
                    if (Child.IsGroup)
                    {
                        Group.ChildGroupIdsMutable.Add(Child.Id);
                    }
                    else
                    {
                        Group.ChildNodeIdsMutable.Add(Child.Id);
                    }
                }

                Contract.GroupsMutable.Add(Group);
            }

            if (Node.Element == null && Node.IsGroup)
            {
                continue;
            }

            NodeContract NodeContract = new NodeContract
            {
                Id = Node.Id,
                Label = Node.DisplayLabel,
                Shape = RoutingShape(Node),
                ParentGroupId = Node.ParentId
            };
            Contract.NodesMutable.Add(NodeContract);
        }

        int CreationIndex = 0;
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (Relationship.HiddenByNesting)
            {
                continue;
            }

            string RouteSourceId = Relationship.SourceId;
            string RouteTargetId = Relationship.TargetId;

            EdgeContract Edge = new EdgeContract
            {
                Id = RouteSourceId + "->" + RouteTargetId + ":" + CreationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SourceId = RouteSourceId,
                TargetId = RouteTargetId,
                Label = EdgeLabel(Relationship),
                Stroke = FlowchartStroke(Relationship),
                MarkerStart = EdgeEndpointMarker.None,
                MarkerEnd = EdgeEndpointMarker.None,
                CreationIndex = CreationIndex
            };
            Contract.EdgesMutable.Add(Edge);
            CreationIndex++;
        }

        return Contract;
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> BuildLeafBounds(
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (EnterpriseLayoutNode Node in Nodes.Values)
        {
            if (Node.IsGroup)
            {
                continue;
            }

            BoundsById[Node.Id] = RoutingBounds(Node);
        }

        return BoundsById;
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> BoundsForEdge(
        EnterpriseRelationship Relationship,
        Dictionary<string, EnterpriseLayoutNode> Nodes,
        Dictionary<string, FlowchartLayoutNodeBounds> LeafBounds)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(LeafBounds, StringComparer.Ordinal);
        AddEndpointBounds(BoundsById, Relationship.SourceId, Nodes);
        AddEndpointBounds(BoundsById, Relationship.TargetId, Nodes);
        return BoundsById;
    }

    private static void AddEndpointBounds(
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string NodeId,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        if (BoundsById.ContainsKey(NodeId))
        {
            return;
        }

        if (!Nodes.TryGetValue(NodeId, out EnterpriseLayoutNode? Node))
        {
            return;
        }

        BoundsById[NodeId] = RoutingBounds(Node);
    }

    private static FlowchartLayoutNodeBounds RoutingBounds(EnterpriseLayoutNode Node)
    {
        if (Node.Element != null && Node.Element.ElementType == EnterpriseElementType.Junction)
        {
            return new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.AbsoluteX + 13,
                CenterY = ToLayoutY(Node.AbsoluteY + 13),
                Width = 26,
                Height = 26
            };
        }

        return new FlowchartLayoutNodeBounds
        {
            NodeId = Node.Id,
            CenterX = Node.AbsoluteX + (Node.Width / 2.0),
            CenterY = ToLayoutY(Node.AbsoluteY + (Node.Height / 2.0)),
            Width = Node.Width,
            Height = Node.Height
        };
    }

    private static EdgeContract? FindUnusedEdge(
        DiagramContract Contract,
        EnterpriseRelationship Relationship,
        HashSet<string> UsedEdgeIds)
    {
        string RouteSourceId = Relationship.SourceId;
        string RouteTargetId = Relationship.TargetId;

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (Edge.SourceId == RouteSourceId
                && Edge.TargetId == RouteTargetId
                && !UsedEdgeIds.Contains(Edge.Id ?? string.Empty))
            {
                return Edge;
            }
        }

        return null;
    }

    private static string RoutingShape(EnterpriseLayoutNode Node)
    {
        if (Node.Element != null && Node.Element.ElementType == EnterpriseElementType.Junction)
        {
            return "circle";
        }

        if (Node.Element != null)
        {
            EnterpriseElementMetadata Metadata = EnterpriseElementCatalog.Elements[Node.Element.ElementType];
            if (Metadata.BoxKind == EnterpriseBoxKind.Ellipse)
            {
                return "circle";
            }
        }

        return "rect";
    }

    private static Dictionary<(string NodeId, EnterprisePortSide Side), List<EnterpriseRelationship>> PlanPortSlots(
        EnterpriseDocument Document,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        Dictionary<(string NodeId, EnterprisePortSide Side), List<EnterpriseRelationship>> Slots =
            new Dictionary<(string, EnterprisePortSide), List<EnterpriseRelationship>>();
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (Relationship.HiddenByNesting)
            {
                continue;
            }

            if (!Nodes.TryGetValue(Relationship.SourceId, out EnterpriseLayoutNode? Source)
                || !Nodes.TryGetValue(Relationship.TargetId, out EnterpriseLayoutNode? Target))
            {
                continue;
            }

            AddSlot(Slots, Source, Target, Relationship);
            AddSlot(Slots, Target, Source, Relationship);
        }

        return Slots;
    }

    private static void AddSlot(
        Dictionary<(string NodeId, EnterprisePortSide Side), List<EnterpriseRelationship>> Slots,
        EnterpriseLayoutNode From,
        EnterpriseLayoutNode Toward,
        EnterpriseRelationship Relationship)
    {
        EnterprisePortSide Side = PreferredSide(From, Toward);
        (string, EnterprisePortSide) Key = (From.Id, Side);
        if (!Slots.TryGetValue(Key, out List<EnterpriseRelationship>? List))
        {
            List = new List<EnterpriseRelationship>();
            Slots[Key] = List;
        }

        List.Add(Relationship);
    }

    private static EnterprisePortSide PreferredSide(
        EnterpriseLayoutNode From,
        EnterpriseLayoutNode Toward)
    {
        EnterpriseRect FromBounds = EnterpriseObstacleCatalog.BoundsOf(From);
        EnterpriseRect TowardBounds = EnterpriseObstacleCatalog.BoundsOf(Toward);
        EnterprisePortSide Side = EnterpriseOrthogonalRouter.FacingSide(FromBounds, TowardBounds.CentreX, TowardBounds.CentreY);
        if (From.IsCompound && !From.IsGroup && Side == EnterprisePortSide.Top)
        {
            return TowardBounds.CentreX < FromBounds.CentreX ? EnterprisePortSide.Left : EnterprisePortSide.Right;
        }

        return Side;
    }

    private static EnterprisePort PortFor(
        EnterpriseRelationship Relationship,
        EnterpriseLayoutNode From,
        EnterpriseLayoutNode Toward,
        Dictionary<(string NodeId, EnterprisePortSide Side), List<EnterpriseRelationship>> Slots)
    {
        EnterprisePortSide Side = PreferredSide(From, Toward);
        if (!Slots.TryGetValue((From.Id, Side), out List<EnterpriseRelationship>? List) || List.Count == 0)
        {
            return EnterpriseOrthogonalRouter.PortOnNode(From, Side, 0.5);
        }

        int Index = List.IndexOf(Relationship);
        if (Index < 0)
        {
            Index = 0;
        }

        double SlotT = (Index + 1.0) / (List.Count + 1.0);
        return EnterpriseOrthogonalRouter.PortOnNode(From, Side, SlotT);
    }

    private static bool SharesPort(HashSet<string> UsedPorts, List<(double X, double Y)> Points)
    {
        if (Points.Count < 2)
        {
            return false;
        }

        return UsedPorts.Contains(PortKey(Points[0])) || UsedPorts.Contains(PortKey(Points[Points.Count - 1]));
    }

    private static void RememberPorts(HashSet<string> UsedPorts, List<(double X, double Y)> Points)
    {
        if (Points.Count < 2)
        {
            return;
        }

        UsedPorts.Add(PortKey(Points[0]));
        UsedPorts.Add(PortKey(Points[Points.Count - 1]));
    }

    private static bool NearPort((double X, double Y) Point, EnterprisePort Port)
    {
        return Math.Abs(Point.X - Port.X) <= 24 && Math.Abs(Point.Y - Port.Y) <= 24;
    }

    private static string PortKey((double X, double Y) Point)
    {
        return Math.Round(Point.X).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ","
            + Math.Round(Point.Y).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? FlowchartStroke(EnterpriseRelationship Relationship)
    {
        string Style = EnterpriseElementCatalog.Relationships[Relationship.Kind].LineStyle;
        if (Style == "dotted")
        {
            return "dotted";
        }

        if (Style == "dashed")
        {
            return "dashed";
        }

        return null;
    }

    private static string? DashArray(EnterpriseRelationship Relationship)
    {
        string Style = EnterpriseElementCatalog.Relationships[Relationship.Kind].LineStyle;
        if (Style == "dotted")
        {
            return "1.5 3";
        }

        if (Style == "dashed")
        {
            return "6 4";
        }

        return null;
    }

    private static void ResolveArchiMateMarkers(
        EnterpriseRelationship Relationship,
        out string? Start,
        out string? End)
    {
        EnterpriseRelationshipMetadata Spec = EnterpriseElementCatalog.Relationships[Relationship.Kind];
        Start = Spec.StartMarker;
        End = Spec.EndMarker;
        if (Relationship.Kind == EnterpriseRelationshipKind.Access)
        {
            EnterpriseAccessKind Access = Relationship.AccessKind ?? EnterpriseAccessKind.Write;
            Start = Access == EnterpriseAccessKind.Read || Access == EnterpriseAccessKind.ReadWrite ? "arrowOpenSmall" : null;
            End = Access == EnterpriseAccessKind.Write || Access == EnterpriseAccessKind.ReadWrite ? "arrowOpenSmall" : null;
        }

        if (Relationship.Kind == EnterpriseRelationshipKind.Association && Relationship.DirectedAssociation)
        {
            End = "halfArrow";
        }
    }

    private static string EdgeLabel(EnterpriseRelationship Relationship)
    {
        string Label = Relationship.Label ?? string.Empty;
        if (Relationship.Kind == EnterpriseRelationshipKind.Influence && !string.IsNullOrEmpty(Relationship.InfluenceStrength))
        {
            return Label.Length == 0
                ? Relationship.InfluenceStrength
                : Label + " (" + Relationship.InfluenceStrength + ")";
        }

        return Label;
    }

    private static (double X, double Y) Unit(double FromX, double FromY, double ToX, double ToY)
    {
        double Dx = ToX - FromX;
        double Dy = ToY - FromY;
        double Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
        if (Length < 0.001)
        {
            return (0, 1);
        }

        return (Dx / Length, Dy / Length);
    }

    private static double ToLayoutY(double SvgY)
    {
        return -SvgY;
    }

    private static double ToSvgY(double LayoutY)
    {
        return -LayoutY;
    }
}

/// <summary>
/// One enterprise relationship after flowchart routing.
/// </summary>
internal sealed class EnterpriseRoutedEdge
{
    internal EnterpriseRoutedEdge(
        EnterpriseRelationship Relationship,
        string PathData,
        List<(double X, double Y)> SvgPoints,
        string? StartMarker,
        string? EndMarker,
        string? DashArray,
        string Label,
        double LabelX,
        double LabelY)
    {
        this.Relationship = Relationship;
        this.PathData = PathData;
        this.SvgPoints = SvgPoints;
        this.StartMarker = StartMarker;
        this.EndMarker = EndMarker;
        this.DashArray = DashArray;
        this.Label = Label;
        this.LabelX = LabelX;
        this.LabelY = LabelY;
    }

    internal EnterpriseRelationship Relationship { get; }

    internal string PathData { get; }

    internal List<(double X, double Y)> SvgPoints { get; }

    internal string? StartMarker { get; }

    internal string? EndMarker { get; }

    internal string? DashArray { get; }

    internal string Label { get; }

    internal double LabelX { get; }

    internal double LabelY { get; }
}

/// <summary>
/// One visible relationship with resolved ports, before path construction.
/// </summary>
internal sealed class EnterpriseVisibleHop
{
    internal EnterpriseVisibleHop(
        EnterpriseRelationship Relationship,
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target,
        EnterprisePort SourcePort,
        EnterprisePort TargetPort)
    {
        this.Relationship = Relationship;
        this.Source = Source;
        this.Target = Target;
        this.SourcePort = SourcePort;
        this.TargetPort = TargetPort;
    }

    internal EnterpriseRelationship Relationship { get; }

    internal EnterpriseLayoutNode Source { get; }

    internal EnterpriseLayoutNode Target { get; }

    internal EnterprisePort SourcePort { get; }

    internal EnterprisePort TargetPort { get; }
}

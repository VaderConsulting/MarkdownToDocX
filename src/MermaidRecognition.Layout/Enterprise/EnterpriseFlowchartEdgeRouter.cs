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
        FlowchartLayoutResult LayoutResult = new FlowchartLayoutResult();
        foreach (FlowchartLayoutNodeBounds Bounds in LeafBounds.Values)
        {
            LayoutResult.NodeBoundsMutable.Add(Bounds);
        }

        Dictionary<string, string> RouteClassByEdgeKey =
            FlowchartEdgeRoutingIntentAnalyzer.BuildRouteClassNamesByEdgeKey(Contract, LayoutResult, Direction);
        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);

        HashSet<string> UsedEdgeIds = new HashSet<string>(StringComparer.Ordinal);
        List<EnterpriseRoutedEdge> Routed = new List<EnterpriseRoutedEdge>();
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (Relationship.HiddenByNesting)
            {
                continue;
            }

            EdgeContract? Edge = FindUnusedEdge(Contract, Relationship, UsedEdgeIds);
            if (Edge == null)
            {
                continue;
            }

            UsedEdgeIds.Add(Edge.Id ?? string.Empty);
            Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
                BoundsForEdge(Relationship, Nodes, LeafBounds);

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
                continue;
            }

            string Label = EdgeLabel(Relationship);
            double LabelX = 0;
            double LabelY = 0;
            if (Label.Length > 0
                && BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                && BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                string? RouteClass = null;
                if (!string.IsNullOrEmpty(Edge.Id) && RouteClassByEdgeKey.TryGetValue(Edge.Id, out string? ById))
                {
                    RouteClass = ById;
                }
                else
                {
                    RouteClassByEdgeKey.TryGetValue(Edge.SourceId + "->" + Edge.TargetId, out RouteClass);
                }

                string? SourceShape = NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode)
                    ? SourceNode.Shape
                    : null;
                (double LayoutLabelX, double LayoutLabelY) = FlowchartSugiyamaSvgRenderer.ResolveDrawnEdgeLabelCentre(
                    Edge,
                    Contract,
                    BoundsById,
                    SourceBounds,
                    TargetBounds,
                    SourceShape,
                    LayoutPoints,
                    Direction,
                    RouteClass,
                    RenderOptions);
                LabelX = LayoutLabelX;
                LabelY = ToSvgY(LayoutLabelY);
            }

            if (IsLayoutReversed(Relationship))
            {
                LayoutPoints.Reverse();
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

            ResolveArchiMateMarkers(Relationship, out string? StartMarker, out string? EndMarker);
            Routed.Add(new EnterpriseRoutedEdge(
                Relationship,
                PathData,
                SvgPoints,
                StartMarker,
                EndMarker,
                DashArray(Relationship),
                Label,
                LabelX,
                LabelY));
        }

        return Routed;
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
            if (IsLayoutReversed(Relationship))
            {
                RouteSourceId = Relationship.TargetId;
                RouteTargetId = Relationship.SourceId;
            }

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
        if (IsLayoutReversed(Relationship))
        {
            RouteSourceId = Relationship.TargetId;
            RouteTargetId = Relationship.SourceId;
        }

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

    private static bool IsLayoutReversed(EnterpriseRelationship Relationship)
    {
        return Relationship.Kind == EnterpriseRelationshipKind.Serving
            || Relationship.Kind == EnterpriseRelationshipKind.Realization
            || Relationship.Kind == EnterpriseRelationshipKind.Assignment;
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

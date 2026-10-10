using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Routes enterprise relationships on the markdown2docx flowchart path
/// (<see cref="FlowchartSugiyamaLayoutEngine"/>, <see cref="FlowchartEdgeRouteResolver"/>,
/// <see cref="FlowchartCurvePathBuilder"/>, <see cref="FlowchartSugiyamaSvgRenderer"/>).
/// ArchiMate markers and dash styles are the only enterprise-specific drawing.
/// </summary>
internal static class EnterpriseFlowchartEdgeRouter
{
    private const double RELATIONSHIP_LABEL_FONT_SIZE_PX = 12;

    // Plain SVG text has no HTML label box or minimum browser box width.
    private static FlowchartLabelDimensions MeasureRelationshipLabel(string Label) => new()
    {
        DisplayText = Label,
        Width = Label.Length * RELATIONSHIP_LABEL_FONT_SIZE_PX * FlowchartEdgeLabelGapHelper.LABEL_CHAR_WIDTH_EM,
        Height = RELATIONSHIP_LABEL_FONT_SIZE_PX
    };

    /// <summary>
    /// Resolves every visible relationship using flowchart layout and routing.
    /// </summary>
    /// <param name="Document">Validated document.</param>
    /// <param name="Nodes">Laid-out nodes.</param>
    /// <returns>Routed edges in SVG coordinates (Y down).</returns>
    internal static List<EnterpriseRoutedEdge> Route(
        EnterpriseDocument Document,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        if (!TryBuildRoutingContext(Document, Nodes, out EnterpriseFlowchartRoutingContext Context))
        {
            return new List<EnterpriseRoutedEdge>();
        }

        HashSet<string> UsedEdgeIds = new HashSet<string>(StringComparer.Ordinal);
        var CompoundPorts = new Dictionary<string, List<(double X, double Y)>>(StringComparer.Ordinal);
        List<EnterpriseRoutedEdge> Routed = new List<EnterpriseRoutedEdge>();
        var RoutedContracts = new List<EdgeContract>();
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (Relationship.HiddenByNesting)
            {
                continue;
            }

            EdgeContract? Edge = FindUnusedEdge(Context.Contract, Relationship, UsedEdgeIds);
            if (Edge == null)
            {
                continue;
            }

            UsedEdgeIds.Add(Edge.Id ?? string.Empty);
            if (!FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Context.Contract,
                    Context.BoundsById,
                    Context.NodeById,
                    Context.Direction,
                    out List<(double X, double Y)> LayoutPoints,
                    out FlowchartEdgeRouteKind RouteKind,
                    Context.Layout)
                || LayoutPoints.Count < 2)
            {
                continue;
            }

            FlowchartCompoundEndpointHelper.ReattachVisiblePorts(Edge, Context.Contract, Context.Layout,
                Context.BoundsById, Context.NodeById, Context.Direction, LayoutPoints, CompoundPorts);
            List<(double X, double Y)> SvgPoints = new List<(double X, double Y)>();
            foreach ((double LayoutX, double LayoutY) in LayoutPoints)
            {
                SvgPoints.Add(Context.Frame.LayoutToSvg(LayoutX, LayoutY));
            }

            if (!TryRouteInsideContainer(Edge, Context.Contract, Nodes, SvgPoints))
            {
                AttachCompoundEnd(Relationship.SourceId, SvgPoints, true, Nodes);
                AttachCompoundEnd(Relationship.TargetId, SvgPoints, false, Nodes);
            }

            string PathData = FlowchartCurvePathBuilder.BuildRoundedCornerPathData(SvgPoints, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
            if (string.IsNullOrWhiteSpace(PathData))
            {
                PathData = FlowchartCurvePathBuilder.BuildPathDataFromGuidePoints(
                    SvgPoints,
                    Context.RenderOptions.Curve);
            }

            string Label = EnterpriseFlowchartContractMapper.EdgeLabel(Relationship);
            double LabelX = 0;
            double LabelY = 0;
            if (Label.Length > 0
                && Context.BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                && Context.BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                string? RouteClass = null;
                if (!string.IsNullOrEmpty(Edge.Id)
                    && Context.RouteClassByEdgeKey.TryGetValue(Edge.Id, out string? ById))
                {
                    RouteClass = ById;
                }
                else
                {
                    Context.RouteClassByEdgeKey.TryGetValue(Edge.SourceId + "->" + Edge.TargetId, out RouteClass);
                }

                string? SourceShape = Context.NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode)
                    ? SourceNode.Shape
                    : null;
                (double LayoutLabelX, double LayoutLabelY) = FlowchartSugiyamaSvgRenderer.ResolveDrawnEdgeLabelCentre(
                    Edge,
                    Context.Contract,
                    Context.BoundsById,
                    SourceBounds,
                    TargetBounds,
                    SourceShape,
                    LayoutPoints,
                    Context.Direction,
                    RouteClass,
                    Context.RenderOptions);
                (LabelX, LabelY) = Context.Frame.LayoutToSvg(LayoutLabelX, LayoutLabelY);
            }

            if (Label.Length > 0 && FlowchartEdgeLabelGapHelper.TryResolveLabelSegmentBreak(
                SvgPoints, MeasureRelationshipLabel(Label), RELATIONSHIP_LABEL_FONT_SIZE_PX, out var LabelBreak))
            {
                LabelX = LabelBreak.CentreX;
                LabelY = LabelBreak.CentreY;
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
            RoutedContracts.Add(Edge);
        }

        SeparateVisibleTracks(Routed, RoutedContracts, Nodes, Context.Contract);
        EnterpriseRelationshipLabelLayout.Place(Routed, Nodes);
        return Routed;
    }

    private static void SeparateVisibleTracks(List<EnterpriseRoutedEdge> Edges, List<EdgeContract> Contracts,
        Dictionary<string, EnterpriseLayoutNode> Nodes, DiagramContract Contract)
    {
        var Layout = new FlowchartLayoutResult();
        for (int Index = 0; Index < Edges.Count; Index++)
        {
            var Route = new FlowchartLayoutEdgeRoute { EdgeId = Contracts[Index].Id ?? string.Empty,
                SourceId = Contracts[Index].SourceId, TargetId = Contracts[Index].TargetId };
            foreach (var Point in Edges[Index].SvgPoints) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            Layout.EdgeRoutesMutable.Add(Route);
        }
        var Obstacles = new List<DiagramGridLayout.GridNode>();
        foreach (var Node in Nodes.Values) Obstacles.Add(new() { Id = Node.Id,
            X = Node.AbsoluteX + Node.Width / 2, Y = Node.AbsoluteY + Node.Height / 2,
            Width = (int)Math.Ceiling(Node.Width), Height = (int)Math.Ceiling(Node.Height) });
        DiagramGridLayout.SeparateTracks(Layout, Obstacles, Contract);
        FlowchartObstacleRouteRepair.Apply(Layout, Contract, Nodes.Values.ToDictionary(Node => Node.Id,
            Node => new FlowchartLayoutNodeBounds { NodeId = Node.Id,
                CenterX = Node.AbsoluteX + Node.Width / 2, CenterY = Node.AbsoluteY + Node.Height / 2,
                Width = Node.Width, Height = Node.Height }, StringComparer.Ordinal));
        for (int Index = 0; Index < Edges.Count; Index++)
        {
            var Edge = Edges[Index];
            Edge.SvgPoints.Clear();
            foreach (var Point in Layout.EdgeRoutes[Index].Points) Edge.SvgPoints.Add((Point.X, Point.Y));
            Edge.PathData = FlowchartCurvePathBuilder.BuildRoundedCornerPathData(Edge.SvgPoints, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
        }
    }

    /// <summary>
    /// Resolves flowchart path data for the same contract enterprise uses, for parity tests.
    /// </summary>
    /// <param name="Document">Validated document.</param>
    /// <param name="Nodes">Laid-out nodes.</param>
    /// <returns>Source, target, and SVG path data for each routed edge.</returns>
    internal static List<EnterpriseFlowchartPathFact> ResolveFlowchartPathFacts(
        EnterpriseDocument Document,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        List<EnterpriseFlowchartPathFact> Facts = new List<EnterpriseFlowchartPathFact>();
        if (!TryBuildRoutingContext(Document, Nodes, out EnterpriseFlowchartRoutingContext Context))
        {
            return Facts;
        }

        foreach (EdgeContract Edge in Context.Contract.Edges)
        {
            if (!FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Context.Contract,
                    Context.BoundsById,
                    Context.NodeById,
                    Context.Direction,
                    out List<(double X, double Y)> LayoutPoints,
                    out FlowchartEdgeRouteKind RouteKind,
                    Context.Layout)
                || LayoutPoints.Count < 2)
            {
                continue;
            }

            List<(double X, double Y)> SvgPoints = new List<(double X, double Y)>();
            foreach ((double LayoutX, double LayoutY) in LayoutPoints)
            {
                SvgPoints.Add(Context.Frame.LayoutToSvg(LayoutX, LayoutY));
            }

            string PathData = FlowchartCurvePathBuilder.BuildRoundedCornerPathData(SvgPoints, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
            if (string.IsNullOrWhiteSpace(PathData))
            {
                PathData = FlowchartCurvePathBuilder.BuildPathDataFromGuidePoints(
                    SvgPoints,
                    Context.RenderOptions.Curve);
            }

            Facts.Add(new EnterpriseFlowchartPathFact(Edge.SourceId, Edge.TargetId, PathData));
        }

        return Facts;
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
            .Append("\"><path data-edge-source=\"")
            .Append(EnterpriseSvgFormat.Escape(Routed.Relationship.SourceId))
            .Append("\" data-edge-target=\"")
            .Append(EnterpriseSvgFormat.Escape(Routed.Relationship.TargetId))
            .Append("\" d=\"")
            .Append(VisiblePath(Routed))
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
                .Append(EnterpriseSvgFormat.Number(Routed.LabelY - (Routed.LabelLines.Count - 1) * 7.5 + 4))
                .Append("\" text-anchor=\"middle\" font-size=\"12\" font-family=\"&quot;trebuchet ms&quot;, verdana, arial, sans-serif\" fill=\"#333\">");
            for (int Index = 0; Index < Routed.LabelLines.Count; Index++)
            {
                Builder.Append("<tspan x=\"").Append(EnterpriseSvgFormat.Number(Routed.LabelX))
                    .Append("\" dy=\"").Append(Index == 0 ? "0" : "15").Append("\">")
                    .Append(EnterpriseSvgFormat.Escape(Routed.LabelLines[Index])).Append("</tspan>");
            }
            Builder.Append("</text>");
        }

        Builder.Append("</g>");
    }

    private static bool TryRouteInsideContainer(EdgeContract Edge, DiagramContract Contract,
        Dictionary<string, EnterpriseLayoutNode> Nodes, List<(double X, double Y)> Points)
    {
        bool ParentSource = FlowchartCompoundEndpointHelper.Contains(Contract, Edge.SourceId, Edge.TargetId);
        bool ParentTarget = FlowchartCompoundEndpointHelper.Contains(Contract, Edge.TargetId, Edge.SourceId);
        if ((!ParentSource && !ParentTarget)
            || !Nodes.TryGetValue(ParentSource ? Edge.SourceId : Edge.TargetId, out var Parent)
            || !Nodes.TryGetValue(ParentSource ? Edge.TargetId : Edge.SourceId, out var Child)) return false;
        // Prefer a direct normal into the reserved lower corridor. When a
        // sibling blocks it, use a side corridor with one rounded bend.
        double X = Child.AbsoluteX + Child.Width / 2;
        double StartY = Child.AbsoluteY + Child.Height;
        double EndY = Parent.AbsoluteY + Parent.Height;
        if (EndY - StartY < 60) return false;
        var Candidates = new List<List<(double X, double Y)>>
        {
            new() { (X, StartY), (X, EndY) },
            new() { (Child.AbsoluteX, Child.AbsoluteY + Child.Height / 2),
                (Parent.AbsoluteX + 40, Child.AbsoluteY + Child.Height / 2), (Parent.AbsoluteX + 40, EndY) },
            new() { (Child.AbsoluteX + Child.Width, Child.AbsoluteY + Child.Height / 2),
                (Parent.AbsoluteX + Parent.Width - 40, Child.AbsoluteY + Child.Height / 2),
                (Parent.AbsoluteX + Parent.Width - 40, EndY) }
        };
        foreach (var Candidate in Candidates)
        {
            bool Clear = true;
            foreach (var Other in Nodes.Values)
            {
                if (Other.Id == Child.Id || Other.Id == Parent.Id
                    || FlowchartCompoundEndpointHelper.Contains(Contract, Other.Id, Child.Id)) continue;
                for (int Index = 1; Index < Candidate.Count; Index++)
                {
                    var A = Candidate[Index - 1]; var B = Candidate[Index];
                    if (Math.Max(A.X, B.X) > Other.AbsoluteX - 6 && Math.Min(A.X, B.X) < Other.AbsoluteX + Other.Width + 6
                        && Math.Max(A.Y, B.Y) > Other.AbsoluteY - 6 && Math.Min(A.Y, B.Y) < Other.AbsoluteY + Other.Height + 6)
                        Clear = false;
                }
            }
            if (!Clear) continue;
            Points.Clear();
            if (ParentSource) Candidate.Reverse();
            Points.AddRange(Candidate);
            return true;
        }
        return false;
    }

    private static void AttachCompoundEnd(string Id, List<(double X, double Y)> Points,
        bool AtStart, Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        if (!Nodes.TryGetValue(Id, out EnterpriseLayoutNode? Node) || !Node.IsCompound
            || Node.IsGroup || Node.Element is null
            || EnterpriseElementCatalog.Elements[Node.Element.ElementType].BoxKind
                is not (EnterpriseBoxKind.Rectangle or EnterpriseBoxKind.Object))
            return;
        int Index = AtStart ? 0 : Points.Count - 1;
        var End = Points[Index];
        var Next = Points[AtStart ? 1 : Points.Count - 2];
        // The shared frame includes its title band. The drawn compound box
        // can be smaller, so extend a normal terminal run to its visible edge.
        if (Math.Abs(End.X - Next.X) < 0.01
            && End.X >= Node.AbsoluteX + 10 && End.X <= Node.AbsoluteX + Node.Width - 10)
            Points[Index] = (End.X, Next.Y < End.Y ? Node.AbsoluteY : Node.AbsoluteY + Node.Height);
        else if (Math.Abs(End.Y - Next.Y) < 0.01
            && End.Y >= Node.AbsoluteY + 10 && End.Y <= Node.AbsoluteY + Node.Height - 10)
            Points[Index] = (Next.X < End.X ? Node.AbsoluteX : Node.AbsoluteX + Node.Width, End.Y);
    }

    private static string VisiblePath(EnterpriseRoutedEdge Routed)
    {
        if (string.IsNullOrWhiteSpace(Routed.Label)) return Routed.PathData;
        var Gap = Routed.LabelBreak;
        if (Gap == null) return Routed.PathData;
        var Start = Routed.SvgPoints[Gap.SegmentStartIndex];
        var End = Routed.SvgPoints[Gap.SegmentEndIndex];
        double Dx = Math.Sign(End.X - Start.X), Dy = Math.Sign(End.Y - Start.Y);
        var Prefix = Routed.SvgPoints.GetRange(0, Gap.SegmentStartIndex + 1);
        Prefix.Add((Gap.CentreX - Dx * Gap.HalfGapWidthPx, Gap.CentreY - Dy * Gap.HalfGapWidthPx));
        var Suffix = Routed.SvgPoints.GetRange(Gap.SegmentEndIndex, Routed.SvgPoints.Count - Gap.SegmentEndIndex);
        Suffix.Insert(0, (Gap.CentreX + Dx * Gap.HalfGapWidthPx, Gap.CentreY + Dy * Gap.HalfGapWidthPx));
        return FlowchartCurvePathBuilder.BuildRoundedCornerPathData(Prefix, DiagramGridRouteBuilder.CORNER_RADIUS_PX)
            + " " + FlowchartCurvePathBuilder.BuildRoundedCornerPathData(Suffix, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
    }

    internal static bool TryBuildRoutingContext(
        EnterpriseDocument Document,
        Dictionary<string, EnterpriseLayoutNode> Nodes,
        out EnterpriseFlowchartRoutingContext Context)
    {
        Context = new EnterpriseFlowchartRoutingContext();
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Document);
        if (Contract.Nodes.Count == 0)
        {
            return false;
        }

        if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout))
        {
            return false;
        }

        if (!FlowchartSvgCoordinateFrameResolver.TryResolve(Contract, Layout, out FlowchartSvgCoordinateFrame Frame))
        {
            return false;
        }

        Dictionary<string, NodeContract> NodeById = new Dictionary<string, NodeContract>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            NodeById[Node.Id] = Node;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        FlowchartSubgraphBoundsRequest BoundsRequest =
            FlowchartSubgraphBoundsRequest.FromContract(Contract, RenderOptions);
        List<FlowchartSubgraphLayoutRect> SubgraphRects = Layout.ClusterBounds.Count > 0
            ? FlowchartSugiyamaSvgRenderer.BuildDagreClusterRects(Contract, Layout)
            : FlowchartSubgraphBoundsBuilder.BuildOrderedRects(
            Contract,
            BoundsById,
            BoundsRequest);
        foreach (FlowchartSubgraphLayoutRect Rect in SubgraphRects)
        {
            BoundsById[Rect.GroupId] = new FlowchartLayoutNodeBounds
            {
                NodeId = Rect.GroupId,
                CenterX = (Rect.Left + Rect.Right) / 2.0,
                CenterY = (Rect.Bottom + Rect.Top) / 2.0,
                Width = Math.Max(1.0, Rect.Right - Rect.Left),
                Height = Math.Max(1.0, Rect.Top - Rect.Bottom)
            };
        }

        Dictionary<string, EnterpriseSvgRect> SvgRects = new Dictionary<string, EnterpriseSvgRect>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            SvgRects[Bounds.NodeId] = EnterpriseSvgRect.FromNodeBounds(Bounds, Frame);
        }

        foreach (FlowchartSubgraphLayoutRect Rect in SubgraphRects)
        {
            SvgRects[Rect.GroupId] = EnterpriseSvgRect.FromSubgraph(Rect, Frame);
        }

        double SvgWidth = Frame.DiagramPaddingPx * 2.0;
        double SvgHeight = Frame.DiagramPaddingPx * 2.0;
        foreach (EnterpriseSvgRect SvgRect in SvgRects.Values)
        {
            SvgWidth = Math.Max(SvgWidth, SvgRect.Left + SvgRect.Width + Frame.DiagramPaddingPx);
            SvgHeight = Math.Max(SvgHeight, SvgRect.Top + SvgRect.Height + Frame.DiagramPaddingPx);
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Context = new EnterpriseFlowchartRoutingContext
        {
            Contract = Contract,
            Layout = Layout,
            Frame = Frame,
            BoundsById = BoundsById,
            NodeById = NodeById,
            Direction = Direction,
            RenderOptions = RenderOptions,
            RouteClassByEdgeKey = FlowchartEdgeRoutingIntentAnalyzer.BuildRouteClassNamesByEdgeKey(
                Contract,
                Layout,
                Direction),
            SvgRects = SvgRects,
            SvgWidth = SvgWidth,
            SvgHeight = SvgHeight
        };
        return true;
    }

    private static EdgeContract? FindUnusedEdge(
        DiagramContract Contract,
        EnterpriseRelationship Relationship,
        HashSet<string> UsedEdgeIds)
    {
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (Edge.SourceId == Relationship.SourceId
                && Edge.TargetId == Relationship.TargetId
                && !UsedEdgeIds.Contains(Edge.Id ?? string.Empty))
            {
                return Edge;
            }
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
}

/// <summary>
/// Shared flowchart layout and routing context for one enterprise diagram.
/// </summary>
internal sealed class EnterpriseFlowchartRoutingContext
{
    internal DiagramContract Contract { get; set; } = new DiagramContract();

    internal FlowchartLayoutResult Layout { get; set; } = new FlowchartLayoutResult();

    internal FlowchartSvgCoordinateFrame Frame { get; set; } = new FlowchartSvgCoordinateFrame();

    internal Dictionary<string, FlowchartLayoutNodeBounds> BoundsById { get; set; } =
        new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);

    internal Dictionary<string, NodeContract> NodeById { get; set; } =
        new Dictionary<string, NodeContract>(StringComparer.Ordinal);

    internal FlowchartDirection Direction { get; set; }

    internal FlowchartRenderOptions RenderOptions { get; set; } = FlowchartRenderOptions.FromContract(new DiagramContract());

    internal Dictionary<string, string> RouteClassByEdgeKey { get; set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    internal Dictionary<string, EnterpriseSvgRect> SvgRects { get; set; } =
        new Dictionary<string, EnterpriseSvgRect>(StringComparer.Ordinal);

    internal double SvgWidth { get; set; } = 40;

    internal double SvgHeight { get; set; } = 40;
}

/// <summary>
/// SVG rectangle matching <see cref="FlowchartSugiyamaSvgRenderer"/> node placement.
/// </summary>
internal sealed class EnterpriseSvgRect
{
    internal EnterpriseSvgRect(double Left, double Top, double Width, double Height)
    {
        this.Left = Left;
        this.Top = Top;
        this.Width = Width;
        this.Height = Height;
    }

    internal double Left { get; }

    internal double Top { get; }

    internal double Width { get; }

    internal double Height { get; }

    internal static EnterpriseSvgRect FromNodeBounds(
        FlowchartLayoutNodeBounds Bounds,
        FlowchartSvgCoordinateFrame Frame)
    {
        double Left = Frame.DiagramPaddingPx + (Bounds.CenterX - (Bounds.Width / 2.0) - Frame.MinLeft);
        double Top = Frame.DiagramPaddingPx + (Frame.MaxTop - Bounds.CenterY - (Bounds.Height / 2.0));
        return new EnterpriseSvgRect(Left, Top, Bounds.Width, Bounds.Height);
    }

    internal static EnterpriseSvgRect FromSubgraph(
        FlowchartSubgraphLayoutRect Rect,
        FlowchartSvgCoordinateFrame Frame)
    {
        double Left = Frame.DiagramPaddingPx + (Rect.Left - Frame.MinLeft);
        double Top = Frame.DiagramPaddingPx + (Frame.MaxTop - Rect.Top);
        return new EnterpriseSvgRect(Left, Top, Rect.Right - Rect.Left, Rect.Top - Rect.Bottom);
    }
}

/// <summary>
/// One flowchart-resolved edge path, used for enterprise/flowchart parity checks.
/// </summary>
internal sealed class EnterpriseFlowchartPathFact
{
    internal EnterpriseFlowchartPathFact(string SourceId, string TargetId, string PathData)
    {
        this.SourceId = SourceId;
        this.TargetId = TargetId;
        this.PathData = PathData;
    }

    internal string SourceId { get; }

    internal string TargetId { get; }

    internal string PathData { get; }
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

    internal string PathData { get; set; }

    internal List<(double X, double Y)> SvgPoints { get; }

    internal string? StartMarker { get; }

    internal string? EndMarker { get; }

    internal string? DashArray { get; }

    internal string Label { get; }

    internal double LabelX { get; set; }

    internal double LabelY { get; set; }

    internal List<string> LabelLines { get; set; } = new();
    internal double LabelWidth { get; set; }
    internal double LabelHeight { get; set; }
    internal FlowchartEdgeLabelGapHelper.LabelSegmentBreak? LabelBreak { get; set; }
}

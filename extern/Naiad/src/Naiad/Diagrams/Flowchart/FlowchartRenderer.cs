namespace MermaidSharp.Diagrams.Flowchart;

public partial class FlowchartRenderer(ILayoutEngine? layoutEngine = null) :
    IDiagramRenderer<FlowchartModel>
{
    ILayoutEngine layoutEngine = layoutEngine ?? new DagreLayoutEngine();

    const string nodeFill = "#ECECFF";
    const string nodeStroke = "#9370DB";
    const string edgeStroke = "#333333";
    const string labelBackground = "rgba(232,232,232,0.8)";

    /// <summary>
    /// Flowchart nodes use Trebuchet in browser CSS; Word SVG uses Arial. Slightly lower em nudge than other diagrams for better optical vertical centre in boxes.
    /// </summary>
    const double flowchartLabelOpticalBiasEm = 0.09;

    /// <summary>
    /// Extra downward shift for node label text in SVG user units (Word vs Mermaid vertical centring).
    /// </summary>
    const double FlowchartNodeLabelExtraVerticalOffsetPx = 4;

    /// <summary>
    /// Vertical gap between ranks (layers) passed to the layout engine. Tuned for tighter packing vs the Mermaid / VS Code reference.
    /// </summary>
    const double FlowchartRankSeparationPx = 55;

    /// <summary>
    /// Catmull–Rom chord divisor: larger values tighten curves toward the polyline control points.
    /// </summary>
    const double FlowchartCatmullChordDivisor = 6.0;

    /// <summary>
    /// Minimum length of the straight segment aligned with rank flow before the arrow marker at the target (matches Mermaid-style vertical/horizontal finish).
    /// </summary>
    const double MinFlowAlignedStemPx = 13.0;

    /// <summary>
    /// Stem length scales with the last span so longer edges show a visibly longer straight approach before the marker.
    /// </summary>
    const double FinalApproachStemFraction = 0.26;

    /// <summary>
    /// Keeps part of the last span for curved transition so the stem joint never collapses to a cusp.
    /// </summary>
    const double FinalApproachCurveReservePx = 10.0;

    /// <summary>
    /// Places the inward Bezier control near the final straight stem so the spline tangent matches the stem line toward the node.
    /// </summary>
    const double StemJointControlFactor = 0.21;

    // FontAwesome icon pattern: fa:fa-icon-name or fab:fa-icon-name
    static Regex iconPattern = IconPatternMyRegex();
    [GeneratedRegex("(fa[bsr]?):fa-([a-z0-9-]+)", RegexOptions.Compiled)]
    private static partial Regex IconPatternMyRegex();

    public SvgDocument Render(FlowchartModel model, RenderOptions options)
    {
        // Calculate node sizes based on text
        foreach (var node in model.Nodes)
        {
            var label = node.Label ?? node.Id;
            label = NormaliseHtmlLineBreaks(label);
            // Strip icon syntax for measurement
            var textForMeasure = iconPattern.Replace(label, "").Trim();
            var textSize = MeasureText(textForMeasure, options.FontSize);
            // Add extra width for icon if present
            var hasIcon = iconPattern.IsMatch(label);
            node.Width = textSize.Width + 30 + (hasIcon ? 20 : 0);
            node.Height = textSize.Height + 27;

            // Adjust size for different shapes
            if (node.Shape is NodeShape.Circle or NodeShape.DoubleCircle)
            {
                var diameter = Math.Max(node.Width, node.Height);
                node.Width = diameter;
                node.Height = diameter;
            }
            else if (node.Shape == NodeShape.Diamond)
            {
                node.Width *= 1.4;
                node.Height *= 1.4;
            }
        }

        // Run layout
        var layoutOptions = new LayoutOptions
        {
            Direction = model.Direction,
            NodeSeparation = 50,
            RankSeparation = FlowchartRankSeparationPx,
            // Weighted edges (longer "-->") need longest-path layering; TightTree compaction was collapsing minlen gaps.
            Ranker = RankerType.LongestPath
        };
        var layoutResult = layoutEngine.Layout(model, layoutOptions);

        ApplySubgraphDirections(model);

        // Build SVG
        var builder = new SvgBuilder()
            .Size(layoutResult.Width, layoutResult.Height)
            .Padding(options.Padding)
            .AddMermaidArrowMarker()
            .AddMermaidCircleMarker()
            .AddMermaidCrossMarker()
            .AddNodeDropShadowFilter();

        // Add mermaid.ink CSS styles
        builder.AddStyles(MermaidStyles.FlowchartStyles);

        // Render edges first (behind nodes)
        foreach (var edge in model.Edges)
        {
            RenderEdge(builder, edge, options, model.Direction, model.GetNode(edge.SourceId));
        }

        RenderSubgraphs(builder, model, options);

        // Render nodes
        foreach (var node in model.Nodes)
        {
            RenderNode(builder, node, options);
        }

        return builder.Build();
    }

    static void ApplySubgraphDirections(FlowchartModel model)
    {
#pragma warning disable IDE0007
        if (model.Subgraphs.Count == 0)
        {
            return;
        }

        foreach (MermaidSharp.Models.Subgraph subgraph in model.Subgraphs)
        {
            ApplySubgraphDirectionsRecursive(model, subgraph);
        }
#pragma warning restore IDE0007
    }

    static void ApplySubgraphDirectionsRecursive(FlowchartModel model, MermaidSharp.Models.Subgraph subgraph)
    {
#pragma warning disable IDE0007
        if (subgraph.Direction == Direction.LeftToRight || subgraph.Direction == Direction.RightToLeft)
        {
            RotateSubgraphLayout90Degrees(model, subgraph);
        }

        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            ApplySubgraphDirectionsRecursive(model, nested);
        }
#pragma warning restore IDE0007
    }

    static void RotateSubgraphLayout90Degrees(FlowchartModel model, MermaidSharp.Models.Subgraph subgraph)
    {
#pragma warning disable IDE0007
        // Heuristic: Naiad’s layout engine currently uses a single diagram direction.
        // For subgraphs that request LR/RL, we rotate the already-laid-out positions (nodes + internal edge points)
        // within the subgraph’s bounds. This is not a full Mermaid compound-layout implementation, but it produces
        // the expected left-to-right visual flow for common fixtures.

        List<Node> nodesInSubgraph = new List<Node>();
        foreach (string nodeId in subgraph.NodeIds)
        {
            Node? node = model.GetNode(nodeId);
            if (node != null)
            {
                nodesInSubgraph.Add(node);
            }
        }

        if (nodesInSubgraph.Count == 0)
        {
            return;
        }

        double MinX = double.PositiveInfinity;
        double MinY = double.PositiveInfinity;
        double MaxX = double.NegativeInfinity;
        double MaxY = double.NegativeInfinity;

        foreach (Node node in nodesInSubgraph)
        {
            MinX = Math.Min(MinX, node.Position.X);
            MinY = Math.Min(MinY, node.Position.Y);
            MaxX = Math.Max(MaxX, node.Position.X);
            MaxY = Math.Max(MaxY, node.Position.Y);
        }

        double Cx = (MinX + MaxX) / 2.0;
        double Cy = (MinY + MaxY) / 2.0;

        foreach (Node node in nodesInSubgraph)
        {
            double Dx = node.Position.X - Cx;
            double Dy = node.Position.Y - Cy;

            // Rotate 90 degrees: (x, y) -> (y, -x)
            double NewDx = Dy;
            double NewDy = -Dx;

            node.Position = new Position(Cx + NewDx, Cy + NewDy);
        }

        HashSet<string> nodeIdSet = new HashSet<string>(subgraph.NodeIds, StringComparer.Ordinal);
        foreach (Edge edge in model.Edges)
        {
            if (!nodeIdSet.Contains(edge.SourceId) || !nodeIdSet.Contains(edge.TargetId))
            {
                continue;
            }

            if (edge.Points.Count == 0)
            {
                continue;
            }

            for (int i = 0; i < edge.Points.Count; i++)
            {
                Position p = edge.Points[i];
                double Dx = p.X - Cx;
                double Dy = p.Y - Cy;
                double NewDx = Dy;
                double NewDy = -Dx;
                edge.Points[i] = new Position(Cx + NewDx, Cy + NewDy);
            }
        }
#pragma warning restore IDE0007
    }

    static void RenderSubgraphs(SvgBuilder builder, FlowchartModel model, RenderOptions options)
    {
        if (model.Subgraphs.Count == 0)
        {
            return;
        }

#pragma warning disable IDE0007
        foreach (MermaidSharp.Models.Subgraph subgraph in model.Subgraphs)
        {
            RenderSubgraphRecursive(builder, model, subgraph, options);
        }
#pragma warning restore IDE0007
    }

    static void RenderSubgraphRecursive(SvgBuilder builder, FlowchartModel model, MermaidSharp.Models.Subgraph subgraph, RenderOptions options)
    {
#pragma warning disable IDE0007
        MermaidSharp.Models.Rect Bounds = ComputeSubgraphBounds(model, subgraph);
#pragma warning restore IDE0007
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        // Mermaid draws subgraph boxes behind nodes.
        builder.AddRect(
            Bounds.X,
            Bounds.Y,
            Bounds.Width,
            Bounds.Height,
            fill: "none",
            stroke: "#999",
            strokeWidth: 1,
            rx: 0);

        #pragma warning disable IDE0007
        string Title = subgraph.Title ?? subgraph.Id;
        #pragma warning restore IDE0007
        if (!string.IsNullOrWhiteSpace(Title))
        {
            // Title near top-left of the box (simple baseline; Word-friendly).
            #pragma warning disable IDE0007
            double TitleX = Bounds.X + 8;
            double TitleY = Bounds.Y + Math.Max(14, options.FontSize);
            #pragma warning restore IDE0007
            builder.AddText(
                TitleX,
                TitleY,
                Title,
                anchor: "start",
                baseline: "middle",
                fontSize: $"{Math.Max(10, options.FontSize)}px",
                fontFamily: options.FontFamily,
                fill: "#333");
        }

        #pragma warning disable IDE0007
        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            RenderSubgraphRecursive(builder, model, nested, options);
        }
        #pragma warning restore IDE0007
    }

    static MermaidSharp.Models.Rect ComputeSubgraphBounds(FlowchartModel model, MermaidSharp.Models.Subgraph subgraph)
    {
#pragma warning disable IDE0007
        const double PADDING_PX = 18.0;
        const double TITLE_BAND_PX = 26.0;

        bool HasAny = false;
        double MinX = 0;
        double MinY = 0;
        double MaxX = 0;
        double MaxY = 0;

        foreach (string nodeId in subgraph.NodeIds)
        {
            Node? node = model.GetNode(nodeId);
            if (node == null)
            {
                continue;
            }

            double left = node.Position.X - node.Width / 2;
            double top = node.Position.Y - node.Height / 2;
            double right = node.Position.X + node.Width / 2;
            double bottom = node.Position.Y + node.Height / 2;

            if (!HasAny)
            {
                HasAny = true;
                MinX = left;
                MinY = top;
                MaxX = right;
                MaxY = bottom;
            }
            else
            {
                MinX = Math.Min(MinX, left);
                MinY = Math.Min(MinY, top);
                MaxX = Math.Max(MaxX, right);
                MaxY = Math.Max(MaxY, bottom);
            }
        }

        if (!HasAny)
        {
            return new MermaidSharp.Models.Rect(0, 0, 0, 0);
        }

        double X = MinX - PADDING_PX;
        double Y = MinY - PADDING_PX - TITLE_BAND_PX;
        double Width = (MaxX - MinX) + (PADDING_PX * 2);
        double Height = (MaxY - MinY) + (PADDING_PX * 2) + TITLE_BAND_PX;
        return new MermaidSharp.Models.Rect(X, Y, Width, Height);
#pragma warning restore IDE0007
    }

    static void RenderNode(SvgBuilder builder, Node node, RenderOptions options)
    {
#pragma warning disable IDE0007
        var x = node.Position.X - node.Width / 2;
        var y = node.Position.Y - node.Height / 2;

        var shapePath = ShapePathGenerator.GetPath(node.Shape, x, y, node.Width, node.Height);
#pragma warning restore IDE0007

        builder.AddPath(shapePath,
            fill: nodeFill,
            stroke: nodeStroke,
            strokeWidth: 1,
            cssClass: "node",
            filter: SvgBuilder.NaiadNodeShadowFilterReference);

        // Render label with icon support
#pragma warning disable IDE0007
        var label = node.Label ?? node.Id;
        var htmlLabel = ConvertIconsToHtml(NormaliseHtmlLineBreaks(label));
#pragma warning restore IDE0007

        builder.AddForeignObject(
            x, y, node.Width, node.Height,
            htmlLabel,
            className: "nodeLabel",
            fontSizePx: options.FontSize,
            opticalVerticalBiasEm: flowchartLabelOpticalBiasEm,
            extraVerticalOffsetPx: FlowchartNodeLabelExtraVerticalOffsetPx);
    }

    static string NormaliseHtmlLineBreaks(string label)
    {
#pragma warning disable IDE0007
        if (string.IsNullOrWhiteSpace(label))
        {
            return label ?? string.Empty;
        }

        // Mermaid flowcharts often embed HTML breaks in labels; `</br>` is common in fixtures.
        // Normalise to `<br/>` so Word’s SVG foreignObject HTML renders consistently.
        string Result = label;
        Result = Result.Replace("</br>", "<br/>", StringComparison.OrdinalIgnoreCase);
        Result = Result.Replace("<br>", "<br/>", StringComparison.OrdinalIgnoreCase);
        Result = Result.Replace("<br/>", "<br/>", StringComparison.OrdinalIgnoreCase);
        return Result;
#pragma warning restore IDE0007
    }

    static void RenderEdge(SvgBuilder builder, Edge edge, RenderOptions options, Direction diagramDirection, Node? sourceNode)
    {
        if (edge.Points.Count < 2)
        {
            return;
        }

        var pointsForPath = edge.Points;

        // Terminal glyphs in Mermaid (arrowhead, circle, cross) are SVG markers.
        //
        // Two distinct mechanisms control the final appearance:
        //
        // 1) Marker anchoring (SvgBuilder marker RefX/RefY):
        //    - Determines where the glyph sits relative to the path endpoint.
        //    - This primarily controls "does the glyph intrude into the node border?".
        //
        // 2) Path endpoint trimming (this code):
        //    - Shortens the rendered connector path so the stroke does not run underneath the marker glyph.
        //    - This primarily controls "does the line appear to run into/through the glyph?".
        //
        // These values are tuned against Mermaid.js preview and verified in Word, because Word's SVG renderer can differ
        // from browsers in anti-aliasing and marker/stroke composition.
        //
        // Endpoint trimming policy (Mermaid parity):
        //
        // - Filled arrowheads: trim the path end slightly so the connector stroke does not visually run through the triangle.
        // - Circle/cross terminals: do not trim the endpoint. Mermaid’s marker geometry (refX values larger than the
        //   glyph max X) already creates a small, consistent gap so the connector stroke does not touch the glyph.
        //
        // NOTE: Mermaid’s markerOffsets2 table (arrow_circle / arrow_cross = 12.5) is used for dash pattern shaping under
        // the "neo" look, not as a general endpoint trim distance for default flowchart rendering.
        // Word parity tweak: ensure the connector stroke does not visibly intrude into the arrowhead fill.
        // The reference shows the stroke stopping slightly earlier than Mermaid’s default 4px trim in some Word renders.
        const double PointArrowMarkerTrimPx = 6.0;
        const double BiDirectionalArrowMarkerTrimPx = 8.0;
        const double CircleMarkerTrimPx = 2.0;
        const double BiDirectionalCircleMarkerTrimPx = 2.0;
        // Cross terminals:
        // In SVG, markers are positioned at the path endpoint. If we trim the path endpoint, the X marker moves with it.
        // To keep the X fixed at the node edge while shortening the visible connector, we render:
        // - a visible trimmed stroke path WITHOUT markers, and
        // - an invisible marker-only path that reaches the true endpoint (node edge).
        // User-tuned: target visible cross stroke end at X=198 when marker endpoint is X=208.2.
        const double CrossStrokeGapPx = 10.2;

        // Returns the (startGapPx, endGapPx) trim distances in SVG user units.
        //
        // - startGapPx trims the first point toward the second point.
        // - endGapPx trims the last point back toward the second-to-last point.
        //
        // The returned values are intentionally specific to EdgeType, because Mermaid’s terminals (and our marker
        // definitions) have different effective "visual extents" once stroke width and Word rendering are considered.
        static (double StartGapPx, double EndGapPx) GetTerminalGaps(EdgeType edgeType) =>
            edgeType switch
            {
                // Arrow terminals
                EdgeType.Arrow => (0.0, PointArrowMarkerTrimPx),
                EdgeType.DottedArrow => (0.0, PointArrowMarkerTrimPx),
                EdgeType.ThickArrow => (0.0, PointArrowMarkerTrimPx),
                EdgeType.BiDirectional => (BiDirectionalArrowMarkerTrimPx, BiDirectionalArrowMarkerTrimPx),

                // Circle terminals
                EdgeType.CircleEnd => (0.0, CircleMarkerTrimPx),
                // Bi-directional circles require a slightly larger trim so the centre stroke does not protrude beyond either dot.
                EdgeType.BiDirectionalCircle => (BiDirectionalCircleMarkerTrimPx, BiDirectionalCircleMarkerTrimPx),

                // Cross terminals
                EdgeType.CrossEnd => (0.0, CrossStrokeGapPx),
                EdgeType.BiDirectionalCross => (CrossStrokeGapPx, CrossStrokeGapPx),

                _ => (0.0, 0.0)
            };

        var gaps = GetTerminalGaps(edge.Type);
        var startGapPx = gaps.StartGapPx;
        var endGapPx = gaps.EndGapPx;

        if ((startGapPx > 0 || endGapPx > 0) && edge.Points.Count >= 2)
        {
            var adjustedPoints = new List<Position>(edge.Points);

            if (startGapPx > 0)
            {
                var p0 = adjustedPoints[0];
                var p1 = adjustedPoints[1];
                var segLen = p0.DistanceTo(p1);
                if (segLen > startGapPx)
                {
                    var dir = (p1 - p0) / segLen;
                    adjustedPoints[0] = p0 + (dir * startGapPx);
                }
            }

            if (endGapPx > 0)
            {
                var lastIdx = adjustedPoints.Count - 1;
                var pN1 = adjustedPoints[lastIdx - 1];
                var pN = adjustedPoints[lastIdx];
                var segLen = pN1.DistanceTo(pN);
                if (segLen > endGapPx)
                {
                    var dir = (pN - pN1) / segLen;
                    adjustedPoints[lastIdx] = pN - (dir * endGapPx);
                }
            }

            pointsForPath = adjustedPoints;
        }

        var pathData = BuildFlowchartEdgePath(pointsForPath, diagramDirection, sourceNode);

        var strokeDasharray = edge.LineStyle switch
        {
            EdgeStyle.Dotted => "2",
            _ => null
        };

        var strokeWidth = edge.LineStyle switch
        {
            EdgeStyle.Thick => 3.5,
            _ => 2.0
        };

        var markerEnd = edge.Type switch
        {
            EdgeType.CircleEnd => "url(#mermaid-svg_flowchart-v2-circleEnd)",
            EdgeType.CrossEnd => "url(#mermaid-svg_flowchart-v2-crossEnd)",
            EdgeType.BiDirectionalCircle => "url(#mermaid-svg_flowchart-v2-circleEnd)",
            EdgeType.BiDirectionalCross => "url(#mermaid-svg_flowchart-v2-crossEnd)",
            _ => edge.HasArrowHead
                ? edge.LineStyle == EdgeStyle.Thick
                    ? "url(#mermaid-svg_flowchart-v2-pointEndThick)"
                    : "url(#mermaid-svg_flowchart-v2-pointEnd)"
                : null
        };

        var markerStart = edge.Type switch
        {
            EdgeType.BiDirectionalCircle => "url(#mermaid-svg_flowchart-v2-circleStart)",
            EdgeType.BiDirectionalCross => "url(#mermaid-svg_flowchart-v2-crossStart)",
            EdgeType.BiDirectional => "url(#mermaid-svg_flowchart-v2-pointStartBiDir)",
            _ => edge.HasArrowTail ? "url(#mermaid-svg_flowchart-v2-pointStart)" : null
        };

        // For cross terminals, keep the marker at the true endpoint while shortening the visible stroke.
        var requiresMarkerSeparation = edge.Type is EdgeType.CrossEnd or EdgeType.BiDirectionalCross;
        if (requiresMarkerSeparation)
        {
            // Visible stroke: trimmed points, no markers.
            builder.AddPath(pathData,
                fill: "none",
                stroke: edgeStroke,
                strokeWidth: strokeWidth,
                strokeDasharray: strokeDasharray,
                markerEnd: null,
                markerStart: null,
                cssClass: "flowchart-link");

            // Marker-only path: original points (untrimmed), markers attached, stroke invisible.
            // This ensures the X stays anchored at the node edge.
            var markerPathData = BuildFlowchartEdgePath(edge.Points, diagramDirection, sourceNode);
            builder.AddPath(markerPathData,
                fill: "none",
                stroke: "none",
                strokeWidth: 0.0,
                strokeDasharray: null,
                markerEnd: markerEnd,
                markerStart: markerStart,
                cssClass: "flowchart-link");
        }
        else
        {
            builder.AddPath(pathData,
                fill: "none",
                stroke: edgeStroke,
                strokeWidth: strokeWidth,
                strokeDasharray: strokeDasharray,
                markerEnd: markerEnd,
                markerStart: markerStart,
                cssClass: "flowchart-link");
        }

        // Render edge label if present
        if (!string.IsNullOrEmpty(edge.Label))
        {
            var labelX = edge.LabelPosition.X;
            var labelY = edge.LabelPosition.Y;
            var labelWidth = edge.Label.Length * 8 + 16;
            var labelHeight = 24;

            builder.AddRect(
                labelX - labelWidth / 2, labelY - labelHeight / 2,
                labelWidth, labelHeight,
                fill: labelBackground, stroke: "none",
                cssClass: "edgeLabel");

            builder.AddForeignObject(
                labelX - labelWidth / 2, labelY - labelHeight / 2,
                labelWidth, labelHeight,
                $"<p>{System.Net.WebUtility.HtmlEncode(edge.Label)}</p>",
                className: "edgeLabel",
                fontSizePx: options.FontSize,
                opticalVerticalBiasEm: flowchartLabelOpticalBiasEm);
        }
    }

    /// <summary>
    /// Smooth cubic curves through Dagre polyline points (Catmull–Rom segment conversion).
    /// Phantom points at the chain ends follow <paramref name="diagramDirection"/> so forks arc smoothly like Mermaid preview.
    /// The final span ends with a straight segment aligned to rank flow (vertical in TB) so arrow markers sit squarely on the target node.
    /// Forks use <paramref name="sourceNode"/> so both outgoing edges share a Catmull control at the parent centre (Mermaid-style fan-out).
    /// </summary>
    static string BuildFlowchartEdgePath(IReadOnlyList<Position> points, Direction diagramDirection, Node? sourceNode)
    {
        if (points.Count < 2)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append('M').Append(Fmt(points[0].X)).Append(',').Append(Fmt(points[0].Y));

        var lastIdx = points.Count - 1;

        for (var segmentIdx = 0; segmentIdx < lastIdx - 1; segmentIdx++)
        {
            var p0 = segmentIdx == 0
                ? ForkPhantomBeforeExit(points[0], points[1], sourceNode, diagramDirection)
                : points[segmentIdx - 1];

            var p1 = points[segmentIdx];
            var p2 = points[segmentIdx + 1];

            var p3 = points[segmentIdx + 2];

            var c1x = p1.X + (p2.X - p0.X) / FlowchartCatmullChordDivisor;
            var c1y = p1.Y + (p2.Y - p0.Y) / FlowchartCatmullChordDivisor;
            var c2x = p2.X - (p3.X - p1.X) / FlowchartCatmullChordDivisor;
            var c2y = p2.Y - (p3.Y - p1.Y) / FlowchartCatmullChordDivisor;

            sb.Append('C')
                .Append(Fmt(c1x)).Append(',').Append(Fmt(c1y)).Append(' ')
                .Append(Fmt(c2x)).Append(',').Append(Fmt(c2y)).Append(' ')
                .Append(Fmt(p2.X)).Append(',').Append(Fmt(p2.Y));
        }

        AppendFinalStemSegment(sb, points, diagramDirection, lastIdx, sourceNode);

        return sb.ToString();
    }

    /// <summary>
    /// Finishes the edge with a cubic to a stem anchor, then a straight line in the diagram's flow direction to the connection point so the marker orientation matches Mermaid's long vertical/horizontal tail.
    /// </summary>
    static void AppendFinalStemSegment(StringBuilder sb, IReadOnlyList<Position> points, Direction diagramDirection, int lastIdx, Node? sourceNode)
    {
        var p1 = points[lastIdx - 1];
        var p2 = points[lastIdx];
        var p0 = lastIdx >= 2
            ? points[lastIdx - 2]
            : ForkPhantomBeforeExit(points[0], points[1], sourceNode, diagramDirection);

        ResolveStemAnchor(p1, p2, diagramDirection, out var pStem, out var stemLength);

        var c1x = p1.X + (pStem.X - p0.X) / FlowchartCatmullChordDivisor;
        var c1y = p1.Y + (pStem.Y - p0.Y) / FlowchartCatmullChordDivisor;

        var controlArm = Math.Max(6.0, stemLength * StemJointControlFactor);
        StemJointEndControl(pStem, diagramDirection, controlArm, out var c2x, out var c2y);

        sb.Append('C')
            .Append(Fmt(c1x)).Append(',').Append(Fmt(c1y)).Append(' ')
            .Append(Fmt(c2x)).Append(',').Append(Fmt(c2y)).Append(' ')
            .Append(Fmt(pStem.X)).Append(',').Append(Fmt(pStem.Y));

        if (stemLength > 0.75)
        {
            sb.Append('L').Append(Fmt(p2.X)).Append(',').Append(Fmt(p2.Y));
        }
    }

    /// <summary>
    /// Computes the stem start (flow-aligned with the target) and the length of the straight run to the arrow tip.
    /// </summary>
    static void ResolveStemAnchor(Position p1, Position p2, Direction diagramDirection, out Position pStem, out double stemLength)
    {
        switch (diagramDirection)
        {
            case Direction.TopToBottom:
            {
                var dy = p2.Y - p1.Y;
                if (Math.Abs(dy) > FinalApproachCurveReservePx)
                {
                    stemLength = Math.Max(MinFlowAlignedStemPx, Math.Abs(dy) * FinalApproachStemFraction);
                    stemLength = Math.Min(stemLength, Math.Abs(dy) - FinalApproachCurveReservePx);
                }
                else
                {
                    stemLength = Math.Max(4.0, Math.Abs(dy) * 0.35);
                }

                if (dy >= 0)
                {
                    pStem = new Position(p2.X, p2.Y - stemLength);
                }
                else
                {
                    pStem = new Position(p2.X, p2.Y + stemLength);
                }

                break;
            }

            case Direction.BottomToTop:
            {
                var dy = p1.Y - p2.Y;
                if (Math.Abs(dy) > FinalApproachCurveReservePx)
                {
                    stemLength = Math.Max(MinFlowAlignedStemPx, Math.Abs(dy) * FinalApproachStemFraction);
                    stemLength = Math.Min(stemLength, Math.Abs(dy) - FinalApproachCurveReservePx);
                }
                else
                {
                    stemLength = Math.Max(4.0, Math.Abs(dy) * 0.35);
                }

                if (dy >= 0)
                {
                    pStem = new Position(p2.X, p2.Y + stemLength);
                }
                else
                {
                    pStem = new Position(p2.X, p2.Y - stemLength);
                }

                break;
            }

            case Direction.LeftToRight:
            {
                var dx = p2.X - p1.X;
                if (Math.Abs(dx) > FinalApproachCurveReservePx)
                {
                    stemLength = Math.Max(MinFlowAlignedStemPx, Math.Abs(dx) * FinalApproachStemFraction);
                    stemLength = Math.Min(stemLength, Math.Abs(dx) - FinalApproachCurveReservePx);
                }
                else
                {
                    stemLength = Math.Max(4.0, Math.Abs(dx) * 0.35);
                }

                if (dx >= 0)
                {
                    pStem = new Position(p2.X - stemLength, p2.Y);
                }
                else
                {
                    pStem = new Position(p2.X + stemLength, p2.Y);
                }

                break;
            }

            case Direction.RightToLeft:
            {
                var dx = p1.X - p2.X;
                if (Math.Abs(dx) > FinalApproachCurveReservePx)
                {
                    stemLength = Math.Max(MinFlowAlignedStemPx, Math.Abs(dx) * FinalApproachStemFraction);
                    stemLength = Math.Min(stemLength, Math.Abs(dx) - FinalApproachCurveReservePx);
                }
                else
                {
                    stemLength = Math.Max(4.0, Math.Abs(dx) * 0.35);
                }

                if (dx >= 0)
                {
                    pStem = new Position(p2.X + stemLength, p2.Y);
                }
                else
                {
                    pStem = new Position(p2.X - stemLength, p2.Y);
                }

                break;
            }

            default:
            {
                var dy = p2.Y - p1.Y;
                stemLength = Math.Max(MinFlowAlignedStemPx, Math.Abs(dy) * FinalApproachStemFraction);
                pStem = new Position(p2.X, p2.Y - stemLength);
                break;
            }
        }

        stemLength = Distance(pStem, p2);
    }

    /// <summary>
    /// Second control point for the cubic ending at <paramref name="pStem"/> so the curve tangent matches the stem line toward the node.
    /// </summary>
    static void StemJointEndControl(Position pStem, Direction diagramDirection, double offset, out double c2x, out double c2y)
    {
        switch (diagramDirection)
        {
            case Direction.TopToBottom:
                c2x = pStem.X;
                c2y = pStem.Y - offset;
                break;

            case Direction.BottomToTop:
                c2x = pStem.X;
                c2y = pStem.Y + offset;
                break;

            case Direction.LeftToRight:
                c2x = pStem.X - offset;
                c2y = pStem.Y;
                break;

            case Direction.RightToLeft:
                c2x = pStem.X + offset;
                c2y = pStem.Y;
                break;

            default:
                c2x = pStem.X;
                c2y = pStem.Y - offset;
                break;
        }
    }

    /// <summary>
    /// Catmull–Rom control before the first polyline vertex on an edge leaving a node.
    /// When <paramref name="sourceNode"/> is set, uses the parent’s layout centre so all outgoing links at a fork
    /// share a common radial origin, matching Mermaid’s fan-out: backward extensions meet at the node’s 2D centre, which is the
    /// projection of a virtual apex in front of the box (positive view Z / toward the viewer), not a higher SVG Y by itself.
    /// </summary>
    static Position ForkPhantomBeforeExit(Position exitPoint, Position towardNext, Node? sourceNode, Direction diagramDirection)
    {
        if (sourceNode is not null)
        {
            return new Position(sourceNode.Position.X, sourceNode.Position.Y);
        }

        return StartPhantom(exitPoint, towardNext, diagramDirection);
    }

    /// <summary>
    /// Synthetic control point before the first routed vertex so the spline exits the first bend with flow-aligned curvature.
    /// </summary>
    static Position StartPhantom(Position first, Position second, Direction diagramDirection)
    {
        var length = Distance(first, second);

        return diagramDirection switch
        {
            Direction.TopToBottom => new Position(first.X, first.Y - length),
            Direction.BottomToTop => new Position(first.X, first.Y + length),
            Direction.LeftToRight => new Position(first.X - length, first.Y),
            Direction.RightToLeft => new Position(first.X + length, first.Y),
            _ => new Position(first.X, first.Y - length)
        };
    }

    static double Distance(Position a, Position b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;

        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Converts FontAwesome icon syntax (fa:fa-icon) to HTML elements.
    /// </summary>
    static string ConvertIconsToHtml(string text)
    {
        // If no icons, just encode and wrap in paragraph
        if (!iconPattern.IsMatch(text))
        {
            return $"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>";
        }

        // Build HTML by processing text segments and icons
        var html = new StringBuilder();
        var lastIndex = 0;

        foreach (Match match in iconPattern.Matches(text))
        {
            // Add text before this icon (encoded)
            if (match.Index > lastIndex)
            {
                var textBefore = text[lastIndex..match.Index];
                html.Append(System.Net.WebUtility.HtmlEncode(textBefore));
            }

            // Add the icon element
            var prefix = match.Groups[1].Value;
            var iconName = match.Groups[2].Value;
            html.Append($"<i class=\"{prefix} fa-{iconName}\"></i>");

            lastIndex = match.Index + match.Length;
        }

        // Add remaining text after last icon
        if (lastIndex < text.Length)
        {
            html.Append(System.Net.WebUtility.HtmlEncode(text[lastIndex..]));
        }

        return $"<p>{html.ToString().Trim()}</p>";
    }

    static Size MeasureText(string text, double fontSize)
    {
        var width = text.Length * fontSize * 0.55;
        var height = fontSize * 1.5;
        return new(width, height);
    }

    static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

namespace MermaidSharp.Diagrams.Flowchart;

public partial class FlowchartRenderer(ILayoutEngine? layoutEngine = null) :
    IDiagramRenderer<FlowchartModel>
{
    ILayoutEngine layoutEngine = layoutEngine ?? new DagreLayoutEngine();

    const string nodeFill = "#ECECFF";
    const string nodeStroke = "#9370DB";
    const string edgeStroke = "#333333";

    /// <summary>
    /// Flowchart nodes use Trebuchet in browser CSS; Word SVG uses Arial. Slightly lower em nudge than other diagrams for better optical vertical centre in boxes.
    /// </summary>
    const double flowchartLabelOpticalBiasEm = 0.09;

    /// <summary>
    /// Extra downward shift for node label text in SVG user units (Word vs Mermaid vertical centring).
    /// </summary>
    const double FlowchartNodeLabelExtraVerticalOffsetPx = 4;

    /// <summary>
    /// Mermaid flowchart defaults (from Mermaid source):
    /// nodeSpacing = 50, rankSpacing = 50.
    /// </summary>
    const double FlowchartNodeSpacingPx = 140;

    const double FlowchartRankSpacingPx = 140;

    /// <summary>
    /// Mermaid flowchart default diagram padding (from Mermaid source): diagramPadding = 8.
    /// </summary>
    const double FlowchartDiagramPaddingPx = 8;

    // FontAwesome icon pattern: fa:fa-icon-name or fab:fa-icon-name
    static Regex iconPattern = IconPatternMyRegex();
    [GeneratedRegex("(fa[bsr]?):fa-([a-z0-9-]+)", RegexOptions.Compiled)]
    private static partial Regex IconPatternMyRegex();

    public SvgDocument Render(FlowchartModel model, RenderOptions options)
    {
        ApplyStyles(model);

        // Calculate node sizes based on text
        foreach (var node in model.Nodes)
        {
            var label = node.Label ?? node.Id;
            label = NormaliseHtmlLineBreaks(label);
            // Strip icon syntax for measurement
            var textForMeasure = iconPattern.Replace(label, "").Trim();
            textForMeasure = textForMeasure.Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase);
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
                // Mermaid's diamond ("question") is a square polygon sized from the label bbox.
                // See Mermaid's flowChartShapes.js question(): s = (w + h) * 0.9 and polygon is s x s.
#pragma warning disable IDE0007
                double s = (node.Width + node.Height) * 0.9;
#pragma warning restore IDE0007
                node.Width = s;
                node.Height = s;
            }
        }

        // Run layout
        var layoutOptions = new LayoutOptions
        {
            Direction = model.Direction,
            NodeSeparation = FlowchartNodeSpacingPx,
            RankSeparation = FlowchartRankSpacingPx,
            // Weighted edges (longer "-->") need longest-path layering; TightTree compaction was collapsing minlen gaps.
            Ranker = RankerType.LongestPath
        };
        var layoutResult = layoutEngine.Layout(model, layoutOptions);

        // Subgraph direction is now handled by the layout engine (Mermaid-style group nodes + per-subgraph dir).
        // Keeping the old post-layout rotation heuristic would double-apply the transform.
        UpdateGroupNodesFromSubgraphBounds(model);

        // Build SVG. Mermaid computes the viewport from the rendered element bounds plus diagramPadding.
#pragma warning disable IDE0007
        MermaidSharp.Models.Rect bounds = ComputeFlowchartContentBounds(model, options);
        var viewBox = $"{Fmt(bounds.X - FlowchartDiagramPaddingPx)} {Fmt(bounds.Y - FlowchartDiagramPaddingPx)} {Fmt(bounds.Width + (FlowchartDiagramPaddingPx * 2.0))} {Fmt(bounds.Height + (FlowchartDiagramPaddingPx * 2.0))}";
#pragma warning restore IDE0007

        var builder = new SvgBuilder()
            .Size(bounds.Width + (FlowchartDiagramPaddingPx * 2.0), bounds.Height + (FlowchartDiagramPaddingPx * 2.0))
            .ViewBox(viewBox)
            .AddMermaidArrowMarker()
            .AddMermaidCircleMarker()
            .AddMermaidCrossMarker()
            .AddNodeDropShadowFilter();

        // Add mermaid.ink CSS styles
        builder.AddStyles(MermaidStyles.FlowchartStyles);

        // Render subgraphs behind edges and nodes.
        RenderSubgraphs(builder, model, options);

        // Render edges (behind nodes)
#pragma warning disable IDE0007
        Dictionary<string, int> IncomingCountByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Edge edge in model.Edges)
        {
            if (!IncomingCountByNodeId.TryGetValue(edge.TargetId, out int count))
            {
                count = 0;
            }
            IncomingCountByNodeId[edge.TargetId] = count + 1;
        }
#pragma warning restore IDE0007

        foreach (var edge in model.Edges)
        {
#pragma warning disable IDE0007
            Node? sourceNode = model.GetNode(edge.SourceId);
            Node? targetNode = model.GetNode(edge.TargetId);
            bool targetHasSingleIncoming = IncomingCountByNodeId.TryGetValue(edge.TargetId, out int incomingCount) && incomingCount == 1;
#pragma warning restore IDE0007
            RenderEdge(builder, edge, options, model.Direction, sourceNode, targetNode, targetHasSingleIncoming);
        }

        // Render nodes
#pragma warning disable IDE0007
        HashSet<string> SubgraphIds = CollectSubgraphIds(model);
#pragma warning restore IDE0007
        foreach (var node in model.Nodes)
        {
            if (node.IsGroup || SubgraphIds.Contains(node.Id))
            {
                continue;
            }

            RenderNode(builder, node, options);
        }

        return builder.Build();
    }

    static HashSet<string> CollectSubgraphIds(FlowchartModel model)
    {
#pragma warning disable IDE0007
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (MermaidSharp.Models.Subgraph subgraph in model.Subgraphs)
        {
            CollectSubgraphIdsRecursive(ids, subgraph);
        }
        return ids;
#pragma warning restore IDE0007
    }

    static void CollectSubgraphIdsRecursive(HashSet<string> ids, MermaidSharp.Models.Subgraph subgraph)
    {
#pragma warning disable IDE0007
        ids.Add(subgraph.Id);
        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            CollectSubgraphIdsRecursive(ids, nested);
        }
#pragma warning restore IDE0007
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
#pragma warning disable IDE0007
        MermaidSharp.Models.Style? subgraphStyle = null;
        if (model.StyleByElementId.TryGetValue(subgraph.Id, out MermaidSharp.Models.Style? styleOverride) && styleOverride is not null)
        {
            subgraphStyle = styleOverride;
        }
#pragma warning restore IDE0007

        builder.AddRect(
            Bounds.X,
            Bounds.Y,
            Bounds.Width,
            Bounds.Height,
            fill: subgraphStyle?.Fill ?? "#ffffde",
            stroke: subgraphStyle?.Stroke ?? "#aaaa33",
            strokeWidth: subgraphStyle?.StrokeWidth ?? 1,
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

    static void UpdateGroupNodesFromSubgraphBounds(FlowchartModel model)
    {
#pragma warning disable IDE0007
        if (model.Subgraphs.Count == 0)
        {
            return;
        }

        foreach (MermaidSharp.Models.Subgraph subgraph in model.Subgraphs)
        {
            UpdateGroupNodesFromSubgraphBoundsRecursive(model, subgraph);
        }
#pragma warning restore IDE0007
    }

    static void UpdateGroupNodesFromSubgraphBoundsRecursive(FlowchartModel model, MermaidSharp.Models.Subgraph subgraph)
    {
#pragma warning disable IDE0007
        MermaidSharp.Models.Rect bounds = ComputeSubgraphBounds(model, subgraph);
        Node? groupNode = model.GetNode(subgraph.Id);
        if (groupNode != null)
        {
            groupNode.IsGroup = true;
            if (groupNode.Width <= 0 || groupNode.Height <= 0)
            {
                groupNode.Width = bounds.Width;
                groupNode.Height = bounds.Height;
            }
            if (groupNode.Position == Position.Zero)
            {
                groupNode.Position = new Position(bounds.X + (bounds.Width / 2.0), bounds.Y + (bounds.Height / 2.0));
            }
            if (string.IsNullOrWhiteSpace(groupNode.Label))
            {
                groupNode.Label = subgraph.Title ?? subgraph.Id;
            }
        }

        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            UpdateGroupNodesFromSubgraphBoundsRecursive(model, nested);
        }
#pragma warning restore IDE0007
    }

    static void RenderNode(SvgBuilder builder, Node node, RenderOptions options)
    {
        if (node.IsGroup)
        {
            return;
        }

#pragma warning disable IDE0007
        var x = node.Position.X - node.Width / 2;
        var y = node.Position.Y - node.Height / 2;

        var shapePath = ShapePathGenerator.GetPath(node.Shape, x, y, node.Width, node.Height);
#pragma warning restore IDE0007

#pragma warning disable IDE0007
        string fill = node.Style.Fill ?? nodeFill;
        string stroke = node.Style.Stroke ?? nodeStroke;
        double strokeWidth = node.Style.StrokeWidth ?? 1.0;
#pragma warning restore IDE0007

        builder.AddPath(shapePath,
            fill: fill,
            stroke: stroke,
            strokeWidth: strokeWidth,
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
            extraVerticalOffsetPx: FlowchartNodeLabelExtraVerticalOffsetPx,
            fill: node.Style.TextColor);
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
        Result = Result.Replace("\\n", "<br/>", StringComparison.Ordinal);
        Result = Result.Replace("\r\n", "<br/>", StringComparison.Ordinal);
        Result = Result.Replace("\n", "<br/>", StringComparison.Ordinal);
        Result = Result.Replace("</br>", "<br/>", StringComparison.OrdinalIgnoreCase);
        Result = Result.Replace("<br>", "<br/>", StringComparison.OrdinalIgnoreCase);
        Result = Result.Replace("<br/>", "<br/>", StringComparison.OrdinalIgnoreCase);
        return Result;
#pragma warning restore IDE0007
    }

    static void RenderEdge(SvgBuilder builder, Edge edge, RenderOptions options, Direction diagramDirection, Node? sourceNode, Node? targetNode, bool targetHasSingleIncoming)
    {
        if (edge.Points.Count < 2)
        {
            return;
        }

        IReadOnlyList<Position> pointsForPath = edge.Points;

        if (ShouldSnapDecisionBranchTargetPort(edge, sourceNode, targetNode, targetHasSingleIncoming))
        {
            pointsForPath = SnapDecisionBranchTargetPort(edge.Points, sourceNode, targetNode!);
        }

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

        if ((startGapPx > 0 || endGapPx > 0) && pointsForPath.Count >= 2)
        {
            var adjustedPoints = new List<Position>(pointsForPath);

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

        var pathData = ShouldUseDecisionBranchCubic(edge, sourceNode)
            ? BuildDecisionBranchPath(pointsForPath, sourceNode, targetNode)
            : BuildFlowchartEdgePath(pointsForPath, diagramDirection, sourceNode);

#pragma warning disable IDE0007
        string? strokeDasharray = edge.Style.StrokeDasharray;
        if (strokeDasharray is null)
        {
            strokeDasharray = edge.LineStyle switch
            {
                EdgeStyle.Dotted => "2",
                _ => null
            };
        }

        double strokeWidth = edge.Style.StrokeWidth ?? (edge.LineStyle switch
        {
            EdgeStyle.Thick => 3.5,
            _ => 1.0
        });

        string stroke = edge.Style.Stroke ?? edgeStroke;
#pragma warning restore IDE0007

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
                stroke: stroke,
                strokeWidth: strokeWidth,
                strokeDasharray: strokeDasharray,
                markerEnd: null,
                markerStart: null,
                cssClass: "flowchart-link");

            // Marker-only path: original points (untrimmed), markers attached, stroke invisible.
            // This ensures the X stays anchored at the node edge.
            IReadOnlyList<Position> markerPoints = edge.Points;
            if (ShouldSnapDecisionBranchTargetPort(edge, sourceNode, targetNode, targetHasSingleIncoming))
            {
                markerPoints = SnapDecisionBranchTargetPort(edge.Points, sourceNode, targetNode!);
            }

            var markerPathData = ShouldUseDecisionBranchCubic(edge, sourceNode)
                ? BuildDecisionBranchPath(markerPoints, sourceNode, targetNode)
                : BuildFlowchartEdgePath(markerPoints, diagramDirection, sourceNode);
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
                stroke: stroke,
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
#pragma warning disable IDE0007
            Size labelSize = MeasureText(edge.Label, options.FontSize);
            double labelWidth = labelSize.Width + 16;
            double labelHeight = Math.Max(24, labelSize.Height + 8);
#pragma warning restore IDE0007
            // Word parity: keep edge labels with transparent background (no filled rect).
            builder.AddRect(
                labelX - labelWidth / 2, labelY - labelHeight / 2,
                labelWidth, labelHeight,
                fill: "none", stroke: "none",
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

    static MermaidSharp.Models.Rect ComputeFlowchartContentBounds(FlowchartModel model, RenderOptions options)
    {
#pragma warning disable IDE0007
        bool hasAny = false;
        double minX = 0;
        double minY = 0;
        double maxX = 0;
        double maxY = 0;

        void AddBounds(double left, double top, double right, double bottom)
        {
            if (!hasAny)
            {
                hasAny = true;
                minX = left;
                minY = top;
                maxX = right;
                maxY = bottom;
                return;
            }

            minX = Math.Min(minX, left);
            minY = Math.Min(minY, top);
            maxX = Math.Max(maxX, right);
            maxY = Math.Max(maxY, bottom);
        }

        foreach (Node node in model.Nodes)
        {
            double left = node.Position.X - node.Width / 2.0;
            double top = node.Position.Y - node.Height / 2.0;
            double right = node.Position.X + node.Width / 2.0;
            double bottom = node.Position.Y + node.Height / 2.0;
            AddBounds(left, top, right, bottom);
        }

        foreach (MermaidSharp.Models.Subgraph subgraph in model.Subgraphs)
        {
            MermaidSharp.Models.Rect subgraphBounds = ComputeSubgraphBounds(model, subgraph);
            if (subgraphBounds.Width > 0 && subgraphBounds.Height > 0)
            {
                AddBounds(subgraphBounds.X, subgraphBounds.Y, subgraphBounds.X + subgraphBounds.Width, subgraphBounds.Y + subgraphBounds.Height);
            }
        }

        foreach (Edge edge in model.Edges)
        {
            if (!string.IsNullOrWhiteSpace(edge.Label))
            {
                Size labelSize = MeasureText(edge.Label, options.FontSize);
                double labelWidth = labelSize.Width + 16;
                double labelHeight = Math.Max(24, labelSize.Height + 8);
                double left = edge.LabelPosition.X - (labelWidth / 2.0);
                double top = edge.LabelPosition.Y - (labelHeight / 2.0);
                double right = edge.LabelPosition.X + (labelWidth / 2.0);
                double bottom = edge.LabelPosition.Y + (labelHeight / 2.0);
                AddBounds(left, top, right, bottom);
            }

            foreach (Position point in edge.Points)
            {
                AddBounds(point.X, point.Y, point.X, point.Y);
            }
        }

        if (!hasAny)
        {
            return new MermaidSharp.Models.Rect(0, 0, 0, 0);
        }

        // Allow for marker extents beyond the last edge point.
        const double MARKER_MARGIN_PX = 12.0;
        minX -= MARKER_MARGIN_PX;
        minY -= MARKER_MARGIN_PX;
        maxX += MARKER_MARGIN_PX;
        maxY += MARKER_MARGIN_PX;
        return new MermaidSharp.Models.Rect(minX, minY, maxX - minX, maxY - minY);
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Mermaid uses <c>d3.line().curve(curveBasis)</c> for flowchart edges (dagre-d3).
    /// We emit the same curveBasis path from the routed polyline points.
    /// </summary>
#pragma warning disable IDE0022
    static string BuildFlowchartEdgePath(IReadOnlyList<Position> points, Direction diagramDirection, Node? sourceNode)
    {
        return BuildFlowchartCurveBasisPath(points);
    }

    static bool ShouldSnapDecisionBranchTargetPort(Edge edge, Node? sourceNode, Node? targetNode, bool targetHasSingleIncoming)
    {
#pragma warning disable IDE0007
        if (!targetHasSingleIncoming)
        {
            return false;
        }

        if (targetNode is null)
        {
            return false;
        }

        // Only apply to the decision branches.
        if (!ShouldUseDecisionBranchCubic(edge, sourceNode))
        {
            return false;
        }

        return true;
#pragma warning restore IDE0007
    }

    static IReadOnlyList<Position> SnapDecisionBranchTargetPort(IReadOnlyList<Position> points, Node? sourceNode, Node targetNode)
    {
#pragma warning disable IDE0007
        if (points.Count < 2)
        {
            return points;
        }

        List<Position> snapped = new List<Position>(points);
        Position last = snapped[^1];
        Position first = snapped[0];

        MermaidSharp.Models.Rect bounds = targetNode.Bounds;

        // Subgraphs can override direction; the overall diagram direction isn't sufficient here.
        // Decide port side based on actual geometry: if the target is to the right, use left-centre; otherwise right-centre.
        double directionDx = last.X - first.X;
        if (sourceNode is not null)
        {
            directionDx = targetNode.Position.X - sourceNode.Position.X;
        }

        double x = directionDx >= 0 ? bounds.X : bounds.X + bounds.Width;
        double y = targetNode.Position.Y;

        snapped[^1] = new Position(x, y);

        // If the segment into the target is extremely short, also nudge the penultimate point so the curve
        // approaches more cleanly (prevents a tiny “kink” at the port).
        Position penultimate = snapped[^2];
        double segmentDx = Math.Abs(penultimate.X - last.X);
        double segmentDy = Math.Abs(penultimate.Y - last.Y);
        if (segmentDx < 4.0 && segmentDy < 4.0)
        {
            snapped[^2] = new Position(penultimate.X, y);
        }

        return snapped;
#pragma warning restore IDE0007
    }

    static bool ShouldUseDecisionBranchCubic(Edge edge, Node? sourceNode)
    {
#pragma warning disable IDE0007
        if (string.IsNullOrWhiteSpace(edge.Label))
        {
            return false;
        }

        string label = edge.Label.Trim();
        bool isYesNo = string.Equals(label, "Yes", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(label, "No", StringComparison.OrdinalIgnoreCase);

        if (!isYesNo)
        {
            return false;
        }

        // Prefer applying only when it looks like a decision branch, but allow for layout-inserted dummy segments
        // where the immediate source node is not the diamond.
        if (sourceNode is not null && sourceNode.Shape == NodeShape.Diamond)
        {
            return true;
        }

        return edge.Points.Count >= 2;
#pragma warning restore IDE0007
    }

    static string BuildDecisionBranchPath(IReadOnlyList<Position> points, Node? sourceNode, Node? targetNode)
    {
#pragma warning disable IDE0007
        if (points.Count < 2)
        {
            return string.Empty;
        }

        Position p0 = points[0];
        Position p3 = points[^1];

        // Mermaid-style links have short straight stubs at endpoints. For parity, those stubs must be
        // perpendicular to the node boundary (i.e. along the boundary normal at the attach point),
        // not necessarily horizontal/vertical.
        const double MAX_ENDPOINT_STUB_PX = 18.0;
        const double MIN_ENDPOINT_STUB_PX = 10.0;

        Position startNormal = ComputeEndpointNormal(p0, sourceNode, fallbackToward: points.Count >= 2 ? points[1] : p3, outward: true);
        Position endNormal = ComputeEndpointNormal(p3, targetNode, fallbackToward: points.Count >= 2 ? points[^2] : p0, outward: false);

        double chordDx = p3.X - p0.X;
        double chordDy = p3.Y - p0.Y;
        double chordLen = Math.Sqrt((chordDx * chordDx) + (chordDy * chordDy));
        double stubLen = chordLen > 0.0 ? Math.Min(MAX_ENDPOINT_STUB_PX, Math.Max(MIN_ENDPOINT_STUB_PX, chordLen * 0.08)) : MIN_ENDPOINT_STUB_PX;

        Position s0 = p0;
        Position s1 = new Position(p0.X + (startNormal.X * stubLen), p0.Y + (startNormal.Y * stubLen));

        Position e3 = p3;
        Position e2 = new Position(p3.X + (endNormal.X * stubLen), p3.Y + (endNormal.Y * stubLen));

        double midDx = e2.X - s1.X;
        double midDy = e2.Y - s1.Y;
        double midLen = Math.Sqrt((midDx * midDx) + (midDy * midDy));
        double controlLen = midLen > 0.0 ? Math.Max(12.0, Math.Min(60.0, midLen * 0.35)) : 24.0;

        Position c1 = new Position(s1.X + (startNormal.X * controlLen), s1.Y + (startNormal.Y * controlLen));
        Position c2 = new Position(e2.X - (endNormal.X * controlLen), e2.Y - (endNormal.Y * controlLen));

        return $"M{Fmt(s0.X)},{Fmt(s0.Y)} L{Fmt(s1.X)},{Fmt(s1.Y)} C{Fmt(c1.X)},{Fmt(c1.Y)} {Fmt(c2.X)},{Fmt(c2.Y)} {Fmt(e2.X)},{Fmt(e2.Y)} L{Fmt(e3.X)},{Fmt(e3.Y)}";
#pragma warning restore IDE0007
    }

    static Position ComputeEndpointNormal(Position attachPoint, Node? node, Position fallbackToward, bool outward)
    {
#pragma warning disable IDE0007
        Position dir;
        if (node is not null)
        {
            // Use centre-to-attach as boundary normal proxy (good for rect/ellipse/diamond where we already
            // computed attach points on the outline).
            dir = new Position(attachPoint.X - node.Position.X, attachPoint.Y - node.Position.Y);
            if (!outward)
            {
                dir = new Position(-dir.X, -dir.Y);
            }
        }
        else
        {
            // Fallback: use the adjacent polyline direction.
            dir = outward
                ? new Position(attachPoint.X - fallbackToward.X, attachPoint.Y - fallbackToward.Y)
                : new Position(fallbackToward.X - attachPoint.X, fallbackToward.Y - attachPoint.Y);
        }

        double len = Math.Sqrt((dir.X * dir.X) + (dir.Y * dir.Y));
        if (len <= 0.0001)
        {
            return new Position(1.0, 0.0);
        }

        return new Position(dir.X / len, dir.Y / len);
#pragma warning restore IDE0007
    }
#pragma warning restore IDE0022

    // Exact d3.curveBasis conversion (see https://raw.githubusercontent.com/d3/d3-shape/main/src/curve/basis.js)
    static string BuildFlowchartCurveBasisPath(IReadOnlyList<Position> points)
    {
#pragma warning disable IDE0007
        if (points.Count < 2)
        {
            return string.Empty;
        }

        if (points.Count == 2)
        {
            return $"M{Fmt(points[0].X)},{Fmt(points[0].Y)} L{Fmt(points[1].X)},{Fmt(points[1].Y)}";
        }

        StringBuilder sb = new StringBuilder();

        double x0 = points[0].X;
        double y0 = points[0].Y;
        double x1 = points[0].X;
        double y1 = points[0].Y;

        sb.Append('M').Append(Fmt(points[0].X)).Append(',').Append(Fmt(points[0].Y));

        // point 1 updates state
        x1 = points[1].X;
        y1 = points[1].Y;

        // point 2: lineTo((5*x0 + x1)/6, (5*y0 + y1)/6)
        double lx = ((5.0 * x0) + x1) / 6.0;
        double ly = ((5.0 * y0) + y1) / 6.0;
        sb.Append('L').Append(Fmt(lx)).Append(',').Append(Fmt(ly));

        for (int i = 2; i < points.Count; i++)
        {
            double px = points[i].X;
            double py = points[i].Y;

            double c1x = ((2.0 * x0) + x1) / 3.0;
            double c1y = ((2.0 * y0) + y1) / 3.0;
            double c2x = (x0 + (2.0 * x1)) / 3.0;
            double c2y = (y0 + (2.0 * y1)) / 3.0;
            double ex = (x0 + (4.0 * x1) + px) / 6.0;
            double ey = (y0 + (4.0 * y1) + py) / 6.0;

            sb.Append('C')
                .Append(Fmt(c1x)).Append(',').Append(Fmt(c1y)).Append(' ')
                .Append(Fmt(c2x)).Append(',').Append(Fmt(c2y)).Append(' ')
                .Append(Fmt(ex)).Append(',').Append(Fmt(ey));

            x0 = x1;
            y0 = y1;
            x1 = px;
            y1 = py;
        }

        // lineEnd emulation (same as ER curveBasis helper)
        double c1xe = ((2.0 * x0) + x1) / 3.0;
        double c1ye = ((2.0 * y0) + y1) / 3.0;
        double c2xe = (x0 + (2.0 * x1)) / 3.0;
        double c2ye = (y0 + (2.0 * y1)) / 3.0;
        double exe = (x0 + (4.0 * x1) + x1) / 6.0;
        double eye = (y0 + (4.0 * y1) + y1) / 6.0;

        sb.Append('C')
            .Append(Fmt(c1xe)).Append(',').Append(Fmt(c1ye)).Append(' ')
            .Append(Fmt(c2xe)).Append(',').Append(Fmt(c2ye)).Append(' ')
            .Append(Fmt(exe)).Append(',').Append(Fmt(eye));

        sb.Append('L').Append(Fmt(x1)).Append(',').Append(Fmt(y1));

        return sb.ToString();
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Converts FontAwesome icon syntax (fa:fa-icon) to HTML elements.
    /// </summary>
    static string ConvertIconsToHtml(string text)
    {
        static string HtmlEncodePreservingBr(string value)
        {
#pragma warning disable IDE0007
            string encoded = System.Net.WebUtility.HtmlEncode(value);
            encoded = encoded.Replace("&lt;br/&gt;", "<br/>", StringComparison.OrdinalIgnoreCase);
            encoded = encoded.Replace("&lt;br&gt;", "<br/>", StringComparison.OrdinalIgnoreCase);
            return encoded;
#pragma warning restore IDE0007
        }

        // If no icons, just encode and wrap in paragraph
        if (!iconPattern.IsMatch(text))
        {
            return $"<p>{HtmlEncodePreservingBr(text)}</p>";
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
                html.Append(HtmlEncodePreservingBr(textBefore));
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
            html.Append(HtmlEncodePreservingBr(text[lastIndex..]));
        }

        return $"<p>{html.ToString().Trim()}</p>";
    }

    static Size MeasureText(string text, double fontSize)
    {
#pragma warning disable IDE0007
        if (string.IsNullOrEmpty(text))
        {
            return new Size(0, fontSize * 1.5);
        }

        string[] Lines = text.Split('\n');
        int MaxLineLength = 0;
        foreach (string Line in Lines)
        {
            int LineLength = Line.Length;
            if (LineLength > MaxLineLength)
            {
                MaxLineLength = LineLength;
            }
        }

        // Mermaid measures actual rendered glyph widths (Trebuchet MS in browser). Word uses Arial, and our earlier
        // constant slightly under-estimated typical widths, compressing node-to-node spacing.
        double Width = MaxLineLength * fontSize * 0.62;
        double Height = Lines.Length * fontSize * 1.5;
        return new Size(Width, Height);
#pragma warning restore IDE0007
    }

    static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    static void ApplyStyles(FlowchartModel model)
    {
#pragma warning disable IDE0007
        // Nodes: classDef + class + style directive.
        foreach (Node node in model.Nodes)
        {
            MermaidSharp.Models.Style effective = new MermaidSharp.Models.Style();

            if (model.ClassesByNodeId.TryGetValue(node.Id, out List<string>? classNames))
            {
                foreach (string className in classNames)
                {
                    if (model.ClassDefinitions.TryGetValue(className, out MermaidSharp.Models.Style? classStyle) && classStyle is not null)
                    {
                        MergeStyle(effective, classStyle);
                    }
                }
            }

            if (model.StyleByElementId.TryGetValue(node.Id, out MermaidSharp.Models.Style? styleOverride) && styleOverride is not null)
            {
                MergeStyle(effective, styleOverride);
            }

            node.Style = effective;
        }

        // Edges: linkStyle by index.
        for (int i = 0; i < model.Edges.Count; i++)
        {
            Edge edge = model.Edges[i];
            if (model.StyleByLinkIndex.TryGetValue(i, out MermaidSharp.Models.Style? edgeStyle) && edgeStyle is not null)
            {
                edge.Style = edgeStyle;
            }
        }
#pragma warning restore IDE0007
    }

    static void MergeStyle(MermaidSharp.Models.Style target, MermaidSharp.Models.Style source)
    {
        if (source.Fill is not null)
        {
            target.Fill = source.Fill;
        }
        if (source.Stroke is not null)
        {
            target.Stroke = source.Stroke;
        }
        if (source.StrokeWidth.HasValue)
        {
            target.StrokeWidth = source.StrokeWidth;
        }
        if (source.StrokeDasharray is not null)
        {
            target.StrokeDasharray = source.StrokeDasharray;
        }
        if (source.FontFamily is not null)
        {
            target.FontFamily = source.FontFamily;
        }
        if (source.FontSize.HasValue)
        {
            target.FontSize = source.FontSize;
        }
        if (source.FontWeight is not null)
        {
            target.FontWeight = source.FontWeight;
        }
        if (source.TextColor is not null)
        {
            target.TextColor = source.TextColor;
        }
    }
}

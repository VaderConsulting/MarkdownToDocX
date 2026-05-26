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
    /// Visible connector stroke stops this many pixels before the centre of each cross (X) marker (Word parity).
    /// </summary>
    const double CrossStrokeGapPx = 10.2;

    /// <summary>
    /// Mermaid flowchart defaults (from Mermaid source).
    /// These can be overridden via Mermaid init config (%%{init: ...}%%).
    /// </summary>
    const double FLOWCHART_DEFAULT_NODE_SPACING_PX = 50.0;
    const double FLOWCHART_DEFAULT_RANK_SPACING_PX = 50.0;
    const double FLOWCHART_DEFAULT_DIAGRAM_PADDING_PX = 8.0;

    /// <summary>
    /// Mermaid’s HTML-label flowchart nodes constrain label width and wrap text.
    /// Word does not lay out HTML in SVG reliably, so we emit wrapped SVG text via <see cref="SvgForeignObject"/>.
    /// </summary>
    const double FlowchartNodeLabelMaxWidthPx = 200.0;

    /// <summary>Mermaid <c>flowDb</c> default when <c>flowchart.padding</c> is unset.</summary>
    const double FLOWCHART_DEFAULT_LABEL_PADDING_PX = 8.0;

    /// <summary>
    /// HTML label line box height in dagre-wrapper (<c>line-height: 1.5</c> plus <c>&lt;p&gt;</c> box). At 16px this yields ~40px inner height and 48px nodes with padding 8.
    /// </summary>
    const double FLOWCHART_HTML_LABEL_LINE_BOX_HEIGHT_EM = 2.5;

    /// <summary>Glyph-width estimate for short flowchart labels (Trebuchet 16px; matches mermaid.ink foreignObject widths).</summary>
    const double FLOWCHART_LABEL_CHAR_WIDTH_EM_SHORT = 0.512;

    /// <summary>Glyph-width estimate for longer single-line labels (Process, Go shopping, etc.).</summary>
    const double FLOWCHART_LABEL_CHAR_WIDTH_EM_LONG = 0.512;

    /// <summary>When measured width is below this fraction of the HTML line box, bump width (End-style short words).</summary>
    const double FLOWCHART_LABEL_MIN_WIDTH_TO_LINE_BOX_RATIO = 1.125;

    /// <summary>Browser HTML label <c>getBoundingClientRect</c> width vs glyph estimate for Trebuchet 16px (mermaid.ink squareRect).</summary>
    const double FLOWCHART_HTML_LABEL_BBOX_WIDTH_TO_LINE_BOX_RATIO = 1.68;

    /// <summary>
    /// Stadium label width vs 1.5em line-height box (mermaid.ink End foreignObject 28.47 / 24).
    /// </summary>
    const double FLOWCHART_STADIUM_LABEL_BBOX_WIDTH_TO_LINE_HEIGHT_RATIO = 1.186;

    const int FLOWCHART_LABEL_SHORT_WORD_MAX_LENGTH = 3;

    /// <summary>Mermaid dagre-wrapper <c>hexagon()</c> bevel divisor (<c>f = 4</c> for classic look).</summary>
    const double FLOWCHART_HEXAGON_BEVEL_DIVISOR = 4.0;

    /// <summary>
    /// Single-line HTML label height for hexagon sizing. Slightly taller than the 2.5em rect line box so the label
    /// is not clipped by the side bevels (matches mermaid.live proportions for labels such as <c>Hexagon</c>).
    /// </summary>
    const double FLOWCHART_HEXAGON_LINE_BOX_HEIGHT_EM = 3.0;

    /// <summary>Per-glyph width for hexagon labels (browser <c>getBoundingClientRect</c> on Trebuchet 16px).</summary>
    const double FLOWCHART_HEXAGON_LABEL_CHAR_WIDTH_EM = 0.54;

    /// <summary>
    /// Caps label width so <c>w = bbox.w + 2*(h/4) + padding</c> does not over-widen the shape when height drives bevel <c>m</c>.
    /// </summary>
    const double FLOWCHART_HEXAGON_MAX_LABEL_WIDTH_TO_LINE_BOX_RATIO = 1.05;

    // FontAwesome icon pattern: fa:fa-icon-name or fab:fa-icon-name
    static Regex iconPattern = IconPatternMyRegex();
    [GeneratedRegex("(fa[bsr]?):fa-([a-z0-9-]+)", RegexOptions.Compiled)]
    private static partial Regex IconPatternMyRegex();

    public SvgDocument Render(FlowchartModel model, RenderOptions options)
    {
        ApplyStyles(model);

#pragma warning disable IDE0007
        double LabelPaddingPx = GetConfigDouble(model, "flowchart.padding", FLOWCHART_DEFAULT_LABEL_PADDING_PX);
#pragma warning restore IDE0007

        // Calculate node sizes based on text
        foreach (var node in model.Nodes)
        {
            var label = node.Label ?? node.Id;
            label = NormaliseHtmlLineBreaks(label);
            // Strip icon syntax for measurement
            var textForMeasure = iconPattern.Replace(label, "").Trim();
            textForMeasure = textForMeasure.Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase);
#pragma warning disable IDE0007
            string wrappedForMeasure = WrapPlainTextToWidth(textForMeasure, options.FontSize, FlowchartNodeLabelMaxWidthPx);
            Size textSize = MeasureFlowchartLabelBBox(wrappedForMeasure, options.FontSize);
#pragma warning restore IDE0007
            // Add extra width for icon if present
#pragma warning disable IDE0007
            var hasIcon = iconPattern.IsMatch(label);
            double iconExtraWidthPx = hasIcon ? 20.0 : 0.0;
#pragma warning restore IDE0007
            // Mermaid squareRect: labelPaddingX = padding*2, drawRect totalWidth = bbox.width + labelPaddingX*2.
            var LabelPaddingX = LabelPaddingPx * 2.0;
            var LabelPaddingY = LabelPaddingPx;
            node.Width = textSize.Width + (2.0 * LabelPaddingX) + iconExtraWidthPx;
            node.Height = textSize.Height + (2.0 * LabelPaddingY);

            // Persist wrapped text for rendering (so Word matches Mermaid's automatic wrapping).
            // Only apply when the label does not already include explicit line breaks.
            if (!hasIcon
                && !label.Contains("<br/>", StringComparison.OrdinalIgnoreCase)
                && !label.Contains("\\n", StringComparison.Ordinal)
                && !label.Contains('\n'))
            {
                node.Label = wrappedForMeasure;
            }

            // Adjust size for different shapes
            if (node.Shape is NodeShape.Circle or NodeShape.DoubleCircle)
            {
                var diameter = Math.Max(node.Width, node.Height);
                node.Width = diameter;
                node.Height = diameter;
            }
            else if (node.Shape == NodeShape.Diamond)
            {
                // Mermaid dagre-wrapper question(): s = (bbox.width + padding) + (bbox.height + padding).
#pragma warning disable IDE0007
                double W0 = textSize.Width + LabelPaddingPx;
                double H0 = textSize.Height + LabelPaddingPx;
                double S = W0 + H0;
#pragma warning restore IDE0007
                node.Width = S;
                node.Height = S;
            }
            else if (node.Shape == NodeShape.Hexagon)
            {
                // dagre-wrapper hexagon(): m = h/4, w = bbox.width + 2*m + padding.
#pragma warning disable IDE0007
                Size HexLabelSize = MeasureFlowchartHexagonLabelBBox(wrappedForMeasure, options.FontSize);
                double H0 = HexLabelSize.Height + LabelPaddingPx;
                double M = H0 / FLOWCHART_HEXAGON_BEVEL_DIVISOR;
                node.Height = H0;
                node.Width = HexLabelSize.Width + (2.0 * M) + LabelPaddingPx + iconExtraWidthPx;
#pragma warning restore IDE0007
            }
            else if (node.Shape == NodeShape.Cylinder)
            {
                // dagre-wrapper cylinder(): w = bbox + P, h_side = bbox + ry + P, visual height = h_side + 2*ry.
#pragma warning disable IDE0007
                double W0 = textSize.Width + LabelPaddingPx + iconExtraWidthPx;
                double Ry = ShapePathGenerator.FlowchartCylinderEllipseRadiusY(W0);
                double HSide = textSize.Height + Ry + LabelPaddingPx;
                node.Width = W0;
                node.Height = HSide + (2.0 * Ry);
#pragma warning restore IDE0007
            }
            else if (node.Shape == NodeShape.Stadium)
            {
                // stadium.ts / dagre-wrapper stadium(): h = bbox.height + padding, w = bbox.width + h/4 + padding.
                // Do not use squareRect 2.5em line-box height — it inflates h, shrinks the flat middle to ~0, and renders End as a circle.
#pragma warning disable IDE0007
                double LabelInnerWidthPx = MeasureFlowchartStadiumInnerLabelWidth(wrappedForMeasure, options.FontSize);
                double LabelInnerHeightPx = MeasureFlowchartStadiumInnerLabelHeight(wrappedForMeasure, options.FontSize);
                node.Height = LabelInnerHeightPx + LabelPaddingPx;
                node.Width = LabelInnerWidthPx + (node.Height / 4.0) + LabelPaddingPx + iconExtraWidthPx;
#pragma warning restore IDE0007
            }
        }

        ApplyFlowchartEdgeLabelLayoutMetrics(model, options);

        // Run layout
        var nodeSeparationPx = GetConfigDouble(model, "flowchart.nodeSpacing", FLOWCHART_DEFAULT_NODE_SPACING_PX);
        var rankSeparationPx = GetConfigDouble(model, "flowchart.rankSpacing", FLOWCHART_DEFAULT_RANK_SPACING_PX);
        var diagramPaddingPx = GetConfigDouble(model, "flowchart.diagramPadding", FLOWCHART_DEFAULT_DIAGRAM_PADDING_PX);

        var layoutOptions = new LayoutOptions
        {
            Direction = model.Direction,
            NodeSeparation = nodeSeparationPx,
            RankSeparation = rankSeparationPx,
            // Weighted edges (longer "-->") need longest-path layering; TightTree compaction was collapsing minlen gaps.
            Ranker = RankerType.LongestPath
        };
        var layoutResult = layoutEngine.Layout(model, layoutOptions);

        // Subgraph direction: compound layout is handled in DagreLayoutEngine.
        // Nested subgraphs always use TD there (Mermaid renders LR/TB/BT/RL/TD the same for nested clusters).
        //
        // Intended (Mermaid): post-layout rotation for LR/RL subgraphs via ApplySubgraphDirections.
        // ApplySubgraphDirections(model);
        UpdateGroupNodesFromSubgraphBounds(model);

        // Mermaid Dagre renderer shifts edge route points and edge labels by subGraphTitleTotalMargin / 2 in Y.
        // This is driven by config.flowchart.subGraphTitleMargin.{top,bottom}.
        ApplySubgraphTitleMarginShiftToEdges(model);

        // Build SVG. Mermaid computes the viewport from the rendered element bounds plus diagramPadding.
#pragma warning disable IDE0007
        MermaidSharp.Models.Rect bounds = ComputeFlowchartContentBounds(model, options);
        var viewBox = $"{Fmt(bounds.X - diagramPaddingPx)} {Fmt(bounds.Y - diagramPaddingPx)} {Fmt(bounds.Width + (diagramPaddingPx * 2.0))} {Fmt(bounds.Height + (diagramPaddingPx * 2.0))}";
#pragma warning restore IDE0007

#pragma warning disable IDE0007
        MermaidSharp.Rendering.MermaidThemeVariables Theme = MermaidSharp.Rendering.MermaidThemeResolver.Resolve(model, options);
#pragma warning restore IDE0007

        var builder = new SvgBuilder()
            .Size(bounds.Width + (diagramPaddingPx * 2.0), bounds.Height + (diagramPaddingPx * 2.0))
            .ViewBox(viewBox)
            .AddMermaidArrowMarker(stroke: Theme.ArrowheadColor, fill: Theme.ArrowheadColor)
            .AddMermaidCircleMarker(stroke: Theme.ArrowheadColor)
            .AddMermaidCrossMarker(stroke: Theme.ArrowheadColor);

        // Add mermaid.ink CSS styles
        builder.AddStyles(MermaidStyles.ApplyThemeVariables(MermaidStyles.FlowchartStyles, Theme));

        // Render subgraphs behind edges and nodes.
        RenderSubgraphs(builder, model, options);

        // Render edges (behind nodes)
        foreach (var edge in model.Edges)
        {
#pragma warning disable IDE0007
            Node? sourceNode = model.GetNode(edge.SourceId);
            Node? targetNode = model.GetNode(edge.TargetId);
#pragma warning restore IDE0007
            RenderEdge(builder, model, edge, options, Theme, model.Direction, sourceNode, targetNode);
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

    static string WrapPlainTextToWidth(string text, double fontSizePx, double maxWidthPx)
    {
#pragma warning disable IDE0007
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        string Normalised = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] Paragraphs = Normalised.Split('\n');

        double maxChars = Math.Max(1.0, maxWidthPx / (fontSizePx * 0.62));
        int maxCharsPerLine = (int)Math.Floor(maxChars);

        List<string> Lines = new List<string>();
        for (int p = 0; p < Paragraphs.Length; p++)
        {
            string Paragraph = Paragraphs[p].Trim();
            if (Paragraph.Length == 0)
            {
                Lines.Add(string.Empty);
                continue;
            }

            string[] Words = Paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string Current = string.Empty;

            for (int w = 0; w < Words.Length; w++)
            {
                string Word = Words[w];
                if (Word.Length > maxCharsPerLine)
                {
                    // Flush current line before hard-breaking long word.
                    if (!string.IsNullOrEmpty(Current))
                    {
                        Lines.Add(Current);
                        Current = string.Empty;
                    }

                    int Index = 0;
                    while (Index < Word.Length)
                    {
                        int Take = Math.Min(maxCharsPerLine, Word.Length - Index);
                        Lines.Add(Word.Substring(Index, Take));
                        Index += Take;
                    }
                    continue;
                }

                if (string.IsNullOrEmpty(Current))
                {
                    Current = Word;
                    continue;
                }

                int ProposedLength = Current.Length + 1 + Word.Length;
                if (ProposedLength <= maxCharsPerLine)
                {
                    Current = Current + " " + Word;
                }
                else
                {
                    Lines.Add(Current);
                    Current = Word;
                }
            }

            if (!string.IsNullOrEmpty(Current))
            {
                Lines.Add(Current);
            }
        }

        return string.Join("\n", Lines);
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Measures wrapped edge labels before layout (Mermaid measures in <c>insertEdgeLabel</c> before <c>dagreLayout</c>).
    /// </summary>
    static void ApplyFlowchartEdgeLabelLayoutMetrics(FlowchartModel model, RenderOptions options)
    {
#pragma warning disable IDE0007
        double WrappingWidthPx = GetConfigDouble(model, "flowchart.wrappingWidth", FlowchartNodeLabelMaxWidthPx);

        foreach (Edge edge in model.Edges)
        {
            if (string.IsNullOrWhiteSpace(edge.Label))
            {
                edge.LabelLayoutWidthPx = 0;
                edge.LabelLayoutHeightPx = 0;
                continue;
            }

            Size LabelSize = PrepareFlowchartEdgeLabel(edge.Label, options.FontSize, WrappingWidthPx).MeasuredSize;
            edge.LabelLayoutWidthPx = LabelSize.Width + 16;
            edge.LabelLayoutHeightPx = Math.Max(24, LabelSize.Height + 8);
        }
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Wraps a flowchart edge label at <c>flowchart.wrappingWidth</c> and builds HTML for rendering (Mermaid <c>createText</c> default width 200px).
    /// </summary>
    static (string Html, Size MeasuredSize) PrepareFlowchartEdgeLabel(string Label, double FontSizePx, double WrappingWidthPx)
    {
#pragma warning disable IDE0007
        string Wrapped = WrapPlainTextToWidth(Label.Trim(), FontSizePx, WrappingWidthPx);
        Size Measured = MeasureText(Wrapped, FontSizePx);
        string Html = ConvertIconsToHtml(NormaliseHtmlLineBreaks(Wrapped));
        return (Html, Measured);
#pragma warning restore IDE0007
    }

    private static double GetConfigDouble(FlowchartModel model, string key, double fallback)
    {
        if (!model.Config.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return fallback;
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
        // Intended (Mermaid): rotate LR/RL subgraph members after layout.
        // Nested clusters are laid out as TD in DagreLayoutEngine; enabling this would double-transform.
        // if (subgraph.Direction == Direction.LeftToRight || subgraph.Direction == Direction.RightToLeft)
        // {
        //     RotateSubgraphLayout90Degrees(model, subgraph);
        // }

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
            // Mermaid centres the cluster label horizontally within the cluster.
#pragma warning disable IDE0007
            const double TITLE_BAND_PX = 26.0;
            double TitleX = Bounds.X + (Bounds.Width / 2.0);
            double TitleY = Bounds.Y + (TITLE_BAND_PX / 2.0) + 3.0;
#pragma warning restore IDE0007
            builder.AddText(
                TitleX,
                TitleY,
                Title,
                anchor: "middle",
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

        void IncludeRect(double left, double top, double right, double bottom)
        {
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

        Node? groupNode = model.GetNode(subgraph.Id);
        if (groupNode is not null && groupNode.IsGroup && groupNode.Width > 0 && groupNode.Height > 0)
        {
            IncludeRect(
                groupNode.Position.X - (groupNode.Width / 2.0),
                groupNode.Position.Y - (groupNode.Height / 2.0),
                groupNode.Position.X + (groupNode.Width / 2.0),
                groupNode.Position.Y + (groupNode.Height / 2.0));
        }

        foreach (string nodeId in CollectSubgraphMemberNodeIdsRecursive(subgraph))
        {
            Node? node = model.GetNode(nodeId);
            if (node == null)
            {
                continue;
            }

            IncludeRect(
                node.Position.X - (node.Width / 2.0),
                node.Position.Y - (node.Height / 2.0),
                node.Position.X + (node.Width / 2.0),
                node.Position.Y + (node.Height / 2.0));
        }

        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            MermaidSharp.Models.Rect nestedBounds = ComputeSubgraphBounds(model, nested);
            if (nestedBounds.Width > 0 && nestedBounds.Height > 0)
            {
                IncludeRect(nestedBounds.X, nestedBounds.Y, nestedBounds.X + nestedBounds.Width, nestedBounds.Y + nestedBounds.Height);
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

    static List<string> CollectSubgraphMemberNodeIdsRecursive(MermaidSharp.Models.Subgraph subgraph)
    {
#pragma warning disable IDE0007
        List<string> nodeIds = new List<string>();
        foreach (MermaidSharp.Models.Subgraph nested in subgraph.NestedSubgraphs)
        {
            nodeIds.AddRange(CollectSubgraphMemberNodeIdsRecursive(nested));
        }

        foreach (string nodeId in subgraph.NodeIds)
        {
            nodeIds.Add(nodeId);
        }

        return nodeIds;
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

    static void ApplySubgraphTitleMarginShiftToEdges(FlowchartModel model)
    {
#pragma warning disable IDE0007
        double TopMargin = GetConfigDouble(model, "flowchart.subGraphTitleMargin.top", 0.0);
        double BottomMargin = GetConfigDouble(model, "flowchart.subGraphTitleMargin.bottom", 0.0);
        double TotalMargin = TopMargin + BottomMargin;
        if (TotalMargin <= 0.0)
        {
            return;
        }

        double ShiftY = TotalMargin / 2.0;
        foreach (Edge edge in model.Edges)
        {
            if (edge.Points.Count == 0)
            {
                continue;
            }

            for (int i = 0; i < edge.Points.Count; i++)
            {
                Position p = edge.Points[i];
                edge.Points[i] = new Position(p.X, p.Y + ShiftY);
            }
        }
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Cylinder: Mermaid fill path, then stroke outline (top arcs 1→2, 2→1, sides, bottom).
    /// </summary>
    static void RenderCylinderNode(SvgBuilder builder, Node node, double x, double y, string fill, string stroke, double strokeWidth)
    {
#pragma warning disable IDE0007
        string FillPath = ShapePathGenerator.FlowchartCylinderMermaidFillPath(x, y, node.Width, node.Height);
        string OutlinePath = ShapePathGenerator.FlowchartCylinderOutlinePath(x, y, node.Width, node.Height);
        builder.AddPath(FillPath, fill: fill, stroke: "none");
        builder.AddPath(OutlinePath, fill: "none", stroke: stroke, strokeWidth: strokeWidth, cssClass: "node",
            strokeLinejoin: "miter", strokeMiterlimit: 16);
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Asymmetric flag: fill the body as one path, then draw each visible edge as an explicit stroke.
    /// This avoids Word leaving the two left corners open when stroking the concave outline as one path.
    /// </summary>
    static void RenderAsymmetricNode(SvgBuilder builder, Node node, double x, double y, string fill, string stroke, double strokeWidth)
    {
#pragma warning disable IDE0007
        double HalfHeight = node.Height / 2.0;
        double LeftX = x - HalfHeight;
        double RightX = x + node.Width;
        double TopY = y;
        double BottomY = y + node.Height;
        double NotchX = x;
        double NotchY = y + HalfHeight;
        double CornerPatchRadius = Math.Max(0.75, strokeWidth * 0.9);
        string FillPath = ShapePathGenerator.GetPath(node.Shape, x, y, node.Width, node.Height);
#pragma warning restore IDE0007
        builder.AddPath(FillPath, fill: fill, stroke: "none");
        builder.AddLine(LeftX, TopY, RightX, TopY, stroke: stroke, strokeWidth: strokeWidth, strokeLinecap: "square");
        builder.AddLine(RightX, TopY, RightX, BottomY, stroke: stroke, strokeWidth: strokeWidth, strokeLinecap: "square");
        builder.AddLine(RightX, BottomY, LeftX, BottomY, stroke: stroke, strokeWidth: strokeWidth, strokeLinecap: "square");
        builder.AddLine(LeftX, BottomY, NotchX, NotchY, stroke: stroke, strokeWidth: strokeWidth, strokeLinecap: "square");
        builder.AddLine(NotchX, NotchY, LeftX, TopY, stroke: stroke, strokeWidth: strokeWidth, strokeLinecap: "square");
        var TopCornerPatch =
            string.Create(
                CultureInfo.InvariantCulture,
                $"M{LeftX - CornerPatchRadius:0.##},{TopY:0.##}L{LeftX:0.##},{TopY - CornerPatchRadius:0.##}L{LeftX + CornerPatchRadius:0.##},{TopY:0.##}L{LeftX:0.##},{TopY + CornerPatchRadius:0.##}Z");
        var BottomCornerPatch =
            string.Create(
                CultureInfo.InvariantCulture,
                $"M{LeftX - CornerPatchRadius:0.##},{BottomY:0.##}L{LeftX:0.##},{BottomY - CornerPatchRadius:0.##}L{LeftX + CornerPatchRadius:0.##},{BottomY:0.##}L{LeftX:0.##},{BottomY + CornerPatchRadius:0.##}Z");
        builder.AddPath(TopCornerPatch, fill: stroke, stroke: "none");
        builder.AddPath(BottomCornerPatch, fill: stroke, stroke: "none");
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
        string fill = node.Style.Fill ?? nodeFill;
        string stroke = node.Style.Stroke ?? nodeStroke;
        double strokeWidth = node.Style.StrokeWidth ?? 1.0;
#pragma warning restore IDE0007

        if (node.Shape == NodeShape.Cylinder)
        {
            RenderCylinderNode(builder, node, x, y, fill, stroke, strokeWidth);
        }
        else if (node.Shape == NodeShape.Asymmetric)
        {
            RenderAsymmetricNode(builder, node, x, y, fill, stroke, strokeWidth);
        }
        else
        {
#pragma warning disable IDE0007
            string ShapePath = ShapePathGenerator.GetPath(node.Shape, x, y, node.Width, node.Height);
#pragma warning restore IDE0007
            builder.AddPath(ShapePath,
                fill: fill,
                stroke: stroke,
                strokeWidth: strokeWidth,
                cssClass: "node",
                strokeLinejoin: "miter",
                strokeMiterlimit: 16);
        }

        // Render label with icon support
#pragma warning disable IDE0007
        var label = node.Label ?? node.Id;
        var htmlLabel = ConvertIconsToHtml(NormaliseHtmlLineBreaks(label));
#pragma warning restore IDE0007

#pragma warning disable IDE0007
        double LabelExtraOffsetPx = FlowchartNodeLabelExtraVerticalOffsetPx;
        if (node.Shape == NodeShape.Cylinder)
        {
            LabelExtraOffsetPx += ShapePathGenerator.FlowchartCylinderEllipseRadiusY(node.Width);
        }
#pragma warning restore IDE0007

        builder.AddForeignObject(
            x, y, node.Width, node.Height,
            htmlLabel,
            className: "nodeLabel",
            fontSizePx: options.FontSize,
            opticalVerticalBiasEm: flowchartLabelOpticalBiasEm,
            extraVerticalOffsetPx: LabelExtraOffsetPx,
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

    static void RenderEdge(SvgBuilder builder, FlowchartModel model, Edge edge, RenderOptions options, MermaidSharp.Rendering.MermaidThemeVariables theme, Direction diagramDirection, Node? sourceNode, Node? targetNode)
    {
        if (edge.Points.Count < 2)
        {
            return;
        }

        IReadOnlyList<Position> pointsForPath = edge.Points;
#pragma warning disable IDE0007
        string edgeCurveType = GetConfigString(model, "flowchart.curve", "basis");
        string pathData = BuildFlowchartEdgePath(pointsForPath, edge, edgeCurveType, model, sourceNode, targetNode, diagramDirection);

        bool useNeoLook = string.Equals(GetConfigString(model, "look", string.Empty), "neo", StringComparison.OrdinalIgnoreCase);
#pragma warning restore IDE0007

#pragma warning disable IDE0007
        string? strokeDasharray = MermaidSharp.Rendering.MermaidEdgeStroke.ResolveStrokeDasharray(edge.Style, edge.LineStyle);
        double strokeWidth = MermaidSharp.Rendering.MermaidEdgeStroke.ResolveStrokeWidth(edge.Style, edge.LineStyle, theme.StrokeWidth);
        string stroke = edge.Style.Stroke ?? edgeStroke;
#pragma warning restore IDE0007

        if (useNeoLook)
        {
#pragma warning disable IDE0007
            strokeDasharray = ComputeNeoStrokeDasharray(pointsForPath, edge, edgeCurveType);
#pragma warning restore IDE0007
        }

        var markerEnd = edge.Type switch
        {
            EdgeType.CircleEnd => useNeoLook ? "url(#mermaid-svg_flowchart-v2-circleEnd-margin)" : "url(#mermaid-svg_flowchart-v2-circleEnd)",
            EdgeType.CrossEnd => useNeoLook ? "url(#mermaid-svg_flowchart-v2-crossEnd-margin)" : "url(#mermaid-svg_flowchart-v2-crossEnd)",
            EdgeType.BiDirectionalCircle => useNeoLook ? "url(#mermaid-svg_flowchart-v2-circleEnd-margin)" : "url(#mermaid-svg_flowchart-v2-circleEnd)",
            EdgeType.BiDirectionalCross => useNeoLook ? "url(#mermaid-svg_flowchart-v2-crossEnd-margin)" : "url(#mermaid-svg_flowchart-v2-crossEnd)",
            _ => edge.HasArrowHead
                ? edge.LineStyle == EdgeStyle.Thick
                    ? "url(#mermaid-svg_flowchart-v2-pointEndThick)"
                    : useNeoLook ? "url(#mermaid-svg_flowchart-v2-pointEnd-margin)" : "url(#mermaid-svg_flowchart-v2-pointEnd)"
                : null
        };

        var markerStart = edge.Type switch
        {
            EdgeType.BiDirectionalCircle => useNeoLook ? "url(#mermaid-svg_flowchart-v2-circleStart-margin)" : "url(#mermaid-svg_flowchart-v2-circleStart)",
            EdgeType.BiDirectionalCross => useNeoLook ? "url(#mermaid-svg_flowchart-v2-crossStart-margin)" : "url(#mermaid-svg_flowchart-v2-crossStart)",
            EdgeType.BiDirectional => useNeoLook ? "url(#mermaid-svg_flowchart-v2-pointStart-margin)" : "url(#mermaid-svg_flowchart-v2-pointStartBiDir)",
            _ => edge.HasArrowTail ? (useNeoLook ? "url(#mermaid-svg_flowchart-v2-pointStart-margin)" : "url(#mermaid-svg_flowchart-v2-pointStart)") : null
        };

        if (edge.Type is EdgeType.CrossEnd or EdgeType.BiDirectionalCross)
        {
            var StartGapPx = edge.Type == EdgeType.BiDirectionalCross ? CrossStrokeGapPx : 0.0;
            var TrimmedPoints = TrimPointsForCrossVisibleStroke(pointsForPath, StartGapPx, CrossStrokeGapPx);
            var VisiblePathData = BuildFlowchartEdgePath(TrimmedPoints, edge, edgeCurveType, model, sourceNode, targetNode, diagramDirection);

            builder.AddPath(
                VisiblePathData,
                fill: "none",
                stroke: stroke,
                strokeWidth: strokeWidth,
                strokeDasharray: strokeDasharray,
                cssClass: "flowchart-link");

            builder.AddPath(
                pathData,
                fill: "none",
                stroke: "none",
                strokeWidth: 0,
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
#pragma warning disable IDE0007
            Node? TargetNode = model.GetNode(edge.TargetId);
            Position LabelAnchor = ResolveFlowchartEdgeLabelPosition(edge, sourceNode, TargetNode, model, diagramDirection);
#pragma warning restore IDE0007
            var labelX = LabelAnchor.X;
            var labelY = LabelAnchor.Y;
#pragma warning disable IDE0007
            double WrappingWidthPx = GetConfigDouble(model, "flowchart.wrappingWidth", FlowchartNodeLabelMaxWidthPx);
            (string HtmlLabel, Size LabelSize) = PrepareFlowchartEdgeLabel(edge.Label, options.FontSize, WrappingWidthPx);
            double LabelWidth = LabelSize.Width + 16;
            double LabelHeight = Math.Max(24, LabelSize.Height + 8);
#pragma warning restore IDE0007
            // Word parity: keep edge labels with transparent background (no filled rect).
            builder.AddRect(
                labelX - LabelWidth / 2, labelY - LabelHeight / 2,
                LabelWidth, LabelHeight,
                fill: "none", stroke: "none",
                cssClass: "edgeLabel");

            builder.AddForeignObject(
                labelX - LabelWidth / 2, labelY - LabelHeight / 2,
                LabelWidth, LabelHeight,
                HtmlLabel,
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
#pragma warning disable IDE0007
                Node? SourceNode = model.GetNode(edge.SourceId);
                Node? TargetNode = model.GetNode(edge.TargetId);
                Position LabelAnchor = ResolveFlowchartEdgeLabelPosition(edge, SourceNode, TargetNode, model, model.Direction);
                double WrappingWidthPx = GetConfigDouble(model, "flowchart.wrappingWidth", FlowchartNodeLabelMaxWidthPx);
                Size LabelSize = PrepareFlowchartEdgeLabel(edge.Label, options.FontSize, WrappingWidthPx).MeasuredSize;
                double LabelWidth = LabelSize.Width + 16;
                double LabelHeight = Math.Max(24, LabelSize.Height + 8);
                double left = LabelAnchor.X - (LabelWidth / 2.0);
                double top = LabelAnchor.Y - (LabelHeight / 2.0);
                double right = LabelAnchor.X + (LabelWidth / 2.0);
                double bottom = LabelAnchor.Y + (LabelHeight / 2.0);
#pragma warning restore IDE0007
                AddBounds(left, top, right, bottom);
            }

#pragma warning disable IDE0007
            foreach (Position point in edge.Points)
            {
                AddBounds(point.X, point.Y, point.X, point.Y);
            }
#pragma warning restore IDE0007
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
    /// Uses layout-snapped polyline points (orthogonal or curveBasis) instead of rebuilding from endpoints only.
    /// </summary>
    static string? TryBuildFlowchartPathFromSnapLineData(
        IReadOnlyList<Position> lineData,
        string? arrowTypeStart,
        string? arrowTypeEnd)
    {
#pragma warning disable IDE0007
        if (lineData.Count < 2)
        {
            return null;
        }

        List<Position> OffsetPoints = ApplyLineWithOffset(lineData, arrowTypeStart, arrowTypeEnd);
        if (OffsetPoints.Count >= 3 && ShouldUseLinearFlowchartPath(OffsetPoints))
        {
            return BuildFlowchartLinearPath(OffsetPoints);
        }

        if (OffsetPoints.Count >= 3)
        {
            return BuildFlowchartCurveBasisPath(OffsetPoints);
        }

        if (OffsetPoints.Count == 2)
        {
            return BuildFlowchartGuidedCurveBasisPath(OffsetPoints[0], OffsetPoints[1]);
        }

        return null;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Mermaid uses <c>d3.line().curve(curveBasis)</c> for flowchart edges (dagre-d3).
    /// We emit the same curveBasis path from the routed polyline points.
    /// </summary>
#pragma warning disable IDE0022
#pragma warning disable IDE0007
    static string BuildFlowchartEdgePath(
        IReadOnlyList<Position> points,
        Edge edge,
        string edgeCurveType,
        FlowchartModel? model,
        Node? sourceNode,
        Node? targetNode,
        Direction diagramDirection)
    {
        // Mermaid flowcharts use:
        // - corner pre-rounding (fixCorners) for non-rounded curves
        // - d3.line().curve(curveBasis) by default
        // - marker-aware endpoint shortening via lineWithOffset (markerOffsets table)
        //
        // Source: packages/mermaid/src/rendering-util/rendering-elements/edges.js
        //        packages/mermaid/src/utils/lineWithOffset.ts
        List<Position> LineData = FilterValidPoints(points);

        (string? ArrowTypeStart, string? ArrowTypeEnd) = GetMermaidArrowTypes(edge.Type);
        string NormalisedCurveType = string.IsNullOrWhiteSpace(edgeCurveType) ? "basis" : edgeCurveType.Trim();

        // Mermaid special-case: "rounded" bypasses D3 curves, applies explicit endpoint shifting,
        // then generates a quadratic-rounded polyline path.
        if (string.Equals(NormalisedCurveType, "rounded", StringComparison.OrdinalIgnoreCase))
        {
            List<Position> Shifted = ApplyMarkerOffsetsToPoints(LineData, ArrowTypeStart, ArrowTypeEnd);
            return GenerateRoundedPath(Shifted, radius: 5.0);
        }

        if (model is not null
            && sourceNode is not null
            && targetNode is not null
            && (diagramDirection == Direction.TopToBottom || diagramDirection == Direction.BottomToTop)
            && string.IsNullOrWhiteSpace(edge.Label)
            && MermaidSharp.Layout.CoordinateAssignment.IsFlowchartTbStadiumDualFanInMergeTarget(model, targetNode.Id)
            && LineData.Count >= 2)
        {
            List<Position> OffsetLineData = ApplyLineWithOffset(LineData, ArrowTypeStart, ArrowTypeEnd);
            OffsetLineData = MermaidSharp.Layout.CoordinateAssignment.ApplyFlowchartOrthogonalAttachStubs(
                OffsetLineData,
                sourceNode,
                targetNode,
                MermaidSharp.Layout.CoordinateAssignment.FLOWCHART_ORTHOGONAL_ATTACH_STUB_PX);
            return BuildFlowchartTbStadiumMergeCurveBasisPath(
                OffsetLineData,
                useTerminalCollectorMarginCurve: false,
                useStadiumMergeHook: true);
        }

        // Decision branches (diamond → labelled targets): use the same guided 3-point curveBasis route as ER /
        // Mermaid dagre (down/right from source, then smooth bend to target). Avoids fixCorners + multi-elbow
        // wobble and the old unused cubic-bezier shortcut.
        if (ShouldUseTbMultiIncomingDiamondGuidedCurve(edge, targetNode, model, diagramDirection, LineData)
            && LineData.Count >= 2
            && targetNode is not null
            && model is not null
            && MermaidSharp.Layout.CoordinateAssignment.TryGetFlowchartVerticalDecisionMultiIncomingEndpoints(
                model,
                edge,
                out Position MultiIncomingSource,
                out Position MultiIncomingOutline,
                out Position MultiIncomingGuide,
                out MermaidSharp.Layout.FlowchartDecisionBranchExitRole _))
        {
            List<Position> GuidedPoints = BuildFlowchartTbMultiIncomingDiamondGuidedPoints(
                MultiIncomingSource,
                MultiIncomingGuide,
                MultiIncomingOutline,
                ArrowTypeStart,
                ArrowTypeEnd);
            return BuildFlowchartCurveBasisPath(GuidedPoints);
        }

        if (model is not null
            && sourceNode is not null
            && targetNode is not null
            && MermaidSharp.Layout.CoordinateAssignment.IsFlowchartLrRectangleToDiamondIngressEdge(model, sourceNode, targetNode)
            && MermaidSharp.Layout.CoordinateAssignment.TryGetFlowchartLrRectangleToDiamondIngressEndpoints(
                model,
                sourceNode,
                targetNode,
                out Position IngressSource,
                out Position IngressTarget))
        {
            List<Position> OffsetIngressEndpoints = ApplyLineWithOffset(
                new List<Position> { IngressSource, IngressTarget },
                ArrowTypeStart,
                ArrowTypeEnd);
            if (OffsetIngressEndpoints.Count >= 2)
            {
                return BuildFlowchartLrRectangleToDiamondIngressCurveBasisPath(
                    OffsetIngressEndpoints[0],
                    OffsetIngressEndpoints[^1]);
            }
        }

        if (model is not null
            && sourceNode is not null
            && targetNode is not null
            && MermaidSharp.Layout.CoordinateAssignment.IsFlowchartLrRetryLoopEdge(model, edge, sourceNode, targetNode)
            && LineData.Count >= 2)
        {
            List<Position> OffsetLoopPoints = ApplyLineWithOffset(LineData, ArrowTypeStart, ArrowTypeEnd);
            if (OffsetLoopPoints.Count >= 2)
            {
                return BuildFlowchartCurveBasisPath(OffsetLoopPoints);
            }
        }

        if (model is not null
            && sourceNode is not null
            && targetNode is not null
            && MermaidSharp.Layout.CoordinateAssignment.IsFlowchartTbUpwardLoopEdge(model, edge, sourceNode, targetNode)
            && LineData.Count >= 2)
        {
            return BuildFlowchartTbUpwardLoopCurveBasisPath(LineData);
        }

        if (ShouldUseDecisionBranchGuidedCurve(edge, sourceNode, model, LineData) && LineData.Count >= 2)
        {
            List<Position> Endpoints = new List<Position> { LineData[0], LineData[^1] };
            if (model is not null
                && sourceNode is not null
                && MermaidSharp.Layout.CoordinateAssignment.TryGetFlowchartHorizontalDecisionBranchEndpoints(
                    model,
                    edge,
                    out Position horizontalSource,
                    out Position horizontalTarget,
                    out MermaidSharp.Layout.FlowchartDecisionBranchExitRole _))
            {
                Endpoints[0] = horizontalSource;
                Endpoints[1] = horizontalTarget;

                List<Position> OffsetHorizontalEndpoints = ApplyLineWithOffset(Endpoints, ArrowTypeStart, ArrowTypeEnd);
                if (OffsetHorizontalEndpoints.Count >= 2)
                {
                    return BuildFlowchartHorizontalDecisionBranchOutwardCurveBasisPath(
                        OffsetHorizontalEndpoints[0],
                        OffsetHorizontalEndpoints[^1],
                        sourceNode.Position.Y);
                }
            }
            else if (model is not null
                && targetNode is not null
                && MermaidSharp.Layout.CoordinateAssignment.TryGetFlowchartVerticalDecisionBranchEndpoints(
                    model,
                    edge,
                    out Position branchSource,
                    out Position branchTarget,
                    out MermaidSharp.Layout.FlowchartDecisionBranchExitRole _))
            {
                Endpoints[0] = branchSource;
                Endpoints[1] = branchTarget;
            }

            List<Position> OffsetEndpoints = ApplyLineWithOffset(Endpoints, ArrowTypeStart, ArrowTypeEnd);
            if (OffsetEndpoints.Count >= 2 && sourceNode is not null)
            {
                return BuildFlowchartVerticalDecisionBranchOutwardCurveBasisPath(
                    OffsetEndpoints[0],
                    OffsetEndpoints[^1],
                    sourceNode.Position.X);
            }
        }

        if (ShouldUseLrSideFanoutGuidedCurve(edge, sourceNode, model, diagramDirection, LineData)
            && LineData.Count >= 2)
        {
            List<Position> OffsetFanEndpoints = ApplyLineWithOffset(
                new List<Position> { LineData[0], LineData[^1] },
                ArrowTypeStart,
                ArrowTypeEnd);
            if (OffsetFanEndpoints.Count >= 2)
            {
                return BuildFlowchartLrSideFanSRouteCurveBasisPath(
                    OffsetFanEndpoints[0],
                    OffsetFanEndpoints[^1]);
            }
        }

        if (ShouldUseLrSideFanInGuidedCurve(edge, targetNode, model, diagramDirection, LineData)
            && LineData.Count >= 2)
        {
            List<Position> OffsetFanEndpoints = ApplyLineWithOffset(
                new List<Position> { LineData[0], LineData[^1] },
                ArrowTypeStart,
                ArrowTypeEnd);
            if (OffsetFanEndpoints.Count >= 2)
            {
                return BuildFlowchartLrSideFanSRouteCurveBasisPath(
                    OffsetFanEndpoints[0],
                    OffsetFanEndpoints[^1]);
            }
        }

        if (ShouldUseTbStadiumMergeFanInGuidedCurve(edge, targetNode, model, diagramDirection, LineData)
            && sourceNode is not null
            && targetNode is not null
            && LineData.Count >= 2)
        {
            List<Position> OffsetLineData = ApplyLineWithOffset(LineData, ArrowTypeStart, ArrowTypeEnd);
            OffsetLineData = MermaidSharp.Layout.CoordinateAssignment.ApplyFlowchartOrthogonalAttachStubs(
                OffsetLineData,
                sourceNode,
                targetNode,
                MermaidSharp.Layout.CoordinateAssignment.FLOWCHART_ORTHOGONAL_ATTACH_STUB_PX);
            return BuildFlowchartTbStadiumMergeCurveBasisPath(
                OffsetLineData,
                useTerminalCollectorMarginCurve: false,
                useStadiumMergeHook: true);
        }

        if (ShouldUseTbTerminalCollectorGuidedCurve(edge, targetNode, model, diagramDirection, LineData)
            && targetNode is not null
            && sourceNode is not null
            && LineData.Count >= 2)
        {
            List<Position> OffsetLineData = ApplyLineWithOffset(LineData, ArrowTypeStart, ArrowTypeEnd);
            bool UseMarginCurve = model is not null
                && MermaidSharp.Layout.CoordinateAssignment.IsFlowchartTbTerminalCollectorMarginEdge(model, edge);
            return BuildFlowchartTbStadiumMergeCurveBasisPath(OffsetLineData, UseMarginCurve);
        }

        if (ShouldUseHubFanGuidedCurve(edge, sourceNode, targetNode, model, diagramDirection, LineData)
            && LineData.Count >= 2)
        {
            List<Position> OffsetFanEndpoints = ApplyLineWithOffset(
                new List<Position> { LineData[0], LineData[^1] },
                ArrowTypeStart,
                ArrowTypeEnd);
            if (OffsetFanEndpoints.Count >= 2)
            {
                double HubCentreX = ResolveFlowchartHubFanCentreX(edge, sourceNode, targetNode, model);
                bool IsFanIn = targetNode is not null
                    && string.Equals(edge.TargetId, targetNode.Id, StringComparison.Ordinal);
                if (IsFanIn)
                {
                    return BuildFlowchartVerticalFanSRouteCurveBasisPath(
                        OffsetFanEndpoints[0],
                        OffsetFanEndpoints[^1]);
                }

                return BuildFlowchartVerticalDecisionBranchOutwardCurveBasisPath(
                    OffsetFanEndpoints[0],
                    OffsetFanEndpoints[^1],
                    HubCentreX);
            }
        }

        if (ShouldUseFlowchartCrossBoundarySCurve(edge, sourceNode, targetNode, model, LineData)
            && LineData.Count >= 2)
        {
            List<Position> OffsetBoundaryPoints = ApplyLineWithOffset(LineData, ArrowTypeStart, ArrowTypeEnd);
            if (OffsetBoundaryPoints.Count >= 3)
            {
                return BuildFlowchartCrossBoundaryGuidedCurveBasisPath(OffsetBoundaryPoints);
            }

            if (OffsetBoundaryPoints.Count >= 2)
            {
                return BuildFlowchartGuidedCurveBasisPath(OffsetBoundaryPoints[0], OffsetBoundaryPoints[^1]);
            }
        }

        List<Position> LineDataForRender = LineData;
        if (model is not null
            && sourceNode is not null
            && targetNode is not null
            && !MermaidSharp.Layout.CoordinateAssignment.ShouldSkipFlowchartOrthogonalAttachStubs(
                model,
                edge,
                sourceNode,
                targetNode))
        {
            LineDataForRender = MermaidSharp.Layout.CoordinateAssignment.ApplyFlowchartOrthogonalAttachStubs(
                LineData,
                sourceNode,
                targetNode,
                MermaidSharp.Layout.CoordinateAssignment.FLOWCHART_ORTHOGONAL_ATTACH_STUB_PX);
        }

        List<Position> OffsetBeforeCorners = ApplyLineWithOffset(LineDataForRender, ArrowTypeStart, ArrowTypeEnd);
        if (OffsetBeforeCorners.Count > 2 && ShouldUseLinearFlowchartPath(OffsetBeforeCorners))
        {
            return BuildFlowchartLinearPath(OffsetBeforeCorners);
        }

        // Mermaid pre-rounds corners for all non-rounded curves.
        LineDataForRender = FixCorners(LineDataForRender);
        List<Position> OffsetPoints = ApplyLineWithOffset(LineDataForRender, ArrowTypeStart, ArrowTypeEnd);

        if (OffsetPoints.Count == 2)
        {
            Position Start = OffsetPoints[0];
            Position End = OffsetPoints[^1];
            if (Math.Abs(Start.X - End.X) < 0.75 && Math.Abs(Start.Y - End.Y) > 0.75)
            {
                return BuildFlowchartLinearPath(OffsetPoints);
            }

            if (Math.Abs(Start.Y - End.Y) < 0.75 && Math.Abs(Start.X - End.X) > 0.75)
            {
                return BuildFlowchartLinearPath(OffsetPoints);
            }

            if (Math.Abs(Start.X - End.X) < 0.75 || Math.Abs(Start.Y - End.Y) < 0.75)
            {
                return BuildFlowchartGuidedCurveBasisPath(Start, End);
            }

            return BuildFlowchartLinearPath(OffsetPoints);
        }

        if (ShouldUseLinearFlowchartPath(OffsetPoints))
        {
            return BuildFlowchartLinearPath(OffsetPoints);
        }

        // Naiad currently implements the default Mermaid curveBasis path.
        // For other curve strings (linear/cardinal/step/etc.), Mermaid changes the curve factory,
        // but our renderer intentionally falls back to basis unless/until those are ported.
        return BuildFlowchartCurveBasisPath(OffsetPoints);
    }

    /// <summary>
    /// Mermaid ER / dagre-style smooth link: three waypoints (start, vertical guide, end) then curveBasis.
    /// Source: <c>ERRenderer.BuildErGuidedCurveBasisPath</c> and dagre elbow routes in Mermaid.
    /// </summary>
    static string BuildFlowchartGuidedCurveBasisPath(Position P0, Position P3)
    {
        double Dx = P3.X - P0.X;
        double Dy = P3.Y - P0.Y;

        List<Position> GuidedPoints;
        if (Math.Abs(Dx) < 0.75)
        {
            double MidY = (P0.Y + P3.Y) / 2.0;
            GuidedPoints = new List<Position> { P0, new Position(P0.X, MidY), P3 };
        }
        else
        {
            double MidY = (P0.Y + P3.Y) / 2.0;
            GuidedPoints = new List<Position> { P0, new Position(P0.X, MidY), P3 };
        }

        return BuildFlowchartCurveBasisPath(GuidedPoints);
    }

    /// <summary>
    /// F34 Receive → diamond: travel horizontally away from Receive, then curve toward the left facet (not vertical-first).
    /// </summary>
    static string BuildFlowchartLrRectangleToDiamondIngressCurveBasisPath(Position P0, Position P3)
    {
#pragma warning disable IDE0007
        double DeltaX = P3.X - P0.X;
        double DeltaY = P3.Y - P0.Y;
        if (Math.Abs(DeltaX) < 0.75)
        {
            double MidY = (P0.Y + P3.Y) / 2.0;
            return BuildFlowchartCurveBasisPath(new List<Position>
            {
                P0,
                new Position(P0.X, MidY),
                P3
            });
        }

        double MidX = (P0.X + P3.X) / 2.0;
        if (Math.Abs(DeltaY) < 0.75)
        {
            return BuildFlowchartCurveBasisPath(new List<Position>
            {
                P0,
                new Position(MidX, P0.Y),
                P3
            });
        }

        return BuildFlowchartCurveBasisPath(new List<Position>
        {
            P0,
            new Position(MidX, P0.Y),
            P3
        });
#pragma warning restore IDE0007
    }

    /// <summary>Short axis-aligned stub so curveBasis tangents meet faces at ~90° (F37/F38 cross-boundary).</summary>
    const double FLOWCHART_CROSS_BOUNDARY_ORTHOGONAL_STUB_PX = 14.0;

    /// <summary>F35: short approach stub before diamond perimeter so curveBasis + arrow meet the outline.</summary>
    const double FLOWCHART_DIAMOND_INCOMING_ENTRY_STUB_PX = 14.0;

    const double FLOWCHART_ARROW_POINT_MARKER_PX = 4.0;

    /// <summary>F20 thick arrow (<c>==&gt;</c>): stroke stops 3px further before <c>pointEndThick</c> than normal arrows.</summary>
    const double FLOWCHART_THICK_ARROW_POINT_MARKER_PX = 7.0;

    static List<Position> BuildFlowchartVerticalFanSRouteGuidedPoints(Position P0, Position P3)
    {
#pragma warning disable IDE0007
        double MidY = (P0.Y + P3.Y) / 2.0;
        if (Math.Abs(P3.X - P0.X) < 0.75)
        {
            return new List<Position>
            {
                P0,
                new Position(P0.X, MidY),
                P3
            };
        }

        return new List<Position>
        {
            P0,
            new Position(P0.X, MidY),
            new Position(P3.X, MidY),
            P3
        };
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F31/F32 TB/BT fan (same curved S-route as F33): <c>(P0, (P0.X, midY), (P3.X, midY), P3)</c> then curveBasis.
    /// </summary>
    static string BuildFlowchartVerticalFanSRouteCurveBasisPath(Position P0, Position P3)
    {
        return BuildFlowchartCurveBasisPath(BuildFlowchartVerticalFanSRouteGuidedPoints(P0, P3));
    }

    /// <summary>
    /// F38/F43 TB cross-boundary: orthogonal exit/entry (vertical legs at source/target X) plus outward bow at <paramref name="GuideX"/>.
    /// </summary>
    static string BuildFlowchartVerticalCrossBoundaryOrthogonalCurveBasisPath(Position P0, Position P3, double GuideX)
    {
        return BuildFlowchartCurveBasisPath(BuildFlowchartVerticalCrossBoundaryOrthogonalGuidedPoints(P0, P3, GuideX));
    }

    static List<Position> BuildFlowchartVerticalCrossBoundaryOrthogonalGuidedPoints(Position P0, Position P3, double GuideX)
    {
#pragma warning disable IDE0007
        double MidY = (P0.Y + P3.Y) / 2.0;
        double FlowSignY = Math.Sign(P3.Y - P0.Y);
        if (Math.Abs(FlowSignY) < 0.01)
        {
            FlowSignY = 1.0;
        }

        if (Math.Abs(P3.X - P0.X) < 0.75)
        {
            double ExitStubY = P0.Y + (FlowSignY * FLOWCHART_CROSS_BOUNDARY_ORTHOGONAL_STUB_PX);
            double EntryStubY = P3.Y - (FlowSignY * FLOWCHART_CROSS_BOUNDARY_ORTHOGONAL_STUB_PX);
            return new List<Position>
            {
                P0,
                new Position(P0.X, ExitStubY),
                new Position(P0.X, MidY),
                new Position(P3.X, MidY),
                new Position(P3.X, EntryStubY),
                P3
            };
        }

        double CorridorExitY = P0.Y + (FlowSignY * FLOWCHART_CROSS_BOUNDARY_ORTHOGONAL_STUB_PX);
        double CorridorEntryY = P3.Y - (FlowSignY * FLOWCHART_CROSS_BOUNDARY_ORTHOGONAL_STUB_PX);
        return new List<Position>
        {
            P0,
            new Position(P0.X, CorridorExitY),
            new Position(P0.X, MidY),
            new Position(GuideX, MidY),
            new Position(P3.X, MidY),
            new Position(P3.X, CorridorEntryY),
            P3
        };
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F37/F40 LR cross-boundary: horizontal exit/entry with outward vertical leg at <paramref name="GuideX"/>.
    /// </summary>
    static string BuildFlowchartLrCrossBoundaryOrthogonalCurveBasisPath(Position P0, Position P3, double GuideX)
    {
        return BuildFlowchartCurveBasisPath(BuildFlowchartLrCrossBoundaryOrthogonalGuidedPoints(P0, P3, GuideX));
    }

    static List<Position> BuildFlowchartLrCrossBoundaryOrthogonalGuidedPoints(Position P0, Position P3, double GuideX)
    {
#pragma warning disable IDE0007
        double FlowSignX = Math.Sign(P3.X - P0.X);
        if (Math.Abs(FlowSignX) < 0.01)
        {
            FlowSignX = 1.0;
        }

        double ExitStubX = P0.X + (FlowSignX * FLOWCHART_CROSS_BOUNDARY_ORTHOGONAL_STUB_PX);
        double EntryStubX = P3.X - (FlowSignX * FLOWCHART_CROSS_BOUNDARY_ORTHOGONAL_STUB_PX);

        if (Math.Abs(P3.Y - P0.Y) < 0.75)
        {
            double MidX = (P0.X + P3.X) / 2.0;
            return new List<Position>
            {
                P0,
                new Position(ExitStubX, P0.Y),
                new Position(MidX, P0.Y),
                P3
            };
        }

        return new List<Position>
        {
            P0,
            new Position(ExitStubX, P0.Y),
            new Position(GuideX, P0.Y),
            new Position(GuideX, P3.Y),
            new Position(EntryStubX, P3.Y),
            P3
        };
#pragma warning restore IDE0007
    }

    static double ResolveFlowchartLrCrossBoundaryGuideX(Position P0, Position P3, Node sourceNode, Direction diagramDirection)
    {
#pragma warning disable IDE0007
        double MidX = (P0.X + P3.X) / 2.0;
        bool RightToLeft = diagramDirection == Direction.RightToLeft;
        bool ExternalSource = string.IsNullOrEmpty(sourceNode.ParentId);
        double Span = Math.Abs(MidX - P0.X);
        double OutwardShift = Math.Min(FLOWCHART_CROSS_BOUNDARY_ORTHOGONAL_STUB_PX, Span * 0.45);
        if (ExternalSource)
        {
            if (RightToLeft)
            {
                return P0.X - OutwardShift;
            }

            return P0.X + OutwardShift;
        }

        if (RightToLeft)
        {
            return P0.X - Math.Min(FLOWCHART_CROSS_BOUNDARY_ORTHOGONAL_STUB_PX, Span * 0.25);
        }

        return P0.X + Math.Min(FLOWCHART_CROSS_BOUNDARY_ORTHOGONAL_STUB_PX, Span * 0.25);
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F33/F24-style LR side fan: S-route <c>(P0, (midX, P0.Y), (midX, P3.Y), P3)</c> then curveBasis.
    /// </summary>
    static string BuildFlowchartLrSideFanSRouteCurveBasisPath(Position P0, Position P3)
    {
        return BuildFlowchartCurveBasisPath(BuildFlowchartLrSideFanSRouteGuidedPoints(P0, P3));
    }

    static List<Position> BuildFlowchartLrSideFanSRouteGuidedPoints(Position P0, Position P3)
    {
#pragma warning disable IDE0007
        double MidX = (P0.X + P3.X) / 2.0;
        if (Math.Abs(P3.Y - P0.Y) < 0.75)
        {
            return new List<Position>
            {
                P0,
                new Position(MidX, P0.Y),
                P3
            };
        }

        return new List<Position>
        {
            P0,
            new Position(MidX, P0.Y),
            new Position(MidX, P3.Y),
            P3
        };
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F24/F33 LR side fan (legacy spread guide): prefer <see cref="BuildFlowchartLrSideFanSRouteCurveBasisPath"/>.
    /// </summary>
    static string BuildFlowchartLrSideOutwardCurveBasisPath(
        Position P0,
        Position P3,
        double HubCentreY,
        FlowchartOutwardCurveSpreadAnchor spreadAnchor)
    {
        return BuildFlowchartCurveBasisPath(
            BuildFlowchartLrSideOutwardCurveGuidedPoints(P0, P3, HubCentreY, spreadAnchor));
    }

    static List<Position> BuildFlowchartLrSideOutwardCurveGuidedPoints(
        Position P0,
        Position P3,
        double HubCentreY,
        FlowchartOutwardCurveSpreadAnchor spreadAnchor)
    {
#pragma warning disable IDE0007
        double MidX = (P0.X + P3.X) / 2.0;
        if (Math.Abs(P3.Y - P0.Y) < 0.75)
        {
            return new List<Position>
            {
                P0,
                new Position(MidX, P0.Y),
                P3
            };
        }

        double Spread;
        double GuideY;
        if (spreadAnchor == FlowchartOutwardCurveSpreadAnchor.SourceLeg)
        {
            Spread = Math.Abs(P0.Y - HubCentreY);
            if (Math.Abs(P0.Y - HubCentreY) < 0.75)
            {
                GuideY = HubCentreY;
            }
            else if (P0.Y < HubCentreY)
            {
                GuideY = P0.Y - Spread;
            }
            else
            {
                GuideY = P0.Y + Spread;
            }
        }
        else
        {
            Spread = Math.Abs(P3.Y - HubCentreY);
            if (Math.Abs(P0.Y - HubCentreY) < 0.75)
            {
                GuideY = HubCentreY;
            }
            else if (P0.Y < HubCentreY)
            {
                GuideY = HubCentreY - Spread;
            }
            else
            {
                GuideY = HubCentreY + Spread;
            }
        }

        return new List<Position>
        {
            P0,
            new Position(MidX, GuideY),
            P3
        };
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F29/F30 TB decision fan-out from a diamond hub: bow outward using the target leg (|P3.X − hub|).
    /// </summary>
    static string BuildFlowchartVerticalDecisionBranchOutwardCurveBasisPath(Position P0, Position P3, double HubCentreX)
    {
        return BuildFlowchartVerticalOutwardCurveBasisPath(
            P0,
            P3,
            HubCentreX,
            FlowchartOutwardCurveSpreadAnchor.TargetLeg);
    }

    /// <summary>
    /// F34 LR decision fan-out: bow outward using the target leg (|P3.Y − hub|).
    /// </summary>
    static string BuildFlowchartHorizontalDecisionBranchOutwardCurveBasisPath(Position P0, Position P3, double HubCentreY)
    {
        return BuildFlowchartCurveBasisPath(
            BuildFlowchartHorizontalOutwardCurveGuidedPoints(
                P0,
                P3,
                HubCentreY,
                FlowchartOutwardCurveSpreadAnchor.TargetLeg));
    }

    static List<Position> BuildFlowchartHorizontalOutwardCurveGuidedPoints(
        Position P0,
        Position P3,
        double HubCentreY,
        FlowchartOutwardCurveSpreadAnchor spreadAnchor)
    {
#pragma warning disable IDE0007
        double MidX = (P0.X + P3.X) / 2.0;
        if (Math.Abs(P3.Y - P0.Y) < 0.75)
        {
            return new List<Position>
            {
                P0,
                new Position(MidX, P0.Y),
                P3
            };
        }

        double Spread;
        double GuideY;
        if (spreadAnchor == FlowchartOutwardCurveSpreadAnchor.SourceLeg)
        {
            Spread = Math.Abs(P0.Y - HubCentreY);
            if (Math.Abs(P0.Y - HubCentreY) < 0.75)
            {
                GuideY = HubCentreY;
            }
            else if (P0.Y < HubCentreY)
            {
                GuideY = P0.Y - Spread;
            }
            else
            {
                GuideY = P0.Y + Spread;
            }
        }
        else
        {
            Spread = Math.Abs(P3.Y - HubCentreY);
            if (Math.Abs(P0.Y - HubCentreY) < 0.75)
            {
                GuideY = HubCentreY;
            }
            else if (P0.Y < HubCentreY)
            {
                GuideY = HubCentreY - Spread;
            }
            else
            {
                GuideY = HubCentreY + Spread;
            }
        }

        return new List<Position>
        {
            P0,
            new Position(MidX, GuideY),
            P3
        };
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F37/F38/F40/F43 cross-boundary: smooth curveBasis through every guided waypoint (no composite L legs on corridor rows).
    /// </summary>
    static string BuildFlowchartCrossBoundaryGuidedCurveBasisPath(IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        if (lineData.Count < 2)
        {
            return string.Empty;
        }

        if (lineData.Count >= 3)
        {
            List<Position> GuidedPoints = new List<Position>();
            for (int PointIndex = 0; PointIndex < lineData.Count; PointIndex++)
            {
                GuidedPoints.Add(lineData[PointIndex]);
            }

            return BuildFlowchartCurveBasisPath(GuidedPoints);
        }

        return BuildFlowchartCurveBasisPath(new List<Position> { lineData[0], lineData[^1] });
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F58 margin collector: find the first waypoint on a horizontal corridor row (wide same-Y run).
    /// </summary>
    static int FindFlowchartHorizontalCorridorRowStartIndex(IReadOnlyList<Position> Points)
    {
        const double Epsilon = 0.75;
        int BestStartIndex = -1;
        int BestRunLength = 0;

        for (int StartIndex = 0; StartIndex < Points.Count - 1; StartIndex++)
        {
            double RowY = Points[StartIndex].Y;
            int RunLength = 1;
            double MinX = Points[StartIndex].X;
            double MaxX = MinX;
            for (int NextIndex = StartIndex + 1; NextIndex < Points.Count; NextIndex++)
            {
                if (Math.Abs(Points[NextIndex].Y - RowY) > Epsilon)
                {
                    break;
                }

                RunLength++;
                MinX = Math.Min(MinX, Points[NextIndex].X);
                MaxX = Math.Max(MaxX, Points[NextIndex].X);
            }

            if (RunLength < 2)
            {
                continue;
            }

            if ((MaxX - MinX) < 24.0)
            {
                continue;
            }

            if (StartIndex < 2)
            {
                continue;
            }

            if (RunLength > BestRunLength)
            {
                BestRunLength = RunLength;
                BestStartIndex = StartIndex;
            }
        }

        return BestStartIndex;
    }

    /// <summary>
    /// F58: curveBasis on the margin leg only; straight segments along the corridor row and into the target.
    /// A single global curveBasis through all waypoints bows through interior nodes.
    /// </summary>
    static bool TryBuildFlowchartTerminalCollectorMarginCompositePath(
        IReadOnlyList<Position> LineData,
        out string Path)
    {
        Path = string.Empty;
        int HorizontalRowStartIndex = FindFlowchartHorizontalCorridorRowStartIndex(LineData);
        if (HorizontalRowStartIndex < 2 || HorizontalRowStartIndex >= LineData.Count - 1)
        {
            return false;
        }

        List<Position> MarginLegPoints = new List<Position>();
        for (int PointIndex = 0; PointIndex <= HorizontalRowStartIndex; PointIndex++)
        {
            MarginLegPoints.Add(LineData[PointIndex]);
        }

        if (MarginLegPoints.Count < 2)
        {
            return false;
        }

        Path = BuildFlowchartCurveBasisPath(MarginLegPoints);
        for (int PointIndex = HorizontalRowStartIndex + 1; PointIndex < LineData.Count; PointIndex++)
        {
            Position Point = LineData[PointIndex];
            Path = Path + "L" + Fmt(Point.X) + "," + Fmt(Point.Y);
        }

        return true;
    }

    const double FLOWCHART_TB_UPWARD_LOOP_CORNER_APPROACH_RATIO = 0.45;

    const double FLOWCHART_TB_UPWARD_LOOP_SOUTH_U_RADIUS_PX = 36.0;

    const double FLOWCHART_TB_UPWARD_LOOP_TOP_ARC_RADIUS_PX = 52.0;

    static string AppendFlowchartCubicSegment(
        string Path,
        double Control1X,
        double Control1Y,
        double Control2X,
        double Control2Y,
        double EndX,
        double EndY)
    {
        return Path + "C"
            + Fmt(Control1X) + "," + Fmt(Control1Y) + " "
            + Fmt(Control2X) + "," + Fmt(Control2Y) + " "
            + Fmt(EndX) + "," + Fmt(EndY);
    }

    static string AppendFlowchartTbUpwardLoopOrthogonalCubic(
        string Path,
        Position From,
        Position To)
    {
        if (Math.Abs(From.X - To.X) <= 0.75)
        {
            double SpanY = To.Y - From.Y;
            return AppendFlowchartCubicSegment(
                Path,
                From.X,
                From.Y + (SpanY * FLOWCHART_TB_UPWARD_LOOP_CORNER_APPROACH_RATIO),
                From.X,
                To.Y,
                To.X,
                To.Y);
        }

        if (Math.Abs(From.Y - To.Y) <= 0.75)
        {
            double SpanX = To.X - From.X;
            return AppendFlowchartCubicSegment(
                Path,
                From.X + (SpanX * FLOWCHART_TB_UPWARD_LOOP_CORNER_APPROACH_RATIO),
                From.Y,
                From.X + (SpanX * (1.0 - FLOWCHART_TB_UPWARD_LOOP_CORNER_APPROACH_RATIO)),
                To.Y,
                To.X,
                To.Y);
        }

        return Path + "L" + Fmt(To.X) + "," + Fmt(To.Y);
    }

    /// <summary>
    /// TB back-edge in a cluster: straight south, outward cubic U onto the margin column, vertical stem, outward east then south into the hub top-centre.
    /// </summary>
    static string BuildFlowchartTbUpwardLoopCurveBasisPath(IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        if (lineData.Count < 2)
        {
            return BuildFlowchartCurveBasisPath(lineData);
        }

        if (lineData.Count < 4)
        {
            return BuildFlowchartCurveBasisPath(lineData);
        }

        Position SourceAttach = lineData[0];
        Position TargetAttach = lineData[lineData.Count - 1];

        int DetourCornerIndex = -1;
        double DetourColumnX = SourceAttach.X;
        for (int PointIndex = 1; PointIndex < lineData.Count - 1; PointIndex++)
        {
            Position Point = lineData[PointIndex];
            if (Math.Abs(Point.X - SourceAttach.X) > 0.75)
            {
                DetourColumnX = Point.X;
                DetourCornerIndex = PointIndex;
                break;
            }
        }

        if (DetourCornerIndex < 1)
        {
            return BuildFlowchartCurveBasisPath(lineData);
        }

        int VerticalEndIndex = DetourCornerIndex;
        for (int PointIndex = DetourCornerIndex + 1; PointIndex < lineData.Count - 1; PointIndex++)
        {
            Position Point = lineData[PointIndex];
            if (Math.Abs(Point.X - DetourColumnX) <= 0.75)
            {
                VerticalEndIndex = PointIndex;
            }
        }

        string Path = "M" + Fmt(SourceAttach.X) + "," + Fmt(SourceAttach.Y);
        for (int PointIndex = 1; PointIndex < DetourCornerIndex; PointIndex++)
        {
            Position Point = lineData[PointIndex];
            if (Math.Abs(Point.X - SourceAttach.X) <= 0.75)
            {
                Path = Path + "L" + Fmt(Point.X) + "," + Fmt(Point.Y);
            }
        }

        Position DetourCorner = lineData[DetourCornerIndex];
        double OutwardSouthY = DetourCorner.Y + FLOWCHART_TB_UPWARD_LOOP_SOUTH_U_RADIUS_PX;
        Path = AppendFlowchartCubicSegment(
            Path,
            SourceAttach.X,
            OutwardSouthY,
            DetourCorner.X,
            OutwardSouthY,
            DetourCorner.X,
            DetourCorner.Y);

        Position VerticalEnd = lineData[VerticalEndIndex];
        if (VerticalEndIndex > DetourCornerIndex
            && (Math.Abs(VerticalEnd.Y - DetourCorner.Y) > 0.75
                || Math.Abs(VerticalEnd.X - DetourCorner.X) > 0.75))
        {
            Path = Path + "L" + Fmt(VerticalEnd.X) + "," + Fmt(VerticalEnd.Y);
        }

        Position EastApproach = VerticalEnd;
        for (int PointIndex = VerticalEndIndex + 1; PointIndex < lineData.Count - 1; PointIndex++)
        {
            Position Point = lineData[PointIndex];
            if (Math.Abs(Point.Y - VerticalEnd.Y) <= 0.75
                && Math.Abs(Point.X - VerticalEnd.X) > 0.75)
            {
                EastApproach = Point;
                break;
            }
        }

        if (Math.Abs(EastApproach.X - VerticalEnd.X) > 0.75)
        {
            if (Math.Abs(EastApproach.Y - VerticalEnd.Y) <= 0.75)
            {
                double CrownY = Math.Min(VerticalEnd.Y, EastApproach.Y) - FLOWCHART_TB_UPWARD_LOOP_TOP_ARC_RADIUS_PX;
                Path = AppendFlowchartCubicSegment(
                    Path,
                    VerticalEnd.X,
                    CrownY,
                    TargetAttach.X,
                    CrownY,
                    TargetAttach.X,
                    TargetAttach.Y);
            }
            else
            {
                double EastSpanX = EastApproach.X - VerticalEnd.X;
                double OutwardNorthY = EastApproach.Y - FLOWCHART_TB_UPWARD_LOOP_SOUTH_U_RADIUS_PX;
                Path = AppendFlowchartCubicSegment(
                    Path,
                    VerticalEnd.X,
                    OutwardNorthY,
                    VerticalEnd.X + (EastSpanX * FLOWCHART_TB_UPWARD_LOOP_CORNER_APPROACH_RATIO),
                    OutwardNorthY,
                    EastApproach.X,
                    EastApproach.Y);

                double SouthSpanY = TargetAttach.Y - EastApproach.Y;
                Path = AppendFlowchartCubicSegment(
                    Path,
                    EastApproach.X,
                    EastApproach.Y - FLOWCHART_TB_UPWARD_LOOP_SOUTH_U_RADIUS_PX,
                    TargetAttach.X,
                    EastApproach.Y + (SouthSpanY * FLOWCHART_TB_UPWARD_LOOP_CORNER_APPROACH_RATIO),
                    TargetAttach.X,
                    TargetAttach.Y);
            }
        }
        else if (Math.Abs(TargetAttach.Y - VerticalEnd.Y) > 0.75)
        {
            double SouthSpanY = TargetAttach.Y - VerticalEnd.Y;
            Path = AppendFlowchartCubicSegment(
                Path,
                VerticalEnd.X,
                VerticalEnd.Y - FLOWCHART_TB_UPWARD_LOOP_SOUTH_U_RADIUS_PX,
                TargetAttach.X,
                VerticalEnd.Y + (SouthSpanY * FLOWCHART_TB_UPWARD_LOOP_CORNER_APPROACH_RATIO),
                TargetAttach.X,
                TargetAttach.Y);
        }

        return Path;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F58 margin: straight vertical stem, then one cubic with vertical tangent at the turn (cp1 on source column at Done row).
    /// </summary>
    static string BuildFlowchartTerminalCollectorMarginCurveBasisPath(IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        if (lineData.Count < 2)
        {
            return BuildFlowchartCurveBasisPath(lineData);
        }

        Position SourceAttach = lineData[0];
        Position TargetAttach = lineData[lineData.Count - 1];
        string Path = "M" + Fmt(SourceAttach.X) + "," + Fmt(SourceAttach.Y);

        for (int PointIndex = 1; PointIndex < lineData.Count - 1; PointIndex++)
        {
            Position Point = lineData[PointIndex];
            if (Math.Abs(Point.X - SourceAttach.X) > 0.75)
            {
                continue;
            }

            Position PreviousPoint = lineData[PointIndex - 1];
            if (Math.Abs(Point.Y - PreviousPoint.Y) > 0.75
                || Math.Abs(Point.X - PreviousPoint.X) > 0.75)
            {
                Path = Path + "L" + Fmt(Point.X) + "," + Fmt(Point.Y);
            }
        }

        Position SideApproach = TargetAttach;
        if (lineData.Count >= 3)
        {
            Position BeforeTarget = lineData[lineData.Count - 2];
            if (Math.Abs(BeforeTarget.Y - TargetAttach.Y) <= 0.75
                && Math.Abs(BeforeTarget.X - TargetAttach.X) > 0.75)
            {
                SideApproach = BeforeTarget;
            }
        }

        double Control1X = SourceAttach.X;
        double Control1Y = TargetAttach.Y;
        double Control2X = SideApproach.X;
        double Control2Y = TargetAttach.Y;

        Path = Path + "C"
            + Fmt(Control1X) + "," + Fmt(Control1Y) + " "
            + Fmt(Control2X) + "," + Fmt(Control2Y) + " "
            + Fmt(TargetAttach.X) + "," + Fmt(TargetAttach.Y);

        return Path;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F53/F53A: vertical stem at source X, then one cubic into the stadium shoulder (avoids curveBasis kink at the approach knot).
    /// </summary>
    static bool TryBuildFlowchartTbStadiumMergeHookPath(
        IReadOnlyList<Position> lineData,
        out string Path)
    {
#pragma warning disable IDE0007
        Path = string.Empty;
        if (lineData.Count < 4)
        {
            return false;
        }

        Position SourceAttach = lineData[0];
        Position TargetAttach = lineData[lineData.Count - 1];

        Path = "M" + Fmt(SourceAttach.X) + "," + Fmt(SourceAttach.Y);
        int LastVerticalIndex = 0;
        for (int PointIndex = 1; PointIndex < lineData.Count - 1; PointIndex++)
        {
            Position Point = lineData[PointIndex];
            if (Math.Abs(Point.X - SourceAttach.X) <= 0.75)
            {
                LastVerticalIndex = PointIndex;
            }
        }

        Position MergeApproach = lineData[lineData.Count - 2];
        for (int PointIndex = lineData.Count - 2; PointIndex >= 1; PointIndex--)
        {
            Position Candidate = lineData[PointIndex];
            if (PointIndex <= LastVerticalIndex)
            {
                continue;
            }

            if (Math.Abs(Candidate.X - SourceAttach.X) <= 0.75)
            {
                continue;
            }

            MergeApproach = Candidate;
            break;
        }

        if (Math.Abs(MergeApproach.X - SourceAttach.X) <= 0.75)
        {
            double BlendedApproachX = SourceAttach.X
                + ((TargetAttach.X - SourceAttach.X) * 0.45);
            MergeApproach = new Position(BlendedApproachX, MergeApproach.Y);
        }

        for (int PointIndex = 1; PointIndex <= LastVerticalIndex; PointIndex++)
        {
            Position Point = lineData[PointIndex];
            if (Math.Abs(Point.X - SourceAttach.X) > 0.75)
            {
                continue;
            }

            Position PreviousPoint = lineData[PointIndex - 1];
            if (Math.Abs(Point.Y - PreviousPoint.Y) > 0.75
                || Math.Abs(Point.X - PreviousPoint.X) > 0.75)
            {
                Path = Path + "L" + Fmt(Point.X) + "," + Fmt(Point.Y);
            }
        }

        Position VerticalEnd = lineData[LastVerticalIndex];
        double Control1X = SourceAttach.X;
        double Control1Y = MergeApproach.Y;
        if (Math.Abs(Control1Y - TargetAttach.Y) <= 0.75)
        {
            Control1Y = VerticalEnd.Y + ((TargetAttach.Y - VerticalEnd.Y) * 0.55);
        }

        double Control2X = MergeApproach.X;
        double Control2Y = MergeApproach.Y;
        const double SHOULDER_APPROACH_LIFT_PX = 14.0;
        if (Math.Abs(Control2Y - TargetAttach.Y) <= 0.75)
        {
            Control2Y = TargetAttach.Y - SHOULDER_APPROACH_LIFT_PX;
        }

        Path = Path + "C"
            + Fmt(Control1X) + "," + Fmt(Control1Y) + " "
            + Fmt(Control2X) + "," + Fmt(Control2Y) + " "
            + Fmt(TargetAttach.X) + "," + Fmt(TargetAttach.Y);

        return true;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F53 TB dual fan-in: vertical stem at source X, then smooth hook into the stadium shoulder attach.
    /// </summary>
    static string BuildFlowchartTbStadiumMergeCurveBasisPath(
        IReadOnlyList<Position> lineData,
        bool useTerminalCollectorMarginCurve,
        bool useStadiumMergeHook = false)
    {
#pragma warning disable IDE0007
        if (useTerminalCollectorMarginCurve)
        {
            return BuildFlowchartTerminalCollectorMarginCurveBasisPath(lineData);
        }

        if (useStadiumMergeHook
            && lineData.Count >= 4
            && TryBuildFlowchartTbStadiumMergeHookPath(lineData, out string MergeHookPath))
        {
            return MergeHookPath;
        }

        if (lineData.Count > 4)
        {
            List<Position> AllGuidedPoints = new List<Position>();
            for (int PointIndex = 0; PointIndex < lineData.Count; PointIndex++)
            {
                AllGuidedPoints.Add(lineData[PointIndex]);
            }

            return BuildFlowchartCurveBasisPath(AllGuidedPoints);
        }

        if (lineData.Count >= 4)
        {
            List<Position> GuidedPoints = new List<Position>
            {
                lineData[0],
                lineData[1],
                lineData[2],
                lineData[^1]
            };
            return BuildFlowchartCurveBasisPath(GuidedPoints);
        }

        if (lineData.Count == 3)
        {
            return BuildFlowchartCurveBasisPath(new List<Position>
            {
                lineData[0],
                lineData[1],
                lineData[2]
            });
        }

        Position P0 = lineData[0];
        Position P3 = lineData[^1];
        double SpanY = P3.Y - P0.Y;
        double StubY = P0.Y + Math.Clamp(SpanY * 0.06, 4.0, 20.0);
        Position StubEnd = new Position(P0.X, StubY);
        double RouteY = P0.Y + Math.Clamp(SpanY * 0.45, 20.0, 40.0);
        Position RoutePoint = new Position(P0.X, RouteY);
        double MidY = (RoutePoint.Y + P3.Y) / 2.0;
        Position ApproachPoint = new Position(P3.X, MidY);

        List<Position> FallbackPoints = new List<Position>
        {
            P0,
            StubEnd,
            RoutePoint,
            ApproachPoint,
            P3
        };
        return BuildFlowchartCurveBasisPath(FallbackPoints);
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Which endpoint supplies spread distance from the hub on the branch X axis.
    /// Fan-out (hub = source): target leg. Fan-in (hub = target): source leg.
    /// </summary>
    enum FlowchartOutwardCurveSpreadAnchor
    {
        TargetLeg,
        SourceLeg
    }

    static string BuildFlowchartVerticalOutwardCurveBasisPath(
        Position P0,
        Position P3,
        double HubCentreX,
        FlowchartOutwardCurveSpreadAnchor spreadAnchor)
    {
        return BuildFlowchartCurveBasisPath(
            BuildFlowchartVerticalOutwardCurveGuidedPoints(P0, P3, HubCentreX, spreadAnchor));
    }

    static List<Position> BuildFlowchartVerticalOutwardCurveGuidedPoints(
        Position P0,
        Position P3,
        double HubCentreX,
        FlowchartOutwardCurveSpreadAnchor spreadAnchor)
    {
#pragma warning disable IDE0007
        double MidY = (P0.Y + P3.Y) / 2.0;
        if (Math.Abs(P3.X - P0.X) < 0.75)
        {
            return new List<Position>
            {
                P0,
                new Position(P0.X, MidY),
                P3
            };
        }

        double Spread;
        double GuideX;
        if (spreadAnchor == FlowchartOutwardCurveSpreadAnchor.SourceLeg)
        {
            Spread = Math.Abs(P0.X - HubCentreX);
            if (Math.Abs(P0.X - HubCentreX) < 0.75)
            {
                GuideX = HubCentreX;
            }
            else if (P0.X < HubCentreX)
            {
                GuideX = P0.X - Spread;
            }
            else
            {
                GuideX = P0.X + Spread;
            }
        }
        else
        {
            Spread = Math.Abs(P3.X - HubCentreX);
            if (Math.Abs(P0.X - HubCentreX) < 0.75)
            {
                GuideX = HubCentreX;
            }
            else if (P0.X < HubCentreX)
            {
                GuideX = HubCentreX - Spread;
            }
            else
            {
                GuideX = HubCentreX + Spread;
            }
        }

        return new List<Position>
        {
            P0,
            new Position(GuideX, MidY),
            P3
        };
#pragma warning restore IDE0007
    }

    static double ResolveFlowchartHubFanCentreX(
        Edge edge,
        Node? sourceNode,
        Node? targetNode,
        FlowchartModel? model)
    {
        if (model is not null
            && targetNode is not null
            && string.Equals(edge.TargetId, targetNode.Id, StringComparison.Ordinal))
        {
            return targetNode.Position.X;
        }

        if (sourceNode is not null)
        {
            return sourceNode.Position.X;
        }

        return 0.0;
    }

    /// <summary>
    /// Straight or Manhattan polylines (min-length dummy chains, aligned TB columns) must not use curveBasis.
    /// </summary>
    static bool ShouldUseLinearFlowchartPath(IReadOnlyList<Position> Points)
    {
        if (Points.Count < 2)
        {
            return false;
        }

        const double Epsilon = 0.75;
        double FirstX = Points[0].X;
        bool SameColumn = true;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            if (Math.Abs(Points[Index].X - FirstX) > Epsilon)
            {
                SameColumn = false;
                break;
            }
        }

        if (SameColumn)
        {
            return true;
        }

        double FirstY = Points[0].Y;
        bool SameRow = true;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            if (Math.Abs(Points[Index].Y - FirstY) > Epsilon)
            {
                SameRow = false;
                break;
            }
        }

        if (SameRow)
        {
            return true;
        }

        for (int Index = 1; Index < Points.Count; Index++)
        {
            bool HorizontalSegment = Math.Abs(Points[Index].X - Points[Index - 1].X) < Epsilon;
            bool VerticalSegment = Math.Abs(Points[Index].Y - Points[Index - 1].Y) < Epsilon;
            if (!HorizontalSegment && !VerticalSegment)
            {
                return false;
            }
        }

        return true;
    }

    static string BuildFlowchartLinearPath(IReadOnlyList<Position> points)
    {
        if (points.Count < 2)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append('M').Append(Fmt(points[0].X)).Append(',').Append(Fmt(points[0].Y));
        for (var i = 1; i < points.Count; i++)
        {
            sb.Append('L').Append(Fmt(points[i].X)).Append(',').Append(Fmt(points[i].Y));
        }

        return sb.ToString();
    }

    static List<Position> FilterValidPoints(IReadOnlyList<Position> points)
    {
        List<Position> Result = new List<Position>(points.Count);
        foreach (Position point in points)
        {
            if (double.IsNaN(point.X) || double.IsNaN(point.Y))
            {
                continue;
            }
            Result.Add(point);
        }
        return Result;
    }

    static List<Position> FixCorners(IReadOnlyList<Position> lineData)
    {
        // Port of Mermaid fixCorners() in edges.js.
        // See `mermaid/packages/mermaid/src/rendering-util/rendering-elements/edges.js`.
        List<int> CornerPointPositions = ExtractCornerPointPositions(lineData);
        if (CornerPointPositions.Count == 0)
        {
            return new List<Position>(lineData);
        }

        List<Position> NewLineData = new List<Position>(lineData.Count + (CornerPointPositions.Count * 2));
        for (int Index = 0; Index < lineData.Count; Index++)
        {
            if (!CornerPointPositions.Contains(Index))
            {
                NewLineData.Add(lineData[Index]);
                continue;
            }

            Position PrevPoint = lineData[Index - 1];
            Position NextPoint = lineData[Index + 1];
            Position CornerPoint = lineData[Index];

            Position NewPrevPoint = FindAdjacentPoint(PrevPoint, CornerPoint, 5.0);
            Position NewNextPoint = FindAdjacentPoint(NextPoint, CornerPoint, 5.0);

            double XDiff = NewNextPoint.X - NewPrevPoint.X;
            double YDiff = NewNextPoint.Y - NewPrevPoint.Y;
            NewLineData.Add(NewPrevPoint);

            const double R = 5.0;
            double A = Math.Sqrt(2.0) * 2.0;
            Position NewCornerPoint = CornerPoint;
            if (Math.Abs(NextPoint.X - PrevPoint.X) > 10.0 && Math.Abs(NextPoint.Y - PrevPoint.Y) >= 10.0)
            {
                if (Math.Abs(CornerPoint.X - NewPrevPoint.X) < 0.0001)
                {
                    NewCornerPoint = new Position(
                        XDiff < 0.0 ? NewPrevPoint.X - R + A : NewPrevPoint.X + R - A,
                        YDiff < 0.0 ? NewPrevPoint.Y - A : NewPrevPoint.Y + A);
                }
                else
                {
                    NewCornerPoint = new Position(
                        XDiff < 0.0 ? NewPrevPoint.X - A : NewPrevPoint.X + A,
                        YDiff < 0.0 ? NewPrevPoint.Y - R + A : NewPrevPoint.Y + R - A);
                }
            }

            NewLineData.Add(NewCornerPoint);
            NewLineData.Add(NewNextPoint);
        }

        return NewLineData;
    }

    static List<int> ExtractCornerPointPositions(IReadOnlyList<Position> points)
    {
        List<int> CornerPointPositions = new List<int>();
        for (int Index = 1; Index < points.Count - 1; Index++)
        {
            Position Prev = points[Index - 1];
            Position Curr = points[Index];
            Position Next = points[Index + 1];

            if (Math.Abs(Prev.X - Curr.X) < 0.0001
                && Math.Abs(Curr.Y - Next.Y) < 0.0001
                && Math.Abs(Curr.X - Next.X) > 5.0
                && Math.Abs(Curr.Y - Prev.Y) > 5.0)
            {
                CornerPointPositions.Add(Index);
            }
            else if (Math.Abs(Prev.Y - Curr.Y) < 0.0001
                     && Math.Abs(Curr.X - Next.X) < 0.0001
                     && Math.Abs(Curr.X - Prev.X) > 5.0
                     && Math.Abs(Curr.Y - Next.Y) > 5.0)
            {
                CornerPointPositions.Add(Index);
            }
        }
        return CornerPointPositions;
    }

    static Position FindAdjacentPoint(Position PointA, Position PointB, double distance)
    {
        double XDiff = PointB.X - PointA.X;
        double YDiff = PointB.Y - PointA.Y;
        double Length = Math.Sqrt((XDiff * XDiff) + (YDiff * YDiff));
        if (Length <= 0.0000001)
        {
            return PointB;
        }
        double Ratio = distance / Length;
        return new Position(PointB.X - (Ratio * XDiff), PointB.Y - (Ratio * YDiff));
    }

    static List<Position> TrimPointsForCrossVisibleStroke(IReadOnlyList<Position> Points, double StartGapPx, double EndGapPx)
    {
        if (Points.Count < 2)
        {
            return new List<Position>(Points);
        }

        List<Position> Trimmed = new List<Position>(Points);
        if (StartGapPx > 0.0)
        {
            Trimmed[0] = FindAdjacentPoint(Points[1], Points[0], StartGapPx);
        }

        if (EndGapPx > 0.0)
        {
            int LastIndex = Points.Count - 1;
            Trimmed[LastIndex] = FindAdjacentPoint(Points[LastIndex - 1], Points[LastIndex], EndGapPx);
        }

        return Trimmed;
    }

    static (string? ArrowTypeStart, string? ArrowTypeEnd) GetMermaidArrowTypes(EdgeType edgeType)
    {
        // Mermaid lineWithOffset offsets only for markerOffsets entries.
        // For flowcharts, that means arrow_point (and certain class-diagram markers).
        return edgeType switch
        {
            EdgeType.BiDirectional => ("arrow_point", "arrow_point"),
            EdgeType.Arrow => (null, "arrow_point"),
            EdgeType.DottedArrow => (null, "arrow_point"),
            EdgeType.ThickArrow => (null, "arrow_point_thick"),
            _ => (null, null)
        };
    }

    static List<Position> ApplyLineWithOffset(IReadOnlyList<Position> points, string? arrowTypeStart, string? arrowTypeEnd)
    {
        // Port of Mermaid getLineFunctionsWithOffset() from lineWithOffset.ts.
        if (points.Count == 0)
        {
            return new List<Position>();
        }

        Dictionary<string, double> MarkerOffsets = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            { "aggregation", 17.25 },
            { "extension", 17.25 },
            { "composition", 17.25 },
            { "dependency", 6.0 },
            { "lollipop", 13.5 },
            { "arrow_point", 4.0 },
            { "arrow_point_thick", FLOWCHART_THICK_ARROW_POINT_MARKER_PX },
            { "arrow_barb", 0.0 },
            { "arrow_barb_neo", 5.5 }
        };

        double StartMarkerHeight = 0.0;
        if (!string.IsNullOrWhiteSpace(arrowTypeStart) && MarkerOffsets.TryGetValue(arrowTypeStart, out double startHeight))
        {
            StartMarkerHeight = startHeight;
        }

        double EndMarkerHeight = 0.0;
        if (!string.IsNullOrWhiteSpace(arrowTypeEnd) && MarkerOffsets.TryGetValue(arrowTypeEnd, out double endHeight))
        {
            EndMarkerHeight = endHeight;
        }

        List<Position> Output = new List<Position>(points.Count);
        for (int Index = 0; Index < points.Count; Index++)
        {
            Position Point = points[Index];

            double XOffset = CalculateLineWithOffsetX(points, Index, arrowTypeStart, arrowTypeEnd, StartMarkerHeight, EndMarkerHeight);
            double YOffset = CalculateLineWithOffsetY(points, Index, arrowTypeStart, arrowTypeEnd, StartMarkerHeight, EndMarkerHeight);

            Output.Add(new Position(Point.X + XOffset, Point.Y + YOffset));
        }

        return Output;
    }

    static double CalculateLineWithOffsetX(
        IReadOnlyList<Position> data,
        int index,
        string? arrowTypeStart,
        string? arrowTypeEnd,
        double startMarkerHeight,
        double endMarkerHeight)
    {
        double Offset = 0.0;
        string Direction = data[0].X < data[data.Count - 1].X ? "left" : "right";

        if (index == 0 && !string.IsNullOrWhiteSpace(arrowTypeStart) && startMarkerHeight > 0.0)
        {
            (double Angle, double DeltaX, double DeltaY) = CalculateDeltaAndAngle(data[0], data[1]);
            Offset = startMarkerHeight * Math.Cos(Angle) * (DeltaX >= 0.0 ? 1.0 : -1.0);
        }
        else if (index == data.Count - 1 && !string.IsNullOrWhiteSpace(arrowTypeEnd) && endMarkerHeight > 0.0)
        {
            (double Angle, double DeltaX, double DeltaY) = CalculateDeltaAndAngle(data[data.Count - 1], data[data.Count - 2]);
            Offset = endMarkerHeight * Math.Cos(Angle) * (DeltaX >= 0.0 ? 1.0 : -1.0);
        }

        double DifferenceToEnd = Math.Abs(data[index].X - data[data.Count - 1].X);
        double DifferenceInYEnd = Math.Abs(data[index].Y - data[data.Count - 1].Y);
        double DifferenceToStart = Math.Abs(data[index].X - data[0].X);
        double DifferenceInYStart = Math.Abs(data[index].Y - data[0].Y);
        const double ExtraRoom = 1.0;

        if (DifferenceToEnd < endMarkerHeight && DifferenceToEnd > 0.0 && DifferenceInYEnd < endMarkerHeight)
        {
            double Adjustment = endMarkerHeight + ExtraRoom - DifferenceToEnd;
            Adjustment *= Direction == "right" ? -1.0 : 1.0;
            Offset -= Adjustment;
        }

        if (DifferenceToStart < startMarkerHeight && DifferenceToStart > 0.0 && DifferenceInYStart < startMarkerHeight)
        {
            double Adjustment = startMarkerHeight + ExtraRoom - DifferenceToStart;
            Adjustment *= Direction == "right" ? -1.0 : 1.0;
            Offset += Adjustment;
        }

        return Offset;
    }

    static double CalculateLineWithOffsetY(
        IReadOnlyList<Position> data,
        int index,
        string? arrowTypeStart,
        string? arrowTypeEnd,
        double startMarkerHeight,
        double endMarkerHeight)
    {
        double Offset = 0.0;
        string Direction = data[0].Y < data[data.Count - 1].Y ? "down" : "up";

        if (index == 0 && !string.IsNullOrWhiteSpace(arrowTypeStart) && startMarkerHeight > 0.0)
        {
            (double Angle, double DeltaX, double DeltaY) = CalculateDeltaAndAngle(data[0], data[1]);
            Offset = startMarkerHeight * Math.Abs(Math.Sin(Angle)) * (DeltaY >= 0.0 ? 1.0 : -1.0);
        }
        else if (index == data.Count - 1 && !string.IsNullOrWhiteSpace(arrowTypeEnd) && endMarkerHeight > 0.0)
        {
            (double Angle, double DeltaX, double DeltaY) = CalculateDeltaAndAngle(data[data.Count - 1], data[data.Count - 2]);
            Offset = endMarkerHeight * Math.Abs(Math.Sin(Angle)) * (DeltaY >= 0.0 ? 1.0 : -1.0);
        }

        double DifferenceToEnd = Math.Abs(data[index].Y - data[data.Count - 1].Y);
        double DifferenceInXEnd = Math.Abs(data[index].X - data[data.Count - 1].X);
        double DifferenceToStart = Math.Abs(data[index].Y - data[0].Y);
        double DifferenceInXStart = Math.Abs(data[index].X - data[0].X);
        const double ExtraRoom = 1.0;

        if (DifferenceToEnd < endMarkerHeight && DifferenceToEnd > 0.0 && DifferenceInXEnd < endMarkerHeight)
        {
            double Adjustment = endMarkerHeight + ExtraRoom - DifferenceToEnd;
            Adjustment *= Direction == "up" ? -1.0 : 1.0;
            Offset -= Adjustment;
        }

        if (DifferenceToStart < startMarkerHeight && DifferenceToStart > 0.0 && DifferenceInXStart < startMarkerHeight)
        {
            double Adjustment = startMarkerHeight + ExtraRoom - DifferenceToStart;
            Adjustment *= Direction == "up" ? -1.0 : 1.0;
            Offset += Adjustment;
        }

        return Offset;
    }

    static (double Angle, double DeltaX, double DeltaY) CalculateDeltaAndAngle(Position point1, Position point2)
    {
        double DeltaX = point2.X - point1.X;
        double DeltaY = point2.Y - point1.Y;

        // Mermaid uses atan(deltaY / deltaX) (not atan2).
        double Angle = Math.Atan(DeltaY / DeltaX);
        return (Angle, DeltaX, DeltaY);
    }

    static List<Position> ApplyMarkerOffsetsToPoints(IReadOnlyList<Position> points, string? arrowTypeStart, string? arrowTypeEnd)
    {
        // Port of Mermaid applyMarkerOffsetsToPoints() (rounded curve only).
        // Source: mermaid/packages/mermaid/src/rendering-util/rendering-elements/edges.js
        if (points.Count < 2)
        {
            return new List<Position>(points);
        }

        Dictionary<string, double> MarkerOffsets = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            { "aggregation", 17.25 },
            { "extension", 17.25 },
            { "composition", 17.25 },
            { "dependency", 6.0 },
            { "lollipop", 13.5 },
            { "arrow_point", 4.0 },
            { "arrow_point_thick", FLOWCHART_THICK_ARROW_POINT_MARKER_PX },
            { "arrow_barb", 0.0 },
            { "arrow_barb_neo", 5.5 }
        };

        List<Position> NewPoints = new List<Position>(points.Count);
        for (int Index = 0; Index < points.Count; Index++)
        {
            NewPoints.Add(points[Index]);
        }

        if (!string.IsNullOrWhiteSpace(arrowTypeStart) && MarkerOffsets.TryGetValue(arrowTypeStart, out double startOffsetValue))
        {
            (double Angle, double DeltaX, double DeltaY) = CalculateDeltaAndAngle(points[0], points[1]);
            NewPoints[0] = new Position(
                points[0].X + (startOffsetValue * Math.Cos(Angle)),
                points[0].Y + (startOffsetValue * Math.Sin(Angle)));
        }

        if (!string.IsNullOrWhiteSpace(arrowTypeEnd) && MarkerOffsets.TryGetValue(arrowTypeEnd, out double endOffsetValue))
        {
            int LastIndex = points.Count - 1;
            (double Angle, double DeltaX, double DeltaY) = CalculateDeltaAndAngle(points[LastIndex - 1], points[LastIndex]);
            NewPoints[LastIndex] = new Position(
                points[LastIndex].X - (endOffsetValue * Math.Cos(Angle)),
                points[LastIndex].Y - (endOffsetValue * Math.Sin(Angle)));
        }

        return NewPoints;
    }

    static string GenerateRoundedPath(IReadOnlyList<Position> points, double radius)
    {
        // Port of Mermaid generateRoundedPath(points, radius=5) in edges.js.
        if (points.Count < 2)
        {
            return string.Empty;
        }

        const double Epsilon = 0.00001;
        StringBuilder Path = new StringBuilder();

        for (int Index = 0; Index < points.Count; Index++)
        {
            Position CurrPoint = points[Index];
            Position PrevPoint = Index > 0 ? points[Index - 1] : points[0];
            Position NextPoint = Index < points.Count - 1 ? points[Index + 1] : points[points.Count - 1];

            if (Index == 0)
            {
                Path.Append('M').Append(Fmt(CurrPoint.X)).Append(',').Append(Fmt(CurrPoint.Y));
                continue;
            }

            if (Index == points.Count - 1)
            {
                Path.Append('L').Append(Fmt(CurrPoint.X)).Append(',').Append(Fmt(CurrPoint.Y));
                continue;
            }

            double PrevDx = CurrPoint.X - PrevPoint.X;
            double PrevDy = CurrPoint.Y - PrevPoint.Y;
            double NextDx = NextPoint.X - CurrPoint.X;
            double NextDy = NextPoint.Y - CurrPoint.Y;

            double PrevLen = Math.Sqrt((PrevDx * PrevDx) + (PrevDy * PrevDy));
            double NextLen = Math.Sqrt((NextDx * NextDx) + (NextDy * NextDy));

            if (PrevLen < Epsilon || NextLen < Epsilon)
            {
                Path.Append('L').Append(Fmt(CurrPoint.X)).Append(',').Append(Fmt(CurrPoint.Y));
                continue;
            }

            double CutLen = Math.Min(radius, Math.Min(PrevLen, NextLen) / 2.0);

            double StartX = CurrPoint.X - ((PrevDx / PrevLen) * CutLen);
            double StartY = CurrPoint.Y - ((PrevDy / PrevLen) * CutLen);
            double EndX = CurrPoint.X + ((NextDx / NextLen) * CutLen);
            double EndY = CurrPoint.Y + ((NextDy / NextLen) * CutLen);

            Path.Append('L').Append(Fmt(StartX)).Append(',').Append(Fmt(StartY));
            Path.Append('Q')
                .Append(Fmt(CurrPoint.X)).Append(',').Append(Fmt(CurrPoint.Y)).Append(' ')
                .Append(Fmt(EndX)).Append(',').Append(Fmt(EndY));
        }

        return Path.ToString();
    }

    private static string GetConfigString(FlowchartModel model, string key, string fallback)
    {
        if (!model.Config.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value;
    }

    static string? ComputeNeoStrokeDasharray(IReadOnlyList<Position> points, Edge edge, string edgeCurveType)
    {
        // Mermaid neo look dash shaping uses path.getTotalLength() with markerOffsets2.
        // Naiad approximates length using the routed polyline points after the same point transforms used for path generation.
        // Source: mermaid/packages/mermaid/src/rendering-util/rendering-elements/edges.js
        List<Position> LineData = FilterValidPoints(points);
        if (string.Equals(edgeCurveType, "rounded", StringComparison.OrdinalIgnoreCase))
        {
            (string? ArrowTypeStart, string? ArrowTypeEnd) = GetMermaidArrowTypes(edge.Type);
            LineData = ApplyMarkerOffsetsToPoints(LineData, ArrowTypeStart, ArrowTypeEnd);
        }
        else
        {
            LineData = FixCorners(LineData);
            (string? ArrowTypeStart, string? ArrowTypeEnd) = GetMermaidArrowTypes(edge.Type);
            LineData = ApplyLineWithOffset(LineData, ArrowTypeStart, ArrowTypeEnd);
        }

        double Length = 0.0;
        for (int i = 1; i < LineData.Count; i++)
        {
            Length += LineData[i - 1].DistanceTo(LineData[i]);
        }

        double oValueS = GetMarkerOffset2Start(edge.Type);
        double oValueE = GetMarkerOffset2End(edge.Type);

        double middleLength = Length - oValueS - oValueE;
        if (middleLength < 0)
        {
            middleLength = 0;
        }

        // Mermaid treats dotted and dashed the same for generateDashArray here (2/2 pattern).
        if (edge.LineStyle is EdgeStyle.Dotted)
        {
            return GenerateNeoDashArray(Length, oValueS, oValueE);
        }

        return $"0 {Fmt(oValueS)} {Fmt(middleLength)} {Fmt(oValueE)}";
    }

    static string GenerateNeoDashArray(double len, double oValueS, double oValueE)
    {
        // Port of generateDashArray in edges.js (dashLength=2, gapLength=2).
        double middleLength = len - oValueS - oValueE;
        if (middleLength < 0)
        {
            middleLength = 0;
        }

        const int DASH_LENGTH = 2;
        const int GAP_LENGTH = 2;
        int numberOfPairs = (int)Math.Floor(middleLength / (DASH_LENGTH + GAP_LENGTH));

        StringBuilder MiddlePattern = new StringBuilder();
        for (int i = 0; i < numberOfPairs; i++)
        {
            if (i > 0)
            {
                MiddlePattern.Append(' ');
            }
            MiddlePattern.Append(DASH_LENGTH).Append(' ').Append(GAP_LENGTH);
        }

        if (MiddlePattern.Length > 0)
        {
            return $"0 {Fmt(oValueS)} {MiddlePattern} {Fmt(oValueE)}";
        }

        return $"0 {Fmt(oValueS)} {Fmt(oValueE)}";
    }

    static double GetMarkerOffset2Start(EdgeType edgeType)
    {
        // Mermaid markerOffsets2: arrow_point=4, arrow_cross=12.5, arrow_circle=12.5.
        return edgeType switch
        {
            EdgeType.BiDirectional => 4.0,
            EdgeType.BiDirectionalCircle => 12.5,
            EdgeType.BiDirectionalCross => 12.5,
            _ => 0.0
        };
    }

    static double GetMarkerOffset2End(EdgeType edgeType)
    {
        return edgeType switch
        {
            EdgeType.Arrow => 4.0,
            EdgeType.DottedArrow => 4.0,
            EdgeType.ThickArrow => FLOWCHART_THICK_ARROW_POINT_MARKER_PX,
            EdgeType.BiDirectional => 4.0,
            EdgeType.CircleEnd => 12.5,
            EdgeType.CrossEnd => 12.5,
            EdgeType.BiDirectionalCircle => 12.5,
            EdgeType.BiDirectionalCross => 12.5,
            _ => 0.0
        };
    }
#pragma warning restore IDE0007

    /// <summary>
    /// F35: guided polyline with an approach stub and perimeter endpoint so arrowheads meet the diamond edge.
    /// </summary>
    static List<Position> BuildFlowchartTbMultiIncomingDiamondGuidedPoints(
        Position SourceAttach,
        Position GuidePoint,
        Position OutlineAttach,
        string? ArrowTypeStart,
        string? ArrowTypeEnd)
    {
#pragma warning disable IDE0007
        double ApproachDx;
        double ApproachDy;
        if (!TryGetFlowchartApproachUnitVector(GuidePoint, OutlineAttach, out ApproachDx, out ApproachDy))
        {
            if (!TryGetFlowchartApproachUnitVector(SourceAttach, OutlineAttach, out ApproachDx, out ApproachDy))
            {
                ApproachDx = 0.0;
                ApproachDy = 1.0;
            }
        }

        Position PathStart = SourceAttach;
        if (!string.IsNullOrWhiteSpace(ArrowTypeStart)
            && ResolveFlowchartArrowMarkerOffsetPx(ArrowTypeStart) > 0.0
            && TryGetFlowchartApproachUnitVector(SourceAttach, GuidePoint, out double StartDx, out double StartDy))
        {
            double StartOffset = ResolveFlowchartArrowMarkerOffsetPx(ArrowTypeStart);
            PathStart = new Position(
                SourceAttach.X + (StartDx * StartOffset),
                SourceAttach.Y + (StartDy * StartOffset));
        }

        Position EntryStub = new Position(
            OutlineAttach.X - (ApproachDx * FLOWCHART_DIAMOND_INCOMING_ENTRY_STUB_PX),
            OutlineAttach.Y - (ApproachDy * FLOWCHART_DIAMOND_INCOMING_ENTRY_STUB_PX));

        // Stroke ends on the diamond outline; Word rasterisation snaps inlined arrow tips to the same boundary.
        Position PathEnd = OutlineAttach;

        return new List<Position>
        {
            PathStart,
            GuidePoint,
            EntryStub,
            PathEnd
        };
#pragma warning restore IDE0007
    }

    static bool TryGetFlowchartApproachUnitVector(Position From, Position To, out double UnitX, out double UnitY)
    {
#pragma warning disable IDE0007
        UnitX = 0.0;
        UnitY = 0.0;
        double DeltaX = To.X - From.X;
        double DeltaY = To.Y - From.Y;
        double Length = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
        if (Length < 0.01)
        {
            return false;
        }

        UnitX = DeltaX / Length;
        UnitY = DeltaY / Length;
        return true;
#pragma warning restore IDE0007
    }

    static double ResolveFlowchartArrowMarkerOffsetPx(string ArrowType)
    {
#pragma warning disable IDE0007
        if (string.Equals(ArrowType, "arrow_point", StringComparison.Ordinal))
        {
            return FLOWCHART_ARROW_POINT_MARKER_PX;
        }

        if (string.Equals(ArrowType, "arrow_point_thick", StringComparison.Ordinal))
        {
            return FLOWCHART_THICK_ARROW_POINT_MARKER_PX;
        }

        if (string.Equals(ArrowType, "arrow_barb_neo", StringComparison.Ordinal))
        {
            return 5.5;
        }

        if (string.Equals(ArrowType, "arrow_barb", StringComparison.Ordinal))
        {
            return 0.0;
        }

        return 0.0;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F35: three paths into a diamond from above — curve from source bottom to distinct top perimeter ports.
    /// </summary>
    static bool ShouldUseTbMultiIncomingDiamondGuidedCurve(
        Edge edge,
        Node? targetNode,
        FlowchartModel? model,
        Direction diagramDirection,
        IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        if (model is null || targetNode is null || lineData.Count < 2)
        {
            return false;
        }

        if (diagramDirection is not Direction.TopToBottom and not Direction.BottomToTop)
        {
            return false;
        }

        if (targetNode.Shape != NodeShape.Diamond)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(edge.Label))
        {
            return false;
        }

        int IncomingCount = 0;
        for (int i = 0; i < model.Edges.Count; i++)
        {
            if (!string.Equals(model.Edges[i].TargetId, targetNode.Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(model.Edges[i].Label))
            {
                continue;
            }

            IncomingCount++;
            Node? SourceNode = model.GetNode(model.Edges[i].SourceId);
            if (SourceNode is null || SourceNode.Position.Y >= targetNode.Position.Y - 0.01)
            {
                return false;
            }
        }

        return IncomingCount >= 3;
#pragma warning restore IDE0007
    }

    static bool ShouldUseDecisionBranchGuidedCurve(Edge edge, Node? sourceNode, FlowchartModel? model, IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        if (string.IsNullOrWhiteSpace(edge.Label))
        {
            return false;
        }

        if (sourceNode is not null && sourceNode.Shape == NodeShape.Diamond)
        {
            int OutgoingCount = 0;
            if (model is not null)
            {
                for (int i = 0; i < model.Edges.Count; i++)
                {
                    if (string.Equals(model.Edges[i].SourceId, sourceNode.Id, StringComparison.Ordinal))
                    {
                        OutgoingCount++;
                    }
                }
            }

            if (OutgoingCount >= 3 && lineData.Count >= 3 && ShouldUseLinearFlowchartPath(lineData))
            {
                return false;
            }

            if (OutgoingCount == 2)
            {
                return true;
            }

            if (lineData.Count >= 3 && ShouldUseLinearFlowchartPath(lineData))
            {
                return false;
            }

            return true;
        }

        string label = edge.Label.Trim();
        bool isCommonDecisionLabel = label.StartsWith("Yes", StringComparison.OrdinalIgnoreCase)
                                     || label.StartsWith("No", StringComparison.OrdinalIgnoreCase);
        if (isCommonDecisionLabel)
        {
            return lineData.Count >= 2;
        }

        return false;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Resolves edge label anchor; LR side fan-out uses mid-span X and target-port Y (Mermaid Live / F24).
    /// TB decision branches use the same guided waypoints as the rendered curveBasis path (F29/F53).
    /// </summary>
    static Position ResolveFlowchartEdgeLabelPosition(
        Edge edge,
        Node? sourceNode,
        Node? targetNode,
        FlowchartModel? model,
        Direction diagramDirection)
    {
#pragma warning disable IDE0007
        if (TryResolveFlowchartLrSideFanoutEdgeLabelPosition(edge, sourceNode, targetNode, model, diagramDirection, out double LabelX, out double LabelY))
        {
            return new Position(LabelX, LabelY);
        }

        if (TryResolveFlowchartDecisionBranchEdgeLabelPosition(edge, sourceNode, targetNode, model, out Position DecisionLabelPosition))
        {
            return DecisionLabelPosition;
        }
#pragma warning restore IDE0007

        return edge.LabelPosition;
    }

    /// <summary>
    /// F29/F53: label at half polyline distance along the same 3 guided points as <see cref="BuildFlowchartVerticalDecisionBranchOutwardCurveBasisPath"/>.
    /// </summary>
    static bool TryResolveFlowchartDecisionBranchEdgeLabelPosition(
        Edge edge,
        Node? sourceNode,
        Node? targetNode,
        FlowchartModel? model,
        out Position labelPosition)
    {
#pragma warning disable IDE0007
        labelPosition = default;

        List<Position> LineData = FilterValidPoints(edge.Points);
        if (!ShouldUseDecisionBranchGuidedCurve(edge, sourceNode, model, LineData) || LineData.Count < 2)
        {
            return false;
        }

        List<Position> Endpoints = new List<Position> { LineData[0], LineData[^1] };
        (string? ArrowTypeStart, string? ArrowTypeEnd) = GetMermaidArrowTypes(edge.Type);
        if (model is not null
            && sourceNode is not null
            && MermaidSharp.Layout.CoordinateAssignment.TryGetFlowchartHorizontalDecisionBranchEndpoints(
                model,
                edge,
                out Position HorizontalSource,
                out Position HorizontalTarget,
                out MermaidSharp.Layout.FlowchartDecisionBranchExitRole _))
        {
            Endpoints[0] = HorizontalSource;
            Endpoints[1] = HorizontalTarget;
            List<Position> OffsetHorizontalEndpoints = ApplyLineWithOffset(Endpoints, ArrowTypeStart, ArrowTypeEnd);
            if (OffsetHorizontalEndpoints.Count >= 2)
            {
                List<Position> HorizontalGuidedPoints = BuildFlowchartHorizontalOutwardCurveGuidedPoints(
                    OffsetHorizontalEndpoints[0],
                    OffsetHorizontalEndpoints[^1],
                    sourceNode.Position.Y,
                    FlowchartOutwardCurveSpreadAnchor.TargetLeg);
                labelPosition = ComputeFlowchartPolylineHalfDistancePosition(HorizontalGuidedPoints);
                return true;
            }
        }

        if (model is not null
            && targetNode is not null
            && MermaidSharp.Layout.CoordinateAssignment.TryGetFlowchartVerticalDecisionBranchEndpoints(
                model,
                edge,
                out Position BranchSource,
                out Position BranchTarget,
                out MermaidSharp.Layout.FlowchartDecisionBranchExitRole _))
        {
            Endpoints[0] = BranchSource;
            Endpoints[1] = BranchTarget;
        }

        List<Position> OffsetEndpoints = ApplyLineWithOffset(Endpoints, ArrowTypeStart, ArrowTypeEnd);
        if (OffsetEndpoints.Count < 2 || sourceNode is null)
        {
            return false;
        }

        List<Position> GuidedPoints = BuildFlowchartVerticalOutwardCurveGuidedPoints(
            OffsetEndpoints[0],
            OffsetEndpoints[^1],
            sourceNode.Position.X,
            FlowchartOutwardCurveSpreadAnchor.TargetLeg);

        labelPosition = ComputeFlowchartPolylineHalfDistancePosition(GuidedPoints);
        return true;
#pragma warning restore IDE0007
    }

    static Position ComputeFlowchartPolylineHalfDistancePosition(IReadOnlyList<Position> Points)
    {
#pragma warning disable IDE0007
        if (Points.Count == 0)
        {
            return Position.Zero;
        }

        if (Points.Count == 1)
        {
            return Points[0];
        }

        double TotalDistance = 0.0;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            Position PreviousPoint = Points[Index - 1];
            Position Point = Points[Index];
            double Dx = Point.X - PreviousPoint.X;
            double Dy = Point.Y - PreviousPoint.Y;
            TotalDistance += Math.Sqrt((Dx * Dx) + (Dy * Dy));
        }

        double RemainingDistance = TotalDistance / 2.0;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            Position PreviousPoint = Points[Index - 1];
            Position Point = Points[Index];
            double Dx = Point.X - PreviousPoint.X;
            double Dy = Point.Y - PreviousPoint.Y;
            double SegmentDistance = Math.Sqrt((Dx * Dx) + (Dy * Dy));
            if (SegmentDistance <= 0.0000001)
            {
                continue;
            }

            if (SegmentDistance < RemainingDistance)
            {
                RemainingDistance -= SegmentDistance;
                continue;
            }

            double DistanceRatio = RemainingDistance / SegmentDistance;
            if (DistanceRatio <= 0.0)
            {
                return PreviousPoint;
            }

            if (DistanceRatio >= 1.0)
            {
                return Point;
            }

            double X = Math.Round(((1.0 - DistanceRatio) * PreviousPoint.X) + (DistanceRatio * Point.X), 5);
            double Y = Math.Round(((1.0 - DistanceRatio) * PreviousPoint.Y) + (DistanceRatio * Point.Y), 5);
            return new Position(X, Y);
        }

        return Points[^1];
#pragma warning restore IDE0007
    }

    static bool TryResolveFlowchartLrSideFanoutEdgeLabelPosition(
        Edge edge,
        Node? sourceNode,
        Node? targetNode,
        FlowchartModel? model,
        Direction diagramDirection,
        out double LabelX,
        out double LabelY)
    {
#pragma warning disable IDE0007
        LabelX = 0.0;
        LabelY = 0.0;

        if (model is null
            || sourceNode is null
            || targetNode is null
            || !ShouldUseLrLabelledRectFanoutGuidedCurve(edge, sourceNode, model, diagramDirection, edge.Points))
        {
            return false;
        }

        // Mermaid Live: label at horizontal mid-span between node centres, vertically aligned with target row.
        LabelX = Math.Round((sourceNode.Position.X + targetNode.Position.X) / 2.0, 2);
        LabelY = Math.Round(targetNode.Position.Y, 2);
        return true;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// LR labelled rectangle fan-out (F24): Mermaid uses a single smooth curveBasis segment per branch.
    /// </summary>
    static bool ShouldUseLrLabelledRectFanoutGuidedCurve(
        Edge edge,
        Node? sourceNode,
        FlowchartModel? model,
        Direction diagramDirection,
        IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        return !string.IsNullOrWhiteSpace(edge.Label)
            && ShouldUseLrSideFanoutGuidedCurve(edge, sourceNode, model, diagramDirection, lineData);
#pragma warning restore IDE0007
    }

    /// <summary>
    /// LR rectangle side fan-out (F33): upper/centre/lower ports on the source right face (perimeter anchors) + curveBasis S-route.
    /// </summary>
    static bool ShouldUseLrSideFanoutGuidedCurve(
        Edge edge,
        Node? sourceNode,
        FlowchartModel? model,
        Direction diagramDirection,
        IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        if (model is null || sourceNode is null || lineData.Count < 2)
        {
            return false;
        }

        if (diagramDirection is not Direction.LeftToRight and not Direction.RightToLeft)
        {
            return false;
        }

        if (sourceNode.Shape == NodeShape.Diamond)
        {
            return false;
        }

        if (!string.Equals(edge.SourceId, sourceNode.Id, StringComparison.Ordinal))
        {
            return false;
        }

        int OutgoingCount = 0;
        bool AllTargetsToSide = true;
        for (int i = 0; i < model.Edges.Count; i++)
        {
            if (!string.Equals(model.Edges[i].SourceId, sourceNode.Id, StringComparison.Ordinal))
            {
                continue;
            }

            OutgoingCount++;
            MermaidSharp.Models.Node? BranchTarget = model.GetNode(model.Edges[i].TargetId);
            if (BranchTarget is null)
            {
                AllTargetsToSide = false;
                break;
            }

            if (diagramDirection == Direction.LeftToRight)
            {
                if (BranchTarget.Position.X <= sourceNode.Position.X + 0.01)
                {
                    AllTargetsToSide = false;
                    break;
                }
            }
            else
            {
                if (BranchTarget.Position.X >= sourceNode.Position.X - 0.01)
                {
                    AllTargetsToSide = false;
                    break;
                }
            }
        }

        return OutgoingCount >= 2 && OutgoingCount <= 3 && AllTargetsToSide;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// LR rectangle side fan-in (F33): upper/lower entries on the target face, outward curveBasis per branch.
    /// </summary>
    static bool ShouldUseLrSideFanInGuidedCurve(
        Edge edge,
        Node? targetNode,
        FlowchartModel? model,
        Direction diagramDirection,
        IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        if (model is null || targetNode is null || lineData.Count < 2)
        {
            return false;
        }

        if (diagramDirection is not Direction.LeftToRight and not Direction.RightToLeft)
        {
            return false;
        }

        if (targetNode.Shape == NodeShape.Diamond)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(edge.Label))
        {
            return false;
        }

        if (!string.Equals(edge.TargetId, targetNode.Id, StringComparison.Ordinal))
        {
            return false;
        }

        int IncomingCount = 0;
        bool AllSourcesFromSide = true;
        for (int i = 0; i < model.Edges.Count; i++)
        {
            if (!string.Equals(model.Edges[i].TargetId, targetNode.Id, StringComparison.Ordinal))
            {
                continue;
            }

            IncomingCount++;
            MermaidSharp.Models.Node? BranchSource = model.GetNode(model.Edges[i].SourceId);
            if (BranchSource is null)
            {
                AllSourcesFromSide = false;
                break;
            }

            if (diagramDirection == Direction.LeftToRight)
            {
                if (BranchSource.Position.X >= targetNode.Position.X - 0.01)
                {
                    AllSourcesFromSide = false;
                    break;
                }
            }
            else
            {
                if (BranchSource.Position.X <= targetNode.Position.X + 0.01)
                {
                    AllSourcesFromSide = false;
                    break;
                }
            }
        }

        return IncomingCount >= 2 && IncomingCount <= 3 && AllSourcesFromSide;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F37/F38/F40/F43 cross-subgraph links: orthogonal stubs + curved S-route (90° to joined face).
    /// </summary>
    static bool ShouldUseFlowchartCrossBoundarySCurve(
        Edge edge,
        Node? sourceNode,
        Node? targetNode,
        FlowchartModel? model,
        IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        if (model is null || sourceNode is null || targetNode is null || lineData.Count < 2)
        {
            return false;
        }

        if (model.Subgraphs.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(edge.Label))
        {
            return false;
        }

        if (sourceNode.Shape == NodeShape.Diamond || targetNode.Shape == NodeShape.Diamond)
        {
            return false;
        }

        if (!MermaidSharp.Layout.CoordinateAssignment.IsFlowchartCrossSubgraphBoundaryEdge(sourceNode, targetNode))
        {
            return false;
        }

        Position P0 = lineData[0];
        Position P3 = lineData[lineData.Count - 1];
        return Math.Abs(P3.X - P0.X) > 0.75 && Math.Abs(P3.Y - P0.Y) > 0.75;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F53: two branches into a stadium/pill — vertical stem then curveBasis into left/right shoulders (not outward fan-out bow).
    /// </summary>
    static bool ShouldUseTbStadiumMergeFanInGuidedCurve(
        Edge edge,
        Node? targetNode,
        FlowchartModel? model,
        Direction diagramDirection,
        IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        if (model is null || targetNode is null || lineData.Count < 2)
        {
            return false;
        }

        if (diagramDirection is not Direction.TopToBottom and not Direction.BottomToTop)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(edge.Label))
        {
            return false;
        }

        if (!MermaidSharp.Layout.CoordinateAssignment.IsFlowchartTbStadiumDualFanInMergeTarget(model, targetNode.Id))
        {
            return false;
        }

        Node? SourceNode = model.GetNode(edge.SourceId);
        if (SourceNode is null || SourceNode.Position.Y > targetNode.Position.Y - 0.01)
        {
            return false;
        }

        return true;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// F58: multi-branch terminal collector into a stadium — curveBasis on margin or shoulder guided polylines.
    /// </summary>
    static bool ShouldUseTbTerminalCollectorGuidedCurve(
        Edge edge,
        Node? targetNode,
        FlowchartModel? model,
        Direction diagramDirection,
        IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        if (model is null || targetNode is null || lineData.Count < 2)
        {
            return false;
        }

        if (diagramDirection is not Direction.TopToBottom and not Direction.BottomToTop)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(edge.Label))
        {
            return false;
        }

        if (!MermaidSharp.Layout.CoordinateAssignment.IsFlowchartTbStadiumTerminalCollectorTarget(model, targetNode.Id))
        {
            return false;
        }

        Node? SourceNode = model.GetNode(edge.SourceId);
        if (SourceNode is null || SourceNode.Position.Y > targetNode.Position.Y - 0.01)
        {
            return false;
        }

        return true;
#pragma warning restore IDE0007
    }

    static bool ShouldUseHubFanGuidedCurve(
        Edge edge,
        Node? sourceNode,
        Node? targetNode,
        FlowchartModel? model,
        Direction diagramDirection,
        IReadOnlyList<Position> lineData)
    {
#pragma warning disable IDE0007
        if (model is null || sourceNode is null || targetNode is null || lineData.Count < 2)
        {
            return false;
        }

        if (sourceNode.Shape == NodeShape.Diamond)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(edge.Label))
        {
            return false;
        }

        if (diagramDirection is not Direction.TopToBottom and not Direction.BottomToTop)
        {
            return false;
        }

        bool BottomToTop = diagramDirection == Direction.BottomToTop;
        int outgoingCount = 0;
        bool allTargetsOnFanOutSide = true;
        for (int i = 0; i < model.Edges.Count; i++)
        {
            if (string.Equals(model.Edges[i].SourceId, sourceNode.Id, StringComparison.Ordinal))
            {
                outgoingCount++;
                MermaidSharp.Models.Node? branchTarget = model.GetNode(model.Edges[i].TargetId);
                if (branchTarget is null)
                {
                    continue;
                }

                if (BottomToTop)
                {
                    if (branchTarget.Position.Y >= sourceNode.Position.Y - 0.01)
                    {
                        allTargetsOnFanOutSide = false;
                    }
                }
                else if (branchTarget.Position.Y <= sourceNode.Position.Y + 0.01)
                {
                    allTargetsOnFanOutSide = false;
                }
            }
        }

        if (outgoingCount >= 2 && outgoingCount <= 3 && allTargetsOnFanOutSide
            && string.Equals(edge.SourceId, sourceNode.Id, StringComparison.Ordinal))
        {
            return true;
        }

        int incomingCount = 0;
        bool allSourcesOnFanInSide = true;
        for (int i = 0; i < model.Edges.Count; i++)
        {
            if (string.Equals(model.Edges[i].TargetId, targetNode.Id, StringComparison.Ordinal))
            {
                incomingCount++;
                MermaidSharp.Models.Node? branchSource = model.GetNode(model.Edges[i].SourceId);
                if (branchSource is null)
                {
                    continue;
                }

                if (BottomToTop)
                {
                    if (branchSource.Position.Y <= targetNode.Position.Y + 0.01)
                    {
                        allSourcesOnFanInSide = false;
                    }
                }
                else if (branchSource.Position.Y >= targetNode.Position.Y - 0.01)
                {
                    allSourcesOnFanInSide = false;
                }
            }
        }

        if (incomingCount >= 2 && incomingCount <= 3 && allSourcesOnFanInSide
            && string.Equals(edge.TargetId, targetNode.Id, StringComparison.Ordinal))
        {
            if (MermaidSharp.Layout.CoordinateAssignment.IsFlowchartTbStadiumDualFanInMergeTarget(model, targetNode.Id))
            {
                return false;
            }

            return true;
        }

        return false;
#pragma warning restore IDE0007
    }

    // Exact d3.curveBasis conversion (see https://raw.githubusercontent.com/d3/d3-shape/main/src/curve/basis.js)
    static string BuildFlowchartCurveBasisPath(IReadOnlyList<Position> points)
    {
#pragma warning disable IDE0007
        if (points.Count < 2)
        {
            return string.Empty;
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

    /// <summary>
    /// Estimates the HTML label bounding box used by Mermaid dagre-wrapper before padding is applied.
    /// </summary>
    static Size MeasureFlowchartLabelBBox(string text, double fontSize)
    {
#pragma warning disable IDE0007
        if (string.IsNullOrEmpty(text))
        {
            double EmptyLineBoxHeightPx = fontSize * FLOWCHART_HTML_LABEL_LINE_BOX_HEIGHT_EM;
            return new Size(0, EmptyLineBoxHeightPx);
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

        double CharWidthEm = MaxLineLength <= 5
            ? FLOWCHART_LABEL_CHAR_WIDTH_EM_SHORT
            : FLOWCHART_LABEL_CHAR_WIDTH_EM_LONG;
        double InnerWidthPx = MaxLineLength * fontSize * CharWidthEm;
        double SingleLineBoxHeightPx = fontSize * FLOWCHART_HTML_LABEL_LINE_BOX_HEIGHT_EM;
        double InnerHeightPx = Lines.Length == 1
            ? SingleLineBoxHeightPx
            : Lines.Length * fontSize * 1.5;

        if (MaxLineLength == 1)
        {
            InnerWidthPx = SingleLineBoxHeightPx;
        }
        else if (MaxLineLength <= FLOWCHART_LABEL_SHORT_WORD_MAX_LENGTH
            && InnerWidthPx < SingleLineBoxHeightPx * FLOWCHART_LABEL_MIN_WIDTH_TO_LINE_BOX_RATIO)
        {
            InnerWidthPx = SingleLineBoxHeightPx * FLOWCHART_LABEL_MIN_WIDTH_TO_LINE_BOX_RATIO;
        }

        if (Lines.Length == 1)
        {
            double BrowserStyleMinWidthPx = SingleLineBoxHeightPx * FLOWCHART_HTML_LABEL_BBOX_WIDTH_TO_LINE_BOX_RATIO;
            if (InnerWidthPx < BrowserStyleMinWidthPx)
            {
                InnerWidthPx = BrowserStyleMinWidthPx;
            }
        }

        return new Size(InnerWidthPx, InnerHeightPx);
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Stadium label width without squareRect line-box min-width inflation (mermaid <c>stadium.ts</c> bbox.width).
    /// </summary>
    static double MeasureFlowchartStadiumInnerLabelWidth(string Text, double FontSize)
    {
#pragma warning disable IDE0007
        if (string.IsNullOrEmpty(Text))
        {
            return FontSize * 1.5 * FLOWCHART_STADIUM_LABEL_BBOX_WIDTH_TO_LINE_HEIGHT_RATIO;
        }

        string[] Lines = Text.Split('\n');
        int MaxLineLength = 0;
        foreach (string Line in Lines)
        {
            int LineLength = Line.Length;
            if (LineLength > MaxLineLength)
            {
                MaxLineLength = LineLength;
            }
        }

        double CharWidthEm = MaxLineLength <= 5
            ? FLOWCHART_LABEL_CHAR_WIDTH_EM_SHORT
            : FLOWCHART_LABEL_CHAR_WIDTH_EM_LONG;
        double LineHeightPx = FontSize * 1.5;
        double InnerWidthPx = MaxLineLength * FontSize * CharWidthEm;

        if (MaxLineLength <= FLOWCHART_LABEL_SHORT_WORD_MAX_LENGTH
            && InnerWidthPx < LineHeightPx * FLOWCHART_LABEL_MIN_WIDTH_TO_LINE_BOX_RATIO)
        {
            InnerWidthPx = LineHeightPx * FLOWCHART_LABEL_MIN_WIDTH_TO_LINE_BOX_RATIO;
        }

        double BrowserStyleMinWidthPx = LineHeightPx * FLOWCHART_STADIUM_LABEL_BBOX_WIDTH_TO_LINE_HEIGHT_RATIO;
        if (InnerWidthPx < BrowserStyleMinWidthPx)
        {
            InnerWidthPx = BrowserStyleMinWidthPx;
        }

        return InnerWidthPx;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Stadium label height: mermaid HTML label uses line-height 1.5 (not the 2.5em squareRect line box).
    /// </summary>
    static double MeasureFlowchartStadiumInnerLabelHeight(string Text, double FontSize)
    {
#pragma warning disable IDE0007
        if (string.IsNullOrEmpty(Text))
        {
            return FontSize * 1.5;
        }

        string[] Lines = Text.Split('\n');
        if (Lines.Length == 1)
        {
            return FontSize * 1.5;
        }

        return Lines.Length * FontSize * 1.5;
#pragma warning restore IDE0007
    }

    /// <summary>
    /// Label bbox for hexagon layout. Uses dagre-wrapper <c>hexagon()</c> sizing; width is capped relative to height
    /// because bevel <c>m = h/4</c> couples horizontal extent to vertical padding.
    /// </summary>
    static Size MeasureFlowchartHexagonLabelBBox(string Text, double FontSize)
    {
#pragma warning disable IDE0007
        if (string.IsNullOrEmpty(Text))
        {
            double EmptyLineBoxHeightPx = FontSize * FLOWCHART_HEXAGON_LINE_BOX_HEIGHT_EM;
            return new Size(0, EmptyLineBoxHeightPx);
        }

        string[] Lines = Text.Split('\n');
        int MaxLineLength = 0;
        foreach (string Line in Lines)
        {
            int LineLength = Line.Length;
            if (LineLength > MaxLineLength)
            {
                MaxLineLength = LineLength;
            }
        }

        double LineBoxHeightPx = FontSize * FLOWCHART_HEXAGON_LINE_BOX_HEIGHT_EM;
        double InnerHeightPx = Lines.Length == 1
            ? LineBoxHeightPx
            : Lines.Length * FontSize * 1.5;

        double InnerWidthPx;
        if (MaxLineLength == 1)
        {
            InnerWidthPx = LineBoxHeightPx;
        }
        else
        {
            double CharWidthEm = MaxLineLength <= FLOWCHART_LABEL_SHORT_WORD_MAX_LENGTH
                ? FLOWCHART_LABEL_CHAR_WIDTH_EM_SHORT
                : FLOWCHART_HEXAGON_LABEL_CHAR_WIDTH_EM;
            InnerWidthPx = MaxLineLength * FontSize * CharWidthEm;
            double MaxInnerWidthPx = LineBoxHeightPx * FLOWCHART_HEXAGON_MAX_LABEL_WIDTH_TO_LINE_BOX_RATIO;
            if (InnerWidthPx > MaxInnerWidthPx)
            {
                InnerWidthPx = MaxInnerWidthPx;
            }
        }

        return new Size(InnerWidthPx, InnerHeightPx);
#pragma warning restore IDE0007
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Renders an analysed enterprise document to SVG using explicit paths (no markers or foreignObject).
/// </summary>
public static partial class EnterpriseSvgRenderer
{
    internal static Action<string, double>? StageTimingObserver { get; set; }
    internal static Action<string>? RenderedSvgObserver { get; set; }
    private const double FONT_SIZE = 12;

    private const double LINE_HEIGHT = 15;

    private const int GEOMETRY_CACHE_CAPACITY = 8;

    private static readonly object _GeometryCacheGate = new object();

    private static readonly Dictionary<string, string> _GeometryCacheByKey = new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly LinkedList<string> _GeometryCacheOrder = new LinkedList<string>();

    /// <summary>
    /// Renders SVG for a document that has already been validated (hidden-by-nesting flags applied).
    /// </summary>
    /// <param name="Document">Parsed and validated document.</param>
    /// <returns>SVG XML.</returns>
    public static string Render(EnterpriseDocument Document)
        => Render(Document, FlowchartRenderContext.Current.LayoutOptions);

    private static string RenderCore(EnterpriseDocument Document, DiagramLayoutOptions LayoutOptions)
    {
        if (Document == null)
        {
            throw new ArgumentNullException(nameof(Document));
        }

        string GeometryKey = GeometrySignature(Document) + LayoutOptions.GeometryCacheKey;
        string? CachedUnstyled;
        lock (_GeometryCacheGate)
        {
            if (_GeometryCacheByKey.TryGetValue(GeometryKey, out string? Hit))
            {
                CachedUnstyled = Hit;
                _GeometryCacheOrder.Remove(GeometryKey);
                _GeometryCacheOrder.AddLast(GeometryKey);
            }
            else
            {
                CachedUnstyled = null;
            }
        }

        if (CachedUnstyled != null)
        {
            return Finish(Document, CachedUnstyled);
        }

        EnterpriseLayoutResult Layout = Measure("enterprise-layout", () => EnterpriseLayoutEngine.Layout(Document));
        EnterpriseFlowchartRoutingContext? RoutingContext = Layout.RoutingContext;
        List<EnterpriseRoutedEdge> RoutedEdges = Measure("enterprise-edge-routing", () => RoutingContext == null
            ? EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes)
            : EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes, RoutingContext));
        Dictionary<string, EnterpriseJunctionCaptionLayout.Caption> Captions = EnterpriseJunctionCaptionLayout.Place(Layout.Nodes, RoutedEdges);
        double TitleHeight = string.IsNullOrEmpty(Document.Title) ? 0 : 30;
        double Width = Math.Ceiling(Math.Max(Layout.Width, 40));
        double Height = Math.Ceiling(Math.Max(Layout.Height, 40) + TitleHeight);
        ExpandForRoutedEdges(RoutedEdges, ref Width, ref Height, TitleHeight);
        foreach (EnterpriseJunctionCaptionLayout.Caption Caption in Captions.Values)
        {
            Width = Math.Max(Width, Caption.X + Caption.Width + 20);
            Height = Math.Max(Height, Caption.Y + Caption.Height + TitleHeight + 20);
        }
        double DiagramWidth = Width;
        if (!string.IsNullOrEmpty(Document.Title))
        {
            // Reserve the literal bold title, including punctuation and Unicode.
            // Do not feed it through the inline-Markdown label estimator.
            double TitleWidth = 0;
            foreach (Rune Glyph in Document.Title.EnumerateRunes())
            {
                double Em = Glyph.Value == ' ' ? 0.35
                    : Glyph.Value is 'i' or 'l' or 'I' ? 0.4
                    : Glyph.Value is 'W' or 'M' or '@' || Glyph.Value > 127 ? 1.0 : 0.75;
                TitleWidth += 15 * Em;
            }
            Width = Math.Ceiling(Math.Max(Width, TitleWidth + 40));
        }
        Stopwatch SvgBuildTimer = Stopwatch.StartNew();
        StageTimingObserver?.Invoke("enterprise-svg-build", -1);
        StringBuilder Builder = new StringBuilder();
        Builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"")
            .Append(EnterpriseSvgFormat.Number(Width))
            .Append("\" height=\"")
            .Append(EnterpriseSvgFormat.Number(Height))
            .Append("\" viewBox=\"0 0 ")
            .Append(EnterpriseSvgFormat.Number(Width))
            .Append(' ')
            .Append(EnterpriseSvgFormat.Number(Height))
            .Append("\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"")
            .Append(EnterpriseSvgFormat.Number(FONT_SIZE))
            .Append("\">");
        if (!string.IsNullOrWhiteSpace(Document.AccessibilityTitle))
        {
            Builder.Append("<title>").Append(EnterpriseSvgFormat.Escape(Document.AccessibilityTitle)).Append("</title>");
        }

        if (!string.IsNullOrWhiteSpace(Document.AccessibilityDescription))
        {
            Builder.Append("<desc>").Append(EnterpriseSvgFormat.Escape(Document.AccessibilityDescription)).Append("</desc>");
        }

        Builder.Append("<rect x=\"0\" y=\"0\" width=\"")
            .Append(EnterpriseSvgFormat.Number(Width))
            .Append("\" height=\"")
            .Append(EnterpriseSvgFormat.Number(Height))
            .Append("\" fill=\"#ffffff\"/>");
        if (!string.IsNullOrEmpty(Document.Title))
        {
            Builder.Append("<text x=\"")
                .Append(EnterpriseSvgFormat.Number(Width / 2.0))
                .Append("\" y=\"20\" text-anchor=\"middle\" font-size=\"15\" font-weight=\"bold\">")
                .Append(EnterpriseSvgFormat.Escape(Document.Title))
                .Append("</text>");
        }

        Builder.Append("<g transform=\"translate(")
            .Append(EnterpriseSvgFormat.Number((Width - DiagramWidth) / 2.0))
            .Append(',')
            .Append(EnterpriseSvgFormat.Number(TitleHeight))
            .Append(")\">");

        List<EnterpriseLayoutNode> Ordered = new List<EnterpriseLayoutNode>(Layout.Nodes.Values);
        Ordered.Sort((Left, Right) => Depth(Left, Layout.Nodes).CompareTo(Depth(Right, Layout.Nodes)));
        foreach (EnterpriseLayoutNode Node in Ordered)
        {
            if (Node.IsGroup)
            {
                AppendGroup(Builder, Node);
            }
            else if (Node.Note != null)
            {
                AppendNote(Builder, Node);
            }
            else
            {
                Captions.TryGetValue(Node.Id, out EnterpriseJunctionCaptionLayout.Caption? Caption);
                AppendElement(Builder, Node, Caption);
            }
        }

        foreach (EnterpriseRoutedEdge Routed in RoutedEdges)
        {
            EnterpriseFlowchartEdgeRouter.Append(Builder, Routed);
        }

        Builder.Append("</g></svg>");
        SvgBuildTimer.Stop();
        StageTimingObserver?.Invoke("enterprise-svg-build", SvgBuildTimer.Elapsed.TotalMilliseconds);
        string Unstyled = FlowchartSvgLineJump.Apply(Builder.ToString());
        lock (_GeometryCacheGate)
        {
            if (_GeometryCacheByKey.ContainsKey(GeometryKey))
            {
                _GeometryCacheByKey[GeometryKey] = Unstyled;
                _GeometryCacheOrder.Remove(GeometryKey);
                _GeometryCacheOrder.AddLast(GeometryKey);
            }
            else
            {
                _GeometryCacheByKey[GeometryKey] = Unstyled;
                _GeometryCacheOrder.AddLast(GeometryKey);
                while (_GeometryCacheOrder.Count > GEOMETRY_CACHE_CAPACITY)
                {
                    string EvictKey = _GeometryCacheOrder.First!.Value;
                    _GeometryCacheOrder.RemoveFirst();
                    _GeometryCacheByKey.Remove(EvictKey);
                }
            }
        }

        return Finish(Document, Unstyled);
    }

    /// <summary>
    /// Applies styles to a geometry drawing. A later edit that only changes colour reuses that drawing.
    /// </summary>
    /// <param name="Document">Document whose styles should be painted.</param>
    /// <param name="Unstyled">SVG before element and line styles are applied.</param>
    /// <returns>SVG ready to show.</returns>
    private static string Finish(EnterpriseDocument Document, string Unstyled)
    {
        string FinalSvg = Measure("enterprise-svg-postprocess", () => EnterpriseSvgConfiguration.Apply(EnterpriseMarkdownSvg.Apply(
            EnterpriseStyleSvg.Apply(Unstyled, Document), Document), Document));
        RenderedSvgObserver?.Invoke(FinalSvg);
        return FinalSvg;
    }

    /// <summary>
    /// Describes everything that changes where nodes and lines are drawn. Styles are left out.
    /// </summary>
    /// <param name="Document">Parsed diagram.</param>
    /// <returns>A signature that stays the same when only colours change.</returns>
    private static string GeometrySignature(EnterpriseDocument Document)
    {
        StringBuilder Key = new StringBuilder();
        Key.Append(Document.LayoutMode).Append('\u001f').Append(Document.Version).Append('\u001f');
        Key.Append(Document.Direction).Append('\u001f').Append(Document.Title).Append('\u001f');
        Key.Append(Document.AccessibilityTitle).Append('\u001f').Append(Document.AccessibilityDescription).Append('\u001f');
        foreach (KeyValuePair<string, string> Setting in Document.Configuration.OrderBy(Pair => Pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            Key.Append(Setting.Key).Append('=').Append(Setting.Value).Append('\u001f');
        }

        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            Key.Append("e|").Append(Element.Id).Append('|').Append(Element.ElementType).Append('|').Append(Element.Label);
            Key.Append('|').Append(Element.IsMarkdownLabel).Append('|').Append(Element.ParentId).Append('|').Append(Element.JunctionKind).Append('|');
            AppendPlacement(Key, Element.PlacementHint);
        }

        foreach (EnterpriseGroup Group in Document.Groups.Values)
        {
            Key.Append("g|").Append(Group.Id).Append('|').Append(Group.Label).Append('|').Append(Group.IsMarkdownLabel);
            Key.Append('|').Append(Group.ParentId).Append('|').Append(Group.Direction).Append('|');
            AppendPlacement(Key, Group.PlacementHint);
        }

        foreach (EnterpriseNote Note in Document.Notes.Values)
        {
            Key.Append("n|").Append(Note.Id).Append('|').Append(Note.Label).Append('|').Append(Note.IsMarkdownLabel);
            Key.Append('|').Append(Note.ParentId).Append('|');
            AppendPlacement(Key, Note.PlacementHint);
        }

        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            Key.Append("r|").Append(Relationship.SourceId).Append('|').Append(Relationship.TargetId).Append('|').Append(Relationship.Kind);
            Key.Append('|').Append(Relationship.Label).Append('|').Append(Relationship.IsMarkdownLabel).Append('|').Append(Relationship.AccessKind);
            Key.Append('|').Append(Relationship.InfluenceStrength).Append('|').Append(Relationship.DirectedAssociation);
            Key.Append('|').Append(Relationship.HiddenByNesting).Append('|').Append(Relationship.MinLength).Append('|');
            AppendRouting(Key, Relationship.RoutingHint);
        }

        foreach (EnterprisePlainConnection Connection in Document.PlainConnections)
        {
            Key.Append("p|").Append(Connection.SourceId).Append('|').Append(Connection.TargetId).Append('|').Append(Connection.MinLength).Append('|');
            AppendRouting(Key, Connection.RoutingHint);
        }

        return Key.ToString();
    }

    private static void AppendPlacement(StringBuilder Key, MermaidRecognition.Contract.PlacementHintContract? Hint)
    {
        if (Hint == null)
        {
            Key.Append('-').Append('\u001f');
            return;
        }

        Key.Append(Hint.Kind).Append('|').Append(Hint.PeerId).Append('|').Append(Hint.Offset).Append('|');
        Key.Append(Hint.Direction).Append('|').Append(Hint.IsActive).Append('\u001f');
    }

    private static void AppendRouting(StringBuilder Key, MermaidRecognition.Contract.RoutingHintContract? Hint)
    {
        if (Hint == null)
        {
            Key.Append('-').Append('\u001f');
            return;
        }

        Key.Append(Hint.Leave).Append('|').Append(Hint.Enter).Append('|').Append(Hint.Travel).Append('|');
        Key.Append(Hint.IsActive).Append('\u001f');
    }

    private static T Measure<T>(string Name, Func<T> Operation)
    {
        Action<string, double>? Observer = StageTimingObserver;
        if (Observer == null)
        {
            return Operation();
        }

        Observer(Name, -1);
        Stopwatch Timer = Stopwatch.StartNew();
        try { return Operation(); }
        finally
        {
            Timer.Stop();
            Observer(Name, Timer.Elapsed.TotalMilliseconds);
        }
    }

    private static void AppendNote(StringBuilder Builder, EnterpriseLayoutNode Node)
    {
        double CentreX = Node.AbsoluteX + (Node.Width / 2.0);
        double Top = Node.AbsoluteY + (Node.Height / 2.0)
            - (((Node.LabelLines.Count - 1) * LINE_HEIGHT) / 2.0) + 4;
        Builder.Append("<g class=\"note\" data-id=\"")
            .Append(EnterpriseSvgFormat.Escape(Node.Id))
            .Append("\">")
            .Append(EnterpriseNotationSvg.NoteBoxSvg(Node.AbsoluteX, Node.AbsoluteY, Node.Width, Node.Height));
        for (int Index = 0; Index < Node.LabelLines.Count; Index++)
        {
            Builder.Append("<text x=\"")
                .Append(EnterpriseSvgFormat.Number(CentreX))
                .Append("\" y=\"")
                .Append(EnterpriseSvgFormat.Number(Top + (Index * LINE_HEIGHT)))
                .Append("\" text-anchor=\"middle\">")
                .Append(EnterpriseSvgFormat.Escape(Node.LabelLines[Index]))
                .Append("</text>");
        }
        Builder.Append("</g>");
    }

    private static void ExpandForRoutedEdges(
        List<EnterpriseRoutedEdge> RoutedEdges,
        ref double Width,
        ref double Height,
        double TitleHeight)
    {
        foreach (EnterpriseRoutedEdge Routed in RoutedEdges)
        {
            foreach ((double X, double Y) in Routed.SvgPoints)
            {
                Width = Math.Max(Width, X + 20);
                Height = Math.Max(Height, Y + TitleHeight + 20);
            }

            if (Routed.Label.Length > 0)
            {
                double LabelWidth = Routed.LabelWidth;
                Width = Math.Max(Width, Routed.LabelX + (LabelWidth / 2.0) + 8);
                Height = Math.Max(Height, Routed.LabelY + (Routed.LabelHeight / 2) + TitleHeight + 16);
            }
        }
    }

    private static int Depth(EnterpriseLayoutNode Node, Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        int Count = 0;
        string? ParentId = Node.ParentId;
        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrEmpty(ParentId) && Seen.Add(ParentId) && Nodes.TryGetValue(ParentId, out EnterpriseLayoutNode? Parent))
        {
            Count++;
            ParentId = Parent.ParentId;
        }

        return Count;
    }

    private static void AppendGroup(StringBuilder Builder, EnterpriseLayoutNode Node)
    {
        Builder.Append("<g class=\"group\" data-id=\"")
            .Append(EnterpriseSvgFormat.Escape(Node.Id))
            .Append("\">")
            .Append(EnterpriseNotationSvg.GroupingTabSvg(Node.AbsoluteX, Node.AbsoluteY, Node.Width, Node.Height, Node.DisplayLabel))
            .Append("</g>");
    }

    private static void AppendElement(StringBuilder Builder, EnterpriseLayoutNode Node,
        EnterpriseJunctionCaptionLayout.Caption? Caption)
    {
        EnterpriseElement Element = Node.Element!;
        double X = Node.AbsoluteX;
        double Y = Node.AbsoluteY;
        Builder.Append("<g class=\"element ")
            .Append(EnterpriseElementCatalog.GetKebabName(Element.ElementType))
            .Append("\" data-id=\"")
            .Append(EnterpriseSvgFormat.Escape(Element.Id))
            .Append("\">")
            .Append(EnterpriseNotationSvg.ElementBoxSvg(Element.ElementType, X, Y, Node.Width, Node.Height, Element.JunctionKind));

        if (Element.ElementType == EnterpriseElementType.Junction)
        {
            string JunctionLabel = Element.Label;
            if (Caption != null)
            {
                for (int Index = 0; Index < Node.LabelLines.Count; Index++)
                {
                Builder.Append("<text x=\"")
                    .Append(EnterpriseSvgFormat.Number(Caption.X))
                    .Append("\" y=\"")
                    .Append(EnterpriseSvgFormat.Number(Caption.Y + EnterpriseJunctionCaptionLayout.FontSize + (Index * LINE_HEIGHT)))
                    .Append("\" font-size=\"11\">")
                    .Append(EnterpriseSvgFormat.Escape(Node.LabelLines[Index]))
                    .Append("</text>");
                }
            }

            Builder.Append("</g>");
            return;
        }

        EnterpriseElementMetadata Metadata = EnterpriseElementCatalog.Elements[Element.ElementType];
        bool Is3d = Metadata.BoxKind == EnterpriseBoxKind.Box3d;
        bool IsObject = Metadata.BoxKind == EnterpriseBoxKind.Object;
        double CentreX = X + (Is3d ? (Node.Width - 30) / 2.0 : Node.Width / 2.0);
        double Top;
        if (Node.IsCompound)
        {
            Top = Y + 20;
        }
        else if (IsObject)
        {
            Top = Y + 12 + ((Node.Height - 12) / 2.0) - (((Node.LabelLines.Count - 1) * LINE_HEIGHT) / 2.0) + 4;
        }
        else
        {
            Top = Y + (Node.Height / 2.0) + (Is3d ? 4 : 0) - (((Node.LabelLines.Count - 1) * LINE_HEIGHT) / 2.0) + 4;
        }

        for (int Index = 0; Index < Node.LabelLines.Count; Index++)
        {
            Builder.Append("<text x=\"")
                .Append(EnterpriseSvgFormat.Number(CentreX))
                .Append("\" y=\"")
                .Append(EnterpriseSvgFormat.Number(Top + (Index * LINE_HEIGHT)))
                .Append("\" text-anchor=\"middle\">")
                .Append(EnterpriseSvgFormat.Escape(Node.LabelLines[Index]))
                .Append("</text>");
        }

        Builder.Append("</g>");
    }
}

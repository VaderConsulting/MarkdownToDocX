using System;
using System.Collections.Generic;
using System.Text;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Renders an analysed enterprise document to SVG using explicit paths (no markers or foreignObject).
/// </summary>
public static class EnterpriseSvgRenderer
{
    private const double FONT_SIZE = 12;

    private const double LINE_HEIGHT = 15;

    /// <summary>
    /// Renders SVG for a document that has already been validated (hidden-by-nesting flags applied).
    /// </summary>
    /// <param name="Document">Parsed and validated document.</param>
    /// <returns>SVG XML.</returns>
    public static string Render(EnterpriseDocument Document)
    {
        if (Document == null)
        {
            throw new ArgumentNullException(nameof(Document));
        }

        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        List<EnterpriseRoutedEdge> RoutedEdges = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        double TitleHeight = string.IsNullOrEmpty(Document.Title) ? 0 : 30;
        double MinX = 0;
        double MinY = 0;
        double MaxX = Layout.Width;
        double MaxY = Layout.Height;
        ExpandForRoutedEdges(RoutedEdges, ref MinX, ref MinY, ref MaxX, ref MaxY);
        double ShiftX = 10 - MinX;
        double ShiftY = 10 - MinY;
        double Width = Math.Ceiling(MaxX + ShiftX + 10);
        double Height = Math.Ceiling(MaxY + ShiftY + TitleHeight + 10);
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
            .Append(EnterpriseSvgFormat.Number(ShiftX))
            .Append(',')
            .Append(EnterpriseSvgFormat.Number(TitleHeight + ShiftY))
            .Append(")\">");

        List<EnterpriseLayoutNode> Ordered = new List<EnterpriseLayoutNode>(Layout.Nodes.Values);
        Ordered.Sort((Left, Right) => Depth(Left, Layout.Nodes).CompareTo(Depth(Right, Layout.Nodes)));
        foreach (EnterpriseLayoutNode Node in Ordered)
        {
            if (Node.IsGroup)
            {
                AppendGroup(Builder, Node);
            }
            else
            {
                AppendElement(Builder, Node);
            }
        }

        foreach (EnterpriseRoutedEdge Routed in RoutedEdges)
        {
            EnterpriseFlowchartEdgeRouter.Append(Builder, Routed);
        }

        Builder.Append("</g></svg>");
        return Builder.ToString();
    }

    private static void ExpandForRoutedEdges(
        List<EnterpriseRoutedEdge> RoutedEdges,
        ref double MinX,
        ref double MinY,
        ref double MaxX,
        ref double MaxY)
    {
        foreach (EnterpriseRoutedEdge Routed in RoutedEdges)
        {
            foreach ((double X, double Y) in Routed.SvgPoints)
            {
                MinX = Math.Min(MinX, X - 20);
                MinY = Math.Min(MinY, Y - 20);
                MaxX = Math.Max(MaxX, X + 20);
                MaxY = Math.Max(MaxY, Y + 20);
            }

            if (Routed.Label.Length > 0)
            {
                double LabelWidth = (Routed.Label.Length * 7.0) + 8;
                MinX = Math.Min(MinX, Routed.LabelX - (LabelWidth / 2.0));
                MaxX = Math.Max(MaxX, Routed.LabelX + (LabelWidth / 2.0));
                MinY = Math.Min(MinY, Routed.LabelY - 12);
                MaxY = Math.Max(MaxY, Routed.LabelY + 12);
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

    private static void AppendElement(StringBuilder Builder, EnterpriseLayoutNode Node)
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
            if (!string.IsNullOrEmpty(JunctionLabel) && !string.Equals(JunctionLabel, Element.Id, StringComparison.Ordinal))
            {
                Builder.Append("<text x=\"")
                    .Append(EnterpriseSvgFormat.Number(X + 30))
                    .Append("\" y=\"")
                    .Append(EnterpriseSvgFormat.Number(Y + 18))
                    .Append("\" font-size=\"11\">")
                    .Append(EnterpriseSvgFormat.Escape(JunctionLabel))
                    .Append("</text>");
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

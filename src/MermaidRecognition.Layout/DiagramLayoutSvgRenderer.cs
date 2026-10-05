using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Paints a <see cref="DiagramLayoutSolution"/> as SVG that the quality gate can parse.
/// </summary>
public static class DiagramLayoutSvgRenderer
{
    private const double ARROWHEAD_WIDTH_PX = 7.0;

    /// <summary>
    /// Renders <paramref name="Solution"/> with flowchart-style boxes and labelled edges.
    /// </summary>
    /// <param name="Contract">Recognised diagram, used for labels and membership.</param>
    /// <param name="Solution">SVG-space geometry.</param>
    /// <param name="Profile">Arrowhead and label options.</param>
    /// <returns>SVG markup.</returns>
    public static string RenderToSvg(
        DiagramContract Contract,
        DiagramLayoutSolution Solution,
        DiagramLayoutProfile Profile)
    {
        if (Contract == null || Solution == null || Solution.NodeBounds.Count == 0)
        {
            return string.Empty;
        }

        DiagramLayoutProfile EffectiveProfile = Profile ?? DiagramLayoutProfile.FlowchartCustom();
        Dictionary<string, string> NodeLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            NodeLabels[Node.Id] = string.IsNullOrWhiteSpace(Node.Label) ? Node.Id : Node.Label;
        }

        Dictionary<string, string> GroupLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            GroupLabels[Group.Id] = string.IsNullOrWhiteSpace(Group.Title) ? Group.Id : Group.Title;
        }

        double Width = Math.Max(1.0, Solution.Width);
        double Height = Math.Max(1.0, Solution.Height);
        StringBuilder Builder = new StringBuilder();
        Builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"");
        Builder.Append(Math.Ceiling(Width).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" height=\"");
        Builder.Append(Math.Ceiling(Height).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" viewBox=\"0 0 ");
        Builder.Append(Math.Ceiling(Width).ToString(CultureInfo.InvariantCulture));
        Builder.Append(' ');
        Builder.Append(Math.Ceiling(Height).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\">");
        Builder.Append("<rect x=\"0\" y=\"0\" width=\"");
        Builder.Append(Width.ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append("\" height=\"");
        Builder.Append(Height.ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append("\" fill=\"white\"/>");

        foreach (FlowchartLayoutNodeBounds Group in Solution.GroupBounds)
        {
            AppendBox(
                Builder,
                Group,
                GroupLabels.TryGetValue(Group.NodeId, out string? Title) ? Title : Group.NodeId,
                "#ffffde",
                "#aaaa33",
                IsGroup: true);
        }

        foreach (FlowchartLayoutNodeBounds Node in Solution.NodeBounds)
        {
            AppendBox(
                Builder,
                Node,
                NodeLabels.TryGetValue(Node.NodeId, out string? Label) ? Label : Node.NodeId,
                "#ECECFF",
                "#9370DB",
                IsGroup: false);
        }

        foreach (DiagramLayoutRoutedEdge Edge in Solution.Edges)
        {
            if (Edge.Samples.Count < 2)
            {
                continue;
            }

            Builder.Append("<path d=\"");
            Builder.Append(BuildPathData(Edge.Samples));
            Builder.Append("\" fill=\"none\" stroke=\"#333333\" stroke-width=\"1\" data-edge-source=\"");
            Builder.Append(Escape(Edge.SourceId));
            Builder.Append("\" data-edge-target=\"");
            Builder.Append(Escape(Edge.TargetId));
            Builder.Append("\"/>");
            AppendArrowhead(Builder, Edge.Samples, EffectiveProfile.ArrowheadLengthPx);
            if (!string.IsNullOrWhiteSpace(Edge.Label))
            {
                (double LabelX, double LabelY) = ResolveLabelPoint(Edge, EffectiveProfile.LabelOffsetPx);
                Builder.Append("<text x=\"");
                Builder.Append(LabelX.ToString("0.###", CultureInfo.InvariantCulture));
                Builder.Append("\" y=\"");
                Builder.Append(LabelY.ToString("0.###", CultureInfo.InvariantCulture));
                Builder.Append("\" text-anchor=\"middle\" fill=\"#333333\" font-family=\"trebuchet ms, verdana, arial, sans-serif\" font-size=\"12\">");
                Builder.Append(Escape(Edge.Label));
                Builder.Append("</text>");
            }
        }

        Builder.Append("</svg>");
        return Builder.ToString();
    }

    private static void AppendBox(
        StringBuilder Builder,
        FlowchartLayoutNodeBounds Bounds,
        string Label,
        string Fill,
        string Stroke,
        bool IsGroup)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Top = Bounds.CenterY - (Bounds.Height / 2.0);
        Builder.Append("<g data-id=\"");
        Builder.Append(Escape(Bounds.NodeId));
        Builder.Append("\">");
        Builder.Append("<rect x=\"");
        Builder.Append(Left.ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append("\" y=\"");
        Builder.Append(Top.ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append("\" width=\"");
        Builder.Append(Bounds.Width.ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append("\" height=\"");
        Builder.Append(Bounds.Height.ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append("\" fill=\"");
        Builder.Append(Fill);
        Builder.Append("\" stroke=\"");
        Builder.Append(Stroke);
        Builder.Append("\" stroke-width=\"1\"/>");
        Builder.Append("<text x=\"");
        Builder.Append(Bounds.CenterX.ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append("\" y=\"");
        Builder.Append((Top + (IsGroup ? 16.0 : (Bounds.Height / 2.0) + 4.0)).ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append("\" text-anchor=\"middle\" fill=\"#333\" font-family=\"trebuchet ms, verdana, arial, sans-serif\" font-size=\"16\">");
        Builder.Append(Escape(Label));
        Builder.Append("</text></g>");
    }

    private static string BuildPathData(IReadOnlyList<(double X, double Y)> Samples)
    {
        StringBuilder Path = new StringBuilder();
        Path.Append('M');
        Path.Append(Samples[0].X.ToString("0.###", CultureInfo.InvariantCulture));
        Path.Append(',');
        Path.Append(Samples[0].Y.ToString("0.###", CultureInfo.InvariantCulture));
        for (int Index = 1; Index < Samples.Count; Index++)
        {
            Path.Append('L');
            Path.Append(Samples[Index].X.ToString("0.###", CultureInfo.InvariantCulture));
            Path.Append(',');
            Path.Append(Samples[Index].Y.ToString("0.###", CultureInfo.InvariantCulture));
        }

        return Path.ToString();
    }

    private static void AppendArrowhead(
        StringBuilder Builder,
        IReadOnlyList<(double X, double Y)> Samples,
        double LengthPx)
    {
        (double TipX, double TipY) = Samples[Samples.Count - 1];
        (double PrevX, double PrevY) = Samples[Samples.Count - 2];
        double Dx = TipX - PrevX;
        double Dy = TipY - PrevY;
        double Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
        if (Length < 1e-6)
        {
            return;
        }

        double Ux = Dx / Length;
        double Uy = Dy / Length;
        double BaseX = TipX - (Ux * LengthPx);
        double BaseY = TipY - (Uy * LengthPx);
        double Px = -Uy * ARROWHEAD_WIDTH_PX;
        double Py = Ux * ARROWHEAD_WIDTH_PX;
        Builder.Append("<polygon points=\"");
        Builder.Append(TipX.ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append(',');
        Builder.Append(TipY.ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append(' ');
        Builder.Append((BaseX + Px).ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append(',');
        Builder.Append((BaseY + Py).ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append(' ');
        Builder.Append((BaseX - Px).ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append(',');
        Builder.Append((BaseY - Py).ToString("0.###", CultureInfo.InvariantCulture));
        Builder.Append("\" fill=\"#333333\" stroke=\"none\"/>");
    }

    private static (double X, double Y) ResolveLabelPoint(DiagramLayoutRoutedEdge Edge, double OffsetPx)
    {
        if (!double.IsNaN(Edge.LabelX) && !double.IsNaN(Edge.LabelY))
        {
            return (Edge.LabelX, Edge.LabelY);
        }

        int Mid = Edge.Samples.Count / 2;
        (double X, double Y) = Edge.Samples[Mid];
        return (X, Y - OffsetPx);
    }

    private static string Escape(string Text)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return string.Empty;
        }

        return Text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}

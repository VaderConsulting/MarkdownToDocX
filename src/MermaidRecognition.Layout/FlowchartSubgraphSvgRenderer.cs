using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Renders subgraph cluster boxes behind flowchart nodes.
/// </summary>
public static class FlowchartSubgraphSvgRenderer
{
    private const double TITLE_BAND_PX = 26.0;

    private const string DEFAULT_FILL = "#ffffde";

    private const string DEFAULT_STROKE = "#aaaa33";

    /// <summary>
    /// Appends cluster rectangles and titles in paint order (outermost first, deepest nested last).
    /// </summary>
    public static void AppendSubgraphs(
        StringBuilder Builder,
        IReadOnlyList<FlowchartSubgraphLayoutRect> SubgraphRects,
        double MinLeft,
        double MaxTop,
        double DiagramPaddingPx,
        FlowchartRenderOptions RenderOptions,
        IReadOnlyList<FlowchartLayoutEdgeRoute>? Routes = null)
    {
        for (int Index = SubgraphRects.Count - 1; Index >= 0; Index--)
        {
            FlowchartSubgraphLayoutRect Rect = SubgraphRects[Index];
            double SvgLeft = DiagramPaddingPx + (Rect.Left - MinLeft);
            double SvgTop = DiagramPaddingPx + (MaxTop - Rect.Top);
            double Width = Rect.Right - Rect.Left;
            double Height = Rect.Top - Rect.Bottom;

            if (Width <= 0.0 || Height <= 0.0)
            {
                continue;
            }

            Builder.Append("<rect x=\"");
            Builder.Append(SvgLeft.ToString(CultureInfo.InvariantCulture));
            Builder.Append("\" y=\"");
            Builder.Append(SvgTop.ToString(CultureInfo.InvariantCulture));
            Builder.Append("\" width=\"");
            Builder.Append(Width.ToString(CultureInfo.InvariantCulture));
            Builder.Append("\" height=\"");
            Builder.Append(Height.ToString(CultureInfo.InvariantCulture));
            Builder.Append("\" fill=\"");
            Builder.Append(DEFAULT_FILL);
            Builder.Append("\" stroke=\"");
            Builder.Append(DEFAULT_STROKE);
            Builder.Append("\" stroke-width=\"1\"/>");

            string Title = Rect.Title ?? Rect.GroupId;
            if (string.IsNullOrWhiteSpace(Title))
            {
                continue;
            }

            double TitleX = SvgLeft + (Width / 2.0);
            double TitleFontSize = Math.Max(10.0, RenderOptions.FontSizePx);
            if (Routes != null)
                TitleX = DiagramPaddingPx + ResolveClearTitleCentre(Rect, Title, TitleFontSize, Routes) - MinLeft;
            double TitleY = SvgTop + (TITLE_BAND_PX / 2.0) + FlowchartRichLabelSvgWriter.ComputeBaselineOffsetPx(TitleFontSize);

            Builder.Append("<text x=\"");
            Builder.Append(TitleX.ToString(CultureInfo.InvariantCulture));
            Builder.Append("\" y=\"");
            Builder.Append(TitleY.ToString(CultureInfo.InvariantCulture));
            Builder.Append("\" text-anchor=\"middle\" fill=\"#333\" font-family=\"");
            Builder.Append(EscapeXml(RenderOptions.FontFamily));
            Builder.Append("\" font-size=\"");
            Builder.Append(TitleFontSize.ToString(CultureInfo.InvariantCulture));
            Builder.Append("px\">");
            Builder.Append(EscapeXml(Title));
            Builder.Append("</text>");
        }
    }

    private static double ResolveClearTitleCentre(FlowchartSubgraphLayoutRect Rect, string Title,
        double FontSize, IReadOnlyList<FlowchartLayoutEdgeRoute> Routes)
    {
        const double Clearance = 6;
        double HalfWidth = FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Title, FontSize) / 2;
        double CentreY = Rect.Top - TITLE_BAND_PX / 2;
        double Left = Rect.Left + HalfWidth + Clearance;
        double Right = Rect.Right - HalfWidth - Clearance;
        double Preferred = (Rect.Left + Rect.Right) / 2;
        if (Left > Right) return Preferred;
        var Blocked = new List<(double Left, double Right)>();
        foreach (var Route in Routes)
        for (int Index = 1; Index < Route.Points.Count; Index++)
        {
            var A = Route.Points[Index - 1]; var B = Route.Points[Index];
            if (Math.Max(A.Y, B.Y) < CentreY - FontSize / 2 - Clearance
                || Math.Min(A.Y, B.Y) > CentreY + FontSize / 2 + Clearance) continue;
            Blocked.Add((Math.Min(A.X, B.X) - HalfWidth - Clearance,
                Math.Max(A.X, B.X) + HalfWidth + Clearance));
        }
        var Candidates = Blocked.SelectMany(Interval => new[] { Interval.Left, Interval.Right })
            .Append(Preferred).Append(Left).Append(Right)
            .Where(X => X >= Left && X <= Right)
            .OrderBy(X => Math.Abs(X - Preferred)).ThenBy(X => X);
        foreach (double Candidate in Candidates)
            if (Blocked.All(Interval => Candidate <= Interval.Left || Candidate >= Interval.Right)) return Candidate;
        return Preferred;
    }

    private static string EscapeXml(string Value) => Value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}

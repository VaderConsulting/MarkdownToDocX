using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Verifies straight spine edge labels are centred on the rendered connector in SVG output.
/// </summary>
public static class FlowchartSvgEdgeLabelSpineHelper
{
    private const double SPINE_CENTRE_TOLERANCE_PX = 2.0;

    private const double FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR = 1.5;

    /// <summary>
    /// Updates <see cref="EdgeActualResult.LabelFacts"/> from rendered SVG geometry.
    /// </summary>
    public static void ApplyStraightSpineLabelCentreFacts(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        string Svg)
    {
        if (string.IsNullOrWhiteSpace(Svg))
        {
            return;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        bool HorizontalSpine = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        foreach (EdgeActualResult Edge in Diagram.Edges)
        {
            if (!Edge.IsVisible || string.IsNullOrWhiteSpace(Edge.Label))
            {
                continue;
            }

            if (!string.Equals(Edge.PathFamily, "Straight", StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(Edge.RouteClass, "CentreSpine", StringComparison.Ordinal)
                && !string.Equals(Edge.RouteClass, "ShortAdjacent", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryMeasureLabelSpineCentreOffset(
                    Svg,
                    Edge.Label,
                    HorizontalSpine,
                    Options,
                    out double CentreOffsetPx))
            {
                continue;
            }

            Edge.LabelFacts["CentredOnConnectorSpine"] =
                Math.Abs(CentreOffsetPx) <= SPINE_CENTRE_TOLERANCE_PX;
        }
    }

    private static bool TryMeasureLabelSpineCentreOffset(
        string Svg,
        string Label,
        bool HorizontalSpine,
        FlowchartRenderOptions Options,
        out double CentreOffsetPx)
    {
        CentreOffsetPx = double.NaN;
        string SearchToken = BuildLabelSearchToken(Label);
        if (string.IsNullOrWhiteSpace(SearchToken))
        {
            return false;
        }

        Match? LabelText = Regex.Matches(
                Svg,
                @"<text x=""([^""]+)"" y=""([^""]+)""[^>]*>(.*?)</text>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase)
            .Cast<Match>()
            .FirstOrDefault(Candidate => Candidate.Groups[3].Value.Contains(SearchToken, StringComparison.Ordinal));

        if (LabelText is null || !LabelText.Success)
        {
            return false;
        }

        double LabelAnchorX = double.Parse(LabelText.Groups[1].Value, CultureInfo.InvariantCulture);
        double FirstLineSvgY = double.Parse(LabelText.Groups[2].Value, CultureInfo.InvariantCulture);
        int LineCount = Regex.Matches(LabelText.Groups[3].Value, "<tspan", RegexOptions.IgnoreCase).Count;
        if (LineCount < 1)
        {
            LineCount = 1;
        }

        double LabelCentreSvgY = FlowchartRichLabelSvgWriter.ComputeLabelBlockCentreY(FirstLineSvgY, LineCount, Options.EdgeLabelFontSizePx, FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR);

        Match? SpineLine = Regex.Matches(
                Svg,
                @"<line x1=""([^""]+)"" y1=""([^""]+)"" x2=""([^""]+)"" y2=""([^""]+)""")
            .Cast<Match>()
            .Where(Candidate =>
            {
                double X1 = double.Parse(Candidate.Groups[1].Value, CultureInfo.InvariantCulture);
                double Y1 = double.Parse(Candidate.Groups[2].Value, CultureInfo.InvariantCulture);
                double X2 = double.Parse(Candidate.Groups[3].Value, CultureInfo.InvariantCulture);
                double Y2 = double.Parse(Candidate.Groups[4].Value, CultureInfo.InvariantCulture);
                if (HorizontalSpine)
                {
                    return Math.Abs(Y1 - Y2) < 0.5 && Math.Abs(X2 - X1) > 50.0;
                }

                return Math.Abs(X1 - X2) < 0.5 && Math.Abs(Y2 - Y1) > 50.0;
            })
            .OrderBy(Candidate =>
            {
                if (HorizontalSpine)
                {
                    double Y1 = double.Parse(Candidate.Groups[2].Value, CultureInfo.InvariantCulture);
                    double Y2 = double.Parse(Candidate.Groups[4].Value, CultureInfo.InvariantCulture);
                    double MidY = (Y1 + Y2) / 2.0;
                    return Math.Abs(MidY - LabelCentreSvgY);
                }

                double X1 = double.Parse(Candidate.Groups[1].Value, CultureInfo.InvariantCulture);
                double X2 = double.Parse(Candidate.Groups[3].Value, CultureInfo.InvariantCulture);
                double MidX = (X1 + X2) / 2.0;
                return Math.Abs(MidX - LabelAnchorX);
            })
            .FirstOrDefault();

        if (SpineLine is null || !SpineLine.Success)
        {
            return false;
        }

        if (HorizontalSpine)
        {
            double SpineMidSvgY =
                (double.Parse(SpineLine.Groups[2].Value, CultureInfo.InvariantCulture)
                + double.Parse(SpineLine.Groups[4].Value, CultureInfo.InvariantCulture)) / 2.0;
            CentreOffsetPx = LabelCentreSvgY - SpineMidSvgY;
            return true;
        }

        double LabelCentreSvgX = LabelAnchorX;
        double SpineMidSvgX =
            (double.Parse(SpineLine.Groups[1].Value, CultureInfo.InvariantCulture)
            + double.Parse(SpineLine.Groups[3].Value, CultureInfo.InvariantCulture)) / 2.0;
        CentreOffsetPx = LabelCentreSvgX - SpineMidSvgX;
        return true;
    }

    private static string BuildLabelSearchToken(string Label)
    {
        string Normalised = Label.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (Normalised.Length <= 24)
        {
            return Normalised;
        }

        return Normalised.Substring(0, 24);
    }
}

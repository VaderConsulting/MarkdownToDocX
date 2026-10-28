using System;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Matches MDConverter DocxWriter embed scaling for diagram legibility checks.</summary>
public static class FlowchartDocxEmbedMetrics
{
    private const double DEFAULT_DPI = 96.0;
    private const double DEFAULT_MAX_WIDTH_CM = 16.0;
    private const double DEFAULT_TARGET_WIDTH_CM = 14.5;
    private const double DEFAULT_MAX_HEIGHT_CM = 18.0;

    /// <summary>Minimum rendered node label height in points before Word scaling is considered unreadable.</summary>
    public const double MIN_READABLE_LABEL_HEIGHT_PT = 7.0;

    public sealed record EmbedReport(double WidthPixels, double HeightPixels, double NaturalWidthCm,
        double EmbeddedWidthCm, double EmbeddedHeightCm, double WidthScale, bool LabelReadable);

    public static EmbedReport Measure(FlowchartLayoutResult Layout, double MaxWidthCm = DEFAULT_MAX_WIDTH_CM,
        double TargetWidthCm = DEFAULT_TARGET_WIDTH_CM, bool ScaleDownOnly = true)
    {
        (double WidthPixels, double HeightPixels) = BoundsPixels(Layout);
        if (WidthPixels <= 0.0 || HeightPixels <= 0.0)
        {
            return new EmbedReport(WidthPixels, HeightPixels, 0.0, TargetWidthCm, TargetWidthCm * 0.5625, 1.0, true);
        }

        double NaturalWidthCm = WidthPixels / DEFAULT_DPI * 2.54;
        double AspectRatio = HeightPixels / WidthPixels;
        double WidthCm = NaturalWidthCm <= 0.0 ? TargetWidthCm : ScaleDownOnly
            ? Math.Min(NaturalWidthCm, TargetWidthCm)
            : TargetWidthCm;
        double HeightCm = WidthCm * AspectRatio;
        if (WidthCm > MaxWidthCm)
        {
            WidthCm = MaxWidthCm;
            HeightCm = WidthCm * AspectRatio;
        }

        if (HeightCm > DEFAULT_MAX_HEIGHT_CM)
        {
            HeightCm = DEFAULT_MAX_HEIGHT_CM;
            WidthCm = HeightCm / AspectRatio;
        }

        double WidthScale = NaturalWidthCm <= 0.0 ? 1.0 : WidthCm / NaturalWidthCm;
        const double NominalLabelPx = 12.0;
        bool LabelReadable = NominalLabelPx * WidthScale >= MIN_READABLE_LABEL_HEIGHT_PT;
        return new EmbedReport(WidthPixels, HeightPixels, NaturalWidthCm, WidthCm, HeightCm, WidthScale, LabelReadable);
    }

    private static (double Width, double Height) BoundsPixels(FlowchartLayoutResult Layout)
    {
        if (Layout.NodeBounds.Count == 0 && Layout.ClusterBounds.Count == 0)
        {
            return (0.0, 0.0);
        }

        double Left = double.PositiveInfinity;
        double Right = double.NegativeInfinity;
        double Bottom = double.PositiveInfinity;
        double Top = double.NegativeInfinity;
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds.Concat(Layout.ClusterBounds))
        {
            Left = Math.Min(Left, Box.CenterX - (Box.Width / 2.0));
            Right = Math.Max(Right, Box.CenterX + (Box.Width / 2.0));
            Bottom = Math.Min(Bottom, Box.CenterY - (Box.Height / 2.0));
            Top = Math.Max(Top, Box.CenterY + (Box.Height / 2.0));
        }

        return (Right - Left, Top - Bottom);
    }
}

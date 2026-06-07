using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using MDConverter.Pipeline;
using MermaidRecognition.NaiadAdapter;
using MermaidSharp;
using SkiaSharp;

namespace MDConverter.Tests;

[TestClass]
public sealed class LinkStyleDefaultRasterArrowTests
{
    const string F68_SOURCE = """
        flowchart LR
        A[A] --> B[B] --> C[C]
        linkStyle default stroke:#c60,stroke-width:3px
        """;

    [TestMethod]
    public void ExpandSvgFlowchartMarkers_LinkStyleDefault_InlinesOrangeArrowheads()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            F68_SOURCE,
            RenderOptions.Default,
            out string Svg);
        Assert.IsTrue(Rendered);

        MethodInfo? ExpandMethod = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(ExpandMethod);

        string ExpandedSvg = (string)ExpandMethod.Invoke(null, new object[] { Svg })!;
        bool HasOrangeArrowFill = Regex.IsMatch(
            ExpandedSvg,
            @"fill=""#cc6600""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.IsTrue(
            HasOrangeArrowFill,
            "Expected raster arrow inlining to use linkStyle stroke colour #cc6600, not default #333333.");
    }

    [TestMethod]
    public void TryRasterizeSvgToPng_LinkStyleDefault_ArrowPixelsMatchEdgeStroke()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            F68_SOURCE,
            RenderOptions.Default,
            out string Svg);
        Assert.IsTrue(Rendered);

        MethodInfo? ExpandMethod = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? RasterizeMethod = typeof(DocxWriter).GetMethod(
            "TryRasterizeSvgToPng",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(ExpandMethod);
        Assert.IsNotNull(RasterizeMethod);

        string ExpandedSvg = (string)ExpandMethod.Invoke(null, new object[] { Svg })!;
        byte[]? PngBytes = (byte[]?)RasterizeMethod.Invoke(null, new object[] { ExpandedSvg });
        Assert.IsNotNull(PngBytes);
        Assert.IsTrue(PngBytes.Length > 0);

        using SKBitmap Bitmap = SKBitmap.Decode(PngBytes);
        Assert.IsNotNull(Bitmap);

        int OrangeArrowPixels = CountPixelsNearColor(Bitmap, 204, 102, 0);
        int DefaultGreyArrowPixels = CountPixelsNearColor(Bitmap, 51, 51, 51);

        Assert.IsTrue(
            OrangeArrowPixels > 20,
            "Expected orange (#cc6600) arrowhead pixels in rasterised PNG, found "
                + OrangeArrowPixels.ToString(CultureInfo.InvariantCulture));
        Assert.IsTrue(
            DefaultGreyArrowPixels < OrangeArrowPixels,
            "Raster arrowheads should not remain default #333 when linkStyle sets stroke:#c60.");
    }

    static int CountPixelsNearColor(SKBitmap Bitmap, byte ExpectedRed, byte ExpectedGreen, byte ExpectedBlue)
    {
        int MatchCount = 0;
        for (int Y = 0; Y < Bitmap.Height; Y++)
        {
            for (int X = 0; X < Bitmap.Width; X++)
            {
                SKColor Pixel = Bitmap.GetPixel(X, Y);
                if (Pixel.Alpha < 128)
                {
                    continue;
                }

                if (Math.Abs(Pixel.Red - ExpectedRed) <= 12
                    && Math.Abs(Pixel.Green - ExpectedGreen) <= 12
                    && Math.Abs(Pixel.Blue - ExpectedBlue) <= 12)
                {
                    MatchCount++;
                }
            }
        }

        return MatchCount;
    }
}

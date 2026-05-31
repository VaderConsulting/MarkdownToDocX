using System.Globalization;
using System.Reflection;
using MDConverter.Pipeline;
using MermaidRecognition.NaiadAdapter;
using MermaidSharp;
using SkiaSharp;

namespace MDConverter.Tests;

[TestClass]
public sealed class F77ClassDefRasterTests
{
    const string F77_SOURCE = """
        flowchart LR
        A[Alpha] --> B[Beta] --> C[Gamma]
        classDef lane fill:#eef,stroke:#369
        class A,C lane
        """;

    [TestMethod]
    public void TryRasterizeSvgToPng_F77Recognition_PreservesLaneClassColours()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderFlowchartSvg(
            F77_SOURCE,
            RenderOptions.Default,
            out string Svg);
        Assert.IsTrue(Rendered);
        Assert.IsTrue(
            Svg.Contains("#eeeeff", StringComparison.OrdinalIgnoreCase),
            "SVG should contain lane fill before rasterisation");

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

        int LaneFillPixels = CountPixelsNearColor(Bitmap, 238, 238, 255);
        int DefaultFillPixels = CountPixelsNearColor(Bitmap, 236, 236, 255);

        Assert.IsTrue(
            LaneFillPixels > 200,
            "Expected lane fill #eeeeff on A/C in rasterised PNG, found "
                + LaneFillPixels.ToString(CultureInfo.InvariantCulture));
        Assert.IsTrue(
            DefaultFillPixels > 200,
            "Expected default fill #ECECFF on B in rasterised PNG, found "
                + DefaultFillPixels.ToString(CultureInfo.InvariantCulture));
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

                if (Math.Abs(Pixel.Red - ExpectedRed) <= 8
                    && Math.Abs(Pixel.Green - ExpectedGreen) <= 8
                    && Math.Abs(Pixel.Blue - ExpectedBlue) <= 8)
                {
                    MatchCount++;
                }
            }
        }

        return MatchCount;
    }
}

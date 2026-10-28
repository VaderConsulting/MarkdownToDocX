using System.Globalization;
using System.Reflection;
using MDConverter.Pipeline;
using MermaidRecognition.NaiadAdapter;
using MermaidSharp;
using SkiaSharp;

namespace MDConverter.Tests;

[TestClass]
public sealed class F53StyleRasterFillTests
{
    const string F53_SOURCE = """
        flowchart TD
        Start([Start]) --> Process[Process]
        Process --> Decision{Valid?}
        Decision -->|Yes| Success[Success]
        Decision -->|No| Error[Error]
        Success --> End([End])
        Error --> End

        style Start fill:#e1f5e1
        style End fill:#e1f5e1
        style Error fill:#f8d7da
        style Success fill:#d1ecf1
        """;

    [TestMethod]
    public void TryRasterizeSvgToPng_F53Recognition_PreservesStyledNodeFillPixels()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            F53_SOURCE,
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

        string ExpandedSvg = (string)ExpandMethod.Invoke(null, [Svg])!;
        byte[]? PngBytes = (byte[]?)RasterizeMethod.Invoke(null, [ExpandedSvg]);
        Assert.IsNotNull(PngBytes);
        Assert.IsTrue(PngBytes.Length > 0);

        using SKBitmap Bitmap = SKBitmap.Decode(PngBytes);
        Assert.IsNotNull(Bitmap);

        int SuccessFillPixels = CountPixelsNearColor(Bitmap, 209, 236, 241);
        int ErrorFillPixels = CountPixelsNearColor(Bitmap, 248, 215, 218);
        int StartFillPixels = CountPixelsNearColor(Bitmap, 225, 245, 225);

        Assert.IsTrue(
            SuccessFillPixels > 200,
            "Expected Success fill #d1ecf1 in rasterised PNG, found " + SuccessFillPixels.ToString(CultureInfo.InvariantCulture));
        Assert.IsTrue(
            ErrorFillPixels > 200,
            "Expected Error fill #f8d7da in rasterised PNG, found " + ErrorFillPixels.ToString(CultureInfo.InvariantCulture));
        Assert.IsTrue(
            StartFillPixels > 200,
            "Expected Start fill #e1f5e1 in rasterised PNG, found " + StartFillPixels.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void TryRasterizeSvgToPng_F53Legacy_PreservesStyledNodeFillPixels()
    {
        string Svg = Mermaid.Render(F53_SOURCE, RenderOptions.Default);
        MethodInfo? ExpandMethod = typeof(DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? RasterizeMethod = typeof(DocxWriter).GetMethod(
            "TryRasterizeSvgToPng",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(ExpandMethod);
        Assert.IsNotNull(RasterizeMethod);

        string ExpandedSvg = (string)ExpandMethod.Invoke(null, [Svg])!;
        byte[]? PngBytes = (byte[]?)RasterizeMethod.Invoke(null, [ExpandedSvg]);
        Assert.IsNotNull(PngBytes);

        using SKBitmap Bitmap = SKBitmap.Decode(PngBytes);
        Assert.IsNotNull(Bitmap);

        int SuccessFillPixels = CountPixelsNearColor(Bitmap, 209, 236, 241);
        Assert.IsTrue(
            SuccessFillPixels > 200,
            "Expected Success fill #d1ecf1 in legacy rasterised PNG, found " + SuccessFillPixels.ToString(CultureInfo.InvariantCulture));
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

using System.Reflection;
using MDConverter.Pipeline;
using SkiaSharp;
using MermaidRecognition.Layout.Enterprise;

namespace MDConverter.Tests;

[TestClass]
public sealed class SvgViewportRasterTests
{
    [TestMethod]
    [DataRow(200, 240)]
    [DataRow(50, 60)]
    [DataRow(100, 120)]
    public void ScaledViewport_PreservesAllFourCornersAndPadding(int Width, int Height)
    {
        string Svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{Width}" height="{Height}" viewBox="10 20 100 120">
              <rect x="10" y="20" width="100" height="120" fill="white"/>
              <rect x="15" y="25" width="90" height="110" fill="#00aa00"/>
            </svg>
            """;
        using SKBitmap Bitmap = Rasterize(Svg);
        Assert.AreEqual(Width * 4, Bitmap.Width);
        Assert.AreEqual(Height * 4, Bitmap.Height);
        foreach (double X in new[] { 0.10, 0.90 })
        {
            foreach (double Y in new[] { 0.10, 0.90 })
            {
                Assert.AreEqual(new SKColor(0, 170, 0), Bitmap.GetPixel((int)(Bitmap.Width * X), (int)(Bitmap.Height * Y)));
            }
        }

        Assert.AreEqual(SKColors.White, Bitmap.GetPixel(Bitmap.Width - 1, Bitmap.Height - 1));
    }

    private static SKBitmap Rasterize(string Svg)
    {
        MethodInfo Method = typeof(DocxWriter).GetMethod("TryRasterizeSvgToPng", BindingFlags.NonPublic | BindingFlags.Static)!;
        var Png = (byte[]?)Method.Invoke(null, [Svg]);
        Assert.IsNotNull(Png);
        return SKBitmap.Decode(Png);
    }

    [TestMethod]
    [DataRow(6)]
    [DataRow(14)]
    [DataRow(24)]
    public void EnterpriseFontDirective_KeepsBothBoxesInsideTheBitmap(int FontSize)
    {
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg("""
            %%{init: {"themeVariables":{"fontSize":"FONT_SIZEpx"}}}%%
            enterprise
            application-component source "Source"
            application-service target "Target"
            source -realization-> target
            """.Replace("FONT_SIZE", FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        using SKBitmap Bitmap = Rasterize(Svg);
        int MaxX = -1, MaxY = -1;
        for (int Y = 0; Y < Bitmap.Height; Y++)
        {
            for (int X = 0; X < Bitmap.Width; X++)
        {
                SKColor Pixel = Bitmap.GetPixel(X, Y);
            if (Pixel.Red > 245 && Pixel.Green > 245 && Pixel.Blue > 245)
                {
                    continue;
                }

                MaxX = Math.Max(MaxX, X); MaxY = Math.Max(MaxY, Y);
        }
        }

        Assert.IsTrue(MaxX > Bitmap.Width * 0.8 && MaxX < Bitmap.Width - 4);
        Assert.IsTrue(MaxY > Bitmap.Height * 0.8 && MaxY < Bitmap.Height - 4);
    }
}

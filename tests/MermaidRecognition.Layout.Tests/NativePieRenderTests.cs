using System.Globalization;
using System.Xml.Linq;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class NativePieRenderTests
{
    [Test]
    public void UnequalSections_PreserveOrderAndShowDataWithoutLosingSmallLegends()
    {
        Assert.That(NativePieRenderBridge.TryRenderSvg("pie showData title Quantities\n\"First\":60\n\"Second\":39.5\n\"Small\":0.5", out string Svg), Is.True);
        var Doc = XDocument.Parse(Svg);
        var Slices = Doc.Descendants().Where(Element => (string?)Element.Attribute("class") == "pie-slice").ToList();
        Assert.That(Slices.Select(Element => (string?)Element.Attribute("data-label")), Is.EqualTo(new[] { "First", "Second" }));
        Assert.That(Doc.Root!.Value, Does.Contain("First [60]").And.Contain("Second [39.5]").And.Contain("Small [0.5]").And.Contain("60%").And.Contain("40%"));
        Assert.That(Slices.Select(Element => (string?)Element.Attribute("d")), Has.All.Contains("A170,170"));
    }

    [Test]
    public void DuplicateLabel_UsesFirstValueAndSingleSectionIsACompleteCircle()
    {
        Assert.That(NativePieRenderBridge.TryRenderSvg("pie showData\n\"A & B\":10\n\"A & B\":90", out string Svg), Is.True);
        var Doc = XDocument.Parse(Svg);
        var Slice = Doc.Descendants().Single(Element => (string?)Element.Attribute("class") == "pie-slice");
        Assert.That(Slice.Name.LocalName, Is.EqualTo("circle"));
        Assert.That(Doc.Root!.Value, Does.Contain("A & B [10]").And.Not.Contain("[90]"));
    }

    [Test]
    public void ManyLongLabels_GrowTheCanvasAndEscapeXml()
    {
        string Source = "pie\n" + string.Join("\n", Enumerable.Range(0, 24).Select(Index => $"\"A & B with a long label {Index}\":1"));
        Assert.That(NativePieRenderBridge.TryRenderSvg(Source, out string Svg), Is.True);
        var Root = XDocument.Parse(Svg).Root!;
        double Height = double.Parse((string)Root.Attribute("height")!, CultureInfo.InvariantCulture);
        foreach (var Text in Root.Descendants().Where(Element => Element.Name.LocalName == "text"))
            Assert.That(double.Parse((string)Text.Attribute("y")!, CultureInfo.InvariantCulture), Is.LessThan(Height));
        Assert.That(Svg, Does.Not.Contain("NaN").And.Not.Contain("Infinity"));
    }

    [TestCase("pie\n\"Empty\":0")]
    [TestCase("pie")]
    public void EmptyOrZeroTotal_RendersFiniteGeometry(string Source)
    {
        Assert.That(NativePieRenderBridge.TryRenderSvg(Source, out string Svg), Is.True);
        XDocument.Parse(Svg);
        Assert.That(Svg, Does.Not.Contain("NaN").And.Not.Contain("Infinity"));
    }

    [TestCase("flowchart TB\na-->b")]
    [TestCase("%%{init: {'theme':'dark'}}%%\npie\n\"One\":1")]
    [TestCase("pie\nunsupported statement")]
    [TestCase("%% pie\n%% \"One\":1")]
    public void UnsupportedSource_LeavesTheFallbackAvailable(string Source)
    {
        Assert.That(NativePieRenderBridge.TryRenderSvg(Source, out string Svg), Is.False);
        Assert.That(Svg, Is.Empty);
    }
}

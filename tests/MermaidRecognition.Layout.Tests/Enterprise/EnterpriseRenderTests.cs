using System.Xml.Linq;

using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseRenderTests
{
    [TestCase("01-application-layer.mmd")]
    [TestCase("02-technology-layer.mmd")]
    [TestCase("03-cross-layer-webshop.mmd")]
    [TestCase("06-business-layer.mmd")]
    [TestCase("07-motivation.mmd")]
    [TestCase("08-strategy.mmd")]
    [TestCase("09-physical.mmd")]
    [TestCase("10-implementation-migration.mmd")]
    [TestCase("11-grouping-location-junction.mmd")]
    [TestCase("12-motivation-to-technology.mmd")]
    public void Render_ValidSamples_ProduceMarkerFreeSvg(string FileName)
    {
        string Source = ReadSample(FileName);
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(Source);
        Assert.That(Svg, Does.Contain("<svg"));
        Assert.That(Svg, Does.Not.Contain("<marker"));
        Assert.That(Svg, Does.Not.Contain("<foreignObject"));
        Assert.That(Svg, Does.Not.Contain("marker-end"));
        XDocument.Parse(Svg);
    }

    [Test]
    public void Render_NestedAssignment_DoesNotDrawHiddenNestingLine()
    {
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(
            """
            enterprise
              application-component webShop "Web Shop"
              application-function checkout "Checkout" in webShop
              webShop -assignment-> checkout
              application-service ordering "Online Ordering"
              checkout -realization-> ordering
            """);

        Assert.That(Svg, Does.Contain("data-id=\"webShop\""));
        Assert.That(Svg, Does.Contain("data-id=\"checkout\""));
        Assert.That(Svg, Does.Not.Contain("data-source=\"webShop\""));
        Assert.That(Svg, Does.Contain("data-source=\"checkout\""));
    }

    [Test]
    public void Render_AllElementTypes_EmitExpectedIconHints()
    {
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(
            """
            enterprise
              application-component ac "AC"
              application-collaboration aco "ACo"
              application-interface ai "AI"
              application-function af "AF"
              application-interaction ain "AIn"
              application-process ap "AP"
              application-event ae "AE"
              application-service asvc "AS"
              data-object dobj "DO"
              node n "N"
              device dev "Dev"
              system-software ss "SS"
              technology-collaboration tc "TC"
              technology-interface ti "TI"
              path p "Pa"
              communication-network cn "CN"
              technology-function tf "TF"
              technology-process tp "TP"
              technology-interaction tin "TIn"
              technology-event te "TE"
              technology-service ts "TS"
              artifact art "Art"
              ac -association-> aco
            """);

        Assert.That(Svg, Does.Contain("#AFFFFF"));
        Assert.That(Svg, Does.Contain("#AFFFAF"));
        Assert.That(Svg, Does.Contain("class=\"element data-object\""));
        Assert.That(Svg, Does.Contain("class=\"element device\""));
        XDocument.Parse(Svg);
    }

    [Test]
    public void Render_NewLayers_EmitLayerFillsAndJunctions()
    {
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(
            """
            enterprise
              stakeholder board "Board"
              business-actor clerk "Clerk"
              capability selling "Selling"
              equipment press "Press"
              work-package cutover "Cutover"
              location london "London"
              and-junction both
              or-junction either
              board -association-> clerk
              clerk -association-> selling
            """);

        Assert.That(Svg, Does.Contain("#CCCCFF"));
        Assert.That(Svg, Does.Contain("#FFFFAF"));
        Assert.That(Svg, Does.Contain("#F8D0C8"));
        Assert.That(Svg, Does.Contain("#AFFFAF"));
        Assert.That(Svg, Does.Contain("#FFE0E0"));
        Assert.That(Svg, Does.Contain("class=\"element stakeholder\""));
        Assert.That(Svg, Does.Contain("class=\"element business-actor\""));
        Assert.That(Svg, Does.Contain("class=\"element junction\""));
        XDocument.Parse(Svg);
    }

    [Test]
    public void Render_NotationLegend_CoversLayersAndRelationships()
    {
        string Svg = EnterpriseNotationLegendRenderer.RenderSvg();
        Assert.That(Svg, Does.Contain("<svg"));
        Assert.That(Svg, Does.Not.Contain("<marker"));
        Assert.That(Svg, Does.Not.Contain("<foreignObject"));
        Assert.That(Svg, Does.Contain("#CCCCFF"));
        Assert.That(Svg, Does.Contain("#F8D0C8"));
        Assert.That(Svg, Does.Contain("#FFFFAF"));
        Assert.That(Svg, Does.Contain("#AFFFFF"));
        Assert.That(Svg, Does.Contain("#AFFFAF"));
        Assert.That(Svg, Does.Contain("#FFE0E0"));
        Assert.That(Svg, Does.Contain("ApplicationComponent"));
        Assert.That(Svg, Does.Contain("BusinessActor"));
        Assert.That(Svg, Does.Contain("Stakeholder"));
        Assert.That(Svg, Does.Contain("WorkPackage"));
        Assert.That(Svg, Does.Contain("composition"));
        Assert.That(Svg, Does.Contain("realization"));
        Assert.That(Svg, Does.Contain("stroke-dasharray=\"5 3\""));
        Assert.That(Svg, Does.Contain("r=\"13\""));
        Assert.That(Svg, Does.Contain("Junction (and)"));
        Assert.That(Svg, Does.Contain("Junction (or)"));
        XDocument.Parse(Svg);
    }

    [Test]
    public void Render_Sample12_SellingBoxesAreDistinct()
    {
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(ReadSample("12-motivation-to-technology.mmd"));
        Assert.That(Svg, Does.Contain("Selling Capability"));
        Assert.That(Svg, Does.Contain("Selling Service"));
        XDocument.Parse(Svg);
    }

    [Test]
    public void Render_EdgeLabels_MatchFlowchartOffsetStyle()
    {
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(ReadSample("03-cross-layer-webshop.mmd"));
        Assert.That(Svg, Does.Contain(">HTTPS<"));
        Assert.That(Svg, Does.Contain("trebuchet ms"));
        Assert.That(Svg, Does.Contain("fill=\"#333\""));
        Assert.That(Svg, Does.Contain("fill=\"#ffffff\""));
        Assert.That(Svg, Does.Contain("rx=\"2\" fill=\"#ffffff\""));
        Assert.That(Svg, Does.Contain("stroke-dasharray=\"1.5 3\""));
        string InfluenceSvg = EnterpriseRenderBridge.RenderEnterpriseSvg(
            """
            enterprise
              stakeholder board "Board"
              driver growth "Growth"
              board -influence-> growth
              business-event start "Start"
              business-event finish "Finish"
              start -flow-> finish
            """);
        Assert.That(InfluenceSvg, Does.Contain("stroke-dasharray=\"6 4\""));
        XDocument.Parse(Svg);
    }

    [Test]
    public void Render_SkipEdge_RoutesAroundInterveningNode()
    {
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(
            """
            enterprise
              application-component top "Top"
              application-component mid "Mid"
              application-component bot "Bottom"
              top -flow-> mid
              mid -flow-> bot
              top -association-> bot
            """);

        XDocument Document = XDocument.Parse(Svg);
        XElement? Skip = Document.Descendants()
            .FirstOrDefault(Element =>
                (string?)Element.Attribute("data-source") == "top"
                && (string?)Element.Attribute("data-target") == "bot");
        Assert.That(Skip, Is.Not.Null);
        XElement? Path = Skip!.Elements().FirstOrDefault(Element => Element.Name.LocalName == "path");
        Assert.That(Path, Is.Not.Null);
        IReadOnlyList<string> Violations = EnterpriseEdgeClearance.FindViolations(
            """
            enterprise
              application-component top "Top"
              application-component mid "Mid"
              application-component bot "Bottom"
              top -flow-> mid
              mid -flow-> bot
              top -association-> bot
            """);
        Assert.That(Violations, Is.Empty, string.Join(Environment.NewLine, Violations));
    }

    [Test]
    public void TryRender_NonEnterprise_ReturnsFalse()
    {
        bool Rendered = EnterpriseRenderBridge.TryRenderEnterpriseSvg("flowchart TD\nA-->B", out string Svg);
        Assert.That(Rendered, Is.False);
        Assert.That(Svg, Is.Empty);
    }

    [Test]
    public void TryRender_InvalidRelationships_ThrowsWithLineNumbers()
    {
        string Source = ReadSample("04-invalid-relationships.mmd");
        InvalidOperationException? Error = Assert.Throws<InvalidOperationException>(
            () => EnterpriseRenderBridge.RenderEnterpriseSvg(Source));
        Assert.That(Error, Is.Not.Null);
        Assert.That(Error!.Message, Does.Contain("line 12"));
        Assert.That(Error.Message, Does.Contain("line 16"));
    }

    private static string ReadSample(string FileName)
    {
        DirectoryInfo? DirectoryCursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (DirectoryCursor != null)
        {
            string Candidate = Path.Combine(DirectoryCursor.FullName, "docs", "enterprise", "samples", FileName);
            if (File.Exists(Candidate))
            {
                return File.ReadAllText(Candidate);
            }

            DirectoryCursor = DirectoryCursor.Parent;
        }

        string Copied = Path.Combine(AppContext.BaseDirectory, "EnterpriseSamples", FileName);
        if (File.Exists(Copied))
        {
            return File.ReadAllText(Copied);
        }

        throw new FileNotFoundException("Sample not found: " + FileName);
    }
}

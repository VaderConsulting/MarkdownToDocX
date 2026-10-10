using System.Text;
using System.Xml.Linq;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseParallelRelationshipTests
{
    [TestCase("TB", 6, 3)]
    [TestCase("LR", 4, 2)]
    public void MultipleRelationshipsBetweenTheSameNodes_KeepDistinctRoutesAndCaptions(string Direction, int Forward, int Reverse)
    {
        var Source = new StringBuilder($"enterprise\n direction {Direction}\n application-component a \"Producer\"\n application-component b \"Consumer\"\n");
        for (int Index = 0; Index < Forward; Index++) Source.AppendLine($"a -flow-> b \"Forward caption {Index}\"");
        for (int Index = 0; Index < Reverse; Index++) Source.AppendLine($"b -flow-> a \"Return caption {Index}\"");
        XDocument Svg = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source.ToString()));
        var Edges = Svg.Descendants().Where(Element => Element.Attribute("data-source") != null).ToList();
        var Paths = Edges.Select(Edge => (string)Edge.Elements().First(Element => Element.Name.LocalName == "path").Attribute("d")!).ToList();
        Assert.That(Paths.Distinct().Count(), Is.EqualTo(Forward + Reverse), string.Join("\n", Paths));
        var Document = EnterpriseValidator.Analyze(Source.ToString()).Document;
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        var Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        foreach (var Edge in Routes)
        {
            Assert.That(Edge.LabelBreak, Is.Not.Null);
            foreach (var Other in Routes.Where(Other => Other != Edge))
            {
                Assert.That(Math.Abs(Edge.LabelX - Other.LabelX) >= (Edge.LabelWidth + Other.LabelWidth) / 2 + 6
                    || Math.Abs(Edge.LabelY - Other.LabelY) >= (Edge.LabelHeight + Other.LabelHeight) / 2 + 6,
                    Is.True, "Relationship captions must remain separated.");
                foreach (var Segment in Other.SvgPoints.Zip(Other.SvgPoints.Skip(1)))
                    Assert.That(Math.Max(Segment.First.X, Segment.Second.X) > Edge.LabelX - Edge.LabelWidth / 2 - 6
                        && Math.Min(Segment.First.X, Segment.Second.X) < Edge.LabelX + Edge.LabelWidth / 2 + 6
                        && Math.Max(Segment.First.Y, Segment.Second.Y) > Edge.LabelY - Edge.LabelHeight / 2 - 6
                        && Math.Min(Segment.First.Y, Segment.Second.Y) < Edge.LabelY + Edge.LabelHeight / 2 + 6,
                        Is.False, "A neighbouring connector must clear the caption.");
            }
        }
    }
}

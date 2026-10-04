using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class F104FeedbackLoopRenderTests
{
    [Test]
    public void RenderToSvg_F104_TwoParallelEdges_BAboveA()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart-elk TB
            A[A] --> B[B]
            B --> A
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds NodeA = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A");
        FlowchartLayoutNodeBounds NodeB = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B");
        Assert.That(NodeB.CenterY, Is.GreaterThan(NodeA.CenterY), "B should be above A in layout");

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Match LabelA = Regex.Match(Svg, @"<text x=""[^""]+"" y=""([^""]+)""[^>]*>A</text>");
        Match LabelB = Regex.Match(Svg, @"<text x=""[^""]+"" y=""([^""]+)""[^>]*>B</text>");
        Assert.That(LabelA.Success, Is.True);
        Assert.That(LabelB.Success, Is.True);

        double SvgAy = double.Parse(LabelA.Groups[1].Value, CultureInfo.InvariantCulture);
        double SvgBy = double.Parse(LabelB.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.That(SvgBy, Is.LessThan(SvgAy), "B should render above A");

        List<double> LineX1 = SvgStraightConnectorReader.Read(Svg)
            .Select(Connector => Connector.X1)
            .ToList();
        Assert.That(LineX1, Has.Count.EqualTo(2));
        Assert.That(Math.Abs(LineX1[0] - LineX1[1]), Is.GreaterThan(5.0), "Feedback edges should be horizontally separated");
    }
}

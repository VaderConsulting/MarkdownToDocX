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
        Assert.That(NodeA.CenterY, Is.GreaterThan(NodeB.CenterY), "A should be above B in layout");

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Match LabelA = Regex.Match(Svg, @"<text x=""[^""]+"" y=""([^""]+)""[^>]*>A</text>");
        Match LabelB = Regex.Match(Svg, @"<text x=""[^""]+"" y=""([^""]+)""[^>]*>B</text>");
        Assert.That(LabelA.Success, Is.True);
        Assert.That(LabelB.Success, Is.True);

        double SvgAy = double.Parse(LabelA.Groups[1].Value, CultureInfo.InvariantCulture);
        double SvgBy = double.Parse(LabelB.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.That(SvgAy, Is.LessThan(SvgBy), "A should render above B");

        Assert.That(Layout.EdgeRoutes, Has.Count.EqualTo(2));
        FlowchartLayoutPoint FirstMid = Layout.EdgeRoutes[0].Points[Layout.EdgeRoutes[0].Points.Count / 2];
        FlowchartLayoutPoint SecondMid = Layout.EdgeRoutes[1].Points[Layout.EdgeRoutes[1].Points.Count / 2];
        double Separation = Math.Abs(FirstMid.X - SecondMid.X);
        Assert.That(Separation, Is.GreaterThan(5.0), "Feedback edges should be horizontally separated");
    }
}

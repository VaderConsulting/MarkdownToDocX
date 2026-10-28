using MermaidRecognition.Core;
using MermaidRecognition.NaiadAdapter;

using MermaidSharp;
using MermaidSharp.Diagrams.Flowchart;
using MermaidSharp.Models;
using MermaidSharp.Rendering;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class F81TrapezoidBracketRenderTests
{
    const string F81_SOURCE = """
        flowchart TD
        Top[/Wide top/] --> Bottom[\Wide bottom/]
        """;

    [Test]
    public void Map_F81_AssignsTrapezoidNodeShapes()
    {
        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(F81_SOURCE));

        Assert.That(Model.GetNode("Top")!.Shape, Is.EqualTo(NodeShape.ParallelogramAlt));
        Assert.That(Model.GetNode("Bottom")!.Shape, Is.EqualTo(NodeShape.TrapezoidAlt));
        Assert.That(Model.GetNode("Top")!.Label, Is.EqualTo("Wide top"));
        Assert.That(Model.GetNode("Bottom")!.Label, Is.EqualTo("Wide bottom"));
    }

    [Test]
    public void TryRenderFlowchartSvg_F81_RendersTrapezoidPathsWithoutDelimiterSlashesInLabels()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            F81_SOURCE,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("Wide top"));
        Assert.That(Svg, Does.Contain("Wide bottom"));
        Assert.That(Svg, Does.Not.Contain("/Wide top/"));
        Assert.That(Svg, Does.Not.Contain(@"\Wide bottom"));
        Assert.That(Svg, Does.Not.Match(@"<path[^>]*d=""M[\d.]+ [\d.]+ H[\d.]+ V[\d.]+ Z"""));

        List<string> NodePaths = [.. System.Text.RegularExpressions.Regex.Matches(
                Svg,
                @"<path\b[^>]*\bd=""([^""]+)""[^>]*\bclass=""node""")
            .Select(Match => Match.Groups[1].Value)];
        Assert.That(NodePaths, Has.Count.EqualTo(2));
        Assert.That(NodePaths[0], Does.Not.Contain(" H").And.Not.Contain(" V"));
        Assert.That(NodePaths[1], Does.Not.Contain(" H").And.Not.Contain(" V"));

        Assert.That(
            GetHorizontalSpanAtTopEdge(NodePaths[1]),
            Is.GreaterThan(GetHorizontalSpanAtBottomEdge(NodePaths[1])),
            "[\\Wide bottom/] uses the trapezoid shape (wider at the top)");
    }

    static double GetHorizontalSpanAtTopEdge(string PathData) => GetHorizontalSpanAtExtremeY(PathData, UseMaxY: false);

    static double GetHorizontalSpanAtBottomEdge(string PathData) => GetHorizontalSpanAtExtremeY(PathData, UseMaxY: true);

    static double GetHorizontalSpanAtExtremeY(string PathData, bool UseMaxY)
    {
        List<(double X, double Y)> Points = [];
        foreach (System.Text.RegularExpressions.Match Match in System.Text.RegularExpressions.Regex.Matches(
                     PathData,
                     @"([ML])\s*([-\d.]+)\s*,\s*([-\d.]+)"))
        {
            Points.Add((double.Parse(Match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(Match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)));
        }

        double TargetY = UseMaxY
            ? Points.Max(Point => Point.Y)
            : Points.Min(Point => Point.Y);
        List<double> XValues = [.. Points.Where(Point => Math.Abs(Point.Y - TargetY) < 0.01).Select(Point => Point.X)];
        return XValues.Max() - XValues.Min();
    }
}

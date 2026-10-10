using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridBypassObstacleTests
{
    [TestCase("TB")]
    [TestCase("BT")]
    [TestCase("LR")]
    [TestCase("RL")]
    public void DirectLinkAlongsideForwardChain_ClearsIntermediateNodes(string Direction)
    {
        string Source = $$"""
            flowchart {{Direction}}
              a[Input] --> b[Split]
              b --> c[Intermediate]
              b --> d[Collector]
              c --> d
            """;
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.Evaluate(
            "forward-chain-bypass", Source);
        Assert.That(Report.Failures.Where(Failure => Failure.Kind == "NodeIntersection"), Is.Empty,
            string.Join("\n", Report.Failures.Select(Failure => Failure.Kind + ": " + Failure.Description)));
    }
}

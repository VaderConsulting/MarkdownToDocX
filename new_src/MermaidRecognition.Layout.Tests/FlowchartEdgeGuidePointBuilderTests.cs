using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartEdgeGuidePointBuilderTests
{
    [Test]
    public void ShouldUseStraightSegment_WhenTrueOneToOneAndAligned_ReturnsTrue()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Ingress] --> B[Enqueue]
            """);

        FlowchartLayoutNodeBounds Source = new FlowchartLayoutNodeBounds
        {
            NodeId = "A",
            CenterX = 100.0,
            CenterY = 200.0,
            Width = 80.0,
            Height = 40.0
        };
        FlowchartLayoutNodeBounds Target = new FlowchartLayoutNodeBounds
        {
            NodeId = "B",
            CenterX = 260.0,
            CenterY = 200.0,
            Width = 80.0,
            Height = 40.0
        };

        Assert.That(
            FlowchartEdgeGuidePointBuilder.ShouldUseStraightSegment(Source, Target, Contract, "A", "B"),
            Is.True);
    }

    [Test]
    public void ShouldUseStraightSegment_WhenFanOutFromSource_ReturnsFalseEvenIfAligned()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            B[Enqueue] --> C[Worker 1]
            B --> D[Worker 2]
            """);

        FlowchartLayoutNodeBounds Source = new FlowchartLayoutNodeBounds
        {
            NodeId = "B",
            CenterX = 100.0,
            CenterY = 200.0,
            Width = 80.0,
            Height = 40.0
        };
        FlowchartLayoutNodeBounds Target = new FlowchartLayoutNodeBounds
        {
            NodeId = "C",
            CenterX = 260.0,
            CenterY = 200.0,
            Width = 80.0,
            Height = 40.0
        };

        Assert.That(
            FlowchartEdgeGuidePointBuilder.ShouldUseStraightSegment(Source, Target, Contract, "B", "C"),
            Is.False);
    }

    [Test]
    public void ShouldUseStraightSegment_WhenFanInToTarget_ReturnsFalseEvenIfAligned()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            W1[Worker 1] --> Agg[Aggregate]
            W2[Worker 2] --> Agg
            """);

        FlowchartLayoutNodeBounds Source = new FlowchartLayoutNodeBounds
        {
            NodeId = "W2",
            CenterX = 100.0,
            CenterY = 100.0,
            Width = 80.0,
            Height = 40.0
        };
        FlowchartLayoutNodeBounds Target = new FlowchartLayoutNodeBounds
        {
            NodeId = "Agg",
            CenterX = 100.0,
            CenterY = 200.0,
            Width = 80.0,
            Height = 40.0
        };

        Assert.That(
            FlowchartEdgeGuidePointBuilder.ShouldUseStraightSegment(Source, Target, Contract, "W2", "Agg"),
            Is.False);
    }

    [Test]
    public void SharesAxisAlignedRowOrColumn_WhenCentresShareColumn_ReturnsTrue()
    {
        FlowchartLayoutNodeBounds Source = new FlowchartLayoutNodeBounds
        {
            NodeId = "A",
            CenterX = 100.0,
            CenterY = 200.0,
            Width = 80.0,
            Height = 40.0
        };
        FlowchartLayoutNodeBounds Target = new FlowchartLayoutNodeBounds
        {
            NodeId = "B",
            CenterX = 100.0,
            CenterY = 320.0,
            Width = 80.0,
            Height = 40.0
        };

        Assert.That(FlowchartEdgeGuidePointBuilder.SharesAxisAlignedRowOrColumn(Source, Target), Is.True);
    }

    [Test]
    public void BuildCurvedGuidePoints_WhenNotAligned_ProducesMoreThanTwoPoints()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A[Start] --> B[Left]
            A --> C[Right]
            """);

        EdgeContract Edge = Contract.Edges.First(Entry => Entry.TargetId == "B");
        List<(double X, double Y)> GuidePoints = FlowchartEdgeGuidePointBuilder.BuildCurvedGuidePoints(
            50.0,
            10.0,
            20.0,
            80.0,
            "rectangle",
            "rectangle",
            Edge,
            Contract,
            FlowchartDirection.TopToBottom,
            100.0,
            10.0);

        Assert.That(GuidePoints.Count, Is.GreaterThan(2));
    }
}

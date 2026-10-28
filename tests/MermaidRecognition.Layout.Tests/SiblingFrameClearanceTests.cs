using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class SiblingFrameClearanceTests
{
    [Test]
    public void Layout_ParallelGroupedChains_KeepFramesApartAndExternalInputAligned()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph Left
              First[First] --> Second[Second]
            end
            subgraph Right
              Third[Third] --> Fourth[Fourth]
            end
            Before[Before] --> First
            Fourth --> After[After]
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutNodeBounds[] Frames = [.. Layout.ClusterBounds.OrderBy(Frame => Frame.CenterX)];
        Assert.That(Frames[1].CenterX - (Frames[1].Width / 2) - Frames[0].CenterX - (Frames[0].Width / 2), Is.GreaterThanOrEqualTo(30));
        Assert.That(Layout.NodeBounds.Single(Node => Node.NodeId == "Before").CenterX,
            Is.EqualTo(Layout.NodeBounds.Single(Node => Node.NodeId == "First").CenterX).Within(0.5));
    }

    [Test]
    public void Layout_SequentialGroups_LeaveRoomForRoundedBorderCrossings()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            subgraph First
              One[One] --> Two[Two]
            end
            subgraph Second
              Three[Three] --> Four[Four]
            end
            Two --> Three
            Before[Before] --> One
            Four --> After[After]
            Two -.-> After
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutNodeBounds[] Frames = [.. Layout.ClusterBounds.OrderBy(Frame => Frame.CenterX)];
        Assert.That(Frames[1].CenterX - (Frames[1].Width / 2) - Frames[0].CenterX - (Frames[0].Width / 2), Is.GreaterThanOrEqualTo(70));
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "Two" && Edge.TargetId == "Three");
        FlowchartLayoutNodeBounds Source = Layout.NodeBounds.Single(Node => Node.NodeId == "Two");
        FlowchartLayoutNodeBounds Target = Layout.NodeBounds.Single(Node => Node.NodeId == "Three");
        Assert.That(Route.Points[0].X, Is.EqualTo(Source.CenterX + (Source.Width / 2)).Within(0.5));
        Assert.That(Route.Points[1].Y, Is.EqualTo(Route.Points[0].Y).Within(0.5));
        Assert.That(Route.Points[^1].Y, Is.EqualTo(Target.CenterY).Within(0.5));
        Assert.That(Route.Points.Zip(Route.Points.Skip(1)).All(Pair => Math.Abs(Pair.First.X - Pair.Second.X) < 0.5
            || Math.Abs(Pair.First.Y - Pair.Second.Y) < 0.5), Is.True);
    }
}

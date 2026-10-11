using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class SiblingFrameAlignmentTests
{
    [TestCase("TB")]
    [TestCase("BT")]
    public void ConnectedRootLocations_KeepMatchingSidesAndCentredContents(string Direction)
    {
        var Document = EnterpriseValidator.Analyze($"""
            enterprise {Direction}
            location first
            location second
            facility nested in first
            node source in nested
            business-actor target in second
            first -aggregation-> nested
            nested -composition-> source
            second -aggregation-> target
            source -serving-> target
            """).Document;
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        var First = Layout.Nodes["first"]; var Second = Layout.Nodes["second"];
        Assert.That(Second.AbsoluteX, Is.EqualTo(First.AbsoluteX).Within(0.01));
        Assert.That(Second.Width, Is.EqualTo(First.Width).Within(0.01));
        var Target = Layout.Nodes["target"];
        Assert.That(Target.AbsoluteX + Target.Width / 2,
            Is.EqualTo(Second.AbsoluteX + Second.Width / 2).Within(15));
        Assert.That(First.AbsoluteY + First.Height <= Second.AbsoluteY - 30
            || Second.AbsoluteY + Second.Height <= First.AbsoluteY - 30, Is.True);
        foreach (var Route in EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes))
        foreach (var Segment in Route.SvgPoints.Zip(Route.SvgPoints.Skip(1)))
        {
            Assert.That(Math.Abs(Segment.First.X - Segment.Second.X) < 0.01
                || Math.Abs(Segment.First.Y - Segment.Second.Y) < 0.01, Is.True);
            var Title = new FlowchartLayoutNodeBounds { CenterX = Second.AbsoluteX + Second.Width / 2,
                CenterY = Second.AbsoluteY + 12, Width = 48, Height = 24 };
            Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Segment.First.X,
                Segment.First.Y, Segment.Second.X, Segment.Second.Y, Title), Is.False);
        }
    }
}

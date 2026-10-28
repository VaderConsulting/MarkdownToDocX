using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class RelationshipLabelClearanceTests
{
    [TestCase(0.5)]
    [TestCase(1)]
    [TestCase(2)]
    public void SmallOffsetBetweenStraightRuns_StillLeavesRoomForLabelBreak(double Offset)
    {
        List<(double X, double Y)> Points = [(100, 100), (100, 140), (100 + Offset, 140), (100 + Offset, 190)];
        EnterpriseRoutedEdge Edge = new EnterpriseRoutedEdge(new EnterpriseRelationship(), "", Points,
            null, null, null, "Transfer", 0, 0);
        EnterpriseRelationshipLabelLayout.Place([Edge], []);
        Assert.That(Edge.LabelBreak, Is.Not.Null);
        Assert.That(Edge.LabelBreak!.Horizontal, Is.False);
        Assert.That(Edge.LabelY - Edge.LabelBreak.HalfGapWidthPx, Is.GreaterThan(140 + (Offset / 2)));
        Assert.That(Edge.LabelY + Edge.LabelBreak.HalfGapWidthPx, Is.LessThanOrEqualTo(180));
    }
}

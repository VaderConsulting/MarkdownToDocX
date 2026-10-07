using MermaidRecognition.Contract;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Group border crossings stay perpendicular and happen once.
/// </summary>
[TestFixture]
public sealed class FlowchartGroupBoundaryCrossingHelperTests
{
    [Test]
    public void InsertPerpendicularCrossings_LeavesTheGroupOnceAtARightAngle()
    {
        DiagramContract Contract = new DiagramContract();
        GroupContract Group = new GroupContract
        {
            Id = "cluster"
        };
        Group.ChildNodeIdsMutable.Add("inside");
        Contract.GroupsMutable.Add(Group);
        EdgeContract Edge = new EdgeContract
        {
            SourceId = "inside",
            TargetId = "outside"
        };
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal)
            {
                ["cluster"] = new FlowchartLayoutNodeBounds
                {
                    NodeId = "cluster",
                    CenterX = 0.0,
                    CenterY = 0.0,
                    Width = 200.0,
                    Height = 200.0
                }
            };
        List<(double X, double Y)> Points =
        [
            (-50.0, 0.0),
            (150.0, 0.0)
        ];

        FlowchartGroupBoundaryCrossingHelper.InsertPerpendicularCrossings(
            Contract,
            BoundsById,
            Edge,
            Points);

        Assert.That(Points.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(Points[0].X, Is.EqualTo(-50.0).Within(0.01));
        Assert.That(Points[0].Y, Is.EqualTo(0.0).Within(0.01));
        Assert.That(Points[1].X, Is.EqualTo(100.0).Within(0.6));
        Assert.That(Points[1].Y, Is.EqualTo(0.0).Within(0.6));
        Assert.That(Points[2].X, Is.GreaterThan(100.0));
        Assert.That(Points[2].Y, Is.EqualTo(0.0).Within(0.6));
        Assert.That(Math.Abs(Points[2].X - Points[1].X), Is.GreaterThan(1.0));
        Assert.That(Math.Abs(Points[2].Y - Points[1].Y), Is.LessThan(0.6));
    }
}

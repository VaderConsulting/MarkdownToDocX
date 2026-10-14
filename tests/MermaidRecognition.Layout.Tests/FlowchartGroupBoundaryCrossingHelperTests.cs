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
    public void RelativeGroupHint_IsRefreshedAgainstExpandedFrameWithoutUnlockingTheNode()
    {
        DiagramContract Contract = new();
        Contract.GroupsMutable.Add(new GroupContract { Id = "allRoles" });
        Contract.GroupsMutable.Add(new GroupContract { Id = "regRoles", ParentGroupId = "allRoles" });
        NodeContract Explorer = new()
        {
            Id = "explorer",
            ParentGroupId = "allRoles",
            PlacementHint = new PlacementHintContract
            {
                Kind = PlacementHintKind.RightOf,
                PeerId = "regRoles"
            }
        };
        Contract.NodesMutable.Add(Explorer);
        DiagramGridLayout.GridNode PlacedExplorer = new()
        {
            Id = Explorer.Id,
            X = 90,
            Y = 0,
            Width = 40,
            Height = 40,
            PlacementHintLocked = true
        };
        DiagramGridLayout.GridFrame ExpandedFrame = new()
        {
            Id = "regRoles",
            Left = -100,
            Right = 100,
            Top = -50,
            Bottom = 50
        };

        bool Refreshed = FlowchartPlacementHints.ReapplyLockedNodeHintAgainstFrame(
            Contract, PlacedExplorer, ExpandedFrame, [PlacedExplorer], DiagramGridMetrics.GRID_UNIT_PX);

        Assert.That(Refreshed, Is.True);
        Assert.That(PlacedExplorer.X - (PlacedExplorer.Width / 2.0),
            Is.GreaterThanOrEqualTo(ExpandedFrame.Right + DiagramGridMetrics.NODE_GAP_PX));
        Assert.That(PlacedExplorer.PlacementHintLocked, Is.True);
    }

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

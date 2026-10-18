using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks authoritative-grid audit coverage using deliberately corrupted returned geometry.</summary>
[TestFixture]
public sealed class CellGridAuditTests
{
    /// <summary>A whole multi-cell region is valid even when its centre does not lie on the legacy node-centre lattice.</summary>
    [Test]
    public void AcceptsWholeSpansAndRejectsStaleNodePixels()
    {
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 2, 1));
        var Node = new DiagramGridLayout.GridNode { Id = "A" }; Node.BindCells(Grid);
        var Layout = new FlowchartLayoutResult();
        var Box = Node.CellNode!.Bounds(Node.CellNode.Region);
        Layout.NodeBoundsMutable.Add(Box);
        Assert.That(CellGridAudit.Faults(new(), Layout, [Node]), Is.Empty);
        Layout.NodeBoundsMutable[0] = new() { NodeId = "A", CenterX = Box.CenterX + 1, CenterY = Box.CenterY, Width = Box.Width, Height = Box.Height };
        Assert.That(CellGridAudit.Faults(new(), Layout, [Node]).Keys, Does.Contain("node:A"));
    }

    /// <summary>An output frame cannot silently grow beyond the frame derived from accepted regions and measured title geometry.</summary>
    [Test]
    public void RejectsUnpublishedFrameResize()
    {
        var Contract = new DiagramContract();
        var Group = new GroupContract { Id = "G" }; Group.ChildNodeIdsMutable.Add("A"); Contract.GroupsMutable.Add(Group);
        Contract.NodesMutable.Add(new() { Id = "A", ParentGroupId = "G" });
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); Grid.TryPlace("A", new(0, 0, 1, 1));
        var Node = new DiagramGridLayout.GridNode { Id = "A", GroupId = "G" }; Node.BindCells(Grid); Node.CellNode!.UseRoutingCoordinates(300);
        var Layout = new FlowchartLayoutResult(); Layout.NodeBoundsMutable.Add(Node.CellNode.Bounds(Node.CellNode.Region));
        var Frame = DiagramGridLayout.ProjectCellFrames(Contract, [Node], Grid.Capture()).Single();
        Layout.ClusterBoundsMutable.Add(new() { NodeId = "G", CenterX = Frame.CenterX(), CenterY = Frame.CenterY(),
            Width = Frame.Right - Frame.Left, Height = Frame.Top - Frame.Bottom });
        Assert.That(CellGridAudit.Faults(Contract, Layout, [Node]), Is.Empty);
        var Original = Layout.ClusterBounds[0];
        Layout.ClusterBoundsMutable[0] = new() { NodeId = "G", CenterX = Original.CenterX, CenterY = Original.CenterY,
            Width = Original.Width + 30, Height = Original.Height };
        Assert.That(CellGridAudit.Faults(Contract, Layout, [Node]).Keys, Does.Contain("frame:G:left"));
    }
}

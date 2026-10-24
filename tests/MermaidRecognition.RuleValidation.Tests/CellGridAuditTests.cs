using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks authoritative-grid audit coverage using deliberately corrupted returned geometry.</summary>
[TestFixture]
public sealed class CellGridAuditTests
{
    /// <summary>Every off-lattice centre change is caught, as are whole-step stale projections the old check misses.</summary>
    /// <param name="Offset">Corruption added to one returned centre coordinate.</param>
    /// <param name="Vertical">Whether to corrupt the vertical coordinate.</param>
    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(30, false)]
    [TestCase(30, true)]
    public void RejectsCentreChangesIncludingWholeLatticeSteps(double Offset, bool Vertical)
    {
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1));
        var Node = new DiagramGridLayout.GridNode { Id = "A" }; Node.BindCells(Grid);
        var Box = Node.CellNode!.Bounds(Node.CellNode.Region);
        var Layout = new FlowchartLayoutResult();
        Layout.NodeBoundsMutable.Add(new() { NodeId = "A", CenterX = Box.CenterX + (Vertical ? 0 : Offset),
            CenterY = Box.CenterY + (Vertical ? Offset : 0), Width = Box.Width, Height = Box.Height });
        Assert.That(CellGridAudit.Faults(new(), Layout, [Node]).Keys, Does.Contain("node:A"));
        var Legacy = RouteRuleCatalogueAuditTests.GridFaults(Layout, Box.CenterX, Box.CenterY, DiagramGridMetrics.GRID_UNIT_PX);
        Assert.That(Legacy.ContainsKey("node:A"), Is.EqualTo(Offset != DiagramGridMetrics.GRID_UNIT_PX));
    }

    /// <summary>Corrupt snapshots cannot evade the audit by projecting consistent pixels from shared cells or undersized gaps.</summary>
    /// <param name="Corruption">Invalid state to inject without the production assignment validator.</param>
    [TestCase("shared")]
    [TestCase("column-gap")]
    [TestCase("row-gap")]
    [TestCase("nonfinite-gap")]
    public void IndependentlyRejectsInvalidOwnershipAndGaps(string Corruption)
    {
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 1)); Grid.TryPlace("B", new(2, 2, 1, 1));
        var Nodes = new[] { new DiagramGridLayout.GridNode { Id = "A" }, new DiagramGridLayout.GridNode { Id = "B" } };
        foreach (var Node in Nodes) Node.BindCells(Grid);
        var Regions = new Dictionary<string, FlowchartCellRegion>(Grid.Capture().Regions);
        if (Corruption == "shared") Regions["B"] = Regions["A"];
        Dictionary<int, double> Columns = [], Rows = [];
        if (Corruption == "column-gap") Columns[0] = Grid.Configuration.ColumnGap - 1;
        if (Corruption == "row-gap") Rows[0] = Grid.Configuration.RowGap - 1;
        if (Corruption == "nonfinite-gap") Columns[0] = double.NaN;
        Grid.Restore(new(Grid, Regions, Columns, Rows));
        var Layout = new FlowchartLayoutResult();
        foreach (var Node in Nodes) Layout.NodeBoundsMutable.Add(Node.CellNode!.Bounds(Node.CellNode.Region));
        Assert.That(CellGridAudit.Faults(new(), Layout, Nodes).Keys, Does.Contain("node:A"));
        if (Corruption == "shared")
            Assert.That(RouteRuleCatalogueAuditTests.GridFaults(Layout, Layout.NodeBounds[0].CenterX,
                Layout.NodeBounds[0].CenterY, DiagramGridMetrics.GRID_UNIT_PX), Is.Empty,
                "Coincident nodes satisfy the old lattice despite sharing every occupied cell.");
    }

    /// <summary>Fractional ownership is excluded by integer coordinates, and empty or overflowing rectangles cannot be constructed.</summary>
    [Test]
    public void RegionRepresentationRequiresContiguousWholeCells()
    {
        Assert.That(typeof(FlowchartCellRegion).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Where(Property => Property.Name is "StartRow" or "StartColumn" or "RowSpan" or "ColumnSpan" or "EndRow" or "EndColumn").All(Property => Property.PropertyType == typeof(int)), Is.True);
        Assert.Throws<ArgumentOutOfRangeException>(() => new FlowchartCellRegion(0, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FlowchartCellRegion(0, 0, 1, -1));
        Assert.Throws<OverflowException>(() => new FlowchartCellRegion(int.MaxValue, 0, 1, 1));
    }

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

    /// <summary>Every frame-edge corruption detected by the old lattice is rejected against the authoritative frame projection.</summary>
    /// <param name="Edge">Frame edge whose coordinate is changed independently.</param>
    [TestCase("left")]
    [TestCase("right")]
    [TestCase("top")]
    [TestCase("bottom")]
    public void RejectsEachOffLatticeFrameEdge(string Edge)
    {
        var Contract = new DiagramContract();
        var Group = new GroupContract { Id = "G" }; Group.ChildNodeIdsMutable.Add("A"); Contract.GroupsMutable.Add(Group);
        Contract.NodesMutable.Add(new() { Id = "A", ParentGroupId = "G" });
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules()); Grid.TryPlace("A", new(0, 0, 1, 1));
        var Node = new DiagramGridLayout.GridNode { Id = "A", GroupId = "G" }; Node.BindCells(Grid); Node.CellNode!.UseRoutingCoordinates(300);
        var Layout = new FlowchartLayoutResult(); Layout.NodeBoundsMutable.Add(Node.CellNode.Bounds(Node.CellNode.Region));
        var Frame = DiagramGridLayout.ProjectCellFrames(Contract, [Node], Grid.Capture()).Single();
        double Left = Frame.Left, Right = Frame.Right, Bottom = Math.Min(Frame.Top, Frame.Bottom), Top = Math.Max(Frame.Top, Frame.Bottom);
        switch (Edge) { case "left": Left++; break; case "right": Right++; break; case "top": Top++; break; case "bottom": Bottom++; break; }
        Layout.ClusterBoundsMutable.Add(new() { NodeId = "G", CenterX = (Left + Right) / 2, CenterY = (Top + Bottom) / 2,
            Width = Right - Left, Height = Top - Bottom });
        Assert.That(RouteRuleCatalogueAuditTests.GridFaults(Layout, Frame.Left, Math.Min(Frame.Top, Frame.Bottom),
            DiagramGridMetrics.GRID_UNIT_PX).Keys, Does.Contain($"frame:G:{Edge}"));
        Assert.That(CellGridAudit.Faults(Contract, Layout, [Node]).Keys, Does.Contain($"frame:G:{Edge}"));
    }
}

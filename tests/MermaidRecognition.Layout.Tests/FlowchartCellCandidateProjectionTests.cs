namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks pure candidate projection against accepted geometry without borrowing live mutable state.</summary>
[TestFixture]
public sealed class FlowchartCellCandidateProjectionTests
{
    /// <summary>Prepared ownership and gaps project exact accepted geometry while leaving current reservations and gaps untouched.</summary>
    [Test]
    public void PreparedGeometryMatchesAppliedStateWithoutATrialMutation()
    {
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 2)); Grid.TryPlace("B", new(2, 3, 1, 1));
        var Nodes = new List<DiagramGridLayout.GridNode> { new() { Id = "A" }, new() { Id = "B" } };
        foreach (var Node in Nodes) Node.BindCells(Grid);
        var Before = Grid.Capture();
        var Plan = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion> { ["B"] = new(2, 4, 1, 1) }, [new(true, 0, 60, 30)])!;
        var Projected = Nodes.Select(Node => Node.CellNode!.Bounds(Plan.After.Regions[Node.Id], null, Plan.After)).ToArray();
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Plan.TryApply(), Is.True);
        for (int Index = 0; Index < Nodes.Count; Index++)
            Assert.That((Nodes[Index].X, Nodes[Index].Y, Nodes[Index].Width, Nodes[Index].Height),
                Is.EqualTo((Projected[Index].CenterX, Projected[Index].CenterY, (int)Projected[Index].Width, (int)Projected[Index].Height)));
        Plan.Rollback(); Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Geometry cannot accidentally use another diagram's gap snapshot.</summary>
    [Test]
    public void RejectsForeignCandidateGeometry()
    {
        var A = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        var B = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Assert.That(() => A.Origin(new(0, 0, 1, 1), B.Capture()), Throws.ArgumentException);
        Assert.That(() => A.Measure(new(0, 0, 1, 1), B.Capture()), Throws.ArgumentException);
    }
}

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks pure candidate projection against accepted geometry without borrowing live mutable state.</summary>
[TestFixture]
public sealed class FlowchartCellCandidateProjectionTests
{
    /// <summary>Prepared ownership and gaps project exact accepted geometry while leaving current reservations and gaps untouched.</summary>
    [Test]
    public void PreparedGeometryMatchesAppliedStateWithoutATrialMutation()
    {
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("A", new(0, 0, 1, 2)); Grid.TryPlace("B", new(2, 3, 1, 1));
        List<DiagramGridLayout.GridNode> Nodes = [new() { Id = "A" }, new() { Id = "B" }];
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Node.BindCells(Grid);
        }

        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        FlowchartCellRepair.Assignment Plan = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion> { ["B"] = new(2, 4, 1, 1) }, [new(true, 0, 60, 30)])!;
        FlowchartLayoutNodeBounds[] Projected = [.. Nodes.Select(Node => Node.CellNode!.Bounds(Plan.After.Regions[Node.Id], null, Plan.After))];
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Plan.TryApply(), Is.True);
        for (int Index = 0; Index < Nodes.Count; Index++)
        {
            Assert.That((Nodes[Index].X, Nodes[Index].Y, Nodes[Index].Width, Nodes[Index].Height),
                Is.EqualTo((Projected[Index].CenterX, Projected[Index].CenterY, (int)Projected[Index].Width, (int)Projected[Index].Height)));
        }

        Plan.Rollback(); Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>Geometry cannot accidentally use another diagram's gap snapshot.</summary>
    [Test]
    public void RejectsForeignCandidateGeometry()
    {
        FlowchartLogicalGrid A = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        FlowchartLogicalGrid B = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Assert.That(() => A.Origin(new(0, 0, 1, 1), B.Capture()), Throws.ArgumentException);
        Assert.That(() => A.Measure(new(0, 0, 1, 1), B.Capture()), Throws.ArgumentException);
    }
}

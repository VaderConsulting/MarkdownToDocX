using System.Runtime.CompilerServices;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Verifies hard failures in test hosts and nonthrowing rejection in production Release policy.</summary>
[TestFixture]
[NonParallelizable]
public sealed class FlowchartCellPixelWriteGuardTests
{
    /// <summary>Detached pixel trial nodes still execute the guard and must be included in the scenario's coverage.</summary>
    [Test]
    public void DetachedTrialWritesUseTheActiveDiagramCoverage()
    {
        var Coverage = new FlowchartCellWriteCoverage { Nodes = 1 };
        using var Scope = FlowchartCellPixelWriteGuard.ActivateCoverage(Coverage);
        var Trial = new DiagramGridLayout.GridNode
        {
            X = 12.5
        };
        Assert.That(Coverage.Checks, Is.EqualTo(1));
        Assert.That(Coverage.BoundChecks, Is.Zero);
    }

    /// <summary>Nested group reflows contribute checks without inflating the primary diagram's node count.</summary>
    [Test]
    public void NestedCoverageAggregatesChecksAndRestoresTheParent()
    {
        var Parent = new FlowchartCellWriteCoverage { Nodes = 2 };
        var Child = new FlowchartCellWriteCoverage { Nodes = 1 };
        using var ParentScope = FlowchartCellPixelWriteGuard.ActivateCoverage(Parent);
        var Trial = new DiagramGridLayout.GridNode();
        using (FlowchartCellPixelWriteGuard.ActivateCoverage(Child)) Trial.X = 12.5;
        Trial.Y = 12.5;
        Assert.That(Child.Checks, Is.EqualTo(1));
        Assert.That(Parent.Checks, Is.EqualTo(2));
        Assert.That(Parent.Nodes, Is.EqualTo(2));
    }
    /// <summary>Each legacy write fails loudly in test hosts, retaining exact ownership and a repair-path diagnostic.</summary>
    /// <param name="Member">Direct pixel-write entry point.</param>
    [TestCase("X")]
    [TestCase("Y")]
    [TestCase("Width")]
    [TestCase("Height")]
    [TestCase("FrameFit")]
    [TestCase("Axis")]
    public void TestsThrowAndNameTheRepairPath(string Member)
    {
        var Fixture = Create();
        var Before = Fixture.Grid.Capture();
        Assert.That(() => AttemptWrite(Fixture.Node, Member), Throws.TypeOf<InvalidOperationException>());
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
        Assert.That(Fixture.Context.RejectedCellPixelWrites.Single().Description,
            Does.Contain(nameof(AttemptWrite)).And.Contain("test-stage"));
        Assert.That(Fixture.Node.CellWriteCoverage!.BoundChecks, Is.EqualTo(1));
        Assert.That(Fixture.Node.CellWriteCoverage.Rejections, Is.EqualTo(1));
    }

#if !DEBUG
    /// <summary>Production Release rejects a write without throwing or changing regions, gaps or derived geometry.</summary>
    /// <param name="Member">Direct pixel-write entry point.</param>
    [TestCase("X")]
    [TestCase("Y")]
    [TestCase("Width")]
    [TestCase("Height")]
    [TestCase("FrameFit")]
    [TestCase("Axis")]
    public void ReleaseKeepsLegalGeometryAndRecordsTheRepairPath(string Member)
    {
        var Fixture = Create();
        var Before = Fixture.Grid.Capture();
        var Pixels = (Fixture.Node.X, Fixture.Node.Y, Fixture.Node.Width, Fixture.Node.Height);
        AppContext.TryGetSwitch(FlowchartCellPixelWriteGuard.StrictTestSwitch, out bool Previous);
        AppContext.SetSwitch(FlowchartCellPixelWriteGuard.StrictTestSwitch, false);
        try { Assert.DoesNotThrow(() => AttemptWrite(Fixture.Node, Member)); }
        finally { AppContext.SetSwitch(FlowchartCellPixelWriteGuard.StrictTestSwitch, Previous); }
        Assert.That(Fixture.Grid.Capture(), Is.SameAs(Before));
        Assert.That((Fixture.Node.X, Fixture.Node.Y, Fixture.Node.Width, Fixture.Node.Height), Is.EqualTo(Pixels));
        Assert.That(Fixture.Context.RejectedCellPixelWrites.Single().Description, Does.Contain(nameof(AttemptWrite)));
        Fixture.Context.BeginRender();
        Assert.That(Fixture.Context.RejectedCellPixelWrites, Has.Count.EqualTo(1), "Painting cannot erase the conversion diagnostic.");
    }
#endif

    /// <summary>A legacy unbound write is counted separately and must never be presented as a successful bound-node guard check.</summary>
    [Test]
    public void UnboundWritesDoNotClaimGuardCoverage()
    {
        var Coverage = new FlowchartCellWriteCoverage { Nodes = 1 };
        var Node = new DiagramGridLayout.GridNode
        {
            CellWriteCoverage = Coverage,
            X = 12.5
        };
        Assert.That(Node.X, Is.EqualTo(12.5));
        Assert.That(Coverage.Checks, Is.EqualTo(1));
        Assert.That(Coverage.BoundChecks, Is.Zero);
        Assert.That(Coverage.BoundNodes, Is.Zero);
        Assert.That(Coverage.Rejections, Is.Zero);
    }

    /// <summary>Calls the real node setters through a stable repair method for diagnostic-path verification.</summary>
    /// <param name="Node">Cell-bound node whose pixels must stay read-only.</param>
    /// <param name="Member">Write entry point selected by the fixture.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AttemptWrite(DiagramGridLayout.GridNode Node, string Member)
    {
        switch (Member)
        {
            case "X": Node.X += 30; break;
            case "Y": Node.Y += 30; break;
            case "Width": Node.Width += 30; break;
            case "Height": Node.Height += 30; break;
            case "FrameFit": Node.SetPositionFromFrameFit(0.25, -0.25); break;
            case "Axis": Node.SetYAfterCoordinateTransform(-Node.Y); break;
        }
    }

    /// <summary>Creates a declared region and attaches its explicit per-diagram diagnostic sink.</summary>
    /// <returns>The owning grid, bound node and diagnostic context.</returns>
    private static (FlowchartLogicalGrid Grid, DiagramGridLayout.GridNode Node, FlowchartRenderContext Context) Create()
    {
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Grid.TryPlace("guarded", new(0, 0, 1, 1));
        var Context = new FlowchartRenderContext { ActiveLayoutStage = "test-stage" };
        var Node = new DiagramGridLayout.GridNode { Id = "guarded" };
        Node.BindCells(Grid, Context);
        return (Grid, Node, Context);
    }
}

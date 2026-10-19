using System.Reflection;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks title reservation before fitting and rejects the legacy late frame writer for authoritative cells.</summary>
[TestFixture]
[NonParallelizable]
public sealed class FlowchartCellFrameTitleTests
{
    /// <summary>A wide multiline title is included before fitting, including a nested member frame, rather than added after clearance was accepted.</summary>
    /// <param name="Nested">Whether the titled frame contains a nested frame.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void LongTitleIsReservedBeforeTheScoredFit(bool Nested)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nsubgraph G[Title]\n"
            + (Nested ? "subgraph Inner\nA[Member]\nend\n" : "A[Member]\n")
            + "end\nsubgraph H[Neighbour]\nB[Member]\nend");
        Contract.Groups.Single(Group => Group.Id == "G").Title = "A deliberately wide frame title requiring its own clearance\nSecond measured line\nThird measured line\nFourth measured line";
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        Assert.That(Grid.TryPlace("A", new(1, 0, 1, 1)), Is.True);
        Assert.That(Grid.TryPlace("B", new(0, 0, 1, 1)), Is.True);
        var Nodes = Contract.Nodes.Select(Node => new DiagramGridLayout.GridNode { Id = Node.Id, GroupId = Node.ParentGroupId }).ToList();
        foreach (var Node in Nodes) Node.BindCells(Grid);
        List<DiagramGridLayout.GridFrame> Frames = [];
        DiagramGridLayout.ReplaceCellFrames(Contract, Nodes, Frames);
        var Reservation = FlowchartFrameTitleReservation.Measure(Contract.Groups.Single(Group => Group.Id == "G").Title!,
            FlowchartRenderOptions.FromContract(Contract).FontSizePx);
        var Before = Frames.Single(Frame => Frame.Id == "G");
        Assert.That(Before.TitleStrip, Is.EqualTo(Reservation.BandHeightPx));
        Assert.That(Before.Right - Before.Left, Is.GreaterThanOrEqualTo(Reservation.TextWidthPx + 2 * Before.Padding));
        Assert.That(DiagramGridLayout.CountUnrelatedFrameOverlaps(Contract, Frames), Is.GreaterThan(0));
        var Context = new FlowchartRenderContext();
        DiagramGridLayout.FitFrames(Contract, Nodes, Frames, false, "long measured title", Context);
        Assert.That(DiagramGridLayout.CountUnrelatedFrameOverlaps(Contract, Frames), Is.Zero);
        Assert.That(Context.MoveBeforeDetourBudget.CandidateMoves, Is.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
        var Fitted = Frames.Single(Frame => Frame.Id == "G");
        Assert.That(Fitted.TitleStrip, Is.EqualTo(Reservation.BandHeightPx));
        Assert.That(Fitted.Right - Fitted.Left, Is.GreaterThanOrEqualTo(Reservation.TextWidthPx + 2 * Fitted.Padding));
        var Member = Nodes.Single(Node => Node.Id == "A");
        Assert.That(Member.Y - Member.Height / 2.0 - Fitted.Top, Is.GreaterThanOrEqualTo(Fitted.TitleStrip));
    }

    /// <summary>The legacy writer would grow a long-title frame across a legal gap into its neighbour; the same calls on a bound model preserve all caches and regions.</summary>
    [Test]
    public void LateTitleGrowthCannotEngulfTheNeighbourOfACellFrame()
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nsubgraph G[A long frame title that spans the neighbour horizontally]\nA[Member]\nend\nsubgraph H[Neighbour]\nB[Member]\nend");
        Assert.That(FlowchartDagrePlacement.TryPlace(Contract, out var Native), Is.True);
        var StateType = typeof(DiagramGridLayout).GetNestedType("GridLayoutState", BindingFlags.NonPublic)!;
        var State = Activator.CreateInstance(StateType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            // The first half is explicitly the unsafe unbound control; the
            // second binds its fresh owners and exercises the production guard.
            null, [Contract, Native, new FlowchartRenderContext { UseCellRegions = false }], null)!;
        typeof(DiagramGridLayout).GetNestedType("GridPreparationStage", BindingFlags.NonPublic)!
            .GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [State]);
        var Layout = (FlowchartLayoutResult)StateType.GetProperty("Copy")!.GetValue(State)!;
        var Nodes = (List<DiagramGridLayout.GridNode>)StateType.GetProperty("Placed")!.GetValue(State)!;
        int Unit = DiagramGridMetrics.GRID_UNIT_PX;
        var Title = FlowchartFrameTitleReservation.Measure(Contract.Groups[0].Title!, FlowchartRenderOptions.FromContract(Contract).FontSizePx);
        double Width = DiagramGridMetrics.CeilToGrid(Title.TextWidthPx + 2 * DiagramGridMetrics.NODE_GAP_PX);
        double Height = Title.BandHeightPx + 2 * DiagramGridMetrics.NODE_GAP_PX + Unit;
        var G = new FlowchartLayoutNodeBounds { NodeId = "G", Width = Width, Height = Height, CenterY = -Height / 2 };
        var H = new FlowchartLayoutNodeBounds { NodeId = "H", Width = Width, Height = Height, CenterY = Unit + Height / 2 };
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds> { ["G"] = G, ["H"] = H };
        Layout.ClusterBoundsMutable.Clear(); Layout.ClusterBoundsMutable.AddRange([G, H]);
        var Grow = typeof(DiagramGridLayout).GetNestedType("ModelRouteCompletionStage", BindingFlags.NonPublic)!
            .GetMethod("GrowFrameTitleClearance", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.That(FrameOverlaps(Contract, Layout), Is.Empty);
        Assert.That((bool)Grow.Invoke(null, [State, Bounds, "G"])!, Is.True);
        Assert.That((bool)Grow.Invoke(null, [State, Bounds, "G"])!, Is.True);
        Assert.That(FrameOverlaps(Contract, Layout), Is.Not.Empty, "The unbound control demonstrates the old late-growth overlap.");
        Bounds["G"] = G; Bounds["H"] = H;
        Layout.ClusterBoundsMutable.Clear(); Layout.ClusterBoundsMutable.AddRange([G, H]);
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        foreach (var Pair in Nodes.Select((Node, Index) => (Node, Index)))
        {
            Assert.That(Grid.TryPlace(Pair.Node.Id, new(Pair.Index, 0, 1, 1)), Is.True);
            Pair.Node.BindCells(Grid);
        }
        var Before = Grid.Capture();
        Assert.That((bool)Grow.Invoke(null, [State, Bounds, "G"])!, Is.False);
        Assert.That((bool)Grow.Invoke(null, [State, Bounds, "G"])!, Is.False);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
        Assert.That(Bounds["G"], Is.SameAs(G));
        Assert.That(Layout.ClusterBounds.Single(Frame => Frame.NodeId == "G"), Is.SameAs(G));
        Assert.That(FrameOverlaps(Contract, Layout), Is.Empty);
    }

    /// <summary>Independently checks unrelated output rectangles without relying on the title-growth writer.</summary>
    /// <param name="Contract">Fixture frame identities.</param>
    /// <param name="Layout">Current output bounds.</param>
    /// <returns>The overlapping frame pairs.</returns>
    private static IEnumerable<string> FrameOverlaps(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        var A = Layout.ClusterBounds.Single(Frame => Frame.NodeId == Contract.Groups[0].Id);
        var B = Layout.ClusterBounds.Single(Frame => Frame.NodeId == Contract.Groups[1].Id);
        if (Math.Abs(A.CenterX - B.CenterX) * 2 < A.Width + B.Width && Math.Abs(A.CenterY - B.CenterY) * 2 < A.Height + B.Height)
            yield return A.NodeId + "/" + B.NodeId;
    }
}

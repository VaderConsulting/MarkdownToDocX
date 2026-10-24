using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Exercises complete rule 4.12 assignments and exact rollback without relying on a placement heuristic.</summary>
[TestFixture, NonParallelizable]
public sealed class BendRemovingPortRepairTests
{
    /// <summary>A straightening trial cannot move either lone port away from its allocated side centre.</summary>
    [Test]
    public void AlignmentKeepsLonePortsAtTheirAllocatedSideCentres()
    {
        var Contract = new DiagramContract { Direction = "LR" };
        var Layout = new FlowchartLayoutResult { UsesGridRoutes = true, RenderContext = new() { UseCellRegions = true } };
        AddNode(Contract, Layout, "a", 0, 0, 120, 120);
        AddNode(Contract, Layout, "b", 300, 20, 120, 120);
        AddRoute(Contract, Layout, "ab", "a", "b", "right", "left", [(60, 0), (180, 0), (180, 20), (240, 20)]);
        using var Active = FlowchartRenderContext.Activate(Layout.RenderContext);
        FlowchartBendRemovingPortRepair.Apply(Contract, Layout);
        var Route = Layout.EdgeRoutes.Single();
        foreach (var End in new[] { (Id: "a", Side: Route.ExitSide, Point: Route.Points[0]),
            (Id: "b", Side: Route.EntrySide, Point: Route.Points[^1]) })
        {
            var Box = Layout.NodeBounds.Single(Box => Box.NodeId == End.Id);
            Assert.That(End.Side is "left" or "right" ? End.Point.Y : End.Point.X,
                Is.EqualTo(End.Side is "left" or "right" ? Box.CenterY : Box.CenterX).Within(0.001));
        }
    }

    /// <summary>A legal same-side exchange straightens both lines and preserves their ordered, distinct ports.</summary>
    [Test]
    public void SameSideSwapRemovesBendsWithoutAddingCrossings()
    {
        var (Contract, Layout) = Fixture(false);
        bool Accepted = false;
        FlowchartBendRemovingPortRepair.SwapDecisionObserver = (Id, Decision) => Accepted |= Id == "hub" && Decision == "accepted";
        try
        {
            using var Active = FlowchartRenderContext.Activate(Layout.RenderContext);
            int Removed = FlowchartBendRemovingPortRepair.Apply(Contract, Layout);
            Assert.That(Accepted, Is.True);
            Assert.That(Removed, Is.EqualTo(6));
            Assert.That(Layout.EdgeRoutes.All(Route => Route.Points.Count == 2), Is.True);
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout), Is.Empty);
        }
        finally { FlowchartBendRemovingPortRepair.SwapDecisionObserver = null; }
    }

    /// <summary>A bend-saving exchange is rejected and restored exactly when its new straight run crosses an unrelated line.</summary>
    [Test]
    public void SwapAddingAnUnrelatedCrossingIsRejectedWithExactRollback()
    {
        var (Contract, Layout) = Fixture(true);
        string Before = JsonSerializer.Serialize(Layout.EdgeRoutes);
        bool Rejected = false;
        FlowchartBendRemovingPortRepair.SwapDecisionObserver = (Id, Decision) =>
        {
            if (Rejected || Id != "hub" || Decision != "crossing-rejected") return;
            Rejected = true;
            Assert.That(JsonSerializer.Serialize(Layout.EdgeRoutes), Is.EqualTo(Before));
        };
        try
        {
            using var Active = FlowchartRenderContext.Activate(Layout.RenderContext);
            FlowchartBendRemovingPortRepair.Apply(Contract, Layout);
            Assert.That(Rejected, Is.True);
        }
        finally { FlowchartBendRemovingPortRepair.SwapDecisionObserver = null; }
    }

    /// <summary>Builds two reversed hub slots, with an optional independent line crossing only the proposed straight assignment.</summary>
    /// <param name="Crossing">Whether to include the remote crossing obstacle.</param>
    /// <returns>Independent declared topology and complete route geometry.</returns>
    private static (DiagramContract Contract, FlowchartLayoutResult Layout) Fixture(bool Crossing)
    {
        var Contract = new DiagramContract { Direction = "LR" };
        var Layout = new FlowchartLayoutResult { UsesGridRoutes = true, RenderContext = new() { UseCellRegions = true } };
        AddNode(Contract, Layout, "hub", 0, 0, 120, 120);
        AddNode(Contract, Layout, "a", 300, 20, 120, 60);
        AddNode(Contract, Layout, "b", 600, -20, 120, 60);
        AddRoute(Contract, Layout, "ha", "hub", "a", "right", "left", [(60, -20), (180, -20), (180, 20), (240, 20)]);
        AddRoute(Contract, Layout, "hb", "hub", "b", "right", "left", [(60, 20), (120, 20), (120, -80), (480, -80), (480, -20), (540, -20)]);
        if (Crossing)
        {
            AddNode(Contract, Layout, "c", 150, 60, 20, 20);
            AddNode(Contract, Layout, "d", 150, 0, 20, 20);
            AddRoute(Contract, Layout, "cd", "c", "d", "bottom", "top", [(150, 50), (150, 10)]);
        }
        return (Contract, Layout);
    }

    /// <summary>Adds the declared rectangular body and its fixed routing bounds.</summary>
    /// <param name="Contract">Owning declarations.</param>
    /// <param name="Layout">Owning fixed route model.</param>
    /// <param name="Id">Unique node identity.</param>
    /// <param name="X">Centre X.</param>
    /// <param name="Y">Centre Y in current routing coordinates.</param>
    /// <param name="Width">Positive rectangle width.</param>
    /// <param name="Height">Positive rectangle height.</param>
    private static void AddNode(DiagramContract Contract, FlowchartLayoutResult Layout, string Id, double X, double Y, double Width, double Height)
    {
        Contract.NodesMutable.Add(new NodeContract { Id = Id, Shape = "rect" });
        Layout.NodeBoundsMutable.Add(new() { NodeId = Id, CenterX = X, CenterY = Y, Width = Width, Height = Height });
    }

    /// <summary>Adds one declared link and independently owned normal-preserving points.</summary>
    /// <param name="Contract">Owning declarations.</param>
    /// <param name="Layout">Owning fixed route model.</param>
    /// <param name="Id">Unique link identity.</param>
    /// <param name="Source">Source node identity.</param>
    /// <param name="Target">Target node identity.</param>
    /// <param name="Exit">Source side.</param>
    /// <param name="Entry">Target side.</param>
    /// <param name="Points">Complete independent polyline.</param>
    private static void AddRoute(DiagramContract Contract, FlowchartLayoutResult Layout, string Id, string Source, string Target,
        string Exit, string Entry, (double X, double Y)[] Points)
    {
        Contract.EdgesMutable.Add(new EdgeContract { Id = Id, SourceId = Source, TargetId = Target });
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = Id, SourceId = Source, TargetId = Target, ExitSide = Exit, EntrySide = Entry,
            ExitNormalX = Exit == "right" ? 1 : 0, ExitNormalY = Exit == "bottom" ? -1 : 0,
            EntryNormalX = Entry == "left" ? -1 : 0, EntryNormalY = Entry == "top" ? 1 : 0 };
        Route.Points.AddRange(Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
        Layout.EdgeRoutesMutable.Add(Route);
    }
}

using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class ReturnRouteOrderTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void AlignedReturn_UsesFreeOppositeSideWhenForwardLinkOccupiesItsCorridor(bool Transpose)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nfirst --> middle\nmiddle --> last\nlast --> middle\nmiddle --> other");
        var Bounds = Boxes(Transpose);
        var Layout = new FlowchartLayoutResult();
        Add(Layout, Contract, 0, Transpose, (0, 30), (0, 150));
        Add(Layout, Contract, 1, Transpose, (0, 210), (0, 330));
        var Return = Add(Layout, Contract, 2, Transpose, (75, 360), (115, 360), (115, 180), (75, 180));
        var Forward = Add(Layout, Contract, 3, Transpose, (75, 200), (240, 200), (240, 330));
        FlowchartReturnRouteOrder.Apply(Layout, Contract, Bounds);
        Assert.That(Transpose ? Return.Points[0].Y : Return.Points[0].X, Is.EqualTo(-75));
        Assert.That(FlowchartBranchRouteOrder.Intersections(Points(Return), Points(Forward)).Crossings, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IncomingAndOutgoingTracksOnSameFace_SwapTogetherToRemoveCrossing(bool Transpose)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nfirst --> middle\nfirst --> other\nother --> first");
        var Layout = new FlowchartLayoutResult();
        var Bounds = Boxes(Transpose);
        Add(Layout, Contract, 0, Transpose, (0, 30), (0, 150));
        var Forward = Add(Layout, Contract, 1, Transpose, (75, 10), (270, 10), (270, 330));
        var Return = Add(Layout, Contract, 2, Transpose, (240, 330), (240, 0), (75, 0));
        FlowchartReturnRouteOrder.Apply(Layout, Contract, Bounds);
        Assert.That(FlowchartBranchRouteOrder.Intersections(Points(Return), Points(Forward)).Crossings, Is.Zero);
        Assert.That(Transpose ? Return.Points[0].Y : Return.Points[0].X, Is.EqualTo(270));
        Assert.That(Transpose ? Forward.Points[^1].Y : Forward.Points[^1].X, Is.EqualTo(240));
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> Boxes(bool Transpose) =>
        new[] { ("first", 0d, 0d), ("middle", 0d, 180d), ("last", 0d, 360d), ("other", 240d, 360d) }
            .ToDictionary(Item => Item.Item1, Item => new FlowchartLayoutNodeBounds { NodeId = Item.Item1,
                CenterX = Transpose ? Item.Item3 : Item.Item2, CenterY = Transpose ? Item.Item2 : Item.Item3,
                Width = Transpose ? 60 : 150, Height = Transpose ? 150 : 60 });

    private static FlowchartLayoutEdgeRoute Add(FlowchartLayoutResult Layout,
        MermaidRecognition.Contract.DiagramContract Contract, int Index, bool Transpose, params (double X, double Y)[] Points)
    {
        var Edge = Contract.Edges[Index];
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId };
        foreach (var Point in Points) Route.Points.Add(new() { X = Transpose ? Point.Y : Point.X, Y = Transpose ? Point.X : Point.Y });
        Layout.EdgeRoutesMutable.Add(Route);
        return Route;
    }
    private static List<(double X, double Y)> Points(FlowchartLayoutEdgeRoute Route) => Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
}

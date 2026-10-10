using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class NestedCounterflowTrackTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void OpposingContainerLink_UsesAFreeTrackBeyondTheDescendantLabel(bool Transpose, bool Blocked)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nsubgraph first\na\nend\nsubgraph second\nb\nend\na -->|requested items| b\nsecond --> first");
        FlowchartLayoutNodeBounds Box(string Id, double X, double Y, double Width, double Height) => new()
        {
            NodeId = Id, CenterX = Transpose ? Y : X, CenterY = Transpose ? X : Y,
            Width = Transpose ? Height : Width, Height = Transpose ? Width : Height
        };
        var Bounds = new[] { Box("first", 0, 0, 240, 160), Box("second", 0, 400, 240, 160),
            Box("a", 0, 20, 150, 60), Box("b", 0, 420, 150, 60) }.ToDictionary(Box => Box.NodeId);
        if (Blocked) Bounds.Add("obstacle", Box("obstacle", 90, 200, 150, 60));
        var Layout = new FlowchartLayoutResult();
        FlowchartLayoutEdgeRoute Add(int Index, params (double X, double Y)[] Points)
        {
            var Edge = Contract.Edges[Index];
            var Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId };
            foreach (var Point in Points) Route.Points.Add(new() { X = Transpose ? Point.Y : Point.X, Y = Transpose ? Point.X : Point.Y });
            Layout.EdgeRoutesMutable.Add(Route);
            return Route;
        }
        var Forward = Add(0, (0, 50), (0, 390));
        var Return = Add(1, (-12, 320), (-12, 80));
        FlowchartNestedCounterflowTracks.Apply(Layout, Contract, Bounds);
        double Track = Transpose ? Return.Points[0].Y : Return.Points[0].X;
        Assert.That(Math.Sign(Track), Is.EqualTo(Blocked ? -1 : 1));
        var Options = FlowchartRenderOptions.FromContract(Contract);
        var LabelSize = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Contract.Edges[0].Label!, Options, Options.EdgeLabelFontSizePx);
        Assert.That(Math.Abs(Track), Is.GreaterThanOrEqualTo((Transpose ? LabelSize.Height : LabelSize.Width) / 2 + 12),
            "The track must reserve the label's extent across the flow axis.");
        Assert.That(Return.Points.All(Point => (Transpose ? Point.Y : Point.X) == Track), Is.True);
        Assert.That(FlowchartBranchRouteOrder.Intersections(Points(Forward), Points(Return)).Crossings, Is.Zero);
        Assert.That(FlowchartBranchRouteOrder.Intersections(Points(Forward), Points(Return)).Overlap, Is.Zero);
        Assert.That(Transpose ? Return.Points[0].X : Return.Points[0].Y, Is.EqualTo(320));
        Assert.That(Transpose ? Return.Points[^1].X : Return.Points[^1].Y, Is.EqualTo(80));
    }

    [TestCase("grid", "TB")]
    [TestCase("open", "TB")]
    [TestCase("grid", "LR")]
    [TestCase("open", "LR")]
    public void NestedOpposingRelationships_KeepLabelsClearAfterVisiblePortsAreAttached(string Mode, string Direction)
    {
        string Source = $"---\nconfig:\n  layout: {Mode}\n---\nenterprise {Direction}\n"
            + "group outerA \"First area\"\ngroup outerB \"Second area\"\n"
            + "application-component containerA \"First container\" in outerA\n"
            + "application-component containerB \"Second container\" in outerB\n"
            + "application-function childA \"First function\" in containerA\n"
            + "application-function childB \"Second function\" in containerB\n"
            + "childA -flow-> childB \"requested items\"\ncontainerB -serving-> containerA";
        var Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.Errors, Is.Empty);
        var Layout = EnterpriseLayoutEngine.Layout(Analysis.Document);
        var Edges = EnterpriseFlowchartEdgeRouter.Route(Analysis.Document, Layout.Nodes);
        var Forward = Edges.Single(Edge => Edge.Label.Length > 0);
        Assert.That(Forward.LabelBreak, Is.Not.Null);
        foreach (var Other in Edges.Where(Edge => Edge != Forward))
        foreach (var Segment in Other.SvgPoints.Zip(Other.SvgPoints.Skip(1)))
            Assert.That(Math.Max(Segment.First.X, Segment.Second.X) > Forward.LabelX - Forward.LabelWidth / 2 - 6
                && Math.Min(Segment.First.X, Segment.Second.X) < Forward.LabelX + Forward.LabelWidth / 2 + 6
                && Math.Max(Segment.First.Y, Segment.Second.Y) > Forward.LabelY - Forward.LabelHeight / 2 - 6
                && Math.Min(Segment.First.Y, Segment.Second.Y) < Forward.LabelY + Forward.LabelHeight / 2 + 6,
                Is.False, $"No opposing connector may overwrite the caption at {Forward.LabelX},{Forward.LabelY} ({Forward.LabelWidth}x{Forward.LabelHeight}). Forward: {string.Join(';', Forward.SvgPoints)}. Opposing: {string.Join(';', Other.SvgPoints)}.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UnrelatedOrFullyBlockedTrack_KeepsTheExistingPorts(bool Blocked)
    {
        var Contract = MermaidRecognizer.Parse(Blocked
            ? "flowchart TB\nsubgraph first\na\nend\nsubgraph second\nb\nend\na -->|information| b\nsecond --> first"
            : "flowchart TB\na -->|information| b\nsecond --> first");
        var Bounds = new[] { ("first", 0d, 0d, 240d, 160d), ("second", 0d, 400d, 240d, 160d),
            ("a", 0d, 20d, 150d, 60d), ("b", 0d, 420d, 150d, 60d) }
            .ToDictionary(Item => Item.Item1, Item => new FlowchartLayoutNodeBounds { NodeId = Item.Item1,
                CenterX = Item.Item2, CenterY = Item.Item3, Width = Item.Item4, Height = Item.Item5 });
        if (Blocked) Bounds.Add("obstacle", new() { NodeId = "obstacle", CenterY = 200, Width = 400, Height = 60 });
        var Layout = new FlowchartLayoutResult();
        foreach (var Edge in Contract.Edges)
        {
            bool Return = Edge.SourceId == "second";
            var Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId };
            Route.Points.Add(new() { X = Return ? -12 : 0, Y = Return ? 320 : 50 });
            Route.Points.Add(new() { X = Return ? -12 : 0, Y = Return ? 80 : 390 });
            Layout.EdgeRoutesMutable.Add(Route);
        }
        FlowchartNestedCounterflowTracks.Apply(Layout, Contract, Bounds);
        Assert.That(Layout.EdgeRoutes[1].Points.Select(Point => Point.X), Is.All.EqualTo(-12));
    }

    private static List<(double X, double Y)> Points(FlowchartLayoutEdgeRoute Route) => Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
}

using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class PlacementHintLayoutTests
{
    [TestCase("note", false)]
    [TestCase("note", true)]
    [TestCase("application-component", true)]
    public void InitialAbsolutePinsUseTheRequestedCellsForNotesAndContainedMembers(string Kind, bool Grouped)
    {
        string Source = "enterprise\n" + (Grouped ? "group area\n" : "") + Kind + " item \"Item\""
            + (Grouped ? " in area" : "") + " at 1 2\n";
        var Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.HasErrors, Is.False);
        var Context = EnterpriseLayoutEngine.Layout(Analysis.Document).RoutingContext!;
        Assert.That(Context, Is.Not.Null);
        var Region = Context.Layout.CellNodes!.Single(Node => Node.Id == "item").CellNode!.Region;
        Assert.That((Region.StartRow, Region.StartColumn), Is.EqualTo((1, 2)));
        Assert.That(FlowchartRouteRuleChecker.Check(Context.Contract, Context.Layout), Is.Empty);
    }

    [Test]
    public void DirectionHints_AreAppliedInFinishedPageCoordinates()
    {
        const string Source = """
            enterprise TB
              application-component abovePeer "Above peer"
              application-component aboveNode "Above node" above abovePeer
              application-component belowPeer "Below peer"
              application-component belowNode "Below node" below belowPeer
            """;

        string Svg = Render(Source);
        string NorthSvg = Render("""
            enterprise TB
              application-component northSource "North source"
              application-component northTarget "North target"
              northSource -flow-> northTarget [leave N]
            """);
        string SouthSvg = Render("""
            enterprise TB
              application-component southSource "South source"
              application-component southTarget "South target"
              southSource -flow-> southTarget [leave S]
            """);

        double abovePeerY = LabelY(Svg, "Above peer");
        double aboveNodeY = LabelY(Svg, "Above node");
        double belowPeerY = LabelY(Svg, "Below peer");
        double belowNodeY = LabelY(Svg, "Below node");
        (double StartY, double NextY) north = FirstTwoPathY(NorthSvg, "northSource", "northTarget");
        (double StartY, double NextY) south = FirstTwoPathY(SouthSvg, "southSource", "southTarget");

        Assert.Multiple(() =>
        {
            Assert.That(aboveNodeY, Is.LessThan(abovePeerY), "above must render toward the top of the page");
            Assert.That(belowNodeY, Is.GreaterThan(belowPeerY), "below must render toward the bottom of the page");
            Assert.That(north.NextY, Is.LessThan(north.StartY), "leave N must route upward on the page");
            Assert.That(south.NextY, Is.GreaterThan(south.StartY), "leave S must route downward on the page");
        });

        static double LabelY(string Svg, string Label)
        {
            Match match = Regex.Match(Svg,
                "<text x=\"[^\"]+\" y=\"([^\"]+)\"[^>]*>" + Regex.Escape(Label) + "</text>");
            Assert.That(match.Success, Is.True, $"Rendered label '{Label}' was not found.");
            return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        static string Render(string Source)
        {
            DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(
                EnterpriseValidator.Analyze(Source).Document);
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
            return FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract, Layout);
        }

        static (double StartY, double NextY) FirstTwoPathY(string Svg, string SourceId, string TargetId)
        {
            Match path = Regex.Match(Svg,
                "<path d=\"([^\"]+)\"[^>]*data-edge-source=\"" + Regex.Escape(SourceId)
                + "\" data-edge-target=\"" + Regex.Escape(TargetId) + "\"");
            if (path.Success)
            {
                MatchCollection coordinates = Regex.Matches(path.Groups[1].Value,
                    @"-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?");
                Assert.That(coordinates.Count, Is.GreaterThanOrEqualTo(2),
                    $"Rendered route {SourceId}->{TargetId} has no initial segment.");
                return (CoordinateY(coordinates[0].Value), CoordinateY(coordinates[1].Value));
            }

            Match line = Regex.Match(Svg,
                "<line[^>]*y1=\"([^\"]+)\"[^>]*y2=\"([^\"]+)\"[^>]*data-edge-source=\""
                + Regex.Escape(SourceId) + "\"[^>]*data-edge-target=\"" + Regex.Escape(TargetId) + "\"");
            Assert.That(line.Success, Is.True, $"Rendered route {SourceId}->{TargetId} was not found.");
            return (double.Parse(line.Groups[1].Value, CultureInfo.InvariantCulture),
                double.Parse(line.Groups[2].Value, CultureInfo.InvariantCulture));

            static double CoordinateY(string Coordinate) =>
                double.Parse(Coordinate.Split(',')[1], CultureInfo.InvariantCulture);
        }
    }

    [Test]
    public void AgainstFlowChain_HonoursAllRelativeHintsBeforeRouting()
    {
        const string Source = """
            enterprise
              application-component c1 "Step 1"
              application-component c2 "Step 2" above c1
              application-component c3 "Step 3" above c2
              application-component c4 "Step 4" above c3
              application-component c5 "Step 5" above c4
              application-component c6 "Step 6" above c5
              c1 -flow-> c2
              c2 -flow-> c3
              c3 -flow-> c4
              c4 -flow-> c5
              c5 -flow-> c6
            """;
        EnterpriseDocument Document = EnterpriseValidator.Analyze(Source).Document;
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutNodeBounds[] Chain = [.. Enumerable.Range(1, 6).Select(Index => Layout.NodeBounds.Single(Node => Node.NodeId == $"c{Index}"))];
        Assert.Multiple(() =>
        {
            Assert.That(Chain.Select(Node => Node.CenterX).Distinct().Count(), Is.EqualTo(1));
            for (int Index = 1; Index < Chain.Length; Index++)
            {
                Assert.That(Chain[Index].CenterY, Is.GreaterThan(Chain[Index - 1].CenterY));
            }

            Assert.That(Layout.EdgeRoutes, Has.Count.EqualTo(5));
            Assert.That(Layout.EdgeRoutes.All(Route => Route.Points.Count == 2), Is.True);
        });
    }

    [Test]
    public void AgainstFlowLineHints_NeverRouteThroughEndpointBodies()
    {
        const string Source = """
            enterprise TB
              application-component c1 "Node 1"
              application-component c2 "Node 2"
              application-component c3 "Node 3"
              application-component c4 "Node 4"
              application-component c5 "Node 5"
              application-component c6 "Node 6"
              application-component c7 "Node 7"
              application-component c8 "Node 8"
              c1 -flow-> c2 [leave N]
              c2 -flow-> c3 [leave N]
              c3 -flow-> c4 [leave N]
              c4 -flow-> c5 [leave N]
              c5 -flow-> c6 [leave N]
              c6 -flow-> c7 [leave N]
              c7 -flow-> c8 [leave N]
            """;
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds
            .ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            Assert.That(Route.Points, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(Route.ExitSide, Is.EqualTo("top"), $"{Route.SourceId}->{Route.TargetId} must leave north.");
            Assert.That(Route.Points[1].Y, Is.GreaterThan(Route.Points[0].Y),
                $"{Route.SourceId}->{Route.TargetId} must travel north in the post-flip Y-up model.");
            foreach ((FlowchartLayoutPoint A, FlowchartLayoutPoint B) in Route.Points.Zip(Route.Points.Skip(1)))
            {
                foreach (FlowchartLayoutNodeBounds Box in Bounds.Values)
                {
                    Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    A.X, A.Y, B.X, B.Y, Box), Is.False,
                    $"Hinted route {Route.SourceId}->{Route.TargetId} enters node {Box.NodeId}: "
                    + string.Join(" ", Route.Points.Select(Point => $"({Point.X},{Point.Y})")));
                }
            }
        }
    }

    [Test]
    public void RelativeHints_KeepSourceOrderedChainOnTheSameRow()
    {
        const string Source = """
            enterprise
              application-component a1 "Part 1"
              application-component a2 "Part 2" right-of a1
              application-component a3 "Part 3" right-of a2
              application-component a4 "Part 4" right-of a3
              application-component a5 "Part 5" right-of a4
              a1 -flow-> a2
              a2 -flow-> a3
              a3 -flow-> a4
              a4 -flow-> a5
            """;

        Dictionary<string, FlowchartLayoutNodeBounds> Layout = LayoutOf(Source);
        FlowchartLayoutNodeBounds[] Ordered = [.. Enumerable.Range(1, 5).Select(Index => Layout[$"a{Index}"])];
        Assert.Multiple(() =>
        {
            for (int Index = 1; Index < Ordered.Length; Index++)
            {
                Assert.That(Ordered[Index].CenterX, Is.GreaterThan(Ordered[Index - 1].CenterX));
            }

            Assert.That(Ordered.Select(Node => Node.CenterY).Distinct().Count(), Is.EqualTo(1),
                "right-of places each node on its peer's row");
        });
    }

    [Test]
    public void LaterPeerMove_CarriesEarlierHintedDependent()
    {
        const string Source = """
            enterprise
              application-component a "A" below b
              application-component b "B" below c
              application-component c "C" below a
              a -flow-> b
              b -flow-> c
            """;

        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.Errors, Is.Empty);
        Assert.That(Analysis.Document.Elements["c"].PlacementHint!.IsActive, Is.False);
        Dictionary<string, FlowchartLayoutNodeBounds> Layout = LayoutOf(Analysis.Document);
        Assert.Multiple(() =>
        {
            Assert.That(Layout["a"].CenterY, Is.LessThan(Layout["b"].CenterY));
            Assert.That(Layout["b"].CenterY, Is.LessThan(Layout["c"].CenterY));
            Assert.That(Layout["a"].CenterX, Is.EqualTo(Layout["b"].CenterX));
            Assert.That(Layout["b"].CenterX, Is.EqualTo(Layout["c"].CenterX));
        });
    }

    [Test]
    public void GroupAndChildHints_MoveWholeGroupAndKeepChildRelations()
    {
        const string Source = """
            enterprise
              application-component out "Outside"
              group box "Box" right-of out
              application-component k1 "Kid 1" in box
              application-component k2 "Kid 2" in box below k1
              application-component k3 "Kid 3" in box right-of k1
              out -flow-> k1
              k1 -flow-> k2
              k1 -flow-> k3
            """;

        Dictionary<string, FlowchartLayoutNodeBounds> Layout = LayoutOf(Source);
        Assert.Multiple(() =>
        {
            Assert.That(Layout["k1"].CenterX, Is.GreaterThan(Layout["out"].CenterX));
            Assert.That(Layout["k2"].CenterY, Is.LessThan(Layout["k1"].CenterY));
            Assert.That(Layout["k3"].CenterX, Is.GreaterThan(Layout["k1"].CenterX));
        });
    }

    [Test]
    public void LaterGroupHints_UseThePeersUpdatedFrame()
    {
        const string Source = """
            enterprise TB
              group first "First"
              application-component a "A" in first
              group second "Second" below first
              application-component b "B" in second
              group third "Third" below second
              application-component c "C" in third
              a -flow-> b
              b -flow-> c
            """;
        EnterpriseDocument Document = EnterpriseValidator.Analyze(Source).Document;
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        Dictionary<string, FlowchartLayoutNodeBounds> Frames = Layout.ClusterBounds
            .ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(Frames["first"].CenterY, Is.GreaterThan(Frames["second"].CenterY));
            Assert.That(Frames["second"].CenterY, Is.GreaterThan(Frames["third"].CenterY));
        });
    }

    [Test]
    public void A168_HintedGroupStack_FollowsUpdatedPeerFrames()
    {
        const string Source = """
            enterprise TB
              group custodians "Custodians"
              business-actor custodian in custodians
              group trusted "Trusted" below custodians
              business-actor trustedAdmin in trusted
              group regRoles "Registered Roles" below trusted
              business-role admin in regRoles
              group landgateOp "Landgate Operation" below regRoles
              business-actor landgateOwner in landgateOp
              group vendorOp "Vendor Operation" right-of landgateOp
              business-actor platformSupport in vendorOp
              custodian -assignment-> admin
              trustedAdmin -assignment-> admin
              landgateOwner -assignment-> admin
              platformSupport -assignment-> admin
            """;
        EnterpriseDocument Document = EnterpriseValidator.Analyze(Source).Document;
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        Dictionary<string, FlowchartLayoutNodeBounds> Frames = Layout.ClusterBounds
            .ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(Frames["custodians"].CenterY, Is.GreaterThan(Frames["trusted"].CenterY));
            Assert.That(Frames["trusted"].CenterY, Is.GreaterThan(Frames["regRoles"].CenterY));
            Assert.That(Frames["regRoles"].CenterY, Is.GreaterThan(Frames["landgateOp"].CenterY));
            Assert.That(Frames["vendorOp"].CenterX, Is.GreaterThan(Frames["landgateOp"].CenterX));
        });
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> LayoutOf(string Source) =>
        LayoutOf(EnterpriseValidator.Analyze(Source).Document);

    private static Dictionary<string, FlowchartLayoutNodeBounds> LayoutOf(EnterpriseDocument Document)
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        return Layout.NodeBounds.ToDictionary(Node => Node.NodeId, StringComparer.Ordinal);
    }
}

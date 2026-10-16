using System.Diagnostics;
using NUnit.Framework;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks the reviewed diagrams against final route geometry rather than cached pictures or baseline allowances.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CheapestLegalLayoutRegressionTests
{
    /// <summary>A projected cylinder port in an open layout must preserve the fractional centre of its aligned neighbour.</summary>
    [Test]
    public void OpenLayoutProjectedSideCentreRemainsStraight()
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue()
            .Select(Case => (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!)
            .Single(Case => Case.Id == "F147");
        var Contract = MermaidRecognizer.Parse(Scenario.Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        var Route = Layout.EdgeRoutes.Single(Route =>
            Contract.Nodes.Single(Node => Node.Id == Route.TargetId).Shape == "cylinder");
        var Target = Layout.NodeBounds.Single(Box => Box.NodeId == Route.TargetId);
        Assert.Multiple(() =>
        {
            Assert.That(Route.Points.Count, Is.EqualTo(2));
            Assert.That(Route.Points[0].X, Is.EqualTo(Target.CenterX));
            Assert.That(Route.Points[^1].X, Is.EqualTo(Target.CenterX));
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
                .Where(Finding => Finding.Rule is "3.2/4.7" or "4.2"), Is.Empty);
        });
    }

    /// <summary>Track separation must retain the pinned crossing ceiling while keeping paths legal.</summary>
    /// <param name="ScenarioId">The catalogue input whose partial track repair added crossings.</param>
    /// <param name="MaximumCrossings">Its unchanged c6605bd crossing count.</param>
    [TestCase("A163", 821)]
    [TestCase("A153", 24)]
    [TestCase("A110", 258)]
    [TestCase("A113", 120)]
    [TestCase("A104", 18)]
    [TestCase("A165", 0)]
    [TestCase("A70", 0)]
    public void PartialTrackRepairCannotAddCrossings(string ScenarioId, int MaximumCrossings)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue()
            .Select(Case => (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!)
            .Single(Case => Case.Id == ScenarioId);
        var Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        Assert.Multiple(() =>
        {
            Assert.That(Findings.Count(Finding => Finding.Rule == "5.9"), Is.LessThanOrEqualTo(MaximumCrossings));
            Assert.That(Findings.Where(Finding => Finding.Rule is "10" or "3.1" or "3.2/4.7" or "4.1"), Is.Empty);
        });
    }

    /// <summary>Both declaration orders must route a slanted diamond departure outside the neighbouring rejection node.</summary>
    /// <param name="ScenarioId">The unchanged binary-decision catalogue example.</param>
    [TestCase("F29")]
    [TestCase("F119")]
    public void DiamondFallbackKeepsEverySegmentLegal(string ScenarioId)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue()
            .Select(Case => (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!)
            .Single(Case => Case.Id == ScenarioId);
        var Contract = MermaidRecognizer.Parse(Scenario.Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
            .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1"), Is.Empty);
        Assert.That(Layout.EdgeRoutes.Count, Is.EqualTo(Contract.Edges.Count));
    }

    /// <summary>Supplies the named review scenarios and structural fan-out and grouped many-to-many examples.</summary>
    /// <returns>One independently reported regression per source diagram.</returns>
    public static IEnumerable<TestCaseData> Scenarios()
    {
        string[] Ids = ["F17", "F30", "F40", "F41", "F52", "F59", "F61", "F73", "F129", "F148", "F151", "F152", "F153", "A147", "A152"];
        foreach (var Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            var Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (Ids.Contains(Scenario.Id)) yield return new TestCaseData(Scenario).SetName("CheapestLegalLayout_" + Scenario.Id);
        }
        yield return new TestCaseData(new RouteRuleCatalogueAuditTests.Scenario("Grouped3x3", """
            flowchart TB
            subgraph Sources
              A[A]
              B[B]
              C[C]
            end
            subgraph Targets
              X[X]
              Y[Y]
              Z[Z]
            end
            A & B & C --> X & Y & Z
            """, false)).SetName("CheapestLegalLayout_Grouped3x3");
        yield return new TestCaseData(new RouteRuleCatalogueAuditTests.Scenario("LargeFanOut",
            "flowchart LR\nHub[Hub]\n" + string.Join("\n", Enumerable.Range(0, 20).Select(Index => $"Hub --> N{Index}[Target {Index}]")),
            false)).SetName("CheapestLegalLayout_LargeFanOut");
    }

    /// <summary>Measures every link and rejects illegal paths, shared tracks, stale guides and avoidable extra bends.</summary>
    /// <param name="Scenario">The unchanged catalogue source or structural regression input.</param>
    [TestCaseSource(nameof(Scenarios))]
    [CancelAfter(60000)]
    public void FinalRoutesUseCheapestLegalCandidates(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        var Timer = Stopwatch.StartNew();
        DiagramContract Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        FlowchartLayoutResult Layout;
        DiagramGridLayout.RouteRepairCandidateObserver = (Option, Subject, Score, Clears) =>
        {
            if (Option == "track-separation") TestContext.Out.WriteLine($"TRACK_SEARCH|{Subject}|accepted={Clears}");
        };
        try { Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out Layout!), Is.True); }
        finally { DiagramGridLayout.RouteRepairCandidateObserver = null; }
        var Nodes = Contract.Nodes.ToDictionary(Node => Node.Id);
        var Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).GroupBy(Box => Box.NodeId)
            .ToDictionary(Group => Group.Key, Group => Group.First());
        var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        Assert.That(FlowchartRouteRuleChecker.CheckPortAllocation(Contract, Layout),
            Is.EqualTo(Findings.Where(Finding => Finding.Rule == "3.2/4.7")),
            "The candidate-only port check must preserve the full audit's distribution findings.");
        var ExactPairs = new List<string>();
        for (int First = 0; First < Layout.EdgeRoutes.Count; First++)
        for (int Second = First + 1; Second < Layout.EdgeRoutes.Count; Second++)
        {
            var A = Layout.EdgeRoutes[First]; var B = Layout.EdgeRoutes[Second];
            if ((A.SourceId != B.SourceId || A.TargetId != B.TargetId)
                && FlowchartRouteRuleChecker.HasProperCrossing(A, B))
                ExactPairs.Add(A.EdgeId + " x " + B.EdgeId);
        }
        Assert.That(Findings.Where(Finding => Finding.Rule == "5.9").Select(Finding => Finding.Subject),
            Is.EquivalentTo(ExactPairs), "The crossing broad phase must retain every exact proper crossing.");
        int TotalBends = 0, Crossings = 0, AboveMinimum = 0;
        double TotalLength = 0;
        var Excess = new List<string>();
        for (int Left = 0; Left < Layout.EdgeRoutes.Count; Left++)
        for (int Right = Left + 1; Right < Layout.EdgeRoutes.Count; Right++)
            Crossings += FlowchartBranchRouteOrder.Intersections(
                Tuples(Layout.EdgeRoutes[Left].Points), Tuples(Layout.EdgeRoutes[Right].Points)).Crossings;
        foreach (var Route in Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2))
        {
            int Bends = FlowchartLayoutScore.CountCorners(Route.Points);
            double Length = PathLength(Tuples(Route.Points));
            TotalBends += Bends; TotalLength += Length;
            int Minimum = Bends;
            double MinimumLength = Length;
            var Edge = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId);
            if (Edge?.RoutingHint?.IsActive != true && Bounds.TryGetValue(Route.SourceId, out var Source)
                && Bounds.TryGetValue(Route.TargetId, out var Target))
                foreach (var Candidate in DiagramGridRouteBuilder.MinimumBendTerminalCandidates(
                             Route.Points[0], Route.Points[^1], Source, Target, Route.ExitSide, Route.EntrySide,
                             Nodes, Bounds, Contract))
                {
                    if (Layout.EdgeRoutes.Any(Other => Other != Route
                            && FlowchartBranchRouteOrder.Intersections(Candidate, Tuples(Other.Points)).Overlap > 0.001)) continue;
                    var Points = Candidate.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }).ToArray();
                    int Turns = FlowchartLayoutScore.CountCorners(Points);
                    double Distance = PathLength(Candidate);
                    if (Turns < Minimum || Turns == Minimum && Distance < MinimumLength)
                    { Minimum = Turns; MinimumLength = Distance; }
                }
            if (Bends > Minimum)
            {
                AboveMinimum++;
                Excess.Add($"{Route.EdgeId}: {Bends} bends; legal candidate has {Minimum}");
            }
            TestContext.Out.WriteLine(FormattableString.Invariant(
                $"ROUTE_COST|{Scenario.Id}|{Route.EdgeId}|bends={Bends}|length={Length:0.###}|minimumBends={Minimum}|minimumLength={MinimumLength:0.###}"));
        }
        TestContext.Out.WriteLine(FormattableString.Invariant(
            $"LAYOUT_COST|{Scenario.Id}|bends={TotalBends}|length={TotalLength:0.###}|crossings={Crossings}|aboveMinimum={AboveMinimum}|ms={Timer.Elapsed.TotalMilliseconds:0.###}"));
        Assert.Multiple(() =>
        {
            if (Scenario.Id == "F61")
            {
                Assert.That(TotalBends, Is.LessThanOrEqualTo(4), "The complete assignment must improve the c6605bd bend count.");
                Assert.That(TotalLength, Is.LessThanOrEqualTo(720), "The complete assignment must improve the c6605bd travelled length.");
                Assert.That(Crossings, Is.LessThanOrEqualTo(2), "The complete assignment must improve the c6605bd crossing count.");
            }
            Assert.That(Findings.Where(Finding => Finding.Rule is "10" or "5.1"), Is.Empty,
                "Legality and shared tracks must be checked even when the link has a hint.");
            Assert.That(Excess, Is.Empty, "The allocated-port search has a cheaper legal bend candidate.");
            foreach (var Guide in Layout.ResolvedEdgeGuides ?? [])
            {
                Assert.That(Contract.Edges, Does.Contain(Guide.Edge), "A drawn path must represent a declared link.");
                Assert.That(Layout.TryCopyDrawnRoutePoints(Guide.Edge, out var Points), Is.True);
                Assert.That(Guide.Points, Is.EqualTo(Points), "The painter must consume the final model route.");
            }
            bool SingleChain = Contract.Edges.Count == Contract.Nodes.Count - 1
                && Contract.Nodes.All(Node => Contract.Edges.Count(Edge => Edge.SourceId == Node.Id) <= 1
                    && Contract.Edges.Count(Edge => Edge.TargetId == Node.Id) <= 1);
            bool LinearComponents = Contract.Nodes.All(Node => Contract.Edges.Count(Edge => Edge.SourceId == Node.Id) <= 1
                && Contract.Edges.Count(Edge => Edge.TargetId == Node.Id) <= 1);
            if (LinearComponents && Contract.Nodes.All(Node => Node.PlacementHint?.IsActive != true)
                && Contract.Groups.All(Group => Group.PlacementHint?.IsActive != true)
                && Contract.Edges.All(Edge => Edge.RoutingHint?.IsActive != true))
                Assert.That(Crossings, Is.Zero, "Unhinted separate chains have no unavoidable crossing.");
            if (SingleChain && Contract.Groups.Count == 0 && Contract.Nodes.Count > FlowchartWrapPlan.MinimumRun)
            {
                var Rows = Layout.NodeBounds.GroupBy(Box => Box.CenterY).Select(Group => Group.OrderBy(Box => Box.CenterX).ToArray()).ToArray();
                int FullWidth = Rows.Max(Row => Row.Length);
                var FullRows = Rows.Where(Row => Row.Length == FullWidth).ToArray();
                foreach (var Row in FullRows.Skip(1)) Assert.That(Row.Select(Box => Box.CenterX),
                    Is.EqualTo(FullRows[0].Select(Box => Box.CenterX)), "Wrapped rows must share the same columns and gap sequence.");
            }
        });
    }

    /// <summary>Copies model points into the intersection helper's coordinate representation.</summary>
    /// <param name="Points">The complete stored route.</param>
    /// <returns>Coordinates in source-to-target order.</returns>
    private static (double X, double Y)[] Tuples(IReadOnlyList<FlowchartLayoutPoint> Points) =>
        Points.Select(Point => (Point.X, Point.Y)).ToArray();

    /// <summary>Measures actual travelled distance, including diagonal diamond terminals.</summary>
    /// <param name="Points">The candidate polyline.</param>
    /// <returns>Euclidean segment lengths summed in pixels.</returns>
    private static double PathLength(IReadOnlyList<(double X, double Y)> Points)
    {
        double Length = 0;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            double Dx = Points[Index].X - Points[Index - 1].X, Dy = Points[Index].Y - Points[Index - 1].Y;
            Length += Math.Sqrt(Dx * Dx + Dy * Dy);
        }
        return Length;
    }
}

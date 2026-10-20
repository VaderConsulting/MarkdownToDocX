using System.Text.Json;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Captures drawn enterprise geometry and quality measurements for the realistic deployment views.</summary>
[TestFixture, NonParallelizable]
public sealed class EnterpriseLayoutReviewTests
{
    private bool PreviousCells;

    /// <summary>Uses production cell ownership for renderer entry points that construct their own render context.</summary>
    [SetUp]
    public void UseProductionCells()
    {
        AppContext.TryGetSwitch(FlowchartRenderContext.CellRegionsSwitch, out PreviousCells);
        AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, true);
        if (Environment.GetEnvironmentVariable("ENTERPRISE_REVIEW_TRACE") is { Length: > 0 } Trace)
            DiagramGridLayout.LayoutStageTimingObserver = (Stage, Milliseconds) =>
                File.AppendAllText(Trace, Stage + "|" + Milliseconds + Environment.NewLine);
    }

    /// <summary>Restores the test host's compatibility default after each independent measurement.</summary>
    [TearDown]
    public void RestoreCellSwitch()
    {
        AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, PreviousCells);
        DiagramGridLayout.LayoutStageTimingObserver = null;
        DiagramGridLayout.BandWrapCandidateObserver = null;
    }

    /// <summary>Profiles one production layout with immediate stage and candidate evidence, without changing any engine limit.</summary>
    /// <param name="Id">Catalogue identity selected for a diagnostic invocation.</param>
    [TestCase("A115"), TestCase("A142"), Explicit("Run in isolation; diagnostic invocations must keep their requested watchdog.")]
    public void ProfileRealisticView(string Id)
    {
        string Path = Environment.GetEnvironmentVariable("ENTERPRISE_REVIEW_TRACE")
            ?? throw new InvalidOperationException("Set ENTERPRISE_REVIEW_TRACE to the diagnostic output path.");
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        var Document = EnterpriseValidator.Analyze(Scenario.Source).Document!;
        var Contract = EnterpriseFlowchartContractMapper.Build(Document);
        File.WriteAllText(Path, "START|" + DateTimeOffset.UtcNow + Environment.NewLine);
        DiagramGridLayout.RouteRepairCandidateObserver = (Option, Subject, Score, Legal) =>
            File.AppendAllText(Path, "CANDIDATE|" + Option + "|" + Subject + "|" + Score + "|" + Legal + Environment.NewLine);
        DiagramGridLayout.FinalRepairConfirmationObserver = (Key, Geometry, Input, Base, Reused, Ms) =>
            File.AppendAllText(Path, "CONFIRM|" + Key + "|" + Base + "|" + Reused + "|" + Ms + Environment.NewLine);
        DiagramGridLayout.FinalRepairObserver = (Key, Reason, Extra) =>
            File.AppendAllText(Path, "FINAL|" + Key + "|" + Reason + "|" + Extra + Environment.NewLine);
        EnterpriseBandPlacement.CapacityObserver = Message => File.AppendAllText(Path, Message + Environment.NewLine);
        DiagramGridLayout.LayoutStageObserver = (Stage, Layout) =>
            File.WriteAllText(Path + ".geometry.json", JsonSerializer.Serialize(new { Stage,
                Nodes = Layout.CellNodes?.Select(Node => new { Node.Id, Node.GroupId, Node.X, Node.Y, Node.Width, Node.Height,
                    Region = new { Node.CellNode!.Region.StartRow, Node.CellNode.Region.StartColumn, Node.CellNode.Region.RowSpan, Node.CellNode.Region.ColumnSpan } }),
                Layout.ClusterBounds, Layout.EdgeRoutes, Layout.RenderContext.LayoutStageViolations }));
        try
        {
            var Clock = System.Diagnostics.Stopwatch.StartNew();
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
            var Score = FlowchartLayoutScore.Measure(Contract, Layout, false);
            File.AppendAllText(Path, "FINISH|" + Clock.Elapsed.TotalSeconds + "|" + JsonSerializer.Serialize(new
            {
                Bends = Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)), Score.Crossings,
                Aspect = Score.Width / Score.Height, Score.EmptyShare, Score.OptimizationScore,
                Findings = FlowchartRouteRuleChecker.Check(Contract, Layout)
            }) + Environment.NewLine);
        }
        finally
        {
            DiagramGridLayout.RouteRepairCandidateObserver = null;
            DiagramGridLayout.FinalRepairConfirmationObserver = null;
            DiagramGridLayout.FinalRepairObserver = null;
            DiagramGridLayout.LayoutStageObserver = null;
            EnterpriseBandPlacement.CapacityObserver = null;
        }
    }

    /// <summary>Names legal fallback side pairs against an already captured A115 layout, avoiding another placement or repair run.</summary>
    [Test, Explicit("Set ENTERPRISE_REVIEW_TRACE to a completed geometry capture's profile path.")]
    public void ProbeA115CapturedTerminals()
    {
        string Path = Environment.GetEnvironmentVariable("ENTERPRISE_REVIEW_TRACE")!;
        using var Capture = JsonDocument.Parse(File.ReadAllText(Path + ".geometry.json"));
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A115");
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        var Bounds = Capture.RootElement.GetProperty("Nodes").EnumerateArray().ToDictionary(Node => Node.GetProperty("Id").GetString()!,
            Node => new FlowchartLayoutNodeBounds { NodeId = Node.GetProperty("Id").GetString()!, CenterX = Node.GetProperty("X").GetDouble(),
                CenterY = Node.GetProperty("Y").GetDouble(), Width = Node.GetProperty("Width").GetDouble(), Height = Node.GetProperty("Height").GetDouble() });
        foreach (var Frame in Capture.RootElement.GetProperty("ClusterBounds").EnumerateArray())
        { var Box = Frame.Deserialize<FlowchartLayoutNodeBounds>()!; Bounds[Box.NodeId] = Box; }
        var Nodes = Contract.Nodes.ToDictionary(Node => Node.Id);
        foreach (var Edge in Contract.Edges.Where(Edge => Edge.Id == "internet->perthEdge:0"))
        foreach (string Exit in new[] { "left", "top", "right", "bottom" })
        foreach (string Entry in new[] { "left", "top", "right", "bottom" })
        {
            var Source = Bounds[Edge.SourceId]; var Target = Bounds[Edge.TargetId];
            var A = Port(Source, Exit); var B = Port(Target, Entry);
            var Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId,
                ExitSide = Exit, EntrySide = Entry, ExitNormalX = A.Nx, ExitNormalY = A.Ny, EntryNormalX = B.Nx, EntryNormalY = B.Ny };
            Route.Points.Add(new() { X = A.X, Y = A.Y }); Route.Points.Add(new() { X = B.X, Y = B.Y });
            var Built = FlowchartAllocatedPortFallback.Build(Contract, Bounds, Nodes, Route, true);
            TestContext.Out.WriteLine("CAPTURED_TERMINAL|" + Edge.Id + "|" + Exit + "/" + Entry + "|points=" + Built.Count
                + "|legal=" + FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Built) + "|" + JsonSerializer.Serialize(Built.Select(Point => new { Point.X, Point.Y })));
        }

        /// <summary>Uses exact rectangular side-centre ports in the captured routing coordinate direction.</summary>
        static (double X, double Y, double Nx, double Ny) Port(FlowchartLayoutNodeBounds Box, string Side) => Side switch
        {
            "left" => (Box.CenterX - Box.Width / 2, Box.CenterY, -1, 0), "right" => (Box.CenterX + Box.Width / 2, Box.CenterY, 1, 0),
            "top" => (Box.CenterX, Box.CenterY + Box.Height / 2, 0, 1), _ => (Box.CenterX, Box.CenterY - Box.Height / 2, 0, -1)
        };
    }

    /// <summary>Lists unrelated frames whose multi-band envelopes constrain the proposed global aspect columns.</summary>
    [Test]
    public void MeasureMultiBandGroupConstraints()
    {
        foreach (var Scenario in RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Where(Case => Case.Id.StartsWith("A", StringComparison.Ordinal)))
        {
            var Document = EnterpriseValidator.Analyze(Scenario.Source).Document;
            if (Document == null) continue;
            var Contract = EnterpriseFlowchartContractMapper.Build(Document);
            var Groups = Contract.Groups.ToDictionary(Group => Group.Id, StringComparer.Ordinal);
            var Roots = Contract.Groups.Where(Group => Group.ParentGroupId == null).Select(Group =>
            {
                var Members = Document.Elements.Values.Where(Element =>
                {
                    string? Parent = Element.ParentId;
                    while (Parent != null)
                    {
                        if (Parent == Group.Id) return true;
                        Parent = Groups.TryGetValue(Parent, out var Ancestor) ? Ancestor.ParentGroupId : null;
                    }
                    return false;
                }).Select(Element => new { Element.Id, Type = Element.ElementType.ToString(),
                    EnterpriseElementCatalog.Elements[Element.ElementType].Layer,
                    EnterpriseElementCatalog.Elements[Element.ElementType].Aspect }).ToArray();
                return new { Group.Id, Members };
            }).Where(Group => Group.Members.Select(Member => Member.Layer).Distinct().Count() > 1).ToArray();
            if (Roots.Length > 1) TestContext.Out.WriteLine("ENTERPRISE_BAND_GROUPS|" + Scenario.Id + "|" + JsonSerializer.Serialize(Roots));
        }
    }

    /// <summary>Measures the shared model alongside the exact rectangles consumed by the enterprise painter.</summary>
    /// <param name="Id">Realistic catalogue view under review.</param>
    [TestCase("A113"), TestCase("A114"), TestCase("A115"), TestCase("A116"), TestCase("A117"), TestCase("A167")]
    public void MeasureRealisticView(string Id)
    {
        if (Environment.GetEnvironmentVariable("ENTERPRISE_WRAP_TRACE") is { Length: > 0 } Trace)
            DiagramGridLayout.BandWrapCandidateObserver = (Width, Layout, Cost, Legal) =>
            {
                int Crossings = 0;
                for (int A = 0; A < Layout.EdgeRoutes.Count; A++) for (int B = A + 1; B < Layout.EdgeRoutes.Count; B++)
                    Crossings += FlowchartBranchRouteOrder.Intersections(Layout.EdgeRoutes[A].Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                        Layout.EdgeRoutes[B].Points.Select(Point => (Point.X, Point.Y)).ToArray()).Crossings;
                File.AppendAllText(Trace, JsonSerializer.Serialize(new { Id, Width, Cost.Bends, Crossings, Score = (double)Cost, Legal }) + Environment.NewLine);
            };
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        var Document = EnterpriseValidator.Analyze(Scenario.Source).Document!;
        var CanonicalContract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(CanonicalContract, new() { UseCellRegions = true }, out var Canonical), Is.True);
        TestContext.Out.WriteLine("ENTERPRISE_CANONICAL|" + Id + "|" + JsonSerializer.Serialize(new
        {
            Bends = Canonical.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)),
            UseCells = new FlowchartRenderContext().UseCellRegions, Canonical.RenderContext.UseCellRegions,
            Canonical.NodeBounds, Canonical.ClusterBounds, Canonical.EdgeRoutes,
            Locations = EnterpriseCrossingLocations.Measure(CanonicalContract, Canonical),
            Snapshot = CellProductionParityTests.Snapshot(CanonicalContract, Canonical)
        }));
        var Painted = EnterpriseLayoutEngine.Layout(Document);
        Assert.That(Painted.RoutingContext, Is.Not.Null);
        var Context = Painted.RoutingContext!;
        Assert.That(Context.Layout.RenderContext.UseCellRegions, Is.True);
        var Score = FlowchartLayoutScore.Measure(Context.Contract, Context.Layout, false);
        var Findings = FlowchartRouteRuleChecker.Check(Context.Contract, Context.Layout);
        int Crossings = 0, Shared = 0;
        var Paths = Context.Layout.EdgeRoutes.Select(Route => Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()).ToArray();
        for (int A = 0; A < Paths.Length; A++) for (int B = A + 1; B < Paths.Length; B++)
        { var Hits = FlowchartBranchRouteOrder.Intersections(Paths[A], Paths[B]); Crossings += Hits.Crossings; if (Hits.Overlap > 0) Shared++; }
        TestContext.Out.WriteLine("ENTERPRISE_REVIEW|" + JsonSerializer.Serialize(new
        {
            Id, Bends = Context.Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)),
            CellsOn = Context.Layout.RenderContext.UseCellRegions, Crossings, Shared, Aspect = Score.Width / Score.Height, Score.EmptyShare,
            Score.OptimizationScore, Findings = Findings.Count, Width = Painted.Width, Height = Painted.Height,
            Drawn = Painted.Nodes.Values.Select(Node => new { Node.Id, Node.AbsoluteX, Node.AbsoluteY, Node.Width, Node.Height,
                Node.IsGroup, Node.IsCompound, Type = Node.Element?.ElementType.ToString() }),
            LogicalFrames = Context.Layout.ClusterBounds
        }));
    }
}

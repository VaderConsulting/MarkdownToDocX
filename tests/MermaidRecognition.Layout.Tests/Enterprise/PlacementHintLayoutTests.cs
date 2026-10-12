using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class PlacementHintLayoutTests
{
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
        FlowchartLayoutNodeBounds[] Chain = Enumerable.Range(1, 6)
            .Select(Index => Layout.NodeBounds.Single(Node => Node.NodeId == $"c{Index}"))
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(Chain.Select(Node => Node.CenterX).Distinct().Count(), Is.EqualTo(1));
            for (int Index = 1; Index < Chain.Length; Index++)
                Assert.That(Chain[Index].CenterY, Is.GreaterThan(Chain[Index - 1].CenterY));
            Assert.That(Layout.EdgeRoutes, Has.Count.EqualTo(5));
            Assert.That(Layout.EdgeRoutes.All(Route => Route.Points.Count == 2), Is.True);
        });
    }

    [Test]
    public void RelativeHints_KeepSourceOrderedRow()
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

        var Layout = LayoutOf(Source);
        var Ordered = Enumerable.Range(1, 5).Select(Index => Layout[$"a{Index}"]).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(Ordered.Select(Node => Node.CenterY).Distinct().Count(), Is.EqualTo(1),
                string.Join(", ", Ordered.Select(Node => $"{Node.NodeId}=({Node.CenterX},{Node.CenterY})")));
            for (int Index = 1; Index < Ordered.Length; Index++)
                Assert.That(Ordered[Index].CenterX, Is.GreaterThan(Ordered[Index - 1].CenterX));
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
        var Layout = LayoutOf(Analysis.Document);
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

        var Layout = LayoutOf(Source);
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
        var Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        return Layout.NodeBounds.ToDictionary(Node => Node.NodeId, StringComparer.Ordinal);
    }
}

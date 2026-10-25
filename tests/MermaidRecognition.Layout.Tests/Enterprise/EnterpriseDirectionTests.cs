using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseDirectionTests
{
    [Test]
    public void LocalDirection_IsCarriedToGroupContractsAndSource()
    {
        var Document = EnterpriseParser.Parse("enterprise LR\ngroup container {\ndirection BT\napplication-component first\napplication-component second\nfirst -flow-> second\n}").Document;
        Assert.That(EnterpriseFlowchartContractMapper.Build(Document).Groups.Single().Direction, Is.EqualTo("BT"));
        Assert.That(EnterpriseFlowchartSourceWriter.Write(Document), Does.Contain("direction BT"));
    }

    [TestCase("TB", false, -1)]
    [TestCase("BT", false, 1)]
    [TestCase("LR", true, 1)]
    [TestCase("RL", true, -1)]
    public void HeaderDirection_ReachesLayout(string Direction, bool Horizontal, int Sign)
    {
        var Document = EnterpriseParser.Parse($"enterprise {Direction}; application-component first; application-component second; first -flow-> second").Document;
        var Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(Contract.Direction, Is.EqualTo(Direction));
        Assert.That(EnterpriseFlowchartSourceWriter.Write(Document), Does.StartWith("flowchart " + Direction));
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        var First = Layout.NodeBounds.Single(Item => Item.NodeId == "first");
        var Second = Layout.NodeBounds.Single(Item => Item.NodeId == "second");
        double Delta = Horizontal ? Second.CenterX - First.CenterX : Second.CenterY - First.CenterY;
        Assert.That(Delta * Sign, Is.GreaterThan(0));
    }

    [Test]
    public void LongerClosingDashes_IncreaseTheFlowGap()
    {
        var ShortDocument = EnterpriseParser.Parse("enterprise TB\napplication-component first\napplication-component second\nfirst -flow-> second").Document;
        var LongDocument = EnterpriseParser.Parse("enterprise TB\napplication-component first\napplication-component second\nfirst -flow---> second").Document;
        var ShortContract = EnterpriseFlowchartContractMapper.Build(ShortDocument);
        var LongContract = EnterpriseFlowchartContractMapper.Build(LongDocument);
        Assert.That(ShortContract.Edges.Single().MinLength, Is.EqualTo(1));
        Assert.That(LongContract.Edges.Single().MinLength, Is.EqualTo(2));
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(ShortContract, out var ShortLayout), Is.True);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(LongContract, out var LongLayout), Is.True);
        double ShortGap = FlowGap(ShortLayout, "first", "second");
        double ExtendedGap = FlowGap(LongLayout, "first", "second");
        Assert.That(ExtendedGap, Is.GreaterThan(ShortGap));
        string ShortSvg = EnterpriseSvgRenderer.Render(ShortDocument);
        string LongSvg = EnterpriseSvgRenderer.Render(LongDocument);
        Assert.That(LongSvg, Is.Not.EqualTo(ShortSvg));
    }

    [Test]
    public void MixedApplicationChain_UsesClosingDashesForTheGap()
    {
        string ShortSource = "enterprise\napplication-component webShop \"Web Shop\"\napplication-service orderApi \"Order API\"\ndata-object order \"Order\"\nwebShop -serving-> orderApi\norderApi -access(write)-> order";
        string LongSource = "enterprise\napplication-component webShop \"Web Shop\"\napplication-service orderApi \"Order API\"\ndata-object order \"Order\"\nwebShop -serving------> orderApi\norderApi -access(write)----> order";
        EnterpriseDocument ShortDocument = EnterpriseParser.Parse(ShortSource).Document;
        EnterpriseDocument LongDocument = EnterpriseParser.Parse(LongSource).Document;
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(EnterpriseFlowchartContractMapper.Build(ShortDocument), out FlowchartLayoutResult ShortLayout), Is.True);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(EnterpriseFlowchartContractMapper.Build(LongDocument), out FlowchartLayoutResult LongLayout), Is.True);
        double ShortServing = FlowGap(ShortLayout, "webShop", "orderApi");
        double LongServing = FlowGap(LongLayout, "webShop", "orderApi");
        double ShortAccess = FlowGap(ShortLayout, "orderApi", "order");
        double LongAccess = FlowGap(LongLayout, "orderApi", "order");
        Assert.That(LongServing, Is.GreaterThan(ShortServing));
        Assert.That(LongAccess, Is.GreaterThan(ShortAccess));
        Assert.That(LongServing, Is.GreaterThan(LongAccess));
        Assert.That(EnterpriseSvgRenderer.Render(LongDocument), Is.Not.EqualTo(EnterpriseSvgRenderer.Render(ShortDocument)));
    }

    private static double FlowGap(FlowchartLayoutResult Layout, string SourceId, string TargetId)
    {
        var Source = Layout.NodeBounds.Single(Item => Item.NodeId == SourceId);
        var Target = Layout.NodeBounds.Single(Item => Item.NodeId == TargetId);
        return Math.Abs(Target.CenterY - Source.CenterY);
    }
}

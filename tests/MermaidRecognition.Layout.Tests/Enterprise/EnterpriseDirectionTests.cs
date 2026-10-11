using MermaidRecognition.Kinds.Enterprise;
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
}

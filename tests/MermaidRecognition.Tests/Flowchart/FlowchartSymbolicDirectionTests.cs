using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

public sealed class FlowchartSymbolicDirectionTests
{
    const string F75_SOURCE = """
        flowchart <
        West[West] --> Centre[Centre] --> East[East]
        """;

    [Test]
    public void FlowchartLessThanHeader_SetsRightToLeftDirection()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(F75_SOURCE);
        Assert.That(Contract.Direction, Is.EqualTo(nameof(FlowchartDirection.RightToLeft)));
    }

    [TestCase("flowchart >", nameof(FlowchartDirection.LeftToRight))]
    [TestCase("flowchart ^", nameof(FlowchartDirection.BottomToTop))]
    [TestCase("flowchart v", nameof(FlowchartDirection.TopToBottom))]
    public void SymbolicHeaders_MapToMermaidDirections(string Header, string ExpectedDirection)
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(Header + "\nA --> B");
        Assert.That(Contract.Direction, Is.EqualTo(ExpectedDirection));
    }
}

using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Tests.Render;

[TestFixture]
public sealed class FlowchartDirectionNormalizerTests
{
    [TestCase(null, FlowchartDirection.TopToBottom)]
    [TestCase("", FlowchartDirection.TopToBottom)]
    [TestCase("TB", FlowchartDirection.TopToBottom)]
    [TestCase("TD", FlowchartDirection.TopToBottom)]
    [TestCase("TopToBottom", FlowchartDirection.TopToBottom)]
    [TestCase("LR", FlowchartDirection.LeftToRight)]
    [TestCase("LeftToRight", FlowchartDirection.LeftToRight)]
    [TestCase("RL", FlowchartDirection.RightToLeft)]
    [TestCase("RightToLeft", FlowchartDirection.RightToLeft)]
    [TestCase("BT", FlowchartDirection.BottomToTop)]
    [TestCase("BottomToTop", FlowchartDirection.BottomToTop)]
    public void Normalize_AcceptsMermaidTokensAndContractEnumNames(string? Input, FlowchartDirection Expected)
    {
        Assert.That(FlowchartDirectionNormalizer.Normalize(Input), Is.EqualTo(Expected));
    }

    [Test]
    public void Parse_DefaultHeader_UsesTopToBottomDirection()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart
            A --> B
            """);

        Assert.That(FlowchartDirectionNormalizer.Normalize(Contract.Direction), Is.EqualTo(FlowchartDirection.TopToBottom));
    }

    [Test]
    public void Parse_LRHeader_UsesLeftToRightDirection()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A --> B
            """);

        Assert.That(FlowchartDirectionNormalizer.Normalize(Contract.Direction), Is.EqualTo(FlowchartDirection.LeftToRight));
    }
}

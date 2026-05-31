using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

[TestFixture]
public sealed class FlowchartF71InteractionTests
{
    const string F71_SOURCE = """
        flowchart LR
        Docs[Documentation] --> API[API]
        click Docs href "https://example.com/docs" "Open docs" _blank
        click API call callback() "Tooltip for callback"
        """;

    [Test]
    public void F71_ClickLines_AreParsedAsInteractions_NotNodeLabels()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(F71_SOURCE);

        Assert.That(Contract.Nodes.Select(Node => Node.Id), Does.Not.Contain("click"));
        Assert.That(Contract.Interactions, Has.Count.EqualTo(2));

        InteractionDirective DocsInteraction = Contract.Interactions.Single(
            Interaction => string.Equals(Interaction.TargetId, "Docs", StringComparison.Ordinal));
        Assert.That(DocsInteraction.Url, Is.EqualTo("https://example.com/docs"));
        Assert.That(DocsInteraction.Tooltip, Is.EqualTo("Open docs"));
        Assert.That(DocsInteraction.TargetWindow, Is.EqualTo("_blank"));
        Assert.That(DocsInteraction.Callback, Is.Null);

        InteractionDirective ApiInteraction = Contract.Interactions.Single(
            Interaction => string.Equals(Interaction.TargetId, "API", StringComparison.Ordinal));
        Assert.That(ApiInteraction.Callback, Is.EqualTo("callback()"));
        Assert.That(ApiInteraction.Tooltip, Is.EqualTo("Tooltip for callback"));
        Assert.That(ApiInteraction.Url, Is.Null);

        NodeContract DocsNode = Contract.Nodes.Single(Node => string.Equals(Node.Id, "Docs", StringComparison.Ordinal));
        Assert.That(DocsNode.Label, Is.EqualTo("Documentation"));
        Assert.That(DocsNode.Label, Does.Not.Contain("example.com"));
    }

}

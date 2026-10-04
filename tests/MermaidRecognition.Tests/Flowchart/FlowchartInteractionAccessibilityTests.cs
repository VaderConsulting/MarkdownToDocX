using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

public class FlowchartInteractionAccessibilityTests
{
    [Test]
    public void ClickHref_IsRecorded()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A --> B
                click A href "https://example.com"
            """);

        InteractionDirective Interaction = Contract.Interactions.Single();
        Assert.That(Interaction.TargetId, Is.EqualTo("A"));
        Assert.That(Interaction.Url, Is.EqualTo("https://example.com"));
        Assert.That(Contract.Diagnostics, Is.Empty);
    }

    [Test]
    public void ClickHref_WithTooltipAndTarget_IsRecorded()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A --> B
                click A href "https://example.com/docs" "Open docs" _blank
            """);

        InteractionDirective Interaction = Contract.Interactions.Single();
        Assert.That(Interaction.Url, Is.EqualTo("https://example.com/docs"));
        Assert.That(Interaction.Tooltip, Is.EqualTo("Open docs"));
        Assert.That(Interaction.TargetWindow, Is.EqualTo("_blank"));
    }

    [Test]
    public void ClickCall_WithTooltip_IsRecordedWithCallbackDiagnostic()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A --> B
                click A call callback() "Tooltip for callback"
            """);

        InteractionDirective Interaction = Contract.Interactions.Single();
        Assert.That(Interaction.Callback, Is.EqualTo("callback()"));
        Assert.That(Interaction.Tooltip, Is.EqualTo("Tooltip for callback"));
        Assert.That(
            Contract.Diagnostics.Select(Diagnostic => Diagnostic.Code),
            Contains.Item("flowchart.click-callback-not-exported"));
    }

    [Test]
    public void ClickCallback_EmitsDiagnostic()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A --> B
                click A myCallback
            """);

        Assert.That(Contract.Interactions.Single().Callback, Is.EqualTo("myCallback"));
        Assert.That(
            Contract.Diagnostics.Select(Diagnostic => Diagnostic.Code),
            Contains.Item("flowchart.click-callback-not-exported"));
    }

    [Test]
    public void AccTitle_And_AccDescr_AreCaptured()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            accTitle: Diagram title
            accDescr {
              Line one
              Line two
            }
            flowchart TD
                A --> B
            """);

        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Diagram title"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Line one" + Environment.NewLine + "Line two"));
    }

    [Test]
    public void FlowchartElk_EmitsUnsupportedDiagnostic()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart-elk TD
                A --> B
            """);

        Assert.That(
            Contract.Diagnostics.Select(Diagnostic => Diagnostic.Code),
            Contains.Item("flowchart.elk-not-native"));
    }
}

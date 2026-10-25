using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using NUnit.Framework;

namespace MermaidRecognition.Tests.Flowchart;

/// <summary>
/// Expand-on-load for flowchart nodes that only appear inline on links.
/// </summary>
[TestFixture]
public sealed class FlowchartScriptNormalizerTests
{
    [Test]
    public void ExpandInlineNodesSplitsStadiumLinkIntoDeclarations()
    {
        const string Source = "flowchart TD\nStart([Start]) --> Done([Done])\n";
        string Expanded = FlowchartScriptNormalizer.ExpandInlineNodes(Source);
        Assert.That(Expanded, Does.Contain("s(\"Start\")"));
        Assert.That(Expanded, Does.Contain("d(\"Done\")"));
        Assert.That(Expanded, Does.Contain("s --> d"));
        Assert.That(Expanded, Does.Not.Contain("Start([Start])"));
        Assert.That(Expanded, Does.Not.Contain("Done([Done])"));
    }

    [Test]
    public void PrepareForEditorUsesFlowchartExpansion()
    {
        const string Source = "flowchart LR\nA([Alpha]) --> B([Beta])\n";
        string Prepared = Parser.PrepareForEditor(Source);
        Assert.That(Prepared, Does.Contain("a(\"Alpha\")"));
        Assert.That(Prepared, Does.Contain("b(\"Beta\")"));
        Assert.That(Prepared, Does.Contain("a --> b"));
    }

    [Test]
    public void ExpandLeavesDeclaredNodesAlone()
    {
        const string Source = "flowchart TD\nStart([Start])\nDone([Done])\nStart --> Done\n";
        string Expanded = FlowchartScriptNormalizer.ExpandInlineNodes(Source);
        Assert.That(Expanded, Is.EqualTo(Source));
    }

    [Test]
    public void ExpandLeavesNonFlowchartUnchanged()
    {
        const string Source = "sequenceDiagram\nAlice->>Bob: hi\n";
        Assert.That(FlowchartScriptNormalizer.ExpandInlineNodes(Source), Is.EqualTo(Source));
        Assert.That(Parser.PrepareForEditor(Source), Is.EqualTo(Source));
    }

    [Test]
    public void ExpandedScriptGivesLineIdsForNodes()
    {
        const string Source = "flowchart TD\nStart([Start]) --> Done([Done])\n";
        string Expanded = Parser.PrepareForEditor(Source);
        DiagramParseResult Result = FlowchartParser.Default.Parse(Expanded);
        Assert.That(Result.ItemAtLine(2)!.Name, Is.EqualTo("s"));
        Assert.That(Result.ItemAtLine(3)!.Name, Is.EqualTo("d"));
        Assert.That(Result.ItemAtLine(4)!.Kind, Is.EqualTo(DiagramItemKind.Relationship));
        Assert.That(Result.ItemAtLine(4)!.SourceName, Is.EqualTo("s"));
        Assert.That(Result.ItemAtLine(4)!.TargetName, Is.EqualTo("d"));
    }

    [Test]
    public void ExpandSkipsIdsAlreadyTaken()
    {
        const string Source = "flowchart TD\ns[Existing]\nStart([Start]) --> Done([Done])\n";
        string Expanded = FlowchartScriptNormalizer.ExpandInlineNodes(Source);
        Assert.That(Expanded, Does.Contain("s[Existing]"));
        Assert.That(Expanded, Does.Contain("s2(\"Start\")"));
        Assert.That(Expanded, Does.Contain("d(\"Done\")"));
        Assert.That(Expanded, Does.Contain("s2 --> d"));
    }

    [Test]
    public void CollectUsedIdsTracksAddsEditsAndDeletes()
    {
        HashSet<string> Used = FlowchartScriptNormalizer.CollectUsedIds("flowchart TD\na[One]\nb[Two]\n");
        Assert.That(Used, Does.Contain("a"));
        Assert.That(Used, Does.Contain("b"));

        string AfterEdit = "flowchart TD\na[One]\nc[Two]\n";
        Used = FlowchartScriptNormalizer.CollectUsedIds(AfterEdit);
        Assert.That(Used, Does.Contain("a"));
        Assert.That(Used, Does.Contain("c"));
        Assert.That(Used, Does.Not.Contain("b"));

        string AfterDelete = "flowchart TD\na[One]\n";
        Used = FlowchartScriptNormalizer.CollectUsedIds(AfterDelete);
        Assert.That(Used, Does.Contain("a"));
        Assert.That(Used, Does.Not.Contain("c"));

        string Next = FlowchartScriptNormalizer.AllocateUniqueId("a", Used);
        Assert.That(Next, Is.EqualTo("a2"));
        Assert.That(Used, Does.Contain("a2"));
    }

    [Test]
    public void ParserCollectUsedIdentifiersIncludesFlowchartNodes()
    {
        HashSet<string> Used = Parser.CollectUsedIdentifiers("flowchart TD\nstart([Go])\nendNode([Stop])\nstart --> endNode\n");
        Assert.That(Used, Does.Contain("start"));
        Assert.That(Used, Does.Contain("endNode"));
    }
}

using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Kinds.Flowchart;
using NUnit.Framework;

namespace MermaidRecognition.Tests.Core;

/// <summary>
/// Shared Parser surface: line-number item ids and kind detection.
/// </summary>
[TestFixture]
public sealed class ParserTests
{
    private const string SOURCE = "enterprise\n"
        + "application-component shop \"Shop\"\n"
        + "application-component cart \"Cart\"\n"
        + "shop -flow-> cart\n";

    [Test]
    public void ForSourceFindsEnterprise()
    {
        Parser? Found = Parser.ForSource(SOURCE);
        Assert.That(Found, Is.SameAs(EnterpriseParser.Default));
        Assert.That(Found!.Detect(SOURCE), Is.True);
    }

    [Test]
    public void ParseGivesOneItemPerLineWithLineNumberIds()
    {
        DiagramParseResult Result = EnterpriseParser.Default.Parse(SOURCE);
        Assert.That(Result.Items.Count, Is.EqualTo(3));
        Assert.That(Result.ItemAtLine(2)!.Name, Is.EqualTo("shop"));
        Assert.That(Result.ItemAtLine(2)!.Id, Is.EqualTo(2));
        Assert.That(Result.ItemAtLine(3)!.Name, Is.EqualTo("cart"));
        Assert.That(Result.ItemAtLine(4)!.Kind, Is.EqualTo(DiagramItemKind.Relationship));
        Assert.That(Result.ItemAtLine(4)!.SourceName, Is.EqualTo("shop"));
        Assert.That(Result.ItemAtLine(4)!.TargetName, Is.EqualTo("cart"));
        Assert.That(Result.ItemAtLine(1), Is.Null);
    }

    [Test]
    public void IsDeclaredLineUsesLineIds()
    {
        int Shop = SOURCE.IndexOf("shop \"Shop\"", System.StringComparison.Ordinal);
        Assert.That(EnterpriseParser.Default.IsDeclaredLine(SOURCE, Shop), Is.True);
        Assert.That(EnterpriseParser.Default.IsDeclaredLine(SOURCE, 0), Is.False);
    }

    [Test]
    public void FlowchartParserGivesLineNumberIds()
    {
        const string Flow = "flowchart TD\nA[Start]\nB{Choice}\nA --> B\n";
        Assert.That(Parser.ForSource(Flow), Is.SameAs(FlowchartParser.Default));
        DiagramParseResult Result = FlowchartParser.Default.Parse(Flow);
        Assert.That(Result.ItemAtLine(2)!.Name, Is.EqualTo("A"));
        Assert.That(Result.ItemAtLine(2)!.Id, Is.EqualTo(2));
        Assert.That(Result.ItemAtLine(3)!.TypeToken, Is.EqualTo("diamond"));
        Assert.That(Result.ItemAtLine(4)!.Kind, Is.EqualTo(DiagramItemKind.Relationship));
        Assert.That(Result.ItemAtLine(4)!.SourceName, Is.EqualTo("A"));
        Assert.That(Result.ItemAtLine(4)!.TargetName, Is.EqualTo("B"));
    }
}

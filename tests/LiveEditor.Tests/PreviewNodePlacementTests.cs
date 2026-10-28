using System.Drawing;
using MermaidRecognition.Kinds.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture]
public sealed class PreviewNodePlacementTests
{
    internal const string Source = "enterprise\n  title LiveEditor preview\n"
        + "  application-component webShop \"Web Shop\"\n"
        + "  application-service orderApi \"Order API\"\n"
        + "  data-object order \"Order\"\n"
        + "  webShop -serving-> orderApi\n  orderApi -access(write)-> order\n";

    [TestCase(195, 145, "left-of webShop")]
    [TestCase(555, 145, "right-of webShop")]
    [TestCase(375, 25, "above webShop")]
    [TestCase(375, 265, "below webShop")]
    [TestCase(135, 145, "left-of webShop +2")]
    public void DropCreatesARelativeHintOnTheElementDeclaration(float X, float Y, string Hint)
    {
        Assert.That(PreviewNodePlacement.TryCreate(Source, "order", Items(), new(X, Y), out var Edit, out var Reason), Is.True, Reason);
        string Updated = Edit!.Apply(Source);
        Assert.That(Updated, Does.Contain("  data-object order \"Order\" " + Hint + "\n"));
        Assert.That(Updated, Does.Contain("  orderApi -access(write)-> order\n"));
        Assert.That(EnterpriseValidator.Analyze(Updated).HasErrors, Is.False);
    }

    [Test]
    public void ExistingHintIsReplacedWhileLabelsClassesCommentsAndNewlinesArePreserved()
    {
        string Text = Source.Replace("\"Order\"", "\"Order left-of a shelf\" push E:::emphasis")
            .Replace("  title LiveEditor preview\n", "  title LiveEditor preview\n  classDef emphasis fill:#eef\n")
            .Replace("\n", "\r\n");
        Assert.That(PreviewNodePlacement.TryCreate(Text, "order", Items(), new(195, 145), out var Edit, out var Reason), Is.True, Reason);
        string Updated = Edit!.Apply(Text);
        Assert.That(Updated, Does.Contain("\"Order left-of a shelf\" left-of webShop :::emphasis\r\n"));
        Assert.That(Updated, Does.Not.Contain("push E"));
        Assert.That(Updated, Does.Contain("classDef emphasis fill:#eef\r\n"));
        Assert.That(Updated.Replace("\r\n", ""), Does.Not.Contain("\n"));
        Assert.That(EnterpriseValidator.Analyze(Updated).Document.Elements["order"].Label, Is.EqualTo("Order left-of a shelf"));
    }

    [Test]
    public void DropCannotCreateAHintCycle()
    {
        string Text = Source.Replace("\"Web Shop\"", "\"Web Shop\" left-of order");
        Assert.That(PreviewNodePlacement.TryCreate(Text, "order", Items(), new(195, 145), out var Edit, out _), Is.False);
        Assert.That(Edit, Is.Null);
    }

    [Test]
    public void APeerInAnotherGroupIsNotUsedForPlacement()
    {
        string Text = "enterprise\n group g {\n application-component webShop\n }\n data-object order\n";
        Assert.That(PreviewNodePlacement.TryCreate(Text, "order", Items(), new(195, 145), out _, out _), Is.False);
    }

    [Test]
    public void InvalidSourceAndMultilineDeclarationsAreLeftForSourceEditing()
    {
        Assert.That(PreviewNodePlacement.TryCreate(Source + " broken syntax\n", "order", Items(), new(195, 145), out _, out _), Is.False);
        string Multiline = Source.Replace("\"Order\"", "\"Order\nwith more detail\"");
        Assert.That(PreviewNodePlacement.TryCreate(Multiline, "order", Items(), new(195, 145), out _, out _), Is.False);
    }

    [Test]
    public void MultipleStatementsOnOneLineAreNotOverwritten()
    {
        string Text = Source.Replace("\"Order\"", "\"Order\" push E; application-component other");
        Assert.That(EnterpriseValidator.Analyze(Text).HasErrors, Is.False);
        Assert.That(PreviewNodePlacement.TryCreate(Text, "order", Items(), new(195, 145), out var Edit, out _), Is.False);
        Assert.That(Edit, Is.Null);
    }

    [Test]
    public void QuotedSemicolonsAndInlineCommentsSurviveTheHintEdit()
    {
        string Text = Source.Replace("\"Order\"", "\"Order; label\" %% keep this ; comment");
        Assert.That(PreviewNodePlacement.TryCreate(Text, "order", Items(), new(195, 145), out var Edit, out var Reason), Is.True, Reason);
        Assert.That(Edit!.Apply(Text), Does.Contain("\"Order; label\" left-of webShop %% keep this ; comment\n"));
    }

    internal static PreviewObject[] Items() =>
    [
        new() { Id = "webShop", Kind = PreviewObjectKind.Node, Left = 300, Top = 100, Right = 450, Bottom = 190 },
        new() { Id = "order", Kind = PreviewObjectKind.Node, Left = 500, Top = 100, Right = 650, Bottom = 190 }
    ];
}

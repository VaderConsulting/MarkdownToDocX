using System;
using System.Linq;
using System.Xml.Linq;
using LiveEditor.Archi;
using LiveEditor.Completion;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture]
public sealed class DiagonalPlacementHintTests
{
    [TestCase("NW")]
    [TestCase("NE")]
    [TestCase("SE")]
    [TestCase("SW")]
    public void CompletionOffersTheNewDirectionAndItsPlacementPeer(string Direction)
    {
        var Engine = new EnterpriseCompletionEngine();
        string Source = "enterprise\napplication-component a\napplication-component b \"B\" ";
        var Clause = Engine.Build(Source, Source.Length);
        Assert.That(Clause.Items.Any(Item => Item.InsertText == Direction + " "), Is.True);
        Source += Direction + " ";
        var Peers = Engine.Build(Source, Source.Length);
        Assert.That(Peers.Items.Any(Item => Item.InsertText.Trim() == "a"), Is.True);
    }

    [TestCase(PlacementHintKind.NorthWest, "NW")]
    [TestCase(PlacementHintKind.NorthEast, "NE")]
    [TestCase(PlacementHintKind.SouthEast, "SE")]
    [TestCase(PlacementHintKind.SouthWest, "SW")]
    public void ArchiPropertiesAndSourceRoundTripBothOffsets(PlacementHintKind Kind, string Direction)
    {
        var Element = new XElement("element");
        ArchiHintProperties.WritePlacement(Element, new() { Kind = Kind, PeerId = "a", Offset = 2, ColumnOffset = 5 });
        var Hint = ArchiHintProperties.ReadPlacement(Element)!;
        Assert.That(Hint.Kind, Is.EqualTo(Kind));
        Assert.That(Hint.ColumnOffset, Is.EqualTo(5));
        string Suffix = ArchiHintProperties.FormatPlacementSuffix(Hint);
        Assert.That(Suffix, Is.EqualTo(" " + Direction + " a +2 +5"));
        var Parsed = EnterpriseValidator.Analyze("enterprise\napplication-component a\napplication-component b" + Suffix + "\n");
        Assert.That(Parsed.HasErrors, Is.False);
        Assert.That(Parsed.Document.Elements["b"].PlacementHint!.ColumnOffset, Is.EqualTo(5));
        Assert.That(EnterpriseHelp.PlacementDocumentation(Direction), Does.Contain("orthogonal"));
    }
}

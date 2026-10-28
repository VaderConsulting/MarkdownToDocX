using System;
using System.Linq;
using System.Xml.Linq;
using LiveEditor.Archi;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture, NonParallelizable]
public sealed class ArchiRoutingPersistenceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void RelationshipPointsAndPlacementOffsetsSurviveFileRoundTrip(bool Exchange)
    {
        const string Source = "enterprise\napplication-component origin at -2 3\napplication-component peer below origin +4\n"
            + "origin -flow-> peer \"label\" [leave S@-2, enter N@0, travel W]\n";
        Assert.That(EnterpriseArchiMapper.TryFromMermaidSource(Source, out var Model, out var Error), Is.True, Error);
        string Xml = Exchange ? ArchiMateExchangeXml.Write(Model) : ArchiNativeModelXml.Write(Model);
        ArchiExchangeModel Loaded;
        Assert.That(Exchange ? ArchiMateExchangeXml.TryRead(Xml, out Loaded, out Error)
            : ArchiNativeModelXml.TryRead(Xml, out Loaded, out Error), Is.True, Error);
        var Parsed = EnterpriseValidator.Analyze(EnterpriseArchiMapper.ToMermaidSource(Loaded));
        Assert.That(Parsed.HasErrors, Is.False);
        var Hint = Parsed.Document.Relationships.Single().RoutingHint!;
        Assert.That((Hint.Leave, Hint.LeavePoint, Hint.Enter, Hint.EnterPoint, Hint.Travel),
            Is.EqualTo(((LayoutHintDirection?)LayoutHintDirection.South, (int?)-2, (LayoutHintDirection?)LayoutHintDirection.North,
                (int?)0, (LayoutHintDirection?)LayoutHintDirection.West)));
        Assert.That(Parsed.Document.Elements.Values.Single(Node => Node.PlacementHint?.Kind == PlacementHintKind.Below).PlacementHint!.Offset, Is.EqualTo(4));
        Assert.That(Parsed.Document.Elements.Values.Single(Node => Node.PlacementHint?.Kind == PlacementHintKind.At).PlacementHint!.CellRow, Is.EqualTo(-2));
    }

    [TestCase("South|North||999999999999|0")]
    [TestCase("999|North||0|0")]
    [TestCase("|North||1|0")]
    public void MalformedRoutingPropertiesDoNotInventAHint(string Value)
    {
        var Node = new XElement("element", new XElement("properties", new XElement("property",
            new XAttribute("key", ArchiHintProperties.ROUTING_KEY), new XAttribute("value", Value))));
        Assert.That(ArchiHintProperties.ReadRouting(Node), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ArchiHintSwitchStillControlsRoutingProperties(bool Exchange)
    {
        bool Before = LiveEditorFeatureGate.W_ArchiHintRoundTrip;
        try
        {
            Assert.That(EnterpriseArchiMapper.TryFromMermaidSource("enterprise\napplication-component a\napplication-component b\na -flow-> b [leave S@1]\n",
                out var Model, out _), Is.True);
            LiveEditorFeatureGate.W_ArchiHintRoundTrip = false;
            Assert.That(EnterpriseArchiMapper.ToMermaidSource(Model), Does.Not.Contain("[leave"));
            string Xml = Exchange ? ArchiMateExchangeXml.Write(Model) : ArchiNativeModelXml.Write(Model);
            Assert.That(Xml, Does.Not.Contain(ArchiHintProperties.ROUTING_KEY));
        }
        finally { LiveEditorFeatureGate.W_ArchiHintRoundTrip = Before; }
    }
}

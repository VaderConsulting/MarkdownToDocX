using System.Collections.Generic;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Architecture;
using MermaidRecognition.Kinds.Block;
using MermaidRecognition.Kinds.C4;
using MermaidRecognition.Kinds.Charts;
using MermaidRecognition.Kinds.Class;
using MermaidRecognition.Kinds.Er;
using MermaidRecognition.Kinds.Sequence;
using MermaidRecognition.Kinds.State;
using NUnit.Framework;

namespace MermaidRecognition.Tests.Core;

/// <summary>
/// Ensures every LiveEditor diagram kind has a Parser that detects, parses line ids, and tracks used identifiers.
/// </summary>
[TestFixture]
public sealed class AllKindParserTests
{
    private static readonly object[] Cases =
    {
        new object[] { "sequenceDiagram\nparticipant alice as Alice\nactor bob as Bob\nalice->>bob: Hi\n", SequenceParser.Default, "alice", "bob" },
        new object[] { "classDiagram\nclass Shop\nclass Cart\nShop --> Cart\n", ClassDiagramParser.Default, "Shop", "Cart" },
        new object[] { "stateDiagram-v2\nstate \"Ready\" as ready\nstate \"Done\" as done\nready --> done\n", StateDiagramParser.Default, "ready", "done" },
        new object[] { "erDiagram\nCUSTOMER {\n  string name\n}\nORDER {\n  string id\n}\nCUSTOMER ||--o{ ORDER : places\n", ErDiagramParser.Default, "CUSTOMER", "ORDER" },
        new object[] { "architecture-beta\ngroup api(API)\nservice gateway(Gateway) in api\njunction j1\n", ArchitectureParser.Default, "api", "gateway" },
        new object[] { "block-beta\ncolumns 1\na[\"Alpha\"]\nb[\"Beta\"]\na --> b\n", BlockDiagramParser.Default, "a", "b" },
        new object[] { "C4Context\nPerson(user, \"User\")\nSystem(shop, \"Shop\")\nRel(user, shop, \"Uses\")\n", C4Parser.Default, "user", "shop" },
        new object[] { "gantt\nsection Build\nTask one :a1, 2024-01-01, 3d\n", GanttParser.Default, "a1", "Build" },
        new object[] { "pie\n\"Apples\" : 40\n\"Oranges\" : 60\n", PieParser.Default, "Apples", "Oranges" },
        new object[] { "gitGraph\ncommit id: \"c1\"\nbranch feature\n", GitGraphParser.Default, "c1", "feature" },
        new object[] { "mindmap\nroot((Idea))\n  Topic\n", MindmapParser.Default, "Idea", "Topic" },
        new object[] { "timeline\nsection 2024\nLaunch\n", TimelineParser.Default, "2024", "Launch" },
        new object[] { "journey\nsection Arrive\nPark: 5: Me\n", JourneyParser.Default, "Arrive", "Park" },
        new object[] { "quadrantChart\nCampaign A: [0.3, 0.6]\n", QuadrantChartParser.Default, "Campaign A", "Campaign A" },
        new object[] { "xychart-beta\ntitle Results\nline [1, 2, 3]\n", XyChartParser.Default, "line", "line" },
        new object[] { "packet-beta\n0-7: \"Type\"\n", PacketParser.Default, "Type", "Type" },
        new object[] { "radar-beta\naxis Speed\n", RadarParser.Default, "Speed", "Speed" },
        new object[] { "treemap-beta\nLeaf: 10\n", TreemapParser.Default, "Leaf", "Leaf" },
    };

    [TestCaseSource(nameof(Cases))]
    public void ForSourceFindsKindParser(string Source, Parser Expected, string? FirstId, string? SecondId)
    {
        Parser? Found = Parser.ForSource(Source);
        Assert.That(Found, Is.SameAs(Expected));
        DiagramParseResult Result = Expected.Parse(Source);
        Assert.That(Result.Items.Count, Is.GreaterThan(0));
        HashSet<string> Used = Parser.CollectUsedIdentifiers(Source);
        if (FirstId != null)
        {
            Assert.That(Used, Does.Contain(FirstId));
        }

        if (SecondId != null)
        {
            Assert.That(Used, Does.Contain(SecondId));
        }
    }

    [Test]
    public void CollectUsedIdentifiersFreesDeletedIds()
    {
        const string Before = "sequenceDiagram\nparticipant alice as Alice\nparticipant bob as Bob\n";
        HashSet<string> Used = Parser.CollectUsedIdentifiers(Before);
        Assert.That(Used, Does.Contain("alice"));
        Assert.That(Used, Does.Contain("bob"));

        const string After = "sequenceDiagram\nparticipant alice as Alice\n";
        Used = Parser.CollectUsedIdentifiers(After);
        Assert.That(Used, Does.Contain("alice"));
        Assert.That(Used, Does.Not.Contain("bob"));
    }
}

using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Class;

/// <summary>
/// Confirms class G12 (L4) parity axes via verified fixtures.
/// </summary>
[TestFixture]
public sealed class ClassL4AxisGateTests
{
    [Test]
    public void G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetClassFixtureDirectory(), "init-leading.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("theme", out string? Theme), Is.True);
        Assert.That(Theme, Is.EqualTo("base"));
    }

    [Test]
    public void G12_Accessibility_Fixture_Populates_AccTitle_And_AccDescr()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetClassFixtureDirectory(), "accessibility.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Domain model"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Describes core\r\ndomain entities"));
    }

    [Test]
    public void G12_ClassApply_Fixture_Applies_ClassDef_To_Target_Node()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetClassFixtureDirectory(), "classDef-style.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.ClassDefs, Has.Count.EqualTo(1));
        Assert.That(Contract.ClassDefs[0].ClassId, Is.EqualTo("highlight"));
        Assert.That(Contract.ClassDefs[0].Style.Stroke, Is.EqualTo("#333"));

        NodeContract Animal = Contract.Nodes.Single(Node => Node.Id == "Animal");
        Assert.That(Animal.Classes, Contains.Item("highlight"));
        Assert.That(Animal.InlineStyle.Stroke, Is.EqualTo("#333"));
    }

    [Test]
    public void G12_Relation_Fixtures_Produce_Edges_With_Expected_Tokens()
    {
        (string FixtureName, string ExpectedToken)[] RelationCases =
        [
            ("inheritance", "<|--"),
            ("relation-composition", "*--"),
            ("relation-aggregation", "o--"),
            ("relation-association", "-->"),
            ("relation-dependency", "..>"),
            ("relation-realization", "<|.."),
            ("relation-reverse-association", "<--"),
            ("relation-solid-link", "--"),
            ("relation-dotted-link", "..")
        ];

        string FixtureRoot = RecognitionFixturePaths.GetClassFixtureDirectory();
        List<string> Failures = [];

        foreach ((string FixtureName, string ExpectedToken) in RelationCases)
        {
            string MmdPath = Path.Combine(FixtureRoot, FixtureName + ".mmd");
            if (!File.Exists(MmdPath))
            {
                Failures.Add(FixtureName + " (missing .mmd)");
                continue;
            }

            DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
            bool HasExpectedToken = Contract.Edges.Any(
                Edge =>
                    Edge.Metadata.TryGetValue("classRelation", out MermaidMetadataValue? MetadataValue)
                    && MetadataValue is RawMetadata RelationMetadata
                    && RelationMetadata.RawValue == ExpectedToken);

            if (!HasExpectedToken)
            {
                Failures.Add(FixtureName + " (expected " + ExpectedToken + ")");
            }
        }

        Assert.That(Failures, Is.Empty, "Relation token matrix failures: " + string.Join(", ", Failures));
    }

    [Test]
    public void G12_Members_Fixture_Attaches_Member_Declarations()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetClassFixtureDirectory(), "members.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        NodeContract Animal = Contract.Nodes.Single(Node => Node.Id == "Animal");
        List<MermaidDeclaration> Members = [.. Animal.Declarations.Where(Declaration => Declaration.Kind == "member")];

        Assert.That(Members, Has.Count.EqualTo(2));
        Assert.That(Members[0].RawText, Is.EqualTo("+String name"));
        Assert.That(Members[1].RawText, Is.EqualTo("+int age"));
    }

    [Test]
    public void G12_Namespace_Fixture_Assigns_ParentGroup_And_Groups()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetClassFixtureDirectory(), "namespace.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Groups, Has.Count.EqualTo(1));

        GroupContract Animals = Contract.Groups[0];
        Assert.That(Animals.Id, Is.EqualTo("animals"));
        Assert.That(Animals.ChildNodeIds, Is.EquivalentTo(["Animal", "Dog"]));

        NodeContract Animal = Contract.Nodes.Single(Node => Node.Id == "Animal");
        NodeContract Dog = Contract.Nodes.Single(Node => Node.Id == "Dog");
        Assert.That(Animal.ParentGroupId, Is.EqualTo("animals"));
        Assert.That(Dog.ParentGroupId, Is.EqualTo("animals"));
    }

    [Test]
    public void G12_Direction_Fixture_Sets_Diagram_Direction()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetClassFixtureDirectory(), "direction.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Direction, Is.EqualTo("LeftToRight"));
        Assert.That(Contract.Edges, Has.Count.EqualTo(1));
        Assert.That(Contract.Edges[0].Metadata["classRelation"], Is.InstanceOf<RawMetadata>());
        RawMetadata RelationMetadata = (RawMetadata)Contract.Edges[0].Metadata["classRelation"];
        Assert.That(RelationMetadata.RawValue, Is.EqualTo("<|--"));
    }

    [Test]
    public void G11_Manifest_Fixtures_All_Parse_Without_Error()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "class.gate-manifest.json"),
            RecognitionFixturePaths.GetClassFixtureDirectory(),
            "classDiagram");
    }
}

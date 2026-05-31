using MermaidRecognition;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Er;

/// <summary>
/// Confirms ER G12 (L4) parity axes via verified fixtures.
/// </summary>
[TestFixture]
public sealed class ErL4AxisGateTests
{
    [Test]
    public void G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetErFixtureDirectory(), "init-leading.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("theme", out string Theme), Is.True);
        Assert.That(Theme, Is.EqualTo("base"));
    }

    [Test]
    public void G12_RelationshipCardinality_Fixtures_Produce_Edges_With_Expected_Markers()
    {
        (string FixtureName, string SourceId, string TargetId, string LeftMarker, string Link, string RightMarker)[] Cases =
            new (string FixtureName, string SourceId, string TargetId, string LeftMarker, string Link, string RightMarker)[]
            {
                ("relationship", "CUSTOMER", "ORDER", "||", "--", "o{"),
                ("relationship", "ORDER", "LINE-ITEM", "||", "--", "|{"),
                ("relation-cardinality", "A", "B", "||", "--", "||"),
                ("relation-cardinality", "C", "D", "|o", "--", "o|"),
                ("relation-cardinality", "E", "F", "||", "..", "o{"),
                ("relation-cardinality", "G", "H", "||", "--", "|{"),
                ("relation-cardinality", "I", "J", "}o", "--", "||"),
            };

        string FixtureRoot = RecognitionFixturePaths.GetErFixtureDirectory();
        List<string> Failures = new List<string>();

        foreach ((string FixtureName, string SourceId, string TargetId, string LeftMarker, string Link, string RightMarker) in Cases)
        {
            string MmdPath = Path.Combine(FixtureRoot, FixtureName + ".mmd");
            if (!File.Exists(MmdPath))
            {
                Failures.Add(FixtureName + " (missing .mmd)");
                continue;
            }

            DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
            EdgeContract? Edge = Contract.Edges.FirstOrDefault(
                Candidate => Candidate.SourceId == SourceId && Candidate.TargetId == TargetId);

            if (Edge is null)
            {
                Failures.Add(FixtureName + " " + SourceId + "->" + TargetId + " (missing edge)");
                continue;
            }

            if (!TryGetErMarker(Edge, "erLeftMarker", out string ActualLeft)
                || ActualLeft != LeftMarker
                || !TryGetErMarker(Edge, "erLink", out string ActualLink)
                || ActualLink != Link
                || !TryGetErMarker(Edge, "erRightMarker", out string ActualRight)
                || ActualRight != RightMarker)
            {
                Failures.Add(
                    FixtureName + " " + SourceId + "->" + TargetId
                    + " (expected " + LeftMarker + Link + RightMarker + ")");
            }
        }

        Assert.That(Failures, Is.Empty, "Cardinality marker matrix failures: " + string.Join(", ", Failures));
    }

    [Test]
    public void G12_EntityAttributes_Fixture_Attaches_Attribute_Declarations()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetErFixtureDirectory(), "entity-attributes.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        NodeContract Customer = Contract.Nodes.Single(Node => Node.Id == "CUSTOMER");
        List<MermaidDeclaration> Attributes = Customer.Declarations
            .Where(Declaration => Declaration.Kind == "attribute")
            .ToList();

        Assert.That(Attributes, Has.Count.EqualTo(2));
        Assert.That(Attributes[0].RawText, Is.EqualTo("string name"));
        Assert.That(Attributes[1].RawText, Is.EqualTo("string email PK"));
        Assert.That(Contract.Edges, Is.Empty);
    }

    [Test]
    public void G12_Direction_Fixture_Sets_Diagram_Direction()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetErFixtureDirectory(), "direction.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Direction, Is.EqualTo("LeftToRight"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.direction"),
            Is.True);
        Assert.That(Contract.Edges, Has.Count.EqualTo(1));
        Assert.That(TryGetErMarker(Contract.Edges[0], "erLeftMarker", out string LeftMarker), Is.True);
        Assert.That(LeftMarker, Is.EqualTo("||"));
    }

    [Test]
    public void G12_ClassDefStyle_Fixture_Applies_ClassDef_And_Class_To_Entity()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetErFixtureDirectory(), "classDef-style.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.ClassDefs, Has.Count.EqualTo(1));
        Assert.That(Contract.ClassDefs[0].ClassId, Is.EqualTo("highlight"));

        NodeContract Customer = Contract.Nodes.Single(Node => Node.Id == "CUSTOMER");
        Assert.That(Customer.Classes, Contains.Item("highlight"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.classDef"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.class"),
            Is.True);
    }

    [Test]
    public void G12_InlineStyle_Fixture_Applies_Fill_To_Entity()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetErFixtureDirectory(), "inline-style.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        NodeContract Customer = Contract.Nodes.Single(Node => Node.Id == "CUSTOMER");
        Assert.That(Customer.InlineStyle.Fill, Is.EqualTo("#bbf"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.style"),
            Is.True);
    }

    [Test]
    public void G12_Accessibility_Fixture_Populates_AccTitle_And_AccDescr()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetErFixtureDirectory(), "accessibility.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Sales schema"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Customer and order entities"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.accTitle"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.accDescr"),
            Is.True);
    }

    [Test]
    public void G12_AccDescrBlock_Fixture_Populates_Multiline_Description()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetErFixtureDirectory(), "accDescr-block.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Accessibility.Title, Is.EqualTo("Sales schema"));
        Assert.That(Contract.Accessibility.Description, Is.EqualTo("Entity relationship model\r\nfor the sales domain"));
    }

    [Test]
    public void G12_EntityAlias_Fixture_Classifies_EntityAlias_LineType()
    {
        string MmdPath = Path.Combine(
            RecognitionFixturePaths.GetErFixtureDirectory(),
            "catalogue-remarked-entity-alias.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.RemarkStatus, Is.EqualTo(MermaidDiagramRemarkStatus.CatalogueRemarked));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.entityAlias"),
            Is.True);
        Assert.That(
            Contract.SourceStatements.Any(
                Statement =>
                    Statement.LineTypeId == "erDiagram.entityAlias"
                    && Statement.RawLine.Contains("[Customer Account]", StringComparison.Ordinal)),
            Is.True);
    }

    [Test]
    public void G11_Manifest_Fixtures_All_Parse_Without_Error()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "er.gate-manifest.json"),
            RecognitionFixturePaths.GetErFixtureDirectory(),
            "erDiagram");
    }

    static bool TryGetErMarker(EdgeContract Edge, string Key, out string Value)
    {
        Value = string.Empty;
        if (!Edge.Metadata.TryGetValue(Key, out MermaidMetadataValue MetadataValue)
            || MetadataValue is not RawMetadata RawMetadata)
        {
            return false;
        }

        Value = RawMetadata.RawValue;
        return true;
    }
}

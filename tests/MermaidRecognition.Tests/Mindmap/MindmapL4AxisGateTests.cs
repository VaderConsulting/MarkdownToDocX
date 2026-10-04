using MermaidRecognition.Contract;



namespace MermaidRecognition.Tests.Mindmap;



/// <summary>

/// Confirms mindmap G12 (L4) parity axes via verified fixtures.

/// </summary>

[TestFixture]

public sealed class MindmapL4AxisGateTests

{

    [Test]

    public void G12_Hierarchy_Fixture_Produces_Three_Level_Tree()

    {

        string MmdPath = Path.Combine(RecognitionFixturePaths.GetMindmapFixtureDirectory(), "hierarchy.mmd");

        Assert.That(File.Exists(MmdPath), Is.True);



        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(Contract.Nodes.Count, Is.EqualTo(4));



        NodeContract Root = Contract.Nodes.Single(Node => Node.Label == "root");

        NodeContract Child = Contract.Nodes.Single(Node => Node.Label == "child1");

        NodeContract Leaf = Contract.Nodes.Single(Node => Node.Label == "leaf1");



        Assert.That(Root.ParentGroupId, Is.Null);

        Assert.That(Child.ParentGroupId, Is.EqualTo(Root.Id));

        Assert.That(Leaf.ParentGroupId, Is.EqualTo(Child.Id));

    }



    [Test]

    public void G12_InitLeading_Fixture_Populates_Theme_In_RawDirectiveValues()

    {

        string MmdPath = Path.Combine(RecognitionFixturePaths.GetMindmapFixtureDirectory(), "init-leading.mmd");

        Assert.That(File.Exists(MmdPath), Is.True);



        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("theme", out string? Theme), Is.True);

        Assert.That(Theme, Is.EqualTo("default"));

    }



    [Test]

    public void G12_Shapes_Fixture_Parses_Circle_Cloud_Bang_And_Hexagon()

    {

        string MmdPath = Path.Combine(RecognitionFixturePaths.GetMindmapFixtureDirectory(), "shapes.mmd");

        Assert.That(File.Exists(MmdPath), Is.True);



        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(Contract.Nodes.Single(Node => Node.Label == "the root").Shape, Is.EqualTo("circle"));

        Assert.That(Contract.Nodes.Single(Node => Node.Label == "cloud label").Shape, Is.EqualTo("cloud"));

        Assert.That(Contract.Nodes.Single(Node => Node.Label == "bang label").Shape, Is.EqualTo("bang"));

        Assert.That(Contract.Nodes.Single(Node => Node.Label == "hex label").Shape, Is.EqualTo("hexagon"));

    }



    [Test]

    public void G10_Multiple_Roots_Emits_Orphan_Node_Diagnostic()

    {

        string Source = """

            mindmap

              root

              fakeRoot

            """;



        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        MermaidDiagnostic Diagnostic = Contract.Diagnostics.Single(

            Candidate => Candidate.Code == "mindmap.orphan-node");



        Assert.That(Diagnostic.Severity, Is.EqualTo(MermaidDiagnosticSeverity.Error));

        Assert.That(Diagnostic.Message, Does.Contain("fakeRoot"));

        Assert.That(Contract.Nodes.Count, Is.EqualTo(1));

        Assert.That(Contract.Nodes[0].Label, Is.EqualTo("root"));

    }



    [Test]

    public void G11_Manifest_Fixtures_All_Parse_Without_Error()

    {

        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(

            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "mindmap.gate-manifest.json"),

            RecognitionFixturePaths.GetMindmapFixtureDirectory(),

            "mindmap");

    }

}



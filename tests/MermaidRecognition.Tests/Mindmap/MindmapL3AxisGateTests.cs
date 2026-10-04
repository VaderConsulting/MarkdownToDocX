using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Mindmap;

/// <summary>
/// Confirms mindmap G4 axis coverage via verified fixtures (L3 gate).
/// </summary>
[TestFixture]
public sealed class MindmapL3AxisGateTests
{
    [Test]
    public void G4_EntityModel_Fixtures_Produce_Hierarchical_Nodes()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetMindmapFixtureDirectory(),
            new[] { "simple", "comment", "hierarchy" },
            Contract => Contract.Nodes.Count > 0);
    }

    [Test]
    public void G5_Containment_Fixtures_Produce_Parent_Links()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetMindmapFixtureDirectory(),
            new[] { "simple", "hierarchy" },
            Contract => Contract.Nodes.Any(Node => Node.ParentGroupId is not null));
    }

    [Test]
    public void G5_Containment_Hierarchy_Fixture_Produces_Three_Level_Tree()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetMindmapFixtureDirectory(), "hierarchy.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
        NodeContract Root = Contract.Nodes.Single(Node => Node.Label == "root");
        NodeContract Child = Contract.Nodes.Single(Node => Node.Label == "child1");
        NodeContract Leaf = Contract.Nodes.Single(Node => Node.Label == "leaf1");

        Assert.That(Root.ParentGroupId, Is.Null);
        Assert.That(Child.ParentGroupId, Is.EqualTo(Root.Id));
        Assert.That(Leaf.ParentGroupId, Is.EqualTo(Child.Id));
    }

    [Test]
    public void G4_Shapes_Fixture_Produces_Non_Default_Shapes()
    {
        WaveKindL3AxisGateTestHelper.AssertAxis(
            RecognitionFixturePaths.GetMindmapFixtureDirectory(),
            new[] { "shapes" },
            Contract => Contract.Nodes.Any(Node => Node.Shape != "mindmap"));
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

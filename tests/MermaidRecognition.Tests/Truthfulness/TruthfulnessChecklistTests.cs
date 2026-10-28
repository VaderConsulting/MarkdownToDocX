namespace MermaidRecognition.Tests.Truthfulness;

using System.Reflection;
using MermaidRecognition;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

/// <summary>
/// Automated checks aligned with <c>docs/currentstatus.md</c>.
/// </summary>
[TestFixture]
public sealed class TruthfulnessChecklistTests
{
    [Test]
    public void Checklist_FlowchartRouter_Type_Exists()
    {
        Type? RouterType = typeof(FlowchartRouter);
        Assert.That(RouterType, Is.Not.Null);
        Assert.That(RouterType!.IsClass, Is.True);
    }

    [Test]
    public void Checklist_MermaidRecognizer_MultiKind_Entry_Exists()
    {
        Type RecognizerType = typeof(MermaidRecognizer);
        MethodInfo[] ParseMethods = [.. RecognizerType.GetMethods().Where(Method => Method.Name == "Parse")];
        Assert.That(ParseMethods.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Checklist_Started_Kinds_Manifests_Are_L4()
    {
        string[] ExpectedKinds =
        [
            "flowchart", "sequence", "class", "state", "er", "pie", "gantt", "mindmap", "gitGraph"
        ];

        string ManifestDir = RecognitionFixturePaths.GetManifestsDirectory();

        foreach (string Kind in ExpectedKinds)
        {
            string ManifestPath = Path.Combine(ManifestDir, Kind + ".gate-manifest.json");
            Assert.That(File.Exists(ManifestPath), Is.True, "Missing manifest for " + Kind);
            string Json = File.ReadAllText(ManifestPath);
            Assert.That(Json, Does.Contain("\"GateLevel\": \"L4\""), Kind + " should be L4");
        }
    }

    [Test]
    public void Checklist_NativeRenderCapabilities_Accepts_Flowchart_Fixture()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "chain-a-b-c.mmd");
        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(NativeRenderCapabilities.CanRender(Contract), Is.True);
    }

    [Test]
    public void Checklist_NativeFlowchartSvgRenderer_Produces_Output()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "chain-a-b-c.mmd");
        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        string Svg = NativeFlowchartSvgRenderer.RenderToSvg(Contract);
        Assert.That(Svg.Length, Is.GreaterThan(50));
    }
}

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

/// <summary>
/// Confirms flowchart G12 (L4) parity axes via verified fixtures.
/// </summary>
[TestFixture]
public sealed class FlowchartL4AxisGateTests
{
    [Test]
    public void G12_F88_EdgeClassAnimate_Fixture_Produces_Animation_Metadata()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "edge-class-animate.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));
        EdgeContract Edge = Contract.Edges.Single(Candidate => Candidate.Id == "e1");
        Assert.That(Edge.Classes, Contains.Item("animate"));
        Assert.That(Edge.Metadata.ContainsKey("animate"), Is.True);
    }

    [Test]
    public void G12_F86_FrontMatterConfig_Sets_FlowchartCurve()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "config-front-matter.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("flowchart.curve", out string? Curve), Is.True);
        Assert.That(Curve, Is.EqualTo("linear"));
        Assert.That(Contract.Edges.Count, Is.EqualTo(2));
    }

    [Test]
    public void G12_F86_FrontMatterConfig_Sets_FlowchartSpacing()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "config-front-matter-spacing.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("flowchart.nodeSpacing", out string? NodeSpacing), Is.True);
        Assert.That(NodeSpacing, Is.EqualTo("60"));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("flowchart.rankSpacing", out string? RankSpacing), Is.True);
        Assert.That(RankSpacing, Is.EqualTo("70"));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("flowchart.diagramPadding", out string? Padding), Is.True);
        Assert.That(Padding, Is.EqualTo("12"));
    }

    [Test]
    public void G12_F86_FrontMatterConfig_Sets_Theme_And_FlowchartCurve()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "config-front-matter-theme.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("theme", out string? Theme), Is.True);
        Assert.That(Theme, Is.EqualTo("forest"));
        Assert.That(Contract.Configuration.RawDirectiveValues.TryGetValue("flowchart.curve", out string? Curve), Is.True);
        Assert.That(Curve, Is.EqualTo("linear"));
        Assert.That(Contract.Edges.Count, Is.EqualTo(1));
    }

    [Test]
    public void G12_F87_EdgeMetadataAnimate_On_ThickLink()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "edge-metadata-f87.mmd");
        Assert.That(File.Exists(MmdPath), Is.True);

        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));
        EdgeContract Edge = Contract.Edges.Single(Candidate => Candidate.Id == "e1");
        Assert.That(Edge.Metadata.ContainsKey("animate"), Is.True);
        Assert.That(Edge.Metadata["animate"], Is.InstanceOf<AnimationMetadata>());
        AnimationMetadata Animation = (AnimationMetadata)Edge.Metadata["animate"];
        Assert.That(Animation.Animate, Is.True);
    }

    [Test]
    public void G11_Manifest_Fixtures_All_Parse_Without_Error()
    {
        WaveKindL3AxisGateTestHelper.AssertManifestFixturesParse(
            Path.Combine(RecognitionFixturePaths.GetManifestsDirectory(), "flowchart.gate-manifest.json"),
            RecognitionFixturePaths.GetFlowchartFixtureDirectory(),
            "flowchart");
    }
}

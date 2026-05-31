using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

/// <summary>
/// G12 parity: F88 <c>classDef</c> animation styling applied to edge id via <c>class e1 animate</c>.
/// </summary>
public class FlowchartEdgeClassAnimateTests
{
    [Test]
    public void EdgeClassDefAnimation_SetsAnimateMetadataFromClass()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
                A e1@--> B
                classDef animate stroke-dasharray:9,5,stroke-dashoffset:900,animation:dash 25s linear infinite;
                class e1 animate
            """);

        EdgeContract Edge = Contract.Edges.Single(Candidate => Candidate.Id == "e1");
        Assert.That(Edge.Classes, Contains.Item("animate"));
        Assert.That(GetEdgeAnimate(Contract, "e1"), Is.True);

        AnimationMetadata Metadata = (AnimationMetadata)Edge.Metadata["animate"];
        Assert.That(Metadata.Animation, Is.EqualTo("dash"));
    }

    [Test]
    public void EdgeClassAnimateFixture_Matches_F88_Scenario()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "edge-class-animate.mmd");
        Assert.That(File.Exists(MmdPath), Is.True, "Missing fixture edge-class-animate.mmd");

        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(Contract.Edges.Count, Is.EqualTo(1));
        Assert.That(GetEdgeAnimate(Contract, "e1"), Is.True);
        ClassDefContract ClassDef = Contract.ClassDefs.Single();
        Assert.That(ClassDef.ClassId, Is.EqualTo("animate"));
        Assert.That(ClassDef.Style.Animation, Does.Contain("dash"));
    }

    static bool? GetEdgeAnimate(DiagramContract Contract, string EdgeId)
    {
        EdgeContract Edge = Contract.Edges.Single(Candidate => Candidate.Id == EdgeId);
        if (!Edge.Metadata.TryGetValue("animate", out MermaidMetadataValue? Value)
            || Value is not AnimationMetadata Animation)
        {
            return null;
        }

        return Animation.Animate;
    }
}

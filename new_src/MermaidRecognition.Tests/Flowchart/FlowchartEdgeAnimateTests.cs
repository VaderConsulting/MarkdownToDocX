using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

/// <summary>
/// G12 parity: edge <c>@{ animate: ... }</c> metadata (vendored flow-node-data.spec.js).
/// </summary>
public class FlowchartEdgeAnimateTests
{
    [Test]
    public void EdgeAnimateMetadata_OverrideLastValueWins()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A e1@--> B
                C e2@--> D
                E e3@--> F
                e1@{ animate: true }
                e2@{ animate: false }
                e3@{ animate: true }
                e3@{ animate: false }
            """);

        Assert.That(GetEdgeAnimate(Contract, "e1"), Is.True);
        Assert.That(GetEdgeAnimate(Contract, "e2"), Is.False);
        Assert.That(GetEdgeAnimate(Contract, "e3"), Is.False);
    }

    [Test]
    public void EdgeAnimateFixture_Matches_Vendored_Override_Scenario()
    {
        string MmdPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "edge-animate.mmd");
        if (!File.Exists(MmdPath))
        {
            Assert.Ignore("Fixture edge-animate.mmd not found.");
        }

        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(Contract.Edges.Count, Is.EqualTo(3));
        Assert.That(GetEdgeAnimate(Contract, "e1"), Is.True);
        Assert.That(GetEdgeAnimate(Contract, "e2"), Is.False);
        Assert.That(GetEdgeAnimate(Contract, "e3"), Is.False);
    }

    [Test]
    public void EdgeAnimationMetadata_PreservesAnimationToken()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
                A e1@--> B
                e1@{ animate: true, animation: dash }
            """);

        EdgeContract Edge = Contract.Edges.Single();
        AnimationMetadata Metadata = (AnimationMetadata)Edge.Metadata["animate"];
        Assert.That(Metadata.Animate, Is.True);
        Assert.That(Metadata.Animation, Is.EqualTo("dash"));
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

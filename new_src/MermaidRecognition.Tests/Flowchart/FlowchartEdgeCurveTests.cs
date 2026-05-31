using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

/// <summary>
/// G12 parity: edge <c>@{ curve: ... }</c> metadata (vendored flow-lines.spec.js).
/// </summary>
public class FlowchartEdgeCurveTests
{
    [Test]
    public void EdgeCurveMetadata_AppliesPerEdgeId()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A e1@--> B
                A uniqueName@--> C
                e1@{ curve: basis }
                uniqueName@{ curve: cardinal }
            """);

        Assert.That(GetEdgeCurve(Contract, "e1"), Is.EqualTo("basis"));
        Assert.That(GetEdgeCurve(Contract, "uniqueName"), Is.EqualTo("cardinal"));
    }

    [Test]
    public void EdgeCurveMetadata_StoresTypedCurveMetadata()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A e1@--> B
                e1@{ curve: stepAfter }
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.Metadata["curve"], Is.InstanceOf<CurveMetadata>());
        CurveMetadata Metadata = (CurveMetadata)Edge.Metadata["curve"];
        Assert.That(Metadata.Curve, Is.EqualTo("stepAfter"));
        Assert.That(Edge.Curve, Is.EqualTo("stepAfter"));
    }

    [Test]
    public void EdgeCurveFixture_Matches_Vendored_EdgeId_Scenario()
    {
        string MmdPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "edge-curve.mmd");
        if (!File.Exists(MmdPath))
        {
            Assert.Ignore("Fixture edge-curve.mmd not found.");
        }

        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(Contract.Edges.Count, Is.EqualTo(2));
        Assert.That(GetEdgeCurve(Contract, "e1"), Is.EqualTo("basis"));
        Assert.That(GetEdgeCurve(Contract, "uniqueName"), Is.EqualTo("cardinal"));
    }

    static string? GetEdgeCurve(DiagramContract Contract, string EdgeId)
    {
        EdgeContract Edge = Contract.Edges.Single(Candidate => Candidate.Id == EdgeId);
        if (!Edge.Metadata.TryGetValue("curve", out MermaidMetadataValue? Value)
            || Value is not CurveMetadata Curve)
        {
            return null;
        }

        return Curve.Curve;
    }
}

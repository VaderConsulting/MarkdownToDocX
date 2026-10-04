using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Tests.Render;

[TestFixture]
public sealed class FlowchartRouterTests
{
    [Test]
    public void TryRouteAll_Simple_Chain_Produces_SimpleLink_Routes()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "chain-a-b-c.mmd");
        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(FlowchartRouter.TryRouteAll(Contract, out FlowchartRouteResult Result), Is.True);
        Assert.That(Result.Routes.Count, Is.EqualTo(Contract.Edges.Count));
        Assert.That(Result.Routes.All(Route => Route.Kind == FlowchartRouteKind.SimpleLink), Is.True);
    }

    [Test]
    public void TryRouteAll_Subgraph_Fixture_May_Classify_CrossGroupBoundary()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "subgraph-membership.mmd");
        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(FlowchartRouter.TryRouteAll(Contract, out FlowchartRouteResult Result), Is.True);
        Assert.That(Result.Routes.Count, Is.EqualTo(Contract.Edges.Count));
    }
}

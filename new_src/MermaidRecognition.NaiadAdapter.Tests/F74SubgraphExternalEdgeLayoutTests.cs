using MermaidRecognition;
using MermaidRecognition.NaiadAdapter;
using MermaidSharp;
using MermaidSharp.Diagrams.Flowchart;
using MermaidSharp.Layout;
using MermaidSharp.Models;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class F74SubgraphExternalEdgeLayoutTests
{
    const string F74_SOURCE = """
        flowchart LR
        subgraph auditId["Audit / compliance"]
          direction TB
          r1[Record] --> r2[Review]
        end
        IN[In] --> r1
        r2 --> OUT[Out]
        """;

    const string ISOLATED_SUBGRAPH_TB_SOURCE = """
        flowchart LR
        subgraph inner["Isolated"]
          direction TB
          a[Top] --> b[Bottom]
        end
        """;

    [Test]
    public void F74_MemberExternalEdges_InheritParentLrInsideSubgraph()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F74_SOURCE);
        FlowchartModel Model = ContractToFlowchartModel.Map(Contract);
        Model.Direction = Direction.LeftToRight;

        LayoutResult Result = new DagreLayoutEngine().Layout(Model, LayoutOptions.Default);
        Assert.That(Result.Width, Is.GreaterThan(0));

        Node NodeR1 = Model.GetNode("r1")!;
        Node NodeR2 = Model.GetNode("r2")!;
        Assert.That(NodeR1.Position.X, Is.LessThan(NodeR2.Position.X),
            "r1 should be left of r2 when parent LR wins over declared subgraph TB.");
        Assert.That(Math.Abs(NodeR1.Position.Y - NodeR2.Position.Y), Is.LessThan(40.0),
            "r1 and r2 should share a rank when laid out LR inside the subgraph.");
    }

    [Test]
    public void IsolatedSubgraph_WithDeclaredTb_StillUsesTb()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(ISOLATED_SUBGRAPH_TB_SOURCE);
        FlowchartModel Model = ContractToFlowchartModel.Map(Contract);
        Model.Direction = Direction.LeftToRight;

        new DagreLayoutEngine().Layout(Model, LayoutOptions.Default);

        Node NodeA = Model.GetNode("a")!;
        Node NodeB = Model.GetNode("b")!;
        Assert.That(NodeA.Position.Y, Is.LessThan(NodeB.Position.Y),
            "Without external member edges, declared subgraph TB should stack nodes vertically.");
    }
}

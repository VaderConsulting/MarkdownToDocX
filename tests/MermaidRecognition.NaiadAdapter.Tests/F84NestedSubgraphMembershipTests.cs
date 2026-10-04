using MermaidRecognition.Core;
using MermaidRecognition.NaiadAdapter;

using MermaidSharp;
using MermaidSharp.Diagrams.Flowchart;
using MermaidSharp.Layout;
using MermaidSharp.Models;
using MermaidSharp.Rendering;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class F84NestedSubgraphMembershipTests
{
    const string F84_SOURCE = """
        flowchart TB
        External[External]
        subgraph Outer["Outer"]
          direction TB
          subgraph Inner["Inner"]
            direction TB
            Shared[Shared node]
          end
          OuterOnly[Outer only]
        end
        External --> Shared
        Shared --> OuterOnly
        """;

    [Test]
    public void Parse_F84_AssignsNestedSubgraphMembership()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F84_SOURCE);

        NodeContract Shared = Contract.Nodes.Single(Node => Node.Id == "Shared");
        NodeContract OuterOnly = Contract.Nodes.Single(Node => Node.Id == "OuterOnly");
        NodeContract External = Contract.Nodes.Single(Node => Node.Id == "External");

        Assert.That(External.ParentGroupId, Is.Null);
        Assert.That(Shared.ParentGroupId, Is.EqualTo("Inner"));
        Assert.That(OuterOnly.ParentGroupId, Is.EqualTo("Outer"));
        Assert.That(Shared.ParentGroupId, Is.Not.EqualTo("Outer"));

        GroupContract Outer = Contract.Groups.Single(Group => Group.Id == "Outer");
        GroupContract Inner = Contract.Groups.Single(Group => Group.Id == "Inner");
        Assert.That(Inner.ParentGroupId, Is.EqualTo("Outer"));
        Assert.That(Outer.ChildGroupIds, Contains.Item("Inner"));
        Assert.That(Inner.ChildNodeIds, Contains.Item("Shared"));
        Assert.That(Outer.ChildNodeIds, Contains.Item("OuterOnly"));
        Assert.That(Outer.ChildNodeIds, Does.Not.Contain("Shared"));
    }

    [Test]
    public void Layout_F84_ExternalAboveShared_AboveOuterOnly()
    {
        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(F84_SOURCE));
        new DagreLayoutEngine().Layout(Model, LayoutOptions.Default);

        Node External = Model.GetNode("External")!;
        Node Shared = Model.GetNode("Shared")!;
        Node OuterOnly = Model.GetNode("OuterOnly")!;

        Assert.That(External.Position.Y, Is.LessThan(Shared.Position.Y),
            "External should render above Shared node");
        Assert.That(Shared.Position.Y, Is.LessThan(OuterOnly.Position.Y),
            "Shared node should render above Outer only inside Outer");
    }

    [Test]
    public void Render_F84_OuterOnly_IsBelowInnerClusterBox()
    {
        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(F84_SOURCE));
        FlowchartRenderer Renderer = new FlowchartRenderer(new DagreLayoutEngine());
        Renderer.Render(Model, RenderOptions.Default);

        Node InnerGroup = Model.GetNode("Inner")!;
        Node OuterOnly = Model.GetNode("OuterOnly")!;
        Node Shared = Model.GetNode("Shared")!;

        double InnerBottom = InnerGroup.Position.Y + (InnerGroup.Height / 2.0);
        double OuterOnlyTop = OuterOnly.Position.Y - (OuterOnly.Height / 2.0);
        double SharedBottom = Shared.Position.Y + (Shared.Height / 2.0);

        Assert.That(OuterOnly.ParentId, Is.EqualTo("Outer"));
        Assert.That(Shared.ParentId, Is.EqualTo("Inner"));
        Assert.That(SharedBottom, Is.LessThanOrEqualTo(InnerBottom + 1.0),
            "Shared node should sit inside the Inner cluster box");
        Assert.That(OuterOnlyTop, Is.GreaterThan(InnerBottom + 4.0),
            "Outer only should render below the Inner cluster border, still inside Outer");
    }
}

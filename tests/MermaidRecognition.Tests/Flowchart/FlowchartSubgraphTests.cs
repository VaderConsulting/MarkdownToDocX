using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

public class FlowchartSubgraphTests
{
    [Test]
    public void Nodes_In_Subgraph_HaveParentGroupId()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                subgraph Inner [Inner title]
                    X --> Y
                end
            """);

        NodeContract NodeX = Contract.Nodes.Single(Node => Node.Id == "X");
        NodeContract NodeY = Contract.Nodes.Single(Node => Node.Id == "Y");
        Assert.That(NodeX.ParentGroupId, Is.EqualTo("Inner"));
        Assert.That(NodeY.ParentGroupId, Is.EqualTo("Inner"));
        GroupContract Group = Contract.Groups.Single(Group => Group.Id == "Inner");
        Assert.That(Group.Title, Is.EqualTo("Inner title"));
        Assert.That(Group.ChildNodeIds, Is.EquivalentTo(["X", "Y"]));
    }

    [Test]
    public void F36_SubgraphStartWithoutSpaceBeforeBracket_OpensSubgraph()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TB
            subgraph SG["Processing"]
              A[Parse] --> B[Transform] --> C[Emit]
            end
            IN[In] --> A
            C --> OUT[Out]
            """);

        GroupContract Group = Contract.Groups.Single();
        Assert.That(Group.Id, Is.EqualTo("SG"));
        Assert.That(Group.Title, Is.EqualTo("Processing"));
        Assert.That(Group.ChildNodeIds, Is.EquivalentTo(["A", "B", "C"]));

        NodeContract NodeA = Contract.Nodes.Single(Node => Node.Id == "A");
        NodeContract NodeIn = Contract.Nodes.Single(Node => Node.Id == "IN");
        Assert.That(NodeA.ParentGroupId, Is.EqualTo("SG"));
        Assert.That(NodeIn.ParentGroupId, Is.Null);
    }

    [Test]
    public void F44_ForwardReferenceIntoNestedSubgraph_MovesNodeToInnerOnExplicitDeclaration()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse("""
            flowchart TB
            subgraph Outer["Outer"]
              direction TB
              Start[Start] --> InnerTop
              subgraph Inner["Inner TB"]
                direction TB
                InnerTop[Inner top] --> InnerBottom[Inner bottom]
              end
              InnerBottom --> End[End]
            end
            """);

        NodeContract InnerTop = Contract.Nodes.Single(Node => Node.Id == "InnerTop");
        GroupContract Inner = Contract.Groups.Single(Group => Group.Id == "Inner");
        GroupContract Outer = Contract.Groups.Single(Group => Group.Id == "Outer");

        Assert.That(InnerTop.ParentGroupId, Is.EqualTo("Inner"));
        Assert.That(Inner.ChildNodeIds, Is.EquivalentTo(["InnerTop", "InnerBottom"]));
        Assert.That(Outer.ChildNodeIds, Is.EquivalentTo(["Start", "End"]));
    }

    [Test]
    public void F45_TwoLevelNesting_AssignsNodesToCorrectSubgraphs()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse("""
            flowchart TB
            subgraph L0["Level 0"]
              direction TB
              A[A] --> B[B]
              subgraph L1["Level 1"]
                direction TB
                subgraph L2["Level 2"]
                  direction TB
                  C[C] --> D[D]
                end
              end
              B --> C
              D --> E[E]
            end
            """);

        Assert.That(Contract.Nodes.Single(Node => Node.Id == "A").ParentGroupId, Is.EqualTo("L0"));
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "B").ParentGroupId, Is.EqualTo("L0"));
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "C").ParentGroupId, Is.EqualTo("L2"));
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "D").ParentGroupId, Is.EqualTo("L2"));
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "E").ParentGroupId, Is.EqualTo("L0"));
        Assert.That(Contract.Groups.Single(Group => Group.Id == "L0").ChildNodeIds, Is.EquivalentTo(["A", "B", "E"]));
        Assert.That(Contract.Groups.Single(Group => Group.Id == "L2").ChildNodeIds, Is.EquivalentTo(["C", "D"]));
    }

    [Test]
    public void F43_NestedSubgraphWithoutSpaceBeforeBracket_PreservesHierarchy()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TB
            subgraph Outer["Outer subgraph"]
              direction TB
              A["Top node"] --> B["Middle node"]
              subgraph Inner["Inner subgraph"]
                direction LR
                C["Left"] --> D["Right"]
              end
              B --> C
              D --> E["Bottom node"]
            end
            """);

        Assert.That(Contract.Groups, Has.Count.EqualTo(2));
        GroupContract Outer = Contract.Groups.Single(Group => Group.Id == "Outer");
        GroupContract Inner = Contract.Groups.Single(Group => Group.Id == "Inner");
        Assert.That(Outer.Title, Is.EqualTo("Outer subgraph"));
        Assert.That(Inner.Title, Is.EqualTo("Inner subgraph"));
        Assert.That(Inner.ParentGroupId, Is.EqualTo("Outer"));
        Assert.That(Inner.Direction, Is.EqualTo("LeftToRight"));
        Assert.That(Outer.Direction, Is.EqualTo("TopToBottom"));
    }
}

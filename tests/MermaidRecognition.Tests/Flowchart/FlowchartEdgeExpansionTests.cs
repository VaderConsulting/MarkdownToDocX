using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

public class FlowchartEdgeExpansionTests
{
    [Test]
    public void Chain_A_to_B_to_C()
    {
        DiagramContract Contract = Parse(
            """
            flowchart TD
                A --> B --> C
            """);

        AssertEdgeSequence(
            Contract,
            ("A", "B"),
            ("B", "C"));
    }

    [Test]
    public void Grouped_A_and_B_to_C()
    {
        DiagramContract Contract = Parse(
            """
            flowchart TD
                A & B --> C
            """);

        AssertEdgeSequence(
            Contract,
            ("A", "C"),
            ("B", "C"));
    }

    [Test]
    public void Grouped_A_to_B_and_C()
    {
        DiagramContract Contract = Parse(
            """
            flowchart TD
                A --> B & C
            """);

        AssertEdgeSequence(
            Contract,
            ("A", "B"),
            ("A", "C"));
    }

    [Test]
    public void Grouped_A_and_B_to_C_and_D()
    {
        DiagramContract Contract = Parse(
            """
            flowchart TD
                A & B --> C & D
            """);

        AssertEdgeSequence(
            Contract,
            ("A", "C"),
            ("A", "D"),
            ("B", "C"),
            ("B", "D"));
    }

    [Test]
    public void Chained_A_to_B_and_C_to_D()
    {
        DiagramContract Contract = Parse(
            """
            flowchart TD
                A --> B & C --> D
            """);

        AssertEdgeSequence(
            Contract,
            ("A", "B"),
            ("A", "C"),
            ("B", "D"),
            ("C", "D"));
    }

    [Test]
    public void UserDefinedEdgeId_OnlyOnFirstExpandedEdge()
    {
        DiagramContract Contract = Parse(
            """
            flowchart TD
                A & B e1@--> C & D
            """);

        EdgeContract? E1 = Contract.Edges.FirstOrDefault(Edge => Edge.Id == "e1");
        Assert.That(E1, Is.Not.Null);
        Assert.That(E1!.IsUserDefinedId, Is.True);
        Assert.That(E1.SourceId, Is.EqualTo("B"));
        Assert.That(E1.TargetId, Is.EqualTo("C"));
        Assert.That(Contract.Edges.Count(Edge => Edge.IsUserDefinedId), Is.EqualTo(1));
    }

    static DiagramContract Parse(string Source)
    {
        return FlowchartRecognizer.Parse(Source);
    }

    static void AssertEdgeSequence(DiagramContract Contract, params (string Source, string Target)[] Expected)
    {
        Assert.That(Contract.Edges.Count, Is.EqualTo(Expected.Length));
        List<EdgeContract> Ordered = [.. Contract.Edges.OrderBy(Edge => Edge.CreationIndex)];
        for (int Index = 0; Index < Expected.Length; Index++)
        {
            Assert.That(Ordered[Index].SourceId, Is.EqualTo(Expected[Index].Source));
            Assert.That(Ordered[Index].TargetId, Is.EqualTo(Expected[Index].Target));
            Assert.That(Ordered[Index].CreationIndex, Is.EqualTo(Index));
        }
    }
}

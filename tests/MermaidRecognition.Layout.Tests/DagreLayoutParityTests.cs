using MermaidRecognition.Layout.Dagre;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DagreLayoutParityTests
{
    [Test]
    public void Layout_Chain_MatchesDagre()
    {
        DagreGraph Graph = GraphWith("TB", 50, 50, 0, 0);
        Graph.SetNode("A", new DagreNodeLabel { Width = 40, Height = 20 }, true);
        Graph.SetNode("B", new DagreNodeLabel { Width = 40, Height = 20 }, true);
        Graph.SetEdge("A", "B", new DagreEdgeLabel(), true);
        DagreLayoutEngine.Layout(Graph);
        AssertNear(Graph, "A", 20, 10);
        AssertNear(Graph, "B", 20, 80);
        Assert.That(Graph.Label.Width, Is.EqualTo(40).Within(0.001));
        Assert.That(Graph.Label.Height, Is.EqualTo(90).Within(0.001));
        AssertPoints(Graph, "A", "B", 20, 20, 20, 45, 20, 70);
    }

    [Test]
    public void Layout_FanOut_MatchesDagre()
    {
        DagreGraph Graph = GraphWith("TB", 50, 50, 8, 8);
        Graph.SetNode("A", new DagreNodeLabel { Width = 100, Height = 40 }, true);
        Graph.SetNode("B", new DagreNodeLabel { Width = 80, Height = 40 }, true);
        Graph.SetNode("C", new DagreNodeLabel { Width = 80, Height = 40 }, true);
        Graph.SetEdge("A", "B", new DagreEdgeLabel(), true);
        Graph.SetEdge("A", "C", new DagreEdgeLabel(), true);
        DagreLayoutEngine.Layout(Graph);
        AssertNear(Graph, "A", 113, 28);
        AssertNear(Graph, "B", 48, 118);
        AssertNear(Graph, "C", 178, 118);
        Assert.That(Graph.Label.Width, Is.EqualTo(226).Within(0.001));
        Assert.That(Graph.Label.Height, Is.EqualTo(146).Within(0.001));
    }

    [Test]
    public void Layout_LeftToRight_MatchesDagre()
    {
        DagreGraph Graph = GraphWith("LR", 50, 50, 0, 0);
        Graph.SetNode("A", new DagreNodeLabel { Width = 40, Height = 20 }, true);
        Graph.SetNode("B", new DagreNodeLabel { Width = 40, Height = 20 }, true);
        Graph.SetEdge("A", "B", new DagreEdgeLabel(), true);
        DagreLayoutEngine.Layout(Graph);
        AssertNear(Graph, "A", 20, 10);
        AssertNear(Graph, "B", 110, 10);
    }

    [Test]
    public void Layout_Cycle_MatchesDagre()
    {
        DagreGraph Graph = GraphWith("TB", 50, 50, 0, 0);
        Graph.SetNode("A", new DagreNodeLabel { Width = 40, Height = 20 }, true);
        Graph.SetNode("B", new DagreNodeLabel { Width = 40, Height = 20 }, true);
        Graph.SetEdge("A", "B", new DagreEdgeLabel(), true);
        Graph.SetEdge("B", "A", new DagreEdgeLabel(), true);
        DagreLayoutEngine.Layout(Graph);
        AssertNear(Graph, "A", 20, 10);
        AssertNear(Graph, "B", 20, 80);
    }

    [Test]
    public void Layout_EdgeLabel_MatchesDagre()
    {
        DagreGraph Graph = GraphWith("TB", 50, 50, 0, 0);
        Graph.SetNode("A", new DagreNodeLabel { Width = 40, Height = 20 }, true);
        Graph.SetNode("B", new DagreNodeLabel { Width = 40, Height = 20 }, true);
        Graph.SetEdge("A", "B", new DagreEdgeLabel
        {
            Width = 30,
            Height = 14,
            LabelPos = "c"
        }, true);
        DagreLayoutEngine.Layout(Graph);
        AssertNear(Graph, "A", 20, 10);
        AssertNear(Graph, "B", 20, 94);
    }

    [Test]
    public void Layout_Compound_MatchesDagre()
    {
        DagreGraph Graph = GraphWith("TB", 50, 50, 0, 0);
        Graph.SetNode("C", new DagreNodeLabel { Width = 10, Height = 10 }, true);
        Graph.SetNode("A", new DagreNodeLabel { Width = 40, Height = 20 }, true);
        Graph.SetNode("B", new DagreNodeLabel { Width = 40, Height = 20 }, true);
        Graph.SetParent("A", "C");
        Graph.SetParent("B", "C");
        Graph.SetEdge("A", "B", new DagreEdgeLabel(), true);
        DagreLayoutEngine.Layout(Graph);
        AssertNear(Graph, "A", 55, 35);
        AssertNear(Graph, "B", 55, 105);
        AssertNear(Graph, "C", 55, 70);
        DagreNodeLabel? Cluster = Graph.Node("C");
        Assert.That(Cluster, Is.Not.Null);
        Assert.That(Cluster!.Width, Is.EqualTo(110).Within(0.001));
        Assert.That(Cluster.Height, Is.EqualTo(140).Within(0.001));
    }

    private static DagreGraph GraphWith(string RankDir, double NodeSep, double RankSep, double MarginX, double MarginY)
    {
        DagreGraph Graph = new DagreGraph(true, true, true);
        Graph.SetGraph(new DagreGraphLabel
        {
            RankDir = RankDir,
            NodeSep = NodeSep,
            RankSep = RankSep,
            MarginX = MarginX,
            MarginY = MarginY,
            EdgeSep = 20
        });
        return Graph;
    }

    private static void AssertNear(DagreGraph Graph, string NodeId, double X, double Y)
    {
        DagreNodeLabel? Node = Graph.Node(NodeId);
        Assert.That(Node, Is.Not.Null, NodeId);
        Assert.That(Node!.X, Is.EqualTo(X).Within(0.001), NodeId + " x was " + Node.X);
        Assert.That(Node.Y, Is.EqualTo(Y).Within(0.001), NodeId + " y was " + Node.Y);
    }

    private static void AssertPoints(DagreGraph Graph, string Source, string Target, params double[] Coordinates)
    {
        DagreEdgeLabel? Label = null;
        foreach (DagreEdge Edge in Graph.Edges())
        {
            if (Edge.V == Source && Edge.W == Target)
            {
                Label = Graph.Edge(Edge);
            }
        }

        Assert.That(Label, Is.Not.Null);
        Assert.That(Label!.Points.Count, Is.EqualTo(Coordinates.Length / 2));
        for (int Index = 0; Index < Label.Points.Count; Index++)
        {
            Assert.That(Label.Points[Index].X, Is.EqualTo(Coordinates[Index * 2]).Within(0.001));
            Assert.That(Label.Points[Index].Y, Is.EqualTo(Coordinates[(Index * 2) + 1]).Within(0.001));
        }
    }
}

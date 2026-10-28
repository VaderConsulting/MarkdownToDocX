using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class SparseRankCompactionTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void DisjointRanksReuseColumns_WhileSkippingEdgesKeepTheirSideCorridor(bool Horizontal, bool Connected)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Connected ? "flowchart TB\nn0 --> n6" : "flowchart TB\nn0");
        List<DiagramGridLayout.GridNode> Nodes = [.. Enumerable.Range(0, 8).Select(Index => new DiagramGridLayout.GridNode
        {
            Id = $"n{Index}", Rank = Index / 2,
            X = Horizontal ? Index / 2 * 150 : Index * 600,
            Y = Horizontal ? Index * 600 : Index / 2 * 150,
            Width = Horizontal ? 60 : 150, Height = Horizontal ? 150 : 60
        })];
        FlowchartSparseRankCompaction.Apply(Contract, Nodes, Horizontal);
        double Cross(int Index) => Horizontal ? Nodes[Index].Y : Nodes[Index].X;
        for (int Rank = 0; Rank < 4; Rank++)
        {
            Assert.That(Cross((Rank * 2) + 1) - Cross(Rank * 2), Is.EqualTo(210));
        }

        Assert.That(Cross(6) - Cross(0), Is.EqualTo(Connected ? 210 : 0));
        Assert.That(Nodes.Max(Node => Horizontal ? Node.Y : Node.X) - Cross(0), Is.LessThanOrEqualTo(420));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SharedColumnsRemainAligned(bool Horizontal)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\na --> b");
        List<DiagramGridLayout.GridNode> Nodes = [.. Enumerable.Range(0, 8).Select(Index => new DiagramGridLayout.GridNode
        { Id = $"n{Index}", Rank = Index, X = Horizontal ? Index * 150 : Index * 600,
            Y = Horizontal ? Index * 600 : Index * 150, Width = 60, Height = 60 })];
        Nodes.Add(new() { Id = "aligned", Rank = 9, X = Horizontal ? 1350 : 1200,
            Y = Horizontal ? 1200 : 1350, Width = 60, Height = 60 });
        FlowchartSparseRankCompaction.Apply(Contract, Nodes, Horizontal);
        Assert.That(Horizontal ? Nodes[2].Y : Nodes[2].X, Is.EqualTo(Horizontal ? Nodes[^1].Y : Nodes[^1].X));
    }

    [Test]
    public void LayeredViewStaysCompact_AndConnectorsClearEveryBodyIncludingTheirEnds()
    {
        string Source = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "EnterpriseFlowchartSamples", "layered-compaction.mmd"));
        EnterpriseDocument Document = EnterpriseValidator.Analyze(Source).Document;
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        Assert.That(Layout.Width, Is.LessThan(3500));
        List<FlowchartLayoutNodeBounds> Bounds = [.. Layout.Nodes.Values.Select(Node => new FlowchartLayoutNodeBounds { NodeId = Node.Id,
            CenterX = Node.AbsoluteX + (Node.Width / 2), CenterY = Node.AbsoluteY + (Node.Height / 2),
            Width = Node.Width, Height = Node.Height })];
        foreach (EnterpriseRoutedEdge Edge in EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes))
        {
            foreach (((double X, double Y) First, (double X, double Y) Second) Pair in Edge.SvgPoints.Zip(Edge.SvgPoints.Skip(1)))
            {
                foreach (FlowchartLayoutNodeBounds? Box in Bounds)
                {
                    Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(Pair.First.X, Pair.First.Y,
                Pair.Second.X, Pair.Second.Y, Box), Is.False,
                $"{Edge.Relationship.SourceId}->{Edge.Relationship.TargetId} must clear {Box.NodeId}.");
                }
            }
        }
    }
}

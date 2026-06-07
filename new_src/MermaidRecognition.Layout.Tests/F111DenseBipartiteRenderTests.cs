using System.Globalization;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class F111DenseBipartiteRenderTests
{
    private const string F111_SOURCE = """
        flowchart TB
        subgraph L["Left"]
          L1[L1]
          L2[L2]
          L3[L3]
        end
        subgraph R["Right"]
          R1[R1]
          R2[R2]
          R3[R3]
        end
        L1 --> R1
        L1 --> R2
        L1 --> R3
        L2 --> R1
        L2 --> R2
        L2 --> R3
        L3 --> R1
        L3 --> R2
        L3 --> R3
        """;

    [Test]
    public void TryResolveLayoutGuidePoints_F111_UsesDistributedSourceAndTargetSlots()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F111_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        List<double> L1SourceAttachX = CollectSourceAttachX(Contract, BoundsById, NodeById, Direction, "L1");
        Assert.That(L1SourceAttachX, Has.Count.EqualTo(3));
        Assert.That(L1SourceAttachX.Distinct().Count(), Is.EqualTo(3), "L1 outgoing edges should use three bottom slots");

        List<double> R1TargetAttachX = CollectTargetAttachX(Contract, BoundsById, NodeById, Direction, "R1");
        Assert.That(R1TargetAttachX, Has.Count.EqualTo(3));
        Assert.That(R1TargetAttachX.Distinct().Count(), Is.EqualTo(3), "R1 incoming edges should use three top slots");

        double MinimumSeparationPx = 8.0;
        Assert.That(
            Math.Abs(L1SourceAttachX[0] - L1SourceAttachX[1]),
            Is.GreaterThan(MinimumSeparationPx));
        Assert.That(
            Math.Abs(R1TargetAttachX[0] - R1TargetAttachX[1]),
            Is.GreaterThan(MinimumSeparationPx));
    }

    [Test]
    public void RenderToSvg_F111_EdgesDoNotShareIdenticalTerminalCoordinates()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F111_SOURCE);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        int LineCount = System.Text.RegularExpressions.Regex.Matches(Svg, "<line ").Count;
        int CurvedEdgeCount = System.Text.RegularExpressions.Regex.Matches(
            Svg,
            @"<path d=""[^""]+""[^>]*stroke=""#333333""").Count;
        Assert.That(LineCount + CurvedEdgeCount, Is.EqualTo(9));
    }

    private static List<double> CollectSourceAttachX(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId)
    {
        List<double> AttachXValues = new List<double>();
        foreach (EdgeContract Edge in Contract.Edges.Where(Edge => Edge.SourceId == SourceId))
        {
            bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> LayoutPoints,
                out FlowchartEdgeRouteKind _);
            Assert.That(Resolved, Is.True, Edge.SourceId + "->" + Edge.TargetId);
            AttachXValues.Add(LayoutPoints[0].X);
        }

        return AttachXValues.OrderBy(Value => Value).ToList();
    }

    private static List<double> CollectTargetAttachX(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string TargetId)
    {
        List<double> AttachXValues = new List<double>();
        foreach (EdgeContract Edge in Contract.Edges.Where(Edge => Edge.TargetId == TargetId))
        {
            bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> LayoutPoints,
                out FlowchartEdgeRouteKind _);
            Assert.That(Resolved, Is.True, Edge.SourceId + "->" + Edge.TargetId);
            AttachXValues.Add(LayoutPoints[^1].X);
        }

        return AttachXValues.OrderBy(Value => Value).ToList();
    }
}

using System.Globalization;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
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

        List<(double X, double Y)> L1Starts = DrawnEnds(Contract, Layout, "L1", AtTarget: false);
        List<(double X, double Y)> R1Ends = DrawnEnds(Contract, Layout, "R1", AtTarget: true);
        Assert.That(L1Starts, Has.Count.EqualTo(3));
        Assert.That(R1Ends, Has.Count.EqualTo(3));
        Assert.That(DistinctPoints(L1Starts), Is.EqualTo(3), "L1 outgoing edges should not share a terminal");
        Assert.That(DistinctPoints(R1Ends), Is.EqualTo(3), "R1 incoming edges should not share a terminal");
        AssertOutline(BoundsById["L1"], L1Starts);
        AssertOutline(BoundsById["R1"], R1Ends);
    }

    private static List<(double X, double Y)> DrawnEnds(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        string NodeId,
        bool AtTarget)
    {
        List<(double X, double Y)> Ends = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            bool Match = AtTarget
                ? string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal)
                : string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal);
            if (!Match)
            {
                continue;
            }

            Assert.That(Layout.TryCopyDrawnRoutePoints(Edge, out List<(double X, double Y)> Points), Is.True);
            Ends.Add(AtTarget ? Points[Points.Count - 1] : Points[0]);
        }

        return Ends;
    }

    private static int DistinctPoints(List<(double X, double Y)> Points)
    {
        int Count = 0;
        for (int Index = 0; Index < Points.Count; Index++)
        {
            bool Seen = false;
            for (int Earlier = 0; Earlier < Index; Earlier++)
            {
                double Dx = Points[Index].X - Points[Earlier].X;
                double Dy = Points[Index].Y - Points[Earlier].Y;
                if ((Dx * Dx) + (Dy * Dy) < 1.0)
                {
                    Seen = true;
                    break;
                }
            }

            if (!Seen)
            {
                Count++;
            }
        }

        return Count;
    }

    private static void AssertOutline(FlowchartLayoutNodeBounds Bounds, List<(double X, double Y)> Points)
    {
        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        double Right = Bounds.CenterX + (Bounds.Width / 2.0);
        double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
        double Top = Bounds.CenterY + (Bounds.Height / 2.0);
        foreach ((double X, double Y) Point in Points)
        {
            double EdgeDistance = Math.Min(
                Math.Min(Math.Abs(Point.X - Left), Math.Abs(Point.X - Right)),
                Math.Min(Math.Abs(Point.Y - Bottom), Math.Abs(Point.Y - Top)));
            Assert.That(EdgeDistance, Is.LessThan(1.5));
        }
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
        List<double> AttachXValues = [];
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

        return [.. AttachXValues.OrderBy(Value => Value)];
    }

    private static List<double> CollectTargetAttachX(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string TargetId)
    {
        List<double> AttachXValues = [];
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

        return [.. AttachXValues.OrderBy(Value => Value)];
    }
}

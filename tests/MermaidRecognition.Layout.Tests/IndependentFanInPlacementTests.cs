using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class IndependentFanInPlacementTests
{
    [TestCase("TB", false)]
    [TestCase("BT", false)]
    [TestCase("LR", true)]
    [TestCase("RL", true)]
    public void Layout_ThreeIndependentInputs_UseAdjacentColumnsAroundMerge(string Direction, bool Horizontal)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($$"""
            flowchart {{Direction}}
            First[First input] --> Merge{Merge?}
            Middle[Middle input] --> Merge
            Last[Last input] --> Merge
            Merge -->|yes| Continue[Continue]
            Merge -->|no| Stop[Stop]
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
        FlowchartLayoutNodeBounds Target = Layout.NodeBounds.Single(Node => Node.NodeId == "Merge");
        FlowchartLayoutNodeBounds[] Parents = [.. Layout.NodeBounds.Where(Node => Node.NodeId is "First" or "Middle" or "Last").OrderBy(Cross)];
        double Pitch = DiagramGridMetrics.CeilToGrid((Horizontal ? Parents[0].Height : Parents[0].Width) + DiagramGridMetrics.NODE_GAP_PX);
        Assert.That(Cross(Parents[1]), Is.EqualTo(Cross(Target)).Within(0.5));
        Assert.That(Cross(Parents[1]) - Cross(Parents[0]), Is.EqualTo(Pitch).Within(0.5));
        Assert.That(Cross(Parents[2]) - Cross(Parents[1]), Is.EqualTo(Pitch).Within(0.5));
    }

    [TestCase("TB")]
    [TestCase("BT")]
    public void Layout_MergeWithTwoOutcomes_UsesDistinctInboundAndOutboundPorts(string Direction)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($$"""
            flowchart {{Direction}}
            First --> Merge{Merge?}
            Middle --> Merge
            Last --> Merge
            Merge -->|yes| Continue
            Merge -->|no| Stop
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutNodeBounds Diamond = Layout.NodeBounds.Single(Node => Node.NodeId == "Merge");
        FlowchartLayoutEdgeRoute[] Inbound = [.. Layout.EdgeRoutes.Where(Edge => Edge.TargetId == "Merge")];
        FlowchartLayoutEdgeRoute[] Outbound = [.. Layout.EdgeRoutes.Where(Edge => Edge.SourceId == "Merge")];
        FlowchartLayoutPoint[] Ports = [.. Inbound.Select(Edge => Edge.Points[^1]), .. Outbound.Select(Edge => Edge.Points[0])];
        Assert.That(Ports.Select(Point => (Point.X, Point.Y)).Distinct().Count(), Is.EqualTo(5));
        Assert.That(Inbound.Count(Edge => Math.Abs(Edge.Points[^1].X - Diamond.CenterX) < 0.5), Is.EqualTo(1));
        Assert.That(Inbound.Count(Edge => Math.Abs(Edge.Points[^1].Y - Diamond.CenterY) < 0.5), Is.EqualTo(2));
        foreach (FlowchartLayoutEdgeRoute? Edge in Inbound.Concat(Outbound))
        {
            for (int Index = 2; Index < Edge.Points.Count - 1; Index++)
            {
                FlowchartLayoutPoint A = Edge.Points[Index - 1];
                FlowchartLayoutPoint B = Edge.Points[Index];
                Assert.That(Math.Abs(A.X - B.X) < 0.5 || Math.Abs(A.Y - B.Y) < 0.5, Is.True);
            }
        }
        foreach (FlowchartLayoutEdgeRoute? Edge in Outbound)
        {
            FlowchartLayoutPoint Start = Edge.Points[0];
            FlowchartLayoutPoint Next = Edge.Points[1];
            Assert.That(FlowchartDiamondAttachHelper.TryResolveOutwardNormal(Diamond, Start.X, Start.Y, out double Nx, out double Ny), Is.True);
            double Dx = Next.X - Start.X;
            double Dy = Next.Y - Start.Y;
            double Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
            Assert.That(((Dx * Nx) + (Dy * Ny)) / Length, Is.EqualTo(1).Within(0.0001), "The terminal is square to the sloping edge.");
            Assert.That(Math.Abs(Start.X - Diamond.CenterX), Is.EqualTo(Diamond.Width / 4).Within(0.5));
            Assert.That(Math.Abs(Start.Y - Diamond.CenterY), Is.EqualTo(Diamond.Height / 4).Within(0.5));
            Assert.That((Start.Y - Diamond.CenterY) * (Direction == "TB" ? -1 : 1), Is.GreaterThan(0));
        }
    }
}
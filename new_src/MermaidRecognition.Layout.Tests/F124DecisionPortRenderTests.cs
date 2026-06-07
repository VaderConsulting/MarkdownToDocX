using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class F124DecisionPortRenderTests
{
    private const string F124_SOURCE = """
        flowchart LR
        A[Receive]
        B{OK?}
        C[Done]
        D[Retry]
        A --> B
        B -->|Yes| C
        B -->|No| D
        D --> A
        """;

    [Test]
    public void TryResolveLayoutGuidePoints_F124_TwoBranchesUseSlotsOneAndThree()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F124_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartLayoutNodeBounds HubBounds = BoundsById["B"];
        double HalfWidth = HubBounds.Width / 2.0;
        double HalfHeight = HubBounds.Height / 2.0;

        (double TopRightSlopeX, double TopRightSlopeY) = (
            HubBounds.CenterX + (HalfWidth / 2.0),
            HubBounds.CenterY + (HalfHeight / 2.0));
        (double RightVertexX, double RightVertexY) = (
            HubBounds.CenterX + HalfWidth,
            HubBounds.CenterY);
        (double LowerRightSlopeX, double LowerRightSlopeY) = (
            HubBounds.CenterX + (HalfWidth / 2.0),
            HubBounds.CenterY - (HalfHeight / 2.0));

        (double YesAttachX, double YesAttachY) = ResolveDecisionSourceAttach(
            Contract,
            BoundsById,
            NodeById,
            Direction,
            "B",
            "C");
        (double NoAttachX, double NoAttachY) = ResolveDecisionSourceAttach(
            Contract,
            BoundsById,
            NodeById,
            Direction,
            "B",
            "D");

        Assert.That(YesAttachX, Is.EqualTo(TopRightSlopeX).Within(1.0), "Yes should exit slot 1 (upper-right slope)");
        Assert.That(YesAttachY, Is.EqualTo(TopRightSlopeY).Within(1.0), "Yes should exit slot 1 (upper-right slope)");
        Assert.That(NoAttachX, Is.EqualTo(LowerRightSlopeX).Within(1.0), "No should exit slot 3 (lower-right slope)");
        Assert.That(NoAttachY, Is.EqualTo(LowerRightSlopeY).Within(1.0), "No should exit slot 3 (lower-right slope)");
        Assert.That(Math.Abs(NoAttachY - RightVertexY), Is.GreaterThan(4.0), "No must not use slot 2 (right vertex)");
        Assert.That(Math.Abs(NoAttachX - RightVertexX), Is.GreaterThan(4.0), "No must not use slot 2 (right vertex)");
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F30_ThreeBranchesUseSlotsOneTwoAndThree()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A[Christmas] -->|Get money| B(Go shopping)
            B --> C{Let me think}
            C -->|One| D[Laptop]
            C -->|Two| E[iPhone]
            C -->|Three| F[Other gift]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartLayoutNodeBounds HubBounds = BoundsById["C"];
        double HalfWidth = HubBounds.Width / 2.0;
        double HalfHeight = HubBounds.Height / 2.0;

        (double LeftMidX, double LeftMidY) = (
            HubBounds.CenterX - (HalfWidth / 2.0),
            HubBounds.CenterY - (HalfHeight / 2.0));
        (double BottomVertexX, double BottomVertexY) = (
            HubBounds.CenterX,
            HubBounds.CenterY - HalfHeight);
        (double RightMidX, double RightMidY) = (
            HubBounds.CenterX + (HalfWidth / 2.0),
            HubBounds.CenterY - (HalfHeight / 2.0));

        (double OneAttachX, double OneAttachY) = ResolveDecisionSourceAttach(
            Contract, BoundsById, NodeById, Direction, "C", "D");
        (double TwoAttachX, double TwoAttachY) = ResolveDecisionSourceAttach(
            Contract, BoundsById, NodeById, Direction, "C", "E");
        (double ThreeAttachX, double ThreeAttachY) = ResolveDecisionSourceAttach(
            Contract, BoundsById, NodeById, Direction, "C", "F");

        Assert.That(OneAttachX, Is.EqualTo(LeftMidX).Within(1.0));
        Assert.That(OneAttachY, Is.EqualTo(LeftMidY).Within(1.0));
        Assert.That(TwoAttachX, Is.EqualTo(BottomVertexX).Within(1.0));
        Assert.That(TwoAttachY, Is.EqualTo(BottomVertexY).Within(1.0));
        Assert.That(ThreeAttachX, Is.EqualTo(RightMidX).Within(1.0));
        Assert.That(ThreeAttachY, Is.EqualTo(RightMidY).Within(1.0));
    }

    private static (double X, double Y) ResolveDecisionSourceAttach(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId,
        string TargetId)
    {
        EdgeContract Edge = Contract.Edges.First(
            Candidate => Candidate.SourceId == SourceId && Candidate.TargetId == TargetId);

        bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Edge,
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> LayoutPoints,
            out FlowchartEdgeRouteKind _);

        Assert.That(Resolved, Is.True, SourceId + "->" + TargetId + " should resolve");
        return LayoutPoints[0];
    }
}

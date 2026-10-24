using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Dave-approved F53 layout at commit 964a3a56. Later engine changes must preserve this geometry.
/// </summary>
[TestFixture]
public sealed class F53ProtectedLayoutReferenceTests
{
    private const string F53_SOURCE = """
        flowchart TD
        Start([Start]) --> Process[Process]
        Process --> Decision{Valid?}
        Decision -->|Yes| Success[Success]
        Decision -->|No| Error[Error]
        Success --> End([End])
        Error --> End

        style Start fill:#e1f5e1
        style End fill:#e1f5e1
        style Error fill:#f8d7da
        style Success fill:#d1ecf1
        """;

    /// <summary>Node centres from <c>964a3a56</c> with cell regions enabled.</summary>
    private static readonly Dictionary<string, (double CenterX, double CenterY)> ApprovedCenters964A3A56 =
        new Dictionary<string, (double CenterX, double CenterY)>(StringComparer.Ordinal)
        {
            ["Decision"] = (405.0, 570.0),
            ["End"] = (405.0, 195.0),
            ["Error"] = (735.0, 345.0),
            ["Process"] = (405.0, 945.0),
            ["Start"] = (405.0, 1245.0),
            ["Success"] = (405.0, 345.0),
        };

    [Test]
    public void Layout_Matches964a3a56ApprovedNodeCentres()
    {
        FlowchartLayoutResult Layout = LayoutF53();
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);

        foreach (KeyValuePair<string, (double CenterX, double CenterY)> Expected in ApprovedCenters964A3A56)
        {
            FlowchartLayoutNodeBounds Bounds = BoundsById[Expected.Key];
            Assert.That(Bounds.CenterX, Is.EqualTo(Expected.Value.CenterX).Within(1.0), Expected.Key);
            Assert.That(Bounds.CenterY, Is.EqualTo(Expected.Value.CenterY).Within(1.0), Expected.Key);
        }
    }

    [Test]
    public void Layout_StartAtTop_YesBranchDown_NoExitsEast()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F53_SOURCE);
        FlowchartLayoutResult Layout = LayoutF53();
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);

        FlowchartLayoutNodeBounds StartBounds = BoundsById["Start"];
        FlowchartLayoutNodeBounds ProcessBounds = BoundsById["Process"];
        FlowchartLayoutNodeBounds DecisionBounds = BoundsById["Decision"];
        FlowchartLayoutNodeBounds SuccessBounds = BoundsById["Success"];
        FlowchartLayoutNodeBounds ErrorBounds = BoundsById["Error"];
        FlowchartLayoutNodeBounds EndBounds = BoundsById["End"];

        Assert.That(StartBounds.CenterY, Is.GreaterThan(ProcessBounds.CenterY), "Start at top");
        Assert.That(ProcessBounds.CenterY, Is.GreaterThan(DecisionBounds.CenterY));
        Assert.That(DecisionBounds.CenterY, Is.GreaterThan(SuccessBounds.CenterY), "Yes branch runs downward");
        Assert.That(Math.Abs(SuccessBounds.CenterX - DecisionBounds.CenterX), Is.LessThan(1.0), "Yes stays on main column");
        Assert.That(ErrorBounds.CenterX, Is.GreaterThan(DecisionBounds.CenterX), "No branch sits to the east");
        Assert.That(EndBounds.CenterY, Is.LessThan(SuccessBounds.CenterY));

        double HalfWidth = DecisionBounds.Width / 2.0;
        double HalfHeight = DecisionBounds.Height / 2.0;
        (double YesVertexX, double YesVertexY) = (
            DecisionBounds.CenterX,
            DecisionBounds.CenterY - HalfHeight);
        (double NoVertexX, double NoVertexY) = (
            DecisionBounds.CenterX + HalfWidth,
            DecisionBounds.CenterY);

        (double YesAttachX, double YesAttachY) = ResolveDecisionSourceAttach(
            Contract, Layout, BoundsById, NodeById, Direction, "Decision", "Success");
        (double NoAttachX, double NoAttachY) = ResolveDecisionSourceAttach(
            Contract, Layout, BoundsById, NodeById, Direction, "Decision", "Error");

        Assert.That(YesAttachX, Is.EqualTo(YesVertexX).Within(1.0), "Yes leaves at S");
        Assert.That(YesAttachY, Is.EqualTo(YesVertexY).Within(1.0));
        Assert.That(NoAttachX, Is.EqualTo(NoVertexX).Within(1.0), "No leaves at E");
        Assert.That(NoAttachY, Is.EqualTo(NoVertexY).Within(1.0));
    }

    private static FlowchartLayoutResult LayoutF53()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F53_SOURCE);
        Assert.That(
            FlowchartSugiyamaLayoutEngine.TryLayout(
                Contract,
                new FlowchartRenderContext { UseCellRegions = true },
                out FlowchartLayoutResult Layout),
            Is.True);
        return Layout;
    }

    private static (double X, double Y) ResolveDecisionSourceAttach(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
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
            out FlowchartEdgeRouteKind _,
            Layout);

        Assert.That(Resolved, Is.True, SourceId + "->" + TargetId + " should resolve");
        return LayoutPoints[0];
    }
}

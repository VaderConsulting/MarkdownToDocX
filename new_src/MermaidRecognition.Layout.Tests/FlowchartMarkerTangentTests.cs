using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartMarkerTangentTests
{
    private const string F35_SOURCE = """
        flowchart TB
        A[Path A] --> D{Merge?}
        B[Path B] --> D
        C[Path C] --> D
        D -->|yes| E[Continue]
        D -->|no| F[Stop]
        """;

    [Test]
    public void ResolveDecisionBranchMarkerApproachPoint_FivePointRoute_UsesTerminalStubNotCorridorGuide()
    {
        List<(double X, double Y)> Points = new List<(double X, double Y)>
        {
            (100.0, 200.0),
            (100.0, 180.0),
            (160.0, 140.0),
            (150.0, 120.0),
            (140.0, 100.0)
        };

        (double TargetApproachX, double TargetApproachY) =
            FlowchartCurvePathBuilder.ResolveMarkerApproachPoint(
                Points,
                FlowchartRenderedPathKind.DecisionBranch,
                AtTargetEnd: true);
        (double SourceApproachX, double SourceApproachY) =
            FlowchartCurvePathBuilder.ResolveMarkerApproachPoint(
                Points,
                FlowchartRenderedPathKind.DecisionBranch,
                AtTargetEnd: false);

        Assert.That(TargetApproachX, Is.EqualTo(150.0).Within(0.5));
        Assert.That(TargetApproachY, Is.EqualTo(120.0).Within(0.5));
        Assert.That(SourceApproachX, Is.EqualTo(100.0).Within(0.5));
        Assert.That(SourceApproachY, Is.EqualTo(180.0).Within(0.5));
        Assert.That(TargetApproachX, Is.Not.EqualTo(160.0).Within(0.5));
    }

    [Test]
    public void ValidateEndpointMarkerTangent_F35_AllVisibleEdges_AlignWithRenderedPath()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F35_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        List<string> Failures = new List<string>();
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> LayoutPoints,
                out FlowchartEdgeRouteKind RouteKind);
            if (!Resolved)
            {
                Failures.Add(Edge.SourceId + "->" + Edge.TargetId + ": route unresolved");
                continue;
            }

            if (Edge.MarkerEnd == EdgeEndpointMarker.Point
                && !FlowchartMarkerTangentValidator.TryValidateEndpointMarkerTangent(
                    LayoutPoints,
                    RouteKind,
                    Direction,
                    AtTargetEnd: true,
                    out string TargetFailure))
            {
                Failures.Add(Edge.SourceId + "->" + Edge.TargetId + " target: " + TargetFailure);
            }

            if (Edge.MarkerStart == EdgeEndpointMarker.Point
                && !FlowchartMarkerTangentValidator.TryValidateEndpointMarkerTangent(
                    LayoutPoints,
                    RouteKind,
                    Direction,
                    AtTargetEnd: false,
                    out string SourceFailure))
            {
                Failures.Add(Edge.SourceId + "->" + Edge.TargetId + " source: " + SourceFailure);
            }
        }

        Assert.That(Failures, Is.Empty, string.Join("\n", Failures));
    }

    [TestCaseSource(nameof(CatalogueFixtureMermaidBlocks))]
    public void ValidateEndpointMarkerTangent_CatalogueBatch_AlignWithRenderedPath(string FixtureId, string Mermaid)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Mermaid);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True, FixtureId);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        List<string> Failures = new List<string>();
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> LayoutPoints,
                out FlowchartEdgeRouteKind RouteKind);
            if (!Resolved)
            {
                continue;
            }

            if (Edge.MarkerEnd == EdgeEndpointMarker.Point
                && !FlowchartMarkerTangentValidator.TryValidateEndpointMarkerTangent(
                    LayoutPoints,
                    RouteKind,
                    Direction,
                    AtTargetEnd: true,
                    out string TargetFailure))
            {
                Failures.Add(Edge.SourceId + "->" + Edge.TargetId + " target: " + TargetFailure);
            }

            if (Edge.MarkerStart == EdgeEndpointMarker.Point
                && !FlowchartMarkerTangentValidator.TryValidateEndpointMarkerTangent(
                    LayoutPoints,
                    RouteKind,
                    Direction,
                    AtTargetEnd: false,
                    out string SourceFailure))
            {
                Failures.Add(Edge.SourceId + "->" + Edge.TargetId + " source: " + SourceFailure);
            }
        }

        Assert.That(Failures, Is.Empty, FixtureId + " marker tangent failures:\n" + string.Join("\n", Failures));
    }

    private static IEnumerable<TestCaseData> CatalogueFixtureMermaidBlocks()
    {
        yield return new TestCaseData("F11", """
            flowchart LR
            A[Rectangle] --> B(Rounded rectangle) --> C{Diamond decision} --> D((Circle))
            """).SetName("Catalogue_F11");

        yield return new TestCaseData("F29", """
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            """).SetName("Catalogue_F29");

        yield return new TestCaseData("F30", """
            flowchart TD
            A[Christmas] -->|Get money| B(Go shopping)
            B --> C{Let me think}
            C -->|One| D[Laptop]
            C -->|Two| E[iPhone]
            C -->|Three| F[Other gift]
            """).SetName("Catalogue_F30");

        yield return new TestCaseData("F31", """
            flowchart TB
            Hub[Hub] --> A[Branch A]
            Hub --> B[Branch B]
            Hub --> C[Branch C]
            """).SetName("Catalogue_F31");

        yield return new TestCaseData("F32", """
            flowchart TB
            W1[Worker 1] --> Agg[Aggregate]
            W2[Worker 2] --> Agg
            W3[Worker 3] --> Agg
            """).SetName("Catalogue_F32");

        yield return new TestCaseData("F33", """
            flowchart LR
            A[Ingress] --> B[Enqueue]
            B --> C[Worker 1]
            B --> D[Worker 2]
            C --> E[Aggregate]
            D --> E
            E --> F[Out]
            """).SetName("Catalogue_F33");

        yield return new TestCaseData("F34", """
            flowchart LR
            A[Receive] --> B{OK?}
            B -->|Yes| C[Done]
            B -->|No| D[Retry]
            D --> A
            """).SetName("Catalogue_F34");

        yield return new TestCaseData("F35", F35_SOURCE).SetName("Catalogue_F35");

        yield return new TestCaseData("F125", """
            flowchart TB
            PA[Path A]
            PB[Path B]
            PC[Path C]
            D{Merge?}
            E[Continue]
            F[Stop]
            PA --> D
            PB --> D
            PC --> D
            D -->|yes| E
            D -->|no| F
            """).SetName("Catalogue_F125");
    }
}

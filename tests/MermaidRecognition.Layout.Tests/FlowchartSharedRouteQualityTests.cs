using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Shared-router quality: obstacles, stacked ports, labels, hooks, and diagram bounds.
/// </summary>
[TestFixture]
public sealed class FlowchartSharedRouteQualityTests
{
    private const string SAMPLE_03_SOURCE = """
        flowchart TB
          subgraph dc["Primary data centre"]
            subgraph appNode["App Server Node"]
              jvm["Java VM"]
              war["shop.war"]
            end
            pg["PostgreSQL"]
            lb["Load Balancer"]
            lan["DC LAN"]
          end
          subgraph webShop["Web Shop"]
            checkout(["Checkout"])
          end
          shopUi["Shop Web UI"]
          ordering(["Online Ordering"])
          order["Order"]
          hosting(["Java Application Hosting"])
          dbSvc(["Database Service"])
          orderTbl["orders table"]
          webShop --> shopUi
          checkout --> ordering
          shopUi --> ordering
          checkout --> order
          war --> webShop
          orderTbl --> order
          jvm --> hosting
          pg --> dbSvc
          hosting --> webShop
          dbSvc --> webShop
          pg --> orderTbl
          lb -->|HTTPS| appNode
          lan --> lb
          lan --> appNode
        """;

    private const double LABEL_EDGE_TOLERANCE_PX = 28.0;

    [Test]
    public void TryResolve_Sample03_NoEdgePiercesNonEndpointNode()
    {
        ResolveAll(out DiagramContract Contract, out Dictionary<string, FlowchartLayoutNodeBounds> BoundsById, out List<ResolvedEdge> Edges);

        foreach (ResolvedEdge Resolved in Edges)
        {
            Assert.That(
                FlowchartSharedRouteQualityHelper.PolylineIntersectsNonEndpointNodes(
                    Resolved.Points,
                    Contract,
                    BoundsById,
                    Resolved.Edge.SourceId,
                    Resolved.Edge.TargetId),
                Is.False,
                Resolved.Edge.SourceId + " -> " + Resolved.Edge.TargetId + " intersects a non-endpoint node");
        }
    }

    [Test]
    public void TryResolve_Sample03_ArrowheadsDoNotStack()
    {
        ResolveAll(out _, out _, out List<ResolvedEdge> Edges);

        for (int LeftIndex = 0; LeftIndex < Edges.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Edges.Count; RightIndex++)
            {
                ResolvedEdge Left = Edges[LeftIndex];
                ResolvedEdge Right = Edges[RightIndex];
                bool ShareTarget = string.Equals(Left.Edge.TargetId, Right.Edge.TargetId, StringComparison.Ordinal);
                bool ShareSource = string.Equals(Left.Edge.SourceId, Right.Edge.SourceId, StringComparison.Ordinal);
                if (ShareTarget)
                {
                    Assert.That(
                        FlowchartSharedRouteQualityHelper.AttachPointsStack(Left.Points[^1], Right.Points[^1]),
                        Is.False,
                        Left.Edge.SourceId + " and " + Right.Edge.SourceId + " stack on " + Left.Edge.TargetId);
                }

                if (ShareSource)
                {
                    Assert.That(
                        FlowchartSharedRouteQualityHelper.AttachPointsStack(Left.Points[0], Right.Points[0]),
                        Is.False,
                        Left.Edge.TargetId + " and " + Right.Edge.TargetId + " stack on " + Left.Edge.SourceId);
                }
            }
        }
    }

    [Test]
    public void TryResolve_Sample03_HttpsLabelStaysOnOwnEdge()
    {
        ResolveAll(
            out DiagramContract Contract,
            out Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
            out List<ResolvedEdge> Edges);
        ResolvedEdge Https = Edges.Single(
            Candidate => string.Equals(Candidate.Edge.SourceId, "lb", StringComparison.Ordinal)
                && string.Equals(Candidate.Edge.TargetId, "appNode", StringComparison.Ordinal));

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartLabelDimensions LabelDimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
            "HTTPS",
            Options,
            Options.EdgeLabelFontSizePx);
        (double LabelX, double LabelY) = FlowchartSugiyamaSvgRenderer.ResolveDrawnEdgeLabelCentre(
            Https.Edge,
            Contract,
            BoundsById,
            BoundsById["lb"],
            BoundsById["appNode"],
            "rect",
            Https.Points,
            FlowchartDirection.TopToBottom,
            null,
            Options);

        double Distance = FlowchartSharedRouteQualityHelper.DistanceFromPointToPolyline(
            LabelX,
            LabelY,
            Https.Points);
        Assert.That(Distance, Is.LessThanOrEqualTo(LABEL_EDGE_TOLERANCE_PX), "HTTPS must sit beside lb -> appNode");

        FlowchartLayoutNodeBounds PostgreSql = BoundsById["pg"];
        double PgLeft = PostgreSql.CenterX - (PostgreSql.Width / 2.0);
        double PgRight = PostgreSql.CenterX + (PostgreSql.Width / 2.0);
        double PgBottom = PostgreSql.CenterY - (PostgreSql.Height / 2.0);
        double PgTop = PostgreSql.CenterY + (PostgreSql.Height / 2.0);
        bool OnPostgreSql = LabelX >= PgLeft
            && LabelX <= PgRight
            && LabelY >= PgBottom
            && LabelY <= PgTop;
        Assert.That(OnPostgreSql, Is.False, "HTTPS must not sit on PostgreSQL");
        _ = LabelDimensions;
    }

    [Test]
    public void TryResolve_Sample03_NoHookedOrDisconnectedRoutes()
    {
        ResolveAll(out _, out _, out List<ResolvedEdge> Edges);
        foreach (ResolvedEdge Resolved in Edges)
        {
            Assert.That(
                FlowchartSharedRouteQualityHelper.HasDisconnectedOrHookedSegments(Resolved.Points),
                Is.False,
                Resolved.Edge.SourceId + " -> " + Resolved.Edge.TargetId + " is hooked or disconnected");
        }
    }

    [Test]
    public void TryResolve_Sample03_RoutesStayInsideDiagramBounds()
    {
        ResolveAll(out DiagramContract Contract, out Dictionary<string, FlowchartLayoutNodeBounds> BoundsById, out List<ResolvedEdge> Edges);
        foreach (ResolvedEdge Resolved in Edges)
        {
            Assert.That(
                FlowchartSharedRouteQualityHelper.PolylineLeavesDiagramBounds(
                    Resolved.Points,
                    Contract,
                    BoundsById),
                Is.False,
                Resolved.Edge.SourceId + " -> " + Resolved.Edge.TargetId + " leaves the diagram bounds");
        }
    }

    [Test]
    public void TryResolve_Sample03_NamedProblemEdgesStayClear()
    {
        ResolveAll(out DiagramContract Contract, out Dictionary<string, FlowchartLayoutNodeBounds> BoundsById, out List<ResolvedEdge> Edges);

        AssertNamedEdgeClear(Edges, Contract, BoundsById, "dbSvc", "webShop", "order");
        AssertNamedEdgeClear(Edges, Contract, BoundsById, "lan", "appNode", "pg");
        AssertNamedEdgeClear(Edges, Contract, BoundsById, "hosting", "webShop", "pg");
        AssertNamedEdgeClear(Edges, Contract, BoundsById, "shopUi", "ordering", "order");
        AssertNamedEdgeClear(Edges, Contract, BoundsById, "orderTbl", "order", "ordering");
    }

    private static void AssertNamedEdgeClear(
        List<ResolvedEdge> Edges,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string SourceId,
        string TargetId,
        string ObstacleId)
    {
        ResolvedEdge Resolved = Edges.Single(
            Candidate => string.Equals(Candidate.Edge.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Candidate.Edge.TargetId, TargetId, StringComparison.Ordinal));
        Assert.That(
            FlowchartSharedRouteQualityHelper.PolylineIntersectsNonEndpointNodes(
                Resolved.Points,
                Contract,
                BoundsById,
                SourceId,
                TargetId),
            Is.False,
            SourceId + " -> " + TargetId);
        _ = ObstacleId;
    }

    private static void ResolveAll(
        out DiagramContract Contract,
        out Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        out List<ResolvedEdge> Edges)
    {
        Contract = MermaidRecognizer.Parse(SAMPLE_03_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        BoundsById = Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, StringComparer.Ordinal);
        FlowchartCompoundEndpointHelper.AddGroupBounds(Contract, BoundsById);
        Dictionary<string, NodeContract> NodeById =
            Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        Edges = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Assert.That(
                FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    FlowchartDirection.TopToBottom,
                    out List<(double X, double Y)> Points,
                    out FlowchartEdgeRouteKind _),
                Is.True,
                Edge.SourceId + " -> " + Edge.TargetId);
            Edges.Add(new ResolvedEdge(Edge, Points));
        }
    }

    private sealed class ResolvedEdge
    {
        public ResolvedEdge(EdgeContract Edge, List<(double X, double Y)> Points)
        {
            this.Edge = Edge;
            this.Points = Points;
        }

        public EdgeContract Edge { get; }

        public List<(double X, double Y)> Points { get; }
    }
}

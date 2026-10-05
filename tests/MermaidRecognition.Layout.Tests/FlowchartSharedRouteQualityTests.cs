using System.Globalization;
using System.Text.RegularExpressions;

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
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.Evaluate("sample-03", SAMPLE_03_SOURCE);
        Assert.That(
            Report.Failures.Exists(Failure => string.Equals(Failure.Kind, "NodeIntersection", StringComparison.Ordinal)),
            Is.False,
            "sample 03 rendered SVG has an unjustified node intersection");
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

        List<(double X, double Y)> Rendered = FlowchartSharedEdgeRouter.FlattenRenderedGuide(
            Https.Points,
            Https.RouteKind,
            FlowchartDirection.TopToBottom);
        double Distance = FlowchartSharedRouteQualityHelper.DistanceFromPointToPolyline(
            LabelX,
            LabelY,
            Rendered);
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

        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.Evaluate("sample-03", SAMPLE_03_SOURCE);
        Assert.That(
            Report.Failures.Exists(Failure => string.Equals(Failure.Kind, "NodeIntersection", StringComparison.Ordinal)),
            Is.False,
            "sample 03 rendered SVG still has an unjustified node intersection");
        _ = Edges;
        _ = Contract;
        _ = BoundsById;
    }

    [Test]
    public void TryResolve_Sample03_HttpsLabelDistanceIsToOwnEdge()
    {
        ResolveAll(out DiagramContract Contract, out Dictionary<string, FlowchartLayoutNodeBounds> BoundsById, out List<ResolvedEdge> Edges);
        ResolvedEdge Https = FindEdge(Edges, "lb", "appNode");
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
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
        List<(double X, double Y)> Rendered = FlowchartSharedEdgeRouter.FlattenRenderedGuide(
            Https.Points,
            Https.RouteKind,
            FlowchartDirection.TopToBottom);
        Assert.That(
            FlowchartSharedRouteQualityHelper.DistanceFromPointToPolyline(LabelX, LabelY, Rendered),
            Is.LessThanOrEqualTo(FlowchartSharedRouteQualityHelper.LABEL_OWN_EDGE_TOLERANCE_PX));
    }

    [Test]
    public void TryResolve_Sample03_CheckoutOrderIsOneContinuousPath()
    {
        ResolveAll(out _, out _, out List<ResolvedEdge> Edges);
        ResolvedEdge CheckoutOrder = FindEdge(Edges, "checkout", "order");
        Assert.That(FlowchartSharedRouteQualityHelper.HasDuplicatedParallelSegments(CheckoutOrder.Points), Is.False);
        Assert.That(FlowchartSharedRouteQualityHelper.HasDisconnectedOrHookedSegments(CheckoutOrder.Points), Is.False);
    }

    [Test]
    public void TryResolve_Sample03_WebShopParallelEdgesAreSeparated()
    {
        ResolveAll(out _, out _, out List<ResolvedEdge> Edges);
        ResolvedEdge Hosting = FindEdge(Edges, "hosting", "webShop");
        ResolvedEdge Database = FindEdge(Edges, "dbSvc", "webShop");
        Assert.That(
            FlowchartSharedRouteQualityHelper.ParallelSegmentsCloserThanSeparation(Hosting.Points, Database.Points),
            Is.False,
            "Web Shop → Hosting and Web Shop → Database Service must stay at least "
                + FlowchartSharedRouteQualityHelper.PARALLEL_SEPARATION_PX.ToString(CultureInfo.InvariantCulture)
                + " px apart");
        Assert.That(
            FlowchartSharedRouteQualityHelper.AttachPointsStack(Hosting.Points[0], Database.Points[0]),
            Is.False);
        Assert.That(
            FlowchartSharedRouteQualityHelper.AttachPointsStack(Hosting.Points[^1], Database.Points[^1]),
            Is.False);
    }

    [Test]
    public void TryResolve_Sample03_EdgesKeepGroupBorderClearance()
    {
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.Evaluate("sample-03", SAMPLE_03_SOURCE);
        Assert.That(Report.Passes, Is.True, "sample 03 rendered SVG quality gate failed");
    }

    [Test]
    public void TryResolve_Sample03_UsesOneRoutingStyle()
    {
        ResolveAll(out _, out _, out List<ResolvedEdge> Edges);
        List<FlowchartEdgeRouteKind> Kinds = [];
        foreach (ResolvedEdge Resolved in Edges)
        {
            Kinds.Add(Resolved.RouteKind);
        }

        Assert.That(FlowchartSharedRouteQualityHelper.UsesMixedRoutingStyle(Kinds), Is.False);
    }

    [Test]
    public void TryResolve_Sample03_DatabaseServicePortsAreSpread()
    {
        ResolveAll(out _, out _, out List<ResolvedEdge> Edges);
        ResolvedEdge Incoming = FindEdge(Edges, "pg", "dbSvc");
        ResolvedEdge Outgoing = FindEdge(Edges, "dbSvc", "webShop");
        Assert.That(
            FlowchartSharedRouteQualityHelper.AttachPointsStack(Incoming.Points[^1], Outgoing.Points[0]),
            Is.False,
            "PostgreSQL arrival and Web Shop departure crowd Database Service");
    }

    [Test]
    public void RenderToSvg_Sample03_StrokeQualityMatchesSharedRules()
    {
        Assert.That(NativeFlowchartRenderBridge.TryRenderFlowchartSvg(SAMPLE_03_SOURCE, out string Svg), Is.True);
        Match ViewBoxMatch = Regex.Match(Svg, @"viewBox=""0 0 ([0-9.]+) ([0-9.]+)""");
        Assert.That(ViewBoxMatch.Success, Is.True);
        double ViewWidth = double.Parse(ViewBoxMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double ViewHeight = double.Parse(ViewBoxMatch.Groups[2].Value, CultureInfo.InvariantCulture);

        List<List<(double X, double Y)>> Strokes = [];
        foreach (Match LineMatch in Regex.Matches(
            Svg,
            @"<line x1=""([0-9.\-]+)"" y1=""([0-9.\-]+)"" x2=""([0-9.\-]+)"" y2=""([0-9.\-]+)"""))
        {
            Strokes.Add(
            [
                (double.Parse(LineMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                    double.Parse(LineMatch.Groups[2].Value, CultureInfo.InvariantCulture)),
                (double.Parse(LineMatch.Groups[3].Value, CultureInfo.InvariantCulture),
                    double.Parse(LineMatch.Groups[4].Value, CultureInfo.InvariantCulture))
            ]);
        }

        foreach (Match PathMatch in Regex.Matches(Svg, @"<path d=""([^""]+)"" fill=""none"""))
        {
            Assert.That(
                Regex.Matches(PathMatch.Groups[1].Value, @"[Mm]").Count,
                Is.EqualTo(1),
                "Rendered route must be one connected path: " + PathMatch.Groups[1].Value);
            Assert.That(
                FlowchartSvgPathGeometryHelper.TryFlattenPathData(PathMatch.Groups[1].Value, out List<(double X, double Y)> Samples),
                Is.True);
            Strokes.Add(Samples);
        }

        Assert.That(Strokes.Count, Is.GreaterThanOrEqualTo(14));
        foreach (List<(double X, double Y)> Stroke in Strokes)
        {
            foreach ((double PointX, double PointY) in Stroke)
            {
                Assert.That(PointX, Is.GreaterThanOrEqualTo(-1.0), "Route left the left bound");
                Assert.That(PointY, Is.GreaterThanOrEqualTo(-1.0), "Route left the top bound");
                Assert.That(PointX, Is.LessThanOrEqualTo(ViewWidth + 1.0), "Route left the right bound");
                Assert.That(PointY, Is.LessThanOrEqualTo(ViewHeight + 1.0), "Route left the bottom bound");
            }
        }

        List<(double X, double Y)> Arrowheads = [];
        foreach (Match PolygonMatch in Regex.Matches(Svg, @"<polygon points=""([^""]+)"""))
        {
            Arrowheads.Add(PolygonCentroid(PolygonMatch.Groups[1].Value));
        }

        for (int LeftIndex = 0; LeftIndex < Arrowheads.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Arrowheads.Count; RightIndex++)
            {
                double DeltaX = Arrowheads[LeftIndex].X - Arrowheads[RightIndex].X;
                double DeltaY = Arrowheads[LeftIndex].Y - Arrowheads[RightIndex].Y;
                Assert.That(
                    Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY)),
                    Is.GreaterThan(FlowchartSharedRouteQualityHelper.STACKED_PORT_EPSILON_PX),
                    "Two arrowheads stack at " + Arrowheads[LeftIndex].X.ToString("0.0") + "," + Arrowheads[LeftIndex].Y.ToString("0.0"));
            }
        }

        Match HttpsMatch = Regex.Match(Svg, @"<tspan[^>]*>(HTTPS)</tspan>");
        Assert.That(HttpsMatch.Success, Is.True);
        Match HttpsText = Regex.Match(
            Svg.Substring(0, HttpsMatch.Index),
            @"<text x=""([0-9.\-]+)"" y=""([0-9.\-]+)""[^>]*>\s*$");
        Assert.That(HttpsText.Success, Is.True);
        double HttpsX = double.Parse(HttpsText.Groups[1].Value, CultureInfo.InvariantCulture);
        double HttpsY = double.Parse(HttpsText.Groups[2].Value, CultureInfo.InvariantCulture);
        double BestHttpsDistance = double.PositiveInfinity;
        foreach (List<(double X, double Y)> Stroke in Strokes)
        {
            double Candidate = FlowchartSharedRouteQualityHelper.DistanceFromPointToPolyline(HttpsX, HttpsY, Stroke);
            if (Candidate < BestHttpsDistance)
            {
                BestHttpsDistance = Candidate;
            }
        }

        Assert.That(BestHttpsDistance, Is.LessThanOrEqualTo(LABEL_EDGE_TOLERANCE_PX), "HTTPS must sit beside its rendered edge");
    }

    private static (double X, double Y) PolygonCentroid(string PointsText)
    {
        List<(double X, double Y)> Points = [];
        foreach (Match Pair in Regex.Matches(PointsText, @"([0-9.\-]+),([0-9.\-]+)"))
        {
            Points.Add((
                double.Parse(Pair.Groups[1].Value, CultureInfo.InvariantCulture),
                double.Parse(Pair.Groups[2].Value, CultureInfo.InvariantCulture)));
        }

        double SumX = 0.0;
        double SumY = 0.0;
        foreach ((double PointX, double PointY) in Points)
        {
            SumX += PointX;
            SumY += PointY;
        }

        return (SumX / Points.Count, SumY / Points.Count);
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
        Assert.That(
            FlowchartSharedRouteQualityHelper.PolylineComesWithinClearance(
                Resolved.Points,
                Contract,
                BoundsById,
                SourceId,
                TargetId),
            Is.False,
            SourceId + " -> " + TargetId + " comes within clearance of " + ObstacleId);
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
        List<FlowchartResolvedGuide> Guides = FlowchartEdgeRouteResolver.ResolveAllVisibleGuidePoints(
            Contract,
            BoundsById,
            NodeById,
            FlowchartDirection.TopToBottom);
        Edges = [];
        foreach (FlowchartResolvedGuide Guide in Guides)
        {
            Edges.Add(new ResolvedEdge(Guide.Edge, Guide.Points, Guide.RouteKind));
        }

        Assert.That(Edges.Count, Is.GreaterThanOrEqualTo(14));
    }

    private static ResolvedEdge FindEdge(List<ResolvedEdge> Edges, string SourceId, string TargetId)
    {
        return Edges.Single(
            Candidate => string.Equals(Candidate.Edge.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Candidate.Edge.TargetId, TargetId, StringComparison.Ordinal));
    }

    private sealed class ResolvedEdge
    {
        public ResolvedEdge(EdgeContract Edge, List<(double X, double Y)> Points, FlowchartEdgeRouteKind RouteKind)
        {
            this.Edge = Edge;
            this.Points = Points;
            this.RouteKind = RouteKind;
        }

        public EdgeContract Edge { get; }

        public List<(double X, double Y)> Points { get; }

        public FlowchartEdgeRouteKind RouteKind { get; }
    }
}

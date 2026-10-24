using System.Globalization;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class F125NodesFirstDiamondRenderTests
{
    private const string F125_SOURCE = """
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
        """;

    [Test]
    public void TryLayout_F125_MergeDecisionCentredUnderMiddlePath()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F125_SOURCE);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        double PathAX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "PA").CenterX;
        double PathBX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "PB").CenterX;
        double PathCX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "PC").CenterX;
        double MergeX = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "D").CenterX;

        Assert.That(PathAX, Is.LessThan(PathBX));
        Assert.That(PathBX, Is.LessThan(PathCX));
        Assert.That(Math.Abs(MergeX - PathBX), Is.LessThan(1.0));
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F125_MultiIncoming_UsesBottomCentreSourcesAndDistinctDiamondPorts()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F125_SOURCE);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        foreach (string PathId in new[] { "PA", "PB", "PC" })
        {
            AssertEdgeSourceFace(
                Contract,
                BoundsById,
                NodeById,
                Direction,
                PathId,
                "D",
                FlowchartEdgeAttachFace.Bottom,
                FlowchartEdgeRouteKind.Decision);
        }

        bool PathBResolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "PB" && Edge.TargetId == "D"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> PathBPoints,
            out _,
            Layout: Layout);
        Assert.That(PathBResolved, Is.True);
        Assert.That(PathBPoints.Count, Is.GreaterThan(2),
            "The diamond edge-centre port is offset from Path B, so the allocated ports do not align for a straight line");

        bool PathAResolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "PA" && Edge.TargetId == "D"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> PathAPoints,
            out _,
            Layout: Layout);
        bool PathCResolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "PC" && Edge.TargetId == "D"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> PathCPoints,
            out _,
            Layout: Layout);

        Assert.That(PathAResolved, Is.True);
        Assert.That(PathCResolved, Is.True);

        AssertDiamondPortRules(Contract, Layout, "D");
        Assert.That(
            PathAPoints[PathAPoints.Count - 1].X,
            Is.LessThan(BoundsById["D"].CenterX),
            "Path A should enter the diamond upper-left perimeter");
        Assert.That(
            PathCPoints[PathCPoints.Count - 1].X,
            Is.GreaterThan(BoundsById["D"].CenterX),
            "Path C should enter the diamond upper-right perimeter");

        (double PathAGuideX, _) = PathAPoints[2];
        (double PathCGuideX, _) = PathCPoints[2];
        Assert.That(
            PathAGuideX,
            Is.EqualTo(PathAPoints[0].X).Within(1.0),
            "Path A keeps its source-column track until the normal approach to its allocated diamond port");
        Assert.That(
            PathCGuideX,
            Is.EqualTo(PathCPoints[0].X).Within(1.0),
            "Path C keeps its source-column track until the normal approach to its allocated diamond port");
    }

    private static void AssertDiamondPortRules(DiagramContract Contract, FlowchartLayoutResult Layout, string DiamondId)
    {
        FlowchartLayoutNodeBounds diamond = Layout.NodeBounds.Single(box => box.NodeId == DiamondId);
        double halfWidth = diamond.Width / 2, halfHeight = diamond.Height / 2;
        List<(EdgeContract Edge, FlowchartLayoutEdgeRoute Route, bool Source, FlowchartLayoutPoint Port)> ports = [];
        foreach (EdgeContract edge in Contract.Edges.Where(edge => edge.SourceId == DiamondId || edge.TargetId == DiamondId))
        {
            FlowchartLayoutEdgeRoute route = Layout.EdgeRoutes.Single(route => route.EdgeId == edge.Id);
            bool source = edge.SourceId == DiamondId;
            FlowchartLayoutPoint port = source ? route.Points[0] : route.Points[^1];
            double outline = Math.Abs((port.X - diamond.CenterX) / halfWidth)
                + Math.Abs((port.Y - diamond.CenterY) / halfHeight);
            Assert.That(outline, Is.EqualTo(1).Within(0.02), $"{edge.Id} ends on the drawn diamond outline");
            double normalX = source ? route.ExitNormalX : route.EntryNormalX;
            double normalY = source ? route.ExitNormalY : route.EntryNormalY;
            Assert.That(Math.Abs(normalX) + Math.Abs(normalY), Is.GreaterThan(0), $"{edge.Id} has an edge-normal port");
            FlowchartLayoutPoint adjacent = source ? route.Points[1] : route.Points[^2];
            double vx = adjacent.X - port.X, vy = adjacent.Y - port.Y;
            double normalMagnitude = Math.Sqrt(normalX * normalX + normalY * normalY);
            Assert.That(Math.Abs(vx * normalY - vy * normalX) / normalMagnitude, Is.LessThan(1.5),
                $"{edge.Id} leaves the slanted edge square-on");
            Assert.That(vx * normalX + vy * normalY, Is.GreaterThan(0),
                $"{edge.Id} departs outward from the diamond");
            ports.Add((edge, route, source, port));
        }

        foreach (var group in ports.GroupBy(item => DiamondEdge(item.Port, diamond)))
        {
            var ordered = group.OrderBy(item => EdgeFraction(item.Port, diamond, group.Key)).ToArray();
            double[] fractions = ordered.Select(item => EdgeFraction(item.Port, diamond, group.Key)).ToArray();
            for (int i = 0; i < fractions.Length; i++)
                Assert.That(fractions[i], Is.EqualTo((i + 1.0) / (fractions.Length + 1)).Within(0.02),
                    $"{group.Key} diamond ports use symmetric k/(n+1) slots");
            for (int i = 1; i < ordered.Length; i++)
            {
                string peerId = ordered[i].Source ? ordered[i].Edge.TargetId : ordered[i].Edge.SourceId;
                string priorPeerId = ordered[i - 1].Source ? ordered[i - 1].Edge.TargetId : ordered[i - 1].Edge.SourceId;
                FlowchartLayoutNodeBounds peer = Layout.NodeBounds.Single(box => box.NodeId == peerId);
                FlowchartLayoutNodeBounds priorPeer = Layout.NodeBounds.Single(box => box.NodeId == priorPeerId);
                Assert.That(PeerFraction(peer, diamond, group.Key), Is.GreaterThanOrEqualTo(PeerFraction(priorPeer, diamond, group.Key)),
                    $"{group.Key} ports follow the relative ordering of their peer nodes");
            }
        }

        foreach (var item in ports)
        {
            FlowchartLayoutEdgeRoute route = item.Route;
            FlowchartLayoutPoint start = route.Points[0], end = route.Points[^1];
            bool aligned = Math.Abs(start.X - end.X) <= 0.5 || Math.Abs(start.Y - end.Y) <= 0.5;
            Assert.That(aligned && route.Points.Count == 2, Is.False,
                "These allocated ports do not align, so 4.2 does not force a straight line");
        }

        double originX = Layout.NodeBounds[0].CenterX, originY = Layout.NodeBounds[0].CenterY;
        foreach (FlowchartLayoutNodeBounds node in Layout.NodeBounds)
        {
            Assert.That(Math.Abs((node.CenterX - originX) / DiagramGridMetrics.GRID_UNIT_PX
                - Math.Round((node.CenterX - originX) / DiagramGridMetrics.GRID_UNIT_PX)), Is.LessThan(0.01),
                $"{node.NodeId} uses the common horizontal grid");
            Assert.That(Math.Abs((node.CenterY - originY) / DiagramGridMetrics.GRID_UNIT_PX
                - Math.Round((node.CenterY - originY) / DiagramGridMetrics.GRID_UNIT_PX)), Is.LessThan(0.01),
                $"{node.NodeId} uses the common vertical grid");
        }
    }

    private static FlowchartDiamondEdge DiamondEdge(FlowchartLayoutPoint point, FlowchartLayoutNodeBounds box)
    {
        double x = point.X - box.CenterX, y = point.Y - box.CenterY;
        if (x >= 0) return y >= 0 ? FlowchartDiamondEdge.TopRight : FlowchartDiamondEdge.BottomRight;
        return y >= 0 ? FlowchartDiamondEdge.TopLeft : FlowchartDiamondEdge.BottomLeft;
    }

    private static double EdgeFraction(FlowchartLayoutPoint point, FlowchartLayoutNodeBounds box, FlowchartDiamondEdge edge)
    {
        double x = point.X - box.CenterX, y = point.Y - box.CenterY;
        double halfWidth = box.Width / 2, halfHeight = box.Height / 2;
        return edge switch
        {
            FlowchartDiamondEdge.TopRight => x / halfWidth,
            FlowchartDiamondEdge.BottomRight => (halfWidth - x) / halfWidth,
            FlowchartDiamondEdge.BottomLeft => -x / halfWidth,
            _ => (halfWidth + x) / halfWidth
        };
    }

    private static double PeerFraction(FlowchartLayoutNodeBounds peer, FlowchartLayoutNodeBounds diamond, FlowchartDiamondEdge edge)
    {
        double x = peer.CenterX - diamond.CenterX, halfWidth = diamond.Width / 2;
        return edge switch
        {
            FlowchartDiamondEdge.TopRight => x / halfWidth,
            FlowchartDiamondEdge.BottomRight => (halfWidth - x) / halfWidth,
            FlowchartDiamondEdge.BottomLeft => -x / halfWidth,
            _ => (halfWidth + x) / halfWidth
        };
    }

    [Test]
    public void RenderToSvg_F125_SideIncomingArrowsUseGuideTangentNotStub()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F125_SOURCE);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "PA" && Edge.TargetId == "D"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> LayoutPoints,
            out _);

        Assert.That(
            FlowchartMarkerTangentValidator.TryValidateEndpointMarkerTangent(
                LayoutPoints,
                FlowchartEdgeRouteKind.Decision,
                Direction,
                AtTargetEnd: true,
                out string TargetFailure),
            Is.True,
            TargetFailure);
    }

    [Test]
    public void RenderToSvg_F125_PathB_IsStraightVertical()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F125_SOURCE);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        int StraightLineCount = Regex.Matches(Svg, "<line ").Count;

        Assert.That(StraightLineCount, Is.GreaterThanOrEqualTo(1), "Path B→Merge? should be a straight vertical connector");
    }

    [Test]
    public void Analyze_F125_SpineAlignedStraightDecisionEdge_DoesNotReportCentreSpineRegression()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F125_SOURCE);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        List<FlowchartEdgeRouteDiagnosticFinding> Findings = FlowchartEdgeRouteDiagnosticAnalyzer.Analyze(
            Contract,
            Layout,
            Direction);

        Assert.That(
            Findings.Any(
                Finding => Finding.Kind == FlowchartEdgeRouteDiagnosticKind.CentreSpineRegression
                    && string.Equals(Finding.EdgeKey, "PB->D", StringComparison.Ordinal)),
            Is.False,
            "Path B to Merge? is a straight spine-aligned connector and must not be flagged as centre-spine regression.");
    }

    [Test]
    public void ResolveEdgeLabelPosition_F125_DecisionBranchLabels_UseHalfPolylineMidspan()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F125_SOURCE);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        Dictionary<string, string> RouteClassByEdgeKey =
            FlowchartEdgeRoutingIntentAnalyzer.BuildRouteClassNamesByEdgeKey(Contract, Layout, Direction);
        FlowchartLayoutNodeBounds MergeBounds = BoundsById["D"];

        foreach (EdgeContract Edge in Contract.Edges.Where(
                     Candidate => string.Equals(Candidate.SourceId, "D", StringComparison.Ordinal)
                         && !string.IsNullOrWhiteSpace(Candidate.Label)))
        {
            Assert.That(
                FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> GuidePoints,
                    out FlowchartEdgeRouteKind _),
                Is.True);

            string RouteClass = ResolveRouteClass(Edge, RouteClassByEdgeKey);
            Assert.That(RouteClass, Is.EqualTo("LocalDecisionBranch"), Edge.Label + " route class");

            (double ExpectedX, double ExpectedY) =
                FlowchartEdgeLabelPlacementHelper.ComputeLocalDecisionBranchLabelExpectedPosition(
                    GuidePoints,
                    MergeBounds,
                    Edge.Label,
                    Direction);
            (double ActualX, double ActualY) = FlowchartEdgeLabelPlacementHelper.ResolveEdgeLabelPosition(
                MergeBounds,
                BoundsById[Edge.TargetId],
                GuidePoints,
                Direction,
                "diamond",
                Edge.Label,
                Edge,
                Contract,
                BoundsById,
                RouteClass);

            Assert.That(ActualX, Is.EqualTo(ExpectedX).Within(0.5), Edge.Label + " layout X");
            Assert.That(ActualY, Is.EqualTo(ExpectedY).Within(0.5), Edge.Label + " layout Y");
        }
    }

    [Test]
    public void RenderToSvg_F125_DecisionBranchLabels_HorizontallyCentredOnConnector()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F125_SOURCE);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartLayoutNodeBounds MergeBounds = BoundsById["D"];
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        foreach (EdgeContract Edge in Contract.Edges.Where(
                     Candidate => string.Equals(Candidate.SourceId, "D", StringComparison.Ordinal)
                         && !string.IsNullOrWhiteSpace(Candidate.Label)))
        {
            Assert.That(
                FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> GuidePoints,
                    out FlowchartEdgeRouteKind _),
                Is.True);

            (double ExpectedLayoutX, double _) =
                FlowchartEdgeLabelPlacementHelper.ComputeLocalDecisionBranchLabelExpectedPosition(
                    GuidePoints,
                    MergeBounds,
                    Edge.Label,
                    Direction);

            Match? LabelText = Regex.Matches(
                    Svg,
                    @"<text x=""([^""]+)"" y=""([^""]+)""[^>]*>(.*?)</text>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase)
                .Cast<Match>()
                .FirstOrDefault(Candidate => Candidate.Groups[3].Value.Contains(Edge.Label!, StringComparison.Ordinal));

            Assert.That(LabelText, Is.Not.Null);
            Assert.That(LabelText!.Success, Is.True, "Could not locate " + Edge.Label + " edge label in SVG.");

            double LabelSvgX = double.Parse(LabelText.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(FlowchartSvgLayoutCoordinateMapper.TryCreate(Contract, Layout, out FlowchartSvgLayoutCoordinateMapper? Mapper), Is.True);
            (double PlacementSvgX, double _) = Mapper!.LayoutToSvg(ExpectedLayoutX, 0.0);
            (double DiamondSvgX, double _) = Mapper.LayoutToSvg(MergeBounds.CenterX, 0.0);
            (double DrawnSvgX, double _) = DrawnEdgeLabelLocator.ResolveSvgCentre(Contract, Layout, Edge);

            // The label is drawn at the decision placement point moved clear of the connector and the diamond
            // (spec §15.0C.8 and the EIP 2.56 addendum), so it stays on the outer side of its branch.
            Assert.That(LabelSvgX, Is.EqualTo(DrawnSvgX).Within(2.0), $"{Edge.Label} SVG X");
            Assert.That(Math.Sign(LabelSvgX - DiamondSvgX), Is.EqualTo(Math.Sign(PlacementSvgX - DiamondSvgX)), $"{Edge.Label} should stay on its branch side.");
        }
    }

    private static void AssertEdgeSourceFace(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId,
        string TargetId,
        FlowchartEdgeAttachFace ExpectedSourceFace,
        FlowchartEdgeRouteKind ExpectedRouteKind)
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
            out FlowchartEdgeRouteKind RouteKind);

        Assert.That(Resolved, Is.True, SourceId + "->" + TargetId + " should resolve");
        Assert.That(RouteKind, Is.EqualTo(ExpectedRouteKind), SourceId + "->" + TargetId + " route kind");

        FlowchartEdgeAttachFace SourceFace = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            BoundsById[SourceId],
            LayoutPoints[0].X,
            LayoutPoints[0].Y);
        Assert.That(SourceFace, Is.EqualTo(ExpectedSourceFace), SourceId + "->" + TargetId + " source port");
    }

    private static string ResolveRouteClass(
        EdgeContract Edge,
        IReadOnlyDictionary<string, string> RouteClassByEdgeKey)
    {
        if (!string.IsNullOrWhiteSpace(Edge.Id)
            && RouteClassByEdgeKey.TryGetValue(Edge.Id, out string? RouteClassById))
        {
            return RouteClassById;
        }

        string SourceTargetKey = Edge.SourceId + "->" + Edge.TargetId;
        if (RouteClassByEdgeKey.TryGetValue(SourceTargetKey, out string? RouteClassByEndpoints))
        {
            return RouteClassByEndpoints;
        }

        return string.Empty;
    }
}

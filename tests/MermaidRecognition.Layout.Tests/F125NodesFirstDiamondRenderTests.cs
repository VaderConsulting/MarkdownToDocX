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
            out _);
        Assert.That(PathBResolved, Is.True);
        Assert.That(PathBPoints.Count, Is.EqualTo(2), "Path B should remain a straight vertical connector");

        bool PathAResolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "PA" && Edge.TargetId == "D"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> PathAPoints,
            out _);
        bool PathCResolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            Contract.Edges.First(Edge => Edge.SourceId == "PC" && Edge.TargetId == "D"),
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> PathCPoints,
            out _);

        Assert.That(PathAResolved, Is.True);
        Assert.That(PathCResolved, Is.True);
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
            "Path A guide mirrors yes/no via swapped decision-branch attach (source-column corridor)");
        Assert.That(
            PathCGuideX,
            Is.EqualTo(PathCPoints[0].X).Within(1.0),
            "Path C guide mirrors no/yes via swapped decision-branch attach (source-column corridor)");
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

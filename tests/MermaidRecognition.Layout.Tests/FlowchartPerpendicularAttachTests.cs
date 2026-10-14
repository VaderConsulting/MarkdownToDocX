using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Asserts that resolved edge guide polylines meet node boundaries at right angles (project rule).
/// </summary>
[TestFixture]
public sealed class FlowchartPerpendicularAttachTests
{
    [Test]
    public void BasisStyle_RendersTheOrthogonalRouteWithRoundedCorners()
    {
        (double X, double Y)[] Points = [(0, 0), (0, 100), (100, 100)];

        string Actual = FlowchartCurvePathBuilder.BuildPathDataFromGuidePoints(Points, "basis");
        string Rounded = FlowchartCurvePathBuilder.BuildRoundedCornerPathData(
            Points, DiagramGridRouteBuilder.CORNER_RADIUS_PX);

        Assert.That(Actual, Is.EqualTo(Rounded));
        Assert.That(Actual, Does.Contain("C"));
    }

    [Test]
    public void BuildCurveBasisPathData_EmitsSmoothTerminalApproachIntoBoundary()
    {
        List<(double X, double Y)> Points =
        [
            (0.0, 0.0),
            (0.0, 10.0),
            (50.0, 10.0),
            (50.0, 20.0)
        ];

        string PathData = FlowchartCurvePathBuilder.BuildCurveBasisPathData(Points);
        Assert.That(PathData, Does.StartWith("M0,0L0,10"));
        Assert.That(PathData, Does.EndWith("L50,20"),
            "Terminal stub-to-boundary must be a straight segment to avoid hook cubics");
    }

    [Test]
    public void BuildCurveBasisPathData_FirstControlPointExtendsAlongSourceStubTangent()
    {
        List<(double X, double Y)> Points =
        [
            (0.0, 0.0),
            (0.0, 10.0),
            (30.0, 40.0),
            (30.0, 50.0)
        ];

        string PathData = FlowchartCurvePathBuilder.BuildCurveBasisPathData(Points);
        Match FirstControl = Regex.Match(PathData, @"^M0,0L0,10C(-?[\d\.]+),(-?[\d\.]+) ");
        Assert.That(FirstControl.Success, Is.True, PathData);
        Assert.That(double.Parse(FirstControl.Groups[1].Value, CultureInfo.InvariantCulture), Is.EqualTo(0.0).Within(0.01), "The first control point should lie on the source stub line.");
        Assert.That(double.Parse(FirstControl.Groups[2].Value, CultureInfo.InvariantCulture), Is.GreaterThan(10.0), "The first control point should extend past the stub end.");
    }

    [Test]
    public void BuildCurveBasisPathData_FivePointRoute_BridgesStubsWithOneCubic()
    {
        List<(double X, double Y)> Points =
        [
            (0.0, 0.0),
            (0.0, 10.0),
            (20.0, 30.0),
            (40.0, 50.0),
            (40.0, 60.0)
        ];

        // Spec §15.0C.3: the two straight stubs are joined by one cubic.
        string PathData = FlowchartCurvePathBuilder.BuildCurveBasisPathData(Points);
        Assert.That(PathData, Does.StartWith("M0,0L0,10C"));
        Assert.That(PathData.Count(Character => Character == 'C'), Is.EqualTo(1));
        Assert.That(PathData, Does.EndWith(" 40,50L40,60"), "Target stub-to-boundary must stay a straight segment");
    }

    [Test]
    public void BuildCurveBasisPathData_SixPointRoute_BridgesStubsWithOneCubic()
    {
        List<(double X, double Y)> Points =
        [
            (0.0, 0.0),
            (0.0, 10.0),
            (10.0, 30.0),
            (40.0, 30.0),
            (40.0, 50.0),
            (40.0, 60.0)
        ];

        // Spec §15.0C.3 covers four- to six-point stub routes: the step guides are bridged by one cubic.
        string PathData = FlowchartCurvePathBuilder.BuildCurveBasisPathData(Points);
        int CurveSegmentCount = PathData.Split('C').Length - 1;
        Assert.That(CurveSegmentCount, Is.EqualTo(1));
        Assert.That(PathData, Does.StartWith("M0,0L0,10C"));
        Assert.That(PathData, Does.EndWith("L40,60"));
    }

    [Test]
    public void ComputeOffsetLabelPositionForStraightSegment_UsesConnectorMidspanOffsetToOneSide()
    {
        (double LabelX, double LabelY) = FlowchartEdgeLabelPlacementHelper.ComputeOffsetLabelPositionForStraightSegment(
            100.0,
            0.0,
            100.0,
            50.0);

        Assert.That(Math.Abs(LabelX - 100.0), Is.EqualTo(FlowchartEdgeLabelPlacementHelper.DEFAULT_OFFSET_PX).Within(0.5));
        Assert.That(LabelY, Is.EqualTo(25.0).Within(0.5));
    }

    [Test]
    public void ComputeSpineMidspanEdgeLabelPosition_TbChain_UsesNodeCentreMidspan()
    {
        FlowchartLayoutNodeBounds SourceBounds = new FlowchartLayoutNodeBounds
        {
            NodeId = "A",
            CenterX = 100.0,
            CenterY = 200.0,
            Width = 80.0,
            Height = 40.0
        };
        FlowchartLayoutNodeBounds TargetBounds = new FlowchartLayoutNodeBounds
        {
            NodeId = "B",
            CenterX = 100.0,
            CenterY = 80.0,
            Width = 80.0,
            Height = 40.0
        };

        List<(double X, double Y)> LayoutPoints =
        [
            (100.0, 180.0),
            (100.0, 100.0)
        ];

        (double LabelX, double LabelY) = FlowchartEdgeLabelPlacementHelper.ComputeSpineMidspanEdgeLabelPosition(
            SourceBounds,
            TargetBounds,
            LayoutPoints,
            FlowchartDirection.TopToBottom);

        Assert.That(LabelX, Is.EqualTo(100.0).Within(0.5));
        Assert.That(LabelY, Is.EqualTo(140.0).Within(0.5));
    }

    [Test]
    public void ComputeOffsetLabelPosition_UsesHalfPolylineDistanceOffsetToOneSide()
    {
        List<(double X, double Y)> GuidePoints =
        [
            (0.0, 0.0),
            (0.0, 10.0),
            (20.0, 30.0),
            (40.0, 50.0)
        ];

        (double LabelX, double LabelY) = FlowchartEdgeLabelPlacementHelper.ComputeOffsetLabelPosition(GuidePoints);
        (double MidX, double MidY) = FlowchartEdgeLabelPlacementHelper.ComputePolylineHalfDistancePosition(GuidePoints);

        double OffsetX = LabelX - MidX;
        double OffsetY = LabelY - MidY;
        double OffsetLength = Math.Sqrt((OffsetX * OffsetX) + (OffsetY * OffsetY));
        Assert.That(OffsetLength, Is.EqualTo(FlowchartEdgeLabelPlacementHelper.DEFAULT_OFFSET_PX).Within(0.5));
        double AlongDiagonal = (OffsetX + OffsetY) / Math.Sqrt(2.0);
        Assert.That(AlongDiagonal, Is.EqualTo(0.0).Within(0.5), "The offset should be at right angles to the 45 degree segment at the midpoint.");
    }

    [Test]
    public void ApplyTerminalStubs_WhenDecisionFanOut_InsertsDiamondNormalStub()
    {
        FlowchartLayoutNodeBounds Diamond = BuildBounds("D", 100.0, 100.0, 80.0, 80.0);
        FlowchartLayoutNodeBounds Target = BuildBounds("T", 40.0, 40.0, 80.0, 40.0);
        double Half = 40.0;
        List<(double X, double Y)> Points =
        [
            (100.0 - (Half / 2.0), 100.0 + (Half / 2.0)),
            (60.0, 60.0),
            (40.0, 60.0)
        ];

        List<(double X, double Y)> Expanded = FlowchartEdgeTerminalStubExpander.Apply(
            Points,
            Diamond,
            "diamond",
            Target,
            "rectangle");

        Assert.That(Expanded.Count, Is.GreaterThan(Points.Count));
        double StubLength = Math.Sqrt(
            ((Expanded[1].X - Expanded[0].X) * (Expanded[1].X - Expanded[0].X))
            + ((Expanded[1].Y - Expanded[0].Y) * (Expanded[1].Y - Expanded[0].Y)));
        Assert.That(StubLength, Is.GreaterThanOrEqualTo(8.0));
    }

    [Test]
    public void TryValidateGuidePoints_WhenVerticalStubOnHorizontalFace_ReturnsTrue()
    {
        FlowchartLayoutNodeBounds Source = BuildBounds("A", 100.0, 200.0, 80.0, 40.0);
        List<(double X, double Y)> Points =
        [
            (100.0, 180.0),
            (100.0, 168.0),
            (200.0, 168.0),
            (200.0, 228.0),
            (200.0, 220.0)
        ];

        bool Valid = FlowchartPerpendicularAttachValidator.TryValidateGuidePoints(
            Points,
            Source,
            "rectangle",
            BuildBounds("B", 200.0, 240.0, 80.0, 40.0),
            "rectangle",
            AllowDiagonalCrossEdge: false,
            out string FailureMessage);

        Assert.That(Valid, Is.True, FailureMessage);
    }

    [Test]
    public void TryValidateGuidePoints_WhenDiagonalStubOnHorizontalFace_ReturnsFalse()
    {
        FlowchartLayoutNodeBounds Source = BuildBounds("A", 100.0, 200.0, 80.0, 40.0);
        List<(double X, double Y)> Points =
        [
            (100.0, 180.0),
            (140.0, 140.0),
            (200.0, 220.0)
        ];

        bool Valid = FlowchartPerpendicularAttachValidator.TryValidateGuidePoints(
            Points,
            Source,
            BuildBounds("B", 200.0, 240.0, 80.0, 40.0),
            AllowDiagonalCrossEdge: false,
            out string FailureMessage);

        Assert.That(Valid, Is.False);
        Assert.That(FailureMessage, Does.Contain("Source attach"));
    }

    [Test]
    public void ValidateAllVisibleEdges_F08_SimpleChain_Passes() => AssertAllVisibleEdgesPerpendicular(
            """
            flowchart LR
            A[One] --> B[Two]
            """,
            "F08");

    [Test]
    public void RenderToSvg_F29_SpineStraight_BranchesCurvedWithStraightTerminals()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        int StraightSpineCount = SvgStraightConnectorReader.Read(Svg).Count(Connector => Math.Abs(Connector.X1 - Connector.X2) < 0.5);
        int OrthogonalBranchCount = Regex.Matches(
            Svg,
            @"data-edge-source=""Check"" data-edge-target=""(OK|Fail)""").Count;

        Assert.That(StraightSpineCount, Is.GreaterThanOrEqualTo(1), "Start→Valid? should be a straight vertical connector");
        Assert.That(OrthogonalBranchCount, Is.GreaterThanOrEqualTo(4),
            "B12a Yes/No branches use orthogonal horizontal/vertical runs with label gaps");
    }

    [Test]
    public void RenderToSvg_F29_DecisionBranches_NoTerminalHookCubic()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        foreach (string TargetId in new[] { "OK", "Fail" })
        {
            string PathData = OrthogonalRouteTestHelper.ExtractEdgePath(Svg, "Check", TargetId);
            OrthogonalRouteTestHelper.AssertOrthogonalPathWithRoundedCorners(PathData);
            System.Text.RegularExpressions.Match Start = System.Text.RegularExpressions.Regex.Match(
                PathData,
                @"M\s*([-\d.]+)[,\s]+([-\d.]+)");
            System.Text.RegularExpressions.Match End = System.Text.RegularExpressions.Regex.Match(
                PathData,
                @"([-\d.]+)[,\s]+([-\d.]+)\s*$");
            Assert.That(Start.Success, Is.True);
            Assert.That(End.Success, Is.True);
            if (string.Equals(TargetId, "Fail", StringComparison.Ordinal))
            {
                double StartY = double.Parse(Start.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                double EndY = double.Parse(End.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                Assert.That(EndY, Is.GreaterThan(StartY), "The branch keeps moving down toward its target");
            }
            else
            {
                Assert.That(OrthogonalRouteTestHelper.LongestHorizontalRunMidpoint(PathData), Is.Not.Null,
                    "Aligned branch exposes a horizontal label run");
            }
        }
    }

    [Test]
    public void RenderToSvg_F29_DecisionBranches_NoAngularPolylines()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        int AngularPolylineCount = System.Text.RegularExpressions.Regex.Matches(
            Svg,
            @"<path d=""([^""]+)"" fill=""none""",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Cast<System.Text.RegularExpressions.Match>()
            .Count(Match =>
                !Match.Groups[1].Value.Contains('C', StringComparison.Ordinal)
                && System.Text.RegularExpressions.Regex.IsMatch(
                    Match.Groups[1].Value,
                    @"M[^""]*L[^""]*L[^""]*L[^""]*L"));

        Assert.That(AngularPolylineCount, Is.EqualTo(0),
            "Yes/No branches must not regress to kinked polyline paths");
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F29_DecisionBranchGuideUsesTargetLegSpread()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        FlowchartLayoutNodeBounds HubBounds = BoundsById["Check"];
        double HubCentreX = HubBounds.CenterX;

        foreach (string TargetId in new[] { "OK", "Fail" })
        {
            EdgeContract Edge = Contract.Edges.First(
                Candidate => Candidate.SourceId == "Check" && Candidate.TargetId == TargetId);

            bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> LayoutPoints,
                out FlowchartEdgeRouteKind RouteKind,
                Layout);

            Assert.That(Resolved, Is.True);
            Assert.That(RouteKind, Is.EqualTo(FlowchartEdgeRouteKind.Grid));
            Assert.That(LayoutPoints.Count, Is.GreaterThanOrEqualTo(2).And.LessThanOrEqualTo(4),
                "Decision branches use orthogonal guides with at most one rounded elbow");

            double SourceX = LayoutPoints[0].X;
            if (string.Equals(TargetId, "OK", StringComparison.Ordinal))
            {
                Assert.That(SourceX, Is.LessThan(HubCentreX - 4.0), "Yes branch exits the left-facing diamond port");
            }
            else
            {
                Assert.That(SourceX, Is.GreaterThan(HubCentreX + 4.0), "No branch exits the right-facing diamond port");
            }
        }
    }

    [Test]
    public void RenderToSvg_F30_CenterBranch_IsStraightVertical()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A[Christmas] -->|Get money| B(Go shopping)
            B --> C{Let me think}
            C -->|One| D[Laptop]
            C -->|Two| E[iPhone]
            C -->|Three| F[Other gift]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        int StraightLineCount = SvgStraightConnectorReader.Read(Svg).Count(Connector => Math.Abs(Connector.X1 - Connector.X2) < 0.5);

        Assert.That(StraightLineCount, Is.GreaterThanOrEqualTo(3),
            "Christmas→Go shopping, Go shopping→Let me think, and Let me think→iPhone should be straight");
    }

    [Test]
    public void RenderToSvg_F30_SideBranches_UseSmoothCurveBodiesNotAngularPolylines()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A[Christmas] -->|Get money| B(Go shopping)
            B --> C{Let me think}
            C -->|One| D[Laptop]
            C -->|Two| E[iPhone]
            C -->|Three| F[Other gift]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        foreach (string TargetId in new[] { "D", "F" })
        {
            string PathData = OrthogonalRouteTestHelper.ExtractEdgePath(Svg, "C", TargetId);
            OrthogonalRouteTestHelper.AssertOrthogonalPathWithRoundedCorners(PathData);
            Assert.That(PathData, Does.Match(@"^M[^""]+L[^""]+"),
                "Side branches leave the diamond horizontally before turning down");
        }
    }

    [Test]
    public void RenderToSvg_F35_PathB_IsStraightVertical()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A[Path A] --> D{Merge?}
            B[Path B] --> D
            C[Path C] --> D
            D -->|yes| E[Continue]
            D -->|no| F[Stop]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        int StraightLineCount = System.Text.RegularExpressions.Regex.Matches(Svg, "<line ").Count;

        Assert.That(StraightLineCount, Is.GreaterThanOrEqualTo(1), "Path B→Merge? should be a straight vertical connector");
    }

    [Test]
    public void ValidateAllVisibleEdges_F29_BinaryDecision_Passes() => AssertAllVisibleEdgesPerpendicular(
            """
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            """,
            "F29");

    [Test]
    public void ValidateAllVisibleEdges_F30_MultiWayDecision_Passes() => AssertAllVisibleEdgesPerpendicular(
            """
            flowchart TD
            A[Christmas] -->|Get money| B(Go shopping)
            B --> C{Let me think}
            C -->|One| D[Laptop]
            C -->|Two| E[iPhone]
            C -->|Three| F[Other gift]
            """,
            "F30");

    [Test]
    public void ValidateAllVisibleEdges_F32_FanIn_Passes() => AssertAllVisibleEdgesPerpendicular(
            """
            flowchart TB
            W1[Worker 1] --> Agg[Aggregate]
            W2[Worker 2] --> Agg
            W3[Worker 3] --> Agg
            """,
            "F32");

    [Test]
    public void ValidateAllVisibleEdges_F33_ParallelWorkers_Passes() => AssertAllVisibleEdgesPerpendicular(
            """
            flowchart LR
            A[Ingress] --> B[Enqueue]
            B --> C[Worker 1]
            B --> D[Worker 2]
            C --> E[Aggregate]
            D --> E
            E --> F[Out]
            """,
            "F33");

    [Test]
    public void ValidateAllVisibleEdges_F34_RetryLoop_Passes() => AssertAllVisibleEdgesPerpendicular(
            """
            flowchart LR
            A[Receive] --> B{OK?}
            B -->|Yes| C[Done]
            B -->|No| D[Retry]
            D --> A
            """,
            "F34");

    [Test]
    public void ValidateAllVisibleEdges_F35_DiamondFanIn_Passes() => AssertAllVisibleEdgesPerpendicular(
            """
            flowchart TB
            A[Path A] --> D{Merge?}
            B[Path B] --> D
            C[Path C] --> D
            D -->|yes| E[Continue]
            D -->|no| F[Stop]
            """,
            "F35");

    [Test]
    public void ValidateAllVisibleEdges_F125_NodesFirstDiamondFanIn_Passes() => AssertAllVisibleEdgesPerpendicular(
            """
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
            """,
            "F125");

    [Test]
    public void ValidateAllVisibleEdges_F37_CrossSubgraph_Passes() => AssertAllVisibleEdgesPerpendicular(
            """
            flowchart LR
            subgraph Box["Vertical stack inside horizontal diagram"]
              direction TB
              S1[Step 1] --> S2[Step 2] --> S3[Step 3]
            end
            Start[Start] --> S1
            S3 --> End[End]
            """,
            "F37");

    [Test]
    public void ValidateAllVisibleEdges_F61_GroupedTwoByTwo_AllowsDiagonalCrossEdges() => AssertAllVisibleEdgesPerpendicular(
            """
            flowchart TB
            A & B --> C & D
            """,
            "F61");

    [TestCaseSource(nameof(CatalogueFixtureMermaidBlocks))]
    public void ValidateAllVisibleEdges_CatalogueBatch_Passes(string FixtureId, string Mermaid) => AssertAllVisibleEdgesPerpendicular(Mermaid, FixtureId);

    private static IEnumerable<TestCaseData> CatalogueFixtureMermaidBlocks()
    {
        yield return new TestCaseData("F11", """
            flowchart LR
            A[Rectangle] --> B(Rounded rectangle) --> C{Diamond decision} --> D((Circle))
            """).SetName("Catalogue_F11");

        yield return new TestCaseData("F31", """
            flowchart TB
            Hub[Hub] --> A[Branch A]
            Hub --> B[Branch B]
            Hub --> C[Branch C]
            """).SetName("Catalogue_F31");

        yield return new TestCaseData("F36", """
            flowchart TB
            subgraph Inner["Inner group"]
              I1[Alpha] --> I2[Beta]
            end
            X[Before] --> I1
            I2 --> Y[After]
            """).SetName("Catalogue_F36");

        yield return new TestCaseData("F38", """
            flowchart TB
            subgraph Pipeline["Pipeline"]
              direction LR
              P1[Read] --> P2[Map] --> P3[Write]
            end
            Ingress[Ingress] --> P1
            P3 --> Egress[Egress]
            """).SetName("Catalogue_F38");
    }

    private static void AssertAllVisibleEdgesPerpendicular(string Mermaid, string FixtureId)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Mermaid);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True, FixtureId);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        List<string> Failures = [];

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

            string EdgeLabel = Edge.SourceId + "->" + Edge.TargetId;
            if (!Resolved)
            {
                Failures.Add(EdgeLabel + ": route could not be resolved");
                continue;
            }

            bool AllowDiagonalCrossEdge = RouteKind == FlowchartEdgeRouteKind.GroupedTwoByTwo;
            string? SourceShape = NodeById.TryGetValue(Edge.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
            string? TargetShape = NodeById.TryGetValue(Edge.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;

            bool Valid = FlowchartPerpendicularAttachValidator.TryValidateGuidePoints(
                LayoutPoints,
                BoundsById[Edge.SourceId],
                SourceShape,
                BoundsById[Edge.TargetId],
                TargetShape,
                AllowDiagonalCrossEdge,
                out string FailureMessage);

            if (!Valid)
            {
                Failures.Add(EdgeLabel + " (" + RouteKind + "): " + FailureMessage);
            }

            if (Edge.MarkerEnd == EdgeEndpointMarker.Point
                && !FlowchartMarkerTangentValidator.TryValidateEndpointMarkerTangent(
                    LayoutPoints,
                    RouteKind,
                    Direction,
                    AtTargetEnd: true,
                    out string TargetTangentFailure))
            {
                Failures.Add(EdgeLabel + " target marker: " + TargetTangentFailure);
            }

            if (Edge.MarkerStart == EdgeEndpointMarker.Point
                && !FlowchartMarkerTangentValidator.TryValidateEndpointMarkerTangent(
                    LayoutPoints,
                    RouteKind,
                    Direction,
                    AtTargetEnd: false,
                    out string SourceTangentFailure))
            {
                Failures.Add(EdgeLabel + " source marker: " + SourceTangentFailure);
            }
        }

        Assert.That(Failures, Is.Empty, FixtureId + " perpendicular attach failures:\n" + string.Join("\n", Failures));
    }

    private static FlowchartLayoutNodeBounds BuildBounds(
        string NodeId,
        double CenterX,
        double CenterY,
        double Width,
        double Height) => new FlowchartLayoutNodeBounds
        {
            NodeId = NodeId,
            CenterX = CenterX,
            CenterY = CenterY,
            Width = Width,
            Height = Height
        };
}

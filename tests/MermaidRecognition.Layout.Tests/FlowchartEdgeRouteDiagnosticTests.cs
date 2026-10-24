using System.Linq;
using System.Text;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Generic layout-space routing diagnostics.
/// SVG comparison and manual inspection remain the final visual validation.
/// These checks explain which routing rule was violated when visual comparison fails.
/// </summary>
[TestFixture]
public sealed class FlowchartEdgeRouteDiagnosticTests
{
    private const string F58_FLOWCHART = """
        flowchart TD
        Start([Start]) --> Ingest[Ingest file]
        Ingest --> Validate{Valid markdown?}
        Validate -->|No| Reject[Reject with error]
        Validate -->|Yes| Parse[Parse blocks]
        Parse --> HasMermaid{Mermaid fence?}
        HasMermaid -->|Yes| Render[Render SVG]
        HasMermaid -->|No| Word[Build DOCX]
        Render --> Word
        DoArchive{Archive?}
        Word --> DoArchive
        DoArchive -->|Yes| Persist
        DoArchive -->|No| Done
        Reject --> Done
        Persist[(Optional archive)] --> Done
        """;

    [Test]
    public void Analyze_F58RegressionFixture_ReportsNoStructuralRoutingDefects()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        List<FlowchartEdgeRouteDiagnosticFinding> Findings = FlowchartEdgeRouteDiagnosticAnalyzer.Analyze(
            Contract,
            Layout,
            Direction);

        List<FlowchartEdgeRouteDiagnosticFinding> StructuralFindings = Findings
            .Where(Finding => Finding.Kind == FlowchartEdgeRouteDiagnosticKind.LongDiagonalCrossing
                || Finding.Kind == FlowchartEdgeRouteDiagnosticKind.UnnecessaryHardElbow
                || Finding.Kind == FlowchartEdgeRouteDiagnosticKind.CentreSpineRegression
                || Finding.Kind == FlowchartEdgeRouteDiagnosticKind.HookOrOvershoot
                || Finding.Kind == FlowchartEdgeRouteDiagnosticKind.NodePassThrough)
            .ToList();

        if (StructuralFindings.Count > 0)
        {
            string Report = FlowchartEdgeRouteDiagnosticReportFormatter.FormatReport(
                StructuralFindings,
                "F58 structural routing checks");
            Assert.Fail(Report);
        }
    }

    [Test]
    public void Analyze_F58RegressionFixture_DoneMergeHasNoPortAmbiguity()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        List<FlowchartEdgeRouteDiagnosticFinding> Findings = FlowchartEdgeRouteDiagnosticAnalyzer.Analyze(
            Contract,
            Layout,
            Direction);

        Assert.That(
            Findings.Any(
                Finding => Finding.Kind == FlowchartEdgeRouteDiagnosticKind.MergeAmbiguity
                    && string.Equals(Finding.TargetNodeId, "Done", StringComparison.Ordinal)),
            Is.False,
            "Reject and Persist sit either side of Done and enter its facing sides (spec §15.0C.1), so they never share a target port.");
    }

    [Test]
    public void Analyze_F58RegressionFixture_ReportsReadableFindingMetadata()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        List<FlowchartEdgeRouteDiagnosticFinding> Findings = FlowchartEdgeRouteDiagnosticAnalyzer.Analyze(
            Contract,
            Layout,
            Direction);

        foreach (FlowchartEdgeRouteDiagnosticFinding Finding in Findings)
        {
            Assert.That(Finding.EdgeKey, Does.Contain("->"));
            Assert.That(Finding.Reason, Is.Not.Empty);
            StringBuilder Line = new StringBuilder(Finding.FormatLine());
            Assert.That(Line.ToString(), Does.Contain(Finding.SourceNodeId));
            Assert.That(Line.ToString(), Does.Contain(Finding.TargetNodeId));
            Assert.That(Line.ToString(), Does.Contain(Finding.SemanticRouteClass.ToString()));
            Assert.That(Line.ToString(), Does.Contain(Finding.RouteKind.ToString()));
        }
    }

    [Test]
    public void AnalyzeEdgeRoute_SyntheticLongDiagonalCrossing_DetectsSpineColumnViolation()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\nA[Input] --> M[Middle] --> Z[Output]\nA --> Z");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract,
            new FlowchartRenderContext { UseCellRegions = true }, out FlowchartLayoutResult Layout), Is.True);

        // Declare a straight three-rank spine atomically. Automatic fan-side
        // placement may legitimately put the skip edge's ends in one row.
        var Owners = Layout.CellNodes!;
        int Pitch = Owners.Max(Node => Node.CellNode!.Region.RowSpan);
        Assert.That(FlowchartCellRepair.TryAssign(Owners, new Dictionary<string, FlowchartCellRegion>
        {
            ["A"] = new(0, 0, Pitch, 1), ["M"] = new(Pitch, 0, Pitch, 1), ["Z"] = new(2 * Pitch, 0, Pitch, 1)
        }), Is.True);
        Layout.NodeBoundsMutable.Clear();
        Layout.NodeBoundsMutable.AddRange(Owners.Select(Node => Node.CellNode!.Bounds(Node.GeometryProvider)));

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        EdgeContract ValidateRejectEdge = Contract.Edges.First(
            Edge => Edge.SourceId == "A" && Edge.TargetId == "Z");
        var Middle = Layout.NodeBounds.Single(Box => Box.NodeId == "M");

        List<(double X, double Y)> BadDiagonalRoute =
        [
            // Cross the actual cell-owned middle body, independent of the
            // historical F58 pixel positions or its changed branch ranks.
            (Middle.CenterX + Middle.Width, Middle.CenterY + Middle.Height),
            (Middle.CenterX - Middle.Width, Middle.CenterY - Middle.Height)
        ];

        List<FlowchartEdgeRouteDiagnosticFinding> Findings = FlowchartEdgeRouteDiagnosticAnalyzer.AnalyzeEdgeRoute(
            ValidateRejectEdge,
            BadDiagonalRoute,
            FlowchartEdgeRouteKind.Decision,
            FlowchartEdgeSemanticRouteClass.LongExceptionRoute,
            Contract,
            Layout,
            Direction);

        Assert.That(
            Findings.Any(Finding => Finding.Kind == FlowchartEdgeRouteDiagnosticKind.LongDiagonalCrossing),
            Is.True,
            "Synthetic diagonal chord through the spine column should be reported as a long diagonal crossing.");
    }

    [Test]
    public void AnalyzeEdgeRoute_SyntheticCentreSpineDetour_DetectsCentreSpineRegression()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TD
            A[Top] --> B[Bottom]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);

        EdgeContract Edge = Contract.Edges.Single();
        FlowchartLayoutNodeBounds SourceBounds = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A");
        FlowchartLayoutNodeBounds TargetBounds = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "B");

        List<(double X, double Y)> DetourRoute =
        [
            (SourceBounds.CenterX, SourceBounds.CenterY - (SourceBounds.Height / 2.0) - 2.0),
            (SourceBounds.CenterX + 80.0, SourceBounds.CenterY - 40.0),
            (TargetBounds.CenterX + 80.0, TargetBounds.CenterY + 40.0),
            (TargetBounds.CenterX, TargetBounds.CenterY + (TargetBounds.Height / 2.0) + 2.0)
        ];

        List<FlowchartEdgeRouteDiagnosticFinding> Findings = FlowchartEdgeRouteDiagnosticAnalyzer.AnalyzeEdgeRoute(
            Edge,
            DetourRoute,
            FlowchartEdgeRouteKind.Curved,
            FlowchartEdgeSemanticRouteClass.CentreSpine,
            Contract,
            Layout,
            Direction);

        Assert.That(
            Findings.Any(Finding => Finding.Kind == FlowchartEdgeRouteDiagnosticKind.CentreSpineRegression),
            Is.True,
            "Synthetic detour around an aligned unobstructed pair should be reported as centre-spine regression.");
    }

    [Test]
    public void AnalyzeEdgeRoute_SyntheticNodePassThrough_DetectsInteriorCrossing()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);

        EdgeContract Edge = Contract.Edges.First(
            Candidate => Candidate.SourceId == "Render" && Candidate.TargetId == "Word");
        FlowchartLayoutNodeBounds ParseBounds = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "Parse");

        List<(double X, double Y)> ThroughBodyRoute =
        [
            (ParseBounds.CenterX - ParseBounds.Width / 2 - DiagramGridMetrics.GRID_UNIT_PX, ParseBounds.CenterY),
            (ParseBounds.CenterX + ParseBounds.Width / 2 + DiagramGridMetrics.GRID_UNIT_PX, ParseBounds.CenterY)
        ];

        List<FlowchartEdgeRouteDiagnosticFinding> Findings = FlowchartEdgeRouteDiagnosticAnalyzer.AnalyzeEdgeRoute(
            Edge,
            ThroughBodyRoute,
            FlowchartEdgeRouteKind.Curved,
            FlowchartEdgeSemanticRouteClass.MergeBack,
            Contract,
            Layout,
            Direction);

        Assert.That(
            Findings.Any(Finding => Finding.Kind == FlowchartEdgeRouteDiagnosticKind.NodePassThrough),
            Is.True,
            "Synthetic middle segment through an unrelated node body should be reported as node pass-through.");
    }

    [Test]
    public void Analyze_F35_SpineAlignedStraightDecisionEdge_DoesNotReportCentreSpineRegression()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            A[Path A] --> D{Merge?}
            B[Path B] --> D
            C[Path C] --> D
            D -->|yes| E[Continue]
            D -->|no| F[Stop]
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        List<FlowchartEdgeRouteDiagnosticFinding> Findings = FlowchartEdgeRouteDiagnosticAnalyzer.Analyze(
            Contract,
            Layout,
            Direction);

        Assert.That(
            Findings.Any(
                Finding => Finding.Kind == FlowchartEdgeRouteDiagnosticKind.CentreSpineRegression
                    && string.Equals(Finding.EdgeKey, "B->D", StringComparison.Ordinal)),
            Is.False,
            "Path B to Merge? is a straight spine-aligned connector and must not be flagged as centre-spine regression.");
    }
}

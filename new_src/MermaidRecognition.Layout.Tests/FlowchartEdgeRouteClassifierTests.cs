using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartEdgeRouteClassifierTests
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
    public void ClassifyWithContext_F58_AssignsExpectedSemanticRouteClasses()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);

        Assert.That(
            FlowchartSpineGraphHelper.TryBuildContext(Contract, BoundsById, Direction, Options, out FlowchartSpineLayoutContext? SpineContext),
            Is.True);
        Assert.That(SpineContext, Is.Not.Null);
        Assert.That(SpineContext!.SpineNodeIds, Does.Contain("Done"));

        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "Start", "Ingest", FlowchartEdgeSemanticRouteClass.CentreSpine);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "Ingest", "Validate", FlowchartEdgeSemanticRouteClass.CentreSpine);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "Validate", "Reject", FlowchartEdgeSemanticRouteClass.LongExceptionRoute);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "Validate", "Parse", FlowchartEdgeSemanticRouteClass.CentreSpine);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "Parse", "HasMermaid", FlowchartEdgeSemanticRouteClass.CentreSpine);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "HasMermaid", "Render", FlowchartEdgeSemanticRouteClass.LocalDecisionBranch);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "HasMermaid", "Word", FlowchartEdgeSemanticRouteClass.LocalDecisionBranch);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "Render", "Word", FlowchartEdgeSemanticRouteClass.MergeBack);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "Word", "DoArchive", FlowchartEdgeSemanticRouteClass.CentreSpine);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "DoArchive", "Persist", FlowchartEdgeSemanticRouteClass.CentreSpine);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "DoArchive", "Done", FlowchartEdgeSemanticRouteClass.SkipBypass);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "Reject", "Done", FlowchartEdgeSemanticRouteClass.MergeBack);
        AssertSemanticClass(Contract, BoundsById, NodeById, Direction, SpineContext, "Persist", "Done", FlowchartEdgeSemanticRouteClass.CentreSpine);
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F58_CentreSpineEdges_UseTwoPointShapeBoundaryAttach()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        AssertCentreSpineRoute(Contract, BoundsById, NodeById, Direction, "Start", "Ingest");
        AssertCentreSpineRoute(Contract, BoundsById, NodeById, Direction, "Validate", "Parse");
        AssertCentreSpineRoute(Contract, BoundsById, NodeById, Direction, "DoArchive", "Persist");
        AssertCentreSpineRoute(Contract, BoundsById, NodeById, Direction, "Persist", "Done");
    }

    [Test]
    public void TryBuildCorridor_F58_CoversSpineColumnForObstacleTests()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);

        Assert.That(
            FlowchartSpineGraphHelper.TryBuildContext(Contract, BoundsById, Direction, Options, out FlowchartSpineLayoutContext? SpineContext),
            Is.True);
        Assert.That(
            FlowchartSpineCorridorHelper.TryBuildCorridor(BoundsById, SpineContext!, out FlowchartSpineCorridorBounds Corridor),
            Is.True);

        FlowchartLayoutNodeBounds ValidateBounds = BoundsById["Validate"];
        FlowchartLayoutNodeBounds RejectBounds = BoundsById["Reject"];
        Assert.That(
            FlowchartRouteObstacleHelper.CentreChordIntersectsSpineCorridor(ValidateBounds, RejectBounds, Corridor),
            Is.True,
            "Validate→Reject chord crosses the spine corridor and should classify as a long exception route");
    }

    private static void AssertSemanticClass(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        FlowchartSpineLayoutContext SpineContext,
        string SourceId,
        string TargetId,
        FlowchartEdgeSemanticRouteClass ExpectedClass)
    {
        EdgeContract Edge = Contract.Edges.First(
            Candidate => Candidate.SourceId == SourceId && Candidate.TargetId == TargetId);
        FlowchartEdgeSemanticRouteClass ActualClass = FlowchartEdgeRouteClassifier.Classify(
            Edge,
            Contract,
            BoundsById,
            NodeById,
            Direction,
            SpineContext);

        Assert.That(
            ActualClass,
            Is.EqualTo(ExpectedClass),
            SourceId + "->" + TargetId + " semantic route class");
    }

    private static void AssertCentreSpineRoute(
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
            out FlowchartEdgeRouteKind RouteKind);

        Assert.That(Resolved, Is.True, SourceId + "->" + TargetId + " should resolve");
        Assert.That(RouteKind, Is.EqualTo(FlowchartEdgeRouteKind.Straight), SourceId + "->" + TargetId + " route kind");
        Assert.That(LayoutPoints.Count, Is.EqualTo(2), SourceId + "->" + TargetId + " should be a two-point centre-spine route");
        Assert.That(
            Math.Abs(LayoutPoints[0].X - LayoutPoints[1].X),
            Is.LessThan(1.0),
            SourceId + "->" + TargetId + " should stay on the spine column");

        if (string.Equals(SourceId, "Persist", System.StringComparison.Ordinal))
        {
            FlowchartLayoutNodeBounds PersistBounds = BoundsById["Persist"];
            Assert.That(
                FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    LayoutPoints[0].X,
                    LayoutPoints[0].Y,
                    LayoutPoints[1].X,
                    LayoutPoints[1].Y,
                    PersistBounds,
                    InsetPx: 4.0),
                Is.False,
                "Persist→Done should not pass through the cylinder body");
        }
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F58_SemanticRoutes_UseExpectedPathShapes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        AssertSemanticRoutePoints(Contract, BoundsById, NodeById, Direction, "Validate", "Reject", 5, FlowchartEdgeRouteKind.Corridor);
        AssertSemanticRoutePoints(Contract, BoundsById, NodeById, Direction, "HasMermaid", "Render", 5);
        AssertSemanticRoutePoints(Contract, BoundsById, NodeById, Direction, "HasMermaid", "Word", 5, FlowchartEdgeRouteKind.Decision);
        AssertSemanticRoutePoints(Contract, BoundsById, NodeById, Direction, "DoArchive", "Done", 6, FlowchartEdgeRouteKind.Corridor);
        AssertSemanticRoutePoints(Contract, BoundsById, NodeById, Direction, "Render", "Word", 6, FlowchartEdgeRouteKind.Corridor);

        EdgeContract ValidateRejectEdge = Contract.Edges.First(
            Edge => Edge.SourceId == "Validate" && Edge.TargetId == "Reject");
        bool ValidateRejectResolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            ValidateRejectEdge,
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> ValidateRejectPoints,
            out FlowchartEdgeRouteKind ValidateRejectRouteKind);
        Assert.That(ValidateRejectResolved, Is.True);
        Assert.That(ValidateRejectRouteKind, Is.EqualTo(FlowchartEdgeRouteKind.Corridor));

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        Assert.That(
            FlowchartSpineGraphHelper.TryBuildContext(Contract, BoundsById, Direction, Options, out FlowchartSpineLayoutContext? SpineContext),
            Is.True);
        Assert.That(
            FlowchartSpineCorridorHelper.TryBuildCorridor(BoundsById, SpineContext!, out FlowchartSpineCorridorBounds Corridor),
            Is.True);
        double CorridorLaneX = ValidateRejectPoints[2].X;
        Assert.That(
            CorridorLaneX,
            Is.LessThan(Corridor.Left - 8.0),
            "Validate→Reject corridor lane should stay outside the spine column");
        Assert.That(
            Math.Abs(CorridorLaneX - ValidateRejectPoints[0].X),
            Is.GreaterThan(120.0),
            "Validate→Reject corridor lane should route in the far margin, not near the source column");

        EdgeContract DoArchiveDoneEdge = Contract.Edges.First(
            Edge => Edge.SourceId == "DoArchive" && Edge.TargetId == "Done");
        bool DoArchiveDoneResolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
            DoArchiveDoneEdge,
            Contract,
            BoundsById,
            NodeById,
            Direction,
            out List<(double X, double Y)> DoArchiveDonePoints,
            out _);
        Assert.That(DoArchiveDoneResolved, Is.True);
        Assert.That(
            DoArchiveDonePoints[2].X,
            Is.GreaterThan(Corridor.Right),
            "DoArchive→Done skip bypass should route outside the spine column on the source side");
    }

    private static void AssertSemanticRoutePoints(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId,
        string TargetId,
        int ExpectedPointCount,
        FlowchartEdgeRouteKind? ExpectedRouteKind = null)
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
        Assert.That(LayoutPoints.Count, Is.EqualTo(ExpectedPointCount), SourceId + "->" + TargetId + " point count");
        if (ExpectedRouteKind.HasValue)
        {
            Assert.That(RouteKind, Is.EqualTo(ExpectedRouteKind.Value), SourceId + "->" + TargetId + " route kind");
        }
    }
}

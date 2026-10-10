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
    public void TryResolveLayoutGuidePoints_F58_ColumnAlignedEdges_AreStraight()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        AssertCentreSpineRoute(Contract, BoundsById, NodeById, Direction, "Start", "Ingest");
        AssertCentreSpineRoute(Contract, BoundsById, NodeById, Direction, "Ingest", "Validate");
        AssertCollinearStraightRoute(Contract, BoundsById, NodeById, Direction, "Parse", "HasMermaid");
        AssertCollinearStraightRoute(Contract, BoundsById, NodeById, Direction, "Word", "DoArchive");
    }

    /// <summary>
    /// Checks a column-aligned edge is one straight vertical run, with or without terminal stub points (spec §15.0A).
    /// </summary>
    private static void AssertCollinearStraightRoute(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        Dictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        string SourceId,
        string TargetId)
    {
        EdgeContract Edge = Contract.Edges.First(Candidate => Candidate.SourceId == SourceId && Candidate.TargetId == TargetId);
        bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(Edge, Contract, BoundsById, NodeById, Direction, out List<(double X, double Y)> LayoutPoints, out _);

        Assert.That(Resolved, Is.True, $"{SourceId}->{TargetId} should resolve");
        foreach ((double X, double Y) Point in LayoutPoints)
        {
            Assert.That(Point.X, Is.EqualTo(BoundsById[SourceId].CenterX).Within(1.0), $"{SourceId}->{TargetId} should stay on the shared column");
        }
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
    }

    [Test]
    public void TryResolveLayoutGuidePoints_F58_Routes_LeaveFacingShoulderAndClearOtherNodes()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F58_FLOWCHART);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        foreach (EdgeContract Edge in Contract.Edges)
        {
            bool Resolved = FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                Edge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> LayoutPoints,
                out FlowchartEdgeRouteKind RouteKind,
                Layout);
            string EdgeKey = $"{Edge.SourceId}->{Edge.TargetId}";
            Assert.That(Resolved, Is.True, $"{EdgeKey} should resolve");
            AssertRouteClearsUnrelatedNodes(EdgeKey, Edge, LayoutPoints, BoundsById);

            if (!FlowchartDiamondAttachHelper.IsDiamondShape(NodeById[Edge.SourceId].Shape))
            {
                continue;
            }

            FlowchartLayoutNodeBounds SourceBounds = BoundsById[Edge.SourceId];
            FlowchartLayoutNodeBounds TargetBounds = BoundsById[Edge.TargetId];
            double TargetSide = Math.Sign(TargetBounds.CenterX - SourceBounds.CenterX);
            if (Math.Abs(TargetSide) < 0.5)
            {
                Assert.That(RouteKind, Is.EqualTo(FlowchartEdgeRouteKind.Straight), $"{EdgeKey} collinear branch stays straight");
                continue;
            }
            Assert.That(RouteKind, Is.EqualTo(FlowchartEdgeRouteKind.Decision), $"{EdgeKey} route kind");
            Assert.That((LayoutPoints[0].X - SourceBounds.CenterX) * TargetSide, Is.GreaterThan(4.0), $"{EdgeKey} should leave the diamond shoulder that faces its target");
            Assert.That(LayoutPoints[LayoutPoints.Count - 1].X, Is.EqualTo(TargetBounds.CenterX).Within(1.0), $"{EdgeKey} should enter the top centre of its target");
        }
    }

    /// <summary>
    /// Checks that no guide segment cuts through a node other than the edge's own source and target.
    /// </summary>
    private static void AssertRouteClearsUnrelatedNodes(string EdgeKey, EdgeContract Edge, List<(double X, double Y)> LayoutPoints, Dictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            if (Bounds.NodeId == Edge.SourceId || Bounds.NodeId == Edge.TargetId)
            {
                continue;
            }

            for (int Index = 1; Index < LayoutPoints.Count; Index++)
            {
                bool Crosses = FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(LayoutPoints[Index - 1].X, LayoutPoints[Index - 1].Y, LayoutPoints[Index].X, LayoutPoints[Index].Y, Bounds, InsetPx: 4.0);
                Assert.That(Crosses, Is.False, $"{EdgeKey} should not pass through {Bounds.NodeId}");
            }
        }
    }
}

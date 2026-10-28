using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// F34 retry-loop path rendering regression tests.
/// </summary>
[TestFixture]
public sealed class FlowchartRetryLoopPathTests
{
    [Test]
    public void BuildRetryLoopPathData_OmitsCollinearPointsAndRoundsEachCorner()
    {
        List<(double X, double Y)> GuidePoints =
        [
            (100.0, 50.0),
            (112.0, 50.0),
            (112.0, 70.0),
            (112.0, 120.0),
            (300.0, 120.0),
            (300.0, 90.0),
            (300.0, 80.0)
        ];

        string PathData = FlowchartCurvePathBuilder.BuildRetryLoopPathData(GuidePoints);

        // Spec §15.0C.4: collinear guide points are dropped and each of the three corners becomes one quarter-circle cubic.
        Assert.That(PathData, Does.Not.Contain("112,70"));
        Assert.That(PathData, Does.StartWith("M100,50"));
        Assert.That(PathData, Does.EndWith("L300,80"));
        Assert.That(PathData.Count(Character => Character == 'C'), Is.EqualTo(3));
        Assert.That(PathData, Does.Contain(" 140,120L272,120C"), "The bottom run should start and end 28 px (the corner radius) from each corner.");
    }

    [Test]
    public void RenderToSvg_F34_RetryLoop_PassesRenderedPathValidation()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[Receive] --> B{OK?}
            B -->|Yes| C[Done]
            B -->|No| D[Retry]
            D --> A
            """);

        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        List<string> PathData = [.. Regex.Matches(
                Svg,
                @"<path d=""([^""]+)"" fill=""none"" stroke=""",
                RegexOptions.CultureInvariant)
            .Select(Match => Match.Groups[1].Value)];

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes.ToDictionary(Node => Node.Id, Node => Node);
        EdgeContract RetryEdge = Contract.Edges.Single(Edge => Edge.SourceId == "D" && Edge.TargetId == "A");
        Assert.That(
            FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                RetryEdge,
                Contract,
                BoundsById,
                NodeById,
                Direction,
                out List<(double X, double Y)> GuidePoints,
                out FlowchartEdgeRouteKind RouteKind),
            Is.True);

        int PathIndex = 0;
        string RetryLoopPath = string.Empty;
        foreach (EdgeContract Edge in Contract.Edges.Where(
                     Candidate => !string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))
        {
            if (!FlowchartSvgEdgeStrokeClassifier.RendersAsSvgPath(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction))
            {
                continue;
            }

            while (PathIndex < PathData.Count
                && !PathData[PathIndex].StartsWith("M", StringComparison.OrdinalIgnoreCase))
            {
                PathIndex++;
            }

            if (PathIndex >= PathData.Count)
            {
                break;
            }

            string CandidatePath = PathData[PathIndex];
            PathIndex++;
            if (Edge.SourceId == "D" && Edge.TargetId == "A")
            {
                RetryLoopPath = CandidatePath;
                break;
            }
        }

        Assert.That(RetryLoopPath, Is.Not.Empty, "Retry loop path was not rendered.");
        FlowchartRenderedPathValidator.FlowchartRenderedPathValidationResult Result =
            FlowchartRenderedPathValidator.Validate(
                RetryLoopPath,
                RetryEdge,
                Contract,
                Layout,
                GuidePoints,
                RouteKind,
                Direction);

        Assert.That(Result.ClearsUnrelatedNodeBodies, Is.True, Result.FailureMessage ?? Result.IntersectedNodeId);
        Assert.That(Result.PerpendicularAttach, Is.True, Result.FailureMessage);
        Assert.That(Result.NoVisibleKink, Is.True, Result.FailureMessage);
    }
}

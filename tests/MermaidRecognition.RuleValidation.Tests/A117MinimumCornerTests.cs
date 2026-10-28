using MDConverter;
using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Renders the named A117 links through the DOCX image entry point and checks their geometric minimum corners.</summary>
[TestFixture, NonParallelizable]
public sealed class A117MinimumCornerTests : ProductionCatalogueTestFixture
{
    /// <summary>Checks every requested relationship without exempting a named line or loosening its expected corner count.</summary>
    [Test]
    public void NamedLinesHaveTheirFewestLegalCornersInTheProductionRender()
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A117");
        DiagramContract? Contract = null; FlowchartLayoutResult? Layout = null;
        Action<DiagramContract, FlowchartLayoutResult>? Previous = DiagramGridLayout.PublishedLayoutObserver;
        DiagramGridLayout.PublishedLayoutObserver = (Source, Result) =>
        {
            Previous?.Invoke(Source, Result);
            if (!Source.Nodes.Any(Node => Node.Id == "wpMigrate"))
            {
                return;
            }

            Contract = Source; Layout = Result;
        };
        try
        {
            byte[]? Png = MermaidDiagramExport.TryRenderPng(Scenario.Source);
            Assert.That(Png, Is.Not.Null.And.Not.Empty);
            Assert.That(Layout, Is.Not.Null);
            Assert.That(Layout!.RenderContext.UseCellRegions, Is.True);
            Assert.That(FlowchartRouteRuleChecker.Check(Contract!, Layout).Where(Finding => Finding.Rule is "10" or "4.1"), Is.Empty);
            (string, string)[] Pairs = [ ("g1", "baseline"), ("g1", "t1"), ("g2", "t1"), ("g2", "t2"), ("g2", "legacyPayroll"),
                ("g3", "t2"), ("g3", "target"), ("g3", "rosterApi"), ("rostering", "rosterApi"), ("programme", "wpMigrate"),
                ("cloudPayroll", "awardRules"), ("awardRules", "cheaper"), ("target", "cheaper"), ("baseline", "legacyPayroll"),
                ("programme", "wpCutover"), ("goLive", "wpRoster") ];
            using IDisposable Active = FlowchartRenderContext.Activate(Layout.RenderContext);
            Assert.Multiple(() =>
            {
                foreach ((string, string) Pair in Pairs)
                {
                    FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Line => Line.SourceId == Pair.Item1 && Line.TargetId == Pair.Item2);
                    int Actual = FlowchartLayoutScore.CountCorners(Route.Points);
                    int Minimum = MinimumCorners(Contract!, Layout, Route);
                    TestContext.Out.WriteLine($"A117_NAMED_MINIMUM|{Route.EdgeId}|actual={Actual}|minimum={Minimum}");
                    Assert.That(Actual, Is.EqualTo(Minimum), Route.EdgeId + " must use its fewest legal corners; port/track contention needs a joint repair.");
                }
            });
        }
        finally { DiagramGridLayout.PublishedLayoutObserver = Previous; }
    }

    /// <summary>Finds a lower bound from legal connection-body centre ports on all side pairs, independent of the retained allocation.</summary>
    /// <param name="Contract">Production declarations and geometry providers.</param>
    /// <param name="Layout">Completed production node and frame obstacles.</param>
    /// <param name="Original">Line under test; honoured side hints remain fixed.</param>
    /// <returns>The least corner count proved by the ordinary allocated-port router on the same fixed bodies.</returns>
    private static int MinimumCorners(DiagramContract Contract, FlowchartLayoutResult Layout, FlowchartLayoutEdgeRoute Original)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).GroupBy(Box => Box.NodeId).ToDictionary(Group => Group.Key, Group => Group.First());
        Dictionary<string, NodeContract> Nodes = Contract.Nodes.ToDictionary(Node => Node.Id);
        int Minimum = FlowchartLayoutScore.CountCorners(Original.Points);
        bool Hinted = DiagramGridLayout.FindContractEdge(Contract, Original)?.RoutingHint?.IsActive == true;
        foreach (string Exit in new[] { "top", "right", "bottom", "left" })
        {
            foreach (string Entry in new[] { "top", "right", "bottom", "left" })
        {
            if (Hinted && (Exit != Original.ExitSide || Entry != Original.EntrySide))
                {
                    continue;
                }

                (double X, double Y, double Nx, double Ny)? A = Port(Nodes.GetValueOrDefault(Original.SourceId), Bounds[Original.SourceId], Exit);
                (double X, double Y, double Nx, double Ny)? B = Port(Nodes.GetValueOrDefault(Original.TargetId), Bounds[Original.TargetId], Entry);
            if (A == null || B == null)
                {
                    continue;
                }

                FlowchartLayoutEdgeRoute Trial = new FlowchartLayoutEdgeRoute { EdgeId = Original.EdgeId, SourceId = Original.SourceId, TargetId = Original.TargetId,
                ExitSide = Exit, EntrySide = Entry, ExitNormalX = A.Value.Nx, ExitNormalY = A.Value.Ny,
                EntryNormalX = B.Value.Nx, EntryNormalY = B.Value.Ny };
            Trial.Points.Add(new() { X = A.Value.X, Y = A.Value.Y }); Trial.Points.Add(new() { X = B.Value.X, Y = B.Value.Y });
                List<(double X, double Y)> Path = FlowchartAllocatedPortFallback.Build(Contract, Bounds, Nodes, Trial, true);
            if (!FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Trial, Path))
                {
                    continue;
                }

                Minimum = Math.Min(Minimum, FlowchartLayoutScore.CountCorners(Path.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }).ToArray()));
            if (Minimum == 0)
                {
                    return 0;
                }
            }
        }

        return Minimum;
    }

    /// <summary>Uses the renderer's connection boundary, with a rectangular frame fallback, in current routing coordinates.</summary>
    /// <param name="Node">Optional renderer-owned shape definition.</param>
    /// <param name="Box">Final measured bounds.</param>
    /// <param name="Side">Candidate side at its centre.</param>
    /// <returns>Exact boundary point and normal, or none when the shape rejects that side.</returns>
    private static (double X, double Y, double Nx, double Ny)? Port(NodeContract? Node, FlowchartLayoutNodeBounds Box, string Side)
    {
        if (Node?.GeometryProvider is { } Provider)
        {
            if (!Provider.Create(Box.Width, Box.Height).ConnectionBoundary.TryPort(Side, 0.5, out NodeGeometryPort Result))
            {
                return null;
            }

            return (Box.CenterX + Result.X, Box.CenterY - Result.Y, Result.NormalX, -Result.NormalY);
        }
        return Side switch
        {
            "left" => (Box.CenterX - (Box.Width / 2), Box.CenterY, -1, 0), "right" => (Box.CenterX + (Box.Width / 2), Box.CenterY, 1, 0),
            "top" => (Box.CenterX, Box.CenterY + (Box.Height / 2), 0, 1), _ => (Box.CenterX, Box.CenterY - (Box.Height / 2), 0, -1)
        };
    }
}

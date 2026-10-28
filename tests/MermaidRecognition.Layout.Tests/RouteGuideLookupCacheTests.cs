using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Prevents per-edge canvas queries from rebuilding a diagram or reusing guides across changed geometry.</summary>
[TestFixture]
[NonParallelizable]
public sealed class RouteGuideLookupCacheTests
{
    /// <summary>Reuses the same complete guide family for repeated lookups in one render and invalidates it after a bound changes.</summary>
    [Test]
    public void RepeatedLookupsReuseOnlyMatchingGeometry()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart LR\nA[A] --> B[B]\nA --> C[C]");
        Dictionary<string, NodeContract> Nodes = Contract.Nodes.ToDictionary(Node => Node.Id);
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>
        {
            ["A"] = new() { NodeId = "A", CenterX = 90, CenterY = 90, Width = 120, Height = 60 },
            ["B"] = new() { NodeId = "B", CenterX = 330, CenterY = 90, Width = 120, Height = 60 },
            ["C"] = new() { NodeId = "C", CenterX = 330, CenterY = 270, Width = 120, Height = 60 }
        };
        FlowchartRenderContext Context = new FlowchartRenderContext();
        using IDisposable Scope = FlowchartRenderContext.Activate(Context);
        EdgeContract Edge = Contract.Edges[0];
        Assert.That(FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(Edge, Contract, Bounds, Nodes,
            FlowchartDirection.LeftToRight, out List<(double X, double Y)>? First, out _), Is.True);
        List<FlowchartResolvedGuide>? Cached = Context.CachedRouteGuides;
        Assert.That(Cached, Is.Not.Null);
        Assert.That(FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(Edge, Contract, Bounds, Nodes,
            FlowchartDirection.LeftToRight, out List<(double X, double Y)>? Second, out _), Is.True);
        Assert.That(Context.CachedRouteGuides, Is.SameAs(Cached));
        Assert.That(Second, Is.EqualTo(First));

        Bounds["B"] = new() { NodeId = "B", CenterX = 450, CenterY = 90, Width = 120, Height = 60 };
        Assert.That(FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(Edge, Contract, Bounds, Nodes,
            FlowchartDirection.LeftToRight, out List<(double X, double Y)>? Moved, out _), Is.True);
        Assert.That(Context.CachedRouteGuides, Is.Not.SameAs(Cached));
        Assert.That(Moved, Is.Not.EqualTo(First));
        Assert.That(Moved[^1].X, Is.EqualTo(390));
    }
}

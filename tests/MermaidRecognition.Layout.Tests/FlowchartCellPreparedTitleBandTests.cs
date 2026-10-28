using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Exercises trial-local prepared title geometry and reconstruction across candidate changes and rollback.</summary>
[TestFixture]
public sealed class FlowchartCellPreparedTitleBandTests
{
    /// <summary>Requires prepared and fresh routing to agree on paths and legality for multiple trials against fixed obstacles.</summary>
    /// <param name="Diamond">Whether the source uses the existing diamond endpoint filtering.</param>
    [TestCase(false), TestCase(true)]
    public void PreparedAndFreshRouteTrialsAreIdentical(bool Diamond)
    {
        string Source = Diamond ? "A{Source}" : "A[Source]";
        DiagramContract Contract = MermaidRecognizer.Parse($"flowchart LR\n subgraph g[Group]\n {Source} --> B[Target]\n end");
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal)
        {
            ["g"] = new() { NodeId = "g", CenterX = 150, CenterY = 120, Width = 600, Height = 240 },
            ["A"] = new() { NodeId = "A", CenterY = 120, Width = 120, Height = 60 },
            ["B"] = new() { NodeId = "B", CenterX = 300, CenterY = 120, Width = 120, Height = 60 }
        };
        Dictionary<string, NodeContract> Nodes = Contract.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute { SourceId = "A", TargetId = "B", ExitSide = "right", EntrySide = "left",
            ExitNormalX = 1, EntryNormalX = -1 };
        Route.Points.Add(new() { X = 60, Y = 120 }); Route.Points.Add(new() { X = 240, Y = 120 });
        FlowchartAllocatedPortFallback.PreparedGeometry Prepared = FlowchartAllocatedPortFallback.Prepare(Contract, Bounds);
        List<(double X, double Y)> Fresh = FlowchartAllocatedPortFallback.Build(Contract, Bounds, Nodes, Route, true);
        Assert.That(Fresh, Is.Not.Empty);
        Assert.That(FlowchartAllocatedPortFallback.Build(Contract, Bounds, Nodes, Route, true, Prepared), Is.EqualTo(Fresh));
        Assert.That(FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Fresh, Prepared), Is.True);
        foreach (double Track in new[] { 150.0, 210.0 })
        {
            (double X, double Y)[] Path = [(60, 120), (80, 120), (80, Track), (220, Track), (220, 120), (240, 120)];
            Assert.That(FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Path, Prepared),
                Is.EqualTo(FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Path)));
            Assert.That(FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route, Path, Prepared), Is.EqualTo(Track == 150));
        }
    }

    /// <summary>Checks reuse between trials with unchanged immutable fitted frame geometry.</summary>
    [Test]
    public void PreparedGeometryMatchesFreshBands()
    {
        (DiagramContract? Contract, Dictionary<string, FlowchartLayoutNodeBounds>? Bounds) = Fixture();
        IReadOnlyList<FlowchartFrameTitleBandHelper.Band> First = FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands;
        Assert.That(FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands, Is.EqualTo(First));
        Assert.That(First, Is.EqualTo(FlowchartFrameTitleBandHelper.Build(Contract, Bounds)));
    }

    /// <summary>Requires frame replacement and exact rollback to match fresh builds on the same mutable dictionary.</summary>
    [Test]
    public void ReplacedFrameAndRollbackInvalidatePreparedGeometry()
    {
        (DiagramContract? Contract, Dictionary<string, FlowchartLayoutNodeBounds>? Bounds) = Fixture();
        FlowchartLayoutNodeBounds Original = Bounds["g"];
        FlowchartFrameTitleBandHelper.Band[] First = [.. FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands];
        Bounds["g"] = new() { NodeId = "g", CenterY = 240, Width = 360, Height = 180 };
        IReadOnlyList<FlowchartFrameTitleBandHelper.Band> Trial = FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands;
        Assert.That(Trial, Is.Not.EqualTo(First));
        Assert.That(Trial, Is.EqualTo(FlowchartFrameTitleBandHelper.Build(Contract, Bounds)));
        Bounds["g"] = Original;
        Assert.That(FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands, Is.EqualTo(First));
    }

    /// <summary>Prevents a rejected title/configuration candidate from reusing its previously prepared band height.</summary>
    [Test]
    public void ChangedTitleAndRollbackInvalidatePreparedGeometry()
    {
        (DiagramContract? Contract, Dictionary<string, FlowchartLayoutNodeBounds>? Bounds) = Fixture();
        string? Title = Contract.Groups[0].Title;
        FlowchartFrameTitleBandHelper.Band[] First = [.. FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands];
        Contract.Groups[0].Title = "A\nlong\ntitle\nwith\nsix\nlines";
        IReadOnlyList<FlowchartFrameTitleBandHelper.Band> Trial = FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands;
        Assert.That(Trial, Is.Not.EqualTo(First));
        Assert.That(Trial, Is.EqualTo(FlowchartFrameTitleBandHelper.Build(Contract, Bounds)));
        Contract.Groups[0].Title = Title;
        Assert.That(FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands, Is.EqualTo(First));
    }

    /// <summary>Does not confuse two contracts sharing an obstacle dictionary.</summary>
    [Test]
    public void DifferentContractCannotReusePreparedBands()
    {
        (DiagramContract? Contract, Dictionary<string, FlowchartLayoutNodeBounds>? Bounds) = Fixture();
        IReadOnlyList<FlowchartFrameTitleBandHelper.Band> First = FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands;
        (DiagramContract? Other, Dictionary<string, FlowchartLayoutNodeBounds> _) = Fixture();
        Other.Groups[0].Title = "A\ndifferent\nframe\ntitle\nwith\nsix lines";
        Assert.That(FlowchartAllocatedPortFallback.Prepare(Other, Bounds).Bands, Is.Not.EqualTo(First));
        Assert.That(FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands, Is.EqualTo(First));
    }

    /// <summary>Preserves fresh topology-dependent reconstruction for incomplete frames instead of caching provisional geometry.</summary>
    [Test]
    public void MissingFramesAlwaysUseFreshComputedGeometry()
    {
        (DiagramContract? Contract, Dictionary<string, FlowchartLayoutNodeBounds>? Bounds) = Fixture();
        Bounds.Remove("g");
        IReadOnlyList<FlowchartFrameTitleBandHelper.Band> First = FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands;
        Bounds["A"] = new() { NodeId = "A", CenterY = 360, Width = 120, Height = 60 };
        IReadOnlyList<FlowchartFrameTitleBandHelper.Band> Next = FlowchartAllocatedPortFallback.Prepare(Contract, Bounds).Bands;
        Assert.That(Next, Is.Not.EqualTo(First));
        Assert.That(Next, Is.EqualTo(FlowchartFrameTitleBandHelper.Build(Contract, Bounds)));
    }

    /// <summary>Creates a declared group with explicit fitted bounds and one contained node.</summary>
    /// <returns>The contract and an ordered mutable inventory of immutable bounds.</returns>
    private static (DiagramContract Contract, Dictionary<string, FlowchartLayoutNodeBounds> Bounds) Fixture() =>
        (MermaidRecognizer.Parse("flowchart TB\n subgraph g[Group]\n A[Node]\n end"), new(StringComparer.Ordinal)
        {
            ["g"] = new() { NodeId = "g", CenterY = 120, Width = 240, Height = 180 },
            ["A"] = new() { NodeId = "A", CenterY = 90, Width = 120, Height = 60 }
        });
}

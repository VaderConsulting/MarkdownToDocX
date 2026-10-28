using MermaidRecognition.Contract;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks declaration-driven initial capacity without routing, moving cells or spending a repair budget.</summary>
[TestFixture]
public sealed class EnterpriseInitialCapacityTests
{
    /// <summary>The embedded approved percentage rounds up and always supplies at least one extra track for traffic.</summary>
    /// <param name="Forecast">Declared simultaneous demand.</param>
    /// <param name="Expected">Approved rounded reservation.</param>
    [TestCase(0, 0), TestCase(1, 2), TestCase(2, 3), TestCase(3, 4), TestCase(4, 5), TestCase(5, 7), TestCase(8, 10)]
    public void HeadroomComesFromTheEmbeddedRule(int Forecast, int Expected)
    {
        Assert.That(FlowchartLayoutRules.CorridorCapacityBuffer, Is.EqualTo(0.25));
        Assert.That(EnterpriseBandPlacement.InitialReservedTrackCount(Forecast), Is.EqualTo(Expected));
    }
    /// <summary>Six blocked fan arms reserve their whole approach and trunk gaps before initial publication.</summary>
    [Test]
    public void InitialFanCapacityPublishesRegionsAndGapsTogether()
    {
        DiagramContract Contract = new DiagramContract();
        Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>
        {
            ["hub"] = new(0, 0, 1, 1), ["wall"] = new(1, 0, 1, 1), ["neighbour"] = new(1, 1, 1, 1)
        };
        for (int Index = 0; Index < 6; Index++)
        {
            string Id = "leaf" + Index;
            Targets[Id] = new(Index + 2, 0, 1, 1);
            Contract.EdgesMutable.Add(new() { SourceId = "hub", TargetId = Id });
        }
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        foreach (KeyValuePair<string, FlowchartCellRegion> Pair in Targets)
        {
            Assert.That(Grid.TryPlace(Pair.Key, Pair.Value), Is.True);
        }

        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        IReadOnlyList<FlowchartCellGapGrowth> Growth = EnterpriseBandPlacement.PlanInitialRoutingCapacity(Contract, Targets, Grid, null);
        Assert.That(Grid.Capture(), Is.SameAs(Before), "Planning must not mutate regions or gaps.");
        Assert.That(Grid.TryPrepare(Targets, Growth, out FlowchartLogicalGrid.Snapshot? After), Is.True);
        Assert.That(Grid.TryCommit(Before, After!), Is.True);
        double Required = EnterpriseBandPlacement.InitialReservedTrackCount(Contract.Edges.Count) * FlowchartLayoutRules.TrackSpacing;
        Assert.Multiple(() =>
        {
            Assert.That(Grid.Capture().Regions, Is.EqualTo(Before.Regions));
            Assert.That(Grid.Capture().RowGaps[0], Is.GreaterThanOrEqualTo(Required));
            Assert.That(Grid.Capture().ColumnGaps[0], Is.GreaterThanOrEqualTo(Required));
        });
    }

    /// <summary>Independent clear straight columns consume no bend tracks and keep their exact initial snapshot.</summary>
    [Test]
    public void ClearPartnerColumnsDoNotGrowAnyGap()
    {
        DiagramContract Contract = new DiagramContract();
        Dictionary<string, FlowchartCellRegion> Targets = [];
        for (int Column = 0; Column < 3; Column++)
        {
            Targets["a" + Column] = new(0, Column, 1, 1);
            Targets["b" + Column] = new(1, Column, 1, 1);
            Contract.EdgesMutable.Add(new() { SourceId = "a" + Column, TargetId = "b" + Column });
        }
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        foreach (KeyValuePair<string, FlowchartCellRegion> Pair in Targets)
        {
            Assert.That(Grid.TryPlace(Pair.Key, Pair.Value), Is.True);
        }

        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        Assert.That(EnterpriseBandPlacement.PlanInitialRoutingCapacity(Contract, Targets, Grid, null), Is.Empty);
        Assert.That(Grid.Capture(), Is.SameAs(Before));
    }

    /// <summary>A wide node in another row excludes its internal column gap; demand uses the nearest open strip without enlarging that node.</summary>
    [Test]
    public void WideNodeInternalGapIsNeverReservedAsACorridor()
    {
        DiagramContract Contract = new DiagramContract();
        Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>
        {
            ["hub"] = new(0, 0, 1, 1), ["wall"] = new(1, 0, 1, 1),
            ["wide"] = new(10, 0, 1, 2), ["neighbour"] = new(10, 3, 1, 1)
        };
        for (int Index = 0; Index < 3; Index++)
        {
            string Id = "leaf" + Index;
            Targets[Id] = new(Index + 2, 0, 1, 1);
            Contract.EdgesMutable.Add(new() { SourceId = "hub", TargetId = Id });
        }
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        foreach (KeyValuePair<string, FlowchartCellRegion> Pair in Targets)
        {
            Assert.That(Grid.TryPlace(Pair.Key, Pair.Value), Is.True);
        }

        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        double Width = Grid.Measure(Targets["wide"]).Width;
        IReadOnlyList<FlowchartCellGapGrowth> Growth = EnterpriseBandPlacement.PlanInitialRoutingCapacity(Contract, Targets, Grid, null);
        Assert.That(Growth.Any(Item => Item.Horizontal && Item.After == 0), Is.False);
        Assert.That(Growth.Any(Item => Item.Horizontal && Item.After == 1), Is.True);
        Assert.That(Grid.TryPrepare(Targets, Growth, out FlowchartLogicalGrid.Snapshot? After), Is.True);
        Assert.That(Grid.TryCommit(Before, After!), Is.True);
        Assert.That(Grid.Measure(Targets["wide"]).Width, Is.EqualTo(Width));
    }
}

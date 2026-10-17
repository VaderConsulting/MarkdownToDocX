namespace MermaidRecognition.Layout.Tests;

/// <summary>Prevents grid rounding and signed-coordinate handling from collapsing distinct channel tracks.</summary>
[TestFixture]
public sealed class ChannelTrackAllocationTests
{
    /// <summary>Allocates only whole distinct grid positions after reserving both terminal clearances.</summary>
    /// <param name="Source">The last departure coordinate along the channel.</param>
    /// <param name="Target">The first arrival coordinate along the channel.</param>
    /// <param name="Count">The number of independent internal tracks required.</param>
    /// <param name="Fits">Whether sufficient legal grid tracks exist.</param>
    [TestCase(255, 555, 9, false)]
    [TestCase(255, 555, 8, true)]
    [TestCase(255, 615, 9, true)]
    [TestCase(-615, -255, 9, true)]
    [TestCase(0, 390, 9, true)]
    [TestCase(0, 60, 1, false)]
    [TestCase(0, 120, 1, true)]
    public void TracksRemainDistinctAtHalfGridPhaseAndNegativeCoordinates(double Source, double Target, int Count, bool Fits)
    {
        Assert.That(DiagramGridLayout.TryAllocateChannelTracks(Source, Target, Count, out var Tracks), Is.EqualTo(Fits));
        if (!Fits) { Assert.That(Tracks, Is.Empty); return; }
        Assert.That(Tracks, Has.Length.EqualTo(Count));
        Assert.That(Tracks.Distinct().Count(), Is.EqualTo(Count));
        Assert.That(Tracks.All(Track => Track % DiagramGridMetrics.GRID_UNIT_PX == 0), Is.True);
        Assert.That(Tracks[0] - Source, Is.GreaterThanOrEqualTo(2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX));
        Assert.That(Target - Tracks[^1], Is.GreaterThanOrEqualTo(2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX));
        Assert.That(Tracks.Zip(Tracks.Skip(1)).All(Pair => Pair.Second - Pair.First >= DiagramGridMetrics.GRID_UNIT_PX), Is.True);
    }
}

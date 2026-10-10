namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class GridCompactionTests
{
    [TestCase(false, 2)]
    [TestCase(true, 2)]
    [TestCase(false, 20)]
    [TestCase(true, 20)]
    public void EmptyStrips_CloseWhileKeepingAlignmentAndGrid(bool Horizontal, int Count)
    {
        var Nodes = new List<DiagramGridLayout.GridNode>();
        for (int Column = 0; Column < Count; Column++)
        for (int Rank = 0; Rank < 2; Rank++)
            Nodes.Add(new() { Id = $"n{Rank}_{Column}", Rank = Rank,
                X = Horizontal ? Rank * 150 : Column * 600,
                Y = Horizontal ? Column * 600 : Rank * 150,
                Width = Horizontal ? 60 : 180, Height = Horizontal ? 180 : 60 });
        FlowchartGridCompaction.Apply(Nodes, Horizontal, 30);
        for (int Column = 0; Column < Count; Column++)
        foreach (var Node in Nodes.Skip(Column * 2).Take(2))
        {
            Assert.That(Horizontal ? Node.Y : Node.X, Is.EqualTo(Column * 210));
            Assert.That(Horizontal ? Node.X : Node.Y, Is.EqualTo(Node.Rank * 150));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReservedCorridor_IsKeptWhileSurplusSpaceCloses(bool Horizontal)
    {
        var Nodes = new List<DiagramGridLayout.GridNode>
        {
            new() { Width = 180, Height = 180 },
            new() { X = Horizontal ? 0 : 300, Y = Horizontal ? 300 : 0, Width = 180, Height = 180 },
            new() { X = Horizontal ? 0 : 900, Y = Horizontal ? 900 : 0, Width = 180, Height = 180 }
        };
        FlowchartGridCompaction.Apply(Nodes, Horizontal, 210);
        Assert.That(Horizontal ? Nodes[1].Y : Nodes[1].X, Is.EqualTo(300));
        Assert.That(Horizontal ? Nodes[2].Y : Nodes[2].X, Is.EqualTo(690));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OverlappingCrossBands_AreNeverCompressed(bool Horizontal)
    {
        var Nodes = new List<DiagramGridLayout.GridNode>
        {
            new() { X = 0, Y = 0, Width = 180, Height = 180 },
            new() { X = 90, Y = 90, Width = 180, Height = 180 }
        };
        FlowchartGridCompaction.Apply(Nodes, Horizontal, 30);
        Assert.That(Nodes[1].X, Is.EqualTo(90));
        Assert.That(Nodes[1].Y, Is.EqualTo(90));
    }
}

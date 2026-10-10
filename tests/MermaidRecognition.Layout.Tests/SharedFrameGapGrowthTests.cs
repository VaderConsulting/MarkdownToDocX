using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class SharedFrameGapGrowthTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void OutsideColumn_GrowsAsAWholeWithoutMovingFrameMembers(bool Mirrored)
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nsubgraph g[Container]\nm[Member]\nend\na[Outside]\nb[Aligned]");
        int Sign = Mirrored ? 1 : -1;
        var Member = new DiagramGridLayout.GridNode { Id = "m", X = 0, Y = 0, Width = 100, Height = 60 };
        var Outside = new DiagramGridLayout.GridNode { Id = "a", X = Sign * 90, Y = 0, Width = 100, Height = 60 };
        var Aligned = new DiagramGridLayout.GridNode { Id = "b", X = Sign * 90, Y = 300, Width = 100, Height = 60 };
        var Frame = new DiagramGridLayout.GridFrame { Id = "g", Left = -100, Right = 100, Top = -100, Bottom = 100 };
        Assert.That(DiagramGridLayout.TryEvictNodeFromFrame(Contract, [Member, Outside, Aligned], Outside, Frame), Is.True);
        Assert.That(Outside.X, Is.EqualTo(Aligned.X));
        Assert.That(Math.Abs(Outside.X) - 50 - 100, Is.GreaterThanOrEqualTo(30));
        Assert.That((Outside.X - Sign * 90) % 30, Is.Zero);
        Assert.That(Member.X, Is.Zero);
        Assert.That(Member.Y, Is.Zero);
    }
}

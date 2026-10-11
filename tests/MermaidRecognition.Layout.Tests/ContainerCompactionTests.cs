using System.Text;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class ContainerCompactionTests
{
    [TestCase("TB")]
    [TestCase("LR")]
    public void OppositeDeclarationOrder_PlacesRelatedPairsAlongTheFlowAxis(string Direction)
    {
        var Contract = MermaidRecognizer.Parse($"flowchart {Direction}\nfirst --> fourth\nsecond --> third");
        var Units = new List<FlowchartContainerPacking.Unit>();
        foreach (var Node in Contract.Nodes.OrderBy(Node => Node.Id))
        {
            var Member = new DiagramGridLayout.GridNode { Id = Node.Id, X = 75, Y = 30, Width = 150, Height = 60 };
            Units.Add(new(new() { Id = Node.Id, Left = 0, Top = 0, Right = 150, Bottom = 60 }, [Member]));
        }
        FlowchartContainerPacking.Apply(Contract, Units);
        var ById = Units.SelectMany(Unit => Unit.Members).ToDictionary(Node => Node.Id);
        foreach (var Edge in Contract.Edges)
        {
            var Source = ById[Edge.SourceId]; var Target = ById[Edge.TargetId];
            Assert.That(Direction == "TB" ? Target.X : Target.Y, Is.EqualTo(Direction == "TB" ? Source.X : Source.Y));
            Assert.That(Direction == "TB" ? Target.Y : Target.X, Is.GreaterThan(Direction == "TB" ? Source.Y : Source.X));
        }
    }

    [TestCase(3)]
    [TestCase(8)]
    [TestCase(20)]
    public void InterleavedSiblingMembers_CompactWithoutStretchingTheirFrames(int Count)
    {
        var Source = new StringBuilder("flowchart TB\n");
        var Raw = new FlowchartLayoutResult();
        for (int Group = 0; Group < Count; Group++)
        {
            Source.AppendLine($"subgraph group{Group}\nnode{Group}a --> node{Group}b\nend");
            Raw.NodeBoundsMutable.Add(new() { NodeId = $"node{Group}a", CenterX = Group * 600,
                CenterY = Group % 2 == 0 ? 0 : 180, Width = 150, Height = 60 });
            Raw.NodeBoundsMutable.Add(new() { NodeId = $"node{Group}b", CenterX = (Count - Group - 1) * 600,
                CenterY = Group % 2 == 0 ? 180 : 0, Width = 150, Height = 60 });
        }
        Source.AppendLine("outside --> node0a");
        Raw.NodeBoundsMutable.Add(new() { NodeId = "outside", CenterX = Count * 2000,
            CenterY = 180, Width = 150, Height = 60 });
        var Layout = DiagramGridLayout.Apply(MermaidRecognizer.Parse(Source.ToString()), Raw);
        Assert.That(Layout.ClusterBounds, Has.Count.EqualTo(Count));
        double BoxWidth = Layout.NodeBounds.Max(Node => Node.Width);
        Assert.That(Layout.NodeBounds.Max(Node => Node.CenterX) - Layout.NodeBounds.Min(Node => Node.CenterX),
            Is.LessThan(Count * (2 * BoxWidth + 300)));
        foreach (var Frame in Layout.ClusterBounds)
        {
            Assert.That(Frame.Width, Is.LessThanOrEqualTo(2 * BoxWidth + 180));
            Assert.That(Frame.Height, Is.LessThanOrEqualTo(3 * Layout.NodeBounds.Max(Node => Node.Height) + 180));
            foreach (var Other in Layout.ClusterBounds.Where(Other => Other != Frame))
                Assert.That(Math.Abs(Frame.CenterX - Other.CenterX) >= (Frame.Width + Other.Width) / 2 + 30
                    || Math.Abs(Frame.CenterY - Other.CenterY) >= (Frame.Height + Other.Height) / 2 + 30, Is.True);
        }
    }
}

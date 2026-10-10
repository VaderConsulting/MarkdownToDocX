using System.Text;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class ContainerCompactionTests
{
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
                CenterY = 0, Width = 150, Height = 60 });
            Raw.NodeBoundsMutable.Add(new() { NodeId = $"node{Group}b", CenterX = (Count - Group - 1) * 600,
                CenterY = 180, Width = 150, Height = 60 });
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
            foreach (var Other in Layout.ClusterBounds.Where(Other => Other != Frame))
                Assert.That(Math.Abs(Frame.CenterX - Other.CenterX) >= (Frame.Width + Other.Width) / 2 + 30
                    || Math.Abs(Frame.CenterY - Other.CenterY) >= (Frame.Height + Other.Height) / 2 + 30, Is.True);
        }
    }
}

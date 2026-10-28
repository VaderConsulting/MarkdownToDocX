using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class LocalLabelGapGrowthTests
{
    [TestCase("TB")]
    [TestCase("BT")]
    [TestCase("LR")]
    [TestCase("RL")]
    public void LongLabel_GrowsItsOwnRankGapWhileOtherGapsKeepTheirBase(string Direction)
    {
        string Label = Direction is "LR" or "RL" ? new string('W', 48) : "first<br/>second<br/>third<br/>fourth<br/>fifth";
        DiagramContract Contract = MermaidRecognizer.Parse($"flowchart {Direction}\n a[Input] -->|{Label}| b[Work]\n b --> c[Continue]\n c --> d[Finish]");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        bool Horizontal = Direction is "LR" or "RL";
        FlowchartLayoutNodeBounds[] Nodes = [.. Contract.Nodes.Select(Node => Layout.NodeBounds.Single(Bounds => Bounds.NodeId == Node.Id))];
        double Gap(int Index) => Math.Abs(Horizontal ? Nodes[Index + 1].CenterX - Nodes[Index].CenterX : Nodes[Index + 1].CenterY - Nodes[Index].CenterY)
            - ((Horizontal ? Nodes[Index + 1].Width + Nodes[Index].Width : Nodes[Index + 1].Height + Nodes[Index].Height) / 2);
        Assert.That(Gap(0), Is.GreaterThan(Gap(1)));
        Assert.That(Gap(1), Is.EqualTo(60).Within(1));
        Assert.That(Gap(2), Is.EqualTo(60).Within(1));
        foreach (int Index in Enumerable.Range(0, 3))
        {
            Assert.That(Gap(Index) % 30, Is.EqualTo(0).Within(1));
        }
    }
}

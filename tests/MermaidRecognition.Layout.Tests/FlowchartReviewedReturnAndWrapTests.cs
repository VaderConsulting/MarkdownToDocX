using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks return channels and row folds independently of scenario identifiers.</summary>
[TestFixture]
public sealed class FlowchartReviewedReturnAndWrapTests
{
    /// <summary>A return to an earlier rank enters from the side of its outside return channel.</summary>
    /// <param name="Direction">Main flow direction.</param>
    [TestCase("TB")]
    [TestCase("BT")]
    [TestCase("LR")]
    [TestCase("RL")]
    public void ReturnEntersTheTargetsChannelSide(string Direction)
    {
        DiagramContract Contract = MermaidRecognizer.Parse($"flowchart {Direction}\nA[Input] --> B[Work]\nB --> C[Check]\nC --> A\nC --> D[Output]");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutEdgeRoute Return = Layout.EdgeRoutes.Single(Route => Route.SourceId == "C" && Route.TargetId == "A");
        string[] Sides = Direction is "LR" or "RL" ? ["top", "bottom"] : ["left", "right"];
        Assert.Multiple(() =>
        {
            Assert.That(Return.EntrySide, Is.AnyOf(Sides));
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
                .Where(Finding => Finding.Rule is "10" or "3.1" or "4.1"), Is.Empty);
        });
    }

    /// <summary>A folded left-to-right chain leaves its old row below and enters the next row above.</summary>
    [Test]
    public void RowWrapLeavesBottomAndEntersTop()
    {
        string Source = "flowchart LR\n" + string.Join("\n", Enumerable.Range(0, 39)
            .Select(Index => $"N{Index}[Step {Index}] --> N{Index + 1}[Step {Index + 1}]"));
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult? Layout), Is.True);
        Dictionary<string, FlowchartLayoutNodeBounds> Boxes = Layout.NodeBounds.ToDictionary(Node => Node.NodeId);
        FlowchartLayoutEdgeRoute[] Wraps = [.. Layout.EdgeRoutes.Where(Route => Boxes[Route.TargetId].CenterY < Boxes[Route.SourceId].CenterY
            && Boxes[Route.TargetId].CenterX < Boxes[Route.SourceId].CenterX)];
        Assert.That(Wraps, Is.Not.Empty);
        foreach (FlowchartLayoutEdgeRoute? Route in Wraps)
        {
            Assert.Multiple(() =>
            {
                Assert.That(Route.ExitSide, Is.EqualTo("bottom"), Route.EdgeId);
                Assert.That(Route.EntrySide, Is.EqualTo("top"), Route.EdgeId);
                Assert.That(FlowchartLayoutScore.CountCorners(Route.Points), Is.EqualTo(2), Route.EdgeId);
            });
        }
    }
}

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartEnteredTitleBandTests
{
    [Test]
    public void FindEnteredBandIds_ReturnsEachBandOnceWhenTwoSegmentsEnterIt()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\n subgraph g[Group]\n A[One] --> B[Two]\n end");
        FlowchartFrameTitleBandHelper.Band Band = new FlowchartFrameTitleBandHelper.Band("g", 0, 80, 200, 100);
        List<(double X, double Y)> Points = new List<(double X, double Y)>
        {
            (50, 50),
            (50, 90),
            (150, 90),
            (150, 50)
        };
        IReadOnlyList<string> Entered = FlowchartFrameTitleBandHelper.FindEnteredBandIds(
            Points,
            new[] { Band },
            "A",
            "B",
            Contract);
        Assert.That(Entered, Is.EqualTo(new[] { "g" }));
    }
}

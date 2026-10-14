using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class SymmetricPortDistributionTests
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void SharedSideSlots_AreSymmetricAndUseTheCentreWhenAvailable(int LineCount)
    {
        int SlotCount = Math.Max(DiagramGridMetrics.TRACKS_PER_GAP, LineCount);
        int[] Slots = DiagramGridMetrics.SelectSlotIndexes(LineCount, SlotCount);

        Assert.That(Slots, Is.Ordered.Ascending);
        Assert.That(Slots[0] + Slots[^1], Is.EqualTo(SlotCount - 1));
        for (int Index = 0; Index < Slots.Length / 2; Index++)
            Assert.That(Slots[Index] + Slots[^(Index + 1)], Is.EqualTo(SlotCount - 1));
        if (LineCount % 2 == 1)
            Assert.That(Slots[LineCount / 2], Is.EqualTo(SlotCount / 2));
    }

    [Test]
    public void TechnologyLayer_AppServerFanOut_UsesSymmetricPorts()
    {
        string Source = File.ReadAllText(FindRepositoryFile("enterprise.md"));
        string Start = "### A18 — Artifact and the complete technology layer (all thirteen types)";
        int Heading = Source.IndexOf(Start, StringComparison.Ordinal);
        Assert.That(Heading, Is.GreaterThanOrEqualTo(0));
        int Fence = Source.IndexOf("```mermaid", Heading, StringComparison.Ordinal);
        int Body = Source.IndexOf('\n', Fence) + 1;
        int End = Source.IndexOf("```", Body, StringComparison.Ordinal);
        string Diagram = Source[Body..End];

        var Document = EnterpriseValidator.Analyze(Diagram).Document;
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        var Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes)
            .Where(Route => Route.Relationship.SourceId == "appServer")
            .ToArray();
        Assert.That(Routes, Has.Length.EqualTo(3));

        var SourceNode = Layout.Nodes["appServer"];
        var Ports = Routes.Select(Route => Route.SvgPoints[0]).OrderBy(Point => Point.X).ToArray();
        Assert.That(Ports.All(Point => Math.Abs(Point.Y - (SourceNode.AbsoluteY + SourceNode.Height)) < 1.0), Is.True,
            "The three selected exits should share the bottom face before they are distributed.");

        double Centre = SourceNode.AbsoluteX + SourceNode.Width / 2.0;
        Assert.That((Ports[0].X + Ports[^1].X) / 2.0, Is.EqualTo(Centre).Within(1.0),
            string.Join(", ", Ports.Select(Point => Point.X.ToString("0.##"))));
        Assert.That(Ports[1].X - Ports[0].X, Is.EqualTo(Ports[2].X - Ports[1].X).Within(1.0));
    }

    private static string FindRepositoryFile(string RelativePath)
    {
        DirectoryInfo? Current = new DirectoryInfo(AppContext.BaseDirectory);
        while (Current != null)
        {
            string Candidate = Path.Combine(Current.FullName, RelativePath);
            if (File.Exists(Candidate)) return Candidate;
            Current = Current.Parent;
        }
        throw new FileNotFoundException(RelativePath);
    }
}

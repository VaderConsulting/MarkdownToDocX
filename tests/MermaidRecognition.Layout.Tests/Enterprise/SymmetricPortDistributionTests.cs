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
        {
            Assert.That(Slots[Index] + Slots[^(Index + 1)], Is.EqualTo(SlotCount - 1));
        }

        if (LineCount % 2 == 1)
        {
            Assert.That(Slots[LineCount / 2], Is.EqualTo(SlotCount / 2));
        }
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

        EnterpriseDocument Document = EnterpriseValidator.Analyze(Diagram).Document;
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        EnterpriseRoutedEdge[] Routes = [.. EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes).Where(Route => Route.Relationship.SourceId == "appServer")];
        Assert.That(Routes, Has.Length.EqualTo(3));

        EnterpriseLayoutNode SourceNode = Layout.Nodes["appServer"];
        foreach (EnterpriseRoutedEdge Route in Routes)
        {
            AssertOnSideGrid(SourceNode, Route.SvgPoints[0]);
        }
    }

    [Test]
    public void A169_HintedAndAutomaticEdgesShareSymmetricPorts()
    {
        string Source = File.ReadAllText(FindRepositoryFile("enterprise.md"));
        string Start = "### A169 - Hints: recreation of a referral agency actors and roles diagram";
        int Heading = Source.IndexOf(Start, StringComparison.Ordinal);
        int Fence = Source.IndexOf("```mermaid", Heading, StringComparison.Ordinal);
        int Body = Source.IndexOf('\n', Fence) + 1;
        int End = Source.IndexOf("```", Body, StringComparison.Ordinal);
        EnterpriseDocument Document = EnterpriseValidator.Analyze(Source[Body..End]).Document;
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        List<EnterpriseRoutedEdge> Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        EnterpriseLayoutNode Referral = Layout.Nodes["referral"];
        EnterpriseRoutedEdge[] ReferralRoutes = [.. Routes.Where(Route => Route.Relationship.TargetId == "referral")];
        Assert.That(ReferralRoutes, Has.Length.EqualTo(3));
        foreach (EnterpriseRoutedEdge Route in ReferralRoutes)
        {
            AssertOnSideGrid(Referral, Route.SvgPoints[^1]);
        }

        EnterpriseLayoutNode Collaborator = Layout.Nodes["collaborator"];
        foreach (EnterpriseRoutedEdge Route in Routes.Where(Route => Route.Relationship.TargetId == "collaborator"))
        {
            AssertOnSideGrid(Collaborator, Route.SvgPoints[^1]);
        }

        foreach (string SourceId in new[] { "water", "westernPower" })
        {
            EnterpriseRoutedEdge Route = Routes.Single(Route => Route.Relationship.SourceId == SourceId
                && Route.Relationship.TargetId == "collaborator");
            EnterpriseLayoutNode SourceNode = Layout.Nodes[SourceId];
            Assert.That(Route.SvgPoints[0].Y,
                Is.EqualTo(SourceNode.AbsoluteY + SourceNode.Height).Within(1.0),
                $"The {SourceId} leave-S hint must remain on the source's bottom side.");
            Assert.That(Route.SvgPoints[^1].Y, Is.EqualTo(Collaborator.AbsoluteY).Within(1.0),
                $"The {SourceId} enter-N hint must remain on the target's top side.");
        }
    }

    /// <summary>
    /// A bend-reducing swap may leave a different side. Whichever side it uses, the point is one of that side's
    /// fixed centre-out grid points.
    /// </summary>
    private static void AssertOnSideGrid(EnterpriseLayoutNode Node, (double X, double Y) Point)
    {
        double Left = Node.AbsoluteX;
        double Top = Node.AbsoluteY;
        double Right = Left + Node.Width;
        double Bottom = Top + Node.Height;
        double Offset;
        double Length;
        if (Math.Abs(Point.Y - Top) < 1.0 || Math.Abs(Point.Y - Bottom) < 1.0)
        {
            Offset = Point.X - Left;
            Length = Node.Width;
        }
        else if (Math.Abs(Point.X - Left) < 1.0 || Math.Abs(Point.X - Right) < 1.0)
        {
            Offset = Point.Y - Top;
            Length = Node.Height;
        }
        else
        {
            Assert.Fail($"({Point.X:0.##},{Point.Y:0.##}) is not on {Node.Id}.");
            return;
        }

        double[] Grid = DiagramGridMetrics.SideGridOffsets(Length);
        Assert.That(Grid.Any(Candidate => Math.Abs(Candidate - Offset) < 0.5), Is.True,
            $"{Node.Id} offset {Offset:0.##} of {Length:0.##}; grid {string.Join(", ", Grid.Select(Value => Value.ToString("0.##")))}");
    }

    private static string FindRepositoryFile(string RelativePath)
    {
        DirectoryInfo? Current = new DirectoryInfo(AppContext.BaseDirectory);
        while (Current != null)
        {
            string Candidate = Path.Combine(Current.FullName, RelativePath);
            if (File.Exists(Candidate))
            {
                return Candidate;
            }

            Current = Current.Parent;
        }
        throw new FileNotFoundException(RelativePath);
    }
}

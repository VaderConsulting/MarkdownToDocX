using System.Text;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseDeepContainerRoutingTests
{
    [TestCase(2, false)]
    [TestCase(4, false)]
    [TestCase(2, true)]
    [TestCase(4, true)]
    public void ExternalRelationshipsToNestedContainers_MeetTheirVisibleBorders(int Depth, bool Outgoing)
    {
        StringBuilder Source = new StringBuilder("enterprise\n communication-network peer \"External network\"\n technology-service service \"Shared output\"\n");
        foreach (string Prefix in new[] { "a", "b" })
        {
            Source.AppendLine($"group {Prefix}g \"Outer frame {Prefix}\"");
            for (int Index = 0; Index < Depth; Index++)
            {
                Source.AppendLine($"node {Prefix}{Index} \"Container {Index}\" in {(Index == 0 ? $"{Prefix}g" : $"{Prefix}{Index - 1}")}");
            }

            Source.AppendLine($"system-software {Prefix}leaf \"Work\" in {Prefix}{Depth - 1}");
            Source.AppendLine($"artifact {Prefix}data \"Data\" in {Prefix}leaf");
            Source.AppendLine(Outgoing ? $"{Prefix}1 -association-> peer" : $"peer -association-> {Prefix}1");
            Source.AppendLine($"{Prefix}leaf -realization-> service");
        }
        Source.AppendLine("aleaf -flow-> bleaf \"transfer\"");
        Source.AppendLine("adata -association-> bdata");
        EnterpriseDocument Document = EnterpriseValidator.Analyze(Source.ToString()).Document;
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        List<EnterpriseRoutedEdge> Edges = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        Dictionary<string, List<(double X, double Y)>> Ports = [];
        foreach (EnterpriseRoutedEdge Edge in Edges)
        {
            Assert.That(Edge.SvgPoints.Zip(Edge.SvgPoints.Skip(1)).All(Segment =>
                Math.Abs(Segment.First.X - Segment.Second.X) < 0.01 || Math.Abs(Segment.First.Y - Segment.Second.Y) < 0.01), Is.True);
            foreach (bool Start in new[] { true, false })
            {
                string Id = Start ? Edge.Relationship.SourceId : Edge.Relationship.TargetId;
                EnterpriseLayoutNode Box = Layout.Nodes[Id];
                if (!Box.IsCompound)
                {
                    continue;
                }

                (double X, double Y) End = Start ? Edge.SvgPoints[0] : Edge.SvgPoints[^1];
                (double X, double Y) Next = Start ? Edge.SvgPoints[1] : Edge.SvgPoints[^2];
                bool Vertical = Math.Abs(End.X - Box.AbsoluteX) < 0.01 || Math.Abs(End.X - Box.AbsoluteX - Box.Width) < 0.01;
                bool Horizontal = Math.Abs(End.Y - Box.AbsoluteY) < 0.01 || Math.Abs(End.Y - Box.AbsoluteY - Box.Height) < 0.01;
                Assert.That((Vertical && End.Y >= Box.AbsoluteY && End.Y <= Box.AbsoluteY + Box.Height)
                    || (Horizontal && End.X >= Box.AbsoluteX && End.X <= Box.AbsoluteX + Box.Width),
                    Is.True, $"Terminal {End} must meet {Box.AbsoluteX},{Box.AbsoluteY} {Box.Width}x{Box.Height}.");
                Assert.That(Vertical ? Math.Abs(Next.Y - End.Y) : Math.Abs(Next.X - End.X), Is.LessThan(0.01));
                if (!Ports.TryGetValue(Id, out List<(double X, double Y)>? Previous))
                {
                    Ports[Id] = Previous = [];
                }

                Assert.That(Previous.All(Port => Math.Abs(Port.X - End.X) + Math.Abs(Port.Y - End.Y) >= 6), Is.True);
                Previous.Add(End);
            }
            if (string.IsNullOrWhiteSpace(Edge.Label))
            {
                continue;
            }

            Assert.That(Edge.LabelBreak, Is.Not.Null);
            foreach (EnterpriseRoutedEdge? Other in Edges.Where(Other => Other != Edge))
            {
                foreach (((double X, double Y) First, (double X, double Y) Second) Segment in Other.SvgPoints.Zip(Other.SvgPoints.Skip(1)))
                {
                    Assert.That(Math.Max(Segment.First.X, Segment.Second.X) > Edge.LabelX - (Edge.LabelWidth / 2) - 6
                    && Math.Min(Segment.First.X, Segment.Second.X) < Edge.LabelX + (Edge.LabelWidth / 2) + 6
                    && Math.Max(Segment.First.Y, Segment.Second.Y) > Edge.LabelY - (Edge.LabelHeight / 2) - 6
                    && Math.Min(Segment.First.Y, Segment.Second.Y) < Edge.LabelY + (Edge.LabelHeight / 2) + 6,
                    Is.False, "Cross-container captions must clear the neighbouring bundle.");
                }
            }
        }
    }
}

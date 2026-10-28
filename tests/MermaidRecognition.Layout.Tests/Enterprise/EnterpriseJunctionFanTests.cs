using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseJunctionFanTests
{
    [TestCase("TB", "and-junction")]
    [TestCase("LR", "or-junction")]
    public void ThreeWaySplitAndMerge_UseSeparateNormalCardinalPorts(string Direction, string Kind)
    {
        EnterpriseDocument Document = EnterpriseValidator.Analyze($"""
            enterprise
            direction {Direction}
            business-event input "Begin"
            {Kind} split
            business-process left "First"
            business-process middle "Second"
            business-process right "Third"
            {Kind} merge
            business-process output "Finish"
            input -triggering-> split
            split -triggering-> middle
            split -triggering-> left
            split -triggering-> right
            middle -triggering-> merge
            left -triggering-> merge
            right -triggering-> merge
            merge -triggering-> output
            """).Document;
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        List<EnterpriseRoutedEdge> Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        foreach (EnterpriseElement? Junction in Document.Elements.Values.Where(Element => Element.JunctionKind != null))
        {
            EnterpriseLayoutNode Box = Layout.Nodes[Junction.Id];
            List<(double X, double Y)> Ports = [];
            foreach (EnterpriseRoutedEdge? Route in Routes.Where(Route => Route.Relationship.SourceId == Junction.Id || Route.Relationship.TargetId == Junction.Id))
            {
                bool Start = Route.Relationship.SourceId == Junction.Id;
                (double X, double Y) End = Start ? Route.SvgPoints[0] : Route.SvgPoints[^1];
                (double X, double Y) Next = Start ? Route.SvgPoints[1] : Route.SvgPoints[^2];
                double X = End.X - Box.AbsoluteX - (Box.Width / 2);
                double Y = End.Y - Box.AbsoluteY - (Box.Height / 2);
                Assert.That(Math.Abs(X) < 0.01 || Math.Abs(Y) < 0.01, Is.True, $"Circle {Junction.Id} at {Box.AbsoluteX},{Box.AbsoluteY} {Box.Width}x{Box.Height}, end {End}, next {Next}, route {Route.Relationship.SourceId} to {Route.Relationship.TargetId} must use cardinal ports.");
                Assert.That(Math.Abs((X * (Next.Y - End.Y)) - (Y * (Next.X - End.X))), Is.LessThan(0.01), "Terminals must be radial.");
                Assert.That((X * (Next.X - End.X)) + (Y * (Next.Y - End.Y)), Is.GreaterThan(0));
                Assert.That(Ports.All(Port => Math.Abs(Port.X - End.X) + Math.Abs(Port.Y - End.Y) >= 6), Is.True);
                Ports.Add(End);
            }
        }
    }
}

using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseForwardBypassTests
{
    [Test]
    public void DirectBypassAroundTwoDependentBranchesAndAnotherInput_UsesTheFreeOuterSide()
    {
        string Source = """
            enterprise
            application-component consumer "Consumer"
            technology-service service "Service"
            node provider "Provider"
            artifact package "Package"
            service -serving-> consumer
            provider -realization-> service
            package -realization-> consumer
            provider -assignment-> package
            provider -serving-> consumer
            system-software secondary "Secondary provider"
            technology-service auxiliary "Auxiliary service"
            artifact table "Stored data"
            data-object record "Record"
            secondary -realization-> auxiliary
            auxiliary -serving-> consumer
            secondary -assignment-> table
            table -realization-> record
            """;
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.Errors, Is.Empty);
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Analysis.Document);
        List<EnterpriseRoutedEdge> Routes = EnterpriseFlowchartEdgeRouter.Route(Analysis.Document, Layout.Nodes);
        EnterpriseLayoutNode Provider = Layout.Nodes["provider"];
        EnterpriseLayoutNode Consumer = Layout.Nodes["consumer"];
        EnterpriseLayoutNode Package = Layout.Nodes["package"];
        List<(double X, double Y)> Bypass = Routes.Single(Route => Route.Relationship.SourceId == Provider.Id
            && Route.Relationship.TargetId == Consumer.Id).SvgPoints;
        List<(double X, double Y)> Branch = Routes.Single(Route => Route.Relationship.SourceId == Provider.Id
            && Route.Relationship.TargetId == Package.Id).SvgPoints;
        int BranchSide = Math.Sign(Package.AbsoluteX + (Package.Width / 2) - Provider.AbsoluteX - (Provider.Width / 2));
        double OuterSide(EnterpriseLayoutNode Node) => Node.AbsoluteX + (BranchSide < 0 ? Node.Width : 0);
        Assert.That(Bypass, Has.Count.EqualTo(4));
        Assert.That(Bypass[0].X, Is.EqualTo(OuterSide(Provider)));
        Assert.That(Bypass[^1].X, Is.EqualTo(OuterSide(Consumer)));
        Assert.That(-BranchSide * (Bypass[1].X - OuterSide(Provider)), Is.GreaterThan(0));
        Assert.That(-BranchSide * (Bypass[1].X - OuterSide(Consumer)), Is.GreaterThan(0));
        Assert.That(Bypass[2].X, Is.EqualTo(Bypass[1].X));
        Assert.That(Branch, Has.Count.EqualTo(3));
        Assert.That(Branch[0].X, Is.EqualTo(Provider.AbsoluteX + (BranchSide > 0 ? Provider.Width : 0)));
        Assert.That(Branch[^1].Y, Is.EqualTo(Package.AbsoluteY));
        Assert.That(Branch[1].Y, Is.EqualTo(Branch[0].Y));
        Assert.That(Branch[1].X, Is.EqualTo(Branch[^1].X));
        for (int First = 0; First < Routes.Count; First++)
        {
            List<(double X, double Y)> Points = Routes[First].SvgPoints;
            foreach (((double X, double Y) First, (double X, double Y) Second) Segment in Points.Zip(Points.Skip(1)))
            {
                Assert.That(Segment.First.X == Segment.Second.X || Segment.First.Y == Segment.Second.Y, Is.True);
            }

            for (int Second = First + 1; Second < Routes.Count; Second++)
            {
                (int Crossings, double Overlap) Intersections = FlowchartBranchRouteOrder.Intersections(Points.ToList(), Routes[Second].SvgPoints.ToList());
                Assert.That(Intersections.Crossings, Is.Zero);
                Assert.That(Intersections.Overlap, Is.Zero);
            }
        }
    }
}

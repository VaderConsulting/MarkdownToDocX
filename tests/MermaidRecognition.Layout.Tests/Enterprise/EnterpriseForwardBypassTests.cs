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
        var Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.Errors, Is.Empty);
        var Layout = EnterpriseLayoutEngine.Layout(Analysis.Document);
        var Routes = EnterpriseFlowchartEdgeRouter.Route(Analysis.Document, Layout.Nodes);
        var Provider = Layout.Nodes["provider"];
        var Consumer = Layout.Nodes["consumer"];
        var Package = Layout.Nodes["package"];
        var Bypass = Routes.Single(Route => Route.Relationship.SourceId == Provider.Id
            && Route.Relationship.TargetId == Consumer.Id).SvgPoints;
        var Branch = Routes.Single(Route => Route.Relationship.SourceId == Provider.Id
            && Route.Relationship.TargetId == Package.Id).SvgPoints;
        int BranchSide = Math.Sign(Package.AbsoluteX + Package.Width / 2 - Provider.AbsoluteX - Provider.Width / 2);
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
            var Points = Routes[First].SvgPoints;
            foreach (var Segment in Points.Zip(Points.Skip(1)))
                Assert.That(Segment.First.X == Segment.Second.X || Segment.First.Y == Segment.Second.Y, Is.True);
            for (int Second = First + 1; Second < Routes.Count; Second++)
            {
                var Intersections = FlowchartBranchRouteOrder.Intersections(Points.ToList(), Routes[Second].SvgPoints.ToList());
                Assert.That(Intersections.Crossings, Is.Zero);
                Assert.That(Intersections.Overlap, Is.Zero);
            }
        }
    }
}

using System;
using System.Linq;
using System.Threading;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture, NonParallelizable, Apartment(ApartmentState.STA)]
public sealed class PreviewRelationshipCommandsTests
{
    [Test]
    public void GroupEndpointDeletionDoesNotAssumeAnElementType()
    {
        const string Script = "enterprise\ngroup g\napplication-component a in g\napplication-component b\ng -triggering-> b\n";
        var Analysis = EnterpriseValidator.Analyze(Script);
        Assert.That(Analysis.HasErrors, Is.False);
        var Item = PreviewHoverIndex.FromScript(Script).Objects.Single(Item => Item.Kind == PreviewObjectKind.Relationship);
        Assert.That(PreviewRelationshipCommands.TryDelete(Script, Item, out var Edit, out var Reason), Is.True, Reason);
        Assert.That(Edit!.Updated, Does.Not.Contain("-triggering->"));
    }
    internal const string Source = "enterprise\napplication-component a\napplication-component b below a +3\n"
        + "application-component c right-of b +3\na -flow-> b \"first\"\nb -flow-> c \"second\"\n";
    internal const string StyledSource = Source + "linkStyle 0 stroke:#d00,font-size:17px\nlinkStyle 1 stroke:#00d,font-size:21px\n";

    internal static (RetainedEnterprisePreview Session, string Svg) Fixture(string Script)
    {
        var Session = new RetainedEnterprisePreview(); Session.Observe(1);
        var Initial = Session.Prepare(Script, 1, EnterpriseValidator.Analyze(Script), new() { EnableIncrementalRouting = true });
        Assert.That(Session.TryCommit(Initial), Is.True);
        return (Session, Initial.Svg);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AddCommandUsesFreeExactPointsAndRetainsExistingRoutes(bool Styled)
    {
        string Script = Styled ? StyledSource : Source;
        var (Session, _) = Fixture(Script);
        var Before = Session.CopyInteractionSnapshot()!;
        var Kind = PreviewRelationshipCommands.AllowedKinds(EnterpriseValidator.Analyze(Script).Document, "a", "c")
            .Single(Kind => Kind.Relationship == EnterpriseRelationshipKind.Flow);
        Assert.That(PreviewRelationshipCommands.TryAdd(Script, "a", "c", Kind, Before, out var Edit, out var Reason), Is.True, Reason);
        Assert.That(Edit!.Updated.Substring(Edit.Start, Edit.Replacement.Length), Is.EqualTo(Edit.Replacement));
        Assert.That(Edit.Replacement, Does.Contain("a -flow-> c [leave ").And.Contain("@"));
        Session.Observe(2);
        var Added = Session.Prepare(Edit.Updated, 2, EnterpriseValidator.Analyze(Edit.Updated), new() { EnableIncrementalRouting = true });
        Assert.That(Added.Kind, Is.EqualTo("LocalRouting"), Added.RebuildReason);
        Assert.That(Session.TryCommit(Added), Is.True);
        var After = Session.CopyInteractionSnapshot()!;
        Assert.That(FlowchartConnectionPoints.Validate(After.Model, After.Layout, CheckRoutes: true), Is.Null);
        Assert.That(After.Layout.EdgeRoutes.Count, Is.EqualTo(3));
        foreach (var Route in Before.Layout.EdgeRoutes)
        {
            var New = After.Layout.EdgeRoutes.Single(Line => Line.EdgeId == Route.EdgeId);
            Assert.That(New.Points.Select(Point => (Point.X, Point.Y)), Is.EqualTo(Route.Points.Select(Point => (Point.X, Point.Y))));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeleteCommandReindexesStylesWithoutRebuildingSurvivingGeometry(bool CrLf)
    {
        string Script = CrLf ? StyledSource.Replace("\n", "\r\n") : StyledSource;
        var (Session, Svg) = Fixture(Script);
        var Item = PreviewHoverIndex.Build(Svg, Script).Objects.Single(Item => Item.Kind == PreviewObjectKind.Relationship && Item.SourceId == "a");
        Assert.That(PreviewRelationshipCommands.TryDelete(Script, Item, out var Edit, out var Reason), Is.True, Reason);
        Assert.That(Edit!.Updated, Does.Not.Contain("a -flow-> b").And.Not.Contain("stroke:#d00"));
        Assert.That(Edit.Updated, Does.Contain("linkStyle 0 stroke:#00d,font-size:21px"));
        if (CrLf) Assert.That(Edit.Updated.Replace("\r\n", string.Empty), Does.Not.Contain("\n"));
        Session.Observe(2);
        var Removed = Session.Prepare(Edit.Updated, 2, EnterpriseValidator.Analyze(Edit.Updated), new() { EnableIncrementalRouting = true });
        Assert.That(Removed.Kind, Is.EqualTo("LocalRouting"), Removed.RebuildReason);
        Assert.That(Removed.RebuiltRouteIds, Is.Empty);
        Assert.That(Removed.Svg, Does.Contain("#00d").And.Not.Contain("#d00"));
        Assert.That(Session.TryCommit(Removed), Is.True);
        Assert.That(Session.CopyInteractionSnapshot()!.Identities.Bindings.Single().Id, Is.EqualTo("relationship:2"));
    }

    [Test]
    public void IndexedGeometryStyleChangesAreDetectedOnTheSurvivingRelationship()
    {
        var (Session, _) = Fixture(StyledSource);
        string Updated = StyledSource.Replace("font-size:21px", "font-size:30px");
        Session.Observe(2);
        var Changed = Session.Prepare(Updated, 2, EnterpriseValidator.Analyze(Updated), new() { EnableIncrementalRouting = true });
        Assert.That(Changed.Kind, Is.EqualTo("InitialLayout"));
        Assert.That(Changed.RebuildReason, Does.Contain("Relationship"));
    }

    [Test]
    public void InvalidAndDuplicateCommandsWriteNothing()
    {
        var (Session, Svg) = Fixture(Source);
        var Document = EnterpriseValidator.Analyze(Source).Document;
        Assert.That(PreviewRelationshipCommands.AllowedKinds(Document, "a", "a"), Is.Empty);
        Assert.That(PreviewRelationshipCommands.AllowedKinds(Document, "a", "b").Any(Kind => Kind.Relationship == EnterpriseRelationshipKind.Flow), Is.False);
        var Item = PreviewHoverIndex.Build(Svg, Source).Objects.First(Item => Item.Kind == PreviewObjectKind.Relationship);
        Item.TargetId = "missing";
        Assert.That(PreviewRelationshipCommands.TryDelete(Source, Item, out _, out _), Is.False);
        var Snapshot = Session.CopyInteractionSnapshot()!;
        Assert.That(PreviewRelationshipCommands.TryAdd(Source + "%% edit", "a", "c", new(EnterpriseRelationshipKind.Flow, "flow"), Snapshot, out _, out _), Is.False);
    }
}

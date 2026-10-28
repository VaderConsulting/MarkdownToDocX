using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramRelationshipIdentityMapTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void UniqueReconnectAndExactPointChangeRetainTheSameIdentity(bool SourceEnd)
    {
        var Previous = Edge("A", "B", "label");
        Previous.RoutingHint = new() { Leave = LayoutHintDirection.South, LeavePoint = 0 };
        var Accepted = Capture(Previous, Edge("X", "Y"));
        var Current = Edge(SourceEnd ? "C" : "A", SourceEnd ? "B" : "C", "label");
        Current.RoutingHint = new() { Leave = LayoutHintDirection.East, LeavePoint = -2, Enter = LayoutHintDirection.North, EnterPoint = 1 };
        var Result = Accepted.Reconcile(1, Model(Current, Edge("X", "Y")));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.Next!.Bindings.Select(Binding => Binding.Id), Is.EqualTo(Accepted.Bindings.Select(Binding => Binding.Id)));
        Assert.That(Result.AddedIds.Concat(Result.RemovedIds), Is.Empty);
    }

    [Test]
    public void ReconnectAndPointChangesCannotGuessBetweenMatchingSiblings()
    {
        var Accepted = Capture(Edge("A", "B", "label"), Edge("A", "C", "label"));
        var Current = Edge("A", "D", "label");
        Current.RoutingHint = new() { Leave = LayoutHintDirection.South, LeavePoint = 2 };
        var Result = Accepted.Reconcile(1, Model(Current));
        Assert.That(Result.FailureReason, Does.Contain("Ambiguous reconnect"));
        Assert.That(Result.Next, Is.Null);
    }
    [Test]
    public void EnterpriseReparseIgnoresCommentsLinesAndGeneratedIds()
    {
        const string Declarations = "enterprise TB\napplication-component A \"A\"\napplication-component B \"B\"\napplication-component C \"C\"\n";
        DiagramContract Before = Parse(Declarations + "A -flow-> B\nB -flow-> C\nA --- C");
        var Accepted = DiagramRelationshipIdentityMap.Capture("document", 1, Before);
        DiagramContract After = Parse(Declarations + "%% inserted comment\nC -flow-> A\n\nA -flow-> B\nB -flow-> C\nA --- C");
        var Result = Accepted.Reconcile(2, After);
        Assert.That(Result.FailureReason, Is.Null);
        Assert.Multiple(() =>
        {
            Assert.That(Result.Next!.Bindings.Skip(1).Select(Item => Item.Id), Is.EqualTo(Accepted.Bindings.Select(Item => Item.Id)));
            Assert.That(Result.AddedIds, Has.Count.EqualTo(1));
            Assert.That(Result.RemovedIds, Is.Empty);
            Assert.That(Result.Next.Bindings[1].ParserId, Is.Not.EqualTo(Accepted.Bindings[0].ParserId));
            Assert.That(After.Edges[1].Id, Is.EqualTo(Result.Next.Bindings[1].ParserId), "Reconciliation must not rewrite the parsed model.");
        });
    }

    [Test]
    public void ReorderedDistinctParallelDeclarationsKeepTheirOwnIdentities()
    {
        var Accepted = Capture(Edge("A", "B", "first"), Edge("A", "B", "second"));
        var Result = Accepted.Reconcile(1, Model(Edge("A", "B", "second"), Edge("A", "B", "inserted"), Edge("A", "B", "first")));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.Multiple(() =>
        {
            Assert.That(Result.Next!.Bindings[0].Id, Is.EqualTo(Accepted.Bindings[1].Id));
            Assert.That(Result.Next.Bindings[2].Id, Is.EqualTo(Accepted.Bindings[0].Id));
            Assert.That(Result.AddedIds, Is.EqualTo(new[] { Result.Next.Bindings[1].Id }));
        });
    }

    [Test]
    public void DeletingDistinctParallelDeclarationReportsOnlyItsIdentity()
    {
        var Accepted = Capture(Edge("A", "B", "first"), Edge("A", "B", "second"));
        var Result = Accepted.Reconcile(1, Model(Edge("A", "B", "second")));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.RemovedIds, Is.EqualTo(new[] { Accepted.Bindings[0].Id }));
        Assert.That(Result.Next!.Bindings[0].Id, Is.EqualTo(Accepted.Bindings[1].Id));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void UniqueReconnectRetainsIdentity(bool ChangeSource)
    {
        var Accepted = Capture(Edge("A", "B", "label"), Edge("X", "Y"));
        var Result = Accepted.Reconcile(1, Model(Edge(ChangeSource ? "C" : "A", ChangeSource ? "B" : "C", "label"), Edge("X", "Y")));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.Next!.Bindings.Select(Item => Item.Id), Is.EqualTo(Accepted.Bindings.Select(Item => Item.Id)));
        Assert.That(Result.AddedIds.Concat(Result.RemovedIds), Is.Empty);
    }

    [Test]
    public void ExplicitIdentitySurvivesBothEndpointsAndContentChanging()
    {
        EdgeContract Previous = Edge("A", "B", "old"); Previous.Id = "chosen"; Previous.IsUserDefinedId = true;
        var Accepted = Capture(Previous);
        EdgeContract Current = Edge("C", "D", "new"); Current.Id = "chosen"; Current.IsUserDefinedId = true;
        Current.RoutingHint = new() { Leave = LayoutHintDirection.West };
        var Result = Accepted.Reconcile(1, Model(Current));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.Next!.Bindings[0].Id, Is.EqualTo(Accepted.Bindings[0].Id));
    }

    [Test]
    public void UniqueEndpointPairRetainsIdentityForLabelKindAndPortChanges()
    {
        var Accepted = Capture(Edge("A", "B", "old"));
        EdgeContract Current = Edge("A", "B", "new");
        Current.MetadataMutable["enterpriseRelationshipKind"] = new RawMetadata("enterpriseRelationshipKind", "Triggering");
        Current.RoutingHint = new() { Enter = LayoutHintDirection.East };
        var Result = Accepted.Reconcile(1, Model(Current));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.Next!.Bindings[0].Id, Is.EqualTo(Accepted.Bindings[0].Id));
    }

    [Test]
    public void IdenticalParallelReparseUsesDocumentedOccurrenceOrder()
    {
        var Accepted = Capture(Edge("A", "B"), Edge("A", "B"));
        var Result = Accepted.Reconcile(1, Model(Edge("A", "B"), Edge("A", "B")));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.Next!.Bindings.Select(Item => Item.Id), Is.EqualTo(Accepted.Bindings.Select(Item => Item.Id)));
        Assert.That(Result.Notices.Single(), Does.Contain("occurrence order"));
    }

    [TestCase(1)]
    [TestCase(3)]
    public void ChangedIdenticalParallelCountIsAmbiguous(int Count)
    {
        var Accepted = Capture(Edge("A", "B"), Edge("A", "B"));
        var Result = Accepted.Reconcile(1, Model(Enumerable.Range(0, Count).Select(_ => Edge("A", "B")).ToArray()));
        Assert.That(Result.Next, Is.Null);
        Assert.That(Result.FailureReason, Does.Contain("indistinguishable"));
        Assert.That(Accepted.Bindings.Select(Item => Item.Id), Is.EqualTo(new[] { "relationship:1", "relationship:2" }));
    }

    [Test]
    public void MultipleParallelLabelChangesAreAmbiguous()
    {
        var Accepted = Capture(Edge("A", "B", "first"), Edge("A", "B", "second"));
        var Result = Accepted.Reconcile(1, Model(Edge("A", "B", "changed first"), Edge("A", "B", "changed second")));
        Assert.That(Result.Next, Is.Null);
        Assert.That(Result.FailureReason, Does.Contain("multiple unmatched"));
    }

    [Test]
    public void EditingOneParallelLabelPreservesBothIdentities()
    {
        var Accepted = Capture(Edge("A", "B", "first"), Edge("A", "B", "second"));
        var Result = Accepted.Reconcile(1, Model(Edge("A", "B", "changed"), Edge("A", "B", "second")));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.Next!.Bindings.Select(Item => Item.Id), Is.EqualTo(Accepted.Bindings.Select(Item => Item.Id)));
    }

    [Test]
    public void RevisionAfterAmbiguityComparesAgainstTheLastAcceptedMap()
    {
        var Accepted = Capture(Edge("A", "B"), Edge("A", "B"));
        Assert.That(Accepted.Reconcile(1, Model(Edge("A", "B"))).Next, Is.Null);
        var Result = Accepted.Reconcile(3, Model(Edge("A", "B"), Edge("A", "B"), Edge("X", "Y")));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.Next!.Bindings.Take(2).Select(Item => Item.Id), Is.EqualTo(Accepted.Bindings.Select(Item => Item.Id)));
        Assert.That(Result.AddedIds, Is.EqualTo(new[] { "relationship:3" }));
    }

    [Test]
    public void SelfLoopIdentitySurvivesReorderingAndPresentationChanges()
    {
        EdgeContract Loop = Edge("A", "A");
        var Accepted = Capture(Loop, Edge("X", "Y"));
        EdgeContract StyledLoop = Edge("A", "A");
        StyledLoop.ClassesMutable.Add("highlight");
        StyledLoop.CreationIndex = 37;
        var Result = Accepted.Reconcile(1, Model(Edge("X", "Y"), StyledLoop));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.Next!.Bindings[1].Id, Is.EqualTo(Accepted.Bindings[0].Id));
        Assert.That(Result.Next.Bindings[1].CreationIndex, Is.EqualTo(37));
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void ChangingAnExplicitIdentityMeansRemoveAndAdd(bool PreviouslyExplicit, bool CurrentlyExplicit)
    {
        EdgeContract Previous = Edge("A", "B"); Previous.Id = "old"; Previous.IsUserDefinedId = PreviouslyExplicit;
        var Accepted = Capture(Previous);
        EdgeContract Current = Edge("A", "B"); Current.Id = "new"; Current.IsUserDefinedId = CurrentlyExplicit;
        var Result = Accepted.Reconcile(1, Model(Current));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.RemovedIds, Is.EqualTo(new[] { Accepted.Bindings[0].Id }));
        Assert.That(Result.AddedIds, Is.EqualTo(new[] { Result.Next!.Bindings[0].Id }));
    }

    [Test]
    public void AmbiguousReconnectDoesNotGuessByCreationOrder()
    {
        var Accepted = Capture(Edge("A", "B"), Edge("A", "C"));
        var Result = Accepted.Reconcile(1, Model(Edge("A", "D")));
        Assert.That(Result.Next, Is.Null);
        Assert.That(Result.FailureReason, Does.Contain("Ambiguous reconnect"));
    }

    [Test]
    public void OneOldDeclarationCannotOwnTwoReconnects()
    {
        var Accepted = Capture(Edge("A", "B"));
        var Result = Accepted.Reconcile(1, Model(Edge("A", "C"), Edge("A", "D")));
        Assert.That(Result.Next, Is.Null);
        Assert.That(Result.FailureReason, Does.Contain("Ambiguous reconnect"));
    }

    [Test]
    public void ChangingBothEndpointsWithoutAnExplicitIdMeansRemoveAndAdd()
    {
        var Accepted = Capture(Edge("A", "B"));
        var Result = Accepted.Reconcile(1, Model(Edge("C", "D")));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.RemovedIds, Is.EqualTo(new[] { Accepted.Bindings[0].Id }));
        Assert.That(Result.AddedIds, Is.EqualTo(new[] { Result.Next!.Bindings[0].Id }));
        Assert.That(Result.Next.Bindings[0].Id, Is.Not.EqualTo(Accepted.Bindings[0].Id));
    }

    [Test]
    public void DeletedIdentitiesAreNotReusedOnReaddition()
    {
        var Accepted = Capture(Edge("A", "B"));
        var Empty = Accepted.Reconcile(1, Model()).Next!;
        var Readded = Empty.Reconcile(2, Model(Edge("A", "B")));
        Assert.That(Readded.Next!.Bindings[0].Id, Is.Not.EqualTo(Accepted.Bindings[0].Id));
    }

    [Test]
    public void DiscardedCandidatesLeaveAcceptedMapAndAllocationUntouched()
    {
        var Accepted = Capture(Edge("A", "B"));
        var Discarded = Accepted.Reconcile(1, Model(Edge("A", "B"), Edge("X", "Y")));
        var Later = Accepted.Reconcile(3, Model(Edge("A", "B"), Edge("P", "Q")));
        Assert.That(Accepted.SourceRevision, Is.Zero);
        Assert.That(Accepted.Bindings, Has.Count.EqualTo(1));
        Assert.That(Later.AddedIds, Is.EqualTo(Discarded.AddedIds));
        Assert.That(Later.Next!.SourceRevision, Is.EqualTo(3), "Invalid intermediate source revisions need not have a map.");
        Assert.That(Later.Next.DocumentId, Is.EqualTo("document"));
    }

    [Test]
    public void CapturedContentDoesNotAliasMutableParserObjects()
    {
        EdgeContract Original = Edge("A", "B", "label");
        Original.RoutingHint = new() { Leave = LayoutHintDirection.South };
        var Accepted = Capture(Original);
        Original.SourceId = "mutated"; Original.Label = "mutated"; Original.RoutingHint.Leave = LayoutHintDirection.North;
        EdgeContract Reconnected = Edge("A", "C", "label");
        Reconnected.RoutingHint = new() { Leave = LayoutHintDirection.South };
        var Result = Accepted.Reconcile(1, Model(Reconnected));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.Next!.Bindings[0].Id, Is.EqualTo(Accepted.Bindings[0].Id));
        Assert.That(Accepted.Bindings[0].SourceId, Is.EqualTo("A"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void StaleRevisionHasNoSuccessor(long Revision)
    {
        var Result = Capture(Edge("A", "B")).Reconcile(Revision, Model(Edge("A", "C")));
        Assert.That(Result.Next, Is.Null);
        Assert.That(Result.FailureReason, Does.Contain("revision"));
    }

    [Test]
    public void DuplicateExplicitIdsHaveNoSuccessor()
    {
        EdgeContract First = Edge("A", "B"); First.IsUserDefinedId = true; First.Id = "duplicate";
        EdgeContract Second = Edge("C", "D"); Second.IsUserDefinedId = true; Second.Id = "duplicate";
        var Result = Capture().Reconcile(1, Model(First, Second));
        Assert.That(Result.Next, Is.Null);
        Assert.That(Result.FailureReason, Does.Contain("unique"));
        Assert.Throws<ArgumentException>(() => Capture(First, Second));
    }

    [Test]
    public void ExplicitAndGeneratedParserIdNamespacesCannotCollide()
    {
        EdgeContract Explicit = Edge("A", "B"); Explicit.Id = "same"; Explicit.IsUserDefinedId = true;
        EdgeContract Generated = Edge("C", "D"); Generated.Id = "same";
        var Accepted = Capture(Explicit, Generated);
        var Result = Accepted.Reconcile(1, Model(Generated, Explicit));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Result.Next!.Bindings.Select(Item => Item.Id), Is.EqualTo(Accepted.Bindings.Reverse().Select(Item => Item.Id)));
    }

    [Test]
    public void EnterpriseKindsAndPlainConnectionsDisambiguateParallelReconnects()
    {
        const string Declarations = "enterprise TB\napplication-component A \"A\"\napplication-component B \"B\"\napplication-component C \"C\"\n";
        var Accepted = DiagramRelationshipIdentityMap.Capture("document", 0, Parse(Declarations + "A -flow-> B\nA -triggering-> B\nA --- B"));
        var Result = Accepted.Reconcile(1, Parse(Declarations + "A -triggering-> C\nA --- C\nA -flow-> C"));
        Assert.That(Result.FailureReason, Is.Null);
        Assert.That(Accepted.Bindings, Has.Count.EqualTo(3));
        // The mapper emits semantic relationships first, then drawing-only connections.
        Assert.That(Result.Next!.Bindings.Select(Item => Item.Id), Is.EqualTo(new[]
        {
            Accepted.Bindings[1].Id, Accepted.Bindings[0].Id, Accepted.Bindings[2].Id
        }));
    }

    private static DiagramRelationshipIdentityMap Capture(params EdgeContract[] Edges) =>
        DiagramRelationshipIdentityMap.Capture("document", 0, Model(Edges));

    private static DiagramContract Model(params EdgeContract[] Edges)
    {
        DiagramContract Model = new() { Kind = "flowchart", Direction = "TB" };
        Model.EdgesMutable.AddRange(Edges);
        return Model;
    }

    private static EdgeContract Edge(string Source, string Target, string? Label = null) =>
        new() { SourceId = Source, TargetId = Target, Label = Label };

    private static DiagramContract Parse(string Source) =>
        EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document);
}

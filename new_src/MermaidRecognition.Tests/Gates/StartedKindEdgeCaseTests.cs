using MermaidRecognition.Diagnostics;

namespace MermaidRecognition.Tests.Gates;

/// <summary>
/// Positive and negative edge cases for the nine started (L4) diagram kinds.
/// </summary>
[TestFixture]
public sealed class StartedKindEdgeCaseTests
{
    [TestCaseSource(typeof(DiagramKindEdgeCaseCatalog), nameof(DiagramKindEdgeCaseCatalog.StartedKindPositiveCases))]
    public void Parse_StartedKind_Positive_MinimalBody_HasStructuredFacts(string ExpectedKind, string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo(ExpectedKind));
        Assert.That(Contract.Diagnostics.Any(D => D.Severity == MermaidDiagnosticSeverity.Error), Is.False);
        Assert.That(
            Contract.SourceStatements.Count > 0 || Contract.Nodes.Count > 0 || Contract.Edges.Count > 0,
            Is.True);
    }

    [TestCaseSource(typeof(DiagramKindEdgeCaseCatalog), nameof(DiagramKindEdgeCaseCatalog.StartedKindNegativeCases))]
    public void Parse_StartedKind_HeaderOnlyOrEmptyBody_HasNoErrors(string ExpectedKind, string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo(ExpectedKind));
        Assert.That(Contract.Diagnostics.Any(D => D.Severity == MermaidDiagnosticSeverity.Error), Is.False);
    }

    [Test]
    public void Parse_Flowchart_Fixture_Chain_HasThreeNodesAndTwoEdges()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "chain-a-b-c.mmd");
        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(Contract.Kind, Is.EqualTo("flowchart"));
        Assert.That(Contract.Nodes.Count, Is.EqualTo(3));
        Assert.That(Contract.Edges.Count, Is.EqualTo(2));
    }

    [Test]
    public void Parse_Sequence_Fixture_ActorParticipant_HasParticipants()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetSequenceFixtureDirectory(), "actor-participant.mmd");
        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(Contract.Kind, Is.EqualTo("sequence"));
        Assert.That(Contract.SourceStatements.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Parse_Negative_StartedKind_WrongHeader_IsNotStartedKind()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("pie\n    \"Only\" : 1");

        Assert.That(Contract.Kind, Is.EqualTo("pie"));
        Assert.That(Contract.Kind, Is.Not.EqualTo("flowchart"));
    }
}

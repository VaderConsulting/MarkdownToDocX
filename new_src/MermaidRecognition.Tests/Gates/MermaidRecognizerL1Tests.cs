using MermaidRecognition.Tests.Gates;

namespace MermaidRecognition.Tests.Gates;

[TestFixture]
public sealed class MermaidRecognizerL1Tests
{
    [TestCaseSource(nameof(ProgrammeSmokeCases))]
    public void Parse_ProgrammeSmoke_PassesL1Gates(string ExpectedKind, string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo(ExpectedKind));
        Assert.That(Contract.Header, Is.Not.Empty);
        bool HasLineCatalogueOrGraphFacts = Contract.SourceStatements.Count > 0
            || (ExpectedKind == "flowchart" && Contract.Nodes.Count > 0 && Contract.Edges.Count > 0);
        Assert.That(HasLineCatalogueOrGraphFacts, Is.True);
        Assert.That(
            Contract.Diagnostics.Any(Diagnostic => Diagnostic.Severity == MermaidDiagnosticSeverity.Error),
            Is.False);
    }

    [Test]
    public void Parse_Pie_SectionsClassified()
    {
        string Source = "pie\n    \"Dogs\" : 40\n    \"Cats\" : 30";
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo("pie"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "pie.section"),
            Is.True);
    }

    [Test]
    public void Parse_Sequence_MessagesClassified()
    {
        string Source = "sequenceDiagram\n    Alice->>Bob: Hi";
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo("sequence"));
        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.message"),
            Is.True);
    }

    [Test]
    public void Parse_Flowchart_StillProducesNodesAndEdges()
    {
        string Source = "flowchart TD\n    A --> B";
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo("flowchart"));
        Assert.That(Contract.Nodes, Is.Not.Empty);
        Assert.That(Contract.Edges, Is.Not.Empty);
    }

    [Test]
    public void Parse_Unknown_EmitsErrorDiagnostic()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("not a diagram");

        Assert.That(Contract.Kind, Is.EqualTo("unknown"));
        Assert.That(
            Contract.Diagnostics.Any(Diagnostic => Diagnostic.Code == "recognition.unknown-diagram"),
            Is.True);
    }

    static IEnumerable<object[]> ProgrammeSmokeCases()
    {
        return L1SmokeSamples.ProgrammeKindAndSource();
    }
}

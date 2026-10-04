using MermaidRecognition.Diagnostics;

namespace MermaidRecognition.Tests.Gates;

/// <summary>
/// Positive and negative edge cases for every programme L1 diagram kind (pass 21).
/// </summary>
[TestFixture]
public sealed class DiagramKindEdgeCaseTests
{
    [TestCaseSource(nameof(PositiveMinimalCases))]
    public void Parse_Positive_Minimal_DetectsKind_WithStructuredFacts(string ExpectedKind, string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo(ExpectedKind));
        Assert.That(Contract.Diagnostics.Any(D => D.Severity == MermaidDiagnosticSeverity.Error), Is.False);
        Assert.That(Contract.SourceStatements.Count > 0 || Contract.Nodes.Count > 0, Is.True);
    }

    [TestCaseSource(nameof(PositiveEdgeCases))]
    public void Parse_Positive_EdgeVariants_RemainErrorFree(string ExpectedKind, string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo(ExpectedKind));
        Assert.That(Contract.Diagnostics.Any(D => D.Severity == MermaidDiagnosticSeverity.Error), Is.False);
    }

    [Test]
    public void Parse_Negative_EmptySource_IsUnknown()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(string.Empty);

        Assert.That(Contract.Kind, Is.EqualTo("unknown"));
        Assert.That(Contract.Diagnostics.Any(D => D.Code == "recognition.unknown-diagram"), Is.True);
    }

    [Test]
    public void Parse_Negative_WhitespaceOnly_IsUnknown()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("   \n  \t  ");

        Assert.That(Contract.Kind, Is.EqualTo("unknown"));
        Assert.That(Contract.Diagnostics.Any(D => D.Code == "recognition.unknown-diagram"), Is.True);
    }

    [Test]
    public void Parse_Negative_RandomText_IsUnknown()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("This is not a diagram at all.");

        Assert.That(Contract.Kind, Is.EqualTo("unknown"));
        Assert.That(Contract.Diagnostics.Any(D => D.Code == "recognition.unknown-diagram"), Is.True);
    }

    [TestCaseSource(nameof(NegativeWrongHeaderCases))]
    public void Parse_Negative_WrongHeader_DoesNotMatchKind(string WrongHeaderSource)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(WrongHeaderSource);

        Assert.That(Contract.Kind, Is.Not.EqualTo("flowchart"));
        Assert.That(Contract.Kind, Is.Not.EqualTo("sequence"));
    }

    [Test]
    public void Parse_Negative_FlowchartHeaderOnly_EmitsMissingHeaderOrRemainsParseable()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TD");

        Assert.That(Contract.Kind, Is.EqualTo("flowchart"));
        Assert.That(
            Contract.Diagnostics.Any(D => D.Code == "recognition.missing-header")
            || Contract.Nodes.Count == 0,
            Is.True);
    }

    [Test]
    public void Parse_Negative_CatalogueRemarkedOnly_EmitsCatalogueRemarkInfo()
    {
        string Source = """
            %% flowchart TD
            %%     A --> B
            """;
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.RemarkStatus, Is.EqualTo(MermaidDiagramRemarkStatus.CatalogueRemarked));
        Assert.That(Contract.Diagnostics.Any(D => D.Code == "recognition.catalogue-remarked"), Is.True);
    }

    [TestCaseSource(nameof(MermaidOnlyKindCases))]
    public void Parse_MermaidOnly_Kinds_EmitUnsupportedNativeWarning(string ExpectedKind, string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo(ExpectedKind));
        Assert.That(Contract.Diagnostics.Any(D => D.Code == "recognition.unsupported-native"), Is.True);
    }

    [TestCaseSource(typeof(DiagramKindEdgeCaseCatalog), nameof(DiagramKindEdgeCaseCatalog.LinePatternHeaderOnlyCases))]
    public void Parse_Negative_LinePatternKinds_HeaderOnly_NoErrors(string ExpectedKind, string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo(ExpectedKind));
        Assert.That(Contract.Diagnostics.Any(D => D.Severity == MermaidDiagnosticSeverity.Error), Is.False);
        Assert.That(Contract.SourceStatements.Any(S => S.LineTypeId.Contains(".header", StringComparison.Ordinal)), Is.True);
    }

    [TestCaseSource(typeof(DiagramKindEdgeCaseCatalog), nameof(DiagramKindEdgeCaseCatalog.MermaidOnlyFixtureCases))]
    public void Parse_MermaidOnly_FixtureFiles_EmitUnsupportedNativeWarning(string ExpectedKind, string FixtureFileName)
    {
        string FixturePath = Path.Combine(
            RecognitionFixturePaths.GetProjectDirectory(),
            "MermaidOnlyRecognition",
            FixtureFileName);
        Assert.That(File.Exists(FixturePath), Is.True, "Fixture missing: " + FixturePath);

        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(FixturePath));

        Assert.That(Contract.Kind, Is.EqualTo(ExpectedKind));
        Assert.That(Contract.Diagnostics.Any(D => D.Code == "recognition.unsupported-native"), Is.True);
    }

    [Test]
    public void Parse_Negative_YamlFrontMatterTrap_IsNotFlowchart()
    {
        string FixturePath = Path.Combine(
            RecognitionFixturePaths.GetProjectDirectory(),
            "MermaidOnlyRecognition",
            "yaml-front-matter.mmd");
        DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(FixturePath));

        Assert.That(Contract.Kind, Is.EqualTo("yaml-front-matter"));
        Assert.That(Contract.Kind, Is.Not.EqualTo("flowchart"));
        Assert.That(Contract.Diagnostics.Any(D => D.Code == "recognition.yaml-front-matter"), Is.True);
    }

    [Test]
    public void Parse_Positive_GraphHeader_IsFlowchartKind()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("graph LR\n    A --> B");

        Assert.That(Contract.Kind, Is.EqualTo("flowchart"));
        Assert.That(Contract.Diagnostics.Any(D => D.Severity == MermaidDiagnosticSeverity.Error), Is.False);
    }

    [TestCaseSource(nameof(NegativeMalformedLineCases))]
    public void Parse_Negative_StartedKinds_MalformedLine_NoErrors(string ExpectedKind, string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo(ExpectedKind));
        Assert.That(Contract.Diagnostics.Any(D => D.Severity == MermaidDiagnosticSeverity.Error), Is.False);
    }

    static IEnumerable<object[]> PositiveMinimalCases()
    {
        foreach (object[] Row in L1SmokeSamples.ProgrammeKindAndSource())
        {
            yield return Row;
        }
    }

    static IEnumerable<object[]> PositiveEdgeCases()
    {
        yield return new object[] { "flowchart", "flowchart LR\n    N1[\"Unicode αβγ\"] --> N2[End]" };
        yield return new object[] { "sequence", "sequenceDiagram\n    %% comment\n    A->>B: ping" };
        yield return new object[] { "classDiagram", "classDiagram\n    direction TB\n    class Animal" };
        yield return new object[] { "stateDiagram", "stateDiagram-v2\n    [*] --> Idle\n    Idle --> Done" };
        yield return new object[] { "erDiagram", "erDiagram\n    A ||--o{ B : rel" };
        yield return new object[] { "pie", "pie title T\n    \"Slice\" : 42" };
        yield return new object[] { "gantt", "gantt\n    dateFormat YYYY-MM-DD\n    section S\n    T :a1, 2024-01-01, 1d" };
        yield return new object[] { "mindmap", "mindmap\n  root((Root))\n    child" };
        yield return new object[] { "gitGraph", "gitGraph\n    commit id: \"abc\"\n    branch develop" };
    }

    static IEnumerable<object[]> NegativeWrongHeaderCases()
    {
        yield return new object[] { "notARealDiagramType\n    A --> B" };
        yield return new object[] { "flwchart TD\n    A --> B" };
    }

    static IEnumerable<object[]> MermaidOnlyKindCases()
    {
        yield return new object[] { "eventmodeling", "eventmodeling\n    title E\n    evt OrderPlaced" };
        yield return new object[] { "treeview", "treeView-beta\n    title T\n    A" };
        yield return new object[] { "ishikawa", "ishikawa\n    title I\n    branch A" };
        yield return new object[] { "venn", "venn-beta\n    title V\n    A [Set A]" };
        yield return new object[] { "wardley", "wardley-beta\n    title W\n    componentA[Component A]" };
        yield return new object[] { "info", "info\n    showInfo" };
    }

    static IEnumerable<object[]> NegativeMalformedLineCases()
    {
        yield return new object[] { "flowchart", "flowchart TD\n    @@@not-valid@@@" };
        yield return new object[] { "sequence", "sequenceDiagram\n    @@@bad@@@" };
        yield return new object[] { "classDiagram", "classDiagram\n    @@@bad@@@" };
        yield return new object[] { "stateDiagram", "stateDiagram-v2\n    @@@bad@@@" };
        yield return new object[] { "erDiagram", "erDiagram\n    @@@bad@@@" };
        yield return new object[] { "pie", "pie\n    @@@bad@@@" };
        yield return new object[] { "gantt", "gantt\n    @@@bad@@@" };
        yield return new object[] { "mindmap", "mindmap\n  @@@bad@@@" };
        yield return new object[] { "gitGraph", "gitGraph\n    @@@bad@@@" };
        yield return new object[] { "timeline", "timeline\n    @@@bad@@@" };
        yield return new object[] { "journey", "journey\n    @@@bad@@@" };
        yield return new object[] { "quadrantChart", "quadrantChart\n    @@@bad@@@" };
        yield return new object[] { "xychart", "xychart-beta\n    @@@bad@@@" };
        yield return new object[] { "requirement", "requirementDiagram\n    @@@bad@@@" };
        yield return new object[] { "architecture", "architecture\n    @@@bad@@@" };
        yield return new object[] { "kanban", "kanban\n    @@@bad@@@" };
        yield return new object[] { "sankey", "sankey-beta\n    @@@bad@@@" };
        yield return new object[] { "block", "block-beta\n    @@@bad@@@" };
        yield return new object[] { "packet", "packet-beta\n    @@@bad@@@" };
        yield return new object[] { "radar", "radar-beta\n    @@@bad@@@" };
        yield return new object[] { "treemap", "treemap\n    @@@bad@@@" };
        yield return new object[] { "c4", "C4Context\n    @@@bad@@@" };
    }
}

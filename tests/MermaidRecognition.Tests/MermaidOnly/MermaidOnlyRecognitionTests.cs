namespace MermaidRecognition.Tests.MermaidOnly;

/// <summary>
/// L1 gates for Mermaid-only kinds (§3.18.2) and the YAML <c>---</c> trap.
/// </summary>
[TestFixture]
public sealed class MermaidOnlyRecognitionTests
{
    private static readonly MermaidOnlyCase[] Cases =
    {
        new MermaidOnlyCase("eventmodeling", "eventmodeling", "recognition.unsupported-native"),
        new MermaidOnlyCase("treeview", "treeview", "recognition.unsupported-native"),
        new MermaidOnlyCase("ishikawa", "ishikawa", "recognition.unsupported-native"),
        new MermaidOnlyCase("venn", "venn", "recognition.unsupported-native"),
        new MermaidOnlyCase("wardley", "wardley", "recognition.unsupported-native"),
        new MermaidOnlyCase("info", "info", "recognition.unsupported-native"),
        new MermaidOnlyCase("yaml-front-matter", "yaml-front-matter", "recognition.yaml-front-matter"),
    };

    [TestCaseSource(nameof(MermaidOnlyCases))]
    public void Fixture_Passes_L1_Gates(MermaidOnlyCase Case)
    {
        string MmdPath = GetFixturePath(Case.FixtureName + ".mmd");
        if (!File.Exists(MmdPath))
        {
            Assert.Ignore("Run MermaidOnlyRecognitionFixtureWriter.Write_All_MermaidOnly_Fixtures first.");
        }

        string Source = File.ReadAllText(MmdPath);
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(Contract.Kind, Is.EqualTo(Case.ExpectedKind));
        Assert.That(Contract.Header, Is.Not.Empty);
        Assert.That(Contract.SourceStatements, Is.Not.Empty);
        Assert.That(
            Contract.Diagnostics.Any(Diagnostic => Diagnostic.Code == Case.ExpectedDiagnosticCode),
            Is.True,
            "Expected diagnostic " + Case.ExpectedDiagnosticCode);
        Assert.That(
            Contract.Diagnostics.Any(Diagnostic => Diagnostic.Severity == MermaidDiagnosticSeverity.Error),
            Is.False);
    }

    [TestCaseSource(nameof(MermaidOnlyCases))]
    public void Fixture_Matches_Verified_Json(MermaidOnlyCase Case)
    {
        string JsonPath = GetFixturePath(Case.FixtureName + ".verified.json");
        string MmdPath = GetFixturePath(Case.FixtureName + ".mmd");
        if (!File.Exists(JsonPath) || !File.Exists(MmdPath))
        {
            Assert.Ignore("Run MermaidOnlyRecognitionFixtureWriter.Write_All_MermaidOnly_Fixtures first.");
        }

        string Source = File.ReadAllText(MmdPath);
        ContractSnapshot Actual = ContractSnapshot.FromDiagram(MermaidRecognizer.Parse(Source));
        ContractSnapshot Expected = ContractSnapshot.FromJson(File.ReadAllText(JsonPath));
        Assert.That(Actual.ToJson(), Is.EqualTo(Expected.ToJson()));
    }

    static IEnumerable<MermaidOnlyCase> MermaidOnlyCases()
    {
        foreach (MermaidOnlyCase Case in Cases)
        {
            yield return Case;
        }
    }

    static string GetFixturePath(string FileName)
    {
        return Path.Combine(
            RecognitionFixturePaths.GetKindFixtureDirectory("MermaidOnlyRecognition"),
            FileName);
    }

    public readonly struct MermaidOnlyCase
    {
        public MermaidOnlyCase(string FixtureNameValue, string ExpectedKindValue, string ExpectedDiagnosticCodeValue)
        {
            FixtureName = FixtureNameValue;
            ExpectedKind = ExpectedKindValue;
            ExpectedDiagnosticCode = ExpectedDiagnosticCodeValue;
        }

        public string FixtureName { get; }

        public string ExpectedKind { get; }

        public string ExpectedDiagnosticCode { get; }
    }
}

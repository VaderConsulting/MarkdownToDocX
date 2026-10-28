using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Er;

[TestFixture]
public sealed class ErRecognitionFixtureTests
{
    private static readonly string[] FixtureNames =
    [
        "relationship",
        "relation-cardinality",
        "entity-attributes",
        "cardinality-zero-or-one",
        "cardinality-zero-or-more-left",
        "cardinality-zero-or-one-dotted",
        "cardinality-zero-or-more-both",
        "cardinality-one-or-more-dotted",
        "cardinality-zero-or-more-dotted",
        "cardinality-optional-zero-or-more",
        "cardinality-zero-or-more-zero-or-one-dotted",
        "direction",
        "attribute-keys",
        "classDef-style",
        "inline-style",
        "comment",
        "accessibility",
        "one-to-one",
        "non-identifying",
        "associative-entity",
        "quoted-relationship-label",
        "multiple-relationships",
        "catalogue-remarked-entity-alias",
        "attribute-comments",
        "entity-only",
        "accDescr-block",
        "combined-schema",
        "recursive-relationship",
        "duplicate-role-labels",
        "init-leading",
        "hyphenated-entities",
        "class-apply-only",
        "relationship-before-entity",
        "complex-smoke",
    ];

    private static readonly string[] FixturesAllowingBody =
    [
        "accDescr-block",
    ];

    [TestCaseSource(nameof(FixtureNameCases))]
    public void Fixture_HasNoBodyFallback(string FixtureName)
    {
        if (FixturesAllowingBody.Contains(FixtureName, StringComparer.Ordinal))
        {
            return;
        }

        string Source = File.ReadAllText(GetFixturePath(FixtureName + ".mmd"));
        DiagramContract Contract = MermaidRecognizer.Parse(Source);

        Assert.That(
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "erDiagram.body"),
            Is.False,
            "Fixture " + FixtureName + " must not use erDiagram.body fallback.");
    }

    [TestCaseSource(nameof(FixtureNameCases))]
    public void Fixture_Matches_Verified_Json(string FixtureName)
    {
        string JsonPath = GetFixturePath(FixtureName + ".verified.json");
        string MmdPath = GetFixturePath(FixtureName + ".mmd");
        if (!File.Exists(JsonPath) || !File.Exists(MmdPath))
        {
            Assert.Ignore("Run ErRecognitionFixtureWriter.Write_All_Er_Fixtures first.");
        }

        string Source = File.ReadAllText(MmdPath);
        ContractSnapshot Actual = ContractSnapshot.FromDiagram(MermaidRecognizer.Parse(Source));
        ContractSnapshot Expected = ContractSnapshot.FromJson(File.ReadAllText(JsonPath));
        Assert.That(Actual.ToJson(), Is.EqualTo(Expected.ToJson()));
    }

    static IEnumerable<string> FixtureNameCases()
    {
        foreach (string Name in FixtureNames)
        {
            yield return Name;
        }
    }

    static string GetFixturePath(string FileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "ErFixtures", FileName);
    }
}

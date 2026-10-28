using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests.Sequence;

/// <summary>
/// Verified JSON and line-type gate tests for sequence fixtures (§11.1.14.2).
/// </summary>
[TestFixture]
public sealed class SequenceRecognitionFixtureTests
{
    private static readonly string[] FixtureNames =
    [
        "simple",
        "participants",
        "activation",
        "notes",
        "autonumber",
        "title",
        "alt-else-blocks",
        "activate-deactivate",
        "loop-control",
        "sequence-accessibility",
        "par-block",
        "par-over",
        "opt-block",
        "critical-block",
        "box-rgb",
        "lifecycle-create",
        "implicit-participants",
        "self-message",
        "arrow-variants",
        "accDescr-block",
        "actor-participant",
        "participant-order",
        "multi-hop",
        "comment",
        "init-leading",
        "break-block",
        "rect-block",
        "closed-arrows",
        "nested-blocks",
        "else-only",
        "long-message-label",
        "quoted-message-label",
        "open-cross-arrows",
        "auth-smoke",
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
            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "sequence.body"),
            Is.False,
            "Fixture " + FixtureName + " must not use sequence.body fallback.");
    }

    [TestCaseSource(nameof(FixtureNameCases))]
    public void Fixture_Matches_Verified_Json(string FixtureName)
    {
        string JsonPath = GetFixturePath(FixtureName + ".verified.json");
        string MmdPath = GetFixturePath(FixtureName + ".mmd");
        if (!File.Exists(JsonPath) || !File.Exists(MmdPath))
        {
            Assert.Ignore(
                "Run SequenceRecognitionFixtureWriter.Write_All_Sequence_Fixtures first.");
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
        return Path.Combine(AppContext.BaseDirectory, "SequenceFixtures", FileName);
    }
}

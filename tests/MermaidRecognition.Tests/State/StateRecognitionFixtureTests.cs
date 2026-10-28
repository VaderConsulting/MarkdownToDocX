using MermaidRecognition.Contract;



namespace MermaidRecognition.Tests.State;



[TestFixture]

public sealed class StateRecognitionFixtureTests

{

    private static readonly string[] FixtureNames =

    [

        "simple",

        "composite",

        "note",

        "fork-join",

        "choice",

        "direction",

        "transition-labels",

        "transition-colons",

        "classDef-style",

        "accessibility",

        "click-href",

        "init-leading",

        "state-description",

        "inline-style",

        "struct-open",

        "note-left",

        "accDescr-block",

        "comment",

        "multiple-states",

        "header-v1",

        "multiple-terminators",

        "hide-empty-description",

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

            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "stateDiagram.body"),

            Is.False,

            "Fixture " + FixtureName + " must not use stateDiagram.body fallback.");

    }



    [TestCaseSource(nameof(FixtureNameCases))]

    public void Fixture_Matches_Verified_Json(string FixtureName)

    {

        string JsonPath = GetFixturePath(FixtureName + ".verified.json");

        string MmdPath = GetFixturePath(FixtureName + ".mmd");

        if (!File.Exists(JsonPath) || !File.Exists(MmdPath))

        {

            Assert.Ignore("Run StateRecognitionFixtureWriter.Write_All_State_Fixtures first.");

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

        return Path.Combine(AppContext.BaseDirectory, "StateFixtures", FileName);

    }

}



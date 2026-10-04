using MermaidRecognition.Contract;



namespace MermaidRecognition.Tests.Class;



[TestFixture]

public sealed class ClassRecognitionFixtureTests

{

    private static readonly string[] FixtureNames =

    {

        "simple",

        "members",

        "multi-level-inheritance",

        "classDef-style",

        "relation-composition",

        "relation-aggregation",

        "relation-association",

        "relation-dependency",

        "relation-realization",

        "relation-reverse-association",

        "relation-solid-link",

        "relation-dotted-link",

        "multiple-classes",

        "inline-style",

        "direction",

        "namespace",

        "accessibility",

        "accessibility-inline",

        "note",

        "click-href",

        "interface-annotation",

        "abstract-stereotype",

        "enum-service-stereotype",

        "relation-cardinality",

        "init-leading",

        "header-v2",

        "generic-class",

        "methods-only",

        "members-and-methods",

        "visibility-modifiers",

        "inheritance",

        "comment",

        "static-members",

        "complex-smoke",

    };



    private static readonly string[] FixturesAllowingBody =

    {

        "accessibility",

    };



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

            Contract.SourceStatements.Any(Statement => Statement.LineTypeId == "classDiagram.body"),

            Is.False,

            "Fixture " + FixtureName + " must not use classDiagram.body fallback.");

    }



    [TestCaseSource(nameof(FixtureNameCases))]

    public void Fixture_Matches_Verified_Json(string FixtureName)

    {

        string JsonPath = GetFixturePath(FixtureName + ".verified.json");

        string MmdPath = GetFixturePath(FixtureName + ".mmd");

        if (!File.Exists(JsonPath) || !File.Exists(MmdPath))

        {

            Assert.Ignore("Run ClassRecognitionFixtureWriter.Write_All_Class_Fixtures first.");

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

        return Path.Combine(AppContext.BaseDirectory, "ClassFixtures", FileName);

    }

}



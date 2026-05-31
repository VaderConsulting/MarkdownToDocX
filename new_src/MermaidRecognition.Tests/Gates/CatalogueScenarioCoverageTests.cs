namespace MermaidRecognition.Tests.Gates;

/// <summary>
/// Ensures catalogue scenario index tables map to existing recognition <c>.mmd</c> fixtures.
/// </summary>
[TestFixture]
public sealed class CatalogueScenarioCoverageTests
{
    private const string SCENARIO_INDEX_HEADING_PREFIX = "## Scenario index";

    private static readonly HashSet<string> RemarkScenarioIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "C24",
        "ST19",
        "E10",
    };

    [Test]
    public void Sequence_CatalogueScenarioCoverage_Fixtures_Exist()
    {
        AssertCatalogueFixturesExist(
            "sequence.md",
            RecognitionFixturePaths.GetSequenceFixtureDirectory());
    }

    [Test]
    public void Class_CatalogueScenarioCoverage_Fixtures_Exist()
    {
        AssertCatalogueFixturesExist(
            "class.md",
            RecognitionFixturePaths.GetClassFixtureDirectory());
    }

    [Test]
    public void State_CatalogueScenarioCoverage_Fixtures_Exist()
    {
        AssertCatalogueFixturesExist(
            "state.md",
            RecognitionFixturePaths.GetStateFixtureDirectory());
    }

    [Test]
    public void EntityRelationship_CatalogueScenarioCoverage_Fixtures_Exist()
    {
        AssertCatalogueFixturesExist(
            "entityrelationship.md",
            RecognitionFixturePaths.GetErFixtureDirectory());
    }

    private static void AssertCatalogueFixturesExist(string CatalogueFileName, string FixtureDirectory)
    {
        string ProjectDirectory = RecognitionFixturePaths.GetProjectDirectory();
        string CataloguePath = Path.GetFullPath(Path.Combine(ProjectDirectory, "..", "..", CatalogueFileName));
        Assert.That(File.Exists(CataloguePath), Is.True, "Missing catalogue: " + CataloguePath);

        string Markdown = File.ReadAllText(CataloguePath);
        List<CatalogueScenarioRow> Rows = ParseScenarioIndexRows(Markdown);
        Assert.That(Rows, Is.Not.Empty, "No scenario index rows in " + CatalogueFileName);

        foreach (CatalogueScenarioRow Row in Rows)
        {
            string MmdPath = Path.Combine(FixtureDirectory, Row.FixtureName + ".mmd");
            Assert.That(
                File.Exists(MmdPath),
                Is.True,
                CatalogueFileName + " " + Row.ScenarioId + " -> " + Row.FixtureName + ".mmd");
        }
    }

    private static List<CatalogueScenarioRow> ParseScenarioIndexRows(string Markdown)
    {
        List<CatalogueScenarioRow> Rows = new List<CatalogueScenarioRow>();
        string[] Lines = Markdown.Split('\n');
        bool InScenarioIndex = false;

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            string Line = Lines[LineIndex].TrimEnd('\r');

            if (Line.StartsWith(SCENARIO_INDEX_HEADING_PREFIX, StringComparison.Ordinal))
            {
                InScenarioIndex = true;
                continue;
            }

            if (!InScenarioIndex)
            {
                continue;
            }

            if (Line.StartsWith("## ", StringComparison.Ordinal) && !Line.StartsWith(SCENARIO_INDEX_HEADING_PREFIX, StringComparison.Ordinal))
            {
                break;
            }

            if (!Line.StartsWith('|'))
            {
                if (Rows.Count > 0)
                {
                    break;
                }

                continue;
            }

            if (IsTableSeparatorRow(Line))
            {
                continue;
            }

            if (!TryParseScenarioIndexRow(Line, out string ScenarioId, out string FixtureCell))
            {
                continue;
            }

            if (string.Equals(ScenarioId, "Id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (RemarkScenarioIds.Contains(ScenarioId))
            {
                continue;
            }

            string FixtureName = NormaliseFixtureName(FixtureCell);
            if (!ShouldIncludeFixture(FixtureName, FixtureCell))
            {
                continue;
            }

            Rows.Add(new CatalogueScenarioRow(ScenarioId, FixtureName));
        }

        return Rows;
    }

    private static bool IsTableSeparatorRow(string Line)
    {
        return Line.Contains("---", StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses id and recognition-fixture columns; line types may contain unescaped <c>|</c> (e.g. <c>|o--o|</c>).
    /// </summary>
    private static bool TryParseScenarioIndexRow(string Line, out string ScenarioId, out string FixtureCell)
    {
        ScenarioId = string.Empty;
        FixtureCell = string.Empty;

        string Trimmed = Line.Trim();
        if (!Trimmed.StartsWith('|'))
        {
            return false;
        }

        if (Trimmed.EndsWith('|'))
        {
            Trimmed = Trimmed.Substring(0, Trimmed.Length - 1);
        }

        if (Trimmed.StartsWith('|'))
        {
            Trimmed = Trimmed.Substring(1);
        }

        int LastPipeIndex = Trimmed.LastIndexOf('|');
        if (LastPipeIndex < 0)
        {
            return false;
        }

        string BeforeNaiadSample = Trimmed.Substring(0, LastPipeIndex);
        int FixturePipeIndex = BeforeNaiadSample.LastIndexOf('|');
        if (FixturePipeIndex < 0)
        {
            return false;
        }

        FixtureCell = BeforeNaiadSample.Substring(FixturePipeIndex + 1).Trim();
        string BeforeFixture = BeforeNaiadSample.Substring(0, FixturePipeIndex);

        int IdPipeIndex = BeforeFixture.IndexOf('|');
        if (IdPipeIndex < 0)
        {
            return false;
        }

        ScenarioId = BeforeFixture.Substring(0, IdPipeIndex).Trim();
        string AfterId = BeforeFixture.Substring(IdPipeIndex + 1);
        int LevelPipeIndex = AfterId.IndexOf('|');
        if (LevelPipeIndex < 0)
        {
            return false;
        }

        return !string.IsNullOrEmpty(ScenarioId);
    }

    private static string NormaliseFixtureName(string FixtureCell)
    {
        string Name = FixtureCell.Trim();
        if (Name.StartsWith('`') && Name.EndsWith('`') && Name.Length >= 2)
        {
            Name = Name.Substring(1, Name.Length - 2);
        }

        return Name.Trim();
    }

    private static bool ShouldIncludeFixture(string FixtureName, string FixtureCell)
    {
        if (string.IsNullOrWhiteSpace(FixtureName))
        {
            return false;
        }

        if (string.Equals(FixtureName, "—", StringComparison.Ordinal)
            || string.Equals(FixtureName, "-", StringComparison.Ordinal))
        {
            return false;
        }

        if (FixtureCell.Contains("remarked", StringComparison.OrdinalIgnoreCase)
            || FixtureName.Contains("remarked", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private sealed class CatalogueScenarioRow
    {
        private readonly string _ScenarioId;
        private readonly string _FixtureName;

        public CatalogueScenarioRow(string ScenarioId, string FixtureName)
        {
            _ScenarioId = ScenarioId;
            _FixtureName = FixtureName;
        }

        public string ScenarioId
        {
            get
            {
                return _ScenarioId;
            }
        }

        public string FixtureName
        {
            get
            {
                return _FixtureName;
            }
        }
    }
}

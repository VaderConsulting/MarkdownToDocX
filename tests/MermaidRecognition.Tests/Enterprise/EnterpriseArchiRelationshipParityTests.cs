using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseArchiRelationshipParityTests
{
    [Test]
    public void EmbeddedRules_MatchPinnedArchiSnapshots_For31And32()
    {
        List<string> Types = new List<string>(EnterpriseRulesCatalog.Instance.Types);
        Assert.That(Types.Count, Is.EqualTo(61));

        string[][] Expected31 = EnterpriseArchiRelationshipsXml.BuildMatrix(ReadSnapshot("relationships-3.1.xml"), Types);
        string[][] Expected32 = EnterpriseArchiRelationshipsXml.BuildMatrix(ReadSnapshot("relationships-3.2.xml"), Types);
        List<string> Mismatches = [];
        CollectMismatches(Types, "3.1", Expected31, Mismatches);
        CollectMismatches(Types, "3.2", Expected32, Mismatches);
        Assert.That(Mismatches, Is.Empty, string.Join(Environment.NewLine, Mismatches));
    }

    [Test]
    public void Rebuilt31Table_FixesTheReportedNewLayerCells()
    {
        EnterpriseRulesCatalog Rules = EnterpriseRulesCatalog.Instance;
        Assert.That(Rules.GetAllowedLetters("3.1", "plateau", "outcome"), Is.EqualTo("nor"));
        Assert.That(Rules.GetAllowedLetters("3.2", "plateau", "outcome"), Is.EqualTo("cgnor"));
        Assert.That(Rules.GetAllowedLetters("3.1", "location", "work-package"), Is.EqualTo("cgo"));
        Assert.That(Rules.GetAllowedLetters("3.2", "location", "work-package"), Is.EqualTo("cgio"));
        Assert.That(Rules.GetAllowedLetters("3.1", "material", "artifact"), Is.EqualTo("o"));
        Assert.That(Rules.GetAllowedLetters("3.2", "material", "artifact"), Is.EqualTo("or"));
        Assert.That(Rules.GetAllowedLetters("3.1", "device", "node"), Is.EqualTo("cfgiorstv"));
        Assert.That(Rules.GetAllowedLetters("3.2", "device", "node"), Is.EqualTo("fotv"));
    }

    private static void CollectMismatches(
        List<string> Types,
        string Version,
        string[][] Expected,
        List<string> Mismatches)
    {
        EnterpriseRulesCatalog Rules = EnterpriseRulesCatalog.Instance;
        for (int Row = 0; Row < Types.Count; Row++)
        {
            for (int Column = 0; Column < Types.Count; Column++)
            {
                string Actual = EnterpriseArchiRelationshipsXml.NormaliseLetters(
                    Rules.GetAllowedLetters(Version, Types[Row], Types[Column]));
                string Wanted = EnterpriseArchiRelationshipsXml.NormaliseLetters(Expected[Row][Column]);
                if (Actual != Wanted)
                {
                    Mismatches.Add(
                        Version + " " + Types[Row] + " -> " + Types[Column]
                        + " embedded='" + Actual + "' archi='" + Wanted + "'");
                }
            }
        }
    }

    private static string ReadSnapshot(string FileName)
    {
        DirectoryInfo? DirectoryCursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (DirectoryCursor != null)
        {
            string Candidate = Path.Combine(DirectoryCursor.FullName, "docs", "enterprise", "archi", FileName);
            if (File.Exists(Candidate))
            {
                return File.ReadAllText(Candidate);
            }

            DirectoryCursor = DirectoryCursor.Parent;
        }

        string Copied = Path.Combine(AppContext.BaseDirectory, "EnterpriseArchi", FileName);
        if (File.Exists(Copied))
        {
            return File.ReadAllText(Copied);
        }

        throw new FileNotFoundException("Archi snapshot not found: " + FileName);
    }
}

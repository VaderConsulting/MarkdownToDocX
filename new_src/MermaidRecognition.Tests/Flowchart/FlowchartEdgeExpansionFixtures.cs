using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

/// <summary>
/// Writes machine-readable contract snapshots for §7.1.1 fixtures (update when parser output is intentional).
/// </summary>
public class FlowchartEdgeExpansionFixtures
{
    [Test]
    public void Write_EdgeExpansion_Snapshots()
    {
        string BaseDir = Path.Combine(
            GetTestProjectDirectory(),
            "FlowchartRecognition");
        Directory.CreateDirectory(BaseDir);

        WriteFixture(BaseDir, "chain-a-b-c.mmd", "A --> B --> C", "chain-a-b-c.verified.json");
        WriteFixture(BaseDir, "grouped-a-b-to-c.mmd", "A & B --> C", "grouped-a-b-to-c.verified.json");
        WriteFixture(BaseDir, "chained-a-b-c-d.mmd", "A --> B & C --> D", "chained-a-b-c-d.verified.json");
    }

    static string GetTestProjectDirectory()
    {
        string? DirectoryPath = Path.GetDirectoryName(typeof(FlowchartEdgeExpansionFixtures).Assembly.Location);
        if (DirectoryPath is null)
        {
            return "FlowchartRecognition";
        }

        string? ProjectDir = Directory.GetParent(DirectoryPath)?.Parent?.Parent?.Parent?.FullName;
        if (ProjectDir is not null && File.Exists(Path.Combine(ProjectDir, "MermaidRecognition.Tests.csproj")))
        {
            return ProjectDir;
        }

        return Path.Combine(DirectoryPath, "Fixtures");
    }

    static void WriteFixture(string BaseDir, string MarkdownName, string Statement, string JsonName)
    {
        string Source =
            """
            flowchart TD
                STATEMENT
            """.Replace("STATEMENT", Statement, StringComparison.Ordinal);
        DiagramContract Contract = FlowchartRecognizer.Parse(Source);
        ContractSnapshot Snapshot = ContractSnapshot.FromDiagram(Contract);
        File.WriteAllText(Path.Combine(BaseDir, JsonName), Snapshot.ToJson());
        File.WriteAllText(Path.Combine(BaseDir, MarkdownName), Source);
    }
}

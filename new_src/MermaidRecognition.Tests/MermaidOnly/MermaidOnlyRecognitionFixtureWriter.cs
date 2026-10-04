namespace MermaidRecognition.Tests.MermaidOnly;

/// <summary>
/// Regenerates L1 smoke fixtures for Mermaid-only diagram kinds and the YAML front-matter trap.
/// </summary>
[TestFixture]
public sealed class MermaidOnlyRecognitionFixtureWriter
{
    [Test]
    [Explicit("Regenerates baseline fixtures; select this test explicitly to update them.")]
    public void Write_All_MermaidOnly_Fixtures()
    {
        string BaseDir = RecognitionFixturePaths.GetKindFixtureDirectory("MermaidOnlyRecognition");
        WriteFixture(BaseDir, "eventmodeling", """
            eventmodeling
                title Order flow
                cmd PlaceOrder
                evt OrderPlaced
            """);
        WriteFixture(BaseDir, "treeview", """
            treeView-beta
                root
                    childA
                    childB
            """);
        WriteFixture(BaseDir, "ishikawa", """
            ishikawa-beta
                title Defect analysis
                category People
                    cause Training gap
            """);
        WriteFixture(BaseDir, "venn", """
            venn-beta
                title Sets
                A [Label A]
            """);
        WriteFixture(BaseDir, "wardley", """
            wardley-beta
                title Map
                componentA[Component A]
            """);
        WriteFixture(BaseDir, "info", """
            info
                showInfo
            """);
        WriteFixture(BaseDir, "yaml-front-matter", """
            ---
            title: Orphan front matter
            ---
            """);
    }

    static void WriteFixture(string BaseDir, string FixtureName, string Source)
    {
        KindFixtureFiles.Write(BaseDir, FixtureName, Source);
    }
}

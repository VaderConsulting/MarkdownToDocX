namespace MermaidRecognition.Tests.Pie;

/// <summary>
/// Regenerates pie chart verified JSON fixtures.
/// </summary>
[TestFixture]
public sealed class PieRecognitionFixtureWriter
{
    [Test]
    [Explicit("Regenerates baseline fixtures; select this test explicitly to update them.")]
    public void Write_All_Pie_Fixtures()
    {
        string BaseDir = RecognitionFixturePaths.GetPieFixtureDirectory();

        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            pie
                "Cats" : 45
                "Dogs" : 55
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "title-showData",
            """
            pie showData
                title Pets
                "Cats" : 45
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "init-leading",
            """
            %%{init: {"theme": "base"}}%%
            pie
                "Alpha" : 10
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "comment",
            """
            pie
            %% slice note
                "Beta" : 20
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "pie-accessibility",
            """
            pie title a neat chart
                accTitle: a neat acc title
                accDescr: a neat description
                "ash" : 60
                "bat" : 40
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "edge-values",
            """
            pie
                "ash" : 60.67
                "zero" : 0
                "bat" : 40
            """);
    }
}
